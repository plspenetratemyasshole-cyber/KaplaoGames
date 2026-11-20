#if UNITY_EDITOR
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    /// <summary>
    /// Creates ONLY the pause player list (right panel + scroll + row template in Content).
    /// Edit PlayerRowTemplate in the scene to move/resize buttons.
    /// Menu: Kaplao / Setup Pause Player List
    /// </summary>
    public static class PausePlayerListSetup
    {
        public const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        public const string PanelName = "HostModerationPanel";
        public const string TemplateName = "PlayerRowTemplate";

        [MenuItem("Kaplao/Setup Pause Player List")]
        public static void SetupPausePlayerList()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform panel = EnsurePlayerListPanel(canvas.transform);
            Transform content = panel.Find("PlayersScroll/Viewport/Content");
            GameObject template = EnsurePlayerRowTemplate(content);

            KaplaoModerationEscUi escUi = canvas.GetComponent<KaplaoModerationEscUi>();
            if (escUi == null)
                escUi = canvas.gameObject.AddComponent<KaplaoModerationEscUi>();

            SerializedObject escSo = new SerializedObject(escUi);
            escSo.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            escSo.FindProperty("listContent").objectReferenceValue = content;
            escSo.FindProperty("playerRowTemplate").objectReferenceValue = template;
            escSo.FindProperty("moderateChatButton").objectReferenceValue =
                panel.Find("ModerateChatButton")?.GetComponent<Button>();
            escSo.FindProperty("moderateChatButtonText").objectReferenceValue =
                panel.Find("ModerateChatButton/Text")?.GetComponent<TextMeshProUGUI>();
            escSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(escUi);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "[Kaplao] Pause player list ready. Edit HostModerationPanel/.../Content/PlayerRowTemplate " +
                "to move buttons, then save the scene.");
        }

        public static Transform EnsurePlayerListPanel(Transform canvas)
        {
            Transform existing = canvas.Find(PanelName);
            if (existing == null)
            {
                Transform underPause = canvas.Find("PauseMenuRoot/" + PanelName);
                if (underPause != null)
                {
                    underPause.SetParent(canvas, true);
                    existing = underPause;
                }
            }

            GameObject panel;
            if (existing != null)
            {
                panel = existing.gameObject;
            }
            else
            {
                panel = CreateImage(PanelName, canvas, new Color(0f, 0f, 0f, 0.55f));
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(1f, 0.5f);
                panelRect.anchorMax = new Vector2(1f, 0.5f);
                panelRect.pivot = new Vector2(1f, 0.5f);
                panelRect.anchoredPosition = new Vector2(-12f, 0f);
                panelRect.sizeDelta = new Vector2(520f, 560f);
            }

            EnsureModerateChatButton(panel.transform);
            EnsureTitle(panel.transform);
            EnsurePlayersScroll(panel.transform);
            // Keep panel visible in editor for layout editing; runtime logic controls visibility.
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            return panel.transform;
        }

        static void EnsureModerateChatButton(Transform panel)
        {
            Transform existing = panel.Find("ModerateChatButton");
            if (existing != null)
                return;

            GameObject button = CreateMenuButton(
                panel,
                "ModerateChatButton",
                "Moderate chat: OFF",
                new Vector2(0.5f, 0.92f),
                new Vector2(260f, 40f));
            button.SetActive(true);
        }

        static void EnsureTitle(Transform panel)
        {
            Transform existing = panel.Find("Title");
            if (existing != null)
                return;

            GameObject title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(panel, false);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.82f);
            titleRect.anchorMax = new Vector2(0.95f, 0.90f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Players";
            titleText.fontSize = 22f;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
        }

        static void EnsurePlayersScroll(Transform panel)
        {
            Transform scrollTransform = panel.Find("PlayersScroll");
            GameObject scrollGo;
            if (scrollTransform != null)
            {
                scrollGo = scrollTransform.gameObject;
            }
            else
            {
                scrollGo = new GameObject(
                    "PlayersScroll",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Mask),
                    typeof(ScrollRect));
                scrollGo.transform.SetParent(panel, false);
            }

            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.04f, 0.04f);
            scrollRect.anchorMax = new Vector2(0.96f, 0.80f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            Transform viewportTransform = scrollGo.transform.Find("Viewport");
            GameObject viewport;
            if (viewportTransform != null)
            {
                viewport = viewportTransform.gameObject;
            }
            else
            {
                viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollGo.transform, false);
            }

            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            Transform contentTransform = viewport.transform.Find("Content");
            GameObject content;
            if (contentTransform != null)
            {
                content = contentTransform.gameObject;
            }
            else
            {
                content = new GameObject(
                    "Content",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
            }

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(0, 0, 0, 0);

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            EnsurePlayerRowTemplate(content.transform);
        }

        public static GameObject EnsurePlayerRowTemplate(Transform content)
        {
            Transform existing = content.Find(TemplateName);
            if (existing != null)
            {
                NormalizeRowTemplate(existing);
                // Keep visible in editor for direct HLG tuning; play mode hides it.
                existing.gameObject.SetActive(true);

                // Ensure editor preview shows non-zero sizes even before runtime rebuild.
                Canvas.ForceUpdateCanvases();
                if (existing.GetComponent<RectTransform>() != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(existing.GetComponent<RectTransform>());
                if (content.GetComponent<RectTransform>() != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetComponent<RectTransform>());

                return existing.gameObject;
            }

            GameObject row = new GameObject(TemplateName, typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            row.SetActive(true);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 36f);

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.minHeight = 36f;
            rowLayout.preferredHeight = 36f;
            rowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.padding = new RectOffset(4, 4, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            CreateNameLabel(row.transform, "PlayerName", flexibleWidth: 1f, minWidth: 80f);
            CreateRowButton(row.transform, "KaplaoButton", "Kaplao:OFF", preferredWidth: 96f);
            CreateRowButton(row.transform, "ModButton", "Mod:OFF", preferredWidth: 84f);
            CreateRowButton(row.transform, "KickButton", "Kick", preferredWidth: 64f);
            CreateRowButton(row.transform, "BanButton", "Ban", preferredWidth: 64f);
            CreateRowButton(row.transform, "BanIpButton", "Ban IP", preferredWidth: 72f);

            // Give some initial layout computation for editor preview.
            Canvas.ForceUpdateCanvases();
            if (row.GetComponent<RectTransform>() != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(row.GetComponent<RectTransform>());
            if (content.GetComponent<RectTransform>() != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetComponent<RectTransform>());

            return row;
        }

        static void NormalizeRowTemplate(Transform row)
        {
            if (row.GetComponent<LayoutElement>() == null)
                row.gameObject.AddComponent<LayoutElement>();

            HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
                return;

            // Manual anchoredPosition from old setup fights HorizontalLayoutGroup at runtime.
            ResetChildForLayoutGroup(row.Find("PlayerName"), flexibleWidth: 1f, minWidth: 80f);
            ResetChildForLayoutGroup(row.Find("KaplaoButton"), preferredWidth: 96f);
            ResetChildForLayoutGroup(row.Find("ModButton"), preferredWidth: 84f);
            ResetChildForLayoutGroup(row.Find("KickButton"), preferredWidth: 64f);
            ResetChildForLayoutGroup(row.Find("BanButton"), preferredWidth: 64f);
            ResetChildForLayoutGroup(row.Find("BanIpButton"), preferredWidth: 72f);
        }

        static void ResetChildForLayoutGroup(
            Transform child,
            float preferredWidth = -1f,
            float minWidth = -1f,
            float flexibleWidth = 0f)
        {
            if (child == null)
                return;

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                // If sizeDelta stays zero, Scene view often looks like the child is missing.
                // HorizontalLayoutGroup will still override positions/sizes at runtime.
                float width =
                    preferredWidth >= 0f ? preferredWidth :
                    minWidth >= 0f ? minWidth :
                    80f;
                rect.sizeDelta = new Vector2(width, 28f);
            }

            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le == null)
                le = child.gameObject.AddComponent<LayoutElement>();

            if (preferredWidth >= 0f)
            {
                le.preferredWidth = preferredWidth;
                le.minWidth = minWidth >= 0f ? minWidth : preferredWidth;
            }

            if (flexibleWidth > 0f)
                le.flexibleWidth = flexibleWidth;
        }

        static void CreateNameLabel(
            Transform parent,
            string name,
            float flexibleWidth = 0f,
            float minWidth = 80f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minWidth = minWidth;
            le.flexibleWidth = flexibleWidth;
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = "PlayerName";
            label.fontSize = 15f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        static Button CreateRowButton(
            Transform parent,
            string name,
            string label,
            float preferredWidth)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.minWidth = preferredWidth;
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            StretchFull(textGo.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 13f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;

            return go.GetComponent<Button>();
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject CreateMenuButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorCenter,
            Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorCenter;
            rect.anchorMax = anchorCenter;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            StretchFull(labelObject.GetComponent<RectTransform>());
            TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return buttonObject;
        }

        static void StretchFull(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
#endif
