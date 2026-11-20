with open('Assets/Scripts/LevelScript/LevelScriptParser.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Add case "print" to TryParseBuiltinFunctionStatement
target_builtin = '''        static LevelScriptNode TryParseBuiltinFunctionStatement(List<Token> tokens, ref int index, Token nameToken, bool requireSemicolon)
        {
            string name = nameToken.Text.ToLowerInvariant();
            switch (name)
            {'''

replacement_builtin = '''        static LevelScriptNode TryParseBuiltinFunctionStatement(List<Token> tokens, ref int index, Token nameToken, bool requireSemicolon)
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
                }'''

assert target_builtin in text, "target_builtin not found in LevelScriptParser.cs"
text = text.replace(target_builtin, replacement_builtin, 1)

# 2. Update ParseCallArgument
target_arg = '''        static ScriptArgument ParseCallArgument(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            if (token.Type == TokenType.Identifier && token.Text == "FindNearestPlayer")
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.GameObject,
                    GameObjectValue = ParseGameObjectExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.StringLiteral)
            {
                index++;
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.String,
                    StringValue = new StringLiteralExpression { Value = token.Text }
                };
            }

            if (token.Type == TokenType.True || token.Type == TokenType.False ||
                token.Type == TokenType.Bang || token.Type == TokenType.LeftParen)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Bool,
                    BoolValue = ParseBoolExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.Number && token.Text.Contains('.'))
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Float,
                    FloatValue = ParseFloatExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.Number)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Int,
                    IntValue = ParseIntExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.Identifier)
            {
                index++;
                return new ScriptArgument { IdentifierName = token.Text, SourceIndex = token.StartIndex };
            }

            throw Error(token,$"Unexpected argument '{token.Text}'.");
        }'''

replacement_arg = '''        static ScriptArgument ParseCallArgument(List<Token> tokens, ref int index)
        {
            Token token = Peek(tokens, index);
            if (token.Type == TokenType.Group ||
                (token.Type == TokenType.Identifier && (token.Text == "FindNearestPlayer" || token.Text == "self" || token.Text == "localPlayer")))
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.GameObject,
                    GameObjectValue = ParseGameObjectExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.StringLiteral)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.String,
                    StringValue = ParseStringExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.True || token.Type == TokenType.False || token.Type == TokenType.Bang)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Bool,
                    BoolValue = ParseBoolExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.Number && token.Text.Contains('.'))
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Float,
                    FloatValue = ParseFloatExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.Number)
            {
                return new ScriptArgument
                {
                    Kind = ScriptValueKind.Int,
                    IntValue = ParseIntExpression(tokens, ref index)
                };
            }

            if (token.Type == TokenType.LeftParen)
            {
                int saved = index;
                try
                {
                    return new ScriptArgument
                    {
                        Kind = ScriptValueKind.Int,
                        IntValue = ParseIntExpression(tokens, ref index)
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

            if (token.Type == TokenType.Identifier)
            {
                string idName = token.Text;
                int startIdx = token.StartIndex;
                index++;

                if (Peek(tokens, index).Type == TokenType.LeftBracket)
                {
                    index++;
                    IntExpression idxExpr = ParseIntExpression(tokens, ref index);
                    Expect(tokens, ref index, TokenType.RightBracket);
                    return new ScriptArgument
                    {
                        IdentifierName = idName,
                        IndexExpr = idxExpr,
                        SourceIndex = startIdx
                    };
                }

                if (Peek(tokens, index).Type == TokenType.Dot)
                {
                    index++;
                    Token member = Expect(tokens, ref index, TokenType.Identifier);
                    string mText = member.Text;
                    if (mText == "count" || mText == "length")
                    {
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.Int,
                            IntValue = new ListCountExpression { ListName = idName }
                        };
                    }
                    if (mText == "x" || mText == "y")
                    {
                        return new ScriptArgument
                        {
                            Kind = ScriptValueKind.Float,
                            FloatValue = new PlayerMemberFloatExpression
                            {
                                PlayerName = idName,
                                Member = mText == "x" ? PlayerMember.X : PlayerMember.Y
                            }
                        };
                    }
                    throw Error(member, $"Unsupported property '{mText}' on '{idName}'.");
                }

                return new ScriptArgument { IdentifierName = idName, SourceIndex = startIdx };
            }

            throw Error(token,$"Unexpected argument '{token.Text}'.");
        }'''

assert target_arg in text, "target_arg not found in LevelScriptParser.cs"
text = text.replace(target_arg, replacement_arg, 1)

with open('Assets/Scripts/LevelScript/LevelScriptParser.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LevelScriptParser.cs")
