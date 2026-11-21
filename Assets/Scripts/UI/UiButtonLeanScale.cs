using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Live Button Juice (inspired by RTSokak's UIButtonJuice).
    /// Frame-rate independent exponential Lerp in Update for smooth scaling,
    /// background color highlight, and neon border glow.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Button Lean Scale")]
    public class UiButtonLeanScale : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        [Header("Scale Settings")]
        [SerializeField] RectTransform target;
        [SerializeField] float hoverScale = 1.045f;
        [SerializeField] float pressScale = 0.95f;
        [SerializeField] float responseSpeed = 18f;

        [Header("Graphics")]
        [SerializeField] Graphic targetGraphic;
        [SerializeField] Graphic borderGraphic;

        Selectable selectable;
        Vector3 baseScale = Vector3.one;
        Color baseBgColor = Color.white;
        Color baseBorderColor = Color.clear;

        float currentScale = 1f;
        float targetScale = 1f;

        Color currentBgColor = Color.white;
        Color targetBgColor = Color.white;

        Color currentBorderColor = Color.clear;
        Color targetBorderColor = Color.clear;

        bool pointerInside;
        bool isPressed;
        bool hasBorder;
        bool hasBg;
        bool initialized;

        static readonly Color NeonCyan = new Color(0.22f, 0.74f, 0.97f, 1f); // #38BDF8

        void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (initialized) return;

            if (target == null)
                target = transform as RectTransform;

            if (target != null)
                baseScale = target.localScale;

            selectable = GetComponent<Selectable>();
            if (selectable is Button btn)
                btn.transition = Selectable.Transition.None;

            if (targetGraphic == null)
            {
                if (selectable != null && selectable.targetGraphic != null)
                    targetGraphic = selectable.targetGraphic;
                else
                    targetGraphic = GetComponent<Image>();
            }

            if (targetGraphic != null)
            {
                baseBgColor = targetGraphic.color;
                currentBgColor = baseBgColor;
                targetBgColor = baseBgColor;
                hasBg = true;
            }

            if (borderGraphic == null)
            {
                Transform b = transform.Find("UI_Border");
                if (b != null) borderGraphic = b.GetComponent<Graphic>();
            }

            if (borderGraphic != null)
            {
                baseBorderColor = borderGraphic.color;
                currentBorderColor = baseBorderColor;
                targetBorderColor = baseBorderColor;
                hasBorder = true;
            }

            initialized = true;
        }

        void OnEnable()
        {
            Initialize();
            pointerInside = false;
            isPressed = false;
            targetScale = 1f;
            currentScale = 1f;

            if (target != null)
                target.localScale = baseScale;

            if (hasBg && targetGraphic != null)
            {
                currentBgColor = baseBgColor;
                targetBgColor = baseBgColor;
                targetGraphic.color = baseBgColor;
            }

            if (hasBorder && borderGraphic != null)
            {
                currentBorderColor = baseBorderColor;
                targetBorderColor = baseBorderColor;
                borderGraphic.color = baseBorderColor;
            }
        }

        void OnDisable()
        {
            pointerInside = false;
            isPressed = false;
            currentScale = 1f;
            targetScale = 1f;

            if (target != null)
                target.localScale = baseScale;

            if (hasBg && targetGraphic != null)
                targetGraphic.color = baseBgColor;

            if (hasBorder && borderGraphic != null)
                borderGraphic.color = baseBorderColor;
        }

        public void SetBaseColors(Color bg, Color border)
        {
            Initialize();
            baseBgColor = bg;
            baseBorderColor = border;

            if (!pointerInside && !isPressed)
            {
                targetBgColor = bg;
                targetBorderColor = border;
            }
            else if (pointerInside && !isPressed)
            {
                ComputeHoverTargets();
            }
            else if (isPressed)
            {
                ComputePressTargets();
            }
        }

        public void SetScale(float hover, float press)
        {
            hoverScale = hover;
            pressScale = press;
        }

        public void SetBorderGraphic(Graphic border)
        {
            borderGraphic = border;
            if (borderGraphic != null)
            {
                baseBorderColor = borderGraphic.color;
                currentBorderColor = baseBorderColor;
                targetBorderColor = baseBorderColor;
                hasBorder = true;
            }
        }

        void Update()
        {
            bool interactable = selectable == null || selectable.interactable;

            if (!interactable)
            {
                targetScale = 1f;
                targetBgColor = new Color(baseBgColor.r, baseBgColor.g, baseBgColor.b, baseBgColor.a * 0.45f);
                targetBorderColor = new Color(baseBorderColor.r, baseBorderColor.g, baseBorderColor.b, baseBorderColor.a * 0.35f);
            }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            float t = 1f - Mathf.Exp(-responseSpeed * dt);

            // Interpolate scale
            currentScale = Mathf.Lerp(currentScale, targetScale, t);
            if (target != null)
                target.localScale = baseScale * currentScale;

            // Interpolate background color
            if (hasBg && targetGraphic != null)
            {
                currentBgColor = Color.Lerp(currentBgColor, targetBgColor, t);
                targetGraphic.color = currentBgColor;
            }

            // Interpolate border color
            if (hasBorder && borderGraphic != null)
            {
                currentBorderColor = Color.Lerp(currentBorderColor, targetBorderColor, t);
                borderGraphic.color = currentBorderColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable)
                return;

            pointerInside = true;
            if (!isPressed)
                ComputeHoverTargets();

            KaplaoUiSoundFx.PlayHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (!isPressed)
                ComputeNormalTargets();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable)
                return;

            isPressed = true;
            ComputePressTargets();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            if (pointerInside)
                ComputeHoverTargets();
            else
                ComputeNormalTargets();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable)
                return;

            // Micro bounce impulse on click
            currentScale = Mathf.Min(currentScale, pressScale * 0.97f);
            KaplaoUiSoundFx.PlayClick();
        }

        void ComputeHoverTargets()
        {
            targetScale = hoverScale;
            targetBgColor = Color.Lerp(baseBgColor, Color.white, 0.14f);
            targetBorderColor = Color.Lerp(baseBorderColor, NeonCyan, 0.55f);
        }

        void ComputePressTargets()
        {
            targetScale = pressScale;
            targetBgColor = Color.Lerp(baseBgColor, Color.black, 0.18f);
            targetBorderColor = Color.Lerp(baseBorderColor, Color.white, 0.25f);
        }

        void ComputeNormalTargets()
        {
            targetScale = 1f;
            targetBgColor = baseBgColor;
            targetBorderColor = baseBorderColor;
        }
    }
}
