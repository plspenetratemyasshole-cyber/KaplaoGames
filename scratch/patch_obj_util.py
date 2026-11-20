import re

# 1. LevelScriptObjectUtility.cs
with open('Assets/Scripts/LevelScript/LevelScriptObjectUtility.cs', 'r', encoding='utf-8') as f:
    text = f.read()

if 'public override string ToString() => IsNull ? "null" : $"{Kind}#{NetId}";' not in text:
    old_target = 'public bool IsNull => NetId == 0;'
    new_target = 'public bool IsNull => NetId == 0;\n\n        public override string ToString() => IsNull ? "null" : $"{Kind}#{NetId}";'
    assert old_target in text, "Could not find IsNull in LevelScriptObjectUtility.cs"
    text = text.replace(old_target, new_target, 1)

if 'public override string ToString() => GameObject.IsNull ? "null" : $"{GameObject}.{ComponentName}";' not in text:
    old_target2 = 'public bool IsNull => GameObject.IsNull || string.IsNullOrEmpty(ComponentName);'
    new_target2 = 'public bool IsNull => GameObject.IsNull || string.IsNullOrEmpty(ComponentName);\n\n        public override string ToString() => GameObject.IsNull ? "null" : $"{GameObject}.{ComponentName}";'
    assert old_target2 in text, "Could not find Component IsNull in LevelScriptObjectUtility.cs"
    text = text.replace(old_target2, new_target2, 1)

with open('Assets/Scripts/LevelScript/LevelScriptObjectUtility.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptObjectUtility.cs")
