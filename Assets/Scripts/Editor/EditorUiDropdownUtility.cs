#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    /// <summary>
    /// Builds working TMP_Dropdown hierarchies (caption, template, items).
    /// </summary>
    public static class EditorUiDropdownUtility
    {
        public static TMP_Dropdown EnsureDropdown(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            IList<string> options)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_Dropdown dropdown = existing.GetComponent<TMP_Dropdown>();
                if (dropdown != null && IsValid(dropdown))
                {
                    ApplyLayout(existing.GetComponent<RectTransform>(), anchoredPosition, size);
                    SetOptions(dropdown, options);
                    return dropdown;
                }

                Object.DestroyImmediate(existing.gameObject);
            }

            return CreateDropdown(parent, name, anchoredPosition, size, options);
        }

        public static void RepairAllInHierarchy(Transform root)
        {
            if (root == null)
                return;

            TMP_Dropdown[] dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                TMP_Dropdown dropdown = dropdowns[i];
                if (dropdown == null || IsValid(dropdown))
                    continue;

                Transform parent = dropdown.transform.parent;
                string name = dropdown.gameObject.name;
                RectTransform rect = dropdown.GetComponent<RectTransform>();
                Vector2 pos = rect != null ? rect.anchoredPosition : Vector2.zero;
                Vector2 size = rect != null ? rect.sizeDelta : new Vector2(220f, 36f);
                List<string> options = CopyOptions(dropdown);
                Object.DestroyImmediate(dropdown.gameObject);
                CreateDropdown(parent, name, pos, size, options);
            }
        }

        static bool IsValid(TMP_Dropdown dropdown) =>
            dropdown.template != null &&
            dropdown.captionText != null &&
            dropdown.itemText != null;

        static List<string> CopyOptions(TMP_Dropdown dropdown)
        {
            var options = new List<string>();
            if (dropdown.options != null)
            {
                for (int i = 0; i < dropdown.options.Count; i++)
                    options.Add(dropdown.options[i].text);
            }

            return options;
        }

        static void ApplyLayout(RectTransform rect, Vector2 pos, Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void SetOptions(TMP_Dropdown dropdown, IList<string> options)
        {
            if (options == null || options.Count == 0)
                return;

            int previous = dropdown.value;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.value = Mathf.Clamp(previous, 0, options.Count - 1);
            dropdown.RefreshShownValue();
        }

        static TMP_Dropdown CreateDropdown(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            IList<string> options)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            root.transform.SetParent(parent, false);
            ApplyLayout(root.GetComponent<RectTransform>(), anchoredPosition, size);

            Image rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

            TextMeshProUGUI caption = CreateCaption(root.transform);
            RectTransform templateRect = CreateTemplate(root.transform, out TextMeshProUGUI itemText);

            TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = rootImage;
            dropdown.captionText = caption;
            dropdown.itemText = itemText;
            dropdown.template = templateRect;
            templateRect.gameObject.SetActive(false);

            SetOptions(dropdown, options);
            return dropdown;
        }

        static TextMeshProUGUI CreateCaption(Transform parent)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            Stretch(labelObject.GetComponent<RectTransform>(), 12f, 8f, 28f, 8f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform CreateTemplate(Transform parent, out TextMeshProUGUI itemText)
        {
            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(parent, false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 150f);
            template.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 0f, 0f, 0f, 0f);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 36f);

            GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 36f);

            GameObject itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBg.transform.SetParent(item.transform, false);
            Stretch(itemBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            itemBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject itemCheck = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            itemCheck.transform.SetParent(item.transform, false);
            RectTransform checkRect = itemCheck.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = new Vector2(14f, 0f);
            checkRect.sizeDelta = new Vector2(14f, 14f);
            itemCheck.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.45f, 1f);

            GameObject itemLabel = new GameObject("Item Label", typeof(RectTransform));
            itemLabel.transform.SetParent(item.transform, false);
            Stretch(itemLabel.GetComponent<RectTransform>(), 28f, 4f, 8f, 4f);
            itemText = itemLabel.AddComponent<TextMeshProUGUI>();
            itemText.fontSize = 16f;
            itemText.color = Color.white;
            itemText.alignment = TextAlignmentOptions.Left;
            itemText.raycastTarget = false;

            Toggle itemToggle = item.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBg.GetComponent<Image>();
            itemToggle.graphic = itemCheck.GetComponent<Image>();
            itemToggle.isOn = true;

            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            return templateRect;
        }

        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
#endif
