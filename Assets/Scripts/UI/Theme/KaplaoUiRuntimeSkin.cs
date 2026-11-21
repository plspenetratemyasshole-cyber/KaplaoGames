using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI.Theme
{
    /// <summary>
    /// Runtime skin applicator. Attached to Canvas in MainMenu and SampleScene.
    /// In Awake, re-links 9-slice sprites and theme assets for builds and Play Mode.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Runtime Skin")]
    public class KaplaoUiRuntimeSkin : MonoBehaviour
    {
        void Awake()
        {
            ApplySkin(transform);
        }

        void Start()
        {
        }

        public static void ApplySkin(Transform root)
        {
            if (root == null)
                return;

            KaplaoUiStyledElement[] elements = root.GetComponentsInChildren<KaplaoUiStyledElement>(true);
            for (int i = 0; i < elements.Length; i++)
            {
                KaplaoUiStyledElement el = elements[i];
                if (el == null) continue;

                switch (el.ElementType)
                {
                    case StyledElementType.Panel:
                        Image pImg = el.GetComponent<Image>();
                        int pRadius = el.Radius > 0 ? el.Radius : 16;
                        if (pImg != null && !KaplaoUiStyler.IsProtectedArtImage(pImg) && (pImg.sprite == null || KaplaoUiStyler.CanSafelyReplaceSprite(pImg.sprite)))
                        {
                            pImg.sprite = KaplaoUiSprites.RoundedRect(pRadius);
                            pImg.type = Image.Type.Sliced;
                            pImg.pixelsPerUnitMultiplier = 14f / pRadius;
                        }
                        Transform border = el.transform.Find("UI_Border");
                        if (border != null)
                        {
                            Image bImg = border.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(pRadius, 2);
                                bImg.type = Image.Type.Sliced;
                                bImg.pixelsPerUnitMultiplier = 14f / pRadius;
                            }
                        }
                        Transform shadow = el.transform.Find("UI_Shadow");
                        if (shadow != null)
                        {
                            Image sImg = shadow.GetComponent<Image>();
                            if (sImg != null)
                            {
                                sImg.sprite = KaplaoUiSprites.SoftShadow(18, 16);
                                sImg.type = Image.Type.Sliced;
                            }
                        }
                        Transform glow = el.transform.Find("UI_TopGlow");
                        if (glow != null)
                        {
                            Image gImg = glow.GetComponent<Image>();
                            if (gImg != null)
                            {
                                gImg.sprite = KaplaoUiSprites.TopGradientOverlay();
                                gImg.type = Image.Type.Sliced;
                                gImg.pixelsPerUnitMultiplier = 14f / pRadius;
                            }
                        }
                        break;

                    case StyledElementType.Card:
                        Image cImg = el.GetComponent<Image>();
                        int cRadius = el.Radius > 0 ? el.Radius : 12;
                        if (cImg != null && !KaplaoUiStyler.IsProtectedArtImage(cImg) && (cImg.sprite == null || KaplaoUiStyler.CanSafelyReplaceSprite(cImg.sprite)))
                        {
                            cImg.sprite = KaplaoUiSprites.RoundedRect(cRadius);
                            cImg.type = Image.Type.Sliced;
                            cImg.pixelsPerUnitMultiplier = 14f / cRadius;
                        }
                        Transform cBorder = el.transform.Find("UI_Border");
                        if (cBorder != null)
                        {
                            Image bImg = cBorder.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(cRadius, 2);
                                bImg.type = Image.Type.Sliced;
                                bImg.pixelsPerUnitMultiplier = 14f / cRadius;
                            }
                        }
                        break;

                    case StyledElementType.Button:
                        Button btn = el.GetComponent<Button>();
                        int bRadius = el.Radius > 0 ? el.Radius : 10;
                        if (btn != null)
                        {
                            Image bImage = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
                            if (bImage != null && !KaplaoUiStyler.IsProtectedArtImage(bImage) && (bImage.sprite == null || KaplaoUiStyler.CanSafelyReplaceSprite(bImage.sprite)))
                            {
                                bImage.sprite = KaplaoUiSprites.RoundedRect(bRadius);
                                bImage.type = Image.Type.Sliced;
                                bImage.pixelsPerUnitMultiplier = 14f / bRadius;
                            }
                        }
                        Transform btnBorder = el.transform.Find("UI_Border");
                        if (btnBorder != null)
                        {
                            Image bImg = btnBorder.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(bRadius, 2);
                                bImg.type = Image.Type.Sliced;
                                bImg.pixelsPerUnitMultiplier = 14f / bRadius;
                            }
                        }
                        Transform btnGlow = el.transform.Find("UI_BtnGlow");
                        if (btnGlow != null)
                        {
                            Image gImg = btnGlow.GetComponent<Image>();
                            if (gImg != null)
                            {
                                gImg.sprite = KaplaoUiSprites.TopGradientOverlay();
                                gImg.type = Image.Type.Sliced;
                                gImg.pixelsPerUnitMultiplier = 14f / bRadius;
                            }
                        }
                        break;

                    case StyledElementType.InputField:
                        TMP_InputField input = el.GetComponent<TMP_InputField>();
                        if (input != null)
                        {
                            Image inImg = input.targetGraphic as Image ?? input.GetComponent<Image>();
                            if (inImg != null && !KaplaoUiStyler.IsProtectedArtImage(inImg) && (inImg.sprite == null || KaplaoUiStyler.CanSafelyReplaceSprite(inImg.sprite)))
                            {
                                inImg.sprite = KaplaoUiSprites.RoundedRect(10);
                                inImg.type = Image.Type.Sliced;
                                inImg.pixelsPerUnitMultiplier = 14f / 10f;
                            }
                        }
                        Transform inBorder = el.transform.Find("UI_Border");
                        if (inBorder != null)
                        {
                            Image bImg = inBorder.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(10, 2);
                                bImg.type = Image.Type.Sliced;
                                bImg.pixelsPerUnitMultiplier = 14f / 10f;
                            }
                        }
                        break;

                    case StyledElementType.Dropdown:
                        TMP_Dropdown drop = el.GetComponent<TMP_Dropdown>();
                        if (drop != null)
                        {
                            Image dropImg = drop.targetGraphic as Image ?? drop.GetComponent<Image>();
                            if (dropImg != null && !KaplaoUiStyler.IsProtectedArtImage(dropImg) && (dropImg.sprite == null || KaplaoUiStyler.CanSafelyReplaceSprite(dropImg.sprite)))
                            {
                                dropImg.sprite = KaplaoUiSprites.RoundedRect(10);
                                dropImg.type = Image.Type.Sliced;
                                dropImg.pixelsPerUnitMultiplier = 14f / 10f;
                            }
                        }
                        break;

                    case StyledElementType.Slider:
                        Slider slider = el.GetComponent<Slider>();
                        if (slider != null)
                        {
                            Transform bg = slider.transform.Find("Background");
                            if (bg != null && bg.TryGetComponent(out Image bgImg))
                            {
                                bgImg.sprite = KaplaoUiSprites.Pill();
                                bgImg.type = Image.Type.Sliced;
                            }
                            Transform fill = slider.transform.Find("Fill Area/Fill");
                            if (fill != null && fill.TryGetComponent(out Image fillImg))
                            {
                                fillImg.sprite = KaplaoUiSprites.Pill();
                                fillImg.type = Image.Type.Sliced;
                            }
                            Transform handle = slider.transform.Find("Handle Slide Area/Handle");
                            if (handle != null && handle.TryGetComponent(out Image handleImg))
                            {
                                handleImg.sprite = KaplaoUiSprites.Circle();
                                handleImg.type = Image.Type.Simple;
                            }
                        }
                        break;

                    case StyledElementType.ToggleSwitch:
                        Toggle toggle = el.GetComponent<Toggle>();
                        if (toggle != null)
                        {
                            Transform tBg = toggle.transform.Find("Background");
                            if (tBg != null && tBg.TryGetComponent(out Image tBgImg))
                            {
                                tBgImg.sprite = KaplaoUiSprites.Pill();
                                tBgImg.type = Image.Type.Sliced;
                            }
                            Transform checkmark = tBg != null ? tBg.Find("Checkmark") : null;
                            if (checkmark != null && checkmark.TryGetComponent(out Image checkImg))
                            {
                                checkImg.sprite = KaplaoUiSprites.Circle();
                                checkImg.type = Image.Type.Simple;
                            }
                        }
                        break;
                }
            }
        }
    }
}
