using KaplaoGames.Shared;
using TMPro;
using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Attach to any TMP_InputField used for float numbers — replaces '.' with ',' while typing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public class KaplaoDecimalInputNormalizer : MonoBehaviour
    {
        TMP_InputField field;
        bool suppress;

        void Awake()
        {
            field = GetComponent<TMP_InputField>();
            field.onValidateInput += ValidateChar;
            field.onValueChanged.AddListener(OnValueChanged);
            field.onEndEdit.AddListener(OnEndEdit);
        }

        void OnDestroy()
        {
            if (field == null)
                return;

            field.onValidateInput -= ValidateChar;
            field.onValueChanged.RemoveListener(OnValueChanged);
            field.onEndEdit.RemoveListener(OnEndEdit);
        }

        static char ValidateChar(string text, int charIndex, char addedChar)
        {
            if (addedChar == '.')
                return ',';

            return addedChar;
        }

        void OnValueChanged(string text)
        {
            ApplyNormalization(text);
        }

        void OnEndEdit(string text)
        {
            ApplyNormalization(text);
        }

        void ApplyNormalization(string text)
        {
            if (suppress || string.IsNullOrEmpty(text) || text.IndexOf('.') < 0)
                return;

            string normalized = KaplaoNumericInputUtility.NormalizeDecimalSeparator(text);
            if (normalized == text)
                return;

            suppress = true;
            int caret = field.caretPosition;
            field.SetTextWithoutNotify(normalized);
            field.caretPosition = Mathf.Min(caret, normalized.Length);
            suppress = false;
        }

        public static void EnsureOn(TMP_InputField inputField)
        {
            if (inputField == null)
                return;

            if (inputField.GetComponent<KaplaoDecimalInputNormalizer>() == null)
                inputField.gameObject.AddComponent<KaplaoDecimalInputNormalizer>();
        }
    }
}
