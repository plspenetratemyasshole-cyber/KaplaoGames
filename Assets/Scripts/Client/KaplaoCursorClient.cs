using System;
using System.Collections.Generic;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Reads Kaplao mouse input on the local client and sends it to the server.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KaplaoCursorServer))]
    [AddComponentMenu("Kaplao/Client/Kaplao Cursor Client")]
    public class KaplaoCursorClient : NetworkBehaviour
    {
        KaplaoCursorServer cursorServer;
        KaplaoTileColorState tileColorState;
        Camera worldCamera;
        KaplaoToolMode toolMode = KaplaoToolMode.Grab;
        byte spikeQuarterTurns;
        Vector2? selectionDragStart;
        bool selectionDragging;
        Vector2 lastPointerScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        int activeMobileTouchId = -1;
        Vector2 pointerGestureStartScreen;
        bool pointerGestureMoved;
        const float PlaceClickMaxMovePixels = 4f;
        const float CursorSendInterval = 1f / 30f;
        float nextCursorSendTime;
        Vector2 lastSentCursorPosition;

        public static KaplaoCursorClient Local { get; private set; }
        static int suppressApplyPlaceColorDepth;

        public KaplaoToolMode ToolMode => toolMode;
        public bool IsSelectionDragging => selectionDragging;
        public Vector2 SelectionDragStart => selectionDragStart ?? Vector2.zero;
        public Vector2 SelectionDragEnd { get; private set; }
        public Vector2 LocalWorldPosition { get; private set; }
        public bool ShiftHeld { get; private set; }
        /// <summary>
        /// True while the local Kaplao is in an active G/R/T gesture.
        /// Do not gate input on the SyncVar — it lags under high ping and blocks re-selection.
        /// </summary>
        public bool IsCreateTransformActive => KaplaoLocalTransformSession.IsActive;

        public static void BeginSuppressApplyPlaceColorToSelection() => suppressApplyPlaceColorDepth++;

        public static void EndSuppressApplyPlaceColorToSelection()
        {
            if (suppressApplyPlaceColorDepth > 0)
                suppressApplyPlaceColorDepth--;
        }

        void Awake()
        {
            cursorServer = GetComponent<KaplaoCursorServer>();
            tileColorState = GetComponent<KaplaoTileColorState>();
            enabled = false;
        }

        public static void EnsureClientReadyForCommands()
        {
            // SetClientNotReady (from older resync code) leaves the client unable to send Commands.
            if (NetworkClient.active && !NetworkClient.ready)
                NetworkClient.Ready();
        }

        public override void OnStartLocalPlayer()
        {
            Local = this;
            worldCamera = Camera.main;
            enabled = true;

            if (cursorServer != null)
            {
                cursorServer.HostGameModeChanged += OnHostGameModeChanged;
                ApplyToolModeForGameMode(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
            }

            if (tileColorState != null)
                tileColorState.ColorChanged += OnPlaceColorChanged;

            EnsureClientReadyForCommands();
            ApplySystemCursorVisibility();
            PushLocalSnapSettingsToServer();
            PushLocalHideCursorToServer();
        }

        public void PushLocalSnapSettingsToServer()
        {
            if (!isLocalPlayer)
                return;

            CreateModeSnapSettings.LoadLocal();
            CmdSetSnapSettings(
                CreateModeSnapSettings.RotationSnapDegrees,
                CreateModeSnapSettings.PositionSnapUnits,
                CreateModeSnapSettings.ScaleSnapUnits);
        }

        public void PushLocalHideCursorToServer()
        {
            if (!isLocalPlayer)
                return;

            CreateModeEditorSettings.LoadLocal();
            CmdSetHideCursor(CreateModeEditorSettings.HideCursor);
        }

        public override void OnStopLocalPlayer()
        {
            if (Local == this)
                Local = null;

            KaplaoLocalTransformSession.ForceClear();
            KaplaoLocalGameModePredictor.Clear();
            KaplaoLocalEditPredictor.ClearAll();

            if (cursorServer != null)
                cursorServer.HostGameModeChanged -= OnHostGameModeChanged;

            if (tileColorState != null)
                tileColorState.ColorChanged -= OnPlaceColorChanged;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            enabled = false;
            selectionDragStart = null;
            selectionDragging = false;
            activeMobileTouchId = -1;
            CmdSendCursor(default);
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            KaplaoLocalGameModePredictor.HandleServerSync(mode);
            HostGameMode effective = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            if (mode != HostGameMode.Create)
                KaplaoLocalTransformSession.ForceClear();

            ApplyToolModeForGameMode(effective);
            ApplySystemCursorVisibility();
        }

        public void RequestSetHostGameMode(HostGameMode mode)
        {
            if (!isLocalPlayer)
                return;

            KaplaoExecutorEditorUi.FlushAllOpenEditorsBeforeModeChange();
            KaplaoLocalGameModePredictor.Predict(mode);
            ApplyToolModeForGameMode(mode);
            ApplySystemCursorVisibility();
            EnsureClientReadyForCommands();
            CmdSetHostGameMode(mode);
        }

        void ApplySystemCursorVisibility()
        {
            if (!isLocalPlayer)
                return;

            if (cursorServer != null && cursorServer.HideCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            if (MobileControlsConfig.IsMobileControlsEnabled)
            {
                // Keep OS cursor visible while testing phone controls on PC.
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            if (PauseCursorState.IsPauseOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            if (mode == HostGameMode.Create || mode == HostGameMode.Code)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            Cursor.visible = false;
        }

        void ApplyToolModeForGameMode(HostGameMode mode)
        {
            if (mode == HostGameMode.Usual)
            {
                toolMode = KaplaoToolMode.Grab;
                return;
            }

            if (mode == HostGameMode.Create && toolMode == KaplaoToolMode.Grab)
                toolMode = KaplaoToolMode.Select;
        }

        void Update()
        {
            if (!isLocalPlayer)
                return;

            // After ReplacePlayerForConnection, stale local-player objects can still exist on host.
            // Only the current NetworkClient.localPlayer should send cursor input.
            if (NetworkClient.localPlayer == null ||
                NetworkClient.localPlayer != netIdentity)
                return;

            KaplaoCursorInput input = ReadCursorInput();
            LocalWorldPosition = input.worldPosition;
            if (KaplaoExecutorEditorUi.BlocksCreateHud)
            {
                ApplySystemCursorVisibility();
                return;
            }

            HandleLocalTransformPrediction(input);

            // Throttle Commands — sending every Update saturates Mirror on the main thread.
            bool buttonEdge = input.primaryPressed || input.primaryReleased ||
                              input.secondaryPressed || input.secondaryReleased;
            bool transformEdge = input.transformRotatePressed || input.transformMovePressed ||
                                 input.transformScalePressed || input.axisXPressed || input.axisYPressed;
            bool movedEnough = (input.worldPosition - lastSentCursorPosition).sqrMagnitude > 0.0004f;
            bool due = Time.unscaledTime >= nextCursorSendTime;
            bool transformDrag = KaplaoLocalTransformSession.IsActive && (due || movedEnough);
            if (buttonEdge || transformEdge || transformDrag || (due && (movedEnough || input.primaryHeld)))
            {
                nextCursorSendTime = Time.unscaledTime + CursorSendInterval;
                lastSentCursorPosition = input.worldPosition;
                CmdSendCursor(input);
            }

            ApplySystemCursorVisibility();
        }

        void HandleLocalTransformPrediction(KaplaoCursorInput input)
        {
            if (cursorServer == null || !KaplaoLocalGameModePredictor.IsCreate(cursorServer))
                return;

            if (input.transformRotatePressed || input.transformMovePressed || input.transformScalePressed)
            {
                CreateTransformMode mode = CreateTransformMode.None;
                if (input.transformRotatePressed)
                    mode = CreateTransformMode.Rotate;
                else if (input.transformMovePressed)
                    mode = CreateTransformMode.Move;
                else if (input.transformScalePressed)
                    mode = CreateTransformMode.Scale;

                if (KaplaoLocalTransformSession.TryBegin(mode, cursorServer))
                {
                    EnsureClientReadyForCommands();
                    CmdBeginCreateTransform((byte)mode);
                }
            }

            if (!KaplaoLocalTransformSession.IsActive)
                return;

            if (input.axisXPressed)
                KaplaoLocalTransformSession.SetAxis(TransformAxisConstraint.X);
            else if (input.axisYPressed)
                KaplaoLocalTransformSession.SetAxis(TransformAxisConstraint.Y);

            if (input.primaryReleased)
            {
                if (KaplaoLocalTransformSession.TryCollectFinal(
                        out uint[] netIds,
                        out Vector2[] positions,
                        out float[] rotations,
                        out Vector2[] scales))
                {
                    KaplaoLocalTransformSession.Confirm();
                    EnsureClientReadyForCommands();
                    CmdFinalizeCreateTransform(netIds, positions, rotations, scales);
                }
                else
                {
                    KaplaoLocalTransformSession.Confirm();
                }

                return;
            }

            if (input.secondaryReleased)
            {
                if (KaplaoLocalTransformSession.TryCollectBaselines(
                        out uint[] netIds,
                        out Vector2[] positions,
                        out float[] rotations,
                        out Vector2[] scales))
                {
                    KaplaoLocalTransformSession.Cancel();
                    EnsureClientReadyForCommands();
                    CmdCancelCreateTransform(netIds, positions, rotations, scales);
                }
                else
                {
                    KaplaoLocalTransformSession.Cancel();
                }

                return;
            }

            KaplaoLocalTransformSession.Update(input.worldPosition);
        }

        public void MobileMoveSelectedTiles(int deltaX, int deltaY)
        {
            if (!isLocalPlayer || cursorServer == null)
                return;

            if (!KaplaoLocalGameModePredictor.IsCreate(cursorServer) ||
                cursorServer.SelectedCount == 0 ||
                IsCreateTransformActive)
            {
                return;
            }

            float moveStep = GetSelectionMoveStep();
            RequestMoveSelectedTilesBy(deltaX * moveStep, deltaY * moveStep);
        }

        public void MobileDuplicateSelectedTiles()
        {
            if (!isLocalPlayer || cursorServer == null)
                return;

            if (!KaplaoLocalGameModePredictor.IsCreate(cursorServer) ||
                cursorServer.SelectedCount == 0 ||
                IsCreateTransformActive)
            {
                return;
            }

            EnsureClientReadyForCommands();
            CmdDuplicateSelectedTilesRight();
        }

        public void RequestMoveSelectedTilesBy(float dx, float dy)
        {
            if (!isLocalPlayer || cursorServer == null)
                return;

            var selected = cursorServer.SelectedTiles;
            var netIds = new uint[selected.Count];
            for (int i = 0; i < selected.Count; i++)
                netIds[i] = selected[i];

            KaplaoLocalEditPredictor.PredictMoveBy(cursorServer, dx, dy);
            EnsureClientReadyForCommands();
            CmdMoveSelectedTilesBy(dx, dy, netIds);
        }

        public void RequestDeleteSelectedTiles()
        {
            if (!isLocalPlayer)
                return;

            EnsureClientReadyForCommands();
            CmdDeleteSelectedTiles();
        }

        public void RequestApplyColorToSelectedTiles(Color32 color)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetColor(cursorServer, color);
            EnsureClientReadyForCommands();
            CmdApplyColorToSelectedTiles(color);
        }

        public void RequestApplyTextureToSelectedTiles(string textureId)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetTexture(cursorServer, textureId);
            EnsureClientReadyForCommands();
            CmdApplyTextureToSelectedTiles(textureId);
        }

        public void RequestApplySortingOrderToSelectedTiles(int sortingOrder)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetSortingOrder(cursorServer, sortingOrder);
            EnsureClientReadyForCommands();
            CmdApplySortingOrderToSelectedTiles(sortingOrder);
        }

        public void RequestAddGroupToSelectedTiles(int group)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictAddGroup(cursorServer, group);
            EnsureClientReadyForCommands();
            CmdAddGroupToSelectedTiles(group);
        }

        public void RequestRemoveGroupFromSelectedTiles(int group)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictRemoveGroup(cursorServer, group);
            EnsureClientReadyForCommands();
            CmdRemoveGroupFromSelectedTiles(group);
        }

        public void RequestSetSelectedTilesGroups(string groups)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetGroups(cursorServer, groups);
            EnsureClientReadyForCommands();
            CmdSetSelectedTilesGroups(groups);
        }

        public void RequestSetSelectedTilesTransparency(float transparency)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetTransparency(cursorServer, transparency);
            EnsureClientReadyForCommands();
            CmdSetSelectedTilesTransparency(transparency);
        }

        public void RequestSetSelectedTilesEditorLayer(int editorLayer)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetEditorLayer(cursorServer, editorLayer);
            EnsureClientReadyForCommands();
            CmdSetSelectedTilesEditorLayer(editorLayer);
        }

        public void RequestSetSelectedTilesNoHitbox(bool noHitbox)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetNoHitbox(cursorServer, noHitbox);
            EnsureClientReadyForCommands();
            CmdSetSelectedTilesNoHitbox(noHitbox);
        }

        public void RequestApplySpawnPointToSelection(bool isSpawnPoint)
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalEditPredictor.PredictSetSpawnPoint(cursorServer, isSpawnPoint);
            EnsureClientReadyForCommands();
            CmdApplySpawnPointToSelection(isSpawnPoint);
        }

        public void RequestSetSelectedTileTransform(
            uint netId,
            float x,
            float y,
            float rotationDegrees,
            float scaleX,
            float scaleY,
            int sortingOrder)
        {
            if (!isLocalPlayer)
                return;

            var patch = TileTransformPatchRecord.Full(x, y, rotationDegrees, scaleX, scaleY, sortingOrder);
            KaplaoLocalEditPredictor.PredictSetTransform(
                netId,
                new Vector2(x, y),
                rotationDegrees,
                new Vector2(scaleX, scaleY));
            EnsureClientReadyForCommands();
            CmdApplyTransformPatchToTile(netId, patch);
        }

        public void RequestApplyTransformPatchToSelection(TileTransformPatchRecord patch)
        {
            if (!isLocalPlayer || !patch.HasAnyField)
                return;

            KaplaoLocalEditPredictor.PredictApplyTransformPatch(cursorServer, patch);
            EnsureClientReadyForCommands();
            CmdApplyTransformPatchToSelection(patch);
        }

        public void RequestSetSelection(uint[] netIds)
        {
            if (!isLocalPlayer)
                return;

            EnsureClientReadyForCommands();
            CmdSetSelection(netIds);
        }

        public void RequestUndoLevelEdit()
        {
            if (!isLocalPlayer)
                return;

            KaplaoLocalTransformSession.ForceClear();
            KaplaoLocalEditPredictor.ClearAll();
            EnsureClientReadyForCommands();
            CmdUndoLevelEdit();
        }

        public void MobileSelectToolSlot(int slot)
        {
            if (!isLocalPlayer || cursorServer == null)
                return;

            HostGameMode gameMode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            ApplyToolSlotForMode(gameMode, slot);
        }

        float GetSelectionMoveStep()
        {
            Keyboard keyboard = Keyboard.current;
            bool shiftHeld = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) ||
                             MobileInputState.ShiftHeld;
            bool ctrlHeld = (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) ||
                            MobileInputState.CtrlHeld;

            if (shiftHeld)
                return 0.1f;
            if (ctrlHeld)
                return 5f;
            return 1f;
        }

        KaplaoCursorInput ReadCursorInput()
        {
            HostGameMode gameMode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            bool mobileControls = MobileControlsConfig.IsMobileControlsEnabled;
            bool uiBlocksInput = IsUiBlockingGameInput();
            if (mobileControls && MobileInputState.IsKaplaoMovingOnMobile())
                uiBlocksInput = true;
            bool keyboardBlockedByTextField = IsTextInputFocused();
            bool inCreateTransform = IsCreateTransformActive;

            Keyboard keyboard = Keyboard.current;
            bool shiftHeld = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) ||
                             MobileInputState.ShiftHeld;
            bool ctrlHeld = (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) ||
                            MobileInputState.CtrlHeld;
            if (keyboard != null && !uiBlocksInput && !keyboardBlockedByTextField)
            {
                if (gameMode == HostGameMode.Create &&
                    (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) &&
                    keyboard.zKey.wasPressedThisFrame)
                {
                    RequestUndoLevelEdit();
                }

                if (gameMode == HostGameMode.Create)
                {
                    if (keyboard.digit2Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Block;

                    if (keyboard.digit3Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Spike;

                    if (keyboard.digit4Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Trigger;

                    if (keyboard.digit5Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Delete;

                    if (keyboard.digit6Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Particle;

                    if (keyboard.digit7Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Keyframe;

                    if (keyboard.digit8Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Executor;
                }

                if (keyboard.digit1Key.wasPressedThisFrame)
                    toolMode = gameMode == HostGameMode.Create ? KaplaoToolMode.Select : KaplaoToolMode.Grab;

                if (gameMode == HostGameMode.Usual)
                {
                    if (keyboard.digit2Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Palm;

                    if (keyboard.digit3Key.wasPressedThisFrame)
                        toolMode = KaplaoToolMode.Gun;
                }

                if (keyboard.rKey.wasPressedThisFrame &&
                    toolMode == KaplaoToolMode.Spike)
                {
                    spikeQuarterTurns = (byte)((spikeQuarterTurns + 1) % 4);
                }
            }

            bool hasSelectionInCreate = gameMode == HostGameMode.Create &&
                                        cursorServer != null &&
                                        cursorServer.SelectedCount > 0;

            // Selection hotkeys must work even when the mouse is over editor UI
            // (settings panel, toolbars). Only block them while typing in a text field.
            if (hasSelectionInCreate && !inCreateTransform && !keyboardBlockedByTextField)
            {
                float moveStep = 1f;
                if (shiftHeld)
                    moveStep = 0.1f;
                else if (ctrlHeld)
                    moveStep = 5f;

                if (keyboard != null)
                {
                    if (keyboard.wKey.wasPressedThisFrame)
                        RequestMoveSelectedTilesBy(0f, moveStep);
                    else if (keyboard.sKey.wasPressedThisFrame)
                        RequestMoveSelectedTilesBy(0f, -moveStep);
                    else if (keyboard.aKey.wasPressedThisFrame)
                        RequestMoveSelectedTilesBy(-moveStep, 0f);
                    else if (ctrlHeld && keyboard.dKey.wasPressedThisFrame)
                    {
                        EnsureClientReadyForCommands();
                        CmdDuplicateSelectedTilesRight();
                    }
                    else if (keyboard.dKey.wasPressedThisFrame)
                        RequestMoveSelectedTilesBy(moveStep, 0f);

                    if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                        RequestDeleteSelectedTiles();

                    if (ctrlHeld && keyboard.cKey.wasPressedThisFrame)
                    {
                        EnsureClientReadyForCommands();
                        CmdCopySelectedTiles();
                    }
                }

                if (mobileControls &&
                    MobileInputState.TryConsumeSelectionDuplicate())
                {
                    MobileDuplicateSelectedTiles();
                }

                if (mobileControls &&
                    MobileInputState.TryConsumeSelectionMove(out int deltaX, out int deltaY))
                {
                    MobileMoveSelectedTiles(deltaX, deltaY);
                }
            }

            if (gameMode == HostGameMode.Create &&
                !keyboardBlockedByTextField &&
                keyboard != null &&
                ctrlHeld &&
                keyboard.vKey.wasPressedThisFrame)
            {
                EnsureClientReadyForCommands();
                Vector2 pasteCenter = GetScreenCenterWorldPosition();
                CmdPasteClipboardTiles(pasteCenter.x, pasteCenter.y);
            }

            if (MobileInputState.TryConsumeToolSlot(out int slot))
                ApplyToolSlotForMode(gameMode, slot);

            Mouse mouse = Mouse.current;
            if (worldCamera == null)
            {
                ShiftHeld = shiftHeld;
                return default;
            }

            bool primaryHeld = false;
            bool primaryPressed = false;
            bool primaryReleased = false;
            bool secondaryPressed = false;
            bool secondaryReleased = false;
            Vector2 screenPosition = lastPointerScreenPosition;

            bool rawPrimaryHeld = false;
            bool rawPrimaryPressed = false;
            bool rawPrimaryReleased = false;
            bool rawSecondaryPressed = false;
            bool rawSecondaryReleased = false;

            if (mobileControls)
            {
                bool gotMobilePointer = TryReadMobilePointer(ref screenPosition, out rawPrimaryHeld, out rawPrimaryPressed, out rawPrimaryReleased);
                if (gotMobilePointer)
                {
                    lastPointerScreenPosition = screenPosition;
                }
                else if (mouse != null)
                {
                    // Desktop fallback for testing mobile mode without touch input.
                    screenPosition = mouse.position.ReadValue();
                    lastPointerScreenPosition = screenPosition;
                    rawPrimaryHeld = mouse.leftButton.isPressed;
                    rawPrimaryPressed = mouse.leftButton.wasPressedThisFrame;
                    rawPrimaryReleased = mouse.leftButton.wasReleasedThisFrame;
                }
            }
            else
            {
                if (mouse == null)
                {
                    ShiftHeld = shiftHeld;
                    return default;
                }

                screenPosition = mouse.position.ReadValue();
                lastPointerScreenPosition = screenPosition;
                rawPrimaryHeld = mouse.leftButton.isPressed;
                rawPrimaryPressed = mouse.leftButton.wasPressedThisFrame;
                rawPrimaryReleased = mouse.leftButton.wasReleasedThisFrame;
                rawSecondaryPressed = mouse.rightButton.wasPressedThisFrame;
                rawSecondaryReleased = mouse.rightButton.wasReleasedThisFrame;
            }

            // Ongoing Shift/select drag must survive accidental UI hover; only block STARTING a drag on UI.
            bool preserveSelectionDrag = selectionDragging &&
                                         gameMode == HostGameMode.Create &&
                                         toolMode == KaplaoToolMode.Select &&
                                         !inCreateTransform;
            if (preserveSelectionDrag)
            {
                primaryHeld = rawPrimaryHeld;
                primaryPressed = false;
                primaryReleased = rawPrimaryReleased;
                secondaryPressed = false;
                secondaryReleased = false;
            }
            else
            {
                primaryHeld = !uiBlocksInput && rawPrimaryHeld;
                primaryPressed = !uiBlocksInput && rawPrimaryPressed;
                primaryReleased = !uiBlocksInput && rawPrimaryReleased;
                secondaryPressed = !uiBlocksInput && rawSecondaryPressed;
                secondaryReleased = !uiBlocksInput && rawSecondaryReleased;
            }

            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
            worldPosition.z = 0f;
            Vector2 worldPoint = worldPosition;

            TrackPointerGesture(screenPosition, primaryPressed, primaryHeld, primaryReleased);

            // Pan-vs-click cancel does not apply while Shift is held (paint mode) or mid selection drag.
            bool cancelFromPan = GameCameraClient.ConsumeCancelClickOnRelease();
            bool cancelClick = !shiftHeld &&
                               !preserveSelectionDrag &&
                               (cancelFromPan || (primaryReleased && pointerGestureMoved));
            if (cancelClick)
            {
                primaryHeld = false;
                primaryPressed = false;
                primaryReleased = false;
                selectionDragging = false;
                selectionDragStart = null;
            }

            if (!shiftHeld && !preserveSelectionDrag && GameCameraClient.IsBlockingCursor)
            {
                primaryHeld = false;
                primaryPressed = false;
                primaryReleased = false;
                selectionDragging = false;
                selectionDragStart = null;
            }

            if (primaryReleased || (!primaryHeld && !primaryPressed))
                pointerGestureMoved = false;

            bool canManageSelectionDrag =
                gameMode == HostGameMode.Create &&
                toolMode == KaplaoToolMode.Select &&
                !inCreateTransform;

            if (canManageSelectionDrag && (preserveSelectionDrag || !uiBlocksInput))
            {
                if (primaryPressed)
                {
                    selectionDragStart = worldPoint;
                    selectionDragging = true;
                }

                if (primaryReleased)
                    selectionDragging = false;

                if (selectionDragging)
                    SelectionDragEnd = worldPoint;
            }
            else if (!preserveSelectionDrag)
            {
                selectionDragStart = null;
                selectionDragging = false;
            }

            bool transformRotatePressed = false;
            bool transformMovePressed = false;
            bool transformScalePressed = false;
            bool axisXPressed = false;
            bool axisYPressed = false;

            if (!uiBlocksInput &&
                keyboard != null &&
                gameMode == HostGameMode.Create &&
                toolMode == KaplaoToolMode.Select &&
                cursorServer != null &&
                cursorServer.SelectedCount > 0)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                    transformRotatePressed = true;
                if (keyboard.gKey.wasPressedThisFrame)
                    transformMovePressed = true;
                if (keyboard.tKey.wasPressedThisFrame)
                    transformScalePressed = true;

                if (inCreateTransform)
                {
                    if (keyboard.xKey.wasPressedThisFrame)
                        axisXPressed = true;
                    if (keyboard.yKey.wasPressedThisFrame)
                        axisYPressed = true;
                }
            }

            ShiftHeld = shiftHeld;
            return new KaplaoCursorInput
            {
                worldPosition = worldPoint,
                primaryHeld = primaryHeld,
                primaryPressed = primaryPressed,
                primaryReleased = primaryReleased,
                secondaryPressed = secondaryPressed,
                secondaryReleased = secondaryReleased,
                shiftHeld = shiftHeld,
                ctrlHeld = ctrlHeld,
                toolMode = toolMode,
                placeColor = tileColorState != null
                    ? tileColorState.PlaceColor
                    : TileColorPalette.DefaultPlace,
                selectionDragStart = selectionDragStart ?? worldPoint,
                selectionDragging = selectionDragging,
                transformRotatePressed = transformRotatePressed,
                transformMovePressed = transformMovePressed,
                transformScalePressed = transformScalePressed,
                axisXPressed = axisXPressed,
                axisYPressed = axisYPressed,
                spikeQuarterTurns = spikeQuarterTurns
            };
        }

        void TrackPointerGesture(Vector2 screenPosition, bool primaryPressed, bool primaryHeld, bool primaryReleased)
        {
            if (primaryPressed)
            {
                pointerGestureStartScreen = screenPosition;
                pointerGestureMoved = false;
            }

            if (!(primaryHeld || primaryPressed || primaryReleased) || pointerGestureMoved)
                return;

            float threshold = PlaceClickMaxMovePixels;
            if ((screenPosition - pointerGestureStartScreen).sqrMagnitude >= threshold * threshold)
                pointerGestureMoved = true;
        }

        void ApplyToolSlotForMode(HostGameMode gameMode, int slot)
        {
            if (slot <= 0)
                return;

            if (gameMode == HostGameMode.Usual)
            {
                toolMode = slot switch
                {
                    1 => KaplaoToolMode.Grab,
                    2 => KaplaoToolMode.Palm,
                    3 => KaplaoToolMode.Gun,
                    _ => toolMode
                };
                return;
            }

            if (gameMode == HostGameMode.Create)
            {
                toolMode = slot switch
                {
                    1 => KaplaoToolMode.Select,
                    2 => KaplaoToolMode.Block,
                    3 => KaplaoToolMode.Spike,
                    4 => KaplaoToolMode.Trigger,
                    5 => KaplaoToolMode.Delete,
                    6 => KaplaoToolMode.Particle,
                    7 => KaplaoToolMode.Keyframe,
                    8 => KaplaoToolMode.Executor,
                    _ => toolMode
                };
            }
        }

        bool TryReadMobilePointer(ref Vector2 screenPosition, out bool held, out bool pressed, out bool released)
        {
            held = false;
            pressed = false;
            released = false;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return false;

            var touches = touchscreen.touches;
            if (touches.Count == 0)
                return false;

            UnityEngine.InputSystem.Controls.TouchControl candidate = null;

            if (activeMobileTouchId >= 0)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.touchId.ReadValue() == activeMobileTouchId)
                    {
                        candidate = touch;
                        break;
                    }
                }
            }

            if (candidate == null)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    int touchId = touch.touchId.ReadValue();
                    if (touchId <= 0)
                        continue;

                    bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
                    if (overUi)
                        continue;

                    if (touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame)
                    {
                        candidate = touch;
                        break;
                    }
                }
            }

            if (candidate == null)
            {
                activeMobileTouchId = -1;
                return false;
            }

            int candidateId = candidate.touchId.ReadValue();
            if (candidateId <= 0)
            {
                activeMobileTouchId = -1;
                return false;
            }

            bool candidateOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(candidateId);
            if (candidateOverUi || MobileInputState.IsUiPointer(candidateId))
            {
                activeMobileTouchId = -1;
                return false;
            }

            screenPosition = candidate.position.ReadValue();
            held = candidate.press.isPressed;
            pressed = candidate.press.wasPressedThisFrame;
            released = candidate.press.wasReleasedThisFrame;

            activeMobileTouchId = held || pressed ? candidateId : -1;
            if (released)
                activeMobileTouchId = -1;
            return held || pressed || released;
        }

        void OnPlaceColorChanged(Color32 color)
        {
            if (suppressApplyPlaceColorDepth > 0)
                return;

            if (!isLocalPlayer || cursorServer == null || !KaplaoLocalGameModePredictor.IsCreate(cursorServer))
                return;

            if (cursorServer.SelectedCount == 0)
                return;

            RequestApplyColorToSelectedTiles(color);
        }

        static bool IsUiBlockingGameInput()
        {
            if (MobileInputState.HasAnyUiPointer())
                return true;

            Vector2 screen = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;

            if (MobileControlsConfig.IsMobileControlsEnabled && Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    int touchId = touch.touchId.ReadValue();
                    if (touchId <= 0 || !touch.press.isPressed)
                        continue;

                    screen = touch.position.ReadValue();
                    if (KaplaoUiPointer.IsInteractiveUiAtScreen(screen))
                        return true;
                }
            }

            if (Mouse.current != null && KaplaoUiPointer.IsInteractiveUiAtScreen(screen))
                return true;

            return false;
        }

        static bool IsTextInputFocused()
        {
            if (KaplaoModeratedChatUi.IsChatInputFocused)
                return true;

            if (EventSystem.current == null)
                return false;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
                return false;

            if (selected.GetComponent<TMPro.TMP_InputField>() != null)
                return true;

            return selected.GetComponent<UnityEngine.UI.InputField>() != null;
        }

        [Command]
        void CmdSendCursor(KaplaoCursorInput input)
        {
            cursorServer.ApplyInput(input);
        }

        [Command]
        public void CmdSetHostGameMode(HostGameMode mode)
        {
            cursorServer.SetHostGameMode(mode);
        }

        [Command]
        public void CmdClearSelection()
        {
            cursorServer.ClearSelection();
        }

        [Command]
        public void CmdDeleteSelectedTiles()
        {
            cursorServer.DeleteSelectedTiles();
        }

        [Command]
        public void CmdSetSelectedTilesGroups(string groups)
        {
            cursorServer.SetSelectedTilesGroups(groups);
        }

        [Command]
        public void CmdAddGroupToSelectedTiles(int group)
        {
            cursorServer.AddGroupToSelectedTiles(group);
        }

        [Command]
        public void CmdRemoveGroupFromSelectedTiles(int group)
        {
            cursorServer.RemoveGroupFromSelectedTiles(group);
        }

        [Command]
        public void CmdSetSelectedTilesTransparency(float transparency)
        {
            cursorServer.SetSelectedTilesTransparency(transparency);
        }

        [Command]
        public void CmdSetSelectedTilesEditorLayer(int editorLayer)
        {
            cursorServer.SetSelectedTilesEditorLayer(editorLayer);
        }

        [Command]
        public void CmdStepEditorLayer(int delta)
        {
            cursorServer.StepEditorLayer(delta);
        }

        [Command]
        public void CmdLinkSelectedTiles()
        {
            cursorServer.LinkSelectedTiles();
        }

        [Command]
        public void CmdUnlinkSelectedTiles()
        {
            cursorServer.UnlinkSelectedTiles();
        }

        [Command]
        public void CmdSetSelectedTilesNoHitbox(bool noHitbox)
        {
            cursorServer.SetSelectedTilesNoHitbox(noHitbox);
        }

        [Command]
        void CmdApplyColorToSelectedTiles(Color32 color)
        {
            cursorServer.ApplyColorToSelectedTiles(color);
        }

        [Command]
        public void CmdApplyTextureToSelectedTiles(string textureId)
        {
            cursorServer.ApplyTextureToSelectedTiles(textureId);
        }

        [Command]
        public void CmdApplyParticleSettingsToSelectedTiles(ParticleEmitterRecord record)
        {
            cursorServer.ApplyParticleSettingsToSelectedTiles(record);
        }

        [Command]
        public void CmdApplySortingOrderToSelectedTiles(int sortingOrder)
        {
            cursorServer.ApplySortingOrderToSelectedTiles(sortingOrder);
        }

        [Command]
        public void CmdRunLevelScript(string source)
        {
            cursorServer.RunLevelScript(source);
        }

        [Command]
        public void CmdStopLevelScript()
        {
            cursorServer.StopLevelScript();
        }

        [Command]
        public void CmdSendScriptEventToServer(string eventName, string jsonArgs)
        {
            cursorServer.HandleScriptEvent(eventName, jsonArgs);
        }

        [Command]
        public void CmdImportLevel(string json)
        {
            cursorServer.ImportLevel(json);
        }

        public void RequestImportLevel(string json)
        {
            if (!isOwned || string.IsNullOrEmpty(json))
                return;

            if (NetworkServer.active)
            {
                cursorServer.ImportLevel(json);
                return;
            }

            const int chunkSize = 32000;
            CmdBeginImportLevel(json.Length);
            for (int offset = 0; offset < json.Length; offset += chunkSize)
            {
                int length = Math.Min(chunkSize, json.Length - offset);
                CmdImportLevelChunk(json.Substring(offset, length));
            }

            CmdFinishImportLevel();
        }

        [Command]
        void CmdBeginImportLevel(int totalLength)
        {
            cursorServer.BeginImportLevel(totalLength);
        }

        [Command]
        void CmdImportLevelChunk(string chunk)
        {
            cursorServer.AppendImportLevelChunk(chunk);
        }

        [Command]
        void CmdFinishImportLevel()
        {
            cursorServer.FinishImportLevel();
        }

        [Command]
        public void CmdSetLevelScript(string script)
        {
            cursorServer.SetLevelScript(script);
        }

        [Command]
        public void CmdWriteScriptFile(string relativePath, string content)
        {
            cursorServer.WriteScriptFile(relativePath, content);
        }

        [Command]
        public void CmdCreateScriptFile(string relativePath, string content)
        {
            cursorServer.CreateScriptFile(relativePath, content);
        }

        [Command]
        public void CmdCreateScriptFolder(string relativePath)
        {
            cursorServer.CreateScriptFolder(relativePath);
        }

        [Command]
        public void CmdDeleteScriptNode(string relativePath)
        {
            cursorServer.DeleteScriptNode(relativePath);
        }

        [Command]
        public void CmdRenameScriptNode(string relativePath, string newName)
        {
            cursorServer.RenameScriptNode(relativePath, newName);
        }

        [Command]
        public void CmdMoveScriptNode(string fromRelativePath, string toRelativePath)
        {
            cursorServer.MoveScriptNode(fromRelativePath, toRelativePath);
        }

        [Command]
        public void CmdPromoteRunner(uint runnerNetId)
        {
            cursorServer.PromoteRunner(runnerNetId);
        }

        [Command]
        public void CmdSendChatMessage(string message)
        {
            cursorServer.SendChatMessage(message);
        }

        [Command]
        public void CmdModerationDecision(ulong requestId, LevelSessionServer.ModerationDecision decision)
        {
            cursorServer.ApplyChatModerationDecision(requestId, decision);
        }

        [Command]
        public void CmdMoveSelectedTilesBy(float dx, float dy, uint[] netIds)
        {
            cursorServer.MoveSelectedTilesBy(dx, dy, netIds);
        }

        [Command]
        public void CmdBeginCreateTransform(byte mode)
        {
            if (mode < (byte)CreateTransformMode.Move || mode > (byte)CreateTransformMode.Scale)
                return;

            cursorServer.ServerBeginCreateTransform((CreateTransformMode)mode);
        }

        [Command]
        public void CmdFinalizeCreateTransform(
            uint[] netIds,
            Vector2[] positions,
            float[] rotations,
            Vector2[] scales)
        {
            cursorServer.ServerFinalizeCreateTransform(netIds, positions, rotations, scales);
        }

        [Command]
        public void CmdCancelCreateTransform(
            uint[] netIds,
            Vector2[] positions,
            float[] rotations,
            Vector2[] scales)
        {
            cursorServer.ServerCancelCreateTransform(netIds, positions, rotations, scales);
        }

        [Command]
        public void CmdApplyTransformPatchToTile(uint netId, TileTransformPatchRecord patch)
        {
            cursorServer.ApplyTransformPatchToTile(netId, patch);
        }

        [Command]
        public void CmdApplyTransformPatchToSelection(TileTransformPatchRecord patch)
        {
            cursorServer.ApplyTransformPatchToSelectedTiles(patch);
        }

        [Command]
        public void CmdSetSelection(uint[] netIds)
        {
            cursorServer.SetSelection(netIds);
        }

        [Command]
        public void CmdDuplicateSelectedTilesRight()
        {
            cursorServer.DuplicateSelectedTilesRight();
        }

        [Command]
        public void CmdSetSnapSettings(float rotationDegrees, float positionUnits, float scaleUnits)
        {
            cursorServer.ServerSetSnapSettings(rotationDegrees, positionUnits, scaleUnits);
        }

        [Command]
        public void CmdSetHideCursor(bool hideCursor)
        {
            cursorServer.ServerSetHideCursor(hideCursor);
        }

        [Command]
        public void CmdCopySelectedTiles()
        {
            cursorServer.CopySelectedTilesToClipboard();
        }

        [Command]
        public void CmdPasteClipboardTiles(float worldX, float worldY)
        {
            cursorServer.PasteClipboardTiles(new Vector2(worldX, worldY));
        }

        public Vector2 GetScreenCenterWorldPosition()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return transform.position;

            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Vector3 world = worldCamera.ScreenToWorldPoint(screenCenter);
            return new Vector2(world.x, world.y);
        }

        public void RequestImportPng(byte[] pngBytes, int tilesX, int tilesY, Vector2 worldCenter)
        {
            if (!isOwned || pngBytes == null || pngBytes.Length == 0)
                return;

            if (tilesX <= 0 || tilesY <= 0)
                return;

            if (NetworkServer.active)
            {
                cursorServer.ImportPng(pngBytes, tilesX, tilesY, worldCenter);
                return;
            }

            const int chunkSize = 24000;
            CmdBeginPngImport(pngBytes.Length, tilesX, tilesY, worldCenter.x, worldCenter.y);
            for (int offset = 0; offset < pngBytes.Length; offset += chunkSize)
            {
                int length = Math.Min(chunkSize, pngBytes.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(pngBytes, offset, chunk, 0, length);
                CmdAppendPngImportChunk(chunk);
            }

            CmdFinishPngImport();
        }

        [Command]
        void CmdBeginPngImport(int totalLength, int tilesX, int tilesY, float centerX, float centerY)
        {
            cursorServer.BeginPngImport(totalLength, tilesX, tilesY, centerX, centerY);
        }

        [Command]
        void CmdAppendPngImportChunk(byte[] chunk)
        {
            cursorServer.AppendPngImportChunk(chunk);
        }

        [Command]
        void CmdFinishPngImport()
        {
            cursorServer.FinishPngImport();
        }

        public void RequestImportCustomTexture(string textureId, byte[] pngBytes)
        {
            if (!isOwned || pngBytes == null || pngBytes.Length == 0 || string.IsNullOrWhiteSpace(textureId))
                return;

            if (NetworkServer.active)
            {
                cursorServer.ImportCustomTexture(textureId, pngBytes);
                return;
            }

            const int chunkSize = 24000;
            CmdBeginCustomTextureImport(textureId, pngBytes.Length);
            for (int offset = 0; offset < pngBytes.Length; offset += chunkSize)
            {
                int length = Math.Min(chunkSize, pngBytes.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(pngBytes, offset, chunk, 0, length);
                CmdAppendCustomTextureImportChunk(chunk);
            }

            CmdFinishCustomTextureImport();
        }

        public void RequestImportCustomSound(string soundId, byte[] audioBytes, string extension)
        {
            if (!isOwned || audioBytes == null || audioBytes.Length == 0 || string.IsNullOrWhiteSpace(soundId))
                return;

            if (NetworkServer.active)
            {
                cursorServer.ImportCustomSound(soundId, extension, audioBytes);
                return;
            }

            const int chunkSize = 24000;
            CmdBeginCustomSoundImport(soundId, extension ?? ".ogg", audioBytes.Length);
            for (int offset = 0; offset < audioBytes.Length; offset += chunkSize)
            {
                int length = Math.Min(chunkSize, audioBytes.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(audioBytes, offset, chunk, 0, length);
                CmdAppendCustomSoundImportChunk(chunk);
            }

            CmdFinishCustomSoundImport();
        }

        [Command]
        void CmdBeginCustomSoundImport(string soundId, string extension, int totalLength)
        {
            cursorServer.BeginCustomSoundImport(soundId, extension, totalLength);
        }

        [Command]
        void CmdAppendCustomSoundImportChunk(byte[] chunk)
        {
            cursorServer.AppendCustomSoundImportChunk(chunk);
        }

        [Command]
        void CmdFinishCustomSoundImport()
        {
            cursorServer.FinishCustomSoundImport();
        }

        [Command]
        public void CmdSetSelectionFilter(string filterJson)
        {
            cursorServer.ServerSetSelectionFilter(SelectionFilterPayload.Deserialize(filterJson));
        }

        [Command]
        public void CmdApplyKeyframeSettingsToSelection(KeyframeRecord record)
        {
            cursorServer.ApplyKeyframeSettingsToSelectedTiles(record);
        }

        [Command]
        public void CmdApplyKeyframeSettingsPatchToSelection(KeyframePatchRecord patch)
        {
            cursorServer.ApplyKeyframeSettingsPatchToSelectedTiles(patch);
        }

        [Command]
        public void CmdApplyExecutorSettingsToSelection(ExecutorTileSettings settings)
        {
            cursorServer.ApplyExecutorSettingsToSelectedTiles(settings);
        }

        [Command]
        public void CmdApplyExecutorSettingsToTiles(uint[] netIds, ExecutorTileSettings settings)
        {
            cursorServer.ApplyExecutorSettingsToTiles(netIds, settings);
        }

        [Command]
        public void CmdApplyExecutorStatesToSelection(ExecutorGroupState start, ExecutorGroupState end)
        {
            cursorServer.ApplyExecutorStatesToSelectedTiles(start, end);
        }

        [Command]
        public void CmdApplyExecutorStatesToTiles(uint[] netIds, ExecutorGroupState start, ExecutorGroupState end)
        {
            cursorServer.ApplyExecutorStatesToTiles(netIds, start, end);
        }

        [Command]
        public void CmdApplySpawnPointToSelection(bool isSpawnPoint)
        {
            cursorServer.ApplySpawnPointToSelectedTriggers(isSpawnPoint);
        }

        [Command]
        void CmdBeginCustomTextureImport(string textureId, int totalLength)
        {
            cursorServer.BeginCustomTextureImport(textureId, totalLength);
        }

        [Command]
        void CmdAppendCustomTextureImportChunk(byte[] chunk)
        {
            cursorServer.AppendCustomTextureImportChunk(chunk);
        }

        [Command]
        void CmdFinishCustomTextureImport()
        {
            cursorServer.FinishCustomTextureImport();
        }

        [Command]
        public void CmdRenameCustomTexture(string oldTextureId, string newTextureId)
        {
            cursorServer.RenameCustomTexture(oldTextureId, newTextureId);
        }

        [Command]
        public void CmdUndoLevelEdit()
        {
            cursorServer.UndoLastLevelEdit();
        }

        [Command]
        public void CmdRequestLevelObserverResync()
        {
            LevelGridServer.Instance?.ServerResyncTileObserversForConnection(connectionToClient);
        }
    }
}
