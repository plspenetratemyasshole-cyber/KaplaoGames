using System;
using System.Collections;
using System.Collections.Generic;
using KaplaoGames.LevelScript;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Code mode IDE: syntax overlay, inline autocomplete ghost, live diagnostics, bracket helpers.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class LevelScriptCodeEditorEnhancer : MonoBehaviour
    {
        const int TabSize = 4;
        const float ValidateDelay = 0.15f;

        [SerializeField] int undoLimit = 128;

        TMP_InputField inputField;
        TMP_Text inputText;
        TMP_Text syntaxOverlay;
        readonly Stack<string> undoStack = new();
        readonly Dictionary<int, int> autoInsertedClosers = new();
        bool applyingChange;
        bool overlayReady;
        bool suppressInputAssists;
        Coroutine pasteSuppressRoutine;
        string lastText = string.Empty;
        string ghostSuffix = string.Empty;
        List<string> currentCandidates;
        int candidateIndex;

        int pendingCaret = -1;
        int lastCaret = -1;
        LevelScriptDiagnostic lastDiagnostic;
        Coroutine validateRoutine;
        string currentFilePath = string.Empty;

        public event Action<LevelScriptDiagnostic> DiagnosticChanged;
        public event Action<string> ParameterHintChanged;

        public LevelScriptDiagnostic LastDiagnostic => lastDiagnostic;

        public void ApplyExternalDiagnostic(LevelScriptDiagnostic diagnostic)
        {
            lastDiagnostic = diagnostic;
            DiagnosticChanged?.Invoke(lastDiagnostic);
            UpdateGhostCompletion();
            RefreshView();
        }

        public string CurrentFilePath
        {
            get => currentFilePath;
            set => currentFilePath = value ?? string.Empty;
        }

        public void RefreshEditorState()
        {
            if (!overlayReady)
                SetupSyntaxOverlay();

            LevelScriptIdeService.ActiveFilePath = currentFilePath;
            lastText = inputField != null ? inputField.text ?? string.Empty : string.Empty;
            lastCaret = inputField != null ? inputField.caretPosition : 0;
            UpdateGhostCompletion();
            ScheduleValidate();
            RefreshView();
        }

        void Awake()
        {
            inputField = GetComponent<TMP_InputField>();
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            SetupSyntaxOverlay();
        }

        void OnEnable()
        {
            if (inputField == null)
                return;

            inputField.onValueChanged.AddListener(OnTextChanged);
            inputField.onValidateInput += OnValidateInput;
            lastText = inputField.text ?? string.Empty;
            lastCaret = inputField.caretPosition;
            ScheduleValidate();
            RefreshView();
        }

        void OnDisable()
        {
            if (inputField == null)
                return;

            inputField.onValueChanged.RemoveListener(OnTextChanged);
            inputField.onValidateInput -= OnValidateInput;
            if (validateRoutine != null)
            {
                StopCoroutine(validateRoutine);
                validateRoutine = null;
            }

            if (pasteSuppressRoutine != null)
            {
                StopCoroutine(pasteSuppressRoutine);
                pasteSuppressRoutine = null;
            }

            suppressInputAssists = false;
        }

        void SetupSyntaxOverlay()
        {
            if (inputField == null)
                return;

            inputText = inputField.textComponent;
            if (inputText == null)
                return;

            Transform textAreaTransform = inputField.textViewport != null
                ? inputField.textViewport
                : inputText.rectTransform.parent;

            Transform existing = textAreaTransform.Find("SyntaxOverlay");
            if (existing != null)
                syntaxOverlay = existing.GetComponent<TextMeshProUGUI>();

            if (syntaxOverlay == null)
            {
                GameObject overlayObject = new GameObject("SyntaxOverlay", typeof(RectTransform));
                overlayObject.transform.SetParent(textAreaTransform, false);
                syntaxOverlay = overlayObject.AddComponent<TextMeshProUGUI>();
            }

            syntaxOverlay.transform.SetAsLastSibling();
            CopyTextStyle(inputText, syntaxOverlay);
            syntaxOverlay.richText = true;
            syntaxOverlay.raycastTarget = false;
            syntaxOverlay.enabled = true;
            StretchFull(syntaxOverlay.rectTransform);

            inputText.richText = false;
            ApplyTransparentInputText();
            inputField.customCaretColor = true;
            inputField.caretColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            inputField.caretWidth = 2;

            Transform oldHint = inputField.transform.Find("CodeAutocompleteHint");
            if (oldHint != null)
                oldHint.gameObject.SetActive(false);

            overlayReady = true;
        }

        static void CopyTextStyle(TMP_Text from, TMP_Text to)
        {
            to.font = from.font;
            to.fontSharedMaterial = from.fontSharedMaterial;
            to.fontSize = from.fontSize;
            to.fontStyle = from.fontStyle;
            to.alignment = from.alignment;
            to.textWrappingMode = from.textWrappingMode;
            to.margin = from.margin;
            to.enableWordWrapping = from.enableWordWrapping;
            to.overflowMode = from.overflowMode;
            to.extraPadding = from.extraPadding;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        void Update()
        {
            if (inputField == null)
                return;

            if (!inputField.isFocused)
            {
                if (ghostSuffix.Length > 0)
                {
                    ghostSuffix = string.Empty;
                    RefreshView();
                }
            }
            else
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null &&
                    (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                     keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed) &&
                    keyboard.vKey.wasPressedThisFrame)
                {
                    SuspendInputAssistsForPaste();
                }

                if (keyboard != null &&
                    (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) &&
                    keyboard.zKey.wasPressedThisFrame &&
                    !keyboard.leftShiftKey.isPressed &&
                    !keyboard.rightShiftKey.isPressed)
                {
                    UndoOnce();
                    return;
                }

                if (suppressInputAssists)
                {
                    if (pendingCaret >= 0)
                    {
                        SetCaret(pendingCaret);
                        pendingCaret = -1;
                    }

                    return;
                }

                if (keyboard != null && !string.IsNullOrEmpty(ghostSuffix))
                {
                    if (keyboard.downArrowKey.wasPressedThisFrame)
                    {
                        CycleCandidate(1);
                        return;
                    }

                    if (keyboard.upArrowKey.wasPressedThisFrame)
                    {
                        CycleCandidate(-1);
                        return;
                    }

                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        AcceptGhostCompletion();
                        return;
                    }
                }

                if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                {
                    if (!string.IsNullOrEmpty(ghostSuffix))
                    {
                        AcceptGhostCompletion();
                        return;
                    }

                    if (TryExpandSnippet())
                        return;

                    InsertAtCaret(new string(' ', TabSize));
                }

                if (pendingCaret >= 0)
                {
                    SetCaret(pendingCaret);
                    pendingCaret = -1;
                }

                if (inputField.caretPosition != lastCaret)
                {
                    lastCaret = inputField.caretPosition;
                    UpdateGhostCompletion();
                    RefreshView();
                }
            }
        }

        void SuspendInputAssistsForPaste()
        {
            suppressInputAssists = true;
            ghostSuffix = string.Empty;
            autoInsertedClosers.Clear();
            if (pasteSuppressRoutine != null)
                StopCoroutine(pasteSuppressRoutine);
            pasteSuppressRoutine = StartCoroutine(EndPasteSuppressSoon());
        }

        IEnumerator EndPasteSuppressSoon()
        {
            // Paste applies across the current + next frames in TMP.
            yield return null;
            yield return null;
            yield return null;
            suppressInputAssists = false;
            pasteSuppressRoutine = null;
            if (inputField != null)
            {
                lastText = inputField.text ?? string.Empty;
                lastCaret = inputField.caretPosition;
                UpdateGhostCompletion();
                RefreshView();
                ScheduleValidate();
            }
        }

        void LateUpdate()
        {
            if (syntaxOverlay == null || inputText == null)
                return;

            ApplyTransparentInputText();
            CopyTextStyle(inputText, syntaxOverlay);
            RectTransform overlayRect = syntaxOverlay.rectTransform;
            RectTransform inputRect = inputText.rectTransform;
            overlayRect.anchorMin = inputRect.anchorMin;
            overlayRect.anchorMax = inputRect.anchorMax;
            overlayRect.pivot = inputRect.pivot;
            overlayRect.anchoredPosition = inputRect.anchoredPosition;
            overlayRect.sizeDelta = inputRect.sizeDelta;
            overlayRect.offsetMin = inputRect.offsetMin;
            overlayRect.offsetMax = inputRect.offsetMax;
            syntaxOverlay.margin = inputText.margin;
        }

        void ApplyTransparentInputText()
        {
            if (inputText == null)
                return;

            Color color = inputText.color;
            if (color.a > 0.001f)
                inputText.color = new Color(color.r, color.g, color.b, 0f);
        }

        char OnValidateInput(string text, int charIndex, char addedChar)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                 keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed) &&
                keyboard.vKey.isPressed)
            {
                SuspendInputAssistsForPaste();
            }

            if (suppressInputAssists)
                return addedChar;

            if (addedChar == '\t')
                return '\0';

            if (IsClosingBracket(addedChar) && charIndex < text.Length && text[charIndex] == addedChar)
            {
                if (ShouldSkipOverExistingCloser(text, charIndex, addedChar))
                {
                    pendingCaret = charIndex + 1;
                    return '\0';
                }

                return addedChar;
            }

            return addedChar;
        }

        bool ShouldSkipOverExistingCloser(string text, int closeIndex, char closingChar)
        {
            if (!autoInsertedClosers.TryGetValue(closeIndex, out int openIndex))
                return true;

            int innerStart = openIndex + 1;
            if (HasUnclosedNestedBrackets(text, innerStart, closeIndex, closingChar))
                return false;

            autoInsertedClosers.Remove(closeIndex);
            return true;
        }

        static bool HasUnclosedNestedBrackets(string text, int start, int end, char closingChar)
        {
            char openingChar = closingChar switch
            {
                ')' => '(',
                '}' => '{',
                ']' => '[',
                '"' => '"',
                _ => '\0'
            };

            if (openingChar == '\0' || start > end)
                return false;

            int depth = 0;
            bool inString = false;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '"' && !IsEscapedInEditor(text, i))
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == openingChar)
                    depth++;
                else if (c == closingChar)
                    depth = Math.Max(0, depth - 1);
            }

            return depth > 0;
        }

        static bool IsEscapedInEditor(string text, int index)
        {
            int slashes = 0;
            for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
                slashes++;
            return (slashes & 1) == 1;
        }

        static bool IsClosingBracket(char c) => c is ')' or '}' or ']' or '"';

        void OnTextChanged(string text)
        {
            text ??= string.Empty;

            if (applyingChange)
            {
                lastText = text;
                UpdateGhostCompletion();
                RefreshView();
                ScheduleValidate();
                return;
            }

            // Bulk insert (paste) or multi-delete: never run bracket/indent assists.
            int lengthDelta = text.Length - lastText.Length;
            if (suppressInputAssists || lengthDelta > 1 || lengthDelta < -1)
            {
                if (lengthDelta > 1 || lengthDelta < -1)
                    SuspendInputAssistsForPaste();

                if (text != lastText)
                {
                    undoStack.Push(lastText);
                    while (undoStack.Count > undoLimit)
                        undoStack.Pop();
                }

                autoInsertedClosers.Clear();
                ghostSuffix = string.Empty;
                lastText = inputField.text ?? string.Empty;
                lastCaret = inputField.caretPosition;
                RefreshView();
                ScheduleValidate();
                return;
            }

            if (text.Length == lastText.Length + 1)
            {
                int insertIndex = FindSingleInsertionIndex(lastText, text);
                if (insertIndex >= 0)
                {
                    ShiftAutoInsertedClosers(insertIndex, 1);
                    TryHandleInsertedChar(text, insertIndex, text[insertIndex]);
                }
            }
            else if (text.Length == lastText.Length - 1)
            {
                int deleteIndex = FindSingleDeletionIndex(lastText, text);
                if (deleteIndex >= 0)
                {
                    RemoveAutoClosersInRange(deleteIndex, 1);
                    ShiftAutoInsertedClosers(deleteIndex + 1, -1);
                }

                undoStack.Push(lastText);
                while (undoStack.Count > undoLimit)
                    undoStack.Pop();
            }
            else if (text != lastText)
            {
                undoStack.Push(lastText);
                while (undoStack.Count > undoLimit)
                    undoStack.Pop();
                autoInsertedClosers.Clear();
            }

            lastText = inputField.text ?? string.Empty;
            lastCaret = inputField.caretPosition;
            UpdateGhostCompletion();
            RefreshView();
            ScheduleValidate();
        }

        void ScheduleValidate()
        {
            if (validateRoutine != null)
                StopCoroutine(validateRoutine);
            validateRoutine = StartCoroutine(ValidateSoon());
        }

        IEnumerator ValidateSoon()
        {
            yield return new WaitForSecondsRealtime(ValidateDelay);
            LevelScriptIdeService.ActiveFilePath = currentFilePath;
            string source = inputField != null ? inputField.text ?? string.Empty : string.Empty;
            if (LevelScriptIdeService.TryValidate(source, out LevelScriptDiagnostic diagnostic))
                diagnostic = LevelScriptDiagnostic.None;

            if (diagnostic.HasError != lastDiagnostic.HasError ||
                diagnostic.StartIndex != lastDiagnostic.StartIndex ||
                diagnostic.EndIndex != lastDiagnostic.EndIndex ||
                diagnostic.Message != lastDiagnostic.Message)
            {
                lastDiagnostic = diagnostic;
                DiagnosticChanged?.Invoke(lastDiagnostic);
            }

            RefreshView();
            validateRoutine = null;
        }

        void RefreshView()
        {
            if (syntaxOverlay == null || inputField == null)
                return;

            LevelScriptIdeService.ActiveFilePath = currentFilePath;
            string source = inputField.text ?? string.Empty;
            int caret = inputField.caretPosition;
            syntaxOverlay.text = LevelScriptIdeService.BuildOverlayRichText(
                source,
                caret,
                ghostSuffix,
                lastDiagnostic);
            syntaxOverlay.ForceMeshUpdate();
            UpdateSignatureHelp(source, caret);
        }

        void UpdateSignatureHelp(string source, int caret)
        {
            if (inputField == null || !inputField.gameObject.activeInHierarchy)
            {
                ParameterHintChanged?.Invoke(null);
                return;
            }

            if (lastDiagnostic.HasError)
            {
                ParameterHintChanged?.Invoke(null);
                return;
            }

            if (LevelScriptIdeService.TryGetActiveSignatureHelp(source, caret, out string signature))
                ParameterHintChanged?.Invoke(signature);
            else
                ParameterHintChanged?.Invoke(null);
        }

        static int FindSingleDeletionIndex(string oldText, string newText)
        {
            int prefix = 0;
            while (prefix < oldText.Length && prefix < newText.Length && oldText[prefix] == newText[prefix])
                prefix++;

            int oldSuffix = oldText.Length;
            int newSuffix = newText.Length;
            while (oldSuffix > prefix && newSuffix > prefix &&
                   oldText[oldSuffix - 1] == newText[newSuffix - 1])
            {
                oldSuffix--;
                newSuffix--;
            }

            return oldSuffix - prefix == 1 ? prefix : -1;
        }

        void ShiftAutoInsertedClosers(int fromIndex, int delta)
        {
            if (delta == 0 || autoInsertedClosers.Count == 0)
                return;

            var updated = new Dictionary<int, int>(autoInsertedClosers.Count);
            foreach (KeyValuePair<int, int> entry in autoInsertedClosers)
            {
                int closeIndex = entry.Key;
                int openIndex = entry.Value;
                int newClose = closeIndex >= fromIndex ? closeIndex + delta : closeIndex;
                int newOpen = openIndex >= fromIndex ? openIndex + delta : openIndex;
                updated[newClose] = newOpen;
            }

            autoInsertedClosers.Clear();
            foreach (KeyValuePair<int, int> entry in updated)
                autoInsertedClosers[entry.Key] = entry.Value;
        }

        void RemoveAutoClosersInRange(int start, int count)
        {
            if (count <= 0 || autoInsertedClosers.Count == 0)
                return;

            var toRemove = new List<int>();
            foreach (KeyValuePair<int, int> entry in autoInsertedClosers)
            {
                if (entry.Key >= start && entry.Key < start + count)
                    toRemove.Add(entry.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                autoInsertedClosers.Remove(toRemove[i]);
        }

        static int FindSingleInsertionIndex(string oldText, string newText)
        {
            int prefix = 0;
            while (prefix < oldText.Length && prefix < newText.Length && oldText[prefix] == newText[prefix])
                prefix++;

            int oldSuffix = oldText.Length;
            int newSuffix = newText.Length;
            while (oldSuffix > prefix && newSuffix > prefix &&
                   oldText[oldSuffix - 1] == newText[newSuffix - 1])
            {
                oldSuffix--;
                newSuffix--;
            }

            return newSuffix - prefix == 1 ? prefix : -1;
        }

        void TryHandleInsertedChar(string text, int insertIndex, char inserted)
        {
            undoStack.Push(lastText);
            while (undoStack.Count > undoLimit)
                undoStack.Pop();

            switch (inserted)
            {
                case '{':
                    InsertClosingPair(insertIndex, "}");
                    break;
                case '(':
                    InsertClosingPair(insertIndex, ")");
                    break;
                case '[':
                    InsertClosingPair(insertIndex, "]");
                    break;
                case '"':
                    if (insertIndex + 1 < text.Length && text[insertIndex + 1] == '"')
                        SetCaret(insertIndex + 2);
                    else
                        InsertClosingPair(insertIndex, "\"");
                    break;
                case '\n':
                    AutoIndentNewLine(insertIndex, text);
                    break;
            }
        }

        void InsertClosingPair(int openIndex, string close)
        {
            string current = inputField.text ?? string.Empty;
            if (openIndex + 1 < current.Length && current[openIndex + 1] == close[0])
            {
                SetCaret(openIndex + 2);
                return;
            }

            applyingChange = true;
            inputField.text = current.Insert(openIndex + 1, close);
            int closeIndex = openIndex + 1;
            autoInsertedClosers[closeIndex] = openIndex;
            SetCaret(openIndex + 1);
            applyingChange = false;
            lastText = inputField.text;
        }

        void AutoIndentNewLine(int insertIndex, string text)
        {
            int lineStart = text.LastIndexOf('\n', Mathf.Max(0, insertIndex - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int indent = 0;
            while (lineStart + indent < text.Length && text[lineStart + indent] == ' ')
                indent++;

            bool extra = insertIndex > 0 && text[insertIndex - 1] == '{';
            string spaces = new string(' ', indent + (extra ? TabSize : 0));

            applyingChange = true;
            inputField.text = text.Insert(insertIndex + 1, spaces);
            int caret = insertIndex + 1 + spaces.Length;
            SetCaret(caret);

            if (extra)
            {
                int closeIndex = caret;
                if (closeIndex < inputField.text.Length && inputField.text[closeIndex] == '}')
                {
                    inputField.text = inputField.text.Insert(closeIndex, "\n" + new string(' ', indent));
                    SetCaret(caret);
                }
            }

            applyingChange = false;
            lastText = inputField.text;
        }

        void InsertAtCaret(string value)
        {
            int caret = inputField.caretPosition;
            string text = inputField.text ?? string.Empty;
            applyingChange = true;
            inputField.text = text.Insert(caret, value);
            SetCaret(caret + value.Length);
            applyingChange = false;
            lastText = inputField.text;
        }

        void AcceptGhostCompletion()
        {
            if (string.IsNullOrEmpty(ghostSuffix))
                return;

            InsertAtCaret(ghostSuffix);
            ghostSuffix = string.Empty;
            RefreshView();
        }

        void UndoOnce()
        {
            if (undoStack.Count == 0)
                return;

            applyingChange = true;
            inputField.text = undoStack.Pop();
            autoInsertedClosers.Clear();
            SetCaret(inputField.text.Length);
            applyingChange = false;
            lastText = inputField.text;
            UpdateGhostCompletion();
            RefreshView();
            ScheduleValidate();
        }

        void SetCaret(int position)
        {
            inputField.caretPosition = position;
            inputField.selectionAnchorPosition = position;
            inputField.selectionFocusPosition = position;
            inputField.ForceLabelUpdate();
            lastCaret = position;
        }

        void UpdateGhostCompletion()
        {
            ghostSuffix = string.Empty;
            if (inputField == null || !inputField.isFocused)
            {
                currentCandidates = null;
                candidateIndex = 0;
                return;
            }

            LevelScriptIdeService.ActiveFilePath = currentFilePath;
            string text = inputField.text ?? string.Empty;
            int caret = inputField.caretPosition;
            if (!LevelScriptIdeService.TryGetCompletion(text, caret, candidateIndex, out ghostSuffix, out currentCandidates))
            {
                candidateIndex = 0;
                return;
            }

            ghostSuffix = LevelScriptIdeService.NormalizeGhostSuffix(text, caret, ghostSuffix);
        }

        void CycleCandidate(int delta)
        {
            if (currentCandidates == null || currentCandidates.Count <= 1 || inputField == null)
                return;

            candidateIndex = (candidateIndex + delta + currentCandidates.Count) % currentCandidates.Count;
            string chosen = currentCandidates[candidateIndex];
            string prefix = LevelScriptIdeService.GetWordPrefix(inputField.text ?? string.Empty, inputField.caretPosition);
            string insert = chosen.Contains('(') ? chosen.Substring(0, chosen.IndexOf('(')) : chosen;
            if (insert.Length > prefix.Length)
                ghostSuffix = insert.Substring(prefix.Length);
            else
                ghostSuffix = chosen.Length > prefix.Length ? chosen.Substring(prefix.Length) : string.Empty;

            ghostSuffix = LevelScriptIdeService.NormalizeGhostSuffix(inputField.text ?? string.Empty, inputField.caretPosition, ghostSuffix);
            RefreshView();
        }

        bool TryExpandSnippet()
        {
            if (inputField == null) return false;
            int caret = inputField.caretPosition;
            string text = inputField.text ?? string.Empty;
            string word = LevelScriptIdeService.GetWordPrefix(text, caret);
            if (string.IsNullOrEmpty(word)) return false;

            string snippet = null;
            int offsetFromStart = 0;
            switch (word.ToLowerInvariant())
            {
                case "for":
                    snippet = @"for (int i = 0; i < 10; i++)
{
    
}";
                    offsetFromStart = snippet.IndexOf('\n') + 5;
                    break;
                case "while":
                    snippet = @"while (true)
{
    
}";
                    offsetFromStart = snippet.IndexOf('\n') + 5;
                    break;
                case "spawn":
                    snippet = @"spawn
{
    
}";
                    offsetFromStart = snippet.IndexOf('\n') + 5;
                    break;
                case "trigger":
                    snippet = @"Group(1).OnTriggerEnter(GameObject player)
{
    
}";
                    offsetFromStart = snippet.IndexOf('\n') + 5;
                    break;
                case "onclientevent":
                    snippet = @"onClientEvent(""eventName"", GameObject player)
{
    
}";
                    offsetFromStart = snippet.IndexOf('\n') + 5;
                    break;
                case "onserverevent":
                    snippet = @"onServerEvent(""eventName"")
{
    
}";
                    offsetFromStart = snippet.IndexOf('\n') + 5;
                    break;
            }

            if (snippet != null)
            {
                int wordStart = caret - word.Length;
                applyingChange = true;
                inputField.text = text.Substring(0, wordStart) + snippet + text.Substring(caret);
                SetCaret(wordStart + offsetFromStart);
                applyingChange = false;
                lastText = inputField.text;
                UpdateGhostCompletion();
                RefreshView();
                return true;
            }

            return false;
        }
    }
}
