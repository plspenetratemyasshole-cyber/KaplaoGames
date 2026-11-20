using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Applies forced landscape on mobile even before scene MobileControlsConfig loads (e.g. Main Menu).
    /// </summary>
    public static class MobileOrientationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            if (Application.isMobilePlatform)
                MobileControlsConfig.ApplyForcedLandscape();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoad()
        {
            if (MobileControlsConfig.Instance != null)
                MobileControlsConfig.Instance.ApplyOrientation();
            else if (Application.isMobilePlatform)
                MobileControlsConfig.ApplyForcedLandscape();
        }
    }
}
