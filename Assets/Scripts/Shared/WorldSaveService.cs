using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using KaplaoGames.Client;
using KaplaoGames.Server;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class WorldSaveService
    {
        public const string LevelFileName = "level.json";
        const int MaxWorldNameLength = 48;

        public static IReadOnlyList<string> ListWorlds()
        {
            string root = LevelIoPaths.LevelsDirectory;
            if (!Directory.Exists(root))
                return Array.Empty<string>();

            string[] directories = Directory.GetDirectories(root);
            var worlds = new List<string>(directories.Length);
            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                if (!File.Exists(GetWorldLevelPath(Path.GetFileName(directory))))
                    continue;

                worlds.Add(Path.GetFileName(directory));
            }

            worlds.Sort(StringComparer.OrdinalIgnoreCase);
            return worlds;
        }

        public static string GetWorldDirectory(string worldName)
        {
            return Path.Combine(ResolveWorldRoot(worldName), SanitizeWorldName(worldName));
        }

        public static string GetUserWorldDirectory(string worldName)
        {
            return Path.Combine(LevelIoPaths.LevelsDirectory, SanitizeWorldName(worldName));
        }

        public static string GetBrowsePlayWorldDirectory(string worldName)
        {
            return Path.Combine(LevelIoPaths.BrowsePlayDirectory, SanitizeWorldName(worldName));
        }

        public static string GetWorldLevelPath(string worldName)
        {
            return Path.Combine(GetWorldDirectory(worldName), LevelFileName);
        }

        static string ResolveWorldRoot(string worldName)
        {
            if (WorldSession.IsBrowsePlaySession &&
                WorldSession.HasActiveWorld &&
                string.Equals(SanitizeWorldName(worldName), WorldSession.ActiveWorldName, StringComparison.OrdinalIgnoreCase))
            {
                return LevelIoPaths.BrowsePlayDirectory;
            }

            return LevelIoPaths.LevelsDirectory;
        }

        public static bool TryValidateWorldName(string worldName, out string sanitized, out string error)
        {
            sanitized = SanitizeWorldName(worldName);
            error = null;

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                error = "World name cannot be empty.";
                return false;
            }

            if (sanitized.Length > MaxWorldNameLength)
            {
                error = $"World name must be {MaxWorldNameLength} characters or fewer.";
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < sanitized.Length; i++)
            {
                if (invalidChars.Contains(sanitized[i]))
                {
                    error = "World name contains invalid characters.";
                    return false;
                }
            }

            return true;
        }

        public static bool WorldExists(string worldName)
        {
            return Directory.Exists(GetWorldDirectory(worldName));
        }

        public static bool TryCreateWorld(string worldName, out string error)
        {
            if (!TryValidateWorldName(worldName, out string sanitized, out error))
                return false;

            string directory = GetWorldDirectory(sanitized);
            if (Directory.Exists(directory))
            {
                error = "A world with this name already exists.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);
                LevelData empty = CreateEmptyLevelData();
                File.WriteAllText(GetWorldLevelPath(sanitized), LevelFileSerializer.ToJson(empty));
                if (!WorldScriptProjectService.TryCreateDefaultProject(sanitized, string.Empty, out error))
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryDeleteWorld(string worldName, out string error)
        {
            error = null;
            string directory = GetWorldDirectory(worldName);
            if (!Directory.Exists(directory))
            {
                error = "World not found.";
                return false;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryLoadWorld(string worldName, out LevelData data, out string error)
        {
            data = null;
            error = null;

            string path = GetWorldLevelPath(worldName);
            if (!File.Exists(path))
            {
                error = "Save file not found.";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (!LevelFileSerializer.TryParse(json, out data, out error))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySaveWorld(string worldName, LevelData data, out string error)
        {
            error = null;
            if (data == null)
            {
                error = "Nothing to save.";
                return false;
            }

            if (!TryValidateWorldName(worldName, out string sanitized, out error))
                return false;

            string directory = GetWorldDirectory(sanitized);
            string path = GetWorldLevelPath(sanitized);

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, LevelFileSerializer.ToJson(data));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySaveActiveWorldFromGrid(out string error)
        {
            error = null;
            if (!WorldSession.HasActiveWorld)
            {
                error = "No active world.";
                return false;
            }

            KaplaoCodeModeUi.FlushPendingScriptSync();
            KaplaoExecutorEditorUi.FlushAllOpenEditorsBeforeModeChange();

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
            {
                error = "Level grid is not ready.";
                return false;
            }

            if (NetworkServer.active)
                LevelSessionServer.Instance?.ServerStopScript();

            LevelData data = grid.ExportLevelData();
            // Scripts live under Scripts/; keep level.json.script empty going forward.
            data.script = string.Empty;
            if (!TrySaveWorld(WorldSession.ActiveWorldName, data, out error))
                return false;

            return TryPersistActiveWorldScript(out error);
        }

        public static bool TryPersistActiveWorldScript(out string error)
        {
            error = null;
            if (!WorldSession.HasActiveWorld || WorldSession.IsBrowsePlaySession)
                return true;

            KaplaoCodeModeUi.FlushPendingScriptSync();

            if (!WorldScriptProjectService.HasProject)
            {
                string legacy = LevelSessionServer.Instance != null
                    ? LevelSessionServer.Instance.LevelScript ?? string.Empty
                    : KaplaoCodeModeUi.GetCurrentScriptText();
                if (!WorldScriptProjectService.TryEnsureProjectForWorld(WorldSession.ActiveWorldName, legacy, out error))
                    return false;
            }

            return WorldScriptProjectService.TryPersistAllToDisk(WorldSession.ActiveWorldName, out error);
        }

        /// <summary>
        /// Legacy helper: writes entry script into Scripts/main.ks (not level.json).
        /// </summary>
        public static bool TryUpdateWorldScript(string worldName, string script, out string error)
        {
            error = null;
            if (!TryValidateWorldName(worldName, out string sanitized, out error))
                return false;

            if (!WorldScriptProjectService.TryEnsureProjectForWorld(sanitized, script, out error))
                return false;

            return WorldScriptProjectService.TryWriteFile(sanitized, WorldScriptProjectService.EntryFileName, script ?? string.Empty, out error);
        }

        public static void OpenSavesFolder()
        {
            string directory = LevelIoPaths.LevelsDirectory;
            Directory.CreateDirectory(directory);

#if UNITY_STANDALONE_WIN
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
#elif UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(directory);
#else
            Application.OpenURL("file://" + directory.Replace("\\", "/"));
#endif
        }

        public static LevelData CreateEmptyLevelData()
        {
            // Default world: a flat platform of 9 blocks from X=-4..4 at Y=0.
            var tiles = new LevelTileRecord[9];
            for (int i = 0; i < tiles.Length; i++)
            {
                int x = i - 4;
                tiles[i] = new LevelTileRecord
                {
                    x = x,
                    y = 0f,
                    rotationDegrees = 0f,
                    scaleX = 1f,
                    scaleY = 1f,
                    type = TileType.Block,
                    r = 255,
                    g = 255,
                    b = 255,
                    textureId = string.Empty,
                    sortingOrder = 0,
                    groups = string.Empty,
                    quarterTurns = 0,
                    transparency = 1f,
                    noHitbox = false,
                    editorLayer = 0,
                    invisible = false
                };
            }

            return new LevelData
            {
                version = 10,
                script = string.Empty,
                tiles = tiles,
                linkGroups = Array.Empty<TileLinkGroupRecord>(),
                bannedIds = Array.Empty<string>(),
                bannedIps = Array.Empty<string>(),
                moderatorIds = Array.Empty<string>()
            };
        }

        public static string SanitizeWorldName(string worldName)
        {
            return string.IsNullOrWhiteSpace(worldName) ? string.Empty : worldName.Trim();
        }
    }
}
