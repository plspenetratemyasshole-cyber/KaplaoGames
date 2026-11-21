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
        public static bool IsProtectedArtImage(Image img)
        {
            if (img == null) return false;
            if (img is RawImage) return true;

            string name = img.gameObject.name;
            string parentName = img.transform.parent != null ? img.transform.parent.name : string.Empty;

            // 1. Skin previews
            if (name.StartsWith("SkinToggle", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("SkinRandomToggle", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Preview", StringComparison.OrdinalIgnoreCase) ||
                parentName.StartsWith("SkinToggle", StringComparison.OrdinalIgnoreCase) ||
                parentName.Equals("SkinRow", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Color picker palette
            if (name.Contains("SvImage") || name.Contains("HueImage") ||
                name.Contains("SvPicker") || name.Contains("HuePicker") ||
                name.StartsWith("Swatch_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 3. File/folder/tree icons, arrows, cursors, block previews
            if (name.Equals("Icon", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Twist", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Arrow", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("FileTree") || name.Contains("Cursor") ||
                name.Equals("TexturePreview", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("BlockPreview", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 4. Mobile joystick
            if (name.Contains("MobileJoystick") || parentName.Contains("MobileJoystick"))
            {
                return true;
            }

            return false;
        }

        public static bool CanSafelyReplaceSprite(Image image, bool forceReplaceSprite = true)
        {
            if (image == null) return false;
            if (IsProtectedArtImage(image)) return false;
            if (forceReplaceSprite) return true;

            Sprite currentSprite = image.sprite;
            if (currentSprite == null) return true;

            string name = currentSprite.name;
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
                   name == "soft_shadow" ||
                   name == "top_glow" ||
                   name == "circle" ||
                   name == "chevron_down";
        }

        public static bool CanSafelyReplaceSprite(Sprite currentSprite)
        {
            if (currentSprite == null)
                return true;

            string name = currentSprite.name;
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
                   name == "soft_shadow" ||
                   name == "top_glow" ||
                   name == "circle" ||
                   name == "chevron_down";
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
            }
            image.color = theme.SurfacePrimary;

            // Border
            EnsureChildBorder(panel.transform, 16, 2, theme.BorderSubtle);

            // Top Glow
            if (withHeaderGlow)
            {
                EnsureChildTopGlow(panel.transform);
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

        public static void StyleButton(Button button, KaplaoButtonVariant variant = KaplaoButtonVariant.Secondary, bool forceReplaceSprite = true)
        {
            if (button == null)
                return;

            button.transition = Selectable.Transition.None;

            KaplaoUiTheme theme = KaplaoUiTheme.Instance;
            var (normal, hover, press, border) = theme.GetButtonColors(variant);

            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                if (CanSafelyReplaceSprite(image, forceReplaceSprite))
                {
                    image.sprite = variant == KaplaoButtonVariant.Ghost || variant == KaplaoButtonVariant.Icon
                        ? KaplaoUiSprites.RoundedRect(8)
                        : KaplaoUiSprites.RoundedRect(10);
                    image.type = Image.Type.Sliced;
                }

                image.color = normal;
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

            if (variant != KaplaoButtonVariant.Ghost && variant != KaplaoButtonVariant.Icon)
            {
                EnsureChildBorder(button.transform, 10, 2, border);
            }

            if (variant == KaplaoButtonVariant.Primary || variant == KaplaoButtonVariant.Success || variant == KaplaoButtonVariant.Danger)
            {
                EnsureChildButtonGlow(button.transform);
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = theme.TextPrimary;
                label.fontStyle |= FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;

                Shadow shadow = label.GetComponent<Shadow>();
                if (shadow == null)
                    shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }

            UiButtonLeanScale scale = button.GetComponent<UiButtonLeanScale>();
            if (scale == null)
                scale = button.gameObject.AddComponent<UiButtonLeanScale>();
            scale.SetBaseColors(normal, border);

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
                Image bgImg = bg.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.sprite = KaplaoUiSprites.Pill();
                    bgImg.type = Image.Type.Sliced;
                    bgImg.color = new Color(0x1E / 255f, 0x29 / 255f, 0x3B / 255f, 0.95f);
                }
            }

            Transform fill = slider.transform.Find("Fill Area/Fill");
            if (fill != null)
            {
                Image fillImg = fill.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.sprite = KaplaoUiSprites.Pill();
                    fillImg.type = Image.Type.Sliced;
                    fillImg.color = theme.AccentCyan;
                }
            }

            Transform handle = slider.transform.Find("Handle Slide Area/Handle");
            if (handle != null)
            {
                Image handleImg = handle.GetComponent<Image>();
                if (handleImg != null)
                {
                    handleImg.sprite = KaplaoUiSprites.Circle();
                    handleImg.type = Image.Type.Simple;
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

            Transform bg = toggle.transform.Find("Background");
            if (bg == null)
                return;

            Image bgImg = bg.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = KaplaoUiSprites.Pill();
                bgImg.type = Image.Type.Sliced;
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
                    checkImg.color = Color.white;
                }

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
                    vpImg.raycastTarget = false;

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

            if (name.Equals("closebutton", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("cancelbutton", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("×") || text.Equals("x", StringComparison.OrdinalIgnoreCase))
            {
                return KaplaoButtonVariant.Ghost;
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
            borderImg.raycastTarget = false;
            borderImg.color = color;

            LayoutElement layout = borderObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return borderImg;
        }

        public static Image EnsureChildTopGlow(Transform parent)
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
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 64f);

            Image glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = KaplaoUiSprites.TopGradientOverlay();
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;
            glowImg.color = Color.white;

            LayoutElement layout = glowObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return glowImg;
        }

        public static Image EnsureChildShadow(Transform parent, int radius, int spread, Color color)
        {
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

            int pad = radius + spread;
            RectTransform rect = shadowObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-pad, -pad);
            rect.offsetMax = new Vector2(pad, pad);

            Image shadowImg = shadowObj.GetComponent<Image>();
            shadowImg.sprite = KaplaoUiSprites.SoftShadow(radius, spread);
            shadowImg.type = Image.Type.Sliced;
            shadowImg.raycastTarget = false;
            shadowImg.color = color;

            LayoutElement layout = shadowObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return shadowImg;
        }

        public static Image EnsureChildButtonGlow(Transform button)
        {
            Transform existing = button.Find("UI_BtnGlow");
            GameObject glowObj;
            if (existing != null)
            {
                glowObj = existing.gameObject;
            }
            else
            {
                glowObj = new GameObject("UI_BtnGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                glowObj.transform.SetParent(button, false);
            }

            RectTransform rect = glowObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = KaplaoUiSprites.TopGradientOverlay();
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 1f, 1f, 0.35f);

            LayoutElement layout = glowObj.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            return glowImg;
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
