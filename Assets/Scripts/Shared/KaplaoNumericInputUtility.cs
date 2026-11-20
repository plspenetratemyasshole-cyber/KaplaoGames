using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Normalizes decimal input: display uses comma, parsing accepts dot or comma.
    /// </summary>
    public static class KaplaoNumericInputUtility
    {
        public static string NormalizeDecimalSeparator(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.Replace('.', ',');
        }

        public static bool TryParseFloat(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = NormalizeDecimalSeparator(text.Trim()).Replace(',', '.');
            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string FormatFloatField(float value)
        {
            return NormalizeDecimalSeparator(value.ToString("G", CultureInfo.InvariantCulture));
        }

        public static void NormalizeFieldText(TMP_InputField field)
        {
            if (field == null || string.IsNullOrEmpty(field.text))
                return;

            string normalized = NormalizeDecimalSeparator(field.text);
            if (normalized == field.text)
                return;

            field.SetTextWithoutNotify(normalized);
        }

        public static bool TryParseInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static void WireDecimalInput(TMP_InputField field)
        {
            if (field == null)
                return;

            field.onValueChanged.RemoveListener(OnDecimalInputChanged);
            field.onValueChanged.AddListener(OnDecimalInputChanged);
        }

        public static void UnwireDecimalInput(TMP_InputField field)
        {
            if (field == null)
                return;

            field.onValueChanged.RemoveListener(OnDecimalInputChanged);
        }

        static void OnDecimalInputChanged(string text)
        {
            // Handled by KaplaoDecimalInputNormalizer on the same GameObject.
        }
    }
}
