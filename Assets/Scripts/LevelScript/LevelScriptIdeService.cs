using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using KaplaoGames.Shared;

namespace KaplaoGames.LevelScript
{
    public static class LevelScriptIdeService
    {
        static readonly HashSet<string> TypeKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "int", "bool", "float", "string", "GameObject", "Component", "list"
        };

        static readonly HashSet<string> ControlKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "spawn", "while", "for", "if", "else", "function", "return", "break", "continue",
            "public", "private", "using"
        };

        static readonly HashSet<string> ActionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "move", "animate", "spawnExecutor", "rotate", "rotateTo", "rotateto", "wait", "goto",
            "scaleTo", "scaleto", "visibility", "transparency", "playSound", "spawnPoint", "print",
            "spawnPoints", "hitbox", "instance", "addComponent", "removeComponent",
            "setTexture", "setColor", "destroy", "Group", "Scene", "GetComponent",

            "abs", "min", "max", "sin", "cos", "sqrt", "floor", "ceil", "clamp", "random",
            "Background", "Camera", "World", "UI",
            "backgroundClear", "backgroundReset", "backgroundHide", "backgroundShow", "backgroundColor", "backgroundSpeed", "backgroundOffset", "backgroundAlpha",
            "cameraShake", "cameraZoom", "cameraPanTo", "cameraFollow", "cameraUnfollow", "cameraFade",
            "setHitboxSize", "hitboxSize", "setHitboxOffset", "hitboxOffset", "setHitboxTrigger", "hitboxTrigger", "setHitboxBounce", "hitboxBounce", "setHitboxFriction", "hitboxFriction",
            "destroySprite", "setSpriteFlip", "setSortingOrder", "setSpriteAlpha", "setSpriteColor",
            "setGravity", "setTimeScale", "setTile", "removeTile", "hasTile",
            "showBanner", "showTimer", "onClientEvent", "onServerEvent", "sendToServer", "sendToClient", "sendToAllClients", "localPlayer", "Network"
        };

        static readonly HashSet<string> LiteralKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "true", "false", "nil"
        };

        static readonly HashSet<string> EaseKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "linear", "easeIn", "easeOut", "easeInOut"
        };

        static readonly HashSet<string> HandlerKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "OnTriggerEnter", "onTriggerEnter", "OnTriggerStay", "onTriggerStay",
            "OnTriggerExit", "onTriggerExit", "onPlayerAdded", "onClientEvent", "onServerEvent"
        };

        static readonly HashSet<string> ObjectKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Player", "Tile", "Runner"
        };

        static readonly string[] CompletionKeywords =
        {
            "spawn", "while", "for", "if", "else", "function", "public", "private", "using",
            "int", "bool", "float", "string", "GameObject", "Component", "list",
            "true", "false", "nil",
            "move", "animate", "spawnExecutor", "rotate", "rotateTo", "wait", "goto",
            "scaleTo", "visibility", "transparency", "playSound", "spawnPoint", "spawnPoints",
            "hitbox", "instance", "addComponent", "removeComponent", "setTexture", "setColor", "destroy",
            "Group", "Scene", "GetComponent", "linear", "easeIn", "easeOut", "easeInOut",

            "break", "continue", "return", "abs", "min", "max", "sin", "cos", "sqrt", "floor", "ceil", "clamp",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit", "onPlayerAdded",
            "Player", "Tile", "Runner", "random",
            "Background", "Camera", "World", "UI",
            "backgroundClear", "backgroundReset", "backgroundHide", "backgroundShow", "backgroundColor", "backgroundSpeed", "backgroundOffset", "backgroundAlpha",
            "cameraShake", "cameraZoom", "cameraPanTo", "cameraFollow", "cameraUnfollow", "cameraFade",
            "setHitboxSize", "hitboxSize", "setHitboxOffset", "hitboxOffset", "setHitboxTrigger", "hitboxTrigger", "setHitboxBounce", "hitboxBounce", "setHitboxFriction", "hitboxFriction",
            "destroySprite", "setSpriteFlip", "setSortingOrder", "setSpriteAlpha", "setSpriteColor",
            "setGravity", "setTimeScale", "setTile", "removeTile", "hasTile",
            "showBanner", "showTimer", "onClientEvent", "onServerEvent", "sendToServer", "sendToClient", "sendToAllClients", "localPlayer", "Network"
        };

        static readonly Regex DeclarationRegex = new(
            @"\b(?:(?:public|private)\s+)?(?:int|bool|float|string|GameObject|Component|list)(?:\[\])?\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        static readonly Regex FunctionRegex = new(
            @"\b(?:(?:public|private)\s+)?function\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        static readonly Regex ForInitRegex = new(
            @"\bfor\s*\(\s*(?:(?:public|private)\s+)?(?:int|bool|float|string|GameObject|Component|list)(?:\[\])?\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        static readonly Regex UserFunctionDefRegex = new(
            @"\b(?:(?:public|private)\s+)?function\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        static readonly Regex UserFunctionParamRegex = new(
            @"\b(?:int|bool|float|string|GameObject|Component)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        static readonly Dictionary<string, string[]> BuiltinCallSignatures =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["move"] = new[] { "targets", "dx", "dy", "time", "ease" },
                ["animate"] = new[] { "targetGroup", "keyGroup" },
                ["spawnexecutor"] = new[] { "targets" },
                ["rotate"] = new[] { "targets", "pivot", "degrees", "time", "ease" },
                ["rotateto"] = new[] { "targets", "pivot", "targetX", "targetY", "offset", "time", "ease" },
                ["wait"] = new[] { "time" },
                ["goto"] = new[] { "targets", "x", "y", "time", "ease" },
                ["scaleto"] = new[] { "targets", "scaleX", "scaleY", "time", "ease" },
                ["transparency"] = new[] { "targets", "value", "time", "anim" },
                ["visibility"] = new[] { "targets", "visible" },
                ["print"] = new[] { "value", "..." },
                ["playsound"] = new[] { "x", "y", "name", "volume" },
                ["spawnpoint"] = new[] { "player", "x", "y" },
                ["spawnpoints"] = new[] { "from", "to" },
                ["hitbox"] = new[] { "targets", "enabled" },
                ["addcomponent"] = new[] { "targets", "component" },
                ["removecomponent"] = new[] { "targets", "component" },
                ["settexture"] = new[] { "targets", "texture" },
                ["setcolor"] = new[] { "targets", "hex" },
                ["destroy"] = new[] { "targets" },
                ["group"] = new[] { "group" },
                ["instance"] = new[] { "type", "x", "y" },
                ["findnearestplayer"] = new[] { "x", "y" },
                ["ontriggerenter"] = new[] { "group" },
                ["ontriggerstay"] = new[] { "group" },
                ["ontriggerexit"] = new[] { "group" },
                ["onplayeradded"] = new[] { "player" },
                ["abs"] = new[] { "value" },
                ["min"] = new[] { "a", "b" },
                ["max"] = new[] { "a", "b" },
                ["sin"] = new[] { "radians" },
                ["cos"] = new[] { "radians" },
                ["sqrt"] = new[] { "value" },
                ["floor"] = new[] { "value" },
                ["ceil"] = new[] { "value" },
                ["clamp"] = new[] { "value", "min", "max" },
                ["random"] = new[] { "min", "max" },
                ["backgroundclear"] = Array.Empty<string>(),
                ["backgroundreset"] = Array.Empty<string>(),
                ["backgroundhide"] = new[] { "layerIndex" },
                ["backgroundshow"] = new[] { "layerIndex" },
                ["backgroundcolor"] = new[] { "layerIndex", "hexColor" },
                ["backgroundspeed"] = new[] { "layerIndex", "speed" },
                ["backgroundoffset"] = new[] { "layerIndex", "offsetY" },
                ["backgroundalpha"] = new[] { "layerIndex", "alpha" },

                ["camerashake"] = new[] { "intensity", "duration" },
                ["camerazoom"] = new[] { "size", "duration", "ease" },
                ["camerapanto"] = new[] { "x", "y", "duration", "ease" },
                ["camerafollow"] = new[] { "target" },
                ["cameraunfollow"] = Array.Empty<string>(),
                ["camerafade"] = new[] { "alpha", "duration", "color" },

                ["sethitboxsize"] = new[] { "targets", "width", "height" },
                ["hitboxsize"] = new[] { "targets", "width", "height" },
                ["sethitboxoffset"] = new[] { "targets", "offsetX", "offsetY" },
                ["hitboxoffset"] = new[] { "targets", "offsetX", "offsetY" },
                ["sethitboxtrigger"] = new[] { "targets", "isTrigger" },
                ["hitboxtrigger"] = new[] { "targets", "isTrigger" },
                ["sethitboxbounce"] = new[] { "targets", "bounciness" },
                ["hitboxbounce"] = new[] { "targets", "bounciness" },
                ["sethitboxfriction"] = new[] { "targets", "friction" },
                ["hitboxfriction"] = new[] { "targets", "friction" },

                ["destroysprite"] = new[] { "targets" },
                ["setspriteflip"] = new[] { "targets", "flipX", "flipY" },
                ["setsortingorder"] = new[] { "targets", "order" },
                ["setspritealpha"] = new[] { "targets", "alpha" },
                ["setspritecolor"] = new[] { "targets", "hexColor" },

                ["setgravity"] = new[] { "gx", "gy" },
                ["settimescale"] = new[] { "scale" },
                ["settile"] = new[] { "x", "y", "tileId" },
                ["removetile"] = new[] { "x", "y" },
                ["hastile"] = new[] { "x", "y" },

                ["showbanner"] = new[] { "text", "duration", "color" },
                ["add"] = new[] { "item" },
                ["removeat"] = new[] { "index" },
                ["clear"] = System.Array.Empty<string>(),
                ["showtimer"] = new[] { "seconds", "label" },

                ["background.clear"] = Array.Empty<string>(),
                ["background.reset"] = Array.Empty<string>(),
                ["background.hide"] = new[] { "layerIndex" },
                ["background.show"] = new[] { "layerIndex" },
                ["background.setcolor"] = new[] { "layerIndex", "hexColor" },
                ["background.setspeed"] = new[] { "layerIndex", "speed" },
                ["background.setoffset"] = new[] { "layerIndex", "offsetY" },
                ["background.setalpha"] = new[] { "layerIndex", "alpha" },

                ["camera.shake"] = new[] { "intensity", "duration" },
                ["camera.zoom"] = new[] { "size", "duration", "ease" },
                ["camera.panto"] = new[] { "x", "y", "duration", "ease" },
                ["camera.follow"] = new[] { "target" },
                ["camera.unfollow"] = Array.Empty<string>(),
                ["camera.fade"] = new[] { "alpha", "duration", "color" },

                ["world.setgravity"] = new[] { "gx", "gy" },
                ["world.settimescale"] = new[] { "scale" },
                ["world.settile"] = new[] { "x", "y", "tileId" },
                ["world.removetile"] = new[] { "x", "y" },
                ["world.hastile"] = new[] { "x", "y" },

                ["ui.showbanner"] = new[] { "text", "duration", "color" },
                ["ui.showtimer"] = new[] { "seconds", "label" },



                ["sendtoserver"] = new[] { "eventName", "...args" },
                ["sendtoclient"] = new[] { "player", "eventName", "...args" },
                ["sendtoallclients"] = new[] { "eventName", "...args" },
                ["onclientevent"] = new[] { "eventName", "player", "...args" },
                ["onserverevent"] = new[] { "eventName", "...args" },
                ["network.sendtoserver"] = new[] { "eventName", "...args" },
                ["network.sendtoclient"] = new[] { "player", "eventName", "...args" },
                ["network.sendtoallclients"] = new[] { "eventName", "...args" },
                ["network.onclientevent"] = new[] { "eventName", "player", "...args" },
                ["network.onserverevent"] = new[] { "eventName", "...args" },
            };

        public readonly struct ParameterInlay
        {
            public readonly int Position;
            public readonly string Label;

            public ParameterInlay(int position, string label)
            {
                Position = position;
                Label = label;
            }
        }

        const string InlayColor = "#8A8A8A";

        /// <summary>
        /// Relative script path of the buffer currently being edited (for usings / link validate).
        /// </summary>
        public static string ActiveFilePath { get; set; } = string.Empty;

        public static bool TryValidate(string source, out LevelScriptDiagnostic diagnostic)
        {
            diagnostic = LevelScriptDiagnostic.None;
            string path = string.IsNullOrEmpty(ActiveFilePath)
                ? WorldScriptProjectService.EntryFileName
                : WorldScriptProjectService.NormalizeRelativePath(ActiveFilePath);

            if (WorldScriptProjectService.HasProject)
            {
                try
                {
                    LevelScriptLinkedProgram linked = LevelScriptModuleLinker.Link(path, source ?? string.Empty);
                    LevelScriptSemanticAnalyzer.Analyze(linked, source ?? string.Empty);
                    return true;
                }
                catch (LevelScriptParseException ex)
                {
                    diagnostic = LevelScriptDiagnostic.FromException(ex, source ?? string.Empty);
                    return false;
                }
                catch (Exception ex)
                {
                    diagnostic = new LevelScriptDiagnostic
                    {
                        HasError = true,
                        Line = 1,
                        Column = 1,
                        StartIndex = 0,
                        EndIndex = Math.Min(1, (source ?? string.Empty).Length),
                        Message = $"Line 1, column 1: {ex.Message}"
                    };
                    return false;
                }
            }

            try
            {
                LevelScriptProgram program = string.IsNullOrWhiteSpace(source)
                    ? new LevelScriptProgram()
                    : LevelScriptParser.Parse(source ?? string.Empty, path);
                var linked = new LevelScriptLinkedProgram { Entry = program };
                linked.ModulesInInitOrder.Add(program);
                for (int i = 0; i < program.Functions.Count; i++)
                {
                    FunctionDefinitionNode function = program.Functions[i];
                    linked.Functions[function.Name] = function;
                    linked.Symbols[function.Name] = new LevelScriptSymbolInfo
                    {
                        Name = function.Name,
                        ModulePath = program.SourcePath,
                        IsPublic = function.IsPublic,
                        Kind = LevelScriptSymbolKind.Function
                    };
                }

                LevelScriptSemanticAnalyzer.Analyze(linked, source ?? string.Empty);
                return true;
            }
            catch (LevelScriptParseException ex)
            {
                diagnostic = LevelScriptDiagnostic.FromException(ex, source ?? string.Empty);
                return false;
            }
            catch (Exception ex)
            {
                diagnostic = new LevelScriptDiagnostic
                {
                    HasError = true,
                    Line = 1,
                    Column = 1,
                    StartIndex = 0,
                    EndIndex = Math.Min(1, (source ?? string.Empty).Length),
                    Message = $"Line 1, column 1: {ex.Message}"
                };
                return false;
            }
        }

        public static IReadOnlyList<string> CollectUserSymbols(string source)
        {
            var symbols = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(source))
                return Array.Empty<string>();

            AddMatches(source, DeclarationRegex, symbols);
            AddMatches(source, FunctionRegex, symbols);
            AddMatches(source, ForInitRegex, symbols);
            CollectImportedPublicSymbols(source, ActiveFilePath, symbols);
            return new List<string>(symbols);
        }

        static void CollectImportedPublicSymbols(string source, string filePath, HashSet<string> symbols)
        {
            if (string.IsNullOrEmpty(source) || !WorldScriptProjectService.HasProject)
                return;

            try
            {
                string path = string.IsNullOrEmpty(filePath)
                    ? WorldScriptProjectService.EntryFileName
                    : WorldScriptProjectService.NormalizeRelativePath(filePath);
                LevelScriptProgram program = LevelScriptParser.Parse(source, path);
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < program.Usings.Count; i++)
                    CollectPublicSymbolsFromUsing(program.Usings[i].Path, visited, symbols);
            }
            catch
            {
                // Incomplete buffer while typing — ignore import completion failures.
            }
        }

        static void CollectPublicSymbolsFromUsing(string rawPath, HashSet<string> visited, HashSet<string> symbols)
        {
            if (!WorldScriptProjectService.TryValidateRelativePath(rawPath, out string dependencyPath, out _))
                return;

            if (!visited.Add(dependencyPath))
                return;

            if (!WorldScriptProjectService.TryReadFile(dependencyPath, out string content, out _))
                return;

            try
            {
                LevelScriptProgram dependency = LevelScriptParser.Parse(content, dependencyPath);
                for (int i = 0; i < dependency.Functions.Count; i++)
                {
                    FunctionDefinitionNode function = dependency.Functions[i];
                    if (function.IsPublic)
                        symbols.Add(function.Name);
                }

                for (int i = 0; i < dependency.MainBody.Count; i++)
                {
                    if (TryGetPublicDeclarationName(dependency.MainBody[i], out string name))
                        symbols.Add(name);
                }

                for (int i = 0; i < dependency.Usings.Count; i++)
                    CollectPublicSymbolsFromUsing(dependency.Usings[i].Path, visited, symbols);
            }
            catch
            {
                // Ignore broken dependency while typing.
            }
        }

        static bool TryGetPublicDeclarationName(LevelScriptNode node, out string name)
        {
            switch (node)
            {
                case IntDeclarationNode d when d.IsPublic:
                    name = d.Name;
                    return true;
                case BoolDeclarationNode d when d.IsPublic:
                    name = d.Name;
                    return true;
                case FloatDeclarationNode d when d.IsPublic:
                    name = d.Name;
                    return true;
                case StringDeclarationNode d when d.IsPublic:
                    name = d.Name;
                    return true;
                case GameObjectDeclarationNode d when d.IsPublic:
                    name = d.Name;
                    return true;
                default:
                    name = null;
                    return false;
            }
        }

        static void AddMatches(string source, Regex regex, HashSet<string> symbols)
        {
            MatchCollection matches = regex.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (match.Groups.Count > 1)
                    symbols.Add(match.Groups[1].Value);
            }
        }


        static readonly string[] BackgroundMembers =
        {
            "clear()", "reset()", "hide(layer)", "show(layer)",
            "setColor(layer, hex)", "setSpeed(layer, speed)", "setOffset(layer, offset)", "setAlpha(layer, alpha)"
        };

        static readonly string[] CameraMembers =
        {
            "shake(intensity, duration)", "zoom(size, duration, ease)",
            "panTo(x, y, duration, ease)", "follow(target)", "unfollow()", "fade(alpha, duration, color)"
        };

        static readonly string[] WorldMembers =
        {
            "setGravity(gx, gy)", "setTimeScale(scale)",
            "setTile(x, y, tileId)", "removeTile(x, y)", "hasTile(x, y)"
        };

        static readonly string[] UiMembers =
        {
            "showBanner(text, duration, color)", "showTimer(seconds, label)"
        };



        static readonly string[] NetworkMembers =
        {
            "sendToServer(eventName, ...args)",
            "sendToClient(player, eventName, ...args)",
            "sendToAllClients(eventName, ...args)",
            "onClientEvent(eventName, player, ...args)",
            "onServerEvent(eventName, ...args)"
        };

        static readonly string[] GameObjectMembers =
        {
            "x", "y", "name",
            "OnTriggerEnter(GameObject player)", "OnTriggerStay(GameObject player)", "OnTriggerExit(GameObject player)"
        };
        static readonly string[] ComponentMembers =
        {
            "velocity", "gravityScale", "Play()", "Stop()", "Pause()", "Clear()"
        };
        static readonly string[] ListMembers = { "add()", "removeAt()", "clear()", "count", "length" };

        static readonly string[] EasingLiterals =
        {
            "\"linear\"", "\"easeIn\"", "\"easeOut\"", "\"easeInOut\""
        };

        static readonly string[] ComponentLiterals =
        {
            "\"Particle\"", "\"Rigidbody2D\"", "\"Hitbox\"", "\"Sprite\""
        };

        public static List<string> GetCompletionCandidates(string source, int caret)
        {
            var candidates = new List<string>(16);
            if (string.IsNullOrEmpty(source) || caret <= 0)
                return candidates;

            caret = Math.Clamp(caret, 0, source.Length);

            if (IsInsideString(source, caret))
            {
                int quoteStart = source.LastIndexOf('"', caret - 1);
                string strPrefix = quoteStart >= 0 ? source.Substring(quoteStart + 1, caret - quoteStart - 1) : "";
                foreach (string lit in EasingLiterals)
                {
                    string unquoted = lit.Trim('"');
                    if (unquoted.StartsWith(strPrefix, StringComparison.OrdinalIgnoreCase))
                        candidates.Add(unquoted);
                }
                foreach (string comp in ComponentLiterals)
                {
                    string unquoted = comp.Trim('"');
                    if (unquoted.StartsWith(strPrefix, StringComparison.OrdinalIgnoreCase) && !candidates.Contains(unquoted))
                        candidates.Add(unquoted);
                }
                return candidates;
            }

            string prefix = GetWordPrefix(source, caret);
            int prefixStart = caret - prefix.Length;

            int dotIndex = prefixStart - 1;
            while (dotIndex >= 0 && char.IsWhiteSpace(source[dotIndex]))
                dotIndex--;

            if (dotIndex >= 0 && source[dotIndex] == '.')
            {
                int receiverEnd = dotIndex;
                int receiverStart = receiverEnd - 1;
                if (receiverStart >= 0 && source[receiverStart] == ')')
                {
                    while (receiverStart > 0 && source[receiverStart] != '(')
                        receiverStart--;
                    while (receiverStart > 0 && (char.IsLetterOrDigit(source[receiverStart - 1]) || source[receiverStart - 1] == '_'))
                        receiverStart--;
                }
                else
                {
                    while (receiverStart >= 0 && (char.IsLetterOrDigit(source[receiverStart]) || source[receiverStart] == '_'))
                        receiverStart--;
                    receiverStart++;
                }

                string receiver = source.Substring(receiverStart, receiverEnd - receiverStart).Trim();
                string[] memberSet = null;

                if (string.Equals(receiver, "Background", StringComparison.OrdinalIgnoreCase))
                    memberSet = BackgroundMembers;
                else if (string.Equals(receiver, "Camera", StringComparison.OrdinalIgnoreCase))
                    memberSet = CameraMembers;
                else if (string.Equals(receiver, "World", StringComparison.OrdinalIgnoreCase))
                    memberSet = WorldMembers;
                else if (string.Equals(receiver, "UI", StringComparison.OrdinalIgnoreCase))
                    memberSet = UiMembers;

                else if (string.Equals(receiver, "Network", StringComparison.OrdinalIgnoreCase))
                    memberSet = NetworkMembers;
                else if (string.Equals(receiver, "localPlayer", StringComparison.OrdinalIgnoreCase))
                    memberSet = GameObjectMembers;
                else if (receiver.StartsWith("Group", StringComparison.OrdinalIgnoreCase) ||
                         receiver.StartsWith("player", StringComparison.OrdinalIgnoreCase) ||
                         receiver.StartsWith("target", StringComparison.OrdinalIgnoreCase) ||
                         receiver.StartsWith("Scene.", StringComparison.OrdinalIgnoreCase))
                    memberSet = GameObjectMembers;
                else
                {
                    Match declMatch = Regex.Match(
                        source,
                        @"\b(?:(?:public|private)\s+)?(GameObject|Component|list|int\[\]|float\[\]|bool\[\]|string\[\])\s+" + Regex.Escape(receiver) + @"\b",
                        RegexOptions.IgnoreCase);
                    if (declMatch.Success)
                    {
                        string typeName = declMatch.Groups[1].Value;
                        if (string.Equals(typeName, "GameObject", StringComparison.OrdinalIgnoreCase))
                            memberSet = GameObjectMembers;
                        else if (string.Equals(typeName, "Component", StringComparison.OrdinalIgnoreCase))
                            memberSet = ComponentMembers;
                        else
                            memberSet = ListMembers;
                    }
                    else
                    {
                        memberSet = ListMembers;
                    }
                }

                if (memberSet != null)
                {
                    for (int i = 0; i < memberSet.Length; i++)
                    {
                        string member = memberSet[i];
                        string memberName = member.Contains('(') ? member.Substring(0, member.IndexOf('(')) : member;
                        if (string.IsNullOrEmpty(prefix) || memberName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            candidates.Add(member);
                    }
                    return candidates;
                }
            }

            if (string.IsNullOrEmpty(prefix))
                return candidates;

            var matches = new List<string>(16);
            for (int i = 0; i < CompletionKeywords.Length; i++)
            {
                string kw = CompletionKeywords[i];
                if (kw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !kw.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    matches.Add(kw);
            }

            IReadOnlyList<string> userSymbols = CollectUserSymbols(source);
            for (int i = 0; i < userSymbols.Count; i++)
            {
                string sym = userSymbols[i];
                if (sym.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !sym.Equals(prefix, StringComparison.OrdinalIgnoreCase) && !matches.Contains(sym))
                    matches.Add(sym);
            }

            matches.Sort((a, b) =>
            {
                bool aExact = a.StartsWith(prefix, StringComparison.Ordinal);
                bool bExact = b.StartsWith(prefix, StringComparison.Ordinal);
                if (aExact != bExact) return aExact ? -1 : 1;
                int lenCmp = a.Length.CompareTo(b.Length);
                return lenCmp != 0 ? lenCmp : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            return matches;
        }

        public static bool TryGetCompletion(string source, int caret, out string ghostSuffix)
        {
            return TryGetCompletion(source, caret, 0, out ghostSuffix, out _);
        }

        public static bool TryGetCompletion(string source, int caret, int candidateOffset, out string ghostSuffix, out List<string> allCandidates)
        {
            ghostSuffix = string.Empty;
            allCandidates = GetCompletionCandidates(source, caret);
            if (allCandidates == null || allCandidates.Count == 0)
                return false;

            int index = Math.Clamp(candidateOffset, 0, allCandidates.Count - 1);
            string chosen = allCandidates[index];
            string prefix = GetWordPrefix(source, caret);

            string insert = chosen.Contains('(') ? chosen.Substring(0, chosen.IndexOf('(')) : chosen;
            if (insert.Length > prefix.Length)
                ghostSuffix = insert.Substring(prefix.Length);
            else
                ghostSuffix = chosen.Length > prefix.Length ? chosen.Substring(prefix.Length) : string.Empty;

            ghostSuffix = NormalizeGhostSuffix(source, caret, ghostSuffix);
            return ghostSuffix.Length > 0;
        }


        public static string NormalizeGhostSuffix(string source, int caret, string ghostSuffix)
        {
            if (string.IsNullOrEmpty(ghostSuffix))
                return string.Empty;

            source ??= string.Empty;
            caret = Math.Clamp(caret, 0, source.Length);
            string after = caret < source.Length ? source.Substring(caret) : string.Empty;

            if (after.Length == 0)
                return ghostSuffix;

            if (after.StartsWith(ghostSuffix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (ghostSuffix.StartsWith(after, StringComparison.OrdinalIgnoreCase))
                return ghostSuffix.Substring(after.Length);

            return ghostSuffix;
        }

        static void CompareCandidates(string prefix, IEnumerable<string> candidates, ref string best)
        {
            foreach (string candidate in candidates)
                CompareCandidate(prefix, candidate, ref best);
        }

        static void CompareCandidate(string prefix, string candidate, ref string best)
        {
            if (candidate.Length <= prefix.Length)
                return;

            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return;

            if (candidate.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                return;

            if (best == null || candidate.Length > best.Length)
                best = candidate;
        }

        public static string GetWordPrefix(string source, int caret)
        {
            if (string.IsNullOrEmpty(source) || caret <= 0)
                return string.Empty;

            caret = Math.Clamp(caret, 0, source.Length);
            int start = caret;
            while (start > 0)
            {
                char c = source[start - 1];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    break;
                start--;
            }

            return source.Substring(start, caret - start);
        }

        public static string BuildOverlayRichText(
            string source,
            int caret,
            string ghostSuffix,
            in LevelScriptDiagnostic diagnostic)
        {
            source ??= string.Empty;
            caret = Math.Clamp(caret, 0, source.Length);
            ghostSuffix = NormalizeGhostSuffix(source, caret, ghostSuffix ?? string.Empty);
            var sb = new StringBuilder(source.Length * 2 + (ghostSuffix?.Length ?? 0) + 32);
            bool errorOpen = false;

            LevelScriptDiagnostic safeDiag = diagnostic;
            if (safeDiag.HasError)
            {
                safeDiag.StartIndex = Math.Clamp(safeDiag.StartIndex, 0, source.Length);
                safeDiag.EndIndex = Math.Clamp(safeDiag.EndIndex, safeDiag.StartIndex, source.Length);
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (i == caret)
                {
                    AppendGhost(sb, ghostSuffix);
                    int skip = CountGhostRenderedOverlap(source, caret, ghostSuffix);
                    if (skip > 0)
                    {
                        i = caret + skip - 1;
                        continue;
                    }
                }

                if (safeDiag.HasError && !errorOpen && i == safeDiag.StartIndex)
                {
                    sb.Append("<color=#F44747><u>");
                    errorOpen = true;
                }

                if (TryEmitComment(source, ref i, sb, safeDiag, ref errorOpen))
                    continue;

                if (TryEmitString(source, ref i, sb, safeDiag, ref errorOpen))
                    continue;

                if (char.IsLetter(source[i]) || source[i] == '_')
                {
                    int start = i;
                    i++;
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                        i++;

                    AppendColored(sb, source.Substring(start, i - start), ClassifyWordColor(source.Substring(start, i - start)));
                    i--;
                }
                else if (char.IsDigit(source[i]) || (source[i] == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1])))
                {
                    int start = i;
                    i++;
                    while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                        i++;

                    AppendColored(sb, source.Substring(start, i - start), "#B5CEA8");
                    i--;
                }
                else
                {
                    AppendEscaped(sb, source[i].ToString());
                }

                if (safeDiag.HasError && errorOpen && i + 1 >= safeDiag.EndIndex)
                {
                    sb.Append("</u></color>");
                    errorOpen = false;
                }
            }

            if (caret == source.Length)
                AppendGhost(sb, ghostSuffix);

            if (errorOpen)
                sb.Append("</u></color>");

            return sb.ToString();
        }

        static int CountGhostRenderedOverlap(string source, int caret, string ghostSuffix)
        {
            if (string.IsNullOrEmpty(ghostSuffix) || caret >= source.Length)
                return 0;

            int max = Math.Min(ghostSuffix.Length, source.Length - caret);
            int skip = 0;
            while (skip < max &&
                   char.ToLowerInvariant(source[caret + skip]) ==
                   char.ToLowerInvariant(ghostSuffix[skip]))
                skip++;

            return skip;
        }

        static bool TryEmitComment(string source, ref int i, StringBuilder sb, in LevelScriptDiagnostic diagnostic, ref bool errorOpen)
        {
            if (i + 1 >= source.Length || source[i] != '/')
                return false;

            if (source[i + 1] == '/')
            {
                int start = i;
                while (i < source.Length && source[i] != '\n')
                    i++;

                AppendColored(sb, source.Substring(start, i - start), "#6A9955");
                if (diagnostic.HasError && errorOpen && i >= diagnostic.EndIndex)
                {
                    sb.Append("</u></color>");
                    errorOpen = false;
                }

                i--;
                return true;
            }

            if (source[i + 1] == '*')
            {
                int start = i;
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                    i++;
                if (i + 1 < source.Length)
                    i += 2;
                else
                    i = source.Length;

                AppendColored(sb, source.Substring(start, i - start), "#6A9955");
                if (diagnostic.HasError && errorOpen && i >= diagnostic.EndIndex)
                {
                    sb.Append("</u></color>");
                    errorOpen = false;
                }

                i--;
                return true;
            }

            return false;
        }

        static bool TryEmitString(string source, ref int i, StringBuilder sb, in LevelScriptDiagnostic diagnostic, ref bool errorOpen)
        {
            if (source[i] != '"')
                return false;

            int start = i;
            i++;
            while (i < source.Length && source[i] != '"')
            {
                if (source[i] == '\\' && i + 1 < source.Length)
                    i += 2;
                else
                    i++;
            }

            if (i < source.Length)
                i++;

            AppendColored(sb, source.Substring(start, i - start), "#CE9178");
            if (diagnostic.HasError && errorOpen && i >= diagnostic.EndIndex)
            {
                sb.Append("</u></color>");
                errorOpen = false;
            }

            i--;
            return true;
        }

        static string ClassifyWordColor(string word)
        {
            if (TypeKeywords.Contains(word))
                return "#4EC9B0";
            if (ControlKeywords.Contains(word) || ActionKeywords.Contains(word) || HandlerKeywords.Contains(word))
                return "#569CD6";
            if (LiteralKeywords.Contains(word) || EaseKeywords.Contains(word))
                return "#569CD6";
            if (ObjectKeywords.Contains(word))
                return "#4EC9B0";
            return "#D4D4D4";
        }

        public static List<ParameterInlay> CollectParameterInlays(string source, int caret)
        {
            var inlays = new List<ParameterInlay>(8);
            if (string.IsNullOrEmpty(source))
                return inlays;

            CollectInlaysForActiveCall(source, caret, inlays);
            return inlays;
        }

        public static bool TryGetActiveSignatureHelp(string source, int caret, out string help)
        {
            help = null;
            if (string.IsNullOrEmpty(source) || caret < 0)
                return false;

            caret = Math.Clamp(caret, 0, source.Length);
            string bestName = null;
            string[] bestParams = null;
            int bestSpan = int.MaxValue;
            int bestNameEnd = -1;
            int bestCloseParen = -1;

            for (int i = 0; i < source.Length; i++)
            {
                if (IsCommentStart(source, i))
                {
                    i = SkipCommentForward(source, i);
                    continue;
                }

                if (source[i] == '"')
                {
                    i = SkipStringForward(source, i);
                    continue;
                }

                if (!char.IsLetter(source[i]) && source[i] != '_')
                    continue;

                int nameStart = i;
                i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                    i++;

                string functionName = source.Substring(nameStart, i - nameStart);
                int prefixCheck = nameStart - 1;
                while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                if (prefixCheck >= 0 && source[prefixCheck] == '.')
                {
                    int nsEnd = prefixCheck;
                    prefixCheck--;
                    while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                    int nsStart = prefixCheck;
                    while (nsStart >= 0 && (char.IsLetterOrDigit(source[nsStart]) || source[nsStart] == '_')) nsStart--;
                    nsStart++;
                    if (nsEnd > nsStart)
                    {
                        string ns = source.Substring(nsStart, nsEnd - nsStart).Trim();
                        functionName = ns + "." + functionName;
                    }
                }
                int nameEnd = i;
                while (nameEnd < source.Length && char.IsWhiteSpace(source[nameEnd]))
                    nameEnd++;

                if (nameEnd >= source.Length || source[nameEnd] != '(')
                {
                    i--;
                    continue;
                }

                if (!TryFindMatchingCloseParen(source, nameEnd, out int closeParen))
                    closeParen = source.Length;

                // Caret must be inside the argument list: after '(' and not past ')'.
                if (caret <= nameEnd || caret > closeParen)
                {
                    i = Math.Min(closeParen, source.Length - 1);
                    continue;
                }

                if (!TryResolveCallSignature(source, functionName, out string[] paramNames) ||
                    paramNames == null ||
                    paramNames.Length == 0)
                {
                    i = Math.Min(closeParen, source.Length - 1);
                    continue;
                }

                int span = closeParen - nameEnd;
                if (span < bestSpan)
                {
                    bestSpan = span;
                    bestName = functionName;
                    bestParams = paramNames;
                    bestNameEnd = nameEnd;
                    bestCloseParen = closeParen;
                }

                i = Math.Min(closeParen, source.Length - 1);
            }

            if (bestName == null || bestParams == null)
                return false;

            List<int> argStarts = SplitArgumentStarts(source, bestNameEnd + 1, bestCloseParen);
            int activeArg = ResolveActiveArgumentIndex(source, caret, bestNameEnd, bestCloseParen, argStarts, bestParams.Length);

            var sb = new StringBuilder(bestName.Length + bestParams.Length * 20 + 20);
            sb.Append(bestName).Append('(');
            for (int p = 0; p < bestParams.Length; p++)
            {
                if (p > 0)
                    sb.Append(", ");

                if (p == activeArg)
                    sb.Append("<b><color=#569CD6>").Append(bestParams[p]).Append("</color></b>");
                else
                    sb.Append(bestParams[p]);
            }

            sb.Append(')');
            help = sb.ToString();
            return true;
        }

        static int ResolveActiveArgumentIndex(
            string source,
            int caret,
            int nameEnd,
            int closeParen,
            List<int> argStarts,
            int paramCount)
        {
            if (paramCount <= 0)
                return 0;

            if (argStarts.Count == 0)
                return 0;

            int argIndex = 0;
            for (int a = 0; a < argStarts.Count; a++)
            {
                int argStart = argStarts[a];
                int argEnd = a + 1 < argStarts.Count ? argStarts[a + 1] : closeParen;
                argEnd = TrimArgEnd(source, argStart, argEnd);
                if (caret >= argStart && caret <= argEnd)
                {
                    argIndex = a;
                    break;
                }

                if (caret > argEnd)
                    argIndex = a + 1;
            }

            return Math.Clamp(argIndex, 0, paramCount - 1);
        }

        public static bool TryGetActiveParameterHint(string source, int caret, out string hint)
        {
            hint = null;
            if (string.IsNullOrEmpty(source) || caret < 0)
                return false;

            caret = Math.Clamp(caret, 0, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (IsCommentStart(source, i))
                {
                    i = SkipCommentForward(source, i);
                    continue;
                }

                if (source[i] == '"')
                {
                    i = SkipStringForward(source, i);
                    continue;
                }

                if (!char.IsLetter(source[i]) && source[i] != '_')
                    continue;

                int nameStart = i;
                i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                    i++;

                string functionName = source.Substring(nameStart, i - nameStart);
                int prefixCheck = nameStart - 1;
                while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                if (prefixCheck >= 0 && source[prefixCheck] == '.')
                {
                    int nsEnd = prefixCheck;
                    prefixCheck--;
                    while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                    int nsStart = prefixCheck;
                    while (nsStart >= 0 && (char.IsLetterOrDigit(source[nsStart]) || source[nsStart] == '_')) nsStart--;
                    nsStart++;
                    if (nsEnd > nsStart)
                    {
                        string ns = source.Substring(nsStart, nsEnd - nsStart).Trim();
                        functionName = ns + "." + functionName;
                    }
                }
                int nameEnd = i;
                while (nameEnd < source.Length && char.IsWhiteSpace(source[nameEnd]))
                    nameEnd++;

                if (nameEnd >= source.Length || source[nameEnd] != '(')
                {
                    i--;
                    continue;
                }

                if (!TryFindMatchingCloseParen(source, nameEnd, out int closeParen))
                {
                    i--;
                    continue;
                }

                if (caret < nameEnd || caret > closeParen)
                {
                    i = closeParen;
                    continue;
                }

                if (!TryResolveCallSignature(source, functionName, out string[] paramNames))
                {
                    i = closeParen;
                    continue;
                }

                List<int> argStarts = SplitArgumentStarts(source, nameEnd + 1, closeParen);
                int argIndex = ResolveActiveArgumentIndex(source, caret, nameEnd, closeParen, argStarts, paramNames.Length);
                argIndex = Math.Clamp(argIndex, 0, paramNames.Length - 1);
                hint = paramNames[argIndex];
                return !string.IsNullOrEmpty(hint);
            }

            return false;
        }

        static int TrimArgEnd(string source, int argStart, int argEnd)
        {
            while (argEnd > argStart && char.IsWhiteSpace(source[argEnd - 1]))
                argEnd--;
            return argEnd;
        }

        static void CollectInlaysForActiveCall(string source, int caret, List<ParameterInlay> inlays)
        {
            caret = Math.Clamp(caret, 0, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (IsCommentStart(source, i))
                {
                    i = SkipCommentForward(source, i);
                    continue;
                }

                if (source[i] == '"')
                {
                    i = SkipStringForward(source, i);
                    continue;
                }

                if (!char.IsLetter(source[i]) && source[i] != '_')
                    continue;

                int nameStart = i;
                i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                    i++;

                string functionName = source.Substring(nameStart, i - nameStart);
                int prefixCheck = nameStart - 1;
                while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                if (prefixCheck >= 0 && source[prefixCheck] == '.')
                {
                    int nsEnd = prefixCheck;
                    prefixCheck--;
                    while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                    int nsStart = prefixCheck;
                    while (nsStart >= 0 && (char.IsLetterOrDigit(source[nsStart]) || source[nsStart] == '_')) nsStart--;
                    nsStart++;
                    if (nsEnd > nsStart)
                    {
                        string ns = source.Substring(nsStart, nsEnd - nsStart).Trim();
                        functionName = ns + "." + functionName;
                    }
                }
                int nameEnd = i;
                while (nameEnd < source.Length && char.IsWhiteSpace(source[nameEnd]))
                    nameEnd++;

                if (nameEnd >= source.Length || source[nameEnd] != '(')
                {
                    i--;
                    continue;
                }

                if (!TryFindMatchingCloseParen(source, nameEnd, out int closeParen))
                {
                    i--;
                    continue;
                }

                if (caret < nameEnd || caret > closeParen)
                {
                    i = closeParen;
                    continue;
                }

                if (!TryResolveCallSignature(source, functionName, out string[] paramNames))
                {
                    i = closeParen;
                    continue;
                }

                List<int> argStarts = SplitArgumentStarts(source, nameEnd + 1, closeParen);
                if (argStarts.Count == 0)
                    argStarts.Add(nameEnd + 1);

                int visibleCount = Math.Min(paramNames.Length, Math.Max(argStarts.Count, 1));
                for (int argIndex = 0; argIndex < visibleCount; argIndex++)
                {
                    int argStart = argIndex < argStarts.Count ? argStarts[argIndex] : argStarts[argStarts.Count - 1];
                    if (ArgAlreadyHasLabel(source, argStart, paramNames[argIndex]))
                        continue;

                    inlays.Add(new ParameterInlay(argStart, paramNames[argIndex] + ": "));
                }

                return;
            }
        }

        static void CollectInlaysForAllCalls(string source, List<ParameterInlay> inlays)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (IsCommentStart(source, i))
                {
                    i = SkipCommentForward(source, i);
                    continue;
                }

                if (source[i] == '"')
                {
                    i = SkipStringForward(source, i);
                    continue;
                }

                if (!char.IsLetter(source[i]) && source[i] != '_')
                    continue;

                int nameStart = i;
                i++;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                    i++;

                string functionName = source.Substring(nameStart, i - nameStart);
                int prefixCheck = nameStart - 1;
                while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                if (prefixCheck >= 0 && source[prefixCheck] == '.')
                {
                    int nsEnd = prefixCheck;
                    prefixCheck--;
                    while (prefixCheck >= 0 && char.IsWhiteSpace(source[prefixCheck])) prefixCheck--;
                    int nsStart = prefixCheck;
                    while (nsStart >= 0 && (char.IsLetterOrDigit(source[nsStart]) || source[nsStart] == '_')) nsStart--;
                    nsStart++;
                    if (nsEnd > nsStart)
                    {
                        string ns = source.Substring(nsStart, nsEnd - nsStart).Trim();
                        functionName = ns + "." + functionName;
                    }
                }
                int nameEnd = i;
                while (nameEnd < source.Length && char.IsWhiteSpace(source[nameEnd]))
                    nameEnd++;

                if (nameEnd >= source.Length || source[nameEnd] != '(')
                {
                    i--;
                    continue;
                }

                if (!TryFindMatchingCloseParen(source, nameEnd, out int closeParen))
                {
                    i--;
                    continue;
                }

                if (!TryResolveCallSignature(source, functionName, out string[] paramNames))
                {
                    i = closeParen;
                    continue;
                }

                List<int> argStarts = SplitArgumentStarts(source, nameEnd + 1, closeParen);
                for (int argIndex = 0; argIndex < argStarts.Count && argIndex < paramNames.Length; argIndex++)
                {
                    int argStart = argStarts[argIndex];
                    if (ArgAlreadyHasLabel(source, argStart, paramNames[argIndex]))
                        continue;

                    inlays.Add(new ParameterInlay(argStart, paramNames[argIndex] + ": "));
                }

                i = closeParen;
            }
        }

        static int SkipStringForward(string source, int index)
        {
            if (index >= source.Length || source[index] != '"')
                return index;

            int i = index + 1;
            while (i < source.Length)
            {
                if (source[i] == '"' && !IsEscaped(source, i))
                    return i;

                if (source[i] == '\\' && i + 1 < source.Length)
                    i += 2;
                else
                    i++;
            }

            return source.Length - 1;
        }

        static bool TryFindMatchingCloseParen(string source, int openParen, out int closeParen)
        {
            closeParen = -1;
            int depth = 0;
            for (int i = openParen; i < source.Length; i++)
            {
                if (IsCommentStart(source, i))
                {
                    i = SkipCommentForward(source, i);
                    continue;
                }

                if (IsInsideString(source, i))
                    continue;

                if (source[i] == '(')
                    depth++;
                else if (source[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeParen = i;
                        return true;
                    }
                }
            }

            return false;
        }

        static bool TryReadIdentifierBefore(string source, int index, out string identifier)
        {
            identifier = string.Empty;
            int end = index;
            while (end > 0 && char.IsWhiteSpace(source[end - 1]))
                end--;

            int start = end;
            while (start > 0)
            {
                char c = source[start - 1];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    break;
                start--;
            }

            if (start == end)
                return false;

            identifier = source.Substring(start, end - start);
            return true;
        }

        static bool TryResolveCallSignature(string source, string functionName, out string[] paramNames)
        {
            paramNames = null;
            if (string.IsNullOrEmpty(functionName))
                return false;

            if (BuiltinCallSignatures.TryGetValue(functionName, out paramNames))
                return true;

            Dictionary<string, string[]> userFunctions = BuildUserFunctionSignatures(source);
            return userFunctions.TryGetValue(functionName, out paramNames);
        }

        static Dictionary<string, string[]> BuildUserFunctionSignatures(string source)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(source))
                return result;

            AddUserFunctionSignaturesFromSource(source, result);
            if (!WorldScriptProjectService.HasProject)
                return result;

            try
            {
                string path = string.IsNullOrEmpty(ActiveFilePath)
                    ? WorldScriptProjectService.EntryFileName
                    : WorldScriptProjectService.NormalizeRelativePath(ActiveFilePath);
                LevelScriptProgram program = LevelScriptParser.Parse(source, path);
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < program.Usings.Count; i++)
                    AddPublicFunctionSignaturesFromUsing(program.Usings[i].Path, visited, result);
            }
            catch
            {
                // Incomplete buffer while typing.
            }

            return result;
        }

        static void AddUserFunctionSignaturesFromSource(string source, Dictionary<string, string[]> result)
        {
            MatchCollection matches = UserFunctionDefRegex.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (match.Groups.Count < 3)
                    continue;

                string name = match.Groups[1].Value;
                string paramList = match.Groups[2].Value;
                var names = new List<string>(4);
                MatchCollection paramMatches = UserFunctionParamRegex.Matches(paramList);
                for (int p = 0; p < paramMatches.Count; p++)
                {
                    if (paramMatches[p].Groups.Count > 1)
                        names.Add(paramMatches[p].Groups[1].Value);
                }

                result[name] = names.ToArray();
            }
        }

        static void AddPublicFunctionSignaturesFromUsing(
            string rawPath,
            HashSet<string> visited,
            Dictionary<string, string[]> result)
        {
            if (!WorldScriptProjectService.TryValidateRelativePath(rawPath, out string dependencyPath, out _))
                return;

            if (!visited.Add(dependencyPath))
                return;

            if (!WorldScriptProjectService.TryReadFile(dependencyPath, out string content, out _))
                return;

            try
            {
                LevelScriptProgram dependency = LevelScriptParser.Parse(content, dependencyPath);
                for (int i = 0; i < dependency.Functions.Count; i++)
                {
                    FunctionDefinitionNode function = dependency.Functions[i];
                    if (!function.IsPublic)
                        continue;

                    var names = new List<string>(function.Parameters.Count);
                    for (int p = 0; p < function.Parameters.Count; p++)
                        names.Add(function.Parameters[p].Name);

                    result[function.Name] = names.ToArray();
                }

                for (int i = 0; i < dependency.Usings.Count; i++)
                    AddPublicFunctionSignaturesFromUsing(dependency.Usings[i].Path, visited, result);
            }
            catch
            {
                // Ignore broken dependency while typing.
            }
        }

        static List<int> SplitArgumentStarts(string source, int start, int end)
        {
            var starts = new List<int>(4);
            start = Math.Clamp(start, 0, source.Length);
            end = Math.Clamp(end, 0, source.Length);
            if (start >= end)
            {
                starts.Add(start);
                return starts;
            }

            int argStart = start;
            int depth = 0;
            bool inString = false;

            for (int i = start; i < end; i++)
            {
                if (!inString && IsCommentStart(source, i))
                {
                    i = SkipCommentForward(source, i);
                    if (i >= end)
                        break;
                    continue;
                }

                char c = source[i];
                if (c == '"' && !IsEscaped(source, i))
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '(' || c == '{' || c == '[')
                    depth++;
                else if (c == ')' || c == '}' || c == ']')
                    depth = Math.Max(0, depth - 1);
                else if (c == ',' && depth == 0)
                {
                    TryAddArgumentStart(source, start, end, argStart, i, starts);
                    argStart = i + 1;
                }
            }

            TryAddArgumentStart(source, start, end, argStart, end, starts);
            return starts;
        }

        static void TryAddArgumentStart(
            string source,
            int rangeStart,
            int rangeEnd,
            int argStart,
            int argEnd,
            List<int> starts)
        {
            argStart = Math.Clamp(argStart, rangeStart, rangeEnd);
            argEnd = Math.Clamp(argEnd, rangeStart, rangeEnd);
            int trimmed = argStart;
            while (trimmed < argEnd && char.IsWhiteSpace(source[trimmed]))
                trimmed++;

            starts.Add(trimmed < argEnd ? trimmed : argStart);
        }

        static bool ArgAlreadyHasLabel(string source, int argStart, string paramName)
        {
            if (string.IsNullOrEmpty(source) || argStart < 0 || argStart >= source.Length)
                return false;

            int i = argStart;
            while (i < source.Length && char.IsWhiteSpace(source[i]))
                i++;

            int labelStart = i;
            while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                i++;

            if (i >= source.Length || source[i] != ':')
                return false;

            string label = source.Substring(labelStart, i - labelStart);
            return label.Equals(paramName, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsCommentStart(string source, int index) =>
            index + 1 < source.Length && source[index] == '/' && (source[index + 1] == '/' || source[index + 1] == '*');

        static int SkipCommentForward(string source, int index)
        {
            if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '*')
            {
                int k = index + 2;
                while (k + 1 < source.Length && !(source[k] == '*' && source[k + 1] == '/'))
                    k++;
                return k + 1 < source.Length ? k + 2 : source.Length;
            }

            int i = index + 2;
            while (i < source.Length && source[i] != '\n')
                i++;
            return i;
        }

        static int SkipCommentBackward(string source, int index)
        {
            int lineStart = source.LastIndexOf('\n', Math.Max(0, index - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            for (int i = lineStart; i < index; i++)
            {
                if (IsCommentStart(source, i))
                    return lineStart - 1;
            }

            return index;
        }

        static bool IsInsideString(string source, int index)
        {
            bool inString = false;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '"' && !IsEscaped(source, i))
                    inString = !inString;
            }

            return inString;
        }

        static bool IsEscaped(string source, int index) =>
            index > 0 && source[index - 1] == '\\';

        static void AppendParameterInlay(StringBuilder sb, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;

            sb.Append("<color=").Append(InlayColor).Append("><i>");
            AppendEscaped(sb, label);
            sb.Append("</i></color>");
        }

        static void AppendGhost(StringBuilder sb, string ghostSuffix)
        {
            if (string.IsNullOrEmpty(ghostSuffix))
                return;

            sb.Append("<color=#808080A0>");
            AppendEscaped(sb, ghostSuffix);
            sb.Append("</color>");
        }

        static void AppendColored(StringBuilder sb, string text, string colorHex)
        {
            sb.Append("<color=").Append(colorHex).Append('>');
            AppendEscaped(sb, text);
            sb.Append("</color>");
        }

        static void AppendEscaped(StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    // TMP does not decode HTML entities (&quot; etc.) — they render literally
                    // and break caret alignment with the transparent input layer.
                    case '<':
                        sb.Append("<noparse><</noparse>");
                        break;
                    case '>':
                        sb.Append("<noparse>></noparse>");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }
    }
}
