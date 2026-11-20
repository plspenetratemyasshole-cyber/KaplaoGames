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
    /// <summary>
    /// Adds Spawn Point toggle and Created At label to Tile Settings panel; binds KaplaoCreateModeUi only.
    /// </summary>
    public static class TileSettingsExtensionsSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Tile Settings Extensions")]
        public static void SetupTileSettingsExtensions()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found.");
                return;
            }

            Transform createMode = canvas.transform.Find("CreateModeUI");
            if (createMode == null)
            {
                Debug.LogError("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
                return;
            }

            Transform settingsPanel = createMode.Find("SettingsPanel");
            if (settingsPanel == null)
            {
                Debug.LogError("[Kaplao] SettingsPanel not found under CreateModeUI.");
                return;
            }

            Toggle spawnPointToggle = EnsureToggle(settingsPanel, "SpawnPointToggle", "Spawn Point", new Vector2(160f, -28f));
            TextMeshProUGUI createdAtLabel = EnsureLabel(settingsPanel, "CreatedAtLabel", string.Empty,
                new Vector2(0f, 34f), new Vector2(320f, 24f), 14f);

            KaplaoCreateModeUi createUi = canvas.GetComponent<KaplaoCreateModeUi>();
            if (createUi == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoCreateModeUi not found — UI created but not bound.");
            }
            else
            {
                SerializedObject serialized = new SerializedObject(createUi);
                serialized.FindProperty("spawnPointToggle").objectReferenceValue = spawnPointToggle;
                serialized.FindProperty("createdAtLabel").objectReferenceValue = createdAtLabel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(createUi);
            }

            FixSettingsPanelInputFocus(settingsPanel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Tile Settings extensions setup complete.");
        }

        [MenuItem("Kaplao/Fix Settings Panel Input Focus")]
        public static void FixSettingsPanelInputFocusMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found.");
                return;
            }

            Transform createMode = canvas.transform.Find("CreateModeUI");
            if (createMode == null)
            {
                Debug.LogError("[Kaplao] CreateModeUI not found.");
                return;
            }

            Transform settingsPanel = createMode.Find("SettingsPanel");
            if (settingsPanel == null)
            {
                Debug.LogError("[Kaplao] SettingsPanel not found.");
                return;
            }

            FixSettingsPanelInputFocus(settingsPanel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Settings panel input focus fixed.");
        }

        static void FixSettingsPanelInputFocus(Transform settingsPanel)
        {
            VerticalLayoutGroup layoutGroup = settingsPanel.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null)
                Object.DestroyImmediate(layoutGroup);

            FixSpawnPointToggleLayout(settingsPanel);

            string[] rowNames =
            {
                "Transparency",
                "Rotation",
                "Position",
                "Scale",
                "Layer",
                "Order"
            };

            foreach (string rowName in rowNames)
            {
                Transform row = settingsPanel.Find(rowName);
                if (row == null)
                    continue;

                Toggle strayToggle = row.GetComponent<Toggle>();
                if (strayToggle != null)
                    Object.DestroyImmediate(strayToggle);

                Transform labelTransform = row.Find("Label");
                if (labelTransform != null)
                {
                    if (labelTransform.TryGetComponent(out TextMeshProUGUI label))
                        label.raycastTarget = false;
                    labelTransform.SetAsFirstSibling();
                }

                TMP_InputField[] inputs = row.GetComponentsInChildren<TMP_InputField>(true);
                for (int i = 0; i < inputs.Length; i++)
                {
                    TMP_InputField input = inputs[i];
                    RectTransform inputRect = input.GetComponent<RectTransform>();
                    inputRect.sizeDelta = new Vector2(100f, 28f);

                    if (inputs.Length == 1)
                        inputRect.anchoredPosition = new Vector2(110f, 0f);
                    else if (i == 0)
                        inputRect.anchoredPosition = new Vector2(0f, 0f);
                    else
                        inputRect.anchoredPosition = new Vector2(110f, 0f);

                    input.transform.SetAsLastSibling();
                }
            }
        }

        static void FixSpawnPointToggleLayout(Transform settingsPanel)
        {
            Transform spawnPoint = settingsPanel.Find("SpawnPointToggle");
            if (spawnPoint == null)
                return;

            RectTransform rect = spawnPoint.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(160f, -28f);
            rect.sizeDelta = new Vector2(320f, 28f);

            spawnPoint.SetSiblingIndex(Mathf.Min(1, settingsPanel.childCount - 1));
        }

        static Toggle EnsureToggle(Transform parent, string name, string labelText, Vector2 pos)
        {
            Transform existing = parent.Find(name);
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
                root.transform.SetParent(parent, false);

                GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(root.transform, false);
                RectTransform bgRect = bg.GetComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0f, 0.5f);
                bgRect.anchorMax = new Vector2(0f, 0.5f);
                bgRect.pivot = new Vector2(0f, 0.5f);
                bgRect.anchoredPosition = Vector2.zero;
                bgRect.sizeDelta = new Vector2(24f, 24f);
                bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

                GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
                check.transform.SetParent(bg.transform, false);
                Stretch(check.GetComponent<RectTransform>());

                Toggle toggle = root.GetComponent<Toggle>();
                toggle.targetGraphic = bg.GetComponent<Image>();
                toggle.graphic = check.GetComponent<Image>();

                GameObject label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(root.transform, false);
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0.5f);
                labelRect.anchorMax = new Vector2(1f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = new Vector2(32f, 0f);
                labelRect.sizeDelta = new Vector2(-32f, 28f);
                TextMeshProUGUI labelTmp = label.AddComponent<TextMeshProUGUI>();
                labelTmp.text = labelText;
                labelTmp.fontSize = 16f;
                labelTmp.color = Color.white;
                labelTmp.raycastTarget = false;
            }

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(320f, 28f);
            return root.GetComponent<Toggle>();
        }

        static TextMeshProUGUI EnsureLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null)
                go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
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
