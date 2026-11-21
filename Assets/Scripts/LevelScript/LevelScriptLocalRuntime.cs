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
        Vector3 initialCamPos;
        float initialCamOrthoSize;
        bool initialCamRecorded;
        Coroutine cameraShakeRoutine;
        Coroutine cameraZoomRoutine;
        Coroutine cameraPanRoutine;
        Coroutine cameraFadeRoutine;
        Coroutine bannerRoutine;
        Coroutine timerRoutine;
        GameObject runtimeBannerOverlay;
        GameObject runtimeTimerOverlay;
        GameObject runtimeFadeOverlay;

        // State tracking for rollback on Stop
        readonly Dictionary<Transform, Vector3> initialPositions = new();
        readonly Dictionary<Transform, Quaternion> initialRotations = new();
        readonly Dictionary<Transform, Vector3> initialScales = new();
        readonly Dictionary<Renderer, bool> initialRendererVisibility = new();
        readonly Dictionary<SpriteRenderer, Color> initialSpriteColors = new();
        readonly Dictionary<Collider2D, bool> initialColliderStates = new();

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
                        LevelScriptSemanticAnalyzer.Analyze(linked, node.Content ?? string.Empty);
                        program = linked.Entry ?? new LevelScriptProgram();
                        foreach (var kvp in linked.Functions)
                            functionRegistry[kvp.Key] = kvp.Value;

                        for (int e = 0; e < linked.EventListeners.Count; e++)
                        {
                            if (linked.EventListeners[e].Target == ScriptEventTarget.ServerEvent)
                                serverEventListeners.Add(linked.EventListeners[e]);
                        }

                        // Initialize global declarations from imported modules
                        if (linked.InitDeclarations.Count > 0)
                        {
                            var initCtx = CreateContext(writesProgramGlobals: true, modulePath: node.RelativePath);
                            for (int d = 0; d < linked.InitDeclarations.Count; d++)
                            {
                                var initRoutine = ExecuteNode(linked.InitDeclarations[d], initCtx);
                                while (initRoutine != null && initRoutine.MoveNext()) { }
                            }
                        }
                    }
                    else
                    {
                        program = LevelScriptParser.Parse(node.Content ?? string.Empty, node.RelativePath);
                        linked = new LevelScriptLinkedProgram { Entry = program };
                        linked.ModulesInInitOrder.Add(program);
                        for (int f = 0; f < program.Functions.Count; f++)
                        {
                            var fn = program.Functions[f];
                            linked.Functions[fn.Name] = fn;
                            linked.Symbols[fn.Name] = new LevelScriptSymbolInfo
                            {
                                Name = fn.Name,
                                ModulePath = program.SourcePath,
                                IsPublic = fn.IsPublic,
                                Kind = LevelScriptSymbolKind.Function
                            };
                        }
                        LevelScriptSemanticAnalyzer.Analyze(linked, node.Content ?? string.Empty);

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
                    KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput($"[{node.RelativePath}] {ex.Message}", KaplaoGames.Client.OutputLogType.Error);
                }
            }

            if (localPrograms.Count == 0)
                return;

            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                initialCamPos = cam.transform.position;
                initialCamOrthoSize = cam.orthographicSize;
                initialCamRecorded = true;
            }

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

            // Rollback visual & transform states
            foreach (var kvp in initialPositions)
            {
                if (kvp.Key != null) kvp.Key.position = kvp.Value;
            }
            initialPositions.Clear();

            foreach (var kvp in initialRotations)
            {
                if (kvp.Key != null) kvp.Key.rotation = kvp.Value;
            }
            initialRotations.Clear();

            foreach (var kvp in initialScales)
            {
                if (kvp.Key != null) kvp.Key.localScale = kvp.Value;
            }
            initialScales.Clear();

            foreach (var kvp in initialRendererVisibility)
            {
                if (kvp.Key != null) kvp.Key.enabled = kvp.Value;
            }
            initialRendererVisibility.Clear();

            foreach (var kvp in initialSpriteColors)
            {
                if (kvp.Key != null) kvp.Key.color = kvp.Value;
            }
            initialSpriteColors.Clear();

            foreach (var kvp in initialColliderStates)
            {
                if (kvp.Key != null) kvp.Key.enabled = kvp.Value;
            }
            initialColliderStates.Clear();
            LevelScriptObjectUtility.ClearSceneObjectRegistry();

            if (initialCamRecorded)
            {
                Camera activeCam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (activeCam != null)
                {
                    activeCam.transform.position = initialCamPos;
                    activeCam.orthographicSize = initialCamOrthoSize;
                }
            }

            KaplaoGames.Client.ParallaxBackground.Instance?.ResetBackground();
        }

        public void DispatchServerEvent(string eventName, string jsonArgs)
        {
            if (!scriptActive || string.IsNullOrEmpty(eventName))
                return;

            if (!ScriptNetworkEventUtility.TryDeserializePayload(jsonArgs, out _, out ScriptNetworkEventArg[] args))
            {
                args = Array.Empty<ScriptNetworkEventArg>();
            }

            for (int i = 0; i < serverEventListeners.Count; i++)
            {
                EventListenerNode listener = serverEventListeners[i];
                if (string.Equals(listener.EventName, eventName, StringComparison.OrdinalIgnoreCase))
                {
                    runningTasks.Add(StartCoroutine(RunServerEventListener(listener, args)));
                }
            }
        }

        IEnumerator RunServerEventListener(EventListenerNode listener, ScriptNetworkEventArg[] args)
        {
            LevelScriptExecutionContext context = CreateContext(writesProgramGlobals: false, modulePath: listener.SourcePath);
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

        void ReportScriptError(string message, string modulePath = null)
        {
            string formatted = !string.IsNullOrEmpty(modulePath) ? $"[{modulePath}] {message}" : message;
            Debug.LogError($"[LevelScriptLocalRuntime] {formatted}");
            KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput(formatted, KaplaoGames.Client.OutputLogType.Error);
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
                    ReportScriptError($"Runtime error: {ex.Message}", context.CurrentModulePath);
                    yield break;
                }

                if (routine != null)
                {
                    while (true)
                    {
                        bool hasMore;
                        try
                        {
                            hasMore = routine.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            ReportScriptError($"Runtime error: {ex.Message}", context.CurrentModulePath);
                            yield break;
                        }

                        if (!hasMore)
                            break;

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
                    if (ret.Value != null)
                    {
                        ScriptValue v = ScriptNetworkEventUtility.EvaluateToScriptValue(ret.Value, context);
                        switch (v.Kind)
                        {
                            case ScriptValueKind.Int: context.ReturnValue = v.IntValue; break;
                            case ScriptValueKind.Float: context.ReturnValue = v.FloatValue; break;
                            case ScriptValueKind.Bool: context.ReturnValue = v.BoolValue; break;
                            case ScriptValueKind.String: context.ReturnValue = v.StringValue; break;
                            case ScriptValueKind.GameObject: context.ReturnValue = v.GameObjectValue; break;
                            case ScriptValueKind.Component: context.ReturnValue = v.ComponentValue; break;
                            case ScriptValueKind.List: context.ReturnValue = v.ListValue; break;
                            default: context.ReturnValue = null; break;
                        }
                    }
                    else if (ret.IntValue != null) context.ReturnValue = ret.IntValue.Evaluate(context);
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
                    int whileIterations = 0;
                    while (scriptActive && whileNode.Condition != null && whileNode.Condition.Evaluate(context))
                    {
                        whileIterations++;
                        if (whileIterations % 5000 == 0)
                            yield return null; // Anti-freeze watchdog
                        if (whileIterations > 200000)
                        {
                            ReportScriptError("Infinite loop detected: while loop exceeded 200,000 iterations without waiting.", context.CurrentModulePath);
                            yield break;
                        }

                        context.PushScope();
                        yield return RunBodyNodes(whileNode.Body, context);
                        context.PopScope();

                        if (context.HasReturn || context.HasBreak)
                        {
                            context.HasBreak = false;
                            break;
                        }
                        if (context.HasContinue)
                        {
                            context.HasContinue = false;
                            continue;
                        }
                    }
                    yield break;

                case ForNode forNode:
                    bool previousWritesGlobals = context.WritesProgramGlobals;
                    context.WritesProgramGlobals = false;
                    context.PushScope();
                    try
                    {
                        if (forNode.Init != null)
                            yield return ExecuteNode(forNode.Init, context);

                        int forIterations = 0;
                        while (scriptActive && (forNode.Condition == null || forNode.Condition.Evaluate(context)))
                        {
                            forIterations++;
                            if (forIterations % 5000 == 0)
                                yield return null; // Anti-freeze watchdog
                            if (forIterations > 200000)
                            {
                                ReportScriptError("Infinite loop detected: for loop exceeded 200,000 iterations without waiting.", context.CurrentModulePath);
                                yield break;
                            }

                            context.PushScope();
                            yield return RunBodyNodes(forNode.Body, context);
                            context.PopScope();

                            if (context.HasReturn || context.HasBreak)
                            {
                                context.HasBreak = false;
                                break;
                            }
                            if (context.HasContinue)
                            {
                                context.HasContinue = false;
                            }

                            if (forNode.Increment != null)
                                yield return ExecuteNode(forNode.Increment, context);
                        }
                    }
                    finally
                    {
                        context.PopScope();
                        context.WritesProgramGlobals = previousWritesGlobals;
                    }
                    yield break;

                case IfNode ifNode:
                    bool executed = false;
                    if (ifNode.Condition != null && ifNode.Condition.Evaluate(context))
                    {
                        context.PushScope();
                        try
                        {
                            yield return RunBodyNodes(ifNode.ThenBody, context);
                        }
                        finally
                        {
                            context.PopScope();
                        }
                        executed = true;
                    }
                    else if (ifNode.ElseIfClauses.Count > 0)
                    {
                        for (int e = 0; e < ifNode.ElseIfClauses.Count; e++)
                        {
                            if (ifNode.ElseIfClauses[e].Condition.Evaluate(context))
                            {
                                context.PushScope();
                                try
                                {
                                    yield return RunBodyNodes(ifNode.ElseIfClauses[e].Body, context);
                                }
                                finally
                                {
                                    context.PopScope();
                                }
                                executed = true;
                                break;
                            }
                        }
                    }

                    if (!executed && ifNode.ElseBody != null && ifNode.ElseBody.Count > 0)
                    {
                        context.PushScope();
                        try
                        {
                            yield return RunBodyNodes(ifNode.ElseBody, context);
                        }
                        finally
                        {
                            context.PopScope();
                        }
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

                    if (NetworkClient.active && NetworkClient.localPlayer != null &&
                        NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer netPlayer))
                    {
                        netPlayer.CmdSendScriptEventToServer(evtName, json);
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
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        int val = context.ReturnValue switch
                        {
                            int iv => iv,
                            float fv => (int)fv,
                            _ => 0
                        };
                        context.DeclareInt(decl.Name, val);
                    }
                    else
                    {
                        context.DeclareInt(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : 0);
                    }
                    yield break;

                case BoolDeclarationNode decl:
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        bool val = context.ReturnValue is bool bv && bv;
                        context.DeclareBool(decl.Name, val);
                    }
                    else
                    {
                        context.DeclareBool(decl.Name, decl.Value != null && decl.Value.Evaluate(context));
                    }
                    yield break;

                case FloatDeclarationNode decl:
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        float val = context.ReturnValue switch
                        {
                            float fv => fv,
                            int iv => (float)iv,
                            _ => 0f
                        };
                        context.DeclareFloat(decl.Name, val);
                    }
                    else
                    {
                        context.DeclareFloat(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : 0f);
                    }
                    yield break;

                case StringDeclarationNode decl:
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        string val = context.ReturnValue != null ? context.ReturnValue.ToString() : string.Empty;
                        context.DeclareString(decl.Name, val);
                    }
                    else
                    {
                        context.DeclareString(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : string.Empty);
                    }
                    yield break;

                case GameObjectDeclarationNode decl:
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        ScriptGameObjectRef val = context.ReturnValue is ScriptGameObjectRef gr ? gr : ScriptGameObjectRef.Null;
                        context.DeclareGameObject(decl.Name, val);
                    }
                    else
                    {
                        context.DeclareGameObject(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : ScriptGameObjectRef.Null);
                    }
                    yield break;

                case ComponentDeclarationNode decl:
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        ScriptComponentRef val = context.ReturnValue is ScriptComponentRef cr ? cr : ScriptComponentRef.Null;
                        context.DeclareComponent(decl.Name, val);
                    }
                    else
                    {
                        context.DeclareComponent(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : ScriptComponentRef.Null);
                    }
                    yield break;

                case ListDeclarationNode decl:
                    if (decl.CallValue != null)
                    {
                        yield return ExecuteNode(decl.CallValue, context);
                        List<ScriptValue> listVal = context.ReturnValue is List<ScriptValue> lv ? lv : new List<ScriptValue>();
                        context.DeclareList(decl.Name, listVal);
                    }
                    else
                    {
                        var initList = new List<ScriptValue>();
                        for (int li = 0; li < decl.InitialItems.Count; li++)
                            initList.Add(ScriptNetworkEventUtility.EvaluateToScriptValue(decl.InitialItems[li], context));
                        context.DeclareList(decl.Name, initList);
                    }
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
                    if (assign.CallValue != null)
                    {
                        yield return ExecuteNode(assign.CallValue, context);
                        int val = context.ReturnValue switch
                        {
                            int iv => iv,
                            float fv => (int)fv,
                            _ => 0
                        };
                        context.SetInt(assign.Name, val);
                    }
                    else
                    {
                        context.SetInt(assign.Name, assign.Value.Evaluate(context));
                    }
                    yield break;

                case BoolAssignmentNode assign:
                    if (assign.CallValue != null)
                    {
                        yield return ExecuteNode(assign.CallValue, context);
                        bool val = context.ReturnValue is bool bv && bv;
                        context.SetBool(assign.Name, val);
                    }
                    else
                    {
                        context.SetBool(assign.Name, assign.Value.Evaluate(context));
                    }
                    yield break;

                case FloatAssignmentNode assign:
                    if (assign.CallValue != null)
                    {
                        yield return ExecuteNode(assign.CallValue, context);
                        float val = context.ReturnValue switch
                        {
                            float fv => fv,
                            int iv => (float)iv,
                            _ => 0f
                        };
                        context.SetFloat(assign.Name, val);
                    }
                    else
                    {
                        context.SetFloat(assign.Name, assign.Value.Evaluate(context));
                    }
                    yield break;

                case StringAssignmentNode assign:
                    if (assign.CallValue != null)
                    {
                        yield return ExecuteNode(assign.CallValue, context);
                        string val = context.ReturnValue != null ? context.ReturnValue.ToString() : string.Empty;
                        context.SetString(assign.Name, val);
                    }
                    else
                    {
                        context.SetString(assign.Name, assign.Value.Evaluate(context));
                    }
                    yield break;

                case GameObjectAssignmentNode goAssign:
                    if (goAssign.CallValue != null)
                    {
                        yield return ExecuteNode(goAssign.CallValue, context);
                        ScriptGameObjectRef val = context.ReturnValue is ScriptGameObjectRef gr ? gr : ScriptGameObjectRef.Null;
                        context.SetGameObject(goAssign.Name, val);
                    }
                    else
                    {
                        context.SetGameObject(goAssign.Name, goAssign.Value != null ? goAssign.Value.Evaluate(context) : ScriptGameObjectRef.Null);
                    }
                    yield break;

                case ListAssignmentNode listAssign:
                    if (listAssign.CallValue != null)
                    {
                        yield return ExecuteNode(listAssign.CallValue, context);
                        List<ScriptValue> val = context.ReturnValue is List<ScriptValue> lv ? lv : new List<ScriptValue>();
                        context.SetList(listAssign.Name, val);
                    }
                    yield break;

                case ComponentAssignmentNode compAssign:
                    if (compAssign.CallValue != null)
                    {
                        yield return ExecuteNode(compAssign.CallValue, context);
                        ScriptComponentRef val = context.ReturnValue is ScriptComponentRef cr ? cr : ScriptComponentRef.Null;
                        context.SetComponent(compAssign.Name, val);
                    }
                    else
                    {
                        context.SetComponent(compAssign.Name, compAssign.Value != null ? compAssign.Value.Evaluate(context) : ScriptComponentRef.Null);
                    }
                    yield break;

                case ComponentPropertyAssignmentNode cpAssign:
                    ApplyComponentProperty(cpAssign, context);
                    yield break;

                case ComponentMethodCallNode cmCall:
                    ApplyComponentMethod(cmCall, context);
                    yield break;

                case ComponentVariableMethodCallNode cvmCall:
                    ApplyComponentVariableMethod(cvmCall, context);
                    yield break;

                case FunctionCallNode call:
                    if (!functionRegistry.TryGetValue(call.Name, out FunctionDefinitionNode def))
                        throw new InvalidOperationException($"Unknown function '{call.Name}'.");

                    string defPath = def.SourcePath ?? string.Empty;
                    bool isAccessible = def.IsPublic || string.Equals(defPath, context.CurrentModulePath, StringComparison.OrdinalIgnoreCase);
                    if (!isAccessible)
                        throw new InvalidOperationException($"Function '{call.Name}' in '{defPath}' is private and cannot be called from '{context.CurrentModulePath}'.");

                    if (call.Arguments.Count != def.Parameters.Count)
                        throw new InvalidOperationException($"Function '{call.Name}' expects {def.Parameters.Count} arguments, but received {call.Arguments.Count}.");

                    if (context.CallStackDepth >= LevelScriptExecutionContext.MaxCallStackDepth)
                    {
                        ReportScriptError($"Stack overflow: function call depth exceeded {LevelScriptExecutionContext.MaxCallStackDepth} in '{call.Name}'.", context.CurrentModulePath);
                        yield break;
                    }

                    LevelScriptExecutionContext fnContext = CreateContext(false, def.SourcePath);
                    fnContext.CallStackDepth = context.CallStackDepth + 1;
                    fnContext.PushScope();
                    for (int p = 0; p < def.Parameters.Count; p++)
                    {
                        BindFunctionArgument(fnContext, def.Parameters[p], call.Arguments[p], call.Name);
                    }

                    yield return RunBodyNodes(def.Body, fnContext);
                    fnContext.PopScope();
                    if (fnContext.HasReturn)
                        context.ReturnValue = fnContext.ReturnValue;
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

                case GotoNode gotoNode:
                    ExecuteLocalGoto(gotoNode, context);
                    yield break;

                case ScaleToNode scaleNode:
                    ExecuteLocalScaleTo(scaleNode, context);
                    yield break;

                case RotateNode rotNode:
                    ExecuteLocalRotate(rotNode, context);
                    yield break;

                case VisibilityNode visNode:
                    ExecuteLocalVisibility(visNode, context);
                    yield break;

                case TransparencyNode transNode:
                    ExecuteLocalTransparency(transNode, context);
                    yield break;

                case HitboxNode hbNode:
                    ExecuteLocalHitbox(hbNode, context);
                    yield break;

                case SetColorNode colNode:
                    ExecuteLocalSetColor(colNode, context);
                    yield break;
            }
        }

        static void BindFunctionArgument(
            LevelScriptExecutionContext context,
            FunctionParameter parameter,
            ScriptArgument argument,
            string functionName)
        {
            ScriptValue val = ScriptNetworkEventUtility.EvaluateToScriptValue(argument, context);

            // Support implicit Int -> Float conversion
            if (parameter.Kind == ScriptValueKind.Float && val.Kind == ScriptValueKind.Int)
            {
                val = ScriptValue.FromFloat(val.IntValue);
            }

            if (parameter.Kind != val.Kind)
            {
                throw new InvalidOperationException(
                    $"Function '{functionName}' argument '{parameter.Name}' expects {parameter.Kind}, got {val.Kind}.");
            }

            context.Declare(parameter.Name, val);
        }

        static void ApplyComponentProperty(ComponentPropertyAssignmentNode node, LevelScriptExecutionContext context)
        {
            if (!string.IsNullOrEmpty(node.ComponentVariableName))
            {
                ScriptComponentRef componentRef = context.GetComponent(node.ComponentVariableName);
                ApplyComponentPropertyToComponent(componentRef, node, context);
                return;
            }

            if (node.Targets != null)
            {
                List<ScriptGameObjectRef> targets = node.Targets.Evaluate(context);
                for (int i = 0; i < targets.Count; i++)
                    ApplyComponentPropertyToRef(targets[i], node, context);
                return;
            }

            ScriptGameObjectRef reference = context.GetGameObject(node.ObjectName);
            ApplyComponentPropertyToRef(reference, node, context);
        }

        static void ApplyComponentPropertyToComponent(
            ScriptComponentRef componentRef,
            ComponentPropertyAssignmentNode node,
            LevelScriptExecutionContext context)
        {
            switch (node.ValueKind)
            {
                case ScriptValueKind.Bool:
                    LevelScriptObjectUtility.SetComponentBool(
                        componentRef.GameObject,
                        componentRef.ComponentName,
                        node.PropertyName,
                        node.BoolValue != null && node.BoolValue.Evaluate(context));
                    return;
                case ScriptValueKind.Int:
                    LevelScriptObjectUtility.SetComponentInt(
                        componentRef.GameObject,
                        componentRef.ComponentName,
                        node.PropertyName,
                        node.IntValue != null ? node.IntValue.Evaluate(context) : 0);
                    return;
                default:
                    LevelScriptObjectUtility.SetComponentFloat(
                        componentRef.GameObject,
                        componentRef.ComponentName,
                        node.PropertyName,
                        node.FloatValue != null ? node.FloatValue.Evaluate(context) : 0f);
                    return;
            }
        }

        static void ApplyComponentPropertyToRef(
            ScriptGameObjectRef reference,
            ComponentPropertyAssignmentNode node,
            LevelScriptExecutionContext context)
        {
            switch (node.ValueKind)
            {
                case ScriptValueKind.Bool:
                    LevelScriptObjectUtility.SetComponentBool(
                        reference,
                        node.ComponentName,
                        node.PropertyName,
                        node.BoolValue != null && node.BoolValue.Evaluate(context));
                    return;
                case ScriptValueKind.Int:
                    LevelScriptObjectUtility.SetComponentInt(
                        reference,
                        node.ComponentName,
                        node.PropertyName,
                        node.IntValue != null ? node.IntValue.Evaluate(context) : 0);
                    return;
                default:
                    LevelScriptObjectUtility.SetComponentFloat(
                        reference,
                        node.ComponentName,
                        node.PropertyName,
                        node.FloatValue != null ? node.FloatValue.Evaluate(context) : 0f);
                    return;
            }
        }

        static void ApplyComponentMethod(ComponentMethodCallNode node, LevelScriptExecutionContext context)
        {
            ScriptGameObjectRef reference = context.GetGameObject(node.ObjectName);
            var arguments = new List<float>(node.Arguments.Count);
            for (int i = 0; i < node.Arguments.Count; i++)
                arguments.Add(node.Arguments[i].Evaluate(context));

            LevelScriptObjectUtility.CallComponentMethod(
                reference,
                node.ComponentName,
                node.MethodName,
                arguments);
        }

        static void ApplyComponentVariableMethod(ComponentVariableMethodCallNode node, LevelScriptExecutionContext context)
        {
            ScriptComponentRef componentRef = context.GetComponent(node.ComponentVariableName);
            var arguments = new List<float>(node.Arguments.Count);
            for (int i = 0; i < node.Arguments.Count; i++)
                arguments.Add(node.Arguments[i].Evaluate(context));

            LevelScriptObjectUtility.CallComponentMethod(
                componentRef.GameObject,
                componentRef.ComponentName,
                node.MethodName,
                arguments);
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

        void TrackInitialPosition(Transform tr)
        {
            if (tr != null && !initialPositions.ContainsKey(tr))
                initialPositions[tr] = tr.position;
        }

        void TrackInitialRotation(Transform tr)
        {
            if (tr != null && !initialRotations.ContainsKey(tr))
                initialRotations[tr] = tr.rotation;
        }

        void TrackInitialScale(Transform tr)
        {
            if (tr != null && !initialScales.ContainsKey(tr))
                initialScales[tr] = tr.localScale;
        }

        void TrackInitialRendererVisibility(Renderer r)
        {
            if (r != null && !initialRendererVisibility.ContainsKey(r))
                initialRendererVisibility[r] = r.enabled;
        }

        void TrackInitialSpriteColor(SpriteRenderer sr)
        {
            if (sr != null && !initialSpriteColors.ContainsKey(sr))
                initialSpriteColors[sr] = sr.color;
        }

        void TrackInitialColliderState(Collider2D c)
        {
            if (c != null && !initialColliderStates.ContainsKey(c))
                initialColliderStates[c] = c.enabled;
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
                    TrackInitialPosition(tr);
                    StartCoroutine(AnimateLocalTransform(tr, tr.position + new Vector3(dx, dy, 0f), dur, ease));
                }
            }
        }

        void ExecuteLocalGoto(GotoNode gotoNode, LevelScriptExecutionContext context)
        {
            if (gotoNode == null || gotoNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = gotoNode.Targets.Evaluate(context);
            float x = gotoNode.X != null ? gotoNode.X.Evaluate(context) : 0f;
            float y = gotoNode.Y != null ? gotoNode.Y.Evaluate(context) : 0f;
            float dur = gotoNode.Duration != null ? gotoNode.Duration.Evaluate(context) : 0f;
            string ease = gotoNode.Ease != null ? gotoNode.Ease.Evaluate(context) : "linear";

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    TrackInitialPosition(tr);
                    StartCoroutine(AnimateLocalTransform(tr, new Vector3(x, y, tr.position.z), dur, ease));
                }
            }
        }

        void ExecuteLocalScaleTo(ScaleToNode scaleNode, LevelScriptExecutionContext context)
        {
            if (scaleNode == null || scaleNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = scaleNode.Targets.Evaluate(context);
            float sx = scaleNode.ScaleX != null ? scaleNode.ScaleX.Evaluate(context) : 1f;
            float sy = scaleNode.ScaleY != null ? scaleNode.ScaleY.Evaluate(context) : 1f;
            float dur = scaleNode.Duration != null ? scaleNode.Duration.Evaluate(context) : 0f;
            string ease = scaleNode.Ease != null ? scaleNode.Ease.Evaluate(context) : "linear";

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    TrackInitialScale(tr);
                    StartCoroutine(AnimateLocalScale(tr, new Vector3(sx, sy, tr.localScale.z), dur, ease));
                }
            }
        }

        void ExecuteLocalRotate(RotateNode rotateNode, LevelScriptExecutionContext context)
        {
            if (rotateNode == null || rotateNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = rotateNode.Targets.Evaluate(context);
            float degrees = rotateNode.Degrees != null ? rotateNode.Degrees.Evaluate(context) : 0f;
            float dur = rotateNode.Duration != null ? rotateNode.Duration.Evaluate(context) : 0f;
            string ease = rotateNode.Ease != null ? rotateNode.Ease.Evaluate(context) : "linear";

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    TrackInitialRotation(tr);
                    Quaternion targetRot = tr.rotation * Quaternion.Euler(0f, 0f, degrees);
                    StartCoroutine(AnimateLocalRotation(tr, targetRot, dur, ease));
                }
            }
        }

        void ExecuteLocalVisibility(VisibilityNode visNode, LevelScriptExecutionContext context)
        {
            if (visNode == null || visNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = visNode.Targets.Evaluate(context);
            bool visible = visNode.Visible != null && visNode.Visible.Evaluate(context);

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    var renderers = tr.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        TrackInitialRendererVisibility(renderers[r]);
                        renderers[r].enabled = visible;
                    }
                }
            }
        }

        void ExecuteLocalTransparency(TransparencyNode transNode, LevelScriptExecutionContext context)
        {
            if (transNode == null || transNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = transNode.Targets.Evaluate(context);
            float alpha = transNode.Value != null ? Mathf.Clamp01(transNode.Value.Evaluate(context)) : 1f;
            float dur = transNode.Duration != null ? transNode.Duration.Evaluate(context) : 0f;
            string ease = transNode.Ease != null ? transNode.Ease.Evaluate(context) : "linear";

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    var srs = tr.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int s = 0; s < srs.Length; s++)
                    {
                        TrackInitialSpriteColor(srs[s]);
                        Color targetCol = new Color(srs[s].color.r, srs[s].color.g, srs[s].color.b, alpha);
                        StartCoroutine(AnimateLocalColor(srs[s], targetCol, dur, ease));
                    }
                }
            }
        }

        void ExecuteLocalHitbox(HitboxNode hbNode, LevelScriptExecutionContext context)
        {
            if (hbNode == null || hbNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = hbNode.Targets.Evaluate(context);
            bool enabled = hbNode.Enabled != null && hbNode.Enabled.Evaluate(context);

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    var colliders = tr.GetComponentsInChildren<Collider2D>(true);
                    for (int c = 0; c < colliders.Length; c++)
                    {
                        TrackInitialColliderState(colliders[c]);
                        colliders[c].enabled = enabled;
                    }
                }
            }
        }

        void ExecuteLocalSetColor(SetColorNode colNode, LevelScriptExecutionContext context)
        {
            if (colNode == null || colNode.Targets == null) return;
            List<ScriptGameObjectRef> targets = colNode.Targets.Evaluate(context);
            string hex = colNode.HexColor != null ? colNode.HexColor.Evaluate(context) : "#FFFFFF";
            if (!ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color col))
                col = Color.white;

            for (int i = 0; i < targets.Count; i++)
            {
                if (LevelScriptObjectUtility.TryResolveTransform(targets[i], out Transform tr) && tr != null)
                {
                    var srs = tr.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int s = 0; s < srs.Length; s++)
                    {
                        TrackInitialSpriteColor(srs[s]);
                        srs[s].color = new Color(col.r, col.g, col.b, srs[s].color.a);
                    }
                }
            }
        }

        IEnumerator AnimateLocalScale(Transform tr, Vector3 targetScale, float duration, string ease)
        {
            if (tr == null) yield break;
            if (duration <= 0.001f)
            {
                tr.localScale = targetScale;
                yield break;
            }

            LevelEase parsedEase = LevelEase.Linear;
            LevelEaseUtility.TryParse(ease, out parsedEase);
            Vector3 startScale = tr.localScale;
            float elapsed = 0f;

            while (elapsed < duration && tr != null && scriptActive)
            {
                elapsed += Time.deltaTime;
                float t = LevelEaseUtility.Evaluate(parsedEase, Mathf.Clamp01(elapsed / duration));
                tr.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
            if (tr != null && scriptActive) tr.localScale = targetScale;
        }

        IEnumerator AnimateLocalRotation(Transform tr, Quaternion targetRot, float duration, string ease)
        {
            if (tr == null) yield break;
            if (duration <= 0.001f)
            {
                tr.rotation = targetRot;
                yield break;
            }

            LevelEase parsedEase = LevelEase.Linear;
            LevelEaseUtility.TryParse(ease, out parsedEase);
            Quaternion startRot = tr.rotation;
            float elapsed = 0f;

            while (elapsed < duration && tr != null && scriptActive)
            {
                elapsed += Time.deltaTime;
                float t = LevelEaseUtility.Evaluate(parsedEase, Mathf.Clamp01(elapsed / duration));
                tr.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            if (tr != null && scriptActive) tr.rotation = targetRot;
        }

        IEnumerator AnimateLocalColor(SpriteRenderer sr, Color targetColor, float duration, string ease)
        {
            if (sr == null) yield break;
            if (duration <= 0.001f)
            {
                sr.color = targetColor;
                yield break;
            }

            LevelEase parsedEase = LevelEase.Linear;
            LevelEaseUtility.TryParse(ease, out parsedEase);
            Color startColor = sr.color;
            float elapsed = 0f;

            while (elapsed < duration && sr != null && scriptActive)
            {
                elapsed += Time.deltaTime;
                float t = LevelEaseUtility.Evaluate(parsedEase, Mathf.Clamp01(elapsed / duration));
                sr.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
            if (sr != null && scriptActive) sr.color = targetColor;
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

            if (tr != null && scriptActive)
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
            Vector3 lastOffset = Vector3.zero;
            float elapsed = 0f;
            try
            {
                while (elapsed < duration && scriptActive)
                {
                    elapsed += Time.deltaTime;
                    cam.transform.position -= lastOffset;
                    Vector2 circle = UnityEngine.Random.insideUnitCircle * intensity;
                    lastOffset = new Vector3(circle.x, circle.y, 0f);
                    cam.transform.position += lastOffset;
                    yield return null;
                }
            }
            finally
            {
                if (cam != null)
                    cam.transform.position -= lastOffset;
                cameraShakeRoutine = null;
            }
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
