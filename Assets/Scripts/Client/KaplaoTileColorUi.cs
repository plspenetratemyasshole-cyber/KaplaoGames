using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Full 24-bit color picker (HSV + RGB + hex) with preset swatches for Create mode.
    /// Panel is built in the scene via Kaplao/Setup Create Mode UI so you can move widgets in the Editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoTileColorUi : MonoBehaviour
    {
        [SerializeField] KaplaoTileColorState colorState;
        [SerializeField] bool autoFindLocalKaplao = true;

        [Header("Visibility")]
        [SerializeField] GameObject kaplaoOnlyRoot;

        [Header("Panel references (TileColorPanel under CreateModeUI)")]
        [SerializeField] RectTransform panelRoot;
        [SerializeField] Image previewFill;
        [SerializeField] RawImage svImage;
        [SerializeField] RawImage hueImage;
        [SerializeField] RectTransform svPickerRoot;
        [SerializeField] RectTransform huePickerRoot;
        [SerializeField] RectTransform swatchGridRoot;
        [SerializeField] Slider redSlider;
        [SerializeField] Slider greenSlider;
        [SerializeField] Slider blueSlider;
        [SerializeField] TMP_InputField hexField;

        readonly List<SwatchView> swatches = new();
        RectTransform svCursor;
        RectTransform hueCursor;
        Texture2D svTexture;
        Texture2D hueTexture;
        KaplaoCursorServer cursorServer;
        bool panelWired;
        bool updatingUi;
        float hue;
        float saturation = 1f;
        float value = 1f;

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;
        public RectTransform PanelRoot => panelRoot;

        void OnEnable()
        {
            if (colorState != null)
                colorState.ColorChanged += OnColorStateChanged;

            if (panelRoot != null)
                WirePanel();

            SyncUiFromState();
            RefreshPanelVisibilityForPlayMode();
            StartCoroutine(WatchLocalKaplao());
        }

        void OnDisable()
        {
            if (colorState != null)
                colorState.ColorChanged -= OnColorStateChanged;

            ClearKaplaoBinding();
        }

        void OnDestroy()
        {
            SafeDestroy(svTexture);
            svTexture = null;
            SafeDestroy(hueTexture);
            hueTexture = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || panelRoot == null)
                return;

            UnityEditor.EditorApplication.delayCall += RefreshEditorPickerPreview;
        }

        void RefreshEditorPickerPreview()
        {
            if (this == null || Application.isPlaying || panelRoot == null)
                return;

            EnsurePickerTextures();
        }
#endif

        static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        public void AssignPanelReferences(PanelReferences references)
        {
            panelRoot = references.PanelRoot;
            previewFill = references.PreviewFill;
            svImage = references.SvImage;
            hueImage = references.HueImage;
            svPickerRoot = references.SvPickerRoot;
            huePickerRoot = references.HuePickerRoot;
            swatchGridRoot = references.SwatchGridRoot;
            redSlider = references.RedSlider;
            greenSlider = references.GreenSlider;
            blueSlider = references.BlueSlider;
            hexField = references.HexField;
            panelWired = false;

            if (isActiveAndEnabled && panelRoot != null)
            {
                WirePanel();
                SyncUiFromState();
            }
        }

        IEnumerator WatchLocalKaplao()
        {
            while (enabled)
            {
                if (!autoFindLocalKaplao)
                {
                    yield return null;
                    continue;
                }

                NetworkIdentity localIdentity = NetworkClient.localPlayer;
                if (localIdentity == null ||
                    !localIdentity.TryGetComponent(out NetworkPlayer networkPlayer) ||
                    networkPlayer.role != PlayerRole.Kaplao)
                {
                    ClearKaplaoBinding();
                    RefreshPanelVisibilityForPlayMode(HostGameMode.Usual);
                    yield return null;
                    continue;
                }

                KaplaoTileColorState nextState = localIdentity.GetComponent<KaplaoTileColorState>();
                KaplaoCursorServer nextServer = localIdentity.GetComponent<KaplaoCursorServer>();
                if (nextState == null || nextServer == null)
                {
                    ClearKaplaoBinding();
                    RefreshPanelVisibilityForPlayMode(HostGameMode.Usual);
                    yield return null;
                    continue;
                }

                if (colorState != nextState || cursorServer != nextServer)
                {
                    ClearKaplaoBinding();
                    colorState = nextState;
                    colorState.ColorChanged += OnColorStateChanged;
                    SubscribeCursorServer(nextServer);
                    SyncUiFromState();
                }

                yield return null;
            }
        }

        void ClearKaplaoBinding()
        {
            if (colorState != null)
                colorState.ColorChanged -= OnColorStateChanged;

            colorState = null;
            UnsubscribeCursorServer();
        }

        void SubscribeCursorServer(KaplaoCursorServer server)
        {
            UnsubscribeCursorServer();
            cursorServer = server;
            if (cursorServer == null)
                return;

            cursorServer.HostGameModeChanged += OnHostGameModeChanged;
            cursorServer.SelectionChanged += OnSelectionChanged;
            KaplaoLocalGameModePredictor.Changed += OnPredictedGameModeChanged;
            RefreshPanelVisibilityForPlayMode(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
            SyncPlaceColorFromSelection();
        }

        void UnsubscribeCursorServer()
        {
            if (cursorServer != null)
            {
                cursorServer.HostGameModeChanged -= OnHostGameModeChanged;
                cursorServer.SelectionChanged -= OnSelectionChanged;
            }

            KaplaoLocalGameModePredictor.Changed -= OnPredictedGameModeChanged;
            cursorServer = null;
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            RefreshPanelVisibilityForPlayMode(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
        }

        void OnPredictedGameModeChanged(HostGameMode mode)
        {
            RefreshPanelVisibilityForPlayMode(mode);
        }

        void OnSelectionChanged()
        {
            SyncPlaceColorFromSelection();
        }

        void SyncPlaceColorFromSelection()
        {
            if (colorState == null || cursorServer == null)
                return;

            if (!KaplaoLocalGameModePredictor.IsCreate(cursorServer))
                return;

            if (cursorServer.SelectedCount == 0)
            {
                KaplaoCursorClient.BeginSuppressApplyPlaceColorToSelection();
                try
                {
                    colorState.SetColor(new Color32(255, 255, 255, 255));
                }
                finally
                {
                    KaplaoCursorClient.EndSuppressApplyPlaceColorToSelection();
                }
                return;
            }

            if (!TryGetFirstSelectedTileColor(out Color32 color))
                return;

            KaplaoCursorClient.BeginSuppressApplyPlaceColorToSelection();
            try
            {
                colorState.SetColor(color);
            }
            finally
            {
                KaplaoCursorClient.EndSuppressApplyPlaceColorToSelection();
            }
        }

        bool TryGetFirstSelectedTileColor(out Color32 color)
        {
            color = default;
            for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
            {
                uint netId = cursorServer.SelectedTiles[i];
                LevelTileServer tile = null;
                if (LevelGridServer.Instance != null)
                    LevelGridServer.Instance.TryGetTileByNetId(netId, out tile);

                if (tile == null)
                    continue;

                color = tile.TileColor;
                color.a = 255;
                return true;
            }

            return false;
        }

        void RefreshPanelVisibilityForPlayMode(HostGameMode? mode = null)
        {
            if (!Application.isPlaying || panelRoot == null)
                return;

            HostGameMode resolved = mode ?? KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            panelRoot.gameObject.SetActive(resolved == HostGameMode.Create);
        }

        void WirePanel()
        {
            if (panelWired || panelRoot == null)
                return;

            HideLegacyRgbUi(panelRoot.parent != null ? panelRoot.parent : panelRoot);

            EnsurePickerTextures();
            EnsureCursors();
            EnsureDragHandlers();
            WireRgbAndHex();
            RebuildSwatches();

            panelWired = true;
        }

        void EnsurePickerTextures()
        {
            if (svImage != null && svTexture == null)
            {
                svTexture = CreatePickerTexture(128, 128);
                svImage.texture = svTexture;
            }

            if (hueImage != null && hueTexture == null)
            {
                hueTexture = CreatePickerTexture(1, 128);
                hueImage.texture = hueTexture;
            }

            RebuildHueTexture();
            RebuildSvTexture();
        }

        static Texture2D CreatePickerTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        void EnsureCursors()
        {
            if (svPickerRoot != null)
                svCursor = FindOrCreateCursor(svPickerRoot, "SvCursor", 12f);
            if (huePickerRoot != null)
                hueCursor = FindOrCreateHueCursor(huePickerRoot);
        }

        static RectTransform FindOrCreateCursor(Transform parent, string name, float size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<RectTransform>();

            GameObject cursorObject = new GameObject(name, typeof(RectTransform));
            cursorObject.transform.SetParent(parent, false);
            RectTransform cursor = cursorObject.GetComponent<RectTransform>();
            cursor.sizeDelta = new Vector2(size, size);

            Image image = cursorObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;

            Outline outline = cursorObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            return cursor;
        }

        static RectTransform FindOrCreateHueCursor(Transform parent)
        {
            Transform existing = parent.Find("HueCursor");
            if (existing != null)
                return existing.GetComponent<RectTransform>();

            GameObject cursorObject = new GameObject("HueCursor", typeof(RectTransform));
            cursorObject.transform.SetParent(parent, false);
            RectTransform cursor = cursorObject.GetComponent<RectTransform>();
            cursor.anchorMin = new Vector2(0f, 0.5f);
            cursor.anchorMax = new Vector2(1f, 0.5f);
            cursor.pivot = new Vector2(0.5f, 0.5f);
            cursor.sizeDelta = new Vector2(0f, 4f);

            Image image = cursorObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return cursor;
        }

        void EnsureDragHandlers()
        {
            if (svPickerRoot != null)
            {
                NormalizedRectDrag svDrag = svPickerRoot.GetComponent<NormalizedRectDrag>() ??
                                            svPickerRoot.gameObject.AddComponent<NormalizedRectDrag>();
                svDrag.OnNormalized = local =>
                {
                    saturation = Mathf.Clamp01(local.x);
                    value = Mathf.Clamp01(local.y);
                    ApplyPickerColorToState();
                };
            }

            if (huePickerRoot != null)
            {
                NormalizedRectDrag hueDrag = huePickerRoot.GetComponent<NormalizedRectDrag>() ??
                                             huePickerRoot.gameObject.AddComponent<NormalizedRectDrag>();
                hueDrag.OnNormalized = local =>
                {
                    hue = Mathf.Clamp01(local.y);
                    RebuildSvTexture();
                    ApplyPickerColorToState();
                };
            }
        }

        void WireRgbAndHex()
        {
            if (redSlider != null)
            {
                redSlider.onValueChanged.RemoveListener(OnRedSliderChanged);
                redSlider.onValueChanged.AddListener(OnRedSliderChanged);
            }

            if (greenSlider != null)
            {
                greenSlider.onValueChanged.RemoveListener(OnGreenSliderChanged);
                greenSlider.onValueChanged.AddListener(OnGreenSliderChanged);
            }

            if (blueSlider != null)
            {
                blueSlider.onValueChanged.RemoveListener(OnBlueSliderChanged);
                blueSlider.onValueChanged.AddListener(OnBlueSliderChanged);
            }

            if (hexField != null)
            {
                hexField.onEndEdit.RemoveListener(OnHexEndEdit);
                hexField.onEndEdit.AddListener(OnHexEndEdit);
            }
        }

        void RebuildSwatches()
        {
            swatches.Clear();
            if (swatchGridRoot == null)
                return;

            for (int i = swatchGridRoot.childCount - 1; i >= 0; i--)
                SafeDestroy(swatchGridRoot.GetChild(i).gameObject);

            Color32[] presets = TileColorPalette.Presets;
            for (int i = 0; i < presets.Length; i++)
                swatches.Add(CreateSwatch(swatchGridRoot, presets[i]));
        }

        SwatchView CreateSwatch(Transform parent, Color32 color)
        {
            GameObject swatchObject = new GameObject($"Swatch_{color.r}_{color.g}_{color.b}", typeof(RectTransform));
            swatchObject.transform.SetParent(parent, false);

            Image ring = swatchObject.AddComponent<Image>();
            ring.color = new Color(1f, 1f, 1f, 0.18f);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(swatchObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            Image fill = fillObject.AddComponent<Image>();
            fill.color = color;

            Button button = swatchObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.onClick.AddListener(() => OnSwatchClicked(color));

            return new SwatchView { Color = color, Ring = ring, Fill = fill };
        }

        static void HideLegacyRgbUi(Transform parent)
        {
            Slider[] sliders = parent.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                Slider slider = sliders[i];
                if (slider == null || slider.name.Contains("RedSlider") || slider.name.Contains("GreenSlider") || slider.name.Contains("BlueSlider"))
                    slider.gameObject.SetActive(false);
            }
        }

        void OnSwatchClicked(Color32 color)
        {
            if (colorState == null)
                return;

            colorState.SetColor(color);
        }

        void OnRedSliderChanged(float _) => OnRgbSliderChanged();
        void OnGreenSliderChanged(float _) => OnRgbSliderChanged();
        void OnBlueSliderChanged(float _) => OnRgbSliderChanged();

        void OnRgbSliderChanged()
        {
            if (updatingUi || colorState == null || redSlider == null)
                return;

            var color = new Color32(
                (byte)redSlider.value,
                (byte)greenSlider.value,
                (byte)blueSlider.value,
                255);
            colorState.SetColor(color);
        }

        void OnHexEndEdit(string text)
        {
            if (updatingUi || colorState == null || string.IsNullOrWhiteSpace(text))
                return;

            string hex = text.Trim().TrimStart('#');
            if (hex.Length != 6)
                return;

            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return;

            colorState.SetColor(new Color32(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF),
                255));
        }

        void ApplyPickerColorToState()
        {
            if (updatingUi || colorState == null)
                return;

            Color rgb = HsvToRgb(hue, saturation, value);
            colorState.SetColor(new Color32(
                (byte)Mathf.RoundToInt(rgb.r * 255f),
                (byte)Mathf.RoundToInt(rgb.g * 255f),
                (byte)Mathf.RoundToInt(rgb.b * 255f),
                255));
        }

        void OnColorStateChanged(Color32 color)
        {
            updatingUi = true;

            if (previewFill != null)
                previewFill.color = color;

            RgbToHsv(color, out hue, out saturation, out value);
            RebuildSvTexture();
            UpdatePickerCursors();
            UpdateRgbSliders(color);
            UpdateHexField(color);
            UpdateSwatchSelection(color);

            updatingUi = false;
        }

        void SyncUiFromState()
        {
            if (colorState == null)
                return;

            OnColorStateChanged(colorState.PlaceColor);
        }

        void UpdateRgbSliders(Color32 color)
        {
            if (redSlider == null)
                return;

            redSlider.SetValueWithoutNotify(color.r);
            greenSlider.SetValueWithoutNotify(color.g);
            blueSlider.SetValueWithoutNotify(color.b);
        }

        void UpdateHexField(Color32 color)
        {
            if (hexField == null)
                return;

            hexField.SetTextWithoutNotify($"{color.r:X2}{color.g:X2}{color.b:X2}");
        }

        void UpdatePickerCursors()
        {
            if (svCursor != null)
            {
                svCursor.anchorMin = new Vector2(saturation, value);
                svCursor.anchorMax = new Vector2(saturation, value);
                svCursor.anchoredPosition = Vector2.zero;
            }

            if (hueCursor != null)
            {
                hueCursor.anchorMin = new Vector2(0f, hue);
                hueCursor.anchorMax = new Vector2(1f, hue);
                hueCursor.anchoredPosition = Vector2.zero;
            }
        }

        void RebuildHueTexture()
        {
            if (hueTexture == null)
                return;

            int height = hueTexture.height;
            for (int y = 0; y < height; y++)
            {
                // Texture y=0 is bottom; top of strip = hue 1 (same as pointer Y).
                float h = y / (float)Mathf.Max(1, height - 1);
                hueTexture.SetPixel(0, y, HsvToRgb(h, 1f, 1f));
            }

            hueTexture.Apply();
        }

        void RebuildSvTexture()
        {
            if (svTexture == null)
                return;

            int width = svTexture.width;
            int height = svTexture.height;
            for (int y = 0; y < height; y++)
            {
                // Top = bright (V=1), bottom = dark (V=0) — matches UI pointer Y.
                float v = y / (float)Mathf.Max(1, height - 1);
                for (int x = 0; x < width; x++)
                {
                    float s = x / (float)Mathf.Max(1, width - 1);
                    svTexture.SetPixel(x, y, HsvToRgb(hue, s, v));
                }
            }

            svTexture.Apply();
        }

        static Color HsvToRgb(float h, float s, float v)
        {
            h = Mathf.Repeat(h, 1f);
            float c = v * s;
            float hp = h * 6f;
            float x = c * (1f - Mathf.Abs(hp % 2f - 1f));
            float m = v - c;

            float r;
            float g;
            float b;
            if (hp < 1f) { r = c; g = x; b = 0f; }
            else if (hp < 2f) { r = x; g = c; b = 0f; }
            else if (hp < 3f) { r = 0f; g = c; b = x; }
            else if (hp < 4f) { r = 0f; g = x; b = c; }
            else if (hp < 5f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }

            return new Color(r + m, g + m, b + m, 1f);
        }

        static void RgbToHsv(Color32 color, out float outHue, out float outSaturation, out float outValue)
        {
            float r = color.r / 255f;
            float g = color.g / 255f;
            float b = color.b / 255f;

            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            float delta = max - min;

            outValue = max;
            outSaturation = max <= 0f ? 0f : delta / max;

            if (delta <= 0f)
            {
                outHue = 0f;
                return;
            }

            float hueValue;
            if (max == r)
                hueValue = (g - b) / delta + (g < b ? 6f : 0f);
            else if (max == g)
                hueValue = (b - r) / delta + 2f;
            else
                hueValue = (r - g) / delta + 4f;

            outHue = hueValue / 6f;
        }

        void UpdateSwatchSelection(Color32 selected)
        {
            for (int i = 0; i < swatches.Count; i++)
            {
                SwatchView swatch = swatches[i];
                bool isSelected = TileColorPalette.ColorsEqual(swatch.Color, selected);
                swatch.Ring.color = isSelected ? Color.white : new Color(1f, 1f, 1f, 0.18f);
                swatch.Ring.rectTransform.localScale = isSelected ? Vector3.one * 1.08f : Vector3.one;
            }
        }

        public struct PanelReferences
        {
            public RectTransform PanelRoot;
            public Image PreviewFill;
            public RawImage SvImage;
            public RawImage HueImage;
            public RectTransform SvPickerRoot;
            public RectTransform HuePickerRoot;
            public RectTransform SwatchGridRoot;
            public Slider RedSlider;
            public Slider GreenSlider;
            public Slider BlueSlider;
            public TMP_InputField HexField;
        }

        sealed class SwatchView
        {
            public Color32 Color;
            public Image Ring;
            public Image Fill;
        }

        sealed class NormalizedRectDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public Action<Vector2> OnNormalized;

            public void OnPointerDown(PointerEventData eventData) => Handle(eventData);
            public void OnDrag(PointerEventData eventData) => Handle(eventData);

            void Handle(PointerEventData eventData)
            {
                if (OnNormalized == null)
                    return;

                RectTransform rect = transform as RectTransform;
                if (rect == null)
                    return;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                    return;

                Rect r = rect.rect;
                float x = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
                float y = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
                OnNormalized.Invoke(new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y)));
            }
        }
    }
}
