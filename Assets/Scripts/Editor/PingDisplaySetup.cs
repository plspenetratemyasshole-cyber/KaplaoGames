#if UNITY_EDITOR
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Editor
{
    public static class PingDisplaySetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Ping Display")]
        public static void SetupPingDisplay()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            NetworkPingDisplay pingDisplay = canvas.GetComponent<NetworkPingDisplay>();
            if (pingDisplay == null)
                pingDisplay = canvas.gameObject.AddComponent<NetworkPingDisplay>();

            Transform root = canvas.transform.Find("NetworkPingDisplay");
            if (root == null)
            {
                var rootObject = new GameObject("NetworkPingDisplay", typeof(RectTransform));
                rootObject.transform.SetParent(canvas.transform, false);
                RectTransform rootRect = rootObject.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(1f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(1f, 1f);
                rootRect.anchoredPosition = new Vector2(-16f, -16f);
                rootRect.sizeDelta = new Vector2(160f, 32f);
                root = rootObject.transform;
            }

            Transform labelTransform = root.Find("PingLabel");
            GameObject labelObject;
            if (labelTransform == null)
            {
                labelObject = new GameObject("PingLabel", typeof(RectTransform));
                labelObject.transform.SetParent(root, false);
            }
            else
            {
                labelObject = labelTransform.gameObject;
            }

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI pingLabel = labelObject.GetComponent<TextMeshProUGUI>();
            if (pingLabel == null)
                pingLabel = labelObject.AddComponent<TextMeshProUGUI>();

            pingLabel.alignment = TextAlignmentOptions.TopRight;
            pingLabel.fontSize = 18f;
            pingLabel.color = Color.white;
            pingLabel.raycastTarget = false;
            pingLabel.textWrappingMode = TextWrappingModes.NoWrap;
            pingLabel.text = "Ping: -- ms";

            SerializedObject serializedPing = new SerializedObject(pingDisplay);
            serializedPing.FindProperty("pingLabel").objectReferenceValue = pingLabel;
            serializedPing.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Ping display setup complete (top-right, visible for Kaplao and Runner).");
        }
    }
}
#endif
