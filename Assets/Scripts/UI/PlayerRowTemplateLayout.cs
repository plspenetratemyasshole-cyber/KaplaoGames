using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Ensures pause player row template has a working HorizontalLayoutGroup setup.
    /// </summary>
    public static class PlayerRowTemplateLayout
    {
        public static void Ensure(GameObject row, bool rebuildLayout = true)
        {
            if (row == null)
                return;

            EnsureRowRect(row);
            EnsureRowLayoutElement(row);
            EnsureHorizontalLayoutGroup(row);
            EnsureChild(row.transform, "PlayerName", flexibleWidth: 1f, minWidth: 80f, isNameLabel: true);
            EnsureChild(row.transform, "KaplaoButton", preferredWidth: 96f);
            EnsureChild(row.transform, "ModButton", preferredWidth: 84f);
            EnsureChild(row.transform, "KickButton", preferredWidth: 64f);
            EnsureChild(row.transform, "BanButton", preferredWidth: 64f);
            EnsureChild(row.transform, "BanIpButton", preferredWidth: 72f);

            if (rebuildLayout)
                Rebuild(row.transform);
        }

        public static void Rebuild(Transform rowOrContent)
        {
            if (rowOrContent is not RectTransform rect)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            Transform parent = rowOrContent.parent;
            if (parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        static void EnsureRowRect(GameObject row)
        {
            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect == null)
                return;

            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 36f);
            rowRect.anchoredPosition = Vector2.zero;
        }

        static void EnsureRowLayoutElement(GameObject row)
        {
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null)
                rowLayout = row.gameObject.AddComponent<LayoutElement>();

            rowLayout.minHeight = 36f;
            rowLayout.preferredHeight = 36f;
            rowLayout.flexibleWidth = 1f;
        }

        static void EnsureHorizontalLayoutGroup(GameObject row)
        {
            HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
                hlg = row.AddComponent<HorizontalLayoutGroup>();

            hlg.spacing = 4f;
            hlg.padding = new RectOffset(4, 4, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
        }

        static void EnsureChild(
            Transform row,
            string childName,
            float preferredWidth = -1f,
            float minWidth = -1f,
            float flexibleWidth = 0f,
            bool isNameLabel = false)
        {
            Transform child = row.Find(childName);
            if (child == null)
            {
                if (isNameLabel)
                    child = CreateNameLabel(row, childName, flexibleWidth, minWidth).transform;
                else
                    child = CreateRowButton(row, childName, childName.Replace("Button", string.Empty), preferredWidth).transform;
            }

            ResetChildForLayoutGroup(child, preferredWidth, minWidth, flexibleWidth);

            if (isNameLabel)
            {
                TextMeshProUGUI label = child.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.enableAutoSizing = false;
                    label.fontSize = 15f;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    if (string.IsNullOrWhiteSpace(label.text))
                        label.text = "PlayerName";
                }
            }
        }

        static void ResetChildForLayoutGroup(
            Transform child,
            float preferredWidth = -1f,
            float minWidth = -1f,
            float flexibleWidth = 0f)
        {
            if (child is not RectTransform rect)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le == null)
                le = child.gameObject.AddComponent<LayoutElement>();

            le.minHeight = 28f;
            le.preferredHeight = 28f;

            if (preferredWidth >= 0f)
            {
                le.preferredWidth = preferredWidth;
                le.minWidth = minWidth >= 0f ? minWidth : preferredWidth;
                le.flexibleWidth = 0f;
            }
            else if (flexibleWidth > 0f)
            {
                le.flexibleWidth = flexibleWidth;
                le.minWidth = minWidth >= 0f ? minWidth : 80f;
            }
        }

        static TextMeshProUGUI CreateNameLabel(Transform parent, string name, float flexibleWidth, float minWidth)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minWidth = minWidth;
            le.flexibleWidth = flexibleWidth;
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = "PlayerName";
            label.fontSize = 15f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        static Button CreateRowButton(Transform parent, string name, string label, float preferredWidth)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.minWidth = preferredWidth;
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            StretchFull(textGo.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 13f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;

            return go.GetComponent<Button>();
        }

        static void StretchFull(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
