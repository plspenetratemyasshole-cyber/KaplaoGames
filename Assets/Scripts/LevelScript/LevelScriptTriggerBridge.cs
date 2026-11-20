using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
  public sealed class TriggerHandlerRegistration
  {
    public int Id;
    public TriggerHandlerNode Handler;
  }

  /// <summary>
  /// Detects overlap between script actors and watched GameObjects, then runs handlers.
  /// Optimized for many handlers (e.g. spawnPoints(1,22)): one physics query per actor per tick,
  /// with cached watch collider maps (no per-frame Group() rescans).
  /// </summary>
  [DisallowMultipleComponent]
  public class LevelScriptTriggerBridge : MonoBehaviour
  {
    public static LevelScriptTriggerBridge Instance { get; private set; }

    struct ContactKey : System.IEquatable<ContactKey>
    {
      public readonly int HandlerId;
      public readonly ScriptObjectKind ActorKind;
      public readonly uint ActorNetId;
      public readonly ScriptObjectKind WatchKind;
      public readonly uint WatchNetId;

      public ContactKey(
        int handlerId,
        ScriptObjectKind actorKind,
        uint actorNetId,
        ScriptObjectKind watchKind,
        uint watchNetId)
      {
        HandlerId = handlerId;
        ActorKind = actorKind;
        ActorNetId = actorNetId;
        WatchKind = watchKind;
        WatchNetId = watchNetId;
      }

      public bool Equals(ContactKey other) =>
        HandlerId == other.HandlerId &&
        ActorKind == other.ActorKind &&
        ActorNetId == other.ActorNetId &&
        WatchKind == other.WatchKind &&
        WatchNetId == other.WatchNetId;

      public override bool Equals(object obj) => obj is ContactKey other && Equals(other);

      public override int GetHashCode() =>
        System.HashCode.Combine(HandlerId, (int)ActorKind, ActorNetId, (int)WatchKind, WatchNetId);
    }

    sealed class HandlerWatchCache
    {
      public readonly Dictionary<int, ScriptGameObjectRef> WatchByColliderId = new();
      public readonly List<ScriptGameObjectRef> Watches = new();
      public bool IsSourceInternal;
    }

    LevelScriptRuntime runtime;
    readonly HashSet<ContactKey> activeContacts = new();
    readonly HashSet<ContactKey> frameContacts = new();
    readonly List<TriggerHandlerRegistration> handlers = new();
    readonly List<HandlerWatchCache> watchCaches = new();
    readonly List<ScriptGameObjectRef> runnerBuffer = new();
    readonly List<Collider2D> overlapHits = new(64);
    readonly Dictionary<int, List<int>> handlersByColliderId = new();
    ContactFilter2D overlapFilter;
    bool watchCacheDirty = true;

    void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(this);
        return;
      }

      Instance = this;
      overlapFilter = new ContactFilter2D();
      overlapFilter.NoFilter();
      overlapFilter.useTriggers = true;
    }

    void OnDestroy()
    {
      if (Instance == this)
        Instance = null;
    }

    public void Begin(LevelScriptRuntime scriptRuntime, IReadOnlyList<TriggerHandlerRegistration> registrations)
    {
      runtime = scriptRuntime;
      activeContacts.Clear();
      frameContacts.Clear();
      handlers.Clear();
      watchCaches.Clear();
      handlersByColliderId.Clear();
      watchCacheDirty = true;
      if (registrations != null)
        handlers.AddRange(registrations);
    }

    public void End()
    {
      runtime = null;
      activeContacts.Clear();
      frameContacts.Clear();
      handlers.Clear();
      watchCaches.Clear();
      handlersByColliderId.Clear();
      runnerBuffer.Clear();
      watchCacheDirty = true;
    }

    public void InvalidateWatchCache()
    {
      watchCacheDirty = true;
    }

    void FixedUpdate()
    {
      if (runtime == null || !runtime.IsRunning || handlers.Count == 0)
        return;

      EnsureWatchCaches();
      frameContacts.Clear();

      // Shared runner list once per tick (most handlers use Runners mode).
      LevelScriptObjectUtility.CollectAllRunnersNonAlloc(runnerBuffer);

      // One physics query per actor, then fan out to all interested handlers.
      for (int a = 0; a < runnerBuffer.Count; a++)
        ProcessActorOverlaps(runnerBuffer[a], TriggerActorMode.Runners);

      // SourceInternal handlers (tile-vs-tile) — rare; keep per-handler path.
      for (int h = 0; h < handlers.Count; h++)
      {
        if (handlers[h].Handler == null || handlers[h].Handler.ActorMode != TriggerActorMode.SourceInternal)
          continue;

        ProcessSourceInternalHandler(h);
      }

      foreach (ContactKey previous in activeContacts)
      {
        if (frameContacts.Contains(previous))
          continue;

        TriggerHandlerNode handler = FindHandler(previous.HandlerId);
        if (handler == null || handler.Kind != TriggerHandlerKind.Exit)
          continue;

        runtime.RunTriggerHandler(
          handler,
          new ScriptGameObjectRef(previous.ActorKind, previous.ActorNetId),
          new ScriptGameObjectRef(previous.WatchKind, previous.WatchNetId));
      }

      activeContacts.Clear();
      foreach (ContactKey contact in frameContacts)
        activeContacts.Add(contact);
    }

    void ProcessActorOverlaps(ScriptGameObjectRef actorRef, TriggerActorMode expectedMode)
    {
      Collider2D actorCollider = LevelScriptObjectUtility.TryGetCollider(actorRef);
      if (actorCollider == null || !actorCollider.enabled)
        return;

      overlapHits.Clear();
      int hitCount = actorCollider.Overlap(overlapFilter, overlapHits);
      for (int i = 0; i < hitCount; i++)
      {
        Collider2D hit = overlapHits[i];
        if (hit == null || hit == actorCollider)
          continue;

        int colliderId = hit.GetInstanceID();
        if (!handlersByColliderId.TryGetValue(colliderId, out List<int> handlerIndices))
          continue;

        for (int hi = 0; hi < handlerIndices.Count; hi++)
        {
          int handlerIndex = handlerIndices[hi];
          TriggerHandlerRegistration registration = handlers[handlerIndex];
          TriggerHandlerNode handler = registration.Handler;
          if (handler == null || handler.ActorMode != expectedMode)
            continue;

          HandlerWatchCache cache = watchCaches[handlerIndex];
          if (!cache.WatchByColliderId.TryGetValue(colliderId, out ScriptGameObjectRef watchRef))
            continue;

          var key = new ContactKey(
            registration.Id,
            actorRef.Kind,
            actorRef.NetId,
            watchRef.Kind,
            watchRef.NetId);
          frameContacts.Add(key);

          if (handler.Kind == TriggerHandlerKind.Enter && !activeContacts.Contains(key))
          {
            runtime.RunTriggerHandler(handler, actorRef, watchRef);
          }
          else if (handler.Kind == TriggerHandlerKind.Stay)
          {
            runtime.TryRunTriggerStay(handler, registration.Id, actorRef, watchRef);
          }
        }
      }
    }

    void ProcessSourceInternalHandler(int handlerIndex)
    {
      TriggerHandlerRegistration registration = handlers[handlerIndex];
      TriggerHandlerNode handler = registration.Handler;
      HandlerWatchCache cache = watchCaches[handlerIndex];
      if (cache.Watches.Count == 0)
        return;

      for (int a = 0; a < cache.Watches.Count; a++)
      {
        ScriptGameObjectRef actorRef = cache.Watches[a];
        Collider2D actorCollider = LevelScriptObjectUtility.TryGetCollider(actorRef);
        if (actorCollider == null || !actorCollider.enabled)
          continue;

        overlapHits.Clear();
        int hitCount = actorCollider.Overlap(overlapFilter, overlapHits);
        for (int i = 0; i < hitCount; i++)
        {
          Collider2D hit = overlapHits[i];
          if (hit == null || hit == actorCollider)
            continue;

          if (!cache.WatchByColliderId.TryGetValue(hit.GetInstanceID(), out ScriptGameObjectRef watchRef))
            continue;

          if (actorRef.Kind == watchRef.Kind && actorRef.NetId == watchRef.NetId)
            continue;

          var key = new ContactKey(
            registration.Id,
            actorRef.Kind,
            actorRef.NetId,
            watchRef.Kind,
            watchRef.NetId);
          frameContacts.Add(key);

          if (handler.Kind == TriggerHandlerKind.Enter && !activeContacts.Contains(key))
            runtime.RunTriggerHandler(handler, actorRef, watchRef);
          else if (handler.Kind == TriggerHandlerKind.Stay)
            runtime.TryRunTriggerStay(handler, registration.Id, actorRef, watchRef);
        }
      }
    }

    TriggerHandlerNode FindHandler(int handlerId)
    {
      for (int i = 0; i < handlers.Count; i++)
      {
        if (handlers[i].Id == handlerId)
          return handlers[i].Handler;
      }

      return null;
    }

    void EnsureWatchCaches()
    {
      if (!watchCacheDirty && watchCaches.Count == handlers.Count)
        return;

      watchCaches.Clear();
      handlersByColliderId.Clear();
      var context = new LevelScriptExecutionContext();

      for (int h = 0; h < handlers.Count; h++)
      {
        var cache = new HandlerWatchCache();
        watchCaches.Add(cache);

        TriggerHandlerNode handler = handlers[h].Handler;
        if (handler?.Source == null)
          continue;

        cache.IsSourceInternal = handler.ActorMode == TriggerActorMode.SourceInternal;
        List<ScriptGameObjectRef> watches = handler.Source.Evaluate(context);
        for (int w = 0; w < watches.Count; w++)
        {
          ScriptGameObjectRef watchRef = watches[w];
          cache.Watches.Add(watchRef);
          Collider2D watchCollider = LevelScriptObjectUtility.TryGetCollider(watchRef);
          if (watchCollider == null || !watchCollider.enabled)
            continue;

          int colliderId = watchCollider.GetInstanceID();
          cache.WatchByColliderId[colliderId] = watchRef;

          if (!handlersByColliderId.TryGetValue(colliderId, out List<int> list))
          {
            list = new List<int>(2);
            handlersByColliderId[colliderId] = list;
          }

          list.Add(h);
        }
      }

      watchCacheDirty = false;
    }
  }
}
