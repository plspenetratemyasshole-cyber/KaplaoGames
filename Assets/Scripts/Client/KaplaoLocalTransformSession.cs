using System.Collections.Generic;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Immediate local G/R/T create-transform session so drag feedback does not wait on SyncVars.
    /// Final pose is committed with CmdFinalizeCreateTransform (absolute values).
    /// </summary>
    public static class KaplaoLocalTransformSession
    {
        struct Baseline
        {
            public Vector2 Position;
            public float Rotation;
            public Vector2 Scale;
        }

        static CreateTransformMode mode = CreateTransformMode.None;
        static TransformAxisConstraint axisConstraint = TransformAxisConstraint.None;
        static readonly Dictionary<uint, Baseline> baselines = new();
        static Vector2 pivot;
        static Vector2 startCursorPos;
        static float startCursorAngle;
        static float startCursorDistance;
        static bool cursorInitialized;
        static float rotationSnap = CreateModeSnapSettings.DefaultRotationSnapDegrees;
        static float positionSnap = CreateModeSnapSettings.DefaultPositionSnapUnits;
        static float scaleSnap = CreateModeSnapSettings.DefaultScaleSnapUnits;

        public static bool IsActive => mode != CreateTransformMode.None;
        public static CreateTransformMode Mode => mode;
        public static Vector2 Pivot => pivot;

        public static bool TryBegin(CreateTransformMode newMode, KaplaoCursorServer server)
        {
            if (newMode == CreateTransformMode.None || server == null || IsActive)
                return false;

            if (!KaplaoLocalGameModePredictor.IsCreate(server))
                return false;

            if (server.SelectedCount == 0)
                return false;

            baselines.Clear();
            pivot = Vector2.zero;
            int count = 0;

            for (int i = 0; i < server.SelectedTiles.Count; i++)
            {
                uint netId = server.SelectedTiles[i];
                if (!TryGetTile(netId, out LevelTileServer tile) || tile.IsAttachedToScriptPivot)
                    continue;

                Vector2 pos = KaplaoLocalEditPredictor.GetEffectivePosition(tile);
                float rot = KaplaoLocalEditPredictor.GetEffectiveRotation(tile);
                Vector2 scale = KaplaoLocalEditPredictor.GetEffectiveScale(tile);
                baselines[netId] = new Baseline
                {
                    Position = pos,
                    Rotation = rot,
                    Scale = scale
                };
                pivot += pos;
                count++;
            }

            if (count == 0)
            {
                baselines.Clear();
                return false;
            }

            pivot /= count;
            mode = newMode;
            axisConstraint = TransformAxisConstraint.None;
            cursorInitialized = false;
            startCursorPos = Vector2.zero;
            startCursorAngle = 0f;
            startCursorDistance = 0.001f;

            rotationSnap = server.RotationSnapDegrees;
            positionSnap = server.PositionSnapUnits;
            scaleSnap = server.ScaleSnapUnits;
            return true;
        }

        public static void SetAxis(TransformAxisConstraint axis)
        {
            if (!IsActive || axis == TransformAxisConstraint.None)
                return;

            axisConstraint = axis;
        }

        public static void Update(Vector2 cursorPosition)
        {
            if (!IsActive || baselines.Count == 0)
                return;

            if (!cursorInitialized)
            {
                startCursorPos = cursorPosition;
                Vector2 dir = cursorPosition - pivot;
                startCursorAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                startCursorDistance = Mathf.Max(dir.magnitude, 0.001f);
                cursorInitialized = true;
                return;
            }

            switch (mode)
            {
                case CreateTransformMode.Move:
                    ApplyMove(cursorPosition);
                    break;
                case CreateTransformMode.Rotate:
                    ApplyRotate(cursorPosition);
                    break;
                case CreateTransformMode.Scale:
                    ApplyScale(cursorPosition);
                    break;
            }
        }

        public static bool TryCollectFinal(
            out uint[] netIds,
            out Vector2[] positions,
            out float[] rotations,
            out Vector2[] scales)
        {
            netIds = null;
            positions = null;
            rotations = null;
            scales = null;
            if (!IsActive || baselines.Count == 0)
                return false;

            var ids = new List<uint>(baselines.Count);
            var posList = new List<Vector2>(baselines.Count);
            var rotList = new List<float>(baselines.Count);
            var scaleList = new List<Vector2>(baselines.Count);

            foreach (KeyValuePair<uint, Baseline> entry in baselines)
            {
                uint netId = entry.Key;
                if (KaplaoLocalEditPredictor.TryGetPredictedTransform(
                        netId,
                        out Vector2 pos,
                        out float rot,
                        out Vector2 scale))
                {
                    ids.Add(netId);
                    posList.Add(pos);
                    rotList.Add(rot);
                    scaleList.Add(scale);
                    continue;
                }

                ids.Add(netId);
                posList.Add(entry.Value.Position);
                rotList.Add(entry.Value.Rotation);
                scaleList.Add(entry.Value.Scale);
            }

            if (ids.Count == 0)
                return false;

            netIds = ids.ToArray();
            positions = posList.ToArray();
            rotations = rotList.ToArray();
            scales = scaleList.ToArray();
            return true;
        }

        public static bool TryCollectBaselines(
            out uint[] netIds,
            out Vector2[] positions,
            out float[] rotations,
            out Vector2[] scales)
        {
            netIds = null;
            positions = null;
            rotations = null;
            scales = null;
            if (!IsActive || baselines.Count == 0)
                return false;

            var ids = new List<uint>(baselines.Count);
            var posList = new List<Vector2>(baselines.Count);
            var rotList = new List<float>(baselines.Count);
            var scaleList = new List<Vector2>(baselines.Count);

            foreach (KeyValuePair<uint, Baseline> entry in baselines)
            {
                ids.Add(entry.Key);
                posList.Add(entry.Value.Position);
                rotList.Add(entry.Value.Rotation);
                scaleList.Add(entry.Value.Scale);
            }

            netIds = ids.ToArray();
            positions = posList.ToArray();
            rotations = rotList.ToArray();
            scales = scaleList.ToArray();
            return true;
        }

        /// <summary>Ends the drag session but keeps transform overrides until SyncVars catch up.</summary>
        public static void Confirm()
        {
            mode = CreateTransformMode.None;
            axisConstraint = TransformAxisConstraint.None;
            baselines.Clear();
            cursorInitialized = false;
        }

        /// <summary>Restores baseline visuals; keep overrides until server cancel SyncVars match.</summary>
        public static void Cancel()
        {
            if (!IsActive)
                return;

            foreach (KeyValuePair<uint, Baseline> entry in baselines)
            {
                KaplaoLocalEditPredictor.PredictSetTransform(
                    entry.Key,
                    entry.Value.Position,
                    entry.Value.Rotation,
                    entry.Value.Scale);
            }

            mode = CreateTransformMode.None;
            axisConstraint = TransformAxisConstraint.None;
            baselines.Clear();
            cursorInitialized = false;
        }

        public static void ForceClear()
        {
            mode = CreateTransformMode.None;
            axisConstraint = TransformAxisConstraint.None;
            baselines.Clear();
            cursorInitialized = false;
        }

        static void ApplyMove(Vector2 cursorPosition)
        {
            Vector2 delta = cursorPosition - startCursorPos;
            if (axisConstraint == TransformAxisConstraint.X)
                delta.y = 0f;
            else if (axisConstraint == TransformAxisConstraint.Y)
                delta.x = 0f;

            delta.x = CreateModeSnapSettings.SnapDelta(delta.x, positionSnap);
            delta.y = CreateModeSnapSettings.SnapDelta(delta.y, positionSnap);
            ApplyGroupDelta(delta, 0f, 1f, null);
        }

        static void ApplyRotate(Vector2 cursorPosition)
        {
            Vector2 dir = cursorPosition - pivot;
            if (dir.sqrMagnitude < 0.0001f)
                return;

            float cursorAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float deltaAngle = cursorAngle - startCursorAngle;
            deltaAngle = CreateModeSnapSettings.SnapDelta(deltaAngle, rotationSnap);
            ApplyGroupDelta(Vector2.zero, deltaAngle, 1f, null);
        }

        static void ApplyScale(Vector2 cursorPosition)
        {
            Vector2 fromPivot = cursorPosition - pivot;
            float currentDistance = Mathf.Max(fromPivot.magnitude, 0.001f);
            float rawFactor = currentDistance / startCursorDistance;
            float snappedFactor = ComputeSnappedUniformScaleFactor(rawFactor);
            ApplyGroupDelta(Vector2.zero, 0f, snappedFactor, axisConstraint);
        }

        static float ComputeSnappedUniformScaleFactor(float rawFactor)
        {
            float snappedDelta = CreateModeSnapSettings.SnapDelta(rawFactor - 1f, scaleSnap);
            return Mathf.Max(0.01f, 1f + snappedDelta);
        }

        static void ApplyGroupDelta(
            Vector2 moveDelta,
            float rotateDeltaDegrees,
            float uniformScaleFactor,
            TransformAxisConstraint? scaleAxis)
        {
            foreach (KeyValuePair<uint, Baseline> entry in baselines)
            {
                Baseline baseline = entry.Value;
                Vector2 newPosition = baseline.Position + moveDelta;
                float newRotation = baseline.Rotation;
                Vector2 newScale = baseline.Scale;

                if (Mathf.Abs(rotateDeltaDegrees) > 0.0001f)
                {
                    Vector2 offset = baseline.Position - pivot;
                    Vector2 rotatedOffset = RotateVector2(offset, rotateDeltaDegrees);
                    newPosition = pivot + rotatedOffset;
                    newRotation = baseline.Rotation + rotateDeltaDegrees;
                }

                if (Mathf.Abs(uniformScaleFactor - 1f) > 0.0001f)
                {
                    TransformAxisConstraint axis = scaleAxis ?? TransformAxisConstraint.None;
                    Vector2 offset = baseline.Position - pivot;

                    switch (axis)
                    {
                        case TransformAxisConstraint.X:
                            newPosition = baseline.Position + new Vector2(offset.x * (uniformScaleFactor - 1f), 0f);
                            newScale = new Vector2(
                                CreateModeSnapSettings.SnapScale(baseline.Scale.x * uniformScaleFactor, scaleSnap),
                                baseline.Scale.y);
                            break;
                        case TransformAxisConstraint.Y:
                            newPosition = baseline.Position + new Vector2(0f, offset.y * (uniformScaleFactor - 1f));
                            newScale = new Vector2(
                                baseline.Scale.x,
                                CreateModeSnapSettings.SnapScale(baseline.Scale.y * uniformScaleFactor, scaleSnap));
                            break;
                        default:
                            newPosition = pivot + offset * uniformScaleFactor;
                            newScale = baseline.Scale * uniformScaleFactor;
                            newScale = new Vector2(
                                CreateModeSnapSettings.SnapScale(newScale.x, scaleSnap),
                                CreateModeSnapSettings.SnapScale(newScale.y, scaleSnap));
                            break;
                    }
                }

                KaplaoLocalEditPredictor.PredictSetTransform(entry.Key, newPosition, newRotation, newScale);
            }
        }

        static Vector2 RotateVector2(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
        }

        static bool TryGetTile(uint netId, out LevelTileServer tile)
        {
            tile = null;
            if (LevelGridServer.Instance != null &&
                LevelGridServer.Instance.TryGetTileByNetId(netId, out tile))
            {
                return tile != null;
            }

            if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) &&
                identity != null &&
                identity.TryGetComponent(out tile))
            {
                return tile != null;
            }

            return false;
        }
    }
}
