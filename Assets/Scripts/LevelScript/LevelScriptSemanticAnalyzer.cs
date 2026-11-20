using System;
using System.Collections.Generic;

namespace KaplaoGames.LevelScript
{
    /// <summary>
    /// Static checks for unknown / inaccessible variables and function calls.
    /// </summary>
    public static class LevelScriptSemanticAnalyzer
    {
        public static void Analyze(LevelScriptLinkedProgram linked, string source)
        {
            if (linked?.Entry == null)
                return;

            source ??= string.Empty;
            string modulePath = linked.Entry.SourcePath ?? string.Empty;
            var globals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAccessibleGlobals(linked, modulePath, globals);

            var scopes = new List<HashSet<string>> { new HashSet<string>(globals, StringComparer.OrdinalIgnoreCase) };

            for (int i = 0; i < linked.Entry.Functions.Count; i++)
            {
                FunctionDefinitionNode function = linked.Entry.Functions[i];
                PushScope(scopes);
                for (int p = 0; p < function.Parameters.Count; p++)
                    Declare(scopes, function.Parameters[p].Name);
                CheckBody(linked, modulePath, function.Body, scopes, source);
                PopScope(scopes);
            }

            for (int i = 0; i < linked.Entry.TriggerHandlers.Count; i++)
            {
                TriggerHandlerNode handler = linked.Entry.TriggerHandlers[i];
                PushScope(scopes);
                if (!string.IsNullOrEmpty(handler.GameObjectParameterName))
                    Declare(scopes, handler.GameObjectParameterName);
                CheckGameObjectArray(handler.Source, linked, modulePath, scopes, source);
                CheckBody(linked, modulePath, handler.Body, scopes, source);
                PopScope(scopes);
            }

            for (int i = 0; i < linked.Entry.PlayerAddedHandlers.Count; i++)
            {
                PlayerAddedHandlerNode handler = linked.Entry.PlayerAddedHandlers[i];
                PushScope(scopes);
                if (!string.IsNullOrEmpty(handler.PlayerParameterName))
                    Declare(scopes, handler.PlayerParameterName);
                CheckBody(linked, modulePath, handler.Body, scopes, source);
                PopScope(scopes);
            }



            for (int i = 0; i < linked.Entry.EventListeners.Count; i++)
            {
                EventListenerNode handler = linked.Entry.EventListeners[i];
                PushScope(scopes);
                if (!string.IsNullOrEmpty(handler.PlayerParameterName))
                    Declare(scopes, handler.PlayerParameterName);
                for (int p = 0; p < handler.ParameterNames.Count; p++)
                    Declare(scopes, handler.ParameterNames[p]);
                CheckBody(linked, modulePath, handler.Body, scopes, source);
                PopScope(scopes);
            }

            for (int i = 0; i < linked.Entry.Spawns.Count; i++)
                CheckBody(linked, modulePath, linked.Entry.Spawns[i].Body, scopes, source);

            for (int i = 0; i < linked.Entry.SpawnPointsDeclarations.Count; i++)
            {
                SpawnPointsNode node = linked.Entry.SpawnPointsDeclarations[i];
                CheckInt(node.From, linked, modulePath, scopes, source);
                CheckInt(node.To, linked, modulePath, scopes, source);
            }

            CheckBody(linked, modulePath, linked.Entry.MainBody, scopes, source);
        }

        static void CollectAccessibleGlobals(
            LevelScriptLinkedProgram linked,
            string modulePath,
            HashSet<string> globals)
        {
            foreach (KeyValuePair<string, LevelScriptSymbolInfo> pair in linked.Symbols)
            {
                LevelScriptSymbolInfo info = pair.Value;
                if (info.Kind != LevelScriptSymbolKind.Variable)
                    continue;

                if (info.IsPublic ||
                    string.Equals(info.ModulePath, modulePath, StringComparison.OrdinalIgnoreCase))
                {
                    globals.Add(info.Name);
                }
            }

            // Declarations in this file that may not yet be in Symbols if empty/private-only path.
            for (int i = 0; i < linked.Entry.MainBody.Count; i++)
            {
                if (TryGetDeclarationName(linked.Entry.MainBody[i], out string name))
                    globals.Add(name);
            }
        }

        static void CheckBody(
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<LevelScriptNode> body,
            List<HashSet<string>> scopes,
            string source,
            int loopDepth = 0)
        {
            for (int i = 0; i < body.Count; i++)
                CheckNode(linked, modulePath, body[i], scopes, source, loopDepth);
        }

        static void CheckNode(
            LevelScriptLinkedProgram linked,
            string modulePath,
            LevelScriptNode node,
            List<HashSet<string>> scopes,
            string source,
            int loopDepth = 0)
        {
            if (node == null)
                return;

            switch (node)
            {
                case ReturnNode ret:
                    CheckInt(ret.IntValue, linked, modulePath, scopes, source);
                    CheckFloat(ret.FloatValue, linked, modulePath, scopes, source);
                    CheckBool(ret.BoolValue, linked, modulePath, scopes, source);
                    CheckString(ret.StringValue, linked, modulePath, scopes, source);
                    break;
                case BreakNode b:
                    if (loopDepth <= 0)
                        throw SemanticError(source, b.SourceIndex, "The 'break' statement cannot be used outside of a loop.");
                    break;
                case ContinueNode c:
                    if (loopDepth <= 0)
                        throw SemanticError(source, c.SourceIndex, "The 'continue' statement cannot be used outside of a loop.");
                    break;
                case IntDeclarationNode d:
                    CheckInt(d.Value, linked, modulePath, scopes, source);
                    Declare(scopes, d.Name);
                    break;
                case BoolDeclarationNode d:
                    CheckBool(d.Value, linked, modulePath, scopes, source);
                    Declare(scopes, d.Name);
                    break;
                case FloatDeclarationNode d:
                    CheckFloat(d.Value, linked, modulePath, scopes, source);
                    Declare(scopes, d.Name);
                    break;
                case StringDeclarationNode d:
                    CheckString(d.Value, linked, modulePath, scopes, source);
                    Declare(scopes, d.Name);
                    break;
                case GameObjectDeclarationNode d:
                    CheckGameObject(d.Value, linked, modulePath, scopes, source);
                    Declare(scopes, d.Name);
                    break;
                case ComponentDeclarationNode d:
                    CheckComponent(d.Value, linked, modulePath, scopes, source);
                    Declare(scopes, d.Name);
                    break;
                case ListDeclarationNode d:
                    Declare(scopes, d.Name);
                    break;
                case ListAddNode add:
                    EnsureVariable(linked, modulePath, add.ListName, -1, scopes, source);
                    break;
                case ListRemoveAtNode rem:
                    EnsureVariable(linked, modulePath, rem.ListName, -1, scopes, source);
                    CheckInt(rem.Index, linked, modulePath, scopes, source);
                    break;
                case ListClearNode clr:
                    EnsureVariable(linked, modulePath, clr.ListName, -1, scopes, source);
                    break;
                case ListSetItemNode setItem:
                    EnsureVariable(linked, modulePath, setItem.ListName, -1, scopes, source);
                    CheckInt(setItem.Index, linked, modulePath, scopes, source);
                    break;
                case IntAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckInt(a.Value, linked, modulePath, scopes, source);
                    break;
                case BoolAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckBool(a.Value, linked, modulePath, scopes, source);
                    break;
                case FloatAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckFloat(a.Value, linked, modulePath, scopes, source);
                    break;
                case StringAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckString(a.Value, linked, modulePath, scopes, source);
                    break;
                case ComponentAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckComponent(a.Value, linked, modulePath, scopes, source);
                    break;
                case FunctionCallNode call:
                    EnsureFunction(linked, modulePath, call.Name, call.SourceIndex, source);
                    for (int i = 0; i < call.Arguments.Count; i++)
                        CheckArgument(call.Arguments[i], linked, modulePath, scopes, source);
                    break;
                case SpawnNode spawn:
                    CheckBody(linked, modulePath, spawn.Body, scopes, source, 0);
                    break;
                case WhileNode whileNode:
                    CheckBool(whileNode.Condition, linked, modulePath, scopes, source);
                    PushScope(scopes);
                    CheckBody(linked, modulePath, whileNode.Body, scopes, source, loopDepth + 1);
                    PopScope(scopes);
                    break;
                case IfNode ifNode:
                    CheckBool(ifNode.Condition, linked, modulePath, scopes, source);
                    CheckBody(linked, modulePath, ifNode.ThenBody, scopes, source, loopDepth);
                    for (int i = 0; i < ifNode.ElseIfClauses.Count; i++)
                    {
                        CheckBool(ifNode.ElseIfClauses[i].Condition, linked, modulePath, scopes, source);
                        CheckBody(linked, modulePath, ifNode.ElseIfClauses[i].Body, scopes, source, loopDepth);
                    }
                    CheckBody(linked, modulePath, ifNode.ElseBody, scopes, source, loopDepth);
                    break;
                case ForNode forNode:
                    PushScope(scopes);
                    if (forNode.Init != null)
                        CheckNode(linked, modulePath, forNode.Init, scopes, source, loopDepth);
                    CheckBool(forNode.Condition, linked, modulePath, scopes, source);
                    if (forNode.Increment != null)
                        CheckNode(linked, modulePath, forNode.Increment, scopes, source, loopDepth);
                    CheckBody(linked, modulePath, forNode.Body, scopes, source, loopDepth + 1);
                    PopScope(scopes);
                    break;
                case MoveNode move:
                    CheckGameObjectArray(move.Targets, linked, modulePath, scopes, source);
                    CheckFloat(move.DeltaX, linked, modulePath, scopes, source);
                    CheckFloat(move.DeltaY, linked, modulePath, scopes, source);
                    CheckFloat(move.Duration, linked, modulePath, scopes, source);
                    CheckString(move.Ease, linked, modulePath, scopes, source);
                    break;
                case WaitNode wait:
                    CheckFloat(wait.Duration, linked, modulePath, scopes, source);
                    break;
                case GotoNode gotoNode:
                    CheckGameObjectArray(gotoNode.Targets, linked, modulePath, scopes, source);
                    CheckFloat(gotoNode.X, linked, modulePath, scopes, source);
                    CheckFloat(gotoNode.Y, linked, modulePath, scopes, source);
                    CheckFloat(gotoNode.Duration, linked, modulePath, scopes, source);
                    CheckString(gotoNode.Ease, linked, modulePath, scopes, source);
                    break;
                case RotateNode rotate:
                    CheckGameObjectArray(rotate.Targets, linked, modulePath, scopes, source);
                    CheckInt(rotate.PivotGroup, linked, modulePath, scopes, source);
                    CheckGameObjectArray(rotate.PivotObjects, linked, modulePath, scopes, source);
                    CheckFloat(rotate.Degrees, linked, modulePath, scopes, source);
                    CheckFloat(rotate.Duration, linked, modulePath, scopes, source);
                    CheckString(rotate.Ease, linked, modulePath, scopes, source);
                    break;
                case RotateToNode rotateTo:
                    CheckGameObjectArray(rotateTo.Targets, linked, modulePath, scopes, source);
                    CheckGameObjectArray(rotateTo.PivotObjects, linked, modulePath, scopes, source);
                    CheckFloat(rotateTo.TargetX, linked, modulePath, scopes, source);
                    CheckFloat(rotateTo.TargetY, linked, modulePath, scopes, source);
                    CheckFloat(rotateTo.OffsetDegrees, linked, modulePath, scopes, source);
                    CheckFloat(rotateTo.Duration, linked, modulePath, scopes, source);
                    CheckString(rotateTo.Ease, linked, modulePath, scopes, source);
                    break;
                case ScaleToNode scaleTo:
                    CheckGameObjectArray(scaleTo.Targets, linked, modulePath, scopes, source);
                    CheckFloat(scaleTo.ScaleX, linked, modulePath, scopes, source);
                    CheckFloat(scaleTo.ScaleY, linked, modulePath, scopes, source);
                    CheckFloat(scaleTo.Duration, linked, modulePath, scopes, source);
                    CheckString(scaleTo.Ease, linked, modulePath, scopes, source);
                    break;
                case AnimateNode animate:
                    CheckGameObjectArray(animate.TargetGroup, linked, modulePath, scopes, source);
                    CheckGameObjectArray(animate.KeyGroup, linked, modulePath, scopes, source);
                    break;
                case VisibilityNode visibility:
                    CheckGameObjectArray(visibility.Targets, linked, modulePath, scopes, source);
                    CheckBool(visibility.Visible, linked, modulePath, scopes, source);
                    break;
                case TransparencyNode transparency:
                    CheckGameObjectArray(transparency.Targets, linked, modulePath, scopes, source);
                    CheckFloat(transparency.Value, linked, modulePath, scopes, source);
                    CheckFloat(transparency.Duration, linked, modulePath, scopes, source);
                    CheckString(transparency.Ease, linked, modulePath, scopes, source);
                    break;
                case HitboxNode hitbox:
                    CheckGameObjectArray(hitbox.Targets, linked, modulePath, scopes, source);
                    CheckBool(hitbox.Enabled, linked, modulePath, scopes, source);
                    break;
                case PrintNode printNode:
                    for (int a = 0; a < printNode.Arguments.Count; a++)
                        CheckArgument(printNode.Arguments[a], linked, modulePath, scopes, source);
                    break;
                case PlaySoundNode playSound:
                    CheckFloat(playSound.X, linked, modulePath, scopes, source);
                    CheckFloat(playSound.Y, linked, modulePath, scopes, source);
                    CheckString(playSound.SoundName, linked, modulePath, scopes, source);
                    CheckFloat(playSound.Volume, linked, modulePath, scopes, source);
                    break;
                case SpawnPointNode spawnPoint:
                    CheckGameObject(spawnPoint.Player, linked, modulePath, scopes, source);
                    CheckFloat(spawnPoint.X, linked, modulePath, scopes, source);
                    CheckFloat(spawnPoint.Y, linked, modulePath, scopes, source);
                    break;
                case SpawnExecutorNode spawnExecutor:
                    CheckGameObjectArray(spawnExecutor.Targets, linked, modulePath, scopes, source);
                    break;
                case AddComponentNode addComponent:
                    CheckGameObjectArray(addComponent.Targets, linked, modulePath, scopes, source);
                    CheckString(addComponent.ComponentName, linked, modulePath, scopes, source);
                    break;
                case RemoveComponentNode removeComponent:
                    CheckGameObjectArray(removeComponent.Targets, linked, modulePath, scopes, source);
                    CheckString(removeComponent.ComponentName, linked, modulePath, scopes, source);
                    break;
                case SetTextureNode setTexture:
                    CheckGameObjectArray(setTexture.Targets, linked, modulePath, scopes, source);
                    CheckString(setTexture.TextureId, linked, modulePath, scopes, source);
                    break;
                case SetColorNode setColor:
                    CheckGameObjectArray(setColor.Targets, linked, modulePath, scopes, source);
                    CheckString(setColor.HexColor, linked, modulePath, scopes, source);
                    break;
                case DestroyNode destroy:
                    CheckGameObjectArray(destroy.Targets, linked, modulePath, scopes, source);
                    break;

                case BackgroundNode bgNode:
                    CheckInt(bgNode.LayerIndex, linked, modulePath, scopes, source);
                    CheckString(bgNode.HexColor, linked, modulePath, scopes, source);
                    CheckFloat(bgNode.Value, linked, modulePath, scopes, source);
                    break;
                case CameraNode camNode:
                    CheckFloat(camNode.Intensity, linked, modulePath, scopes, source);
                    CheckFloat(camNode.Duration, linked, modulePath, scopes, source);
                    CheckFloat(camNode.ExtraDuration, linked, modulePath, scopes, source);
                    CheckString(camNode.Ease, linked, modulePath, scopes, source);
                    CheckString(camNode.HexColor, linked, modulePath, scopes, source);
                    CheckGameObject(camNode.Target, linked, modulePath, scopes, source);
                    break;
                case HitboxConfigNode hitboxConfig:
                    CheckGameObjectArray(hitboxConfig.Targets, linked, modulePath, scopes, source);
                    CheckFloat(hitboxConfig.X, linked, modulePath, scopes, source);
                    CheckFloat(hitboxConfig.Y, linked, modulePath, scopes, source);
                    CheckBool(hitboxConfig.BoolValue, linked, modulePath, scopes, source);
                    break;
                case SpriteActionNode spriteAction:
                    CheckGameObjectArray(spriteAction.Targets, linked, modulePath, scopes, source);
                    CheckBool(spriteAction.FlipX, linked, modulePath, scopes, source);
                    CheckBool(spriteAction.FlipY, linked, modulePath, scopes, source);
                    CheckInt(spriteAction.SortingOrder, linked, modulePath, scopes, source);
                    CheckFloat(spriteAction.Alpha, linked, modulePath, scopes, source);
                    CheckString(spriteAction.HexColor, linked, modulePath, scopes, source);
                    break;
                case WorldNode worldNode:
                    CheckFloat(worldNode.GravityX, linked, modulePath, scopes, source);
                    CheckFloat(worldNode.GravityY, linked, modulePath, scopes, source);
                    CheckInt(worldNode.TileX, linked, modulePath, scopes, source);
                    CheckInt(worldNode.TileY, linked, modulePath, scopes, source);
                    CheckInt(worldNode.TileId, linked, modulePath, scopes, source);
                    break;
                case UINode uiNode:
                    CheckString(uiNode.Text, linked, modulePath, scopes, source);
                    CheckFloat(uiNode.Duration, linked, modulePath, scopes, source);
                    CheckString(uiNode.HexColor, linked, modulePath, scopes, source);
                    break;

                case SendToServerNode sendToServer:
                    CheckString(sendToServer.EventName, linked, modulePath, scopes, source);
                    for (int a = 0; a < sendToServer.Arguments.Count; a++)
                        CheckArgument(sendToServer.Arguments[a], linked, modulePath, scopes, source);
                    break;
                case SendToClientNode sendToClient:
                    CheckGameObject(sendToClient.TargetPlayer, linked, modulePath, scopes, source);
                    CheckString(sendToClient.EventName, linked, modulePath, scopes, source);
                    for (int a = 0; a < sendToClient.Arguments.Count; a++)
                        CheckArgument(sendToClient.Arguments[a], linked, modulePath, scopes, source);
                    break;
                case SendToAllClientsNode sendToAll:
                    CheckString(sendToAll.EventName, linked, modulePath, scopes, source);
                    for (int a = 0; a < sendToAll.Arguments.Count; a++)
                        CheckArgument(sendToAll.Arguments[a], linked, modulePath, scopes, source);
                    break;

                case ComponentPropertyAssignmentNode componentAssignment:
                    if (!string.IsNullOrEmpty(componentAssignment.ObjectName))
                        EnsureVariable(linked, modulePath, componentAssignment.ObjectName, componentAssignment.SourceIndex, scopes, source);
                    CheckGameObjectArray(componentAssignment.Targets, linked, modulePath, scopes, source);
                    CheckFloat(componentAssignment.FloatValue, linked, modulePath, scopes, source);
                    CheckInt(componentAssignment.IntValue, linked, modulePath, scopes, source);
                    CheckBool(componentAssignment.BoolValue, linked, modulePath, scopes, source);
                    break;
                case ComponentMethodCallNode methodCall:
                    if (!string.IsNullOrEmpty(methodCall.ObjectName))
                        EnsureVariable(linked, modulePath, methodCall.ObjectName, methodCall.SourceIndex, scopes, source);
                    for (int i = 0; i < methodCall.Arguments.Count; i++)
                        CheckFloat(methodCall.Arguments[i], linked, modulePath, scopes, source);
                    break;
                case ComponentVariableMethodCallNode compVarMethodCall:
                    if (!string.IsNullOrEmpty(compVarMethodCall.ComponentVariableName))
                        EnsureVariable(linked, modulePath, compVarMethodCall.ComponentVariableName, 0, scopes, source);
                    for (int i = 0; i < compVarMethodCall.Arguments.Count; i++)
                        CheckFloat(compVarMethodCall.Arguments[i], linked, modulePath, scopes, source);
                    break;
            }
        }

        static void CheckArgument(
            ScriptArgument argument,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            if (argument == null)
                return;

            if (!string.IsNullOrEmpty(argument.IdentifierName))
            {
                EnsureVariable(linked, modulePath, argument.IdentifierName, argument.SourceIndex, scopes, source);
                return;
            }

            CheckInt(argument.IntValue, linked, modulePath, scopes, source);
            CheckFloat(argument.FloatValue, linked, modulePath, scopes, source);
            CheckBool(argument.BoolValue, linked, modulePath, scopes, source);
            CheckString(argument.StringValue, linked, modulePath, scopes, source);
            CheckGameObject(argument.GameObjectValue, linked, modulePath, scopes, source);
        }

        static void CheckInt(
            IntExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case IntVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case ListCountExpression countExpr:
                    EnsureVariable(linked, modulePath, countExpr.ListName, -1, scopes, source);
                    return;
                case ListItemIntExpression itemExpr:
                    EnsureVariable(linked, modulePath, itemExpr.ListName, -1, scopes, source);
                    CheckInt(itemExpr.Index, linked, modulePath, scopes, source);
                    return;
                case IntBinaryExpression binary:
                    CheckInt(binary.Left, linked, modulePath, scopes, source);
                    CheckInt(binary.Right, linked, modulePath, scopes, source);
                    return;
                case IntMathFunctionExpression intMath:
                    CheckFloat(intMath.FloatMath, linked, modulePath, scopes, source);
                    return;
            }
        }

        static void CheckFloat(
            FloatExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case FloatVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case ListItemFloatExpression itemExpr:
                    EnsureVariable(linked, modulePath, itemExpr.ListName, -1, scopes, source);
                    CheckInt(itemExpr.Index, linked, modulePath, scopes, source);
                    return;
                case FloatBinaryExpression binary:
                    CheckFloat(binary.Left, linked, modulePath, scopes, source);
                    CheckFloat(binary.Right, linked, modulePath, scopes, source);
                    return;
                case MathFunctionExpression math:
                    for (int i = 0; i < math.Arguments.Count; i++)
                        CheckFloat(math.Arguments[i], linked, modulePath, scopes, source);
                    return;
                case RandomFloatExpression random:
                    CheckFloat(random.Min, linked, modulePath, scopes, source);
                    CheckFloat(random.Max, linked, modulePath, scopes, source);
                    return;
                case PlayerMemberFloatExpression player:
                    EnsureVariable(linked, modulePath, player.PlayerName, -1, scopes, source);
                    return;
                case GameObjectMemberFloatExpression member:
                    EnsureVariable(linked, modulePath, member.ObjectName, -1, scopes, source);
                    return;
                case GameObjectArrayMemberFloatExpression arrayMember:
                    CheckGameObjectArray(arrayMember.Targets, linked, modulePath, scopes, source);
                    return;
                case GroupMemberFloatExpression groupMember:
                    CheckInt(groupMember.GroupId, linked, modulePath, scopes, source);
                    return;
            }
        }

        static void CheckBool(
            BoolExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case BoolVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case BoolBinaryExpression binary:
                    CheckBool(binary.Left, linked, modulePath, scopes, source);
                    CheckBool(binary.Right, linked, modulePath, scopes, source);
                    return;
                case BoolNotExpression unary:
                    CheckBool(unary.Operand, linked, modulePath, scopes, source);
                    return;
                case ComparisonExpression comparison:
                    CheckInt(comparison.Left, linked, modulePath, scopes, source);
                    CheckInt(comparison.Right, linked, modulePath, scopes, source);
                    return;
                case FloatComparisonExpression comparison:
                    CheckFloat(comparison.Left, linked, modulePath, scopes, source);
                    CheckFloat(comparison.Right, linked, modulePath, scopes, source);
                    return;
                case StringComparisonExpression comparison:
                    CheckString(comparison.Left, linked, modulePath, scopes, source);
                    CheckString(comparison.Right, linked, modulePath, scopes, source);
                    return;
                case GameObjectComparisonExpression comparison:
                    CheckGameObject(comparison.Left, linked, modulePath, scopes, source);
                    CheckGameObject(comparison.Right, linked, modulePath, scopes, source);
                    return;

                case HasTileExpression hasTile:
                    CheckInt(hasTile.X, linked, modulePath, scopes, source);
                    CheckInt(hasTile.Y, linked, modulePath, scopes, source);
                    return;

            }
        }

        static void CheckString(
            StringExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case StringVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case ListItemStringExpression itemExpr:
                    EnsureVariable(linked, modulePath, itemExpr.ListName, -1, scopes, source);
                    CheckInt(itemExpr.Index, linked, modulePath, scopes, source);
                    return;
                case PlayerMemberStringExpression player:
                    EnsureVariable(linked, modulePath, player.PlayerName, -1, scopes, source);
                    return;
                case StringConcatExpression concat:
                    CheckString(concat.Left, linked, modulePath, scopes, source);
                    CheckString(concat.Right, linked, modulePath, scopes, source);
                    return;
                case StringConvertExpression conv:
                    if (!string.IsNullOrEmpty(conv.VariableName))
                        EnsureVariable(linked, modulePath, conv.VariableName, -1, scopes, source);
                    if (conv.IntExpr != null)
                        CheckInt(conv.IntExpr, linked, modulePath, scopes, source);
                    if (conv.FloatExpr != null)
                        CheckFloat(conv.FloatExpr, linked, modulePath, scopes, source);
                    if (conv.BoolExpr != null)
                        CheckBool(conv.BoolExpr, linked, modulePath, scopes, source);
                    return;
            }
        }

        static void CheckGameObject(
            GameObjectExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case GameObjectVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case InstanceCallExpression instance:
                    CheckString(instance.TypeName, linked, modulePath, scopes, source);
                    CheckFloat(instance.X, linked, modulePath, scopes, source);
                    CheckFloat(instance.Y, linked, modulePath, scopes, source);
                    return;
                case FindNearestPlayerExpression nearest:
                    CheckFloat(nearest.X, linked, modulePath, scopes, source);
                    CheckFloat(nearest.Y, linked, modulePath, scopes, source);
                    return;

                case LocalPlayerExpression:
                    return;
            }
        }

        static void CheckComponent(
            ComponentExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case ComponentVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case GetComponentExpression getComp:
                    CheckGameObject(getComp.Target, linked, modulePath, scopes, source);
                    return;
            }
        }

        static void CheckGameObjectArray(
            GameObjectArrayExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source)
        {
            switch (expression)
            {
                case null:
                    return;
                case GroupGameObjectArrayExpression group:
                    CheckInt(group.GroupId, linked, modulePath, scopes, source);
                    return;
                case MultiGroupGameObjectArrayExpression:
                    return;
                case AllPlayersGameObjectArrayExpression:
                    return;
                case GameObjectArrayLiteralExpression literal:
                    for (int i = 0; i < literal.Items.Count; i++)
                        CheckGameObject(literal.Items[i], linked, modulePath, scopes, source);
                    return;
            }
        }

        static LevelScriptParseException SemanticError(string source, int sourceIndex, string message)

        {

            if (sourceIndex < 0)

                sourceIndex = 0;

            IndexToLineColumn(source, sourceIndex, out int line, out int column);

            return new LevelScriptParseException(line, column, sourceIndex, message);

        }



        static void EnsureVariable(
            LevelScriptLinkedProgram linked,
            string modulePath,
            string name,
            int sourceIndex,
            List<HashSet<string>> scopes,
            string source)
        {
            if (string.IsNullOrEmpty(name) || IsKnown(scopes, name))
                return;

            throw UnknownSymbol(linked, modulePath, name, sourceIndex, source, LevelScriptSymbolKind.Variable);
        }

        static void EnsureFunction(
            LevelScriptLinkedProgram linked,
            string modulePath,
            string name,
            int sourceIndex,
            string source)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (linked.Functions.TryGetValue(name, out FunctionDefinitionNode definition))
            {
                string defPath = definition.SourcePath ?? string.Empty;
                bool sameModule = string.Equals(defPath, modulePath, StringComparison.OrdinalIgnoreCase);
                bool isPublic = definition.IsPublic ||
                                (linked.Symbols.TryGetValue(name, out LevelScriptSymbolInfo info) && info.IsPublic);
                if (sameModule || isPublic)
                    return;
            }

            throw UnknownSymbol(linked, modulePath, name, sourceIndex, source, LevelScriptSymbolKind.Function);
        }

        static LevelScriptParseException UnknownSymbol(
            LevelScriptLinkedProgram linked,
            string modulePath,
            string name,
            int sourceIndex,
            string source,
            LevelScriptSymbolKind kind)
        {
            string label = kind == LevelScriptSymbolKind.Function ? "function" : "variable";
            string message = $"Unknown {label} '{name}'.";
            if (LevelScriptModuleLinker.TryFindPrivateSymbol(linked, name, modulePath, out LevelScriptSymbolInfo privateSymbol))
                message += " " + LevelScriptModuleLinker.FormatPrivateAccessHint(privateSymbol);

            if (sourceIndex < 0)
                sourceIndex = FindIdentifierIndex(source, name);

            IndexToLineColumn(source, Math.Max(0, sourceIndex), out int line, out int column);
            return new LevelScriptParseException(line, column, sourceIndex, message);
        }

        static int FindIdentifierIndex(string source, string name)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(name))
                return 0;

            for (int i = 0; i <= source.Length - name.Length; i++)
            {
                if (!string.Equals(source.Substring(i, name.Length), name, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool leftOk = i == 0 || !IsIdentChar(source[i - 1]);
                bool rightOk = i + name.Length >= source.Length || !IsIdentChar(source[i + name.Length]);
                if (leftOk && rightOk)
                    return i;
            }

            return 0;
        }

        static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

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

        static bool IsKnown(List<HashSet<string>> scopes, string name)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].Contains(name))
                    return true;
            }

            return false;
        }

        static void Declare(List<HashSet<string>> scopes, string name)
        {
            if (!string.IsNullOrEmpty(name) && scopes.Count > 0)
                scopes[scopes.Count - 1].Add(name);
        }

        static void PushScope(List<HashSet<string>> scopes) =>
            scopes.Add(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        static void PopScope(List<HashSet<string>> scopes)
        {
            if (scopes.Count > 1)
                scopes.RemoveAt(scopes.Count - 1);
        }

        static bool TryGetDeclarationName(LevelScriptNode node, out string name)
        {
            switch (node)
            {
                case IntDeclarationNode d:
                    name = d.Name;
                    return true;
                case BoolDeclarationNode d:
                    name = d.Name;
                    return true;
                case FloatDeclarationNode d:
                    name = d.Name;
                    return true;
                case StringDeclarationNode d:
                    name = d.Name;
                    return true;
                case GameObjectDeclarationNode d:
                    name = d.Name;
                    return true;
                case ComponentDeclarationNode d:
                    name = d.Name;
                    return true;
                case ListDeclarationNode d:
                    name = d.Name;
                    return true;
                default:
                    name = null;
                    return false;
            }
        }
    }
}
