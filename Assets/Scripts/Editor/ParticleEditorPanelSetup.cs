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
    public static class ParticleEditorPanelSetup
    {
        const string PanelName = "ParticleEditorPanel";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Particle Editor UI")]
        public static void SetupFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Kaplao] Exit Play Mode before running Kaplao/Setup Particle Editor UI.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (!TryGetCanvasAndRoot(scene, out Canvas canvas, out Transform createModeRoot))
                return;

            KaplaoParticleEditorUi ui = EnsureParticleEditorUi(canvas);
            if (!RebuildAndBind(ui, createModeRoot))
            {
                Debug.LogError("[Kaplao] Failed to rebuild Particle Editor UI.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Kaplao] Particle editor UI rebuilt in SampleScene and saved.");
        }

        public static bool RebuildAndBind(KaplaoParticleEditorUi ui, Transform createModeRoot)
        {
            if (ui == null || createModeRoot == null)
                return false;

            Transform existing = createModeRoot.Find(PanelName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            RectTransform panelRoot;
            try
            {
                panelRoot = BuildPanel(createModeRoot);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Kaplao] Particle editor build failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }

            if (panelRoot == null)
                return false;

            ParticleEditorBindings bindings = KaplaoParticleEditorUi.CollectBindingsFromPanel(panelRoot);
            if (!bindings.HasShell)
            {
                Debug.LogError("[Kaplao] Built panel is missing shell controls.");
                return false;
            }

            if (!bindings.HasMotionRows)
            {
                Debug.LogError("[Kaplao] Built panel is missing motion rows (InputField/Slider).");
                return false;
            }

            ui.AssignBindings(bindings, createModeRoot.gameObject);
            PersistSerializedBindings(ui, bindings, createModeRoot.gameObject);

            panelRoot.gameObject.SetActive(false);
            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(panelRoot.gameObject);
            EditorUtility.SetDirty(createModeRoot.gameObject);

            int motionRows = panelRoot.Find("Body/TabsRoot/MotionTabRoot/Viewport/Content")?.childCount ?? 0;
            Debug.Log($"[Kaplao] Particle editor bound: {motionRows} motion rows, {bindings.closeButton != null} close button.");
            return true;
        }

        public static bool TryBindExistingPanel(KaplaoParticleEditorUi ui, Transform createModeRoot)
        {
            if (ui == null || createModeRoot == null)
                return false;

            Transform panel = createModeRoot.Find(PanelName);
            if (panel == null || !panel.TryGetComponent(out RectTransform panelRoot))
                return false;

            ParticleEditorBindings bindings = KaplaoParticleEditorUi.CollectBindingsFromPanel(panelRoot);
            if (!bindings.HasShell || !bindings.HasMotionRows)
                return false;

            ui.AssignBindings(bindings, createModeRoot.gameObject);
            PersistSerializedBindings(ui, bindings, createModeRoot.gameObject);
            panelRoot.gameObject.SetActive(false);
            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(panelRoot.gameObject);
            return true;
        }

        static bool TryGetCanvasAndRoot(Scene scene, out Canvas canvas, out Transform createModeRoot)
        {
            canvas = Object.FindFirstObjectByType<Canvas>();
            createModeRoot = null;

            if (canvas == null)
            {
                Debug.LogWarning($"[Kaplao] Canvas not found in scene '{scene.name}'.");
                return false;
            }

            createModeRoot = FindChildRecursive(canvas.transform, "CreateModeUI");
            if (createModeRoot == null)
            {
                Debug.LogWarning("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
                return false;
            }

            return true;
        }

        static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        static KaplaoParticleEditorUi EnsureParticleEditorUi(Canvas canvas)
        {
            KaplaoParticleEditorUi ui = canvas.GetComponent<KaplaoParticleEditorUi>();
            if (ui == null)
                ui = canvas.gameObject.AddComponent<KaplaoParticleEditorUi>();
            return ui;
        }

        static void PersistSerializedBindings(
            KaplaoParticleEditorUi ui,
            ParticleEditorBindings bindings,
            GameObject kaplaoRoot)
        {
            SerializedObject serializedUi = new SerializedObject(ui);
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = kaplaoRoot;
            CopyBindings(serializedUi.FindProperty("bindings"), bindings);
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CopyBindings(SerializedProperty property, ParticleEditorBindings bindings)
        {
            property.FindPropertyRelative("panelRoot").objectReferenceValue = bindings.panelRoot;
            property.FindPropertyRelative("closeButton").objectReferenceValue = bindings.closeButton;
            property.FindPropertyRelative("motionTabButton").objectReferenceValue = bindings.motionTabButton;
            property.FindPropertyRelative("visualTabButton").objectReferenceValue = bindings.visualTabButton;
            property.FindPropertyRelative("motionTabRoot").objectReferenceValue = bindings.motionTabRoot;
            property.FindPropertyRelative("visualTabRoot").objectReferenceValue = bindings.visualTabRoot;
            property.FindPropertyRelative("enabledToggle").objectReferenceValue = bindings.enabledToggle;
            property.FindPropertyRelative("previewImage").objectReferenceValue = bindings.previewImage;
            property.FindPropertyRelative("previewBackground").objectReferenceValue = bindings.previewBackground;

            CopyFloatBinding(property.FindPropertyRelative("duration"), bindings.duration);
            CopyRangeBinding(property.FindPropertyRelative("lifetime"), bindings.lifetime);
            CopyIntBinding(property.FindPropertyRelative("emission"), bindings.emission);
            CopyRangeBinding(property.FindPropertyRelative("angle"), bindings.angle);
            CopyRangeBinding(property.FindPropertyRelative("speed"), bindings.speed);
            CopyVec2Binding(property.FindPropertyRelative("posVar"), bindings.posVar);
            CopyVec2Binding(property.FindPropertyRelative("gravity"), bindings.gravity);
            CopyRangeBinding(property.FindPropertyRelative("accelRad"), bindings.accelRad);
            CopyRangeBinding(property.FindPropertyRelative("accelTan"), bindings.accelTan);

            CopyRangeBinding(property.FindPropertyRelative("startSize"), bindings.startSize);
            CopyRangeBinding(property.FindPropertyRelative("endSize"), bindings.endSize);
            CopyRangeBinding(property.FindPropertyRelative("startSpin"), bindings.startSpin);
            CopyRangeBinding(property.FindPropertyRelative("endSpin"), bindings.endSpin);
            CopyRangeBinding(property.FindPropertyRelative("startR"), bindings.startR);
            CopyRangeBinding(property.FindPropertyRelative("startG"), bindings.startG);
            CopyRangeBinding(property.FindPropertyRelative("startB"), bindings.startB);
            CopyRangeBinding(property.FindPropertyRelative("startA"), bindings.startA);
            CopyRangeBinding(property.FindPropertyRelative("endR"), bindings.endR);
            CopyRangeBinding(property.FindPropertyRelative("endG"), bindings.endG);
            CopyRangeBinding(property.FindPropertyRelative("endB"), bindings.endB);
            CopyRangeBinding(property.FindPropertyRelative("endA"), bindings.endA);
        }

        static void CopyFloatBinding(SerializedProperty property, ParticleFloatBinding binding)
        {
            property.FindPropertyRelative("input").objectReferenceValue = binding.input;
            property.FindPropertyRelative("slider").objectReferenceValue = binding.slider;
        }

        static void CopyIntBinding(SerializedProperty property, ParticleIntBinding binding)
        {
            property.FindPropertyRelative("input").objectReferenceValue = binding.input;
            property.FindPropertyRelative("slider").objectReferenceValue = binding.slider;
        }

        static void CopyRangeBinding(SerializedProperty property, ParticleFloatRangeBinding binding)
        {
            property.FindPropertyRelative("valueInput").objectReferenceValue = binding.valueInput;
            property.FindPropertyRelative("valueSlider").objectReferenceValue = binding.valueSlider;
            property.FindPropertyRelative("rangeInput").objectReferenceValue = binding.rangeInput;
            property.FindPropertyRelative("rangeSlider").objectReferenceValue = binding.rangeSlider;
        }

        static void CopyVec2Binding(SerializedProperty property, ParticleVec2Binding binding)
        {
            property.FindPropertyRelative("xInput").objectReferenceValue = binding.xInput;
            property.FindPropertyRelative("xSlider").objectReferenceValue = binding.xSlider;
            property.FindPropertyRelative("yInput").objectReferenceValue = binding.yInput;
            property.FindPropertyRelative("ySlider").objectReferenceValue = binding.ySlider;
        }

        static RectTransform BuildPanel(Transform parent)
        {
            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panelObject, "Create Particle Editor Panel");
            panelObject.transform.SetParent(parent, false);
            RectTransform root = panelObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(980f, 620f);
            root.anchoredPosition = Vector2.zero;

            Image bg = panelObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(12, 12, 12, 12);
            panelLayout.spacing = 10f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlHeight = false;
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childForceExpandWidth = true;

            BuildHeader(panelObject.transform);
            BuildBody(panelObject.transform);
            panelObject.SetActive(false);
            return root;
        }

        static void BuildHeader(Transform parent)
        {
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 44f;

            HorizontalLayoutGroup headerH = header.AddComponent<HorizontalLayoutGroup>();
            headerH.childAlignment = TextAnchor.MiddleLeft;
            headerH.spacing = 10f;
            headerH.childForceExpandWidth = false;
            headerH.childForceExpandHeight = false;

            CreateTabButton(header.transform, "Motion", "MotionTabButton");
            CreateTabButton(header.transform, "Visual", "VisualTabButton");

            GameObject enabledRoot = new GameObject("EnabledRoot", typeof(RectTransform));
            enabledRoot.transform.SetParent(header.transform, false);
            LayoutElement enabledLayout = enabledRoot.AddComponent<LayoutElement>();
            enabledLayout.preferredWidth = 220f;
            HorizontalLayoutGroup enabledH = enabledRoot.AddComponent<HorizontalLayoutGroup>();
            enabledH.childAlignment = TextAnchor.MiddleRight;
            enabledH.spacing = 8f;
            enabledH.childForceExpandWidth = false;
            enabledH.childForceExpandHeight = false;
            CreateLabel(enabledRoot.transform, "Enabled");
            CreateToggle(enabledRoot.transform, "EnabledToggle");

            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(header.transform, false);
            LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;

            CreateTabButton(header.transform, "Close", "CloseButton");
        }

        static void BuildBody(Transform parent)
        {
            GameObject body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(parent, false);
            LayoutElement bodyLayout = body.AddComponent<LayoutElement>();
            bodyLayout.flexibleHeight = 1f;
            bodyLayout.minHeight = 500f;
            HorizontalLayoutGroup bodyH = body.AddComponent<HorizontalLayoutGroup>();
            bodyH.childAlignment = TextAnchor.UpperLeft;
            bodyH.spacing = 12f;
            bodyH.childForceExpandHeight = true;
            bodyH.childForceExpandWidth = true;

            GameObject preview = new GameObject("Preview", typeof(RectTransform));
            preview.transform.SetParent(body.transform, false);
            LayoutElement previewLayout = preview.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = 420f;
            previewLayout.flexibleHeight = 1f;
            Image previewBg = preview.AddComponent<Image>();
            previewBg.color = Color.black;

            GameObject previewDisplay = new GameObject("PreviewDisplay", typeof(RectTransform));
            previewDisplay.transform.SetParent(preview.transform, false);
            StretchFull(previewDisplay.GetComponent<RectTransform>());
            RawImage previewRaw = previewDisplay.AddComponent<RawImage>();
            previewRaw.color = Color.white;

            GameObject tabsRoot = new GameObject("TabsRoot", typeof(RectTransform));
            tabsRoot.transform.SetParent(body.transform, false);
            LayoutElement tabsLayout = tabsRoot.AddComponent<LayoutElement>();
            tabsLayout.flexibleWidth = 1f;
            tabsLayout.flexibleHeight = 1f;
            Image tabsBg = tabsRoot.AddComponent<Image>();
            tabsBg.color = new Color(0.1f, 0.11f, 0.14f, 0.95f);
            VerticalLayoutGroup tabsV = tabsRoot.AddComponent<VerticalLayoutGroup>();
            tabsV.padding = new RectOffset(10, 10, 10, 10);
            tabsV.spacing = 10f;
            tabsV.childAlignment = TextAnchor.UpperCenter;
            tabsV.childControlHeight = true;
            tabsV.childControlWidth = true;
            tabsV.childForceExpandHeight = true;
            tabsV.childForceExpandWidth = true;

            RectTransform motionContent = CreateTabRoot(tabsRoot.transform, "MotionTabRoot", true);
            RectTransform visualContent = CreateTabRoot(tabsRoot.transform, "VisualTabRoot", false);

            BuildMotionRows(motionContent);
            BuildVisualRows(visualContent);
        }

        static void BuildMotionRows(RectTransform content)
        {
            CreateFloatRow(content.transform, "Row_Duration", "Duration", -1f, 10f);
            CreateRangeRow(content.transform, "Row_Lifetime", "Lifetime", 0f, 2f, 0f, 2f);
            CreateIntRow(content.transform, "Row_Emission", "Emission", 1, 200);
            CreateRangeRow(content.transform, "Row_Angle", "Angle", -180f, 180f, 0f, 180f);
            CreateRangeRow(content.transform, "Row_Speed", "Speed", 0f, 500f, 0f, 250f);
            CreateVec2Row(content.transform, "Row_PosVar", "PosVar", 0f, 5f);
            CreateVec2Row(content.transform, "Row_Gravity", "Gravity", -1000f, 1000f);
            CreateRangeRow(content.transform, "Row_AccelRad", "AccelRad", -800f, 800f, 0f, 800f);
            CreateRangeRow(content.transform, "Row_AccelTan", "AccelTan", -800f, 800f, 0f, 800f);
        }

        static void BuildVisualRows(RectTransform content)
        {
            CreateRangeRow(content.transform, "Row_StartSize", "StartSize", 0f, 1f, 0f, 1f);
            CreateRangeRow(content.transform, "Row_EndSize", "EndSize", 0f, 1f, 0f, 1f);
            CreateRangeRow(content.transform, "Row_StartSpin", "StartSpin", 0f, 360f, 0f, 360f);
            CreateRangeRow(content.transform, "Row_EndSpin", "EndSpin", 0f, 360f, 0f, 360f);
            CreateRangeRow(content.transform, "Row_Start_R", "Start_R", 0f, 255f, 0f, 255f);
            CreateRangeRow(content.transform, "Row_Start_G", "Start_G", 0f, 255f, 0f, 255f);
            CreateRangeRow(content.transform, "Row_Start_B", "Start_B", 0f, 255f, 0f, 255f);
            CreateRangeRow(content.transform, "Row_Start_A", "Start_A", 0f, 1f, 0f, 1f);
            CreateRangeRow(content.transform, "Row_End_R", "End_R", 0f, 255f, 0f, 255f);
            CreateRangeRow(content.transform, "Row_End_G", "End_G", 0f, 255f, 0f, 255f);
            CreateRangeRow(content.transform, "Row_End_B", "End_B", 0f, 255f, 0f, 255f);
            CreateRangeRow(content.transform, "Row_End_A", "End_A", 0f, 1f, 0f, 1f);
        }

        static RectTransform CreateTabRoot(Transform parent, string name, bool active)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 420f;

            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(root.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            StretchFull(viewportRect);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            scroll.viewport = viewportRect;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.padding = new RectOffset(4, 4, 4, 4);
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = contentRect;

            root.SetActive(active);
            return contentRect;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        static void CreateFloatRow(Transform parent, string rowName, string label, float min, float max)
        {
            GameObject row = CreateRowRoot(parent, rowName);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            CreateLabel(row.transform, label);
            CreateInput(row.transform, "ValueInput", "0");
            CreateSlider(row.transform, "ValueSlider", min, max, false);
        }

        static void CreateIntRow(Transform parent, string rowName, string label, int min, int max)
        {
            GameObject row = CreateRowRoot(parent, rowName);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            CreateLabel(row.transform, label);
            CreateInput(row.transform, "ValueInput", min.ToString());
            CreateSlider(row.transform, "ValueSlider", min, max, true);
        }

        static void CreateRangeRow(
            Transform parent,
            string rowName,
            string label,
            float valueMin,
            float valueMax,
            float rangeMin,
            float rangeMax)
        {
            GameObject row = CreateRowRoot(parent, rowName);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            CreateLabel(row.transform, label);
            CreateInput(row.transform, "ValueInput", "0");
            CreateSlider(row.transform, "ValueSlider", valueMin, valueMax, false);
            CreateLabel(row.transform, "+-");
            CreateInput(row.transform, "RangeInput", "0");
            CreateSlider(row.transform, "RangeSlider", rangeMin, rangeMax, false);
        }

        static void CreateVec2Row(Transform parent, string rowName, string label, float min, float max)
        {
            GameObject row = CreateRowRoot(parent, rowName);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            CreateLabel(row.transform, label);
            CreateInput(row.transform, "XInput", "0");
            CreateSlider(row.transform, "XSlider", min, max, false);
            CreateLabel(row.transform, "Y");
            CreateInput(row.transform, "YInput", "0");
            CreateSlider(row.transform, "YSlider", min, max, false);
        }

        static GameObject CreateRowRoot(Transform parent, string name)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            return row;
        }

        static void CreateTabButton(Transform parent, string text, string objectName)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = CreateLabel(go.transform, text);
            label.alignment = TextAlignmentOptions.Center;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 120f;
            layout.preferredHeight = 36f;
        }

        static void CreateButton(Transform parent, string text, string objectName)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = CreateLabel(go.transform, text);
            label.alignment = TextAlignmentOptions.Center;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 120f;
            layout.preferredHeight = 32f;
        }

        static Toggle CreateToggle(Transform parent, string objectName)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Toggle toggle = go.AddComponent<Toggle>();
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
            toggle.targetGraphic = bg;

            GameObject check = new GameObject("Checkmark", typeof(RectTransform));
            check.transform.SetParent(go.transform, false);
            Image checkImage = check.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.75f, 0.3f, 0.95f);
            toggle.graphic = checkImage;

            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 32f;
            layout.preferredHeight = 32f;
            return toggle;
        }

        static TMP_InputField CreateInput(Transform parent, string objectName, string initial)
        {
            GameObject fieldObject = new GameObject(objectName, typeof(RectTransform));
            fieldObject.transform.SetParent(parent, false);
            LayoutElement fieldLayout = fieldObject.AddComponent<LayoutElement>();
            fieldLayout.preferredWidth = 80f;
            fieldLayout.preferredHeight = 28f;

            Image fieldBackground = fieldObject.AddComponent<Image>();
            fieldBackground.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(fieldObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(6f, 2f);
            textAreaRect.offsetMax = new Vector2(-6f, -2f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            StretchFull(textRect);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = initial;
            text.fontSize = 14f;
            text.color = Color.white;
            text.raycastTarget = false;

            TMP_InputField inputField = fieldObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.text = initial;
            return inputField;
        }

        static Slider CreateSlider(Transform parent, string objectName, float min, float max, bool wholeNumbers)
        {
            GameObject sliderObject = new GameObject(objectName, typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);
            LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
            sliderLayout.preferredWidth = 180f;
            sliderLayout.preferredHeight = 20f;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;

            GameObject background = new GameObject("Background", typeof(RectTransform));
            background.transform.SetParent(sliderObject.transform, false);
            StretchFull(background.GetComponent<RectTransform>());
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            StretchFull(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(6f, 4f);
            fillAreaRect.offsetMax = new Vector2(-6f, -4f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.35f, 0.55f, 0.95f, 1f);
            slider.fillRect = fill.GetComponent<RectTransform>();

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            StretchFull(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(6f, 0f);
            handleAreaRect.offsetMax = new Vector2(-6f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12f, 0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            return slider;
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string text)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 120f;
            layout.preferredHeight = 28f;
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 14f;
            label.color = new Color(1f, 1f, 1f, 0.92f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }
    }
}
#endif
