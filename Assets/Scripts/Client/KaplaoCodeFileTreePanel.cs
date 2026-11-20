using System;
using System.Collections.Generic;
using KaplaoGames.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Left-side script project explorer with expand/collapse, selection, context menu, drag-drop.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoCodeFileTreePanel : MonoBehaviour
    {
        [SerializeField] RectTransform contentRoot;
        [SerializeField] Sprite fileIcon;
        [SerializeField] Sprite folderIcon;
        [SerializeField] Sprite folderToggleIcon;
        [SerializeField] float rowHeight = 24f;
        [SerializeField] float indentPerLevel = 14f;

        readonly Dictionary<string, bool> expandedFolders =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        readonly List<RowView> rows = new List<RowView>();
        readonly HashSet<string> selectedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> visiblePaths = new List<string>();

        string selectedPath = string.Empty;
        string selectionAnchorPath = string.Empty;
        string dragSourcePath;
        string dragDisplayName;
        bool dragIsDirectory;
        bool dragActive;
        GameObject dragGhost;
        RectTransform dragGhostRect;
        RectTransform dragParent;
        Canvas dragCanvas;
        Camera dragEventCamera;

        public event Action<string> FileActivated;
        public event Action<string, Vector2> ContextMenuRequested;
        public event Action<string, string> NodeMoved;
        public event Action SelectionChanged;

        public string SelectedPath => selectedPath;

        public IReadOnlyCollection<string> SelectedPaths => selectedPaths;

        public bool IsDragging => dragActive && !string.IsNullOrEmpty(dragSourcePath);

        public static bool IsEditableScriptPath(string relativePath)
        {
            string path = WorldScriptProjectService.NormalizeRelativePath(relativePath);
            return WorldScriptProjectService.IsServerScriptPath(path) ||
                   WorldScriptProjectService.IsLocalScriptPath(path);
        }

        public string SelectedFolderForCreate
        {
            get
            {
                if (string.IsNullOrEmpty(selectedPath))
                    return string.Empty;

                if (WorldScriptProjectService.TryGetNode(selectedPath, out ScriptNodeState node) && node.IsDirectory)
                    return selectedPath;

                return WorldScriptProjectService.GetParentPath(selectedPath);
            }
        }

        public void SetIcons(Sprite file, Sprite folder, Sprite folderToggle = null)
        {
            fileIcon = file;
            folderIcon = folder;
            if (folderToggle != null)
                folderToggleIcon = folderToggle;
        }

        public void SetContentRoot(RectTransform root) => contentRoot = root;

        public void SetDragParent(RectTransform parent) => dragParent = parent;

        public void SelectPath(string relativePath, bool notify = true)
        {
            selectedPath = WorldScriptProjectService.NormalizeRelativePath(relativePath);
            selectionAnchorPath = selectedPath;
            selectedPaths.Clear();
            if (!string.IsNullOrEmpty(selectedPath))
                selectedPaths.Add(selectedPath);
            Refresh();
            if (notify)
                SelectionChanged?.Invoke();
        }

        public void Refresh()
        {
            if (contentRoot == null)
                return;

            EnsureRootExpanded();
            ClearRows();
            visiblePaths.Clear();

            IReadOnlyList<ScriptNodeState> nodes = WorldScriptProjectService.GetSortedNodes();
            var children = new Dictionary<string, List<ScriptNodeState>>(StringComparer.OrdinalIgnoreCase);
            children[string.Empty] = new List<ScriptNodeState>();

            for (int i = 0; i < nodes.Count; i++)
            {
                ScriptNodeState node = nodes[i];
                string parent = WorldScriptProjectService.GetParentPath(node.RelativePath);
                if (!children.TryGetValue(parent, out List<ScriptNodeState> list))
                {
                    list = new List<ScriptNodeState>();
                    children[parent] = list;
                }

                list.Add(node);
            }

            foreach (KeyValuePair<string, List<ScriptNodeState>> pair in children)
            {
                pair.Value.Sort((a, b) =>
                {
                    int dir = b.IsDirectory.CompareTo(a.IsDirectory);
                    if (dir != 0)
                        return dir;
                    return string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
                });
            }

            EmitChildren(string.Empty, 0, children);

            // Drop selections that no longer exist.
            if (selectedPaths.Count > 0)
            {
                var stale = new List<string>();
                foreach (string path in selectedPaths)
                {
                    if (!WorldScriptProjectService.TryGetNode(path, out _))
                        stale.Add(path);
                }

                for (int i = 0; i < stale.Count; i++)
                    selectedPaths.Remove(stale[i]);

                if (!string.IsNullOrEmpty(selectedPath) && !selectedPaths.Contains(selectedPath))
                    selectedPath = selectedPaths.Count > 0 ? GetFirstSelected() : string.Empty;
            }

            float height = Mathf.Max(rows.Count * rowHeight, 40f);
            contentRoot.sizeDelta = new Vector2(contentRoot.sizeDelta.x, height);
        }

        string GetFirstSelected()
        {
            foreach (string path in selectedPaths)
                return path;
            return string.Empty;
        }

        void EnsureRootExpanded()
        {
            IReadOnlyList<ScriptNodeState> nodes = WorldScriptProjectService.GetSortedNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!nodes[i].IsDirectory)
                    continue;
                if (!expandedFolders.ContainsKey(nodes[i].RelativePath))
                    expandedFolders[nodes[i].RelativePath] = true;
            }
        }

        void EmitChildren(string parent, int depth, Dictionary<string, List<ScriptNodeState>> children)
        {
            if (!children.TryGetValue(parent, out List<ScriptNodeState> list))
                return;

            for (int i = 0; i < list.Count; i++)
            {
                ScriptNodeState node = list[i];
                visiblePaths.Add(node.RelativePath);
                CreateRow(node, depth);
                if (node.IsDirectory &&
                    expandedFolders.TryGetValue(node.RelativePath, out bool expanded) &&
                    expanded)
                {
                    EmitChildren(node.RelativePath, depth + 1, children);
                }
            }
        }

        void CreateRow(ScriptNodeState node, int depth)
        {
            GameObject rowObject = new GameObject("Row_" + node.RelativePath.Replace('/', '_'),
                typeof(RectTransform), typeof(Image), typeof(Button));
            rowObject.transform.SetParent(contentRoot, false);

            RectTransform rect = rowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -rows.Count * rowHeight);

            Image background = rowObject.GetComponent<Image>();
            bool selected = selectedPaths.Contains(node.RelativePath);
            bool primary = string.Equals(selectedPath, node.RelativePath, StringComparison.OrdinalIgnoreCase);
            background.color = primary
                ? new Color(0.22f, 0.36f, 0.55f, 0.95f)
                : selected
                    ? new Color(0.18f, 0.28f, 0.42f, 0.85f)
                    : new Color(0f, 0f, 0f, 0.01f);
            background.raycastTarget = true;

            Button button = rowObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;

            float left = 6f + depth * indentPerLevel;
            string displayName = GetFileName(node.RelativePath);

            if (node.IsDirectory)
            {
                bool expanded = expandedFolders.TryGetValue(node.RelativePath, out bool exp) && exp;
                GameObject twist = new GameObject("Twist", typeof(RectTransform), typeof(Image), typeof(Button));
                twist.transform.SetParent(rowObject.transform, false);
                RectTransform twistRect = twist.GetComponent<RectTransform>();
                twistRect.anchorMin = new Vector2(0f, 0.5f);
                twistRect.anchorMax = new Vector2(0f, 0.5f);
                twistRect.pivot = new Vector2(0.5f, 0.5f);
                twistRect.anchoredPosition = new Vector2(left + 8f, 0f);
                twistRect.sizeDelta = new Vector2(16f, 16f);

                Image twistImage = twist.GetComponent<Image>();
                twistImage.raycastTarget = true;
                if (folderToggleIcon != null)
                {
                    twistImage.sprite = folderToggleIcon;
                    twistImage.color = Color.white;
                    twistImage.preserveAspect = true;
                    twistRect.localRotation = Quaternion.Euler(0f, 0f, expanded ? -90f : 0f);
                }
                else
                {
                    twistImage.color = new Color(0f, 0f, 0f, 0.01f);
                    GameObject twistLabelObject = new GameObject("TwistLabel", typeof(RectTransform));
                    twistLabelObject.transform.SetParent(twist.transform, false);
                    TextMeshProUGUI twistLabel = twistLabelObject.AddComponent<TextMeshProUGUI>();
                    RectTransform twistLabelRect = twistLabel.rectTransform;
                    twistLabelRect.anchorMin = Vector2.zero;
                    twistLabelRect.anchorMax = Vector2.one;
                    twistLabelRect.offsetMin = Vector2.zero;
                    twistLabelRect.offsetMax = Vector2.zero;
                    twistLabel.text = expanded ? "▾" : "▸";
                    twistLabel.fontSize = 14f;
                    twistLabel.color = Color.white;
                    twistLabel.alignment = TextAlignmentOptions.Center;
                    twistLabel.raycastTarget = false;
                }

                Button twistButton = twist.GetComponent<Button>();
                twistButton.targetGraphic = twistImage;
                string folderPath = node.RelativePath;
                twistButton.onClick.AddListener(() => ToggleExpand(folderPath));
                left += 16f;
            }
            else
            {
                left += 16f;
            }

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(rowObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(left, 0f);
            iconRect.sizeDelta = new Vector2(16f, 16f);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = node.IsDirectory ? folderIcon : fileIcon;
            iconImage.color = iconImage.sprite != null ? Color.white : new Color(0.7f, 0.75f, 0.85f, 1f);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            left += 20f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(rowObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(left, 0f);
            labelRect.offsetMax = new Vector2(-4f, 0f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = displayName;
            label.fontSize = 15f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            string path = node.RelativePath;
            bool isDirectory = node.IsDirectory;
            button.onClick.AddListener(() => OnRowClicked(path, isDirectory));

            var handlers = rowObject.AddComponent<TreeRowPointerHandlers>();
            handlers.Bind(
                onContext: screenPos =>
                {
                    PrepareContextSelection(path);
                    Refresh();
                    SelectionChanged?.Invoke();
                    ContextMenuRequested?.Invoke(path, screenPos);
                },
                onBeginDrag: eventData => BeginDragRow(path, isDirectory, displayName, eventData),
                onDrag: eventData => UpdateDragGhost(eventData),
                onDrop: () => CompleteDropOnto(path, isDirectory),
                onEndDrag: eventData => FinishDrag(eventData));

            rows.Add(new RowView { Root = rowObject, Path = path });
        }

        void PrepareContextSelection(string path)
        {
            path = WorldScriptProjectService.NormalizeRelativePath(path);
            if (string.IsNullOrEmpty(path))
                return;

            // Like VS Code: RMB on already-selected keeps multi-select; otherwise select only that item.
            if (!selectedPaths.Contains(path))
            {
                selectedPaths.Clear();
                selectedPaths.Add(path);
                selectionAnchorPath = path;
            }

            selectedPath = path;
        }

        void OnRowClicked(string path, bool isDirectory)
        {
            path = WorldScriptProjectService.NormalizeRelativePath(path);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                        Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (shift && !string.IsNullOrEmpty(selectionAnchorPath))
            {
                SelectVisibleRange(selectionAnchorPath, path);
            }
            else if (ctrl)
            {
                if (selectedPaths.Contains(path))
                {
                    selectedPaths.Remove(path);
                    if (string.Equals(selectedPath, path, StringComparison.OrdinalIgnoreCase))
                        selectedPath = selectedPaths.Count > 0 ? GetFirstSelected() : string.Empty;
                }
                else
                {
                    selectedPaths.Add(path);
                    selectedPath = path;
                }

                selectionAnchorPath = path;
            }
            else
            {
                selectedPaths.Clear();
                selectedPaths.Add(path);
                selectedPath = path;
                selectionAnchorPath = path;
            }

            Refresh();
            SelectionChanged?.Invoke();

            // Only editable scripts open in the center editor.
            if (!isDirectory && IsEditableScriptPath(path) &&
                string.Equals(selectedPath, path, StringComparison.OrdinalIgnoreCase) &&
                selectedPaths.Contains(path))
            {
                FileActivated?.Invoke(path);
            }
        }

        void SelectVisibleRange(string fromPath, string toPath)
        {
            int fromIndex = IndexOfVisible(fromPath);
            int toIndex = IndexOfVisible(toPath);
            if (fromIndex < 0 || toIndex < 0)
            {
                selectedPaths.Clear();
                selectedPaths.Add(toPath);
                selectedPath = toPath;
                return;
            }

            if (fromIndex > toIndex)
            {
                int tmp = fromIndex;
                fromIndex = toIndex;
                toIndex = tmp;
            }

            selectedPaths.Clear();
            for (int i = fromIndex; i <= toIndex; i++)
                selectedPaths.Add(visiblePaths[i]);

            selectedPath = toPath;
        }

        int IndexOfVisible(string path)
        {
            for (int i = 0; i < visiblePaths.Count; i++)
            {
                if (string.Equals(visiblePaths[i], path, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        void BeginDragRow(string path, bool isDirectory, string displayName, PointerEventData eventData)
        {
            if (string.Equals(path, WorldScriptProjectService.EntryFileName, StringComparison.OrdinalIgnoreCase))
                return;

            dragSourcePath = path;
            dragDisplayName = displayName;
            dragIsDirectory = isDirectory;
            dragActive = true;
            CreateDragGhost(eventData);
        }

        void CreateDragGhost(PointerEventData eventData)
        {
            DestroyDragGhost();

            Transform parent = dragParent != null
                ? dragParent
                : (contentRoot != null ? contentRoot.GetComponentInParent<Canvas>()?.transform : null);
            if (parent == null)
                return;

            dragCanvas = parent.GetComponentInParent<Canvas>();
            if (dragCanvas == null)
                return;

            dragEventCamera = dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : dragCanvas.worldCamera;

            dragGhost = new GameObject("FileTreeDragGhost", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
            dragGhost.transform.SetParent(parent, false);
            dragGhost.transform.SetAsLastSibling();

            Canvas ghostCanvas = dragGhost.GetComponent<Canvas>();
            ghostCanvas.overrideSorting = true;
            ghostCanvas.sortingOrder = 5000;

            CanvasGroup group = dragGhost.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.75f;

            Image bg = dragGhost.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.28f, 0.42f, 0.9f);
            bg.raycastTarget = false;

            dragGhostRect = dragGhost.GetComponent<RectTransform>();
            dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
            dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhostRect.pivot = new Vector2(0f, 0.5f);
            dragGhostRect.sizeDelta = new Vector2(180f, rowHeight);

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(dragGhost.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(16f, 16f);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = dragIsDirectory ? folderIcon : fileIcon;
            iconImage.color = iconImage.sprite != null ? Color.white : new Color(0.7f, 0.75f, 0.85f, 1f);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(dragGhost.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(28f, 0f);
            labelRect.offsetMax = new Vector2(-6f, 0f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = dragDisplayName ?? string.Empty;
            label.fontSize = 14f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            UpdateDragGhost(eventData);
        }

        void UpdateDragGhost(PointerEventData eventData)
        {
            if (!dragActive || dragGhostRect == null)
                return;

            RectTransform parentRect = dragGhostRect.parent as RectTransform;
            if (parentRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, dragEventCamera, out Vector2 localPoint))
                return;

            dragGhostRect.anchoredPosition = localPoint + new Vector2(14f, -10f);
        }

        void FinishDrag(PointerEventData eventData)
        {
            // Drop on a row already cleared the source. Empty tree space → move to root.
            if (!string.IsNullOrEmpty(dragSourcePath) && IsPointerOverTreeArea(eventData))
                CompleteDropToRoot();

            DestroyDragGhost();
            dragSourcePath = null;
            dragActive = false;
        }

        bool IsPointerOverTreeArea(PointerEventData eventData)
        {
            RectTransform panelRect = transform as RectTransform;
            if (panelRect == null)
                return false;

            Camera cam = dragEventCamera;
            if (dragCanvas != null && dragCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = dragCanvas.worldCamera;
            else if (dragCanvas != null)
                cam = null;

            return RectTransformUtility.RectangleContainsScreenPoint(panelRect, eventData.position, cam);
        }

        void DestroyDragGhost()
        {
            if (dragGhost != null)
            {
                Destroy(dragGhost);
                dragGhost = null;
                dragGhostRect = null;
            }
        }

        public void CompleteDropToRoot()
        {
            if (string.IsNullOrEmpty(dragSourcePath))
                return;

            string sourceName = GetFileName(dragSourcePath);
            string destination = sourceName;
            string from = dragSourcePath;
            dragSourcePath = null;
            DestroyDragGhost();
            dragActive = false;

            if (!string.Equals(from, destination, StringComparison.OrdinalIgnoreCase))
                NodeMoved?.Invoke(from, destination);
        }

        void CompleteDropOnto(string path, bool isDirectory)
        {
            if (string.IsNullOrEmpty(dragSourcePath))
                return;

            string destinationFolder = isDirectory
                ? path
                : WorldScriptProjectService.GetParentPath(path);
            string sourceName = GetFileName(dragSourcePath);
            string destination = WorldScriptProjectService.CombinePath(destinationFolder, sourceName);
            string from = dragSourcePath;
            dragSourcePath = null;
            DestroyDragGhost();
            dragActive = false;

            if (!string.Equals(from, destination, StringComparison.OrdinalIgnoreCase))
                NodeMoved?.Invoke(from, destination);
        }

        static string GetFileName(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        void ToggleExpand(string folderPath)
        {
            bool current = expandedFolders.TryGetValue(folderPath, out bool expanded) && expanded;
            expandedFolders[folderPath] = !current;
            Refresh();
        }

        void ClearRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Root != null)
                    Destroy(rows[i].Root);
            }

            rows.Clear();
        }

        void OnDisable()
        {
            DestroyDragGhost();
            dragSourcePath = null;
            dragActive = false;
        }

        public void NotifyBackgroundContext(Vector2 screenPosition)
        {
            selectedPath = string.Empty;
            selectionAnchorPath = string.Empty;
            selectedPaths.Clear();
            Refresh();
            SelectionChanged?.Invoke();
            ContextMenuRequested?.Invoke(string.Empty, screenPosition);
        }

        sealed class RowView
        {
            public GameObject Root;
            public string Path;
        }
    }

    public sealed class TreeRowPointerHandlers : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IDropHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        Action<Vector2> onContext;
        Action<PointerEventData> onBeginDrag;
        Action<PointerEventData> onDrag;
        Action onDrop;
        Action<PointerEventData> onEndDrag;

        public void Bind(
            Action<Vector2> onContext,
            Action<PointerEventData> onBeginDrag,
            Action<PointerEventData> onDrag,
            Action onDrop,
            Action<PointerEventData> onEndDrag)
        {
            this.onContext = onContext;
            this.onBeginDrag = onBeginDrag;
            this.onDrag = onDrag;
            this.onDrop = onDrop;
            this.onEndDrag = onEndDrag;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                onContext?.Invoke(eventData.position);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            onBeginDrag?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnDrop(PointerEventData eventData) => onDrop?.Invoke();

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }

    public sealed class FileTreeBackgroundContext : MonoBehaviour, IPointerClickHandler, IDropHandler
    {
        public KaplaoCodeFileTreePanel Panel;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && Panel != null)
                Panel.NotifyBackgroundContext(eventData.position);
        }

        public void OnDrop(PointerEventData eventData)
        {
            Panel?.CompleteDropToRoot();
        }
    }
}
