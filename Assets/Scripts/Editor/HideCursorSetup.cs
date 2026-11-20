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
    public static class HideCursorSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Hide Cursor")]
        public static void SetupHideCursor()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform createModeRoot = canvas.transform.Find("CreateModeUI");
            if (createModeRoot == null)
            {
                Debug.LogWarning("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
                return;
            }

            Transform panel = createModeRoot.Find("CreateSettingsPanel");
            if (panel == null)
            {
                Debug.LogWarning("[Kaplao] CreateSettingsPanel not found. Run Kaplao/Setup Create Mode UI first.");
                return;
            }

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null && panelRect.sizeDelta.y < 480f)
                panelRect.sizeDelta = new Vector2(Mathf.Max(360f, panelRect.sizeDelta.x), 480f);

            Toggle hideCursorToggle = FindOrCreateSettingsToggle(panel, "HideCursorToggle", "Hide cursor", new Vector2(0f, -80f));
            SetAnchored(hideCursorToggle.transform, new Vector2(0f, -80f), new Vector2(320f, 28f));

            // Keep zoom / save buttons from overlapping the new toggle when panel already existed.
            RepositionIfExists(panel, "MinZoomLabel", new Vector2(0f, -116f), new Vector2(280f, 22f));
            RepositionIfExists(panel, "MinZoomInputField", new Vector2(0f, -144f), new Vector2(280f, 34f));
            RepositionIfExists(panel, "MaxZoomLabel", new Vector2(0f, -180f), new Vector2(280f, 22f));
            RepositionIfExists(panel, "MaxZoomInputField", new Vector2(0f, -208f), new Vector2(280f, 34f));
            RepositionIfExists(panel, "CreateSettingsSaveButton", new Vector2(-70f, -250f), new Vector2(120f, 36f));
            RepositionIfExists(panel, "CreateSettingsCloseButton", new Vector2(70f, -250f), new Vector2(120f, 36f));

            KaplaoCreateModeUi createModeUi = canvas.GetComponent<KaplaoCreateModeUi>();
            if (createModeUi == null)
                createModeUi = canvas.gameObject.AddComponent<KaplaoCreateModeUi>();

            SerializedObject serializedUi = new SerializedObject(createModeUi);
            SerializedProperty hideProp = serializedUi.FindProperty("hideCursorToggle");
            if (hideProp != null)
                hideProp.objectReferenceValue = hideCursorToggle;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Hide Cursor checkbox setup complete (Create Settings).");
        }

        static Toggle FindOrCreateSettingsToggle(Transform parent, string name, string label, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Toggle existingToggle = existing.GetComponent<Toggle>();
                if (existingToggle != null)
                    return existingToggle;
            }

            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = position;
            rowRect.sizeDelta = new Vector2(320f, 28f);

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(row.transform, false);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(24f, 24f);
            Image bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(4f, 4f);
            checkRect.offsetMax = new Vector2(-4f, -4f);
            Image checkImage = checkmark.GetComponent<Image>();
            checkImage.color = new Color(0.35f, 0.85f, 0.45f, 1f);

            Toggle toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(row.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(32f, 0f);
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 18f;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = Color.white;

            return toggle;
        }

        static void SetAnchored(Transform target, Vector2 anchoredPosition, Vector2 size)
        {
            if (target == null)
                return;

            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        static void RepositionIfExists(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                SetAnchored(existing, anchoredPosition, size);
        }
    }
}
#endif
