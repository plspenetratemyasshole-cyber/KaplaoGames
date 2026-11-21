using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
  /// <summary>
  /// Executes level scripts using coroutines. Spawn blocks and main body can run in parallel.
  /// </summary>
  [DisallowMultipleComponent]
  public class LevelScriptRuntime : MonoBehaviour
  {
    public static LevelScriptRuntime Instance { get; private set; }

    public event System.Action<string> ScriptError;
    public event System.Action<string> ScriptPrint;

    readonly List<Coroutine> runningTasks = new();
    readonly List<TriggerHandlerRegistration> triggerRegistrations = new();
    LevelGroupAnimator animator;
    LevelScriptTriggerBridge triggerBridge;
    Dictionary<string, FunctionDefinitionNode> functionRegistry = new();
    readonly List<PlayerAddedHandlerNode> playerAddedHandlers = new();

    readonly List<EventListenerNode> clientEventListeners = new();
    readonly HashSet<uint> knownRunnerNetIds = new();
    readonly HashSet<long> activeStayKeys = new();
    LevelScriptProgramGlobals programGlobals;
    LevelScriptProgram activeProgram;
    LevelScriptLinkedProgram activeLinked;
    int nextTriggerHandlerId = 1;
    bool scriptActive;

    // Track objects created by instance() to destroy them on Stop
    readonly List<ScriptGameObjectRef> createdObjects = new();
    
    // Track destroyed objects to restore them on Stop
    readonly struct DestroyedObjectRecord
    {
      public readonly ScriptGameObjectRef Reference;
      public readonly GameObject OriginalPrefab;  // Prefab для тайлов, или сам объект для Scene
      public readonly Vector3 Position;
      public readonly Quaternion Rotation;
      public readonly bool IsSceneObject;  // true если это объект сцены (не prefab)

      public DestroyedObjectRecord(ScriptGameObjectRef reference, GameObject original, Vector3 pos, Quaternion rot, bool isSceneObject)
      {
        Reference = reference;
        OriginalPrefab = original;
        Position = pos;
        Rotation = rot;
        IsSceneObject = isSceneObject;
      }
    }
    readonly List<DestroyedObjectRecord> destroyedObjects = new();

    // Track added components to remove them on Stop
    readonly struct AddedComponentRecord
    {
      public readonly uint NetId;
      public readonly string ComponentTypeName;

      public AddedComponentRecord(uint netId, string typeName)
      {
        NetId = netId;
        ComponentTypeName = typeName;
      }
    }
    readonly List<AddedComponentRecord> addedComponents = new();
    readonly List<LevelTileRecord> destroyedTileSnapshots = new();
    Transform cameraFollowTarget;
    Vector2 initialGravity = new Vector2(0f, -9.81f);
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


    public IReadOnlyList<TriggerHandlerRegistration> TriggerRegistrations => triggerRegistrations;

    void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(this);
        return;
      }

      Instance = this;
      initialGravity = Physics2D.gravity;
      animator = GetComponent<LevelGroupAnimator>();
      if (animator == null)
        animator = gameObject.AddComponent<LevelGroupAnimator>();

      if (GetComponent<KeyframeAnimator>() == null)
        gameObject.AddComponent<KeyframeAnimator>();

      if (GetComponent<ExecutorRuntime>() == null)
        gameObject.AddComponent<ExecutorRuntime>();

      triggerBridge = GetComponent<LevelScriptTriggerBridge>();
      if (triggerBridge == null)
        triggerBridge = gameObject.AddComponent<LevelScriptTriggerBridge>();
    }

    void OnDestroy()
    {
      if (Instance == this)
        Instance = null;
    }

    public bool TryRun(string source, out string error)
    {
      bool wasRunning = scriptActive;
      StopScriptInternal(restoreTiles: wasRunning);

      LevelScriptProgram program;
      LevelScriptLinkedProgram linked = null;
      try
      {
        if (WorldScriptProjectService.HasProject)
        {
          linked = LevelScriptModuleLinker.Link(
            WorldScriptProjectService.EntryFileName,
            source ?? string.Empty);
          program = linked.Entry ?? new LevelScriptProgram();
        }
        else
        {
          program = string.IsNullOrWhiteSpace(source)
            ? new LevelScriptProgram()
            : LevelScriptParser.Parse(source);
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
        }

        LevelScriptSemanticAnalyzer.Analyze(linked, source ?? string.Empty);
      }
      catch (LevelScriptParseException ex)
      {
        error = ex.Message;
        return false;
      }
      catch (System.Exception ex)
      {
        error = ex.Message;
        return false;
      }

      error = null;
      try
      {
        programGlobals = new LevelScriptProgramGlobals();
        activeProgram = program;
        activeLinked = linked;
        LevelGroupAnimator.CaptureAllTilesBaseState();
        animator?.ResetMotions();
        KeyframeAnimator.Instance?.StopAll();
        if (linked != null)
          functionRegistry = new Dictionary<string, FunctionDefinitionNode>(
            linked.Functions,
            System.StringComparer.OrdinalIgnoreCase);
        else
          BuildFunctionRegistry(program);
        RegisterTriggerHandlers(program);
        RegisterPlayerAddedHandlers(program);

        RegisterClientEventListeners(program, linked);
        SeedKnownRunners();
        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam != null)
        {
          initialCamPos = cam.transform.position;
          initialCamOrthoSize = cam.orthographicSize;
          initialCamRecorded = true;
        }

        scriptActive = true;
        triggerBridge?.Begin(this, triggerRegistrations);

        ExecutorRuntime runtime = ExecutorRuntime.Instance ?? GetComponent<ExecutorRuntime>() ??
                                  gameObject.AddComponent<ExecutorRuntime>();
        runtime.BeginWithScript();

        if ((linked != null && linked.InitDeclarations.Count > 0) || program.MainBody.Count > 0)
          runningTasks.Add(StartCoroutine(RunEntryProgram(program)));

        for (int i = 0; i < program.Spawns.Count; i++)
          runningTasks.Add(StartCoroutine(RunBody(program.Spawns[i].Body)));
      }
      catch (System.Exception ex)
      {
        StopScriptInternal(restoreTiles: false);
        error = ex.Message;
        return false;
      }

      return true;
    }

    public bool IsRunning => scriptActive;

    public void StopScript()
    {
      StopScriptInternal(restoreTiles: scriptActive);
    }

    /// <summary>
    /// Stops coroutines without restoring tile transforms (used during level undo import).
    /// </summary>
    public void AbortRunningScriptWithoutRestore()
    {
      StopScriptInternal(restoreTiles: false);
    }

    void StopScriptInternal(bool restoreTiles)
    {
      bool wasActive = scriptActive;
      scriptActive = false;

      for (int i = 0; i < runningTasks.Count; i++)
      {
        if (runningTasks[i] != null)
          StopCoroutine(runningTasks[i]);
      }

      runningTasks.Clear();
      triggerRegistrations.Clear();
      playerAddedHandlers.Clear();
      LevelScriptObjectUtility.ClearSceneObjectRegistry();

      clientEventListeners.Clear();
      knownRunnerNetIds.Clear();
      activeStayKeys.Clear();
      functionRegistry.Clear();
      programGlobals?.Clear();
      programGlobals = null;
      activeProgram = null;
      activeLinked = null;
      nextTriggerHandlerId = 1;
      triggerBridge?.End();
      animator?.ResetMotions();
      KeyframeAnimator.Instance?.StopAll();
      ExecutorRuntime.Instance?.StopAll();

      if (restoreTiles && wasActive)
      {
        animator?.RestoreAllTilesToBase();
        DestroyCreatedObjects();
        RestoreDestroyedObjects();
        RemoveAddedComponents();
      }

      // Очищаем трекинг списки
      createdObjects.Clear();
      destroyedObjects.Clear();
      destroyedTileSnapshots.Clear();
      addedComponents.Clear();
      Physics2D.gravity = initialGravity;
      Time.timeScale = 1f;
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

    public void RegisterCreatedObject(ScriptGameObjectRef reference)
    {
      if (!createdObjects.Contains(reference))
        createdObjects.Add(reference);
    }

    public void RegisterDestroyedObject(ScriptGameObjectRef reference, GameObject original, bool isSceneObject)
    {
      if (original == null)
        return;

      destroyedObjects.Add(new DestroyedObjectRecord(
        reference,
        original,
        original.transform.position,
        original.transform.rotation,
        isSceneObject
      ));
    }

    public void RegisterDestroyedTile(LevelTileRecord record)
    {
      if (record.type != TileType.None)
        destroyedTileSnapshots.Add(record);
    }

    public LevelScriptExecutionContext CreateEvaluationContext() => CreateContext();

    public void RegisterAddedComponent(uint netId, string componentTypeName)
    {
      addedComponents.Add(new AddedComponentRecord(netId, componentTypeName));
    }

    void RestoreDestroyedObjects()
    {
      for (int i = 0; i < destroyedObjects.Count; i++)
      {
        DestroyedObjectRecord record = destroyedObjects[i];
        if (record.OriginalPrefab == null)
          continue;

        // Для объектов сцены просто включаем обратно
        if (record.IsSceneObject)
        {
          record.OriginalPrefab.SetActive(true);
          record.OriginalPrefab.transform.position = record.Position;
          record.OriginalPrefab.transform.rotation = record.Rotation;
        }
      }

      if (LevelGridServer.Instance != null)
      {
        for (int i = 0; i < destroyedTileSnapshots.Count; i++)
        {
          LevelGridServer.Instance.SpawnTileFromRecord(destroyedTileSnapshots[i]);
        }
      }
      destroyedTileSnapshots.Clear();
    }

    void RemoveAddedComponents()
    {
      if (!NetworkServer.active || addedComponents.Count == 0)
        return;

      for (int i = 0; i < addedComponents.Count; i++)
      {
        AddedComponentRecord record = addedComponents[i];
        ScriptGameObjectRef objRef = new ScriptGameObjectRef(ScriptObjectKind.Tile, record.NetId);
        
        // Используем RemoveComponent из ObjectUtility для правильного удаления
        try
        {
          LevelScriptObjectUtility.RemoveComponent(objRef, record.ComponentTypeName);
        }
        catch
        {
          // Игнорируем ошибки - объект или компонент могли быть уже удалены
        }
      }
    }

    void DestroyCreatedObjects()
    {
      if (createdObjects.Count == 0)
        return;

      for (int i = 0; i < createdObjects.Count; i++)
      {
        ScriptGameObjectRef objRef = createdObjects[i];
        
        if (!LevelScriptObjectUtility.TryResolveTransform(objRef, out Transform transform) || transform == null)
          continue;

        GameObject obj = transform.gameObject;
        if (obj == null)
          continue;

        // Используем NetworkServer.Destroy для сетевых объектов, Object.Destroy для остальных
        if (NetworkServer.active && obj.TryGetComponent<NetworkIdentity>(out _))
        {
          NetworkServer.Destroy(obj);
        }
        else
        {
          Object.Destroy(obj);
        }
      }
    }

    public void RunTriggerHandler(
      TriggerHandlerNode handler,
      ScriptGameObjectRef actorRef,
      ScriptGameObjectRef watchRef = default)
    {
      if (handler == null || handler.Body.Count == 0)
        return;

      runningTasks.Add(StartCoroutine(RunTriggerBody(handler, actorRef, watchRef)));
    }

    /// <summary>
    /// Stay fires every physics step; do not stack a new coroutine while one is already running
    /// for the same handler+actor pair (that was a major hitch source on busy triggers).
    /// </summary>
    public void TryRunTriggerStay(
      TriggerHandlerNode handler,
      int handlerId,
      ScriptGameObjectRef actorRef,
      ScriptGameObjectRef watchRef = default)
    {
      if (handler == null || handler.Body.Count == 0)
        return;

      long key = StayKey(handlerId, actorRef.NetId);
      if (!activeStayKeys.Add(key))
        return;

      runningTasks.Add(StartCoroutine(RunStayBody(handler, actorRef, watchRef, key)));
    }

    static long StayKey(int handlerId, uint actorNetId) =>
      ((long)(uint)handlerId << 32) | actorNetId;

    public void NotifyPlayerAdded(uint playerNetId)
    {
      if (!scriptActive || knownRunnerNetIds.Contains(playerNetId))
        return;

      knownRunnerNetIds.Add(playerNetId);

      for (int i = 0; i < playerAddedHandlers.Count; i++)
        runningTasks.Add(StartCoroutine(RunPlayerAddedBody(playerAddedHandlers[i], playerNetId)));
    }

    void RegisterClientEventListeners(LevelScriptProgram program, LevelScriptLinkedProgram linked)
    {
      clientEventListeners.Clear();
      if (linked != null)
      {
        for (int i = 0; i < linked.EventListeners.Count; i++)
        {
          if (linked.EventListeners[i].Target == ScriptEventTarget.ClientEvent)
            clientEventListeners.Add(linked.EventListeners[i]);
        }
      }
      for (int i = 0; i < program.EventListeners.Count; i++)
      {
        if (program.EventListeners[i].Target == ScriptEventTarget.ClientEvent && !clientEventListeners.Contains(program.EventListeners[i]))
          clientEventListeners.Add(program.EventListeners[i]);
      }
    }

    public void DispatchClientEvent(string eventName, ScriptGameObjectRef playerRef, string jsonArgs)
    {
      if (!scriptActive || string.IsNullOrEmpty(eventName))
        return;

      ScriptNetworkEventUtility.TryDeserializePayload(jsonArgs, out _, out ScriptNetworkEventArg[] args);
      args ??= System.Array.Empty<ScriptNetworkEventArg>();

      for (int i = 0; i < clientEventListeners.Count; i++)
      {
        EventListenerNode listener = clientEventListeners[i];
        if (string.Equals(listener.EventName, eventName, System.StringComparison.OrdinalIgnoreCase))
        {
          runningTasks.Add(StartCoroutine(RunClientEventBody(listener, playerRef, args)));
        }
      }
    }

    IEnumerator RunClientEventBody(EventListenerNode listener, ScriptGameObjectRef playerRef, ScriptNetworkEventArg[] args)
    {
      var context = CreateContext();
      context.LocalPlayerRef = playerRef;

      if (!string.IsNullOrEmpty(listener.PlayerParameterName))
      {
        BindGameObjectParameter(context, listener.PlayerParameterName, playerRef);
      }

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


    void RegisterPlayerAddedHandlers(LevelScriptProgram program)
    {
      playerAddedHandlers.Clear();
      playerAddedHandlers.AddRange(program.PlayerAddedHandlers);
    }

    void SeedKnownRunners()
    {
      knownRunnerNetIds.Clear();
      if (NetworkServer.active)
      {
        foreach (KeyValuePair<uint, NetworkIdentity> pair in NetworkServer.spawned)
        {
          if (pair.Value != null &&
              pair.Value.TryGetComponent(out NetworkPlayer player) &&
              player.role == PlayerRole.Runner)
          {
            knownRunnerNetIds.Add(player.netId);
          }
        }

        return;
      }

      NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
      for (int i = 0; i < players.Length; i++)
      {
        if (players[i].role == PlayerRole.Runner)
          knownRunnerNetIds.Add(players[i].netId);
      }
    }

    IEnumerator RunStayBody(
      TriggerHandlerNode handler,
      ScriptGameObjectRef actorRef,
      ScriptGameObjectRef watchRef,
      long stayKey)
    {
      try
      {
        // Nested IEnumerator (not StartCoroutine) so StopScript stops the whole stay body.
        yield return RunTriggerBody(handler, actorRef, watchRef);
      }
      finally
      {
        activeStayKeys.Remove(stayKey);
      }
    }

    IEnumerator RunTriggerBody(
      TriggerHandlerNode handler,
      ScriptGameObjectRef actorRef,
      ScriptGameObjectRef watchRef)
    {
      var context = CreateContext();
      if (handler.Source is GroupGameObjectArrayExpression groupSource)
      {
        int group = groupSource.GroupId != null
          ? groupSource.GroupId.Evaluate(context)
          : context.GetCurrentTriggerGroup();
        context.SetCurrentTriggerGroup(group);
      }
      else if (handler.Source is MultiGroupGameObjectArrayExpression multiSource)
      {
        if (TryResolveWatchGroup(multiSource, watchRef, out int hitGroup))
          context.SetCurrentTriggerGroup(hitGroup);
        else if (multiSource.GroupIds.Count > 0)
          context.SetCurrentTriggerGroup(multiSource.GroupIds[0]);
      }

      if (!string.IsNullOrEmpty(handler.GameObjectParameterName))
        BindGameObjectParameter(context, handler.GameObjectParameterName, actorRef);

      yield return RunBodyNodes(handler.Body, context);
    }

    static bool TryResolveWatchGroup(
      MultiGroupGameObjectArrayExpression multiSource,
      ScriptGameObjectRef watchRef,
      out int groupId)
    {
      groupId = 0;
      if (watchRef.IsNull || watchRef.Kind != ScriptObjectKind.Tile)
        return false;

      if (!LevelScriptObjectUtility.TryResolveTile(watchRef.NetId, out LevelTileServer tile) || tile == null)
        return false;

      IReadOnlyList<int> tileGroups = tile.CachedGroupIds;
      for (int i = 0; i < multiSource.GroupIds.Count; i++)
      {
        int candidate = multiSource.GroupIds[i];
        for (int t = 0; t < tileGroups.Count; t++)
        {
          if (tileGroups[t] == candidate)
          {
            groupId = candidate;
            return true;
          }
        }
      }

      return false;
    }

    static void BindGameObjectParameter(LevelScriptExecutionContext context, string name, ScriptGameObjectRef reference)
    {
      context.BindGameObject(name, reference);

      if (reference.Kind != ScriptObjectKind.Player ||
          !LevelScriptObjectUtility.TryResolvePlayer(reference.NetId, out NetworkPlayer player))
      {
        return;
      }

      Vector3 position = player.transform.position;
      context.BindPlayer(name, new ScriptPlayerBinding
      {
        NetId = player.netId,
        Name = player.displayName,
        X = position.x,
        Y = position.y
      });
    }

    IEnumerator RunPlayerAddedBody(PlayerAddedHandlerNode handler, uint playerNetId)
    {
      if (!LevelScriptObjectUtility.TryResolvePlayer(playerNetId, out NetworkPlayer player))
        yield break;

      var context = CreateContext();
      var reference = new ScriptGameObjectRef(ScriptObjectKind.Player, player.netId);
      BindGameObjectParameter(context, handler.PlayerParameterName, reference);
      yield return RunBodyNodes(handler.Body, context);
    }

    void BuildFunctionRegistry(LevelScriptProgram program)
    {
      functionRegistry = new Dictionary<string, FunctionDefinitionNode>(System.StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < program.Functions.Count; i++)
      {
        FunctionDefinitionNode function = program.Functions[i];
        if (functionRegistry.ContainsKey(function.Name))
          throw new LevelScriptParseException($"Duplicate function '{function.Name}'.");

        functionRegistry[function.Name] = function;
      }
    }

    void RegisterTriggerHandlers(LevelScriptProgram program)
    {
      triggerRegistrations.Clear();
      nextTriggerHandlerId = 1;

      for (int i = 0; i < program.TriggerHandlers.Count; i++)
      {
        triggerRegistrations.Add(new TriggerHandlerRegistration
        {
          Id = nextTriggerHandlerId++,
          Handler = program.TriggerHandlers[i]
        });
      }

      RegisterSpawnPointsHandlers(program);
      RegisterAutoExecutorTriggers();
      RegisterTileSpawnPointHandlers();
    }

    void RegisterAutoExecutorTriggers()
    {
      LevelGridServer grid = LevelGridServer.Instance;
      if (grid == null)
        return;

      // Find all executors that should be spawned from triggers
      var executorByGroup = new System.Collections.Generic.Dictionary<int, ExecutorTile>();
      ExecutorTile[] executors = UnityEngine.Object.FindObjectsByType<ExecutorTile>(FindObjectsSortMode.None);
      for (int i = 0; i < executors.Length; i++)
      {
        ExecutorTile executor = executors[i];
        if (executor == null || !executor.SpawnedExecutor)
          continue;

        LevelTileServer tile = executor.GetComponent<LevelTileServer>();
        if (tile == null)
          continue;

        // Use the first group of the executor as its trigger group
        IReadOnlyList<int> groups = tile.CachedGroupIds;
        for (int g = 0; g < groups.Count; g++)
        {
          int group = groups[g];
          if (!executorByGroup.ContainsKey(group))
            executorByGroup[group] = executor;
        }
      }

      // Collect all trigger groups that have matching executors
      var multiSource = new MultiGroupGameObjectArrayExpression();
      grid.ForEachTile(tile =>
      {
        if (tile == null || tile.TileType != TileType.Trigger)
          return;

        IReadOnlyList<int> groups = tile.CachedGroupIds;
        for (int g = 0; g < groups.Count; g++)
        {
          int group = groups[g];
          // Only add to source if there's an executor with this group
          if (executorByGroup.ContainsKey(group) && !multiSource.GroupIds.Contains(group))
            multiSource.GroupIds.Add(group);
        }
      });

      // Create a single handler for all auto-executor triggers
      if (multiSource.GroupIds.Count > 0)
      {
        var handler = new TriggerHandlerNode
        {
          Kind = TriggerHandlerKind.Enter,
          Source = multiSource,
          ActorMode = TriggerActorMode.Runners
        };

        // This will be filled dynamically when the trigger fires
        // The group() function call inside the body will use the correct group
        var groupExpr = new GroupGameObjectArrayExpression { GroupId = null }; // Uses current trigger group
        handler.Body.Add(new SpawnExecutorNode { Targets = groupExpr });

        triggerRegistrations.Add(new TriggerHandlerRegistration
        {
          Id = nextTriggerHandlerId++,
          Handler = handler
        });
      }
    }

    void RegisterTileSpawnPointHandlers()
    {
      LevelGridServer grid = LevelGridServer.Instance;
      if (grid == null)
        return;

      var multiSource = new MultiGroupGameObjectArrayExpression();
      grid.ForEachTile(tile =>
      {
        if (tile == null || tile.TileType != TileType.Trigger || !tile.IsSpawnPoint)
          return;

        IReadOnlyList<int> groups = tile.CachedGroupIds;
        if (groups.Count == 0)
        {
          triggerRegistrations.Add(new TriggerHandlerRegistration
          {
            Id = nextTriggerHandlerId++,
            Handler = CreateSingleTileSpawnPointHandler(tile)
          });
          return;
        }

        for (int i = 0; i < groups.Count; i++)
        {
          int group = groups[i];
          if (!multiSource.GroupIds.Contains(group))
            multiSource.GroupIds.Add(group);
        }
      });

      if (multiSource.GroupIds.Count > 0)
      {
        triggerRegistrations.Add(new TriggerHandlerRegistration
        {
          Id = nextTriggerHandlerId++,
          Handler = CreateMultiGroupSpawnPointHandler(multiSource)
        });
      }
    }

    public void RefreshTileSpawnPointHandlers()
    {
      if (!scriptActive || activeProgram == null)
        return;

      triggerRegistrations.Clear();
      nextTriggerHandlerId = 1;
      RegisterTriggerHandlers(activeProgram);
      triggerBridge?.Begin(this, triggerRegistrations);
    }

    void RegisterSpawnPointsHandlers(LevelScriptProgram program)
    {
      if (program.SpawnPointsDeclarations.Count == 0)
        return;

      var context = CreateContext();
      var multiSource = new MultiGroupGameObjectArrayExpression();

      for (int i = 0; i < program.SpawnPointsDeclarations.Count; i++)
      {
        SpawnPointsNode declaration = program.SpawnPointsDeclarations[i];
        int from = declaration.From.Evaluate(context);
        int to = declaration.To.Evaluate(context);
        if (from > to)
        {
          int swap = from;
          from = to;
          to = swap;
        }

        if (from < 0)
        {
          throw new System.InvalidOperationException(
            $"spawnPoints({from}, {to}): group ids must be non-negative.");
        }

        for (int group = from; group <= to; group++)
        {
          if (!multiSource.GroupIds.Contains(group))
            multiSource.GroupIds.Add(group);
        }
      }

      if (multiSource.GroupIds.Count == 0)
        return;

      triggerRegistrations.Add(new TriggerHandlerRegistration
      {
        Id = nextTriggerHandlerId++,
        Handler = CreateMultiGroupSpawnPointHandler(multiSource)
      });
    }

    static TriggerHandlerNode CreateMultiGroupSpawnPointHandler(MultiGroupGameObjectArrayExpression source)
    {
      const string playerParameter = "player";
      var handler = new TriggerHandlerNode
      {
        Kind = TriggerHandlerKind.Enter,
        Source = source,
        ActorMode = TriggerActorMode.Runners,
        GameObjectParameterName = playerParameter
      };

      handler.Body.Add(new SpawnPointNode
      {
        Player = new GameObjectVariableExpression { Name = playerParameter },
        X = new GroupMemberFloatExpression { Member = PlayerMember.X },
        Y = new GroupMemberFloatExpression { Member = PlayerMember.Y }
      });

      return handler;
    }

    static TriggerHandlerNode CreateSingleTileSpawnPointHandler(LevelTileServer tile)
    {
      const string playerParameter = "player";
      var source = new GameObjectArrayLiteralExpression();
      source.Items.Add(new FixedTileGameObjectExpression { NetId = tile.netId });

      Vector2 position = tile.Position;
      var handler = new TriggerHandlerNode
      {
        Kind = TriggerHandlerKind.Enter,
        Source = source,
        ActorMode = TriggerActorMode.Runners,
        GameObjectParameterName = playerParameter
      };

      handler.Body.Add(new SpawnPointNode
      {
        Player = new GameObjectVariableExpression { Name = playerParameter },
        X = new FloatLiteralExpression { Value = position.x },
        Y = new FloatLiteralExpression { Value = position.y }
      });

      return handler;
    }

    static LevelEase ParseEase(string easeName)
    {
      if (!LevelEaseUtility.TryParse(easeName, out LevelEase ease))
      {
        throw new System.InvalidOperationException(
          $"Unknown ease '{easeName}'. Use \"linear\", \"easeIn\", \"easeOut\", or \"easeInOut\".");
      }

      return ease;
    }

    IEnumerator RunEntryProgram(LevelScriptProgram program)
    {
      if (activeLinked != null)
      {
        for (int i = 0; i < activeLinked.InitDeclarations.Count; i++)
        {
          LevelScriptNode node = activeLinked.InitDeclarations[i];
          LevelScriptExecutionContext initContext = CreateContext(
            writesProgramGlobals: true,
            modulePath: GetDeclarationSourcePath(node));
          yield return ExecuteNode(node, initContext);
        }
      }

      if (program.MainBody.Count == 0)
        yield break;

      yield return RunBody(program.MainBody);
    }

    IEnumerator RunBody(List<LevelScriptNode> body)
    {
      var context = CreateContext(writesProgramGlobals: true);
      yield return RunBodyNodes(body, context);
    }

    IEnumerator RunSpawnBody(List<LevelScriptNode> body, LevelScriptExecutionContext context)
    {
      yield return RunBodyNodes(body, context);
    }

    LevelScriptExecutionContext CreateContext(bool writesProgramGlobals = false, string modulePath = null)
    {
      string path = modulePath;
      if (string.IsNullOrEmpty(path))
        path = activeProgram?.SourcePath ?? WorldScriptProjectService.EntryFileName;

      return new LevelScriptExecutionContext(functionRegistry, programGlobals, activeLinked, path)
      {
        WritesProgramGlobals = writesProgramGlobals
      };
    }

    static string GetDeclarationSourcePath(LevelScriptNode node)
    {
      return node switch
      {
        IntDeclarationNode d => d.SourcePath,
        BoolDeclarationNode d => d.SourcePath,
        FloatDeclarationNode d => d.SourcePath,
        StringDeclarationNode d => d.SourcePath,
        GameObjectDeclarationNode d => d.SourcePath,
        ComponentDeclarationNode d => d.SourcePath,
        _ => string.Empty
      };
    }

    static readonly Stack<Stack<IEnumerator>> stackPool = new();

    static Stack<IEnumerator> RentStack()
    {
      return stackPool.Count > 0 ? stackPool.Pop() : new Stack<IEnumerator>();
    }

    static void ReturnStack(Stack<IEnumerator> stack)
    {
      stack.Clear();
      stackPool.Push(stack);
    }

    IEnumerator RunBodyNodes(List<LevelScriptNode> body, LevelScriptExecutionContext context)
    {
      for (int i = 0; i < body.Count; i++)
      {
        if (context.HasReturn || context.HasBreak || context.HasContinue)
          yield break;

        IEnumerator routine;
        try
        {
          routine = ExecuteNode(body[i], context);
        }
        catch (System.Exception ex)
        {
          Debug.LogException(ex);
          ReportScriptError(ex.Message);
          yield break;
        }

        yield return RunEnumeratorSafe(routine);
        if (!scriptActive || context.HasReturn || context.HasBreak || context.HasContinue)
          yield break;
      }
    }

    IEnumerator RunEnumeratorSafe(IEnumerator routine)
    {
      if (routine == null)
        yield break;

      Stack<IEnumerator> stack = RentStack();
      stack.Push(routine);

      try
      {
        while (stack.Count > 0 && scriptActive)
        {
          IEnumerator current = stack.Peek();
          bool moved;
          object yielded = null;
          try
          {
            moved = current.MoveNext();
            if (moved)
              yielded = current.Current;
          }
          catch (System.Exception ex)
          {
            ReportScriptError(ex.Message);
            yield break;
          }

          if (!moved)
          {
            stack.Pop();
            continue;
          }

          if (yielded is IEnumerator nested)
          {
            stack.Push(nested);
            continue;
          }

          yield return yielded;
        }
      }
      finally
      {
        ReturnStack(stack);
      }
    }

    void ReportScriptError(string message)
    {
      if (string.IsNullOrEmpty(message))
        message = "Script error.";

      if (!message.StartsWith("Line ", System.StringComparison.OrdinalIgnoreCase) &&
          activeProgram != null)
      {
        // Best-effort: keep runtime errors consistent with IDE diagnostics.
        message = "Line 1, column 1: " + message;
      }

      Debug.LogError($"[LevelScriptRuntime] {message}");
      ScriptError?.Invoke(message);
      LevelSessionServer.Instance?.ServerReportScriptError(message);
      StopScriptInternal(restoreTiles: true);
    }

    IEnumerator ExecuteNode(LevelScriptNode node, LevelScriptExecutionContext context)
    {
      switch (node)
      {
        case SpawnNode spawn:
          LevelScriptExecutionContext spawnContext = context.Fork();
          runningTasks.Add(StartCoroutine(RunSpawnBody(spawn.Body, spawnContext)));
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
          while (whileNode.Condition.Evaluate(context))
          {
            whileIterations++;
            if (whileIterations % 5000 == 0)
              yield return null; // Anti-freeze watchdog: let Unity render and process events
            if (whileIterations > 200000)
            {
              ReportScriptError("Infinite loop detected: while loop exceeded 200,000 iterations without waiting.");
              yield break;
            }

            context.PushScope();
            try
            {
              for (int i = 0; i < whileNode.Body.Count; i++)
              {
                yield return ExecuteNode(whileNode.Body[i], context);
                if (context.HasReturn || context.HasBreak || context.HasContinue)
                  break;
              }
            }
            finally
            {
              context.PopScope();
            }

            if (context.HasReturn)
              yield break;
            if (context.HasBreak)
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

        case IfNode ifNode:
          if (ifNode.Condition.Evaluate(context))
          {
            context.PushScope();
            try
            {
              for (int i = 0; i < ifNode.ThenBody.Count; i++)
              {
                yield return ExecuteNode(ifNode.ThenBody[i], context);
                if (context.HasReturn || context.HasBreak || context.HasContinue)
                  break;
              }
            }
            finally
            {
              context.PopScope();
            }
          }
          else
          {
            bool matched = false;
            for (int i = 0; i < ifNode.ElseIfClauses.Count; i++)
            {
              ElseIfClause clause = ifNode.ElseIfClauses[i];
              if (!clause.Condition.Evaluate(context))
                continue;

              matched = true;
              context.PushScope();
              try
              {
                for (int j = 0; j < clause.Body.Count; j++)
                {
                  yield return ExecuteNode(clause.Body[j], context);
                  if (context.HasReturn || context.HasBreak || context.HasContinue)
                    break;
                }
              }
              finally
              {
                context.PopScope();
              }
              break;
            }

            if (!matched && ifNode.ElseBody != null && ifNode.ElseBody.Count > 0)
            {
              context.PushScope();
              try
              {
                for (int i = 0; i < ifNode.ElseBody.Count; i++)
                {
                  yield return ExecuteNode(ifNode.ElseBody[i], context);
                  if (context.HasReturn || context.HasBreak || context.HasContinue)
                    break;
                }
              }
              finally
              {
                context.PopScope();
              }
            }
          }
          yield break;

        case IntDeclarationNode declaration:
          if (declaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(declaration.CallValue, context);
            int intVal = context.ReturnValue switch
            {
              int iv => iv,
              float fv => (int)fv,
              _ => 0
            };
            context.DeclareInt(declaration.Name, intVal);
          }
          else
          {
            context.DeclareInt(declaration.Name, declaration.Value != null ? declaration.Value.Evaluate(context) : 0);
          }
          yield break;

        case BoolDeclarationNode boolDeclaration:
          if (boolDeclaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(boolDeclaration.CallValue, context);
            bool bVal = context.ReturnValue is bool bv && bv;
            context.DeclareBool(boolDeclaration.Name, bVal);
          }
          else
          {
            context.DeclareBool(boolDeclaration.Name, boolDeclaration.Value != null && boolDeclaration.Value.Evaluate(context));
          }
          yield break;

        case FloatDeclarationNode floatDeclaration:
          if (floatDeclaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(floatDeclaration.CallValue, context);
            float fVal = context.ReturnValue switch
            {
              float fv => fv,
              int iv => (float)iv,
              _ => 0f
            };
            context.DeclareFloat(floatDeclaration.Name, fVal);
          }
          else
          {
            context.DeclareFloat(floatDeclaration.Name, floatDeclaration.Value != null ? floatDeclaration.Value.Evaluate(context) : 0f);
          }
          yield break;

        case StringDeclarationNode stringDeclaration:
          if (stringDeclaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(stringDeclaration.CallValue, context);
            string sVal = context.ReturnValue != null ? context.ReturnValue.ToString() : string.Empty;
            context.DeclareString(stringDeclaration.Name, sVal);
          }
          else
          {
            context.DeclareString(stringDeclaration.Name, stringDeclaration.Value != null ? stringDeclaration.Value.Evaluate(context) : string.Empty);
          }
          yield break;

        case GameObjectDeclarationNode gameObjectDeclaration:
          if (gameObjectDeclaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(gameObjectDeclaration.CallValue, context);
            ScriptGameObjectRef goVal = context.ReturnValue is ScriptGameObjectRef gr ? gr : ScriptGameObjectRef.Null;
            context.DeclareGameObject(gameObjectDeclaration.Name, goVal);
          }
          else
          {
            context.DeclareGameObject(
              gameObjectDeclaration.Name,
              gameObjectDeclaration.Value != null ? gameObjectDeclaration.Value.Evaluate(context) : ScriptGameObjectRef.Null);
          }
          yield break;

        case ComponentDeclarationNode componentDeclaration:
          if (componentDeclaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(componentDeclaration.CallValue, context);
            ScriptComponentRef compVal = context.ReturnValue is ScriptComponentRef cr ? cr : ScriptComponentRef.Null;
            context.DeclareComponent(componentDeclaration.Name, compVal);
          }
          else
          {
            context.DeclareComponent(
              componentDeclaration.Name,
              componentDeclaration.Value != null ? componentDeclaration.Value.Evaluate(context) : ScriptComponentRef.Null);
          }
          yield break;

        case ListDeclarationNode listDeclaration:
          if (listDeclaration.CallValue != null)
          {
            yield return ExecuteFunctionCall(listDeclaration.CallValue, context);
            List<ScriptValue> listVal = context.ReturnValue is List<ScriptValue> lv ? lv : new List<ScriptValue>();
            context.DeclareList(listDeclaration.Name, listVal);
          }
          else
          {
            var initList = new List<ScriptValue>();
            for (int li = 0; li < listDeclaration.InitialItems.Count; li++)
              initList.Add(ScriptNetworkEventUtility.EvaluateToScriptValue(listDeclaration.InitialItems[li], context));
            context.DeclareList(listDeclaration.Name, initList);
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

        case IntAssignmentNode assignment:
          if (assignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(assignment.CallValue, context);
            int intVal = context.ReturnValue switch
            {
              int iv => iv,
              float fv => (int)fv,
              _ => 0
            };
            context.SetInt(assignment.Name, intVal);
          }
          else
          {
            context.SetInt(assignment.Name, assignment.Value.Evaluate(context));
          }
          yield break;

        case BoolAssignmentNode boolAssignment:
          if (boolAssignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(boolAssignment.CallValue, context);
            bool bVal = context.ReturnValue is bool bv && bv;
            context.SetBool(boolAssignment.Name, bVal);
          }
          else
          {
            context.SetBool(boolAssignment.Name, boolAssignment.Value.Evaluate(context));
          }
          yield break;

        case FloatAssignmentNode floatAssignment:
          if (floatAssignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(floatAssignment.CallValue, context);
            float fVal = context.ReturnValue switch
            {
              float fv => fv,
              int iv => (float)iv,
              _ => 0f
            };
            context.SetFloat(floatAssignment.Name, fVal);
          }
          else
          {
            context.SetFloat(floatAssignment.Name, floatAssignment.Value.Evaluate(context));
          }
          yield break;

        case StringAssignmentNode stringAssignment:
          if (stringAssignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(stringAssignment.CallValue, context);
            string sVal = context.ReturnValue != null ? context.ReturnValue.ToString() : string.Empty;
            context.SetString(stringAssignment.Name, sVal);
          }
          else
          {
            context.SetString(stringAssignment.Name, stringAssignment.Value.Evaluate(context));
          }
          yield break;

        case GameObjectAssignmentNode goAssignment:
          if (goAssignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(goAssignment.CallValue, context);
            ScriptGameObjectRef goVal = context.ReturnValue is ScriptGameObjectRef gr ? gr : ScriptGameObjectRef.Null;
            context.SetGameObject(goAssignment.Name, goVal);
          }
          else
          {
            context.SetGameObject(goAssignment.Name, goAssignment.Value != null ? goAssignment.Value.Evaluate(context) : ScriptGameObjectRef.Null);
          }
          yield break;

        case ListAssignmentNode listAssignment:
          if (listAssignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(listAssignment.CallValue, context);
            List<ScriptValue> listVal = context.ReturnValue is List<ScriptValue> lv ? lv : new List<ScriptValue>();
            context.SetList(listAssignment.Name, listVal);
          }
          yield break;

        case ComponentAssignmentNode componentAssignment:
          if (componentAssignment.CallValue != null)
          {
            yield return ExecuteFunctionCall(componentAssignment.CallValue, context);
            ScriptComponentRef compVal = context.ReturnValue is ScriptComponentRef cr ? cr : ScriptComponentRef.Null;
            context.SetComponent(componentAssignment.Name, compVal);
          }
          else
          {
            context.SetComponent(componentAssignment.Name, componentAssignment.Value != null ? componentAssignment.Value.Evaluate(context) : ScriptComponentRef.Null);
          }
          yield break;

        case ComponentPropertyAssignmentNode componentAssignment:
          ApplyComponentProperty(componentAssignment, context);
          yield break;

        case ComponentMethodCallNode componentMethodCall:
          ApplyComponentMethod(componentMethodCall, context);
          yield break;

        case ComponentVariableMethodCallNode componentVarMethodCall:
          ApplyComponentVariableMethod(componentVarMethodCall, context);
          yield break;

        case FunctionCallNode call:
          yield return ExecuteFunctionCall(call, context);
          yield break;

        case ForNode forNode:
          bool previousWritesProgramGlobals = context.WritesProgramGlobals;
          context.WritesProgramGlobals = false;
          context.PushScope();
          int forIterations = 0;
          try
          {
            if (forNode.Init != null)
              yield return ExecuteNode(forNode.Init, context);

            while (forNode.Condition == null || forNode.Condition.Evaluate(context))
            {
              forIterations++;
              if (forIterations % 5000 == 0)
                yield return null; // Anti-freeze watchdog
              if (forIterations > 200000)
              {
                ReportScriptError("Infinite loop detected: for loop exceeded 200,000 iterations without waiting.");
                yield break;
              }

              for (int i = 0; i < forNode.Body.Count; i++)
              {
                yield return ExecuteNode(forNode.Body[i], context);
                if (context.HasReturn || context.HasBreak || context.HasContinue)
                  break;
              }

              if (context.HasReturn)
                yield break;

              if (context.HasBreak)
              {
                context.HasBreak = false;
                yield break;
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
            context.WritesProgramGlobals = previousWritesProgramGlobals;
          }
          yield break;

        case MoveNode move:
        {
          List<ScriptGameObjectRef> targets = move.Targets.Evaluate(context);
          float duration = move.Duration.Evaluate(context);
          animator?.StartMoveObjects(
            targets,
            move.DeltaX.Evaluate(context),
            move.DeltaY.Evaluate(context),
            duration,
            ParseEase(move.Ease.Evaluate(context)));
          if (duration <= 0f)
            animator?.SyncScriptedTilesNow();
          yield break;
        }

        case AnimateNode animate:
        {
          List<ScriptGameObjectRef> targetRefs = animate.TargetGroup.Evaluate(context);
          List<ScriptGameObjectRef> keyRefs = animate.KeyGroup.Evaluate(context);
          KeyframeAnimator keyframeAnimator = KeyframeAnimator.Instance;
          if (keyframeAnimator == null)
            keyframeAnimator = GetComponent<KeyframeAnimator>() ?? gameObject.AddComponent<KeyframeAnimator>();
          keyframeAnimator.StartAnimateObjects(targetRefs, keyRefs);
          yield break;
        }

        case SpawnExecutorNode spawnExecutor:
        {
          List<ScriptGameObjectRef> refs = spawnExecutor.Targets.Evaluate(context);
          ExecutorRuntime runtime = ExecutorRuntime.Instance ?? GetComponent<ExecutorRuntime>() ??
                                    gameObject.AddComponent<ExecutorRuntime>();
          runtime.SpawnExecutorsFromObjects(refs);
          yield break;
        }

        case RotateNode rotate:
        {
          List<ScriptGameObjectRef> targets = rotate.Targets.Evaluate(context);
          if (rotate.PivotObjects != null)
          {
            List<ScriptGameObjectRef> pivotObjects = rotate.PivotObjects.Evaluate(context);
            animator?.StartRotateObjectsAroundObjects(
              targets,
              pivotObjects,
              rotate.Degrees.Evaluate(context),
              rotate.Duration.Evaluate(context),
              ParseEase(rotate.Ease.Evaluate(context)));
          }
          else
          {
            animator?.StartRotateObjects(
              targets,
              rotate.PivotGroup.Evaluate(context),
              rotate.Degrees.Evaluate(context),
              rotate.Duration.Evaluate(context),
              ParseEase(rotate.Ease.Evaluate(context)));
          }
          yield break;
        }

        case RotateToNode rotateTo:
        {
          List<ScriptGameObjectRef> targets = rotateTo.Targets.Evaluate(context);
          List<ScriptGameObjectRef> pivotObjects = rotateTo.PivotObjects != null
            ? rotateTo.PivotObjects.Evaluate(context)
            : null;
          animator?.StartRotateToObjects(
            targets,
            pivotObjects,
            rotateTo.TargetX.Evaluate(context),
            rotateTo.TargetY.Evaluate(context),
            rotateTo.OffsetDegrees.Evaluate(context),
            rotateTo.Duration.Evaluate(context),
            ParseEase(rotateTo.Ease.Evaluate(context)));
          yield break;
        }

        case WaitNode wait:
        {
          float waitDuration = wait.Duration.Evaluate(context);
          if (waitDuration > 0f)
            yield return new WaitForSeconds(waitDuration);
          yield break;
        }

        case GotoNode gotoNode:
        {
          List<ScriptGameObjectRef> targets = gotoNode.Targets.Evaluate(context);
          animator?.StartGotoObjects(
            targets,
            gotoNode.X.Evaluate(context),
            gotoNode.Y.Evaluate(context),
            gotoNode.Duration.Evaluate(context),
            ParseEase(gotoNode.Ease.Evaluate(context)));
          yield break;
        }

        case ScaleToNode scaleTo:
        {
          List<ScriptGameObjectRef> targets = scaleTo.Targets.Evaluate(context);
          animator?.StartScaleToObjects(
            targets,
            scaleTo.ScaleX.Evaluate(context),
            scaleTo.ScaleY.Evaluate(context),
            scaleTo.Duration.Evaluate(context),
            ParseEase(scaleTo.Ease.Evaluate(context)));
          yield break;
        }

        case TransparencyNode transparency:
          ApplyObjectTransparency(
            transparency.Targets.Evaluate(context),
            transparency.Value.Evaluate(context),
            transparency.Duration.Evaluate(context),
            ParseEase(transparency.Ease.Evaluate(context)));
          yield break;

        case VisibilityNode visibility:
          ApplyObjectTransparency(
            visibility.Targets.Evaluate(context),
            visibility.Visible.Evaluate(context) ? 1f : 0f,
            0f,
            LevelEase.Linear);
          yield break;

        case PlaySoundNode playSound:
          if (LevelSessionServer.Instance != null)
          {
            LevelSessionServer.Instance.ServerPlaySound(
              playSound.X.Evaluate(context),
              playSound.Y.Evaluate(context),
              playSound.SoundName.Evaluate(context),
              playSound.Volume.Evaluate(context));
          }
          else
          {
            LevelSoundPlayer.PlayAt(
              playSound.X.Evaluate(context),
              playSound.Y.Evaluate(context),
              playSound.SoundName.Evaluate(context),
              playSound.Volume.Evaluate(context));
          }
          yield break;

        case SpawnPointNode spawnPoint:
          ApplySpawnPoint(
            spawnPoint.Player.Evaluate(context),
            spawnPoint.X.Evaluate(context),
            spawnPoint.Y.Evaluate(context));
          yield break;

        case HitboxNode hitbox:
          ApplyObjectHitbox(hitbox.Targets.Evaluate(context), hitbox.Enabled.Evaluate(context));
          yield break;

        case AddComponentNode addComponent:
          ApplyAddComponent(addComponent.Targets.Evaluate(context), addComponent.ComponentName.Evaluate(context));
          yield break;

        case RemoveComponentNode removeComponent:
          ApplyRemoveComponent(removeComponent.Targets.Evaluate(context), removeComponent.ComponentName.Evaluate(context));
          yield break;

        case SetTextureNode setTexture:
          ApplySetTexture(setTexture.Targets.Evaluate(context), setTexture.TextureId.Evaluate(context));
          yield break;

        case SetColorNode setColor:
          ApplySetColor(setColor.Targets.Evaluate(context), setColor.HexColor.Evaluate(context));
          yield break;

        case PrintNode printNode:
          ExecutePrint(printNode, context);
          yield break;

        case DestroyNode destroy:
          ApplyDestroy(destroy.Targets.Evaluate(context));
          yield break;

        case BackgroundNode bgNode:
          LevelScriptObjectUtility.ApplyBackgroundAction(
            bgNode.Kind,
            bgNode.LayerIndex != null ? bgNode.LayerIndex.Evaluate(context) : 0,
            bgNode.HexColor != null ? bgNode.HexColor.Evaluate(context) : null,
            bgNode.Value != null ? bgNode.Value.Evaluate(context) : 0f);
          yield break;

        case CameraNode camNode:
          ExecuteCameraAction(camNode, context);
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

        case WorldNode worldNode:
          LevelScriptObjectUtility.ApplyWorldAction(
            worldNode.Kind,
            worldNode.GravityX != null ? worldNode.GravityX.Evaluate(context) : 0f,
            worldNode.GravityY != null ? worldNode.GravityY.Evaluate(context) : 0f,
            worldNode.TileX != null ? worldNode.TileX.Evaluate(context) : 0,
            worldNode.TileY != null ? worldNode.TileY.Evaluate(context) : 0,
            worldNode.TileId != null ? worldNode.TileId.Evaluate(context) : 0);
          yield break;

        case UINode uiNode:
          ExecuteUIAction(uiNode, context);
          yield break;



        case SendToClientNode sendToClient:
        {
          ScriptGameObjectRef targetRef = sendToClient.TargetPlayer != null
              ? sendToClient.TargetPlayer.Evaluate(context)
              : ScriptGameObjectRef.Null;
          string evtName = sendToClient.EventName != null ? sendToClient.EventName.Evaluate(context) : string.Empty;
          var args = new ScriptNetworkEventArg[sendToClient.Arguments.Count];
          for (int a = 0; a < args.Length; a++)
            args[a] = ScriptNetworkEventUtility.EvaluateArgument(sendToClient.Arguments[a], context);
          string json = ScriptNetworkEventUtility.SerializePayload(evtName, args);

          if (LevelScriptObjectUtility.TryResolvePlayer(targetRef.NetId, out NetworkPlayer np))
          {
            LevelSessionServer.Instance?.ServerSendScriptEventToPlayer(np, evtName, json);
          }
          yield break;
        }

        case SendToAllClientsNode sendToAll:
        {
          string evtName = sendToAll.EventName != null ? sendToAll.EventName.Evaluate(context) : string.Empty;
          var args = new ScriptNetworkEventArg[sendToAll.Arguments.Count];
          for (int a = 0; a < args.Length; a++)
            args[a] = ScriptNetworkEventUtility.EvaluateArgument(sendToAll.Arguments[a], context);
          string json = ScriptNetworkEventUtility.SerializePayload(evtName, args);

          LevelSessionServer.Instance?.ServerBroadcastScriptEvent(evtName, json);
          yield break;
        }

        case SendToServerNode sendToServer:
        {
          string evtName = sendToServer.EventName != null ? sendToServer.EventName.Evaluate(context) : string.Empty;
          var args = new ScriptNetworkEventArg[sendToServer.Arguments.Count];
          for (int a = 0; a < args.Length; a++)
            args[a] = ScriptNetworkEventUtility.EvaluateArgument(sendToServer.Arguments[a], context);
          string json = ScriptNetworkEventUtility.SerializePayload(evtName, args);

          var localRef = (Mirror.NetworkClient.localPlayer != null)
              ? new ScriptGameObjectRef(ScriptObjectKind.Player, Mirror.NetworkClient.localPlayer.netId)
              : ScriptGameObjectRef.Null;
          DispatchClientEvent(evtName, localRef, json);
          yield break;
        }

      }
    }

    static void ApplyComponentProperty(ComponentPropertyAssignmentNode node, LevelScriptExecutionContext context)
    {
      // If ComponentVariableName is set, get Component reference directly
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
          ApplyComponentProperty(targets[i], node, context);

        return;
      }

      ScriptGameObjectRef reference = context.GetGameObject(node.ObjectName);
      ApplyComponentProperty(reference, node, context);
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

    static void ApplyComponentProperty(
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

    static void ApplyObjectTransparency(
      IReadOnlyList<ScriptGameObjectRef> objects,
      float value,
      float duration,
      LevelEase ease)
    {
      LevelGridServer grid = LevelGridServer.Instance;
      if (grid == null)
        return;

      List<uint> netIds = LevelScriptObjectUtility.CollectTileNetIds(objects);
      if (netIds.Count == 0)
        return;

      float clampedValue = Mathf.Clamp01(value);
      if (duration <= 0f)
      {
        grid.TrySetTilesTransparencyByNetIds(netIds, clampedValue, updateBaseState: false);
        return;
      }

      LevelGroupAnimator.Instance?.StartTransparencyObjects(netIds, clampedValue, duration, ease);
    }

    static void ApplySpawnPoint(ScriptGameObjectRef player, float x, float y)
    {
      if (player.Kind != ScriptObjectKind.Player)
        return;

      if (!LevelScriptObjectUtility.TryResolvePlayer(player.NetId, out NetworkPlayer networkPlayer))
        return;

      if (!networkPlayer.TryGetComponent(out RunnerMovementServer runner))
        return;

      runner.ServerSetSpawnPoint(x, y);
    }

    static void ApplyObjectHitbox(IReadOnlyList<ScriptGameObjectRef> objects, bool enabled)
    {
      LevelGridServer grid = LevelGridServer.Instance;
      if (grid == null)
        return;

      List<uint> netIds = LevelScriptObjectUtility.CollectTileNetIds(objects);
      if (netIds.Count > 0)
        grid.TrySetTilesNoHitboxByNetIds(netIds, !enabled, updateBaseState: false);
    }

    static void ApplyAddComponent(IReadOnlyList<ScriptGameObjectRef> objects, string componentName)
    {
      LevelScriptRuntime runtime = LevelScriptRuntime.Instance;
      
      for (int i = 0; i < objects.Count; i++)
      {
        ScriptGameObjectRef obj = objects[i];
        LevelScriptObjectUtility.AddComponent(obj, componentName);
        
        // Регистрируем добавленный компонент для удаления при Stop
        if (runtime != null)
          runtime.RegisterAddedComponent(obj.NetId, componentName);
      }
    }

    static void ApplyRemoveComponent(IReadOnlyList<ScriptGameObjectRef> objects, string componentName)
    {
      for (int i = 0; i < objects.Count; i++)
        LevelScriptObjectUtility.RemoveComponent(objects[i], componentName);
    }

    static void ApplySetTexture(IReadOnlyList<ScriptGameObjectRef> objects, string textureId)
    {
      LevelGridServer grid = LevelGridServer.Instance;
      if (grid == null)
        return;

      List<uint> netIds = LevelScriptObjectUtility.CollectTileNetIds(objects);
      if (netIds.Count > 0)
        grid.TrySetTilesTextureByNetIds(netIds, textureId, updateBaseState: false);
    }

    static void ApplySetColor(IReadOnlyList<ScriptGameObjectRef> objects, string hex)
    {
      LevelGridServer grid = LevelGridServer.Instance;
      if (grid == null)
        return;

      Color32 color = LevelScriptObjectUtility.ParseHexColor(hex);
      List<uint> netIds = LevelScriptObjectUtility.CollectTileNetIds(objects);
      if (netIds.Count > 0)
        grid.TrySetTilesColorByNetIds(netIds, color, updateBaseState: false);
    }

    void ExecutePrint(PrintNode printNode, LevelScriptExecutionContext context)
    {
      string formatted = BuildPrintString(printNode, context);
      Debug.Log($"[LevelScript] {formatted}");
      ScriptPrint?.Invoke(formatted);
      if (LevelSessionServer.Instance != null)
        LevelSessionServer.Instance.ServerReportScriptPrint(formatted);
      else
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

    static void ApplyDestroy(IReadOnlyList<ScriptGameObjectRef> objects)
    {
      if (objects == null || objects.Count == 0)
        return;

      LevelScriptRuntime runtime = Instance;

      for (int i = 0; i < objects.Count; i++)
      {
        ScriptGameObjectRef obj = objects[i];
        
        // Не уничтожаем игроков
        if (obj.Kind == ScriptObjectKind.Player)
          continue;

        if (!LevelScriptObjectUtility.TryResolveTransform(obj, out Transform transform) || transform == null)
          continue;

        GameObject gameObject = transform.gameObject;
        if (gameObject == null)
          continue;

        bool isSceneObject = obj.Kind == ScriptObjectKind.SceneObject;

        // Для объектов сцены используем SetActive(false)
        if (isSceneObject)
        {
          if (runtime != null)
            runtime.RegisterDestroyedObject(obj, gameObject, isSceneObject: true);
          gameObject.SetActive(false);
        }
        else
        {
          if (gameObject.TryGetComponent(out LevelTileServer tileServer))
          {
            LevelTileRecord record = LevelGridServer.CreateRecordFromTile(tileServer);
            if (runtime != null)
              runtime.RegisterDestroyedTile(record);
            if (LevelGridServer.Instance != null && obj.Kind == ScriptObjectKind.Tile)
            {
              LevelGridServer.Instance.TryRemoveTileByNetId(obj.NetId);
              continue;
            }
          }

          // Используем NetworkServer.Destroy для сетевых объектов, Object.Destroy для остальных
          if (NetworkServer.active && gameObject.TryGetComponent<NetworkIdentity>(out _))
          {
            NetworkServer.Destroy(gameObject);
          }
          else
          {
            Object.Destroy(gameObject);
          }
        }
      }

      LevelScriptTriggerBridge.Instance?.InvalidateWatchCache();
    }

    IEnumerator ExecuteFunctionCall(FunctionCallNode call, LevelScriptExecutionContext context)
    {
      if (!context.TryGetFunctionAccessible(call.Name, out FunctionDefinitionNode definition, out string accessError))
        throw new System.InvalidOperationException(accessError ?? $"Unknown function '{call.Name}'.");

      if (call.Arguments.Count != definition.Parameters.Count)
      {
        throw new System.InvalidOperationException(
          $"Function '{call.Name}' expects {definition.Parameters.Count} arguments, got {call.Arguments.Count}.");
      }

      if (context.CallStackDepth >= LevelScriptExecutionContext.MaxCallStackDepth)
      {
        ReportScriptError($"Stack overflow: call stack exceeded depth of {LevelScriptExecutionContext.MaxCallStackDepth} in '{call.Name}'.");
        yield break;
      }

      context.CallStackDepth++;
      string previousModulePath = context.CurrentModulePath;
      bool previousWritesGlobals = context.WritesProgramGlobals;
      context.WritesProgramGlobals = false;
      context.PushScope();
      context.CurrentModulePath = definition.SourcePath ?? previousModulePath;
      try
      {
        for (int i = 0; i < definition.Parameters.Count; i++)
          BindFunctionArgument(context, definition.Parameters[i], call.Arguments[i], call.Name);

        for (int i = 0; i < definition.Body.Count; i++)
        {
          yield return ExecuteNode(definition.Body[i], context);
          if (context.HasReturn)
            break;
        }
      }
      finally
      {
        context.CallStackDepth--;
        if (context.HasReturn)
          context.HasReturn = false; // Return consumed by function call
        context.WritesProgramGlobals = previousWritesGlobals;
        context.CurrentModulePath = previousModulePath;
        context.PopScope();
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
        throw new System.InvalidOperationException(
          $"Function '{functionName}' argument '{parameter.Name}' expects {parameter.Kind}, got {val.Kind}.");
      }

      context.Declare(parameter.Name, val);
    }

    void LateUpdate()
    {
      if (cameraFollowTarget != null)
      {
        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam != null)
        {
          Vector3 p = cam.transform.position;
          cam.transform.position = new Vector3(cameraFollowTarget.position.x, cameraFollowTarget.position.y, p.z);
        }
      }
    }

    void ExecuteCameraAction(CameraNode node, LevelScriptExecutionContext context)
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
            if (LevelScriptObjectUtility.TryResolveTransform(targetRef, out Transform t))
              cameraFollowTarget = t;
          }
          break;
        }
        case CameraActionKind.Unfollow:
        {
          cameraFollowTarget = null;
          break;
        }
        case CameraActionKind.Fade:
        {
          float targetAlpha = node.Intensity != null ? node.Intensity.Evaluate(context) : 1f;
          float duration = node.Duration != null ? node.Duration.Evaluate(context) : 1f;
          string hexColor = node.HexColor != null ? node.HexColor.Evaluate(context) : "#000000";
          if (cameraFadeRoutine != null) StopCoroutine(cameraFadeRoutine);
          cameraFadeRoutine = StartCoroutine(FadeCameraRoutine(targetAlpha, duration, hexColor));
          break;
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
      while (elapsed < duration)
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
      while (elapsed < duration)
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
      GameObject go = new GameObject("LevelScriptRuntimeCanvas");
      canvas = go.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 999;
      go.AddComponent<UnityEngine.UI.CanvasScaler>();
      go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
      return canvas;
    }

    IEnumerator FadeCameraRoutine(float targetAlpha, float duration, string hexColor)
    {
      Canvas canvas = EnsureRuntimeCanvas();
      if (canvas == null) yield break;

      if (runtimeFadeOverlay == null)
      {
        runtimeFadeOverlay = new GameObject("RuntimeFadeOverlay");
        runtimeFadeOverlay.transform.SetParent(canvas.transform, false);
        RectTransform rt = runtimeFadeOverlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = runtimeFadeOverlay.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0);
      }

      var image = runtimeFadeOverlay.GetComponent<UnityEngine.UI.Image>();
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
      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        image.color = Color.Lerp(startColor, endColor, Mathf.Clamp01(elapsed / duration));
        yield return null;
      }
      image.color = endColor;
      cameraFadeRoutine = null;
    }

    void ExecuteUIAction(UINode node, LevelScriptExecutionContext context)
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

      GameObject bannerObj = new GameObject("RuntimeBanner");
      runtimeBannerOverlay = bannerObj;
      bannerObj.transform.SetParent(canvas.transform, false);

      RectTransform rt = bannerObj.AddComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.15f, 0.85f);
      rt.anchorMax = new Vector2(0.85f, 0.95f);
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;

      var bg = bannerObj.AddComponent<UnityEngine.UI.Image>();
      bg.color = new Color(0f, 0f, 0f, 0.75f);

      GameObject textObj = new GameObject("BannerText");
      textObj.transform.SetParent(bannerObj.transform, false);
      RectTransform textRt = textObj.AddComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = Vector2.zero;
      textRt.offsetMax = Vector2.zero;

      var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
      tmp.text = text;
      tmp.alignment = TMPro.TextAlignmentOptions.Center;
      tmp.fontSize = 28;
      Color col = Color.white;
      if (!string.IsNullOrEmpty(hexColor))
        ColorUtility.TryParseHtmlString(hexColor.StartsWith("#") ? hexColor : "#" + hexColor, out col);
      tmp.color = col;

      yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

      if (bannerObj != null)
        Destroy(bannerObj);
      bannerRoutine = null;
    }

    IEnumerator ShowTimerRoutine(float totalSeconds, string label)
    {
      Canvas canvas = EnsureRuntimeCanvas();
      if (canvas == null) yield break;

      if (runtimeTimerOverlay != null)
        Destroy(runtimeTimerOverlay);

      GameObject timerObj = new GameObject("RuntimeTimer");
      runtimeTimerOverlay = timerObj;
      timerObj.transform.SetParent(canvas.transform, false);

      RectTransform rt = timerObj.AddComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.35f, 0.9f);
      rt.anchorMax = new Vector2(0.65f, 0.98f);
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;

      var bg = timerObj.AddComponent<UnityEngine.UI.Image>();
      bg.color = new Color(0f, 0f, 0f, 0.7f);

      GameObject textObj = new GameObject("TimerText");
      textObj.transform.SetParent(timerObj.transform, false);
      RectTransform textRt = textObj.AddComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.offsetMin = Vector2.zero;
      textRt.offsetMax = Vector2.zero;

      var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
      tmp.alignment = TMPro.TextAlignmentOptions.Center;
      tmp.fontSize = 24;
      tmp.color = Color.yellow;

      float remaining = totalSeconds;
      while (remaining > 0f)
      {
        tmp.text = string.IsNullOrEmpty(label)
          ? $"{remaining:0.0}s"
          : $"{label}: {remaining:0.0}s";
        yield return null;
        remaining -= Time.deltaTime;
      }

      tmp.text = string.IsNullOrEmpty(label) ? "0.0s" : $"{label}: 0.0s";
      yield return new WaitForSeconds(0.5f);

      if (timerObj != null)
        Destroy(timerObj);
      timerRoutine = null;
    }
  }
}
