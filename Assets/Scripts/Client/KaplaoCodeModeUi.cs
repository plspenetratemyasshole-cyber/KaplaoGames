using System;
using System.Collections;
using System.Collections.Generic;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Fullscreen Code IDE: file tree, tabs, editor, run/stop for Scripts/main.ks.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoCodeModeUi : MonoBehaviour
    {
        const float ScriptSyncDelay = 0.35f;
        public const float TopBarHeight = 104f;
        public const float FileTreeWidth = 260f;
        public const float TabBarHeight = 32f;

        [SerializeField] GameObject overlayRoot;
        [SerializeField] TMP_InputField codeInputField;
        [SerializeField] Button runButton;
        [SerializeField] TextMeshProUGUI runButtonLabel;
        [SerializeField] Button documentationButton;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] string documentationUrl = "https://github.com/";
        [SerializeField] Sprite fileIcon;
        [SerializeField] Sprite folderIcon;
        [SerializeField] Sprite folderToggleIcon;
        [SerializeField] KaplaoCodeFileTreePanel fileTreePanel;
        [SerializeField] KaplaoCodeTabBar tabBar;
        [SerializeField] KaplaoCodeContextMenuUi contextMenu;
        [SerializeField] GameObject namePromptRoot;
        [SerializeField] TMP_InputField namePromptInput;
        [SerializeField] TextMeshProUGUI namePromptTitle;
        [SerializeField] GameObject deleteConfirmRoot;
        [SerializeField] TextMeshProUGUI deleteConfirmMessage;
        [SerializeField] KaplaoCodeOutputPanel outputPanel;

        [Header("Layout")]
        [Tooltip("When enabled the overlay layout is applied automatically at runtime. Disable this to edit overlay RectTransform manually in the Editor.")]
        [SerializeField] private bool autoLayoutOverlay = true;

        KaplaoCursorClient cursorClient;
        KaplaoCursorServer cursorServer;
        NetworkPlayer localPlayer;
        bool applyingRemoteScript;
        bool scriptEditorDirty;
        bool sessionSubscribed;
        bool projectSubscribed;
        string openFilePath = WorldScriptProjectService.EntryFileName;
        readonly Dictionary<string, string> openFileBuffers =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, bool> dirtyFiles =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Coroutine scriptSyncRoutine;
        LevelScriptDiagnostic lastDiagnostic;

        enum NamePromptMode
        {
            None,
            NewFile,
            NewLocalFile,
            NewFolder,
            Rename
        }

        NamePromptMode namePromptMode;
        string namePromptTargetFolder = string.Empty;
        string namePromptRenamePath = string.Empty;

        struct ClipboardEntry
        {
            public bool IsDirectory;
            public string Name;
            public string Content;
            public List<(string relativePath, bool isDirectory, string content)> Tree;
        }

        ClipboardEntry? clipboard;

        public static KaplaoCodeModeUi Instance { get; private set; }

        public static void FlushPendingScriptSync() => Instance?.FlushScriptSyncImmediate();

        public static string GetCurrentScriptText()
        {
            if (Instance == null)
                return WorldScriptProjectService.GetEntryScriptContent();

            if (Instance.openFileBuffers.TryGetValue(WorldScriptProjectService.EntryFileName, out string buffered))
                return buffered;

            return WorldScriptProjectService.GetEntryScriptContent();
        }

        void Awake()
        {
            Instance = this;
            ResolveCodeInputField();
            ResolveStatusLabel();
            if (outputPanel == null && overlayRoot != null)
                outputPanel = overlayRoot.GetComponentInChildren<KaplaoCodeOutputPanel>(true);

            if (overlayRoot != null)
                overlayRoot.SetActive(false);

            if (runButtonLabel == null && runButton != null)
                runButtonLabel = runButton.GetComponentInChildren<TextMeshProUGUI>();

            if (fileTreePanel != null)
            {
                fileTreePanel.SetIcons(fileIcon, folderIcon, folderToggleIcon);
                if (overlayRoot != null)
                    fileTreePanel.SetDragParent(overlayRoot.GetComponent<RectTransform>());
            }

            EnsureCodeEditorEnhancer();
            if (autoLayoutOverlay)
                ApplyIdeLayout(overlayRoot, codeInputField, runButton, statusLabel, documentationButton);
            EnsureStatusLabelVisible();
            HideNamePrompt();
            HideDeleteConfirm();
            contextMenu?.Hide();
        }

        void ResolveCodeInputField()
        {
            if (codeInputField != null)
                return;

            if (overlayRoot != null)
                codeInputField = overlayRoot.GetComponentInChildren<TMP_InputField>(true);
        }

        void ResolveStatusLabel()
        {
            if (statusLabel != null)
                return;

            if (overlayRoot == null)
                return;

            Transform found = overlayRoot.transform.Find("CodeStatusLabel");
            if (found != null)
                statusLabel = found.GetComponent<TextMeshProUGUI>();
        }

        void EnsureStatusLabelVisible()
        {
            ResolveStatusLabel();
            if (statusLabel == null)
                return;

            statusLabel.gameObject.SetActive(true);
            statusLabel.enabled = true;
            statusLabel.richText = false;
            statusLabel.raycastTarget = false;
            statusLabel.color = Color.white;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.overflowMode = TextOverflowModes.Ellipsis;
            statusLabel.transform.SetAsLastSibling();
        }

        void EnsureCodeEditorEnhancer()
        {
            ResolveCodeInputField();
            if (codeInputField == null)
                return;

            LevelScriptCodeEditorEnhancer enhancer = codeInputField.GetComponent<LevelScriptCodeEditorEnhancer>();
            if (enhancer == null)
                enhancer = codeInputField.gameObject.AddComponent<LevelScriptCodeEditorEnhancer>();

            enhancer.DiagnosticChanged -= OnEditorDiagnosticChanged;
            enhancer.DiagnosticChanged += OnEditorDiagnosticChanged;
            enhancer.ParameterHintChanged -= OnEditorParameterHintChanged;
            enhancer.ParameterHintChanged += OnEditorParameterHintChanged;
            enhancer.CurrentFilePath = openFilePath ?? string.Empty;
        }

        public static void ApplyIdeLayout(
            GameObject overlayRoot,
            TMP_InputField codeInputField,
            Button runButton,
            TextMeshProUGUI statusLabel,
            Button documentationButton = null)
        {
            if (overlayRoot == null)
                return;

            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            if (overlayRect == null)
                return;

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = new Vector2(0f, -TopBarHeight);

            Transform tree = overlayRoot.transform.Find("FileTreePanel");
            if (tree != null)
            {
                RectTransform treeRect = tree.GetComponent<RectTransform>();
                treeRect.anchorMin = new Vector2(0f, 0f);
                treeRect.anchorMax = new Vector2(0f, 1f);
                treeRect.pivot = new Vector2(0f, 0.5f);
                treeRect.anchoredPosition = Vector2.zero;
                treeRect.sizeDelta = new Vector2(FileTreeWidth, 0f);
                treeRect.offsetMin = new Vector2(0f, 64f);
                treeRect.offsetMax = new Vector2(FileTreeWidth, -8f);
            }

            Transform tabBarTransform = overlayRoot.transform.Find("TabBar");
            if (tabBarTransform != null)
            {
                RectTransform tabRect = tabBarTransform.GetComponent<RectTransform>();
                tabRect.anchorMin = new Vector2(0f, 1f);
                tabRect.anchorMax = new Vector2(1f, 1f);
                tabRect.pivot = new Vector2(0.5f, 1f);
                tabRect.anchoredPosition = Vector2.zero;
                tabRect.sizeDelta = new Vector2(0f, TabBarHeight);
                tabRect.offsetMin = new Vector2(FileTreeWidth + 8f, -TabBarHeight - 8f);
                tabRect.offsetMax = new Vector2(-8f, -8f);
            }

            Transform outputPanelTransform = overlayRoot.transform.Find("CodeOutputPanel");
            float outputHeight = 150f;
            if (outputPanelTransform != null)
            {
                RectTransform outRect = outputPanelTransform.GetComponent<RectTransform>();
                outRect.anchorMin = new Vector2(0f, 0f);
                outRect.anchorMax = new Vector2(1f, 0f);
                outRect.pivot = new Vector2(0.5f, 0f);
                outRect.anchoredPosition = Vector2.zero;
                outRect.offsetMin = new Vector2(FileTreeWidth + 8f, 52f);
                outRect.offsetMax = new Vector2(-8f, 52f + outputHeight);
            }

            if (codeInputField != null)
            {
                RectTransform codeRect = codeInputField.GetComponent<RectTransform>();
                codeRect.anchorMin = Vector2.zero;
                codeRect.anchorMax = Vector2.one;
                float bottomOffset = outputPanelTransform != null ? (52f + outputHeight + 6f) : 72f;
                codeRect.offsetMin = new Vector2(FileTreeWidth + 8f, bottomOffset);
                codeRect.offsetMax = new Vector2(-8f, -(TabBarHeight + 16f));
            }

            if (runButton != null)
            {
                RectTransform runRect = runButton.GetComponent<RectTransform>();
                runRect.anchorMin = new Vector2(1f, 0f);
                runRect.anchorMax = new Vector2(1f, 0f);
                runRect.pivot = new Vector2(1f, 0f);
                runRect.anchoredPosition = new Vector2(-24f, 12f);
                runRect.sizeDelta = new Vector2(120f, 32f);
            }

            if (documentationButton != null)
            {
                RectTransform docRect = documentationButton.GetComponent<RectTransform>();
                docRect.anchorMin = new Vector2(0f, 0f);
                docRect.anchorMax = new Vector2(0f, 0f);
                docRect.pivot = new Vector2(0f, 0f);
                docRect.anchoredPosition = new Vector2(FileTreeWidth + 16f, 12f);
                docRect.sizeDelta = new Vector2(140f, 32f);
            }

            if (statusLabel != null)
            {
                RectTransform statusRect = statusLabel.rectTransform;
                statusRect.anchorMin = new Vector2(0.5f, 0f);
                statusRect.anchorMax = new Vector2(0.5f, 0f);
                statusRect.pivot = new Vector2(0.5f, 0f);
                statusRect.anchoredPosition = new Vector2(FileTreeWidth * 0.5f, 14f);
                statusRect.sizeDelta = new Vector2(500f, 28f);
            }
        }

        // Back-compat for older setup scripts.
        public static void ApplyOverlayLayout(
            GameObject overlayRoot,
            TMP_InputField codeInputField,
            Button runButton,
            TextMeshProUGUI statusLabel,
            Button documentationButton = null) =>
            ApplyIdeLayout(overlayRoot, codeInputField, runButton, statusLabel, documentationButton);

        void OnEnable()
        {
            Instance = this;
            ResolveCodeInputField();
            ResolveStatusLabel();
            EnsureCodeEditorEnhancer();
            EnsureStatusLabelVisible();

            if (runButton != null)
                runButton.onClick.AddListener(ToggleRunStop);

            if (documentationButton != null)
                documentationButton.onClick.AddListener(OpenDocumentation);

            if (codeInputField != null)
                codeInputField.onValueChanged.AddListener(OnCodeInputChanged);

            if (fileTreePanel != null)
            {
                fileTreePanel.FileActivated += OnTreeFileActivated;
                fileTreePanel.ContextMenuRequested += ShowContextMenu;
                fileTreePanel.NodeMoved += RequestMoveNode;
            }

            if (tabBar != null)
            {
                tabBar.TabSelected += OpenFileFromTab;
                tabBar.TabClosed += CloseTab;
            }

            if (namePromptInput != null)
                namePromptInput.onSubmit.AddListener(OnNamePromptSubmit);

            WireNamePromptButtons();
            WireDeleteConfirmButtons();

            WorldScriptProjectService.ProjectChanged += OnProjectChanged;
            StartCoroutine(BindWhenReady());
        }

        void OnTreeFileActivated(string path) => OpenFile(path, syncTreeSelection: false);

        void OpenFileFromTab(string path) => OpenFile(path, syncTreeSelection: true);

        void WireDeleteConfirmButtons()
        {
            if (deleteConfirmRoot == null)
                return;

            Transform okTransform = deleteConfirmRoot.transform.Find("OkButton");
            if (okTransform != null && okTransform.TryGetComponent(out Button okButton))
            {
                okButton.onClick.RemoveListener(ConfirmDeleteSelection);
                okButton.onClick.AddListener(ConfirmDeleteSelection);
            }

            Transform cancelTransform = deleteConfirmRoot.transform.Find("CancelButton");
            if (cancelTransform != null && cancelTransform.TryGetComponent(out Button cancelButton))
            {
                cancelButton.onClick.RemoveListener(CancelDeleteConfirm);
                cancelButton.onClick.AddListener(CancelDeleteConfirm);
            }
        }

        void WireNamePromptButtons()
        {
            if (namePromptRoot == null)
                return;

            Transform okTransform = namePromptRoot.transform.Find("OkButton");
            if (okTransform != null && okTransform.TryGetComponent(out Button okButton))
            {
                okButton.onClick.RemoveListener(ConfirmNamePrompt);
                okButton.onClick.AddListener(ConfirmNamePrompt);
            }

            Transform cancelTransform = namePromptRoot.transform.Find("CancelButton");
            if (cancelTransform != null && cancelTransform.TryGetComponent(out Button cancelButton))
            {
                cancelButton.onClick.RemoveListener(CancelNamePrompt);
                cancelButton.onClick.AddListener(CancelNamePrompt);
            }
        }

        void OnNamePromptSubmit(string _) => ConfirmNamePrompt();

        void OnDisable()
        {
            FlushScriptSyncImmediate();

            if (Instance == this)
                Instance = null;

            if (codeInputField != null &&
                codeInputField.TryGetComponent(out LevelScriptCodeEditorEnhancer enhancer))
            {
                enhancer.DiagnosticChanged -= OnEditorDiagnosticChanged;
                enhancer.ParameterHintChanged -= OnEditorParameterHintChanged;
            }

            if (runButton != null)
                runButton.onClick.RemoveListener(ToggleRunStop);

            if (documentationButton != null)
                documentationButton.onClick.RemoveListener(OpenDocumentation);

            if (codeInputField != null)
                codeInputField.onValueChanged.RemoveListener(OnCodeInputChanged);

            if (fileTreePanel != null)
            {
                fileTreePanel.FileActivated -= OnTreeFileActivated;
                fileTreePanel.ContextMenuRequested -= ShowContextMenu;
                fileTreePanel.NodeMoved -= RequestMoveNode;
            }

            if (tabBar != null)
            {
                tabBar.TabSelected -= OpenFileFromTab;
                tabBar.TabClosed -= CloseTab;
            }

            if (namePromptInput != null)
                namePromptInput.onSubmit.RemoveListener(OnNamePromptSubmit);

            if (namePromptRoot != null)
            {
                Transform okTransform = namePromptRoot.transform.Find("OkButton");
                if (okTransform != null && okTransform.TryGetComponent(out Button okButton))
                    okButton.onClick.RemoveListener(ConfirmNamePrompt);

                Transform cancelTransform = namePromptRoot.transform.Find("CancelButton");
                if (cancelTransform != null && cancelTransform.TryGetComponent(out Button cancelButton))
                    cancelButton.onClick.RemoveListener(CancelNamePrompt);
            }

            if (deleteConfirmRoot != null)
            {
                Transform okTransform = deleteConfirmRoot.transform.Find("OkButton");
                if (okTransform != null && okTransform.TryGetComponent(out Button okButton))
                    okButton.onClick.RemoveListener(ConfirmDeleteSelection);

                Transform cancelTransform = deleteConfirmRoot.transform.Find("CancelButton");
                if (cancelTransform != null && cancelTransform.TryGetComponent(out Button cancelButton))
                    cancelButton.onClick.RemoveListener(CancelDeleteConfirm);
            }

            WorldScriptProjectService.ProjectChanged -= OnProjectChanged;
            Unsubscribe();
        }

        void Update()
        {
            if (overlayRoot == null || !overlayRoot.activeSelf)
                return;

            if (deleteConfirmRoot != null && deleteConfirmRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    HideDeleteConfirm();
                return;
            }

            if (namePromptRoot != null && namePromptRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    HideNamePrompt();
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                        Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (ctrl && Input.GetKeyDown(KeyCode.S))
                FlushScriptSyncImmediate();

            bool editorFocused = codeInputField != null && codeInputField.isFocused;
            if (!editorFocused)
            {
                if (Input.GetKeyDown(KeyCode.F2))
                    TryRenameSelectedNode();
                else if (Input.GetKeyDown(KeyCode.Delete))
                    TryDeleteSelectedNode();

                if (ctrl && Input.GetKeyDown(KeyCode.C))
                    TryCopySelectedNode();
                else if (ctrl && Input.GetKeyDown(KeyCode.V))
                    TryPasteClipboard();
                else if (ctrl && Input.GetKeyDown(KeyCode.D))
                    TryDuplicateSelectedNode();
            }
        }

        void TryCopySelectedNode()
        {
            string path = fileTreePanel != null ? fileTreePanel.SelectedPath : openFilePath;
            if (string.IsNullOrEmpty(path))
                return;
            CopyNode(path);
        }

        void TryPasteClipboard()
        {
            string folder = fileTreePanel != null ? fileTreePanel.SelectedFolderForCreate : string.Empty;
            PasteInto(folder);
        }

        void TryDuplicateSelectedNode()
        {
            string path = fileTreePanel != null ? fileTreePanel.SelectedPath : openFilePath;
            if (string.IsNullOrEmpty(path))
                return;
            if (string.Equals(path, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Cannot duplicate main.ks.");
                return;
            }

            CopyNode(path);
            string folder = WorldScriptProjectService.GetParentPath(path);
            PasteDuplicateInto(folder);
        }

        void PasteDuplicateInto(string folder)
        {
            if (!clipboard.HasValue)
                return;

            ClipboardEntry entry = clipboard.Value;
            string baseName = entry.Name;
            string destination = UniqueCopyPath(folder, baseName, entry.IsDirectory);

            if (entry.IsDirectory)
            {
                RequestCreateFolder(destination);
                for (int i = 0; i < entry.Tree.Count; i++)
                {
                    (string relativePath, bool isDirectory, string content) child = entry.Tree[i];
                    if (string.IsNullOrEmpty(child.relativePath))
                        continue;
                    string childPath = WorldScriptProjectService.CombinePath(destination, child.relativePath);
                    if (child.isDirectory)
                        RequestCreateFolder(childPath);
                    else
                        RequestCreateFile(childPath, child.content);
                }
            }
            else
            {
                RequestCreateFile(destination, entry.Content);
                StartCoroutine(OpenFileNextFrame(destination));
            }

            SetStatus("Duplicated " + baseName);
        }

        string UniqueCopyPath(string folder, string baseName, bool isDirectory)
        {
            string candidateName = "Copy of " + baseName;
            string destination = WorldScriptProjectService.CombinePath(folder, candidateName);
            int index = 2;
            while (WorldScriptProjectService.TryGetNode(destination, out _))
            {
                candidateName = "Copy of " + baseName + " (" + index + ")";
                destination = WorldScriptProjectService.CombinePath(folder, candidateName);
                index++;
                if (index > 99)
                    break;
            }

            return destination;
        }

        void TryRenameSelectedNode()
        {
            string path = fileTreePanel != null ? fileTreePanel.SelectedPath : openFilePath;
            if (string.IsNullOrEmpty(path))
                return;
            if (string.Equals(path, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Cannot rename main.ks.");
                return;
            }

            if (!WorldScriptProjectService.TryGetNode(path, out _))
                return;

            string folder = WorldScriptProjectService.GetParentPath(path);
            BeginNamePrompt(NamePromptMode.Rename, folder, path);
        }

        void TryDeleteSelectedNode()
        {
            BeginDeleteConfirm(CollectDeletableSelection());
        }

        List<string> CollectDeletableSelection()
        {
            var result = new List<string>();
            if (fileTreePanel != null && fileTreePanel.SelectedPaths.Count > 0)
            {
                foreach (string path in fileTreePanel.SelectedPaths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    if (string.Equals(path, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!WorldScriptProjectService.TryGetNode(path, out _))
                        continue;
                    result.Add(path);
                }
            }
            else
            {
                string path = !string.IsNullOrEmpty(fileTreePanel?.SelectedPath)
                    ? fileTreePanel.SelectedPath
                    : openFilePath;
                if (!string.IsNullOrEmpty(path) &&
                    !string.Equals(path, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase) &&
                    WorldScriptProjectService.TryGetNode(path, out _))
                {
                    result.Add(path);
                }
            }

            return result;
        }

        readonly List<string> pendingDeletePaths = new List<string>();

        void BeginDeleteConfirm(List<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                if (fileTreePanel != null &&
                    string.Equals(fileTreePanel.SelectedPath, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("Cannot delete main.ks.");
                }

                return;
            }

            pendingDeletePaths.Clear();
            pendingDeletePaths.AddRange(paths);

            if (deleteConfirmRoot == null)
            {
                ConfirmDeleteSelection();
                return;
            }

            if (deleteConfirmMessage != null)
            {
                deleteConfirmMessage.text = pendingDeletePaths.Count == 1
                    ? "Are you sure?\nDelete \"" + GetDisplayName(pendingDeletePaths[0]) + "\"?"
                    : "Are you sure?\nDelete " + pendingDeletePaths.Count + " items?";
            }

            deleteConfirmRoot.SetActive(true);
            deleteConfirmRoot.transform.SetAsLastSibling();
        }

        static string GetDisplayName(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        public void ConfirmDeleteSelection()
        {
            HideDeleteConfirm();
            for (int i = 0; i < pendingDeletePaths.Count; i++)
                RequestDeleteNode(pendingDeletePaths[i]);
            pendingDeletePaths.Clear();
        }

        public void CancelDeleteConfirm() => HideDeleteConfirm();

        void HideDeleteConfirm()
        {
            if (deleteConfirmRoot != null)
                deleteConfirmRoot.SetActive(false);
        }

        IEnumerator BindWhenReady()
        {
            while (enabled)
            {
                EnsureSessionSubscribed();

                if (NetworkClient.localPlayer != null &&
                    NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player))
                {
                    if (localPlayer != player)
                    {
                        UnsubscribeLocalPlayer();
                        localPlayer = player;
                        localPlayer.RoleChanged += OnLocalRoleChanged;
                    }

                    if (player.role == PlayerRole.Kaplao)
                    {
                        KaplaoCursorClient nextClient = NetworkClient.localPlayer.GetComponent<KaplaoCursorClient>();
                        KaplaoCursorServer nextServer = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
                        if (nextClient != cursorClient || nextServer != cursorServer)
                        {
                            UnsubscribeCursorOnly();
                            cursorClient = nextClient;
                            cursorServer = nextServer;
                            if (cursorClient != null && cursorServer != null)
                            {
                                cursorServer.HostGameModeChanged += OnModeChanged;
                                KaplaoLocalGameModePredictor.Changed += OnModeChanged;
                                OnModeChanged(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
                                SetRunningState(LevelSessionServer.Instance != null && LevelSessionServer.Instance.ScriptRunning);
                            }
                        }
                    }
                    else
                    {
                        if (overlayRoot != null)
                            overlayRoot.SetActive(false);
                        UnsubscribeCursorOnly();
                    }
                }
                else
                {
                    UnsubscribeCursorOnly();
                }

                yield return null;
            }
        }

        void EnsureSessionSubscribed()
        {
            if (LevelSessionServer.Instance == null)
                return;

            if (!sessionSubscribed)
            {
                LevelSessionServer.Instance.LevelScriptChanged += OnSharedScriptChanged;
                LevelSessionServer.Instance.ScriptRunningStateChanged += OnScriptRunningStateChanged;
                sessionSubscribed = true;
                OnScriptRunningStateChanged(LevelSessionServer.Instance.ScriptRunning);
            }

            if (!projectSubscribed)
            {
                LevelSessionServer.Instance.ScriptProjectChanged += OnProjectChanged;
                projectSubscribed = true;
                OnProjectChanged();
            }
        }

        void UnsubscribeSession()
        {
            if (LevelSessionServer.Instance == null)
            {
                sessionSubscribed = false;
                projectSubscribed = false;
                return;
            }

            if (sessionSubscribed)
            {
                LevelSessionServer.Instance.LevelScriptChanged -= OnSharedScriptChanged;
                LevelSessionServer.Instance.ScriptRunningStateChanged -= OnScriptRunningStateChanged;
                sessionSubscribed = false;
            }

            if (projectSubscribed)
            {
                LevelSessionServer.Instance.ScriptProjectChanged -= OnProjectChanged;
                projectSubscribed = false;
            }
        }

        void UnsubscribeLocalPlayer()
        {
            if (localPlayer == null)
                return;

            localPlayer.RoleChanged -= OnLocalRoleChanged;
            localPlayer = null;
        }

        void OnLocalRoleChanged(PlayerRole role)
        {
            if (role != PlayerRole.Kaplao)
            {
                if (overlayRoot != null)
                    overlayRoot.SetActive(false);
                UnsubscribeCursorOnly();
            }
        }

        void UnsubscribeCursorOnly()
        {
            if (cursorServer != null)
                cursorServer.HostGameModeChanged -= OnModeChanged;

            KaplaoLocalGameModePredictor.Changed -= OnModeChanged;
            cursorServer = null;
            cursorClient = null;
        }

        void Unsubscribe()
        {
            UnsubscribeSession();
            UnsubscribeCursorOnly();
            UnsubscribeLocalPlayer();
        }

        void OnProjectChanged()
        {
            fileTreePanel?.Refresh();

            // Refresh open buffers from project when not dirty.
            List<string> keys = new List<string>(openFileBuffers.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string path = keys[i];
                if (dirtyFiles.TryGetValue(path, out bool dirty) && dirty)
                    continue;

                if (WorldScriptProjectService.TryReadFile(path, out string content, out _))
                    openFileBuffers[path] = content;
                else
                {
                    openFileBuffers.Remove(path);
                    dirtyFiles.Remove(path);
                    tabBar?.Close(path);
                }
            }

            if (!string.IsNullOrEmpty(openFilePath) &&
                openFileBuffers.TryGetValue(openFilePath, out string active) &&
                codeInputField != null)
            {
                if (!string.Equals(codeInputField.text, active, StringComparison.Ordinal))
                {
                    applyingRemoteScript = true;
                    int caret = codeInputField.caretPosition;
                    codeInputField.text = active;
                    codeInputField.caretPosition = Math.Min(caret, active.Length);
                    scriptEditorDirty = false;
                    dirtyFiles[openFilePath] = false;
                    tabBar?.SetDirty(openFilePath, false);
                    applyingRemoteScript = false;
                    RefreshEditorWhenVisible();
                    RefreshStatusLine();
                }
            }

            if (overlayRoot != null && overlayRoot.activeSelf &&
                (string.IsNullOrEmpty(openFilePath) || !WorldScriptProjectService.TryGetNode(openFilePath, out _)))
            {
                OpenFile(WorldScriptProjectService.EntryFileName);
            }
        }

        void OnSharedScriptChanged(string script)
        {
            // Entry file may be updated via SyncVar while other files use project sync.
            if (!dirtyFiles.TryGetValue(WorldScriptProjectService.EntryFileName, out bool dirty) || !dirty)
            {
                openFileBuffers[WorldScriptProjectService.EntryFileName] = script ?? string.Empty;
                if (string.Equals(openFilePath, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase) &&
                    codeInputField != null &&
                    !codeInputField.isFocused)
                {
                    applyingRemoteScript = true;
                    codeInputField.text = script ?? string.Empty;
                    applyingRemoteScript = false;
                    scriptEditorDirty = false;
                }
            }
        }

        void OnScriptRunningStateChanged(bool running) => SetRunningState(running);

        void OnModeChanged(HostGameMode mode)
        {
            HostGameMode effective = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            bool show = effective == HostGameMode.Code;
            if (!show)
                FlushScriptSyncImmediate();

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(show);
                if (show)
                    BringCodeOverlayBelowPause();
            }

            if (show)
                Cursor.visible = true;

            if (!show)
                return;

            fileTreePanel?.SetIcons(fileIcon, folderIcon, folderToggleIcon);
            if (fileTreePanel != null && overlayRoot != null)
                fileTreePanel.SetDragParent(overlayRoot.GetComponent<RectTransform>());
            fileTreePanel?.Refresh();
            OpenFile(WorldScriptProjectService.EntryFileName);
            SetRunningState(LevelSessionServer.Instance != null && LevelSessionServer.Instance.ScriptRunning);
            EnsureStatusLabelVisible();
            RefreshStatusLine();
            StartCoroutine(RefreshEditorNextFrame());
        }

        void BringCodeOverlayBelowPause()
        {
            if (overlayRoot == null)
                return;

            overlayRoot.transform.SetAsLastSibling();
            if (overlayRoot.TryGetComponent(out RectTransform rect))
                UiFlyInUtility.SnapToRest(rect);

            if (!PauseCursorState.IsPauseOpen)
                return;

            PauseMenuUI pauseUi = FindFirstObjectByType<PauseMenuUI>();
            if (pauseUi != null)
            {
                pauseUi.transform.SetAsLastSibling();
                if (pauseUi.PauseMenuRoot != null)
                    pauseUi.PauseMenuRoot.transform.SetAsLastSibling();
            }

            RunnerPauseMenuUI runnerPause = FindFirstObjectByType<RunnerPauseMenuUI>();
            if (runnerPause != null)
            {
                runnerPause.transform.SetAsLastSibling();
                if (runnerPause.PauseMenuRoot != null)
                    runnerPause.PauseMenuRoot.transform.SetAsLastSibling();
            }

            BringHostModerationAbovePause();
        }

        static void BringHostModerationAbovePause()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            Transform moderation = canvas.transform.Find("HostModerationPanel");
            if (moderation != null)
                moderation.SetAsLastSibling();
        }

        void OpenFile(string relativePath) => OpenFile(relativePath, syncTreeSelection: true);

        void OpenFile(string relativePath, bool syncTreeSelection)
        {
            relativePath = WorldScriptProjectService.NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(relativePath))
                return;

            if (!WorldScriptProjectService.TryGetNode(relativePath, out ScriptNodeState node) || node.IsDirectory)
                return;

            if (!KaplaoCodeFileTreePanel.IsEditableScriptPath(relativePath))
                return;

            FlushScriptSyncImmediate();

            if (!openFileBuffers.TryGetValue(relativePath, out string content))
            {
                content = node.Content ?? string.Empty;
                openFileBuffers[relativePath] = content;
            }

            openFilePath = relativePath;
            bool dirty = dirtyFiles.TryGetValue(relativePath, out bool d) && d;
            tabBar?.OpenOrFocus(relativePath, dirty);
            if (syncTreeSelection)
                fileTreePanel?.SelectPath(relativePath, notify: false);

            if (codeInputField != null)
            {
                applyingRemoteScript = true;
                codeInputField.text = content;
                applyingRemoteScript = false;
                scriptEditorDirty = dirty;
                if (codeInputField.TryGetComponent(out LevelScriptCodeEditorEnhancer enhancer))
                    enhancer.CurrentFilePath = relativePath;
            }

            RefreshEditorWhenVisible();
            RefreshStatusLine();
        }

        void CloseTab(string relativePath)
        {
            if (string.Equals(relativePath, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase) &&
                tabBar != null && !tabBar.HasTab(WorldScriptProjectService.EntryFileName))
            {
                // Always keep entry available.
                OpenFile(WorldScriptProjectService.EntryFileName);
                return;
            }

            FlushScriptSyncImmediate();
            openFileBuffers.Remove(relativePath);
            dirtyFiles.Remove(relativePath);
            tabBar?.Close(relativePath);

            string next = tabBar != null ? tabBar.ActivePath : WorldScriptProjectService.EntryFileName;
            if (string.IsNullOrEmpty(next))
                next = WorldScriptProjectService.EntryFileName;
            OpenFile(next);
        }

        void OnCodeInputChanged(string _)
        {
            if (applyingRemoteScript || codeInputField == null || string.IsNullOrEmpty(openFilePath))
                return;

            scriptEditorDirty = true;
            openFileBuffers[openFilePath] = codeInputField.text ?? string.Empty;
            dirtyFiles[openFilePath] = true;
            tabBar?.SetDirty(openFilePath, true);

            if (scriptSyncRoutine != null)
                StopCoroutine(scriptSyncRoutine);
            scriptSyncRoutine = StartCoroutine(SyncScriptSoon());
        }

        IEnumerator SyncScriptSoon()
        {
            yield return new WaitForSecondsRealtime(ScriptSyncDelay);
            FlushScriptSyncImmediate();
            scriptSyncRoutine = null;
        }

        void FlushScriptSyncImmediate()
        {
            if (scriptSyncRoutine != null)
            {
                StopCoroutine(scriptSyncRoutine);
                scriptSyncRoutine = null;
            }

            if (applyingRemoteScript || codeInputField == null || string.IsNullOrEmpty(openFilePath))
                return;

            if (!scriptEditorDirty && !(dirtyFiles.TryGetValue(openFilePath, out bool dirty) && dirty))
                return;

            string content = codeInputField.text ?? string.Empty;
            openFileBuffers[openFilePath] = content;
            RequestWriteFile(openFilePath, content);
            scriptEditorDirty = false;
            dirtyFiles[openFilePath] = false;
            tabBar?.SetDirty(openFilePath, false);
        }

        void FlushAllDirtyScriptBuffers()
        {
            if (scriptSyncRoutine != null)
            {
                StopCoroutine(scriptSyncRoutine);
                scriptSyncRoutine = null;
            }

            if (applyingRemoteScript)
                return;

            if (codeInputField != null && !string.IsNullOrEmpty(openFilePath))
            {
                openFileBuffers[openFilePath] = codeInputField.text ?? string.Empty;
                if (scriptEditorDirty || (dirtyFiles.TryGetValue(openFilePath, out bool activeDirty) && activeDirty))
                    dirtyFiles[openFilePath] = true;
            }

            List<string> paths = new List<string>(dirtyFiles.Keys);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (!dirtyFiles.TryGetValue(path, out bool dirty) || !dirty)
                    continue;

                if (!openFileBuffers.TryGetValue(path, out string content))
                    continue;

                RequestWriteFile(path, content);
                dirtyFiles[path] = false;
                tabBar?.SetDirty(path, false);
            }

            scriptEditorDirty = false;
        }

        void RequestWriteFile(string path, string content)
        {
            if (NetworkServer.active)
            {
                if (cursorServer != null)
                    cursorServer.WriteScriptFile(path, content);
                else
                    LevelSessionServer.Instance?.ServerTryWriteScriptFile(path, content, out _);
                return;
            }

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdWriteScriptFile(path, content);
        }

        void RequestCreateFile(string path, string content)
        {
            if (NetworkServer.active)
            {
                if (cursorServer != null)
                    cursorServer.CreateScriptFile(path, content);
                else
                    LevelSessionServer.Instance?.ServerTryCreateScriptFile(path, content, out _);
                return;
            }

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdCreateScriptFile(path, content);
        }

        void RequestCreateFolder(string path)
        {
            if (NetworkServer.active)
            {
                if (cursorServer != null)
                    cursorServer.CreateScriptFolder(path);
                else
                    LevelSessionServer.Instance?.ServerTryCreateScriptFolder(path, out _);
                return;
            }

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdCreateScriptFolder(path);
        }

        void RequestDeleteNode(string path)
        {
            if (NetworkServer.active)
            {
                if (cursorServer != null)
                    cursorServer.DeleteScriptNode(path);
                else
                    LevelSessionServer.Instance?.ServerTryDeleteScriptNode(path, out _);
                return;
            }

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdDeleteScriptNode(path);
        }

        void RequestRenameNode(string path, string newName)
        {
            if (NetworkServer.active)
            {
                if (cursorServer != null)
                    cursorServer.RenameScriptNode(path, newName);
                else
                    LevelSessionServer.Instance?.ServerTryRenameScriptNode(path, newName, out _);
                return;
            }

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdRenameScriptNode(path, newName);
        }

        void RequestMoveNode(string from, string to)
        {
            if (NetworkServer.active)
            {
                if (cursorServer != null)
                    cursorServer.MoveScriptNode(from, to);
                else
                    LevelSessionServer.Instance?.ServerTryMoveScriptNode(from, to, out _);
                return;
            }

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdMoveScriptNode(from, to);
        }

        void ShowContextMenu(string path, Vector2 screenPosition)
        {
            if (contextMenu == null)
                return;

            bool hasTarget = !string.IsNullOrEmpty(path);
            bool isFolder = hasTarget &&
                            WorldScriptProjectService.TryGetNode(path, out ScriptNodeState node) &&
                            node.IsDirectory;
            bool isEntry = hasTarget &&
                           string.Equals(path, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase);

            string createFolder = isFolder
                ? path
                : (hasTarget ? WorldScriptProjectService.GetParentPath(path) : string.Empty);

            var items = new List<(string, Action)>
            {
                ("New Script (.ks)", () => BeginNamePrompt(NamePromptMode.NewFile, createFolder, null)),
                ("New Local Script (.lks)", () => BeginNamePrompt(NamePromptMode.NewLocalFile, createFolder, null)),
                ("New Folder", () => BeginNamePrompt(NamePromptMode.NewFolder, createFolder, null))
            };

            if (hasTarget)
                items.Add(("Copy (Ctrl+C)", () => CopyNode(path)));

            if (clipboard.HasValue)
                items.Add(("Paste (Ctrl+V)", () => PasteInto(createFolder)));

            if (hasTarget && !isEntry)
                items.Add(("Duplicate (Ctrl+D)", () =>
                {
                    CopyNode(path);
                    PasteDuplicateInto(createFolder);
                }));

            items.Add(("Reveal in File Explorer", () => WorldScriptProjectService.RevealInExplorer(path)));

            if (hasTarget && !isEntry)
            {
                items.Add(("Rename (F2)", () => BeginNamePrompt(NamePromptMode.Rename, createFolder, path)));
                items.Add(("Delete (Del)", () => BeginDeleteConfirm(CollectDeletableSelection())));
            }

            contextMenu.Show(screenPosition, items);
        }

        void CopyNode(string path)
        {
            if (!WorldScriptProjectService.TryGetNode(path, out ScriptNodeState node))
                return;

            string name = path;
            int slash = name.LastIndexOf('/');
            name = slash >= 0 ? name.Substring(slash + 1) : name;

            var entry = new ClipboardEntry
            {
                IsDirectory = node.IsDirectory,
                Name = name,
                Content = node.IsDirectory ? string.Empty : (node.Content ?? string.Empty),
                Tree = new List<(string, bool, string)>()
            };

            if (node.IsDirectory)
            {
                IReadOnlyList<ScriptNodeState> all = WorldScriptProjectService.GetSortedNodes();
                for (int i = 0; i < all.Count; i++)
                {
                    ScriptNodeState n = all[i];
                    if (string.Equals(n.RelativePath, path, StringComparison.OrdinalIgnoreCase) ||
                        n.RelativePath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = string.Equals(n.RelativePath, path, StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : n.RelativePath.Substring(path.Length + 1);
                        entry.Tree.Add((rel, n.IsDirectory, n.Content ?? string.Empty));
                    }
                }
            }

            clipboard = entry;
            SetStatus("Copied " + name);
        }

        void PasteInto(string folder)
        {
            if (!clipboard.HasValue)
                return;

            ClipboardEntry entry = clipboard.Value;
            string baseName = entry.Name;
            string destination = WorldScriptProjectService.CombinePath(folder, baseName);
            if (WorldScriptProjectService.TryGetNode(destination, out _))
                destination = UniqueCopyPath(folder, baseName, entry.IsDirectory);

            if (entry.IsDirectory)
            {
                RequestCreateFolder(destination);
                for (int i = 0; i < entry.Tree.Count; i++)
                {
                    (string relativePath, bool isDirectory, string content) child = entry.Tree[i];
                    if (string.IsNullOrEmpty(child.relativePath))
                        continue;
                    string childPath = WorldScriptProjectService.CombinePath(destination, child.relativePath);
                    if (child.isDirectory)
                        RequestCreateFolder(childPath);
                    else
                        RequestCreateFile(childPath, child.content);
                }
            }
            else
            {
                RequestCreateFile(destination, entry.Content);
            }
        }

        void BeginNamePrompt(NamePromptMode mode, string folder, string renamePath)
        {
            namePromptMode = mode;
            namePromptTargetFolder = folder ?? string.Empty;
            namePromptRenamePath = renamePath ?? string.Empty;

            if (namePromptRoot == null || namePromptInput == null)
                return;

            if (namePromptTitle != null)
            {
                namePromptTitle.text = mode switch
                {
                    NamePromptMode.NewFile => "New Script (.ks)",
                    NamePromptMode.NewLocalFile => "New Local Script (.lks)",
                    NamePromptMode.NewFolder => "New Folder",
                    NamePromptMode.Rename => "Rename",
                    _ => "Name"
                };
            }

            if (mode == NamePromptMode.Rename && !string.IsNullOrEmpty(renamePath))
            {
                string name = renamePath;
                int slash = name.LastIndexOf('/');
                namePromptInput.text = slash >= 0 ? name.Substring(slash + 1) : name;
            }
            else if (mode == NamePromptMode.NewFile)
            {
                namePromptInput.text = "script.ks";
            }
            else if (mode == NamePromptMode.NewLocalFile)
            {
                namePromptInput.text = "client.lks";
            }
            else
            {
                namePromptInput.text = "folder";
            }

            namePromptRoot.SetActive(true);
            namePromptRoot.transform.SetAsLastSibling();
            namePromptInput.Select();
            namePromptInput.ActivateInputField();
        }

        public void ConfirmNamePrompt()
        {
            if (namePromptMode == NamePromptMode.None || namePromptInput == null)
                return;

            NamePromptMode mode = namePromptMode;
            string folder = namePromptTargetFolder;
            string renamePath = namePromptRenamePath;
            string name = (namePromptInput.text ?? string.Empty).Trim();
            HideNamePrompt();
            if (string.IsNullOrEmpty(name))
                return;

            switch (mode)
            {
                case NamePromptMode.NewFile:
                {
                    if (!name.Contains("."))
                        name += WorldScriptProjectService.ServerExtension;
                    string path = WorldScriptProjectService.CombinePath(folder, name);
                    RequestCreateFile(path, string.Empty);
                    StartCoroutine(OpenFileNextFrame(path));
                    break;
                }
                case NamePromptMode.NewLocalFile:
                {
                    if (!name.Contains("."))
                        name += WorldScriptProjectService.LocalExtension;
                    string path = WorldScriptProjectService.CombinePath(folder, name);
                    RequestCreateFile(path, string.Empty);
                    StartCoroutine(OpenFileNextFrame(path));
                    break;
                }
                case NamePromptMode.NewFolder:
                {
                    string path = WorldScriptProjectService.CombinePath(folder, name);
                    RequestCreateFolder(path);
                    break;
                }
                case NamePromptMode.Rename:
                {
                    name = PreserveExtensionOnRename(renamePath, name);
                    RequestRenameNode(renamePath, name);
                    break;
                }
            }
        }

        static string PreserveExtensionOnRename(string oldRelativePath, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(oldRelativePath))
                return newName;

            if (!WorldScriptProjectService.TryGetNode(oldRelativePath, out ScriptNodeState node) || node.IsDirectory)
                return newName;

            string oldFileName = oldRelativePath;
            int slash = oldFileName.LastIndexOf('/');
            if (slash >= 0)
                oldFileName = oldFileName.Substring(slash + 1);

            string oldExt = GetExtension(oldFileName);
            string newExt = GetExtension(newName);
            if (string.IsNullOrEmpty(newExt) && !string.IsNullOrEmpty(oldExt))
                return newName + oldExt;

            return newName;
        }

        static string GetExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            int dot = fileName.LastIndexOf('.');
            // Require a real suffix: ".ks", not leading-dot-only names like ".gitignore" handled as name.
            if (dot <= 0 || dot >= fileName.Length - 1)
                return string.Empty;

            return fileName.Substring(dot);
        }

        public void CancelNamePrompt() => HideNamePrompt();

        void HideNamePrompt()
        {
            namePromptMode = NamePromptMode.None;
            if (namePromptRoot != null)
                namePromptRoot.SetActive(false);
        }

        IEnumerator OpenFileNextFrame(string path)
        {
            yield return null;
            yield return null;
            OpenFile(path);
        }

        void ToggleRunStop()
        {
            KaplaoCursorClient.EnsureClientReadyForCommands();
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null && !NetworkServer.active)
                return;

            bool isRunning = LevelSessionServer.Instance != null && LevelSessionServer.Instance.ScriptRunning;
            if (!isRunning && LevelScriptRuntime.Instance != null && LevelScriptRuntime.Instance.IsRunning)
                isRunning = true;

            if (isRunning)
            {
                if (client != null)
                    client.CmdStopLevelScript();
                else
                    LevelSessionServer.Instance?.ServerStopScript();
                return;
            }

            FlushAllDirtyScriptBuffers();

            string script = WorldScriptProjectService.GetEntryScriptContent();
            if (openFileBuffers.TryGetValue(WorldScriptProjectService.EntryFileName, out string buffered))
                script = buffered;

            LevelScriptIdeService.ActiveFilePath = WorldScriptProjectService.EntryFileName;
            if (!LevelScriptIdeService.TryValidate(script ?? string.Empty, out LevelScriptDiagnostic diagnostic))
            {
                SetStatus(diagnostic.Message);
                AppendOutput(diagnostic.Message, OutputLogType.Error);
                OpenFile(WorldScriptProjectService.EntryFileName);
                if (codeInputField != null &&
                    codeInputField.TryGetComponent(out LevelScriptCodeEditorEnhancer enhancer))
                {
                    enhancer.CurrentFilePath = WorldScriptProjectService.EntryFileName;
                    enhancer.ApplyExternalDiagnostic(diagnostic);
                }

                return;
            }

            if (NetworkServer.active)
            {
                if (cursorServer != null)
                {
                    cursorServer.WriteScriptFile(WorldScriptProjectService.EntryFileName, script);
                    cursorServer.RunLevelScript(script);
                }
                else if (LevelSessionServer.Instance != null)
                {
                    LevelSessionServer.Instance.ServerTryWriteScriptFile(
                        WorldScriptProjectService.EntryFileName, script, out _);
                    if (!LevelSessionServer.Instance.ServerTryStartScript(script, out string error) &&
                        !string.IsNullOrEmpty(error))
                    {
                        SetStatus(error);
                    }
                }

                return;
            }

            client.CmdWriteScriptFile(WorldScriptProjectService.EntryFileName, script);
            client.CmdRunLevelScript(script);
        }

        void OnEditorParameterHintChanged(string signature) => RefreshStatusLine(signature);

        void OnEditorDiagnosticChanged(LevelScriptDiagnostic diagnostic)
        {
            lastDiagnostic = diagnostic;
            RefreshStatusLine();
        }

        void RefreshStatusLine(string signatureHint = null)
        {
            if (overlayRoot == null || !overlayRoot.activeSelf)
                return;

            EnsureStatusLabelVisible();

            // Errors always win over signature hints so Line/column stays visible.
            if (lastDiagnostic.HasError && !string.IsNullOrEmpty(lastDiagnostic.Message))
            {
                SetStatus(lastDiagnostic.Message);
                return;
            }

            string signature = signatureHint;
            if (signature == null &&
                codeInputField != null &&
                LevelScriptIdeService.TryGetActiveSignatureHelp(
                    codeInputField.text ?? string.Empty,
                    codeInputField.caretPosition,
                    out string activeSignature))
            {
                signature = activeSignature;
            }

            if (!string.IsNullOrEmpty(signature))
            {
                SetStatus(signature);
                return;
            }

            string file = string.IsNullOrEmpty(openFilePath) ? WorldScriptProjectService.EntryFileName : openFilePath;
            SetStatus(file + " — Run starts main.ks");
        }

        void RefreshEditorWhenVisible()
        {
            if (codeInputField == null || overlayRoot == null || !overlayRoot.activeSelf)
                return;

            EnsureCodeEditorEnhancer();
            if (codeInputField.TryGetComponent(out LevelScriptCodeEditorEnhancer enhancer))
                enhancer.RefreshEditorState();
        }

        IEnumerator RefreshEditorNextFrame()
        {
            yield return null;
            RefreshEditorWhenVisible();
        }

        public void SetRunningState(bool running)
        {
            if (runButtonLabel != null)
                runButtonLabel.text = running ? "Stop" : "Run";
        }

        public void AppendOutput(string message, OutputLogType type)
        {
            if (outputPanel == null && overlayRoot != null)
                outputPanel = overlayRoot.GetComponentInChildren<KaplaoCodeOutputPanel>(true);

            if (outputPanel != null)
                outputPanel.AppendEntry(message, type);
            else
                LevelScriptOutputService.Log(message, type);
        }

        public void NotifyScriptWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            AppendOutput(message, OutputLogType.Warning);
        }

        public void OnOutputPanelToggled(bool collapsed, float height)
        {
            if (codeInputField == null) return;
            RectTransform codeRect = codeInputField.GetComponent<RectTransform>();
            float bottomOffset = 52f + height + 6f;
            codeRect.offsetMin = new Vector2(FileTreeWidth + 8f, bottomOffset);
        }

        public void NotifyScriptRuntimeError(string message)
        {
            SetRunningState(false);
            if (string.IsNullOrEmpty(message))
                return;

            SetStatus(message);
            AppendOutput(message, OutputLogType.Error);
            if (codeInputField == null ||
                !codeInputField.TryGetComponent(out LevelScriptCodeEditorEnhancer enhancer))
                return;

            string source = codeInputField.text ?? string.Empty;
            if (TryBuildDiagnosticFromMessage(source, message, out LevelScriptDiagnostic diagnostic))
                enhancer.ApplyExternalDiagnostic(diagnostic);
        }

        static bool TryBuildDiagnosticFromMessage(string source, string message, out LevelScriptDiagnostic diagnostic)
        {
            diagnostic = default;
            if (string.IsNullOrEmpty(message))
                return false;

            // "Line 3, column 5: Unknown function 'foo'."
            if (message.StartsWith("Line ", StringComparison.OrdinalIgnoreCase))
            {
                int colon = message.IndexOf(':');
                if (colon > 0)
                {
                    // Prefer live validate for accurate underline; still show message.
                }
            }

            int quoteStart = message.IndexOf('\'');
            int quoteEnd = quoteStart >= 0 ? message.IndexOf('\'', quoteStart + 1) : -1;
            if (quoteStart < 0 || quoteEnd <= quoteStart)
                return false;

            string name = message.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(source))
                return false;

            int start = IndexOfIdentifier(source, name);
            if (start < 0)
                start = 0;

            int end = start;
            while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_'))
                end++;

            IndexToLineColumn(source, start, out int line, out int column);
            diagnostic = new LevelScriptDiagnostic
            {
                HasError = true,
                Line = line,
                Column = column,
                StartIndex = start,
                EndIndex = Math.Max(end, start + 1),
                Message = message
            };
            return true;
        }

        static int IndexOfIdentifier(string source, string name)
        {
            for (int i = 0; i <= source.Length - name.Length; i++)
            {
                if (!string.Equals(source.Substring(i, name.Length), name, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool leftOk = i == 0 || !(char.IsLetterOrDigit(source[i - 1]) || source[i - 1] == '_');
                bool rightOk = i + name.Length >= source.Length ||
                               !(char.IsLetterOrDigit(source[i + name.Length]) || source[i + name.Length] == '_');
                if (leftOk && rightOk)
                    return i;
            }

            return -1;
        }

        static void IndexToLineColumn(string source, int index, out int line, out int column)
        {
            line = 1;
            column = 1;
            index = Math.Clamp(index, 0, source.Length);
            for (int i = 0; i < index; i++)
            {
                if (source[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
        }

        public void SetStatus(string message)
        {
            EnsureStatusLabelVisible();
            if (statusLabel == null)
                return;

            statusLabel.text = message ?? string.Empty;
            statusLabel.ForceMeshUpdate(true);
        }

        void OpenDocumentation()
        {
            if (string.IsNullOrWhiteSpace(documentationUrl))
            {
                SetStatus("Documentation URL is not set.");
                return;
            }

            Application.OpenURL(documentationUrl.Trim());
        }

        public GameObject OverlayRoot => overlayRoot;
    }
}
