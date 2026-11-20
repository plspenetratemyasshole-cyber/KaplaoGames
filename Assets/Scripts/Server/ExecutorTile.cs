using System;
using Mirror;
using UnityEngine;
using KaplaoGames.Shared;

namespace KaplaoGames.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public class ExecutorTile : NetworkBehaviour
    {
        [SyncVar] int syncedTargetGroup = 1;
        [SyncVar] float syncedDuration = 1f;
        [SyncVar] string syncedAnim = "linear";
        [SyncVar] int syncedX360;
        [SyncVar] bool syncedSpawnedExecutor;
        [SyncVar] bool syncedSpawnExecutorAtEnd;
        [SyncVar] int syncedSpawnAtEndGroup;
        [SyncVar] bool syncedLoop;
        [SyncVar] float syncedLoopEverySeconds = 2f;
        [SyncVar] bool syncedUseKeyframes;
        [SyncVar] int syncedKeyframesGroup = 1;
        [SyncVar] bool syncedCloseLoop;
        [SyncVar] bool syncedReturnEnabled;
        [SyncVar] float syncedReturnDelaySeconds;

        [SyncVar] bool syncedHasStartState;
        [SyncVar] Vector2 syncedStartPivot;
        [SyncVar] float syncedStartRotation;
        [SyncVar] float syncedStartScale = 1f;

        [SyncVar] bool syncedHasEndState;
        [SyncVar] Vector2 syncedEndPivot;
        [SyncVar] float syncedEndRotation;
        [SyncVar] float syncedEndScale = 1f;

        public int TargetGroup => syncedTargetGroup;
        public float Duration => syncedDuration;
        public string Anim => syncedAnim;
        public int X360 => syncedX360;
        public bool SpawnedExecutor => syncedSpawnedExecutor;
        public bool SpawnExecutorAtEnd => syncedSpawnExecutorAtEnd;
        public int SpawnAtEndGroup => syncedSpawnAtEndGroup;
        public bool Loop => syncedLoop;
        public float LoopEverySeconds => syncedLoopEverySeconds;
        public bool UseKeyframes => syncedUseKeyframes;
        public int KeyframesGroup => syncedKeyframesGroup;
        public bool CloseLoop => syncedCloseLoop;
        public bool ReturnEnabled => syncedReturnEnabled;
        public float ReturnDelaySeconds => syncedReturnDelaySeconds;

        public ExecutorGroupState StartState => new ExecutorGroupState
        {
            hasValue = syncedHasStartState,
            pivotX = syncedStartPivot.x,
            pivotY = syncedStartPivot.y,
            rotation = syncedStartRotation,
            scale = syncedStartScale
        };

        public ExecutorGroupState EndState => new ExecutorGroupState
        {
            hasValue = syncedHasEndState,
            pivotX = syncedEndPivot.x,
            pivotY = syncedEndPivot.y,
            rotation = syncedEndRotation,
            scale = syncedEndScale
        };

        public ExecutorRecord ExportRecord()
        {
            return new ExecutorRecord
            {
                targetGroup = syncedTargetGroup,
                duration = syncedDuration,
                anim = syncedAnim,
                x360 = syncedX360,
                spawnedExecutor = syncedSpawnedExecutor,
                spawnExecutorAtEnd = syncedSpawnExecutorAtEnd,
                spawnAtEndGroup = syncedSpawnAtEndGroup,
                loop = syncedLoop,
                loopEverySeconds = syncedLoopEverySeconds,
                useKeyframes = syncedUseKeyframes,
                keyframesGroup = syncedKeyframesGroup,
                closeLoop = syncedCloseLoop,
                returnEnabled = syncedReturnEnabled,
                returnDelaySeconds = syncedReturnDelaySeconds,
                startState = StartState,
                endState = EndState
            };
        }

        [Server]
        public void ServerApplyDefaults()
        {
            ServerApply(new ExecutorRecord
            {
                targetGroup = 1,
                duration = 1f,
                anim = "linear",
                loopEverySeconds = 2f,
                keyframesGroup = 1,
                startState = default,
                endState = default
            });
        }

        [Server]
        public void ServerApplySettings(ExecutorTileSettings settings)
        {
            if (settings == null)
                return;

            // Patch-style: only overwrite fields the client marked as present.
            // Prevents multi-select / empty UI from wiping saved values with defaults.
            if (settings.hasTargetGroup)
                syncedTargetGroup = Mathf.Max(0, settings.targetGroup);
            if (settings.hasDuration)
                syncedDuration = Mathf.Max(0f, settings.duration);
            if (settings.hasAnim)
                syncedAnim = string.IsNullOrWhiteSpace(settings.anim) ? "linear" : settings.anim.Trim().ToLowerInvariant();
            if (settings.hasX360)
                syncedX360 = settings.x360;
            if (settings.hasSpawnedExecutor)
                syncedSpawnedExecutor = settings.spawnedExecutor;
            if (settings.hasSpawnExecutorAtEnd)
                syncedSpawnExecutorAtEnd = settings.spawnExecutorAtEnd;
            if (settings.hasSpawnAtEndGroup)
                syncedSpawnAtEndGroup = Mathf.Max(0, settings.spawnAtEndGroup);
            if (settings.hasLoop)
                syncedLoop = settings.loop;
            if (settings.hasLoopEverySeconds)
                syncedLoopEverySeconds = Mathf.Max(0f, settings.loopEverySeconds);
            if (settings.hasUseKeyframes)
                syncedUseKeyframes = settings.useKeyframes;
            if (settings.hasKeyframesGroup)
                syncedKeyframesGroup = Mathf.Max(0, settings.keyframesGroup);
            if (settings.hasCloseLoop)
                syncedCloseLoop = settings.closeLoop;
            if (settings.hasReturnEnabled)
                syncedReturnEnabled = settings.returnEnabled && !syncedUseKeyframes;
            else if (settings.hasUseKeyframes && syncedUseKeyframes)
                syncedReturnEnabled = false;
            if (settings.hasReturnDelaySeconds)
                syncedReturnDelaySeconds = Mathf.Max(0f, settings.returnDelaySeconds);

            syncedLoopEverySeconds = ClampLoopEvery(
                syncedLoop,
                syncedLoopEverySeconds,
                syncedDuration,
                syncedReturnEnabled,
                syncedReturnDelaySeconds);
        }

        [Server]
        public void ServerApplyStates(ExecutorGroupState start, ExecutorGroupState end)
        {
            ApplyState(start, out syncedHasStartState, out syncedStartPivot, out syncedStartRotation, out syncedStartScale);
            ApplyState(end, out syncedHasEndState, out syncedEndPivot, out syncedEndRotation, out syncedEndScale);
        }

        [Server]
        public void ServerApply(ExecutorRecord record)
        {
            syncedTargetGroup = Mathf.Max(0, record.targetGroup);
            syncedDuration = Mathf.Max(0f, record.duration <= 0f && record.useKeyframes ? 0f : Mathf.Max(record.duration, 0f));
            if (syncedDuration <= 0f && !record.useKeyframes)
                syncedDuration = 1f;
            syncedAnim = string.IsNullOrWhiteSpace(record.anim) ? "linear" : record.anim.Trim().ToLowerInvariant();
            syncedX360 = record.x360;
            syncedSpawnedExecutor = record.spawnedExecutor;
            syncedSpawnExecutorAtEnd = record.spawnExecutorAtEnd;
            syncedSpawnAtEndGroup = Mathf.Max(0, record.spawnAtEndGroup);
            syncedLoop = record.loop;
            syncedLoopEverySeconds = Mathf.Max(0f, record.loopEverySeconds);
            syncedUseKeyframes = record.useKeyframes;
            syncedKeyframesGroup = Mathf.Max(0, record.keyframesGroup);
            syncedCloseLoop = record.closeLoop;
            syncedReturnEnabled = record.returnEnabled && !record.useKeyframes;
            syncedReturnDelaySeconds = Mathf.Max(0f, record.returnDelaySeconds);
            syncedLoopEverySeconds = ClampLoopEvery(
                syncedLoop,
                syncedLoopEverySeconds,
                syncedDuration,
                syncedReturnEnabled,
                syncedReturnDelaySeconds);
            ServerApplyStates(record.startState, record.endState);
        }

        public static float MinLoopEverySeconds(float duration, bool returnEnabled, float returnDelaySeconds)
        {
            float oneWay = Mathf.Max(0f, duration);
            if (!returnEnabled)
                return oneWay;
            return oneWay * 2f + Mathf.Max(0f, returnDelaySeconds);
        }

        static float ClampLoopEvery(bool loop, float loopEvery, float duration, bool returnEnabled, float returnDelay)
        {
            if (!loop)
                return Mathf.Max(0f, loopEvery);
            return Mathf.Max(MinLoopEverySeconds(duration, returnEnabled, returnDelay), loopEvery);
        }

        static void ApplyState(
            ExecutorGroupState state,
            out bool hasValue,
            out Vector2 pivot,
            out float rotation,
            out float scale)
        {
            hasValue = state.hasValue;
            pivot = new Vector2(state.pivotX, state.pivotY);
            rotation = state.rotation;
            scale = state.scale <= 0f ? 1f : state.scale;
        }
    }

    [Serializable]
    public class ExecutorTileSettings
    {
        public bool hasTargetGroup;
        public int targetGroup = 1;
        public bool hasDuration;
        public float duration = 1f;
        public bool hasAnim;
        public string anim = "linear";
        public bool hasX360;
        public int x360;
        public bool hasSpawnedExecutor;
        public bool spawnedExecutor;
        public bool hasSpawnExecutorAtEnd;
        public bool spawnExecutorAtEnd;
        public bool hasSpawnAtEndGroup;
        public int spawnAtEndGroup;
        public bool hasLoop;
        public bool loop;
        public bool hasLoopEverySeconds;
        public float loopEverySeconds = 2f;
        public bool hasUseKeyframes;
        public bool useKeyframes;
        public bool hasKeyframesGroup;
        public int keyframesGroup = 1;
        public bool hasCloseLoop;
        public bool closeLoop;
        public bool hasReturnEnabled;
        public bool returnEnabled;
        public bool hasReturnDelaySeconds;
        public float returnDelaySeconds;

        public bool HasAnyField =>
            hasTargetGroup || hasDuration || hasAnim || hasX360 ||
            hasSpawnedExecutor || hasSpawnExecutorAtEnd || hasSpawnAtEndGroup ||
            hasLoop || hasLoopEverySeconds || hasUseKeyframes || hasKeyframesGroup ||
            hasCloseLoop || hasReturnEnabled || hasReturnDelaySeconds;
    }
}
