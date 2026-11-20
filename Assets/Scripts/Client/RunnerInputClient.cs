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
    /// Reads runner input on the owning client. Movement syncs via client-authoritative NetworkTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunnerMovementServer))]
    public class RunnerInputClient : NetworkBehaviour
    {
        RunnerMovementServer movement;
        bool jumpPressed;

        void Awake()
        {
            movement = GetComponent<RunnerMovementServer>();
            enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            enabled = true;
        }

        public override void OnStopLocalPlayer()
        {
            enabled = false;
            jumpPressed = false;
        }

        void Update()
        {
            if (!isLocalPlayer)
                return;

            if (KaplaoModeratedChatUi.IsChatInputFocused)
            {
                jumpPressed = false;
                return;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    jumpPressed = true;
                }

                if (Keyboard.current.rKey.wasPressedThisFrame)
                    movement.RequestSuicide();
            }

            if (!MobileControlsConfig.IsMobileControlsEnabled || Touchscreen.current == null)
                return;

            // Jump on empty-screen taps. Only skip the touch that hit UI —
            // do not clear jump while another finger holds the move stick.
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                int touchId = touch.touchId.ReadValue();
                if (touchId <= 0)
                    continue;

                if (MobileInputState.IsUiPointer(touchId))
                    continue;

                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
                if (!overUi)
                    jumpPressed = true;
            }

            if (MobileInputState.ConsumeRunnerJump())
                jumpPressed = true;
        }

        void FixedUpdate()
        {
            if (!isLocalPlayer)
                return;

            if (KaplaoModeratedChatUi.IsChatInputFocused)
            {
                jumpPressed = false;
                movement.SetInput(default);
                return;
            }

            RunnerInput input = ReadInput();
            input.jump = jumpPressed;
            jumpPressed = false;
            movement.SetInput(input);
        }

        static RunnerInput ReadInput()
        {
            if (KaplaoModeratedChatUi.IsChatInputFocused)
                return default;

            bool mobileControls = MobileControlsConfig.IsMobileControlsEnabled;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null && !mobileControls)
                return default;

            float horizontal = 0f;
            bool fastFall = false;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) horizontal -= 1f;
                if (keyboard.dKey.isPressed) horizontal += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    fastFall = true;
            }

            if (mobileControls)
            {
                if (Mathf.Abs(horizontal) <= 0.01f)
                    horizontal = MobileInputState.RunnerHorizontal;

                if (!fastFall && MobileInputState.RunnerVertical < -0.35f)
                    fastFall = true;
            }

            return new RunnerInput { horizontal = horizontal, fastFall = fastFall };
        }
    }
}
