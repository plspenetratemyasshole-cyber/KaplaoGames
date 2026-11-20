with open('Assets/Scripts/LevelScript/LevelScriptNodes.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Update ScriptArgument and add PrintNode
target_arg = '''    public sealed class ScriptArgument
    {
        public ScriptValueKind Kind;
        public IntExpression IntValue;
        public FloatExpression FloatValue;
        public BoolExpression BoolValue;
        public StringExpression StringValue;
        public GameObjectExpression GameObjectValue;
        public string IdentifierName;
        public int SourceIndex = -1;
    }'''

replacement_arg = '''    public sealed class ScriptArgument
    {
        public ScriptValueKind Kind;
        public IntExpression IntValue;
        public FloatExpression FloatValue;
        public BoolExpression BoolValue;
        public StringExpression StringValue;
        public GameObjectExpression GameObjectValue;
        public string IdentifierName;
        public IntExpression IndexExpr;
        public int SourceIndex = -1;
    }

    public sealed class PrintNode : LevelScriptNode
    {
        public readonly List<ScriptArgument> Arguments = new();
    }'''

assert target_arg in text, "target_arg not found in LevelScriptNodes.cs"
text = text.replace(target_arg, replacement_arg, 1)

# 2. Update EvaluateToScriptValue and add FormatScriptValue in ScriptNetworkEventUtility
target_eval = '''        public static ScriptValue EvaluateToScriptValue(ScriptArgument arg, LevelScriptExecutionContext context)
        {
            if (arg == null) return ScriptValue.FromInt(0);
            switch (arg.Kind)
            {
                case ScriptValueKind.Int: return ScriptValue.FromInt(arg.IntValue != null ? arg.IntValue.Evaluate(context) : 0);
                case ScriptValueKind.Float: return ScriptValue.FromFloat(arg.FloatValue != null ? arg.FloatValue.Evaluate(context) : 0f);
                case ScriptValueKind.Bool: return ScriptValue.FromBool(arg.BoolValue != null && arg.BoolValue.Evaluate(context));
                case ScriptValueKind.String: return ScriptValue.FromString(arg.StringValue != null ? arg.StringValue.Evaluate(context) : string.Empty);
                case ScriptValueKind.GameObject: return ScriptValue.FromGameObject(arg.GameObjectValue != null ? arg.GameObjectValue.Evaluate(context) : ScriptGameObjectRef.Null);
                default:
                    if (!string.IsNullOrEmpty(arg.IdentifierName) && context.TryGetValue(arg.IdentifierName, out ScriptValue val))
                        return val;
                    return ScriptValue.FromInt(0);
            }
        }'''

replacement_eval = '''        public static ScriptValue EvaluateToScriptValue(ScriptArgument arg, LevelScriptExecutionContext context)
        {
            if (arg == null) return ScriptValue.FromInt(0);
            switch (arg.Kind)
            {
                case ScriptValueKind.Int: return ScriptValue.FromInt(arg.IntValue != null ? arg.IntValue.Evaluate(context) : 0);
                case ScriptValueKind.Float: return ScriptValue.FromFloat(arg.FloatValue != null ? arg.FloatValue.Evaluate(context) : 0f);
                case ScriptValueKind.Bool: return ScriptValue.FromBool(arg.BoolValue != null && arg.BoolValue.Evaluate(context));
                case ScriptValueKind.String: return ScriptValue.FromString(arg.StringValue != null ? arg.StringValue.Evaluate(context) : string.Empty);
                case ScriptValueKind.GameObject: return ScriptValue.FromGameObject(arg.GameObjectValue != null ? arg.GameObjectValue.Evaluate(context) : ScriptGameObjectRef.Null);
                default:
                    if (!string.IsNullOrEmpty(arg.IdentifierName))
                    {
                        if (context.TryGetValue(arg.IdentifierName, out ScriptValue val))
                        {
                            if (arg.IndexExpr != null && val.Kind == ScriptValueKind.List && val.ListValue != null)
                            {
                                int idx = arg.IndexExpr.Evaluate(context);
                                if (idx >= 0 && idx < val.ListValue.Count)
                                    return val.ListValue[idx];
                                return ScriptValue.FromInt(0);
                            }
                            return val;
                        }
                    }
                    return ScriptValue.FromInt(0);
            }
        }

        public static string FormatScriptValue(ScriptValue val)
        {
            switch (val.Kind)
            {
                case ScriptValueKind.Int:
                    return val.IntValue.ToString();
                case ScriptValueKind.Float:
                    return val.FloatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case ScriptValueKind.Bool:
                    return val.BoolValue ? "true" : "false";
                case ScriptValueKind.String:
                    return val.StringValue ?? string.Empty;
                case ScriptValueKind.GameObject:
                    return val.GameObjectValue.ToString();
                case ScriptValueKind.Component:
                    return val.ComponentValue.ToString();
                case ScriptValueKind.List:
                    if (val.ListValue == null) return "[]";
                    var items = new List<string>(val.ListValue.Count);
                    for (int i = 0; i < val.ListValue.Count; i++)
                        items.Add(FormatScriptValue(val.ListValue[i]));
                    return "[" + string.Join(", ", items) + "]";
                default:
                    return string.Empty;
            }
        }'''

assert target_eval in text, "target_eval not found in LevelScriptNodes.cs"
text = text.replace(target_eval, replacement_eval, 1)

with open('Assets/Scripts/LevelScript/LevelScriptNodes.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptNodes.cs")
