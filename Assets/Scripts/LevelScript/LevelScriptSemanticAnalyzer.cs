using System;
using System.Collections.Generic;
using KaplaoGames.Shared;

namespace KaplaoGames.LevelScript
{
    /// <summary>
    /// Static checks for unknown / inaccessible variables and function calls, and execution context constraints (.ks vs .lks).
    /// </summary>
    public static class LevelScriptSemanticAnalyzer
    {
        public static void Analyze(LevelScriptLinkedProgram linked, string source)
        {
            if (linked == null)
                return;

            source ??= string.Empty;
            var modules = linked.ModulesInInitOrder != null && linked.ModulesInInitOrder.Count > 0
                ? linked.ModulesInInitOrder
                : (linked.Entry != null ? new List<LevelScriptProgram> { linked.Entry } : new List<LevelScriptProgram>());

            for (int m = 0; m < modules.Count; m++)
            {
                LevelScriptProgram module = modules[m];
                if (module == null)
                    continue;

                string modulePath = module.SourcePath ?? string.Empty;
                string moduleSource = source;
                if (!string.IsNullOrEmpty(modulePath) &&
                    (linked.Entry == null || !string.Equals(modulePath, linked.Entry.SourcePath, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!WorldScriptProjectService.TryReadFile(modulePath, out moduleSource, out _))
                        moduleSource = string.Empty;
                }

                AnalyzeModule(linked, module, modulePath, moduleSource);
            }
        }

        static void AnalyzeModule(
            LevelScriptLinkedProgram linked,
            LevelScriptProgram module,
            string modulePath,
            string moduleSource)
        {
            bool isLocal = WorldScriptProjectService.IsLocalScriptPath(modulePath);

            // Context validation: Server vs Local
            if (isLocal)
            {
                if (module.PlayerAddedHandlers.Count > 0)
                    throw SemanticError(moduleSource, -1, "'onPlayerAdded' is server-only and cannot be used in local scripts (.lks).", modulePath);
                if (module.TriggerHandlers.Count > 0)
                    throw SemanticError(moduleSource, -1, "Trigger handlers ('Group(...).OnTrigger*') are server-only and cannot be used in local scripts (.lks).", modulePath);
                if (module.SpawnPointsDeclarations.Count > 0)
                    throw SemanticError(moduleSource, -1, "'spawnPoints' is server-only and cannot be used in local scripts (.lks).", modulePath);
            }

            var globals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAccessibleGlobals(linked, module, modulePath, globals);

            var scopes = new List<HashSet<string>> { new HashSet<string>(globals, StringComparer.OrdinalIgnoreCase) };

            for (int i = 0; i < module.Functions.Count; i++)
            {
                FunctionDefinitionNode function = module.Functions[i];
                PushScope(scopes);
                for (int p = 0; p < function.Parameters.Count; p++)
                    Declare(scopes, function.Parameters[p].Name);
                CheckBody(linked, modulePath, function.Body, scopes, moduleSource, isLocal);
                PopScope(scopes);
            }

            for (int i = 0; i < module.TriggerHandlers.Count; i++)
            {
                TriggerHandlerNode handler = module.TriggerHandlers[i];
                PushScope(scopes);
                if (!string.IsNullOrEmpty(handler.GameObjectParameterName))
                    Declare(scopes, handler.GameObjectParameterName);
                CheckGameObjectArray(handler.Source, linked, modulePath, scopes, moduleSource, isLocal);
                CheckBody(linked, modulePath, handler.Body, scopes, moduleSource, isLocal);
                PopScope(scopes);
            }

            for (int i = 0; i < module.PlayerAddedHandlers.Count; i++)
            {
                PlayerAddedHandlerNode handler = module.PlayerAddedHandlers[i];
                PushScope(scopes);
                if (!string.IsNullOrEmpty(handler.PlayerParameterName))
                    Declare(scopes, handler.PlayerParameterName);
                CheckBody(linked, modulePath, handler.Body, scopes, moduleSource, isLocal);
                PopScope(scopes);
            }

            for (int i = 0; i < module.EventListeners.Count; i++)
            {
                EventListenerNode handler = module.EventListeners[i];
                if (handler.Target == ScriptEventTarget.ServerEvent && !isLocal)
                    throw SemanticError(moduleSource, -1, "'onServerEvent' is client-only and cannot be used in server scripts (.ks).", modulePath);
                if (handler.Target == ScriptEventTarget.ClientEvent && isLocal)
                    throw SemanticError(moduleSource, -1, "'onClientEvent' is server-only and cannot be used in local scripts (.lks).", modulePath);

                PushScope(scopes);
                if (!string.IsNullOrEmpty(handler.PlayerParameterName))
                    Declare(scopes, handler.PlayerParameterName);
                for (int p = 0; p < handler.ParameterNames.Count; p++)
                    Declare(scopes, handler.ParameterNames[p]);
                CheckBody(linked, modulePath, handler.Body, scopes, moduleSource, isLocal);
                PopScope(scopes);
            }

            for (int i = 0; i < module.Spawns.Count; i++)
                CheckBody(linked, modulePath, module.Spawns[i].Body, scopes, moduleSource, isLocal);

            for (int i = 0; i < module.SpawnPointsDeclarations.Count; i++)
            {
                SpawnPointsNode node = module.SpawnPointsDeclarations[i];
                CheckInt(node.From, linked, modulePath, scopes, moduleSource, isLocal);
                CheckInt(node.To, linked, modulePath, scopes, moduleSource, isLocal);
            }

            CheckBody(linked, modulePath, module.MainBody, scopes, moduleSource, isLocal);
        }

        static void CollectAccessibleGlobals(
            LevelScriptLinkedProgram linked,
            LevelScriptProgram module,
            string modulePath,
            HashSet<string> globals)
        {
            if (linked?.Symbols != null)
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
            }

            // Also include declarations from this module's main body
            if (module?.MainBody != null)
            {
                for (int i = 0; i < module.MainBody.Count; i++)
                {
                    if (TryGetDeclarationName(module.MainBody[i], out string name))
                        globals.Add(name);
                }
            }
        }

        static void CheckBody(
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<LevelScriptNode> body,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal,
            int loopDepth = 0)
        {
            if (body == null)
                return;

            for (int i = 0; i < body.Count; i++)
                CheckNode(linked, modulePath, body[i], scopes, source, isLocal, loopDepth);
        }

        static void CheckNode(
            LevelScriptLinkedProgram linked,
            string modulePath,
            LevelScriptNode node,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal,
            int loopDepth = 0)
        {
            if (node == null)
                return;

            switch (node)
            {
                case ReturnNode ret:
                    if (ret.Value != null)
                        CheckArgument(ret.Value, linked, modulePath, scopes, source, isLocal);
                    CheckInt(ret.IntValue, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(ret.FloatValue, linked, modulePath, scopes, source, isLocal);
                    CheckBool(ret.BoolValue, linked, modulePath, scopes, source, isLocal);
                    CheckString(ret.StringValue, linked, modulePath, scopes, source, isLocal);
                    break;
                case BreakNode b:
                    if (loopDepth <= 0)
                        throw SemanticError(source, b.SourceIndex, "The 'break' statement cannot be used outside of a loop.", modulePath);
                    break;
                case ContinueNode c:
                    if (loopDepth <= 0)
                        throw SemanticError(source, c.SourceIndex, "The 'continue' statement cannot be used outside of a loop.", modulePath);
                    break;
                case IntDeclarationNode d:
                    CheckInt(d.Value, linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case BoolDeclarationNode d:
                    CheckBool(d.Value, linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case FloatDeclarationNode d:
                    CheckFloat(d.Value, linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case StringDeclarationNode d:
                    CheckString(d.Value, linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case GameObjectDeclarationNode d:
                    CheckGameObject(d.Value, linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case ComponentDeclarationNode d:
                    CheckComponent(d.Value, linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case ListDeclarationNode d:
                    for (int i = 0; i < d.InitialItems.Count; i++)
                        CheckArgument(d.InitialItems[i], linked, modulePath, scopes, source, isLocal);
                    if (d.CallValue != null)
                        CheckNode(linked, modulePath, d.CallValue, scopes, source, isLocal, loopDepth);
                    Declare(scopes, d.Name);
                    break;
                case ListAddNode add:
                    EnsureVariable(linked, modulePath, add.ListName, -1, scopes, source);
                    CheckArgument(add.Item, linked, modulePath, scopes, source, isLocal);
                    break;
                case ListRemoveAtNode rem:
                    EnsureVariable(linked, modulePath, rem.ListName, -1, scopes, source);
                    CheckInt(rem.Index, linked, modulePath, scopes, source, isLocal);
                    break;
                case ListClearNode clr:
                    EnsureVariable(linked, modulePath, clr.ListName, -1, scopes, source);
                    break;
                case ListSetItemNode setItem:
                    EnsureVariable(linked, modulePath, setItem.ListName, -1, scopes, source);
                    CheckInt(setItem.Index, linked, modulePath, scopes, source, isLocal);
                    CheckArgument(setItem.Value, linked, modulePath, scopes, source, isLocal);
                    break;
                case IntAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckInt(a.Value, linked, modulePath, scopes, source, isLocal);
                    if (a.CallValue != null)
                        CheckNode(linked, modulePath, a.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case BoolAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckBool(a.Value, linked, modulePath, scopes, source, isLocal);
                    if (a.CallValue != null)
                        CheckNode(linked, modulePath, a.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case FloatAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckFloat(a.Value, linked, modulePath, scopes, source, isLocal);
                    if (a.CallValue != null)
                        CheckNode(linked, modulePath, a.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case StringAssignmentNode a:
                    EnsureVariable(linked, modulePath, a.Name, a.SourceIndex, scopes, source);
                    CheckString(a.Value, linked, modulePath, scopes, source, isLocal);
                    if (a.CallValue != null)
                        CheckNode(linked, modulePath, a.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case GameObjectAssignmentNode ga:
                    EnsureVariable(linked, modulePath, ga.Name, ga.SourceIndex, scopes, source);
                    CheckGameObject(ga.Value, linked, modulePath, scopes, source, isLocal);
                    if (ga.CallValue != null)
                        CheckNode(linked, modulePath, ga.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case ListAssignmentNode la:
                    EnsureVariable(linked, modulePath, la.Name, la.SourceIndex, scopes, source);
                    if (la.CallValue != null)
                        CheckNode(linked, modulePath, la.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case ComponentAssignmentNode ca:
                    EnsureVariable(linked, modulePath, ca.Name, ca.SourceIndex, scopes, source);
                    CheckComponent(ca.Value, linked, modulePath, scopes, source, isLocal);
                    if (ca.CallValue != null)
                        CheckNode(linked, modulePath, ca.CallValue, scopes, source, isLocal, loopDepth);
                    break;
                case FunctionCallNode call:
                    EnsureFunction(linked, modulePath, call, source);
                    for (int i = 0; i < call.Arguments.Count; i++)
                        CheckArgument(call.Arguments[i], linked, modulePath, scopes, source, isLocal);
                    break;
                case SpawnNode spawn:
                    CheckBody(linked, modulePath, spawn.Body, scopes, source, isLocal, 0);
                    break;
                case WhileNode whileNode:
                    CheckBool(whileNode.Condition, linked, modulePath, scopes, source, isLocal);
                    PushScope(scopes);
                    CheckBody(linked, modulePath, whileNode.Body, scopes, source, isLocal, loopDepth + 1);
                    PopScope(scopes);
                    break;
                case IfNode ifNode:
                    CheckBool(ifNode.Condition, linked, modulePath, scopes, source, isLocal);
                    PushScope(scopes);
                    CheckBody(linked, modulePath, ifNode.ThenBody, scopes, source, isLocal, loopDepth);
                    PopScope(scopes);
                    for (int i = 0; i < ifNode.ElseIfClauses.Count; i++)
                    {
                        CheckBool(ifNode.ElseIfClauses[i].Condition, linked, modulePath, scopes, source, isLocal);
                        PushScope(scopes);
                        CheckBody(linked, modulePath, ifNode.ElseIfClauses[i].Body, scopes, source, isLocal, loopDepth);
                        PopScope(scopes);
                    }
                    if (ifNode.ElseBody != null && ifNode.ElseBody.Count > 0)
                    {
                        PushScope(scopes);
                        CheckBody(linked, modulePath, ifNode.ElseBody, scopes, source, isLocal, loopDepth);
                        PopScope(scopes);
                    }
                    break;
                case ForNode forNode:
                    PushScope(scopes);
                    if (forNode.Init != null)
                        CheckNode(linked, modulePath, forNode.Init, scopes, source, isLocal, loopDepth);
                    CheckBool(forNode.Condition, linked, modulePath, scopes, source, isLocal);
                    if (forNode.Increment != null)
                        CheckNode(linked, modulePath, forNode.Increment, scopes, source, isLocal, loopDepth);
                    CheckBody(linked, modulePath, forNode.Body, scopes, source, isLocal, loopDepth + 1);
                    PopScope(scopes);
                    break;
                case MoveNode move:
                    CheckGameObjectArray(move.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(move.DeltaX, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(move.DeltaY, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(move.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(move.Ease, linked, modulePath, scopes, source, isLocal);
                    break;
                case WaitNode wait:
                    CheckFloat(wait.Duration, linked, modulePath, scopes, source, isLocal);
                    break;
                case GotoNode gotoNode:
                    CheckGameObjectArray(gotoNode.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(gotoNode.X, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(gotoNode.Y, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(gotoNode.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(gotoNode.Ease, linked, modulePath, scopes, source, isLocal);
                    break;
                case RotateNode rotate:
                    CheckGameObjectArray(rotate.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckInt(rotate.PivotGroup, linked, modulePath, scopes, source, isLocal);
                    CheckGameObjectArray(rotate.PivotObjects, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(rotate.Degrees, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(rotate.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(rotate.Ease, linked, modulePath, scopes, source, isLocal);
                    break;
                case RotateToNode rotateTo:
                    CheckGameObjectArray(rotateTo.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckGameObjectArray(rotateTo.PivotObjects, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(rotateTo.TargetX, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(rotateTo.TargetY, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(rotateTo.OffsetDegrees, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(rotateTo.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(rotateTo.Ease, linked, modulePath, scopes, source, isLocal);
                    break;
                case ScaleToNode scaleTo:
                    CheckGameObjectArray(scaleTo.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(scaleTo.ScaleX, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(scaleTo.ScaleY, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(scaleTo.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(scaleTo.Ease, linked, modulePath, scopes, source, isLocal);
                    break;
                case AnimateNode animate:
                    CheckGameObjectArray(animate.TargetGroup, linked, modulePath, scopes, source, isLocal);
                    CheckGameObjectArray(animate.KeyGroup, linked, modulePath, scopes, source, isLocal);
                    break;
                case VisibilityNode visibility:
                    CheckGameObjectArray(visibility.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckBool(visibility.Visible, linked, modulePath, scopes, source, isLocal);
                    break;
                case TransparencyNode transparency:
                    CheckGameObjectArray(transparency.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(transparency.Value, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(transparency.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(transparency.Ease, linked, modulePath, scopes, source, isLocal);
                    break;
                case HitboxNode hitbox:
                    CheckGameObjectArray(hitbox.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckBool(hitbox.Enabled, linked, modulePath, scopes, source, isLocal);
                    break;
                case PrintNode printNode:
                    for (int a = 0; a < printNode.Arguments.Count; a++)
                        CheckArgument(printNode.Arguments[a], linked, modulePath, scopes, source, isLocal);
                    break;
                case PlaySoundNode playSound:
                    CheckFloat(playSound.X, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(playSound.Y, linked, modulePath, scopes, source, isLocal);
                    CheckString(playSound.SoundName, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(playSound.Volume, linked, modulePath, scopes, source, isLocal);
                    break;
                case SpawnPointNode spawnPoint:
                    if (isLocal)
                        throw SemanticError(source, -1, "'spawnPoint' is server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckGameObject(spawnPoint.Player, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(spawnPoint.X, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(spawnPoint.Y, linked, modulePath, scopes, source, isLocal);
                    break;
                case SpawnExecutorNode spawnExecutor:
                    CheckGameObjectArray(spawnExecutor.Targets, linked, modulePath, scopes, source, isLocal);
                    break;
                case AddComponentNode addComponent:
                    CheckGameObjectArray(addComponent.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckString(addComponent.ComponentName, linked, modulePath, scopes, source, isLocal);
                    break;
                case RemoveComponentNode removeComponent:
                    CheckGameObjectArray(removeComponent.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckString(removeComponent.ComponentName, linked, modulePath, scopes, source, isLocal);
                    break;
                case SetTextureNode setTexture:
                    CheckGameObjectArray(setTexture.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckString(setTexture.TextureId, linked, modulePath, scopes, source, isLocal);
                    break;
                case SetColorNode setColor:
                    CheckGameObjectArray(setColor.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckString(setColor.HexColor, linked, modulePath, scopes, source, isLocal);
                    break;
                case DestroyNode destroy:
                    if (isLocal)
                        throw SemanticError(source, -1, "'destroy' is server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckGameObjectArray(destroy.Targets, linked, modulePath, scopes, source, isLocal);
                    break;

                case BackgroundNode bgNode:
                    CheckInt(bgNode.LayerIndex, linked, modulePath, scopes, source, isLocal);
                    CheckString(bgNode.HexColor, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(bgNode.Value, linked, modulePath, scopes, source, isLocal);
                    break;
                case CameraNode camNode:
                    CheckFloat(camNode.Intensity, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(camNode.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(camNode.ExtraDuration, linked, modulePath, scopes, source, isLocal);
                    CheckString(camNode.Ease, linked, modulePath, scopes, source, isLocal);
                    CheckString(camNode.HexColor, linked, modulePath, scopes, source, isLocal);
                    CheckGameObject(camNode.Target, linked, modulePath, scopes, source, isLocal);
                    break;
                case HitboxConfigNode hitboxConfig:
                    CheckGameObjectArray(hitboxConfig.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(hitboxConfig.X, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(hitboxConfig.Y, linked, modulePath, scopes, source, isLocal);
                    CheckBool(hitboxConfig.BoolValue, linked, modulePath, scopes, source, isLocal);
                    break;
                case SpriteActionNode spriteAction:
                    CheckGameObjectArray(spriteAction.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckBool(spriteAction.FlipX, linked, modulePath, scopes, source, isLocal);
                    CheckBool(spriteAction.FlipY, linked, modulePath, scopes, source, isLocal);
                    CheckInt(spriteAction.SortingOrder, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(spriteAction.Alpha, linked, modulePath, scopes, source, isLocal);
                    CheckString(spriteAction.HexColor, linked, modulePath, scopes, source, isLocal);
                    break;
                case WorldNode worldNode:
                    if (isLocal)
                        throw SemanticError(source, -1, "'World' commands are server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckFloat(worldNode.GravityX, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(worldNode.GravityY, linked, modulePath, scopes, source, isLocal);
                    CheckInt(worldNode.TileX, linked, modulePath, scopes, source, isLocal);
                    CheckInt(worldNode.TileY, linked, modulePath, scopes, source, isLocal);
                    CheckInt(worldNode.TileId, linked, modulePath, scopes, source, isLocal);
                    break;
                case UINode uiNode:
                    CheckString(uiNode.Text, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(uiNode.Duration, linked, modulePath, scopes, source, isLocal);
                    CheckString(uiNode.HexColor, linked, modulePath, scopes, source, isLocal);
                    break;

                case SendToServerNode sendToServer:
                    if (!isLocal)
                        throw SemanticError(source, -1, "'sendToServer' is client-only and cannot be used in server scripts (.ks).", modulePath);
                    CheckString(sendToServer.EventName, linked, modulePath, scopes, source, isLocal);
                    for (int a = 0; a < sendToServer.Arguments.Count; a++)
                        CheckArgument(sendToServer.Arguments[a], linked, modulePath, scopes, source, isLocal);
                    break;
                case SendToClientNode sendToClient:
                    if (isLocal)
                        throw SemanticError(source, -1, "'sendToClient' is server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckGameObject(sendToClient.TargetPlayer, linked, modulePath, scopes, source, isLocal);
                    CheckString(sendToClient.EventName, linked, modulePath, scopes, source, isLocal);
                    for (int a = 0; a < sendToClient.Arguments.Count; a++)
                        CheckArgument(sendToClient.Arguments[a], linked, modulePath, scopes, source, isLocal);
                    break;
                case SendToAllClientsNode sendToAll:
                    if (isLocal)
                        throw SemanticError(source, -1, "'sendToAllClients' is server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckString(sendToAll.EventName, linked, modulePath, scopes, source, isLocal);
                    for (int a = 0; a < sendToAll.Arguments.Count; a++)
                        CheckArgument(sendToAll.Arguments[a], linked, modulePath, scopes, source, isLocal);
                    break;

                case ComponentPropertyAssignmentNode componentAssignment:
                    if (!string.IsNullOrEmpty(componentAssignment.ObjectName))
                        EnsureVariable(linked, modulePath, componentAssignment.ObjectName, componentAssignment.SourceIndex, scopes, source);
                    if (!string.IsNullOrEmpty(componentAssignment.ComponentVariableName))
                        EnsureVariable(linked, modulePath, componentAssignment.ComponentVariableName, componentAssignment.SourceIndex, scopes, source);
                    CheckGameObjectArray(componentAssignment.Targets, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(componentAssignment.FloatValue, linked, modulePath, scopes, source, isLocal);
                    CheckInt(componentAssignment.IntValue, linked, modulePath, scopes, source, isLocal);
                    CheckBool(componentAssignment.BoolValue, linked, modulePath, scopes, source, isLocal);
                    break;
                case ComponentMethodCallNode methodCall:
                    if (!string.IsNullOrEmpty(methodCall.ObjectName))
                        EnsureVariable(linked, modulePath, methodCall.ObjectName, methodCall.SourceIndex, scopes, source);
                    for (int i = 0; i < methodCall.Arguments.Count; i++)
                        CheckFloat(methodCall.Arguments[i], linked, modulePath, scopes, source, isLocal);
                    break;
                case ComponentVariableMethodCallNode compVarMethodCall:
                    if (!string.IsNullOrEmpty(compVarMethodCall.ComponentVariableName))
                        EnsureVariable(linked, modulePath, compVarMethodCall.ComponentVariableName, 0, scopes, source);
                    for (int i = 0; i < compVarMethodCall.Arguments.Count; i++)
                        CheckFloat(compVarMethodCall.Arguments[i], linked, modulePath, scopes, source, isLocal);
                    break;
            }
        }

        static void CheckArgument(
            ScriptArgument argument,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
        {
            if (argument == null)
                return;

            if (!string.IsNullOrEmpty(argument.IdentifierName))
            {
                EnsureVariable(linked, modulePath, argument.IdentifierName, argument.SourceIndex, scopes, source);
                if (argument.IndexExpr != null)
                    CheckInt(argument.IndexExpr, linked, modulePath, scopes, source, isLocal);
                return;
            }

            CheckInt(argument.IntValue, linked, modulePath, scopes, source, isLocal);
            CheckFloat(argument.FloatValue, linked, modulePath, scopes, source, isLocal);
            CheckBool(argument.BoolValue, linked, modulePath, scopes, source, isLocal);
            CheckString(argument.StringValue, linked, modulePath, scopes, source, isLocal);
            CheckGameObject(argument.GameObjectValue, linked, modulePath, scopes, source, isLocal);
            CheckComponent(argument.ComponentValue, linked, modulePath, scopes, source, isLocal);
        }

        static void CheckInt(
            IntExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
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
                    CheckInt(itemExpr.Index, linked, modulePath, scopes, source, isLocal);
                    return;
                case IntBinaryExpression binary:
                    CheckInt(binary.Left, linked, modulePath, scopes, source, isLocal);
                    CheckInt(binary.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case IntMathFunctionExpression intMath:
                    CheckFloat(intMath.FloatMath, linked, modulePath, scopes, source, isLocal);
                    return;
            }
        }

        static void CheckFloat(
            FloatExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
        {
            switch (expression)
            {
                case null:
                    return;
                case FloatVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case FloatFromIntExpression fromInt:
                    CheckInt(fromInt.Value, linked, modulePath, scopes, source, isLocal);
                    return;
                case ListItemFloatExpression itemExpr:
                    EnsureVariable(linked, modulePath, itemExpr.ListName, -1, scopes, source);
                    CheckInt(itemExpr.Index, linked, modulePath, scopes, source, isLocal);
                    return;
                case FloatBinaryExpression binary:
                    CheckFloat(binary.Left, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(binary.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case MathFunctionExpression math:
                    for (int i = 0; i < math.Arguments.Count; i++)
                        CheckFloat(math.Arguments[i], linked, modulePath, scopes, source, isLocal);
                    return;
                case RandomFloatExpression random:
                    CheckFloat(random.Min, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(random.Max, linked, modulePath, scopes, source, isLocal);
                    return;
                case PlayerMemberFloatExpression player:
                    if (string.Equals(player.PlayerName, "localPlayer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isLocal)
                            throw SemanticError(source, -1, "'localPlayer' is client-only and cannot be used in server scripts (.ks).", modulePath);
                        return;
                    }
                    EnsureVariable(linked, modulePath, player.PlayerName, -1, scopes, source);
                    return;
                case GameObjectMemberFloatExpression member:
                    EnsureVariable(linked, modulePath, member.ObjectName, -1, scopes, source);
                    return;
                case GameObjectArrayMemberFloatExpression arrayMember:
                    CheckGameObjectArray(arrayMember.Targets, linked, modulePath, scopes, source, isLocal);
                    return;
                case GroupMemberFloatExpression groupMember:
                    CheckInt(groupMember.GroupId, linked, modulePath, scopes, source, isLocal);
                    return;
            }
        }

        static void CheckBool(
            BoolExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
        {
            switch (expression)
            {
                case null:
                    return;
                case BoolVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case BoolBinaryExpression binary:
                    CheckBool(binary.Left, linked, modulePath, scopes, source, isLocal);
                    CheckBool(binary.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case BoolNotExpression unary:
                    CheckBool(unary.Operand, linked, modulePath, scopes, source, isLocal);
                    return;
                case ComparisonExpression comparison:
                    CheckInt(comparison.Left, linked, modulePath, scopes, source, isLocal);
                    CheckInt(comparison.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case FloatComparisonExpression comparison:
                    CheckFloat(comparison.Left, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(comparison.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case StringComparisonExpression comparison:
                    CheckString(comparison.Left, linked, modulePath, scopes, source, isLocal);
                    CheckString(comparison.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case GameObjectComparisonExpression comparison:
                    CheckGameObject(comparison.Left, linked, modulePath, scopes, source, isLocal);
                    CheckGameObject(comparison.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case BoolComparisonExpression boolComp:
                    CheckBool(boolComp.Left, linked, modulePath, scopes, source, isLocal);
                    CheckBool(boolComp.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case DynamicEqualityExpression dynEq:
                    CheckArgument(dynEq.Left, linked, modulePath, scopes, source, isLocal);
                    CheckArgument(dynEq.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case ListItemBoolExpression itemBool:
                    EnsureVariable(linked, modulePath, itemBool.ListName, -1, scopes, source);
                    CheckInt(itemBool.Index, linked, modulePath, scopes, source, isLocal);
                    return;
                case HasTileExpression hasTile:
                    if (isLocal)
                        throw SemanticError(source, -1, "'hasTile' is server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckInt(hasTile.X, linked, modulePath, scopes, source, isLocal);
                    CheckInt(hasTile.Y, linked, modulePath, scopes, source, isLocal);
                    return;
            }
        }

        static void CheckString(
            StringExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
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
                    CheckInt(itemExpr.Index, linked, modulePath, scopes, source, isLocal);
                    return;
                case PlayerMemberStringExpression player:
                    if (string.Equals(player.PlayerName, "localPlayer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isLocal)
                            throw SemanticError(source, -1, "'localPlayer' is client-only and cannot be used in server scripts (.ks).", modulePath);
                        return;
                    }
                    EnsureVariable(linked, modulePath, player.PlayerName, -1, scopes, source);
                    return;
                case StringConcatExpression concat:
                    CheckString(concat.Left, linked, modulePath, scopes, source, isLocal);
                    CheckString(concat.Right, linked, modulePath, scopes, source, isLocal);
                    return;
                case StringConvertExpression conv:
                    if (!string.IsNullOrEmpty(conv.VariableName))
                        EnsureVariable(linked, modulePath, conv.VariableName, -1, scopes, source);
                    if (conv.IntExpr != null)
                        CheckInt(conv.IntExpr, linked, modulePath, scopes, source, isLocal);
                    if (conv.FloatExpr != null)
                        CheckFloat(conv.FloatExpr, linked, modulePath, scopes, source, isLocal);
                    if (conv.BoolExpr != null)
                        CheckBool(conv.BoolExpr, linked, modulePath, scopes, source, isLocal);
                    return;
            }
        }

        static void CheckGameObject(
            GameObjectExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
        {
            switch (expression)
            {
                case null:
                    return;
                case GameObjectVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case ListItemGameObjectExpression itemGo:
                    EnsureVariable(linked, modulePath, itemGo.ListName, -1, scopes, source);
                    CheckInt(itemGo.Index, linked, modulePath, scopes, source, isLocal);
                    return;
                case InstanceCallExpression instance:
                    if (isLocal)
                        throw SemanticError(source, -1, "'instance' is server-only and cannot be used in local scripts (.lks).", modulePath);
                    CheckString(instance.TypeName, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(instance.X, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(instance.Y, linked, modulePath, scopes, source, isLocal);
                    return;
                case FindNearestPlayerExpression nearest:
                    CheckFloat(nearest.X, linked, modulePath, scopes, source, isLocal);
                    CheckFloat(nearest.Y, linked, modulePath, scopes, source, isLocal);
                    return;
                case LocalPlayerExpression:
                    if (!isLocal)
                        throw SemanticError(source, -1, "'localPlayer' is client-only and cannot be used in server scripts (.ks).", modulePath);
                    return;
            }
        }

        static void CheckComponent(
            ComponentExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
        {
            switch (expression)
            {
                case null:
                    return;
                case ComponentVariableExpression variable:
                    EnsureVariable(linked, modulePath, variable.Name, variable.SourceIndex, scopes, source);
                    return;
                case GetComponentExpression getComp:
                    CheckGameObject(getComp.Target, linked, modulePath, scopes, source, isLocal);
                    return;
            }
        }

        static void CheckGameObjectArray(
            GameObjectArrayExpression expression,
            LevelScriptLinkedProgram linked,
            string modulePath,
            List<HashSet<string>> scopes,
            string source,
            bool isLocal)
        {
            switch (expression)
            {
                case null:
                    return;
                case GroupGameObjectArrayExpression group:
                    CheckInt(group.GroupId, linked, modulePath, scopes, source, isLocal);
                    return;
                case MultiGroupGameObjectArrayExpression:
                    return;
                case AllPlayersGameObjectArrayExpression:
                    return;
                case GameObjectArrayLiteralExpression literal:
                    for (int i = 0; i < literal.Items.Count; i++)
                        CheckGameObject(literal.Items[i], linked, modulePath, scopes, source, isLocal);
                    return;
            }
        }

        static LevelScriptParseException SemanticError(string source, int sourceIndex, string message, string filePath = null)
        {
            if (sourceIndex < 0)
                sourceIndex = 0;

            IndexToLineColumn(source, sourceIndex, out int line, out int column);
            return new LevelScriptParseException(line, column, sourceIndex, message, filePath);
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
            FunctionCallNode call,
            string source)
        {
            string name = call.Name;
            if (string.IsNullOrEmpty(name))
                return;

            if (linked.Functions.TryGetValue(name, out FunctionDefinitionNode definition))
            {
                string defPath = definition.SourcePath ?? string.Empty;
                bool sameModule = string.Equals(defPath, modulePath, StringComparison.OrdinalIgnoreCase);
                bool isPublic = definition.IsPublic ||
                                (linked.Symbols.TryGetValue(name, out LevelScriptSymbolInfo info) && info.IsPublic);
                if (sameModule || isPublic)
                {
                    int expectedCount = definition.Parameters.Count;
                    int actualCount = call.Arguments.Count;
                    if (actualCount != expectedCount)
                    {
                        throw SemanticError(source, call.SourceIndex,
                            $"Function '{name}' expects {expectedCount} argument{(expectedCount == 1 ? "" : "s")}, but received {actualCount}.", modulePath);
                    }
                    return;
                }
            }

            throw UnknownSymbol(linked, modulePath, name, call.SourceIndex, source, LevelScriptSymbolKind.Function);
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
            return new LevelScriptParseException(line, column, sourceIndex, message, modulePath);
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
