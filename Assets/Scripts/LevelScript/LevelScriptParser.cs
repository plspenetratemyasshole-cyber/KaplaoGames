using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KaplaoGames.LevelScript
{
    public sealed class LevelScriptParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public int StartIndex { get; }
        public string FilePath { get; }

        public LevelScriptParseException(string message, string filePath = null)
            : base(FormatMessage(1, 1, message, filePath, false))
        {
            Line = 1;
            Column = 1;
            StartIndex = -1;
            FilePath = filePath ?? string.Empty;
        }

        public LevelScriptParseException(int line, int column, int startIndex, string message, string filePath = null)
            : base(FormatMessage(line, column, message, filePath, true))
        {
            Line = line;
            Column = column;
            StartIndex = startIndex;
            FilePath = filePath ?? string.Empty;
        }

        static string FormatMessage(int line, int column, string message, string filePath, bool hasLocation)
        {
            bool isImported = !string.IsNullOrEmpty(filePath) &&
                              !string.Equals(filePath, "main.ks", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(filePath, "main.lks", StringComparison.OrdinalIgnoreCase);

            if (isImported)
            {
                if (hasLocation)
                    return $"[{filePath}] Line {line}, column {column}: {message}";
                return $"[{filePath}] {message}";
            }

            if (hasLocation)
                return $"Line {line}, column {column}: {message}";
            return message;
        }
    }

    public static class LevelScriptParser
    {
        enum TokenType
        {
            Spawn,
            While,
            For,
            If,
            Else,
            Function,
            Int,
            Bool,
            Float,
            String,
            GameObject,
            Component,
            Scene,
            GetComponent,
            True,
            False,
            Nil,
            Move,
            Animate,
            SpawnExecutor,
            Rotate,
            Wait,
            Goto,
            ScaleTo,
            Visibility,
            Transparency,
            PlaySound,
            SpawnPoint,
            SpawnPoints,
            Hitbox,
            RotateTo,
            Instance,
            SetTexture,
            SetColor,
            Destroy,
            OnTriggerEnter,
            OnTriggerStay,
            OnTriggerExit,
            OnPlayerAdded,
            OnClientEvent,
            OnServerEvent,
            SendToServer,
            SendToClient,
            SendToAllClients,
            LocalPlayer,
            Network,
            List,
            LeftBracket,
            RightBracket,
            Group,
            Public,
            Private,
            Using,
            Return,
            Break,
            Continue,
            Dot,
            Identifier,
            StringLiteral,
            Number,
            LeftParen,
            RightParen,
            LeftBrace,
            RightBrace,
            Comma,
            Semicolon,
            Plus,
            Minus,
            Star,
            Slash,
            Percent,
            Equal,
            Less,
            LessOrEqual,
            Greater,
            GreaterOrEqual,
            EqualEqual,
            BangEqual,
            Bang,
            AmpAmp,
            PipePipe,
            PlusPlus,
            MinusMinus,
            PlusEqual,
            MinusEqual,
            StarEqual,
            SlashEqual,
            PercentEqual,
            End
        }

        readonly struct Token
        {
            public readonly TokenType Type;
            public readonly string Text;
            public readonly int StartIndex;

            public Token(TokenType type, string text, int startIndex = -1)
            {
                Type = type;
                Text = text;
                StartIndex = startIndex;
            }

            public int Length => string.IsNullOrEmpty(Text) ? 0 : Text.Length;
        }

        private sealed class ParserState
        {
            public readonly string source;
            public readonly string sourcePath;
            public readonly List<Token> tokens;
            public int index;

            public ParserState(string source, string sourcePath)
            {
                this.source = source ?? string.Empty;
                this.sourcePath = sourcePath ?? string.Empty;
                this.tokens = Tokenize(this.source);
                this.index = 0;
            }
        }

        [ThreadStatic]
        static Stack<ParserState> stateStack;

        static ParserState CurrentState => stateStack != null && stateStack.Count > 0 ? stateStack.Peek() : null;

        public static bool TryValidate(string source, out LevelScriptDiagnostic diagnostic)
        {
            diagnostic = LevelScriptDiagnostic.None;
            if (string.IsNullOrWhiteSpace(source))
                return true;

            try
            {
                Parse(source);
                return true;
            }
            catch (LevelScriptParseException ex)
            {
                diagnostic = LevelScriptDiagnostic.FromException(ex, source);
                return false;
            }
        }

        public static LevelScriptProgram Parse(string source, string sourcePath = null)
        {
            if (string.IsNullOrWhiteSpace(source))
                return new LevelScriptProgram { SourcePath = sourcePath ?? string.Empty };

            if (stateStack == null)
                stateStack = new Stack<ParserState>();

            var state = new ParserState(source, sourcePath);
            stateStack.Push(state);
            try
            {
                List<Token> tokens = state.tokens;
                ref int index = ref state.index;
                var program = new LevelScriptProgram { SourcePath = sourcePath ?? string.Empty };

            while (Peek(tokens, index).Type != TokenType.End)
            {
                Token token = Peek(tokens, index);
                switch (token.Type)
                {
                    case TokenType.Using:
                        program.Usings.Add(ParseUsing(tokens, ref index));
                        break;
                    case TokenType.Public:
                    case TokenType.Private:
                    {
                        bool isPublic = token.Type == TokenType.Public;
                        index++;
                        if (Peek(tokens, index).Type == TokenType.Function)
                        {
                            FunctionDefinitionNode function = ParseFunction(tokens, ref index);
                            function.IsPublic = isPublic;
                            function.SourcePath = program.SourcePath;
                            program.Functions.Add(function);
                        }
                        else
                        {
                            LevelScriptNode declaration = ParseVisibilityDeclaration(tokens, ref index, isPublic, program.SourcePath);
                            program.MainBody.Add(declaration);
                        }

                        break;
                    }
                    case TokenType.Function:
                    {
                        FunctionDefinitionNode function = ParseFunction(tokens, ref index);
                        function.SourcePath = program.SourcePath;
                        program.Functions.Add(function);
                        break;
                    }
                    case TokenType.OnTriggerEnter:
                    case TokenType.OnTriggerStay:
                    case TokenType.OnTriggerExit:
                        program.TriggerHandlers.Add(ParseTriggerHandler(tokens, ref index));
                        break;
                    case TokenType.Group:
                        if (LooksLikeGameObjectArrayComponentStatement(tokens, index))
                            program.MainBody.Add(ParseGameObjectArrayComponentStatement(tokens, ref index, true));
                        else
                            program.TriggerHandlers.Add(ParseGroupTriggerHandler(tokens, ref index));
                        break;
                    case TokenType.LeftBrace:
                        if (LooksLikeGameObjectArrayComponentStatement(tokens, index))
                            program.MainBody.Add(ParseBraceComponentStatement(tokens, ref index, true));
                        else
                            program.TriggerHandlers.Add(ParseBraceTriggerHandler(tokens, ref index));
                        break;
                    case TokenType.OnPlayerAdded:
                        program.PlayerAddedHandlers.Add(ParsePlayerAddedHandler(tokens, ref index));
                        break;
                    case TokenType.OnClientEvent:
                        program.EventListeners.Add(ParseEventListener(tokens, ref index, ScriptEventTarget.ClientEvent));
                        break;
                    case TokenType.OnServerEvent:
                        program.EventListeners.Add(ParseEventListener(tokens, ref index, ScriptEventTarget.ServerEvent));
                        break;
                    case TokenType.Network:
                        if (index + 2 < tokens.Count && tokens[index + 1].Type == TokenType.Dot)
                        {
                            if (tokens[index + 2].Type == TokenType.OnClientEvent)
                            {
                                index += 2;
                                program.EventListeners.Add(ParseEventListener(tokens, ref index, ScriptEventTarget.ClientEvent));
                                break;
                            }
                            else if (tokens[index + 2].Type == TokenType.OnServerEvent)
                            {
                                index += 2;
                                program.EventListeners.Add(ParseEventListener(tokens, ref index, ScriptEventTarget.ServerEvent));
                                break;
                            }
                        }
                        goto default;
                    case TokenType.SpawnPoints:
                        program.SpawnPointsDeclarations.Add(ParseSpawnPoints(tokens, ref index));
                        break;
                    case TokenType.Spawn:
                        program.Spawns.Add(ParseSpawn(tokens, ref index));
                        break;
                    default:
                    {
                        LevelScriptNode statement = ParseStatement(tokens, ref index);
                        ApplySourcePathToDeclaration(statement, program.SourcePath);
                        program.MainBody.Add(statement);
                        break;
                    }
                }
            }

            return program;
            }
            finally
            {
                if (stateStack != null && stateStack.Count > 0)
                    stateStack.Pop();
            }
        }

        static UsingDirectiveNode ParseUsing(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Using);
            string path = ParseModulePath(tokens, ref index);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            if (string.IsNullOrWhiteSpace(path))
                throw Error(tokens, index, "using requires a module path like folder/file.ks.");

            return new UsingDirectiveNode { Path = path };
        }

        static string ParseModulePath(List<Token> tokens, ref int index)
        {
            Token first = Peek(tokens, index);
            if (first.Type != TokenType.Identifier)
                throw Error(tokens, index, "Expected module path after using.");

            var sb = new System.Text.StringBuilder();
            sb.Append(Expect(tokens, ref index, TokenType.Identifier).Text);

            while (true)
            {
                Token next = Peek(tokens, index);
                if (next.Type == TokenType.Slash)
                {
                    index++;
                    sb.Append('/');
                    sb.Append(Expect(tokens, ref index, TokenType.Identifier).Text);
                    continue;
                }

                if (next.Type == TokenType.Dot)
                {
                    index++;
                    sb.Append('.');
                    sb.Append(Expect(tokens, ref index, TokenType.Identifier).Text);
                    continue;
                }

                break;
            }

            return sb.ToString();
        }

        static LevelScriptNode ParseVisibilityDeclaration(
            List<Token> tokens,
            ref int index,
            bool isPublic,
            string sourcePath)
        {
            TokenType type = Peek(tokens, index).Type;
            LevelScriptNode node;

            if (type == TokenType.List)
            {
                index++;
                node = ParseListDeclaration(tokens, ref index, true);
            }
            else if (index + 2 < tokens.Count && tokens[index + 1].Type == TokenType.LeftBracket && tokens[index + 2].Type == TokenType.RightBracket)
            {
                index += 3;
                node = ParseListDeclaration(tokens, ref index, true);
            }
            else
            {
                node = type switch
                {
                    TokenType.Int => ParseIntDeclaration(tokens, ref index, true),
                    TokenType.Bool => ParseBoolDeclaration(tokens, ref index, true),
                    TokenType.Float => ParseFloatDeclaration(tokens, ref index, true),
                    TokenType.String => ParseStringDeclaration(tokens, ref index, true),
                    TokenType.GameObject => ParseGameObjectDeclaration(tokens, ref index, true),
                    TokenType.Component => ParseComponentDeclaration(tokens, ref index, true),
                    _ => throw Error(tokens, index, "public/private must precede a function, variable, or list declaration.")
                };
            }

            SetDeclarationVisibility(node, isPublic, sourcePath);
            return node;
        }

        static void SetDeclarationVisibility(LevelScriptNode node, bool isPublic, string sourcePath)
        {
            switch (node)
            {
                case IntDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
                case BoolDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
                case FloatDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
                case StringDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
                case GameObjectDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
                case ComponentDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
                case ListDeclarationNode d:
                    d.IsPublic = isPublic;
                    d.SourcePath = sourcePath ?? string.Empty;
                    break;
            }
        }

        static void ApplySourcePathToDeclaration(LevelScriptNode node, string sourcePath)
        {
            SetDeclarationVisibility(node, isPublic: false, sourcePath);
        }

        static FunctionDefinitionNode ParseFunction(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Function);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.LeftParen);

            var function = new FunctionDefinitionNode { Name = nameToken.Text };
            if (Peek(tokens, index).Type != TokenType.RightParen)
            {
                do
                {
                    function.Parameters.Add(ParseFunctionParameter(tokens, ref index));
                }
                while (ConsumeIf(tokens, ref index, TokenType.Comma));
            }

            Expect(tokens, ref index, TokenType.RightParen);
            ParseBlockBody(tokens, ref index, function.Body);
            return function;
        }

        static FunctionParameter ParseFunctionParameter(List<Token> tokens, ref int index)
        {
            TokenType pType = Peek(tokens, index).Type;
            ScriptValueKind kind;
            if (pType == TokenType.List)
            {
                kind = ScriptValueKind.List;
                index++;
            }
            else if (index + 2 < tokens.Count && tokens[index + 1].Type == TokenType.LeftBracket && tokens[index + 2].Type == TokenType.RightBracket)
            {
                kind = ScriptValueKind.List;
                index += 3;
            }
            else
            {
                kind = pType switch
                {
                    TokenType.Int => ScriptValueKind.Int,
                    TokenType.Bool => ScriptValueKind.Bool,
                    TokenType.Float => ScriptValueKind.Float,
                    TokenType.String => ScriptValueKind.String,
                    TokenType.GameObject => ScriptValueKind.GameObject,
                    TokenType.Component => ScriptValueKind.Component,
                    _ => throw Error(tokens, index,
                        "Function parameters must use int, bool, float, string, GameObject, Component, or List types.")
                };
                index++;
            }

            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            return new FunctionParameter { Kind = kind, Name = nameToken.Text };
        }

        static TriggerHandlerNode ParseTriggerHandler(List<Token> tokens, ref int index)
        {
            TriggerHandlerKind kind = Peek(tokens, index).Type switch
            {
                TokenType.OnTriggerEnter => TriggerHandlerKind.Enter,
                TokenType.OnTriggerStay => TriggerHandlerKind.Stay,
                TokenType.OnTriggerExit => TriggerHandlerKind.Exit,
                _ => throw Error(tokens, index, "Expected trigger handler.")
            };

            index++;
            Expect(tokens, ref index, TokenType.LeftParen);
            IntExpression group = ParseIntArg(tokens, ref index, "group");
            Expect(tokens, ref index, TokenType.RightParen);

            var handler = new TriggerHandlerNode
            {
                Kind = kind,
                Source = new GroupGameObjectArrayExpression { GroupId = group },
                ActorMode = TriggerActorMode.Runners
            };
            ParseBlockBody(tokens, ref index, handler.Body);
            return handler;
        }

        static TriggerHandlerNode ParseGroupTriggerHandler(List<Token> tokens, ref int index)
        {
            GameObjectArrayExpression source = ParseGroupArrayExpression(tokens, ref index);
            return ParseObjectTriggerHandler(tokens, ref index, source);
        }

        static TriggerHandlerNode ParseBraceTriggerHandler(List<Token> tokens, ref int index)
        {
            GameObjectArrayExpression source = ParseGameObjectArrayArg(tokens, ref index);
            return ParseObjectTriggerHandler(tokens, ref index, source);
        }

        static TriggerHandlerNode ParseObjectTriggerHandler(List<Token> tokens, ref int index, GameObjectArrayExpression source)
        {
            Expect(tokens, ref index, TokenType.Dot);

            TriggerHandlerKind kind = Peek(tokens, index).Type switch
            {
                TokenType.OnTriggerEnter => TriggerHandlerKind.Enter,
                TokenType.OnTriggerStay => TriggerHandlerKind.Stay,
                TokenType.OnTriggerExit => TriggerHandlerKind.Exit,
                _ => throw Error(tokens, index,
                    "Expected OnTriggerEnter, OnTriggerStay, or OnTriggerExit after GameObject source.")
            };

            index++;
            Expect(tokens, ref index, TokenType.LeftParen);

            string gameObjectParameter = null;
            if (Peek(tokens, index).Type == TokenType.GameObject)
            {
                index++;
                gameObjectParameter = Expect(tokens, ref index, TokenType.Identifier).Text;
            }
            else if (Peek(tokens, index).Type == TokenType.Identifier)
            {
                gameObjectParameter = Expect(tokens, ref index, TokenType.Identifier).Text;
            }

            Expect(tokens, ref index, TokenType.RightParen);

            var handler = new TriggerHandlerNode
            {
                Kind = kind,
                Source = source,
                ActorMode = DetectActorMode(source),
                GameObjectParameterName = gameObjectParameter
            };
            ParseBlockBody(tokens, ref index, handler.Body);
            return handler;
        }

        static TriggerActorMode DetectActorMode(GameObjectArrayExpression source)
        {
            if (source is AllPlayersGameObjectArrayExpression)
                return TriggerActorMode.SourceInternal;

            if (source is GameObjectArrayLiteralExpression literal)
            {
                if (literal.Items.Count == 0)
                    return TriggerActorMode.Runners;

                for (int i = 0; i < literal.Items.Count; i++)
                {
                    if (literal.Items[i] is not AllPlayersArrayItemExpression)
                        return TriggerActorMode.Runners;
                }

                return TriggerActorMode.SourceInternal;
            }

            return TriggerActorMode.Runners;
        }

        static GameObjectArrayExpression ParseGameObjectArrayArg(List<Token> tokens, ref int index)
        {
            TokenType nextType = Peek(tokens, index).Type;
            
            // Array literal: {obj1, obj2}
            if (nextType == TokenType.LeftBrace)
                return ParseGameObjectArrayLiteral(tokens, ref index);

            // Group(n)
            if (nextType == TokenType.Group)
                return ParseGroupArrayExpression(tokens, ref index);

            // Single GameObject - wrap in array
            GameObjectExpression singleObj = ParseGameObjectExpression(tokens, ref index);
            var literal = new GameObjectArrayLiteralExpression();
            literal.Items.Add(singleObj);
            return literal;
        }

        static GameObjectArrayExpression ParseGameObjectArrayLiteral(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.LeftBrace);

            if (Peek(tokens, index).Type == TokenType.RightBrace)
                throw Error(tokens, index, "GameObject array cannot be empty.");

            var items = new List<GameObjectExpression>();
            do
            {
                items.Add(ParseGameObjectArrayItem(tokens, ref index));
            }
            while (ConsumeIf(tokens, ref index, TokenType.Comma));

            Expect(tokens, ref index, TokenType.RightBrace);

            if (items.Count == 1 && items[0] is AllPlayersArrayItemExpression)
                return new AllPlayersGameObjectArrayExpression();

            return CreateGameObjectArrayLiteral(items);
        }

        static GameObjectArrayLiteralExpression CreateGameObjectArrayLiteral(List<GameObjectExpression> items)
        {
            var literal = new GameObjectArrayLiteralExpression();
            literal.Items.AddRange(items);
            return literal;
        }

        static GameObjectExpression ParseGameObjectArrayItem(List<Token> tokens, ref int index)
        {
            // Support Scene.X, player, and regular GameObjects
            return ParseGameObjectExpression(tokens, ref index);
        }

        static GroupGameObjectArrayExpression ParseGroupArrayExpression(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Group);
            Expect(tokens, ref index, TokenType.LeftParen);

            IntExpression groupId = null;
            if (Peek(tokens, index).Type != TokenType.RightParen)
                groupId = ParseIntExpression(tokens, ref index);

            Expect(tokens, ref index, TokenType.RightParen);
            return new GroupGameObjectArrayExpression { GroupId = groupId };
        }

        static GameObjectExpression ParseGameObjectExpression(List<Token> tokens, ref int index)
        {
            if (Peek(tokens, index).Type == TokenType.Nil)
            {
                index++;
                return new GameObjectNilExpression();
            }

            if (Peek(tokens, index).Type == TokenType.Scene)
            {
                return ParseSceneObjectExpression(tokens, ref index);
            }

            if (Peek(tokens, index).Type == TokenType.Identifier &&
                string.Equals(Peek(tokens, index).Text, "FindNearestPlayer", StringComparison.OrdinalIgnoreCase))
            {
                return ParseFindNearestPlayerExpression(tokens, ref index);
            }

            if (Peek(tokens, index).Type == TokenType.Instance)
                return ParseInstanceCallExpression(tokens, ref index);

            if (Peek(tokens, index).Type == TokenType.Identifier &&
                string.Equals(Peek(tokens, index).Text, "instance", StringComparison.OrdinalIgnoreCase))
            {
                return ParseInstanceCallExpression(tokens, ref index);
            }

            if (Peek(tokens, index).Type == TokenType.LocalPlayer)
            {
                index++;
                return new LocalPlayerExpression();
            }

            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);

            if (nameToken.Text.Equals("localPlayer", StringComparison.OrdinalIgnoreCase))
                return new LocalPlayerExpression();
            
            // Check for "player" keyword which represents all players in array context
            if (string.Equals(nameToken.Text, "player", StringComparison.OrdinalIgnoreCase))
                return new AllPlayersArrayItemExpression();

            // List indexing: objs[i]
            if (Peek(tokens, index).Type == TokenType.LeftBracket)
            {
                index++;
                IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                Expect(tokens, ref index, TokenType.RightBracket);
                return new ListItemGameObjectExpression { ListName = nameToken.Text, Index = idxExpr };
            }
            
            // Check if this is a GetComponent call
            if (Peek(tokens, index).Type == TokenType.Dot &&
                Peek(tokens, index + 1).Type == TokenType.GetComponent)
            {
                throw Error(tokens, index, "GetComponent can only be called on Scene objects or GameObject variables with explicit type.");
            }

            return new GameObjectVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex };
        }

        static SceneObjectExpression ParseSceneObjectExpression(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Scene);
            Expect(tokens, ref index, TokenType.Dot);
            
            // Build path: Scene.Child1.Child2.Child3 or Scene."Child 1"."Child 2"
            StringBuilder pathBuilder = new StringBuilder();
            
            // First child: Identifier or StringLiteral
            Token childToken = Peek(tokens, index);
            if (childToken.Type == TokenType.Identifier)
            {
                index++;
                pathBuilder.Append(childToken.Text);
            }
            else if (childToken.Type == TokenType.StringLiteral)
            {
                index++;
                pathBuilder.Append(childToken.Text); // Token.Text for StringLiteral already has the value without quotes
            }
            else
            {
                throw Error(tokens, index, "Expected identifier or string after Scene.");
            }

            // Continue parsing dots and children
            while (Peek(tokens, index).Type == TokenType.Dot)
            {
                // Lookahead to check if next is GetComponent or Identifier/StringLiteral
                Token nextToken = Peek(tokens, index + 1);
                
                if (nextToken.Type == TokenType.GetComponent)
                    break; // This is GetComponent call, not child access

                if (nextToken.Type != TokenType.Identifier && nextToken.Type != TokenType.StringLiteral)
                    break; // Not a child name

                index++; // consume dot
                
                if (nextToken.Type == TokenType.Identifier)
                {
                    index++;
                    pathBuilder.Append('.');
                    pathBuilder.Append(nextToken.Text);
                }
                else if (nextToken.Type == TokenType.StringLiteral)
                {
                    index++;
                    pathBuilder.Append('.');
                    pathBuilder.Append(nextToken.Text);
                }
            }

            return new SceneObjectExpression { Path = pathBuilder.ToString() };
        }

        static ComponentExpression ParseComponentExpression(List<Token> tokens, ref int index)
        {
            if (Peek(tokens, index).Type == TokenType.Nil)
            {
                index++;
                return new ComponentNilExpression();
            }

            // Check for GetComponent call: obj.GetComponent<Type>()
            int saved = index;
            try
            {
                GameObjectExpression target = ParseGameObjectExpression(tokens, ref index);
                if (Peek(tokens, index).Type == TokenType.Dot &&
                    Peek(tokens, index + 1).Type == TokenType.GetComponent)
                {
                    index++; // consume dot
                    Expect(tokens, ref index, TokenType.GetComponent);
                    Expect(tokens, ref index, TokenType.Less);
                    Token componentToken = Expect(tokens, ref index, TokenType.Identifier);
                    Expect(tokens, ref index, TokenType.Greater);
                    Expect(tokens, ref index, TokenType.LeftParen);
                    Expect(tokens, ref index, TokenType.RightParen);

                    return new GetComponentExpression
                    {
                        Target = target,
                        ComponentName = componentToken.Text
                    };
                }
            }
            catch (LevelScriptParseException)
            {
                index = saved;
            }

            // Must be a variable
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            return new ComponentVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex };
        }

        static FindNearestPlayerExpression ParseFindNearestPlayerExpression(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.LeftParen);
            FloatExpression x = ParseFloatArg(tokens, ref index, "x", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression y = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
            Expect(tokens, ref index, TokenType.RightParen);

            return new FindNearestPlayerExpression
            {
                X = x,
                Y = y
            };
        }

        static InstanceCallExpression ParseInstanceCallExpression(List<Token> tokens, ref int index)
        {
            if (Peek(tokens, index).Type == TokenType.Instance)
                index++;
            else
                Expect(tokens, ref index, TokenType.Identifier);

            Expect(tokens, ref index, TokenType.LeftParen);
            StringExpression typeName = ParseStringArg(tokens, ref index, "type");
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression x = ParseFloatArg(tokens, ref index, "x", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression y = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
            Expect(tokens, ref index, TokenType.RightParen);

            return new InstanceCallExpression
            {
                TypeName = typeName,
                X = x,
                Y = y
            };
        }

        static EventListenerNode ParseEventListener(List<Token> tokens, ref int index, ScriptEventTarget target)
        {
            index++;
            Expect(tokens, ref index, TokenType.LeftParen);

            StringExpression eventExpr = ParseStringExpression(tokens, ref index);
            string eventName = (eventExpr is StringLiteralExpression lit) ? lit.Value : string.Empty;

            string playerParam = null;
            var paramNames = new List<string>();

            if (target == ScriptEventTarget.ClientEvent)
            {
                if (ConsumeIf(tokens, ref index, TokenType.Comma))
                {
                    ConsumeIfTypeKeyword(tokens, ref index);
                    Token playerToken = Expect(tokens, ref index, TokenType.Identifier);
                    playerParam = playerToken.Text;

                    while (ConsumeIf(tokens, ref index, TokenType.Comma))
                    {
                        ConsumeIfTypeKeyword(tokens, ref index);
                        Token paramToken = Expect(tokens, ref index, TokenType.Identifier);
                        paramNames.Add(paramToken.Text);
                    }
                }
            }
            else
            {
                while (ConsumeIf(tokens, ref index, TokenType.Comma))
                {
                    ConsumeIfTypeKeyword(tokens, ref index);
                    Token paramToken = Expect(tokens, ref index, TokenType.Identifier);
                    paramNames.Add(paramToken.Text);
                }
            }

            Expect(tokens, ref index, TokenType.RightParen);

            var listener = new EventListenerNode
            {
                Target = target,
                EventName = eventName,
                PlayerParameterName = playerParam,
            };
            listener.ParameterNames.AddRange(paramNames);
            ParseBlockBody(tokens, ref index, listener.Body);
            return listener;
        }

        static bool ConsumeIfTypeKeyword(List<Token> tokens, ref int index)
        {
            TokenType t = Peek(tokens, index).Type;
            if (t == TokenType.Int || t == TokenType.Float || t == TokenType.Bool ||
                t == TokenType.String || t == TokenType.GameObject || t == TokenType.Component)
            {
                index++;
                return true;
            }
            return false;
        }

        static LevelScriptNode ParseNetworkStatement(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Network);
            Expect(tokens, ref index, TokenType.Dot);
            Token action = Peek(tokens, index);
            if (action.Type == TokenType.SendToServer)
                return ParseSendToServer(tokens, ref index);
            if (action.Type == TokenType.SendToClient)
                return ParseSendToClient(tokens, ref index);
            if (action.Type == TokenType.SendToAllClients)
                return ParseSendToAllClients(tokens, ref index);

            throw Error(action, $"Expected sendToServer, sendToClient, or sendToAllClients after Network., got '{action.Text}'.");
        }

        static SendToServerNode ParseSendToServer(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SendToServer);
            Expect(tokens, ref index, TokenType.LeftParen);
            StringExpression eventName = ParseStringExpression(tokens, ref index);
            var node = new SendToServerNode { EventName = eventName };
            while (ConsumeIf(tokens, ref index, TokenType.Comma))
            {
                node.Arguments.Add(ParseCallArgument(tokens, ref index));
            }
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return node;
        }

        static SendToClientNode ParseSendToClient(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SendToClient);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectExpression player = ParseGameObjectExpression(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression eventName = ParseStringExpression(tokens, ref index);
            var node = new SendToClientNode { TargetPlayer = player, EventName = eventName };
            while (ConsumeIf(tokens, ref index, TokenType.Comma))
            {
                node.Arguments.Add(ParseCallArgument(tokens, ref index));
            }
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return node;
        }

        static SendToAllClientsNode ParseSendToAllClients(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SendToAllClients);
            Expect(tokens, ref index, TokenType.LeftParen);
            StringExpression eventName = ParseStringExpression(tokens, ref index);
            var node = new SendToAllClientsNode { EventName = eventName };
            while (ConsumeIf(tokens, ref index, TokenType.Comma))
            {
                node.Arguments.Add(ParseCallArgument(tokens, ref index));
            }
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return node;
        }


        static PlayerAddedHandlerNode ParsePlayerAddedHandler(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.OnPlayerAdded);
            Expect(tokens, ref index, TokenType.LeftParen);
            ConsumeIfTypeKeyword(tokens, ref index);
            Token playerToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.RightParen);

            var handler = new PlayerAddedHandlerNode
            {
                PlayerParameterName = playerToken.Text
            };
            ParseBlockBody(tokens, ref index, handler.Body);
            return handler;
        }

        static SpawnNode ParseSpawn(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Spawn);
            var spawn = new SpawnNode();
            ParseBlockBody(tokens, ref index, spawn.Body);
            return spawn;
        }

        static void ParseBlockBody(List<Token> tokens, ref int index, List<LevelScriptNode> body)
        {
            Expect(tokens, ref index, TokenType.LeftBrace);

            while (Peek(tokens, index).Type != TokenType.RightBrace)
                body.Add(ParseStatement(tokens, ref index));

            Expect(tokens, ref index, TokenType.RightBrace);
        }

        static LevelScriptNode ParseStatement(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            switch (token.Type)
            {
                case TokenType.Public:
                case TokenType.Private:
                    throw Error(tokens, index, "public/private are only allowed at the top level.");
                case TokenType.Using:
                    throw Error(tokens, index, "using is only allowed at the top level.");
                case TokenType.Spawn:
                    return ParseSpawn(tokens, ref index);
                case TokenType.While:
                    return ParseWhile(tokens, ref index);
                case TokenType.For:
                    return ParseFor(tokens, ref index);
                case TokenType.If:
                    return ParseIf(tokens, ref index);
                case TokenType.List:
                    index++;
                    return ParseListDeclaration(tokens, ref index, true);
                case TokenType.Component:
                    return ParseComponentDeclaration(tokens, ref index, true);
                case TokenType.Int:
                case TokenType.Bool:
                case TokenType.Float:
                case TokenType.String:
                case TokenType.GameObject:
                    if (index + 2 < tokens.Count && tokens[index + 1].Type == TokenType.LeftBracket && tokens[index + 2].Type == TokenType.RightBracket)
                    {
                        index += 3;
                        return ParseListDeclaration(tokens, ref index, true);
                    }
                    if (token.Type == TokenType.Int) return ParseIntDeclaration(tokens, ref index, true);
                    if (token.Type == TokenType.Bool) return ParseBoolDeclaration(tokens, ref index, true);
                    if (token.Type == TokenType.Float) return ParseFloatDeclaration(tokens, ref index, true);
                    if (token.Type == TokenType.String) return ParseStringDeclaration(tokens, ref index, true);
                    return ParseGameObjectDeclaration(tokens, ref index, true);
                case TokenType.Move:
                    return ParseMove(tokens, ref index);
                case TokenType.Animate:
                    return ParseAnimate(tokens, ref index);
                case TokenType.SpawnExecutor:
                    return ParseSpawnExecutor(tokens, ref index);
                case TokenType.Rotate:
                    return ParseRotate(tokens, ref index);
                case TokenType.Wait:
                    return ParseWait(tokens, ref index);
                case TokenType.Goto:
                    return ParseGoto(tokens, ref index);
                case TokenType.ScaleTo:
                    return ParseScaleTo(tokens, ref index);
                case TokenType.Transparency:
                    return ParseTransparency(tokens, ref index);
                case TokenType.Visibility:
                    return ParseVisibility(tokens, ref index);
                case TokenType.PlaySound:
                    return ParsePlaySound(tokens, ref index);
                case TokenType.SpawnPoint:
                    return ParseSpawnPoint(tokens, ref index);
                case TokenType.Hitbox:
                    return ParseHitbox(tokens, ref index);
                case TokenType.SendToServer:
                    return ParseSendToServer(tokens, ref index);
                case TokenType.SendToClient:
                    return ParseSendToClient(tokens, ref index);
                case TokenType.SendToAllClients:
                    return ParseSendToAllClients(tokens, ref index);
                case TokenType.Network:
                    return ParseNetworkStatement(tokens, ref index);
                case TokenType.RotateTo:
                    return ParseRotateTo(tokens, ref index);
                case TokenType.SetTexture:
                    return ParseSetTexture(tokens, ref index);
                case TokenType.SetColor:
                    return ParseSetColor(tokens, ref index);
                case TokenType.Destroy:
                    return ParseDestroy(tokens, ref index);
                case TokenType.Group:
                    return ParseGameObjectArrayComponentStatement(tokens, ref index, true);
                case TokenType.LeftBrace:
                    return ParseBraceComponentStatement(tokens, ref index, true);
                case TokenType.Identifier:
                    return ParseIdentifierStatement(tokens, ref index, true);
                case TokenType.Return:
                    return ParseReturn(tokens, ref index);
                case TokenType.Break:
                    return ParseBreak(tokens, ref index);
                case TokenType.Continue:
                    return ParseContinue(tokens, ref index);
                default:
                    throw Error(token,$"Unexpected token '{token.Text}'.");
            }
        }

        static ReturnNode ParseReturn(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Return);
            if (Peek(tokens, index).Type == TokenType.Semicolon)
            {
                index++;
                return new ReturnNode();
            }

            ScriptArgument arg = ParseCallArgument(tokens, ref index);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return new ReturnNode
            {
                Value = arg,
                IntValue = arg.IntValue,
                FloatValue = arg.FloatValue,
                BoolValue = arg.BoolValue,
                StringValue = arg.StringValue
            };
        }

        static BreakNode ParseBreak(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Break);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return new BreakNode();
        }

        static ContinueNode ParseContinue(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Continue);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return new ContinueNode();
        }

        static WhileNode ParseWhile(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.While);
            Expect(tokens, ref index, TokenType.LeftParen);
            BoolExpression condition = ParseBoolExpression(tokens, ref index);
            Expect(tokens, ref index, TokenType.RightParen);

            var loop = new WhileNode { Condition = condition };
            ParseBlockBody(tokens, ref index, loop.Body);
            return loop;
        }

        static IfNode ParseIf(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.If);
            Expect(tokens, ref index, TokenType.LeftParen);
            BoolExpression condition = ParseBoolExpression(tokens, ref index);
            Expect(tokens, ref index, TokenType.RightParen);

            var ifNode = new IfNode { Condition = condition };
            ParseBlockBody(tokens, ref index, ifNode.ThenBody);

            while (Peek(tokens, index).Type == TokenType.Else)
            {
                index++;
                if (Peek(tokens, index).Type == TokenType.If)
                {
                    index++;
                    Expect(tokens, ref index, TokenType.LeftParen);
                    BoolExpression elseIfCondition = ParseBoolExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightParen);

                    var clause = new ElseIfClause { Condition = elseIfCondition };
                    ParseBlockBody(tokens, ref index, clause.Body);
                    ifNode.ElseIfClauses.Add(clause);
                    continue;
                }

                ParseBlockBody(tokens, ref index, ifNode.ElseBody);
                break;
            }

            return ifNode;
        }

        static ForNode ParseFor(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.For);
            Expect(tokens, ref index, TokenType.LeftParen);

            LevelScriptNode init = null;
            if (Peek(tokens, index).Type != TokenType.Semicolon)
            {
                init = Peek(tokens, index).Type switch
                {
                    TokenType.Int => ParseIntDeclaration(tokens, ref index, false),
                    TokenType.Bool => ParseBoolDeclaration(tokens, ref index, false),
                    TokenType.Float => ParseFloatDeclaration(tokens, ref index, false),
                    TokenType.String => ParseStringDeclaration(tokens, ref index, false),
                    TokenType.GameObject => ParseGameObjectDeclaration(tokens, ref index, false),
                    TokenType.Component => ParseComponentDeclaration(tokens, ref index, false),
                    _ => ParseAssignmentOrIncrement(tokens, ref index, false)
                };
            }
            Expect(tokens, ref index, TokenType.Semicolon);

            BoolExpression condition = new BoolLiteralExpression { Value = true };
            if (Peek(tokens, index).Type != TokenType.Semicolon)
                condition = ParseBoolExpression(tokens, ref index);
            Expect(tokens, ref index, TokenType.Semicolon);

            LevelScriptNode increment = null;
            if (Peek(tokens, index).Type != TokenType.RightParen)
                increment = ParseAssignmentOrIncrement(tokens, ref index, false);

            Expect(tokens, ref index, TokenType.RightParen);

            var loop = new ForNode
            {
                Init = init,
                Condition = condition,
                Increment = increment
            };
            ParseBlockBody(tokens, ref index, loop.Body);
            return loop;
        }

        static bool IsUserFunctionCall(List<Token> tokens, int index)
        {
            if (index + 1 < tokens.Count &&
                tokens[index].Type == TokenType.Identifier &&
                tokens[index + 1].Type == TokenType.LeftParen &&
                !IsBuiltinFunction(tokens[index].Text))
            {
                return true;
            }
            return false;
        }

        static bool IsBuiltinFunction(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return string.Equals(name, "random", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "FindNearestPlayer", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "instance", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "hasTile", StringComparison.OrdinalIgnoreCase) ||
                   TryMapMathFunction(name, out _, out _);
        }

        static IntDeclarationNode ParseIntDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.Int);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.Equal);
            if (IsUserFunctionCall(tokens, index))
            {
                Token fnToken = tokens[index];
                FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new IntDeclarationNode { Name = nameToken.Text, CallValue = call };
            }
            IntExpression value = ParseIntExpression(tokens, ref index);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new IntDeclarationNode { Name = nameToken.Text, Value = value };
        }

        static BoolDeclarationNode ParseBoolDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.Bool);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.Equal);
            if (IsUserFunctionCall(tokens, index))
            {
                Token fnToken = tokens[index];
                FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new BoolDeclarationNode { Name = nameToken.Text, CallValue = call };
            }
            BoolExpression value = ParseBoolExpression(tokens, ref index);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new BoolDeclarationNode { Name = nameToken.Text, Value = value };
        }

        static FloatDeclarationNode ParseFloatDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.Float);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.Equal);
            if (IsUserFunctionCall(tokens, index))
            {
                Token fnToken = tokens[index];
                FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new FloatDeclarationNode { Name = nameToken.Text, CallValue = call };
            }
            FloatExpression value = ParseFloatExpression(tokens, ref index);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new FloatDeclarationNode { Name = nameToken.Text, Value = value };
        }

        static StringDeclarationNode ParseStringDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.String);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.Equal);
            if (IsUserFunctionCall(tokens, index))
            {
                Token fnToken = tokens[index];
                FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new StringDeclarationNode { Name = nameToken.Text, CallValue = call };
            }
            StringExpression value = ParseStringExpression(tokens, ref index);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new StringDeclarationNode { Name = nameToken.Text, Value = value };
        }

        static GameObjectDeclarationNode ParseGameObjectDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.GameObject);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.Equal);
            if (IsUserFunctionCall(tokens, index))
            {
                Token fnToken = tokens[index];
                FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new GameObjectDeclarationNode { Name = nameToken.Text, CallValue = call };
            }
            GameObjectExpression value = ParseGameObjectExpression(tokens, ref index);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new GameObjectDeclarationNode { Name = nameToken.Text, Value = value };
        }

        static ListDeclarationNode ParseListDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            var node = new ListDeclarationNode { Name = nameToken.Text };
            if (ConsumeIf(tokens, ref index, TokenType.Equal))
            {
                if (IsUserFunctionCall(tokens, index))
                {
                    Token fnToken = tokens[index];
                    node.CallValue = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                }
                else
                {
                    Expect(tokens, ref index, TokenType.LeftBracket);
                    if (Peek(tokens, index).Type != TokenType.RightBracket)
                    {
                        do
                        {
                            node.InitialItems.Add(ParseCallArgument(tokens, ref index));
                        } while (ConsumeIf(tokens, ref index, TokenType.Comma));
                    }
                    Expect(tokens, ref index, TokenType.RightBracket);
                }
            }
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return node;
        }

        static ComponentDeclarationNode ParseComponentDeclaration(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.Component);
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            Expect(tokens, ref index, TokenType.Equal);
            if (IsUserFunctionCall(tokens, index))
            {
                Token fnToken = tokens[index];
                FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new ComponentDeclarationNode { Name = nameToken.Text, CallValue = call };
            }
            ComponentExpression value = ParseComponentExpression(tokens, ref index);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new ComponentDeclarationNode { Name = nameToken.Text, Value = value };
        }

        static LevelScriptNode ParseIdentifierStatement(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            Token nameToken = Expect(tokens, ref index, TokenType.Identifier);
            if (Peek(tokens, index).Type == TokenType.LeftBracket)
            {
                index++;
                IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                Expect(tokens, ref index, TokenType.RightBracket);
                Expect(tokens, ref index, TokenType.Equal);
                ScriptArgument val = ParseCallArgument(tokens, ref index);
                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return new ListSetItemNode { ListName = nameToken.Text, Index = idxExpr, Value = val };
            }
            if (Peek(tokens, index).Type == TokenType.Dot)
            {
                LevelScriptNode namespaced = TryParseNamespacedStatement(tokens, ref index, nameToken, requireSemicolon);
                if (namespaced != null)
                    return namespaced;
            }

            if (Peek(tokens, index).Type == TokenType.LeftParen)
            {
                LevelScriptNode builtin = TryParseBuiltinFunctionStatement(tokens, ref index, nameToken, requireSemicolon);
                if (builtin != null)
                    return builtin;

                return ParseFunctionCall(tokens, ref index, nameToken.Text, nameToken.StartIndex, requireSemicolon);
            }

            return ParseAssignmentOrIncrement(tokens, ref index, requireSemicolon, nameToken);
        }

        static bool LooksLikeGameObjectArrayComponentStatement(List<Token> tokens, int index)
        {
            int saved = index;
            TokenType start = Peek(tokens, index).Type;
            if (start != TokenType.Group && start != TokenType.LeftBrace)
                return false;

            try
            {
                if (start == TokenType.Group)
                    ParseGroupArrayExpression(tokens, ref index);
                else
                    ParseGameObjectArrayLiteral(tokens, ref index);

                if (Peek(tokens, index).Type != TokenType.Dot)
                    return false;

                index++;
                return Peek(tokens, index).Type == TokenType.Identifier &&
                       IsComponentPropertyName(Peek(tokens, index).Text);
            }
            catch (LevelScriptParseException)
            {
                return false;
            }
            finally
            {
                index = saved;
            }
        }

        static bool IsComponentPropertyName(string name) =>
            string.Equals(name, "Particle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Rigidbody2D", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Player", StringComparison.OrdinalIgnoreCase);

        static LevelScriptNode ParseGameObjectArrayComponentStatement(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            GameObjectArrayExpression targets = ParseGroupArrayExpression(tokens, ref index);
            return ParseComponentPropertyAssignment(tokens, ref index, requireSemicolon, null, targets);
        }

        static LevelScriptNode ParseBraceComponentStatement(List<Token> tokens, ref int index, bool requireSemicolon)
        {
            int savedIndex = index;
            GameObjectArrayExpression targets = ParseGameObjectArrayLiteral(tokens, ref index);
            if (Peek(tokens, index).Type == TokenType.Dot)
            {
                int lookahead = index + 1;
                if (lookahead < tokens.Count &&
                    tokens[lookahead].Type == TokenType.Identifier &&
                    IsComponentPropertyName(tokens[lookahead].Text))
                {
                    return ParseComponentPropertyAssignment(tokens, ref index, requireSemicolon, null, targets);
                }
            }

            index = savedIndex;
            throw Error(tokens, index, "Expected {GameObject...}.Component.property assignment.");
        }

        static ComponentPropertyAssignmentNode ParseComponentPropertyAssignment(
            List<Token> tokens,
            ref int index,
            bool requireSemicolon,
            string objectName,
            GameObjectArrayExpression targets)
        {
            Expect(tokens, ref index, TokenType.Dot);
            Token componentToken = ExpectMemberToken(tokens, ref index);
            Expect(tokens, ref index, TokenType.Dot);
            Token propertyToken = ExpectMemberToken(tokens, ref index);
            if (Peek(tokens, index).Type == TokenType.LeftParen)
            {
                throw Error(tokens, index,
                    "Component method calls are only supported on a single GameObject variable.");
            }

            string propertyPath = propertyToken.Text;
            while (Peek(tokens, index).Type == TokenType.Dot)
            {
                index++;
                Token nextProperty = ExpectMemberToken(tokens, ref index);
                propertyPath += "." + nextProperty.Text;
            }

            Expect(tokens, ref index, TokenType.Equal);
            var assignment = new ComponentPropertyAssignmentNode
            {
                ObjectName = objectName,
                Targets = targets,
                ComponentName = componentToken.Text,
                PropertyName = propertyPath
            };

            if (string.Equals(componentToken.Text, "Particle", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propertyPath, "enabled", StringComparison.OrdinalIgnoreCase))
            {
                assignment.ValueKind = ScriptValueKind.Bool;
                assignment.BoolValue = ParseBoolExpression(tokens, ref index);
            }
            else if (string.Equals(componentToken.Text, "Particle", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(propertyPath, "Emission.Value", StringComparison.OrdinalIgnoreCase))
            {
                assignment.ValueKind = ScriptValueKind.Int;
                assignment.IntValue = ParseIntExpression(tokens, ref index);
            }
            else if (string.Equals(componentToken.Text, "Player", StringComparison.OrdinalIgnoreCase) &&
                     (string.Equals(propertyPath, "spawnPointX", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(propertyPath, "spawnPointY", StringComparison.OrdinalIgnoreCase)))
            {
                assignment.ValueKind = ScriptValueKind.Float;
                assignment.FloatValue = ParseFloatExpression(tokens, ref index);
            }
            else
            {
                assignment.ValueKind = ScriptValueKind.Float;
                assignment.FloatValue = ParseFloatExpression(tokens, ref index);
            }

            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return assignment;
        }

        static ComponentPropertyAssignmentNode ParseComponentVariablePropertyAssignment(
            List<Token> tokens,
            ref int index,
            bool requireSemicolon,
            string componentVariableName)
        {
            Expect(tokens, ref index, TokenType.Dot);
            Token propertyToken = ExpectMemberToken(tokens, ref index);
            
            string propertyPath = propertyToken.Text;
            while (Peek(tokens, index).Type == TokenType.Dot)
            {
                index++;
                Token nextProperty = ExpectMemberToken(tokens, ref index);
                propertyPath += "." + nextProperty.Text;
            }

            Expect(tokens, ref index, TokenType.Equal);
            
            var assignment = new ComponentPropertyAssignmentNode
            {
                ComponentVariableName = componentVariableName,
                PropertyName = propertyPath
            };

            // Try to infer type - default to Float
            int saved = index;
            try
            {
                assignment.ValueKind = ScriptValueKind.Int;
                assignment.IntValue = ParseIntExpression(tokens, ref index);
            }
            catch (LevelScriptParseException)
            {
                index = saved;
                try
                {
                    assignment.ValueKind = ScriptValueKind.Bool;
                    assignment.BoolValue = ParseBoolExpression(tokens, ref index);
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                    assignment.ValueKind = ScriptValueKind.Float;
                    assignment.FloatValue = ParseFloatExpression(tokens, ref index);
                }
            }

            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return assignment;
        }


        static LevelScriptNode TryParseNamespacedStatement(List<Token> tokens, ref int index, Token nameToken, bool requireSemicolon)
        {
            string ns = nameToken.Text;
            if (index + 2 < tokens.Count && tokens[index].Type == TokenType.Dot && tokens[index + 1].Type == TokenType.Identifier && tokens[index + 2].Type == TokenType.LeftParen)
            {
                string methodName = tokens[index + 1].Text;
                if (string.Equals(methodName, "add", StringComparison.OrdinalIgnoreCase))
                {
                    index += 3; // consume ., add, (
                    var node = new ListAddNode { ListName = ns, Item = ParseCallArgument(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                if (string.Equals(methodName, "removeat", StringComparison.OrdinalIgnoreCase))
                {
                    index += 3; // consume ., removeat, (
                    var node = new ListRemoveAtNode { ListName = ns, Index = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                if (string.Equals(methodName, "clear", StringComparison.OrdinalIgnoreCase))
                {
                    index += 3; // consume ., clear, (
                    var node = new ListClearNode { ListName = ns };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
            }
            if (string.Equals(ns, "Background", StringComparison.OrdinalIgnoreCase))
            {
                int saved = index;
                Expect(tokens, ref index, TokenType.Dot);
                Token method = ExpectMemberToken(tokens, ref index);
                string mName = method.Text.ToLowerInvariant();
                Expect(tokens, ref index, TokenType.LeftParen);

                var bgNode = new BackgroundNode();
                switch (mName)
                {
                    case "clear":
                        bgNode.Kind = BackgroundActionKind.Clear;
                        break;
                    case "reset":
                        bgNode.Kind = BackgroundActionKind.Reset;
                        break;
                    case "hide":
                        bgNode.Kind = BackgroundActionKind.HideLayer;
                        bgNode.LayerIndex = ParseIntExpression(tokens, ref index);
                        break;
                    case "show":
                        bgNode.Kind = BackgroundActionKind.ShowLayer;
                        bgNode.LayerIndex = ParseIntExpression(tokens, ref index);
                        break;
                    case "setcolor":
                        bgNode.Kind = BackgroundActionKind.SetColor;
                        bgNode.LayerIndex = ParseIntExpression(tokens, ref index);
                        Expect(tokens, ref index, TokenType.Comma);
                        bgNode.HexColor = ParseStringExpression(tokens, ref index);
                        break;
                    case "setspeed":
                        bgNode.Kind = BackgroundActionKind.SetSpeed;
                        bgNode.LayerIndex = ParseIntExpression(tokens, ref index);
                        Expect(tokens, ref index, TokenType.Comma);
                        bgNode.Value = ParseFloatExpression(tokens, ref index);
                        break;
                    case "setoffset":
                        bgNode.Kind = BackgroundActionKind.SetOffset;
                        bgNode.LayerIndex = ParseIntExpression(tokens, ref index);
                        Expect(tokens, ref index, TokenType.Comma);
                        bgNode.Value = ParseFloatExpression(tokens, ref index);
                        break;
                    case "setalpha":
                        bgNode.Kind = BackgroundActionKind.SetAlpha;
                        bgNode.LayerIndex = ParseIntExpression(tokens, ref index);
                        Expect(tokens, ref index, TokenType.Comma);
                        bgNode.Value = ParseFloatExpression(tokens, ref index);
                        break;
                    default:
                        index = saved;
                        return null;
                }
                Expect(tokens, ref index, TokenType.RightParen);
                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return bgNode;
            }

            if (string.Equals(ns, "Camera", StringComparison.OrdinalIgnoreCase))
            {
                int saved = index;
                Expect(tokens, ref index, TokenType.Dot);
                Token method = ExpectMemberToken(tokens, ref index);
                string mName = method.Text.ToLowerInvariant();
                Expect(tokens, ref index, TokenType.LeftParen);

                var camNode = new CameraNode();
                switch (mName)
                {
                    case "shake":
                        camNode.Kind = CameraActionKind.Shake;
                        camNode.Intensity = ParseFloatArg(tokens, ref index, "intensity");
                        Expect(tokens, ref index, TokenType.Comma);
                        camNode.Duration = ParseFloatArg(tokens, ref index, "duration");
                        break;
                    case "zoom":
                        camNode.Kind = CameraActionKind.Zoom;
                        camNode.Intensity = ParseFloatArg(tokens, ref index, "size");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                        {
                            camNode.Duration = ParseFloatArg(tokens, ref index, "duration");
                            if (ConsumeIf(tokens, ref index, TokenType.Comma))
                                camNode.Ease = ParseStringArg(tokens, ref index, "ease");
                        }
                        break;
                    case "panto":
                        camNode.Kind = CameraActionKind.PanTo;
                        camNode.Intensity = ParseFloatArg(tokens, ref index, "x", allowNegative: true);
                        Expect(tokens, ref index, TokenType.Comma);
                        camNode.Duration = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                        {
                            camNode.ExtraDuration = ParseFloatArg(tokens, ref index, "duration");
                            if (ConsumeIf(tokens, ref index, TokenType.Comma))
                                camNode.Ease = ParseStringArg(tokens, ref index, "ease");
                        }
                        break;
                    case "follow":
                        camNode.Kind = CameraActionKind.Follow;
                        camNode.Target = ParseGameObjectExpression(tokens, ref index);
                        break;
                    case "unfollow":
                        camNode.Kind = CameraActionKind.Unfollow;
                        break;
                    case "fade":
                        camNode.Kind = CameraActionKind.Fade;
                        camNode.Intensity = ParseFloatArg(tokens, ref index, "alpha");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                        {
                            camNode.Duration = ParseFloatArg(tokens, ref index, "duration");
                            if (ConsumeIf(tokens, ref index, TokenType.Comma))
                                camNode.HexColor = ParseStringArg(tokens, ref index, "color");
                        }
                        break;
                    default:
                        index = saved;
                        return null;
                }
                Expect(tokens, ref index, TokenType.RightParen);
                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return camNode;
            }

            if (string.Equals(ns, "World", StringComparison.OrdinalIgnoreCase))
            {
                int saved = index;
                Expect(tokens, ref index, TokenType.Dot);
                Token method = ExpectMemberToken(tokens, ref index);
                string mName = method.Text.ToLowerInvariant();
                Expect(tokens, ref index, TokenType.LeftParen);

                var wNode = new WorldNode();
                switch (mName)
                {
                    case "setgravity":
                        wNode.Kind = WorldActionKind.SetGravity;
                        wNode.GravityX = ParseFloatArg(tokens, ref index, "gravityX", allowNegative: true);
                        Expect(tokens, ref index, TokenType.Comma);
                        wNode.GravityY = ParseFloatArg(tokens, ref index, "gravityY", allowNegative: true);
                        break;
                    case "settimescale":
                        wNode.Kind = WorldActionKind.SetTimeScale;
                        wNode.GravityX = ParseFloatArg(tokens, ref index, "timeScale");
                        break;
                    case "settile":
                        wNode.Kind = WorldActionKind.SetTile;
                        wNode.TileX = ParseIntArg(tokens, ref index, "x");
                        Expect(tokens, ref index, TokenType.Comma);
                        wNode.TileY = ParseIntArg(tokens, ref index, "y");
                        Expect(tokens, ref index, TokenType.Comma);
                        wNode.TileId = ParseIntArg(tokens, ref index, "tileId");
                        break;
                    case "removetile":
                        wNode.Kind = WorldActionKind.RemoveTile;
                        wNode.TileX = ParseIntArg(tokens, ref index, "x");
                        Expect(tokens, ref index, TokenType.Comma);
                        wNode.TileY = ParseIntArg(tokens, ref index, "y");
                        break;
                    default:
                        index = saved;
                        return null;
                }
                Expect(tokens, ref index, TokenType.RightParen);
                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return wNode;
            }

            if (string.Equals(ns, "UI", StringComparison.OrdinalIgnoreCase))
            {
                int saved = index;
                Expect(tokens, ref index, TokenType.Dot);
                Token method = ExpectMemberToken(tokens, ref index);
                string mName = method.Text.ToLowerInvariant();
                Expect(tokens, ref index, TokenType.LeftParen);

                var uiNode = new UINode();
                switch (mName)
                {
                    case "showbanner":
                        uiNode.Kind = UIActionKind.ShowBanner;
                        uiNode.Text = ParseStringArg(tokens, ref index, "text");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                        {
                            uiNode.Duration = ParseFloatArg(tokens, ref index, "duration");
                            if (ConsumeIf(tokens, ref index, TokenType.Comma))
                                uiNode.HexColor = ParseStringArg(tokens, ref index, "color");
                        }
                        break;
                    case "showtimer":
                        uiNode.Kind = UIActionKind.ShowTimer;
                        uiNode.Duration = ParseFloatArg(tokens, ref index, "seconds");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                            uiNode.Text = ParseStringArg(tokens, ref index, "label");
                        break;
                    default:
                        index = saved;
                        return null;
                }
                Expect(tokens, ref index, TokenType.RightParen);
                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);
                return uiNode;
            }

            return null;
        }

        static LevelScriptNode TryParseBuiltinFunctionStatement(List<Token> tokens, ref int index, Token nameToken, bool requireSemicolon)
        {
            string name = nameToken.Text.ToLowerInvariant();
            switch (name)
            {
                case "print":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new PrintNode();
                    if (Peek(tokens, index).Type != TokenType.RightParen)
                    {
                        while (true)
                        {
                            node.Arguments.Add(ParseCallArgument(tokens, ref index));
                            if (!ConsumeIf(tokens, ref index, TokenType.Comma))
                                break;
                        }
                    }
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                // Background globals
                case "backgroundclear":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return new BackgroundNode { Kind = BackgroundActionKind.Clear };
                }
                case "backgroundreset":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return new BackgroundNode { Kind = BackgroundActionKind.Reset };
                }
                case "backgroundhide":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new BackgroundNode { Kind = BackgroundActionKind.HideLayer, LayerIndex = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "backgroundshow":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new BackgroundNode { Kind = BackgroundActionKind.ShowLayer, LayerIndex = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "backgroundcolor":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new BackgroundNode { Kind = BackgroundActionKind.SetColor, LayerIndex = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.HexColor = ParseStringExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "backgroundspeed":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new BackgroundNode { Kind = BackgroundActionKind.SetSpeed, LayerIndex = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Value = ParseFloatExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "backgroundoffset":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new BackgroundNode { Kind = BackgroundActionKind.SetOffset, LayerIndex = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Value = ParseFloatExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "backgroundalpha":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new BackgroundNode { Kind = BackgroundActionKind.SetAlpha, LayerIndex = ParseIntExpression(tokens, ref index) };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Value = ParseFloatExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }

                // Camera globals
                case "camerashake":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new CameraNode
                    {
                        Kind = CameraActionKind.Shake,
                        Intensity = ParseFloatArg(tokens, ref index, "intensity"),
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Duration = ParseFloatArg(tokens, ref index, "duration");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "camerazoom":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new CameraNode
                    {
                        Kind = CameraActionKind.Zoom,
                        Intensity = ParseFloatArg(tokens, ref index, "size")
                    };
                    if (ConsumeIf(tokens, ref index, TokenType.Comma))
                    {
                        node.Duration = ParseFloatArg(tokens, ref index, "duration");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                            node.Ease = ParseStringArg(tokens, ref index, "ease");
                    }
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "camerapanto":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new CameraNode
                    {
                        Kind = CameraActionKind.PanTo,
                        Intensity = ParseFloatArg(tokens, ref index, "x", allowNegative: true)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Duration = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
                    if (ConsumeIf(tokens, ref index, TokenType.Comma))
                    {
                        node.ExtraDuration = ParseFloatArg(tokens, ref index, "duration");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                            node.Ease = ParseStringArg(tokens, ref index, "ease");
                    }
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "camerafollow":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new CameraNode
                    {
                        Kind = CameraActionKind.Follow,
                        Target = ParseGameObjectExpression(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "cameraunfollow":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return new CameraNode { Kind = CameraActionKind.Unfollow };
                }
                case "camerafade":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new CameraNode
                    {
                        Kind = CameraActionKind.Fade,
                        Intensity = ParseFloatArg(tokens, ref index, "alpha")
                    };
                    if (ConsumeIf(tokens, ref index, TokenType.Comma))
                    {
                        node.Duration = ParseFloatArg(tokens, ref index, "duration");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                            node.HexColor = ParseStringArg(tokens, ref index, "color");
                    }
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }

                // Hitbox globals
                case "sethitboxsize":
                case "hitboxsize":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new HitboxConfigNode
                    {
                        Kind = HitboxConfigKind.Size,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.X = ParseFloatArg(tokens, ref index, "width");
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Y = ParseFloatArg(tokens, ref index, "height");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "sethitboxoffset":
                case "hitboxoffset":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new HitboxConfigNode
                    {
                        Kind = HitboxConfigKind.Offset,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.X = ParseFloatArg(tokens, ref index, "offsetX", allowNegative: true);
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Y = ParseFloatArg(tokens, ref index, "offsetY", allowNegative: true);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "sethitboxtrigger":
                case "hitboxtrigger":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new HitboxConfigNode
                    {
                        Kind = HitboxConfigKind.Trigger,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.BoolValue = ParseBoolArg(tokens, ref index, "isTrigger");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "sethitboxbounce":
                case "hitboxbounce":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new HitboxConfigNode
                    {
                        Kind = HitboxConfigKind.Bounce,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.X = ParseFloatArg(tokens, ref index, "bounciness");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "sethitboxfriction":
                case "hitboxfriction":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new HitboxConfigNode
                    {
                        Kind = HitboxConfigKind.Friction,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.X = ParseFloatArg(tokens, ref index, "friction");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }

                // Sprite globals
                case "destroysprite":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new SpriteActionNode
                    {
                        Kind = SpriteActionKind.Destroy,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "setspriteflip":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new SpriteActionNode
                    {
                        Kind = SpriteActionKind.Flip,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.FlipX = ParseBoolArg(tokens, ref index, "flipX");
                    Expect(tokens, ref index, TokenType.Comma);
                    node.FlipY = ParseBoolArg(tokens, ref index, "flipY");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "setsortingorder":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new SpriteActionNode
                    {
                        Kind = SpriteActionKind.SortingOrder,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.SortingOrder = ParseIntArg(tokens, ref index, "order");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "setspritealpha":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new SpriteActionNode
                    {
                        Kind = SpriteActionKind.Alpha,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.Alpha = ParseFloatArg(tokens, ref index, "alpha");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "setspritecolor":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new SpriteActionNode
                    {
                        Kind = SpriteActionKind.Color,
                        Targets = ParseGameObjectArrayArg(tokens, ref index)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.HexColor = ParseStringArg(tokens, ref index, "hex");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }

                // World globals
                case "setgravity":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new WorldNode
                    {
                        Kind = WorldActionKind.SetGravity,
                        GravityX = ParseFloatArg(tokens, ref index, "gravityX", allowNegative: true)
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.GravityY = ParseFloatArg(tokens, ref index, "gravityY", allowNegative: true);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "settimescale":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new WorldNode
                    {
                        Kind = WorldActionKind.SetTimeScale,
                        GravityX = ParseFloatArg(tokens, ref index, "timeScale")
                    };
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "settile":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new WorldNode
                    {
                        Kind = WorldActionKind.SetTile,
                        TileX = ParseIntArg(tokens, ref index, "x")
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.TileY = ParseIntArg(tokens, ref index, "y");
                    Expect(tokens, ref index, TokenType.Comma);
                    node.TileId = ParseIntArg(tokens, ref index, "tileId");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "removetile":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new WorldNode
                    {
                        Kind = WorldActionKind.RemoveTile,
                        TileX = ParseIntArg(tokens, ref index, "x")
                    };
                    Expect(tokens, ref index, TokenType.Comma);
                    node.TileY = ParseIntArg(tokens, ref index, "y");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }

                // UI globals
                case "showbanner":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new UINode
                    {
                        Kind = UIActionKind.ShowBanner,
                        Text = ParseStringArg(tokens, ref index, "text")
                    };
                    if (ConsumeIf(tokens, ref index, TokenType.Comma))
                    {
                        node.Duration = ParseFloatArg(tokens, ref index, "duration");
                        if (ConsumeIf(tokens, ref index, TokenType.Comma))
                            node.HexColor = ParseStringArg(tokens, ref index, "color");
                    }
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
                case "showtimer":
                {
                    Expect(tokens, ref index, TokenType.LeftParen);
                    var node = new UINode
                    {
                        Kind = UIActionKind.ShowTimer,
                        Duration = ParseFloatArg(tokens, ref index, "seconds")
                    };
                    if (ConsumeIf(tokens, ref index, TokenType.Comma))
                        node.Text = ParseStringArg(tokens, ref index, "label");
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon) ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return node;
                }
            }

            return null;
        }

        static FunctionCallNode ParseFunctionCall(
            List<Token> tokens,
            ref int index,
            string name,
            int sourceIndex,
            bool requireSemicolon)
        {
            Expect(tokens, ref index, TokenType.LeftParen);
            var call = new FunctionCallNode { Name = name, SourceIndex = sourceIndex };

            if (Peek(tokens, index).Type != TokenType.RightParen)
            {
                do
                {
                    call.Arguments.Add(ParseCallArgument(tokens, ref index));
                }
                while (ConsumeIf(tokens, ref index, TokenType.Comma));
            }

            Expect(tokens, ref index, TokenType.RightParen);
            if (requireSemicolon)
                ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return call;
        }

        static ScriptArgument ParseCallArgument(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);

            // 1. nil, localPlayer, Scene.*, Group(...), FindNearestPlayer(...), instance(...) -> GameObject
            if (token.Type == TokenType.Nil)
            {
                index++;
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.GameObject,
                    GameObjectValue = new GameObjectNilExpression()
                };
            }

            if (token.Type == TokenType.LocalPlayer)
            {
                if (index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.Dot)
                {
                    index += 2;
                    Token member = ExpectMemberToken(tokens, ref index);
                    string mText = member.Text;
                    if (string.Equals(mText, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.String,
                            StringValue = new PlayerMemberStringExpression { PlayerName = "localPlayer" }
                        };
                    }
                    if (string.Equals(mText, "x", StringComparison.OrdinalIgnoreCase) || string.Equals(mText, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerMember m = string.Equals(mText, "x", StringComparison.OrdinalIgnoreCase) ? PlayerMember.X : PlayerMember.Y;
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.Float,
                            FloatValue = new PlayerMemberFloatExpression { PlayerName = "localPlayer", Member = m }
                        };
                    }
                    throw Error(member, $"Unsupported property '{mText}' on localPlayer.");
                }

                index++;
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.GameObject,
                    GameObjectValue = new LocalPlayerExpression()
                };
            }

            if (token.Type == TokenType.Scene ||
                token.Type == TokenType.Group ||
                (token.Type == TokenType.Identifier && (string.Equals(token.Text, "FindNearestPlayer", StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(token.Text, "instance", StringComparison.OrdinalIgnoreCase))))
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.GameObject,
                    GameObjectValue = ParseGameObjectExpression(tokens, ref index)
                };
            }

            // 2. String literal or string expression
            if (token.Type == TokenType.StringLiteral)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.String,
                    StringValue = ParseStringExpression(tokens, ref index)
                };
            }

            // 3. Boolean literals or unary not
            if (token.Type == TokenType.True || token.Type == TokenType.False || token.Type == TokenType.Bang)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Bool,
                    BoolValue = ParseBoolExpression(tokens, ref index)
                };
            }

            // 4. Negative number or minus sign: -5, -(x + 1), etc.
            if (token.Type == TokenType.Minus)
            {
                FloatExpression fExpr = ParseFloatExpression(tokens, ref index);
                if (fExpr is FloatLiteralExpression fLit && Math.Abs(fLit.Value - (int)fLit.Value) < 0.00001f && !tokens[index - 1].Text.Contains('.'))
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Int,
                        IntValue = new IntLiteralExpression { Value = (int)fLit.Value }
                    };
                }
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Float,
                    FloatValue = fExpr
                };
            }

            // Number: float or int
            if (token.Type == TokenType.Number)
            {
                if (token.Text.Contains('.'))
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Float,
                        FloatValue = ParseFloatExpression(tokens, ref index)
                    };
                }

                int saved = index;
                // If followed by arithmetic operator (+ - * / %)
                if (index + 1 < tokens.Count && tokens[index + 1].Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent)
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Int,
                        IntValue = ParseIntExpression(tokens, ref index)
                    };
                }

                index++;
                if (int.TryParse(token.Text, out int intVal))
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Int,
                        IntValue = new IntLiteralExpression { Value = intVal }
                    };
                }
                index = saved;
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Float,
                    FloatValue = ParseFloatExpression(tokens, ref index)
                };
            }

            // Parenthesized expression: could be arithmetic (counter + 1), or boolean (a && b)
            if (token.Type == TokenType.LeftParen)
            {
                int saved = index;
                try
                {
                    FloatExpression fExpr = ParseFloatExpression(tokens, ref index);
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Float,
                        FloatValue = fExpr
                    };
                }
                catch
                {
                    index = saved;
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Bool,
                        BoolValue = ParseBoolExpression(tokens, ref index)
                    };
                }
            }

            // 5. Identifier: could be player.name, player.x, doors.count, counter + 1, score, or items[i]
            if (token.Type == TokenType.Identifier)
            {
                string idName = token.Text;
                int startIdx = token.StartIndex;

                // Check for built-in math functions: abs(-3), min(a, b), etc.
                if (TryMapMathFunction(idName, out _, out _) || string.Equals(idName, "random", StringComparison.OrdinalIgnoreCase))
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Float,
                        FloatValue = ParseFloatExpression(tokens, ref index)
                    };
                }

                // Lookahead: items[i]
                if (index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.LeftBracket)
                {
                    // Check if it's part of an arithmetic expression: items[i] + 1
                    int saved = index;
                    index += 2; // consume id and [
                    IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightBracket);

                    if (index < tokens.Count && tokens[index].Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent)
                    {
                        index = saved;
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.Float,
                            FloatValue = ParseFloatExpression(tokens, ref index)
                        };
                    }

                    return new ScriptArgument
                    {
                        IdentifierName = idName,
                        IndexExpr = idxExpr,
                        SourceIndex = startIdx
                    };
                }

                // Lookahead: dot access (.name, .x, .y, .count, .length)
                if (index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.Dot)
                {
                    index += 2; // consume id and .
                    Token member = ExpectMemberToken(tokens, ref index);
                    string mText = member.Text;
                    if (string.Equals(mText, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.String,
                            StringValue = new PlayerMemberStringExpression { PlayerName = idName }
                        };
                    }
                    if (string.Equals(mText, "count", StringComparison.OrdinalIgnoreCase) || string.Equals(mText, "length", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index < tokens.Count && tokens[index].Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent)
                        {
                            index -= 3;
                            return new ScriptArgument
                            {
                                Kind = ScriptValueKind.Float,
                                FloatValue = ParseFloatExpression(tokens, ref index)
                            };
                        }
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.Int,
                            IntValue = new ListCountExpression { ListName = idName }
                        };
                    }
                    if (string.Equals(mText, "x", StringComparison.OrdinalIgnoreCase) || string.Equals(mText, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerMember m = string.Equals(mText, "x", StringComparison.OrdinalIgnoreCase) ? PlayerMember.X : PlayerMember.Y;
                        if (index < tokens.Count && tokens[index].Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent)
                        {
                            index -= 3;
                            return new ScriptArgument
                            {
                                Kind = ScriptValueKind.Float,
                                FloatValue = ParseFloatExpression(tokens, ref index)
                            };
                        }
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.Float,
                            FloatValue = new PlayerMemberFloatExpression { PlayerName = idName, Member = m }
                        };
                    }
                    throw Error(member, $"Unsupported property '{mText}' on '{idName}'.");
                }

                // Check if part of an arithmetic expression: counter + 1, counter * 2
                if (index + 1 < tokens.Count && tokens[index + 1].Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent)
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Float,
                        FloatValue = ParseFloatExpression(tokens, ref index)
                    };
                }

                // Check if part of a comparison expression: a > 5, a == b
                if (index + 1 < tokens.Count && IsComparisonToken(tokens[index + 1].Type))
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Bool,
                        BoolValue = ParseBoolExpression(tokens, ref index)
                    };
                }

                // Standalone identifier: variable of any runtime type
                index++;
                return new ScriptArgument
                {
                    IdentifierName = idName,
                    SourceIndex = startIdx
                };
            }

            throw Error(token, $"Unexpected argument '{token.Text}'.");
        }

        static LevelScriptNode ParseAssignmentOrIncrement(List<Token> tokens, ref int index, bool requireSemicolon, Token nameToken = default)
        {
            if (nameToken.Type == default)
                nameToken = Expect(tokens, ref index, TokenType.Identifier);

            Token next = Peek(tokens, index);
            if (next.Type == TokenType.Dot)
            {
                int saved = index;
                index++;
                Token methodToken = ExpectMemberToken(tokens, ref index);
                
                // Check for AddComponent<Type>() or RemoveComponent<Type>()
                if ((string.Equals(methodToken.Text, "AddComponent", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(methodToken.Text, "RemoveComponent", StringComparison.OrdinalIgnoreCase)) &&
                    Peek(tokens, index).Type == TokenType.Less)
                {
                    bool isAdd = string.Equals(methodToken.Text, "AddComponent", StringComparison.OrdinalIgnoreCase);
                    Expect(tokens, ref index, TokenType.Less);
                    Token componentTypeToken = Expect(tokens, ref index, TokenType.Identifier);
                    Expect(tokens, ref index, TokenType.Greater);
                    Expect(tokens, ref index, TokenType.LeftParen);
                    Expect(tokens, ref index, TokenType.RightParen);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    // Wrap single GameObject in an array expression
                    var targetArray = new GameObjectArrayLiteralExpression();
                    targetArray.Items.Add(new GameObjectVariableExpression 
                    { 
                        Name = nameToken.Text, 
                        SourceIndex = nameToken.StartIndex 
                    });

                    if (isAdd)
                    {
                        return new AddComponentNode
                        {
                            Targets = targetArray,
                            ComponentName = new StringLiteralExpression { Value = componentTypeToken.Text }
                        };
                    }
                    else
                    {
                        return new RemoveComponentNode
                        {
                            Targets = targetArray,
                            ComponentName = new StringLiteralExpression { Value = componentTypeToken.Text }
                        };
                    }
                }
                
                // Check if there's a second dot (GameObject.Component.Property)
                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    Expect(tokens, ref index, TokenType.Dot);
                    Token propertyToken = ExpectMemberToken(tokens, ref index);
                    if (Peek(tokens, index).Type == TokenType.LeftParen)
                    {
                        Expect(tokens, ref index, TokenType.LeftParen);
                        var node = new ComponentMethodCallNode
                        {
                            ObjectName = nameToken.Text,
                            ComponentName = methodToken.Text,
                            MethodName = propertyToken.Text
                        };

                        if (Peek(tokens, index).Type != TokenType.RightParen)
                        {
                            do
                            {
                                node.Arguments.Add(ParseFloatExpression(tokens, ref index));
                            }
                            while (ConsumeIf(tokens, ref index, TokenType.Comma));
                        }

                        Expect(tokens, ref index, TokenType.RightParen);
                        if (requireSemicolon)
                            ConsumeIf(tokens, ref index, TokenType.Semicolon);

                        return node;
                    }

                    index = saved;
                    return ParseComponentPropertyAssignment(tokens, ref index, requireSemicolon, nameToken.Text, null);
                }
                else
                {
                    // Only one dot: could be Component.Method(args) or Component.Property = value
                    if (Peek(tokens, index).Type == TokenType.LeftParen)
                    {
                        Expect(tokens, ref index, TokenType.LeftParen);
                        
                        var node = new ComponentVariableMethodCallNode
                        {
                            ComponentVariableName = nameToken.Text,
                            MethodName = methodToken.Text
                        };

                        if (Peek(tokens, index).Type != TokenType.RightParen)
                        {
                            do
                            {
                                node.Arguments.Add(ParseFloatExpression(tokens, ref index));
                            }
                            while (ConsumeIf(tokens, ref index, TokenType.Comma));
                        }

                        Expect(tokens, ref index, TokenType.RightParen);
                        if (requireSemicolon)
                            ConsumeIf(tokens, ref index, TokenType.Semicolon);

                        return node;
                    }
                    
                    // Component variable.Property = value
                    index = saved;
                    return ParseComponentVariablePropertyAssignment(tokens, ref index, requireSemicolon, nameToken.Text);
                }
            }

            if (next.Type == TokenType.Equal)
            {
                index++;
                Token rhsToken = Peek(tokens, index);

                // User function call: x = calcOffset(...)
                if (IsUserFunctionCall(tokens, index))
                {
                    Token fnToken = tokens[index];
                    FunctionCallNode call = ParseFunctionCall(tokens, ref index, fnToken.Text, fnToken.StartIndex, false);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return new FloatAssignmentNode { Name = nameToken.Text, CallValue = call };
                }

                if (rhsToken.Type == TokenType.StringLiteral)
                {
                    StringExpression stringValue = ParseStringExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new StringAssignmentNode { Name = nameToken.Text, Value = stringValue };
                }

                if (rhsToken.Type == TokenType.True || rhsToken.Type == TokenType.False)
                {
                    BoolExpression boolValue = ParseBoolExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new BoolAssignmentNode { Name = nameToken.Text, Value = boolValue };
                }

                if (rhsToken.Type == TokenType.GetComponent)
                {
                    ComponentExpression componentValue = ParseComponentExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new ComponentAssignmentNode { Name = nameToken.Text, Value = componentValue };
                }

                if (rhsToken.Type == TokenType.LeftBracket)
                {
                    index++;
                    var listAssign = new ListAssignmentNode { Name = nameToken.Text };
                    if (Peek(tokens, index).Type != TokenType.RightBracket)
                    {
                        do
                        {
                            listAssign.InitialItems.Add(ParseCallArgument(tokens, ref index));
                        } while (ConsumeIf(tokens, ref index, TokenType.Comma));
                    }
                    Expect(tokens, ref index, TokenType.RightBracket);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return listAssign;
                }

                // Check for GameObject expressions: nil, localPlayer, Scene.*, FindNearestPlayer, instance, or objs[0]
                if (IsGameObjectToken(tokens, index))
                {
                    GameObjectExpression goValue = ParseGameObjectExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return new GameObjectAssignmentNode { Name = nameToken.Text, Value = goValue };
                }

                int saved = index;
                try
                {
                    IntExpression intValue = ParseIntExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new IntAssignmentNode { Name = nameToken.Text, Value = intValue };
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                }

                try
                {
                    FloatExpression floatValue = ParseFloatExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new FloatAssignmentNode { Name = nameToken.Text, Value = floatValue };
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                }

                try
                {
                    BoolExpression boolValue = ParseBoolExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new BoolAssignmentNode { Name = nameToken.Text, Value = boolValue };
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                }

                try
                {
                    StringExpression stringValue = ParseStringExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new StringAssignmentNode { Name = nameToken.Text, Value = stringValue };
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                }

                try
                {
                    GameObjectExpression goValue = ParseGameObjectExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    return new GameObjectAssignmentNode { Name = nameToken.Text, Value = goValue };
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                }

                ComponentExpression compValue = ParseComponentExpression(tokens, ref index);
                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);

                return new ComponentAssignmentNode { Name = nameToken.Text, Value = compValue };
            }

            if (next.Type is TokenType.PlusEqual or TokenType.MinusEqual or TokenType.StarEqual or TokenType.SlashEqual or TokenType.PercentEqual)
            {
                TokenType opType = next.Type;
                index++;
                Token rhsToken = Peek(tokens, index);
                if (rhsToken.Type == TokenType.StringLiteral && opType == TokenType.PlusEqual)
                {
                    StringExpression stringVal = ParseStringExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);
                    return new StringAssignmentNode
                    {
                        Name = nameToken.Text,
                        Value = new StringConcatExpression
                        {
                            Left = new StringVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex },
                            Right = stringVal
                        }
                    };
                }

                int saved = index;
                try
                {
                    IntExpression intRhs = ParseIntExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    IntBinaryOperator binOp = opType switch
                    {
                        TokenType.PlusEqual => IntBinaryOperator.Add,
                        TokenType.MinusEqual => IntBinaryOperator.Subtract,
                        TokenType.StarEqual => IntBinaryOperator.Multiply,
                        TokenType.SlashEqual => IntBinaryOperator.Divide,
                        TokenType.PercentEqual => IntBinaryOperator.Modulo,
                        _ => IntBinaryOperator.Add
                    };

                    return new IntAssignmentNode
                    {
                        Name = nameToken.Text,
                        Value = new IntBinaryExpression
                        {
                            Left = new IntVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex },
                            Operator = binOp,
                            Right = intRhs
                        }
                    };
                }
                catch (LevelScriptParseException)
                {
                    index = saved;
                    FloatExpression floatRhs = ParseFloatExpression(tokens, ref index);
                    if (requireSemicolon)
                        ConsumeIf(tokens, ref index, TokenType.Semicolon);

                    FloatBinaryOperator binOp = opType switch
                    {
                        TokenType.PlusEqual => FloatBinaryOperator.Add,
                        TokenType.MinusEqual => FloatBinaryOperator.Subtract,
                        TokenType.StarEqual => FloatBinaryOperator.Multiply,
                        TokenType.SlashEqual => FloatBinaryOperator.Divide,
                        TokenType.PercentEqual => FloatBinaryOperator.Modulo,
                        _ => FloatBinaryOperator.Add
                    };

                    return new FloatAssignmentNode
                    {
                        Name = nameToken.Text,
                        Value = new FloatBinaryExpression
                        {
                            Left = new FloatVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex },
                            Operator = binOp,
                            Right = floatRhs
                        }
                    };
                }
            }

            if (next.Type == TokenType.PlusPlus || next.Type == TokenType.MinusMinus)
            {
                index++;
                var node = new IntAssignmentNode
                {
                    Name = nameToken.Text,
                    Value = new IntBinaryExpression
                    {
                        Left = new IntVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex },
                        Right = new IntLiteralExpression { Value = 1 },
                        Operator = next.Type == TokenType.PlusPlus
                            ? IntBinaryOperator.Add
                            : IntBinaryOperator.Subtract
                    }
                };

                if (requireSemicolon)
                    ConsumeIf(tokens, ref index, TokenType.Semicolon);

                return node;
            }

            if (next.Type == TokenType.RightParen)
            {
                // Permissive for-loop increment: bare variable identifier 'i' defaults to increment by 1
                return new IntAssignmentNode
                {
                    Name = nameToken.Text,
                    Value = new IntBinaryExpression
                    {
                        Left = new IntVariableExpression { Name = nameToken.Text, SourceIndex = nameToken.StartIndex },
                        Right = new IntLiteralExpression { Value = 1 },
                        Operator = IntBinaryOperator.Add
                    }
                };
            }

            throw Error(nameToken, $"Expected assignment or increment after '{nameToken.Text}'.");
        }

        static BoolExpression ParseBoolExpression(List<Token> tokens, ref int index) => ParseBoolOr(tokens, ref index);

        static BoolExpression ParseBoolOr(List<Token> tokens, ref int index)
        {
            BoolExpression left = ParseBoolAnd(tokens, ref index);
            while (Peek(tokens, index).Type == TokenType.PipePipe)
            {
                index++;
                left = new BoolBinaryExpression
                {
                    Left = left,
                    Right = ParseBoolAnd(tokens, ref index),
                    Operator = BoolBinaryOperator.Or
                };
            }

            return left;
        }

        static BoolExpression ParseBoolAnd(List<Token> tokens, ref int index)
        {
            BoolExpression left = ParseBoolUnary(tokens, ref index);
            while (Peek(tokens, index).Type == TokenType.AmpAmp)
            {
                index++;
                left = new BoolBinaryExpression
                {
                    Left = left,
                    Right = ParseBoolUnary(tokens, ref index),
                    Operator = BoolBinaryOperator.And
                };
            }

            return left;
        }

        static BoolExpression ParseBoolUnary(List<Token> tokens, ref int index)
        {
            if (Peek(tokens, index).Type == TokenType.Bang)
            {
                index++;
                return new BoolNotExpression { Operand = ParseBoolUnary(tokens, ref index) };
            }

            return ParseBoolPrimary(tokens, ref index);
        }

        static bool IsGameObjectToken(List<Token> tokens, int index)
        {
            if (index >= tokens.Count) return false;
            TokenType t = tokens[index].Type;
            if (t == TokenType.Nil) return true;
            if (t == TokenType.LocalPlayer)
            {
                return !(index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.Dot);
            }
            if (t is TokenType.Scene or TokenType.Group) return true;
            if (t == TokenType.Identifier)
            {
                string text = tokens[index].Text;
                if (string.Equals(text, "nil", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(text, "localPlayer", StringComparison.OrdinalIgnoreCase))
                {
                    return !(index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.Dot);
                }
                if (string.Equals(text, "FindNearestPlayer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "instance", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static BoolExpression ParseBoolPrimary(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);

            // 1. true / false literals
            if (token.Type == TokenType.True)
            {
                index++;
                if (Peek(tokens, index).Type is TokenType.EqualEqual or TokenType.BangEqual)
                {
                    ComparisonOperator op = ReadComparisonOperator(tokens, ref index);
                    BoolExpression right = ParseBoolPrimary(tokens, ref index);
                    return new BoolComparisonExpression
                    {
                        Left = new BoolLiteralExpression { Value = true },
                        Right = right,
                        Operator = op
                    };
                }
                return new BoolLiteralExpression { Value = true };
            }

            if (token.Type == TokenType.False)
            {
                index++;
                if (Peek(tokens, index).Type is TokenType.EqualEqual or TokenType.BangEqual)
                {
                    ComparisonOperator op = ReadComparisonOperator(tokens, ref index);
                    BoolExpression right = ParseBoolPrimary(tokens, ref index);
                    return new BoolComparisonExpression
                    {
                        Left = new BoolLiteralExpression { Value = false },
                        Right = right,
                        Operator = op
                    };
                }
                return new BoolLiteralExpression { Value = false };
            }

            // 1b. hasTile(...) and World.hasTile(...)
            if (token.Type == TokenType.Identifier && token.Text.Equals("hasTile", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.LeftParen)
            {
                index += 2;
                IntExpression x = ParseIntArg(tokens, ref index, "x");
                Expect(tokens, ref index, TokenType.Comma);
                IntExpression y = ParseIntArg(tokens, ref index, "y");
                Expect(tokens, ref index, TokenType.RightParen);
                return new HasTileExpression { X = x, Y = y };
            }

            if (token.Type == TokenType.Identifier && token.Text.Equals("World", StringComparison.OrdinalIgnoreCase) &&
                index + 3 < tokens.Count && tokens[index + 1].Type == TokenType.Dot &&
                tokens[index + 2].Text.Equals("hasTile", StringComparison.OrdinalIgnoreCase) &&
                tokens[index + 3].Type == TokenType.LeftParen)
            {
                index += 4;
                IntExpression x = ParseIntArg(tokens, ref index, "x");
                Expect(tokens, ref index, TokenType.Comma);
                IntExpression y = ParseIntArg(tokens, ref index, "y");
                Expect(tokens, ref index, TokenType.RightParen);
                return new HasTileExpression { X = x, Y = y };
            }

            // 2. Parentheses: (a && b) vs ((x + 1) == 5)
            if (token.Type == TokenType.LeftParen)
            {
                int saved = index;
                index++;
                try
                {
                    BoolExpression nested = ParseBoolExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightParen);
                    Token afterParen = Peek(tokens, index);
                    if (afterParen.Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent ||
                        IsComparisonToken(afterParen.Type))
                    {
                        index = saved;
                    }
                    else
                    {
                        return nested;
                    }
                }
                catch
                {
                    index = saved;
                }
            }

            // 3. Unary not: !expr
            if (token.Type == TokenType.Bang)
            {
                index++;
                return new BoolNotExpression { Operand = ParseBoolPrimary(tokens, ref index) };
            }

            // 4. Standalone list indexing bool: if (flags[0])
            if (token.Type == TokenType.Identifier && index + 1 < tokens.Count && tokens[index + 1].Type == TokenType.LeftBracket)
            {
                int saved = index;
                string listName = token.Text;
                index += 2; // consume id and [
                try
                {
                    IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightBracket);
                    Token afterBracket = Peek(tokens, index);
                    if (!IsComparisonToken(afterBracket.Type) &&
                        afterBracket.Type is not (TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent))
                    {
                        return new ListItemBoolExpression { ListName = listName, Index = idxExpr };
                    }
                }
                catch
                {
                }
                index = saved;
            }

            // 5. Comparisons:
            // Check if this looks like a GameObject comparison: nil, localPlayer, Scene.*, FindNearestPlayer, instance
            if (IsGameObjectToken(tokens, index))
            {
                return ParseGameObjectComparison(tokens, ref index);
            }

            // Check if this looks like a String comparison: starts with string literal
            if (token.Type == TokenType.StringLiteral)
            {
                return ParseStringComparison(tokens, ref index);
            }

            // Check if this is an identifier with .name (string comparison)
            if (token.Type == TokenType.Identifier && index + 2 < tokens.Count &&
                tokens[index + 1].Type == TokenType.Dot &&
                string.Equals(tokens[index + 2].Text, "name", StringComparison.OrdinalIgnoreCase))
            {
                return ParseStringComparison(tokens, ref index);
            }

            // Try Float / Numeric comparison: covers x == -1, player.x > 50, doors.count > 0, arr[0] == 5, x + 1 == 5
            int savedNumeric = index;
            try
            {
                FloatExpression left = ParseFloatExpression(tokens, ref index);
                if (IsComparisonToken(Peek(tokens, index).Type))
                {
                    ComparisonOperator op = ReadComparisonOperator(tokens, ref index);
                    if (IsGameObjectToken(tokens, index))
                    {
                        index = savedNumeric;
                        return ParseGameObjectComparison(tokens, ref index);
                    }

                    FloatExpression right = ParseFloatExpression(tokens, ref index);
                    return new FloatComparisonExpression
                    {
                        Left = left,
                        Right = right,
                        Operator = op
                    };
                }
            }
            catch
            {
            }
            index = savedNumeric;

            // Try Dynamic Equality for == / !=: covers a == b, flag == true, etc.
            int savedDyn = index;
            try
            {
                ScriptArgument leftArg = ParseCallArgument(tokens, ref index);
                if (Peek(tokens, index).Type is TokenType.EqualEqual or TokenType.BangEqual)
                {
                    bool negate = Peek(tokens, index).Type == TokenType.BangEqual;
                    index++;
                    ScriptArgument rightArg = ParseCallArgument(tokens, ref index);
                    return new DynamicEqualityExpression
                    {
                        Left = leftArg,
                        Right = rightArg,
                        Negate = negate
                    };
                }
            }
            catch
            {
            }
            index = savedDyn;

            // Single identifier: standalone boolean variable
            if (token.Type == TokenType.Identifier)
            {
                index++;
                return new BoolVariableExpression { Name = token.Text, SourceIndex = token.StartIndex };
            }

            throw Error(token, $"Expected boolean expression, got '{token.Text}'.");
        }

        static BoolExpression ParseNumericComparison(List<Token> tokens, ref int index)
        {
            int saved = index;
            IntExpression intLeft = ParseIntExpression(tokens, ref index);
            if (IsComparisonToken(Peek(tokens, index).Type))
                return ParseComparison(tokens, ref index, intLeft);

            index = saved;
            return ParseFloatComparison(tokens, ref index);
        }

        static GameObjectComparisonExpression ParseGameObjectComparison(List<Token> tokens, ref int index)
        {
            GameObjectExpression left = ParseGameObjectExpression(tokens, ref index);
            ComparisonOperator comparison = ReadComparisonOperator(tokens, ref index);
            GameObjectExpression right = ParseGameObjectExpression(tokens, ref index);
            return new GameObjectComparisonExpression
            {
                Left = left,
                Right = right,
                Operator = comparison
            };
        }

        static ComparisonExpression ParseComparison(List<Token> tokens, ref int index, IntExpression left = null)
        {
            left ??= ParseIntExpression(tokens, ref index);
            ComparisonOperator comparison = ReadComparisonOperator(tokens, ref index);
            IntExpression right = ParseIntExpression(tokens, ref index);
            return new ComparisonExpression
            {
                Left = left,
                Right = right,
                Operator = comparison
            };
        }

        static FloatComparisonExpression ParseFloatComparison(List<Token> tokens, ref int index)
        {
            FloatExpression left = ParseFloatExpression(tokens, ref index);
            ComparisonOperator comparison = ReadComparisonOperator(tokens, ref index);
            FloatExpression right = ParseFloatExpression(tokens, ref index);
            return new FloatComparisonExpression
            {
                Left = left,
                Right = right,
                Operator = comparison
            };
        }

        static StringComparisonExpression ParseStringComparison(List<Token> tokens, ref int index)
        {
            StringExpression left = ParseStringExpression(tokens, ref index);
            ComparisonOperator comparison = ReadComparisonOperator(tokens, ref index);
            if (comparison is not (ComparisonOperator.Equal or ComparisonOperator.NotEqual))
                throw Error(tokens, index, "String comparisons support only == and !=.");

            StringExpression right = ParseStringExpression(tokens, ref index);
            return new StringComparisonExpression
            {
                Left = left,
                Right = right,
                Operator = comparison
            };
        }

        static ComparisonOperator ReadComparisonOperator(List<Token> tokens, ref int index)
        {
            Token op = Peek(tokens, index);
            index++;
            return op.Type switch
            {
                TokenType.Less => ComparisonOperator.Less,
                TokenType.LessOrEqual => ComparisonOperator.LessOrEqual,
                TokenType.Greater => ComparisonOperator.Greater,
                TokenType.GreaterOrEqual => ComparisonOperator.GreaterOrEqual,
                TokenType.EqualEqual => ComparisonOperator.Equal,
                TokenType.BangEqual => ComparisonOperator.NotEqual,
                _ => throw Error(op, "Expected comparison operator.")
            };
        }

        static bool IsComparisonToken(TokenType type) =>
            type is TokenType.Less or TokenType.LessOrEqual or TokenType.Greater or TokenType.GreaterOrEqual or TokenType.EqualEqual or TokenType.BangEqual;

        static IntExpression ParseIntExpression(List<Token> tokens, ref int index)
        {
            IntExpression expression = ParseIntTerm(tokens, ref index);
            while (true)
            {
                Token token = Peek(tokens, index);
                if (token.Type != TokenType.Plus && token.Type != TokenType.Minus)
                    return expression;

                index++;
                IntExpression right = ParseIntTerm(tokens, ref index);
                expression = new IntBinaryExpression
                {
                    Left = expression,
                    Right = right,
                    Operator = token.Type == TokenType.Plus
                        ? IntBinaryOperator.Add
                        : IntBinaryOperator.Subtract
                };
            }
        }

        static bool TryMapMathFunction(string name, out MathFunctionKind kind, out int argCount)
        {
            switch (name.ToLowerInvariant())
            {
                case "abs": kind = MathFunctionKind.Abs; argCount = 1; return true;
                case "min": kind = MathFunctionKind.Min; argCount = 2; return true;
                case "max": kind = MathFunctionKind.Max; argCount = 2; return true;
                case "sin": kind = MathFunctionKind.Sin; argCount = 1; return true;
                case "cos": kind = MathFunctionKind.Cos; argCount = 1; return true;
                case "sqrt": kind = MathFunctionKind.Sqrt; argCount = 1; return true;
                case "floor": kind = MathFunctionKind.Floor; argCount = 1; return true;
                case "ceil": kind = MathFunctionKind.Ceil; argCount = 1; return true;
                case "clamp": kind = MathFunctionKind.Clamp; argCount = 3; return true;
                default: kind = default; argCount = 0; return false;
            }
        }

        static IntExpression ParseIntTerm(List<Token> tokens, ref int index)
        {
            IntExpression expression = ParseIntFactor(tokens, ref index);
            while (true)
            {
                Token token = Peek(tokens, index);
                if (token.Type != TokenType.Star && token.Type != TokenType.Slash && token.Type != TokenType.Percent)
                    return expression;

                index++;
                IntExpression right = ParseIntFactor(tokens, ref index);
                expression = new IntBinaryExpression
                {
                    Left = expression,
                    Right = right,
                    Operator = token.Type switch
                    {
                        TokenType.Star => IntBinaryOperator.Multiply,
                        TokenType.Slash => IntBinaryOperator.Divide,
                        TokenType.Percent => IntBinaryOperator.Modulo,
                        _ => IntBinaryOperator.Multiply
                    }
                };
            }
        }

        static IntExpression ParseIntFactor(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            if (token.Type == TokenType.Minus)
            {
                index++;
                return new IntBinaryExpression
                {
                    Left = new IntLiteralExpression { Value = 0 },
                    Right = ParseIntFactor(tokens, ref index),
                    Operator = IntBinaryOperator.Subtract
                };
            }

            if (token.Type == TokenType.LeftParen)
            {
                index++;
                IntExpression nested = ParseIntExpression(tokens, ref index);
                Expect(tokens, ref index, TokenType.RightParen);
                return nested;
            }

            if (token.Type == TokenType.Identifier)
            {
                string name = token.Text;
                index++;

                if (Peek(tokens, index).Type == TokenType.Dot && index + 1 < tokens.Count)
                {
                    string prop = tokens[index + 1].Text;
                    if (string.Equals(prop, "count", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(prop, "length", StringComparison.OrdinalIgnoreCase))
                    {
                        index += 2;
                        return new ListCountExpression { ListName = name };
                    }
                }

                if (Peek(tokens, index).Type == TokenType.LeftBracket)
                {
                    index++;
                    IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightBracket);
                    return new ListItemIntExpression { ListName = name, Index = idxExpr };
                }

                if (Peek(tokens, index).Type == TokenType.LeftParen)
                {
                    index++;
                    if (TryMapMathFunction(name, out MathFunctionKind mathKind, out int argCount))
                    {
                        var mathExpr = new MathFunctionExpression { Kind = mathKind };
                        for (int a = 0; a < argCount; a++)
                        {
                            if (a > 0)
                                Expect(tokens, ref index, TokenType.Comma);
                            mathExpr.Arguments.Add(ParseFloatExpression(tokens, ref index));
                        }
                        Expect(tokens, ref index, TokenType.RightParen);
                        return new IntMathFunctionExpression { FloatMath = mathExpr };
                    }

                    throw Error(tokens, index, $"Unknown integer function '{name}'.");
                }

                return new IntVariableExpression { Name = name, SourceIndex = token.StartIndex };
            }

            if (token.Type == TokenType.Number)
            {
                index++;
                if (!int.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    throw Error(token,$"Expected integer value, got '{token.Text}'.");

                return new IntLiteralExpression { Value = value };
            }

            throw Error(token,$"Expected integer expression, got '{token.Text}'.");
        }

        static FloatExpression ParseFloatExpression(List<Token> tokens, ref int index)
        {
            FloatExpression expression = ParseFloatTerm(tokens, ref index);
            while (true)
            {
                Token token = Peek(tokens, index);
                if (token.Type != TokenType.Plus && token.Type != TokenType.Minus)
                    return expression;

                index++;
                FloatExpression right = ParseFloatTerm(tokens, ref index);
                expression = new FloatBinaryExpression
                {
                    Left = expression,
                    Right = right,
                    Operator = token.Type == TokenType.Plus
                        ? FloatBinaryOperator.Add
                        : FloatBinaryOperator.Subtract
                };
            }
        }

        static FloatExpression ParseFloatTerm(List<Token> tokens, ref int index)
        {
            FloatExpression expression = ParseFloatFactor(tokens, ref index);
            while (true)
            {
                Token token = Peek(tokens, index);
                if (token.Type != TokenType.Star && token.Type != TokenType.Slash && token.Type != TokenType.Percent)
                    return expression;

                index++;
                FloatExpression right = ParseFloatFactor(tokens, ref index);
                expression = new FloatBinaryExpression
                {
                    Left = expression,
                    Right = right,
                    Operator = token.Type switch
                    {
                        TokenType.Star => FloatBinaryOperator.Multiply,
                        TokenType.Slash => FloatBinaryOperator.Divide,
                        TokenType.Percent => FloatBinaryOperator.Modulo,
                        _ => FloatBinaryOperator.Multiply
                    }
                };
            }
        }

        static FloatExpression ParseFloatFactor(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            if (token.Type == TokenType.Minus)
            {
                index++;
                if (Peek(tokens, index).Type == TokenType.Number)
                {
                    Token numToken = Peek(tokens, index);
                    index++;
                    if (!float.TryParse(numToken.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float numVal))
                        throw Error(numToken, $"Expected float value, got '{numToken.Text}'.");
                    return new FloatLiteralExpression { Value = -numVal };
                }

                return new FloatBinaryExpression
                {
                    Left = new FloatLiteralExpression { Value = 0f },
                    Right = ParseFloatFactor(tokens, ref index),
                    Operator = FloatBinaryOperator.Subtract
                };
            }

            if (token.Type == TokenType.LeftParen)
            {
                index++;
                FloatExpression nested = ParseFloatExpression(tokens, ref index);
                Expect(tokens, ref index, TokenType.RightParen);
                return nested;
            }

            if (token.Type == TokenType.LeftBrace)
            {
                int savedIndex = index;
                GameObjectArrayExpression array = ParseGameObjectArrayLiteral(tokens, ref index);
                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    index++;
                    PlayerMember member = ParseMemberAxis(tokens, ref index);
                    return new GameObjectArrayMemberFloatExpression
                    {
                        Targets = array,
                        Member = member
                    };
                }

                index = savedIndex;
                throw Error(token,"{...} must be followed by .x or .y when used as float.");
            }

            if (token.Type == TokenType.Group)
            {
                index++;
                Expect(tokens, ref index, TokenType.LeftParen);

                IntExpression groupId = null;
                if (Peek(tokens, index).Type != TokenType.RightParen)
                    groupId = ParseIntExpression(tokens, ref index);

                Expect(tokens, ref index, TokenType.RightParen);
                Expect(tokens, ref index, TokenType.Dot);
                PlayerMember member = ParseMemberAxis(tokens, ref index);
                return new GroupMemberFloatExpression
                {
                    GroupId = groupId,
                    Member = member
                };
            }

            if (token.Type == TokenType.LocalPlayer)
            {
                index++;
                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    index++;
                    PlayerMember member = ParseMemberAxis(tokens, ref index);
                    return new PlayerMemberFloatExpression
                    {
                        PlayerName = "localPlayer",
                        Member = member
                    };
                }
                throw Error(token, "localPlayer must be followed by .x or .y when used as float.");
            }

            if (token.Type == TokenType.Identifier)
            {
                string name = token.Text;
                index++;

                if (Peek(tokens, index).Type == TokenType.Dot && index + 1 < tokens.Count)
                {
                    string prop = tokens[index + 1].Text;
                    if (string.Equals(prop, "count", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(prop, "length", StringComparison.OrdinalIgnoreCase))
                    {
                        index += 2;
                        return new FloatFromIntExpression
                        {
                            Value = new ListCountExpression { ListName = name }
                        };
                    }
                }

                if (Peek(tokens, index).Type == TokenType.LeftBracket)
                {
                    index++;
                    IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightBracket);
                    return new ListItemFloatExpression { ListName = name, Index = idxExpr };
                }

                if (Peek(tokens, index).Type == TokenType.LeftParen)
                {
                    index++;
                    if (string.Equals(name, "random", StringComparison.OrdinalIgnoreCase))
                    {
                        FloatExpression min = ParseFloatExpression(tokens, ref index);
                        Expect(tokens, ref index, TokenType.Comma);
                        FloatExpression max = ParseFloatExpression(tokens, ref index);
                        Expect(tokens, ref index, TokenType.RightParen);
                        return new RandomFloatExpression { Min = min, Max = max };
                    }

                    if (TryMapMathFunction(name, out MathFunctionKind mathKind, out int argCount))
                    {
                        var mathExpr = new MathFunctionExpression { Kind = mathKind };
                        for (int a = 0; a < argCount; a++)
                        {
                            if (a > 0)
                                Expect(tokens, ref index, TokenType.Comma);
                            mathExpr.Arguments.Add(ParseFloatExpression(tokens, ref index));
                        }
                        Expect(tokens, ref index, TokenType.RightParen);
                        return mathExpr;
                    }

                    throw Error(tokens, index, $"Unknown float function '{name}'.");
                }

                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    index++;
                    PlayerMember member = ParseMemberAxis(tokens, ref index);
                    return new PlayerMemberFloatExpression
                    {
                        PlayerName = name,
                        Member = member
                    };
                }

                return new FloatVariableExpression { Name = name, SourceIndex = token.StartIndex };
            }

            if (token.Type == TokenType.Number)
            {
                index++;
                if (!float.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    throw Error(token,$"Expected float value, got '{token.Text}'.");

                return new FloatLiteralExpression { Value = value };
            }

            throw Error(token,$"Expected float expression, got '{token.Text}'.");
        }

        static StringExpression ParseStringExpression(List<Token> tokens, ref int index)
        {
            StringExpression left = ParseStringPrimary(tokens, ref index);
            while (Peek(tokens, index).Type == TokenType.Plus)
            {
                index++;
                StringExpression right = ParseStringPrimary(tokens, ref index);
                left = new StringConcatExpression { Left = left, Right = right };
            }
            return left;
        }

        static StringExpression ParseStringPrimary(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            if (token.Type == TokenType.StringLiteral)
            {
                index++;
                return new StringLiteralExpression { Value = token.Text };
            }

            if (token.Type == TokenType.Number)
            {
                index++;
                return new StringLiteralExpression { Value = token.Text };
            }

            if (token.Type == TokenType.Minus)
            {
                index++;
                Token numToken = Expect(tokens, ref index, TokenType.Number);
                return new StringLiteralExpression { Value = "-" + numToken.Text };
            }

            if (token.Type == TokenType.True)
            {
                index++;
                return new StringLiteralExpression { Value = "true" };
            }

            if (token.Type == TokenType.False)
            {
                index++;
                return new StringLiteralExpression { Value = "false" };
            }

            if (token.Type == TokenType.LocalPlayer)
            {
                index++;
                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    index++;
                    Token memberToken = Expect(tokens, ref index, TokenType.Identifier);
                    string mText = memberToken.Text.ToLowerInvariant();
                    if (mText == "name")
                        return new PlayerMemberStringExpression { PlayerName = "localPlayer" };
                    if (mText == "x" || mText == "y")
                    {
                        return new StringConvertExpression
                        {
                            FloatExpr = new PlayerMemberFloatExpression
                            {
                                PlayerName = "localPlayer",
                                Member = mText == "x" ? PlayerMember.X : PlayerMember.Y
                            }
                        };
                    }
                    throw Error(memberToken, $"Unsupported property '{memberToken.Text}' on localPlayer in string expression.");
                }

                return new PlayerMemberStringExpression { PlayerName = "localPlayer" };
            }

            if (token.Type == TokenType.Identifier)
            {
                string name = token.Text;
                index++;

                if (Peek(tokens, index).Type == TokenType.LeftBracket)
                {
                    index++;
                    IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightBracket);
                    return new ListItemStringExpression { ListName = name, Index = idxExpr };
                }

                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    index++;
                    Token memberToken = Expect(tokens, ref index, TokenType.Identifier);
                    string mText = memberToken.Text.ToLowerInvariant();
                    if (mText == "name")
                        return new PlayerMemberStringExpression { PlayerName = name };
                    if (mText == "x" || mText == "y")
                    {
                        return new StringConvertExpression
                        {
                            FloatExpr = new PlayerMemberFloatExpression
                            {
                                PlayerName = name,
                                Member = mText == "x" ? PlayerMember.X : PlayerMember.Y
                            }
                        };
                    }
                    if (mText == "count" || mText == "length")
                    {
                        return new StringConvertExpression
                        {
                            IntExpr = new ListCountExpression { ListName = name }
                        };
                    }

                    throw Error(memberToken, $"Unknown member '{memberToken.Text}' on '{name}'. Use x, y, name, count, or length.");
                }

                return new StringConvertExpression { VariableName = name };
            }

            if (token.Type == TokenType.LeftParen)
            {
                int saved = index;
                index++;
                // Try parsing as float/numeric expression first, e.g. (counter * 2.5) or (x + 1)
                try
                {
                    FloatExpression floatExpr = ParseFloatExpression(tokens, ref index);
                    if (Peek(tokens, index).Type == TokenType.RightParen)
                    {
                        index++;
                        return new StringConvertExpression { FloatExpr = floatExpr };
                    }
                }
                catch
                {
                }

                // Fall back to StringExpression
                index = saved + 1;
                try
                {
                    StringExpression nested = ParseStringExpression(tokens, ref index);
                    if (Peek(tokens, index).Type == TokenType.RightParen)
                    {
                        index++;
                        return nested;
                    }
                }
                catch
                {
                }

                index = saved;
                throw Error(token, $"Failed to parse parenthesized expression in string concatenation.");
            }

            throw Error(token,$"Expected string expression, got '{token.Text}'.");
        }

        static PlayerMember ParseMemberAxis(List<Token> tokens, ref int index)
        {
            Token memberToken = Expect(tokens, ref index, TokenType.Identifier);
            return memberToken.Text.ToLowerInvariant() switch
            {
                "x" => PlayerMember.X,
                "y" => PlayerMember.Y,
                _ => throw Error(memberToken, $"Unknown member '{memberToken.Text}'. Use x or y.")
            };
        }

        static MoveNode ParseMove(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Move);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression dx = ParseFloatArg(tokens, ref index, "dx", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression dy = ParseFloatArg(tokens, ref index, "dy", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression ease = ParseStringArg(tokens, ref index, "ease");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new MoveNode
            {
                Targets = targets,
                DeltaX = dx,
                DeltaY = dy,
                Duration = duration,
                Ease = ease
            };
        }

        static AnimateNode ParseAnimate(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Animate);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targetGroup = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            GameObjectArrayExpression keyGroup = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new AnimateNode
            {
                TargetGroup = targetGroup,
                KeyGroup = keyGroup
            };
        }

        static SpawnExecutorNode ParseSpawnExecutor(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SpawnExecutor);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);
            return new SpawnExecutorNode { Targets = targets };
        }

        static RotateNode ParseRotate(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Rotate);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            IntExpression pivot = null;
            GameObjectArrayExpression pivotObjects = null;
            Token pivotToken = Peek(tokens, index);
            if (pivotToken.Type == TokenType.LeftBrace || pivotToken.Type == TokenType.Group)
                pivotObjects = ParseGameObjectArrayArg(tokens, ref index);
            else
                pivot = ParseIntArg(tokens, ref index, "pivot group");
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression degrees = ParseFloatArg(tokens, ref index, "degrees", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression ease = ParseStringArg(tokens, ref index, "ease");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new RotateNode
            {
                Targets = targets,
                PivotGroup = pivot,
                PivotObjects = pivotObjects,
                Degrees = degrees,
                Duration = duration,
                Ease = ease
            };
        }

        static RotateToNode ParseRotateTo(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.RotateTo);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            GameObjectArrayExpression pivotObjects = null;
            Token maybePivot = Peek(tokens, index);
            if (maybePivot.Type == TokenType.LeftBrace || maybePivot.Type == TokenType.Group)
            {
                pivotObjects = ParseGameObjectArrayArg(tokens, ref index);
                Expect(tokens, ref index, TokenType.Comma);
            }

            FloatExpression targetX = ParseFloatArg(tokens, ref index, "targetX", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression targetY = ParseFloatArg(tokens, ref index, "targetY", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression offset = ParseFloatArg(tokens, ref index, "offset", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression ease = ParseStringArg(tokens, ref index, "ease");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new RotateToNode
            {
                Targets = targets,
                PivotObjects = pivotObjects,
                TargetX = targetX,
                TargetY = targetY,
                OffsetDegrees = offset,
                Duration = duration,
                Ease = ease
            };
        }

        static WaitNode ParseWait(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Wait);
            Expect(tokens, ref index, TokenType.LeftParen);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new WaitNode { Duration = duration };
        }

        static GotoNode ParseGoto(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Goto);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression x = ParseFloatArg(tokens, ref index, "x", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression y = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression ease = ParseStringArg(tokens, ref index, "ease");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new GotoNode
            {
                Targets = targets,
                X = x,
                Y = y,
                Duration = duration,
                Ease = ease
            };
        }

        static ScaleToNode ParseScaleTo(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.ScaleTo);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression scaleX = ParseFloatArg(tokens, ref index, "scaleX");
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression scaleY = ParseFloatArg(tokens, ref index, "scaleY");
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression ease = ParseStringArg(tokens, ref index, "ease");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new ScaleToNode
            {
                Targets = targets,
                ScaleX = scaleX,
                ScaleY = scaleY,
                Duration = duration,
                Ease = ease
            };
        }

        static TransparencyNode ParseTransparency(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Transparency);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression value = ParseFloatArg(tokens, ref index, "value");
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression duration = ParseFloatArg(tokens, ref index, "time");
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression ease = ParseStringArg(tokens, ref index, "anim");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new TransparencyNode
            {
                Targets = targets,
                Value = value,
                Duration = duration,
                Ease = ease
            };
        }

        static VisibilityNode ParseVisibility(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Visibility);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            BoolExpression visible = ParseBoolArg(tokens, ref index, "visible");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new VisibilityNode
            {
                Targets = targets,
                Visible = visible
            };
        }

        static PlaySoundNode ParsePlaySound(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.PlaySound);
            Expect(tokens, ref index, TokenType.LeftParen);
            FloatExpression x = ParseFloatArg(tokens, ref index, "x", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression y = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression soundName = ParseStringArg(tokens, ref index, "name");
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression volume = ParseFloatArg(tokens, ref index, "volume");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new PlaySoundNode
            {
                X = x,
                Y = y,
                SoundName = soundName,
                Volume = volume
            };
        }

        static SpawnPointNode ParseSpawnPoint(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SpawnPoint);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectExpression player = ParseGameObjectExpression(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression x = ParseFloatArg(tokens, ref index, "x", allowNegative: true);
            Expect(tokens, ref index, TokenType.Comma);
            FloatExpression y = ParseFloatArg(tokens, ref index, "y", allowNegative: true);
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new SpawnPointNode
            {
                Player = player,
                X = x,
                Y = y
            };
        }

        static SpawnPointsNode ParseSpawnPoints(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SpawnPoints);
            Expect(tokens, ref index, TokenType.LeftParen);
            IntExpression from = ParseIntArg(tokens, ref index, "from");
            Expect(tokens, ref index, TokenType.Comma);
            IntExpression to = ParseIntArg(tokens, ref index, "to");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new SpawnPointsNode
            {
                From = from,
                To = to
            };
        }

        static HitboxNode ParseHitbox(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Hitbox);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            BoolExpression enabled = ParseBoolArg(tokens, ref index, "enabled");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new HitboxNode
            {
                Targets = targets,
                Enabled = enabled
            };
        }

        static SetTextureNode ParseSetTexture(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SetTexture);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression texture = ParseStringArg(tokens, ref index, "texture");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new SetTextureNode
            {
                Targets = targets,
                TextureId = texture
            };
        }

        static SetColorNode ParseSetColor(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.SetColor);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.Comma);
            StringExpression hex = ParseStringArg(tokens, ref index, "hex");
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new SetColorNode
            {
                Targets = targets,
                HexColor = hex
            };
        }

        static DestroyNode ParseDestroy(List<Token> tokens, ref int index)
        {
            Expect(tokens, ref index, TokenType.Destroy);
            Expect(tokens, ref index, TokenType.LeftParen);
            GameObjectArrayExpression targets = ParseGameObjectArrayArg(tokens, ref index);
            Expect(tokens, ref index, TokenType.RightParen);
            ConsumeIf(tokens, ref index, TokenType.Semicolon);

            return new DestroyNode
            {
                Targets = targets
            };
        }

        static BoolExpression ParseBoolArg(List<Token> tokens, ref int index, string name)
        {
            _ = name;
            return ParseBoolExpression(tokens, ref index);
        }

        static FloatExpression ParseFloatArg(List<Token> tokens, ref int index, string name, bool allowNegative = false)
        {
            FloatExpression expression = ParseFloatExpression(tokens, ref index);
            if (!allowNegative && expression is FloatLiteralExpression literal && literal.Value < 0f)
                throw Error(tokens, index, $"{name} must be >= 0.");

            return expression;
        }

        static StringExpression ParseStringArg(List<Token> tokens, ref int index, string name)
        {
            try
            {
                return ParseStringExpression(tokens, ref index);
            }
            catch (LevelScriptParseException ex)
            {
                if (ex.StartIndex >= 0)
                    throw;

                throw Error(tokens, index, $"Invalid string expression for {name}: {ex.Message}");
            }
        }

        static IntExpression ParseIntArg(List<Token> tokens, ref int index, string name)
        {
            try
            {
                return ParseIntExpression(tokens, ref index);
            }
            catch (LevelScriptParseException ex)
            {
                if (ex.StartIndex >= 0)
                    throw;

                throw Error(tokens, index, $"Invalid integer expression for {name}: {ex.Message}");
            }
        }

        static Token Expect(List<Token> tokens, ref int index, TokenType type)
        {
            Token token = Peek(tokens, index);
            if (token.Type != type)
                throw Error(tokens, index, $"Expected {type}, got '{token.Text}'.");

            index++;
            return token;
        }

        static Token ExpectMemberToken(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            if (token.Type == TokenType.Identifier || IsKeyword(token.Type))
            {
                index++;
                return token;
            }
            throw Error(tokens, index, $"Expected Identifier, got '{token.Text}'.");
        }

        static bool IsKeyword(TokenType type) =>
            type >= TokenType.Spawn && type <= TokenType.Continue &&
            type != TokenType.LeftBracket && type != TokenType.RightBracket;

        static Token Peek(List<Token> tokens, int index)
        {
            var state = CurrentState;
            int srcLen = state?.source != null ? state.source.Length : 0;
            return (index >= 0 && index < tokens.Count) ? tokens[index] : new Token(TokenType.End, string.Empty, srcLen);
        }

        static bool ConsumeIf(List<Token> tokens, ref int index, TokenType type)
        {
            if (Peek(tokens, index).Type != type)
                return false;

            index++;
            return true;
        }

        static void IndexToLineColumn(string source, int index, out int line, out int column)
        {
            line = 1;
            column = 1;
            if (string.IsNullOrEmpty(source))
                return;

            index = Math.Clamp(index, 0, source.Length);
            for (int i = 0; i < index; i++)
            {
                if (source[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
        }

        static LevelScriptParseException ErrorAt(int index, string message)
        {
            var state = CurrentState;
            string src = state?.source ?? string.Empty;
            string path = state?.sourcePath ?? string.Empty;
            IndexToLineColumn(src, index, out int line, out int column);
            return new LevelScriptParseException(line, column, index, message, path);
        }

        static LevelScriptParseException Error(List<Token> tokens, int index, string message)
        {
            Token token = Peek(tokens, index);
            if (token.StartIndex >= 0)
                return ErrorAt(token.StartIndex, message);

            return ErrorAt(0, message);
        }

        static LevelScriptParseException Error(Token token, string message)
        {
            if (token.StartIndex >= 0)
                return ErrorAt(token.StartIndex, message);

            return ErrorAt(0, message);
        }

        static List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < source.Length)
            {
                char c = source[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    i += 2;
                    while (i < source.Length && source[i] != '\n')
                        i++;
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                        i++;
                    if (i + 1 < source.Length)
                        i += 2;
                    else
                        i = source.Length;
                    continue;
                }

                if (c == '"')
                {
                    int quoteStart = i;
                    i++;
                    var sb = new StringBuilder();
                    while (i < source.Length && source[i] != '"')
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            i++;
                            sb.Append(source[i] switch
                            {
                                'n' => '\n',
                                'r' => '\r',
                                't' => '\t',
                                '"' => '"',
                                '\\' => '\\',
                                _ => source[i]
                            });
                            i++;
                            continue;
                        }

                        sb.Append(source[i]);
                        i++;
                    }

                    if (i >= source.Length)
                        throw ErrorAt(i, "Unterminated string literal.");

                    i++;
                    tokens.Add(new Token(TokenType.StringLiteral, sb.ToString(), quoteStart));
                    continue;
                }

                switch (c)
                {
                    case '(':
                        tokens.Add(new Token(TokenType.LeftParen, "(", i));
                        i++;
                        continue;
                    case ')':
                        tokens.Add(new Token(TokenType.RightParen, ")", i));
                        i++;
                        continue;
                    case '[':
                        tokens.Add(new Token(TokenType.LeftBracket, "[", i));
                        i++;
                        continue;
                    case ']':
                        tokens.Add(new Token(TokenType.RightBracket, "]", i));
                        i++;
                        continue;
                    case '{':
                        tokens.Add(new Token(TokenType.LeftBrace, "{", i));
                        i++;
                        continue;
                    case '}':
                        tokens.Add(new Token(TokenType.RightBrace, "}", i));
                        i++;
                        continue;
                    case ',':
                        tokens.Add(new Token(TokenType.Comma, ",", i));
                        i++;
                        continue;
                    case ';':
                        tokens.Add(new Token(TokenType.Semicolon, ";", i));
                        i++;
                        continue;
                    case '.':
                        if (i + 1 < source.Length && char.IsDigit(source[i + 1]))
                            break;
                        tokens.Add(new Token(TokenType.Dot, ".", i));
                        i++;
                        continue;
                    case '+':
                        if (i + 1 < source.Length && source[i + 1] == '+')
                        {
                            tokens.Add(new Token(TokenType.PlusPlus, "++", i));
                            i += 2;
                            continue;
                        }
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.PlusEqual, "+=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Plus, "+", i));
                        i++;
                        continue;
                    case '-':
                        if (i + 1 < source.Length && source[i + 1] == '-')
                        {
                            tokens.Add(new Token(TokenType.MinusMinus, "--", i));
                            i += 2;
                            continue;
                        }
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.MinusEqual, "-=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Minus, "-", i));
                        i++;
                        continue;
                    case '*':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.StarEqual, "*=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Star, "*", i));
                        i++;
                        continue;
                    case '/':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.SlashEqual, "/=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Slash, "/", i));
                        i++;
                        continue;
                    case '%':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.PercentEqual, "%=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Percent, "%", i));
                        i++;
                        continue;
                    case '=':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.EqualEqual, "==", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Equal, "=", i));
                        i++;
                        continue;
                    case '!':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.BangEqual, "!=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Bang, "!", i));
                        i++;
                        continue;
                    case '&':
                        if (i + 1 < source.Length && source[i + 1] == '&')
                        {
                            tokens.Add(new Token(TokenType.AmpAmp, "&&", i));
                            i += 2;
                            continue;
                        }
                        break;
                    case '|':
                        if (i + 1 < source.Length && source[i + 1] == '|')
                        {
                            tokens.Add(new Token(TokenType.PipePipe, "||", i));
                            i += 2;
                            continue;
                        }
                        break;
                    case '<':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.LessOrEqual, "<=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Less, "<", i));
                        i++;
                        continue;
                    case '>':
                        if (i + 1 < source.Length && source[i + 1] == '=')
                        {
                            tokens.Add(new Token(TokenType.GreaterOrEqual, ">=", i));
                            i += 2;
                            continue;
                        }
                        tokens.Add(new Token(TokenType.Greater, ">", i));
                        i++;
                        continue;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    i++;
                    while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                        i++;

                    tokens.Add(new Token(TokenType.Number, source.Substring(start, i - start), start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    i++;
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                        i++;

                    tokens.Add(MapKeyword(source.Substring(start, i - start), start));
                    continue;
                }

                throw ErrorAt(i, $"Unexpected character '{c}'.");
            }

            tokens.Add(new Token(TokenType.End, string.Empty, source.Length));
            return tokens;
        }

        static Token MapKeyword(string word, int startIndex)
        {
            switch (word.ToLowerInvariant())
            {
                case "spawn": return new Token(TokenType.Spawn, word, startIndex);
                case "while": return new Token(TokenType.While, word, startIndex);
                case "for": return new Token(TokenType.For, word, startIndex);
                case "if": return new Token(TokenType.If, word, startIndex);
                case "else": return new Token(TokenType.Else, word, startIndex);
                case "function": return new Token(TokenType.Function, word, startIndex);
                case "int": return new Token(TokenType.Int, word, startIndex);
                case "bool": return new Token(TokenType.Bool, word, startIndex);
                case "float": return new Token(TokenType.Float, word, startIndex);
                case "string": return new Token(TokenType.String, word, startIndex);
                case "gameobject": return new Token(TokenType.GameObject, word, startIndex);
                case "component": return new Token(TokenType.Component, word, startIndex);
                case "scene": return new Token(TokenType.Scene, word, startIndex);
                case "getcomponent": return new Token(TokenType.GetComponent, word, startIndex);
                case "true": return new Token(TokenType.True, word, startIndex);
                case "false": return new Token(TokenType.False, word, startIndex);
                case "nil": return new Token(TokenType.Nil, word, startIndex);
                case "move": return new Token(TokenType.Move, word, startIndex);
                case "animate": return new Token(TokenType.Animate, word, startIndex);
                case "spawnexecutor": return new Token(TokenType.SpawnExecutor, word, startIndex);
                case "rotate": return new Token(TokenType.Rotate, word, startIndex);
                case "wait": return new Token(TokenType.Wait, word, startIndex);
                case "goto": return new Token(TokenType.Goto, word, startIndex);
                case "scaleto": return new Token(TokenType.ScaleTo, word, startIndex);
                case "visibility": return new Token(TokenType.Visibility, word, startIndex);
                case "transparency": return new Token(TokenType.Transparency, word, startIndex);
                case "playsound": return new Token(TokenType.PlaySound, word, startIndex);
                case "spawnpoint": return new Token(TokenType.SpawnPoint, word, startIndex);
                case "spawnpoints": return new Token(TokenType.SpawnPoints, word, startIndex);
                case "hitbox": return new Token(TokenType.Hitbox, word, startIndex);
                case "rotateto": return new Token(TokenType.RotateTo, word, startIndex);
                case "instance": return new Token(TokenType.Instance, word, startIndex);
                case "settexture": return new Token(TokenType.SetTexture, word, startIndex);
                case "setcolor": return new Token(TokenType.SetColor, word, startIndex);
                case "destroy": return new Token(TokenType.Destroy, word, startIndex);
                case "ontriggerenter": return new Token(TokenType.OnTriggerEnter, word, startIndex);
                case "ontriggerstay": return new Token(TokenType.OnTriggerStay, word, startIndex);
                case "ontriggerexit": return new Token(TokenType.OnTriggerExit, word, startIndex);
                case "onplayeradded": return new Token(TokenType.OnPlayerAdded, word, startIndex);
                case "onclientevent": return new Token(TokenType.OnClientEvent, word, startIndex);
                case "onserverevent": return new Token(TokenType.OnServerEvent, word, startIndex);
                case "sendtoserver": return new Token(TokenType.SendToServer, word, startIndex);
                case "sendtoclient": return new Token(TokenType.SendToClient, word, startIndex);
                case "sendtoallclients": return new Token(TokenType.SendToAllClients, word, startIndex);
                case "localplayer": return new Token(TokenType.LocalPlayer, word, startIndex);
                case "network": return new Token(TokenType.Network, word, startIndex);
                case "list": return new Token(TokenType.List, word, startIndex);
                case "group": return new Token(TokenType.Group, word, startIndex);
                case "public": return new Token(TokenType.Public, word, startIndex);
                case "private": return new Token(TokenType.Private, word, startIndex);
                case "using": return new Token(TokenType.Using, word, startIndex);
                case "return": return new Token(TokenType.Return, word, startIndex);
                case "break": return new Token(TokenType.Break, word, startIndex);
                case "continue": return new Token(TokenType.Continue, word, startIndex);
                default:
                    return new Token(TokenType.Identifier, word, startIndex);
            }
        }
    }
}