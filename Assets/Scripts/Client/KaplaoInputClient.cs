using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Reads Kaplao input on the owning client. Movement syncs via client-authoritative NetworkTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KaplaoMovementServer))]
    public class KaplaoInputClient : NetworkBehaviour
    {
        KaplaoMovementServer movement;
        KaplaoCursorServer cursorServer;

        void Awake()
        {
            movement = GetComponent<KaplaoMovementServer>();
            cursorServer = GetComponent<KaplaoCursorServer>();
            enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            enabled = true;
        }

        public override void OnStopLocalPlayer()
        {
            enabled = false;
        }

        void Update()
        {
            if (!isLocalPlayer)
                return;

            movement.SetInput(ReadInput(cursorServer));
        }

        static KaplaoInput ReadInput(KaplaoCursorServer cursorServer)
        {
            bool mobileControls = MobileControlsConfig.IsMobileControlsEnabled;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null && !mobileControls)
                return default;

            HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            if (mode == HostGameMode.Create || mode == HostGameMode.Code)
                return default;

            bool hasSelectionInCreate = cursorServer != null &&
                                        KaplaoLocalGameModePredictor.IsCreate(cursorServer) &&
                                        cursorServer.SelectedCount > 0;
            if (hasSelectionInCreate)
                return default;

            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) horizontal -= 1f;
                if (keyboard.dKey.isPressed) horizontal += 1f;
                if (keyboard.wKey.isPressed) vertical += 1f;
                if (keyboard.sKey.isPressed) vertical -= 1f;
            }

            if (mobileControls)
            {
                Vector2 move = MobileInputState.KaplaoMove;
                horizontal += move.x;
                vertical += move.y;
            }

            bool shiftHeld = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) ||
                             MobileInputState.ShiftHeld;

            return new KaplaoInput
            {
                horizontal = horizontal,
                vertical = vertical,
                shiftHeld = shiftHeld
            };
        }
    }
}
