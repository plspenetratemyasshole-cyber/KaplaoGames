using UnityEngine;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mobile Controls Config")]
    public class MobileControlsConfig : MonoBehaviour
    {
        [SerializeField] bool buildForPhone;
        [Tooltip("Admin runner cheats panel (Kaplao fly / Immortality). Enable in SampleScene Canvas.")]
        [SerializeField] bool cheats;
        [SerializeField] bool forceLandscape = true;

        public static MobileControlsConfig Instance { get; private set; }

        public static bool IsMobileControlsEnabled
        {
            get
            {
                if (Application.isMobilePlatform)
                    return true;

                return Instance != null && Instance.buildForPhone;
            }
        }

        public static bool IsCheatsEnabled => Instance != null && Instance.cheats;

        void Awake()
        {
            Instance = this;
            ApplyOrientation();
        }

        void OnEnable()
        {
            ApplyOrientation();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ApplyOrientation()
        {
            if (!ShouldForceLandscape())
                return;

            ApplyForcedLandscape();
        }

        public static void ApplyForcedLandscape()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        bool ShouldForceLandscape()
        {
            if (!forceLandscape)
                return false;

            return IsMobileControlsEnabled;
        }
    }
}
