using System.Collections.Generic;
using KaplaoGames.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Shared KCP/TCP transport dropdown helpers for menus.
    /// </summary>
    public static class TransportModeUi
    {
        public const string DropdownObjectName = "TransportDropdown";

        public static void ConfigureDropdown(TMP_Dropdown dropdown, bool interactable = true)
        {
            if (dropdown == null)
                return;

            dropdown.onValueChanged.RemoveAllListeners();
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>
            {
                "KCP (UDP)",
                "TCP"
            });
            dropdown.value = PlayerSession.UseTcpTransport ? 1 : 0;
            dropdown.RefreshShownValue();
            dropdown.interactable = interactable;
        }

        public static void WireDropdown(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> onChanged)
        {
            if (dropdown == null || onChanged == null)
                return;

            dropdown.onValueChanged.RemoveListener(onChanged);
            dropdown.onValueChanged.AddListener(onChanged);
        }

        public static void SyncDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
                return;

            int expected = PlayerSession.UseTcpTransport ? 1 : 0;
            if (dropdown.value == expected)
            {
                dropdown.RefreshShownValue();
                return;
            }

            dropdown.SetValueWithoutNotify(expected);
            dropdown.RefreshShownValue();
        }

        public static void SetTransportLabel(TextMeshProUGUI label)
        {
            if (label == null)
                return;

            string mode = PlayerSession.UseTcpTransport ? "TCP" : "KCP (UDP)";
            label.text = $"Transport: {mode}\n(change in Main Menu before hosting)";
        }

        public static TMP_Dropdown EnsureDropdown(
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            string objectName = DropdownObjectName)
        {
            if (parent == null)
                return null;

            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                TMP_Dropdown existingDropdown = existing.GetComponent<TMP_Dropdown>();
                if (existingDropdown != null)
                    return existingDropdown;
            }

            GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = size;

            Image rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(root.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, 12f, 8f, 28f, 8f);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;

            GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowObject.transform.SetParent(root.transform, false);
            RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            arrowRect.sizeDelta = new Vector2(18f, 18f);
            Image arrowImage = arrowObject.GetComponent<Image>();
            arrowImage.color = new Color(1f, 1f, 1f, 0.7f);
            arrowImage.raycastTarget = false;

            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(root.transform, false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 90f);
            Image templateImage = template.GetComponent<Image>();
            templateImage.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            template.SetActive(false);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 0f, 0f, 0f, 0f);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = Color.white;
            Mask mask = viewport.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 45f);

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
            RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
            Stretch(itemLabelRect, 28f, 4f, 8f, 4f);
            TextMeshProUGUI itemText = itemLabel.AddComponent<TextMeshProUGUI>();
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

            TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = rootImage;
            dropdown.captionText = label;
            dropdown.itemText = itemText;
            dropdown.template = templateRect;
            return dropdown;
        }

        public static TextMeshProUGUI EnsureLabel(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
        {
            if (parent == null)
                return null;

            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                TextMeshProUGUI existingLabel = existing.GetComponent<TextMeshProUGUI>();
                if (existingLabel != null)
                    return existingLabel;
            }

            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 16f;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
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
