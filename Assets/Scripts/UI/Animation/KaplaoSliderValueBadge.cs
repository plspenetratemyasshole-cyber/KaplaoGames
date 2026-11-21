using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Live value badge for settings sliders showing percentage (e.g. "60%").
    /// Positioned neatly to the right of the slider and punches slightly when modified.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    [AddComponentMenu("Kaplao/UI/Slider Value Badge")]
    public class KaplaoSliderValueBadge : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI badgeLabel;
        [SerializeField] float offsetX = 16f;

        Slider slider;
        RectTransform badgeRect;

        void Awake()
        {
            slider = GetComponent<Slider>();
            EnsureBadge();
        }

        void OnEnable()
        {
            if (slider == null)
                slider = GetComponent<Slider>();

            EnsureBadge();

            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnValueChanged);
                slider.onValueChanged.AddListener(OnValueChanged);
                UpdateText(slider.value, punch: false);
            }
        }

        void OnDisable()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(OnValueChanged);
        }

        public void EnsureBadge()
        {
            if (badgeLabel != null)
                return;

            Transform existing = transform.Find("ValueBadge");
            GameObject badgeGo;
            if (existing != null)
            {
                badgeGo = existing.gameObject;
            }
            else
            {
                badgeGo = new GameObject("ValueBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
                badgeGo.transform.SetParent(transform, false);
            }

            badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 0.5f);
            badgeRect.anchorMax = new Vector2(1f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(offsetX, 0f);
            badgeRect.sizeDelta = new Vector2(52f, 24f);

            badgeLabel = badgeGo.GetComponent<TextMeshProUGUI>();
            badgeLabel.fontSize = 13.5f;
            badgeLabel.fontStyle = FontStyles.Bold;
            badgeLabel.color = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 1f); // #38BDF8
            badgeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            badgeLabel.raycastTarget = false;
        }

        void OnValueChanged(float value)
        {
            UpdateText(value, punch: true);
        }

        void UpdateText(float value, bool punch)
        {
            if (badgeLabel == null)
                EnsureBadge();

            if (badgeLabel != null)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(value * 100f), 0, 100);
                badgeLabel.text = $"{pct}%";

                if (punch && Application.isPlaying && badgeRect != null)
                {
                    LeanTween.cancel(badgeRect.gameObject);
                    badgeRect.localScale = Vector3.one * 1.2f;
                    LeanTween.scale(badgeRect.gameObject, Vector3.one, 0.16f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                }
            }
        }
    }
}
