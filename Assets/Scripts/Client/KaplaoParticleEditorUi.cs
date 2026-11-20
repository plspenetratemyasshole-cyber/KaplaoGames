using System;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class KaplaoParticleEditorUi : MonoBehaviour
    {
        [SerializeField] GameObject kaplaoOnlyRoot;
        [SerializeField] ParticleEditorBindings bindings = new();

        const string FloatUiFormat = "0.0";

        KaplaoCursorServer cursorServer;
        NetworkPlayer localPlayer;
        bool panelWired;
        bool suppressApply;
        ParticleEmitterRecord current;
        ParticlePreview preview;

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;
        public RectTransform PanelRoot => bindings.panelRoot;
        public ParticleEditorBindings Bindings => bindings;
        public bool IsOpen => bindings.panelRoot != null && bindings.panelRoot.gameObject.activeSelf;

        public void SetKaplaoOnlyRoot(GameObject root) => kaplaoOnlyRoot = root;

        public void AssignBindings(ParticleEditorBindings newBindings, GameObject kaplaoRoot = null)
        {
            if (newBindings != null)
                bindings = newBindings;

            if (kaplaoRoot != null)
                kaplaoOnlyRoot = kaplaoRoot;

            panelWired = false;

            if (isActiveAndEnabled && bindings.panelRoot != null)
                WirePanel();
        }

        public static ParticleEditorBindings CollectBindingsFromPanel(RectTransform panelRoot)
        {
            var result = new ParticleEditorBindings { panelRoot = panelRoot };
            if (panelRoot == null)
                return result;

            Transform header = panelRoot.Find("Header");
            Transform body = panelRoot.Find("Body");
            if (header == null || body == null)
                return result;

            Transform preview = body.Find("Preview");
            Transform tabsRoot = body.Find("TabsRoot");
            if (preview == null || tabsRoot == null)
                return result;

            result.closeButton = header.Find("CloseButton")?.GetComponent<Button>();
            result.motionTabButton = header.Find("MotionTabButton")?.GetComponent<Button>();
            result.visualTabButton = header.Find("VisualTabButton")?.GetComponent<Button>();
            result.enabledToggle = header.Find("EnabledRoot/EnabledToggle")?.GetComponent<Toggle>();
            result.previewBackground = preview.GetComponent<Image>();
            result.previewImage = preview.Find("PreviewDisplay")?.GetComponent<RawImage>();
            if (result.previewImage == null)
                result.previewImage = preview.GetComponentInChildren<RawImage>(true);
            result.motionTabRoot = tabsRoot.Find("MotionTabRoot")?.GetComponent<RectTransform>();
            result.visualTabRoot = tabsRoot.Find("VisualTabRoot")?.GetComponent<RectTransform>();

            Transform motionContent = tabsRoot.Find("MotionTabRoot/Viewport/Content");
            Transform visualContent = tabsRoot.Find("VisualTabRoot/Viewport/Content");

            if (motionContent != null)
            {
                result.duration = CollectFloatBinding(motionContent, "Row_Duration");
                result.lifetime = CollectRangeBinding(motionContent, "Row_Lifetime");
                result.emission = CollectIntBinding(motionContent, "Row_Emission");
                result.angle = CollectRangeBinding(motionContent, "Row_Angle");
                result.speed = CollectRangeBinding(motionContent, "Row_Speed");
                result.posVar = CollectVec2Binding(motionContent, "Row_PosVar");
                result.gravity = CollectVec2Binding(motionContent, "Row_Gravity");
                result.accelRad = CollectRangeBinding(motionContent, "Row_AccelRad");
                result.accelTan = CollectRangeBinding(motionContent, "Row_AccelTan");
            }

            if (visualContent != null)
            {
                result.startSize = CollectRangeBinding(visualContent, "Row_StartSize");
                result.endSize = CollectRangeBinding(visualContent, "Row_EndSize");
                result.startSpin = CollectRangeBinding(visualContent, "Row_StartSpin");
                result.endSpin = CollectRangeBinding(visualContent, "Row_EndSpin");
                result.startR = CollectRangeBinding(visualContent, "Row_Start_R");
                result.startG = CollectRangeBinding(visualContent, "Row_Start_G");
                result.startB = CollectRangeBinding(visualContent, "Row_Start_B");
                result.startA = CollectRangeBinding(visualContent, "Row_Start_A");
                result.endR = CollectRangeBinding(visualContent, "Row_End_R");
                result.endG = CollectRangeBinding(visualContent, "Row_End_G");
                result.endB = CollectRangeBinding(visualContent, "Row_End_B");
                result.endA = CollectRangeBinding(visualContent, "Row_End_A");
            }

            return result;
        }

        static ParticleFloatBinding CollectFloatBinding(Transform content, string rowName)
        {
            Transform row = content.Find(rowName);
            if (row == null)
                return new ParticleFloatBinding();

            return new ParticleFloatBinding
            {
                input = row.Find("ValueInput")?.GetComponent<TMP_InputField>(),
                slider = row.Find("ValueSlider")?.GetComponent<Slider>()
            };
        }

        static ParticleIntBinding CollectIntBinding(Transform content, string rowName)
        {
            Transform row = content.Find(rowName);
            if (row == null)
                return new ParticleIntBinding();

            return new ParticleIntBinding
            {
                input = row.Find("ValueInput")?.GetComponent<TMP_InputField>(),
                slider = row.Find("ValueSlider")?.GetComponent<Slider>()
            };
        }

        static ParticleFloatRangeBinding CollectRangeBinding(Transform content, string rowName)
        {
            Transform row = content.Find(rowName);
            if (row == null)
                return new ParticleFloatRangeBinding();

            return new ParticleFloatRangeBinding
            {
                valueInput = row.Find("ValueInput")?.GetComponent<TMP_InputField>(),
                valueSlider = row.Find("ValueSlider")?.GetComponent<Slider>(),
                rangeInput = row.Find("RangeInput")?.GetComponent<TMP_InputField>(),
                rangeSlider = row.Find("RangeSlider")?.GetComponent<Slider>()
            };
        }

        static ParticleVec2Binding CollectVec2Binding(Transform content, string rowName)
        {
            Transform row = content.Find(rowName);
            if (row == null)
                return new ParticleVec2Binding();

            return new ParticleVec2Binding
            {
                xInput = row.Find("XInput")?.GetComponent<TMP_InputField>(),
                xSlider = row.Find("XSlider")?.GetComponent<Slider>(),
                yInput = row.Find("YInput")?.GetComponent<TMP_InputField>(),
                ySlider = row.Find("YSlider")?.GetComponent<Slider>()
            };
        }

        void OnEnable()
        {
            if (kaplaoOnlyRoot == null)
                kaplaoOnlyRoot = gameObject;

            TryResolveBindingsFromScene();
            WirePanel();
            Hide();
            StartCoroutine(BindWhenReady());
        }

        void OnDisable()
        {
            UnwirePanel();
            ClearKaplaoBinding();
        }

        System.Collections.IEnumerator BindWhenReady()
        {
            while (enabled)
            {
                if (NetworkClient.localPlayer != null &&
                    NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player))
                {
                    if (localPlayer != player)
                        localPlayer = player;

                    if (player.role == PlayerRole.Kaplao)
                    {
                        KaplaoCursorServer nextServer = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
                        if (cursorServer != nextServer)
                            cursorServer = nextServer;
                    }
                    else
                    {
                        ClearKaplaoBinding();
                    }
                }
                else
                {
                    ClearKaplaoBinding();
                }

                yield return null;
            }
        }

        void ClearKaplaoBinding()
        {
            localPlayer = null;
            cursorServer = null;
        }

        void TryResolveBindingsFromScene()
        {
            if (bindings.HasShell && bindings.duration.input != null)
                return;

            Transform createModeRoot = transform.Find("CreateModeUI");
            if (createModeRoot == null)
            {
                GameObject createModeObject = GameObject.Find("CreateModeUI");
                if (createModeObject != null)
                    createModeRoot = createModeObject.transform;
            }

            if (createModeRoot == null)
                return;

            Transform panel = createModeRoot.Find("ParticleEditorPanel");
            if (panel == null || !panel.TryGetComponent(out RectTransform panelRect))
                return;

            AssignBindings(CollectBindingsFromPanel(panelRect), createModeRoot.gameObject);
        }

        void WirePanel()
        {
            if (panelWired || bindings.panelRoot == null || !bindings.HasShell)
                return;

            bindings.closeButton.onClick.RemoveListener(Hide);
            bindings.closeButton.onClick.AddListener(Hide);

            bindings.motionTabButton.onClick.RemoveListener(ShowMotionTab);
            bindings.motionTabButton.onClick.AddListener(ShowMotionTab);

            bindings.visualTabButton.onClick.RemoveListener(ShowVisualTab);
            bindings.visualTabButton.onClick.AddListener(ShowVisualTab);

            if (bindings.enabledToggle != null)
            {
                bindings.enabledToggle.onValueChanged.RemoveListener(OnEnabledChanged);
                bindings.enabledToggle.onValueChanged.AddListener(OnEnabledChanged);
            }

            WireFloat(bindings.duration, -1f, 10f, () => current.duration, v => current.duration = v);
            WireRange(bindings.lifetime, 0f, 2f, 0f, 2f,
                () => current.lifetime.value, v => current.lifetime.value = v,
                () => current.lifetime.range, v => current.lifetime.range = v);
            WireInt(bindings.emission, 1, 200, () => current.emission, v => current.emission = v);
            WireRange(bindings.angle, -180f, 180f, 0f, 180f,
                () => current.angle.value, v => current.angle.value = v,
                () => current.angle.range, v => current.angle.range = v);
            WireRange(bindings.speed, 0f, 500f, 0f, 250f,
                () => current.speed.value, v => current.speed.value = v,
                () => current.speed.range, v => current.speed.range = v);
            WireVec2(bindings.posVar, 0f, 5f,
                () => current.posVarX, v => current.posVarX = v,
                () => current.posVarY, v => current.posVarY = v);
            WireVec2(bindings.gravity, -1000f, 1000f,
                () => current.gravityX, v => current.gravityX = v,
                () => current.gravityY, v => current.gravityY = v);
            WireRange(bindings.accelRad, -800f, 800f, 0f, 800f,
                () => current.accelRad.value, v => current.accelRad.value = v,
                () => current.accelRad.range, v => current.accelRad.range = v);
            WireRange(bindings.accelTan, -800f, 800f, 0f, 800f,
                () => current.accelTan.value, v => current.accelTan.value = v,
                () => current.accelTan.range, v => current.accelTan.range = v);

            WireRange(bindings.startSize, 0f, 1f, 0f, 1f,
                () => current.startSize.value, v => current.startSize.value = v,
                () => current.startSize.range, v => current.startSize.range = v);
            WireRange(bindings.endSize, 0f, 1f, 0f, 1f,
                () => current.endSize.value, v => current.endSize.value = v,
                () => current.endSize.range, v => current.endSize.range = v);
            WireRange(bindings.startSpin, 0f, 360f, 0f, 360f,
                () => current.startSpin.value, v => current.startSpin.value = v,
                () => current.startSpin.range, v => current.startSpin.range = v);
            WireRange(bindings.endSpin, 0f, 360f, 0f, 360f,
                () => current.endSpin.value, v => current.endSpin.value = v,
                () => current.endSpin.range, v => current.endSpin.range = v);
            WireRange(bindings.startR, 0f, 255f, 0f, 255f,
                () => current.startR.value, v => current.startR.value = v,
                () => current.startR.range, v => current.startR.range = v);
            WireRange(bindings.startG, 0f, 255f, 0f, 255f,
                () => current.startG.value, v => current.startG.value = v,
                () => current.startG.range, v => current.startG.range = v);
            WireRange(bindings.startB, 0f, 255f, 0f, 255f,
                () => current.startB.value, v => current.startB.value = v,
                () => current.startB.range, v => current.startB.range = v);
            WireRange(bindings.startA, 0f, 1f, 0f, 1f,
                () => current.startA.value, v => current.startA.value = v,
                () => current.startA.range, v => current.startA.range = v);
            WireRange(bindings.endR, 0f, 255f, 0f, 255f,
                () => current.endR.value, v => current.endR.value = v,
                () => current.endR.range, v => current.endR.range = v);
            WireRange(bindings.endG, 0f, 255f, 0f, 255f,
                () => current.endG.value, v => current.endG.value = v,
                () => current.endG.range, v => current.endG.range = v);
            WireRange(bindings.endB, 0f, 255f, 0f, 255f,
                () => current.endB.value, v => current.endB.value = v,
                () => current.endB.range, v => current.endB.range = v);
            WireRange(bindings.endA, 0f, 1f, 0f, 1f,
                () => current.endA.value, v => current.endA.value = v,
                () => current.endA.range, v => current.endA.range = v);

            EnsurePreview();
            panelWired = true;
        }

        void UnwirePanel()
        {
            if (!panelWired)
                return;

            if (bindings.closeButton != null)
                bindings.closeButton.onClick.RemoveListener(Hide);
            if (bindings.motionTabButton != null)
                bindings.motionTabButton.onClick.RemoveListener(ShowMotionTab);
            if (bindings.visualTabButton != null)
                bindings.visualTabButton.onClick.RemoveListener(ShowVisualTab);
            if (bindings.enabledToggle != null)
                bindings.enabledToggle.onValueChanged.RemoveListener(OnEnabledChanged);

            panelWired = false;
        }

        void ShowMotionTab() => SetTab(ParticleEditorTab.Motion);
        void ShowVisualTab() => SetTab(ParticleEditorTab.Visual);

        public void ShowForSelection()
        {
            TryResolveBindingsFromScene();
            WirePanel();

            if (bindings.panelRoot == null || !bindings.HasShell || !bindings.HasMotionRows)
            {
                Debug.LogWarning("[Kaplao] Particle editor panel is missing or incomplete. Run Kaplao/Setup Particle Editor UI (opens SampleScene).");
                return;
            }

            if (IsOpen)
            {
                Hide();
                return;
            }

            if (cursorServer == null || localPlayer == null || localPlayer.role != PlayerRole.Kaplao)
            {
                Debug.LogWarning("[Kaplao] Particle editor requires Kaplao in Create mode.");
                return;
            }

            if (!TryGetFirstSelectedEmitter(out ParticleEmitterTile emitter))
            {
                Debug.LogWarning("[Kaplao] Select a Particle tile to edit.");
                return;
            }

            current = emitter.ExportRecord();
            SyncUiFromRecord();
            EnsurePreview();
            preview.SetRunning(true);
            preview.Apply(current);

            bindings.panelRoot.SetAsLastSibling();
            bindings.panelRoot.gameObject.SetActive(true);
            SetTab(ParticleEditorTab.Motion);
        }

        public void Hide()
        {
            preview?.SetRunning(false);
            if (bindings.panelRoot != null)
                bindings.panelRoot.gameObject.SetActive(false);
        }

        void SetTab(ParticleEditorTab tab)
        {
            if (bindings.motionTabRoot != null)
                bindings.motionTabRoot.gameObject.SetActive(tab == ParticleEditorTab.Motion);
            if (bindings.visualTabRoot != null)
                bindings.visualTabRoot.gameObject.SetActive(tab == ParticleEditorTab.Visual);
        }

        void OnEnabledChanged(bool enabled)
        {
            current.enabled = enabled;
            ApplyToSelection();
        }

        void SyncUiFromRecord()
        {
            suppressApply = true;

            if (bindings.enabledToggle != null)
                bindings.enabledToggle.SetIsOnWithoutNotify(current.enabled);

            SyncFloat(bindings.duration, current.duration);
            SyncRange(bindings.lifetime, current.lifetime.value, current.lifetime.range);
            SyncInt(bindings.emission, current.emission);
            SyncRange(bindings.angle, current.angle.value, current.angle.range);
            SyncRange(bindings.speed, current.speed.value, current.speed.range);
            SyncVec2(bindings.posVar, current.posVarX, current.posVarY);
            SyncVec2(bindings.gravity, current.gravityX, current.gravityY);
            SyncRange(bindings.accelRad, current.accelRad.value, current.accelRad.range);
            SyncRange(bindings.accelTan, current.accelTan.value, current.accelTan.range);

            SyncRange(bindings.startSize, current.startSize.value, current.startSize.range);
            SyncRange(bindings.endSize, current.endSize.value, current.endSize.range);
            SyncRange(bindings.startSpin, current.startSpin.value, current.startSpin.range);
            SyncRange(bindings.endSpin, current.endSpin.value, current.endSpin.range);
            SyncRange(bindings.startR, current.startR.value, current.startR.range);
            SyncRange(bindings.startG, current.startG.value, current.startG.range);
            SyncRange(bindings.startB, current.startB.value, current.startB.range);
            SyncRange(bindings.startA, current.startA.value, current.startA.range);
            SyncRange(bindings.endR, current.endR.value, current.endR.range);
            SyncRange(bindings.endG, current.endG.value, current.endG.range);
            SyncRange(bindings.endB, current.endB.value, current.endB.range);
            SyncRange(bindings.endA, current.endA.value, current.endA.range);

            suppressApply = false;
        }

        bool particleApplyQueued;
        ParticleEmitterRecord pendingParticleApply;

        void ApplyToSelection()
        {
            if (suppressApply)
                return;

            // Preview is local-only; tile mutation goes through Command so remote Kaplao works.
            preview?.Apply(current);
            pendingParticleApply = current;
            if (particleApplyQueued)
                return;

            particleApplyQueued = true;
            StartCoroutine(FlushParticleApplyNextFrame());
        }

        System.Collections.IEnumerator FlushParticleApplyNextFrame()
        {
            yield return null;
            particleApplyQueued = false;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            client?.CmdApplyParticleSettingsToSelectedTiles(pendingParticleApply);
        }

        void WireFloat(ParticleFloatBinding field, float min, float max, Func<float> get, Action<float> set)
        {
            if (field.input != null)
            {
                field.input.onEndEdit.RemoveAllListeners();
                field.input.onEndEdit.AddListener(v =>
                {
                    if (suppressApply || !KaplaoNumericInputUtility.TryParseFloat(v, out float parsed))
                        return;

                    set(parsed);
                    SyncSliderOnly(field, parsed, min, max);
                    ApplyToSelection();
                });
            }

            if (field.slider != null)
            {
                field.slider.minValue = min;
                field.slider.maxValue = max;
                field.slider.onValueChanged.RemoveAllListeners();
                field.slider.onValueChanged.AddListener(v =>
                {
                    if (suppressApply)
                        return;

                    float clamped = Mathf.Clamp(v, min, max);
                    set(clamped);
                    SyncFloat(field, clamped);
                    ApplyToSelection();
                });
            }
        }

        void WireInt(ParticleIntBinding field, int min, int max, Func<int> get, Action<int> set)
        {
            if (field.input != null)
            {
                field.input.onEndEdit.RemoveAllListeners();
                field.input.onEndEdit.AddListener(v =>
                {
                    if (suppressApply || !int.TryParse(v, out int parsed))
                        return;

                    set(parsed);
                    SyncSliderOnly(field, parsed, min, max);
                    ApplyToSelection();
                });
            }

            if (field.slider != null)
            {
                field.slider.minValue = min;
                field.slider.maxValue = max;
                field.slider.wholeNumbers = true;
                field.slider.onValueChanged.RemoveAllListeners();
                field.slider.onValueChanged.AddListener(v =>
                {
                    if (suppressApply)
                        return;

                    int clamped = Mathf.Clamp(Mathf.RoundToInt(v), min, max);
                    set(clamped);
                    SyncInt(field, clamped);
                    ApplyToSelection();
                });
            }
        }

        void WireRange(
            ParticleFloatRangeBinding field,
            float valueMin,
            float valueMax,
            float rangeMin,
            float rangeMax,
            Func<float> getValue,
            Action<float> setValue,
            Func<float> getRange,
            Action<float> setRange)
        {
            WireFloat(new ParticleFloatBinding { input = field.valueInput, slider = field.valueSlider },
                valueMin, valueMax, getValue, setValue);
            WireFloat(new ParticleFloatBinding { input = field.rangeInput, slider = field.rangeSlider },
                rangeMin, rangeMax, getRange, setRange);
        }

        void WireVec2(
            ParticleVec2Binding field,
            float min,
            float max,
            Func<float> getX,
            Action<float> setX,
            Func<float> getY,
            Action<float> setY)
        {
            WireFloat(new ParticleFloatBinding { input = field.xInput, slider = field.xSlider }, min, max, getX, setX);
            WireFloat(new ParticleFloatBinding { input = field.yInput, slider = field.ySlider }, min, max, getY, setY);
        }

        static void SyncFloat(ParticleFloatBinding field, float value)
        {
            if (field.input != null)
                field.input.SetTextWithoutNotify(FormatFloat(value));
            if (field.slider != null)
                field.slider.SetValueWithoutNotify(Mathf.Clamp(value, field.slider.minValue, field.slider.maxValue));
        }

        static void SyncSliderOnly(ParticleFloatBinding field, float value, float sliderMin, float sliderMax)
        {
            if (field.slider != null)
                field.slider.SetValueWithoutNotify(Mathf.Clamp(value, sliderMin, sliderMax));
        }

        static string FormatFloat(float value) => value.ToString(FloatUiFormat);

        static void SyncInt(ParticleIntBinding field, int value)
        {
            if (field.input != null)
                field.input.SetTextWithoutNotify(value.ToString());
            if (field.slider != null)
                field.slider.SetValueWithoutNotify(Mathf.Clamp(value, (int)field.slider.minValue, (int)field.slider.maxValue));
        }

        static void SyncSliderOnly(ParticleIntBinding field, int value, int sliderMin, int sliderMax)
        {
            if (field.slider != null)
                field.slider.SetValueWithoutNotify(Mathf.Clamp(value, sliderMin, sliderMax));
        }

        static void SyncRange(ParticleFloatRangeBinding field, float value, float range)
        {
            SyncFloat(new ParticleFloatBinding { input = field.valueInput, slider = field.valueSlider }, value);
            SyncFloat(new ParticleFloatBinding { input = field.rangeInput, slider = field.rangeSlider }, range);
        }

        static void SyncVec2(ParticleVec2Binding field, float x, float y)
        {
            SyncFloat(new ParticleFloatBinding { input = field.xInput, slider = field.xSlider }, x);
            SyncFloat(new ParticleFloatBinding { input = field.yInput, slider = field.ySlider }, y);
        }

        bool TryGetFirstSelectedEmitter(out ParticleEmitterTile emitter)
        {
            emitter = null;
            if (cursorServer == null || cursorServer.SelectedTiles.Count == 0)
                return false;

            for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTile(cursorServer.SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Particle)
                    continue;

                if (tile.TryGetComponent(out emitter) && emitter != null)
                    return true;
            }

            return false;
        }

        void EnsurePreview()
        {
            if (preview == null)
                preview = gameObject.AddComponent<ParticlePreview>();

            preview.Assign(bindings.previewImage, bindings.previewBackground);
        }

        enum ParticleEditorTab : byte
        {
            Motion,
            Visual
        }
    }

    sealed class ParticlePreview : MonoBehaviour
    {
        const int TextureSize = 360;
        static readonly Vector3 PreviewWorldOrigin = new Vector3(10000f, 10000f, 0f);
        static Transform sharedWorldRoot;

        RawImage target;
        Image background;
        Camera cam;
        RenderTexture rt;
        Transform root;
        readonly System.Collections.Generic.List<SpriteRenderer> pool = new();
        readonly System.Collections.Generic.List<PreviewParticle> particles = new();
        float accumulator;
        ParticleEmitterRecord record;
        bool running;
        int previewLayer = -1;

        sealed class PreviewParticle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public float StartSize;
            public float EndSize;
            public float StartSpin;
            public float EndSpin;
            public Color StartColor;
            public Color EndColor;
            public float AccelRad;
            public float AccelTan;
        }

        public void Assign(RawImage preview, Image bg)
        {
            target = preview;
            background = bg;
            Ensure();
            BindRenderTarget();
        }

        void BindRenderTarget()
        {
            if (target == null || rt == null)
                return;

            target.texture = rt;
            target.color = Color.white;
            if (background != null)
                background.color = Color.black;
        }

        public void Apply(ParticleEmitterRecord newRecord)
        {
            if (!newRecord.enabled)
                ClearParticles();

            record = newRecord;
        }

        public void SetRunning(bool value)
        {
            running = value;
            if (!running)
            {
                ClearParticles();
                RenderFrame();
            }
        }

        void OnEnable()
        {
            Ensure();
        }

        void OnDestroy()
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }

        static Transform GetSharedWorldRoot()
        {
            if (sharedWorldRoot != null)
                return sharedWorldRoot;

            var worldRoot = new GameObject("ParticlePreviewWorld");
            worldRoot.transform.position = PreviewWorldOrigin;
            worldRoot.hideFlags = HideFlags.HideAndDontSave;
            sharedWorldRoot = worldRoot.transform;
            return sharedWorldRoot;
        }

        static int ResolvePreviewLayer()
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            return layer >= 0 ? layer : 2;
        }

        void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        void Ensure()
        {
            if (previewLayer < 0)
                previewLayer = ResolvePreviewLayer();

            if (cam == null)
            {
                Transform worldRoot = GetSharedWorldRoot();

                GameObject camObj = new GameObject("ParticlePreviewCamera");
                camObj.transform.SetParent(worldRoot, false);
                camObj.transform.localPosition = new Vector3(0f, 0f, -10f);
                cam = camObj.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 6f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 1 << previewLayer;
                cam.enabled = false;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                camObj.AddComponent<UniversalAdditionalCameraData>();
                SetLayerRecursive(camObj, previewLayer);

                rt = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;

                GameObject rootObj = new GameObject("ParticlePreviewRoot");
                rootObj.transform.SetParent(worldRoot, false);
                rootObj.transform.localPosition = Vector3.zero;
                root = rootObj.transform;
                SetLayerRecursive(rootObj, previewLayer);
            }

            BindRenderTarget();
        }

        void Update()
        {
            if (!running || cam == null || rt == null)
                return;

            if (record.enabled)
                Emit(Time.deltaTime);
            else
                ClearParticles();

            Simulate(Time.deltaTime);
            RenderFrame();
        }

        void RenderFrame()
        {
            if (cam != null)
                cam.Render();
        }

        void ClearParticles()
        {
            accumulator = 0f;
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].Renderer != null)
                    particles[i].Renderer.gameObject.SetActive(false);
            }

            particles.Clear();
        }

        void Emit(float dt)
        {
            float rate = Mathf.Max(0f, record.emission);
            accumulator += rate * dt;
            int toSpawn = Mathf.FloorToInt(accumulator);
            if (toSpawn <= 0)
                return;
            accumulator -= toSpawn;

            for (int i = 0; i < toSpawn; i++)
                Spawn();
        }

        void Spawn()
        {
            SpriteRenderer r = GetRenderer();
            r.sprite = ResolveSprite(record.textureId);

            Vector2 pos = new Vector2(
                UnityEngine.Random.Range(-record.posVarX, record.posVarX),
                UnityEngine.Random.Range(-record.posVarY, record.posVarY));

            float lifetime = Mathf.Max(0.05f, Sample(record.lifetime.value, record.lifetime.range));
            float angle = Sample(record.angle.value, record.angle.range) * Mathf.Deg2Rad;
            float speed = Mathf.Max(0f, Sample(record.speed.value, record.speed.range));
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            var p = new PreviewParticle
            {
                Renderer = r,
                Position = pos,
                Velocity = vel,
                Age = 0f,
                Lifetime = lifetime,
                StartSize = Mathf.Max(0f, Sample(record.startSize.value, record.startSize.range)),
                EndSize = Mathf.Max(0f, Sample(record.endSize.value, record.endSize.range)),
                StartSpin = Sample(record.startSpin.value, record.startSpin.range),
                EndSpin = Sample(record.endSpin.value, record.endSpin.range),
                StartColor = new Color(
                    Mathf.Clamp01(Sample(record.startR.value, record.startR.range) / 255f),
                    Mathf.Clamp01(Sample(record.startG.value, record.startG.range) / 255f),
                    Mathf.Clamp01(Sample(record.startB.value, record.startB.range) / 255f),
                    Mathf.Clamp01(Sample(record.startA.value, record.startA.range))),
                EndColor = new Color(
                    Mathf.Clamp01(Sample(record.endR.value, record.endR.range) / 255f),
                    Mathf.Clamp01(Sample(record.endG.value, record.endG.range) / 255f),
                    Mathf.Clamp01(Sample(record.endB.value, record.endB.range) / 255f),
                    Mathf.Clamp01(Sample(record.endA.value, record.endA.range))),
                AccelRad = Sample(record.accelRad.value, record.accelRad.range),
                AccelTan = Sample(record.accelTan.value, record.accelTan.range)
            };

            particles.Add(p);
        }

        void Simulate(float dt)
        {
            Vector2 gravity = new Vector2(record.gravityX, record.gravityY);

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                PreviewParticle p = particles[i];
                p.Age += dt;
                float t = p.Age / p.Lifetime;
                if (t >= 1f)
                {
                    p.Renderer.gameObject.SetActive(false);
                    particles.RemoveAt(i);
                    continue;
                }

                Vector2 v = p.Velocity;
                Vector2 accel = ParticleMotion.ComputeAcceleration(p.Position, gravity, p.AccelRad, p.AccelTan);

                v += accel * dt;
                p.Velocity = v;
                p.Position += v * dt;

                p.Renderer.transform.localPosition = new Vector3(p.Position.x, p.Position.y, 0f);
                float size = Mathf.Lerp(p.StartSize, p.EndSize, t);
                p.Renderer.transform.localScale = new Vector3(size, size, 1f);
                float spin = Mathf.Lerp(p.StartSpin, p.EndSpin, t);
                p.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
                p.Renderer.color = Color.Lerp(p.StartColor, p.EndColor, t);
            }
        }

        SpriteRenderer GetRenderer()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && !pool[i].gameObject.activeSelf)
                {
                    pool[i].gameObject.SetActive(true);
                    return pool[i];
                }
            }

            GameObject go = new GameObject("P", typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            go.layer = previewLayer >= 0 ? previewLayer : ResolvePreviewLayer();
            SpriteRenderer r = go.GetComponent<SpriteRenderer>();
            r.sortingOrder = 10;
            pool.Add(r);
            return r;
        }

        static Sprite ResolveSprite(string id) => ParticleSpriteUtility.ResolveSprite(id);

        static float Sample(float v, float range)
        {
            if (range <= 0f)
                return v;
            return UnityEngine.Random.Range(v - range, v + range);
        }
    }
}
