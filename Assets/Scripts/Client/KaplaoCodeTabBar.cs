using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Simple editor tab strip for open script files.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoCodeTabBar : MonoBehaviour
    {
        [SerializeField] RectTransform contentRoot;
        [SerializeField] float tabWidth = 140f;
        [SerializeField] float tabHeight = 28f;

        readonly List<TabView> tabs = new List<TabView>();
        string activePath = string.Empty;

        public event Action<string> TabSelected;
        public event Action<string> TabClosed;

        public string ActivePath => activePath;

        void Awake() => EnsureContentRootLayout();

        void OnEnable() => EnsureContentRootLayout();

        public void SetContentRoot(RectTransform root)
        {
            contentRoot = root;
            EnsureContentRootLayout();
        }

        public void SetActive(string relativePath)
        {
            activePath = relativePath ?? string.Empty;
            RefreshVisuals();
        }

        public void OpenOrFocus(string relativePath, bool dirty)
        {
            relativePath ??= string.Empty;
            TabView existing = FindTab(relativePath);
            if (existing == null)
            {
                existing = CreateTab(relativePath);
                tabs.Add(existing);
            }

            existing.Dirty = dirty;
            activePath = relativePath;
            RebuildLayout();
            RefreshVisuals();
        }

        public void SetDirty(string relativePath, bool dirty)
        {
            TabView tab = FindTab(relativePath);
            if (tab == null)
                return;
            tab.Dirty = dirty;
            RefreshVisuals();
        }

        public void Close(string relativePath)
        {
            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(tabs[i].Path, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (tabs[i].Root != null)
                        Destroy(tabs[i].Root);
                    tabs.RemoveAt(i);
                }
            }

            if (string.Equals(activePath, relativePath, StringComparison.OrdinalIgnoreCase))
                activePath = tabs.Count > 0 ? tabs[tabs.Count - 1].Path : string.Empty;

            RebuildLayout();
            RefreshVisuals();
        }

        public void CloseAll()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Root != null)
                    Destroy(tabs[i].Root);
            }

            tabs.Clear();
            activePath = string.Empty;
        }

        public bool HasTab(string relativePath) => FindTab(relativePath) != null;

        TabView FindTab(string relativePath)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (string.Equals(tabs[i].Path, relativePath, StringComparison.OrdinalIgnoreCase))
                    return tabs[i];
            }

            return null;
        }

        TabView CreateTab(string path)
        {
            GameObject tabObject = new GameObject("Tab_" + path.Replace('/', '_'),
                typeof(RectTransform), typeof(Image), typeof(Button));
            tabObject.transform.SetParent(contentRoot, false);

            Image background = tabObject.GetComponent<Image>();
            background.color = new Color(0.16f, 0.17f, 0.21f, 1f);

            Button button = tabObject.GetComponent<Button>();
            button.targetGraphic = background;
            string captured = path;
            button.onClick.AddListener(() => TabSelected?.Invoke(captured));

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(tabObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-22f, 0f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 14f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            GameObject closeObject = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeObject.transform.SetParent(tabObject.transform, false);
            RectTransform closeRect = closeObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-4f, 0f);
            closeRect.sizeDelta = new Vector2(18f, 18f);
            Image closeImage = closeObject.GetComponent<Image>();
            closeImage.color = new Color(0.25f, 0.26f, 0.3f, 1f);
            Button closeButton = closeObject.GetComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(() => TabClosed?.Invoke(captured));

            GameObject closeLabelObject = new GameObject("X", typeof(RectTransform));
            closeLabelObject.transform.SetParent(closeObject.transform, false);
            RectTransform xRect = closeLabelObject.GetComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = Vector2.zero;
            xRect.offsetMax = Vector2.zero;
            TextMeshProUGUI xLabel = closeLabelObject.AddComponent<TextMeshProUGUI>();
            xLabel.text = "×";
            xLabel.fontSize = 16f;
            xLabel.alignment = TextAlignmentOptions.Center;
            xLabel.color = Color.white;
            xLabel.raycastTarget = false;

            return new TabView
            {
                Path = path,
                Root = tabObject,
                Label = label,
                Background = background
            };
        }

        void RebuildLayout()
        {
            if (contentRoot == null)
                return;

            for (int i = 0; i < tabs.Count; i++)
            {
                RectTransform rect = tabs[i].Root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(tabWidth, tabHeight);

                LayoutElement layoutElement = tabs[i].Root.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = tabs[i].Root.AddComponent<LayoutElement>();

                layoutElement.preferredWidth = tabWidth;
                layoutElement.preferredHeight = tabHeight;
                layoutElement.flexibleWidth = 0f;
                layoutElement.flexibleHeight = 0f;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        void EnsureContentRootLayout()
        {
            if (contentRoot == null)
                return;

            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(0f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.offsetMin = new Vector2(4f, 2f);
            contentRoot.offsetMax = new Vector2(-4f, -2f);

            HorizontalLayoutGroup layout = contentRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = contentRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = 4f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        void RefreshVisuals()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                TabView tab = tabs[i];
                bool active = string.Equals(tab.Path, activePath, StringComparison.OrdinalIgnoreCase);
                tab.Background.color = active
                    ? new Color(0.24f, 0.28f, 0.36f, 1f)
                    : new Color(0.14f, 0.15f, 0.18f, 1f);

                string name = tab.Path;
                int slash = name.LastIndexOf('/');
                name = slash >= 0 ? name.Substring(slash + 1) : name;
                tab.Label.text = tab.Dirty ? name + " *" : name;
            }
        }

        sealed class TabView
        {
            public string Path;
            public GameObject Root;
            public TextMeshProUGUI Label;
            public Image Background;
            public bool Dirty;
        }
    }
}
