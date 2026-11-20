using System.Collections.Generic;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
    /// <summary>
    /// animate() via Pos→Rot→Scale empties (same attach/sync pattern as LevelGroupAnimator).
    /// Position: group center moves by path delta from KF0 (no teleport).
    /// Rotation/scale: local channels on child empties so they cannot shove the path.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyframeAnimator : MonoBehaviour
    {
        public static KeyframeAnimator Instance { get; private set; }

        const int CurveSamples = 20;
        const float ScriptNetworkSyncInterval = 1f / 6f;

        readonly List<ActiveAnimation> activeAnimations = new();
        float nextScriptNetworkSyncTime;

        sealed class ActiveAnimation
        {
            public int TargetGroup;
            public int KeyGroup;
            public List<KeyframeSample> Keyframes;
            public int SegmentIndex;
            public float SegmentStartTime;
            public Vector2 RestPivotPosition;
            public Vector2 FirstKeyframePosition;
            public float FirstKeyframeRotation;
            public Vector2 FirstKeyframeScale;
            public bool CloseLoop;
            public bool Distance;
            public float[] SegmentCumulativeLengths;
            public readonly List<TileMotionEntry> Tiles = new();
        }

        sealed class TileMotionEntry
        {
            public LevelTileServer Tile;
            public Vector2 LocalOffset;
            public float BaseRotation;
            public Vector2 BaseScale;
        }

        public struct KeyframeSample
        {
            public Vector2 Position;
            public float Rotation;
            public Vector2 Scale;
            public float Duration;
            public LevelEase Ease;
            public bool Curve;
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

        void LateUpdate()
        {
            if (!NetworkServer.active || activeAnimations.Count == 0)
                return;

            float now = (float)NetworkTime.time;
            for (int i = activeAnimations.Count - 1; i >= 0; i--)
            {
                if (!AdvanceAnimation(activeAnimations[i], now))
                {
                    ActiveAnimation finished = activeAnimations[i];
                    FinalizeAnimation(finished, restoreBase: false);
                    NotifyClientsAnimationStopped(finished.TargetGroup, finished.KeyGroup);
                    activeAnimations.RemoveAt(i);
                }
            }

            if (activeAnimations.Count > 0 && now >= nextScriptNetworkSyncTime)
            {
                nextScriptNetworkSyncTime = now + ScriptNetworkSyncInterval;
                SyncActiveToClients();
            }
        }

        public void StopAll()
        {
            for (int a = 0; a < activeAnimations.Count; a++)
                FinalizeAnimation(activeAnimations[a], restoreBase: true);

            activeAnimations.Clear();
        }

        public void StartAnimateGroups(int targetGroup, int keyGroup)
        {
            StartAnimateGroups(targetGroup, keyGroup, GetCloseLoopForGroup(keyGroup));
        }

        public bool StartAnimateGroups(int targetGroup, int keyGroup, bool closeLoop)
        {
            if (!NetworkServer.active)
                return false;

            CollectKeyframes(keyGroup, out List<KeyframeSample> keyframes);
            if (keyframes.Count < 2)
            {
                Debug.LogWarning(
                    $"[KeyframeAnimator] animate needs >= 2 keyframes in group {keyGroup}, got {keyframes.Count}.");
                return false;
            }

            CollectGroupTiles(targetGroup, out List<LevelTileServer> targets);
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] == null || targets[i].TileType == TileType.Keyframe ||
                    targets[i].TileType == TileType.Executor)
                    targets.RemoveAt(i);
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning($"[KeyframeAnimator] animate target group {targetGroup} has no tiles.");
                return false;
            }

            for (int a = activeAnimations.Count - 1; a >= 0; a--)
            {
                if (SharesTiles(activeAnimations[a], targets))
                {
                    FinalizeAnimation(activeAnimations[a], restoreBase: true);
                    activeAnimations.RemoveAt(a);
                }
            }

            Vector2 center = Vector2.zero;
            var entries = new List<TileMotionEntry>();
            for (int i = 0; i < targets.Count; i++)
            {
                LevelTileServer tile = targets[i];
                if (tile.IsAttachedToScriptPivot)
                    tile.DetachFromScriptPivot();

                tile.ApplyScriptTransform(Vector2.zero, 0f);
                tile.ServerBeginScriptDrivenMotion();
                center += (Vector2)tile.transform.position;
                entries.Add(new TileMotionEntry
                {
                    Tile = tile,
                    BaseRotation = tile.PlacementRotationDegrees,
                    BaseScale = tile.Scale
                });
            }

            center /= targets.Count;
            Vector2 offsetSum = Vector2.zero;
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].LocalOffset = (Vector2)entries[i].Tile.transform.position - center;
                offsetSum += entries[i].LocalOffset;
            }

            Vector2 centroid = offsetSum / entries.Count;
            for (int i = 0; i < entries.Count; i++)
                entries[i].LocalOffset -= centroid;

            KeyframeSample first = keyframes[0];
            var anim = new ActiveAnimation
            {
                TargetGroup = targetGroup,
                KeyGroup = keyGroup,
                Keyframes = keyframes,
                SegmentIndex = 0,
                SegmentStartTime = (float)NetworkTime.time,
                RestPivotPosition = center,
                FirstKeyframePosition = first.Position,
                FirstKeyframeRotation = first.Rotation,
                FirstKeyframeScale = first.Scale,
                CloseLoop = closeLoop,
                Distance = GetDistanceForGroup(keyGroup),
                SegmentCumulativeLengths = GetDistanceForGroup(keyGroup)
                    ? BuildSegmentCumulativeLengths(keyframes, closeLoop)
                    : null
            };
            anim.Tiles.AddRange(entries);

            activeAnimations.Add(anim);
            ApplySegment(anim, 0, 0f);
            SyncAnimationNow(anim);
            NotifyClientsAnimationStarted(anim, targetGroup, keyGroup);
            return true;
        }

        public bool IsTileAnimating(LevelTileServer tile)
        {
            if (tile == null)
                return false;

            for (int a = 0; a < activeAnimations.Count; a++)
            {
                List<TileMotionEntry> targets = activeAnimations[a].Tiles;
                if (targets == null)
                    continue;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i].Tile == tile)
                        return true;
                }
            }

            return false;
        }

        void NotifyClientsAnimationStarted(ActiveAnimation anim, int targetGroup, int keyGroup)
        {
            if (anim?.Tiles == null || anim.Tiles.Count == 0)
                return;

            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            var netIds = new uint[anim.Tiles.Count];
            for (int i = 0; i < anim.Tiles.Count; i++)
            {
                LevelTileServer tile = anim.Tiles[i].Tile;
                netIds[i] = tile != null && tile.netIdentity != null ? tile.netIdentity.netId : 0u;
            }

            KeyframeSample first = anim.Keyframes[0];
            session.RpcKeyframeAnimationStarted(
                targetGroup,
                keyGroup,
                anim.CloseLoop,
                anim.Distance,
                anim.SegmentStartTime,
                anim.RestPivotPosition.x,
                anim.RestPivotPosition.y,
                first.Position.x,
                first.Position.y,
                first.Rotation,
                first.Scale.x,
                first.Scale.y,
                netIds);
        }

        void NotifyClientsAnimationStopped(int targetGroup, int keyGroup)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            session?.RpcKeyframeAnimationStopped(targetGroup, keyGroup);
        }

        public void StartAnimateObjects(List<ScriptGameObjectRef> targets, List<ScriptGameObjectRef> keyframes)
        {
            int targetGroup = ResolvePrimaryGroup(targets);
            int keyGroup = ResolvePrimaryGroup(keyframes);
            if (targetGroup < 0 || keyGroup < 0)
            {
                Debug.LogWarning(
                    $"[KeyframeAnimator] animate could not resolve groups (target={targetGroup}, key={keyGroup}).");
                return;
            }

            StartAnimateGroups(targetGroup, keyGroup, GetCloseLoopForGroup(keyGroup));
        }

        public static bool GetCloseLoopForGroup(int keyGroup)
        {
            if (keyGroup < 0)
                return false;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return false;

            var tiles = new List<LevelTileServer>();
            grid.CollectTilesWithGroup(keyGroup, tiles);
            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile == null || tile.TileType != TileType.Keyframe ||
                    !tile.TryGetComponent(out KeyframeTile keyframe))
                    continue;

                if (keyframe.CloseLoop)
                    return true;
            }

            return false;
        }

        public static bool GetDistanceForGroup(int keyGroup)
        {
            if (keyGroup < 0)
                return false;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return false;

            var tiles = new List<LevelTileServer>();
            grid.CollectTilesWithGroup(keyGroup, tiles);
            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile == null || tile.TileType != TileType.Keyframe ||
                    !tile.TryGetComponent(out KeyframeTile keyframe))
                    continue;

                if (keyframe.Distance)
                    return true;
            }

            return false;
        }

        static bool SharesTiles(ActiveAnimation anim, List<LevelTileServer> tiles)
        {
            if (anim?.Tiles == null || tiles == null)
                return false;

            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile == null)
                    continue;

                for (int j = 0; j < anim.Tiles.Count; j++)
                {
                    if (anim.Tiles[j].Tile == tile)
                        return true;
                }
            }

            return false;
        }

        static int ResolvePrimaryGroup(List<ScriptGameObjectRef> refs)
        {
            if (refs == null)
                return -1;

            for (int i = 0; i < refs.Count; i++)
            {
                if (refs[i].Kind != ScriptObjectKind.Tile)
                    continue;

                if (!LevelScriptObjectUtility.TryResolveTile(refs[i].NetId, out LevelTileServer tile) || tile == null)
                    continue;

                IReadOnlyList<int> groups = tile.CachedGroupIds;
                if (groups.Count > 0)
                    return groups[0];
            }

            return -1;
        }

        static void CollectGroupTiles(int group, out List<LevelTileServer> output)
        {
            output = new List<LevelTileServer>();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            grid.CollectTilesWithGroup(group, output);
        }

        static void CollectKeyframes(int group, out List<KeyframeSample> output)
        {
            output = new List<KeyframeSample>();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            var pairs = new List<(int spawnId, long createdAt, uint netId, KeyframeSample sample)>();
            var keyframeTiles = new List<LevelTileServer>();
            grid.CollectTilesWithGroup(group, keyframeTiles);
            for (int i = 0; i < keyframeTiles.Count; i++)
            {
                LevelTileServer tile = keyframeTiles[i];
                if (tile == null || tile.TileType != TileType.Keyframe)
                    continue;

                if (!tile.TryGetComponent(out KeyframeTile keyframeTile))
                    continue;

                LevelEaseUtility.TryParse(keyframeTile.Anim, out LevelEase ease);
                uint netId = tile.netIdentity != null ? tile.netIdentity.netId : 0u;

                // Same source as KeyframePathVisualizer so motion matches the yellow path.
                pairs.Add((keyframeTile.SpawnId, tile.CreatedAtUnixMs, netId, new KeyframeSample
                {
                    Position = tile.Position,
                    Rotation = tile.PlacementRotationDegrees,
                    Scale = tile.Scale,
                    Duration = keyframeTile.Duration,
                    Ease = ease,
                    Curve = keyframeTile.Curve
                }));
            }

            pairs.Sort((a, b) =>
            {
                int cmp = a.spawnId.CompareTo(b.spawnId);
                if (cmp != 0) return cmp;
                cmp = a.createdAt.CompareTo(b.createdAt);
                if (cmp != 0) return cmp;
                return a.netId.CompareTo(b.netId);
            });

            for (int i = 0; i < pairs.Count; i++)
                output.Add(pairs[i].sample);
        }

        bool AdvanceAnimation(ActiveAnimation anim, float now)
        {
            if (anim.Keyframes == null || anim.Keyframes.Count < 2)
                return false;

            if (anim.Distance)
                return AdvanceDistanceAnimation(anim, now);

            int lastIndex = anim.Keyframes.Count - 1;
            int segmentCount = anim.CloseLoop ? anim.Keyframes.Count : lastIndex;
            while (anim.SegmentIndex < segmentCount)
            {
                KeyframeSample from = anim.Keyframes[anim.SegmentIndex];
                float duration = Mathf.Max(0.0001f, from.Duration);
                float t = (now - anim.SegmentStartTime) / duration;
                if (t < 1f)
                {
                    float eased = LevelEaseUtility.Evaluate(from.Ease, Mathf.Clamp01(t));
                    ApplySegment(anim, anim.SegmentIndex, eased);
                    return true;
                }

                ApplySegment(anim, anim.SegmentIndex, 1f);
                anim.SegmentIndex++;
                anim.SegmentStartTime = now;
            }

            return false;
        }

        bool AdvanceDistanceAnimation(ActiveAnimation anim, float now)
        {
            float totalDuration = Mathf.Max(0.0001f, anim.Keyframes[0].Duration);
            float u = (now - anim.SegmentStartTime) / totalDuration;
            if (u >= 1f)
            {
                ApplyAtPathProgress(anim, 1f);
                return false;
            }

            float eased = LevelEaseUtility.Evaluate(anim.Keyframes[0].Ease, u);
            ApplyAtPathProgress(anim, eased);
            return true;
        }

        static void ApplyAtPathProgress(ActiveAnimation anim, float pathProgress)
        {
            pathProgress = Mathf.Clamp01(pathProgress);
            int segmentCount = anim.CloseLoop ? anim.Keyframes.Count : anim.Keyframes.Count - 1;
            if (segmentCount <= 0)
                return;

            float[] cumulative = anim.SegmentCumulativeLengths;
            if (cumulative == null || cumulative.Length != segmentCount + 1)
                cumulative = BuildSegmentCumulativeLengths(anim.Keyframes, anim.CloseLoop);

            float totalLength = cumulative[segmentCount];
            if (totalLength <= 0.0001f)
            {
                ApplySegment(anim, 0, pathProgress);
                return;
            }

            float target = pathProgress * totalLength;
            int segmentIndex = 0;
            while (segmentIndex < segmentCount - 1 && cumulative[segmentIndex + 1] < target)
                segmentIndex++;

            float segmentStart = cumulative[segmentIndex];
            float segmentEnd = cumulative[segmentIndex + 1];
            float segmentLength = segmentEnd - segmentStart;
            float localT = segmentLength > 0.0001f ? (target - segmentStart) / segmentLength : 0f;
            ApplySegment(anim, segmentIndex, localT);
        }

        static float[] BuildSegmentCumulativeLengths(IReadOnlyList<KeyframeSample> keyframes, bool closeLoop)
        {
            int segmentCount = closeLoop ? keyframes.Count : keyframes.Count - 1;
            var cumulative = new float[segmentCount + 1];
            cumulative[0] = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                int nextIndex = i + 1;
                if (nextIndex >= keyframes.Count)
                    nextIndex = 0;
                cumulative[i + 1] = cumulative[i] + MeasureSegmentLength(keyframes, i, nextIndex, closeLoop);
            }

            return cumulative;
        }

        static float MeasureSegmentLength(
            IReadOnlyList<KeyframeSample> keyframes,
            int fromIndex,
            int toIndex,
            bool closeLoop)
        {
            const int samples = 12;
            EvaluatePose(keyframes, fromIndex, toIndex, closeLoop, 0f,
                out Vector2 previous, out _, out _);
            float length = 0f;
            for (int s = 1; s <= samples; s++)
            {
                float u = s / (float)samples;
                EvaluatePose(keyframes, fromIndex, toIndex, closeLoop, u,
                    out Vector2 current, out _, out _);
                length += Vector2.Distance(previous, current);
                previous = current;
            }

            return length;
        }

        static void ApplySegment(ActiveAnimation anim, int segmentIndex, float t)
        {
            if (anim.Keyframes == null || segmentIndex < 0 || segmentIndex >= anim.Keyframes.Count)
                return;

            int nextIndex = segmentIndex + 1;
            if (nextIndex >= anim.Keyframes.Count)
            {
                if (!anim.CloseLoop)
                    return;
                nextIndex = 0;
            }

            EvaluatePose(anim.Keyframes, segmentIndex, nextIndex, anim.CloseLoop, t,
                out Vector2 pathPos, out float pathRot, out Vector2 pathScale);

            Vector2 pathDelta = pathPos - anim.FirstKeyframePosition;
            float rotOffset = Mathf.DeltaAngle(anim.FirstKeyframeRotation, pathRot);
            Vector2 scaleRatio = new Vector2(
                SafeRatio(pathScale.x, anim.FirstKeyframeScale.x),
                SafeRatio(pathScale.y, anim.FirstKeyframeScale.y));

            Vector2 worldCenter = anim.RestPivotPosition + pathDelta;
            Quaternion rot = Quaternion.Euler(0f, 0f, rotOffset);

            for (int i = 0; i < anim.Tiles.Count; i++)
            {
                TileMotionEntry entry = anim.Tiles[i];
                if (entry.Tile == null)
                    continue;

                Vector2 scaledLocal = Vector2.Scale(entry.LocalOffset, scaleRatio);
                Vector2 rotatedLocal = rot * scaledLocal;
                Vector2 worldPos = worldCenter + rotatedLocal;
                float scriptRot = rotOffset;
                Vector2 scriptScale = scaleRatio;

                Vector2 basePos = entry.Tile.GetBaseWorldPosition();
                entry.Tile.ApplyScriptTransform(
                    new Vector2(worldPos.x - basePos.x, worldPos.y - basePos.y),
                    scriptRot,
                    scriptScale.x,
                    scriptScale.y);
            }

            Physics2D.SyncTransforms();
        }

        void SyncActiveToClients()
        {
            for (int a = 0; a < activeAnimations.Count; a++)
                SyncAnimationNow(activeAnimations[a]);
        }

        static void SyncAnimationNow(ActiveAnimation anim)
        {
            if (anim?.Tiles == null)
                return;

            for (int i = 0; i < anim.Tiles.Count; i++)
            {
                LevelTileServer tile = anim.Tiles[i].Tile;
                if (tile != null)
                    tile.ServerSyncScriptStateFromCurrentTransform(roundValues: true);
            }
        }

        static void FinalizeAnimation(ActiveAnimation anim, bool restoreBase)
        {
            if (anim == null)
                return;

            if (anim.Tiles != null)
            {
                for (int i = 0; i < anim.Tiles.Count; i++)
                {
                    LevelTileServer tile = anim.Tiles[i].Tile;
                    if (tile == null)
                        continue;

                    if (tile.IsAttachedToScriptPivot)
                        tile.DetachFromScriptPivot();

                    if (restoreBase)
                    {
                        tile.ApplyScriptTransform(Vector2.zero, 0f);
                        tile.ServerSetScale(anim.Tiles[i].BaseScale);
                    }
                    else
                    {
                        tile.ServerSyncScriptStateFromCurrentTransform(roundValues: true);
                    }

                    tile.ServerEndScriptDrivenMotion();
                }
            }
        }

        public static void EvaluatePose(
            IReadOnlyList<KeyframeSample> keyframes,
            int segmentIndex,
            float t,
            out Vector2 position,
            out float rotation,
            out Vector2 scale)
        {
            EvaluatePose(keyframes, segmentIndex, segmentIndex + 1, false, t,
                out position, out rotation, out scale);
        }

        public static void EvaluatePose(
            IReadOnlyList<KeyframeSample> keyframes,
            int fromIndex,
            int toIndex,
            float t,
            out Vector2 position,
            out float rotation,
            out Vector2 scale)
        {
            EvaluatePose(keyframes, fromIndex, toIndex, false, t, out position, out rotation, out scale);
        }

        public static void EvaluatePose(
            IReadOnlyList<KeyframeSample> keyframes,
            int fromIndex,
            int toIndex,
            bool closeLoop,
            float t,
            out Vector2 position,
            out float rotation,
            out Vector2 scale)
        {
            KeyframeSample from = keyframes[fromIndex];
            KeyframeSample to = keyframes[toIndex];
            t = Mathf.Clamp01(t);

            bool curved = from.Curve || to.Curve;
            bool adjacent = toIndex == fromIndex + 1 ||
                            (closeLoop && fromIndex == keyframes.Count - 1 && toIndex == 0);
            position = curved && adjacent
                ? CatmullRomSegment(keyframes, fromIndex, toIndex, closeLoop, t)
                : Vector2.LerpUnclamped(from.Position, to.Position, t);

            rotation = from.Rotation + Mathf.DeltaAngle(from.Rotation, to.Rotation) * t;
            scale = Vector2.LerpUnclamped(from.Scale, to.Scale, t);
        }

        public static void BuildPathPoints(IReadOnlyList<KeyframeSample> keyframes, List<Vector3> points)
        {
            BuildPathPoints(keyframes, points, false);
        }

        public static void BuildPathPoints(
            IReadOnlyList<KeyframeSample> keyframes,
            List<Vector3> points,
            bool closeLoop)
        {
            points.Clear();
            if (keyframes == null || keyframes.Count == 0)
                return;

            points.Add(new Vector3(keyframes[0].Position.x, keyframes[0].Position.y, 0f));
            int lastOpenSegment = keyframes.Count - 2;
            for (int i = 0; i <= lastOpenSegment; i++)
                AppendSegmentPoints(keyframes, i, i + 1, closeLoop, points);

            if (closeLoop && keyframes.Count >= 2)
                AppendSegmentPoints(keyframes, keyframes.Count - 1, 0, true, points);
        }

        static void AppendSegmentPoints(
            IReadOnlyList<KeyframeSample> keyframes,
            int fromIndex,
            int toIndex,
            bool closeLoop,
            List<Vector3> points)
        {
            KeyframeSample from = keyframes[fromIndex];
            KeyframeSample to = keyframes[toIndex];
            bool curved = from.Curve || to.Curve;
            if (curved)
            {
                for (int s = 1; s <= CurveSamples; s++)
                {
                    float u = s / (float)CurveSamples;
                    Vector2 p = CatmullRomSegment(keyframes, fromIndex, toIndex, closeLoop, u);
                    points.Add(new Vector3(p.x, p.y, 0f));
                }
            }
            else
            {
                points.Add(new Vector3(to.Position.x, to.Position.y, 0f));
            }
        }

        static Vector2 CatmullRomSegment(
            IReadOnlyList<KeyframeSample> keyframes,
            int fromIndex,
            int toIndex,
            bool closeLoop,
            float t)
        {
            int count = keyframes.Count;
            if (closeLoop)
            {
                int Wrap(int index) => ((index % count) + count) % count;
                Vector2 p0 = keyframes[Wrap(fromIndex - 1)].Position;
                Vector2 p1 = keyframes[fromIndex].Position;
                Vector2 p2 = keyframes[toIndex].Position;
                Vector2 p3 = keyframes[Wrap(fromIndex + 2)].Position;
                return CatmullRom(p0, p1, p2, p3, t);
            }

            Vector2 openP0 = keyframes[Mathf.Max(fromIndex - 1, 0)].Position;
            Vector2 openP1 = keyframes[fromIndex].Position;
            Vector2 openP2 = keyframes[toIndex].Position;
            Vector2 openP3 = keyframes[Mathf.Min(fromIndex + 2, count - 1)].Position;
            return CatmullRom(openP0, openP1, openP2, openP3, t);
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static float SafeRatio(float to, float from) => Mathf.Abs(from) < 0.0001f ? 1f : to / from;
    }
}
