#if UNITY_EDITOR
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class MobileGameplayUiSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Mobile UI")]
        public static void Setup()
        {
            SetupMainMenu();
            SetupGameScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kaplao] Mobile UI setup complete (MainMenu + SampleScene).");
        }

        static void SetupMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in MainMenu.");
                return;
            }

            EnsureMobileControlsConfig(canvas);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetupGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            EnsureEventSystem();
            EnsureMobileControlsConfig(canvas);

            Transform root = EnsureRoot(canvas.transform, "MobileControlsRoot");
            root.gameObject.SetActive(false);

            Transform kaplaoRoot = EnsureRoot(root, "KaplaoMobileRoot");
            Transform runnerRoot = EnsureRoot(root, "RunnerMobileRoot");

            MobileJoystick joystick = EnsureKaplaoJoystick(kaplaoRoot);
            GameObject selectionPad = EnsureKaplaoSelectionMovePad(kaplaoRoot);
            EnsureKaplaoButtons(kaplaoRoot);
            MobileJoystick runnerJoystick = EnsureRunnerJoystick(runnerRoot);
            EnsureRunnerButtons(runnerRoot);
            GameObject pauseButton = EnsurePauseButton(root);

            Transform createToolSlots = kaplaoRoot.Find("CreateToolSlots");
            Transform usualToolSlots = kaplaoRoot.Find("UsualToolSlots");
            Transform rightButtons = kaplaoRoot.Find("RightButtons");

            MobileGameplayUi gameplayUi = canvas.GetComponent<MobileGameplayUi>();
            if (gameplayUi == null)
                gameplayUi = canvas.gameObject.AddComponent<MobileGameplayUi>();

            Bind(
                gameplayUi,
                root.gameObject,
                kaplaoRoot.gameObject,
                runnerRoot.gameObject,
                pauseButton,
                joystick,
                runnerJoystick,
                selectionPad,
                createToolSlots != null ? createToolSlots.gameObject : null,
                usualToolSlots != null ? usualToolSlots.gameObject : null,
                rightButtons != null ? rightButtons.gameObject : null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EnsureMobileControlsConfig(Canvas canvas)
        {
            if (canvas.GetComponent<MobileControlsConfig>() == null)
                canvas.gameObject.AddComponent<MobileControlsConfig>();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }

        static Transform EnsureRoot(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;

            var rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            StretchFull(rootObject.GetComponent<RectTransform>());
            return rootObject.transform;
        }

        static MobileJoystick EnsureKaplaoJoystick(Transform kaplaoRoot)
        {
            Transform area = kaplaoRoot.Find("MoveJoystick");
            if (area == null)
            {
                GameObject obj = CreateImage("MoveJoystick", kaplaoRoot, new Color(1f, 1f, 1f, 0.15f));
                RectTransform rect = obj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(130f, 130f);
                rect.sizeDelta = new Vector2(220f, 220f);
                area = obj.transform;
            }

            Transform handle = area.Find("Handle");
            if (handle == null)
            {
                GameObject handleObj = CreateImage("Handle", area, new Color(1f, 1f, 1f, 0.45f));
                RectTransform rect = handleObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(100f, 100f);
                handle = handleObj.transform;
            }

            MobileJoystick joystick = area.GetComponent<MobileJoystick>();
            if (joystick == null)
                joystick = area.gameObject.AddComponent<MobileJoystick>();

            SerializedObject so = new SerializedObject(joystick);
            so.FindProperty("area").objectReferenceValue = area.GetComponent<RectTransform>();
            so.FindProperty("handle").objectReferenceValue = handle.GetComponent<RectTransform>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return joystick;
        }

        static GameObject EnsureKaplaoSelectionMovePad(Transform kaplaoRoot)
        {
            Transform pad = kaplaoRoot.Find("SelectionMovePad");
            if (pad == null)
            {
                GameObject padObject = new GameObject("SelectionMovePad", typeof(RectTransform));
                padObject.transform.SetParent(kaplaoRoot, false);
                RectTransform padRect = padObject.GetComponent<RectTransform>();
                padRect.anchorMin = new Vector2(0f, 0f);
                padRect.anchorMax = new Vector2(0f, 0f);
                padRect.pivot = new Vector2(0.5f, 0.5f);
                padRect.anchoredPosition = new Vector2(130f, 130f);
                padRect.sizeDelta = new Vector2(220f, 220f);
                pad = padObject.transform;
            }

            EnsureTapButton(pad, "MoveUpButton", "^", new Vector2(0f, 72f), MobileTapButton.ActionType.SelectMoveUp);
            EnsureTapButton(pad, "MoveDownButton", "v", new Vector2(0f, -72f), MobileTapButton.ActionType.SelectMoveDown);
            EnsureTapButton(pad, "MoveLeftButton", "<", new Vector2(-72f, 0f), MobileTapButton.ActionType.SelectMoveLeft);
            EnsureTapButton(pad, "MoveRightButton", ">", new Vector2(72f, 0f), MobileTapButton.ActionType.SelectMoveRight);
            EnsureTapButton(pad, "CopyButton", "Copy", new Vector2(0f, 0f), MobileTapButton.ActionType.SelectDuplicate);

            pad.gameObject.SetActive(false);
            return pad.gameObject;
        }

        static void EnsureKaplaoButtons(Transform kaplaoRoot)
        {
            Transform legacySlots = kaplaoRoot.Find("ToolSlots");
            if (legacySlots != null)
                Object.DestroyImmediate(legacySlots.gameObject);

            EnsureToolSlotGroup(
                kaplaoRoot,
                "CreateToolSlots",
                new[] { "Sel", "Blk", "Spk", "Trg", "Del", "Prt", "Key", "Exe" },
                8,
                new Vector2(736f, 84f));
            EnsureToolSlotGroup(
                kaplaoRoot,
                "UsualToolSlots",
                new[] { "Grab", "Palm", "Gun" },
                3,
                new Vector2(300f, 84f));

            Transform right = EnsureRoot(kaplaoRoot, "RightButtons");
            RectTransform rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(1f, 0f);
            rightRect.anchorMax = new Vector2(1f, 0f);
            rightRect.pivot = new Vector2(1f, 0f);
            rightRect.anchoredPosition = new Vector2(-26f, 26f);
            rightRect.sizeDelta = new Vector2(220f, 300f);

            EnsureNamedButton(right, "ShiftButton", "Shift", new Vector2(0f, 170f), MobileHoldButton.ActionType.Shift);
            EnsureNamedButton(right, "CtrlButton", "Ctrl", new Vector2(0f, 80f), MobileHoldButton.ActionType.Ctrl);
        }

        static void EnsureToolSlotGroup(
            Transform kaplaoRoot,
            string groupName,
            string[] labels,
            int slotCount,
            Vector2 groupSize)
        {
            Transform slots = EnsureRoot(kaplaoRoot, groupName);
            RectTransform slotsRect = slots.GetComponent<RectTransform>();
            slotsRect.anchorMin = new Vector2(0.5f, 0f);
            slotsRect.anchorMax = new Vector2(0.5f, 0f);
            slotsRect.pivot = new Vector2(0.5f, 0f);
            slotsRect.anchoredPosition = new Vector2(0f, 24f);
            slotsRect.sizeDelta = groupSize;

            float spacing = 88f;
            float startX = -((slotCount - 1) * spacing) * 0.5f;

            for (int i = 0; i < slotCount; i++)
            {
                string name = $"Slot{i + 1}Button";
                Transform existing = slots.Find(name);
                if (existing == null)
                {
                    GameObject button = CreateButton(
                        name,
                        slots,
                        labels[i],
                        new Vector2(startX + i * spacing, 42f),
                        new Vector2(76f, 64f));
                    EnsureHoldButton(button, (MobileHoldButton.ActionType)((int)MobileHoldButton.ActionType.Tool1 + i));
                }
            }

            slots.gameObject.SetActive(groupName == "UsualToolSlots");
        }

        static MobileJoystick EnsureRunnerJoystick(Transform runnerRoot)
        {
            Transform area = runnerRoot.Find("MoveJoystick");
            if (area == null)
            {
                GameObject obj = CreateImage("MoveJoystick", runnerRoot, new Color(1f, 1f, 1f, 0.15f));
                RectTransform rect = obj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(130f, 130f);
                rect.sizeDelta = new Vector2(220f, 220f);
                area = obj.transform;
            }

            Transform handle = area.Find("Handle");
            if (handle == null)
            {
                GameObject handleObj = CreateImage("Handle", area, new Color(1f, 1f, 1f, 0.45f));
                RectTransform rect = handleObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(100f, 100f);
                handle = handleObj.transform;
            }

            MobileJoystick joystick = area.GetComponent<MobileJoystick>();
            if (joystick == null)
                joystick = area.gameObject.AddComponent<MobileJoystick>();

            SerializedObject so = new SerializedObject(joystick);
            so.FindProperty("area").objectReferenceValue = area.GetComponent<RectTransform>();
            so.FindProperty("handle").objectReferenceValue = handle.GetComponent<RectTransform>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform legacyButtons = runnerRoot.Find("MoveButtons");
            if (legacyButtons != null)
                legacyButtons.gameObject.SetActive(false);

            return joystick;
        }

        static void EnsureRunnerButtons(Transform runnerRoot)
        {
            Transform buttons = EnsureRoot(runnerRoot, "MoveButtons");
            RectTransform rect = buttons.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(360f, 88f);

            EnsureNamedButton(buttons, "MoveLeftButton", "Left", new Vector2(-90f, 44f), MobileHoldButton.ActionType.RunnerLeft);
            EnsureNamedButton(buttons, "MoveRightButton", "Right", new Vector2(90f, 44f), MobileHoldButton.ActionType.RunnerRight);
            EnsureRunnerSuicideButton(runnerRoot);

            // Prefer joystick when present.
            if (runnerRoot.Find("MoveJoystick") != null)
                buttons.gameObject.SetActive(false);
        }

        static void EnsureRunnerSuicideButton(Transform runnerRoot)
        {
            Transform existing = runnerRoot.Find("SuicideButton");
            GameObject button;
            if (existing == null)
            {
                button = CreateButton("SuicideButton", runnerRoot, "Die", new Vector2(210f, 90f), new Vector2(120f, 64f));
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-24f, 24f);
            }
            else
            {
                button = existing.gameObject;
            }

            MobileTapButton tap = button.GetComponent<MobileTapButton>();
            if (tap == null)
                tap = button.AddComponent<MobileTapButton>();

            SerializedObject serialized = new SerializedObject(tap);
            serialized.FindProperty("actionType").enumValueIndex = (int)MobileTapButton.ActionType.RunnerSuicide;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject EnsurePauseButton(Transform root)
        {
            Transform existing = root.Find("PauseButton");
            GameObject buttonObject;
            if (existing == null)
            {
                buttonObject = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(root, false);

                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-40f, -70f);
                rect.sizeDelta = new Vector2(160f, 64f);

                Image image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                image.raycastTarget = true;

                GameObject labelObject = new GameObject("Text", typeof(RectTransform));
                labelObject.transform.SetParent(buttonObject.transform, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                StretchFull(labelRect);

                TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
                label.text = "Pause";
                label.fontSize = 24f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
            }
            else
            {
                buttonObject = existing.gameObject;
            }

            MobilePauseButton pause = buttonObject.GetComponent<MobilePauseButton>();
            if (pause == null)
                pause = buttonObject.AddComponent<MobilePauseButton>();

            SerializedObject pauseSo = new SerializedObject(pause);
            pauseSo.FindProperty("findUiIfMissing").boolValue = true;
            pauseSo.FindProperty("pauseMenuUi").objectReferenceValue = Object.FindFirstObjectByType<PauseMenuUI>();
            pauseSo.FindProperty("runnerPauseMenuUi").objectReferenceValue = Object.FindFirstObjectByType<RunnerPauseMenuUI>();
            pauseSo.ApplyModifiedPropertiesWithoutUndo();

            buttonObject.transform.SetAsLastSibling();
            return buttonObject;
        }

        static void EnsureNamedButton(Transform parent, string name, string text, Vector2 position, MobileHoldButton.ActionType action)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return;

            GameObject button = CreateButton(name, parent, text, position, new Vector2(150f, 64f));
            EnsureHoldButton(button, action);
        }

        static void EnsureTapButton(Transform parent, string name, string text, Vector2 position, MobileTapButton.ActionType action)
        {
            Transform existing = parent.Find(name);
            GameObject button;
            if (existing == null)
            {
                button = CreateButton(name, parent, text, position, new Vector2(72f, 72f));
            }
            else
            {
                button = existing.gameObject;
            }

            MobileTapButton tap = button.GetComponent<MobileTapButton>();
            if (tap == null)
                tap = button.AddComponent<MobileTapButton>();

            SerializedObject so = new SerializedObject(tap);
            so.FindProperty("actionType").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateButton(string name, Transform parent, string text, Vector2 position, Vector2 size)
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
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            image.raycastTarget = true;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            StretchFull(labelRect);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return buttonObject;
        }

        static void EnsureHoldButton(GameObject button, MobileHoldButton.ActionType action)
        {
            MobileHoldButton hold = button.GetComponent<MobileHoldButton>();
            if (hold == null)
                hold = button.AddComponent<MobileHoldButton>();

            SerializedObject so = new SerializedObject(hold);
            so.FindProperty("actionType").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return imageObject;
        }

        static void Bind(
            MobileGameplayUi ui,
            GameObject root,
            GameObject kaplaoRoot,
            GameObject runnerRoot,
            GameObject pauseButton,
            MobileJoystick joystick,
            MobileJoystick runnerJoystick,
            GameObject selectionPad,
            GameObject createToolSlots,
            GameObject usualToolSlots,
            GameObject rightButtons)
        {
            SerializedObject serialized = new SerializedObject(ui);
            serialized.FindProperty("root").objectReferenceValue = root;
            serialized.FindProperty("kaplaoRoot").objectReferenceValue = kaplaoRoot;
            serialized.FindProperty("runnerRoot").objectReferenceValue = runnerRoot;
            serialized.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            serialized.FindProperty("kaplaoMoveJoystick").objectReferenceValue = joystick;
            serialized.FindProperty("runnerMoveJoystick").objectReferenceValue = runnerJoystick;
            serialized.FindProperty("kaplaoSelectionMovePad").objectReferenceValue = selectionPad;
            serialized.FindProperty("createToolSlots").objectReferenceValue = createToolSlots;
            serialized.FindProperty("usualToolSlots").objectReferenceValue = usualToolSlots;
            serialized.FindProperty("kaplaoRightButtons").objectReferenceValue = rightButtons;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ui);
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
