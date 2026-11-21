using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI
{
    /// <summary>
    /// View component for individual world entries in the Single Player world list.
    /// Manages card selection neon glow, accent bar, and double-click to launch.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/World Entry View")]
    public class WorldEntryView : MonoBehaviour, IPointerClickHandler
    {
        public string WorldName;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Subtitle;
        public Image Background;
        public Image Border;
        public Image SelectionAccentBar;
        public UiButtonLeanScale ButtonJuice;

        public Action<string> OnDoubleClicked;

        float lastClickTime;
        bool isSelected;

        static readonly Color NormalBg = new Color(0x17 / 255f, 0x20 / 255f, 0x36 / 255f, 0.95f); // #172036
        static readonly Color SelectedBg = new Color(0x1E / 255f, 0x3A / 255f, 0x5F / 255f, 0.98f); // #1E3A5F
        static readonly Color NormalBorder = new Color(0x2B / 255f, 0x3B / 255f, 0x5C / 255f, 0.55f); // #2B3B5C
        static readonly Color SelectedBorder = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.95f); // #38BDF8 Neon Cyan

        void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (Background == null)
                Background = GetComponent<Image>();

            if (Border == null)
            {
                Transform b = transform.Find("UI_Border");
                if (b != null) Border = b.GetComponent<Image>();
            }

            if (SelectionAccentBar == null)
            {
                Transform a = transform.Find("SelectionAccentBar");
                if (a != null) SelectionAccentBar = a.GetComponent<Image>();
            }

            if (ButtonJuice == null)
                ButtonJuice = GetComponent<UiButtonLeanScale>();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            EnsureComponents();

            Color targetBg = selected ? SelectedBg : NormalBg;
            Color targetBorder = selected ? SelectedBorder : NormalBorder;

            if (Background != null)
                Background.color = targetBg;

            if (Border != null)
                Border.color = targetBorder;

            if (SelectionAccentBar != null)
            {
                SelectionAccentBar.gameObject.SetActive(selected);
                SelectionAccentBar.color = SelectedBorder;
            }

            if (ButtonJuice != null)
            {
                ButtonJuice.SetBaseColors(targetBg, targetBorder);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - lastClickTime < 0.35f && isSelected)
            {
                lastClickTime = 0f;
                OnDoubleClicked?.Invoke(WorldName);
            }
            else
            {
                lastClickTime = now;
            }
        }

        public static string FormatLastPlayed(long utcTicks)
        {
            if (utcTicks <= 0)
                return "Not visited yet";

            DateTime lastUtc = new DateTime(utcTicks, DateTimeKind.Utc);
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan span = nowUtc - lastUtc;
            if (span.TotalSeconds < 0)
                span = TimeSpan.Zero;

            if (span.TotalSeconds < 60)
                return "Last played: Just now";
            if (span.TotalMinutes < 60)
                return $"Last played: {(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24)
                return $"Last played: {(int)span.TotalHours}h ago";

            DateTime lastLocal = lastUtc.ToLocalTime();
            DateTime todayLocal = DateTime.Now.Date;
            if (lastLocal.Date == todayLocal.AddDays(-1))
                return "Last played: Yesterday";

            return $"Last played: {lastLocal:dd MMM yyyy, HH:mm}";
        }
    }
}
