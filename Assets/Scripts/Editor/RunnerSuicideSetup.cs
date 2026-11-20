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
    public static class RunnerSuicideSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Runner Suicide Button")]
        public static void SetupRunnerSuicideButton()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform mobileRoot = canvas.transform.Find("MobileControlsRoot");
            if (mobileRoot == null)
            {
                Debug.LogWarning("[Kaplao] MobileControlsRoot not found. Run Kaplao/Setup Mobile UI first.");
                return;
            }

            Transform runnerRoot = mobileRoot.Find("RunnerMobileRoot");
            if (runnerRoot == null)
            {
                Debug.LogWarning("[Kaplao] RunnerMobileRoot not found. Run Kaplao/Setup Mobile UI first.");
                return;
            }

            EnsureRunnerSuicideButton(runnerRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Runner Suicide button setup complete (Die under RunnerMobileRoot).");
        }

        static void EnsureRunnerSuicideButton(Transform runnerRoot)
        {
            Transform existing = runnerRoot.Find("SuicideButton");
            GameObject button;
            if (existing == null)
            {
                button = CreateButton("SuicideButton", runnerRoot, "Die", new Vector2(120f, 64f));
            }
            else
            {
                button = existing.gameObject;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(120f, 64f);

            MobileTapButton tap = button.GetComponent<MobileTapButton>();
            if (tap == null)
                tap = button.AddComponent<MobileTapButton>();

            SerializedObject serialized = new SerializedObject(tap);
            serialized.FindProperty("actionType").enumValueIndex = (int)MobileTapButton.ActionType.RunnerSuicide;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateButton(string name, Transform parent, string text, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            image.raycastTarget = true;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return buttonObject;
        }
    }
}
#endif
