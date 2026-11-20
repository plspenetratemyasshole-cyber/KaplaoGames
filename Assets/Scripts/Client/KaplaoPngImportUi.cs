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
    /// <summary>
    /// Kaplao Create-mode PNG importer: places solid-color tiles (no textures).
    /// Layout is authored in the scene — do not force positions at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoPngImportUi : MonoBehaviour
    {
        [SerializeField] Button importPngButton;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_InputField tilesXInput;
        [SerializeField] TMP_InputField tilesYInput;
        [SerializeField] Button selectFileButton;
        [SerializeField] Button addButton;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI fileNameLabel;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] GameObject kaplaoOnlyRoot;

        KaplaoCursorClient cursorClient;
        KaplaoCursorServer cursorServer;
        byte[] pendingPngBytes;

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;

        public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

        public void DismissPanelIfOpen()
        {
            if (IsPanelVisible)
                ClosePanel();
        }

        void Awake()
        {
            if (kaplaoOnlyRoot == null)
                kaplaoOnlyRoot = gameObject;

            if (kaplaoOnlyRoot == gameObject &&
                TryGetComponent(out KaplaoCreateModeUi createModeUi) &&
                createModeUi.KaplaoOnlyRoot != null)
            {
                kaplaoOnlyRoot = createModeUi.KaplaoOnlyRoot;
            }

            EnsureUiReferences();

            if (tilesXInput != null && string.IsNullOrEmpty(tilesXInput.text))
                tilesXInput.text = "10";
            if (tilesYInput != null && string.IsNullOrEmpty(tilesYInput.text))
                tilesYInput.text = "10";

            if (panelRoot != null)
                panelRoot.SetActive(false);

            SetCreateModeVisible(false);
        }

        void OnEnable()
        {
            if (importPngButton != null)
                importPngButton.onClick.AddListener(TogglePanel);
            if (selectFileButton != null)
                selectFileButton.onClick.AddListener(SelectFile);
            if (addButton != null)
                addButton.onClick.AddListener(ImportPending);
            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            StartCoroutine(WatchLocalKaplao());
        }

        void OnDisable()
        {
            if (importPngButton != null)
                importPngButton.onClick.RemoveListener(TogglePanel);
            if (selectFileButton != null)
                selectFileButton.onClick.RemoveListener(SelectFile);
            if (addButton != null)
                addButton.onClick.RemoveListener(ImportPending);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(ClosePanel);

            ClearCursorBinding();
        }

        void TogglePanel()
        {
            if (panelRoot == null)
                return;

            bool opening = !panelRoot.activeSelf;
            panelRoot.SetActive(opening);
            if (opening)
                ClearPendingFile();
        }

        void ClosePanel()
        {
            ClearPendingFile();
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        void ClearPendingFile()
        {
            pendingPngBytes = null;
            if (fileNameLabel != null)
                fileNameLabel.text = string.Empty;
        }

        void SelectFile()
        {
            string path;
            try
            {
                path = PickPathWithVisibleCursor(PngFileDialog.PickImportPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("File dialog failed.");
                return;
            }

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (!File.Exists(path))
                {
                    SetStatus("File not found.");
                    return;
                }

                byte[] pngBytes = File.ReadAllBytes(path);
                if (!PngImportUtility.TryLoadTexture(pngBytes, out Texture2D texture, out string decodeError))
                {
                    SetStatus(decodeError);
                    return;
                }

                UnityEngine.Object.Destroy(texture);
                pendingPngBytes = pngBytes;
                if (fileNameLabel != null)
                    fileNameLabel.text = Path.GetFileName(path);
                SetStatus("File selected. Press Add to import.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"Failed to read file: {ex.Message}");
            }
        }

        void ImportPending()
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
            if (client == null)
            {
                SetStatus("Kaplao is not ready yet.");
                return;
            }

            if (pendingPngBytes == null || pendingPngBytes.Length == 0)
            {
                SetStatus("Select a PNG file first.");
                return;
            }

            if (!TryReadTileCounts(out int tilesX, out int tilesY, out string countError))
            {
                SetStatus(countError);
                return;
            }

            KaplaoCursorClient.EnsureClientReadyForCommands();
            Vector2 center = client.GetScreenCenterWorldPosition();
            client.RequestImportPng(pendingPngBytes, tilesX, tilesY, center);
            SetStatus($"Importing {fileNameLabel?.text ?? "PNG"}...");
        }

        bool TryReadTileCounts(out int tilesX, out int tilesY, out string error)
        {
            tilesX = 0;
            tilesY = 0;
            error = null;

            string rawX = tilesXInput != null ? tilesXInput.text : string.Empty;
            string rawY = tilesYInput != null ? tilesYInput.text : string.Empty;

            if (!int.TryParse(rawX, out tilesX) || !int.TryParse(rawY, out tilesY))
            {
                error = "Enter tile counts as whole numbers.";
                return false;
            }

            if (tilesX <= 0 || tilesY <= 0)
            {
                error = "Tile counts must be greater than zero.";
                return false;
            }

            return true;
        }

        IEnumerator WatchLocalKaplao()
        {
            while (enabled)
            {
                NetworkIdentity localIdentity = NetworkClient.localPlayer;
                if (localIdentity == null ||
                    !localIdentity.TryGetComponent(out NetworkPlayer networkPlayer) ||
                    networkPlayer.role != PlayerRole.Kaplao)
                {
                    ClearCursorBinding();
                    SetCreateModeVisible(false);
                    yield return null;
                    continue;
                }

                KaplaoCursorClient nextClient = localIdentity.GetComponent<KaplaoCursorClient>();
                KaplaoCursorServer nextServer = localIdentity.GetComponent<KaplaoCursorServer>();
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

                SetCreateModeVisible(KaplaoLocalGameModePredictor.IsCreate(cursorServer) &&
                                     !KaplaoExecutorEditorUi.BlocksCreateHud);
                yield return null;
            }
        }

        void ClearCursorBinding()
        {
            if (cursorServer != null)
                cursorServer.HostGameModeChanged -= OnHostGameModeChanged;

            cursorServer = null;
            cursorClient = null;
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            bool create = KaplaoLocalGameModePredictor.IsCreate(cursorServer);
            SetCreateModeVisible(create);
            if (!create)
                ClosePanel();
        }

        void SetCreateModeVisible(bool visible)
        {
            if (KaplaoExecutorEditorUi.BlocksCreateHud)
                visible = false;

            if (importPngButton != null && importPngButton.gameObject.activeSelf != visible)
                importPngButton.gameObject.SetActive(visible);

            if (!visible)
                ClosePanel();
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }

        void EnsureUiReferences()
        {
            Transform parent = kaplaoOnlyRoot != null ? kaplaoOnlyRoot.transform : transform;

            if (importPngButton == null)
            {
                Transform buttonTransform = parent.Find("ImportPngButton");
                if (buttonTransform != null)
                    importPngButton = buttonTransform.GetComponent<Button>();
            }

            if (panelRoot == null)
            {
                Transform panelTransform = parent.Find("PngImportPanel");
                if (panelTransform != null)
                    panelRoot = panelTransform.gameObject;
            }

            if (panelRoot == null)
                return;

            Transform panel = panelRoot.transform;

            if (tilesXInput == null)
                tilesXInput = panel.Find("TilesXInput")?.GetComponent<TMP_InputField>();
            if (tilesYInput == null)
                tilesYInput = panel.Find("TilesYInput")?.GetComponent<TMP_InputField>();
            if (selectFileButton == null)
                selectFileButton = panel.Find("SelectPngFileButton")?.GetComponent<Button>();
            if (addButton == null)
                addButton = panel.Find("PngImportAddButton")?.GetComponent<Button>();
            if (closeButton == null)
                closeButton = panel.Find("PngImportCloseButton")?.GetComponent<Button>();
            if (fileNameLabel == null)
                fileNameLabel = panel.Find("PngFileNameLabel")?.GetComponent<TextMeshProUGUI>();
            if (statusLabel == null)
                statusLabel = panel.Find("PngImportStatusLabel")?.GetComponent<TextMeshProUGUI>();
        }

        public static void ApplyPngImportPanelLayout(
            Transform importButton,
            Transform panel,
            TMP_InputField tilesXInput,
            TMP_InputField tilesYInput,
            Button selectFileButton,
            Button addButton,
            Button closeButton,
            TextMeshProUGUI fileNameLabel,
            TextMeshProUGUI statusLabel)
        {
            RectTransform buttonRect = importButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(1f, 1f);
                buttonRect.anchorMax = new Vector2(1f, 1f);
                buttonRect.pivot = new Vector2(1f, 1f);
                buttonRect.anchoredPosition = new Vector2(-16f, -144f);
                buttonRect.sizeDelta = new Vector2(220f, 36f);
            }

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.anchoredPosition = new Vector2(-16f, -188f);
                panelRect.sizeDelta = new Vector2(220f, 204f);
            }

            ApplyFieldLayout(tilesXInput, new Vector2(0f, 0f), new Vector2(96f, 32f));
            ApplyFieldLayout(tilesYInput, new Vector2(0f, -36f), new Vector2(96f, 32f));
            ApplyButtonLayout(selectFileButton, new Vector2(0f, -76f), new Vector2(180f, 32f));
            ApplyLabelLayout(fileNameLabel, new Vector2(0f, -112f), new Vector2(200f, 22f));
            ApplyButtonLayout(addButton, new Vector2(-50f, -148f), new Vector2(86f, 32f));
            ApplyButtonLayout(closeButton, new Vector2(50f, -148f), new Vector2(86f, 32f));
            ApplyLabelLayout(statusLabel, new Vector2(0f, -176f), new Vector2(200f, 22f));
        }

        static void ApplyFieldLayout(TMP_InputField field, Vector2 position, Vector2 size)
        {
            if (field == null)
                return;

            RectTransform rect = field.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void ApplyButtonLayout(Button button, Vector2 position, Vector2 size)
        {
            if (button == null)
                return;

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void ApplyLabelLayout(TextMeshProUGUI label, Vector2 position, Vector2 size)
        {
            if (label == null)
                return;

            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.fontSize = 14f;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        static string PickPathWithVisibleCursor(Func<string> picker)
        {
            bool wasVisible = Cursor.visible;
            CursorLockMode previousLockState = Cursor.lockState;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            try
            {
                return picker != null ? picker() : null;
            }
            finally
            {
                Cursor.visible = wasVisible;
                Cursor.lockState = previousLockState;
            }
        }
    }
}
