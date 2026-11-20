#if UNITY_EDITOR
using KaplaoGames.Client;
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class RunnerCheatsUiSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Runner Cheats UI")]
        public static void Setup()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            if (canvas.GetComponent<MobileControlsConfig>() == null)
                canvas.gameObject.AddComponent<MobileControlsConfig>();

            Transform panel = EnsurePanel(canvas.transform);
            EnsureOptionalFields(panel);

            RunnerCheatsUi ui = canvas.GetComponent<RunnerCheatsUi>();
            if (ui == null)
                ui = canvas.gameObject.AddComponent<RunnerCheatsUi>();

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            so.FindProperty("kaplaoFlyButton").objectReferenceValue =
                panel.Find("KaplaoFlyButton")?.GetComponent<Button>();
            so.FindProperty("kaplaoFlyButtonText").objectReferenceValue =
                panel.Find("KaplaoFlyButton/Text")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("flySpeedField").objectReferenceValue =
                panel.Find("FlySpeedField")?.GetComponent<TMP_InputField>();
            so.FindProperty("immortalityButton").objectReferenceValue =
                panel.Find("ImmortalityButton")?.GetComponent<Button>();
            so.FindProperty("immortalityButtonText").objectReferenceValue =
                panel.Find("ImmortalityButton/Text")?.GetComponent<TextMeshProUGUI>();
            so.FindProperty("spikePushField").objectReferenceValue =
                panel.Find("SpikePushField")?.GetComponent<TMP_InputField>();
            so.FindProperty("spikePulseSizeField").objectReferenceValue =
                panel.Find("SpikePulseSizeField")?.GetComponent<TMP_InputField>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Runner Cheats UI ready. Enable Cheats on Mobile Controls Config.");
        }

        static Transform EnsurePanel(Transform canvas)
        {
            Transform existing = canvas.Find("RunnerCheatsPanel");
            if (existing != null)
                return existing;

            GameObject panel = new GameObject(
                "RunnerCheatsPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            panel.transform.SetParent(canvas, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(220f, 220f);

            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateToggleButton(panel.transform, "KaplaoFlyButton", "Kaplao fly: OFF");
            CreateDecimalField(
                panel.transform,
                "FlySpeedField",
                "Fly speed",
                RunnerCheatsState.DefaultFlySpeed.ToString("0.##"));
            CreateToggleButton(panel.transform, "ImmortalityButton", "Immortality: OFF");
            CreateDecimalField(
                panel.transform,
                "SpikePushField",
                "Spike push",
                RunnerCheatsState.DefaultSpikePushForce.ToString("0.##"));
            CreateDecimalField(
                panel.transform,
                "SpikePulseSizeField",
                "Pulse size",
                RunnerCheatsState.DefaultSpikePulseSize.ToString("0.##"));

            panel.SetActive(false);
            return panel.transform;
        }

        static void EnsureOptionalFields(Transform panel)
        {
            EnsureFieldAfter(
                panel,
                "SpikePushField",
                "Spike push",
                RunnerCheatsState.DefaultSpikePushForce.ToString("0.##"),
                afterName: "ImmortalityButton");

            EnsureFieldAfter(
                panel,
                "SpikePulseSizeField",
                "Pulse size",
                RunnerCheatsState.DefaultSpikePulseSize.ToString("0.##"),
                afterName: "SpikePushField");
        }

        static void EnsureFieldAfter(
            Transform panel,
            string fieldName,
            string placeholder,
            string defaultValue,
            string afterName)
        {
            if (panel.Find(fieldName) != null)
                return;

            CreateDecimalField(panel, fieldName, placeholder, defaultValue);

            Transform field = panel.Find(fieldName);
            Transform after = panel.Find(afterName);
            if (field != null && after != null)
                field.SetSiblingIndex(after.GetSiblingIndex() + 1);
        }

        static void CreateToggleButton(Transform parent, string name, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 36f;
            le.minHeight = 36f;

            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            RectTransform tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(4f, 0f);
            tr.offsetMax = new Vector2(-4f, 0f);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 15f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }

        static void CreateDecimalField(Transform parent, string name, string placeholderText, string defaultValue)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(TMP_InputField),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 36f;
            le.minHeight = 36f;

            go.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            RectTransform areaRect = textArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(8f, 4f);
            areaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textArea.transform, false);
            Stretch(placeholderGo.GetComponent<RectTransform>());
            TextMeshProUGUI placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = placeholderText;
            placeholder.fontSize = 14f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textArea.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = defaultValue;
            text.fontSize = 15f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.text = defaultValue;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
