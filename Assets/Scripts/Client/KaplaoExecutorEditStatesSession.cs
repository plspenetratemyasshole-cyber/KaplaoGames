using System.Collections.Generic;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local-only Edit States session: original tiles never move.
    /// Start / End / Preview are visual copies. G/R/T edits the active copy root.
    /// </summary>
    public sealed class KaplaoExecutorEditStatesSession
    {
        enum TransformTool
        {
            None,
            Move,
            Rotate,
            Scale
        }

        readonly List<LevelTileServer> targetTiles = new();
        readonly List<CopySprite> startCopies = new();
        readonly List<CopySprite> endCopies = new();
        readonly List<CopySprite> previewCopies = new();
        Vector2 basePivot;
        Transform startRoot;
        Transform endRoot;
        Transform previewRoot;
        LineRenderer stateLine;
        GameObject rootsObject;
        ExecutorGroupState startState;
        ExecutorGroupState endState;
        TransformTool tool;
        Vector2 grabCursorOrigin;
        Vector2 grabRootOrigin;
        float grabRootRotation;
        float grabRootScale;
        float previewStartedAt;
        float previewDuration = 1f;
        string previewAnim = "linear";
        int previewX360;
        bool previewReturnEnabled = false;
        float previewReturnDelay = 0f;
        bool editingStart = true;

        struct CopySprite
        {
            public Vector2 LocalOffset;
            public float LocalRotation;
            public Vector2 LocalScale;
            public SpriteRenderer Renderer;
        }

        public bool IsActive { get; private set; }
        public bool EditingStart => editingStart;
        public ExecutorGroupState StartState => startState;
        public ExecutorGroupState EndState => endState;

        public bool Begin(
            IList<LevelTileServer> tiles,
            ExecutorGroupState savedStart,
            ExecutorGroupState savedEnd,
            float duration,
            string anim,
            int x360,
            int targetGroup,
            bool returnEnabled = false,
            float returnDelaySeconds = 0f)
        {
            End();
            if (tiles == null || tiles.Count == 0)
                return false;

            targetTiles.Clear();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] != null)
                    targetTiles.Add(tiles[i]);
            }

            if (targetTiles.Count == 0)
                return false;

            previewDuration = Mathf.Max(0.01f, duration);
            previewAnim = anim;
            previewX360 = x360;
            previewStartedAt = Time.time;
            previewReturnEnabled = returnEnabled;
            previewReturnDelay = Mathf.Max(0f, returnDelaySeconds);
            editingStart = false;
            basePivot = ComputePivot(targetTiles);
            startState = savedStart.hasValue ? savedStart : ExecutorGroupState.Identity(basePivot);
            endState = savedEnd.hasValue ? savedEnd : ExecutorGroupState.Identity(basePivot);

            rootsObject = new GameObject("ExecutorEditStatesRoots");
            startRoot = CreateRoot("Start");
            endRoot = CreateRoot("End");
            previewRoot = CreateRoot("Preview");
            BuildCopies(startRoot, startCopies, new Color(0.35f, 1f, 0.45f, 0.95f), 80);
            BuildCopies(endRoot, endCopies, new Color(0.35f, 0.7f, 1f, 0.95f), 80);
            BuildCopies(previewRoot, previewCopies, new Color(1f, 1f, 1f, 0.4f), 90);
            ApplyStateToRoot(startRoot, startState);
            ApplyStateToRoot(endRoot, endState);

            GameObject lineObject = new GameObject("ExecutorStateLine");
            lineObject.transform.SetParent(rootsObject.transform, false);
            stateLine = lineObject.AddComponent<LineRenderer>();
            stateLine.positionCount = 2;
            stateLine.startWidth = 0.06f;
            stateLine.endWidth = 0.06f;
            Material lineMaterial = Shader.Find("Sprites/Default") != null
                ? new Material(Shader.Find("Sprites/Default"))
                : new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            stateLine.material = lineMaterial;
            stateLine.startColor = new Color(0.3f, 1f, 1f, 0.8f);
            stateLine.endColor = new Color(0.3f, 1f, 1f, 0.8f);
            stateLine.sortingOrder = 260;

            LevelTileServer.ExecutorEditStatesActive = true;
            LevelTileServer.ExecutorEditStatesTargetGroup = targetGroup;
            RefreshWorldAppearance();
            SetActiveState(false);
            FlyCameraTo(basePivot);
            IsActive = true;
            return true;
        }

        public void Tick()
        {
            if (!IsActive)
                return;

            HandleTransformHotkeys();
            HandleActiveTool();
            HandleCopyClicks();
            UpdatePreview();
            UpdateLine();
            CaptureActiveRoot();
        }

        public void SetActiveState(bool editStart)
        {
            editingStart = editStart;
            CaptureActiveRoot();
            if (startRoot != null)
                TintCopies(startCopies, editStart ? new Color(0.35f, 1f, 0.45f, 1f) : new Color(0.35f, 1f, 0.45f, 0.45f));
            if (endRoot != null)
                TintCopies(endCopies, !editStart ? new Color(0.35f, 0.7f, 1f, 1f) : new Color(0.35f, 0.7f, 1f, 0.45f));
            tool = TransformTool.None;
        }

        public void CaptureBothStates()
        {
            bool wasStart = editingStart;
            editingStart = true;
            CaptureActiveRoot();
            editingStart = false;
            CaptureActiveRoot();
            editingStart = wasStart;
        }

        public void ResetStartState()
        {
            startState = ExecutorGroupState.Identity(basePivot);
            ApplyStateToRoot(startRoot, startState);
        }

        public void ResetEndState()
        {
            endState = ExecutorGroupState.Identity(basePivot);
            ApplyStateToRoot(endRoot, endState);
        }

        public void End()
        {
            if (IsActive)
                CaptureBothStates();

            IsActive = false;
            tool = TransformTool.None;
            LevelTileServer.ExecutorEditStatesActive = false;
            LevelTileServer.ExecutorEditStatesTargetGroup = -1;
            RefreshWorldAppearance();

            if (rootsObject != null)
                Object.Destroy(rootsObject);

            rootsObject = null;
            startRoot = null;
            endRoot = null;
            previewRoot = null;
            stateLine = null;
            startCopies.Clear();
            endCopies.Clear();
            previewCopies.Clear();
            targetTiles.Clear();
        }

        void HandleTransformHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            KaplaoCursorClient cursor = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            Vector2 cursorPos = cursor != null ? cursor.LocalWorldPosition : Vector2.zero;

            if (keyboard.gKey.wasPressedThisFrame)
                BeginTool(TransformTool.Move, cursorPos);
            else if (keyboard.rKey.wasPressedThisFrame)
                BeginTool(TransformTool.Rotate, cursorPos);
            else if (keyboard.tKey.wasPressedThisFrame)
                BeginTool(TransformTool.Scale, cursorPos);

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && tool != TransformTool.None)
                tool = TransformTool.None;
        }

        void BeginTool(TransformTool next, Vector2 cursorPos)
        {
            Transform root = ActiveRoot();
            if (root == null)
                return;

            tool = next;
            grabCursorOrigin = cursorPos;
            grabRootOrigin = root.position;
            grabRootRotation = root.eulerAngles.z;
            grabRootScale = root.localScale.x;
        }

        void HandleActiveTool()
        {
            if (tool == TransformTool.None)
                return;

            Transform root = ActiveRoot();
            if (root == null)
                return;

            KaplaoCursorClient cursor = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            Vector2 cursorPos = cursor != null ? cursor.LocalWorldPosition : grabCursorOrigin;

            switch (tool)
            {
                case TransformTool.Move:
                {
                    // Apply position snap from Create settings.
                    Vector2 delta = cursorPos - grabCursorOrigin;
                    delta.x = CreateModeSnapSettings.SnapDelta(delta.x, CreateModeSnapSettings.PositionSnapUnits);
                    delta.y = CreateModeSnapSettings.SnapDelta(delta.y, CreateModeSnapSettings.PositionSnapUnits);
                    root.position = grabRootOrigin + delta;
                    break;
                }
                case TransformTool.Rotate:
                {
                    // Apply rotation snap from Create settings.
                    Vector2 from = grabCursorOrigin - (Vector2)root.position;
                    Vector2 to = cursorPos - (Vector2)root.position;
                    float delta = Vector2.SignedAngle(from, to);
                    delta = CreateModeSnapSettings.SnapDelta(delta, CreateModeSnapSettings.RotationSnapDegrees);
                    root.rotation = Quaternion.Euler(0f, 0f, grabRootRotation + delta);
                    break;
                }
                case TransformTool.Scale:
                {
                    // Apply scale snap from Create settings.
                    float startDist = Mathf.Max(0.01f, Vector2.Distance(grabCursorOrigin, grabRootOrigin));
                    float nowDist = Mathf.Max(0.01f, Vector2.Distance(cursorPos, root.position));
                    float rawFactor = Mathf.Max(0.01f, grabRootScale * (nowDist / startDist));
                    float snappedFactor = CreateModeSnapSettings.SnapScale(rawFactor, CreateModeSnapSettings.ScaleSnapUnits);
                    root.localScale = new Vector3(snappedFactor, snappedFactor, 1f);
                    break;
                }
            }
        }

        void HandleCopyClicks()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || tool != TransformTool.None)
                return;

            KaplaoCursorClient cursor = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (cursor == null)
                return;

            Vector2 pos = cursor.LocalWorldPosition;
            if (HitCopies(startCopies, pos))
                SetActiveState(true);
            else if (HitCopies(endCopies, pos))
                SetActiveState(false);
        }

        static bool HitCopies(List<CopySprite> copies, Vector2 world)
        {
            for (int i = 0; i < copies.Count; i++)
            {
                SpriteRenderer sr = copies[i].Renderer;
                if (sr != null && sr.bounds.Contains(new Vector3(world.x, world.y, sr.bounds.center.z)))
                    return true;
            }

            return false;
        }

        void UpdatePreview()
        {
            if (previewRoot == null || startRoot == null || endRoot == null)
                return;

            float now = Time.time;
            
            // Calculate full cycle duration including return animation and delay
            float forwardDuration = previewDuration;
            float returnDuration = previewReturnEnabled ? previewDuration : 0f;
            float totalCycleDuration = forwardDuration + returnDuration + previewReturnDelay;
            
            if (totalCycleDuration <= 0f)
                totalCycleDuration = 1f; // Prevent division by zero
            
            // Get position in cycle (wraps around)
            float timeInCycle = Mathf.Repeat(now - previewStartedAt, totalCycleDuration);
            
            if (!previewReturnEnabled || timeInCycle < forwardDuration + previewReturnDelay)
            {
                // Forward animation (start -> end)
                float t = Mathf.Clamp01(timeInCycle / forwardDuration);
                float eased = ApplyEase(t, previewAnim);
                previewRoot.position = Vector3.Lerp(startRoot.position, endRoot.position, eased);
                float startZ = startRoot.eulerAngles.z;
                float endZ = endRoot.eulerAngles.z + previewX360 * 360f;
                previewRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startZ, endZ, eased));
                previewRoot.localScale = Vector3.Lerp(startRoot.localScale, endRoot.localScale, eased);
            }
            else
            {
                // Return animation (end -> start)
                float returnTimeInCycle = timeInCycle - forwardDuration - previewReturnDelay;
                if (returnTimeInCycle < 0f || returnDuration <= 0f)
                {
                    // Stay at end position during delay or until return starts
                    previewRoot.position = endRoot.position;
                    float endZ = endRoot.eulerAngles.z + previewX360 * 360f;
                    previewRoot.rotation = Quaternion.Euler(0f, 0f, endZ);
                    previewRoot.localScale = endRoot.localScale;
                }
                else
                {
                    float t = Mathf.Clamp01(returnTimeInCycle / returnDuration);
                    float eased = ApplyEase(t, previewAnim);
                    previewRoot.position = Vector3.Lerp(endRoot.position, startRoot.position, eased);
                    float endZ = endRoot.eulerAngles.z + previewX360 * 360f;
                    float startZ = startRoot.eulerAngles.z;
                    previewRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(endZ, startZ, eased));
                    previewRoot.localScale = Vector3.Lerp(endRoot.localScale, startRoot.localScale, eased);
                }
            }
        }

        void UpdateLine()
        {
            if (stateLine == null || startRoot == null || endRoot == null)
                return;

            stateLine.SetPosition(0, startRoot.position);
            stateLine.SetPosition(1, endRoot.position);
        }

        void CaptureActiveRoot()
        {
            Transform root = ActiveRoot();
            if (root == null)
                return;

            var state = new ExecutorGroupState
            {
                hasValue = true,
                pivotX = root.position.x,
                pivotY = root.position.y,
                rotation = root.eulerAngles.z,
                scale = Mathf.Max(0.01f, root.localScale.x)
            };

            if (editingStart)
                startState = state;
            else
                endState = state;
        }

        Transform ActiveRoot() => editingStart ? startRoot : endRoot;

        Transform CreateRoot(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(rootsObject.transform, false);
            go.transform.position = new Vector3(basePivot.x, basePivot.y, 0f);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        void BuildCopies(Transform root, List<CopySprite> output, Color tint, int extraOrder)
        {
            output.Clear();
            for (int i = 0; i < targetTiles.Count; i++)
            {
                LevelTileServer tile = targetTiles[i];
                SpriteRenderer source = tile != null ? tile.GetComponent<SpriteRenderer>() : null;
                GameObject go = new GameObject($"Copy_{i}");
                go.transform.SetParent(root, false);
                Vector2 offset = tile.Position - basePivot;
                go.transform.localPosition = offset;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, tile.PlacementRotationDegrees);
                go.transform.localScale = new Vector3(tile.Scale.x, tile.Scale.y, 1f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = source != null ? source.sprite : null;
                sr.color = tint;
                sr.sortingOrder = tile.SortingOrder + extraOrder;
                output.Add(new CopySprite
                {
                    LocalOffset = offset,
                    LocalRotation = tile.PlacementRotationDegrees,
                    LocalScale = tile.Scale,
                    Renderer = sr
                });
            }
        }

        static void ApplyStateToRoot(Transform root, ExecutorGroupState state)
        {
            if (root == null || !state.hasValue)
                return;

            root.position = new Vector3(state.pivotX, state.pivotY, 0f);
            root.rotation = Quaternion.Euler(0f, 0f, state.rotation);
            float s = state.scale <= 0f ? 1f : state.scale;
            root.localScale = new Vector3(s, s, 1f);
        }

        static void TintCopies(List<CopySprite> copies, Color color)
        {
            for (int i = 0; i < copies.Count; i++)
            {
                if (copies[i].Renderer != null)
                    copies[i].Renderer.color = color;
            }
        }

        static Vector2 ComputePivot(List<LevelTileServer> tiles)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null)
                    continue;
                sum += tiles[i].Position;
                count++;
            }

            return count > 0 ? sum / count : Vector2.zero;
        }

        static void RefreshWorldAppearance()
        {
            LevelTileServer[] tiles = Object.FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i]?.RefreshClientAppearance();
        }

        static void FlyCameraTo(Vector2 world)
        {
            if (NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.TryGetComponent(out GameCameraClient camera))
            {
                camera.FlyToWorld(world);
            }
        }

        static float ApplyEase(float t, string anim)
        {
            LevelEaseUtility.TryParse(anim, out LevelEase ease);
            return LevelEaseUtility.Evaluate(ease, t);
        }
    }
}
