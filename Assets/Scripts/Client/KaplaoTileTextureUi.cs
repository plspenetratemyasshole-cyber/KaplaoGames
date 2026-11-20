using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Searchable block texture picker for Create mode selection.
    /// Panel is built in the scene via Kaplao/Setup Texture Picker UI so you can move widgets in the Editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoTileTextureUi : MonoBehaviour
    {
        [SerializeField] GameObject kaplaoOnlyRoot;

        [Header("Panel references (TexturePickerPanel under CreateModeUI)")]
        [SerializeField] RectTransform panelRoot;
        [SerializeField] TMP_InputField searchField;
        [SerializeField] RectTransform listContent;
        [SerializeField] GridLayoutGroup listGrid;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_InputField renameInputField;
        [SerializeField] Button changeNameButton;

        [Header("Layout")]
        [SerializeField] Vector2 panelSize = new Vector2(640f, 520f);
        [SerializeField] Vector2 gridCellSize = new Vector2(108f, 118f);
        [SerializeField] Vector2 gridSpacing = new Vector2(8f, 8f);
        [SerializeField] int gridColumnCount = 5;
        [SerializeField] float previewSize = 88f;
        [SerializeField] float nameRowHeight = 34f;

        KaplaoCursorClient cursorClient;
        readonly List<TextureEntryView> entryViews = new();
        bool panelWired;
        string currentSearch = string.Empty;
        Sprite noneSprite;
        string selectedTextureId = string.Empty;

        public System.Action<string> OverrideTextureChosen { get; set; }

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;
        public RectTransform PanelRoot => panelRoot;
        public bool IsOpen => panelRoot != null && panelRoot.gameObject.activeSelf;

        public struct PanelReferences
        {
            public RectTransform PanelRoot;
            public TMP_InputField SearchField;
            public RectTransform ListContent;
            public GridLayoutGroup ListGrid;
            public TextMeshProUGUI StatusLabel;
            public Button CloseButton;
            public TMP_InputField RenameInputField;
            public Button ChangeNameButton;
        }

        public void SetKaplaoOnlyRoot(GameObject root) => kaplaoOnlyRoot = root;

        public void AssignPanelReferences(PanelReferences references)
        {
            panelRoot = references.PanelRoot;
            searchField = references.SearchField;
            listContent = references.ListContent;
            listGrid = references.ListGrid;
            statusLabel = references.StatusLabel;
            closeButton = references.CloseButton;
            renameInputField = references.RenameInputField;
            changeNameButton = references.ChangeNameButton;
            panelWired = false;

            if (isActiveAndEnabled && panelRoot != null)
                WirePanel();
        }

        void OnEnable()
        {
            if (panelRoot != null)
                WirePanel();

            HidePanel();
            StartCoroutine(WatchLocalKaplao());
        }

        void OnDisable()
        {
            ClearKaplaoBinding();
        }

        IEnumerator WatchLocalKaplao()
        {
            while (enabled)
            {
                if (!KaplaoLocalBindingUtility.TryGetLocalKaplao(
                        out _,
                        out _,
                        out KaplaoCursorClient nextClient,
                        out _))
                {
                    ClearKaplaoBinding();
                    yield return null;
                    continue;
                }

                if (cursorClient != nextClient)
                    cursorClient = nextClient;

                yield return null;
            }
        }

        void ClearKaplaoBinding()
        {
            cursorClient = null;
        }

        public void ShowPanel()
        {
            if (panelRoot == null)
            {
                Debug.LogWarning("[Kaplao] Texture picker panel is missing. Run Kaplao/Setup Texture Picker UI.");
                return;
            }

            WirePanel();
            currentSearch = string.Empty;
            if (searchField != null)
                searchField.SetTextWithoutNotify(string.Empty);
            selectedTextureId = string.Empty;
            SetRenameControlsVisible(false);
            if (statusLabel != null)
                statusLabel.color = new Color(1f, 1f, 1f, 0.75f);

            RebuildList();
            panelRoot.SetAsLastSibling();
            panelRoot.gameObject.SetActive(true);
        }

        public void HidePanel()
        {
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(false);
        }

        void WirePanel()
        {
            if (panelWired || panelRoot == null)
                return;

            if (listGrid == null && listContent != null)
                listGrid = listContent.GetComponent<GridLayoutGroup>();

            ApplyGridLayoutSettings();

            if (searchField != null)
            {
                searchField.onValueChanged.RemoveListener(OnSearchChanged);
                searchField.onValueChanged.AddListener(OnSearchChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HidePanel);
                closeButton.onClick.AddListener(HidePanel);
            }

            if (changeNameButton != null)
            {
                changeNameButton.onClick.RemoveListener(OnChangeNameClicked);
                changeNameButton.onClick.AddListener(OnChangeNameClicked);
            }

            panelWired = true;
        }

        void RebuildList()
        {
            if (listContent == null)
                return;

            ClearEntryViews();

            BlockTextureCatalog catalog = BlockTextureCatalog.Instance;
            bool hasCatalog = catalog != null && catalog.Entries.Count > 0;
            List<BlockTextureCatalog.Entry> customEntries = RuntimeBlockTextureRegistry.BuildPickerEntries();
            bool hasCustom = customEntries.Count > 0;

            if (!hasCatalog && !hasCustom)
            {
                if (statusLabel != null)
                    statusLabel.text = "Catalog is empty. Run Kaplao/Block Textures/3. Build Catalog.";
                return;
            }

            int shown = 0;
            if (AddNoneEntryIfVisible())
                shown++;

            // Order: None → custom world textures → built-in catalog.
            for (int i = 0; i < customEntries.Count; i++)
            {
                BlockTextureCatalog.Entry entry = customEntries[i];
                if (entry.sprite == null)
                    continue;

                if (!MatchesSearch(entry))
                    continue;

                entryViews.Add(CreateEntryView(entry));
                shown++;
            }

            if (hasCatalog)
            {
                IReadOnlyList<BlockTextureCatalog.Entry> entries = catalog.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    BlockTextureCatalog.Entry entry = entries[i];
                    if (entry.sprite == null)
                        continue;

                    if (!MatchesSearch(entry))
                        continue;

                    entryViews.Add(CreateEntryView(entry));
                    shown++;
                }
            }

            if (statusLabel != null)
                statusLabel.text = shown == 0 ? "No textures match search." : $"{shown} textures";
        }

        bool AddNoneEntryIfVisible()
        {
            if (!string.IsNullOrWhiteSpace(currentSearch))
            {
                string query = currentSearch.Trim();
                if (!"none".Contains(query, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (noneSprite == null)
                noneSprite = CreateWhiteSprite();

            var entry = new BlockTextureCatalog.Entry
            {
                id = string.Empty,
                displayName = "None",
                sprite = noneSprite
            };

            entryViews.Add(CreateEntryView(entry));
            return true;
        }

        bool MatchesSearch(BlockTextureCatalog.Entry entry)
        {
            if (string.IsNullOrWhiteSpace(currentSearch))
                return true;

            string query = currentSearch.Trim();
            return entry.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        TextureEntryView CreateEntryView(BlockTextureCatalog.Entry entry)
        {
            GameObject item = new GameObject($"Texture_{entry.id}", typeof(RectTransform));
            item.transform.SetParent(listContent, false);

            Image background = item.AddComponent<Image>();
            background.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            VerticalLayoutGroup layout = item.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            GameObject previewObject = new GameObject("Preview", typeof(RectTransform));
            previewObject.transform.SetParent(item.transform, false);
            LayoutElement previewLayout = previewObject.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = previewSize;
            previewLayout.preferredHeight = previewSize;

            Image preview = previewObject.AddComponent<Image>();
            preview.sprite = entry.sprite;
            preview.preserveAspect = true;
            preview.type = Image.Type.Simple;

            GameObject nameObject = new GameObject("Name", typeof(RectTransform));
            nameObject.transform.SetParent(item.transform, false);
            LayoutElement nameLayout = nameObject.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = nameRowHeight;

            TextMeshProUGUI nameLabel = nameObject.AddComponent<TextMeshProUGUI>();
            nameLabel.text = entry.displayName;
            nameLabel.fontSize = 11f;
            nameLabel.alignment = TextAlignmentOptions.Top;
            nameLabel.textWrappingMode = TextWrappingModes.Normal;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.color = new Color(1f, 1f, 1f, 0.9f);

            Button button = item.AddComponent<Button>();
            button.targetGraphic = background;
            string textureId = entry.id;
            button.onClick.AddListener(() => OnTextureChosen(textureId));

            return new TextureEntryView { Root = item };
        }

        void OnTextureChosen(string textureId)
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
            {
                SetRenameStatus("Kaplao is not ready.", success: false);
                return;
            }

            selectedTextureId = textureId ?? string.Empty;
            bool canRename = WorldCustomTextureService.IsCustomTexture(selectedTextureId);
            SetRenameControlsVisible(canRename);
            if (canRename && renameInputField != null)
                renameInputField.SetTextWithoutNotify(selectedTextureId);

            if (OverrideTextureChosen != null)
            {
                OverrideTextureChosen.Invoke(textureId ?? string.Empty);
                SetRenameStatus($"Selected texture '{textureId ?? "None"}'.", success: true);
                return;
            }

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestApplyTextureToSelectedTiles(textureId ?? string.Empty);
            SetRenameStatus($"Applied texture '{textureId ?? "None"}'.", success: true);
        }

        void OnChangeNameClicked()
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
            {
                SetRenameStatus("Kaplao is not ready.", success: false);
                return;
            }

            if (!WorldCustomTextureService.IsCustomTexture(selectedTextureId))
            {
                SetRenameStatus("Select a custom texture first.", success: false);
                return;
            }

            string nextName = renameInputField != null ? renameInputField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(nextName))
            {
                SetRenameStatus("Enter a new texture name.", success: false);
                return;
            }

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdRenameCustomTexture(selectedTextureId, nextName);
        }

        void SetRenameControlsVisible(bool visible)
        {
            if (renameInputField != null)
                renameInputField.gameObject.SetActive(visible);
            if (changeNameButton != null)
                changeNameButton.gameObject.SetActive(visible);
        }

        public void SetRenameStatus(string message, bool success)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = message ?? string.Empty;
            statusLabel.color = success
                ? new Color(0.45f, 1f, 0.55f, 0.95f)
                : new Color(1f, 0.55f, 0.55f, 0.95f);

            if (!success)
                return;

            if (renameInputField != null && !string.IsNullOrWhiteSpace(renameInputField.text))
                selectedTextureId = renameInputField.text.Trim();

            RebuildList();
        }

        static Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }

        void OnSearchChanged(string value)
        {
            currentSearch = value ?? string.Empty;
            RebuildList();
        }

        void ClearEntryViews()
        {
            for (int i = 0; i < entryViews.Count; i++)
                SafeDestroy(entryViews[i].Root);

            entryViews.Clear();
        }

        static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        void ApplyGridLayoutSettings()
        {
            if (listGrid == null)
                return;

            listGrid.cellSize = gridCellSize;
            listGrid.spacing = gridSpacing;
            listGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            listGrid.constraintCount = Mathf.Max(1, gridColumnCount);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            gridColumnCount = Mathf.Max(1, gridColumnCount);
            previewSize = Mathf.Max(32f, previewSize);
            nameRowHeight = Mathf.Max(20f, nameRowHeight);

            if (panelRoot != null)
                panelRoot.sizeDelta = panelSize;

            ApplyGridLayoutSettings();
        }
#endif

        sealed class TextureEntryView
        {
            public GameObject Root;
        }
    }
}
