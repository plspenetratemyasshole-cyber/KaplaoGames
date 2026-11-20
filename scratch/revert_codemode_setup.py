with open('Assets/Scripts/Editor/CodeModeIdeSetup.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Remove outputPanel instantiation from CodeModeIdeSetup
t1 = '''            // --- Output Panel ---
            KaplaoCodeOutputPanel outputPanel = FindOrCreateOutputPanel(overlay.transform);

            Button runButton = FindOrCreateButton(overlay.transform, "RunScriptButton", "Run",'''
r1 = '''            Button runButton = FindOrCreateButton(overlay.transform, "RunScriptButton", "Run",'''
assert t1 in text, "t1 not found"
text = text.replace(t1, r1, 1)

# 2. Remove outputPanel from so
t2 = '''            so.FindProperty("deleteConfirmMessage").objectReferenceValue = deleteMessage;
            so.FindProperty("outputPanel").objectReferenceValue = outputPanel;
            so.ApplyModifiedPropertiesWithoutUndo();'''
r2 = '''            so.FindProperty("deleteConfirmMessage").objectReferenceValue = deleteMessage;
            so.ApplyModifiedPropertiesWithoutUndo();'''
assert t2 in text, "t2 not found"
text = text.replace(t2, r2, 1)

# 3. Remove FindOrCreateOutputPanel method
start_idx = text.find('        static KaplaoCodeOutputPanel FindOrCreateOutputPanel(Transform parent)')
end_idx = text.find('        static TMP_InputField FindOrCreateSingleLineInput(Transform parent, string name)')
assert start_idx != -1 and end_idx != -1, "FindOrCreateOutputPanel bounds not found"
text = text[:start_idx] + text[end_idx:]

with open('Assets/Scripts/Editor/CodeModeIdeSetup.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Reverted CodeModeIdeSetup.cs to original state")
