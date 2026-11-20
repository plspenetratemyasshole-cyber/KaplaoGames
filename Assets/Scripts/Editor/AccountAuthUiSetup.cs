#if UNITY_EDITOR
using KaplaoGames.Shared;
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
    /// Creates account/auth controls on SettingsPanel and wires MainMenuUI + KaplaoAccountConfig.
    /// Menu: Kaplao / Setup Account Auth UI
    /// </summary>
    public static class AccountAuthUiSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string PanelName = "SettingsPanel";

        [MenuItem("Kaplao/Setup Account Auth UI")]
        public static void SetupAccountAuthUi()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in MainMenu.");
                return;
            }

            Transform root = ResolveMenuRoot(canvas.transform);
            if (root == null)
            {
                Debug.LogError("[Kaplao] Menu root not found.");
                return;
            }

            Transform settings = root.Find(PanelName);
            if (settings == null)
            {
                Debug.LogError("[Kaplao] SettingsPanel not found. Run Kaplao/Setup Settings Panel first.");
                return;
            }

            EnsureLabel(settings, "NicknameLabel", "Nickname", new Vector2(-160f, -95f), 22f);
            TMP_InputField nicknameInput = EnsureTextInput(
                settings,
                "NicknameInput",
                "Nickname",
                new Vector2(40f, -95f),
                new Vector2(280f, 36f));

            TextMeshProUGUI emailLabel = EnsureLabel(
                settings,
                "AccountEmailLabel",
                "Signed in as …",
                new Vector2(0f, -140f),
                18f);
            emailLabel.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            emailLabel.gameObject.SetActive(false);

            Button signIn = EnsureButton(settings, "SignInButton", "Sign in", new Vector2(-120f, -190f), new Vector2(200f, 40f));
            Button signOut = EnsureButton(settings, "SignOutButton", "Sign out", new Vector2(120f, -190f), new Vector2(200f, 40f));
            signOut.gameObject.SetActive(false);

            // Keep Back reachable below account controls + skin row.
            Transform back = settings.Find("BackButton");
            if (back != null)
            {
                RectTransform backRect = back.GetComponent<RectTransform>();
                if (backRect != null)
                    backRect.anchoredPosition = new Vector2(0f, -250f);
            }

            MainMenuUI menuUi = canvas.GetComponent<MainMenuUI>();
            if (menuUi == null)
                menuUi = Object.FindFirstObjectByType<MainMenuUI>();

            if (menuUi == null)
            {
                Debug.LogWarning("[Kaplao] MainMenuUI not found — account UI created but not bound.");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            KaplaoAccountConfig config = menuUi.GetComponent<KaplaoAccountConfig>();
            if (config == null)
                config = Undo.AddComponent<KaplaoAccountConfig>(menuUi.gameObject);

            KaplaoAccountClient client = menuUi.GetComponent<KaplaoAccountClient>();
            if (client == null)
                client = Undo.AddComponent<KaplaoAccountClient>(menuUi.gameObject);

            SerializedObject serialized = new SerializedObject(menuUi);
            serialized.FindProperty("settingsNicknameInput").objectReferenceValue = nicknameInput;
            serialized.FindProperty("settingsAccountEmailLabel").objectReferenceValue = emailLabel;
            serialized.FindProperty("settingsSignInButton").objectReferenceValue = signIn;
            serialized.FindProperty("settingsSignOutButton").objectReferenceValue = signOut;
            serialized.FindProperty("accountConfig").objectReferenceValue = config;
            serialized.FindProperty("accountClient").objectReferenceValue = client;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(menuUi);
            EditorUtility.SetDirty(config);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                "[Kaplao] Account Auth UI ready. Set Website Url on KaplaoAccountConfig (default http://localhost:3000).");
        }

        static Transform ResolveMenuRoot(Transform canvas)
        {
            Transform mainPanel = canvas.Find("MainPanel");
            if (mainPanel != null && mainPanel.parent != null)
                return mainPanel.parent;

            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                if (child.Find("MainPanel") != null)
                    return child;
            }

            return canvas;
        }

        static TextMeshProUGUI EnsureLabel(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            float fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject labelObject;
            if (existing != null)
            {
                labelObject = existing.gameObject;
            }
            else
            {
                labelObject = new GameObject(name, typeof(RectTransform));
                labelObject.transform.SetParent(parent, false);
            }

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(420f, 36f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = labelObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static TMP_InputField EnsureTextInput(
            Transform parent,
            string name,
            string placeholderText,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_InputField existingInput = existing.GetComponent<TMP_InputField>();
                if (existingInput != null)
                {
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    if (existingRect != null)
                    {
                        existingRect.anchoredPosition = anchoredPosition;
                        existingRect.sizeDelta = size;
                    }

                    return existingInput;
                }
            }

            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0x2E / 255f, 0x2E / 255f, 0x2E / 255f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(root.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
            placeholder.text = placeholderText;
            placeholder.fontSize = 18f;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.raycastTarget = false;
            StretchFull(placeholder.rectTransform);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            StretchFull(text.rectTransform);

            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = 20;
            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = image;
            return input;
        }

        static Button EnsureButton(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                {
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    if (existingRect != null)
                    {
                        existingRect.anchoredPosition = anchoredPosition;
                        existingRect.sizeDelta = size;
                    }

                    return existingButton;
                }
            }

            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            StretchFull(labelObject.GetComponent<RectTransform>());
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            return buttonObject.GetComponent<Button>();
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
