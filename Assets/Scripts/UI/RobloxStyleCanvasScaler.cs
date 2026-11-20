using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Roblox-like canvas scaling: UI size depends on aspect ratio, not raw resolution.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public class RobloxStyleCanvasScaler : MonoBehaviour
    {
        [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);

        CanvasScaler canvasScaler;
        Vector2Int lastScreenSize;

        void Awake()
        {
            canvasScaler = GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            ApplyMatch();
        }

        void Update()
        {
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize == lastScreenSize)
                return;

            lastScreenSize = screenSize;
            ApplyMatch();
        }

        void ApplyMatch()
        {
            if (canvasScaler == null)
                return;

            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float referenceAspect = referenceResolution.x / referenceResolution.y;
            canvasScaler.matchWidthOrHeight = aspect > referenceAspect ? 1f : 0f;
        }
    }
}
