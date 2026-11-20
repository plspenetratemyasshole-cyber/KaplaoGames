using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Create-mode-only editor display toggles and Kaplao zoom limits.
    /// Stored per local player (nickname) in PlayerPrefs.
    /// </summary>
    public static class CreateModeEditorSettings
    {
        const string KeyShowGrid = "Kaplao.CreateEditor.ShowGrid";
        const string KeyShowHitboxes = "Kaplao.CreateEditor.ShowHitboxes";
        const string KeyShowPlayers = "Kaplao.CreateEditor.ShowPlayers";
        const string KeyHideCursor = "Kaplao.CreateEditor.HideCursor";
        const string KeyHitboxTrail = "Kaplao.CreateEditor.HitboxTrail";
        const string KeyHasZoomOverride = "Kaplao.CreateEditor.HasZoomOverride";
        const string KeyMinOrtho = "Kaplao.CreateEditor.MinOrthoSize";
        const string KeyMaxOrtho = "Kaplao.CreateEditor.MaxOrthoSize";

        public static bool ShowGrid { get; private set; } = true;
        public static bool ShowHitboxes { get; private set; } = true;
        public static bool ShowPlayers { get; private set; } = true;
        public static bool HideCursor { get; private set; }
        public static bool HitboxTrail { get; private set; }
        public static bool HasZoomOverride { get; private set; }
        public static float MinOrthoSize { get; private set; } = 3f;
        public static float MaxOrthoSize { get; private set; } = 20f;

        public static event System.Action Changed;

        static CreateModeEditorSettings()
        {
            LoadLocal();
        }

        public static void LoadLocal()
        {
            string suffix = GetPlayerKeySuffix();
            ShowGrid = ReadInt(KeyShowGrid, suffix, 1) != 0;
            ShowHitboxes = ReadInt(KeyShowHitboxes, suffix, 1) != 0;
            ShowPlayers = ReadInt(KeyShowPlayers, suffix, 1) != 0;
            HideCursor = ReadInt(KeyHideCursor, suffix, 0) != 0;
            HitboxTrail = ReadInt(KeyHitboxTrail, suffix, 0) != 0;
            HasZoomOverride = ReadInt(KeyHasZoomOverride, suffix, 0) != 0;
            MinOrthoSize = ReadFloat(KeyMinOrtho, suffix, 3f);
            MaxOrthoSize = ReadFloat(KeyMaxOrtho, suffix, 20f);
            NormalizeZoomRange();
        }

        public static void SaveLocal(
            bool showGrid,
            bool showHitboxes,
            bool showPlayers,
            bool hideCursor,
            bool hitboxTrail,
            float minOrthoSize,
            float maxOrthoSize)
        {
            ShowGrid = showGrid;
            ShowHitboxes = showHitboxes;
            ShowPlayers = showPlayers;
            HideCursor = hideCursor;
            HitboxTrail = hitboxTrail;
            HasZoomOverride = true;
            MinOrthoSize = minOrthoSize;
            MaxOrthoSize = maxOrthoSize;
            NormalizeZoomRange();

            string suffix = GetPlayerKeySuffix();
            PlayerPrefs.SetInt(KeyShowGrid + suffix, showGrid ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowHitboxes + suffix, showHitboxes ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowPlayers + suffix, showPlayers ? 1 : 0);
            PlayerPrefs.SetInt(KeyHideCursor + suffix, hideCursor ? 1 : 0);
            PlayerPrefs.SetInt(KeyHitboxTrail + suffix, hitboxTrail ? 1 : 0);
            PlayerPrefs.SetInt(KeyHasZoomOverride + suffix, 1);
            PlayerPrefs.SetFloat(KeyMinOrtho + suffix, MinOrthoSize);
            PlayerPrefs.SetFloat(KeyMaxOrtho + suffix, MaxOrthoSize);
            PlayerPrefs.Save();
            NotifyChanged();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        public static float GetKaplaoMinOrtho(float inspectorBase)
        {
            return HasZoomOverride ? MinOrthoSize : Mathf.Max(0.01f, inspectorBase);
        }

        public static float GetKaplaoMaxOrtho(float inspectorBase)
        {
            float min = GetKaplaoMinOrtho(inspectorBase);
            float max = HasZoomOverride ? MaxOrthoSize : Mathf.Max(0.01f, inspectorBase);
            return Mathf.Max(min, max);
        }

        static void NormalizeZoomRange()
        {
            MinOrthoSize = Mathf.Max(0.01f, MinOrthoSize);
            MaxOrthoSize = Mathf.Max(MinOrthoSize, MaxOrthoSize);
        }

        static int ReadInt(string key, string suffix, int defaultValue)
        {
            if (PlayerPrefs.HasKey(key + suffix))
                return PlayerPrefs.GetInt(key + suffix, defaultValue);

            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetInt(key, defaultValue);

            return defaultValue;
        }

        static float ReadFloat(string key, string suffix, float defaultValue)
        {
            if (PlayerPrefs.HasKey(key + suffix))
                return PlayerPrefs.GetFloat(key + suffix, defaultValue);

            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetFloat(key, defaultValue);

            return defaultValue;
        }

        static string GetPlayerKeySuffix()
        {
            return "." + PlayerSession.SanitizeNickname(PlayerSession.Nickname);
        }
    }
}
