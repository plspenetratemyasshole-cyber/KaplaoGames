using System.Collections.Generic;
using KaplaoGames.LevelScript;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Client-side keyframe animation prediction. Runs the same pose math as the server
    /// between throttled SyncVar updates so high-latency clients see smooth motion.
    /// Server state still wins when sync arrives (small snap is acceptable).
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyframeMotionPredictor : MonoBehaviour
    {
        public static KeyframeMotionPredictor Instance { get; private set; }

        readonly List<PredictedAnimation> active = new();

        sealed class PredictedAnimation
        {
            public int TargetGroup;
            public int KeyGroup;
            public bool CloseLoop;
            public bool Distance;
            public float StartTime;
            public Vector2 RestPivot;
            public Vector2 FirstKeyframePosition;
            public float FirstKeyframeRotation;
            public Vector2 FirstKeyframeScale;
            public List<KeyframeAnimator.KeyframeSample> Keyframes;
            public float[] SegmentCumulativeLengths;
            public readonly List<TileEntry> Tiles = new();
            public int SegmentIndex;
            public float SegmentStartTime;
        }

        sealed class TileEntry
        {
            public LevelTileServer Tile;
            public Vector2 LocalOffset;
            public float BaseRotation;
            public Vector2 BaseScale;
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
            if (!NetworkClient.active || NetworkServer.active || active.Count == 0)
                return;

            float now = (float)NetworkTime.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (!AdvancePrediction(active[i], now))
                    active.RemoveAt(i);
            }
        }

        public static void HandleAnimationStarted(
            int targetGroup,
            int keyGroup,
            bool closeLoop,
            bool distance,
            float startTime,
            float restPivotX,
            float restPivotY,
            float firstPosX,
            float firstPosY,
            float firstRot,
            float firstScaleX,
            float firstScaleY,
            uint[] targetNetIds)
        {
            EnsureInstance();

            RemoveAnimationsForGroups(targetGroup, keyGroup);

            if (!CollectKeyframesClient(keyGroup, out List<KeyframeAnimator.KeyframeSample> keyframes) ||
                keyframes.Count < 2)
                return;

            var tiles = new List<TileEntry>();
            if (targetNetIds != null)
            {
                for (int i = 0; i < targetNetIds.Length; i++)
                {
                    if (!LevelScriptObjectUtility.TryResolveTile(targetNetIds[i], out LevelTileServer tile) ||
                        tile == null || tile.TileType == TileType.Keyframe || tile.TileType == TileType.Executor)
                        continue;

                    tiles.Add(new TileEntry
                    {
                        Tile = tile,
                        BaseRotation = tile.PlacementRotationDegrees,
                        BaseScale = tile.Scale
                    });
                }
            }

            if (tiles.Count == 0)
                return;

            Vector2 center = new Vector2(restPivotX, restPivotY);
            Vector2 offsetSum = Vector2.zero;
            for (int i = 0; i < tiles.Count; i++)
            {
                Vector2 world = tiles[i].Tile.transform.position;
                tiles[i].LocalOffset = world - center;
                offsetSum += tiles[i].LocalOffset;
            }

            Vector2 centroid = offsetSum / tiles.Count;
            for (int i = 0; i < tiles.Count; i++)
                tiles[i].LocalOffset -= centroid;

            var anim = new PredictedAnimation
            {
                TargetGroup = targetGroup,
                KeyGroup = keyGroup,
                CloseLoop = closeLoop,
                Distance = distance,
                StartTime = startTime,
                RestPivot = center,
                FirstKeyframePosition = new Vector2(firstPosX, firstPosY),
                FirstKeyframeRotation = firstRot,
                FirstKeyframeScale = new Vector2(firstScaleX, firstScaleY),
                Keyframes = keyframes,
                SegmentCumulativeLengths = distance
                    ? BuildSegmentCumulativeLengths(keyframes, closeLoop)
                    : null,
                SegmentIndex = 0,
                SegmentStartTime = startTime
            };
            anim.Tiles.AddRange(tiles);

            Instance.active.Add(anim);
            ApplyPredictedPose(anim, 0, 0f);
        }

        public static void HandleAnimationStopped(int targetGroup, int keyGroup)
        {
            if (Instance == null)
                return;

            RemoveAnimationsForGroups(targetGroup, keyGroup);
        }

        static void EnsureInstance()
        {
            if (Instance != null)
                return;

            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                session = FindFirstObjectByType<LevelSessionServer>();

            if (session == null)
                return;

            if (session.GetComponent<KeyframeMotionPredictor>() == null)
                session.gameObject.AddComponent<KeyframeMotionPredictor>();
        }

        static void RemoveAnimationsForGroups(int targetGroup, int keyGroup)
        {
            if (Instance == null)
                return;

            for (int i = Instance.active.Count - 1; i >= 0; i--)
            {
                PredictedAnimation anim = Instance.active[i];
                if (anim.TargetGroup == targetGroup && anim.KeyGroup == keyGroup)
                    Instance.active.RemoveAt(i);
            }
        }

        static bool AdvancePrediction(PredictedAnimation anim, float now)
        {
            if (anim.Keyframes == null || anim.Keyframes.Count < 2)
                return false;

            if (anim.Distance)
            {
                float totalDuration = Mathf.Max(0.0001f, anim.Keyframes[0].Duration);
                float u = (now - anim.StartTime) / totalDuration;
                if (u >= 1f)
                {
                    ApplyAtPathProgress(anim, 1f);
                    return false;
                }

                float eased = LevelEaseUtility.Evaluate(anim.Keyframes[0].Ease, u);
                ApplyAtPathProgress(anim, eased);
                return true;
            }

            int lastIndex = anim.Keyframes.Count - 1;
            int segmentCount = anim.CloseLoop ? anim.Keyframes.Count : lastIndex;
            while (anim.SegmentIndex < segmentCount)
            {
                KeyframeAnimator.KeyframeSample from = anim.Keyframes[anim.SegmentIndex];
                float duration = Mathf.Max(0.0001f, from.Duration);
                float t = (now - anim.SegmentStartTime) / duration;
                if (t < 1f)
                {
                    float eased = LevelEaseUtility.Evaluate(from.Ease, Mathf.Clamp01(t));
                    ApplyPredictedPose(anim, anim.SegmentIndex, eased);
                    return true;
                }

                ApplyPredictedPose(anim, anim.SegmentIndex, 1f);
                anim.SegmentIndex++;
                anim.SegmentStartTime = now;
            }

            return false;
        }

        static void ApplyAtPathProgress(PredictedAnimation anim, float pathProgress)
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
                ApplyPredictedPose(anim, 0, pathProgress);
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
            ApplyPredictedPose(anim, segmentIndex, localT);
        }

        static void ApplyPredictedPose(PredictedAnimation anim, int segmentIndex, float t)
        {
            int nextIndex = segmentIndex + 1;
            if (nextIndex >= anim.Keyframes.Count)
            {
                if (!anim.CloseLoop)
                    return;
                nextIndex = 0;
            }

            KeyframeAnimator.EvaluatePose(
                anim.Keyframes,
                segmentIndex,
                nextIndex,
                anim.CloseLoop,
                t,
                out Vector2 pathPos,
                out float pathRot,
                out Vector2 pathScale);

            Vector2 pathDelta = pathPos - anim.FirstKeyframePosition;
            float rotOffset = Mathf.DeltaAngle(anim.FirstKeyframeRotation, pathRot);
            Vector2 scaleRatio = new Vector2(
                SafeRatio(pathScale.x, anim.FirstKeyframeScale.x),
                SafeRatio(pathScale.y, anim.FirstKeyframeScale.y));

            Vector2 worldCenter = anim.RestPivot + pathDelta;
            Quaternion rot = Quaternion.Euler(0f, 0f, rotOffset);

            for (int i = 0; i < anim.Tiles.Count; i++)
            {
                TileEntry entry = anim.Tiles[i];
                if (entry.Tile == null)
                    continue;

                Vector2 scaledLocal = Vector2.Scale(entry.LocalOffset, scaleRatio);
                Vector2 rotatedLocal = rot * scaledLocal;
                Vector2 worldPos = worldCenter + rotatedLocal;
                float worldRot = entry.BaseRotation + rotOffset;
                Vector2 worldScale = Vector2.Scale(entry.BaseScale, scaleRatio);

                entry.Tile.ClientApplyPredictedScriptPose(
                    worldPos,
                    worldRot,
                    worldScale);
            }
        }

        static float SafeRatio(float value, float baseValue)
        {
            if (Mathf.Abs(baseValue) < 0.0001f)
                return 1f;
            return value / baseValue;
        }

        static float[] BuildSegmentCumulativeLengths(
            IReadOnlyList<KeyframeAnimator.KeyframeSample> keyframes,
            bool closeLoop)
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
            IReadOnlyList<KeyframeAnimator.KeyframeSample> keyframes,
            int fromIndex,
            int toIndex,
            bool closeLoop)
        {
            const int samples = 12;
            KeyframeAnimator.EvaluatePose(keyframes, fromIndex, toIndex, closeLoop, 0f,
                out Vector2 previous, out _, out _);
            float length = 0f;
            for (int s = 1; s <= samples; s++)
            {
                float u = s / (float)samples;
                KeyframeAnimator.EvaluatePose(keyframes, fromIndex, toIndex, closeLoop, u,
                    out Vector2 current, out _, out _);
                length += Vector2.Distance(previous, current);
                previous = current;
            }

            return length;
        }

        static bool CollectKeyframesClient(int group, out List<KeyframeAnimator.KeyframeSample> output)
        {
            output = new List<KeyframeAnimator.KeyframeSample>();
            if (group < 0 || !NetworkClient.active)
                return false;

            var pairs = new List<(int spawnId, long createdAt, uint netId, KeyframeAnimator.KeyframeSample sample)>();
            foreach (var kv in NetworkClient.spawned)
            {
                if (kv.Value == null || !kv.Value.TryGetComponent(out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Keyframe || !tile.HasGroup(group))
                    continue;

                if (!tile.TryGetComponent(out KeyframeTile keyframeTile))
                    continue;

                LevelEaseUtility.TryParse(keyframeTile.Anim, out LevelEase ease);
                uint netId = tile.netIdentity != null ? tile.netIdentity.netId : 0u;
                pairs.Add((keyframeTile.SpawnId, tile.CreatedAtUnixMs, netId, new KeyframeAnimator.KeyframeSample
                {
                    Position = tile.Position,
                    Rotation = tile.PlacementRotationDegrees,
                    Scale = tile.Scale,
                    Duration = keyframeTile.Duration,
                    Ease = ease,
                    Curve = keyframeTile.Curve
                }));
            }

            if (pairs.Count < 2)
                return false;

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

            return true;
        }
    }
}
