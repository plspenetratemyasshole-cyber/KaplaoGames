using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Smooth animated switch toggle: moves Handle circle and transitions track color (#334155 <-> #10B981).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    [AddComponentMenu("Kaplao/UI/Toggle Switch Animator")]
    public class KaplaoUiToggleAnimator : MonoBehaviour
    {
        [SerializeField] RectTransform handle;
        [SerializeField] Graphic trackGraphic;
        [SerializeField] float offPosX = -12f;
        [SerializeField] float onPosX = 12f;
        [SerializeField] Color offColor = new Color(0x33 / 255f, 0x41 / 255f, 0x55 / 255f, 1f);
        [SerializeField] Color onColor = new Color(0x10 / 255f, 0xB9 / 255f, 0x81 / 255f, 1f);
        [SerializeField] float duration = 0.16f;

        Toggle toggle;

        public void Configure(RectTransform handleRect, Graphic track, float offX, float onX)
        {
            handle = handleRect;
            trackGraphic = track;
            offPosX = offX;
            onPosX = onX;
        }

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            if (handle == null)
            {
                Transform h = transform.Find("Handle") ?? transform.Find("Background/Checkmark");
                if (h != null) handle = h as RectTransform;
            }

            if (trackGraphic == null)
            {
                Transform bg = transform.Find("Background");
                if (bg != null) trackGraphic = bg.GetComponent<Graphic>();
                if (trackGraphic == null) trackGraphic = GetComponent<Graphic>();
            }
        }

        void OnEnable()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(OnValueChanged);
                toggle.onValueChanged.AddListener(OnValueChanged);
                Snap(toggle.isOn);
            }
        }

        void OnDisable()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(OnValueChanged);

            if (handle != null)
                LeanTween.cancel(handle.gameObject);
            if (trackGraphic != null)
                LeanTween.cancel(trackGraphic.gameObject);
        }

        void OnValueChanged(bool isOn)
        {
            if (!gameObject.activeInHierarchy || !Application.isPlaying)
            {
                Snap(isOn);
                return;
            }

            float targetX = isOn ? onPosX : offPosX;
            Color targetCol = isOn ? onColor : offColor;

            if (handle != null)
            {
                LeanTween.cancel(handle.gameObject);
                Vector2 pos = handle.anchoredPosition;
                LeanTween.value(handle.gameObject, pos.x, targetX, duration)
                    .setIgnoreTimeScale(true)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnUpdate((float x) =>
                    {
                        if (handle != null)
                            handle.anchoredPosition = new Vector2(x, handle.anchoredPosition.y);
                    });
            }

            if (trackGraphic != null)
            {
                LeanTween.cancel(trackGraphic.gameObject);
                Color startCol = trackGraphic.color;
                LeanTween.value(trackGraphic.gameObject, startCol, targetCol, duration)
                    .setIgnoreTimeScale(true)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnUpdate((Color c) =>
                    {
                        if (trackGraphic != null)
                            trackGraphic.color = c;
                    });
            }
        }

        void Snap(bool isOn)
        {
            if (handle != null)
            {
                handle.anchoredPosition = new Vector2(isOn ? onPosX : offPosX, handle.anchoredPosition.y);
            }

            if (trackGraphic != null)
            {
                trackGraphic.color = isOn ? onColor : offColor;
            }
        }
    }
}
