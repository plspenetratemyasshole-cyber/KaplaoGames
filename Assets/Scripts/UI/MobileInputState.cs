using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.UI
{
    public static class MobileInputState
    {
        public const int MaxToolSlot = 8;

        public static Vector2 KaplaoMove;
        public static bool KaplaoMoveJoystickHeld;
        public static Vector2 RunnerMove;
        public static bool RunnerMoveJoystickHeld;
        public static bool ShiftHeld;
        public static bool CtrlHeld;
        public static bool RunnerLeftHeld;
        public static bool RunnerRightHeld;

        static readonly HashSet<int> uiPointers = new HashSet<int>();

        static bool runnerJumpPressed;
        static int pendingToolSlot;
        static int pendingSelectionMoveX;
        static int pendingSelectionMoveY;
        static bool pendingSelectionDuplicate;
        static int lastUiPointerDownFrame = -1;

        public static float RunnerHorizontal
        {
            get
            {
                if (Mathf.Abs(RunnerMove.x) > 0.01f)
                    return Mathf.Clamp(RunnerMove.x, -1f, 1f);

                if (RunnerLeftHeld == RunnerRightHeld)
                    return 0f;
                return RunnerLeftHeld ? -1f : 1f;
            }
        }

        public static float RunnerVertical
        {
            get
            {
                if (Mathf.Abs(RunnerMove.y) > 0.01f)
                    return Mathf.Clamp(RunnerMove.y, -1f, 1f);
                return 0f;
            }
        }

        public static void QueueRunnerJump()
        {
            runnerJumpPressed = true;
        }

        public static bool ConsumeRunnerJump()
        {
            bool value = runnerJumpPressed;
            runnerJumpPressed = false;
            return value;
        }

        public static void SelectToolSlot(int slot)
        {
            pendingToolSlot = Mathf.Clamp(slot, 1, MaxToolSlot);
        }

        public static bool TryConsumeToolSlot(out int slot)
        {
            if (pendingToolSlot <= 0)
            {
                slot = 0;
                return false;
            }

            slot = pendingToolSlot;
            pendingToolSlot = 0;
            return true;
        }

        public static void RegisterUiPointer(int pointerId)
        {
            uiPointers.Add(pointerId);
            lastUiPointerDownFrame = Time.frameCount;
        }

        public static void UnregisterUiPointer(int pointerId)
        {
            uiPointers.Remove(pointerId);
        }

        public static bool IsUiPointer(int pointerId)
        {
            return uiPointers.Contains(pointerId);
        }

        public static bool HasAnyUiPointer()
        {
            return uiPointers.Count > 0;
        }

        public static bool WasUiPointerPressedThisFrame()
        {
            return lastUiPointerDownFrame == Time.frameCount;
        }

        public static void QueueSelectionMove(int deltaX, int deltaY)
        {
            pendingSelectionMoveX = Mathf.Clamp(deltaX, -1, 1);
            pendingSelectionMoveY = Mathf.Clamp(deltaY, -1, 1);
        }

        public static bool TryConsumeSelectionMove(out int deltaX, out int deltaY)
        {
            if (pendingSelectionMoveX == 0 && pendingSelectionMoveY == 0)
            {
                deltaX = 0;
                deltaY = 0;
                return false;
            }

            deltaX = pendingSelectionMoveX;
            deltaY = pendingSelectionMoveY;
            pendingSelectionMoveX = 0;
            pendingSelectionMoveY = 0;
            return true;
        }

        public static void QueueSelectionDuplicate()
        {
            pendingSelectionDuplicate = true;
        }

        public static bool TryConsumeSelectionDuplicate()
        {
            if (!pendingSelectionDuplicate)
                return false;

            pendingSelectionDuplicate = false;
            return true;
        }

        public static bool IsKaplaoMovingOnMobile()
        {
            if (!MobileControlsConfig.IsMobileControlsEnabled)
                return false;

            return KaplaoMoveJoystickHeld || KaplaoMove.sqrMagnitude > 0.0001f;
        }
    }
}

