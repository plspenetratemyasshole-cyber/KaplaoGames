import re

# 1. LevelScriptSemanticAnalyzer.cs
with open('Assets/Scripts/LevelScript/LevelScriptSemanticAnalyzer.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_sem = '''                case PlaySoundNode playSound:
                    CheckFloat(playSound.X, linked, modulePath, scopes, source);'''

replacement_sem = '''                case PrintNode printNode:
                    for (int a = 0; a < printNode.Arguments.Count; a++)
                        CheckArgument(printNode.Arguments[a], linked, modulePath, scopes, source);
                    break;
                case PlaySoundNode playSound:
                    CheckFloat(playSound.X, linked, modulePath, scopes, source);'''

assert target_sem in text, "target_sem not found in LevelScriptSemanticAnalyzer.cs"
text = text.replace(target_sem, replacement_sem, 1)

with open('Assets/Scripts/LevelScript/LevelScriptSemanticAnalyzer.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptSemanticAnalyzer.cs")

# 2. LevelScriptRuntime.cs
with open('Assets/Scripts/LevelScript/LevelScriptRuntime.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_runtime_event = 'public event System.Action<string> ScriptError;'
replacement_runtime_event = 'public event System.Action<string> ScriptError;\n    public event System.Action<string> ScriptPrint;'

assert target_runtime_event in text, "target_runtime_event not found in LevelScriptRuntime.cs"
text = text.replace(target_runtime_event, replacement_runtime_event, 1)

target_runtime_node = '''        case DestroyNode destroy:
          ApplyDestroy(destroy.Targets.Evaluate(context));
          yield break;'''

replacement_runtime_node = '''        case PrintNode printNode:
          ExecutePrint(printNode, context);
          yield break;

        case DestroyNode destroy:
          ApplyDestroy(destroy.Targets.Evaluate(context));
          yield break;'''

assert target_runtime_node in text, "target_runtime_node not found in LevelScriptRuntime.cs"
text = text.replace(target_runtime_node, replacement_runtime_node, 1)

target_runtime_helper = '    static void ApplyDestroy(IReadOnlyList<ScriptGameObjectRef> objects)'
replacement_runtime_helper = '''    void ExecutePrint(PrintNode printNode, LevelScriptExecutionContext context)
    {
      string formatted = BuildPrintString(printNode, context);
      Debug.Log($"[LevelScript] {formatted}");
      ScriptPrint?.Invoke(formatted);
      if (LevelSessionServer.Instance != null)
        LevelSessionServer.Instance.ServerReportScriptPrint(formatted);
      else
        KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput(formatted, KaplaoGames.Client.OutputLogType.Info);
    }

    static string BuildPrintString(PrintNode printNode, LevelScriptExecutionContext context)
    {
      if (printNode.Arguments == null || printNode.Arguments.Count == 0)
        return string.Empty;

      var parts = new List<string>(printNode.Arguments.Count);
      for (int i = 0; i < printNode.Arguments.Count; i++)
      {
        ScriptValue val = ScriptNetworkEventUtility.EvaluateToScriptValue(printNode.Arguments[i], context);
        parts.Add(ScriptNetworkEventUtility.FormatScriptValue(val));
      }

      return string.Join(" ", parts);
    }

    void ApplyDestroy(List<ScriptGameObjectRef> targets)'''

assert target_runtime_helper in text, "target_runtime_helper not found in LevelScriptRuntime.cs"
text = text.replace(target_runtime_helper, replacement_runtime_helper, 1)

with open('Assets/Scripts/LevelScript/LevelScriptRuntime.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptRuntime.cs")

# 3. LevelScriptLocalRuntime.cs
with open('Assets/Scripts/LevelScript/LevelScriptLocalRuntime.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_local_node = '''                case IntDeclarationNode decl:
                    context.DeclareInt(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : 0);
                    yield break;'''

replacement_local_node = '''                case PrintNode printNode:
                    ExecutePrint(printNode, context);
                    yield break;

                case IntDeclarationNode decl:
                    context.DeclareInt(decl.Name, decl.Value != null ? decl.Value.Evaluate(context) : 0);
                    yield break;'''

assert target_local_node in text, "target_local_node not found in LevelScriptLocalRuntime.cs"
text = text.replace(target_local_node, replacement_local_node, 1)

target_local_helper = '        void ExecuteLocalMove(MoveNode move, LevelScriptExecutionContext context)'
replacement_local_helper = '''        void ExecutePrint(PrintNode printNode, LevelScriptExecutionContext context)
        {
            string formatted = BuildPrintString(printNode, context);
            Debug.Log($"[LevelScriptLocal] {formatted}");
            KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput(formatted, KaplaoGames.Client.OutputLogType.Info);
        }

        static string BuildPrintString(PrintNode printNode, LevelScriptExecutionContext context)
        {
            if (printNode.Arguments == null || printNode.Arguments.Count == 0)
                return string.Empty;

            var parts = new List<string>(printNode.Arguments.Count);
            for (int i = 0; i < printNode.Arguments.Count; i++)
            {
                ScriptValue val = ScriptNetworkEventUtility.EvaluateToScriptValue(printNode.Arguments[i], context);
                parts.Add(ScriptNetworkEventUtility.FormatScriptValue(val));
            }

            return string.Join(" ", parts);
        }

        void ExecuteLocalMove(MoveNode move, LevelScriptExecutionContext context)'''

assert target_local_helper in text, "target_local_helper not found in LevelScriptLocalRuntime.cs"
text = text.replace(target_local_helper, replacement_local_helper, 1)

with open('Assets/Scripts/LevelScript/LevelScriptLocalRuntime.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptLocalRuntime.cs")

# 4. LevelSessionServer.cs
with open('Assets/Scripts/Server/LevelSessionServer.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_session_event = 'public event Action<string> ScriptRuntimeError;'
replacement_session_event = 'public event Action<string> ScriptRuntimeError;\n        public event Action<string> ScriptPrint;\n        public event Action<string> ScriptWarning;'

assert target_session_event in text, "target_session_event not found in LevelSessionServer.cs"
text = text.replace(target_session_event, replacement_session_event, 1)

target_session_rpc = '''        [ClientRpc]
        void RpcScriptRuntimeError(string message)
        {
            ScriptRuntimeError?.Invoke(message);
            KaplaoCodeModeUi.Instance?.NotifyScriptRuntimeError(message);
        }'''

replacement_session_rpc = '''        [ClientRpc]
        void RpcScriptRuntimeError(string message)
        {
            ScriptRuntimeError?.Invoke(message);
            KaplaoCodeModeUi.Instance?.NotifyScriptRuntimeError(message);
        }

        [Server]
        public void ServerReportScriptPrint(string message)
        {
            RpcScriptPrint(message ?? string.Empty);
        }

        [ClientRpc]
        void RpcScriptPrint(string message)
        {
            ScriptPrint?.Invoke(message);
            KaplaoCodeModeUi.Instance?.AppendOutput(message, OutputLogType.Info);
        }

        [Server]
        public void ServerReportScriptWarning(string message)
        {
            RpcScriptWarning(message ?? string.Empty);
        }

        [ClientRpc]
        void RpcScriptWarning(string message)
        {
            ScriptWarning?.Invoke(message);
            KaplaoCodeModeUi.Instance?.NotifyScriptWarning(message);
        }'''

assert target_session_rpc in text, "target_session_rpc not found in LevelSessionServer.cs"
text = text.replace(target_session_rpc, replacement_session_rpc, 1)

with open('Assets/Scripts/Server/LevelSessionServer.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelSessionServer.cs")

# 5. LevelScriptIdeService.cs
with open('Assets/Scripts/LevelScript/LevelScriptIdeService.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_action_kw = '"playSound", "spawnPoint",'
replacement_action_kw = '"playSound", "spawnPoint", "print",'

assert target_action_kw in text, "target_action_kw not found in LevelScriptIdeService.cs"
text = text.replace(target_action_kw, replacement_action_kw, 1)

target_comp_builtins = '"playSound()", "spawnPoint()",'
replacement_comp_builtins = '"playSound()", "spawnPoint()", "print()",'

assert target_comp_builtins in text, "target_comp_builtins not found in LevelScriptIdeService.cs"
text = text.replace(target_comp_builtins, replacement_comp_builtins, 1)

target_builtin_sig = '["playsound"] = new[] { "x", "y", "name", "volume" },'
replacement_builtin_sig = '["print"] = new[] { "value", "..." },\n                ["playsound"] = new[] { "x", "y", "name", "volume" },'

assert target_builtin_sig in text, "target_builtin_sig not found in LevelScriptIdeService.cs"
text = text.replace(target_builtin_sig, replacement_builtin_sig, 1)

with open('Assets/Scripts/LevelScript/LevelScriptIdeService.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptIdeService.cs")
