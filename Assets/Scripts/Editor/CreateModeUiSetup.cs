#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using KaplaoGames.Client;
using KaplaoGames.LevelScript;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class CreateModeUiSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string KaplaoPrefabPath = "Assets/Prefabs/Kaplao.prefab";

        [MenuItem("Kaplao/Setup Create Mode UI")]
        public static void SetupCreateModeUi()
        {
            EnsureKaplaoComponents();
            SetupCanvasUi();
            EnsureLevelScriptRuntime();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kaplao] Create mode + code UI setup complete.");
        }

        [MenuItem("Kaplao/Ensure UI Above World")]
        public static void EnsureUiAboveWorldFromMenu()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in open scene.");
                return;
            }

            EnsureGameUiCanvas(canvas);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Kaplao] Canvas sorting order set to 150 (above tiles, below cursors).");
        }

        static void EnsureGameUiCanvas(Canvas canvas)
        {
            if (canvas.GetComponent<KaplaoGameUiCanvas>() == null)
                canvas.gameObject.AddComponent<KaplaoGameUiCanvas>();
        }

        static void EnsureKaplaoComponents()
        {
            GameObject kaplaoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KaplaoPrefabPath);
            if (kaplaoPrefab == null)
            {
                Debug.LogWarning("[Kaplao] Kaplao prefab not found.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(KaplaoPrefabPath);
            if (root.GetComponent<TileSelectionVisuals>() == null)
                root.AddComponent<TileSelectionVisuals>();

            PrefabUtility.SaveAsPrefabAsset(root, KaplaoPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void SetupCanvasUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            EnsureGameUiCanvas(canvas);

            if (canvas.GetComponent<GroupLabelVisibility>() == null)
                canvas.gameObject.AddComponent<GroupLabelVisibility>();
            if (canvas.GetComponent<EditorLayerVisuals>() == null)
                canvas.gameObject.AddComponent<EditorLayerVisuals>();

            KaplaoCreateModeUi existingUi = canvas.GetComponent<KaplaoCreateModeUi>();
            if (existingUi == null)
                existingUi = canvas.gameObject.AddComponent<KaplaoCreateModeUi>();

            Transform root = canvas.transform.Find("CreateModeUI");
            if (root != null)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Create Mode UI already exists",
                    "CreateModeUI already exists in the scene. Running setup can overwrite your manual layout changes.\n\nContinue anyway?",
                    "Continue",
                    "Cancel");
                if (!proceed)
                {
                    Debug.Log("[Kaplao] Setup Create Mode UI cancelled to preserve existing layout.");
                    return;
                }
            }
            else
            {
                GameObject rootObject = new GameObject("CreateModeUI", typeof(RectTransform));
                rootObject.transform.SetParent(canvas.transform, false);
                RectTransform rootRect = rootObject.GetComponent<RectTransform>();
                StretchFull(rootRect);
                root = rootObject.transform;
            }

            Button usualButton = FindOrCreateModeButton(root, "UsualModeButton", "Usual", new Vector2(0.5f, 1f), new Vector2(-220f, -16f));
            Button createButton = FindOrCreateModeButton(root, "CreateModeButton", "Create", new Vector2(0.5f, 1f), new Vector2(-110f, -16f));
            Button codeButton = FindOrCreateModeButton(root, "CodeModeButton", "Code", new Vector2(0.5f, 1f), new Vector2(0f, -16f));
            Button createSettingsButton = FindOrCreateModeButton(root, "CreateSettingsButton", "Create Settings",
                new Vector2(0.5f, 1f), new Vector2(120f, -16f));

            GameObject toolbar = FindOrCreateChild(root, "SelectionToolbar");
            RectTransform toolbarRect = toolbar.GetComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0.5f, 0f);
            toolbarRect.anchorMax = new Vector2(0.5f, 0f);
            toolbarRect.pivot = new Vector2(0.5f, 0f);
            toolbarRect.anchoredPosition = new Vector2(0f, 24f);
            toolbarRect.sizeDelta = new Vector2(820f, 44f);
            toolbar.SetActive(false);

            Button setGroupButton = FindOrCreateToolbarButton(toolbar.transform, "SetGroupButton", "Set Group", new Vector2(-240f, 0f));
            Button setTextureButton = FindOrCreateToolbarButton(toolbar.transform, "SetTextureButton", "Set Texture", new Vector2(-120f, 0f));
            Button settingsButton = FindOrCreateToolbarButton(toolbar.transform, "SettingsButton", "Settings", new Vector2(0f, 0f));
            Button deselectButton = FindOrCreateToolbarButton(toolbar.transform, "DeselectButton", "Deselect", new Vector2(120f, 0f));
            Button deleteButton = FindOrCreateToolbarButton(toolbar.transform, "DeleteSelectionButton", "Delete", new Vector2(240f, 0f));
            Button editParticlesButton = FindOrCreateToolbarButton(toolbar.transform, "EditParticlesButton", "Edit Particles", new Vector2(360f, 0f));

            GameObject panel = FindOrCreateChild(root, "SetGroupPanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(420f, 360f);
            Image panelImage = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            panel.SetActive(false);

            // Prefer dedicated Kaplao/Setup Set Group Panel for full GD layout.
            // Keep minimal stubs so existing Create Mode setup still binds something.
            TMP_InputField inputField = FindOrCreateInputField(panel.transform, "GroupInputField",
                new Vector2(50f, 100f), new Vector2(80f, 36f));
            Button nextFreeButton = FindOrCreatePanelButton(panel.transform, "NextFreeGroupButton", "Next Free",
                new Vector2(-120f, 100f));
            Button prevGroupButton = FindOrCreatePanelButton(panel.transform, "PrevGroupButton", "<",
                new Vector2(-20f, 100f));
            Button nextGroupButton = FindOrCreatePanelButton(panel.transform, "NextGroupButton", ">",
                new Vector2(120f, 100f));
            Button addButton = FindOrCreatePanelButton(panel.transform, "AddGroupButton", "Add",
                new Vector2(0f, 52f));
            Button closeButton = FindOrCreatePanelButton(panel.transform, "SetGroupCloseButton", "Close",
                new Vector2(0f, -150f));
            Transform groupsGrid = panel.transform.Find("GroupsGrid");
            if (groupsGrid == null)
            {
                GameObject grid = new GameObject("GroupsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
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
                groupsGrid = grid.transform;
            }

            GameObject settingsPanel = FindOrCreateChild(root, "SettingsPanel");
            RectTransform settingsPanelRect = settingsPanel.GetComponent<RectTransform>();
            settingsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            settingsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            settingsPanelRect.pivot = new Vector2(0.5f, 0.5f);
            settingsPanelRect.anchoredPosition = Vector2.zero;
            settingsPanelRect.sizeDelta = new Vector2(420f, 300f);
            Image settingsPanelImage = settingsPanel.GetComponent<Image>() ?? settingsPanel.AddComponent<Image>();
            settingsPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            settingsPanel.SetActive(false);

            FindOrCreateLabel(settingsPanel.transform, "SettingsTitleLabel", "Tile Settings",
                new Vector2(0f, 60f), new Vector2(320f, 32f), 22f).alignment = TextAlignmentOptions.Center;

            TMP_InputField transparencyInput = FindOrCreateInputField(settingsPanel.transform, "TransparencyInputField",
                new Vector2(0f, 10f), new Vector2(280f, 34f));
            SetInputPlaceholder(transparencyInput, "0..1");
            Toggle noHitboxToggle = FindOrCreateSettingsToggle(settingsPanel.transform, "NoHitboxToggle",
                "No Hitbox (disable collision)", new Vector2(0f, -34f));
            settingsPanelRect.sizeDelta = new Vector2(420f, 340f);
            TMP_InputField posXInput = FindOrCreateInputField(settingsPanel.transform, "PosXInputField", new Vector2(-130f, -78f), new Vector2(90f, 34f));
            TMP_InputField posYInput = FindOrCreateInputField(settingsPanel.transform, "PosYInputField", new Vector2(-30f, -78f), new Vector2(90f, 34f));
            TMP_InputField rotInput = FindOrCreateInputField(settingsPanel.transform, "RotationInputField", new Vector2(70f, -78f), new Vector2(90f, 34f));
            TMP_InputField scaleXInput = FindOrCreateInputField(settingsPanel.transform, "ScaleXInputField", new Vector2(-80f, -118f), new Vector2(90f, 34f));
            TMP_InputField scaleYInput = FindOrCreateInputField(settingsPanel.transform, "ScaleYInputField", new Vector2(30f, -118f), new Vector2(90f, 34f));
            FindOrCreateLabel(settingsPanel.transform, "SortingOrderLabel", "Order", new Vector2(-120f, -158f), new Vector2(70f, 28f), 16f);
            TMP_InputField sortingOrderInput = FindOrCreateInputField(settingsPanel.transform, "SortingOrderInputField", new Vector2(-20f, -158f), new Vector2(90f, 34f));
            SetInputPlaceholder(sortingOrderInput, "-10..10");
            Button applyTransformButton = FindOrCreatePanelButton(settingsPanel.transform, "ApplyTransformButton", "Apply",
                new Vector2(0f, -208f));
            applyTransformButton.gameObject.SetActive(false);
            Button settingsCloseButton = FindOrCreatePanelButton(settingsPanel.transform, "SettingsCloseButton", "Close",
                new Vector2(0f, -208f));

            GameObject createSettingsPanel = FindOrCreateChild(root, "CreateSettingsPanel");
            RectTransform createSettingsPanelRect = createSettingsPanel.GetComponent<RectTransform>();
            createSettingsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            createSettingsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            createSettingsPanelRect.pivot = new Vector2(0.5f, 0.5f);
            createSettingsPanelRect.anchoredPosition = Vector2.zero;
            createSettingsPanelRect.sizeDelta = new Vector2(360f, 540f);
            Image createSettingsPanelImage = createSettingsPanel.GetComponent<Image>() ?? createSettingsPanel.AddComponent<Image>();
            createSettingsPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            createSettingsPanel.SetActive(false);

            FindOrCreateLabel(createSettingsPanel.transform, "CreateSettingsTitleLabel", "Create Settings",
                new Vector2(0f, 240f), new Vector2(320f, 32f), 22f).alignment = TextAlignmentOptions.Center;
            TMP_InputField rotationSnapInput = FindOrCreateInputField(createSettingsPanel.transform, "RotationSnapInputField",
                new Vector2(0f, 190f), new Vector2(280f, 34f));
            TMP_InputField positionSnapInput = FindOrCreateInputField(createSettingsPanel.transform, "PositionSnapInputField",
                new Vector2(0f, 150f), new Vector2(280f, 34f));
            TMP_InputField scaleSnapInput = FindOrCreateInputField(createSettingsPanel.transform, "ScaleSnapInputField",
                new Vector2(0f, 110f), new Vector2(280f, 34f));
            Toggle showGridToggle = FindOrCreateSettingsToggle(createSettingsPanel.transform, "ShowGridToggle",
                "Show grid", new Vector2(0f, 70f));
            Toggle showHitboxesToggle = FindOrCreateSettingsToggle(createSettingsPanel.transform, "ShowHitboxesToggle",
                "Show hitboxes", new Vector2(0f, 30f));
            Toggle showPlayersToggle = FindOrCreateSettingsToggle(createSettingsPanel.transform, "ShowPlayersToggle",
                "Show players", new Vector2(0f, -10f));
            Toggle hideCursorToggle = FindOrCreateSettingsToggle(createSettingsPanel.transform, "HideCursorToggle",
                "Hide cursor", new Vector2(0f, -50f));
            Toggle hitboxTrailToggle = FindOrCreateSettingsToggle(createSettingsPanel.transform, "HitboxTrailToggle",
                "Hitbox Trail", new Vector2(0f, -90f));
            FindOrCreateLabel(createSettingsPanel.transform, "MinZoomLabel", "Min ortho size",
                new Vector2(0f, -126f), new Vector2(280f, 22f), 14f);
            TMP_InputField minZoomInput = FindOrCreateInputField(createSettingsPanel.transform, "MinZoomInputField",
                new Vector2(0f, -154f), new Vector2(280f, 34f));
            SetInputPlaceholder(minZoomInput, "3");
            FindOrCreateLabel(createSettingsPanel.transform, "MaxZoomLabel", "Max ortho size",
                new Vector2(0f, -190f), new Vector2(280f, 22f), 14f);
            TMP_InputField maxZoomInput = FindOrCreateInputField(createSettingsPanel.transform, "MaxZoomInputField",
                new Vector2(0f, -218f), new Vector2(280f, 34f));
            SetInputPlaceholder(maxZoomInput, "20");
            Button createSettingsSaveButton = FindOrCreatePanelButton(createSettingsPanel.transform, "CreateSettingsSaveButton", "Save",
                new Vector2(-70f, -260f));
            Button createSettingsCloseButton = FindOrCreatePanelButton(createSettingsPanel.transform, "CreateSettingsCloseButton", "Close",
                new Vector2(70f, -260f));

            SetAnchored(createSettingsPanel.transform.Find("CreateSettingsTitleLabel"), new Vector2(0f, 240f), new Vector2(320f, 32f));
            SetAnchored(rotationSnapInput, new Vector2(0f, 190f), new Vector2(280f, 34f));
            SetAnchored(positionSnapInput, new Vector2(0f, 150f), new Vector2(280f, 34f));
            SetAnchored(scaleSnapInput, new Vector2(0f, 110f), new Vector2(280f, 34f));
            SetAnchored(showGridToggle, new Vector2(0f, 70f));
            SetAnchored(showHitboxesToggle, new Vector2(0f, 30f));
            SetAnchored(showPlayersToggle, new Vector2(0f, -10f));
            SetAnchored(hideCursorToggle, new Vector2(0f, -50f));
            SetAnchored(hitboxTrailToggle, new Vector2(0f, -90f));
            SetAnchored(createSettingsPanel.transform.Find("MinZoomLabel"), new Vector2(0f, -126f), new Vector2(280f, 22f));
            SetAnchored(minZoomInput, new Vector2(0f, -154f), new Vector2(280f, 34f));
            SetAnchored(createSettingsPanel.transform.Find("MaxZoomLabel"), new Vector2(0f, -190f), new Vector2(280f, 22f));
            SetAnchored(maxZoomInput, new Vector2(0f, -218f), new Vector2(280f, 34f));
            SetAnchored(createSettingsSaveButton, new Vector2(-70f, -260f));
            SetAnchored(createSettingsCloseButton, new Vector2(70f, -260f));

            TextMeshProUGUI cursorCoordsLabel = FindOrCreateLabel(root, "CursorCoordinatesLabel", "X: 0.00  Y: 0.00",
                new Vector2(180f, 18f), new Vector2(320f, 26f), 16f);
            cursorCoordsLabel.alignment = TextAlignmentOptions.MidlineRight;
            cursorCoordsLabel.rectTransform.anchorMin = new Vector2(1f, 0f);
            cursorCoordsLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            cursorCoordsLabel.rectTransform.pivot = new Vector2(1f, 0f);

            SerializedObject serializedUi = new SerializedObject(existingUi);
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = root.gameObject;
            serializedUi.FindProperty("usualModeButton").objectReferenceValue = usualButton;
            serializedUi.FindProperty("createModeButton").objectReferenceValue = createButton;
            serializedUi.FindProperty("codeModeButton").objectReferenceValue = codeButton;
            serializedUi.FindProperty("selectionToolbarRoot").objectReferenceValue = toolbar;
            serializedUi.FindProperty("setGroupButton").objectReferenceValue = setGroupButton;
            serializedUi.FindProperty("setTextureButton").objectReferenceValue = setTextureButton;
            serializedUi.FindProperty("editParticlesButton").objectReferenceValue = editParticlesButton;
            serializedUi.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            serializedUi.FindProperty("deselectButton").objectReferenceValue = deselectButton;
            serializedUi.FindProperty("deleteSelectionButton").objectReferenceValue = deleteButton;
            serializedUi.FindProperty("setGroupPanelRoot").objectReferenceValue = panel;
            serializedUi.FindProperty("groupInputField").objectReferenceValue = inputField;
            serializedUi.FindProperty("nextFreeGroupButton").objectReferenceValue = nextFreeButton;
            serializedUi.FindProperty("prevGroupButton").objectReferenceValue = prevGroupButton;
            serializedUi.FindProperty("nextGroupButton").objectReferenceValue = nextGroupButton;
            serializedUi.FindProperty("addGroupButton").objectReferenceValue = addButton;
            serializedUi.FindProperty("setGroupCloseButton").objectReferenceValue = closeButton;
            serializedUi.FindProperty("groupsGridRoot").objectReferenceValue = groupsGrid as RectTransform;
            serializedUi.FindProperty("settingsPanelRoot").objectReferenceValue = settingsPanel;
            serializedUi.FindProperty("transparencyInputField").objectReferenceValue = transparencyInput;
            serializedUi.FindProperty("noHitboxToggle").objectReferenceValue = noHitboxToggle;
            serializedUi.FindProperty("posXInputField").objectReferenceValue = posXInput;
            serializedUi.FindProperty("posYInputField").objectReferenceValue = posYInput;
            serializedUi.FindProperty("rotationInputField").objectReferenceValue = rotInput;
            serializedUi.FindProperty("scaleXInputField").objectReferenceValue = scaleXInput;
            serializedUi.FindProperty("scaleYInputField").objectReferenceValue = scaleYInput;
            serializedUi.FindProperty("sortingOrderInputField").objectReferenceValue = sortingOrderInput;
            serializedUi.FindProperty("applyTransformButton").objectReferenceValue = applyTransformButton;
            serializedUi.FindProperty("settingsCloseButton").objectReferenceValue = settingsCloseButton;
            serializedUi.FindProperty("createSettingsButton").objectReferenceValue = createSettingsButton;
            serializedUi.FindProperty("createSettingsPanelRoot").objectReferenceValue = createSettingsPanel;
            serializedUi.FindProperty("rotationSnapInputField").objectReferenceValue = rotationSnapInput;
            serializedUi.FindProperty("positionSnapInputField").objectReferenceValue = positionSnapInput;
            serializedUi.FindProperty("scaleSnapInputField").objectReferenceValue = scaleSnapInput;
            serializedUi.FindProperty("showGridToggle").objectReferenceValue = showGridToggle;
            serializedUi.FindProperty("showHitboxesToggle").objectReferenceValue = showHitboxesToggle;
            serializedUi.FindProperty("showPlayersToggle").objectReferenceValue = showPlayersToggle;
            serializedUi.FindProperty("hideCursorToggle").objectReferenceValue = hideCursorToggle;
            serializedUi.FindProperty("hitboxTrailToggle").objectReferenceValue = hitboxTrailToggle;
            serializedUi.FindProperty("minZoomInputField").objectReferenceValue = minZoomInput;
            serializedUi.FindProperty("maxZoomInputField").objectReferenceValue = maxZoomInput;
            serializedUi.FindProperty("createSettingsSaveButton").objectReferenceValue = createSettingsSaveButton;
            serializedUi.FindProperty("createSettingsCloseButton").objectReferenceValue = createSettingsCloseButton;
            serializedUi.FindProperty("cursorCoordinatesLabel").objectReferenceValue = cursorCoordsLabel;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.GetComponent<CreateModeGridOverlay>() == null)
                mainCamera.gameObject.AddComponent<CreateModeGridOverlay>();
            if (canvas.GetComponent<CreateModeGridOverlay>() != null)
                Object.DestroyImmediate(canvas.GetComponent<CreateModeGridOverlay>());
            if (canvas.GetComponent<CreateModeRotationOverlay>() == null)
                canvas.gameObject.AddComponent<CreateModeRotationOverlay>();
            if (canvas.GetComponent<CreateModeSelectionHud>() == null)
                canvas.gameObject.AddComponent<CreateModeSelectionHud>();

            KaplaoTileTextureUi tileTextureUi = canvas.GetComponent<KaplaoTileTextureUi>();
            if (tileTextureUi == null)
                tileTextureUi = canvas.gameObject.AddComponent<KaplaoTileTextureUi>();

            SerializedObject serializedTextureUi = new SerializedObject(tileTextureUi);
            serializedTextureUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = root.gameObject;
            serializedTextureUi.ApplyModifiedPropertiesWithoutUndo();

            GameSessionUi sessionUi = canvas.GetComponent<GameSessionUi>();
            if (sessionUi != null)
            {
                SerializedObject serializedSession = new SerializedObject(sessionUi);
                SerializedProperty roots = serializedSession.FindProperty("kaplaoOnlyRoots");
                AddKaplaoOnlyRoot(roots, root.gameObject);

                KaplaoTileColorUi tileColorUi = canvas.GetComponent<KaplaoTileColorUi>();
                if (tileColorUi != null && tileColorUi.KaplaoOnlyRoot != null)
                    AddKaplaoOnlyRoot(roots, tileColorUi.KaplaoOnlyRoot);

                serializedSession.ApplyModifiedPropertiesWithoutUndo();
            }

            BindTileColorPanelReferences(canvas, root);
            BindTexturePickerPanelReferences(canvas, root);
            BindParticleEditorPanelReferences(canvas, root);

            SetupCodeOverlay(canvas);
            SetupLevelIoUi(canvas, root);
            SetupPromotePanel(canvas);
            SetupHostSettingsUi(canvas);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BindTileColorPanelReferences(Canvas canvas, Transform createModeRoot)
        {
            KaplaoTileColorUi colorUi = canvas.GetComponent<KaplaoTileColorUi>();
            if (colorUi == null)
                return;

            TileColorPanelSetup.TryBindExistingPanel(colorUi, createModeRoot);
        }

        static void BindTexturePickerPanelReferences(Canvas canvas, Transform createModeRoot)
        {
            KaplaoTileTextureUi textureUi = canvas.GetComponent<KaplaoTileTextureUi>();
            if (textureUi == null)
                return;

            TexturePickerPanelSetup.TryBindExistingPanel(textureUi, createModeRoot);
        }

        static void BindParticleEditorPanelReferences(Canvas canvas, Transform createModeRoot)
        {
            KaplaoParticleEditorUi particleEditorUi = canvas.GetComponent<KaplaoParticleEditorUi>();
            if (particleEditorUi == null)
                particleEditorUi = canvas.gameObject.AddComponent<KaplaoParticleEditorUi>();

            ParticleEditorPanelSetup.RebuildAndBind(particleEditorUi, createModeRoot);
        }

        static void SetupCodeOverlay(Canvas canvas)
        {
            // Full IDE layout is built by Kaplao/Setup Code Mode IDE.
            KaplaoCodeModeUi codeUi = canvas.GetComponent<KaplaoCodeModeUi>();
            if (codeUi == null)
                codeUi = canvas.gameObject.AddComponent<KaplaoCodeModeUi>();

            if (canvas.transform.Find("CodeModeOverlay") == null)
            {
                Debug.LogWarning("[Kaplao] CodeModeOverlay missing. Run Kaplao/Setup Code Mode IDE.");
            }
        }

        static void SetupLevelIoUi(Canvas canvas, Transform createModeRoot)
        {
            KaplaoLevelIoUi levelIoUi = canvas.GetComponent<KaplaoLevelIoUi>();
            if (levelIoUi == null)
                levelIoUi = canvas.gameObject.AddComponent<KaplaoLevelIoUi>();

            GameObject panel = FindOrCreateChild(createModeRoot, "LevelIoPanel");

            Button importButton = FindOrCreateLevelIoButton(panel.transform, "ImportLevelButton", "Import",
                new Vector2(0f, 1f), Vector2.zero);
            Button exportButton = FindOrCreateLevelIoButton(panel.transform, "ExportLevelButton", "Export",
                new Vector2(1f, 1f), Vector2.zero);

            TextMeshProUGUI statusLabel = FindOrCreateLabel(panel.transform, "LevelIoStatusLabel", string.Empty,
                new Vector2(0f, -40f), new Vector2(220f, 24f), 14f);

            KaplaoLevelIoUi.ApplyLevelIoPanelLayout(panel.transform, importButton, exportButton, statusLabel);

            SerializedObject serializedLevelIo = new SerializedObject(levelIoUi);
            serializedLevelIo.FindProperty("importButton").objectReferenceValue = importButton;
            serializedLevelIo.FindProperty("exportButton").objectReferenceValue = exportButton;
            serializedLevelIo.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serializedLevelIo.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createModeRoot.gameObject;
            serializedLevelIo.ApplyModifiedPropertiesWithoutUndo();

            GameSessionUi sessionUi = canvas.GetComponent<GameSessionUi>();
            if (sessionUi != null)
            {
                SerializedObject serializedSession = new SerializedObject(sessionUi);
                SerializedProperty roots = serializedSession.FindProperty("kaplaoOnlyRoots");
                AddKaplaoOnlyRoot(roots, createModeRoot.gameObject);
                serializedSession.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetupHostSettingsUi(Canvas canvas)
        {
            Transform existing = canvas.transform.Find("HostSettingsUI");
            GameObject rootObject = existing != null
                ? existing.gameObject
                : new GameObject("HostSettingsUI", typeof(RectTransform));

            if (existing == null)
                rootObject.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-16f, -128f);
            rootRect.sizeDelta = new Vector2(280f, 36f);

            Transform toggleTransform = rootObject.transform.Find("RunnerInteractToggle");
            Toggle toggle;
            TextMeshProUGUI label;
            if (toggleTransform != null)
            {
                toggle = toggleTransform.GetComponent<Toggle>();
                label = toggleTransform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                toggle = CreateHostToggle(rootObject.transform, out label);
            }

            KaplaoHostSettingsUi hostSettingsUi = canvas.GetComponent<KaplaoHostSettingsUi>();
            if (hostSettingsUi == null)
                hostSettingsUi = canvas.gameObject.AddComponent<KaplaoHostSettingsUi>();

            SerializedObject serialized = new SerializedObject(hostSettingsUi);
            serialized.FindProperty("hostOnlyRoot").objectReferenceValue = rootObject;
            serialized.FindProperty("runnersCanInteractToggle").objectReferenceValue = toggle;
            serialized.FindProperty("runnersCanInteractLabel").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            rootObject.SetActive(false);
        }

        static Toggle CreateHostToggle(Transform parent, out TextMeshProUGUI label)
        {
            GameObject row = new GameObject("RunnerInteractToggle", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

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
            toggle.isOn = true;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(row.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(32f, 0f);
            labelRect.offsetMax = Vector2.zero;

            label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "Runners can collide";
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;

            return toggle;
        }

        static void SetupPromotePanel(Canvas canvas)
        {
            KaplaoPromoteRunnerUi promoteUi = canvas.GetComponent<KaplaoPromoteRunnerUi>();
            if (promoteUi == null)
                promoteUi = canvas.gameObject.AddComponent<KaplaoPromoteRunnerUi>();

            Transform existing = canvas.transform.Find("PromoteRunnerPanel");
            GameObject panel = existing != null
                ? existing.gameObject
                : new GameObject("PromoteRunnerPanel", typeof(RectTransform), typeof(Image));

            if (existing == null)
                panel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(420f, 180f);

            Image panelImage = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            TextMeshProUGUI messageLabel = FindOrCreateLabel(panel.transform, "PromoteMessageLabel",
                "Promote runner to Kaplao?", Vector2.zero, new Vector2(380f, 48f), 20f);
            messageLabel.alignment = TextAlignmentOptions.Center;

            Button promoteButton = FindOrCreatePanelButton(panel.transform, "PromoteConfirmButton", "Promote",
                new Vector2(-80f, -50f));
            Button cancelButton = FindOrCreatePanelButton(panel.transform, "PromoteCancelButton", "Cancel",
                new Vector2(80f, -50f));

            panel.SetActive(false);

            SerializedObject serializedPromoteUi = new SerializedObject(promoteUi);
            serializedPromoteUi.FindProperty("panelRoot").objectReferenceValue = panel;
            serializedPromoteUi.FindProperty("messageLabel").objectReferenceValue = messageLabel;
            serializedPromoteUi.FindProperty("promoteButton").objectReferenceValue = promoteButton;
            serializedPromoteUi.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            serializedPromoteUi.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddKaplaoOnlyRoot(SerializedProperty roots, GameObject root)
        {
            if (root == null)
                return;

            for (int i = 0; i < roots.arraySize; i++)
            {
                if (roots.GetArrayElementAtIndex(i).objectReferenceValue == root)
                    return;
            }

            roots.InsertArrayElementAtIndex(roots.arraySize);
            roots.GetArrayElementAtIndex(roots.arraySize - 1).objectReferenceValue = root;
        }

        static Button FindOrCreateLevelIoButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            Button button = CreateButton(parent, name, label, anchor, position, new Vector2(100f, 36f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.pivot = anchor;
            return button;
        }

        static void EnsureLevelScriptRuntime()
        {
            LevelGridServer grid = Object.FindFirstObjectByType<LevelGridServer>();
            if (grid == null)
            {
                Debug.LogWarning("[Kaplao] LevelGrid not found. Run Kaplao/Setup Level System first.");
                return;
            }

            if (grid.GetComponent<LevelScriptRuntime>() == null)
                grid.gameObject.AddComponent<LevelScriptRuntime>();

            if (grid.GetComponent<LevelGroupAnimator>() == null)
                grid.gameObject.AddComponent<LevelGroupAnimator>();

            if (grid.GetComponent<NetworkIdentity>() == null)
                grid.gameObject.AddComponent<NetworkIdentity>();

            if (grid.GetComponent<LevelSessionServer>() == null)
                grid.gameObject.AddComponent<LevelSessionServer>();

            if (grid.GetComponent<WorldLoadBootstrap>() == null)
                grid.gameObject.AddComponent<WorldLoadBootstrap>();
        }

        static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        static Button FindOrCreateModeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            return CreateButton(parent, name, label, anchor, position, new Vector2(100f, 36f));
        }

        static Button FindOrCreateCodeRunButton(Transform parent)
        {
            Transform existing = parent.Find("RunScriptButton");
            if (existing != null)
                return existing.GetComponent<Button>();

            Button button = CreateButton(parent, "RunScriptButton", "Run", new Vector2(1f, 0f), new Vector2(-24f, 24f),
                new Vector2(120f, 40f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.pivot = new Vector2(1f, 0f);
            return button;
        }

        static Button FindOrCreateCodeDocButton(Transform parent)
        {
            Transform existing = parent.Find("DocumentationButton");
            if (existing != null)
                return existing.GetComponent<Button>();

            Button button = CreateButton(parent, "DocumentationButton", "Documentation", new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(160f, 40f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 0f);
            return button;
        }

        static Button FindOrCreateToolbarButton(Transform parent, string name, string label, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            return CreateButton(parent, name, label, new Vector2(0.5f, 0.5f), position, new Vector2(120f, 36f));
        }

        static Button FindOrCreatePanelButton(Transform parent, string name, string label, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            return CreateButton(parent, name, label, new Vector2(0.5f, 0.5f), position, new Vector2(120f, 36f));
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI text = CreateLabel(buttonObject.transform, "Text", label, Vector2.zero, size, 20f);
            text.alignment = TextAlignmentOptions.Center;
            StretchFull(text.rectTransform);

            return button;
        }

        static TextMeshProUGUI FindOrCreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TextMeshProUGUI>();

            return CreateLabel(parent, name, text, position, size, fontSize);
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
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            return label;
        }

        static TMP_InputField FindOrCreateInputField(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_InputField>();

            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            inputObject.transform.SetParent(parent, false);

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(inputObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
            placeholder.text = "1,2";
            placeholder.fontSize = 18f;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            StretchFull(placeholder.rectTransform);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = image;
            return inputField;
        }

        static void SetInputPlaceholder(TMP_InputField inputField, string text)
        {
            if (inputField?.placeholder is TextMeshProUGUI placeholder)
                placeholder.text = text;
        }

        static void SetAnchored(Component component, Vector2 position, Vector2? size = null)
        {
            if (component == null)
                return;
            RectTransform rect = component.GetComponent<RectTransform>();
            if (rect == null)
                return;
            rect.anchoredPosition = position;
            if (size.HasValue)
                rect.sizeDelta = size.Value;
        }

        static TMP_InputField FindOrCreateMultilineInputField(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_InputField>();

            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            inputObject.transform.SetParent(parent, false);

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(inputObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(12f, 12f);
            textAreaRect.offsetMax = new Vector2(-12f, -12f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputField.targetGraphic = image;
            return inputField;
        }

        static Toggle FindOrCreateSettingsToggle(Transform parent, string name, string label, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Toggle>();

            return CreateSettingsToggle(parent, name, label, position);
        }

        static Toggle CreateSettingsToggle(Transform parent, string name, string label, Vector2 position)
        {
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

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    public static class BlockTexturePipeline
    {
        public const string TextureFolder = "Assets/BlockTextures";
        public const string CatalogAssetPath = "Assets/Resources/BlockTextureCatalog.asset";

        [MenuItem("Kaplao/Block Textures/1. Pad Non-Square To Square")]
        public static void PadNonSquareTextures()
        {
            EnsureTextureFolder();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            int padded = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryPadTextureFile(path))
                    padded++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Kaplao] Padded {padded} non-square block textures in {TextureFolder}.");
        }

        [MenuItem("Kaplao/Block Textures/2. Reimport All (Point, Sprite, 16 PPU)")]
        public static void ReimportAllTextures()
        {
            EnsureTextureFolder();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"[Kaplao] Reimported {guids.Length} textures from {TextureFolder}.");
        }

        [MenuItem("Kaplao/Block Textures/3. Build Catalog")]
        public static void BuildCatalog()
        {
            EnsureTextureFolder();
            EnsureResourcesFolder();

            var entries = new List<BlockTextureCatalog.Entry>();
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { TextureFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;

                string id = Path.GetFileNameWithoutExtension(path);
                entries.Add(new BlockTextureCatalog.Entry
                {
                    id = id,
                    displayName = FormatDisplayName(id),
                    sprite = sprite
                });
            }

            entries.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));

            BlockTextureCatalog catalog = AssetDatabase.LoadAssetAtPath<BlockTextureCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BlockTextureCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kaplao] Block texture catalog built with {entries.Count} entries at {CatalogAssetPath}.");
        }

        static bool TryPadTextureFile(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;

            importer.isReadable = true;
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (source == null)
                return false;

            int width = source.width;
            int height = source.height;
            if (width == height)
                return false;

            int size = Mathf.Max(width, height);
            var padded = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    padded.SetPixel(x, y, clear);
            }

            int offsetX = (size - width) / 2;
            int offsetY = (size - height) / 2;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    padded.SetPixel(offsetX + x, offsetY + y, source.GetPixel(x, y));
            }

            padded.Apply();
            File.WriteAllBytes(assetPath, padded.EncodeToPNG());
            Object.DestroyImmediate(padded);
            return true;
        }

        static string FormatDisplayName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return id;

            return id.Replace('_', ' ');
        }

        static void EnsureTextureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TextureFolder))
            {
                AssetDatabase.CreateFolder("Assets", "BlockTextures");
                Debug.Log($"[Kaplao] Created folder {TextureFolder}. Copy your PNG block textures there.");
            }
        }

        static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    public sealed class BlockTextureImportProcessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(BlockTexturePipeline.TextureFolder + "/"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 32;
        }
    }
}
#endif
