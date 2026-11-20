#if UNITY_EDITOR
using KaplaoGames.Client;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class SetGroupPanelSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string PanelName = "SetGroupPanel";

        [MenuItem("Kaplao/Setup Set Group Panel")]
        public static void SetupFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Kaplao] Exit Play Mode before running Kaplao/Setup Set Group Panel.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform createModeRoot = ResolveCreateModeRoot(canvas.transform);
            if (createModeRoot == null)
            {
                Debug.LogError("[Kaplao] CreateModeUI root not found.");
                return;
            }

            KaplaoCreateModeUi createUi = canvas.GetComponent<KaplaoCreateModeUi>();
            if (createUi == null)
                createUi = Object.FindFirstObjectByType<KaplaoCreateModeUi>();
            if (createUi == null)
            {
                Debug.LogError("[Kaplao] KaplaoCreateModeUi not found. Run Kaplao/Setup Create Mode UI first.");
                return;
            }

            GameObject panel = BuildOrReplacePanel(createModeRoot);
            Bind(createUi, panel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Kaplao] SetGroupPanel created/updated (GD-style). Bound to KaplaoCreateModeUi.");
        }

        static Transform ResolveCreateModeRoot(Transform canvas)
        {
            Transform named = canvas.Find("CreateModeUI");
            if (named != null)
                return named;

            KaplaoCreateModeUi ui = Object.FindFirstObjectByType<KaplaoCreateModeUi>();
            if (ui != null && ui.KaplaoOnlyRoot != null)
                return ui.KaplaoOnlyRoot.transform;

            return canvas;
        }

        static GameObject BuildOrReplacePanel(Transform parent)
        {
            Transform existing = parent.Find(PanelName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(420f, 360f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            panel.SetActive(false);

            CreateLabel(panel.transform, "Title", "Edit Group", new Vector2(0f, 150f), new Vector2(360f, 32f), 22f);

            CreateButton(panel.transform, "NextFreeGroupButton", "Next Free", new Vector2(-120f, 100f), new Vector2(120f, 36f));
            CreateButton(panel.transform, "PrevGroupButton", "<", new Vector2(-20f, 100f), new Vector2(40f, 36f));
            CreateInputField(panel.transform, "GroupInputField", new Vector2(50f, 100f), new Vector2(80f, 36f));
            CreateButton(panel.transform, "NextGroupButton", ">", new Vector2(120f, 100f), new Vector2(40f, 36f));
            CreateButton(panel.transform, "AddGroupButton", "Add", new Vector2(0f, 52f), new Vector2(120f, 36f));
            CreateButton(panel.transform, "SetGroupCloseButton", "Close", new Vector2(0f, -150f), new Vector2(120f, 36f));

            GameObject grid = new GameObject("GroupsGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            grid.transform.SetParent(panel.transform, false);
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, -40f);
            gridRect.sizeDelta = new Vector2(360f, 140f);

            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(56f, 36f);
            layout.spacing = new Vector2(8f, 8f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;

            ContentSizeFitter fitter = grid.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(panel.transform, "GroupsHintLabel", "Tap group to remove", new Vector2(0f, 10f), new Vector2(360f, 24f), 14f);

            return panel;
        }

        static void Bind(KaplaoCreateModeUi ui, GameObject panel)
        {
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("setGroupPanelRoot").objectReferenceValue = panel;
            so.FindProperty("groupInputField").objectReferenceValue =
                panel.transform.Find("GroupInputField")?.GetComponent<TMP_InputField>();
            so.FindProperty("nextFreeGroupButton").objectReferenceValue =
                panel.transform.Find("NextFreeGroupButton")?.GetComponent<Button>();
            so.FindProperty("prevGroupButton").objectReferenceValue =
                panel.transform.Find("PrevGroupButton")?.GetComponent<Button>();
            so.FindProperty("nextGroupButton").objectReferenceValue =
                panel.transform.Find("NextGroupButton")?.GetComponent<Button>();
            so.FindProperty("addGroupButton").objectReferenceValue =
                panel.transform.Find("AddGroupButton")?.GetComponent<Button>();
            so.FindProperty("setGroupCloseButton").objectReferenceValue =
                panel.transform.Find("SetGroupCloseButton")?.GetComponent<Button>();
            so.FindProperty("groupsGridRoot").objectReferenceValue =
                panel.transform.Find("GroupsGrid") as RectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ui);
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return button;
        }

        static TMP_InputField CreateInputField(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(root.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(6f, 4f);
            textAreaRect.offsetMax = new Vector2(-6f, -4f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            TMP_InputField input = root.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.targetGraphic = image;
            input.text = "1";
            return input;
        }
    }
}
#endif
