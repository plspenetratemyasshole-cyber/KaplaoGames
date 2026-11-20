using System;
using System.Collections;
using System.IO;
using KaplaoGames.Network;
using KaplaoGames.Platform;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class KaplaoSoundImportUi : MonoBehaviour
    {
        const bool HideOpenButtonForNow = true;

        [SerializeField] Button importSoundButton;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_InputField soundNameInput;
        [SerializeField] Button selectFileButton;
        [SerializeField] Button addButton;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI fileNameLabel;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] GameObject kaplaoOnlyRoot;

        public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

        public void DismissPanelIfOpen()
        {
            if (IsPanelVisible)
                ClosePanel();
        }

        KaplaoCursorClient cursorClient;
        KaplaoCursorServer cursorServer;
        byte[] pendingBytes;
        string pendingExtension = ".ogg";

        void Awake()
        {
            if (kaplaoOnlyRoot == null)
                kaplaoOnlyRoot = gameObject;

            if (panelRoot != null)
                panelRoot.SetActive(false);

            SetCreateModeVisible(false);
        }

        void OnEnable()
        {
            if (importSoundButton != null) importSoundButton.onClick.AddListener(TogglePanel);
            if (selectFileButton != null) selectFileButton.onClick.AddListener(SelectFile);
            if (addButton != null) addButton.onClick.AddListener(ImportPending);
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
            StartCoroutine(WatchLocalKaplao());
        }

        void OnDisable()
        {
            if (importSoundButton != null) importSoundButton.onClick.RemoveListener(TogglePanel);
            if (selectFileButton != null) selectFileButton.onClick.RemoveListener(SelectFile);
            if (addButton != null) addButton.onClick.RemoveListener(ImportPending);
            if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
            ClearCursorBinding();
        }

        void TogglePanel()
        {
            if (panelRoot == null) return;
            bool opening = !panelRoot.activeSelf;
            panelRoot.SetActive(opening);
            if (opening) ClearPendingFile();
        }

        void ClosePanel()
        {
            ClearPendingFile();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void ClearPendingFile()
        {
            pendingBytes = null;
            pendingExtension = ".ogg";
            if (fileNameLabel != null) fileNameLabel.text = string.Empty;
        }

        void SelectFile()
        {
            string path = PickPathWithVisibleCursor(SoundFileDialog.PickImportPath);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                pendingBytes = File.ReadAllBytes(path);
                pendingExtension = Path.GetExtension(path);
                if (fileNameLabel != null) fileNameLabel.text = Path.GetFileName(path);
                SetStatus("File selected. Press Add to import.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
        }

        void ImportPending()
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
            if (client == null) { SetStatus("Kaplao is not ready yet."); return; }
            if (pendingBytes == null || pendingBytes.Length == 0) { SetStatus("Select a sound file first."); return; }

            string rawName = soundNameInput != null ? soundNameInput.text : string.Empty;
            if (!WorldCustomSoundService.TrySanitizeSoundId(rawName, out string soundId, out string nameError))
            {
                SetStatus(nameError);
                return;
            }

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestImportCustomSound(soundId, pendingBytes, pendingExtension);
            SetStatus($"Importing sound '{soundId}'...");
        }

        IEnumerator WatchLocalKaplao()
        {
            while (enabled)
            {
                if (NetworkClient.localPlayer == null ||
                    !NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer networkPlayer) ||
                    networkPlayer.role != PlayerRole.Kaplao)
                {
                    ClearCursorBinding();
                    SetCreateModeVisible(false);
                    yield return null;
                    continue;
                }

                KaplaoCursorClient nextClient = NetworkClient.localPlayer.GetComponent<KaplaoCursorClient>();
                KaplaoCursorServer nextServer = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
                if (nextClient == null || nextServer == null)
                {
                    ClearCursorBinding();
                    SetCreateModeVisible(false);
                    yield return null;
                    continue;
                }

                if (cursorClient != nextClient || cursorServer != nextServer)
                {
                    ClearCursorBinding();
                    cursorClient = nextClient;
                    cursorServer = nextServer;
                    cursorServer.HostGameModeChanged += OnHostGameModeChanged;
                }

                SetCreateModeVisible(KaplaoLocalGameModePredictor.IsCreate(cursorServer));
                yield return null;
            }
        }

        void ClearCursorBinding()
        {
            if (cursorServer != null) cursorServer.HostGameModeChanged -= OnHostGameModeChanged;
            cursorServer = null;
            cursorClient = null;
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            SetCreateModeVisible(KaplaoLocalGameModePredictor.IsCreate(cursorServer));
            if (!KaplaoLocalGameModePredictor.IsCreate(cursorServer)) ClosePanel();
        }

        void SetCreateModeVisible(bool visible)
        {
            bool show = visible && !HideOpenButtonForNow;
            if (importSoundButton != null && importSoundButton.gameObject.activeSelf != show)
                importSoundButton.gameObject.SetActive(show);
            if (!show) ClosePanel();
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        static string PickPathWithVisibleCursor(Func<string> picker)
        {
            bool wasVisible = Cursor.visible;
            CursorLockMode previousLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            try { return picker?.Invoke(); }
            finally { Cursor.visible = wasVisible; Cursor.lockState = previousLockState; }
        }
    }
}
