using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KaplaoGames.UI.Animation;

namespace KaplaoGames.UI.Theme
{
    /// <summary>
    /// Universal styling utility for Kaplao modern UI.
    /// Works both in Editor upgrade tools and dynamically at runtime.
    /// </summary>
    public static class KaplaoUiStyler
    {
        public static bool HasAncestorName(Transform t, string ancestorName)
        {
            Transform cur = t;
            while (cur != null)
            {
                if (cur.name.Equals(ancestorName, StringComparison.OrdinalIgnoreCase) ||
                    cur.name.IndexOf(ancestorName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                cur = cur.parent;
            }
            return false;
        }

        public static bool IsOldLegacyTextureName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("70_2026") ||
                   name.StartsWith("71_2026") ||
                   name.StartsWith("72_2026") ||
                   name.StartsWith("73_2026") ||
                   name.StartsWith("74_2026") ||
                   name.StartsWith("75_2026") ||
                   name.StartsWith("76_2026") ||
                   name.StartsWith("77_2026") ||
                   name.StartsWith("78_2026") ||
                   name.StartsWith("79_2026") ||
                   name.StartsWith("80_2026") ||
                   name.Contains("Безымянный-Photoroom") ||
                   name.Contains("1290196515");
        }

        public static bool IsDefaultOrThemeSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            return name == "UISprite" ||
                   name == "Background" ||
                   name == "InputFieldBackground" ||
                   name == "Knob" ||
                   name == "Checkmark" ||
                   name == "DropdownArrow" ||
                   name.StartsWith("KaplaoUi_", StringComparison.OrdinalIgnoreCase) ||
                   name == "rounded_rect" ||
                   name == "rounded_border" ||
                   name == "pill" ||
                   name == "switch_track" ||
                   name == "soft_shadow" ||
                   name == "top_glow" ||
                   name == "circle" ||
                   name == "chevron_down";
        }

        public static bool IsProtectedArtImage(Image img)
        {
            if (img == null) return false;
            if (img is RawImage) return true;

            string name = img.gameObject.name;
            string parentName = img.transform.parent != null ? img.transform.parent.name : string.Empty;

            // 1. Ancestor check: UsualToolSlots, CreateToolSlots, SelectionMovePad, MobileJoystick, SelectionToolbar
            if (HasAncestorName(img.transform, "UsualToolSlots") ||
                HasAncestorName(img.transform, "CreateToolSlots") ||
                HasAncestorName(img.transform, "SelectionMovePad") ||
                HasAncestorName(img.transform, "MobileJoystick"))
            {
                return true;
            }

            // 2. Specific button names or icons
            if (name.StartsWith("Slot", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Equals("LayerPrevButton", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("LayerNextButton", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Prev", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Next", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Arrow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Equals("SendButton", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("InfoButton", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Unban", StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentName.Equals("SendButton", StringComparison.OrdinalIgnoreCase) ||
                parentName.Equals("InfoButton", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 3. Skin previews & color picker
            if (name.StartsWith("SkinToggle", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("SkinRandomToggle", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Preview", StringComparison.OrdinalIgnoreCase) ||
                parentName.StartsWith("SkinToggle", StringComparison.OrdinalIgnoreCase) ||
                parentName.Equals("SkinRow", StringComparison.OrdinalIgnoreCase) ||
                HasAncestorName(img.transform, "SkinRow"))
            {
                return true;
            }

            if (name.Contains("SvImage") || name.Contains("HueImage") ||
                name.Contains("SvPicker") || name.Contains("HuePicker") ||
                name.StartsWith("Swatch_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 4. Cursors, block previews, twist
            if (name.Equals("Twist", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("FileTree") || name.Contains("Cursor") ||
                name.Equals("TexturePreview", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("BlockPreview", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool CanSafelyReplaceSprite(Image image, bool forceReplaceSprite = true)
        {
            if (image == null) return false;
            if (IsProtectedArtImage(image)) return false;

            Sprite currentSprite = image.sprite;
            if (currentSprite == null) return true;

            string name = currentSprite.name;
            if (IsDefaultOrThemeSprite(name)) return true;
            if (IsOldLegacyTextureName(name)) return true;

            return false;
        }

        public static bool CanSafelyReplaceSprite(Sprite currentSprite)
        {
            if (currentSprite == null)
                return true;

            string name = currentSprite.name;
            if (IsDefaultOrThemeSprite(name)) return true;
            if (IsOldLegacyTextureName(name)) return true;

            return false;
        }

        public static void StylePanel(GameObject panel, bool withShadow = true, bool withHeaderGlow = true, bool forceReplaceSprite = true)
        {
            if (panel == null)
                return;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;

            Image image = panel.GetComponent<Image>();
            if (image == null)
                image = panel.AddComponent<Image>();

            if (CanSafelyReplaceSprite(image, forceReplaceSprite))
            {
                image.sprite = KaplaoUiSprites.RoundedRect(16);
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 14f / 16f;
            }
            image.color = theme.SurfacePrimary;

            // Border
            EnsureChildBorder(panel.transform, 16, 2, theme.BorderSubtle);

            // Top Glow
            if (withHeaderGlow)
            {
                EnsureChildTopGlow(panel.transform, 16);
            }

            // Soft Shadow
            if (withShadow)
            {
                EnsureChildShadow(panel.transform, 18, 16, theme.SoftShadowColor);
            }

            KaplaoUiStyledElement marker = panel.GetComponent<KaplaoUiStyledElement>() ?? panel.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.Panel;
            marker.Radius = 16;
            marker.WithShadow = withShadow;
            marker.WithHeaderGlow = withHeaderGlow;
        }

        public static void StyleBackdrop(Image dimImage)
        {
            if (dimImage == null)
                return;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;
            dimImage.color = theme.BackdropDim;

            KaplaoUiStyledElement marker = dimImage.GetComponent<KaplaoUiStyledElement>() ?? dimImage.gameObject.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.Backdrop;
        }

        public static void CenterPivotPreservingPosition(RectTransform rt)
        {
            if (rt == null) return;
            Vector2 newPivot = new Vector2(0.5f, 0.5f);
            if ((rt.pivot - newPivot).sqrMagnitude < 0.0001f) return;
            // Skip elements driven by RobloxStyleRect (RobloxStyleRect overwrites pivot in Update)
            if (rt.GetComponent("RobloxStyleRect") != null) return;

            Vector2 size = rt.rect.size;
            if (size.sqrMagnitude < 0.01f) size = rt.sizeDelta;
            Vector2 deltaPivot = newPivot - rt.pivot;
            Vector3 deltaPos = new Vector3(deltaPivot.x * size.x * rt.localScale.x, deltaPivot.y * size.y * rt.localScale.y, 0f);
            rt.pivot = newPivot;
            rt.anchoredPosition += (Vector2)deltaPos;
        }

        public static void StyleButton(Button button, KaplaoButtonVariant variant = KaplaoButtonVariant.Secondary, bool forceReplaceSprite = true)
        {
            if (button == null)
                return;

            button.transition = Selectable.Transition.None;

            RectTransform btnRt = button.GetComponent<RectTransform>();
            if (btnRt != null && btnRt.anchorMin == btnRt.anchorMax)
            {
                CenterPivotPreservingPosition(btnRt);
            }

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;
            var (normal, hover, press, border) = theme.GetButtonColors(variant);

            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            bool isProtected = IsProtectedArtImage(image);

            if (image != null)
            {
                if (!isProtected && CanSafelyReplaceSprite(image, forceReplaceSprite))
                {
                    int radius = variant == KaplaoButtonVariant.Ghost || variant == KaplaoButtonVariant.Icon ? 8 : 10;
                    image.sprite = KaplaoUiSprites.RoundedRect(radius);
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 14f / radius;
                    image.color = normal;
                }
                else if (isProtected)
                {
                    image.color = Color.white;
                    image.preserveAspect = true;
                }
                else
                {
                    image.color = normal;
                }
            }

            ColorBlock colors = button.colors;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            if (!isProtected && variant != KaplaoButtonVariant.Ghost && variant != KaplaoButtonVariant.Icon)
            {
                EnsureChildBorder(button.transform, 10, 2, border);
            }
            else if (isProtected)
            {
                Transform oldBorder = button.transform.Find("UI_Border");
                if (oldBorder != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(oldBorder.gameObject);
                    else UnityEngine.Object.DestroyImmediate(oldBorder.gameObject);
                }
            }

            // UI_BtnGlow is removed completely from all buttons
            Transform oldGlow = button.transform.Find("UI_BtnGlow");
            if (oldGlow != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(oldGlow.gameObject);
                else UnityEngine.Object.DestroyImmediate(oldGlow.gameObject);
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = theme.TextPrimary;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;

                label.enableAutoSizing = true;
                label.fontSizeMax = Mathf.Clamp(label.fontSize, 13f, 22f);
                label.fontSizeMin = Mathf.Max(8f, label.fontSizeMax * 0.6f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.textWrappingMode = TextWrappingModes.NoWrap;

                RectTransform btnRect = button.GetComponent<RectTransform>();
                if (btnRect != null && btnRect.rect.width > 0f && btnRect.rect.width < 85f)
                {
                    label.fontStyle &= ~FontStyles.Bold;
                }
                else
                {
                    label.fontStyle |= FontStyles.Bold;
                }

                Shadow shadow = label.GetComponent<Shadow>();
                if (shadow == null)
                    shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }

            UiButtonLeanScale scale = button.GetComponent<UiButtonLeanScale>();
            if (scale == null)
                scale = button.gameObject.AddComponent<UiButtonLeanScale>();

            if (isProtected)
            {
                scale.SetBaseColors(Color.white, Color.clear);
            }
            else
            {
                scale.SetBaseColors(normal, border);
                if (variant != KaplaoButtonVariant.Ghost && variant != KaplaoButtonVariant.Icon)
                {
                    KaplaoUiButtonRipple ripple = button.GetComponent<KaplaoUiButtonRipple>();
                    if (ripple == null)
                        button.gameObject.AddComponent<KaplaoUiButtonRipple>();
                }
            }

            KaplaoUiStyledElement marker = button.GetComponent<KaplaoUiStyledElement>() ?? button.gameObject.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.Button;
            marker.ButtonVariant = variant;
            marker.Radius = 10;
        }

        public static void StyleInputField(TMP_InputField input, bool forceReplaceSprite = true)
        {
            if (input == null)
                return;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;

            Image bg = input.targetGraphic as Image ?? input.GetComponent<Image>();
            if (bg != null)
            {
                if (CanSafelyReplaceSprite(bg, forceReplaceSprite))
                {
                    bg.sprite = KaplaoUiSprites.RoundedRect(10);
                    bg.type = Image.Type.Sliced;
                    bg.pixelsPerUnitMultiplier = 14f / 10f;
                }
                bg.color = theme.InputSurface;
            }

            ColorBlock colors = input.colors;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            input.colors = colors;

            Image border = EnsureChildBorder(input.transform, 10, 2, theme.InputBorder);

            KaplaoUiInputFocusEffect focus = input.GetComponent<KaplaoUiInputFocusEffect>();
            if (focus == null)
                focus = input.gameObject.AddComponent<KaplaoUiInputFocusEffect>();
            if (border != null)
                focus.SetBorderGraphic(border);

            if (input.textViewport != null)
            {
                RectMask2D mask = input.textViewport.GetComponent<RectMask2D>();
                if (mask == null)
                    mask = input.textViewport.gameObject.AddComponent<RectMask2D>();
            }

            if (input.placeholder is TextMeshProUGUI placeholder)
            {
                placeholder.color = theme.TextMuted;
                placeholder.raycastTarget = false;
            }

            if (input.textComponent != null)
            {
                input.textComponent.color = theme.TextPrimary;
                input.textComponent.raycastTarget = false;
            }

            KaplaoUiStyledElement marker = input.GetComponent<KaplaoUiStyledElement>() ?? input.gameObject.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.InputField;
            marker.Radius = 10;
        }

        public static void StyleDropdown(TMP_Dropdown dropdown, bool forceReplaceSprite = true)
        {
            if (dropdown == null)
                return;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;

            Image bg = dropdown.targetGraphic as Image ?? dropdown.GetComponent<Image>();
            if (bg != null)
            {
                if (CanSafelyReplaceSprite(bg, forceReplaceSprite))
                {
                    bg.sprite = KaplaoUiSprites.RoundedRect(10);
                    bg.type = Image.Type.Sliced;
                    bg.pixelsPerUnitMultiplier = 14f / 10f;
                }
                bg.color = theme.SurfaceCard;
            }

            EnsureChildBorder(dropdown.transform, 10, 2, theme.BorderSubtle);

            Transform arrowTrans = dropdown.transform.Find("Arrow");
            if (arrowTrans != null)
            {
                Image arrow = arrowTrans.GetComponent<Image>();
                if (arrow != null)
                {
                    arrow.sprite = KaplaoUiSprites.ChevronDown();
                    arrow.color = theme.TextSecondary;
                }
            }

            if (dropdown.template != null)
            {
                RectTransform templateRect = dropdown.template;
                Image templateBg = templateRect.GetComponent<Image>();
                if (templateBg != null)
                {
                    templateBg.sprite = KaplaoUiSprites.RoundedRect(12);
                    templateBg.type = Image.Type.Sliced;
                    templateBg.pixelsPerUnitMultiplier = 14f / 12f;
                    templateBg.color = theme.SurfacePrimary;
                }

                EnsureChildBorder(templateRect, 12, 2, theme.BorderSubtle);
                EnsureChildShadow(templateRect, 16, 12, theme.SoftShadowColor);

                Transform viewport = templateRect.Find("Viewport");
                if (viewport != null)
                {
                    Mask oldMask = viewport.GetComponent<Mask>();
                    if (oldMask != null)
                        UnityEngine.Object.DestroyImmediate(oldMask);

                    Image vpImg = viewport.GetComponent<Image>();
                    if (vpImg != null)
                    {
                        vpImg.color = new Color(0f, 0f, 0f, 0f);
                        vpImg.raycastTarget = false;
                    }

                    RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
                    if (rectMask == null)
                        viewport.gameObject.AddComponent<RectMask2D>();
                }
            }

            KaplaoUiStyledElement marker = dropdown.GetComponent<KaplaoUiStyledElement>() ?? dropdown.gameObject.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.Dropdown;
            marker.Radius = 10;
        }

        public static void StyleSlider(Slider slider)
        {
            if (slider == null)
                return;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;

            Transform bg = slider.transform.Find("Background");
            if (bg != null)
            {
                RectTransform bgRt = bg as RectTransform;
                if (bgRt != null)
                {
                    bgRt.anchorMin = new Vector2(0f, 0.5f);
                    bgRt.anchorMax = new Vector2(1f, 0.5f);
                    bgRt.pivot = new Vector2(0.5f, 0.5f);
                    bgRt.anchoredPosition = Vector2.zero;
                    bgRt.sizeDelta = new Vector2(0f, 8f);
                }

                Image bgImg = bg.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.sprite = KaplaoUiSprites.Pill();
                    bgImg.type = Image.Type.Sliced;
                    bgImg.color = new Color(0x1E / 255f, 0x29 / 255f, 0x3B / 255f, 0.95f);
                }
            }

            Transform fillArea = slider.transform.Find("Fill Area");
            if (fillArea != null)
            {
                RectTransform faRt = fillArea as RectTransform;
                if (faRt != null)
                {
                    faRt.anchorMin = new Vector2(0f, 0.5f);
                    faRt.anchorMax = new Vector2(1f, 0.5f);
                    faRt.pivot = new Vector2(0.5f, 0.5f);
                    faRt.anchoredPosition = Vector2.zero;
                    faRt.sizeDelta = new Vector2(-12f, 8f);
                }
            }

            Transform fill = slider.transform.Find("Fill Area/Fill");
            if (fill != null)
            {
                RectTransform fillRt = fill as RectTransform;
                if (fillRt != null)
                {
                    fillRt.anchorMin = new Vector2(0f, 0f);
                    fillRt.anchorMax = new Vector2(1f, 1f);
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;
                }

                Image fillImg = fill.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.sprite = KaplaoUiSprites.Pill();
                    fillImg.type = Image.Type.Sliced;
                    fillImg.color = theme.AccentCyan;
                }
            }

            Transform handleArea = slider.transform.Find("Handle Slide Area");
            if (handleArea != null)
            {
                RectTransform haRt = handleArea as RectTransform;
                if (haRt != null)
                {
                    haRt.anchorMin = new Vector2(0f, 0.5f);
                    haRt.anchorMax = new Vector2(1f, 0.5f);
                    haRt.pivot = new Vector2(0.5f, 0.5f);
                    haRt.anchoredPosition = Vector2.zero;
                    haRt.sizeDelta = new Vector2(-12f, 20f);
                }
            }

            Transform handle = slider.transform.Find("Handle Slide Area/Handle");
            if (handle != null)
            {
                RectTransform hRt = handle as RectTransform;
                if (hRt != null)
                {
                    hRt.anchorMin = new Vector2(0.5f, 0.5f);
                    hRt.anchorMax = new Vector2(0.5f, 0.5f);
                    hRt.pivot = new Vector2(0.5f, 0.5f);
                    hRt.anchoredPosition = new Vector2(hRt.anchoredPosition.x, 0f);
                    hRt.sizeDelta = new Vector2(20f, 20f);
                }

                Image handleImg = handle.GetComponent<Image>();
                if (handleImg != null)
                {
                    handleImg.sprite = KaplaoUiSprites.Circle();
                    handleImg.type = Image.Type.Simple;
                    handleImg.preserveAspect = true;
                    handleImg.color = Color.white;
                }

                UiButtonLeanScale scale = handle.GetComponent<UiButtonLeanScale>();
                if (scale == null)
                    handle.gameObject.AddComponent<UiButtonLeanScale>();
            }

            KaplaoUiStyledElement marker = slider.GetComponent<KaplaoUiStyledElement>() ?? slider.gameObject.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.Slider;
        }

        public static void StyleToggleAsSwitch(Toggle toggle)
        {
            if (toggle == null)
                return;

            // Never style character skin previews as switch
            if (toggle.name.StartsWith("SkinToggle", StringComparison.OrdinalIgnoreCase) ||
                toggle.name.StartsWith("SkinRandom", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;

            Transform bg = toggle.transform.Find("Background");
            if (bg == null)
                return;

            RectTransform bgRect = bg as RectTransform;
            if (bgRect != null)
            {
                bgRect.anchorMin = new Vector2(0f, 0.5f);
                bgRect.anchorMax = new Vector2(0f, 0.5f);
                bgRect.pivot = new Vector2(0f, 0.5f);
                bgRect.anchoredPosition = Vector2.zero;
                bgRect.sizeDelta = new Vector2(46f, 24f);
            }

            Image bgImg = bg.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = KaplaoUiSprites.SwitchTrack();
                bgImg.type = Image.Type.Sliced;
                bgImg.pixelsPerUnitMultiplier = 2f;
                bgImg.color = toggle.isOn
                    ? new Color(0x10 / 255f, 0xB9 / 255f, 0x81 / 255f, 1f)
                    : new Color(0x33 / 255f, 0x41 / 255f, 0x55 / 255f, 1f);
            }

            Transform checkmark = bg.Find("Checkmark");
            if (checkmark != null)
            {
                Image checkImg = checkmark.GetComponent<Image>();
                if (checkImg != null)
                {
                    checkImg.sprite = KaplaoUiSprites.Circle();
                    checkImg.type = Image.Type.Simple;
                    checkImg.preserveAspect = true;
                    checkImg.color = Color.white;
                    checkImg.raycastTarget = false;
                }

                Shadow shadow = checkmark.GetComponent<Shadow>();
                if (shadow == null)
                    shadow = checkmark.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
                shadow.effectDistance = new Vector2(0f, -1f);

                RectTransform checkRect = checkmark as RectTransform;
                if (checkRect != null)
                {
                    checkRect.anchorMin = new Vector2(0.5f, 0.5f);
                    checkRect.anchorMax = new Vector2(0.5f, 0.5f);
                    checkRect.pivot = new Vector2(0.5f, 0.5f);
                    checkRect.sizeDelta = new Vector2(18f, 18f);

                    KaplaoUiToggleAnimator anim = toggle.GetComponent<KaplaoUiToggleAnimator>();
                    if (anim == null)
                        anim = toggle.gameObject.AddComponent<KaplaoUiToggleAnimator>();

                    anim.Configure(checkRect, bgImg, -11f, 11f);
                }
            }

            Transform labelTrans = toggle.transform.Find("Label");
            if (labelTrans != null)
            {
                TextMeshProUGUI label = labelTrans.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    RectTransform labelRect = labelTrans as RectTransform;
                    if (labelRect != null)
                    {
                        labelRect.offsetMin = new Vector2(54f, labelRect.offsetMin.y);
                    }
                    label.color = Color.white;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 12f;
                    label.fontSizeMax = 18f;
                }
            }

            KaplaoUiStyledElement marker = toggle.GetComponent<KaplaoUiStyledElement>() ?? toggle.gameObject.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.ToggleSwitch;
        }

        public static void StyleCardRow(GameObject row, bool forceReplaceSprite = true)
        {
            if (row == null)
                return;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;

            Image img = row.GetComponent<Image>();
            if (img == null)
                img = row.AddComponent<Image>();

            if (CanSafelyReplaceSprite(img, forceReplaceSprite))
            {
                img.sprite = KaplaoUiSprites.RoundedRect(12);
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 14f / 12f;
            }
            img.color = theme.SurfaceCard;

            EnsureChildBorder(row.transform, 12, 2, theme.BorderSubtle);

            UiButtonLeanScale scale = row.GetComponent<UiButtonLeanScale>();
            if (scale == null)
                scale = row.AddComponent<UiButtonLeanScale>();
            scale.SetBaseColors(theme.SurfaceCard, theme.BorderSubtle);
            scale.SetScale(1.02f, 0.985f);

            KaplaoUiStyledElement marker = row.GetComponent<KaplaoUiStyledElement>() ?? row.AddComponent<KaplaoUiStyledElement>();
            marker.ElementType = StyledElementType.Card;
            marker.Radius = 12;
        }

        public static void StyleScrollView(ScrollRect scrollRect)
        {
            if (scrollRect == null)
                return;

            scrollRect.scrollSensitivity = 35f;

            Transform viewport = scrollRect.viewport != null ? scrollRect.viewport.transform : scrollRect.transform.Find("Viewport");
            if (viewport != null)
            {
                Mask oldMask = viewport.GetComponent<Mask>();
                if (oldMask != null)
                    UnityEngine.Object.DestroyImmediate(oldMask);

                Image vpImg = viewport.GetComponent<Image>();
                if (vpImg != null)
                {
                    vpImg.color = new Color(0f, 0f, 0f, 0f);
                    vpImg.raycastTarget = false;
                }

                RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
                if (rectMask == null)
                    viewport.gameObject.AddComponent<RectMask2D>();
            }

            if (scrollRect.verticalScrollbar != null)
            {
                Scrollbar bar = scrollRect.verticalScrollbar;
                Image barBg = bar.GetComponent<Image>();
                if (barBg != null)
                {
                    barBg.sprite = KaplaoUiSprites.Pill();
                    barBg.type = Image.Type.Sliced;
                    barBg.color = new Color(0x0A / 255f, 0x0F / 255f, 0x1C / 255f, 0.6f);
                }

                if (bar.handleRect != null)
                {
                    Image handleImg = bar.handleRect.GetComponent<Image>();
                    if (handleImg != null)
                    {
                        handleImg.sprite = KaplaoUiSprites.Pill();
                        handleImg.type = Image.Type.Sliced;
                        handleImg.color = new Color(0x33 / 255f, 0x41 / 255f, 0x55 / 255f, 0.85f);
                    }
                }
            }
        }

        public static KaplaoButtonVariant DetermineButtonVariant(GameObject buttonObj)
        {
            if (buttonObj == null)
                return KaplaoButtonVariant.Secondary;

            string name = buttonObj.name;
            TextMeshProUGUI label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            string text = label != null ? label.text : string.Empty;

            string combined = (name + " " + text).ToLowerInvariant();

            if (combined.Contains("delete") ||
                combined.Contains("ban") ||
                combined.Contains("kick") ||
                combined.Contains("reject") ||
                combined.Contains("signout") ||
                combined.Contains("sign out") ||
                combined.Contains("stop") ||
                combined.Contains("exit without"))
            {
                return KaplaoButtonVariant.Danger;
            }

            if (combined.Contains("play") ||
                combined.Contains("start") ||
                combined.Contains("continue") ||
                combined.Contains("publish") ||
                combined.Contains("createnewworld") ||
                combined.Contains("create new world") ||
                combined.Contains("accept") ||
                combined.Contains("confirm") ||
                combined.Contains("connect") ||
                combined.Contains("send") ||
                combined.Contains("signin") ||
                combined.Contains("sign in") ||
                combined.Contains("run"))
            {
                return KaplaoButtonVariant.Primary;
            }

            if (combined.Contains("save") || combined.Contains("apply"))
            {
                return KaplaoButtonVariant.Success;
            }

            if (text.Equals("×") || text.Equals("x", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("closexbutton", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("xbutton", StringComparison.OrdinalIgnoreCase))
            {
                return KaplaoButtonVariant.Ghost;
            }

            if (name.Equals("closebutton", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("cancelbutton", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("close") ||
                combined.Contains("cancel"))
            {
                return KaplaoButtonVariant.Secondary;
            }

            return KaplaoButtonVariant.Secondary;
        }

        public static Image EnsureChildBorder(Transform parent, int radius, int thickness, Color color)
        {
            Transform existing = parent.Find("UI_Border");
            GameObject borderObj;
            if (existing != null)
            {
                borderObj = existing.gameObject;
            }
            else
            {
                borderObj = new GameObject("UI_Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                borderObj.transform.SetParent(parent, false);
            }

            RectTransform rect = borderObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = KaplaoUiSprites.RoundedBorder(radius, thickness);
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = 14f / Mathf.Max(4f, radius);
            borderImg.raycastTarget = false;
            borderImg.color = color;

            LayoutElement layout = borderObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return borderImg;
        }

        public static Image EnsureChildTopGlow(Transform parent, int radius = 16)
        {
            Transform existing = parent.Find("UI_TopGlow");
            GameObject glowObj;
            if (existing != null)
            {
                glowObj = existing.gameObject;
            }
            else
            {
                glowObj = new GameObject("UI_TopGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                glowObj.transform.SetParent(parent, false);
            }

            RectTransform rect = glowObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = KaplaoUiSprites.TopGradientOverlay();
            glowImg.type = Image.Type.Sliced;
            glowImg.pixelsPerUnitMultiplier = 14f / Mathf.Max(4f, radius);
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 1f, 1f, 0.055f);

            LayoutElement layout = glowObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return glowImg;
        }

        public static Image EnsureChildShadow(Transform parent, int radius = 16, int spread = 28, Color? colorOpt = null)
        {
            Color color = colorOpt ?? KaplaoUiTheme.Instance.SoftShadowColor;
            Transform existing = parent.Find("UI_Shadow");
            GameObject shadowObj;
            if (existing != null)
            {
                shadowObj = existing.gameObject;
            }
            else
            {
                shadowObj = new GameObject("UI_Shadow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                shadowObj.transform.SetParent(parent, false);
                shadowObj.transform.SetAsFirstSibling();
            }

            int pad = 36;
            RectTransform rect = shadowObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-pad, -40f);
            rect.offsetMax = new Vector2(pad, 32f);

            Image shadowImg = shadowObj.GetComponent<Image>();
            shadowImg.sprite = KaplaoUiSprites.SoftShadow(16, 28);
            shadowImg.type = Image.Type.Sliced;
            shadowImg.raycastTarget = false;
            shadowImg.color = color;

            LayoutElement layout = shadowObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return shadowImg;
        }

        public static Image EnsureChildButtonGlow(Transform button, int radius = 10)
        {
            if (button != null)
            {
                Transform old = button.Find("UI_BtnGlow");
                if (old != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(old.gameObject);
                    else UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
            }
            return null;
        }

        public static void StyleHierarchy(Transform root, bool forceReplaceSprites = true)
        {
            if (root == null) return;

            ScrollRect[] scrolls = root.GetComponentsInChildren<ScrollRect>(true);
            for (int i = 0; i < scrolls.Length; i++)
            {
                if (scrolls[i] != null) StyleScrollView(scrolls[i]);
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn == null) continue;
                KaplaoButtonVariant variant = DetermineButtonVariant(btn.gameObject);
                StyleButton(btn, variant, forceReplaceSprites);
            }

            TMP_InputField[] inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] != null) StyleInputField(inputs[i], forceReplaceSprites);
            }

            TMP_Dropdown[] dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                if (dropdowns[i] != null) StyleDropdown(dropdowns[i], forceReplaceSprites);
            }

            Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                if (sliders[i] != null) StyleSlider(sliders[i]);
            }

            Toggle[] toggles = root.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle t = toggles[i];
                if (t == null) continue;
                if (t.name.StartsWith("Skin", StringComparison.OrdinalIgnoreCase) ||
                    (t.transform.parent != null && t.transform.parent.name.StartsWith("Skin", StringComparison.OrdinalIgnoreCase)))
                    continue;
                StyleToggleAsSwitch(t);
            }
        }

        public static void ApplyRuntimePolish(Transform root)
        {
            StyleHierarchy(root, forceReplaceSprites: true);
        }
    }
}
