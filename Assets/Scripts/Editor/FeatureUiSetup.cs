#if UNITY_EDITOR
using KaplaoGames.Client;
using KaplaoGames.LevelScript;
using KaplaoGames.Server;
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
    /// Editor setup for Selection Settings, Import SFX, Level Settings, Keyframes, Public Levels.
    /// </summary>
    public static class FeatureUiSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Kaplao/Setup Selection Settings")]
        public static void SetupSelectionSettings() => RunGameScene(SetupSelectionSettingsInScene);

        [MenuItem("Kaplao/Setup Import SFX")]
        public static void SetupImportSfx() => RunGameScene(SetupImportSfxInScene);

        [MenuItem("Kaplao/Setup Level Settings")]
        public static void SetupLevelSettings() => RunGameScene(SetupLevelSettingsInScene);

        [MenuItem("Kaplao/Setup Keyframes UI")]
        public static void SetupKeyframesUi() => RunGameScene(SetupKeyframesInScene);

        [MenuItem("Kaplao/Setup Executor UI")]
        public static void SetupExecutorUi() => RunGameScene(SetupExecutorInScene);

        [MenuItem("Kaplao/Setup Public Levels UI")]
        public static void SetupPublicLevelsUi()
        {
            SetupPublishInGameScene();
            SetupBrowseInMainMenu();
        }

        static void RunGameScene(System.Action<Canvas, Transform> action)
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

            action(canvas, createMode);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetupSelectionSettingsInScene(Canvas canvas, Transform createMode)
        {
            KaplaoSelectionSettingsUi ui = canvas.GetComponent<KaplaoSelectionSettingsUi>() ??
                                           canvas.gameObject.AddComponent<KaplaoSelectionSettingsUi>();

            Button openButton = UiHelpers.FindOrCreateTopRightButton(createMode, "SelectionSettingsButton", "Selection Settings", -408f);
            Button resetButton = UiHelpers.FindOrCreateTopRightButton(createMode, "SelectionResetButton", "Reset Selection", -448f);
            resetButton.gameObject.SetActive(false);

            GameObject panel = UiHelpers.FindOrCreatePanel(createMode, "SelectionSettingsPanel", new Vector2(520f, 420f));
            TMP_Dropdown criterion = EditorUiDropdownUtility.EnsureDropdown(
                panel.transform, "CriterionDropdown", new Vector2(0f, 150f), new Vector2(220f, 36f),
                new[] { "None", "Type", "Color", "Group", "Date", "Alpha", "Rotation", "Position", "Scale", "Order" });

            GameObject typePanel = UiHelpers.FindOrCreateChild(panel.transform, "TypePanel");
            Toggle block = UiHelpers.FindOrCreateToggle(typePanel.transform, "BlockToggle", "Block", new Vector2(-160f, 0f));
            Toggle spike = UiHelpers.FindOrCreateToggle(typePanel.transform, "SpikeToggle", "Spike", new Vector2(-80f, 0f));
            Toggle trigger = UiHelpers.FindOrCreateToggle(typePanel.transform, "TriggerToggle", "Trigger", new Vector2(0f, 0f));
            Toggle particle = UiHelpers.FindOrCreateToggle(typePanel.transform, "ParticleToggle", "Particle", new Vector2(80f, 0f));
            Toggle keyframe = UiHelpers.FindOrCreateToggle(typePanel.transform, "KeyframeToggle", "Keyframe", new Vector2(160f, 0f));

            GameObject colorPanel = UiHelpers.FindOrCreateChild(panel.transform, "ColorPanel");
            TMP_InputField colorHex = UiHelpers.FindOrCreateInput(colorPanel.transform, "ColorHexInput", new Vector2(-60f, 40f));
            Button colorAdd = UiHelpers.FindOrCreateButton(colorPanel.transform, "ColorAddButton", "Add", new Vector2(120f, 40f));
            RectTransform colorGrid = UiHelpers.FindOrCreateGrid(colorPanel.transform, "ColorGrid");

            GameObject groupPanel = UiHelpers.FindOrCreateChild(panel.transform, "GroupPanel");
            TMP_InputField groupInput = UiHelpers.FindOrCreateInput(groupPanel.transform, "GroupInput", new Vector2(-60f, 40f));
            Button groupAdd = UiHelpers.FindOrCreateButton(groupPanel.transform, "GroupAddButton", "Add", new Vector2(120f, 40f));
            RectTransform groupGrid = UiHelpers.FindOrCreateGrid(groupPanel.transform, "GroupGrid");

            GameObject comparePanel = UiHelpers.FindOrCreateChild(panel.transform, "ComparePanel");
            TMP_Dropdown compare = EditorUiDropdownUtility.EnsureDropdown(
                comparePanel.transform, "CompareDropdown", new Vector2(0f, 60f), new Vector2(220f, 36f),
                new[] { "<", ">", "<=", ">=", "=", "!=" });

            TMP_InputField value = UiHelpers.FindOrCreateInput(comparePanel.transform, "ValueInput", new Vector2(-80f, 0f));
            TMP_InputField valueX = UiHelpers.FindOrCreateInput(comparePanel.transform, "ValueXInput", new Vector2(-80f, -40f));
            TMP_InputField valueY = UiHelpers.FindOrCreateInput(comparePanel.transform, "ValueYInput", new Vector2(80f, -40f));
            TMP_InputField h = UiHelpers.FindOrCreateInput(comparePanel.transform, "DateHoursInput", new Vector2(-160f, -40f));
            TMP_InputField m = UiHelpers.FindOrCreateInput(comparePanel.transform, "DateMinutesInput", new Vector2(-80f, -40f));
            TMP_InputField d = UiHelpers.FindOrCreateInput(comparePanel.transform, "DateDaysInput", new Vector2(0f, -40f));
            TMP_InputField mo = UiHelpers.FindOrCreateInput(comparePanel.transform, "DateMonthsInput", new Vector2(80f, -40f));
            TMP_InputField y = UiHelpers.FindOrCreateInput(comparePanel.transform, "DateYearsInput", new Vector2(160f, -40f));

            Button apply = UiHelpers.FindOrCreateButton(panel.transform, "ApplyButton", "Apply", new Vector2(-70f, -160f));
            apply.gameObject.SetActive(false);
            Button close = UiHelpers.FindOrCreateButton(panel.transform, "CloseButton", "Close", new Vector2(0f, -160f));
            panel.SetActive(false);

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("openButton").objectReferenceValue = openButton;
            so.FindProperty("resetButton").objectReferenceValue = resetButton;
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("criterionDropdown").objectReferenceValue = criterion;
            so.FindProperty("typePanel").objectReferenceValue = typePanel;
            so.FindProperty("typeBlockToggle").objectReferenceValue = block;
            so.FindProperty("typeSpikeToggle").objectReferenceValue = spike;
            so.FindProperty("typeTriggerToggle").objectReferenceValue = trigger;
            so.FindProperty("typeParticleToggle").objectReferenceValue = particle;
            so.FindProperty("typeKeyframeToggle").objectReferenceValue = keyframe;
            so.FindProperty("colorPanel").objectReferenceValue = colorPanel;
            so.FindProperty("colorHexInput").objectReferenceValue = colorHex;
            so.FindProperty("colorAddButton").objectReferenceValue = colorAdd;
            so.FindProperty("colorGridRoot").objectReferenceValue = colorGrid;
            so.FindProperty("groupPanel").objectReferenceValue = groupPanel;
            so.FindProperty("groupInput").objectReferenceValue = groupInput;
            so.FindProperty("groupAddButton").objectReferenceValue = groupAdd;
            so.FindProperty("groupGridRoot").objectReferenceValue = groupGrid;
            so.FindProperty("comparePanel").objectReferenceValue = comparePanel;
            so.FindProperty("compareDropdown").objectReferenceValue = compare;
            so.FindProperty("valueInput").objectReferenceValue = value;
            so.FindProperty("valueXInput").objectReferenceValue = valueX;
            so.FindProperty("valueYInput").objectReferenceValue = valueY;
            so.FindProperty("dateHoursInput").objectReferenceValue = h;
            so.FindProperty("dateMinutesInput").objectReferenceValue = m;
            so.FindProperty("dateDaysInput").objectReferenceValue = d;
            so.FindProperty("dateMonthsInput").objectReferenceValue = mo;
            so.FindProperty("dateYearsInput").objectReferenceValue = y;
            so.FindProperty("applyButton").objectReferenceValue = apply;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createMode.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Kaplao] Selection Settings UI setup complete.");
        }

        static void SetupImportSfxInScene(Canvas canvas, Transform createMode)
        {
            KaplaoSoundImportUi ui = canvas.GetComponent<KaplaoSoundImportUi>() ??
                                     canvas.gameObject.AddComponent<KaplaoSoundImportUi>();

            Button importButton = UiHelpers.FindOrCreateTopRightButton(createMode, "ImportSoundButton", "Import SFX", -328f);
            GameObject panel = UiHelpers.FindOrCreatePanel(createMode, "SoundImportPanel", new Vector2(220f, 184f));
            TMP_InputField nameInput = UiHelpers.FindOrCreateInput(panel.transform, "SoundNameInput", new Vector2(0f, 0f));
            nameInput.text = "custom";
            Button select = UiHelpers.FindOrCreateButton(panel.transform, "SelectSoundFileButton", "Select File", new Vector2(0f, -44f));
            Button add = UiHelpers.FindOrCreateButton(panel.transform, "SoundImportAddButton", "Add", new Vector2(-50f, -116f));
            Button close = UiHelpers.FindOrCreateButton(panel.transform, "SoundImportCloseButton", "Close", new Vector2(50f, -116f));
            TextMeshProUGUI fileLabel = UiHelpers.FindOrCreateLabel(panel.transform, "SoundFileNameLabel", string.Empty, new Vector2(0f, -80f));
            TextMeshProUGUI status = UiHelpers.FindOrCreateLabel(panel.transform, "SoundImportStatusLabel", string.Empty, new Vector2(0f, -144f));
            panel.SetActive(false);

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("importSoundButton").objectReferenceValue = importButton;
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("soundNameInput").objectReferenceValue = nameInput;
            so.FindProperty("selectFileButton").objectReferenceValue = select;
            so.FindProperty("addButton").objectReferenceValue = add;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("fileNameLabel").objectReferenceValue = fileLabel;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createMode.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Kaplao] Import SFX UI setup complete.");
        }

        static void SetupLevelSettingsInScene(Canvas canvas, Transform createMode)
        {
            KaplaoLevelSettingsUi ui = canvas.GetComponent<KaplaoLevelSettingsUi>() ??
                                       canvas.gameObject.AddComponent<KaplaoLevelSettingsUi>();

            Button open = UiHelpers.FindOrCreateTopRightButton(createMode, "LevelSettingsButton", "Level Settings", -488f);
            GameObject panel = UiHelpers.FindOrCreatePanel(createMode, "LevelSettingsPanel", new Vector2(360f, 260f));
            Toggle collide = UiHelpers.FindOrCreateToggle(panel.transform, "RunnersCanCollideToggle", "Runners can collide", new Vector2(0f, 60f));
            TMP_InputField spawnX = UiHelpers.FindOrCreateInput(panel.transform, "WorldSpawnXInput", new Vector2(-70f, 0f));
            TMP_InputField spawnY = UiHelpers.FindOrCreateInput(panel.transform, "WorldSpawnYInput", new Vector2(70f, 0f));
            UiHelpers.FindOrCreateLabel(panel.transform, "WorldSpawnLabel", "World Spawn X / Y", new Vector2(0f, 30f));
            Button apply = UiHelpers.FindOrCreateButton(panel.transform, "LevelSettingsApplyButton", "Apply", new Vector2(-60f, -80f));
            apply.gameObject.SetActive(false);
            Button close = UiHelpers.FindOrCreateButton(panel.transform, "LevelSettingsCloseButton", "Close", new Vector2(0f, -80f));
            panel.SetActive(false);

            Transform hostSettings = canvas.transform.Find("HostSettingsUI");
            if (hostSettings != null)
                hostSettings.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("openButton").objectReferenceValue = open;
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("runnersCanCollideToggle").objectReferenceValue = collide;
            so.FindProperty("worldSpawnXInput").objectReferenceValue = spawnX;
            so.FindProperty("worldSpawnYInput").objectReferenceValue = spawnY;
            so.FindProperty("applyButton").objectReferenceValue = apply;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createMode.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Kaplao] Level Settings UI setup complete. HostSettingsUI hidden.");
        }

        static void SetupKeyframesInScene(Canvas canvas, Transform createMode)
        {
            KaplaoKeyframeEditorUi editor = canvas.GetComponent<KaplaoKeyframeEditorUi>() ??
                                            canvas.gameObject.AddComponent<KaplaoKeyframeEditorUi>();
            if (canvas.GetComponent<KeyframePathVisualizer>() == null)
                canvas.gameObject.AddComponent<KeyframePathVisualizer>();

            Transform toolbar = createMode.Find("SelectionToolbar") ?? createMode;
            Button editButton = UiHelpers.FindOrCreateToolbarButton(toolbar, "EditKeyframeButton", "Edit Keyframe", new Vector2(480f, 0f));
            GameObject panel = UiHelpers.FindOrCreatePanel(createMode, "KeyframeEditorPanel", new Vector2(380f, 404f));

            UiHelpers.FindOrCreateLabel(panel.transform, "DurationLabel", "Duration", new Vector2(-90f, 110f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField duration = UiHelpers.FindOrCreateInput(panel.transform, "DurationInput", new Vector2(80f, 110f));
            UiHelpers.FindOrCreateLabel(panel.transform, "AnimLabel", "Anim", new Vector2(-90f, 64f), TextAlignmentOptions.MidlineLeft);
            TMP_Dropdown anim = EditorUiDropdownUtility.EnsureDropdown(
                panel.transform, "AnimDropdown", new Vector2(80f, 64f), new Vector2(180f, 36f),
                new[] { "linear", "easeIn", "easeOut", "easeInOut" });
            UiHelpers.FindOrCreateLabel(panel.transform, "SpawnIdLabel", "Spawn ID", new Vector2(-90f, 18f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField spawnId = UiHelpers.FindOrCreateInput(panel.transform, "SpawnIdInput", new Vector2(80f, 18f));
            Toggle curve = UiHelpers.FindOrCreateCheckbox(panel.transform, "CurveToggle", "Curve", new Vector2(0f, -32f));
            Toggle closeLoop = UiHelpers.FindOrCreateCheckbox(panel.transform, "KeyframeCloseLoopToggle", "Close loop", new Vector2(0f, -76f));
            Toggle distance = UiHelpers.FindOrCreateCheckbox(panel.transform, "KeyframeDistanceToggle", "Distance", new Vector2(0f, -120f));
            Button apply = UiHelpers.FindOrCreateButton(panel.transform, "KeyframeApplyButton", "Apply", new Vector2(-60f, -178f));
            apply.gameObject.SetActive(false);
            Button close = UiHelpers.FindOrCreateButton(panel.transform, "KeyframeCloseButton", "Close", new Vector2(0f, -178f));
            panel.SetActive(false);

            KaplaoCreateModeUi createUi = canvas.GetComponent<KaplaoCreateModeUi>();
            if (createUi != null)
            {
                SerializedObject createSo = new SerializedObject(createUi);
                createSo.FindProperty("editKeyframeButton").objectReferenceValue = editButton;
                createSo.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject so = new SerializedObject(editor);
            so.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createMode.gameObject;
            so.FindProperty("panelRoot").objectReferenceValue = panel.GetComponent<RectTransform>();
            so.FindProperty("durationInput").objectReferenceValue = duration;
            so.FindProperty("animDropdown").objectReferenceValue = anim;
            so.FindProperty("spawnIdInput").objectReferenceValue = spawnId;
            so.FindProperty("curveToggle").objectReferenceValue = curve;
            so.FindProperty("closeLoopToggle").objectReferenceValue = closeLoop;
            so.FindProperty("distanceToggle").objectReferenceValue = distance;
            so.FindProperty("applyButton").objectReferenceValue = apply;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Kaplao] Keyframes UI setup complete.");
        }

        static void SetupPublishInGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform pauseMenuRoot = canvas.transform.Find("PauseMenuRoot");
            if (pauseMenuRoot == null)
            {
                Debug.LogError("[Kaplao] Pause menu not found. Run Kaplao/Setup Pause Menu UI.");
                return;
            }

            Transform pauseMenuPanel = pauseMenuRoot.Find("PauseMenuPanel");
            if (pauseMenuPanel == null)
            {
                Debug.LogError("[Kaplao] PauseMenuPanel not found. Run Kaplao/Setup Pause Menu UI.");
                return;
            }

            Button publishBtn = UiHelpers.FindOrCreateCenterButton(pauseMenuPanel, "PublishButton", "Publish", new Vector2(0.5f, -0.12f));

            // If PublishLevelPanel was previously nested inside PauseMenuPanel, move it out to PauseMenuRoot.
            Transform oldNested = pauseMenuPanel.Find("PublishLevelPanel");
            if (oldNested != null)
                oldNested.SetParent(pauseMenuRoot, false);

            GameObject panel = UiHelpers.FindOrCreatePanel(pauseMenuRoot, "PublishLevelPanel", new Vector2(400f, 260f));
            TMP_InputField title = UiHelpers.FindOrCreateInput(panel.transform, "PublishTitleInput", new Vector2(0f, 40f));
            Button confirm = UiHelpers.FindOrCreateButton(panel.transform, "PublishConfirmButton", "Publish", new Vector2(-60f, -20f));
            Button cancel = UiHelpers.FindOrCreateButton(panel.transform, "PublishCancelButton", "Cancel", new Vector2(60f, -20f));
            TextMeshProUGUI status = UiHelpers.FindOrCreateLabel(panel.transform, "PublishStatusLabel", string.Empty, new Vector2(0f, -60f));
            GameObject progress = UiHelpers.FindOrCreateChild(panel.transform, "PublishProgressRoot");
            Slider bar = progress.GetComponent<Slider>() ?? progress.AddComponent<Slider>();
            TextMeshProUGUI pct = UiHelpers.FindOrCreateLabel(progress.transform, "PublishProgressLabel", "0%", new Vector2(0f, -20f));
            panel.SetActive(false);
            progress.SetActive(false);

            KaplaoPublishLevelUi ui = canvas.GetComponent<KaplaoPublishLevelUi>() ??
                                      canvas.gameObject.AddComponent<KaplaoPublishLevelUi>();
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("publishButton").objectReferenceValue = publishBtn;
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("titleInput").objectReferenceValue = title;
            so.FindProperty("confirmPublishButton").objectReferenceValue = confirm;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("progressRoot").objectReferenceValue = progress;
            so.FindProperty("progressBar").objectReferenceValue = bar;
            so.FindProperty("progressLabel").objectReferenceValue = pct;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Publish level UI setup complete.");
        }

        static void SetupBrowseInMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform mainPanel = canvas.transform.Find("MainPanel");
            if (mainPanel == null) mainPanel = canvas.transform;

            Button browse = UiHelpers.FindOrCreateCenterButton(mainPanel, "BrowseLevelsButton", "Browse Levels", new Vector2(0f, -440f));
            GameObject panel = UiHelpers.FindOrCreatePanel(canvas.transform, "BrowseLevelsPanel", new Vector2(680f, 580f));
            TMP_InputField search = UiHelpers.FindOrCreateInput(panel.transform, "BrowseSearchInput", new Vector2(0f, 220f));
            RectTransform searchRt = search.GetComponent<RectTransform>();
            if (searchRt != null) searchRt.sizeDelta = new Vector2(580f, 44f);

            GameObject scroll = UiHelpers.FindOrCreateChild(panel.transform, "BrowseScroll");
            RectTransform scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchoredPosition = new Vector2(0f, 15f);
            scrollRt.sizeDelta = new Vector2(580f, 340f);

            // Clean up old Mask if present, use RectMask2D
            Mask oldMask = scroll.GetComponent<Mask>();
            if (oldMask != null) Object.DestroyImmediate(oldMask);
            if (scroll.GetComponent<RectMask2D>() == null) scroll.AddComponent<RectMask2D>();

            GameObject contentObj = UiHelpers.FindOrCreateChild(scroll.transform, "BrowseContent");
            RectTransform content = contentObj.GetComponent<RectTransform>();
            GridLayoutGroup oldGrid = contentObj.GetComponent<GridLayoutGroup>();
            if (oldGrid != null) Object.DestroyImmediate(oldGrid);

            VerticalLayoutGroup vlg = contentObj.GetComponent<VerticalLayoutGroup>() ?? contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>() ?? contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            ScrollRect scrollRect = scroll.GetComponent<ScrollRect>() ?? scroll.AddComponent<ScrollRect>();
            scrollRect.content = content;
            scrollRect.viewport = scrollRt;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            GameObject entryTemplate = UiHelpers.FindOrCreateChild(content, "BrowseEntryTemplate");
            RectTransform entryRt = entryTemplate.GetComponent<RectTransform>();
            entryRt.sizeDelta = new Vector2(0f, 76f);
            LayoutElement entryLe = entryTemplate.GetComponent<LayoutElement>() ?? entryTemplate.AddComponent<LayoutElement>();
            entryLe.minHeight = 76f;
            entryLe.preferredHeight = 76f;
            entryLe.flexibleHeight = 0f;

            TextMeshProUGUI title = UiHelpers.FindOrCreateLabel(entryTemplate.transform, "Title", "Level", Vector2.zero);
            title.fontStyle = FontStyles.Bold;
            title.fontSize = 18f;
            title.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(1f, 0.95f);
            titleRt.offsetMin = new Vector2(20f, 0f);
            titleRt.offsetMax = new Vector2(-20f, 0f);

            TextMeshProUGUI author = UiHelpers.FindOrCreateLabel(entryTemplate.transform, "Author", "by author", Vector2.zero);
            author.fontSize = 13.5f;
            author.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 1f);
            RectTransform authorRt = author.rectTransform;
            authorRt.anchorMin = new Vector2(0f, 0.08f);
            authorRt.anchorMax = new Vector2(1f, 0.45f);
            authorRt.offsetMin = new Vector2(20f, 0f);
            authorRt.offsetMax = new Vector2(-20f, 0f);

            entryTemplate.SetActive(false);

            Button play = UiHelpers.FindOrCreateButton(panel.transform, "BrowsePlayButton", "Play", new Vector2(-160f, -182f));
            play.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 40f);
            Button copy = UiHelpers.FindOrCreateButton(panel.transform, "BrowseCopyButton", "Copy", new Vector2(0f, -182f));
            copy.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 40f);
            Button back = UiHelpers.FindOrCreateButton(panel.transform, "BrowseBackButton", "Back", new Vector2(160f, -182f));
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 40f);

            GameObject progress = UiHelpers.FindOrCreateChild(panel.transform, "BrowseProgressRoot");
            RectTransform progressRt = progress.GetComponent<RectTransform>() ?? progress.AddComponent<RectTransform>();
            progressRt.anchoredPosition = new Vector2(0f, -234f);
            progressRt.sizeDelta = new Vector2(520f, 26f);
            Slider bar = progress.GetComponent<Slider>() ?? progress.AddComponent<Slider>();
            TextMeshProUGUI pct = UiHelpers.FindOrCreateLabel(progress.transform, "BrowseProgressLabel", "0%", Vector2.zero);
            progress.SetActive(false);

            Button deleteFromServers = UiHelpers.FindOrCreateButton(panel.transform, "BrowseDeleteFromServersButton", "Delete From Servers", new Vector2(0f, -282f));
            deleteFromServers.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
            play.gameObject.SetActive(false);
            copy.gameObject.SetActive(false);
            deleteFromServers.gameObject.SetActive(false);

            GameObject deleteConfirmPanel = UiHelpers.FindOrCreatePanel(panel.transform, "BrowseDeleteConfirmPanel", new Vector2(420f, 180f));
            deleteConfirmPanel.transform.SetAsLastSibling();
            TextMeshProUGUI deleteConfirmLabel = UiHelpers.FindOrCreateLabel(deleteConfirmPanel.transform, "BrowseDeleteConfirmLabel", "Delete this level from public servers?", new Vector2(0f, 24f));
            deleteConfirmLabel.rectTransform.sizeDelta = new Vector2(380f, 72f);
            Button deleteConfirm = UiHelpers.FindOrCreateButton(deleteConfirmPanel.transform, "BrowseDeleteConfirmButton", "Delete permanently", new Vector2(-90f, -40f));
            Button deleteCancel = UiHelpers.FindOrCreateButton(deleteConfirmPanel.transform, "BrowseDeleteCancelButton", "Cancel", new Vector2(90f, -40f));
            deleteConfirmPanel.SetActive(false);

            TextMeshProUGUI status = UiHelpers.FindOrCreateLabel(panel.transform, "BrowseStatusLabel", string.Empty, new Vector2(0f, -148f));
            panel.SetActive(false);

            KaplaoBrowseLevelsUi ui = canvas.GetComponent<KaplaoBrowseLevelsUi>() ??
                                      canvas.gameObject.AddComponent<KaplaoBrowseLevelsUi>();
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("browseButton").objectReferenceValue = browse;
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("searchInput").objectReferenceValue = search;
            so.FindProperty("listContent").objectReferenceValue = content;
            so.FindProperty("entryTemplate").objectReferenceValue = entryTemplate;
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("copyButton").objectReferenceValue = copy;
            so.FindProperty("deleteFromServersButton").objectReferenceValue = deleteFromServers;
            so.FindProperty("deleteConfirmPanel").objectReferenceValue = deleteConfirmPanel;
            so.FindProperty("deleteConfirmLabel").objectReferenceValue = deleteConfirmLabel;
            so.FindProperty("deleteConfirmButton").objectReferenceValue = deleteConfirm;
            so.FindProperty("deleteCancelButton").objectReferenceValue = deleteCancel;
            so.FindProperty("progressRoot").objectReferenceValue = progress;
            so.FindProperty("progressBar").objectReferenceValue = bar;
            so.FindProperty("progressLabel").objectReferenceValue = pct;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Browse levels UI setup complete.");
        }

        static void SetupExecutorInScene(Canvas canvas, Transform createMode)
        {
            KaplaoExecutorEditorUi editor = canvas.GetComponent<KaplaoExecutorEditorUi>() ??
                                            canvas.gameObject.AddComponent<KaplaoExecutorEditorUi>();

            Transform toolbar = createMode.Find("SelectionToolbar") ?? createMode;
            Button editButton = UiHelpers.FindOrCreateToolbarButton(toolbar, "EditExecutorButton", "Edit Executor", new Vector2(600f, 0f));

            GameObject panel = UiHelpers.FindOrCreatePanel(createMode, "ExecutorEditorPanel", new Vector2(440f, 760f));

            Transform oldCloseLoop = panel.transform.Find("CloseLoopToggle");
            if (oldCloseLoop != null)
                Object.DestroyImmediate(oldCloseLoop.gameObject);

            UiHelpers.FindOrCreateLabel(panel.transform, "TargetGroupLabel", "Target Group", new Vector2(-100f, 330f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField targetGroup = UiHelpers.FindOrCreateInput(panel.transform, "TargetGroupInput", new Vector2(90f, 330f));
            UiHelpers.FindOrCreateLabel(panel.transform, "ExDurationLabel", "Duration", new Vector2(-100f, 286f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField duration    = UiHelpers.FindOrCreateInput(panel.transform, "ExDurationInput",  new Vector2(90f, 286f));
            UiHelpers.FindOrCreateLabel(panel.transform, "ExAnimLabel", "Anim", new Vector2(-100f, 242f), TextAlignmentOptions.MidlineLeft);
            TMP_Dropdown   anim        = EditorUiDropdownUtility.EnsureDropdown(panel.transform, "ExAnimDropdown",
                new Vector2(90f, 242f), new Vector2(180f, 36f), new[] { "linear", "easeIn", "easeOut", "easeInOut" });
            UiHelpers.FindOrCreateLabel(panel.transform, "X360Label", "x360", new Vector2(-100f, 198f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField x360        = UiHelpers.FindOrCreateInput(panel.transform, "X360Input",        new Vector2(90f, 198f));

            Toggle spawnedToggle    = UiHelpers.FindOrCreateCheckbox(panel.transform, "SpawnedExecutorToggle",  "Spawned Executor",          new Vector2(0f, 150f));
            Toggle spawnAtEndToggle = UiHelpers.FindOrCreateCheckbox(panel.transform, "SpawnAtEndToggle",       "Spawn Executor at the end", new Vector2(0f, 114f));
            UiHelpers.FindOrCreateLabel(panel.transform, "SpawnAtEndGroupLabel", "Spawn group", new Vector2(-100f, 74f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField spawnAtEndGroup = UiHelpers.FindOrCreateInput(panel.transform, "SpawnAtEndGroupInput", new Vector2(90f, 74f));
            Toggle loopToggle       = UiHelpers.FindOrCreateCheckbox(panel.transform, "LoopToggle",             "Loop",                      new Vector2(0f, 30f));
            UiHelpers.FindOrCreateLabel(panel.transform, "LoopEveryLabel", "Every N seconds", new Vector2(-100f, -10f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField loopEvery = UiHelpers.FindOrCreateInput(panel.transform, "LoopEveryInput",         new Vector2(90f, -10f));
            Toggle returnToggle     = UiHelpers.FindOrCreateCheckbox(panel.transform, "ReturnToggle",           "Return",                    new Vector2(0f, -54f));
            UiHelpers.FindOrCreateLabel(panel.transform, "ReturnDelayLabel", "Time between", new Vector2(-100f, -94f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField returnDelay = UiHelpers.FindOrCreateInput(panel.transform, "ReturnDelayInput",     new Vector2(90f, -94f));
            Toggle useKfToggle      = UiHelpers.FindOrCreateCheckbox(panel.transform, "UseKeyframesToggle",     "Use Keyframes",             new Vector2(0f, -138f));
            UiHelpers.FindOrCreateLabel(panel.transform, "KeyframesGroupLabel", "Keyframes' Group", new Vector2(-100f, -178f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField kfGroup  = UiHelpers.FindOrCreateInput(panel.transform, "KeyframesGroupInput",     new Vector2(90f, -178f));

            Button editStates = UiHelpers.FindOrCreateButton(panel.transform, "EditStatesButton",  "Edit States", new Vector2(0f, -230f));
            Button apply      = UiHelpers.FindOrCreateButton(panel.transform, "ExApplyButton",     "Apply",       new Vector2(-60f, -280f));
            apply.gameObject.SetActive(false);
            Button close      = UiHelpers.FindOrCreateButton(panel.transform, "ExCloseButton",     "Close",       new Vector2(0f, -280f));
            panel.SetActive(false);

            Button leaveEditStates = UiHelpers.FindOrCreateButton(createMode, "LeaveEditStatesButton", "Leave Edit States", new Vector2(0f, -40f));
            Button editStartState = UiHelpers.FindOrCreateButton(createMode, "EditStartStateButton", "Edit Start State", new Vector2(-220f, -40f));
            Button editEndState = UiHelpers.FindOrCreateButton(createMode, "EditEndStateButton", "Edit End State", new Vector2(220f, -40f));
            Button resetStartState = UiHelpers.FindOrCreateButton(createMode, "ResetStartStateButton", "Reset Start State", new Vector2(-220f, -90f));
            Button resetEndState = UiHelpers.FindOrCreateButton(createMode, "ResetEndStateButton", "Reset End State", new Vector2(220f, -90f));
            TextMeshProUGUI grtHint = UiHelpers.FindOrCreateLabel(createMode, "ExecutorEditStatesHintLabel", "G / R / T on selected Start/End state", new Vector2(0f, -140f));
            SetTopCenter(leaveEditStates.GetComponent<RectTransform>(), new Vector2(0f, -40f), new Vector2(220f, 40f));
            SetTopCenter(editStartState.GetComponent<RectTransform>(), new Vector2(-220f, -40f), new Vector2(200f, 40f));
            SetTopCenter(editEndState.GetComponent<RectTransform>(), new Vector2(220f, -40f), new Vector2(200f, 40f));
            SetTopCenter(resetStartState.GetComponent<RectTransform>(), new Vector2(-220f, -90f), new Vector2(200f, 40f));
            SetTopCenter(resetEndState.GetComponent<RectTransform>(), new Vector2(220f, -90f), new Vector2(200f, 40f));
            RectTransform hintRect = grtHint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -140f);
            leaveEditStates.gameObject.SetActive(false);
            editStartState.gameObject.SetActive(false);
            editEndState.gameObject.SetActive(false);
            resetStartState.gameObject.SetActive(false);
            resetEndState.gameObject.SetActive(false);
            grtHint.gameObject.SetActive(false);

            KaplaoCreateModeUi createUi = canvas.GetComponent<KaplaoCreateModeUi>();
            if (createUi != null)
            {
                SerializedObject createSo = new SerializedObject(createUi);
                createSo.FindProperty("editExecutorButton").objectReferenceValue = editButton;
                createSo.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject so = new SerializedObject(editor);
            so.FindProperty("kaplaoOnlyRoot").objectReferenceValue      = createMode.gameObject;
            so.FindProperty("panelRoot").objectReferenceValue          = panel.GetComponent<RectTransform>();
            so.FindProperty("targetGroupInput").objectReferenceValue   = targetGroup;
            so.FindProperty("durationInput").objectReferenceValue      = duration;
            so.FindProperty("animDropdown").objectReferenceValue       = anim;
            so.FindProperty("x360Input").objectReferenceValue          = x360;
            so.FindProperty("spawnedExecutorToggle").objectReferenceValue = spawnedToggle;
            so.FindProperty("spawnAtEndToggle").objectReferenceValue      = spawnAtEndToggle;
            so.FindProperty("spawnAtEndGroupInput").objectReferenceValue  = spawnAtEndGroup;
            so.FindProperty("loopToggle").objectReferenceValue         = loopToggle;
            so.FindProperty("loopEveryInput").objectReferenceValue     = loopEvery;
            so.FindProperty("useKeyframesToggle").objectReferenceValue = useKfToggle;
            so.FindProperty("keyframesGroupInput").objectReferenceValue = kfGroup;
            so.FindProperty("returnToggle").objectReferenceValue = returnToggle;
            so.FindProperty("returnDelayInput").objectReferenceValue = returnDelay;
            so.FindProperty("editStatesButton").objectReferenceValue   = editStates;
            so.FindProperty("closeButton").objectReferenceValue        = close;
            so.FindProperty("leaveEditStatesButton").objectReferenceValue = leaveEditStates;
            so.FindProperty("editStartStateButton").objectReferenceValue = editStartState;
            so.FindProperty("editEndStateButton").objectReferenceValue = editEndState;
            so.FindProperty("resetStartStateButton").objectReferenceValue = resetStartState;
            so.FindProperty("resetEndStateButton").objectReferenceValue = resetEndState;
            so.FindProperty("grtHintLabel").objectReferenceValue = grtHint;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Kaplao] Executor UI setup complete.");
        }

        static void SetTopCenter(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }
    }

    static class UiHelpers
    {
        public static Button FindOrCreateTopRightButton(Transform parent, string name, string label, float y)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();

            Button button = CreateButton(parent, name, label, new Vector2(220f, 36f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, y);
            return button;
        }

        public static Button FindOrCreateCenterButton(Transform parent, string name, string label, Vector2 anchorY)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();

            Button button = CreateButton(parent, name, label, new Vector2(280f, 44f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(anchorY.x, anchorY.y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return button;
        }

        public static Button FindOrCreateToolbarButton(Transform parent, string name, string label, Vector2 pos)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();

            Button button = CreateButton(parent, name, label, new Vector2(120f, 36f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            return button;
        }

        public static GameObject FindOrCreatePanel(Transform parent, string name, Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject panel;
            if (existing != null) panel = existing.gameObject;
            else
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(parent, false);
                panel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            }

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return panel;
        }

        public static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t.gameObject;
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform FindOrCreateGrid(Transform parent, string name)
        {
            GameObject go = FindOrCreateChild(parent, name);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 120f);
            if (go.GetComponent<GridLayoutGroup>() == null)
            {
                GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(56f, 36f);
                grid.spacing = new Vector2(8f, 8f);
            }
            return rect;
        }

        public static TMP_InputField FindOrCreateInput(Transform parent, string name, Vector2 pos)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<TMP_InputField>();

            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(160f, 34f);
            root.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(root.transform, false);
            Stretch(textArea.GetComponent<RectTransform>());
            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            Stretch(text.rectTransform);
            TMP_InputField field = root.GetComponent<TMP_InputField>();
            field.textComponent = text;
            field.textViewport = textArea.GetComponent<RectTransform>();
            return field;
        }

        public static TMP_Dropdown FindOrCreateDropdown(Transform parent, string name, Vector2 pos) =>
            EditorUiDropdownUtility.EnsureDropdown(parent, name, pos, new Vector2(220f, 36f), null);

        public static Toggle FindOrCreateToggle(Transform parent, string name, string label, Vector2 pos) =>
            FindOrCreateCheckbox(parent, name, label, pos);

        public static Toggle FindOrCreateCheckbox(Transform parent, string name, string label, Vector2 pos)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (existing.Find("Background") == null)
                    Object.DestroyImmediate(existing.gameObject);
                else
                {
                    Toggle existingToggle = existing.GetComponent<Toggle>();
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    existingRect.anchoredPosition = pos;
                    existingRect.sizeDelta = new Vector2(360f, 28f);
                    Transform labelTransform = existing.Find("Label");
                    if (labelTransform != null)
                    {
                        TextMeshProUGUI existingLabel = labelTransform.GetComponent<TextMeshProUGUI>();
                        if (existingLabel != null)
                            existingLabel.text = label;
                    }

                    if (existingToggle != null)
                        return existingToggle;
                }
            }

            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = pos;
            rowRect.sizeDelta = new Vector2(360f, 28f);

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(row.transform, false);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(22f, 22f);
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
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(30f, 0f);
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 18f;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = Color.white;
            return toggle;
        }

        public static Button FindOrCreateButton(Transform parent, string name, string label, Vector2 pos)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();
            Button button = CreateButton(parent, name, label, new Vector2(120f, 36f));
            button.GetComponent<RectTransform>().anchoredPosition = pos;
            return button;
        }

        public static TextMeshProUGUI FindOrCreateLabel(Transform parent, string name, string text, Vector2 pos) =>
            FindOrCreateLabel(parent, name, text, pos, TextAlignmentOptions.Center);

        public static TextMeshProUGUI FindOrCreateLabel(
            Transform parent,
            string name,
            string text,
            Vector2 pos,
            TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TextMeshProUGUI existingLabel = existing.GetComponent<TextMeshProUGUI>();
                existing.GetComponent<RectTransform>().anchoredPosition = pos;
                if (existingLabel != null)
                {
                    existingLabel.text = text;
                    existingLabel.alignment = alignment;
                    return existingLabel;
                }
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(160f, 28f);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 16f;
            label.alignment = alignment;
            label.color = Color.white;
            return label;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = FindOrCreateLabel(buttonObject.transform, "Text", label, Vector2.zero);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform);
            return button;
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
