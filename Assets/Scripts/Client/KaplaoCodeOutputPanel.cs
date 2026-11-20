using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    public enum OutputLogType
    {
        Info,
        Warning,
        Error
    }

    public static class LevelScriptOutputService
    {
        public static event Action<string, OutputLogType> LogAppended;

        public static void Log(string message, OutputLogType type)
        {
            LogAppended?.Invoke(message, type);
        }

        public static void LogInfo(string message) => Log(message, OutputLogType.Info);
        public static void LogWarning(string message) => Log(message, OutputLogType.Warning);
        public static void LogError(string message) => Log(message, OutputLogType.Error);
    }

    /// <summary>
    /// Rich multiline output console for LevelScript IDE (prints, warnings, errors, copy/clear/collapse).
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoCodeOutputPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] RectTransform panelRoot;
        [SerializeField] TMP_InputField outputField;
        [SerializeField] TextMeshProUGUI titleLabel;
        [SerializeField] Button copyButton;
        [SerializeField] Button clearButton;
        [SerializeField] Button toggleButton;
        [SerializeField] TextMeshProUGUI copyButtonLabel;

        [Header("Settings")]
        [SerializeField] int maxEntries = 500;
        [SerializeField] float expandedHeight = 150f;
        [SerializeField] float collapsedHeight = 26f;

        public struct LogEntry
        {
            public DateTime Timestamp;
            public OutputLogType Type;
            public string Message;
        }

        readonly List<LogEntry> entries = new List<LogEntry>();
        readonly StringBuilder displayBuilder = new StringBuilder();
        readonly StringBuilder plainTextBuilder = new StringBuilder();

        bool isCollapsed;
        Coroutine copyFeedbackRoutine;
        string originalCopyButtonText = "Copy";

        public bool IsCollapsed => isCollapsed;
        public float ExpandedHeight => expandedHeight;
        public float CollapsedHeight => collapsedHeight;
        public float CurrentHeight => isCollapsed ? collapsedHeight : expandedHeight;

        void Awake()
        {
            if (panelRoot == null)
                panelRoot = GetComponent<RectTransform>();

            if (copyButtonLabel == null && copyButton != null)
                copyButtonLabel = copyButton.GetComponentInChildren<TextMeshProUGUI>();

            if (copyButtonLabel != null)
                originalCopyButtonText = copyButtonLabel.text;

            ConfigureOutputField();
        }

        void OnEnable()
        {
            if (copyButton != null)
                copyButton.onClick.AddListener(CopyToClipboard);

            if (clearButton != null)
                clearButton.onClick.AddListener(Clear);

            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleCollapsed);

            LevelScriptOutputService.LogAppended -= OnExternalLogAppended;
            LevelScriptOutputService.LogAppended += OnExternalLogAppended;
        }

        void OnDisable()
        {
            if (copyButton != null)
                copyButton.onClick.RemoveListener(CopyToClipboard);

            if (clearButton != null)
                clearButton.onClick.RemoveListener(Clear);

            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(ToggleCollapsed);

            if (copyFeedbackRoutine != null)
            {
                StopCoroutine(copyFeedbackRoutine);
                copyFeedbackRoutine = null;
            }
            if (copyButtonLabel != null && !string.IsNullOrEmpty(originalCopyButtonText))
                copyButtonLabel.text = originalCopyButtonText;

            LevelScriptOutputService.LogAppended -= OnExternalLogAppended;
        }

        void ConfigureOutputField()
        {
            if (outputField == null)
                return;

            outputField.readOnly = true;
            outputField.richText = true;
            outputField.customCaretColor = true;
            outputField.caretColor = Color.clear;
            outputField.selectionColor = new Color(0.25f, 0.5f, 0.95f, 0.4f);
            outputField.lineType = TMP_InputField.LineType.MultiLineNewline;

            if (outputField.textComponent != null)
            {
                outputField.textComponent.richText = true;
                outputField.textComponent.fontSize = 13.5f;
                outputField.textComponent.textWrappingMode = TextWrappingModes.Normal;
                outputField.textComponent.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        void OnExternalLogAppended(string message, OutputLogType type)
        {
            AppendEntry(message, type);
        }

        public void AppendEntry(string message, OutputLogType type)
        {
            if (string.IsNullOrEmpty(message))
                return;

            DateTime now = DateTime.Now;
            string timeStr = now.ToString("HH:mm:ss");

            var entry = new LogEntry
            {
                Timestamp = now,
                Type = type,
                Message = message
            };

            entries.Add(entry);
            if (entries.Count > maxEntries)
            {
                entries.RemoveAt(0);
                RebuildAllText();
            }
            else
            {
                AppendEntryText(timeStr, type, message);
            }

            // Auto-expand if an error occurred while collapsed
            if (type == OutputLogType.Error && isCollapsed)
            {
                SetCollapsed(false);
            }

            UpdateDisplay();
        }

        void AppendEntryText(string timeStr, OutputLogType type, string message)
        {
            switch (type)
            {
                case OutputLogType.Error:
                    plainTextBuilder.AppendLine($"[{timeStr}] [Error] {message}");
                    displayBuilder.AppendLine($"<color=#FF5555>[{timeStr}] [Error] {EscapeRichText(message)}</color>");
                    break;
                case OutputLogType.Warning:
                    plainTextBuilder.AppendLine($"[{timeStr}] [Warning] {message}");
                    displayBuilder.AppendLine($"<color=#FFD700>[{timeStr}] [Warning] {EscapeRichText(message)}</color>");
                    break;
                case OutputLogType.Info:
                default:
                    plainTextBuilder.AppendLine($"[{timeStr}] {message}");
                    displayBuilder.AppendLine($"<color=#FFFFFF>[{timeStr}] {EscapeRichText(message)}</color>");
                    break;
            }
        }

        void RebuildAllText()
        {
            plainTextBuilder.Clear();
            displayBuilder.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                string timeStr = e.Timestamp.ToString("HH:mm:ss");
                AppendEntryText(timeStr, e.Type, e.Message);
            }
        }

        void UpdateDisplay()
        {
            if (outputField != null)
            {
                outputField.text = displayBuilder.ToString();
                outputField.stringPosition = outputField.text.Length;
                outputField.caretPosition = outputField.text.Length;
            }

            UpdateTitle();
        }

        void UpdateTitle()
        {
            if (titleLabel == null)
                return;

            int count = entries.Count;
            titleLabel.text = count > 0 ? $"OUTPUT ({count})" : "OUTPUT";
        }

        public void Clear()
        {
            entries.Clear();
            plainTextBuilder.Clear();
            displayBuilder.Clear();

            if (outputField != null)
                outputField.text = string.Empty;

            UpdateTitle();
        }

        public void CopyToClipboard()
        {
            string textToCopy = plainTextBuilder.ToString();
            if (string.IsNullOrEmpty(textToCopy))
                textToCopy = displayBuilder.ToString();

            if (string.IsNullOrEmpty(textToCopy))
                return;

            GUIUtility.systemCopyBuffer = textToCopy;

            if (copyButtonLabel != null)
            {
                if (copyFeedbackRoutine != null)
                    StopCoroutine(copyFeedbackRoutine);
                copyFeedbackRoutine = StartCoroutine(ShowCopiedFeedbackRoutine());
            }
        }

        IEnumerator ShowCopiedFeedbackRoutine()
        {
            copyButtonLabel.text = "Copied!";
            yield return new WaitForSecondsRealtime(1.2f);
            copyButtonLabel.text = originalCopyButtonText;
            copyFeedbackRoutine = null;
        }

        public void ToggleCollapsed()
        {
            SetCollapsed(!isCollapsed);
        }

        public void SetCollapsed(bool collapsed)
        {
            isCollapsed = collapsed;
            float targetHeight = isCollapsed ? collapsedHeight : expandedHeight;

            if (panelRoot != null)
            {
                Vector2 min = panelRoot.offsetMin;
                panelRoot.offsetMax = new Vector2(panelRoot.offsetMax.x, min.y + targetHeight);
            }

            Transform content = transform.Find("ContentArea");
            if (content != null)
                content.gameObject.SetActive(!isCollapsed);

            if (toggleButton != null)
            {
                TextMeshProUGUI label = toggleButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = isCollapsed ? "▲" : "▼";
            }

            KaplaoCodeModeUi.Instance?.OnOutputPanelToggled(isCollapsed, targetHeight);
        }

        static string EscapeRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            // Escape '<' and '>' if user prints HTML tags so they don't break TMP layout
            return text.Replace("<", "＜").Replace(">", "＞");
        }
    }
}
