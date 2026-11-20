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
    /// Kaplao-only Import / Export level buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoLevelIoUi : MonoBehaviour
    {
        [SerializeField] Button importButton;
        [SerializeField] Button exportButton;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] GameObject kaplaoOnlyRoot;

        KaplaoCursorClient cursorClient;
        NetworkPlayer localPlayer;

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;

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

            EnsureButtons();
            ApplyPanelLayout();
        }

        void ApplyPanelLayout()
        {
            Transform panel = importButton != null
                ? importButton.transform.parent
                : exportButton != null
                    ? exportButton.transform.parent
                    : kaplaoOnlyRoot != null
                        ? kaplaoOnlyRoot.transform.Find("LevelIoPanel")
                        : null;

            if (panel == null)
                return;

            ApplyLevelIoPanelLayout(panel, importButton, exportButton, statusLabel);
        }

        public static void ApplyLevelIoPanelLayout(
            Transform panel,
            Button importButton,
            Button exportButton,
            TextMeshProUGUI statusLabel)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.anchoredPosition = new Vector2(-16f, -56f);
                panelRect.sizeDelta = new Vector2(220f, 80f);
            }

            ApplyIoButtonLayout(importButton, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            ApplyIoButtonLayout(exportButton, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero);

            if (statusLabel != null)
            {
                RectTransform statusRect = statusLabel.rectTransform;
                statusRect.anchorMin = new Vector2(0.5f, 1f);
                statusRect.anchorMax = new Vector2(0.5f, 1f);
                statusRect.pivot = new Vector2(0.5f, 1f);
                statusRect.anchoredPosition = new Vector2(0f, -40f);
                statusRect.sizeDelta = new Vector2(220f, 24f);
                statusLabel.raycastTarget = false;
            }

            if (importButton != null)
                importButton.transform.SetSiblingIndex(1);

            if (exportButton != null)
                exportButton.transform.SetAsLastSibling();
        }

        static void ApplyIoButtonLayout(Button button, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            if (button == null)
                return;

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(100f, 36f);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.raycastTarget = false;
        }

        void EnsureButtons()
        {
            if (importButton != null && exportButton != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            Transform parent = kaplaoOnlyRoot != null ? kaplaoOnlyRoot.transform : canvas.transform;
            Transform panelTransform = parent.Find("LevelIoPanel");
            GameObject panel = panelTransform != null
                ? panelTransform.gameObject
                : CreateLevelIoPanel(parent);

            if (importButton == null)
            {
                Transform importTransform = panel.transform.Find("ImportLevelButton");
                if (importTransform != null)
                    importButton = importTransform.GetComponent<Button>();
            }

            if (exportButton == null)
            {
                Transform exportTransform = panel.transform.Find("ExportLevelButton");
                if (exportTransform != null)
                    exportButton = exportTransform.GetComponent<Button>();
            }

            if (statusLabel == null)
            {
                Transform statusTransform = panel.transform.Find("LevelIoStatusLabel");
                if (statusTransform != null)
                    statusLabel = statusTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        static GameObject CreateLevelIoPanel(Transform parent)
        {
            var panelObject = new GameObject("LevelIoPanel", typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-16f, -16f);
            panelRect.sizeDelta = new Vector2(220f, 72f);

            CreateIoButton(panelObject.transform, "ImportLevelButton", "Import", new Vector2(0f, 1f), Vector2.zero);
            CreateIoButton(panelObject.transform, "ExportLevelButton", "Export", new Vector2(1f, 1f), Vector2.zero);

            GameObject statusObject = new GameObject("LevelIoStatusLabel", typeof(RectTransform));
            statusObject.transform.SetParent(panelObject.transform, false);
            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -44f);
            statusRect.sizeDelta = new Vector2(220f, 24f);

            TextMeshProUGUI status = statusObject.AddComponent<TextMeshProUGUI>();
            status.fontSize = 14f;
            status.color = Color.white;
            status.alignment = TextAlignmentOptions.Center;
            status.raycastTarget = false;

            return panelObject;
        }

        static void CreateIoButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(100f, 36f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        void OnEnable()
        {
            if (importButton != null)
                importButton.onClick.AddListener(ImportLevel);
            if (exportButton != null)
                exportButton.onClick.AddListener(ExportLevel);

            StartCoroutine(BindLocalKaplaoWhenReady());
        }

        void OnDisable()
        {
            if (importButton != null)
                importButton.onClick.RemoveListener(ImportLevel);
            if (exportButton != null)
                exportButton.onClick.RemoveListener(ExportLevel);

            if (localPlayer != null)
            {
                localPlayer.RoleChanged -= OnLocalRoleChanged;
                localPlayer = null;
            }

            cursorClient = null;
        }

        IEnumerator BindLocalKaplaoWhenReady()
        {
            while (enabled)
            {
                if (NetworkClient.localPlayer != null &&
                    NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorClient foundClient) &&
                    NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player))
                {
                    if (localPlayer != player)
                    {
                        if (localPlayer != null)
                            localPlayer.RoleChanged -= OnLocalRoleChanged;

                        localPlayer = player;
                        localPlayer.RoleChanged += OnLocalRoleChanged;
                    }

                    if (player.role == PlayerRole.Kaplao)
                        cursorClient = foundClient;
                    else
                        cursorClient = null;
                }
                else
                {
                    cursorClient = null;
                }

                yield return null;
            }
        }

        void OnLocalRoleChanged(PlayerRole role)
        {
            if (role != PlayerRole.Kaplao)
                cursorClient = null;
        }

        void ImportLevel()
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
            if (client == null)
            {
                SetStatus("Kaplao is not ready yet.");
                return;
            }

            string path;
            try
            {
                path = PickPathWithVisibleCursor(LevelFileDialog.PickImportPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("Import dialog failed.");
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

                string json = File.ReadAllText(path);
                if (!LevelFileSerializer.TryParse(json, out _, out string error))
                {
                    SetStatus(error);
                    return;
                }

                client.RequestImportLevel(json);
                SetStatus($"Importing {Path.GetFileName(path)}...");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"Import failed: {ex.Message}");
            }
        }

        void ExportLevel()
        {
            if (!NetworkServer.active)
            {
                SetStatus("Only the host can export the level.");
                return;
            }

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
            {
                SetStatus("Level grid is not available.");
                return;
            }

            string path;
            try
            {
                path = PickPathWithVisibleCursor(LevelFileDialog.PickExportPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("Export dialog failed.");
                return;
            }

            if (string.IsNullOrEmpty(path))
                return;

            KaplaoExecutorEditorUi.FlushAllOpenEditorsBeforeModeChange();

            try
            {
                if (!path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                    path += ".json";

                LevelData data = grid.ExportLevelData();
                File.WriteAllText(path, LevelFileSerializer.ToJson(data));
                SetStatus($"Exported {data.tiles.Length} tiles to {Path.GetFileName(path)}.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"Export failed: {ex.Message}");
            }
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }

        static string PickPathWithVisibleCursor(System.Func<string> picker)
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
