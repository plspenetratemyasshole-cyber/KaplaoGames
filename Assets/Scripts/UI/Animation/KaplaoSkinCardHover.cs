using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Playful hover card interaction and cyan active border for Runner Skin cards in SettingsPanel.
    /// Scales to 1.12 on hover and lights up an Electric Cyan border when selected.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    [AddComponentMenu("Kaplao/UI/Skin Card Hover")]
    public class KaplaoSkinCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] float hoverScale = 1.10f;
        [SerializeField] float responseSpeed = 18f;

        Toggle toggle;
        Image borderImage;
        float currentScale = 1f;
        float targetScale = 1f;
        bool isHovered;

        static readonly Color SelectedBorderColor = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.95f); // #38BDF8
        static readonly Color UnselectedBorderColor = new Color(0x2B / 255f, 0x3B / 255f, 0x5C / 255f, 0.40f);

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            EnsureBorder();
        }

        void OnEnable()
        {
            if (toggle == null)
                toggle = GetComponent<Toggle>();

            EnsureBorder();

            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(OnToggleChanged);
                toggle.onValueChanged.AddListener(OnToggleChanged);
                UpdateBorderState(toggle.isOn);
            }
        }

        void OnDisable()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(OnToggleChanged);

            targetScale = 1f;
            currentScale = 1f;
            transform.localScale = Vector3.one;
        }

        public void EnsureBorder()
        {
            if (TryGetComponent(out Image bgImg))
            {
                bgImg.sprite = KaplaoUiSprites.RoundedRect(10);
                bgImg.type = Image.Type.Sliced;
                bgImg.pixelsPerUnitMultiplier = 1.4f;
                bgImg.color = new Color(0x17 / 255f, 0x20 / 255f, 0x36 / 255f, 1f);
            }

            Transform checkmark = transform.Find("Checkmark");
            if (checkmark != null && checkmark.TryGetComponent(out Image chkImg))
            {
                chkImg.sprite = KaplaoUiSprites.RoundedRect(10);
                chkImg.type = Image.Type.Sliced;
                chkImg.pixelsPerUnitMultiplier = 1.4f;
                chkImg.color = new Color(0.22f, 0.74f, 0.97f, 0.35f);
            }

            Transform btnGlow = transform.Find("UI_BtnGlow");
            if (btnGlow != null)
            {
                if (Application.isPlaying) Destroy(btnGlow.gameObject);
                else DestroyImmediate(btnGlow.gameObject);
            }
            Transform topGlow = transform.Find("UI_TopGlow");
            if (topGlow != null)
            {
                if (Application.isPlaying) Destroy(topGlow.gameObject);
                else DestroyImmediate(topGlow.gameObject);
            }

            Transform preview = transform.Find("Preview");
            if (preview != null && (name.Contains("Random") || name.Contains("Rnd")))
            {
                preview.gameObject.SetActive(false);
            }

            if (borderImage != null)
                return;

            Transform border = transform.Find("UI_Border");
            if (border == null)
            {
                GameObject borderObj = new GameObject("UI_Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                borderObj.transform.SetParent(transform, false);
                border = borderObj.transform;
            }

            RectTransform rect = border.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            borderImage = border.GetComponent<Image>();
            borderImage.sprite = KaplaoUiSprites.RoundedBorder(10, 2);
            borderImage.type = Image.Type.Sliced;
            borderImage.pixelsPerUnitMultiplier = 1.4f;
            borderImage.raycastTarget = false;

            LayoutElement le = border.GetComponent<LayoutElement>();
            if (le != null)
                le.ignoreLayout = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            targetScale = hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            targetScale = 1f;
        }

        void OnToggleChanged(bool isOn)
        {
            UpdateBorderState(isOn);
        }

        void UpdateBorderState(bool isOn)
        {
            EnsureBorder();
            if (borderImage != null)
            {
                borderImage.color = isOn ? SelectedBorderColor : UnselectedBorderColor;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            float t = 1f - Mathf.Exp(-responseSpeed * dt);
            currentScale = Mathf.Lerp(currentScale, targetScale, t);
            transform.localScale = Vector3.one * currentScale;
        }
    }
}
