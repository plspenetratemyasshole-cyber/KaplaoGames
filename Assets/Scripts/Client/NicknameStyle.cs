using KaplaoGames.Shared;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Applies nickname font (from Unity registry) and Easy Text Effects (from website preset).
    /// </summary>
    public static class NicknameStyle
    {
        public static void Apply(TextMeshProUGUI label, string accountEmail, string nicknamePreset = "none")
        {
            Apply(label, accountEmail, nicknamePreset, changeFont: true);
        }

        public static void ApplyShimmerOnly(TextMeshProUGUI label, string accountEmail, string nicknamePreset = "none")
        {
            Apply(label, accountEmail, nicknamePreset, changeFont: false);
        }

        public static void Apply(
            TextMeshProUGUI label,
            string accountEmail,
            string nicknamePreset,
            bool changeFont)
        {
            if (label == null)
                return;

            // Keep catalog warm so other players' presets resolve.
            KaplaoNicknamePresetCatalog.RequestRefresh();

            KaplaoNicknameFontRegistry fonts = KaplaoNicknameFontRegistry.Load();
            KaplaoNicknameStyleDto style = null;
            bool hasStyle = KaplaoNicknamePresetCatalog.TryGetStyle(nicknamePreset, out style);

            if (changeFont)
            {
                TMP_FontAsset font = null;
                if (hasStyle && fonts != null)
                    font = fonts.ResolveFont(style.FontName);
                else if (fonts != null)
                    font = fonts.ResolveFont(null);
                else
                    font = TMP_Settings.defaultFontAsset;

                AssignFont(label, font);
            }

            // Remove legacy shimmer if present.
            PrivilegedNicknameShimmer legacy = label.GetComponent<PrivilegedNicknameShimmer>();
            if (legacy != null)
            {
                legacy.Deactivate();
                Object.Destroy(legacy);
            }

            KaplaoNicknameEasyEffectsApplier applier = label.GetComponent<KaplaoNicknameEasyEffectsApplier>();
            if (hasStyle)
            {
                if (applier == null)
                    applier = label.gameObject.AddComponent<KaplaoNicknameEasyEffectsApplier>();
                applier.Apply(style);
            }
            else
            {
                if (applier != null)
                    applier.Clear();

                label.enableVertexGradient = false;
                if (changeFont)
                    label.color = fonts != null ? fonts.FallbackColor : Color.white;
                ClearOutline(label);
            }
        }

        public static void AssignFont(TextMeshProUGUI label, TMP_FontAsset font)
        {
            if (label == null || font == null)
                return;

            label.font = font;
            if (font.material != null)
                label.fontSharedMaterial = font.material;

            // Reset outline until preset style re-applies it.
            ClearOutline(label);
            label.enableVertexGradient = false;
            label.extraPadding = true;

            label.ForceMeshUpdate(ignoreActiveState: true);
        }

        /// <summary>
        /// Native TMP SDF outline from admin preset. Uses a material instance so shared fonts stay clean.
        /// </summary>
        public static void ApplyOutline(TextMeshProUGUI label, string outlineHex, float outlineWidth)
        {
            if (label == null)
                return;

            float width = Mathf.Clamp01(outlineWidth);
            if (width <= 0.0001f)
            {
                ClearOutline(label);
                return;
            }

            Color outlineColor = Color.black;
            if (!string.IsNullOrWhiteSpace(outlineHex))
                KaplaoNicknameEasyEffectsApplier.TryParseHex(outlineHex, out outlineColor);

            // Ensure a per-label material instance (do not mutate the shared font material).
            if (label.fontSharedMaterial != null)
                _ = label.fontMaterial;

            label.extraPadding = true;
            label.outlineColor = outlineColor;
            label.outlineWidth = width;
            label.ForceMeshUpdate(ignoreActiveState: true);
        }

        public static void ApplyOutline(TextMeshProUGUI label, KaplaoNicknameStyleDto style)
        {
            if (style == null)
            {
                ClearOutline(label);
                return;
            }

            ApplyOutline(label, style.OutlineColor, style.OutlineWidth);
        }

        public static void ClearOutline(TextMeshProUGUI label)
        {
            if (label == null)
                return;

            label.outlineWidth = 0f;
            label.outlineColor = new Color32(0, 0, 0, 0);
        }

        public static void EnsureWorldSpaceTmpCanvas(Canvas canvas)
        {
            if (canvas == null)
                return;

            canvas.additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
        }
    }
}
