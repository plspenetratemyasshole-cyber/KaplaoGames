using KaplaoGames.Network;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace KaplaoGames.Client
{
  /// <summary>
  /// Enter / Shift+Enter toggles Kaplao and Runner for world host and admin accounts.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class PlayerRoleToggleInputClient : MonoBehaviour
  {
    NetworkPlayer networkPlayer;

    void Awake()
    {
      networkPlayer = GetComponent<NetworkPlayer>();
    }

    void Update()
    {
      if (!isActiveAndEnabled || networkPlayer == null || !networkPlayer.isLocalPlayer)
        return;

      if (!CanLocalPlayerToggleRole())
        return;

      if (IsTextInputFocused())
        return;

      Keyboard keyboard = Keyboard.current;
      if (keyboard == null)
        return;

      bool enterPressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
      if (!enterPressed)
        return;

      bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
      bool spawnAtCurrentKaplaoPosition = shiftHeld && LocalPlayerRole.IsKaplao();
      networkPlayer.CmdRequestEnterRoleToggle(spawnAtCurrentKaplaoPosition);
    }

    public static bool CanLocalPlayerToggleRole()
    {
      if (WorldSession.IsBrowsePlaySession || (Server.LevelSessionServer.Instance != null && Server.LevelSessionServer.Instance.isBrowsePlaySession))
        return false;

      if (!NetworkClient.active || NetworkClient.localPlayer == null)
        return false;

      if (NetworkServer.active)
        return true;

      return NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player) && player.isAdmin;
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

      if (selected.GetComponent<TMP_InputField>() != null)
        return true;

      return selected.GetComponent<UnityEngine.UI.InputField>() != null;
    }
  }
}
