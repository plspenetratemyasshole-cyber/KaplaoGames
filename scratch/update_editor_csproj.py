with open('Assembly-CSharp-Editor.csproj', 'r', encoding='utf-8') as f:
    text = f.read()

target = '<Compile Include="Assets\\Scripts\\Editor\\CodeModeIdeSetup.cs" />'
replacement = '<Compile Include="Assets\\Scripts\\Editor\\CodeModeIdeSetup.cs" />\n    <Compile Include="Assets\\Scripts\\Editor\\CodeOutputPanelSetup.cs" />'

assert target in text, "Target not found in Assembly-CSharp-Editor.csproj"
text = text.replace(target, replacement, 1)

with open('Assembly-CSharp-Editor.csproj', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated Assembly-CSharp-Editor.csproj")
