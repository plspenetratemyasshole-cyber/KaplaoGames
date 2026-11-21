using System;
using System.Collections.Generic;

namespace KaplaoGames.LevelScript
{
    public enum ScriptValueKind : byte
    {
        None = 0,
        Int = 1,
        Bool = 2,
        Float = 3,
        String = 4,
        GameObject = 5,
        Component = 6,
        List = 7
    }

    public enum TriggerActorMode : byte
    {
        Runners,
        SourceInternal
    }

    public enum PlayerMember : byte
    {
        X,
        Y
    }

    public sealed class ScriptPlayerBinding
    {
        public uint NetId;
        public string Name;
        public float X;
        public float Y;
    }

    /// <summary>
    /// Unified dynamic value used by the LevelScript runtime for variables, arguments, and lists.
    /// Eliminates multiple separate scope collections and avoids scope divergence.
    /// </summary>
    public struct ScriptValue : IEquatable<ScriptValue>
    {
        public ScriptValueKind Kind;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
        public string StringValue;
        public ScriptGameObjectRef GameObjectValue;
        public ScriptComponentRef ComponentValue;
        public List<ScriptValue> ListValue;

        public static ScriptValue FromInt(int value) => new() { Kind = ScriptValueKind.Int, IntValue = value };
        public static ScriptValue FromFloat(float value) => new() { Kind = ScriptValueKind.Float, FloatValue = value };
        public static ScriptValue FromBool(bool value) => new() { Kind = ScriptValueKind.Bool, BoolValue = value };
        public static ScriptValue FromString(string value) => new() { Kind = ScriptValueKind.String, StringValue = value ?? string.Empty };
        public static ScriptValue FromGameObject(ScriptGameObjectRef value) => new() { Kind = ScriptValueKind.GameObject, GameObjectValue = value };
        public static ScriptValue FromComponent(ScriptComponentRef value) => new() { Kind = ScriptValueKind.Component, ComponentValue = value };
        public static ScriptValue FromList(List<ScriptValue> value) => new() { Kind = ScriptValueKind.List, ListValue = value ?? new List<ScriptValue>() };

        public float AsFloat() => Kind == ScriptValueKind.Float ? FloatValue : (Kind == ScriptValueKind.Int ? (float)IntValue : 0f);
        public int AsInt() => Kind == ScriptValueKind.Int ? IntValue : (Kind == ScriptValueKind.Float ? (int)FloatValue : 0);

        public override string ToString()
        {
            switch (Kind)
            {
                case ScriptValueKind.Int: return IntValue.ToString();
                case ScriptValueKind.Float: return FloatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case ScriptValueKind.Bool: return BoolValue ? "true" : "false";
                case ScriptValueKind.String: return StringValue ?? string.Empty;
                case ScriptValueKind.GameObject: return GameObjectValue.ToString();
                case ScriptValueKind.Component: return ComponentValue.ToString();
                case ScriptValueKind.List: return $"List[{ListValue?.Count ?? 0}]";
                default: return "null";
            }
        }

        public bool Equals(ScriptValue other)
        {
            if (Kind != other.Kind)
            {
                if ((Kind == ScriptValueKind.Int || Kind == ScriptValueKind.Float) && (other.Kind == ScriptValueKind.Int || other.Kind == ScriptValueKind.Float))
                    return Math.Abs(AsFloat() - other.AsFloat()) < 0.00001f;
                return false;
            }
            switch (Kind)
            {
                case ScriptValueKind.Int: return IntValue == other.IntValue;
                case ScriptValueKind.Float: return Math.Abs(FloatValue - other.FloatValue) < 0.00001f;
                case ScriptValueKind.Bool: return BoolValue == other.BoolValue;
                case ScriptValueKind.String: return string.Equals(StringValue, other.StringValue, StringComparison.Ordinal);
                case ScriptValueKind.GameObject: return GameObjectValue.Equals(other.GameObjectValue);
                case ScriptValueKind.Component: return ComponentValue.Equals(other.ComponentValue);
                case ScriptValueKind.List: return ReferenceEquals(ListValue, other.ListValue);
                default: return true;
            }
        }

        public override bool Equals(object obj) => obj is ScriptValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, IntValue, FloatValue, StringValue);
        public static bool operator ==(ScriptValue left, ScriptValue right) => left.Equals(right);
        public static bool operator !=(ScriptValue left, ScriptValue right) => !left.Equals(right);
    }

    /// <summary>
    /// Program-level variables declared in the script main body (shared across triggers/spawns).
    /// </summary>
    public sealed class LevelScriptProgramGlobals
    {
        readonly object syncRoot = new();
        readonly Dictionary<string, ScriptValue> values = new(StringComparer.OrdinalIgnoreCase);

        public void Clear()
        {
            lock (syncRoot)
                values.Clear();
        }

        public bool Contains(string name)
        {
            lock (syncRoot)
                return values.ContainsKey(name);
        }

        public void Declare(string name, ScriptValue val)
        {
            lock (syncRoot)
                values[name] = val;
        }

        public void SetValue(string name, ScriptValue val)
        {
            lock (syncRoot)
            {
                if (values.TryGetValue(name, out ScriptValue existing) && existing.Kind == ScriptValueKind.Float && val.Kind == ScriptValueKind.Int)
                    val = ScriptValue.FromFloat(val.IntValue);
                values[name] = val;
            }
        }

        public bool TryGetValue(string name, out ScriptValue val)
        {
            lock (syncRoot)
                return values.TryGetValue(name, out val);
        }

        public bool TryGetKind(string name, out ScriptValueKind kind)
        {
            lock (syncRoot)
            {
                if (values.TryGetValue(name, out ScriptValue val))
                {
                    kind = val.Kind;
                    return true;
                }
                kind = default;
                return false;
            }
        }

        public void DeclareInt(string name, int value) => Declare(name, ScriptValue.FromInt(value));
        public void DeclareBool(string name, bool value) => Declare(name, ScriptValue.FromBool(value));
        public void DeclareFloat(string name, float value) => Declare(name, ScriptValue.FromFloat(value));
        public void DeclareString(string name, string value) => Declare(name, ScriptValue.FromString(value));
        public void DeclareGameObject(string name, ScriptGameObjectRef value) => Declare(name, ScriptValue.FromGameObject(value));
        public void DeclareComponent(string name, ScriptComponentRef value) => Declare(name, ScriptValue.FromComponent(value));
        public void DeclareList(string name, List<ScriptValue> value) => Declare(name, ScriptValue.FromList(value));

        public bool TryGetInt(string name, out int value)
        {
            if (TryGetValue(name, out var v) && (v.Kind == ScriptValueKind.Int || v.Kind == ScriptValueKind.Float)) { value = v.AsInt(); return true; }
            value = 0; return false;
        }
        public bool TryGetBool(string name, out bool value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.Bool) { value = v.BoolValue; return true; }
            value = false; return false;
        }
        public bool TryGetFloat(string name, out float value)
        {
            if (TryGetValue(name, out var v) && (v.Kind == ScriptValueKind.Float || v.Kind == ScriptValueKind.Int)) { value = v.AsFloat(); return true; }
            value = 0f; return false;
        }
        public bool TryGetString(string name, out string value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.String) { value = v.StringValue; return true; }
            value = null; return false;
        }
        public bool TryGetGameObject(string name, out ScriptGameObjectRef value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.GameObject) { value = v.GameObjectValue; return true; }
            value = default; return false;
        }
        public bool TryGetComponent(string name, out ScriptComponentRef value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.Component) { value = v.ComponentValue; return true; }
            value = default; return false;
        }
        public bool TryGetList(string name, out List<ScriptValue> value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.List) { value = v.ListValue; return true; }
            value = null; return false;
        }

        public void SetInt(string name, int value) => SetValue(name, ScriptValue.FromInt(value));
        public void SetBool(string name, bool value) => SetValue(name, ScriptValue.FromBool(value));
        public void SetFloat(string name, float value) => SetValue(name, ScriptValue.FromFloat(value));
        public void SetString(string name, string value) => SetValue(name, ScriptValue.FromString(value));
        public void SetGameObject(string name, ScriptGameObjectRef value) => SetValue(name, ScriptValue.FromGameObject(value));
        public void SetComponent(string name, ScriptComponentRef value) => SetValue(name, ScriptValue.FromComponent(value));
        public void SetList(string name, List<ScriptValue> value) => SetValue(name, ScriptValue.FromList(value));
    }

    public sealed class LevelScriptExecutionContext
    {
        readonly List<Dictionary<string, ScriptValue>> scopes = new();
        readonly Stack<Dictionary<string, ScriptValue>> scopePool = new();
        readonly Dictionary<string, ScriptPlayerBinding> playerBindings = new();
        readonly Dictionary<string, FunctionDefinitionNode> functions;
        readonly LevelScriptProgramGlobals programGlobals;
        LevelScriptLinkedProgram linkedProgram;
        string currentModulePath = string.Empty;
        int? currentTriggerGroup;

        public ScriptGameObjectRef LocalPlayerRef { get; set; } = ScriptGameObjectRef.Null;
        public int CallStackDepth { get; set; } = 0;
        public const int MaxCallStackDepth = 128;

        public bool WritesProgramGlobals { get; set; }
        public bool HasReturn { get; set; }
        public bool HasBreak { get; set; }
        public bool HasContinue { get; set; }
        public object ReturnValue { get; set; }

        public string CurrentModulePath
        {
            get => currentModulePath;
            set => currentModulePath = value ?? string.Empty;
        }

        public LevelScriptExecutionContext(
            Dictionary<string, FunctionDefinitionNode> functionRegistry = null,
            LevelScriptProgramGlobals globals = null,
            LevelScriptLinkedProgram linked = null,
            string modulePath = null)
        {
            functions = functionRegistry ?? new Dictionary<string, FunctionDefinitionNode>();
            programGlobals = globals;
            linkedProgram = linked;
            currentModulePath = modulePath ?? string.Empty;
            PushScope();
        }

        Dictionary<string, ScriptValue> CurrentScope
        {
            get
            {
                if (scopes.Count == 0)
                    PushScope();
                return scopes[scopes.Count - 1];
            }
        }

        public void PushScope()
        {
            var dict = scopePool.Count > 0 ? scopePool.Pop() : new Dictionary<string, ScriptValue>(StringComparer.OrdinalIgnoreCase);
            dict.Clear();
            scopes.Add(dict);
        }

        public void PopScope()
        {
            if (scopes.Count > 1)
            {
                int last = scopes.Count - 1;
                var dict = scopes[last];
                dict.Clear();
                scopePool.Push(dict);
                scopes.RemoveAt(last);
            }
        }

        public LevelScriptExecutionContext Fork()
        {
            var forked = new LevelScriptExecutionContext(functions, programGlobals, linkedProgram, currentModulePath);
            forked.LocalPlayerRef = LocalPlayerRef;
            forked.WritesProgramGlobals = false;
            forked.CallStackDepth = CallStackDepth;
            forked.scopes.Clear();
            for (int i = 0; i < scopes.Count; i++)
            {
                var dict = new Dictionary<string, ScriptValue>(scopes[i], StringComparer.OrdinalIgnoreCase);
                forked.scopes.Add(dict);
            }
            foreach (KeyValuePair<string, ScriptPlayerBinding> pair in playerBindings)
                forked.playerBindings[pair.Key] = pair.Value;
            forked.currentTriggerGroup = currentTriggerGroup;
            forked.HasReturn = HasReturn;
            forked.HasBreak = HasBreak;
            forked.HasContinue = HasContinue;
            forked.ReturnValue = ReturnValue;
            return forked;
        }

        public bool TryGetFunction(string name, out FunctionDefinitionNode definition) =>
            functions.TryGetValue(name, out definition);

        public void SetCurrentTriggerGroup(int groupId) => currentTriggerGroup = groupId;

        public int GetCurrentTriggerGroup()
        {
            if (!currentTriggerGroup.HasValue)
                throw new InvalidOperationException("Group() is only available inside a trigger handler.");
            return currentTriggerGroup.Value;
        }

        public void BindPlayer(string name, ScriptPlayerBinding binding) => playerBindings[name] = binding;

        public float GetPlayerMember(string playerName, PlayerMember member)
        {
            if (!playerBindings.TryGetValue(playerName, out ScriptPlayerBinding binding))
                throw new InvalidOperationException($"Unknown player variable '{playerName}'.");
            return member == PlayerMember.X ? binding.X : binding.Y;
        }

        public string GetPlayerName(string playerName)
        {
            if (!playerBindings.TryGetValue(playerName, out ScriptPlayerBinding binding))
                throw new InvalidOperationException($"Unknown player variable '{playerName}'.");
            return binding.Name;
        }

        public void Declare(string name, ScriptValue val)
        {
            if (WritesProgramGlobals && programGlobals != null)
            {
                if (programGlobals.Contains(name))
                    throw new InvalidOperationException($"Variable '{name}' is already declared.");
                programGlobals.Declare(name, val);
                return;
            }

            var cur = CurrentScope;
            if (cur.ContainsKey(name))
                throw new InvalidOperationException($"Variable '{name}' is already declared.");

            cur[name] = val;
        }

        public void DeclareInt(string name, int value) => Declare(name, ScriptValue.FromInt(value));
        public void DeclareBool(string name, bool value) => Declare(name, ScriptValue.FromBool(value));
        public void DeclareFloat(string name, float value) => Declare(name, ScriptValue.FromFloat(value));
        public void DeclareString(string name, string value) => Declare(name, ScriptValue.FromString(value));
        public void DeclareGameObject(string name, ScriptGameObjectRef value) => Declare(name, ScriptValue.FromGameObject(value));
        public void DeclareComponent(string name, ScriptComponentRef value) => Declare(name, ScriptValue.FromComponent(value));
        public void DeclareList(string name, List<ScriptValue> value) => Declare(name, ScriptValue.FromList(value));

        public void SetValue(string name, ScriptValue val)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].TryGetValue(name, out ScriptValue existing))
                {
                    if (existing.Kind == ScriptValueKind.Float && val.Kind == ScriptValueKind.Int)
                        val = ScriptValue.FromFloat(val.IntValue);
                    else if (existing.Kind != val.Kind)
                        throw new InvalidOperationException($"Variable '{name}' is not a {val.Kind.ToString().ToLowerInvariant()}.");
                    scopes[i][name] = val;
                    return;
                }
            }

            if (programGlobals != null && programGlobals.TryGetValue(name, out ScriptValue gVal))
            {
                EnsureCanAccessVariable(name);
                if (gVal.Kind == ScriptValueKind.Float && val.Kind == ScriptValueKind.Int)
                    val = ScriptValue.FromFloat(val.IntValue);
                else if (gVal.Kind != val.Kind)
                    throw new InvalidOperationException($"Variable '{name}' is not a {val.Kind.ToString().ToLowerInvariant()}.");
                programGlobals.SetValue(name, val);
                return;
            }

            CurrentScope[name] = val;
        }

        public void SetInt(string name, int value) => SetValue(name, ScriptValue.FromInt(value));
        public void SetBool(string name, bool value) => SetValue(name, ScriptValue.FromBool(value));
        public void SetFloat(string name, float value) => SetValue(name, ScriptValue.FromFloat(value));
        public void SetString(string name, string value) => SetValue(name, ScriptValue.FromString(value));
        public void SetGameObject(string name, ScriptGameObjectRef value) => SetValue(name, ScriptValue.FromGameObject(value));
        public void SetComponent(string name, ScriptComponentRef value) => SetValue(name, ScriptValue.FromComponent(value));
        public void SetList(string name, List<ScriptValue> value) => SetValue(name, ScriptValue.FromList(value));

        public void BindGameObject(string name, ScriptGameObjectRef reference) => SetValue(name, ScriptValue.FromGameObject(reference));

        public bool TryGetValue(string name, out ScriptValue val)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].TryGetValue(name, out val))
                    return true;
            }

            if (programGlobals != null && programGlobals.TryGetValue(name, out val))
            {
                if (!CanAccessSymbol(name, LevelScriptSymbolKind.Variable, out _))
                {
                    val = default;
                    return false;
                }
                return true;
            }

            val = default;
            return false;
        }

        public ScriptValue GetValue(string name)
        {
            if (TryGetValue(name, out ScriptValue val))
                return val;
            throw new InvalidOperationException(BuildUnknownSymbolError(name, LevelScriptSymbolKind.Variable));
        }

        public int GetInt(string name) => GetValue(name).AsInt();
        public bool GetBool(string name) => GetValue(name).BoolValue;
        public float GetFloat(string name) => GetValue(name).AsFloat();
        public float GetNumericAsFloat(string name) => GetFloat(name);
        public string GetString(string name) => GetValue(name).StringValue ?? string.Empty;
        public ScriptGameObjectRef GetGameObject(string name) => GetValue(name).GameObjectValue;
        public ScriptComponentRef GetComponent(string name) => GetValue(name).ComponentValue;
        public List<ScriptValue> GetList(string name)
        {
            var val = GetValue(name);
            return val.ListValue ?? (val.ListValue = new List<ScriptValue>());
        }

        public bool TryGetInt(string name, out int value)
        {
            if (TryGetValue(name, out var v) && (v.Kind == ScriptValueKind.Int || v.Kind == ScriptValueKind.Float)) { value = v.AsInt(); return true; }
            value = 0; return false;
        }

        public bool TryGetBool(string name, out bool value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.Bool) { value = v.BoolValue; return true; }
            value = false; return false;
        }

        public bool TryGetFloat(string name, out float value)
        {
            if (TryGetValue(name, out var v) && (v.Kind == ScriptValueKind.Float || v.Kind == ScriptValueKind.Int)) { value = v.AsFloat(); return true; }
            value = 0f; return false;
        }

        public bool TryGetString(string name, out string value)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.String) { value = v.StringValue; return true; }
            value = null; return false;
        }

        public bool TryGetGameObject(string name, out ScriptGameObjectRef reference)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.GameObject) { reference = v.GameObjectValue; return true; }
            reference = default; return false;
        }

        public bool TryGetComponent(string name, out ScriptComponentRef reference)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.Component) { reference = v.ComponentValue; return true; }
            reference = default; return false;
        }

        public bool TryGetList(string name, out List<ScriptValue> list)
        {
            if (TryGetValue(name, out var v) && v.Kind == ScriptValueKind.List) { list = v.ListValue; return true; }
            list = null; return false;
        }

        public bool TryGetVariableKind(string name, out ScriptValueKind kind)
        {
            if (TryGetValue(name, out var v)) { kind = v.Kind; return true; }
            kind = default; return false;
        }

        public bool TryGetAnyVariableString(string name, out string str)
        {
            if (TryGetValue(name, out var v)) { str = v.ToString(); return true; }
            str = string.Empty; return false;
        }

        public bool TryGetFunctionAccessible(string name, out FunctionDefinitionNode definition, out string error)
        {
            error = null;
            definition = null;
            if (!functions.TryGetValue(name, out definition))
            {
                error = BuildUnknownSymbolError(name, LevelScriptSymbolKind.Function);
                return false;
            }

            if (!CanAccessSymbol(name, LevelScriptSymbolKind.Function, out string accessError))
            {
                error = accessError;
                definition = null;
                return false;
            }

            return true;
        }

        bool CanAccessSymbol(string name, LevelScriptSymbolKind expectedKind, out string error)
        {
            error = null;
            if (linkedProgram == null ||
                !linkedProgram.Symbols.TryGetValue(name, out LevelScriptSymbolInfo info))
            {
                return true;
            }

            if (info.Kind != expectedKind)
                return true;

            if (info.IsPublic)
                return true;

            if (string.Equals(info.ModulePath, currentModulePath, StringComparison.OrdinalIgnoreCase))
                return true;

            error = BuildUnknownSymbolError(name, expectedKind);
            return false;
        }

        void EnsureCanAccessVariable(string name)
        {
            if (!CanAccessSymbol(name, LevelScriptSymbolKind.Variable, out string error))
                throw new InvalidOperationException(error);
        }

        string BuildUnknownSymbolError(string name, LevelScriptSymbolKind kind)
        {
            string label = kind == LevelScriptSymbolKind.Function ? "function" : "variable";
            if (LevelScriptModuleLinker.TryFindPrivateSymbol(
                    linkedProgram, name, currentModulePath, out LevelScriptSymbolInfo privateSymbol))
            {
                return $"Unknown {label} '{name}'. {LevelScriptModuleLinker.FormatPrivateAccessHint(privateSymbol)}";
            }

            return $"Unknown {label} '{name}'.";
        }
    }
    public abstract class GameObjectArrayExpression
    {
        public abstract List<ScriptGameObjectRef> Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class GroupGameObjectArrayExpression : GameObjectArrayExpression
    {
        public IntExpression GroupId;

        public override List<ScriptGameObjectRef> Evaluate(LevelScriptExecutionContext context)
        {
            int group = GroupId != null
                ? GroupId.Evaluate(context)
                : context.GetCurrentTriggerGroup();

            return LevelScriptObjectUtility.CollectTilesInGroup(group);
        }
    }

    /// <summary>
    /// All tiles belonging to any of the listed groups (used by spawnPoints range).
    /// </summary>
    public sealed class MultiGroupGameObjectArrayExpression : GameObjectArrayExpression
    {
        public readonly List<int> GroupIds = new();

        public override List<ScriptGameObjectRef> Evaluate(LevelScriptExecutionContext context)
        {
            var result = new List<ScriptGameObjectRef>();
            var seen = new HashSet<uint>();
            for (int i = 0; i < GroupIds.Count; i++)
            {
                List<ScriptGameObjectRef> members = LevelScriptObjectUtility.CollectTilesInGroup(GroupIds[i]);
                for (int m = 0; m < members.Count; m++)
                {
                    if (seen.Add(members[m].NetId))
                        result.Add(members[m]);
                }
            }

            return result;
        }
    }

    public sealed class GameObjectArrayLiteralExpression : GameObjectArrayExpression
    {
        public readonly List<GameObjectExpression> Items = new();

        public override List<ScriptGameObjectRef> Evaluate(LevelScriptExecutionContext context)
        {
            var result = new List<ScriptGameObjectRef>();
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is AllPlayersArrayItemExpression)
                {
                    result.AddRange(LevelScriptObjectUtility.CollectAllRunners());
                    continue;
                }

                result.Add(Items[i].Evaluate(context));
            }

            return result;
        }
    }

    public abstract class GameObjectExpression
    {
        public abstract ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class GameObjectNilExpression : GameObjectExpression
    {
        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            ScriptGameObjectRef.Null;
    }

    public sealed class GameObjectVariableExpression : GameObjectExpression
    {
        public string Name;
        public int SourceIndex = -1;

        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            context.GetGameObject(Name);
    }

    public sealed class AllPlayersArrayItemExpression : GameObjectExpression
    {
        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            throw new InvalidOperationException("Use {player} as a GameObject array.");
    }

    public sealed class FixedTileGameObjectExpression : GameObjectExpression
    {
        public uint NetId;

        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            new ScriptGameObjectRef(ScriptObjectKind.Tile, NetId);
    }

    public sealed class AllPlayersGameObjectArrayExpression : GameObjectArrayExpression
    {
        public override List<ScriptGameObjectRef> Evaluate(LevelScriptExecutionContext context) =>
            LevelScriptObjectUtility.CollectAllRunners();
    }

    public sealed class InstanceCallExpression : GameObjectExpression
    {
        public StringExpression TypeName;
        public FloatExpression X;
        public FloatExpression Y;

        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            LevelScriptObjectUtility.SpawnTile(
                TypeName.Evaluate(context),
                X.Evaluate(context),
                Y.Evaluate(context));
    }

    public sealed class FindNearestPlayerExpression : GameObjectExpression
    {
        public FloatExpression X;
        public FloatExpression Y;

        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            LevelScriptObjectUtility.FindNearestPlayer(
                X.Evaluate(context),
                Y.Evaluate(context));
    }

    public sealed class SceneObjectExpression : GameObjectExpression
    {
        public string Path; // e.g. "MainCamera" or "ParallaxBackground.Clouds4"

        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context) =>
            LevelScriptObjectUtility.ResolveSceneObject(Path);
    }



    public sealed class LocalPlayerExpression : GameObjectExpression
    {
        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context)
        {
            if (context != null && !context.LocalPlayerRef.IsNull)
                return context.LocalPlayerRef;

            if (Mirror.NetworkClient.localPlayer != null)
                return new ScriptGameObjectRef(ScriptObjectKind.Player, Mirror.NetworkClient.localPlayer.netId);

            return ScriptGameObjectRef.Null;
        }
    }

    public abstract class ComponentExpression
    {
        public abstract ScriptComponentRef Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class ComponentNilExpression : ComponentExpression
    {
        public override ScriptComponentRef Evaluate(LevelScriptExecutionContext context) =>
            ScriptComponentRef.Null;
    }

    public sealed class ComponentVariableExpression : ComponentExpression
    {
        public string Name;
        public int SourceIndex = -1;

        public override ScriptComponentRef Evaluate(LevelScriptExecutionContext context) =>
            context.GetComponent(Name);
    }

    public sealed class GetComponentExpression : ComponentExpression
    {
        public GameObjectExpression Target;
        public string ComponentName;

        public override ScriptComponentRef Evaluate(LevelScriptExecutionContext context)
        {
            ScriptGameObjectRef gameObject = Target.Evaluate(context);
            return new ScriptComponentRef(gameObject, ComponentName);
        }
    }

    public abstract class IntExpression
    {
        public abstract int Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class IntLiteralExpression : IntExpression
    {
        public int Value;
        public override int Evaluate(LevelScriptExecutionContext context) => Value;
    }

    public sealed class IntVariableExpression : IntExpression
    {
        public string Name;
        public int SourceIndex = -1;
        public override int Evaluate(LevelScriptExecutionContext context) => context.GetInt(Name);
    }

    public enum IntBinaryOperator : byte
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo
    }

    public sealed class ListCountExpression : IntExpression
    {
        public string ListName;
        public override int Evaluate(LevelScriptExecutionContext context)
        {
            if (context.TryGetList(ListName, out var list) && list != null)
                return list.Count;
            return 0;
        }
    }

    public sealed class ListItemIntExpression : IntExpression
    {
        public string ListName;
        public IntExpression Index;
        public override int Evaluate(LevelScriptExecutionContext context)
        {
            if (context.TryGetList(ListName, out var list) && list != null)
            {
                int idx = Index != null ? Index.Evaluate(context) : 0;
                if (idx >= 0 && idx < list.Count)
                    return list[idx].AsInt();
            }
            return 0;
        }
    }

    public sealed class ListItemFloatExpression : FloatExpression
    {
        public string ListName;
        public IntExpression Index;
        public override float Evaluate(LevelScriptExecutionContext context)
        {
            if (context.TryGetList(ListName, out var list) && list != null)
            {
                int idx = Index != null ? Index.Evaluate(context) : 0;
                if (idx >= 0 && idx < list.Count)
                    return list[idx].AsFloat();
            }
            return 0f;
        }
    }

    public sealed class ListItemStringExpression : StringExpression
    {
        public string ListName;
        public IntExpression Index;
        public override string Evaluate(LevelScriptExecutionContext context)
        {
            if (context.TryGetList(ListName, out var list) && list != null)
            {
                int idx = Index != null ? Index.Evaluate(context) : 0;
                if (idx >= 0 && idx < list.Count)
                    return list[idx].ToString();
            }
            return string.Empty;
        }
    }

    public sealed class ListItemBoolExpression : BoolExpression
    {
        public string ListName;
        public IntExpression Index;
        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            if (context.TryGetList(ListName, out var list) && list != null)
            {
                int idx = Index != null ? Index.Evaluate(context) : 0;
                if (idx >= 0 && idx < list.Count)
                    return list[idx].BoolValue;
            }
            return false;
        }
    }

    public sealed class ListItemGameObjectExpression : GameObjectExpression
    {
        public string ListName;
        public IntExpression Index;
        public override ScriptGameObjectRef Evaluate(LevelScriptExecutionContext context)
        {
            if (context.TryGetList(ListName, out var list) && list != null)
            {
                int idx = Index != null ? Index.Evaluate(context) : 0;
                if (idx >= 0 && idx < list.Count)
                    return list[idx].GameObjectValue;
            }
            return ScriptGameObjectRef.Null;
        }
    }

    public sealed class IntBinaryExpression : IntExpression
    {
        public IntExpression Left;
        public IntExpression Right;
        public IntBinaryOperator Operator;

        public override int Evaluate(LevelScriptExecutionContext context)
        {
            int left = Left.Evaluate(context);
            int right = Right.Evaluate(context);

            return Operator switch
            {
                IntBinaryOperator.Add => left + right,
                IntBinaryOperator.Subtract => left - right,
                IntBinaryOperator.Multiply => left * right,
                IntBinaryOperator.Divide => right == 0
                    ? throw new InvalidOperationException("Division by zero.")
                    : left / right,
                IntBinaryOperator.Modulo => right == 0
                    ? throw new InvalidOperationException("Modulo by zero.")
                    : left % right,
                _ => left
            };
        }
    }

    public abstract class FloatExpression
    {
        public abstract float Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class FloatLiteralExpression : FloatExpression
    {
        public float Value;
        public override float Evaluate(LevelScriptExecutionContext context) => Value;
    }

    public sealed class FloatFromIntExpression : FloatExpression
    {
        public IntExpression Value;
        public override float Evaluate(LevelScriptExecutionContext context) =>
            Value != null ? Value.Evaluate(context) : 0f;
    }

    public sealed class FloatVariableExpression : FloatExpression
    {
        public string Name;
        public int SourceIndex = -1;
        public override float Evaluate(LevelScriptExecutionContext context) => context.GetNumericAsFloat(Name);
    }

    public sealed class RandomFloatExpression : FloatExpression
    {
        public FloatExpression Min;
        public FloatExpression Max;

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            float min = Min.Evaluate(context);
            float max = Max.Evaluate(context);
            if (min > max)
                (min, max) = (max, min);

            return UnityEngine.Random.Range(min, max);
        }
    }

    public enum FloatBinaryOperator : byte
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo
    }

    public sealed class FloatBinaryExpression : FloatExpression
    {
        public FloatExpression Left;
        public FloatExpression Right;
        public FloatBinaryOperator Operator;

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            float left = Left.Evaluate(context);
            float right = Right.Evaluate(context);

            return Operator switch
            {
                FloatBinaryOperator.Add => left + right,
                FloatBinaryOperator.Subtract => left - right,
                FloatBinaryOperator.Multiply => left * right,
                FloatBinaryOperator.Divide => right == 0f
                    ? throw new InvalidOperationException("Division by zero.")
                    : left / right,
                FloatBinaryOperator.Modulo => right == 0f
                    ? throw new InvalidOperationException("Modulo by zero.")
                    : left % right,
                _ => left
            };
        }
    }

    public abstract class StringExpression
    {
        public abstract string Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class StringLiteralExpression : StringExpression
    {
        public string Value;
        public override string Evaluate(LevelScriptExecutionContext context) => Value;
    }

    public sealed class StringVariableExpression : StringExpression
    {
        public string Name;
        public int SourceIndex = -1;
        public override string Evaluate(LevelScriptExecutionContext context) => context.GetString(Name);
    }

    public sealed class StringConcatExpression : StringExpression
    {
        public StringExpression Left;
        public StringExpression Right;

        public override string Evaluate(LevelScriptExecutionContext context)
        {
            string l = Left != null ? Left.Evaluate(context) : string.Empty;
            string r = Right != null ? Right.Evaluate(context) : string.Empty;
            return l + r;
        }
    }

    public sealed class StringConvertExpression : StringExpression
    {
        public string VariableName;
        public IntExpression IntExpr;
        public FloatExpression FloatExpr;
        public BoolExpression BoolExpr;

        public override string Evaluate(LevelScriptExecutionContext context)
        {
            if (!string.IsNullOrEmpty(VariableName))
            {
                if (context.TryGetAnyVariableString(VariableName, out string str))
                    return str;
                return string.Empty;
            }
            if (IntExpr != null) return IntExpr.Evaluate(context).ToString();
            if (FloatExpr != null) return FloatExpr.Evaluate(context).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (BoolExpr != null) return BoolExpr.Evaluate(context) ? "true" : "false";
            return string.Empty;
        }
    }

    public enum MathFunctionKind : byte
    {
        Abs,
        Min,
        Max,
        Sin,
        Cos,
        Sqrt,
        Floor,
        Ceil,
        Clamp
    }

    public sealed class MathFunctionExpression : FloatExpression
    {
        public MathFunctionKind Kind;
        public readonly List<FloatExpression> Arguments = new();

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            switch (Kind)
            {
                case MathFunctionKind.Abs:
                    return Math.Abs(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f);
                case MathFunctionKind.Min:
                    return Math.Min(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f,
                                    Arguments.Count > 1 ? Arguments[1].Evaluate(context) : 0f);
                case MathFunctionKind.Max:
                    return Math.Max(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f,
                                    Arguments.Count > 1 ? Arguments[1].Evaluate(context) : 0f);
                case MathFunctionKind.Sin:
                    return (float)Math.Sin(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f);
                case MathFunctionKind.Cos:
                    return (float)Math.Cos(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f);
                case MathFunctionKind.Sqrt:
                    return (float)Math.Sqrt(Math.Max(0f, Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f));
                case MathFunctionKind.Floor:
                    return (float)Math.Floor(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f);
                case MathFunctionKind.Ceil:
                    return (float)Math.Ceiling(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f);
                case MathFunctionKind.Clamp:
                    return Math.Clamp(Arguments.Count > 0 ? Arguments[0].Evaluate(context) : 0f,
                                      Arguments.Count > 1 ? Arguments[1].Evaluate(context) : 0f,
                                      Arguments.Count > 2 ? Arguments[2].Evaluate(context) : 0f);
                default:
                    return 0f;
            }
        }
    }

    public sealed class IntMathFunctionExpression : IntExpression
    {
        public MathFunctionExpression FloatMath;

        public override int Evaluate(LevelScriptExecutionContext context)
        {
            return (int)FloatMath.Evaluate(context);
        }
    }

    public sealed class PlayerMemberFloatExpression : FloatExpression
    {
        public string PlayerName;
        public PlayerMember Member;

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            if (string.Equals(PlayerName, "localPlayer", StringComparison.OrdinalIgnoreCase))
            {
                ScriptGameObjectRef localRef = (context != null && !context.LocalPlayerRef.IsNull)
                    ? context.LocalPlayerRef
                    : (Mirror.NetworkClient.localPlayer != null
                        ? new ScriptGameObjectRef(ScriptObjectKind.Player, Mirror.NetworkClient.localPlayer.netId)
                        : ScriptGameObjectRef.Null);

                if (LevelScriptObjectUtility.TryGetPosition(localRef, out float lx, out float ly))
                    return Member == PlayerMember.X ? lx : ly;
            }

            if (context.TryGetGameObject(PlayerName, out ScriptGameObjectRef reference) &&
                LevelScriptObjectUtility.TryGetPosition(reference, out float x, out float y))
            {
                return Member == PlayerMember.X ? x : y;
            }

            return context.GetPlayerMember(PlayerName, Member);
        }
    }

    public sealed class GameObjectMemberFloatExpression : FloatExpression
    {
        public string ObjectName;
        public PlayerMember Member;

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            ScriptGameObjectRef reference = context.GetGameObject(ObjectName);
            if (!LevelScriptObjectUtility.TryGetPosition(reference, out float x, out float y))
                return 0f;

            return Member == PlayerMember.X ? x : y;
        }
    }

    public sealed class PlayerMemberStringExpression : StringExpression
    {
        public string PlayerName;

        public override string Evaluate(LevelScriptExecutionContext context)
        {
            if (string.Equals(PlayerName, "localPlayer", StringComparison.OrdinalIgnoreCase))
            {
                ScriptGameObjectRef localRef = (context != null && !context.LocalPlayerRef.IsNull)
                    ? context.LocalPlayerRef
                    : (Mirror.NetworkClient.localPlayer != null
                        ? new ScriptGameObjectRef(ScriptObjectKind.Player, Mirror.NetworkClient.localPlayer.netId)
                        : ScriptGameObjectRef.Null);

                if (LevelScriptObjectUtility.TryResolveTransform(localRef, out var t))
                    return t.name;
            }

            if (context != null && context.TryGetGameObject(PlayerName, out ScriptGameObjectRef reference) &&
                LevelScriptObjectUtility.TryResolveTransform(reference, out var objTr))
            {
                return objTr.name;
            }

            return context != null ? context.GetPlayerName(PlayerName) : string.Empty;
        }
    }

    public sealed class GameObjectArrayMemberFloatExpression : FloatExpression
    {
        public GameObjectArrayExpression Targets;
        public PlayerMember Member;

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            List<ScriptGameObjectRef> objects = Targets.Evaluate(context);
            if (!LevelScriptObjectUtility.TryGetCenter(objects, out float x, out float y))
                return 0f;

            return Member == PlayerMember.X ? x : y;
        }
    }

    public sealed class GroupMemberFloatExpression : FloatExpression
    {
        public IntExpression GroupId;
        public PlayerMember Member;

        public override float Evaluate(LevelScriptExecutionContext context)
        {
            int group = GroupId != null
                ? GroupId.Evaluate(context)
                : context.GetCurrentTriggerGroup();

            if (!LevelScriptGroupUtility.TryGetGroupCenter(group, out float x, out float y))
                return 0f;

            return Member == PlayerMember.X ? x : y;
        }
    }

    public abstract class BoolExpression
    {
        public abstract bool Evaluate(LevelScriptExecutionContext context);
    }

    public sealed class BoolLiteralExpression : BoolExpression
    {
        public bool Value;
        public override bool Evaluate(LevelScriptExecutionContext context) => Value;
    }

    public sealed class BoolVariableExpression : BoolExpression
    {
        public string Name;
        public int SourceIndex = -1;
        public override bool Evaluate(LevelScriptExecutionContext context) => context.GetBool(Name);
    }

    public enum ComparisonOperator : byte
    {
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Equal,
        NotEqual
    }

    public sealed class GameObjectComparisonExpression : BoolExpression
    {
        public GameObjectExpression Left;
        public GameObjectExpression Right;
        public ComparisonOperator Operator;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            ScriptGameObjectRef left = Left.Evaluate(context);
            ScriptGameObjectRef right = Right.Evaluate(context);
            bool equal = left.Equals(right);

            return Operator switch
            {
                ComparisonOperator.Equal => equal,
                ComparisonOperator.NotEqual => !equal,
                _ => throw new InvalidOperationException("GameObject comparisons support only == and !=.")
            };
        }
    }

    public sealed class ComparisonExpression : BoolExpression
    {
        public IntExpression Left;
        public IntExpression Right;
        public ComparisonOperator Operator;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            int left = Left.Evaluate(context);
            int right = Right.Evaluate(context);

            return Operator switch
            {
                ComparisonOperator.Less => left < right,
                ComparisonOperator.LessOrEqual => left <= right,
                ComparisonOperator.Greater => left > right,
                ComparisonOperator.GreaterOrEqual => left >= right,
                ComparisonOperator.Equal => left == right,
                ComparisonOperator.NotEqual => left != right,
                _ => false
            };
        }
    }

    public sealed class FloatComparisonExpression : BoolExpression
    {
        public FloatExpression Left;
        public FloatExpression Right;
        public ComparisonOperator Operator;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            float left = Left.Evaluate(context);
            float right = Right.Evaluate(context);

            return Operator switch
            {
                ComparisonOperator.Less => left < right,
                ComparisonOperator.LessOrEqual => left <= right,
                ComparisonOperator.Greater => left > right,
                ComparisonOperator.GreaterOrEqual => left >= right,
                ComparisonOperator.Equal => left == right,
                ComparisonOperator.NotEqual => left != right,
                _ => false
            };
        }
    }

    public sealed class StringComparisonExpression : BoolExpression
    {
        public StringExpression Left;
        public StringExpression Right;
        public ComparisonOperator Operator;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            string left = Left.Evaluate(context);
            string right = Right.Evaluate(context);

            return Operator switch
            {
                ComparisonOperator.Equal => left == right,
                ComparisonOperator.NotEqual => left != right,
                _ => throw new InvalidOperationException("String comparisons support only == and !=.")
            };
        }
    }

    public sealed class BoolComparisonExpression : BoolExpression
    {
        public BoolExpression Left;
        public BoolExpression Right;
        public ComparisonOperator Operator;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            bool left = Left != null && Left.Evaluate(context);
            bool right = Right != null && Right.Evaluate(context);

            return Operator switch
            {
                ComparisonOperator.Equal => left == right,
                ComparisonOperator.NotEqual => left != right,
                _ => throw new InvalidOperationException("Bool comparisons support only == and !=.")
            };
        }
    }

    public sealed class DynamicEqualityExpression : BoolExpression
    {
        public ScriptArgument Left;
        public ScriptArgument Right;
        public bool Negate;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            ScriptValue l = ScriptNetworkEventUtility.EvaluateToScriptValue(Left, context);
            ScriptValue r = ScriptNetworkEventUtility.EvaluateToScriptValue(Right, context);
            bool eq = l.Equals(r);
            return Negate ? !eq : eq;
        }
    }

    public enum BoolBinaryOperator : byte
    {
        And,
        Or
    }

    public sealed class BoolBinaryExpression : BoolExpression
    {
        public BoolExpression Left;
        public BoolExpression Right;
        public BoolBinaryOperator Operator;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            return Operator switch
            {
                BoolBinaryOperator.And => Left.Evaluate(context) && Right.Evaluate(context),
                BoolBinaryOperator.Or => Left.Evaluate(context) || Right.Evaluate(context),
                _ => false
            };
        }
    }

    public sealed class BoolNotExpression : BoolExpression
    {
        public BoolExpression Operand;
        public override bool Evaluate(LevelScriptExecutionContext context) => !Operand.Evaluate(context);
    }

    public abstract class LevelScriptNode
    {
        public int SourceIndex = -1;
    }

    public sealed class LevelScriptProgram : LevelScriptNode
    {
        public string SourcePath = string.Empty;
        public readonly List<UsingDirectiveNode> Usings = new();
        public readonly List<FunctionDefinitionNode> Functions = new();
        public readonly List<TriggerHandlerNode> TriggerHandlers = new();
        public readonly List<PlayerAddedHandlerNode> PlayerAddedHandlers = new();
        public readonly List<SpawnNode> Spawns = new();
        public readonly List<SpawnPointsNode> SpawnPointsDeclarations = new();
        public readonly List<EventListenerNode> EventListeners = new();

        public readonly List<LevelScriptNode> MainBody = new();
    }

    public sealed class UsingDirectiveNode : LevelScriptNode
    {
        public string Path = string.Empty;
    }

    /// <summary>
    /// Registers checkpoint triggers for every group id in [From, To].
    /// Touching group i sets the runner spawn to that group's center.
    /// </summary>
    public sealed class SpawnPointsNode : LevelScriptNode
    {
        public IntExpression From;
        public IntExpression To;
    }

    public sealed class FunctionDefinitionNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public readonly List<FunctionParameter> Parameters = new();
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class FunctionParameter
    {
        public ScriptValueKind Kind;
        public string Name;
    }

    public enum TriggerHandlerKind : byte
    {
        Enter,
        Stay,
        Exit
    }

    public sealed class TriggerHandlerNode : LevelScriptNode
    {
        public TriggerHandlerKind Kind;
        public GameObjectArrayExpression Source;
        public TriggerActorMode ActorMode = TriggerActorMode.Runners;
        public string GameObjectParameterName;
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class PlayerAddedHandlerNode : LevelScriptNode
    {
        public string PlayerParameterName;
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class SpawnNode : LevelScriptNode
    {
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class WhileNode : LevelScriptNode
    {
        public BoolExpression Condition;
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class IfNode : LevelScriptNode
    {
        public BoolExpression Condition;
        public readonly List<LevelScriptNode> ThenBody = new();
        public readonly List<ElseIfClause> ElseIfClauses = new();
        public readonly List<LevelScriptNode> ElseBody = new();
    }

    public sealed class ElseIfClause
    {
        public BoolExpression Condition;
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class IntDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public IntExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class BoolDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public BoolExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class FloatDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public FloatExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class StringDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public StringExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class GameObjectDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public GameObjectExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class ComponentDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public ComponentExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class ListDeclarationNode : LevelScriptNode
    {
        public string Name;
        public bool IsPublic;
        public string SourcePath = string.Empty;
        public readonly List<ScriptArgument> InitialItems = new();
        public FunctionCallNode CallValue;
    }

    public sealed class ListAddNode : LevelScriptNode
    {
        public string ListName;
        public ScriptArgument Item;
    }

    public sealed class ListRemoveAtNode : LevelScriptNode
    {
        public string ListName;
        public IntExpression Index;
    }

    public sealed class ListClearNode : LevelScriptNode
    {
        public string ListName;
    }

    public sealed class ListSetItemNode : LevelScriptNode
    {
        public string ListName;
        public IntExpression Index;
        public ScriptArgument Value;
    }

    public sealed class ListAssignmentNode : LevelScriptNode
    {
        public string Name;
        public readonly List<ScriptArgument> InitialItems = new();
        public FunctionCallNode CallValue;
    }

    public sealed class GameObjectAssignmentNode : LevelScriptNode
    {
        public string Name;
        public GameObjectExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class IntAssignmentNode : LevelScriptNode
    {
        public string Name;
        public IntExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class BoolAssignmentNode : LevelScriptNode
    {
        public string Name;
        public BoolExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class FloatAssignmentNode : LevelScriptNode
    {
        public string Name;
        public FloatExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class StringAssignmentNode : LevelScriptNode
    {
        public string Name;
        public StringExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class ComponentAssignmentNode : LevelScriptNode
    {
        public string Name;
        public ComponentExpression Value;
        public FunctionCallNode CallValue;
    }

    public sealed class ComponentPropertyAssignmentNode : LevelScriptNode
    {
        public string ObjectName; // For GameObject.ComponentName.Property = value
        public string ComponentVariableName; // For Component variable: comp.Property = value
        public GameObjectArrayExpression Targets;
        public string ComponentName;
        public string PropertyName;
        public ScriptValueKind ValueKind;
        public FloatExpression FloatValue;
        public IntExpression IntValue;
        public BoolExpression BoolValue;
    }

    public sealed class ComponentMethodCallNode : LevelScriptNode
    {
        public string ObjectName;
        public string ComponentName;
        public string MethodName;
        public readonly List<FloatExpression> Arguments = new();
    }

    public sealed class ComponentVariableMethodCallNode : LevelScriptNode
    {
        public string ComponentVariableName;
        public string MethodName;
        public readonly List<FloatExpression> Arguments = new();
    }

    public sealed class FunctionCallNode : LevelScriptNode
    {
        public string Name;
        public readonly List<ScriptArgument> Arguments = new();
    }

    public sealed class ReturnNode : LevelScriptNode
    {
        public ScriptArgument Value;
        public IntExpression IntValue;
        public FloatExpression FloatValue;
        public BoolExpression BoolValue;
        public StringExpression StringValue;
    }

    public sealed class BreakNode : LevelScriptNode
    {
    }

    public sealed class ContinueNode : LevelScriptNode
    {
    }

    public sealed class ScriptArgument
    {
        public ScriptValueKind Kind;
        public IntExpression IntValue;
        public FloatExpression FloatValue;
        public BoolExpression BoolValue;
        public StringExpression StringValue;
        public GameObjectExpression GameObjectValue;
        public ComponentExpression ComponentValue;
        public string IdentifierName;
        public IntExpression IndexExpr;
        public int SourceIndex = -1;
    }

    public sealed class PrintNode : LevelScriptNode
    {
        public readonly List<ScriptArgument> Arguments = new();
    }

    public sealed class ForNode : LevelScriptNode
    {
        public LevelScriptNode Init;
        public BoolExpression Condition;
        public LevelScriptNode Increment;
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class MoveNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public FloatExpression DeltaX;
        public FloatExpression DeltaY;
        public FloatExpression Duration;
        public StringExpression Ease;
    }

    public sealed class AnimateNode : LevelScriptNode
    {
        public GameObjectArrayExpression TargetGroup;
        public GameObjectArrayExpression KeyGroup;
    }

    public sealed class SpawnExecutorNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
    }

    public sealed class RotateNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public IntExpression PivotGroup;
        public GameObjectArrayExpression PivotObjects;
        public FloatExpression Degrees;
        public FloatExpression Duration;
        public StringExpression Ease;
    }

    public sealed class RotateToNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public GameObjectArrayExpression PivotObjects;
        public FloatExpression TargetX;
        public FloatExpression TargetY;
        public FloatExpression OffsetDegrees;
        public FloatExpression Duration;
        public StringExpression Ease;
    }

    public sealed class WaitNode : LevelScriptNode
    {
        public FloatExpression Duration;
    }

    public sealed class TransparencyNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public FloatExpression Value;
        public FloatExpression Duration;
        public StringExpression Ease;
    }

    public sealed class PlaySoundNode : LevelScriptNode
    {
        public FloatExpression X;
        public FloatExpression Y;
        public StringExpression SoundName;
        public FloatExpression Volume;
    }

    public sealed class SpawnPointNode : LevelScriptNode
    {
        public GameObjectExpression Player;
        public FloatExpression X;
        public FloatExpression Y;
    }

    public sealed class VisibilityNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public BoolExpression Visible;
    }

    public sealed class HitboxNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public BoolExpression Enabled;
    }

    public sealed class GotoNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public FloatExpression X;
        public FloatExpression Y;
        public FloatExpression Duration;
        public StringExpression Ease;
    }

    public sealed class ScaleToNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public FloatExpression ScaleX;
        public FloatExpression ScaleY;
        public FloatExpression Duration;
        public StringExpression Ease;
    }

    public sealed class AddComponentNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public StringExpression ComponentName;
    }

    public sealed class RemoveComponentNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public StringExpression ComponentName;
    }

    public sealed class SetTextureNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public StringExpression TextureId;
    }

    public sealed class SetColorNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
        public StringExpression HexColor;
    }

    public sealed class DestroyNode : LevelScriptNode
    {
        public GameObjectArrayExpression Targets;
    }


    public sealed class HasTileExpression : BoolExpression
    {
        public IntExpression X;
        public IntExpression Y;

        public override bool Evaluate(LevelScriptExecutionContext context)
        {
            int cellX = X != null ? X.Evaluate(context) : 0;
            int cellY = Y != null ? Y.Evaluate(context) : 0;
            return KaplaoGames.Server.LevelGridServer.Instance != null &&
                   KaplaoGames.Server.LevelGridServer.Instance.TryGetTile(new UnityEngine.Vector2Int(cellX, cellY), out _);
        }
    }

    public enum BackgroundActionKind : byte
    {
        Clear,
        HideLayer,
        ShowLayer,
        SetColor,
        SetSpeed,
        SetOffset,
        SetAlpha,
        Reset
    }

    public sealed class BackgroundNode : LevelScriptNode
    {
        public BackgroundActionKind Kind;
        public IntExpression LayerIndex;
        public StringExpression HexColor;
        public FloatExpression Value;
    }

    public enum CameraActionKind : byte
    {
        Shake,
        Zoom,
        PanTo,
        Follow,
        Unfollow,
        Fade
    }

    public sealed class CameraNode : LevelScriptNode
    {
        public CameraActionKind Kind;
        public FloatExpression Intensity;
        public FloatExpression Duration;
        public FloatExpression ExtraDuration;
        public StringExpression Ease;
        public StringExpression HexColor;
        public GameObjectExpression Target;
    }

    public enum HitboxConfigKind : byte
    {
        Size,
        Offset,
        Trigger,
        Bounce,
        Friction
    }

    public sealed class HitboxConfigNode : LevelScriptNode
    {
        public HitboxConfigKind Kind;
        public GameObjectArrayExpression Targets;
        public FloatExpression X;
        public FloatExpression Y;
        public BoolExpression BoolValue;
    }

    public enum SpriteActionKind : byte
    {
        Destroy,
        Flip,
        SortingOrder,
        Alpha,
        Color
    }

    public sealed class SpriteActionNode : LevelScriptNode
    {
        public SpriteActionKind Kind;
        public GameObjectArrayExpression Targets;
        public BoolExpression FlipX;
        public BoolExpression FlipY;
        public IntExpression SortingOrder;
        public FloatExpression Alpha;
        public StringExpression HexColor;
    }

    public enum WorldActionKind : byte
    {
        SetGravity,
        SetTimeScale,
        SetTile,
        RemoveTile
    }

    public sealed class WorldNode : LevelScriptNode
    {
        public WorldActionKind Kind;
        public FloatExpression GravityX;
        public FloatExpression GravityY;
        public IntExpression TileX;
        public IntExpression TileY;
        public IntExpression TileId;
    }

    public enum UIActionKind : byte
    {
        ShowBanner,
        ShowTimer
    }

    public sealed class UINode : LevelScriptNode
    {
        public UIActionKind Kind;
        public StringExpression Text;
        public FloatExpression Duration;
        public StringExpression HexColor;
    }

    public enum ScriptEventTarget : byte
    {
        ClientEvent, // server listens to this (sent from client via sendToServer)
        ServerEvent  // client listens to this (sent from server via sendToClient / sendToAllClients)
    }

    public sealed class EventListenerNode : LevelScriptNode
    {
        public ScriptEventTarget Target;
        public string EventName = string.Empty;
        public string SourcePath = string.Empty;
        public string PlayerParameterName;
        public readonly List<string> ParameterNames = new();
        public readonly List<LevelScriptNode> Body = new();
    }

    public sealed class SendToServerNode : LevelScriptNode
    {
        public StringExpression EventName;
        public readonly List<ScriptArgument> Arguments = new();
    }

    public sealed class SendToClientNode : LevelScriptNode
    {
        public GameObjectExpression TargetPlayer;
        public StringExpression EventName;
        public readonly List<ScriptArgument> Arguments = new();
    }

    public sealed class SendToAllClientsNode : LevelScriptNode
    {
        public StringExpression EventName;
        public readonly List<ScriptArgument> Arguments = new();
    }

    [Serializable]
    public struct ScriptNetworkEventArg
    {
        public ScriptValueKind Kind;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
        public string StringValue;
        public uint NetId;
        public ScriptObjectKind ObjectKind;
    }

    [Serializable]
    public sealed class ScriptNetworkEventPayload
    {
        public string EventName;
        public ScriptNetworkEventArg[] Args;
    }

    public static class ScriptNetworkEventUtility
    {
        public static ScriptValue EvaluateToScriptValue(ScriptArgument arg, LevelScriptExecutionContext context)
        {
            if (arg == null) return ScriptValue.FromInt(0);

            if (!string.IsNullOrEmpty(arg.IdentifierName))
            {
                if (context != null && context.TryGetValue(arg.IdentifierName, out ScriptValue val))
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
                return ScriptValue.FromInt(0);
            }

            switch (arg.Kind)
            {
                case ScriptValueKind.Int: return ScriptValue.FromInt(arg.IntValue != null ? arg.IntValue.Evaluate(context) : 0);
                case ScriptValueKind.Float: return ScriptValue.FromFloat(arg.FloatValue != null ? arg.FloatValue.Evaluate(context) : 0f);
                case ScriptValueKind.Bool: return ScriptValue.FromBool(arg.BoolValue != null && arg.BoolValue.Evaluate(context));
                case ScriptValueKind.String: return ScriptValue.FromString(arg.StringValue != null ? arg.StringValue.Evaluate(context) : string.Empty);
                case ScriptValueKind.GameObject: return ScriptValue.FromGameObject(arg.GameObjectValue != null ? arg.GameObjectValue.Evaluate(context) : ScriptGameObjectRef.Null);
                case ScriptValueKind.Component: return ScriptValue.FromComponent(arg.ComponentValue != null ? arg.ComponentValue.Evaluate(context) : ScriptComponentRef.Null);
                default: return ScriptValue.FromInt(0);
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
        }

        public static string SerializePayload(string eventName, ScriptNetworkEventArg[] args)
        {
            var payload = new ScriptNetworkEventPayload
            {
                EventName = eventName ?? string.Empty,
                Args = args ?? Array.Empty<ScriptNetworkEventArg>()
            };
            return UnityEngine.JsonUtility.ToJson(payload);
        }

        public static bool TryDeserializePayload(string json, out string eventName, out ScriptNetworkEventArg[] args)
        {
            eventName = string.Empty;
            args = null;
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                var payload = UnityEngine.JsonUtility.FromJson<ScriptNetworkEventPayload>(json);
                if (payload == null)
                    return false;

                eventName = payload.EventName ?? string.Empty;
                args = payload.Args ?? Array.Empty<ScriptNetworkEventArg>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static ScriptNetworkEventArg EvaluateArgument(ScriptArgument arg, LevelScriptExecutionContext context)
        {
            if (arg == null)
                return default;

            ScriptValue val = EvaluateToScriptValue(arg, context);
            switch (val.Kind)
            {
                case ScriptValueKind.Int:
                    return new ScriptNetworkEventArg { Kind = ScriptValueKind.Int, IntValue = val.IntValue };
                case ScriptValueKind.Float:
                    return new ScriptNetworkEventArg { Kind = ScriptValueKind.Float, FloatValue = val.FloatValue };
                case ScriptValueKind.Bool:
                    return new ScriptNetworkEventArg { Kind = ScriptValueKind.Bool, BoolValue = val.BoolValue };
                case ScriptValueKind.String:
                    return new ScriptNetworkEventArg { Kind = ScriptValueKind.String, StringValue = val.StringValue ?? string.Empty };
                case ScriptValueKind.GameObject:
                    return new ScriptNetworkEventArg { Kind = ScriptValueKind.GameObject, NetId = val.GameObjectValue.NetId, ObjectKind = val.GameObjectValue.Kind };
                default:
                    return default;
            }
        }

        public static void BindNetworkArg(LevelScriptExecutionContext context, string paramName, ScriptNetworkEventArg arg)
        {
            if (context == null || string.IsNullOrEmpty(paramName))
                return;

            switch (arg.Kind)
            {
                case ScriptValueKind.Int:
                    context.DeclareInt(paramName, arg.IntValue);
                    break;
                case ScriptValueKind.Float:
                    context.DeclareFloat(paramName, arg.FloatValue);
                    break;
                case ScriptValueKind.Bool:
                    context.DeclareBool(paramName, arg.BoolValue);
                    break;
                case ScriptValueKind.String:
                    context.DeclareString(paramName, arg.StringValue ?? string.Empty);
                    break;
                case ScriptValueKind.GameObject:
                    var goRef = new ScriptGameObjectRef(arg.ObjectKind, arg.NetId);
                    context.DeclareGameObject(paramName, goRef);
                    if (arg.ObjectKind == ScriptObjectKind.Player && LevelScriptObjectUtility.TryResolvePlayer(arg.NetId, out Network.NetworkPlayer player))
                    {
                        context.BindPlayer(paramName, new ScriptPlayerBinding
                        {
                            NetId = player.netId,
                            Name = player.displayName,
                            X = player.transform.position.x,
                            Y = player.transform.position.y
                        });
                    }
                    break;
            }
        }
    }
}
