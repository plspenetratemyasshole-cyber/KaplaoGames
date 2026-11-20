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
    public static class CantChangeModeUiSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Can't Change Mode UI")]
        public static void SetupCantChangeModeUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform panel = EnsurePanel(canvas.transform);
            CantChangeModeUi ui = canvas.GetComponent<CantChangeModeUi>();
            if (ui == null)
                ui = canvas.gameObject.AddComponent<CantChangeModeUi>();

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            so.FindProperty("okButton").objectReferenceValue =
                panel.Find("OkButton")?.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kaplao] Can't Change Mode UI setup complete in SampleScene. Save the scene.");
        }

        static Transform EnsurePanel(Transform canvas)
        {
            Transform existing = canvas.Find("CantChangeModePanel");
            if (existing != null)
                return existing;

            GameObject panel = new GameObject("CantChangeModePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(420f, 180f);

            panel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            GameObject messageGo = new GameObject("MessageText", typeof(RectTransform));
            messageGo.transform.SetParent(panel.transform, false);
            RectTransform messageRect = messageGo.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0.5f, 0.5f);
            messageRect.anchorMax = new Vector2(0.5f, 0.5f);
            messageRect.pivot = new Vector2(0.5f, 0.5f);
            messageRect.anchoredPosition = new Vector2(0f, 28f);
            messageRect.sizeDelta = new Vector2(380f, 48f);

            TextMeshProUGUI message = messageGo.AddComponent<TextMeshProUGUI>();
            message.text = "You can't change this";
            message.fontSize = 22f;
            message.color = Color.white;
            message.alignment = TextAlignmentOptions.Center;

            GameObject buttonGo = new GameObject("OkButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -42f);
            buttonRect.sizeDelta = new Vector2(140f, 40f);

            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            buttonGo.GetComponent<Button>().targetGraphic = buttonImage;

            GameObject labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Ok";
            label.fontSize = 20f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;

            panel.SetActive(false);
            return panel.transform;
        }
    }
}
#endif
