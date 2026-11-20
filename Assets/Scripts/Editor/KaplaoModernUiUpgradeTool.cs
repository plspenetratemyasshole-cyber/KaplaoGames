#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Server;
using KaplaoGames.UI;
using KaplaoGames.UI.Animation;
using KaplaoGames.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    /// <summary>
    /// Idempotent Upgrade Tool for MainMenu and SampleScene UI.
    /// Polishes all UI panels to the modern Deep Slate + Electric Cyan theme,
    /// sets up LeanTween animations, fixes masks and layouts, and preserves Inspector references.
    /// </summary>
    public static class KaplaoModernUiUpgradeTool
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/UI Overhaul/Upgrade BOTH Scenes (Full Polish)", priority = 0)]
        public static void UpgradeBothScenes()
        {
            Debug.Log("[Kaplao] Starting full UI Overhaul for both scenes...");
            KaplaoUiSprites.ExportSpritesToResources();
            UpgradeMainMenuScene();
            UpgradeSampleScene();
            Debug.Log("[Kaplao] UI Overhaul complete for BOTH scenes!");
            EditorUtility.DisplayDialog("Kaplao UI Overhaul", "Full UI polish complete for MainMenu and SampleScene!\nAll sprites, panels, inputs, and animations have been upgraded.", "Awesome!");
        }

        [MenuItem("Kaplao/UI Overhaul/1. Upgrade MainMenu Scene UI", priority = 10)]
        public static void UpgradeMainMenuScene()
        {
            Debug.Log("[Kaplao] Upgrading MainMenu scene UI...");
            KaplaoUiSprites.ExportSpritesToResources();

            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in MainMenu.");
                return;
            }

            EnsureRuntimeSkin(canvas);
            EnsureToast(canvas);

            PolishMainMenuPanels(canvas);
            KaplaoUiStyler.StyleHierarchy(canvas.transform);

            MainMenuSceneUiBinder.BindMainMenuUi();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] MainMenu scene UI upgrade complete and saved.");
        }

        [MenuItem("Kaplao/UI Overhaul/2. Upgrade SampleScene UI", priority = 20)]
        public static void UpgradeSampleScene()
        {
            Debug.Log("[Kaplao] Upgrading SampleScene UI...");
            KaplaoUiSprites.ExportSpritesToResources();

            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            EnsureRuntimeSkin(canvas);
            EnsureToast(canvas);

            PolishSampleScenePauseMenu(canvas);
            PolishSampleSceneModeration(canvas);
            PolishSampleSceneChat(canvas);
            PolishSampleSceneCreateMode(canvas);
            PolishSampleSceneCodeMode(canvas);
            PolishSampleSceneHud(canvas);

            KaplaoUiStyler.StyleHierarchy(canvas.transform);

            SampleSceneUiBinder.BindSampleSceneUi();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] SampleScene UI upgrade complete and saved.");
        }

        static void EnsureRuntimeSkin(Canvas canvas)
        {
            if (canvas == null) return;
            if (canvas.GetComponent<KaplaoUiRuntimeSkin>() == null)
                canvas.gameObject.AddComponent<KaplaoUiRuntimeSkin>();
        }

        static void EnsureToast(Canvas canvas)
        {
            if (canvas == null) return;
            KaplaoUiToast existing = canvas.GetComponentInChildren<KaplaoUiToast>(true);
            if (existing != null) return;

            GameObject root = new GameObject("KaplaoUiToastRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            root.AddComponent<KaplaoUiToast>();
        }

        #region MainMenu Polish

        static void PolishMainMenuPanels(Canvas canvas)
        {
            Transform root = canvas.transform;

            // 1. MainPanel
            Transform mainPanel = root.Find("MainPanel");
            if (mainPanel != null)
            {
                StyleButtonIfExists(mainPanel, "SinglePlayerButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(mainPanel, "NetworkGameButton", KaplaoButtonVariant.Secondary);
                StyleButtonIfExists(mainPanel, "SettingsButton", KaplaoButtonVariant.Secondary);
                StyleButtonIfExists(mainPanel, "ExitButton", KaplaoButtonVariant.Ghost);
                StyleButtonIfExists(mainPanel, "BrowseLevelsButton", KaplaoButtonVariant.Secondary);
            }

            // 2. SinglePlayerPanel
            Transform singlePlayerPanel = root.Find("SinglePlayerPanel");
            if (singlePlayerPanel != null)
            {
                KaplaoUiStyler.StylePanel(singlePlayerPanel.gameObject, true, true);

                // ScrollView Viewport + Content
                Transform scrollView = singlePlayerPanel.Find("WorldScrollView");
                if (scrollView != null)
                {
                    ScrollRect sr = scrollView.GetComponent<ScrollRect>();
                    if (sr != null)
                        KaplaoUiStyler.StyleScrollView(sr);

                    Transform viewport = scrollView.Find("Viewport");
                    if (viewport != null)
                    {
                        EnsureRectMask2D(viewport.gameObject);

                        Transform content = viewport.Find("Content");
                        if (content != null)
                        {
                            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                            layout.spacing = 8f;
                            layout.padding = new RectOffset(6, 6, 8, 8);
                            layout.childAlignment = TextAnchor.UpperCenter;
                            layout.childControlWidth = true;
                            layout.childControlHeight = false;
                            layout.childForceExpandWidth = true;
                            layout.childForceExpandHeight = false;

                            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        }
                    }
                }

                // WorldEntryTemplate
                Transform entryTemplate = singlePlayerPanel.Find("WorldScrollView/Viewport/Content/WorldEntryTemplate");
                if (entryTemplate != null)
                {
                    RectTransform ert = entryTemplate.GetComponent<RectTransform>();
                    ert.sizeDelta = new Vector2(ert.sizeDelta.x, 64f);

                    LayoutElement le = entryTemplate.GetComponent<LayoutElement>() ?? entryTemplate.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 64f;
                    le.minHeight = 64f;
                    le.flexibleWidth = 1f;

                    if (entryTemplate.GetComponent<WorldEntryView>() == null)
                        entryTemplate.gameObject.AddComponent<WorldEntryView>();

                    KaplaoUiStyler.StyleCardRow(entryTemplate.gameObject);

                    Transform title = entryTemplate.Find("Title");
                    if (title != null)
                    {
                        RectTransform trt = title.GetComponent<RectTransform>();
                        trt.anchorMin = new Vector2(0f, 0.45f);
                        trt.anchorMax = new Vector2(1f, 1f);
                        trt.offsetMin = new Vector2(18f, 0f);
                        trt.offsetMax = new Vector2(-18f, -6f);

                        TextMeshProUGUI tmp = title.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.fontSize = 18f;
                            tmp.fontStyle = FontStyles.Bold;
                            tmp.color = KaplaoUiTheme.Current.TextPrimary;
                            tmp.alignment = TextAlignmentOptions.MidlineLeft;
                        }
                    }

                    Transform subtitle = entryTemplate.Find("Subtitle");
                    if (subtitle == null)
                    {
                        GameObject subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                        subGo.transform.SetParent(entryTemplate, false);
                        subtitle = subGo.transform;
                    }

                    RectTransform srt = subtitle.GetComponent<RectTransform>();
                    srt.anchorMin = new Vector2(0f, 0f);
                    srt.anchorMax = new Vector2(1f, 0.45f);
                    srt.offsetMin = new Vector2(18f, 6f);
                    srt.offsetMax = new Vector2(-18f, 0f);

                    TextMeshProUGUI subTmp = subtitle.GetComponent<TextMeshProUGUI>();
                    if (subTmp != null)
                    {
                        subTmp.fontSize = 13f;
                        subTmp.color = KaplaoUiTheme.Current.TextSecondary;
                        subTmp.alignment = TextAlignmentOptions.MidlineLeft;
                    }
                }

                // Action Buttons
                StyleButtonIfExists(singlePlayerPanel, "WorldActionsBar/PlayButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(singlePlayerPanel, "WorldActionsBar/DeleteButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(singlePlayerPanel, "BottomBar/CreateNewWorldButton", KaplaoButtonVariant.Success);
                StyleButtonIfExists(singlePlayerPanel, "BottomBar/OpenSavesFolderButton", KaplaoButtonVariant.Secondary);
                StyleButtonIfExists(singlePlayerPanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 3. SettingsPanel
            Transform settingsPanel = root.Find("SettingsPanel");
            if (settingsPanel != null)
            {
                RectTransform srt = settingsPanel.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Vector2.zero;
                srt.sizeDelta = new Vector2(760f, 680f);

                KaplaoUiStyler.StylePanel(settingsPanel.gameObject, true, true);

                // Title
                Transform title = settingsPanel.Find("Title");
                SetAnchored(title, new Vector2(0f, 300f), new Vector2(400f, 40f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 28f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.color = KaplaoUiTheme.Current.TextPrimary;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                // Section 1: Account & Profile
                SetAnchored(settingsPanel.Find("NicknameLabel"), new Vector2(-200f, 240f), new Vector2(120f, 32f));
                SetAnchored(settingsPanel.Find("NicknameInput"), new Vector2(60f, 240f), new Vector2(360f, 36f));
                SetAnchored(settingsPanel.Find("AccountEmailLabel"), new Vector2(0f, 200f), new Vector2(500f, 24f));
                SetAnchored(settingsPanel.Find("SignInButton"), new Vector2(-80f, 160f), new Vector2(140f, 36f));
                SetAnchored(settingsPanel.Find("SignOutButton"), new Vector2(80f, 160f), new Vector2(140f, 36f));

                StyleButtonIfExists(settingsPanel, "SignInButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(settingsPanel, "SignOutButton", KaplaoButtonVariant.Ghost);

                // Section 2: Runner Skin
                SetAnchored(settingsPanel.Find("SkinLabel"), new Vector2(-200f, 105f), new Vector2(120f, 30f));
                SetAnchored(settingsPanel.Find("SkinRow"), new Vector2(0f, 55f));

                // Section 3: Audio & Language
                SetAnchored(settingsPanel.Find("MusicPackLabel"), new Vector2(-200f, -5f), new Vector2(120f, 30f));
                SetAnchored(settingsPanel.Find("MusicPackDropdown"), new Vector2(-60f, -5f), new Vector2(150f, 34f));
                SetAnchored(settingsPanel.Find("LanguageDropdown"), new Vector2(140f, -5f), new Vector2(150f, 34f));

                SetAnchored(settingsPanel.Find("MusicVolumeLabel"), new Vector2(-220f, -45f), new Vector2(90f, 24f));
                SetAnchored(settingsPanel.Find("MusicVolumeSlider"), new Vector2(-80f, -45f), new Vector2(160f, 20f));
                SetAnchored(settingsPanel.Find("SfxVolumeLabel"), new Vector2(40f, -45f), new Vector2(90f, 24f));
                SetAnchored(settingsPanel.Find("SfxVolumeSlider"), new Vector2(180f, -45f), new Vector2(160f, 20f));

                // Section 4: Network
                SetAnchored(settingsPanel.Find("NetworkDropdown"), new Vector2(-100f, -95f), new Vector2(240f, 34f));
                SetAnchored(settingsPanel.Find("TransportDropdown"), new Vector2(150f, -95f), new Vector2(200f, 34f));
                SetAnchored(settingsPanel.Find("PortLabel"), new Vector2(-160f, -140f), new Vector2(80f, 30f));
                SetAnchored(settingsPanel.Find("PortInput"), new Vector2(20f, -140f), new Vector2(160f, 34f));

                // Footer BackButton
                SetAnchored(settingsPanel.Find("BackButton"), new Vector2(0f, -305f), new Vector2(240f, 42f));
                StyleButtonIfExists(settingsPanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 4. NetworkPanel
            Transform networkPanel = root.Find("NetworkPanel");
            if (networkPanel != null)
            {
                KaplaoUiStyler.StylePanel(networkPanel.gameObject, true, true);
                StyleButtonIfExists(networkPanel, "ConnectDirectlyButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(networkPanel, "RefreshButton", KaplaoButtonVariant.Secondary);
                StyleButtonIfExists(networkPanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 5. CreateWorldNamePanel
            Transform createWorldPanel = root.Find("CreateWorldNamePanel");
            if (createWorldPanel != null)
            {
                KaplaoUiStyler.StylePanel(createWorldPanel.gameObject, true, true);
                StyleButtonIfExists(createWorldPanel, "ContinueButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(createWorldPanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 6. NicknamePanel
            Transform nicknamePanel = root.Find("NicknamePanel");
            if (nicknamePanel != null)
            {
                KaplaoUiStyler.StylePanel(nicknamePanel.gameObject, true, true);
                StyleButtonIfExists(nicknamePanel, "ContinueButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(nicknamePanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 7. DeleteWorldConfirmPanel
            Transform deletePanel = root.Find("DeleteWorldConfirmPanel");
            if (deletePanel != null)
            {
                Transform dialog = deletePanel.Find("DialogBox") ?? deletePanel;
                KaplaoUiStyler.StylePanel(dialog.gameObject, true, true);
                StyleButtonIfExists(deletePanel, "DialogBox/ConfirmDeleteButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(deletePanel, "DialogBox/CancelButton", KaplaoButtonVariant.Ghost);
            }

            // 8. BrowseLevelsPanel
            Transform browsePanel = root.Find("BrowseLevelsPanel");
            if (browsePanel != null)
            {
                KaplaoUiStyler.StylePanel(browsePanel.gameObject, true, true);

                Transform browseScroll = browsePanel.Find("BrowseScroll");
                if (browseScroll != null)
                {
                    EnsureRectMask2D(browseScroll.gameObject);
                    Transform content = browseScroll.Find("BrowseContent");
                    if (content != null)
                    {
                        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    }
                }

                StyleButtonIfExists(browsePanel, "BrowsePlayButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(browsePanel, "BrowseCopyButton", KaplaoButtonVariant.Secondary);
                StyleButtonIfExists(browsePanel, "BrowseDeleteFromServersButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(browsePanel, "BrowseBackButton", KaplaoButtonVariant.Ghost);

                Transform delConfirm = browsePanel.Find("BrowseDeleteConfirmPanel");
                if (delConfirm != null)
                {
                    KaplaoUiStyler.StylePanel(delConfirm.gameObject, true, true);
                    StyleButtonIfExists(delConfirm, "BrowseDeleteConfirmButton", KaplaoButtonVariant.Danger);
                    StyleButtonIfExists(delConfirm, "BrowseDeleteCancelButton", KaplaoButtonVariant.Ghost);
                }
            }
        }

        #endregion

        #region SampleScene Polish

        static void PolishSampleScenePauseMenu(Canvas canvas)
        {
            Transform pauseRoot = canvas.transform.Find("PauseMenuRoot");
            if (pauseRoot != null)
            {
                Transform dim = pauseRoot.Find("DimBackground");
                if (dim != null && dim.TryGetComponent(out Image dimImg))
                    KaplaoUiStyler.StyleBackdrop(dimImg);

                Transform pausePanel = pauseRoot.Find("PauseMenuPanel");
                if (pausePanel != null)
                {
                    // Reparent PublishLevelPanel if incorrectly nested
                    Transform nestedPublish = pausePanel.Find("PublishLevelPanel");
                    if (nestedPublish != null)
                        nestedPublish.SetParent(pauseRoot, false);

                    VerticalLayoutGroup vlg = pausePanel.GetComponent<VerticalLayoutGroup>() ?? pausePanel.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.MiddleCenter;
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 10f;
                    vlg.padding = new RectOffset(24, 24, 24, 24);

                    ContentSizeFitter csf = pausePanel.GetComponent<ContentSizeFitter>() ?? pausePanel.gameObject.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    KaplaoUiStyler.StylePanel(pausePanel.gameObject, true, true);

                    StyleMenuButtonWithLayout(pausePanel, "ResumeButton", KaplaoButtonVariant.Primary);
                    StyleMenuButtonWithLayout(pausePanel, "SaveButton", KaplaoButtonVariant.Success);
                    StyleMenuButtonWithLayout(pausePanel, "SaveAndExitButton", KaplaoButtonVariant.Success);
                    StyleMenuButtonWithLayout(pausePanel, "ExitButton", KaplaoButtonVariant.Danger);
                    StyleMenuButtonWithLayout(pausePanel, "OpenToLanButton", KaplaoButtonVariant.Secondary);
                    StyleMenuButtonWithLayout(pausePanel, "PublishButton", KaplaoButtonVariant.Secondary);
                }

                Transform saveSuccess = pauseRoot.Find("SaveSuccessPanel");
                if (saveSuccess != null)
                {
                    KaplaoUiStyler.StylePanel(saveSuccess.gameObject, true, true);
                    StyleButtonIfExists(saveSuccess, "CloseButton", KaplaoButtonVariant.Ghost);
                }

                Transform exitConfirm = pauseRoot.Find("ExitWithoutSavePanel");
                if (exitConfirm != null)
                {
                    KaplaoUiStyler.StylePanel(exitConfirm.gameObject, true, true);
                    StyleButtonIfExists(exitConfirm, "ConfirmExitButton", KaplaoButtonVariant.Danger);
                    StyleButtonIfExists(exitConfirm, "CancelButton", KaplaoButtonVariant.Ghost);
                }

                Transform lanPanel = pauseRoot.Find("OpenToLanPanel");
                if (lanPanel != null)
                {
                    KaplaoUiStyler.StylePanel(lanPanel.gameObject, true, true);
                    StyleButtonIfExists(lanPanel, "ContinueButton", KaplaoButtonVariant.Primary);
                    StyleButtonIfExists(lanPanel, "CancelButton", KaplaoButtonVariant.Ghost);
                }

                Transform publishPanel = pauseRoot.Find("PublishLevelPanel");
                if (publishPanel != null)
                {
                    KaplaoUiStyler.StylePanel(publishPanel.gameObject, true, true);
                    StyleButtonIfExists(publishPanel, "PublishConfirmButton", KaplaoButtonVariant.Primary);
                    StyleButtonIfExists(publishPanel, "PublishCancelButton", KaplaoButtonVariant.Ghost);
                }
            }

            Transform runnerRoot = canvas.transform.Find("RunnerPauseMenuRoot");
            if (runnerRoot != null)
            {
                Transform dim = runnerRoot.Find("DimBackground");
                if (dim != null && dim.TryGetComponent(out Image dimImg))
                    KaplaoUiStyler.StyleBackdrop(dimImg);

                Transform runnerPanel = runnerRoot.Find("RunnerPauseMenuPanel");
                if (runnerPanel != null)
                {
                    VerticalLayoutGroup vlg = runnerPanel.GetComponent<VerticalLayoutGroup>() ?? runnerPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.MiddleCenter;
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 10f;
                    vlg.padding = new RectOffset(20, 20, 24, 24);

                    ContentSizeFitter csf = runnerPanel.GetComponent<ContentSizeFitter>() ?? runnerPanel.gameObject.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    KaplaoUiStyler.StylePanel(runnerPanel.gameObject, true, true);

                    StyleMenuButtonWithLayout(runnerPanel, "ResumeButton", KaplaoButtonVariant.Primary);
                    StyleMenuButtonWithLayout(runnerPanel, "ExitButton", KaplaoButtonVariant.Danger);
                }
            }
        }

        static void PolishSampleSceneModeration(Canvas canvas)
        {
            Transform hostPanel = canvas.transform.Find("PausePlayerListPanel") ?? canvas.transform.Find("HostModerationPanel");
            if (hostPanel != null)
            {
                KaplaoUiStyler.StylePanel(hostPanel.gameObject, true, true);
                StyleButtonIfExists(hostPanel, "ModerateChatButton", KaplaoButtonVariant.Primary);
            }

            Transform bannedPanel = canvas.transform.Find("BannedPlayersPanel");
            if (bannedPanel != null)
            {
                KaplaoUiStyler.StylePanel(bannedPanel.gameObject, true, true);

                Transform scroll = bannedPanel.Find("BannedScroll");
                if (scroll != null)
                {
                    RemoveLegacyMask(scroll.gameObject);
                    Transform viewport = scroll.Find("Viewport");
                    if (viewport != null)
                    {
                        RemoveLegacyMask(viewport.gameObject);
                        EnsureRectMask2D(viewport.gameObject);
                    }
                }
            }

            Transform modPanel = canvas.transform.Find("ModeratorModerationPanel");
            if (modPanel != null)
            {
                KaplaoUiStyler.StylePanel(modPanel.gameObject, true, true);
                StyleButtonIfExists(modPanel, "AcceptButton", KaplaoButtonVariant.Success);
                StyleButtonIfExists(modPanel, "RejectButton", KaplaoButtonVariant.Ghost);
                StyleButtonIfExists(modPanel, "KickButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(modPanel, "BanButton", KaplaoButtonVariant.Danger);
            }
        }

        static void PolishSampleSceneChat(Canvas canvas)
        {
            Transform chatRoot = canvas.transform.Find("ModeratedChatUiRoot");
            if (chatRoot != null)
            {
                RectTransform crt = chatRoot.GetComponent<RectTransform>();
                crt.anchorMin = Vector2.zero;
                crt.anchorMax = Vector2.zero;
                crt.pivot = Vector2.zero;
                crt.anchoredPosition = new Vector2(18f, 18f);
                crt.sizeDelta = new Vector2(420f, 300f);

                Transform chatScroll = chatRoot.Find("ChatPanel/ChatScroll");
                if (chatScroll != null)
                {
                    Transform viewport = chatScroll.Find("Viewport");
                    if (viewport != null)
                    {
                        RemoveLegacyMask(viewport.gameObject);
                        EnsureRectMask2D(viewport.gameObject);
                    }
                }
            }

            Transform devChat = canvas.transform.Find("DeveloperChatCanvas") ?? canvas.transform.Find("DeveloperChat");
            if (devChat != null)
            {
                Transform textArea = devChat.Find("ChatPanel/InputContainer/ChatInput/Text Area");
                if (textArea != null)
                    EnsureRectMask2D(textArea.gameObject);
            }
        }

        static void PolishSampleSceneCreateMode(Canvas canvas)
        {
            Transform createMode = canvas.transform.Find("CreateModeUI");
            if (createMode == null) return;

            StyleButtonIfExists(createMode, "UsualModeButton", KaplaoButtonVariant.Secondary);
            StyleButtonIfExists(createMode, "CreateModeButton", KaplaoButtonVariant.Primary);
            StyleButtonIfExists(createMode, "CodeModeButton", KaplaoButtonVariant.Secondary);
            StyleButtonIfExists(createMode, "CreateSettingsButton", KaplaoButtonVariant.Secondary);

            Transform toolbar = createMode.Find("SelectionToolbar");
            if (toolbar != null)
            {
                StyleButtonIfExists(toolbar, "DeleteButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(toolbar, "DeselectButton", KaplaoButtonVariant.Ghost);
            }

            // SettingsPanel (Tile Settings)
            Transform settingsPanel = createMode.Find("SettingsPanel");
            if (settingsPanel != null)
            {
                RectTransform rt = settingsPanel.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(440f, 480f);
                KaplaoUiStyler.StylePanel(settingsPanel.gameObject, true, true);
                StyleButtonIfExists(settingsPanel, "ApplyTransformButton", KaplaoButtonVariant.Success);
                StyleButtonIfExists(settingsPanel, "SettingsCloseButton", KaplaoButtonVariant.Ghost);
            }

            // CreateSettingsPanel
            Transform createSettings = createMode.Find("CreateSettingsPanel");
            if (createSettings != null)
            {
                RectTransform rt = createSettings.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(380f, 620f);
                KaplaoUiStyler.StylePanel(createSettings.gameObject, true, true);
                StyleButtonIfExists(createSettings, "CreateSettingsSaveButton", KaplaoButtonVariant.Success);
                StyleButtonIfExists(createSettings, "CreateSettingsCloseButton", KaplaoButtonVariant.Ghost);
            }

            // Other dialogs
            string[] panels = new[]
            {
                "SetGroupPanel",
                "TileColorPanel",
                "ParticleEditorPanel",
                "KeyframeEditorPanel",
                "ExecutorEditorPanel"
            };

            for (int i = 0; i < panels.Length; i++)
            {
                Transform p = createMode.Find(panels[i]);
                if (p != null)
                    KaplaoUiStyler.StylePanel(p.gameObject, true, true);
            }
        }

        static void PolishSampleSceneCodeMode(Canvas canvas)
        {
            Transform codeMode = canvas.transform.Find("CodeModeOverlay") ?? canvas.transform.Find("CodeModeIde");
            if (codeMode != null)
            {
                KaplaoUiStyler.StylePanel(codeMode.gameObject, true, false);
            }
        }

        static void PolishSampleSceneHud(Canvas canvas)
        {
            Transform ping = canvas.transform.Find("PingDisplay") ?? canvas.transform.Find("NetworkPingDisplay");
            if (ping != null)
            {
                if (ping.TryGetComponent(out Image img))
                {
                    img.sprite = KaplaoUiSprites.Pill();
                    img.color = new Color(0.06f, 0.09f, 0.16f, 0.85f);
                }
            }
        }

        #endregion

        #region Helpers

        static void StyleButtonIfExists(Transform parent, string relativePath, KaplaoButtonVariant variant)
        {
            if (parent == null) return;
            Transform t = parent.Find(relativePath);
            if (t != null && t.TryGetComponent(out Button btn))
                KaplaoUiStyler.StyleButton(btn, variant);
        }

        static void StyleMenuButtonWithLayout(Transform parent, string name, KaplaoButtonVariant variant)
        {
            Transform t = parent.Find(name);
            if (t == null) return;

            if (t.TryGetComponent(out Button btn))
                KaplaoUiStyler.StyleButton(btn, variant);

            LayoutElement le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 280f;
            le.preferredHeight = 44f;
            le.minWidth = 280f;
            le.minHeight = 44f;
        }

        static void SetAnchored(Transform t, Vector2 pos, Vector2? size = null)
        {
            if (t == null) return;
            if (t is RectTransform rt)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                if (size.HasValue)
                    rt.sizeDelta = size.Value;
            }
        }

        static void EnsureRectMask2D(GameObject target)
        {
            if (target == null) return;
            RemoveLegacyMask(target);
            if (target.GetComponent<RectMask2D>() == null)
                target.AddComponent<RectMask2D>();
        }

        static void RemoveLegacyMask(GameObject target)
        {
            if (target == null) return;
            Mask mask = target.GetComponent<Mask>();
            if (mask != null)
                UnityEngine.Object.DestroyImmediate(mask);
        }

        #endregion
    }
}
#endif
