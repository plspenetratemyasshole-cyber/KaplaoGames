using System;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Shared
{
    [Serializable]
    public class KaplaoNicknameFontEntry
    {
        [Tooltip("Name used in the website admin (case-insensitive).")]
        public string name = "default";

        public TMP_FontAsset font;
    }

    /// <summary>
    /// Only Unity-side nickname setting: map font names from admin presets to TMP assets.
    /// Visual effects come from website nickname presets + Easy Text Effects.
    /// </summary>
    [CreateAssetMenu(fileName = "KaplaoNicknameFontRegistry", menuName = "Kaplao/Nickname Font Registry")]
    public class KaplaoNicknameFontRegistry : ScriptableObject
    {
        public const string ResourcePath = "KaplaoNicknameFontRegistry";

        static KaplaoNicknameFontRegistry cached;

        [SerializeField] KaplaoNicknameFontEntry[] fonts = Array.Empty<KaplaoNicknameFontEntry>();
        [SerializeField] TMP_FontAsset fallbackFont;
        [SerializeField] Color fallbackColor = Color.white;

        public Color FallbackColor => fallbackColor;

        public static KaplaoNicknameFontRegistry Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<KaplaoNicknameFontRegistry>(ResourcePath);
            return cached;
        }

        public static void ClearCache()
        {
            cached = null;
        }

        public TMP_FontAsset ResolveFont(string fontName)
        {
            if (!string.IsNullOrWhiteSpace(fontName) && fonts != null)
            {
                string key = fontName.Trim();
                for (int i = 0; i < fonts.Length; i++)
                {
                    KaplaoNicknameFontEntry entry = fonts[i];
                    if (entry == null || entry.font == null || string.IsNullOrWhiteSpace(entry.name))
                        continue;

                    if (string.Equals(entry.name.Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return entry.font;
                }
            }

            if (fallbackFont != null)
                return fallbackFont;

            return TMP_Settings.defaultFontAsset;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            cached = this;
        }
#endif
    }
}
