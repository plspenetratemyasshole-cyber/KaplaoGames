using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/World Entry View")]
    public class WorldEntryView : MonoBehaviour
    {
        public string WorldName;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Subtitle;
        public Image Background;
        public Image Border;

        public void SetSelected(bool selected)
        {
            if (Background != null)
            {
                Background.color = selected
                    ? new Color(0x1D / 255f, 0x3A / 255f, 0x5F / 255f, 0.98f) // #1D3A5F
                    : new Color(0x17 / 255f, 0x20 / 255f, 0x36 / 255f, 0.95f); // #172036
            }

            if (Border != null)
            {
                Border.color = selected
                    ? new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.95f) // #38BDF8
                    : new Color(0x2B / 255f, 0x3B / 255f, 0x5C / 255f, 0.55f); // #2B3B5C
            }
        }

        public static string FormatLastPlayed(long utcTicks)
        {
            if (utcTicks <= 0)
                return string.Empty;

            DateTime lastUtc = new DateTime(utcTicks, DateTimeKind.Utc);
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan span = nowUtc - lastUtc;
            if (span.TotalSeconds < 0)
                span = TimeSpan.Zero;

            if (span.TotalSeconds < 60)
                return "Just now";
            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours}h ago";

            DateTime lastLocal = lastUtc.ToLocalTime();
            DateTime todayLocal = DateTime.Now.Date;
            if (lastLocal.Date == todayLocal.AddDays(-1))
                return "Yesterday";

            return lastLocal.ToString("dd MMM yyyy, HH:mm");
        }
    }
}
