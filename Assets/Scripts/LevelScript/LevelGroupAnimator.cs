using System.Collections.Generic;
using Mirror;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
    /// <summary>
    /// Local tile motion for level scripts. Group tiles are parented under pivot empties and moved/rotated there.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelGroupAnimator : MonoBehaviour
    {
        public static LevelGroupAnimator Instance { get; private set; }

        readonly Dictionary<int, GroupPivot> groupPivots = new();
        readonly List<MoveMotion> moveMotions = new();
        readonly List<RotateMotion> rotateMotions = new();
        readonly List<RotateToMotion> rotateToMotions = new();
        readonly List<ScaleMotion> scaleMotions = new();
        readonly List<TransparencyMotion> transparencyMotions = new();

        sealed class TransparencyMotion
        {
            public LevelTileServer Tile;
            public float StartValue;
            public float TargetValue;
            public float StartTime;
            public float Duration;
            public LevelEase Ease;
            public float LastApplied;
        }

        sealed class GroupPivot
        {
            public int GroupId;
            public Transform Transform;
            public readonly List<LevelTileServer> Tiles = new();
        }

        sealed class MoveMotion
        {
            public Transform Target;
            public Vector3 Delta;
            public float StartTime;
            public float Duration;
            public LevelEase Ease;
            public float LastApplied;
        }

        sealed class RotateMotion
        {
            public Transform Target;
            public Transform OrbitWrapper;
            public float Degrees;
            public float StartTime;
            public float Duration;
            public LevelEase Ease;
            public float LastAppliedDegrees;
        }

        sealed class RotateToMotion
        {
            public Transform Wrapper;
            public float StartAngle;
            public float TargetAngle;
            public float StartTime;
            public float Duration;
            public LevelEase Ease;
            public float LastAppliedAngle;
        }

        sealed class ScaleMotion
        {
            public Transform Target;
            public Vector3 StartScale;
            public Vector3 TargetScale;
            public float StartTime;
            public float Duration;
            public LevelEase Ease;
            public float LastApplied;
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
            float now = Time.time;
            ApplyMoveMotions(now);
            ApplyRotateMotions(now);
            ApplyRotateToMotions(now);
            ApplyScaleMotions(now);
            ApplyTransparencyMotions(now);

            // Throttle network pose sync hard. Continuous move() loops used to
            // SyncVar-spam every animated tile to all clients and tank FPS.
            if (NetworkServer.active && now >= nextScriptNetworkSyncTime)
            {
                nextScriptNetworkSyncTime = now + ScriptNetworkSyncInterval;
                SyncActivePivotTilesToClients();
            }
        }

        const float ScriptNetworkSyncInterval = 1f / 6f;
        float nextScriptNetworkSyncTime;

        void SyncActivePivotTilesToClients()
        {
            if (groupPivots.Count == 0)
                return;

            foreach (GroupPivot pivot in groupPivots.Values)
            {
                if (pivot?.Transform == null)
                    continue;

                for (int i = 0; i < pivot.Tiles.Count; i++)
                {
                    LevelTileServer tile = pivot.Tiles[i];
                    if (tile != null)
                        tile.ServerSyncScriptStateFromCurrentTransform(roundValues: true);
                }
            }
        }

        public void SyncScriptedTilesNow()
        {
            ApplyMoveMotions(Time.time);
            SyncActivePivotTilesToClients();
        }

        public bool IsTileInMotion(LevelTileServer tile)
        {
            if (tile == null)
                return false;

            if (tile.IsAttachedToScriptPivot)
                return true;

            foreach (GroupPivot pivot in groupPivots.Values)
            {
                if (pivot?.Tiles == null)
                    continue;

                for (int i = 0; i < pivot.Tiles.Count; i++)
                {
                    if (pivot.Tiles[i] == tile)
                        return true;
                }
            }

            return false;
        }

        public void ResetAll()
        {
            ResetMotions();
            RestoreAllTilesToBase();
        }

        public void ResetMotions()
        {
            moveMotions.Clear();
            rotateMotions.Clear();
            rotateToMotions.Clear();
            scaleMotions.Clear();
            transparencyMotions.Clear();

            foreach (GroupPivot pivot in groupPivots.Values)
                DestroyPivot(pivot);

            groupPivots.Clear();

            ForEachRegisteredTile(tile =>
            {
                tile.DetachFromScriptPivot();
                tile.ApplyScriptTransform(Vector2.zero, 0f);
            });
        }

        public void RestoreAllTilesToBase()
        {
            if (!NetworkServer.active)
                return;

            ForEachRegisteredTile(tile => tile.ServerRestoreBaseState());
        }

        public static void CaptureAllTilesBaseState()
        {
            if (!NetworkServer.active)
                return;

            ForEachRegisteredTile(tile => tile.ServerCaptureBaseState());
        }

        static void ForEachRegisteredTile(System.Action<LevelTileServer> callback)
        {
            if (callback == null)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null && grid.TileCount > 0)
            {
                grid.ForEachTile(callback);
                return;
            }

            // Fallback for scenes without a populated grid registry.
            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null)
                    callback(tiles[i]);
            }
        }

        public void StartMoveObjects(IReadOnlyList<ScriptGameObjectRef> objects, float deltaX, float deltaY, float duration, LevelEase ease)
        {
            if (objects == null || objects.Count == 0)
                return;

            Vector3 delta = new Vector3(deltaX, deltaY, 0f) * GridMath.CellSize;
            float startTime = Time.time;
            float clampedDuration = Mathf.Max(0f, duration);

            for (int i = 0; i < objects.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTransform(objects[i], out Transform target) || target == null)
                    continue;

                if (clampedDuration <= 0f)
                {
                    ApplyMoveStep(target, delta);
                    continue;
                }

                moveMotions.Add(new MoveMotion
                {
                    Target = target,
                    Delta = delta,
                    StartTime = startTime,
                    Duration = clampedDuration,
                    Ease = ease,
                    LastApplied = 0f
                });
            }
        }

        public void StartMove(int group, float deltaX, float deltaY, float duration, LevelEase ease)
        {
            GroupPivot pivot = EnsureGroupPivot(group);
            if (pivot == null)
                return;

            Vector3 delta = new Vector3(deltaX, deltaY, 0f) * GridMath.CellSize;
            float clampedDuration = Mathf.Max(0f, duration);
            if (clampedDuration <= 0f)
            {
                ApplyMoveStep(pivot.Transform, delta);
                return;
            }

            moveMotions.Add(new MoveMotion
            {
                Target = pivot.Transform,
                Delta = delta,
                StartTime = Time.time,
                Duration = clampedDuration,
                Ease = ease,
                LastApplied = 0f
            });
        }

        public void StartRotateObjects(
            IReadOnlyList<ScriptGameObjectRef> objects,
            int pivotGroup,
            float degrees,
            float duration,
            LevelEase ease)
        {
            if (objects == null || objects.Count == 0)
                return;

            float startTime = Time.time;
            float clampedDuration = Mathf.Max(0f, duration);

            if (pivotGroup <= 0)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    if (!LevelScriptObjectUtility.TryResolveTransform(objects[i], out Transform target) || target == null)
                        continue;

                    rotateMotions.Add(new RotateMotion
                    {
                        Target = target,
                        Degrees = degrees,
                        StartTime = startTime,
                        Duration = clampedDuration,
                        Ease = ease,
                        LastAppliedDegrees = 0f
                    });
                }

                return;
            }

            List<LevelTileServer> pivotTiles = GetTilesInGroup(pivotGroup);
            if (pivotTiles.Count == 0)
                return;

            Vector2 orbitCenter = ComputeTilesWorldCenter(pivotTiles);
            var orbitObject = new GameObject($"ScriptOrbit_{pivotGroup}");
            orbitObject.transform.SetParent(transform, false);
            orbitObject.transform.position = new Vector3(orbitCenter.x, orbitCenter.y, 0f);

            for (int i = 0; i < objects.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTransform(objects[i], out Transform target) || target == null)
                    continue;

                target.SetParent(orbitObject.transform, true);
            }

            rotateMotions.Add(new RotateMotion
            {
                Target = orbitObject.transform,
                OrbitWrapper = orbitObject.transform,
                Degrees = degrees,
                StartTime = startTime,
                Duration = clampedDuration,
                Ease = ease,
                LastAppliedDegrees = 0f
            });
        }

        public void StartRotate(int group, int pivotGroup, float degrees, float duration, LevelEase ease)
        {
            GroupPivot pivot = EnsureGroupPivot(group);
            if (pivot == null)
                return;

            Transform rotateTarget = pivot.Transform;
            Transform orbitWrapper = null;

            if (pivotGroup != group)
            {
                List<LevelTileServer> pivotTiles = GetTilesInGroup(pivotGroup);
                if (pivotTiles.Count == 0)
                    return;

                Vector2 orbitCenter = ComputeTilesWorldCenter(pivotTiles);
                GameObject orbitObject = new GameObject($"ScriptOrbit_{pivotGroup}_for_{group}");
                orbitObject.transform.SetParent(transform, false);
                orbitObject.transform.position = new Vector3(orbitCenter.x, orbitCenter.y, 0f);
                orbitObject.transform.rotation = Quaternion.identity;
                pivot.Transform.SetParent(orbitObject.transform, true);
                rotateTarget = orbitObject.transform;
                orbitWrapper = orbitObject.transform;
            }

            rotateMotions.Add(new RotateMotion
            {
                Target = rotateTarget,
                OrbitWrapper = orbitWrapper,
                Degrees = degrees,
                StartTime = Time.time,
                Duration = Mathf.Max(0f, duration),
                Ease = ease,
                LastAppliedDegrees = 0f
            });
        }

        public void StartRotateToObjects(
            IReadOnlyList<ScriptGameObjectRef> objects,
            IReadOnlyList<ScriptGameObjectRef> pivotObjects,
            float targetX,
            float targetY,
            float offsetDegrees,
            float duration,
            LevelEase ease)
        {
            if (objects == null || objects.Count == 0)
                return;

            float clampedDuration = Mathf.Max(0f, duration);
            if (!TryResolvePivotCenter(objects, pivotObjects, out Vector2 pivotCenter))
                return;

            float dx = targetX - pivotCenter.x;
            float dy = targetY - pivotCenter.y;
            float targetAngle = ComputeRotateToAngle(dx, dy, offsetDegrees);
            if (float.IsNaN(targetAngle))
                return;

            Transform wrapper = EnsureRotateToWrapper(objects, pivotCenter);
            if (wrapper == null)
                return;

            float startAngle = wrapper.eulerAngles.z;
            if (clampedDuration <= 0f)
            {
                wrapper.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                return;
            }

            // Update existing motion for this wrapper instead of stacking.
            for (int i = 0; i < rotateToMotions.Count; i++)
            {
                if (rotateToMotions[i].Wrapper != wrapper)
                    continue;

                rotateToMotions[i].StartAngle = startAngle;
                rotateToMotions[i].TargetAngle = targetAngle;
                rotateToMotions[i].StartTime = Time.time;
                rotateToMotions[i].Duration = clampedDuration;
                rotateToMotions[i].Ease = ease;
                rotateToMotions[i].LastAppliedAngle = startAngle;
                return;
            }

            rotateToMotions.Add(new RotateToMotion
            {
                Wrapper = wrapper,
                StartAngle = startAngle,
                TargetAngle = targetAngle,
                StartTime = Time.time,
                Duration = clampedDuration,
                Ease = ease,
                LastAppliedAngle = startAngle
            });
        }

        static float ComputeRotateToAngle(float dx, float dy, float offsetDegrees)
        {
            const float epsilon = 0.001f;
            if (Mathf.Abs(dx) < epsilon && Mathf.Abs(dy) < epsilon)
                return float.NaN;

            return Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + offsetDegrees;
        }

        public void StartRotateObjectsAroundObjects(
            IReadOnlyList<ScriptGameObjectRef> objects,
            IReadOnlyList<ScriptGameObjectRef> pivotObjects,
            float degrees,
            float duration,
            LevelEase ease)
        {
            if (objects == null || objects.Count == 0)
                return;

            if (!TryResolvePivotCenter(objects, pivotObjects, out Vector2 pivotCenter))
                return;

            var orbitObject = new GameObject("ScriptOrbit_Objects");
            orbitObject.transform.SetParent(transform, false);
            orbitObject.transform.position = new Vector3(pivotCenter.x, pivotCenter.y, 0f);
            orbitObject.transform.rotation = Quaternion.identity;

            AttachTilesToPivot(objects, orbitObject.transform);

            rotateMotions.Add(new RotateMotion
            {
                Target = orbitObject.transform,
                OrbitWrapper = orbitObject.transform,
                Degrees = degrees,
                StartTime = Time.time,
                Duration = Mathf.Max(0f, duration),
                Ease = ease,
                LastAppliedDegrees = 0f
            });
        }

        public void StartGotoObjects(
            IReadOnlyList<ScriptGameObjectRef> objects,
            float x,
            float y,
            float duration,
            LevelEase ease)
        {
            if (objects == null || objects.Count == 0)
                return;

            float startTime = Time.time;
            float clampedDuration = Mathf.Max(0f, duration);
            var targetPosition = new Vector3(x, y, 0f);

            for (int i = 0; i < objects.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTransform(objects[i], out Transform target) || target == null)
                    continue;

                Vector3 delta = targetPosition - target.position;
                moveMotions.Add(new MoveMotion
                {
                    Target = target,
                    Delta = delta,
                    StartTime = startTime,
                    Duration = clampedDuration,
                    Ease = ease,
                    LastApplied = 0f
                });
            }
        }

        public void StartGoto(int group, float x, float y, float duration, LevelEase ease)
        {
            GroupPivot pivot = EnsureGroupPivot(group);
            if (pivot == null)
                return;

            Vector3 target = new Vector3(x, y, pivot.Transform.position.z);
            var motion = new MoveMotion
            {
                Target = pivot.Transform,
                Delta = target - pivot.Transform.position,
                StartTime = Time.time,
                Duration = Mathf.Max(0f, duration),
                Ease = ease,
                LastApplied = 0f
            };
            moveMotions.Add(motion);
        }

        public void StartTransparencyObjects(
            IReadOnlyList<uint> netIds,
            float targetValue,
            float duration,
            LevelEase ease)
        {
            if (netIds == null || netIds.Count == 0)
                return;

            float startTime = Time.time;
            float clampedDuration = Mathf.Max(0f, duration);
            float clampedTarget = Mathf.Clamp01(targetValue);
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < netIds.Count; i++)
            {
                if (!grid.TryGetTileByNetId(netIds[i], out LevelTileServer tile) || tile == null)
                    continue;

                transparencyMotions.Add(new TransparencyMotion
                {
                    Tile = tile,
                    StartValue = tile.Transparency,
                    TargetValue = clampedTarget,
                    StartTime = startTime,
                    Duration = clampedDuration,
                    Ease = ease,
                    LastApplied = tile.Transparency
                });
            }
        }

        void ApplyTransparencyMotions(float now)
        {
            for (int i = transparencyMotions.Count - 1; i >= 0; i--)
            {
                TransparencyMotion motion = transparencyMotions[i];
                if (motion.Tile == null)
                {
                    transparencyMotions.RemoveAt(i);
                    continue;
                }

                float t = motion.Duration <= 0f
                    ? 1f
                    : Mathf.Clamp01((now - motion.StartTime) / motion.Duration);
                float eased = LevelEaseUtility.Evaluate(motion.Ease, t);
                float value = Mathf.Lerp(motion.StartValue, motion.TargetValue, eased);

                if (!Mathf.Approximately(value, motion.LastApplied))
                {
                    motion.Tile.ServerSetTransparency(value, updateBaseState: false);
                    motion.LastApplied = value;
                }

                if (t >= 1f)
                    transparencyMotions.RemoveAt(i);
            }
        }

        public void StartScaleToObjects(
            IReadOnlyList<ScriptGameObjectRef> objects,
            float scaleX,
            float scaleY,
            float duration,
            LevelEase ease)
        {
            if (objects == null || objects.Count == 0)
                return;

            float startTime = Time.time;
            float clampedDuration = Mathf.Max(0f, duration);
            var targetScale = new Vector3(Mathf.Max(0.01f, scaleX), Mathf.Max(0.01f, scaleY), 1f);

            for (int i = 0; i < objects.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTransform(objects[i], out Transform target) || target == null)
                    continue;

                scaleMotions.Add(new ScaleMotion
                {
                    Target = target,
                    StartScale = target.localScale,
                    TargetScale = targetScale,
                    StartTime = startTime,
                    Duration = clampedDuration,
                    Ease = ease,
                    LastApplied = 0f
                });
            }
        }

        public void StartScaleTo(int group, float scaleX, float scaleY, float duration, LevelEase ease)
        {
            GroupPivot pivot = EnsureGroupPivot(group);
            if (pivot == null)
                return;

            Vector3 startScale = pivot.Transform.localScale;
            Vector3 targetScale = new Vector3(Mathf.Max(0.01f, scaleX), Mathf.Max(0.01f, scaleY), 1f);
            scaleMotions.Add(new ScaleMotion
            {
                Target = pivot.Transform,
                StartScale = startScale,
                TargetScale = targetScale,
                StartTime = Time.time,
                Duration = Mathf.Max(0f, duration),
                Ease = ease,
                LastApplied = 0f
            });
        }

        GroupPivot EnsureGroupPivot(int group)
        {
            if (groupPivots.TryGetValue(group, out GroupPivot existing))
            {
                // Do not rescan the whole grid every move()/frame — membership is fixed
                // for a running script in practice. Occasional refresh is enough.
                return existing;
            }

            List<LevelTileServer> tiles = GetTilesInGroup(group);
            if (tiles.Count == 0)
                return null;

            var pivotObject = new GameObject($"ScriptGroup_{group}");
            pivotObject.transform.SetParent(transform, false);

            Vector2 center = ComputeTilesWorldCenter(tiles);
            pivotObject.transform.position = new Vector3(center.x, center.y, 0f);
            pivotObject.transform.rotation = Quaternion.identity;

            var pivot = new GroupPivot
            {
                GroupId = group,
                Transform = pivotObject.transform
            };

            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile.IsAttachedToScriptPivot)
                    continue;

                pivot.Tiles.Add(tile);
                tile.AttachToScriptPivot(pivotObject.transform);
            }

            groupPivots[group] = pivot;
            return pivot;
        }

        void RefreshPivotMembership(GroupPivot pivot)
        {
            List<LevelTileServer> tiles = GetTilesInGroup(pivot.GroupId);
            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile.IsAttachedToScriptPivot || pivot.Tiles.Contains(tile))
                    continue;

                pivot.Tiles.Add(tile);
                tile.AttachToScriptPivot(pivot.Transform);
            }
        }

        void ApplyMoveMotions(float now)
        {
            for (int i = moveMotions.Count - 1; i >= 0; i--)
            {
                MoveMotion motion = moveMotions[i];
                if (motion.Target == null)
                {
                    moveMotions.RemoveAt(i);
                    continue;
                }

                float t = motion.Duration <= 0f ? 1f : Mathf.Clamp01((now - motion.StartTime) / motion.Duration);
                float eased = LevelEaseUtility.Evaluate(motion.Ease, t);
                float step = eased - motion.LastApplied;

                if (step > 0f)
                    ApplyMoveStep(motion.Target, motion.Delta * step);

                motion.LastApplied = eased;
                if (t >= 1f)
                    moveMotions.RemoveAt(i);
            }
        }

        static void ApplyMoveStep(Transform target, Vector3 worldDelta)
        {
            if (worldDelta.sqrMagnitude <= 0f)
                return;

            if (target.TryGetComponent(out LevelTileServer tile))
            {
                tile.ServerApplyScriptWorldDelta(worldDelta);
                return;
            }

            target.position += worldDelta;
            // Pivot tile SyncVars are flushed on a throttled LateUpdate tick.
        }

        void ApplyRotateToMotions(float now)
        {
            for (int i = rotateToMotions.Count - 1; i >= 0; i--)
            {
                RotateToMotion motion = rotateToMotions[i];
                if (motion.Wrapper == null)
                {
                    rotateToMotions.RemoveAt(i);
                    continue;
                }

                float t = motion.Duration <= 0f ? 1f : Mathf.Clamp01((now - motion.StartTime) / motion.Duration);
                float eased = LevelEaseUtility.Evaluate(motion.Ease, t);
                float angle = Mathf.LerpAngle(motion.StartAngle, motion.TargetAngle, eased);
                float frameDegrees = Mathf.DeltaAngle(motion.LastAppliedAngle, angle);

                if (Mathf.Abs(frameDegrees) > 0.0001f)
                    motion.Wrapper.Rotate(0f, 0f, frameDegrees, Space.Self);

                motion.LastAppliedAngle = angle;
                if (t >= 1f)
                    rotateToMotions.RemoveAt(i);
            }
        }

        void ApplyRotateMotions(float now)
        {
            for (int i = rotateMotions.Count - 1; i >= 0; i--)
            {
                RotateMotion motion = rotateMotions[i];
                if (motion.Target == null)
                {
                    CleanupOrbitWrapper(motion.OrbitWrapper);
                    rotateMotions.RemoveAt(i);
                    continue;
                }

                float t = motion.Duration <= 0f ? 1f : Mathf.Clamp01((now - motion.StartTime) / motion.Duration);
                float degrees = motion.Degrees * LevelEaseUtility.Evaluate(motion.Ease, t);
                float frameDegrees = degrees - motion.LastAppliedDegrees;

                if (Mathf.Abs(frameDegrees) > 0.0001f)
                    motion.Target.Rotate(0f, 0f, frameDegrees, Space.Self);

                motion.LastAppliedDegrees = degrees;
                if (t >= 1f)
                {
                    FinalizeOrbitWrapper(motion);
                    rotateMotions.RemoveAt(i);
                }
            }
        }

        void ApplyScaleMotions(float now)
        {
            for (int i = scaleMotions.Count - 1; i >= 0; i--)
            {
                ScaleMotion motion = scaleMotions[i];
                if (motion.Target == null)
                {
                    scaleMotions.RemoveAt(i);
                    continue;
                }

                float t = motion.Duration <= 0f ? 1f : Mathf.Clamp01((now - motion.StartTime) / motion.Duration);
                float eased = LevelEaseUtility.Evaluate(motion.Ease, t);
                Vector3 scale = Vector3.LerpUnclamped(motion.StartScale, motion.TargetScale, eased);
                motion.Target.localScale = scale;

                motion.LastApplied = eased;
                if (t >= 1f)
                    scaleMotions.RemoveAt(i);
            }
        }

        static void FinalizeOrbitWrapper(RotateMotion motion)
        {
            FinalizeOrbitWrapper(motion.OrbitWrapper);
        }

        static void FinalizeOrbitWrapper(Transform orbitWrapper)
        {
            if (orbitWrapper == null)
                return;

            Transform parent = orbitWrapper.parent;
            while (orbitWrapper.childCount > 0)
                orbitWrapper.GetChild(0).SetParent(parent, true);

            CleanupOrbitWrapper(orbitWrapper);
        }

        static void CleanupOrbitWrapper(Transform orbitWrapper)
        {
            if (orbitWrapper != null)
                Destroy(orbitWrapper.gameObject);
        }

        void DestroyPivot(GroupPivot pivot)
        {
            for (int i = 0; i < pivot.Tiles.Count; i++)
                pivot.Tiles[i].DetachFromScriptPivot();

            if (pivot.Transform != null)
                Destroy(pivot.Transform.gameObject);
        }

        static Vector2 ComputeTilesWorldCenter(List<LevelTileServer> tiles)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < tiles.Count; i++)
                sum += (Vector2)tiles[i].transform.position;

            return sum / tiles.Count;
        }

        bool TryCreateObjectWrapper(
            IReadOnlyList<ScriptGameObjectRef> objects,
            string wrapperName,
            out Transform wrapper,
            out Vector2 center)
        {
            wrapper = null;
            center = Vector2.zero;

            var targets = new List<Transform>();
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < objects.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTransform(objects[i], out Transform target) || target == null)
                    continue;

                targets.Add(target);
                sum += (Vector2)target.position;
            }

            if (targets.Count == 0)
                return false;

            center = sum / targets.Count;
            var wrapperObject = new GameObject(wrapperName);
            wrapperObject.transform.SetParent(transform, false);
            wrapperObject.transform.position = new Vector3(center.x, center.y, 0f);
            wrapperObject.transform.rotation = Quaternion.identity;

            for (int i = 0; i < targets.Count; i++)
                targets[i].SetParent(wrapperObject.transform, true);

            wrapper = wrapperObject.transform;
            return true;
        }

        static bool TryResolvePivotCenter(
            IReadOnlyList<ScriptGameObjectRef> objects,
            IReadOnlyList<ScriptGameObjectRef> pivotObjects,
            out Vector2 center)
        {
            center = Vector2.zero;

            IReadOnlyList<ScriptGameObjectRef> source = pivotObjects != null && pivotObjects.Count > 0
                ? pivotObjects
                : objects;

            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTransform(source[i], out Transform target) || target == null)
                    continue;

                sum += (Vector2)target.position;
                count++;
            }

            if (count == 0)
                return false;

            center = sum / count;
            return true;
        }

        Transform EnsureRotateToWrapper(IReadOnlyList<ScriptGameObjectRef> objects, Vector2 center)
        {
            // Reuse existing pivot if all tiles already attached to the same ScriptRotateTo pivot.
            Transform existingPivot = null;
            bool anyTile = false;

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Kind != ScriptObjectKind.Tile)
                    continue;

                if (!LevelScriptObjectUtility.TryResolveTile(objects[i].NetId, out LevelTileServer tile) || tile == null)
                    continue;

                anyTile = true;
                if (!tile.IsAttachedToScriptPivot)
                {
                    existingPivot = null;
                    break;
                }

                Transform pivot = tile.ScriptPivotTransform;
                if (pivot == null)
                {
                    existingPivot = null;
                    break;
                }

                if (existingPivot == null)
                    existingPivot = pivot;
                else if (existingPivot != pivot)
                {
                    existingPivot = null;
                    break;
                }
            }

            if (anyTile && existingPivot != null && existingPivot.name.StartsWith("ScriptRotateTo"))
                return existingPivot;

            var wrapperObject = new GameObject("ScriptRotateTo");
            wrapperObject.transform.SetParent(transform, false);
            wrapperObject.transform.position = new Vector3(center.x, center.y, 0f);
            wrapperObject.transform.rotation = Quaternion.identity;

            AttachTilesToPivot(objects, wrapperObject.transform);
            return wrapperObject.transform;
        }

        static void AttachTilesToPivot(IReadOnlyList<ScriptGameObjectRef> objects, Transform pivot)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Kind != ScriptObjectKind.Tile)
                    continue;

                if (!LevelScriptObjectUtility.TryResolveTile(objects[i].NetId, out LevelTileServer tile) || tile == null)
                    continue;

                tile.AttachToScriptPivot(pivot);
            }
        }

        static List<LevelTileServer> GetTilesInGroup(int group)
        {
            var result = new List<LevelTileServer>();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null && grid.TileCount > 0)
            {
                grid.CollectTilesWithGroup(group, result);
                return result;
            }

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null && tiles[i].HasGroup(group))
                    result.Add(tiles[i]);
            }

            return result;
        }

    }
}
