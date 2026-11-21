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
    /// Correct execution order: Base StyleHierarchy FIRST -> Specific variants & custom panels SECOND.
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
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Kaplao UI Overhaul", "Full UI polish complete for MainMenu and SampleScene!\nAll sprites, panels, inputs, and animations have been upgraded.", "Awesome!");
        }

        public static void UpgradeBothScenesBatch()
        {
            Debug.Log("[Kaplao] Starting batch UI Overhaul for both scenes...");
            KaplaoUiSprites.ExportSpritesToResources();
            UpgradeMainMenuScene();
            UpgradeSampleScene();
            Debug.Log("[Kaplao] Batch UI Overhaul complete for BOTH scenes!");
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
            EnsureMenuAtmosphere(canvas);

            Transform mainPanel = canvas.transform.Find("MainPanel");
            if (mainPanel != null)
                EnsureProfileBadge(mainPanel);

            // 1. BASE STYLING FIRST (force replace procedural sprites on standard elements)
            KaplaoUiStyler.StyleHierarchy(canvas.transform, forceReplaceSprites: true);

            // 2. SPECIFIC VARIANTS & REBUILD
            PolishMainMenuPanels(canvas);

            // 3. ATTACH PANEL ANIMATORS
            AttachPanelAnimator(canvas.transform.Find("SinglePlayerPanel"), PanelAnimStyle.SlideFromBottom);
            AttachPanelAnimator(canvas.transform.Find("NetworkPanel"), PanelAnimStyle.SlideFromBottom);
            AttachPanelAnimator(canvas.transform.Find("SettingsPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("NicknamePanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("CreateWorldNamePanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("DeleteWorldConfirmPanel/DialogBox") ?? canvas.transform.Find("DeleteWorldConfirmPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("BrowseLevelsPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("BrowseLevelsPanel/BrowseDeleteConfirmPanel"), PanelAnimStyle.PopScale);

            // Cleanups: remove button glows, refresh shadows, clear scroll viewports
            RemoveAllButtonGlows(canvas.transform);
            RefreshAllShadows(canvas.transform);
            CleanAllScrollViewports(canvas.transform);

            // 4. BIND UI REFERENCES
            MainMenuSceneUiBinder.BindMainMenuUi(canvas, scene);

            // 5. GUARANTEE PANEL VISIBILITY STATES
            SetPanelActive(canvas.transform.Find("MainPanel"), true);
            SetPanelActive(canvas.transform.Find("SettingsPanel"), false);
            SetPanelActive(canvas.transform.Find("SinglePlayerPanel"), false);
            SetPanelActive(canvas.transform.Find("NetworkPanel"), false);
            SetPanelActive(canvas.transform.Find("CreateWorldNamePanel"), false);
            SetPanelActive(canvas.transform.Find("NicknamePanel"), false);
            SetPanelActive(canvas.transform.Find("DeleteWorldConfirmPanel"), false);
            SetPanelActive(canvas.transform.Find("BrowseLevelsPanel"), false);
            SetPanelActive(canvas.transform.Find("KaplaoUiToastRoot"), false);

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
            EnsureHudComponents(canvas);

            // 1. BASE STYLING FIRST (force replace procedural sprites on standard elements)
            KaplaoUiStyler.StyleHierarchy(canvas.transform, forceReplaceSprites: true);

            // 2. SPECIFIC VARIANTS & REBUILD
            PolishSampleScenePauseMenu(canvas);
            PolishSampleSceneModeration(canvas);
            PolishSampleSceneChat(canvas);
            PolishSampleSceneCreateMode(canvas);
            PolishSampleSceneCodeMode(canvas);
            PolishSampleSceneHud(canvas);

            // Restore all art sprites / tool slots / icons
            RestoreSampleSceneIcons(canvas, scene);

            // 3. ATTACH PANEL ANIMATORS
            AttachPanelAnimator(canvas.transform.Find("PauseMenuRoot/PauseMenuPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("PauseMenuRoot/SaveSuccessPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("PauseMenuRoot/ExitWithoutSavePanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("PauseMenuRoot/OpenToLanPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("PauseMenuRoot/PublishLevelPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("RunnerPauseMenuRoot/RunnerPauseMenuPanel") ?? canvas.transform.Find("RunnerPauseMenuRoot/PauseMenuPanel"), PanelAnimStyle.PopScale);

            Transform createMode = canvas.transform.Find("CreateModeUI");
            if (createMode != null)
            {
                AttachPanelAnimator(createMode.Find("SelectionToolbar"), PanelAnimStyle.SlideFromBottom);
                AttachPanelAnimator(createMode.Find("SettingsPanel"), PanelAnimStyle.PopScale);
                AttachPanelAnimator(createMode.Find("CreateSettingsPanel"), PanelAnimStyle.PopScale);
                AttachPanelAnimator(createMode.Find("SetGroupPanel"), PanelAnimStyle.PopScale);
                AttachPanelAnimator(createMode.Find("TileColorPanel"), PanelAnimStyle.PopScale);
                AttachPanelAnimator(createMode.Find("ParticleEditorPanel"), PanelAnimStyle.PopScale);
                AttachPanelAnimator(createMode.Find("KeyframeEditorPanel"), PanelAnimStyle.PopScale);
                AttachPanelAnimator(createMode.Find("ExecutorEditorPanel"), PanelAnimStyle.PopScale);
            }

            AttachPanelAnimator(canvas.transform.Find("HostModerationPanel") ?? canvas.transform.Find("PausePlayerListPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("BannedPlayersPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("ModeratorModerationPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("DocumentationPanel"), PanelAnimStyle.PopScale);
            AttachPanelAnimator(canvas.transform.Find("CantChangeModePanel"), PanelAnimStyle.PopScale);

            // Cleanups: remove button glows, refresh shadows, clear scroll viewports
            RemoveAllButtonGlows(canvas.transform);
            RefreshAllShadows(canvas.transform);
            CleanAllScrollViewports(canvas.transform);

            // 4. BIND UI REFERENCES
            SampleSceneUiBinder.BindSampleSceneUi(canvas, scene);

            // 5. GUARANTEE INACTIVE PANELS ARE SAVED WITH active = false
            SetPanelActive(canvas.transform.Find("PauseMenuRoot"), false);
            SetPanelActive(canvas.transform.Find("RunnerPauseMenuRoot"), false);
            SetPanelActive(canvas.transform.Find("HostModerationPanel") ?? FindDeepChild(canvas.transform, "HostModerationPanel"), false);
            SetPanelActive(canvas.transform.Find("BannedPlayersPanel") ?? FindDeepChild(canvas.transform, "BannedPlayersPanel"), false);
            SetPanelActive(canvas.transform.Find("ModeratorModerationPanel"), false);
            SetPanelActive(canvas.transform.Find("DocumentationPanel"), false);
            SetPanelActive(canvas.transform.Find("CantChangeModePanel"), false);
            SetPanelActive(canvas.transform.Find("KaplaoUiToastRoot"), false);

            if (createMode != null)
            {
                SetPanelActive(createMode.Find("SelectionToolbar"), false);
                SetPanelActive(createMode.Find("SettingsPanel"), false);
                SetPanelActive(createMode.Find("CreateSettingsPanel"), false);
                SetPanelActive(createMode.Find("SetGroupPanel"), false);
                SetPanelActive(createMode.Find("TileColorPanel"), false);
                SetPanelActive(createMode.Find("ParticleEditorPanel"), false);
                SetPanelActive(createMode.Find("KeyframeEditorPanel"), false);
                SetPanelActive(createMode.Find("ExecutorEditorPanel"), false);
            }

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
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                return;
            }

            GameObject root = new GameObject("KaplaoUiToastRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            root.AddComponent<KaplaoUiToast>();
            root.SetActive(false);
        }

        static void EnsureMenuAtmosphere(Canvas canvas)
        {
            if (canvas == null) return;
            Transform existing = canvas.transform.Find("MenuAtmosphereLayer");
            if (existing == null)
            {
                GameObject go = new GameObject("MenuAtmosphereLayer", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                go.transform.SetAsFirstSibling();
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.AddComponent<KaplaoMenuBackgroundFx>();
            }
        }

        static void EnsureProfileBadge(Transform mainPanel)
        {
            if (mainPanel == null) return;
            Transform existing = mainPanel.Find("ProfileBadge");
            if (existing == null)
            {
                GameObject badgeObj = new GameObject("ProfileBadge", typeof(RectTransform));
                badgeObj.transform.SetParent(mainPanel, false);
                badgeObj.AddComponent<KaplaoProfileBadgeUi>();
            }
        }

        static void EnsureHudComponents(Canvas canvas)
        {
            if (canvas == null) return;

            NetworkPingDisplay ping = canvas.GetComponentInChildren<NetworkPingDisplay>(true);
            if (ping == null)
            {
                canvas.gameObject.AddComponent<NetworkPingDisplay>();
            }

            // Remove KaplaoModeBannerHud and any banner object (banner is no longer used)
            KaplaoModeBannerHud[] banners = canvas.GetComponentsInChildren<KaplaoModeBannerHud>(true);
            for (int i = 0; i < banners.Length; i++)
            {
                if (banners[i] != null)
                    UnityEngine.Object.DestroyImmediate(banners[i]);
            }

            Transform bannerObj = canvas.transform.Find("KaplaoModeBanner");
            if (bannerObj != null)
            {
                UnityEngine.Object.DestroyImmediate(bannerObj.gameObject);
            }
        }

        static void AttachPanelAnimator(Transform t, PanelAnimStyle style)
        {
            if (t == null) return;
            KaplaoPanelAnimator anim = t.GetComponent<KaplaoPanelAnimator>();
            if (anim == null)
                anim = t.gameObject.AddComponent<KaplaoPanelAnimator>();
            anim.Style = style;
        }

        #region MainMenu Polish

        static void PolishMainMenuPanels(Canvas canvas)
        {
            Transform root = canvas.transform;

            // 1. MainPanel
            Transform mainPanel = root.Find("MainPanel");
            if (mainPanel != null)
            {
                StripRobloxStyleRectAndResetScale(mainPanel);

                // Group buttons in MainMenuButtonsGroup
                Transform group = mainPanel.Find("MainMenuButtonsGroup");
                if (group == null)
                {
                    GameObject groupObj = new GameObject("MainMenuButtonsGroup", typeof(RectTransform));
                    groupObj.transform.SetParent(mainPanel, false);
                    group = groupObj.transform;
                }

                RectTransform grt = group.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(0.5f, 0.5f);
                grt.anchorMax = new Vector2(0.5f, 0.5f);
                grt.pivot = new Vector2(0.5f, 0.5f);
                grt.anchoredPosition = new Vector2(0f, -60f);
                grt.localScale = Vector3.one;

                // Glass card background
                KaplaoUiStyler.StylePanel(group.gameObject, true, true);
                if (group.TryGetComponent(out Image gImg))
                {
                    gImg.sprite = KaplaoUiSprites.RoundedRect(18);
                    gImg.color = new Color(KaplaoUiTheme.Current.SurfacePrimary.r, KaplaoUiTheme.Current.SurfacePrimary.g, KaplaoUiTheme.Current.SurfacePrimary.b, 0.78f);
                }

                VerticalLayoutGroup vlg = group.GetComponent<VerticalLayoutGroup>() ?? group.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(24, 24, 24, 24);
                vlg.spacing = 12f;
                vlg.childAlignment = TextAnchor.MiddleCenter;
                vlg.childControlWidth = false;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;

                ContentSizeFitter csf = group.GetComponent<ContentSizeFitter>() ?? group.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Reparent buttons into MainMenuButtonsGroup in order
                string[] buttonOrder = { "SinglePlayerButton", "NetworkGameButton", "BrowseLevelsButton", "SettingsButton", "ExitButton" };
                for (int i = 0; i < buttonOrder.Length; i++)
                {
                    Transform btnT = mainPanel.Find(buttonOrder[i]) ?? group.Find(buttonOrder[i]);
                    if (btnT != null)
                    {
                        btnT.SetParent(group, false);
                        btnT.localScale = Vector3.one;
                        float btnH = (buttonOrder[i] == "ExitButton") ? 44f : 48f;
                        float fontS = (buttonOrder[i] == "ExitButton") ? 17f : 18f;
                        KaplaoButtonVariant variant = (buttonOrder[i] == "SinglePlayerButton") ? KaplaoButtonVariant.Primary :
                                                      (buttonOrder[i] == "ExitButton") ? KaplaoButtonVariant.Danger : KaplaoButtonVariant.Secondary;

                        StyleMenuButtonWithLayout(group, buttonOrder[i], variant, 280f, btnH);

                        var tmp = btnT.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (tmp != null)
                        {
                            tmp.fontSize = fontS;
                            tmp.fontStyle = FontStyles.Bold;
                        }
                    }
                }

                // ProfileBadge
                Transform badge = mainPanel.Find("ProfileBadge");
                if (badge != null)
                {
                    RectTransform brt = badge.GetComponent<RectTransform>();
                    brt.anchorMin = new Vector2(1f, 1f);
                    brt.anchorMax = new Vector2(1f, 1f);
                    brt.pivot = new Vector2(1f, 1f);
                    brt.anchoredPosition = new Vector2(-24f, -84f);
                    brt.localScale = Vector3.one;
                }
            }

            // 2. SinglePlayerPanel
            Transform singlePlayerPanel = root.Find("SinglePlayerPanel");
            if (singlePlayerPanel != null)
            {
                StripRobloxStyleRectAndResetScale(singlePlayerPanel);

                RectTransform srt = singlePlayerPanel.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Vector2.zero;
                srt.sizeDelta = new Vector2(780f, 620f);
                srt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(singlePlayerPanel.gameObject, true, true);

                Transform title = singlePlayerPanel.Find("Title");
                SetAnchored(title, new Vector2(0f, 260f), new Vector2(400f, 36f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 22f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                Transform title2 = singlePlayerPanel.Find("Title2");
                if (title2 != null)
                    title2.gameObject.SetActive(false);

                Transform scrollView = singlePlayerPanel.Find("WorldScrollView");
                if (scrollView != null)
                {
                    RectTransform svRt = scrollView.GetComponent<RectTransform>();
                    svRt.anchorMin = new Vector2(0.5f, 0.5f);
                    svRt.anchorMax = new Vector2(0.5f, 0.5f);
                    svRt.pivot = new Vector2(0.5f, 0.5f);
                    svRt.anchoredPosition = new Vector2(0f, 20f);
                    svRt.sizeDelta = new Vector2(710f, 370f);
                    svRt.localScale = Vector3.one;

                    ScrollRect sr = scrollView.GetComponent<ScrollRect>();
                    if (sr != null)
                        KaplaoUiStyler.StyleScrollView(sr);

                    Transform viewport = scrollView.Find("Viewport");
                    if (viewport != null)
                    {
                        EnsureRectMask2D(viewport.gameObject);
                        if (viewport.TryGetComponent(out Image vpImg))
                        {
                            vpImg.color = Color.clear;
                            vpImg.raycastTarget = false;
                        }

                        Transform content = viewport.Find("Content");
                        if (content != null)
                        {
                            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                            layout.spacing = 8f;
                            layout.padding = new RectOffset(8, 8, 8, 8);
                            layout.childAlignment = TextAnchor.UpperCenter;
                            layout.childControlWidth = true;
                            layout.childControlHeight = false;
                            layout.childForceExpandWidth = true;
                            layout.childForceExpandHeight = false;

                            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                        }
                    }

                    // Empty state label
                    Transform empty = scrollView.Find("EmptyStateLabel");
                    if (empty == null)
                    {
                        GameObject emptyObj = new GameObject("EmptyStateLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                        emptyObj.transform.SetParent(scrollView, false);
                        empty = emptyObj.transform;
                    }
                    RectTransform ert = empty.GetComponent<RectTransform>();
                    ert.anchorMin = Vector2.zero;
                    ert.anchorMax = Vector2.one;
                    ert.offsetMin = new Vector2(20f, 20f);
                    ert.offsetMax = new Vector2(-20f, -20f);
                    ert.localScale = Vector3.one;
                    if (empty.TryGetComponent(out TextMeshProUGUI emptyTmp))
                    {
                        emptyTmp.text = "Нет созданных миров. Нажмите 'Создать мир' ниже!";
                        emptyTmp.fontSize = 15f;
                        emptyTmp.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 0.7f);
                        emptyTmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                // WorldEntryTemplate
                Transform entryTemplate = singlePlayerPanel.Find("WorldScrollView/Viewport/Content/WorldEntryTemplate");
                if (entryTemplate != null)
                {
                    RectTransform ert = entryTemplate.GetComponent<RectTransform>();
                    ert.sizeDelta = new Vector2(ert.sizeDelta.x, 56f);
                    ert.localScale = Vector3.one;

                    LayoutElement le = entryTemplate.GetComponent<LayoutElement>() ?? entryTemplate.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 56f;
                    le.minHeight = 56f;
                    le.flexibleWidth = 1f;

                    if (entryTemplate.GetComponent<WorldEntryView>() == null)
                        entryTemplate.gameObject.AddComponent<WorldEntryView>();

                    KaplaoUiStyler.StyleCardRow(entryTemplate.gameObject, forceReplaceSprite: true);

                    Transform titleTxt = entryTemplate.Find("Title");
                    if (titleTxt != null)
                    {
                        RectTransform trt = titleTxt.GetComponent<RectTransform>();
                        trt.anchorMin = new Vector2(0f, 0.45f);
                        trt.anchorMax = new Vector2(1f, 1f);
                        trt.offsetMin = new Vector2(18f, 0f);
                        trt.offsetMax = new Vector2(-18f, -4f);

                        if (titleTxt.TryGetComponent(out TextMeshProUGUI tmp))
                        {
                            tmp.fontSize = 16f;
                            tmp.fontStyle = FontStyles.Bold;
                            tmp.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
                            tmp.alignment = TextAlignmentOptions.MidlineLeft;
                        }
                    }

                    Transform subtitle = entryTemplate.Find("Subtitle");
                    if (subtitle != null)
                    {
                        RectTransform srt2 = subtitle.GetComponent<RectTransform>();
                        srt2.anchorMin = new Vector2(0f, 0f);
                        srt2.anchorMax = new Vector2(1f, 0.45f);
                        srt2.offsetMin = new Vector2(18f, 4f);
                        srt2.offsetMax = new Vector2(-18f, 0f);

                        if (subtitle.TryGetComponent(out TextMeshProUGUI subTmp))
                        {
                            subTmp.fontSize = 12f;
                            subTmp.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 1f);
                            subTmp.alignment = TextAlignmentOptions.MidlineLeft;
                        }
                    }
                }

                // WorldActionsBar
                Transform actionsBar = singlePlayerPanel.Find("WorldActionsBar");
                if (actionsBar != null)
                {
                    SetAnchored(actionsBar, new Vector2(0f, -195f), new Vector2(710f, 46f));
                    Image abImg = actionsBar.GetComponent<Image>();
                    if (abImg != null)
                        UnityEngine.Object.DestroyImmediate(abImg);

                    HorizontalLayoutGroup hlg = actionsBar.GetComponent<HorizontalLayoutGroup>() ?? actionsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 16f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;

                    StyleMenuButtonWithLayout(actionsBar, "PlayButton", KaplaoButtonVariant.Success, 210f, 44f);
                    StyleMenuButtonWithLayout(actionsBar, "DeleteButton", KaplaoButtonVariant.Danger, 150f, 44f);
                }

                // BottomBar
                Transform bottomBar = singlePlayerPanel.Find("BottomBar");
                if (bottomBar == null)
                {
                    GameObject bbObj = new GameObject("BottomBar", typeof(RectTransform));
                    bbObj.transform.SetParent(singlePlayerPanel, false);
                    bottomBar = bbObj.transform;
                }

                SetAnchored(bottomBar, new Vector2(0f, -255f), new Vector2(710f, 48f));
                HorizontalLayoutGroup bhlg = bottomBar.GetComponent<HorizontalLayoutGroup>() ?? bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                bhlg.spacing = 16f;
                bhlg.childAlignment = TextAnchor.MiddleCenter;
                bhlg.childControlWidth = false;
                bhlg.childControlHeight = false;
                bhlg.childForceExpandWidth = false;
                bhlg.childForceExpandHeight = false;

                Transform createBtn = singlePlayerPanel.Find("CreateNewWorldButton") ?? bottomBar.Find("CreateNewWorldButton");
                if (createBtn != null)
                {
                    createBtn.SetParent(bottomBar, false);
                    createBtn.localScale = Vector3.one;
                    StyleMenuButtonWithLayout(bottomBar, "CreateNewWorldButton", KaplaoButtonVariant.Primary, 240f, 44f);
                }

                Transform backBtn = singlePlayerPanel.Find("BackButton") ?? bottomBar.Find("BackButton");
                if (backBtn != null)
                {
                    backBtn.SetParent(bottomBar, false);
                    backBtn.localScale = Vector3.one;
                    StyleMenuButtonWithLayout(bottomBar, "BackButton", KaplaoButtonVariant.Secondary, 140f, 44f);
                }
            }

            // 3. SettingsPanel
            Transform settingsPanel = root.Find("SettingsPanel");
            if (settingsPanel != null)
            {
                StripRobloxStyleRectAndResetScale(settingsPanel);

                RectTransform srt = settingsPanel.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Vector2.zero;
                srt.sizeDelta = new Vector2(840f, 700f);
                srt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(settingsPanel.gameObject, true, true);

                // Title
                Transform title = settingsPanel.Find("Title");
                SetAnchored(title, new Vector2(0f, 310f), new Vector2(400f, 36f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 24f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.color = KaplaoUiTheme.Current.TextPrimary;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                // Section 1: Nickname & Account
                SetLabelAndControl(settingsPanel, "NicknameLabel", "NicknameInput", 255f, 36f);

                Transform accountEmail = settingsPanel.Find("AccountEmailLabel");
                SetAnchored(accountEmail, new Vector2(0f, 215f), new Vector2(500f, 22f));
                if (accountEmail != null && accountEmail.TryGetComponent(out TextMeshProUGUI emailTmp))
                {
                    emailTmp.fontSize = 13f;
                    emailTmp.alignment = TextAlignmentOptions.Center;
                    emailTmp.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 1f);
                }

                SetAnchored(settingsPanel.Find("SignInButton"), new Vector2(-80f, 175f), new Vector2(140f, 34f));
                SetAnchored(settingsPanel.Find("SignOutButton"), new Vector2(80f, 175f), new Vector2(140f, 34f));
                StyleButtonIfExists(settingsPanel, "SignInButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(settingsPanel, "SignOutButton", KaplaoButtonVariant.Ghost);

                // Section 2: Runner Skin
                Transform skinLabel = settingsPanel.Find("SkinLabel");
                SetAnchored(skinLabel, new Vector2(-200f, 115f), new Vector2(260f, 32f));
                if (skinLabel != null && skinLabel.TryGetComponent(out TextMeshProUGUI skinLblTmp))
                {
                    skinLblTmp.alignment = TextAlignmentOptions.MidlineRight;
                    skinLblTmp.fontSize = 15f;
                    skinLblTmp.color = KaplaoUiTheme.Current.TextSecondary;
                }

                Transform skinRow = settingsPanel.Find("SkinRow");
                if (skinRow != null)
                {
                    SetAnchored(skinRow, new Vector2(140f, 115f), new Vector2(300f, 60f));
                    HorizontalLayoutGroup hlg = skinRow.GetComponent<HorizontalLayoutGroup>() ?? skinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 10f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;

                    for (int i = 0; i < skinRow.childCount; i++)
                    {
                        Transform child = skinRow.GetChild(i);
                        RectTransform crt = child.GetComponent<RectTransform>();
                        if (crt != null)
                        {
                            crt.sizeDelta = new Vector2(52f, 52f);
                            crt.localScale = Vector3.one;
                        }

                        // Strip toggle animator or background switch components from skin previews
                        KaplaoUiToggleAnimator ta = child.GetComponent<KaplaoUiToggleAnimator>();
                        if (ta != null) UnityEngine.Object.DestroyImmediate(ta);

                        if (child.GetComponent<KaplaoSkinCardHover>() == null)
                            child.gameObject.AddComponent<KaplaoSkinCardHover>();
                    }
                }

                // Section 3: Audio & Language
                SetLabelAndControl(settingsPanel, "MusicPackLabel", "MusicPackDropdown", 55f, 34f);
                SetLabelAndControl(settingsPanel, "MusicVolumeLabel", "MusicVolumeSlider", 10f, 24f);
                SetLabelAndControl(settingsPanel, "SfxVolumeLabel", "SfxVolumeSlider", -35f, 24f);
                SetLabelAndControl(settingsPanel, "LanguageLabel", "LanguageDropdown", -80f, 34f);

                // Value badges on sliders
                Transform musicSlider = settingsPanel.Find("MusicVolumeSlider");
                if (musicSlider != null && musicSlider.GetComponent<KaplaoSliderValueBadge>() == null)
                    musicSlider.gameObject.AddComponent<KaplaoSliderValueBadge>();

                Transform sfxSlider = settingsPanel.Find("SfxVolumeSlider");
                if (sfxSlider != null && sfxSlider.GetComponent<KaplaoSliderValueBadge>() == null)
                    sfxSlider.gameObject.AddComponent<KaplaoSliderValueBadge>();

                // Warning
                Transform warning = settingsPanel.Find("Warning");
                if (warning != null)
                {
                    SetAnchored(warning, new Vector2(0f, -135f), new Vector2(600f, 24f));
                    if (warning.TryGetComponent(out TextMeshProUGUI warnTmp))
                    {
                        warnTmp.fontSize = 12f;
                        warnTmp.color = new Color(0xF5 / 255f, 0x9E / 255f, 0x0B / 255f, 1f); // amber
                        warnTmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Section 4: Network
                SetLabelAndControl(settingsPanel, "NetworkLabel", "NetworkDropdown", -180f, 34f);
                SetLabelAndControl(settingsPanel, "TransportLabel", "TransportDropdown", -225f, 34f);
                SetLabelAndControl(settingsPanel, "PortLabel", "PortInput", -270f, 34f);

                // Footer BackButton
                SetAnchored(settingsPanel.Find("BackButton"), new Vector2(0f, -315f), new Vector2(240f, 44f));
                StyleButtonIfExists(settingsPanel, "BackButton", KaplaoButtonVariant.Primary);
            }

            // 4. NetworkPanel
            Transform networkPanel = root.Find("NetworkPanel");
            if (networkPanel != null)
            {
                StripRobloxStyleRectAndResetScale(networkPanel);

                RectTransform nrt = networkPanel.GetComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0.5f, 0.5f);
                nrt.anchorMax = new Vector2(0.5f, 0.5f);
                nrt.pivot = new Vector2(0.5f, 0.5f);
                nrt.anchoredPosition = Vector2.zero;
                nrt.sizeDelta = new Vector2(780f, 620f);
                nrt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(networkPanel.gameObject, true, true);

                Transform title = networkPanel.Find("Title");
                SetAnchored(title, new Vector2(0f, 260f), new Vector2(400f, 36f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 22f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                Transform scrollView = networkPanel.Find("ServerScrollView");
                if (scrollView != null)
                {
                    RectTransform svRt = scrollView.GetComponent<RectTransform>();
                    svRt.anchorMin = new Vector2(0.5f, 0.5f);
                    svRt.anchorMax = new Vector2(0.5f, 0.5f);
                    svRt.pivot = new Vector2(0.5f, 0.5f);
                    svRt.anchoredPosition = new Vector2(0f, 40f);
                    svRt.sizeDelta = new Vector2(710f, 310f);
                    svRt.localScale = Vector3.one;

                    ScrollRect sr = scrollView.GetComponent<ScrollRect>();
                    if (sr != null)
                        KaplaoUiStyler.StyleScrollView(sr);

                    Transform viewport = scrollView.Find("Viewport");
                    if (viewport != null)
                    {
                        EnsureRectMask2D(viewport.gameObject);
                        if (viewport.TryGetComponent(out Image vpImg))
                        {
                            vpImg.color = Color.clear;
                            vpImg.raycastTarget = false;
                        }

                        Transform content = viewport.Find("Content");
                        if (content != null)
                        {
                            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                            layout.spacing = 8f;
                            layout.padding = new RectOffset(8, 8, 8, 8);
                            layout.childAlignment = TextAnchor.UpperCenter;
                            layout.childControlWidth = true;
                            layout.childControlHeight = false;
                            layout.childForceExpandWidth = true;
                            layout.childForceExpandHeight = false;

                            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                        }
                    }

                    // Empty state label
                    Transform empty = scrollView.Find("EmptyStateLabel");
                    if (empty == null)
                    {
                        GameObject emptyObj = new GameObject("EmptyStateLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                        emptyObj.transform.SetParent(scrollView, false);
                        empty = emptyObj.transform;
                    }
                    RectTransform ert = empty.GetComponent<RectTransform>();
                    ert.anchorMin = Vector2.zero;
                    ert.anchorMax = Vector2.one;
                    ert.offsetMin = new Vector2(20f, 20f);
                    ert.offsetMax = new Vector2(-20f, -20f);
                    ert.localScale = Vector3.one;
                    if (empty.TryGetComponent(out TextMeshProUGUI emptyTmp))
                    {
                        emptyTmp.text = "Локальные серверы не найдены. Создайте игру или подключитесь по IP";
                        emptyTmp.fontSize = 14f;
                        emptyTmp.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 0.7f);
                        emptyTmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Direct IP row
                Transform ipInput = networkPanel.Find("DirectIpInput") ?? networkPanel.Find("IpInput");
                if (ipInput != null)
                {
                    SetAnchored(ipInput, new Vector2(-70f, -142f), new Vector2(400f, 38f));
                }

                Transform connectBtn = networkPanel.Find("ConnectDirectlyButton");
                if (connectBtn != null)
                {
                    SetAnchored(connectBtn, new Vector2(195f, -142f), new Vector2(110f, 38f));
                    StyleButtonIfExists(networkPanel, "ConnectDirectlyButton", KaplaoButtonVariant.Primary);
                }

                // Network status text
                Transform statusText = networkPanel.Find("NetworkStatusText") ?? networkPanel.Find("StatusText");
                if (statusText != null)
                {
                    SetAnchored(statusText, new Vector2(0f, -195f), new Vector2(600f, 26f));
                    if (statusText.TryGetComponent(out TextMeshProUGUI stTmp))
                    {
                        stTmp.fontSize = 13f;
                        stTmp.color = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 1f);
                        stTmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Bottom buttons
                Transform refreshBtn = networkPanel.Find("RefreshButton") ?? networkPanel.Find("RefreshServersButton");
                if (refreshBtn != null)
                {
                    SetAnchored(refreshBtn, new Vector2(-110f, -246f), new Vector2(180f, 42f));
                    StyleButtonIfExists(networkPanel, refreshBtn.name, KaplaoButtonVariant.Secondary);
                }

                Transform backBtn = networkPanel.Find("BackButton");
                if (backBtn != null)
                {
                    SetAnchored(backBtn, new Vector2(110f, -246f), new Vector2(140f, 42f));
                    StyleButtonIfExists(networkPanel, "BackButton", KaplaoButtonVariant.Secondary);
                }
            }

            // 5. CreateWorldNamePanel
            Transform createWorldPanel = root.Find("CreateWorldNamePanel");
            if (createWorldPanel != null)
            {
                StripRobloxStyleRectAndResetScale(createWorldPanel);

                RectTransform cwnRt = createWorldPanel.GetComponent<RectTransform>();
                cwnRt.anchorMin = new Vector2(0.5f, 0.5f);
                cwnRt.anchorMax = new Vector2(0.5f, 0.5f);
                cwnRt.pivot = new Vector2(0.5f, 0.5f);
                cwnRt.anchoredPosition = Vector2.zero;
                cwnRt.sizeDelta = new Vector2(460f, 260f);
                cwnRt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(createWorldPanel.gameObject, true, true);

                Transform title = createWorldPanel.Find("Title");
                SetAnchored(title, new Vector2(0f, 85f), new Vector2(400f, 36f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 20f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                Transform input = createWorldPanel.Find("WorldNameInput");
                SetAnchored(input, new Vector2(0f, 20f), new Vector2(380f, 42f));

                SetAnchored(createWorldPanel.Find("BackButton"), new Vector2(-95f, -65f), new Vector2(130f, 40f));
                SetAnchored(createWorldPanel.Find("ContinueButton"), new Vector2(95f, -65f), new Vector2(170f, 40f));

                StyleButtonIfExists(createWorldPanel, "ContinueButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(createWorldPanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 6. NicknamePanel
            Transform nicknamePanel = root.Find("NicknamePanel");
            if (nicknamePanel != null)
            {
                StripRobloxStyleRectAndResetScale(nicknamePanel);

                RectTransform nnRt = nicknamePanel.GetComponent<RectTransform>();
                nnRt.anchorMin = new Vector2(0.5f, 0.5f);
                nnRt.anchorMax = new Vector2(0.5f, 0.5f);
                nnRt.pivot = new Vector2(0.5f, 0.5f);
                nnRt.anchoredPosition = Vector2.zero;
                nnRt.sizeDelta = new Vector2(460f, 260f);
                nnRt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(nicknamePanel.gameObject, true, true);

                Transform title = nicknamePanel.Find("Title");
                SetAnchored(title, new Vector2(0f, 85f), new Vector2(400f, 36f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 20f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                Transform input = nicknamePanel.Find("NicknameInput");
                SetAnchored(input, new Vector2(0f, 20f), new Vector2(380f, 42f));

                SetAnchored(nicknamePanel.Find("BackButton"), new Vector2(-95f, -65f), new Vector2(130f, 40f));
                SetAnchored(nicknamePanel.Find("ContinueButton"), new Vector2(95f, -65f), new Vector2(170f, 40f));

                StyleButtonIfExists(nicknamePanel, "ContinueButton", KaplaoButtonVariant.Primary);
                StyleButtonIfExists(nicknamePanel, "BackButton", KaplaoButtonVariant.Ghost);
            }

            // 7. DeleteWorldConfirmPanel
            Transform deletePanel = root.Find("DeleteWorldConfirmPanel");
            if (deletePanel != null)
            {
                StripRobloxStyleRectAndResetScale(deletePanel);

                RectTransform dpRt = deletePanel.GetComponent<RectTransform>();
                dpRt.anchorMin = new Vector2(0f, 0f);
                dpRt.anchorMax = new Vector2(1f, 1f);
                dpRt.offsetMin = Vector2.zero;
                dpRt.offsetMax = Vector2.zero;
                dpRt.localScale = Vector3.one;

                Transform dialog = deletePanel.Find("DialogBox") ?? deletePanel;
                RectTransform dRt = dialog.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0.5f, 0.5f);
                dRt.anchorMax = new Vector2(0.5f, 0.5f);
                dRt.pivot = new Vector2(0.5f, 0.5f);
                dRt.anchoredPosition = Vector2.zero;
                dRt.sizeDelta = new Vector2(460f, 240f);
                dRt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(dialog.gameObject, true, true);

                Transform title = dialog.Find("Title") ?? dialog.Find("Message");
                SetAnchored(title, new Vector2(0f, 50f), new Vector2(400f, 60f));
                if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                {
                    titleTmp.fontSize = 17f;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                }

                SetAnchored(dialog.Find("CancelButton"), new Vector2(-85f, -55f), new Vector2(130f, 40f));
                SetAnchored(dialog.Find("ConfirmDeleteButton"), new Vector2(85f, -55f), new Vector2(160f, 40f));

                StyleButtonIfExists(dialog, "ConfirmDeleteButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(dialog, "CancelButton", KaplaoButtonVariant.Ghost);
            }

            // 8. BrowseLevelsPanel
            Transform browsePanel = root.Find("BrowseLevelsPanel");
            if (browsePanel != null)
            {
                StripRobloxStyleRectAndResetScale(browsePanel);

                RectTransform bpRt = browsePanel.GetComponent<RectTransform>();
                bpRt.anchorMin = new Vector2(0.5f, 0.5f);
                bpRt.anchorMax = new Vector2(0.5f, 0.5f);
                bpRt.pivot = new Vector2(0.5f, 0.5f);
                bpRt.anchoredPosition = Vector2.zero;
                bpRt.sizeDelta = new Vector2(780f, 620f);
                bpRt.localScale = Vector3.one;
                KaplaoUiStyler.StylePanel(browsePanel.gameObject, true, true);

                Transform search = browsePanel.Find("BrowseSearchInput");
                if (search != null)
                {
                    RectTransform srt = search.GetComponent<RectTransform>();
                    srt.anchorMin = new Vector2(0.5f, 1f);
                    srt.anchorMax = new Vector2(0.5f, 1f);
                    srt.pivot = new Vector2(0.5f, 1f);
                    srt.anchoredPosition = new Vector2(0f, -60f);
                    srt.sizeDelta = new Vector2(700f, 40f);
                    srt.localScale = Vector3.one;
                }

                Transform browseScroll = browsePanel.Find("BrowseScroll");
                if (browseScroll != null)
                {
                    RectTransform bsRt = browseScroll.GetComponent<RectTransform>();
                    bsRt.anchorMin = new Vector2(0.5f, 0.5f);
                    bsRt.anchorMax = new Vector2(0.5f, 0.5f);
                    bsRt.pivot = new Vector2(0.5f, 0.5f);
                    bsRt.anchoredPosition = new Vector2(0f, 15f);
                    bsRt.sizeDelta = new Vector2(700f, 370f);
                    bsRt.localScale = Vector3.one;

                    ScrollRect sr = browseScroll.GetComponent<ScrollRect>();
                    if (sr != null)
                        KaplaoUiStyler.StyleScrollView(sr);

                    Transform viewport = browseScroll.Find("Viewport") ?? browseScroll;
                    if (viewport != null)
                    {
                        EnsureRectMask2D(viewport.gameObject);
                        if (viewport.TryGetComponent(out Image vpImg))
                        {
                            vpImg.color = Color.clear;
                            vpImg.raycastTarget = false;
                        }
                    }

                    Transform content = browseScroll.Find("BrowseContent") ?? browseScroll.Find("Viewport/Content");
                    if (content != null)
                    {
                        GridLayoutGroup oldGrid = content.GetComponent<GridLayoutGroup>();
                        if (oldGrid != null) UnityEngine.Object.DestroyImmediate(oldGrid);

                        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                        vlg.spacing = 8f;
                        vlg.padding = new RectOffset(8, 8, 8, 8);
                        vlg.childAlignment = TextAnchor.UpperCenter;
                        vlg.childControlWidth = true;
                        vlg.childControlHeight = false;
                        vlg.childForceExpandWidth = true;
                        vlg.childForceExpandHeight = false;

                        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                        RectTransform cRt = content.GetComponent<RectTransform>();
                        cRt.anchorMin = new Vector2(0f, 1f);
                        cRt.anchorMax = new Vector2(1f, 1f);
                        cRt.pivot = new Vector2(0.5f, 1f);
                        cRt.offsetMin = Vector2.zero;
                        cRt.offsetMax = Vector2.zero;

                        Transform entryTemplate = content.Find("BrowseEntryTemplate");
                        if (entryTemplate != null)
                        {
                            RectTransform entryRt = entryTemplate.GetComponent<RectTransform>();
                            entryRt.sizeDelta = new Vector2(0f, 76f);
                            entryRt.localScale = Vector3.one;
                            LayoutElement entryLe = entryTemplate.GetComponent<LayoutElement>() ?? entryTemplate.gameObject.AddComponent<LayoutElement>();
                            entryLe.minHeight = 76f;
                            entryLe.preferredHeight = 76f;
                            entryLe.flexibleHeight = 0f;

                            KaplaoUiStyler.StyleCardRow(entryTemplate.gameObject, forceReplaceSprite: true);

                            Transform title = entryTemplate.Find("Title");
                            if (title != null && title.TryGetComponent(out TextMeshProUGUI titleTmp))
                            {
                                titleTmp.fontStyle = FontStyles.Bold;
                                titleTmp.fontSize = 18f;
                                titleTmp.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
                            }

                            Transform author = entryTemplate.Find("Author");
                            if (author != null && author.TryGetComponent(out TextMeshProUGUI authorTmp))
                            {
                                authorTmp.fontSize = 13.5f;
                                authorTmp.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 1f);
                            }
                        }
                    }
                }

                SetAnchored(browsePanel.Find("BrowsePlayButton"), new Vector2(-165f, -255f), new Vector2(150f, 42f));
                SetAnchored(browsePanel.Find("BrowseCopyButton"), new Vector2(-5f, -255f), new Vector2(150f, 42f));
                SetAnchored(browsePanel.Find("BrowseDeleteFromServersButton"), new Vector2(155f, -255f), new Vector2(150f, 42f));
                SetAnchored(browsePanel.Find("BrowseBackButton"), new Vector2(300f, -255f), new Vector2(110f, 42f));

                StyleButtonIfExists(browsePanel, "BrowsePlayButton", KaplaoButtonVariant.Success);
                StyleButtonIfExists(browsePanel, "BrowseCopyButton", KaplaoButtonVariant.Secondary);
                StyleButtonIfExists(browsePanel, "BrowseDeleteFromServersButton", KaplaoButtonVariant.Danger);
                StyleButtonIfExists(browsePanel, "BrowseBackButton", KaplaoButtonVariant.Secondary);

                Transform delConfirm = browsePanel.Find("BrowseDeleteConfirmPanel");
                if (delConfirm != null)
                {
                    StripRobloxStyleRectAndResetScale(delConfirm);
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
                pauseRoot.gameObject.SetActive(false);

                Transform dim = pauseRoot.Find("DimBackground");
                if (dim != null && dim.TryGetComponent(out Image dimImg))
                    KaplaoUiStyler.StyleBackdrop(dimImg);

                Transform pausePanel = pauseRoot.Find("PauseMenuPanel");
                if (pausePanel != null)
                {
                    StripRobloxStyleRectAndResetScale(pausePanel);

                    // Reparent PublishLevelPanel if incorrectly nested
                    Transform nestedPublish = pausePanel.Find("PublishLevelPanel");
                    if (nestedPublish != null)
                        nestedPublish.SetParent(pauseRoot, false);

                    RectTransform prt = pausePanel.GetComponent<RectTransform>();
                    prt.anchorMin = new Vector2(0.5f, 0.5f);
                    prt.anchorMax = new Vector2(0.5f, 0.5f);
                    prt.pivot = new Vector2(0.5f, 0.5f);
                    prt.anchoredPosition = Vector2.zero;
                    prt.sizeDelta = new Vector2(340f, 340f);
                    prt.localScale = Vector3.one;

                    VerticalLayoutGroup vlg = pausePanel.GetComponent<VerticalLayoutGroup>() ?? pausePanel.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 10f;
                    vlg.padding = new RectOffset(24, 24, 24, 24);

                    ContentSizeFitter csf = pausePanel.GetComponent<ContentSizeFitter>() ?? pausePanel.gameObject.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    KaplaoUiStyler.StylePanel(pausePanel.gameObject, true, true);

                    // Ensure informative header with world name and LAN status pill
                    EnsurePauseHeader(pausePanel);

                    StyleMenuButtonWithLayout(pausePanel, "ResumeButton", KaplaoButtonVariant.Primary, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "SettingsButton", KaplaoButtonVariant.Secondary, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "HostSettingsButton", KaplaoButtonVariant.Secondary, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "ModerationButton", KaplaoButtonVariant.Secondary, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "SaveButton", KaplaoButtonVariant.Success, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "SaveAndExitButton", KaplaoButtonVariant.Success, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "OpenToLanButton", KaplaoButtonVariant.Secondary, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "PublishButton", KaplaoButtonVariant.Secondary, 280f, 42f);
                    StyleMenuButtonWithLayout(pausePanel, "ExitButton", KaplaoButtonVariant.Danger, 280f, 42f);
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
                runnerRoot.gameObject.SetActive(false);

                Transform dim = runnerRoot.Find("DimBackground");
                if (dim != null && dim.TryGetComponent(out Image dimImg))
                    KaplaoUiStyler.StyleBackdrop(dimImg);

                Transform runnerPanel = runnerRoot.Find("RunnerPauseMenuPanel") ?? runnerRoot.Find("PauseMenuPanel");
                if (runnerPanel != null)
                {
                    StripRobloxStyleRectAndResetScale(runnerPanel);

                    RectTransform rrt = runnerPanel.GetComponent<RectTransform>();
                    rrt.anchorMin = new Vector2(0.5f, 0.5f);
                    rrt.anchorMax = new Vector2(0.5f, 0.5f);
                    rrt.pivot = new Vector2(0.5f, 0.5f);
                    rrt.anchoredPosition = Vector2.zero;
                    rrt.sizeDelta = new Vector2(340f, 340f);
                    rrt.localScale = Vector3.one;

                    VerticalLayoutGroup vlg = runnerPanel.GetComponent<VerticalLayoutGroup>() ?? runnerPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 10f;
                    vlg.padding = new RectOffset(24, 24, 24, 24);

                    ContentSizeFitter csf = runnerPanel.GetComponent<ContentSizeFitter>() ?? runnerPanel.gameObject.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    KaplaoUiStyler.StylePanel(runnerPanel.gameObject, true, true);

                    StyleMenuButtonWithLayout(runnerPanel, "ResumeButton", KaplaoButtonVariant.Primary, 280f, 42f);
                    StyleMenuButtonWithLayout(runnerPanel, "ExitButton", KaplaoButtonVariant.Danger, 280f, 42f);
                }
            }
        }

        static void EnsurePauseHeader(Transform pausePanel)
        {
            if (pausePanel == null) return;
            Transform header = pausePanel.Find("Header");
            if (header == null)
            {
                GameObject headerObj = new GameObject("Header", typeof(RectTransform));
                headerObj.transform.SetParent(pausePanel, false);
                headerObj.transform.SetSiblingIndex(0);
                header = headerObj.transform;
            }

            RectTransform hrt = header.GetComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(290f, 48f);
            hrt.localScale = Vector3.one;

            LayoutElement hle = header.GetComponent<LayoutElement>() ?? header.gameObject.AddComponent<LayoutElement>();
            hle.minHeight = 48f;
            hle.preferredHeight = 48f;
            hle.flexibleWidth = 1f;

            // WorldNameLabel
            Transform wnl = header.Find("WorldNameLabel");
            if (wnl == null)
            {
                GameObject wnlObj = new GameObject("WorldNameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                wnlObj.transform.SetParent(header, false);
                wnl = wnlObj.transform;
            }
            RectTransform wnRt = wnl.GetComponent<RectTransform>();
            wnRt.anchorMin = new Vector2(0f, 0.44f);
            wnRt.anchorMax = new Vector2(1f, 1f);
            wnRt.offsetMin = Vector2.zero;
            wnRt.offsetMax = Vector2.zero;
            wnRt.localScale = Vector3.one;
            if (wnl.TryGetComponent(out TextMeshProUGUI wnlTmp))
            {
                wnlTmp.fontSize = 15f;
                wnlTmp.fontStyle = FontStyles.Bold;
                wnlTmp.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
                wnlTmp.alignment = TextAlignmentOptions.Center;
                if (string.IsNullOrEmpty(wnlTmp.text))
                    wnlTmp.text = "МИР";
            }

            // StatusPill
            Transform pill = header.Find("StatusPill");
            if (pill == null)
            {
                GameObject pillObj = new GameObject("StatusPill", typeof(RectTransform), typeof(Image));
                pillObj.transform.SetParent(header, false);
                pill = pillObj.transform;
            }
            RectTransform pillRt = pill.GetComponent<RectTransform>();
            pillRt.anchorMin = new Vector2(0.5f, 0f);
            pillRt.anchorMax = new Vector2(0.5f, 0.44f);
            pillRt.pivot = new Vector2(0.5f, 0.5f);
            pillRt.anchoredPosition = new Vector2(0f, 2f);
            pillRt.sizeDelta = new Vector2(160f, 20f);
            pillRt.localScale = Vector3.one;

            if (pill.TryGetComponent(out Image pillImg))
            {
                pillImg.sprite = KaplaoUiSprites.RoundedRect(10);
                pillImg.type = Image.Type.Sliced;
                pillImg.color = new Color(0.04f, 0.08f, 0.16f, 0.95f);
            }

            // StatusLabel
            Transform sl = pill.Find("StatusLabel");
            if (sl == null)
            {
                GameObject slObj = new GameObject("StatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                slObj.transform.SetParent(pill, false);
                sl = slObj.transform;
            }
            RectTransform slRt = sl.GetComponent<RectTransform>();
            slRt.anchorMin = Vector2.zero;
            slRt.anchorMax = Vector2.one;
            slRt.offsetMin = Vector2.zero;
            slRt.offsetMax = Vector2.zero;
            slRt.localScale = Vector3.one;
            if (sl.TryGetComponent(out TextMeshProUGUI slTmp))
            {
                slTmp.fontSize = 11f;
                slTmp.fontStyle = FontStyles.Bold;
                slTmp.color = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 1f);
                slTmp.alignment = TextAlignmentOptions.Center;
                if (string.IsNullOrEmpty(slTmp.text))
                    slTmp.text = "ОДИНОЧНАЯ";
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

            Transform bannedPanel = canvas.transform.Find("BannedPlayersPanel") ?? FindDeepChild(canvas.transform, "BannedPlayersPanel");
            if (bannedPanel != null)
            {
                bannedPanel.SetParent(canvas.transform, false);
                StripRobloxStyleRectAndResetScale(bannedPanel);

                RectTransform bprt = bannedPanel.GetComponent<RectTransform>();
                bprt.anchorMin = new Vector2(0.5f, 0.5f);
                bprt.anchorMax = new Vector2(0.5f, 0.5f);
                bprt.pivot = new Vector2(0.5f, 0.5f);
                bprt.anchoredPosition = Vector2.zero;
                bprt.sizeDelta = new Vector2(460f, 520f);
                bprt.localScale = Vector3.one;

                KaplaoUiStyler.StylePanel(bannedPanel.gameObject, true, true);
                if (bannedPanel.TryGetComponent(out Image bpImg))
                {
                    bpImg.sprite = KaplaoUiSprites.RoundedRect(16);
                    bpImg.color = KaplaoUiTheme.Current.SurfacePrimary;
                }

                Transform title = bannedPanel.Find("Title");
                if (title != null)
                {
                    SetAnchored(title, new Vector2(0f, 225f), new Vector2(380f, 36f));
                    if (title.TryGetComponent(out TextMeshProUGUI titleTmp))
                    {
                        titleTmp.fontSize = 20f;
                        titleTmp.fontStyle = FontStyles.Bold;
                        titleTmp.color = KaplaoUiTheme.Current.TextPrimary;
                        titleTmp.alignment = TextAlignmentOptions.Center;
                    }
                }

                Transform scroll = bannedPanel.Find("BannedScroll");
                if (scroll != null)
                {
                    SetAnchored(scroll, new Vector2(0f, -5f), new Vector2(410f, 380f));
                    if (scroll.TryGetComponent(out Image sImg))
                    {
                        sImg.sprite = KaplaoUiSprites.RoundedRect(10);
                        sImg.color = new Color(0.06f, 0.09f, 0.16f, 0.85f);
                    }
                    Transform viewport = scroll.Find("Viewport");
                    if (viewport != null)
                    {
                        EnsureRectMask2D(viewport.gameObject);
                        if (viewport.TryGetComponent(out Image vpImg))
                        {
                            vpImg.color = Color.clear;
                            vpImg.raycastTarget = false;
                        }
                    }
                }

                AttachPanelAnimator(bannedPanel, PanelAnimStyle.PopScale);
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

                Transform fileTreePanel = codeMode.Find("FileTreePanel");
                if (fileTreePanel != null)
                {
                    if (fileTreePanel.TryGetComponent(out Image ftpImg))
                    {
                        ftpImg.sprite = KaplaoUiSprites.RoundedRect(10);
                        ftpImg.color = new Color(0.051f, 0.067f, 0.102f, 1f); // #0D111A
                    }

                    Transform fileTreeScroll = fileTreePanel.Find("FileTreeScroll");
                    if (fileTreeScroll != null)
                    {
                        if (fileTreeScroll.TryGetComponent(out Image ftsImg))
                        {
                            ftsImg.color = Color.clear;
                            ftsImg.raycastTarget = true;
                        }

                        Transform viewport = fileTreeScroll.Find("Viewport");
                        if (viewport != null)
                        {
                            EnsureRectMask2D(viewport.gameObject);
                            if (viewport.TryGetComponent(out Image vpImg))
                            {
                                vpImg.color = Color.clear;
                                vpImg.raycastTarget = false;
                            }
                        }
                    }
                }
            }
        }

        static void PolishSampleSceneHud(Canvas canvas)
        {
            NetworkPingDisplay ping = canvas.GetComponentInChildren<NetworkPingDisplay>(true);
            if (ping != null)
            {
                ping.RebuildPill();
            }

            Transform hostSettings = canvas.transform.Find("HostSettingsUI");
            if (hostSettings != null)
            {
                RectTransform hsRt = hostSettings.GetComponent<RectTransform>();
                hsRt.anchorMin = new Vector2(1f, 1f);
                hsRt.anchorMax = new Vector2(1f, 1f);
                hsRt.pivot = new Vector2(1f, 1f);
                hsRt.anchoredPosition = new Vector2(-16f, -128f);
                hsRt.sizeDelta = new Vector2(290f, 44f);
                KaplaoUiStyler.StylePanel(hostSettings.gameObject, true, true);

                Toggle runnerToggle = hostSettings.GetComponentInChildren<Toggle>(true);
                if (runnerToggle != null)
                {
                    KaplaoUiStyler.StyleToggleAsSwitch(runnerToggle);
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

        static void StyleMenuButtonWithLayout(Transform parent, string name, KaplaoButtonVariant variant, float width = 280f, float height = 44f)
        {
            Transform t = parent.Find(name);
            if (t == null) return;

            if (t.TryGetComponent(out Button btn))
                KaplaoUiStyler.StyleButton(btn, variant);

            LayoutElement le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
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

        static void SetPanelActive(Transform t, bool active)
        {
            if (t != null)
                t.gameObject.SetActive(active);
        }

        static void StripRobloxStyleRect(Transform root)
        {
            StripRobloxStyleRectAndResetScale(root);
        }

        static void StripRobloxStyleRectAndResetScale(Transform root)
        {
            if (root == null) return;
            var scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                var s = scripts[i];
                if (s != null && s.GetType().Name == "RobloxStyleRect")
                {
                    UnityEngine.Object.DestroyImmediate(s);
                }
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;
                if (t.name == "LayerPrevButton")
                    t.localScale = new Vector3(-0.8f, 0.8f, 1f);
                else if (t.name == "LayerNextButton")
                    t.localScale = new Vector3(0.8f, 0.8f, 1f);
                else
                    t.localScale = Vector3.one;
            }
        }

        static void RemoveAllButtonGlows(Transform root)
        {
            if (root == null) return;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == "UI_BtnGlow")
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        static void RefreshAllShadows(Transform root)
        {
            if (root == null) return;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == "UI_Shadow")
                {
                    if (t.TryGetComponent(out Image img))
                    {
                        img.sprite = KaplaoUiSprites.SoftShadow(16, 28);
                        img.type = Image.Type.Sliced;
                        img.color = KaplaoUiTheme.Current.SoftShadowColor;
                        img.raycastTarget = false;
                    }
                    if (t is RectTransform rt)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = new Vector2(-36f, -40f);
                        rt.offsetMax = new Vector2(36f, 32f);
                    }
                }
            }
        }

        static void CleanAllScrollViewports(Transform root)
        {
            if (root == null) return;
            ScrollRect[] srs = root.GetComponentsInChildren<ScrollRect>(true);
            for (int i = 0; i < srs.Length; i++)
            {
                ScrollRect sr = srs[i];
                if (sr == null) continue;
                KaplaoUiStyler.StyleScrollView(sr);
                if (sr.viewport != null)
                {
                    EnsureRectMask2D(sr.viewport.gameObject);
                    if (sr.viewport.TryGetComponent(out Image vpImg))
                    {
                        vpImg.color = Color.clear;
                        vpImg.raycastTarget = false;
                    }
                }
            }

            TMP_Dropdown[] dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                TMP_Dropdown dd = dropdowns[i];
                if (dd == null) continue;
                KaplaoUiStyler.StyleDropdown(dd);
                if (dd.template != null)
                {
                    Transform vp = dd.template.Find("Viewport");
                    if (vp != null)
                    {
                        EnsureRectMask2D(vp.gameObject);
                        if (vp.TryGetComponent(out Image vpImg))
                        {
                            vpImg.color = Color.clear;
                            vpImg.raycastTarget = false;
                        }
                    }
                }
            }
        }

        static void SetLabelAndControl(Transform parent, string labelName, string controlName, float y, float controlHeight)
        {
            if (parent == null) return;
            Transform lbl = parent.Find(labelName);
            if (lbl != null)
            {
                StripRobloxStyleRectAndResetScale(lbl);
                RectTransform lrt = lbl.GetComponent<RectTransform>();
                if (lrt != null)
                {
                    lrt.anchorMin = new Vector2(0.5f, 0.5f);
                    lrt.anchorMax = new Vector2(0.5f, 0.5f);
                    lrt.pivot = new Vector2(1f, 0.5f);
                    lrt.anchoredPosition = new Vector2(-20f, y);
                    lrt.sizeDelta = new Vector2(260f, 32f);
                    lrt.localScale = Vector3.one;
                }
                if (lbl.TryGetComponent(out TextMeshProUGUI tmp))
                {
                    tmp.alignment = TextAlignmentOptions.MidlineRight;
                    tmp.fontSize = 15f;
                    tmp.color = KaplaoUiTheme.Current.TextSecondary;
                }
            }

            Transform ctrl = parent.Find(controlName);
            if (ctrl != null)
            {
                StripRobloxStyleRectAndResetScale(ctrl);
                RectTransform crt = ctrl.GetComponent<RectTransform>();
                if (crt != null)
                {
                    crt.anchorMin = new Vector2(0.5f, 0.5f);
                    crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.pivot = new Vector2(0f, 0.5f);
                    crt.anchoredPosition = new Vector2(20f, y);
                    crt.sizeDelta = new Vector2(300f, controlHeight);
                    crt.localScale = Vector3.one;
                }
            }
        }

        static Sprite LoadSpriteOrSubSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite sub && !string.IsNullOrEmpty(sub.name))
                        return sub;
                }
            }
            return null;
        }

        static void SetProtectedSlotIcon(Transform t, Sprite sprite)
        {
            if (t == null) return;
            Transform glow = t.Find("UI_BtnGlow");
            if (glow != null) UnityEngine.Object.DestroyImmediate(glow.gameObject);
            Transform border = t.Find("UI_Border");
            if (border != null) UnityEngine.Object.DestroyImmediate(border.gameObject);

            if (t.TryGetComponent(out Image img))
            {
                if (sprite != null)
                    img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
            }

            if (t.TryGetComponent(out UiButtonLeanScale scale))
            {
                scale.SetBaseColors(Color.white, Color.clear);
            }

            Transform info = t.Find("InfoButton");
            if (info != null)
            {
                Transform infoGlow = info.Find("UI_BtnGlow");
                if (infoGlow != null) UnityEngine.Object.DestroyImmediate(infoGlow.gameObject);
                Transform infoBorder = info.Find("UI_Border");
                if (infoBorder != null) UnityEngine.Object.DestroyImmediate(infoBorder.gameObject);

                if (info.TryGetComponent(out Image infoImg))
                {
                    infoImg.color = Color.white;
                    infoImg.preserveAspect = true;
                }
                if (info.TryGetComponent(out UiButtonLeanScale infoScale))
                {
                    infoScale.SetBaseColors(Color.white, Color.clear);
                }
            }
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void RestoreSampleSceneIcons(Canvas canvas, Scene scene)
        {
            if (canvas == null) return;

            Sprite cursorSprite = LoadSpriteOrSubSprite("Assets/Sprites/Cursor/cursor_released.png");
            Sprite palmSprite = LoadSpriteOrSubSprite("Assets/Sprites/Cursor/101-palm-hands-png-hand-image--Photoroom.png");
            Sprite crosshairSprite = LoadSpriteOrSubSprite("Assets/Sprites/Cursor/a-red-line-art-icon-of-a-crosshair-with-a-central-dot-representing-a-tactical-dot-sight-or-red-dot-sight-free-vector-Photoroom.png");
            Sprite triangleSprite = LoadSpriteOrSubSprite("Assets/Sprites/Triangle.png");
            Sprite trashSprite = LoadSpriteOrSubSprite("Assets/Sprites/Cursor/4436879.png");
            Sprite particleSprite = LoadSpriteOrSubSprite("Assets/Sprites/favpng_9f26e9efcc5b7879fe9eeffe100f49c3.png");
            Sprite keyframeSprite = LoadSpriteOrSubSprite("Assets/Sprites/favpng_6374a0db9667374a82a20da688e86eb9.png");
            Sprite executorSprite = LoadSpriteOrSubSprite("Assets/Sprites/laptop-pixel-art-cartoon-illustration-free-png.png");
            Sprite arrowSprite = LoadSpriteOrSubSprite("Assets/Sprites/arrow.png");
            Sprite rightArrowSprite = LoadSpriteOrSubSprite("Assets/Sprites/right-arrow-button.png");
            Sprite sendSprite = LoadSpriteOrSubSprite("Assets/Sprites/send.png");
            Sprite infoSprite = LoadSpriteOrSubSprite("Assets/Sprites/UI/info.png");

            Transform usualSlots = FindDeepChild(canvas.transform, "UsualToolSlots");
            if (usualSlots != null)
            {
                SetProtectedSlotIcon(usualSlots.Find("Slot1Button"), cursorSprite);
                SetProtectedSlotIcon(usualSlots.Find("Slot2Button"), palmSprite);
                SetProtectedSlotIcon(usualSlots.Find("Slot3Button"), crosshairSprite);
            }

            Transform createSlots = FindDeepChild(canvas.transform, "CreateToolSlots");
            if (createSlots != null)
            {
                SetProtectedSlotIcon(createSlots.Find("Slot1Button"), cursorSprite);
                SetProtectedSlotIcon(createSlots.Find("Slot3Button"), triangleSprite);
                SetProtectedSlotIcon(createSlots.Find("Slot5Button"), trashSprite);
                SetProtectedSlotIcon(createSlots.Find("Slot6Button"), particleSprite);
                SetProtectedSlotIcon(createSlots.Find("Slot7Button"), keyframeSprite);
                SetProtectedSlotIcon(createSlots.Find("Slot8Button"), executorSprite);

                for (int i = 1; i <= 8; i++)
                {
                    Transform slot = createSlots.Find($"Slot{i}Button");
                    if (slot != null)
                    {
                        Transform info = slot.Find("InfoButton");
                        if (info != null && infoSprite != null)
                        {
                            SetProtectedSlotIcon(info, infoSprite);
                        }
                    }
                }
            }

            Transform prev = FindDeepChild(canvas.transform, "LayerPrevButton");
            if (prev != null)
            {
                SetProtectedSlotIcon(prev, arrowSprite);
                RectTransform prt = prev.GetComponent<RectTransform>();
                if (prt != null) prt.localScale = new Vector3(-0.8f, 0.8f, 1f);
            }

            Transform next = FindDeepChild(canvas.transform, "LayerNextButton");
            if (next != null)
            {
                SetProtectedSlotIcon(next, arrowSprite);
                RectTransform nrt = next.GetComponent<RectTransform>();
                if (nrt != null) nrt.localScale = new Vector3(0.8f, 0.8f, 1f);
            }

            Transform movePad = FindDeepChild(canvas.transform, "SelectionMovePad");
            if (movePad != null)
            {
                SetProtectedSlotIcon(movePad.Find("MoveRightButton"), rightArrowSprite);
                Transform moveLeft = movePad.Find("MoveLeftButton");
                if (moveLeft != null)
                {
                    SetProtectedSlotIcon(moveLeft, rightArrowSprite);
                    RectTransform mlrt = moveLeft.GetComponent<RectTransform>();
                    if (mlrt != null) mlrt.localScale = new Vector3(-1f, 1f, 1f);
                }
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Button[] allButtons = roots[r].GetComponentsInChildren<Button>(true);
                for (int i = 0; i < allButtons.Length; i++)
                {
                    Button b = allButtons[i];
                    if (b.name == "SendButton")
                        SetProtectedSlotIcon(b.transform, sendSprite);
                    else if (b.name == "InfoButton")
                        SetProtectedSlotIcon(b.transform, infoSprite);
                }
            }
        }

        static void EnsureRectMask2D(GameObject target)
        {
            if (target == null) return;
            RemoveLegacyMask(target);
            if (target.GetComponent<RectMask2D>() == null)
                target.AddComponent<RectMask2D>();
            if (target.TryGetComponent(out Image img))
            {
                img.color = Color.clear;
                img.raycastTarget = false;
            }
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
