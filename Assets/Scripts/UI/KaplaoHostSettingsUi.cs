using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
  /// <summary>
  /// Host-only session rules. Visible only on the machine running the server (not promoted Kaplao clients).
  /// </summary>
  [DisallowMultipleComponent]
  public class KaplaoHostSettingsUi : MonoBehaviour
  {
    [SerializeField] GameObject hostOnlyRoot;
    [SerializeField] Toggle runnersCanInteractToggle;
    [SerializeField] TextMeshProUGUI runnersCanInteractLabel;

    bool suppressToggleCallback;

    void OnEnable()
    {
      if (runnersCanInteractToggle != null)
        runnersCanInteractToggle.onValueChanged.AddListener(OnRunnersCanInteractToggleChanged);
    }

    void OnDisable()
    {
      if (runnersCanInteractToggle != null)
        runnersCanInteractToggle.onValueChanged.RemoveListener(OnRunnersCanInteractToggleChanged);
    }

    void Update()
    {
      bool visible = IsLocalHostKaplao();
      if (hostOnlyRoot != null && hostOnlyRoot.activeSelf != visible)
        hostOnlyRoot.SetActive(visible);

      if (!visible)
        return;

      SyncToggleFromSession();
    }

    static bool IsLocalHostKaplao()
    {
      if (!NetworkServer.active || !NetworkClient.isConnected)
        return false;

      if (NetworkClient.localPlayer == null)
        return false;

      return NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer networkPlayer) &&
             networkPlayer.role == PlayerRole.Kaplao;
    }

    void SyncToggleFromSession()
    {
      if (runnersCanInteractToggle == null || LevelSessionServer.Instance == null)
        return;

      bool sessionValue = LevelSessionServer.Instance.RunnersCanInteract;
      if (runnersCanInteractToggle.isOn == sessionValue)
        return;

      suppressToggleCallback = true;
      runnersCanInteractToggle.isOn = sessionValue;
      suppressToggleCallback = false;
    }

    void OnRunnersCanInteractToggleChanged(bool canInteract)
    {
      if (suppressToggleCallback)
        return;

      if (!IsLocalHostKaplao())
        return;

      LevelSessionServer session = LevelSessionServer.Instance;
      if (session == null)
        return;

      session.CmdSetRunnersCanInteract(canInteract);
    }
  }
}
