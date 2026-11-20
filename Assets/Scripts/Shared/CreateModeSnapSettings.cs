using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Snap increments for Blender-style R/G/S transforms in Create mode.
    /// Stored per local player (nickname) in PlayerPrefs; each Kaplao pushes values to its server cursor.
    /// </summary>
    public static class CreateModeSnapSettings
    {
        public const float DefaultRotationSnapDegrees = 15f;
        public const float DefaultPositionSnapUnits = 1f;
        public const float DefaultScaleSnapUnits = 0.1f;

        const string KeyRotation = "Kaplao.CreateSnap.Rotation";
        const string KeyPosition = "Kaplao.CreateSnap.Position";
        const string KeyScale = "Kaplao.CreateSnap.Scale";

        public static float RotationSnapDegrees { get; private set; } = DefaultRotationSnapDegrees;
        public static float PositionSnapUnits { get; private set; } = DefaultPositionSnapUnits;
        public static float ScaleSnapUnits { get; private set; } = DefaultScaleSnapUnits;

        static CreateModeSnapSettings()
        {
            LoadLocal();
        }

        public static void LoadLocal()
        {
            string suffix = GetPlayerKeySuffix();
            RotationSnapDegrees = ReadFloat(KeyRotation, suffix, DefaultRotationSnapDegrees);
            PositionSnapUnits = ReadFloat(KeyPosition, suffix, DefaultPositionSnapUnits);
            ScaleSnapUnits = ReadFloat(KeyScale, suffix, DefaultScaleSnapUnits);
        }

        public static void SaveLocal(float rotationDegrees, float positionUnits, float scaleUnits)
        {
            RotationSnapDegrees = Mathf.Max(0.0001f, rotationDegrees);
            PositionSnapUnits = Mathf.Max(0.0001f, positionUnits);
            ScaleSnapUnits = Mathf.Max(0.0001f, scaleUnits);

            string suffix = GetPlayerKeySuffix();
            PlayerPrefs.SetFloat(KeyRotation + suffix, RotationSnapDegrees);
            PlayerPrefs.SetFloat(KeyPosition + suffix, PositionSnapUnits);
            PlayerPrefs.SetFloat(KeyScale + suffix, ScaleSnapUnits);
            PlayerPrefs.Save();
        }

        public static float SnapAngle(float degrees, float increment) => Snap(degrees, increment);

        public static float SnapPosition(float value, float increment) => Snap(value, increment);

        public static float SnapScale(float value, float increment) => Snap(Mathf.Max(0.01f, value), increment);

        /// <summary>Snaps a delta (offset, angle change, etc.) to the nearest increment.</summary>
        public static float SnapDelta(float delta, float increment) => Snap(delta, increment);

        static float Snap(float value, float increment)
        {
            if (increment <= 0f)
                return value;

            return Mathf.Round(value / increment) * increment;
        }

        static float ReadFloat(string key, string suffix, float defaultValue)
        {
            if (PlayerPrefs.HasKey(key + suffix))
                return PlayerPrefs.GetFloat(key + suffix, defaultValue);

            // Migrate older global prefs (shared across all Kaplao on this machine).
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
