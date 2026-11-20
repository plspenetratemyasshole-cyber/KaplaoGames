using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class KaplaoLevelSettingsUi : MonoBehaviour
    {
        const bool HideOpenButtonForNow = true;

        [SerializeField] Button openButton;
        [SerializeField] GameObject panelRoot;
        [SerializeField] Toggle runnersCanCollideToggle;
        [SerializeField] TMP_InputField worldSpawnXInput;
        [SerializeField] TMP_InputField worldSpawnYInput;
        [SerializeField] Button applyButton;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject kaplaoOnlyRoot;

        KaplaoCursorServer cursorServer;
        bool suppress;

        void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (HideOpenButtonForNow && openButton != null) openButton.gameObject.SetActive(false);
            if (applyButton != null) applyButton.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (openButton != null) openButton.onClick.AddListener(TogglePanel);
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
            if (runnersCanCollideToggle != null) runnersCanCollideToggle.onValueChanged.AddListener(OnLiveApplyToggle);
            if (worldSpawnXInput != null) worldSpawnXInput.onEndEdit.AddListener(OnLiveApplyText);
            if (worldSpawnYInput != null) worldSpawnYInput.onEndEdit.AddListener(OnLiveApplyText);
        }

        void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(TogglePanel);
            if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
            if (runnersCanCollideToggle != null) runnersCanCollideToggle.onValueChanged.RemoveListener(OnLiveApplyToggle);
            if (worldSpawnXInput != null) worldSpawnXInput.onEndEdit.RemoveListener(OnLiveApplyText);
            if (worldSpawnYInput != null) worldSpawnYInput.onEndEdit.RemoveListener(OnLiveApplyText);
        }

        void Update()
        {
            bool visible = !HideOpenButtonForNow &&
                           NetworkServer.active && NetworkClient.isConnected &&
                           NetworkClient.localPlayer != null &&
                           NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer np) &&
                           np.role == PlayerRole.Kaplao &&
                           cursorServer != null &&
                           KaplaoLocalGameModePredictor.IsCreate(cursorServer);

            if (openButton != null && openButton.gameObject.activeSelf != visible)
                openButton.gameObject.SetActive(visible);

            if (NetworkClient.localPlayer != null)
                cursorServer = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();

            if (panelRoot != null && panelRoot.activeSelf)
                SyncFromSession();
        }

        void TogglePanel()
        {
            if (panelRoot == null) return;
            bool open = !panelRoot.activeSelf;
            panelRoot.SetActive(open);
            if (open) SyncFromSession();
        }

        void ClosePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void SyncFromSession()
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null) return;

            suppress = true;
            try
            {
                if (runnersCanCollideToggle != null)
                    runnersCanCollideToggle.isOn = session.RunnersCanInteract;
                Vector2 spawn = session.WorldSpawn;
                if (worldSpawnXInput != null && !worldSpawnXInput.isFocused)
                    worldSpawnXInput.text = spawn.x.ToString("G");
                if (worldSpawnYInput != null && !worldSpawnYInput.isFocused)
                    worldSpawnYInput.text = spawn.y.ToString("G");
            }
            finally
            {
                suppress = false;
            }
        }

        void OnLiveApplyToggle(bool _)
        {
            if (!suppress)
                Apply();
        }

        void OnLiveApplyText(string _)
        {
            if (!suppress)
                Apply();
        }

        void Apply()
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null) return;

            if (runnersCanCollideToggle != null && !suppress)
                session.CmdSetRunnersCanInteract(runnersCanCollideToggle.isOn);

            float x = Parse(worldSpawnXInput, 0f);
            float y = Parse(worldSpawnYInput, 5f);
            session.CmdSetWorldSpawn(x, y);
        }

        static float Parse(TMP_InputField field, float fallback) =>
            field != null && KaplaoNumericInputUtility.TryParseFloat(field.text, out float v) ? v : fallback;
    }
}
