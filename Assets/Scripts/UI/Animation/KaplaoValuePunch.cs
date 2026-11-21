using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Value Punch effect (inspired by RTSokak's UIValuePunch).
    /// Quick scale-punch (1.18x -> 1.0x) and color flash on text update.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Value Punch")]
    public class KaplaoValuePunch : MonoBehaviour
    {
        [SerializeField] RectTransform punchTarget;
        [SerializeField] Graphic colorGraphic;
        [SerializeField] float punchScale = 1.18f;
        [SerializeField] float duration = 0.22f;

        Vector3 baseScale = Vector3.one;
        Color baseColor = Color.white;
        int scaleTweenId = -1;
        int colorTweenId = -1;

        void Awake()
        {
            if (punchTarget == null)
                punchTarget = transform as RectTransform;

            if (punchTarget != null)
                baseScale = punchTarget.localScale;

            if (colorGraphic == null)
                colorGraphic = GetComponent<Graphic>();

            if (colorGraphic != null)
                baseColor = colorGraphic.color;
        }

        public void Punch(Color? flashColor = null)
        {
            if (punchTarget == null)
                punchTarget = transform as RectTransform;

            if (punchTarget == null)
                return;

            if (scaleTweenId != -1)
                LeanTween.cancel(scaleTweenId);
            if (colorTweenId != -1)
                LeanTween.cancel(colorTweenId);

            punchTarget.localScale = baseScale * punchScale;
            scaleTweenId = LeanTween.scale(punchTarget, baseScale, duration)
                .setIgnoreTimeScale(true)
                .setEaseOutQuad()
                .id;

            if (colorGraphic != null && flashColor.HasValue)
            {
                colorGraphic.color = flashColor.Value;
                colorTweenId = LeanTween.value(gameObject, flashColor.Value, baseColor, duration)
                    .setIgnoreTimeScale(true)
                    .setEaseOutQuad()
                    .setOnUpdate((Color c) => colorGraphic.color = c)
                    .id;
            }
        }
    }
}
