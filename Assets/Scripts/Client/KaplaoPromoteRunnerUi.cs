using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Prompt shown when Kaplao clicks a runner in Create mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoPromoteRunnerUi : MonoBehaviour
    {
        [SerializeField] GameObject panelRoot;
        [SerializeField] TextMeshProUGUI messageLabel;
        [SerializeField] Button promoteButton;
        [SerializeField] Button cancelButton;

        KaplaoCursorClient cursorClient;
        uint pendingRunnerNetId;

        void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            Hide();
        }

        void OnEnable()
        {
            if (promoteButton != null)
                promoteButton.onClick.AddListener(ConfirmPromote);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
        }

        void OnDisable()
        {
            if (promoteButton != null)
                promoteButton.onClick.RemoveListener(ConfirmPromote);
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(Hide);
        }

        void Update()
        {
            if (pendingRunnerNetId == 0)
                return;

            if (cursorClient == null)
                TryBindCursor();
        }

        void TryBindCursor()
        {
            if (NetworkClient.localPlayer == null ||
                !NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player) ||
                player.role != PlayerRole.Kaplao)
            {
                return;
            }

            cursorClient = NetworkClient.localPlayer.GetComponent<KaplaoCursorClient>();
        }

        public void Show(uint runnerNetId, string runnerName)
        {
            pendingRunnerNetId = runnerNetId;
            TryBindCursor();

            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (messageLabel != null)
            {
                string safeName = string.IsNullOrWhiteSpace(runnerName) ? "Runner" : runnerName;
                messageLabel.text = $"Promote {safeName} to Kaplao?";
            }
        }

        public void Hide()
        {
            pendingRunnerNetId = 0;

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        void ConfirmPromote()
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
            if (client == null || pendingRunnerNetId == 0)
            {
                Hide();
                return;
            }

            client.CmdPromoteRunner(pendingRunnerNetId);
            Hide();
        }
    }
}
