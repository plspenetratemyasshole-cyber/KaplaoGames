using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
    /// <summary>
    /// Runs Executor tiles on script Start/Run. One animation per target group at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExecutorRuntime : MonoBehaviour
    {
        public static ExecutorRuntime Instance { get; private set; }

        readonly HashSet<int> lockedGroups = new();
        readonly HashSet<uint> runningExecutors = new();
        readonly HashSet<uint> loopingExecutors = new();
        readonly List<Coroutine> loops = new();
        readonly List<ActiveMotion> motions = new();
        bool active;
        float nextScriptNetworkSyncTime;

        const float ScriptNetworkSyncInterval = 1f / 6f;

        sealed class TileMotionEntry
        {
            public LevelTileServer Tile;
            public Vector2 LocalOffset;
            public float BaseRotation;
            public Vector2 BaseScale;
        }

        sealed class ActiveMotion
        {
            public ExecutorTile Executor;
            public int TargetGroup;
            public List<LevelTileServer> Tiles;
            public readonly List<TileMotionEntry> Entries = new();
            public ExecutorGroupState StartState;
            public ExecutorGroupState EndState;
            public Vector2 BasePivot;
            public float StartTime;
            public float Duration;
            public LevelEase Ease;
            public int ExtraTurns;
            public bool ReturnEnabled;
            public float ReturnDelay;
            public bool Returning;
            public bool WaitingReturn;
            public float ReturnWaitUntil;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void BeginWithScript()
        {
            StopAll();
            active = true;

            ExecutorTile[] executors = FindObjectsByType<ExecutorTile>(FindObjectsSortMode.None);
            for (int i = 0; i < executors.Length; i++)
            {
                ExecutorTile executor = executors[i];
                if (executor == null || executor.SpawnedExecutor)
                    continue;

                TryBegin(executor);
                StartLoopIfNeeded(executor);
            }
        }

        public void StopAll()
        {
            active = false;
            for (int i = 0; i < loops.Count; i++)
            {
                if (loops[i] != null)
                    StopCoroutine(loops[i]);
            }

            // Restore tiles to their base positions when stopping
            foreach (ActiveMotion motion in motions)
            {
                if (motion?.Tiles == null)
                    continue;

                for (int t = 0; t < motion.Tiles.Count; t++)
                {
                    LevelTileServer tile = motion.Tiles[t];
                    if (tile != null)
                    {
                        // First detach from any script pivot to get proper world position
                        tile.DetachFromScriptPivot();
                        // Then restore to base state - this will snap the tile back to its original position
                        tile.ServerRestoreBaseState();
                    }
                }
            }

            loops.Clear();
            motions.Clear();
            lockedGroups.Clear();
            runningExecutors.Clear();
            loopingExecutors.Clear();
        }

        public bool IsTileInActiveMotion(LevelTileServer tile)
        {
            if (tile == null)
                return false;

            for (int i = 0; i < motions.Count; i++)
            {
                List<LevelTileServer> tiles = motions[i].Tiles;
                if (tiles == null)
                    continue;

                for (int t = 0; t < tiles.Count; t++)
                {
                    if (tiles[t] == tile)
                        return true;
                }
            }

            return false;
        }

        public void SpawnExecutorsFromObjects(List<ScriptGameObjectRef> objects)
        {
            if (!active || objects == null)
                return;

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Kind != ScriptObjectKind.Tile)
                    continue;

                if (!LevelScriptObjectUtility.TryResolveTile(objects[i].NetId, out LevelTileServer tile) || tile == null)
                    continue;

                if (!tile.TryGetComponent(out ExecutorTile executor) || !executor.SpawnedExecutor)
                    continue;

                TryBegin(executor);
                StartLoopIfNeeded(executor);
            }
        }

        void StartLoopIfNeeded(ExecutorTile executor)
        {
            if (executor == null || !executor.Loop || !loopingExecutors.Add(executor.netId))
                return;

            loops.Add(StartCoroutine(LoopExecutor(executor)));
        }

        IEnumerator LoopExecutor(ExecutorTile executor)
        {
            float every = Mathf.Max(
                executor.LoopEverySeconds,
                ExecutorTile.MinLoopEverySeconds(executor.Duration, executor.ReturnEnabled, executor.ReturnDelaySeconds));
            if (every <= 0f)
                every = 1f;

            while (active && executor != null)
            {
                yield return new WaitForSeconds(every);
                if (!active || executor == null)
                    yield break;

                TryBegin(executor);
            }
        }

        public bool TryBegin(ExecutorTile executor)
        {
            if (!active || executor == null || !NetworkServer.active)
                return false;

            if (runningExecutors.Contains(executor.netId))
                return false;

            int group = executor.TargetGroup;
            if (lockedGroups.Contains(group))
                return false;

            if (executor.UseKeyframes)
            {
                KeyframeAnimator keyframeAnimator = KeyframeAnimator.Instance;
                if (keyframeAnimator == null)
                    keyframeAnimator = gameObject.AddComponent<KeyframeAnimator>();

                bool closeLoop = KeyframeAnimator.GetCloseLoopForGroup(executor.KeyframesGroup);
                if (!keyframeAnimator.StartAnimateGroups(group, executor.KeyframesGroup, closeLoop))
                    return false;

                lockedGroups.Add(group);
                runningExecutors.Add(executor.netId);
                StartCoroutine(FinishAfter(executor, EstimateKeyframeDuration(executor.KeyframesGroup, closeLoop)));
                return true;
            }

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return false;

            var tiles = new List<LevelTileServer>();
            grid.CollectTilesWithGroup(group, tiles);
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                LevelTileServer tile = tiles[i];
                if (tile == null || tile.TileType == TileType.Executor || tile.TileType == TileType.Keyframe)
                    tiles.RemoveAt(i);
            }

            if (tiles.Count == 0)
                return false;

            Vector2 basePivot = ComputeRestPivot(tiles);
            ExecutorGroupState start = executor.StartState.hasValue
                ? executor.StartState
                : ExecutorGroupState.Identity(basePivot);
            ExecutorGroupState end = executor.EndState.hasValue
                ? executor.EndState
                : start;

            var motion = new ActiveMotion
            {
                Executor = executor,
                TargetGroup = group,
                Tiles = tiles,
                StartState = start,
                EndState = end,
                BasePivot = basePivot,
                StartTime = Time.time,
                Duration = Mathf.Max(0.0001f, executor.Duration),
                ExtraTurns = executor.X360,
                ReturnEnabled = executor.ReturnEnabled,
                ReturnDelay = Mathf.Max(0f, executor.ReturnDelaySeconds)
            };
            LevelEaseUtility.TryParse(executor.Anim, out motion.Ease);

            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                motion.Entries.Add(new TileMotionEntry
                {
                    Tile = tile,
                    LocalOffset = tile.RestPosition - basePivot,
                    BaseRotation = tile.RestRotationDegrees,
                    BaseScale = tile.RestScale
                });

                if (tile.IsAttachedToScriptPivot)
                    tile.DetachFromScriptPivot();
                tile.ApplyScriptTransform(Vector2.zero, 0f);
                tile.ServerBeginScriptDrivenMotion();
            }

            lockedGroups.Add(group);
            runningExecutors.Add(executor.netId);
            motions.Add(motion);
            ApplyMotion(motion, 0f);
            return true;
        }

        IEnumerator FinishAfter(ExecutorTile executor, float duration)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
            CompleteExecutor(executor, restore: false);
        }

        static float EstimateKeyframeDuration(int keyGroup, bool closeLoop)
        {
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return 1f;

            if (KeyframeAnimator.GetDistanceForGroup(keyGroup))
            {
                var tiles = new List<LevelTileServer>();
                grid.CollectTilesWithGroup(keyGroup, tiles);
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (tiles[i] != null && tiles[i].TryGetComponent(out KeyframeTile keyframe))
                        return Mathf.Max(0.01f, keyframe.Duration);
                }

                return 1f;
            }

            float total = 0f;
            float last = 0f;
            var keyframeTiles = new List<LevelTileServer>();
            grid.CollectTilesWithGroup(keyGroup, keyframeTiles);
            int count = 0;
            for (int i = 0; i < keyframeTiles.Count; i++)
            {
                if (keyframeTiles[i] != null && keyframeTiles[i].TryGetComponent(out KeyframeTile keyframe))
                {
                    last = Mathf.Max(0f, keyframe.Duration);
                    total += last;
                    count++;
                }
            }

            if (count < 2)
                return 1f;

            return closeLoop ? total : Mathf.Max(0.01f, total - last);
        }

        void LateUpdate()
        {
            if (!NetworkServer.active || motions.Count == 0)
                return;

            float now = Time.time;
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                ActiveMotion motion = motions[i];
                if (motion.WaitingReturn)
                {
                    if (now < motion.ReturnWaitUntil)
                        continue;

                    StartReturnMotion(motion, now);
                    continue;
                }

                float t = (now - motion.StartTime) / motion.Duration;
                if (t >= 1f)
                {
                    ApplyMotion(motion, 1f);
                    if (!motion.Returning && motion.ReturnEnabled)
                    {
                        motion.WaitingReturn = true;
                        motion.ReturnWaitUntil = now + motion.ReturnDelay;
                        continue;
                    }

                    CompleteMotion(motion);
                    motions.RemoveAt(i);
                    continue;
                }

                ApplyMotion(motion, LevelEaseUtility.Evaluate(motion.Ease, t));
            }

            if (motions.Count > 0 && now >= nextScriptNetworkSyncTime)
            {
                nextScriptNetworkSyncTime = now + ScriptNetworkSyncInterval;
                SyncActiveMotionsToClients();
            }
        }

        void SyncActiveMotionsToClients()
        {
            for (int i = 0; i < motions.Count; i++)
            {
                ActiveMotion motion = motions[i];
                if (motion?.Tiles == null)
                    continue;

                for (int t = 0; t < motion.Tiles.Count; t++)
                {
                    LevelTileServer tile = motion.Tiles[t];
                    if (tile != null)
                        tile.ServerSyncScriptStateFromCurrentTransform(roundValues: true);
                }
            }
        }

        void StartReturnMotion(ActiveMotion motion, float now)
        {
            motion.WaitingReturn = false;
            motion.Returning = true;
            ExecutorGroupState previousStart = motion.StartState;
            motion.StartState = motion.EndState;
            motion.EndState = previousStart;
            motion.ExtraTurns = 0;
            motion.StartTime = now;
            ApplyMotion(motion, 0f);
        }

        void ApplyMotion(ActiveMotion motion, float t)
        {
            ExecutorGroupState start = motion.StartState;
            ExecutorGroupState end = motion.EndState;

            float startScale = start.scale <= 0f ? 1f : start.scale;
            float endScale = end.scale <= 0f ? 1f : end.scale;
            Vector2 startPivot = start.hasValue ? start.Pivot : motion.BasePivot;
            Vector2 endPivot = end.hasValue ? end.Pivot : motion.BasePivot;
            Vector2 pivot = Vector2.LerpUnclamped(startPivot, endPivot, t);
            float endRotation = end.rotation + motion.ExtraTurns * 360f;
            float rotation = Mathf.LerpAngle(start.rotation, endRotation, t);
            float scale = Mathf.LerpUnclamped(startScale, endScale, t);
            Quaternion groupRotation = Quaternion.Euler(0f, 0f, rotation);

            for (int i = 0; i < motion.Entries.Count; i++)
            {
                TileMotionEntry entry = motion.Entries[i];
                LevelTileServer tile = entry.Tile;
                if (tile == null)
                    continue;

                Vector2 scaledLocal = entry.LocalOffset * scale;
                Vector2 worldPos = pivot + (Vector2)(groupRotation * scaledLocal);
                float scriptRotation = rotation;
                Vector2 basePos = tile.GetBaseWorldPosition();
                tile.ApplyScriptTransform(
                    new Vector2(worldPos.x - basePos.x, worldPos.y - basePos.y),
                    scriptRotation,
                    scale,
                    scale);
            }

            Physics2D.SyncTransforms();
        }

        void CompleteMotion(ActiveMotion motion)
        {
            if (motion?.Tiles != null)
            {
                bool snapForLoop = motion.Executor != null &&
                                   motion.Executor.Loop &&
                                   !motion.Returning &&
                                   !motion.ReturnEnabled;
                if (snapForLoop)
                    ApplyMotion(motion, 0f);

                for (int i = 0; i < motion.Tiles.Count; i++)
                {
                    LevelTileServer tile = motion.Tiles[i];
                    if (tile == null)
                        continue;

                    if (snapForLoop)
                        tile.ApplyScriptTransform(Vector2.zero, 0f);
                    else
                        tile.ServerSyncScriptStateFromCurrentTransform(roundValues: true);

                    tile.ServerEndScriptDrivenMotion();
                }
            }

            CompleteExecutor(motion?.Executor, restore: false);
        }

        void CompleteExecutor(ExecutorTile executor, bool restore)
        {
            if (executor == null)
                return;

            runningExecutors.Remove(executor.netId);
            lockedGroups.Remove(executor.TargetGroup);

            if (restore)
                return;

            if (executor.SpawnExecutorAtEnd)
                SpawnExecutorsInGroup(executor.SpawnAtEndGroup);
        }

        void SpawnExecutorsInGroup(int group)
        {
            if (group < 0)
                return;

            ExecutorTile[] executors = FindObjectsByType<ExecutorTile>(FindObjectsSortMode.None);
            for (int i = 0; i < executors.Length; i++)
            {
                ExecutorTile executor = executors[i];
                if (executor == null)
                    continue;

                LevelTileServer tile = executor.GetComponent<LevelTileServer>();
                if (tile == null || !tile.HasGroup(group))
                    continue;

                TryBegin(executor);
                StartLoopIfNeeded(executor);
            }
        }

        static Vector2 ComputeRestPivot(List<LevelTileServer> tiles)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null)
                    continue;
                sum += tiles[i].RestPosition;
                count++;
            }

            return count > 0 ? sum / count : Vector2.zero;
        }
    }
}
