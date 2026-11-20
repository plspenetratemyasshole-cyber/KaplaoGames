using UnityEngine;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Smooth subtle ambient floating/breathing (sine wave ±4px on Y via Time.unscaledTime).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Ambient Float")]
    public class KaplaoUiAmbientFloat : MonoBehaviour
    {
        [SerializeField] float amplitudeY = 4f;
        [SerializeField] float frequency = 1.4f;

        RectTransform rectTransform;
        Vector3 basePosition;
        bool hasBase;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                basePosition = rectTransform.anchoredPosition3D;
                hasBase = true;
            }
        }

        void OnEnable()
        {
            if (rectTransform != null && !hasBase)
            {
                basePosition = rectTransform.anchoredPosition3D;
                hasBase = true;
            }
        }

        void Update()
        {
            if (rectTransform == null)
                return;

            float offsetY = Mathf.Sin(Time.unscaledTime * frequency) * amplitudeY;
            rectTransform.anchoredPosition3D = basePosition + new Vector3(0f, offsetY, 0f);
        }

        public void SetBasePosition(Vector3 position)
        {
            basePosition = position;
            hasBase = true;
        }
    }
}
