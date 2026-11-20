using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public enum SelectionFilterCriterion : byte
    {
        None = 0,
        Type = 1,
        Color = 2,
        Group = 3,
        Date = 4,
        Alpha = 5,
        Rotation = 6,
        Position = 7,
        Scale = 8,
        Order = 9
    }

    public enum SelectionCompareOp : byte
    {
        Less = 0,
        Greater = 1,
        LessOrEqual = 2,
        GreaterOrEqual = 3,
        Equal = 4,
        NotEqual = 5
    }

    [Serializable]
    public struct SelectionFilterDate
    {
        public int hours;
        public int minutes;
        public int days;
        public int months;
        public int years;
    }

    /// <summary>
    /// Create-mode selection filter. Default = everything selectable.
    /// </summary>
    [Serializable]
    public class CreateModeSelectionFilter
    {
        public SelectionFilterCriterion criterion = SelectionFilterCriterion.None;

        public bool allowBlock = true;
        public bool allowSpike = true;
        public bool allowTrigger = true;
        public bool allowParticle = true;
        public bool allowKeyframe = true;
        public bool allowExecutor = true;

        public List<string> allowedColorsHex = new();
        public List<int> allowedGroups = new();

        public SelectionCompareOp dateOp = SelectionCompareOp.Less;
        public SelectionFilterDate dateThreshold;

        public SelectionCompareOp numericOp = SelectionCompareOp.Equal;
        public float numericValue;
        public float numericValueX;
        public float numericValueY;

        public bool IsDefault =>
            criterion == SelectionFilterCriterion.None;

        public static CreateModeSelectionFilter CreateDefault() => new CreateModeSelectionFilter();

        public bool MatchesTile(
            TileType type,
            Color32 color,
            IReadOnlyList<int> groups,
            long createdAtUnixMs,
            float transparency,
            float rotationDegrees,
            Vector2 position,
            Vector2 scale,
            int sortingOrder)
        {
            if (IsDefault)
                return true;

            switch (criterion)
            {
                case SelectionFilterCriterion.Type:
                    return type switch
                    {
                        TileType.Block => allowBlock,
                        TileType.Spike => allowSpike,
                        TileType.Trigger => allowTrigger,
                        TileType.Particle => allowParticle,
                        TileType.Keyframe => allowKeyframe,
                        TileType.Executor => allowExecutor,
                        _ => false
                    };

                case SelectionFilterCriterion.Color:
                    if (allowedColorsHex == null || allowedColorsHex.Count == 0)
                        return true;
                    string hex = ColorUtility.ToHtmlStringRGB(new Color32(color.r, color.g, color.b, 255));
                    for (int i = 0; i < allowedColorsHex.Count; i++)
                    {
                        if (HexMatches(allowedColorsHex[i], hex))
                            return true;
                    }
                    return false;

                case SelectionFilterCriterion.Group:
                    if (allowedGroups == null || allowedGroups.Count == 0)
                        return true;
                    if (groups == null || groups.Count == 0)
                        return false;
                    for (int i = 0; i < allowedGroups.Count; i++)
                    {
                        for (int j = 0; j < groups.Count; j++)
                        {
                            if (groups[j] == allowedGroups[i])
                                return true;
                        }
                    }
                    return false;

                case SelectionFilterCriterion.Date:
                    return CompareDate(createdAtUnixMs, BuildThresholdUtc(dateThreshold), dateOp);

                case SelectionFilterCriterion.Alpha:
                    return CompareFloat(transparency, numericValue, numericOp);

                case SelectionFilterCriterion.Rotation:
                    return CompareFloat(rotationDegrees, numericValue, numericOp);

                case SelectionFilterCriterion.Position:
                    return CompareFloat(position.x, numericValueX, numericOp) &&
                           CompareFloat(position.y, numericValueY, numericOp);

                case SelectionFilterCriterion.Scale:
                    return CompareFloat(scale.x, numericValueX, numericOp) &&
                           CompareFloat(scale.y, numericValueY, numericOp);

                case SelectionFilterCriterion.Order:
                    return CompareFloat(sortingOrder, numericValue, numericOp);

                default:
                    return true;
            }
        }

        static bool HexMatches(string stored, string rgbHex)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return false;

            string a = NormalizeHex(stored);
            string b = NormalizeHex(rgbHex);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeHex(string hex)
        {
            string trimmed = hex.Trim().TrimStart('#');
            return trimmed.Length >= 6 ? trimmed.Substring(0, 6) : trimmed;
        }

        public static bool TryParseHexColor(string raw, out Color32 color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim().TrimStart('#');
            if (text.Length != 6 && text.Length != 8)
                return false;

            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                return false;

            if (text.Length == 6)
            {
                color = new Color32(
                    (byte)((value >> 16) & 0xFF),
                    (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF),
                    255);
                return true;
            }

            color = new Color32(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
            return true;
        }

        static DateTime BuildThresholdUtc(SelectionFilterDate date)
        {
            int year = Mathf.Clamp(date.years <= 0 ? DateTime.UtcNow.Year : date.years, 1, 9999);
            int month = Mathf.Clamp(date.months <= 0 ? 1 : date.months, 1, 12);
            int day = Mathf.Clamp(date.days <= 0 ? 1 : date.days, 1, DateTime.DaysInMonth(year, month));
            int hour = Mathf.Clamp(date.hours, 0, 23);
            int minute = Mathf.Clamp(date.minutes, 0, 59);
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }

        static bool CompareDate(long createdAtUnixMs, DateTime thresholdUtc, SelectionCompareOp op)
        {
            if (createdAtUnixMs <= 0)
                return op == SelectionCompareOp.NotEqual;

            var created = DateTimeOffset.FromUnixTimeMilliseconds(createdAtUnixMs).UtcDateTime;
            int cmp = created.CompareTo(thresholdUtc);
            return op switch
            {
                SelectionCompareOp.Less => cmp < 0,
                SelectionCompareOp.Greater => cmp > 0,
                SelectionCompareOp.LessOrEqual => cmp <= 0,
                SelectionCompareOp.GreaterOrEqual => cmp >= 0,
                SelectionCompareOp.Equal => cmp == 0,
                SelectionCompareOp.NotEqual => cmp != 0,
                _ => true
            };
        }

        static bool CompareFloat(float value, float threshold, SelectionCompareOp op)
        {
            const float epsilon = 0.0001f;
            return op switch
            {
                SelectionCompareOp.Less => value < threshold - epsilon,
                SelectionCompareOp.Greater => value > threshold + epsilon,
                SelectionCompareOp.LessOrEqual => value <= threshold + epsilon,
                SelectionCompareOp.GreaterOrEqual => value >= threshold - epsilon,
                SelectionCompareOp.Equal => Mathf.Abs(value - threshold) <= epsilon,
                SelectionCompareOp.NotEqual => Mathf.Abs(value - threshold) > epsilon,
                _ => true
            };
        }

        public int CountEnabledTypes()
        {
            int count = 0;
            if (allowBlock) count++;
            if (allowSpike) count++;
            if (allowTrigger) count++;
            if (allowParticle) count++;
            if (allowKeyframe) count++;
            if (allowExecutor) count++;
            return count;
        }
    }
}
