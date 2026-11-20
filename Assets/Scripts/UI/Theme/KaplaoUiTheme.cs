using System;
using UnityEngine;

namespace KaplaoGames.UI.Theme
{
    public enum KaplaoButtonVariant
    {
        Primary,
        Secondary,
        Success,
        Danger,
        Ghost,
        Icon
    }

    /// <summary>
    /// Modern Deep Slate + Electric Cyan / Indigo Gaming UI Theme.
    /// Loaded from Resources/KaplaoUiTheme or defaults in code.
    /// </summary>
    [CreateAssetMenu(fileName = "KaplaoUiTheme", menuName = "Kaplao/UI Theme")]
    public class KaplaoUiTheme : ScriptableObject
    {
        public const string ResourcePath = "KaplaoUiTheme";
        static KaplaoUiTheme cached;

        [Header("Surfaces & Backgrounds")]
        public Color BackdropDim = new Color(0x05 / 255f, 0x08 / 255f, 0x11 / 255f, 0.72f);
        public Color SurfacePrimary = new Color(0x0F / 255f, 0x15 / 255f, 0x24 / 255f, 0.94f);
        public Color SurfaceCard = new Color(0x17 / 255f, 0x20 / 255f, 0x36 / 255f, 0.95f);
        public Color SurfaceCardHover = new Color(0x1F / 255f, 0x2C / 255f, 0x4A / 255f, 0.95f);
        public Color SurfaceCardSelected = new Color(0x1D / 255f, 0x3A / 255f, 0x5F / 255f, 0.98f);
        public Color HeaderSurface = new Color(0x13 / 255f, 0x1B / 255f, 0x2E / 255f, 0.98f);
        public Color InputSurface = new Color(0x0A / 255f, 0x0F / 255f, 0x1C / 255f, 0.95f);
        public Color CodeEditorBackground = new Color(0x0B / 255f, 0x0F / 255f, 0x19 / 255f, 0.98f);
        public Color CodeSidebarBackground = new Color(0x0F / 255f, 0x14 / 255f, 0x22 / 255f, 0.98f);

        [Header("Borders & Shadows")]
        public Color BorderSubtle = new Color(0x2B / 255f, 0x3B / 255f, 0x5C / 255f, 0.55f);
        public Color BorderFocus = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.95f);
        public Color InputBorder = new Color(0x26 / 255f, 0x35 / 255f, 0x52 / 255f, 0.95f);
        public Color SoftShadowColor = new Color(0f, 0f, 0f, 0.45f);

        [Header("Typography")]
        public Color TextPrimary = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
        public Color TextSecondary = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 1f);
        public Color TextMuted = new Color(0x64 / 255f, 0x74 / 255f, 0x8B / 255f, 1f);
        public Color AccentCyan = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 1f);
        public Color AccentIndigo = new Color(0x81 / 255f, 0x8C / 255f, 0xF8 / 255f, 1f);

        [Header("Button - Primary (Indigo/Cyan)")]
        public Color ButtonPrimaryNormal = new Color(0x25 / 255f, 0x63 / 255f, 0xEB / 255f, 1f);
        public Color ButtonPrimaryHover = new Color(0x3B / 255f, 0x82 / 255f, 0xF6 / 255f, 1f);
        public Color ButtonPrimaryPress = new Color(0x1D / 255f, 0x4E / 255f, 0xD8 / 255f, 1f);

        [Header("Button - Success (Emerald)")]
        public Color ButtonSuccessNormal = new Color(0x10 / 255f, 0xB9 / 255f, 0x81 / 255f, 1f);
        public Color ButtonSuccessHover = new Color(0x34 / 255f, 0xD3 / 255f, 0x99 / 255f, 1f);
        public Color ButtonSuccessPress = new Color(0x05 / 255f, 0x96 / 255f, 0x69 / 255f, 1f);

        [Header("Button - Danger (Ruby)")]
        public Color ButtonDangerNormal = new Color(0xDC / 255f, 0x26 / 255f, 0x26 / 255f, 1f);
        public Color ButtonDangerHover = new Color(0xEF / 255f, 0x44 / 255f, 0x44 / 255f, 1f);
        public Color ButtonDangerPress = new Color(0xB9 / 255f, 0x1C / 255f, 0x1C / 255f, 1f);

        [Header("Button - Secondary (Slate)")]
        public Color ButtonSecondaryNormal = new Color(0x1E / 255f, 0x29 / 255f, 0x3B / 255f, 0.95f);
        public Color ButtonSecondaryHover = new Color(0x28 / 255f, 0x35 / 255f, 0x48 / 255f, 1f);
        public Color ButtonSecondaryPress = new Color(0x15 / 255f, 0x1D / 255f, 0x2B / 255f, 1f);
        public Color ButtonSecondaryBorder = new Color(0x33 / 255f, 0x41 / 255f, 0x55 / 255f, 0.8f);

        public static KaplaoUiTheme Instance
        {
            get
            {
                if (cached != null) return cached;
                cached = Resources.Load<KaplaoUiTheme>(ResourcePath);
                if (cached == null)
                    cached = CreateInstance<KaplaoUiTheme>();
                return cached;
            }
        }

        public static KaplaoUiTheme Current => Instance;

        public (Color normal, Color hover, Color press, Color border) GetButtonColors(KaplaoButtonVariant variant)
        {
            switch (variant)
            {
                case KaplaoButtonVariant.Primary:
                    return (ButtonPrimaryNormal, ButtonPrimaryHover, ButtonPrimaryPress, new Color(0x60 / 255f, 0xA5 / 255f, 0xFA / 255f, 0.7f));

                case KaplaoButtonVariant.Success:
                    return (ButtonSuccessNormal, ButtonSuccessHover, ButtonSuccessPress, new Color(0x6E / 255f, 0xE7 / 255f, 0xB7 / 255f, 0.7f));

                case KaplaoButtonVariant.Danger:
                    return (ButtonDangerNormal, ButtonDangerHover, ButtonDangerPress, new Color(0xFC / 255f, 0xA5 / 255f, 0xA5 / 255f, 0.7f));

                case KaplaoButtonVariant.Ghost:
                case KaplaoButtonVariant.Icon:
                    return (new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.22f), new Color(1f, 1f, 1f, 0.15f));

                case KaplaoButtonVariant.Secondary:
                default:
                    return (ButtonSecondaryNormal, ButtonSecondaryHover, ButtonSecondaryPress, ButtonSecondaryBorder);
            }
        }
    }
}
