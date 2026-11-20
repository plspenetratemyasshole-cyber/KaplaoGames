using System;
using System.Collections.Generic;
using KaplaoGames.Shared;

namespace KaplaoGames.LevelScript
{
    public enum LevelScriptSymbolKind : byte
    {
        Variable = 0,
        Function = 1
    }

    public sealed class LevelScriptSymbolInfo
    {
        public string Name;
        public string ModulePath;
        public bool IsPublic;
        public LevelScriptSymbolKind Kind;
    }

    /// <summary>
    /// Linked multi-file Level Script program with visibility metadata.
    /// </summary>
    public sealed class LevelScriptLinkedProgram
    {
        public LevelScriptProgram Entry;
        public readonly List<LevelScriptProgram> ModulesInInitOrder = new();
        public readonly Dictionary<string, FunctionDefinitionNode> Functions =
            new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, LevelScriptSymbolInfo> Symbols =
            new(StringComparer.OrdinalIgnoreCase);
        public readonly List<LevelScriptNode> InitDeclarations = new();
        public readonly List<EventListenerNode> EventListeners = new();
    }

    /// <summary>
    /// Resolves <c>using path/file.ks</c> graphs and collects public/private symbols.
    /// </summary>
    public static class LevelScriptModuleLinker
    {
        public static bool TryLinkEntry(out LevelScriptLinkedProgram linked, out string error)
        {
            return TryLink(WorldScriptProjectService.EntryFileName, out linked, out error);
        }

        public static bool TryLink(string entryPath, out LevelScriptLinkedProgram linked, out string error)
        {
            linked = null;
            error = null;
            entryPath = WorldScriptProjectService.NormalizeRelativePath(entryPath);
            if (string.IsNullOrEmpty(entryPath))
                entryPath = WorldScriptProjectService.EntryFileName;

            if (!WorldScriptProjectService.TryReadFile(entryPath, out string entrySource, out error))
            {
                // Empty brand-new project: allow empty entry.
                entrySource = string.Empty;
                error = null;
            }

            try
            {
                linked = Link(entryPath, entrySource);
                return true;
            }
            catch (LevelScriptParseException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static LevelScriptLinkedProgram Link(string entryPath, string entrySource)
        {
            entryPath = WorldScriptProjectService.NormalizeRelativePath(entryPath);
            if (string.IsNullOrEmpty(entryPath))
                entryPath = WorldScriptProjectService.EntryFileName;

            var linked = new LevelScriptLinkedProgram();
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var initOrder = new List<string>();
            var parsed = new Dictionary<string, LevelScriptProgram>(StringComparer.OrdinalIgnoreCase);

            LevelScriptProgram entry = LevelScriptParser.Parse(entrySource ?? string.Empty, entryPath);
            parsed[entryPath] = entry;
            linked.Entry = entry;

            LoadDependencies(entry, entryPath, visiting, visited, initOrder, parsed);

            for (int i = 0; i < initOrder.Count; i++)
            {
                if (parsed.TryGetValue(initOrder[i], out LevelScriptProgram module))
                    linked.ModulesInInitOrder.Add(module);
            }

            for (int i = 0; i < linked.ModulesInInitOrder.Count; i++)
                CollectModule(linked, linked.ModulesInInitOrder[i]);

            return linked;
        }

        static void LoadDependencies(
            LevelScriptProgram program,
            string programPath,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<string> initOrder,
            Dictionary<string, LevelScriptProgram> parsed)
        {
            if (!visiting.Add(programPath))
                throw new LevelScriptParseException($"Circular using detected at '{programPath}'.");

            for (int i = 0; i < program.Usings.Count; i++)
            {
                string rawPath = program.Usings[i].Path ?? string.Empty;
                if (!WorldScriptProjectService.TryValidateRelativePath(rawPath, out string dependencyPath, out string pathError))
                    throw new LevelScriptParseException($"Invalid using path '{rawPath}': {pathError}");

                if (!dependencyPath.EndsWith(WorldScriptProjectService.ServerExtension, StringComparison.OrdinalIgnoreCase) &&
                    !dependencyPath.EndsWith(WorldScriptProjectService.LocalExtension, StringComparison.OrdinalIgnoreCase))
                {
                    throw new LevelScriptParseException(
                        $"using '{dependencyPath}' must reference a {WorldScriptProjectService.ServerExtension} or {WorldScriptProjectService.LocalExtension} file.");
                }

                if (!parsed.TryGetValue(dependencyPath, out LevelScriptProgram dependency))
                {
                    if (!WorldScriptProjectService.TryReadFile(dependencyPath, out string source, out string readError))
                        throw new LevelScriptParseException($"Cannot load using '{dependencyPath}': {readError}");

                    dependency = LevelScriptParser.Parse(source, dependencyPath);
                    parsed[dependencyPath] = dependency;
                }

                if (!visited.Contains(dependencyPath))
                    LoadDependencies(dependency, dependencyPath, visiting, visited, initOrder, parsed);
            }

            visiting.Remove(programPath);
            if (visited.Add(programPath))
                initOrder.Add(programPath);
        }

        static void CollectModule(LevelScriptLinkedProgram linked, LevelScriptProgram module)
        {
            string modulePath = module.SourcePath ?? string.Empty;

            for (int i = 0; i < module.EventListeners.Count; i++)
            {
                linked.EventListeners.Add(module.EventListeners[i]);
            }

            for (int i = 0; i < module.Functions.Count; i++)
            {
                FunctionDefinitionNode function = module.Functions[i];
                function.SourcePath = modulePath;
                if (linked.Functions.ContainsKey(function.Name))
                    throw new LevelScriptParseException($"Duplicate function '{function.Name}'.");

                linked.Functions[function.Name] = function;
                RegisterSymbol(linked, function.Name, modulePath, function.IsPublic, LevelScriptSymbolKind.Function);
            }

            for (int i = 0; i < module.MainBody.Count; i++)
            {
                LevelScriptNode node = module.MainBody[i];
                if (!TryGetDeclaration(node, out string name, out bool isPublic))
                    continue;

                RegisterSymbol(linked, name, modulePath, isPublic, LevelScriptSymbolKind.Variable);

                // Imported modules only contribute declarations to init (not their other top-level statements).
                if (linked.Entry == null ||
                    !string.Equals(modulePath, linked.Entry.SourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    linked.InitDeclarations.Add(node);
                }
            }
        }

        static void RegisterSymbol(
            LevelScriptLinkedProgram linked,
            string name,
            string modulePath,
            bool isPublic,
            LevelScriptSymbolKind kind)
        {
            if (linked.Symbols.TryGetValue(name, out LevelScriptSymbolInfo existing))
            {
                throw new LevelScriptParseException(
                    $"Duplicate {(kind == LevelScriptSymbolKind.Function ? "function" : "variable")} '{name}' " +
                    $"(in '{existing.ModulePath}' and '{modulePath}').");
            }

            linked.Symbols[name] = new LevelScriptSymbolInfo
            {
                Name = name,
                ModulePath = modulePath,
                IsPublic = isPublic,
                Kind = kind
            };
        }

        static bool TryGetDeclaration(LevelScriptNode node, out string name, out bool isPublic)
        {
            switch (node)
            {
                case IntDeclarationNode d:
                    name = d.Name;
                    isPublic = d.IsPublic;
                    return true;
                case BoolDeclarationNode d:
                    name = d.Name;
                    isPublic = d.IsPublic;
                    return true;
                case FloatDeclarationNode d:
                    name = d.Name;
                    isPublic = d.IsPublic;
                    return true;
                case StringDeclarationNode d:
                    name = d.Name;
                    isPublic = d.IsPublic;
                    return true;
                case GameObjectDeclarationNode d:
                    name = d.Name;
                    isPublic = d.IsPublic;
                    return true;
                default:
                    name = null;
                    isPublic = false;
                    return false;
            }
        }

        public static string FormatPrivateAccessHint(LevelScriptSymbolInfo symbol)
        {
            if (symbol == null)
                return null;

            string kind = symbol.Kind == LevelScriptSymbolKind.Function ? "function" : "variable";
            string path = string.IsNullOrEmpty(symbol.ModulePath) ? "another file" : symbol.ModulePath;
            return $"Did you mean the {kind} '{symbol.Name}' from '{path}'? Make it public to use it here.";
        }

        public static bool TryFindPrivateSymbol(
            LevelScriptLinkedProgram linked,
            string name,
            string currentModulePath,
            out LevelScriptSymbolInfo symbol)
        {
            symbol = null;
            if (linked == null || string.IsNullOrEmpty(name))
                return false;

            if (!linked.Symbols.TryGetValue(name, out LevelScriptSymbolInfo found) || found.IsPublic)
                return false;

            if (string.Equals(found.ModulePath, currentModulePath, StringComparison.OrdinalIgnoreCase))
                return false;

            symbol = found;
            return true;
        }
    }
}
