using System;
using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.LevelScript
{
    /// <summary>
    /// Executes client-local level scripts (.lks) entirely on the client.
    /// Handles server-to-client events (onServerEvent) and client-to-server sends (sendToServer).
    /// Actions taken here (Camera, UI, local sound, local motion) are isolated to this client only.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelScriptLocalRuntime : MonoBehaviour
    {
        public static LevelScriptLocalRuntime Instance { get; private set; }

        readonly List<Coroutine> runningTasks = new();
        readonly List<EventListenerNode> serverEventListeners = new();
        readonly Dictionary<string, FunctionDefinitionNode> functionRegistry = new(StringComparer.OrdinalIgnoreCase);
        LevelScriptProgramGlobals programGlobals;
        bool scriptActive;

        // Local Camera State
        Transform cameraFollowTarget;
        Coroutine cameraShakeRoutine;
        Coroutine cameraZoomRoutine;
        Coroutine cameraPanRoutine;
        Coroutine cameraFadeRoutine;
        Coroutine bannerRoutine;
        Coroutine timerRoutine;
        GameObject runtimeBannerOverlay;
        GameObject runtimeTimerOverlay;
        GameObject runtimeFadeOverlay;

        public bool IsRunning => scriptActive;

        public static LevelScriptLocalRuntime EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            GameObject go = new GameObject("LevelScriptLocalRuntime");
            DontDestroyOnLoad(go);
            return go.AddComponent<LevelScriptLocalRuntime>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            StopScript();
        }

        public void OnScriptRunningStateChanged(bool isRunning)
        {
            if (isRunning)
            {
                StartLocalScripts();
            }
            else
            {
                StopScript();
            }
        }

        public void StartLocalScripts()
        {
            StopScript();

            if (!WorldScriptProjectService.HasProject)
                return;

            programGlobals = new LevelScriptProgramGlobals();
            functionRegistry.Clear();
            serverEventListeners.Clear();

            IReadOnlyList<ScriptNodeState> nodes = WorldScriptProjectService.GetSortedNodes();
            var localPrograms = new List<LevelScriptProgram>();

            for (int i = 0; i < nodes.Count; i++)
            {
                ScriptNodeState node = nodes[i];
                if (node.IsDirectory || !WorldScriptProjectService.IsLocalScriptPath(node.RelativePath))
                    continue;

                try
                {
                    LevelScriptLinkedProgram linked;
                    LevelScriptProgram program;

                    if (LevelScriptModuleLinker.TryLink(node.RelativePath, out linked, out _))
                    {
                        program = linked.Entry ?? new LevelScriptProgram();
                        foreach (var kvp in linked.Functions)
                            functionRegistry[kvp.Key] = kvp.Value;

                        for (int e = 0; e < linked.EventListeners.Count; e++)
                        {
                            if (linked.EventListeners[e].Target == ScriptEventTarget.ServerEvent)
                                serverEventListeners.Add(linked.EventListeners[e]);
                        }
                    }
                    else
                    {
                        program = LevelScriptParser.Parse(node.Content ?? string.Empty, node.RelativePath);
                        for (int f = 0; f < program.Functions.Count; f++)
                            functionRegistry[program.Functions[f].Name] = program.Functions[f];

                        for (int e = 0; e < program.EventListeners.Count; e++)
                        {
                            if (program.EventListeners[e].Target == ScriptEventTarget.ServerEvent)
                                serverEventListeners.Add(program.EventListeners[e]);
                        }
                    }

                    localPrograms.Add(program);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelScriptLocalRuntime] Failed to load local script '{node.RelativePath}': {ex.Message}");
                }
            }

            if (localPrograms.Count == 0)
                return;

            scriptActive = true;

            for (int i = 0; i < localPrograms.Count; i++)
            {
                LevelScriptProgram prog = localPrograms[i];
                if (prog.MainBody.Count > 0)
                    runningTasks.Add(StartCoroutine(RunBody(prog.MainBody, prog.SourcePath)));

                for (int s = 0; s < prog.Spawns.Count; s++)
                    runningTasks.Add(StartCoroutine(RunBody(prog.Spawns[s].Body, prog.SourcePath)));
            }
        }

        public void StopScript()
        {
            scriptActive = false;

            for (int i = 0; i < runningTasks.Count; i++)
            {
                if (runningTasks[i] != null)
                    StopCoroutine(runningTasks[i]);
            }
            runningTasks.Clear();

            serverEventListeners.Clear();
            functionRegistry.Clear();
            programGlobals?.Clear();
            programGlobals = null;

            cameraFollowTarget = null;
            if (cameraShakeRoutine != null) { StopCoroutine(cameraShakeRoutine); cameraShakeRoutine = null; }
            if (cameraZoomRoutine != null) { StopCoroutine(cameraZoomRoutine); cameraZoomRoutine = null; }
            if (cameraPanRoutine != null) { StopCoroutine(cameraPanRoutine); cameraPanRoutine = null; }
            if (cameraFadeRoutine != null) { StopCoroutine(cameraFadeRoutine); cameraFadeRoutine = null; }
            if (bannerRoutine != null) { StopCoroutine(bannerRoutine); bannerRoutine = null; }
            if (timerRoutine != null) { StopCoroutine(timerRoutine); timerRoutine = null; }

            if (runtimeBannerOverlay != null) { Destroy(runtimeBannerOverlay); runtimeBannerOverlay = null; }
            if (runtimeTimerOverlay != null) { Destroy(runtimeTimerOverlay); runtimeTimerOverlay = null; }
            if (runtimeFadeOverlay != null) { Destroy(runtimeFadeOverlay); runtimeFadeOverlay = null; }
        }

        public void DispatchServerEvent(string eventName, string jsonArgs)
        {
            if (!scriptActive || string.IsNullOrEmpty(eventName))
                return;

            ScriptNetworkEventUtility.TryDeserializePayload(jsonArgs, out _, out ScriptNetworkEventArg[] args);
            args ??= Array.Empty<ScriptNetworkEventArg>();

            for (int i = 0; i < serverEventListeners.Count; i++)
            {
                EventListenerNode listener = serverEventListeners[i];
                if (string.Equals(listener.EventName, eventName, StringComparison.OrdinalIgnoreCase))
                {
                    runningTasks.Add(StartCoroutine(RunServerEventBody(listener, args)));
                }
            }
        }

        IEnumerator RunServerEventBody(EventListenerNode listener, ScriptNetworkEventArg[] args)
        {
            var context = CreateContext(writesProgramGlobals: true);
            context.LocalPlayerRef = GetLocalPlayerRef();

            for (int i = 0; i < listener.ParameterNames.Count; i++)
            {
                string paramName = listener.ParameterNames[i];
                if (i < args.Length)
                {
                    ScriptNetworkEventUtility.BindNetworkArg(context, paramName, args[i]);
                }
            }

            yield return RunBodyNodes(listener.Body, context);
        }

        public ScriptGameObjectRef GetLocalPlayerRef()
        {
            if (NetworkClient.localPlayer != null)
                return new ScriptGameObjectRef(ScriptObjectKind.Player, NetworkClient.localPlayer.netId);

            return ScriptGameObjectRef.Null;
        }

        LevelScriptExecutionContext CreateContext(bool writesProgramGlobals = false, string modulePath = null)
        {
            return new LevelScriptExecutionContext(functionRegistry, programGlobals, null, modulePath ?? string.Empty)
            {
                WritesProgramGlobals = writesProgramGlobals,
                LocalPlayerRef = GetLocalPlayerRef()
            };
        }

        IEnumerator RunBody(List<LevelScriptNode> body, string modulePath)
        {
            var context = CreateContext(writesProgramGlobals: true, modulePath: modulePath);
            yield return RunBodyNodes(body, context);
        }

        IEnumerator RunBodyNodes(List<LevelScriptNode> body, LevelScriptExecutionContext context)
        {
            if (body == null || body.Count == 0)
                yield break;

            for (int i = 0; i < body.Count; i++)
            {
                if (!scriptActive || context.HasReturn || context.HasBreak || context.HasContinue)
                    yield break;

                IEnumerator routine;
                try
                {
                    routine = ExecuteNode(body[i], context);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelScriptLocalRuntime] Error executing local node: {ex.Message}");
                    yield break;
                }

                if (routine != null)
                {
                    while (routine.MoveNext())
                    {
                        if (!scriptActive || context.HasReturn || context.HasBreak || context.HasContinue)
                            yield break;

                        yield return routine.Current;
                    }
                }
            }
        }

        IEnumerator ExecuteNode(LevelScriptNode node, LevelScriptExecutionContext context)
        {
            switch (node)
            {
                case SpawnNode spawn:
                    LevelScriptExecutionContext spawnContext = context.Fork();
                    runningTasks.Add(StartCoroutine(RunBodyNodes(spawn.Body, spawnContext)));
                    yield break;

                case ReturnNode ret:
                    context.HasReturn = true;
                    if (ret.IntValue != null) context.ReturnValue = ret.IntValue.Evaluate(context);
                    else if (ret.FloatValue != null) context.ReturnValue = ret.FloatValue.Evaluate(context);
                    else if (ret.BoolValue != null) context.ReturnValue = ret.BoolValue.Evaluate(context);
                    else if (ret.StringValue != null) context.ReturnValue = ret.StringValue.Evaluate(context);
                    else context.ReturnValue = null;
                    yield break;

                case BreakNode:
                    context.HasBreak = true;
                    yield break;

                case ContinueNode:
                    context.HasContinue = true;
                    yield break;

                case WhileNode whileNode:
                    while (scriptActive && whileNode.Condition != null && whileNode.Condition.Evaluate(context))
                    {
                        context.PushScope();
                        yield return RunBodyNodes(whileNode.Body, context);
                        context.PopScope();

                        if (context.HasReturn || context.HasBreak)
                        {
                            context.HasBreak = false;
                            break;
                        }
                        context.HasContinue = false;
                        yield return null;
                    }
                    yield break;

                case ForNode forNode:
                    if (forNode.Init != null)
                        yield return ExecuteNode(forNode.Init, context);

                    while (scriptActive && (forNode.Condition == null || forNode.Condition.Evaluate(context)))
                    {
                        context.PushScope();
                        yield return RunBodyNodes(forNode.Body, context);
                        context.PopScope();

                        if (context.HasReturn || context.HasBreak)
                        {
                            context.HasBreak = false;
                            break;
                        }
                        context.HasContinue = false;

                        if (forNode.Increment != null)
                            yield return ExecuteNode(forNode.Increment, context);

                        yield return null;
                    }
                    yield break;

                case IfNode ifNode:
                    bool executed = false;
                    if (ifNode.Condition != null && ifNode.Condition.Evaluate(context))
                    {
                        context.PushScope();
                        yield return RunBodyNodes(ifNode.ThenBody, context);
                        context.PopScope();
                        executed = true;
                    }
                    else if (ifNode.ElseIfClauses.Count > 0)
                    {
                        for (int e = 0; e < ifNode.ElseIfClauses.Count; e++)
                        {
                            if (ifNode.ElseIfClauses[e].Condition.Evaluate(context))
                            {
                                context.PushScope();
                                yield return RunBodyNodes(ifNode.ElseIfClauses[e].Body, context);
                                context.PopScope();
                                executed = true;
                                break;
                            }
                        }
                    }

                    if (!executed && ifNode.ElseBody.Count > 0)
                    {
                        context.PushScope();
                        yield return RunBodyNodes(ifNode.ElseBody, context);
                        context.PopScope();
                    }
                    yield break;

                case WaitNode wait:
                    float duration = wait.Duration != null ? wait.Duration.Evaluate(context) : 0f;
                    if (duration > 0f)
                        yield return new WaitForSeconds(duration);
                    yield break;

                case SendToServerNode sendToServer:
                {
                    string evtName = sendToServer.EventName != null ? sendToServer.EventName.Evaluate(context) : string.Empty;
                    var args = new ScriptNetworkEventArg[sendToServer.Arguments.Count];
                    for (int a = 0; a < args.Length; a++)
                        args[a] = ScriptNetworkEventUtility.EvaluateArgument(sendToServer.Arguments[a], context);
                    string json = ScriptNetworkEventUtility.SerializePayload(evtName, args);

                    KaplaoCursorClient cursor = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
                    if (cursor != null)
                    {
                        KaplaoCursorClient.EnsureClientReadyForCommands();
                        cursor.CmdSendScriptEventToServer(evtName, json);
                    }
                    else if (LevelScriptRuntime.Instance != null && LevelScriptRuntime.Instance.IsRunning)
                    {
                        var localRef = GetLocalPlayerRef();
                        LevelScriptRuntime.Instance.DispatchClientEvent(evtName, localRef, json);
                    }
                    yield break;
                }

                case PrintNode printNode:
                    ExecutePrint(printNode, context);
                    yield break;

                case IntDeclarationNode decl:
                    context.DeclareInt(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : 0);
                    yield break;

                case BoolDeclarationNode decl:
                    context.DeclareBool(decl.Name, decl.Value != null && decl.Value.Evaluate(context));
                    yield break;

                case FloatDeclarationNode decl:
                    context.DeclareFloat(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : 0f);
                    yield break;

                case StringDeclarationNode decl:
                    context.DeclareString(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : string.Empty);
                    yield break;

                case GameObjectDeclarationNode decl:
                    context.DeclareGameObject(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : ScriptGameObjectRef.Null);
                    yield break;

                case ListDeclarationNode decl:
                    var initList = new List<ScriptValue>();
                    for (int li = 0; li < decl.InitialItems.Count; li++)
                        initList.Add(ScriptNetworkEventUtility.EvaluateToScriptValue(decl.InitialItems[li], context));
                    context.DeclareList(decl.Name, initList);
                    yield break;

                case ListAddNode listAdd:
                    if (context.TryGetList(listAdd.ListName, out var targetAddList))
                        targetAddList.Add(ScriptNetworkEventUtility.EvaluateToScriptValue(listAdd.Item, context));
                    yield break;

                case ListRemoveAtNode listRemove:
                    if (context.TryGetList(listRemove.ListName, out var targetRemList))
                    {
                        int remIdx = listRemove.Index != null ? listRemove.Index.Evaluate(context) : -1;
                        if (remIdx >= 0 && remIdx < targetRemList.Count)
                            targetRemList.RemoveAt(remIdx);
                    }
                    yield break;

                case ListClearNode listClear:
                    if (context.TryGetList(listClear.ListName, out var targetClrList))
                        targetClrList.Clear();
                    yield break;

                case ListSetItemNode listSetItem:
                    if (context.TryGetList(listSetItem.ListName, out var targetSetList))
                    {
                        int setIdx = listSetItem.Index != null ? listSetItem.Index.Evaluate(context) : -1;
                        if (setIdx >= 0 && setIdx < targetSetList.Count)
                            targetSetList[setIdx] = ScriptNetworkEventUtility.EvaluateToScriptValue(listSetItem.Value, context);
                    }
                    yield break;

                case IntAssignmentNode assign:
                    context.SetInt(assign.Name, assign.Value.Evaluate(context));
                    yield break;

                case BoolAssignmentNode assign:
                    context.SetBool(assign.Name, assign.Value.Evaluate(context));
                    yield break;

                case FloatAssignmentNode assign:
                    context.SetFloat(assign.Name, assign.Value.Evaluate(context));
                    yield break;

                case StringAssignmentNode assign:
                    context.SetString(assign.Name, assign.Value.Evaluate(context));
                    yield break;

                case FunctionCallNode call:
                    if (functionRegistry.TryGetValue(call.Name, out FunctionDefinitionNode def))
                    {
                        if (context.CallStackDepth >= LevelScriptExecutionContext.MaxCallStackDepth)
                        {
                            Debug.LogError($"[LevelScriptLocalRuntime] Stack overflow: function call depth exceeded {LevelScriptExecutionContext.MaxCallStackDepth} in '{call.Name}'.");
                            yield break;
                        }
                        LevelScriptExecutionContext fnContext = CreateContext(false, def.SourcePath);
                        fnContext.CallStackDepth = context.CallStackDepth + 1;
                        fnContext.PushScope();
                        for (int p = 0; p < def.Parameters.Count; p++)
                        {
                            string pName = def.Parameters[p].Name;
                            if (p < call.Arguments.Count)
                            {
                                ScriptArgument arg = call.Arguments[p];
                                switch (def.Parameters[p].Kind)
                                {
                                    case ScriptValueKind.Int: fnContext.DeclareInt(pName, arg.IntValue != null ? arg.IntValue.Evaluate(context) : 0); break;
                                    case ScriptValueKind.Float: fnContext.DeclareFloat(pName, arg.FloatValue != null ? arg.FloatValue.Evaluate(context) : 0f); break;
                                    case ScriptValueKind.Bool: fnContext.DeclareBool(pName, arg.BoolValue != null && arg.BoolValue.Evaluate(context)); break;
                                    case ScriptValueKind.String: fnContext.DeclareString(pName, arg.StringValue != null ? arg.StringValue.Evaluate(context) : string.Empty); break;
                                    case ScriptValueKind.GameObject: fnContext.DeclareGameObject(pName, arg.GameObjectValue != null ? arg.GameObjectValue.Evaluate(context) : ScriptGameObjectRef.Null); break;
                                }
                            }
                        }
                        yield return RunBodyNodes(def.Body, fnContext);
                        fnContext.PopScope();
                        if (fnContext.HasReturn)
                            context.ReturnValue = fnContext.ReturnValue;
                    }
                    yield break;

                case PlaySoundNode playSound:
                {
                    float x = playSound.X != null ? playSound.X.Evaluate(context) : 0f;
                    float y = playSound.Y != null ? playSound.Y.Evaluate(context) : 0f;
                    string sound = playSound.SoundName != null ? playSound.SoundName.Evaluate(context) : string.Empty;
                    float vol = playSound.Volume != null ? playSound.Volume.Evaluate(context) : 1f;

                    if (!string.IsNullOrEmpty(sound))
                    {
                        LevelSoundPlayer.PlayAt(x, y, sound, vol);
                    }
                    yield break;
                }

                case CameraNode camNode:
                    ExecuteLocalCameraNode(camNode, context);
                    yield break;

                case UINode uiNode:
                    ExecuteLocalUiNode(uiNode, context);
                    yield break;

                case BackgroundNode bgNode:
                    LevelScriptObjectUtility.ApplyBackgroundAction(
                        bgNode.Kind,
                        bgNode.LayerIndex != null ? bgNode.LayerIndex.Evaluate(context) : 0,
                        bgNode.HexColor != null ? bgNode.HexColor.Evaluate(context) : null,
                        bgNode.Value != null ? bgNode.Value.Evaluate(context) : 0f);
                    yield break;

                case HitboxConfigNode hitboxConfig:
                    LevelScriptObjectUtility.ApplyHitboxConfig(
                        hitboxConfig.Kind,
                        hitboxConfig.Targets.Evaluate(context),
                        hitboxConfig.X != null ? hitboxConfig.X.Evaluate(context) : 0f,
                        hitboxConfig.Y != null ? hitboxConfig.Y.Evaluate(context) : 0f,
                        hitboxConfig.BoolValue != null ? hitboxConfig.BoolValue.Evaluate(context) : false);
                    yield break;

                case SpriteActionNode spriteAction:
                    LevelScriptObjectUtility.ApplySpriteAction(
                        spriteAction.Kind,
                        spriteAction.Targets.Evaluate(context),
                        spriteAction.FlipX != null ? spriteAction.FlipX.Evaluate(context) : false,
                        spriteAction.FlipY != null ? spriteAction.FlipY.Evaluate(context) : false,
                        spriteAction.SortingOrder != null ? spriteAction.SortingOrder.Evaluate(context) : 0,
                        spriteAction.Alpha != null ? spriteAction.Alpha.Evaluate(context) : 1f,
                        spriteAction.HexColor != null ? spriteAction.HexColor.Evaluate(context) : null);
                    yield break;

                case MoveNode move:
                    ExecuteLocalMove(move, context);
                    yield break;
            }
        }

        void ExecutePrint(PrintNode printNode, LevelScriptExecutionContext context)
        {
            string formatted = BuildPrintString(printNode, context);
            Debug.Log($"[LevelScriptLocal] {formatted}");
            KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput(formatted, KaplaoGames.Client.OutputLogType.Info);
        }

        static string BuildPrintString(PrintNode printNode, LevelScriptExecutionContext context)
        {
            if (printNode.Arguments == null || printNode.Arguments.Count == 0)
                return string.Empty;

            var parts = new List<string>(printNode.Arguments.Count);
            for (int i = 0; i < printNode.Arguments.Count; i++)
            {
                ScriptValue val = ScriptNetworkEventUtility.EvaluateToScriptValue(printNode.Arguments[i], context);
                parts.Add(ScriptNetworkEventUtility.FormatScriptValue(val));
            }

            return string.Join(" ", parts);
        }

        void ExecuteLocalMove(MoveNode move, LevelScriptExecutionContext context)
        {
            if (move == null || move.Targets == null) return;
            List<ScriptGameObjectRef> targets = move.Targets.Evaluate(context);
            float dx = move.DeltaX != null ? move.DeltaX.Evaluate(context) : 0f;
            float dy = move.DeltaY != null ? move.DeltaY.Evaluate(context) : 0f;
            float dur = move.Duration != null ? move.Duration.Evaluate(context) : 0f;
            string ease = move.Ease != null ? move.Ease.Evaluate(context) : "linear";

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    StartCoroutine(AnimateLocalTransform(tr, tr.position + new Vector3(dx, dy, 0f), dur, ease));
                }
            }
        }

        IEnumerator AnimateLocalTransform(Transform tr, Vector3 targetPos, float duration, string ease)
        {
            if (tr == null) yield break;
            if (duration <= 0.001f)
            {
                tr.position = targetPos;
                yield break;
            }

            LevelEase parsedEase = LevelEase.Linear;
            LevelEaseUtility.TryParse(ease, out parsedEase);
            Vector3 startPos = tr.position;
            float elapsed = 0f;

            while (elapsed < duration && tr != null && scriptActive)
            {
                elapsed += Time.deltaTime;
                float t = LevelEaseUtility.Evaluate(parsedEase, Mathf.Clamp01(elapsed / duration));
                tr.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            if (tr != null)
                tr.position = targetPos;
        }

        void ExecuteLocalCameraNode(CameraNode node, LevelScriptExecutionContext context)
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) return;

            switch (node.Kind)
            {
                case CameraActionKind.Shake:
                {
                    float intensity = node.Intensity != null ? node.Intensity.Evaluate(context) : 0.5f;
                    float duration = node.Duration != null ? node.Duration.Evaluate(context) : 0.5f;
                    if (cameraShakeRoutine != null) StopCoroutine(cameraShakeRoutine);
                    cameraShakeRoutine = StartCoroutine(ShakeCameraRoutine(intensity, duration));
                    break;
                }
                case CameraActionKind.Zoom:
                {
                    float targetSize = node.Intensity != null ? node.Intensity.Evaluate(context) : 8f;
                    float duration = node.Duration != null ? node.Duration.Evaluate(context) : 0f;
                    string ease = node.Ease != null ? node.Ease.Evaluate(context) : "linear";
                    if (cameraZoomRoutine != null) StopCoroutine(cameraZoomRoutine);
                    cameraZoomRoutine = StartCoroutine(ZoomCameraRoutine(targetSize, duration, ease));
                    break;
                }
                case CameraActionKind.PanTo:
                {
                    float targetX = node.Intensity != null ? node.Intensity.Evaluate(context) : cam.transform.position.x;
                    float targetY = node.Duration != null ? node.Duration.Evaluate(context) : cam.transform.position.y;
                    float duration = node.ExtraDuration != null ? node.ExtraDuration.Evaluate(context) : 0f;
                    string ease = node.Ease != null ? node.Ease.Evaluate(context) : "linear";
                    if (cameraPanRoutine != null) StopCoroutine(cameraPanRoutine);
                    cameraPanRoutine = StartCoroutine(PanCameraRoutine(targetX, targetY, duration, ease));
                    break;
                }
                case CameraActionKind.Follow:
                {
                    if (node.Target != null)
                    {
                        ScriptGameObjectRef targetRef = node.Target.Evaluate(context);
                        if (LevelScriptObjectUtility.TryResolveTransform(targetRef, out Transform tr))
                            cameraFollowTarget = tr;
                    }
                    break;
                }
                case CameraActionKind.Unfollow:
                    cameraFollowTarget = null;
                    break;
                case CameraActionKind.Fade:
                {
                    float targetAlpha = node.Intensity != null ? node.Intensity.Evaluate(context) : 1f;
                    float duration = node.Duration != null ? node.Duration.Evaluate(context) : 0.5f;
                    string hexColor = node.HexColor != null ? node.HexColor.Evaluate(context) : "#000000";
                    if (cameraFadeRoutine != null) StopCoroutine(cameraFadeRoutine);
                    cameraFadeRoutine = StartCoroutine(FadeCameraRoutine(targetAlpha, duration, hexColor));
                    break;
                }
            }
        }

        void LateUpdate()
        {
            if (scriptActive && cameraFollowTarget != null)
            {
                Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    Vector3 targetPos = cameraFollowTarget.position;
                    cam.transform.position = new Vector3(targetPos.x, targetPos.y, cam.transform.position.z);
                }
            }
        }

        IEnumerator ShakeCameraRoutine(float intensity, float duration)
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null || duration <= 0f) yield break;
            Vector3 originalPos = cam.transform.position;
            float elapsed = 0f;
            while (elapsed < duration && scriptActive)
            {
                elapsed += Time.deltaTime;
                Vector2 offset = UnityEngine.Random.insideUnitCircle * intensity;
                cam.transform.position = new Vector3(originalPos.x + offset.x, originalPos.y + offset.y, originalPos.z);
                yield return null;
            }
            cam.transform.position = originalPos;
            cameraShakeRoutine = null;
        }

        IEnumerator ZoomCameraRoutine(float targetSize, float duration, string ease)
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) yield break;
            if (duration <= 0.001f)
            {
                cam.orthographicSize = Mathf.Max(0.5f, targetSize);
                yield break;
            }
            LevelEase parsedEase = LevelEase.Linear;
            LevelEaseUtility.TryParse(ease, out parsedEase);
            float startSize = cam.orthographicSize;
            float elapsed = 0f;
            while (elapsed < duration && scriptActive)
            {
                elapsed += Time.deltaTime;
                float t = LevelEaseUtility.Evaluate(parsedEase, Mathf.Clamp01(elapsed / duration));
                cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
                yield return null;
            }
            cam.orthographicSize = targetSize;
            cameraZoomRoutine = null;
        }

        IEnumerator PanCameraRoutine(float targetX, float targetY, float duration, string ease)
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) yield break;
            Vector3 startPos = cam.transform.position;
            Vector3 endPos = new Vector3(targetX, targetY, startPos.z);
            if (duration <= 0.001f)
            {
                cam.transform.position = endPos;
                yield break;
            }
            LevelEase parsedEase = LevelEase.Linear;
            LevelEaseUtility.TryParse(ease, out parsedEase);
            float elapsed = 0f;
            while (elapsed < duration && scriptActive)
            {
                elapsed += Time.deltaTime;
                float t = LevelEaseUtility.Evaluate(parsedEase, Mathf.Clamp01(elapsed / duration));
                cam.transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
            cam.transform.position = endPos;
            cameraPanRoutine = null;
        }

        Canvas EnsureRuntimeCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) return canvas;
            GameObject go = new GameObject("LevelScriptLocalCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        IEnumerator FadeCameraRoutine(float targetAlpha, float duration, string hexColor)
        {
            Canvas canvas = EnsureRuntimeCanvas();
            if (canvas == null) yield break;

            if (runtimeFadeOverlay == null)
            {
                runtimeFadeOverlay = new GameObject("RuntimeLocalFadeOverlay");
                runtimeFadeOverlay.transform.SetParent(canvas.transform, false);
                RectTransform rt = runtimeFadeOverlay.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var img = runtimeFadeOverlay.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
            }

            var image = runtimeFadeOverlay.GetComponent<Image>();
            Color col = Color.black;
            if (!string.IsNullOrEmpty(hexColor))
                ColorUtility.TryParseHtmlString(hexColor.StartsWith("#") ? hexColor : "#" + hexColor, out col);

            Color startColor = image.color;
            Color endColor = new Color(col.r, col.g, col.b, Mathf.Clamp01(targetAlpha));

            if (duration <= 0.001f)
            {
                image.color = endColor;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && scriptActive)
            {
                elapsed += Time.deltaTime;
                image.color = Color.Lerp(startColor, endColor, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            image.color = endColor;
            cameraFadeRoutine = null;
        }

        void ExecuteLocalUiNode(UINode node, LevelScriptExecutionContext context)
        {
            switch (node.Kind)
            {
                case UIActionKind.ShowBanner:
                {
                    string text = node.Text != null ? node.Text.Evaluate(context) : "";
                    float duration = node.Duration != null ? node.Duration.Evaluate(context) : 3f;
                    string hexColor = node.HexColor != null ? node.HexColor.Evaluate(context) : "#FFFFFF";
                    if (bannerRoutine != null) StopCoroutine(bannerRoutine);
                    bannerRoutine = StartCoroutine(ShowBannerRoutine(text, duration, hexColor));
                    break;
                }
                case UIActionKind.ShowTimer:
                {
                    float seconds = node.Duration != null ? node.Duration.Evaluate(context) : 10f;
                    string label = node.Text != null ? node.Text.Evaluate(context) : "";
                    if (timerRoutine != null) StopCoroutine(timerRoutine);
                    timerRoutine = StartCoroutine(ShowTimerRoutine(seconds, label));
                    break;
                }
            }
        }

        IEnumerator ShowBannerRoutine(string text, float duration, string hexColor)
        {
            Canvas canvas = EnsureRuntimeCanvas();
            if (canvas == null) yield break;

            if (runtimeBannerOverlay != null)
                Destroy(runtimeBannerOverlay);

            GameObject bannerObj = new GameObject("RuntimeLocalBanner");
            runtimeBannerOverlay = bannerObj;
            bannerObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = bannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.85f);
            rt.anchorMax = new Vector2(0.85f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = bannerObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            GameObject textObj = new GameObject("BannerText");
            textObj.transform.SetParent(bannerObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 24;

            Color col = Color.white;
            if (!string.IsNullOrEmpty(hexColor))
                ColorUtility.TryParseHtmlString(hexColor.StartsWith("#") ? hexColor : "#" + hexColor, out col);
            tmp.color = col;
            tmp.text = text ?? "";

            yield return new WaitForSeconds(duration);

            if (runtimeBannerOverlay != null)
                Destroy(runtimeBannerOverlay);
            bannerRoutine = null;
        }

        IEnumerator ShowTimerRoutine(float seconds, string label)
        {
            Canvas canvas = EnsureRuntimeCanvas();
            if (canvas == null) yield break;

            if (runtimeTimerOverlay != null)
                Destroy(runtimeTimerOverlay);

            GameObject timerObj = new GameObject("RuntimeLocalTimer");
            runtimeTimerOverlay = timerObj;
            timerObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = timerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.4f, 0.92f);
            rt.anchorMax = new Vector2(0.6f, 0.98f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = timerObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject textObj = new GameObject("TimerText");
            textObj.transform.SetParent(timerObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 20;
            tmp.color = Color.yellow;

            float remaining = seconds;
            while (remaining > 0f && scriptActive)
            {
                if (tmp != null)
                    tmp.text = string.IsNullOrEmpty(label)
                        ? $"{Mathf.CeilToInt(remaining)}s"
                        : $"{label}: {Mathf.CeilToInt(remaining)}s";

                yield return null;
                remaining -= Time.deltaTime;
            }

            if (runtimeTimerOverlay != null)
                Destroy(runtimeTimerOverlay);
            timerRoutine = null;
        }
    }
}
