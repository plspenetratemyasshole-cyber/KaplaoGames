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
            KaplaoUiStyler.ApplyRuntimePolish(transform);
        }

        void Start()
        {
            ApplySkin(transform);
            KaplaoUiStyler.ApplyRuntimePolish(transform);
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
                        if (pImg != null && KaplaoUiStyler.CanSafelyReplaceSprite(pImg.sprite))
                        {
                            pImg.sprite = KaplaoUiSprites.RoundedRect(el.Radius > 0 ? el.Radius : 16);
                            pImg.type = Image.Type.Sliced;
                        }
                        Transform border = el.transform.Find("UI_Border");
                        if (border != null)
                        {
                            Image bImg = border.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(el.Radius > 0 ? el.Radius : 16, 2);
                                bImg.type = Image.Type.Sliced;
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
                            }
                        }
                        break;

                    case StyledElementType.Card:
                        Image cImg = el.GetComponent<Image>();
                        if (cImg != null && KaplaoUiStyler.CanSafelyReplaceSprite(cImg.sprite))
                        {
                            cImg.sprite = KaplaoUiSprites.RoundedRect(el.Radius > 0 ? el.Radius : 12);
                            cImg.type = Image.Type.Sliced;
                        }
                        Transform cBorder = el.transform.Find("UI_Border");
                        if (cBorder != null)
                        {
                            Image bImg = cBorder.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(el.Radius > 0 ? el.Radius : 12, 2);
                                bImg.type = Image.Type.Sliced;
                            }
                        }
                        break;

                    case StyledElementType.Button:
                        Button btn = el.GetComponent<Button>();
                        if (btn != null)
                        {
                            Image bImage = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
                            if (bImage != null && KaplaoUiStyler.CanSafelyReplaceSprite(bImage.sprite))
                            {
                                bImage.sprite = KaplaoUiSprites.RoundedRect(el.Radius > 0 ? el.Radius : 10);
                                bImage.type = Image.Type.Sliced;
                            }
                        }
                        Transform btnBorder = el.transform.Find("UI_Border");
                        if (btnBorder != null)
                        {
                            Image bImg = btnBorder.GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.sprite = KaplaoUiSprites.RoundedBorder(el.Radius > 0 ? el.Radius : 10, 2);
                                bImg.type = Image.Type.Sliced;
                            }
                        }
                        break;

                    case StyledElementType.InputField:
                        TMP_InputField input = el.GetComponent<TMP_InputField>();
                        if (input != null)
                        {
                            Image inImg = input.targetGraphic as Image ?? input.GetComponent<Image>();
                            if (inImg != null && KaplaoUiStyler.CanSafelyReplaceSprite(inImg.sprite))
                            {
                                inImg.sprite = KaplaoUiSprites.RoundedRect(10);
                                inImg.type = Image.Type.Sliced;
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
                            }
                        }
                        break;

                    case StyledElementType.Dropdown:
                        TMP_Dropdown drop = el.GetComponent<TMP_Dropdown>();
                        if (drop != null)
                        {
                            Image dropImg = drop.targetGraphic as Image ?? drop.GetComponent<Image>();
                            if (dropImg != null && KaplaoUiStyler.CanSafelyReplaceSprite(dropImg.sprite))
                            {
                                dropImg.sprite = KaplaoUiSprites.RoundedRect(10);
                                dropImg.type = Image.Type.Sliced;
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
