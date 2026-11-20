using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Browsable Minecraft sound picker for Create mode. Sounds live under StreamingAssets/MinecraftSounds/.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoSoundPickerUi : MonoBehaviour
    {
        const string DefaultFolderSpritePath = "Assets/Sprites/Explorer/folder.png";
        const string DefaultSoundSpritePath = "Assets/Sprites/Explorer/ogg.png";

        [SerializeField] GameObject kaplaoOnlyRoot;

        [Header("Panel references (SoundPickerPanel under CreateModeUI)")]
        [SerializeField] RectTransform panelRoot;
        [SerializeField] TextMeshProUGUI pathLabel;
        [SerializeField] TextMeshProUGUI selectedPathLabel;
        [SerializeField] RectTransform listContent;
        [SerializeField] GridLayoutGroup listGrid;
        [SerializeField] Button backButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button copyNameButton;
        [SerializeField] TextMeshProUGUI statusLabel;

        [Header("Icons")]
        [SerializeField] Sprite folderIconSprite;
        [SerializeField] Sprite soundIconSprite;

        [Header("Layout")]
        [SerializeField] Vector2 panelSize = new Vector2(640f, 520f);
        [SerializeField] Vector2 gridCellSize = new Vector2(108f, 118f);
        [SerializeField] Vector2 gridSpacing = new Vector2(8f, 8f);
        [SerializeField] int gridColumnCount = 5;
        [SerializeField] float previewSize = 88f;
        [SerializeField] float nameRowHeight = 34f;

        readonly List<GameObject> entryViews = new();
        string currentRelativeFolder = string.Empty;
        string selectedRelativePath = string.Empty;
        bool panelWired;
        Coroutine copyFeedbackRoutine;

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;
        public bool IsOpen => panelRoot != null && panelRoot.gameObject.activeSelf;

        public struct PanelReferences
        {
            public RectTransform PanelRoot;
            public TextMeshProUGUI PathLabel;
            public TextMeshProUGUI SelectedPathLabel;
            public RectTransform ListContent;
            public GridLayoutGroup ListGrid;
            public Button BackButton;
            public Button CloseButton;
            public Button CopyNameButton;
            public TextMeshProUGUI StatusLabel;
        }

        public void SetKaplaoOnlyRoot(GameObject root) => kaplaoOnlyRoot = root;

        public void AssignPanelReferences(PanelReferences references)
        {
            panelRoot = references.PanelRoot;
            pathLabel = references.PathLabel;
            selectedPathLabel = references.SelectedPathLabel;
            listContent = references.ListContent;
            listGrid = references.ListGrid;
            backButton = references.BackButton;
            closeButton = references.CloseButton;
            copyNameButton = references.CopyNameButton;
            statusLabel = references.StatusLabel;
            panelWired = false;

            if (isActiveAndEnabled && panelRoot != null)
                WirePanel();
        }

        void OnEnable()
        {
            if (panelRoot != null)
                WirePanel();

            HidePanel();
        }

        public void ShowPanel()
        {
            if (panelRoot == null)
            {
                Debug.LogWarning("[Kaplao] Sound picker panel is missing. Run Kaplao/Setup Sound Picker UI.");
                return;
            }

            WirePanel();
            currentRelativeFolder = string.Empty;
            selectedRelativePath = string.Empty;
            RefreshSelectedPathLabel();
            RebuildList();
            panelRoot.SetAsLastSibling();
            panelRoot.gameObject.SetActive(true);
        }

        public void HidePanel()
        {
            LevelSoundPlayer.Stop();
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
            EnsureIconsLoaded();

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(GoToParentFolder);
                backButton.onClick.AddListener(GoToParentFolder);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HidePanel);
                closeButton.onClick.AddListener(HidePanel);
            }

            if (copyNameButton != null)
            {
                copyNameButton.onClick.RemoveListener(CopySelectedPath);
                copyNameButton.onClick.AddListener(CopySelectedPath);
            }

            panelWired = true;
        }

        void EnsureIconsLoaded()
        {
#if UNITY_EDITOR
            if (folderIconSprite == null)
                folderIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultFolderSpritePath);

            if (soundIconSprite == null)
                soundIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultSoundSpritePath);
#endif
        }

        void GoToParentFolder()
        {
            if (string.IsNullOrEmpty(currentRelativeFolder))
            {
                HidePanel();
                return;
            }

            currentRelativeFolder = Path.GetDirectoryName(currentRelativeFolder)?.Replace('\\', '/') ?? string.Empty;
            RebuildList();
        }

        void RebuildList()
        {
            if (listContent == null)
                return;

            ClearEntries();

            string rootPath = LevelSoundPlayer.GetStreamingSoundsRootPath();
            string folderPath = string.IsNullOrEmpty(currentRelativeFolder)
                ? rootPath
                : Path.Combine(rootPath, currentRelativeFolder);

            if (pathLabel != null)
            {
                pathLabel.text = string.IsNullOrEmpty(currentRelativeFolder)
                    ? "MinecraftSounds/"
                    : $"MinecraftSounds/{currentRelativeFolder}/";
            }

            if (!Directory.Exists(folderPath))
            {
                if (statusLabel != null)
                    statusLabel.text = $"Folder not found. Put sounds in Assets/StreamingAssets/{LevelSoundPlayer.SoundsRootFolderName}/";
                return;
            }

            string[] directories = Directory.GetDirectories(folderPath);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                CreateFolderEntry(name);
            }

            string[] files = Directory.GetFiles(folderPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            int soundCount = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]).ToLowerInvariant();
                if (extension != ".ogg" && extension != ".wav" && extension != ".mp3")
                    continue;

                string name = Path.GetFileName(files[i]);
                string relativePath = string.IsNullOrEmpty(currentRelativeFolder)
                    ? name
                    : $"{currentRelativeFolder}/{name}";
                CreateSoundEntry(name, relativePath.Replace('\\', '/'));
                soundCount++;
            }

            if (statusLabel != null)
                statusLabel.text = $"{directories.Length} folders, {soundCount} sounds";
        }

        void CreateFolderEntry(string folderName)
        {
            Button button = CreateGridEntry(folderName, folderIconSprite);
            string nextFolder = string.IsNullOrEmpty(currentRelativeFolder)
                ? folderName
                : $"{currentRelativeFolder}/{folderName}";
            button.onClick.AddListener(() =>
            {
                currentRelativeFolder = nextFolder.Replace('\\', '/');
                RebuildList();
            });
        }

        void CreateSoundEntry(string displayName, string relativePath)
        {
            Button button = CreateGridEntry(displayName, soundIconSprite);
            button.onClick.AddListener(() =>
            {
                selectedRelativePath = relativePath;
                RefreshSelectedPathLabel();
                PreviewSound(relativePath);
            });
        }

        Button CreateGridEntry(string displayName, Sprite iconSprite)
        {
            GameObject item = new GameObject(displayName, typeof(RectTransform));
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
            preview.sprite = iconSprite;
            preview.preserveAspect = true;
            preview.type = Image.Type.Simple;
            preview.color = iconSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);

            GameObject nameObject = new GameObject("Name", typeof(RectTransform));
            nameObject.transform.SetParent(item.transform, false);
            LayoutElement nameLayout = nameObject.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = nameRowHeight;

            TextMeshProUGUI nameLabel = nameObject.AddComponent<TextMeshProUGUI>();
            nameLabel.text = displayName;
            nameLabel.fontSize = 11f;
            nameLabel.alignment = TextAlignmentOptions.Top;
            nameLabel.textWrappingMode = TextWrappingModes.Normal;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.color = new Color(1f, 1f, 1f, 0.9f);

            Button button = item.AddComponent<Button>();
            button.targetGraphic = background;
            entryViews.Add(item);
            return button;
        }

        void PreviewSound(string relativePath)
        {
            LevelSoundPlayer.Preview(relativePath);
        }

        void RefreshSelectedPathLabel()
        {
            if (selectedPathLabel != null)
                selectedPathLabel.text = string.IsNullOrEmpty(selectedRelativePath) ? "(none)" : selectedRelativePath;
        }

        void CopySelectedPath()
        {
            if (string.IsNullOrEmpty(selectedRelativePath) || copyNameButton == null)
                return;

            GUIUtility.systemCopyBuffer = selectedRelativePath;
            if (copyFeedbackRoutine != null)
                StopCoroutine(copyFeedbackRoutine);

            copyFeedbackRoutine = StartCoroutine(ShowCopiedFeedback());
        }

        IEnumerator ShowCopiedFeedback()
        {
            TextMeshProUGUI label = copyNameButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null)
                yield break;

            string original = label.text;
            label.text = "Copied!";
            yield return new WaitForSecondsRealtime(0.5f);
            label.text = original;
            copyFeedbackRoutine = null;
        }

        void ClearEntries()
        {
            for (int i = 0; i < entryViews.Count; i++)
                SafeDestroy(entryViews[i]);

            entryViews.Clear();
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

        static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
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
    }
}
