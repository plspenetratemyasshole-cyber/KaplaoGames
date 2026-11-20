using KaplaoGames.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local-only runner cheat toggles. Used when MobileControlsConfig.Cheats is enabled.
    /// </summary>
    public static class RunnerCheatsState
    {
        public const float DefaultFlySpeed = 12f;
        public const float DefaultSpikePushForce = 0.35f;
        public const float DefaultSpikePulseSize = 0.18f;

        public static bool KaplaoFly { get; set; }
        public static bool Immortality { get; set; }
        public static float FlySpeed { get; set; } = DefaultFlySpeed;
        public static float SpikePushForce { get; set; } = DefaultSpikePushForce;
        public static float SpikePulseSize { get; set; } = DefaultSpikePulseSize;

        public static bool IsKaplaoFlyActive =>
            MobileControlsConfig.IsCheatsEnabled && KaplaoFly;

        public static void Reset()
        {
            KaplaoFly = false;
            Immortality = false;
            FlySpeed = DefaultFlySpeed;
            SpikePushForce = DefaultSpikePushForce;
            SpikePulseSize = DefaultSpikePulseSize;
        }
    }
}
