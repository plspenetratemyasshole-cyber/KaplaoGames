with open('Assembly-CSharp.csproj', 'r', encoding='utf-8') as f:
    text = f.read()

target = '<Compile Include="Assets\\Scripts\\Client\\KaplaoCodeModeUi.cs" />'
replacement = '<Compile Include="Assets\\Scripts\\Client\\KaplaoCodeModeUi.cs" />\n    <Compile Include="Assets\\Scripts\\Client\\KaplaoCodeOutputPanel.cs" />'

assert target in text, "Target not found in Assembly-CSharp.csproj"
text = text.replace(target, replacement, 1)

with open('Assembly-CSharp.csproj', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated Assembly-CSharp.csproj")
