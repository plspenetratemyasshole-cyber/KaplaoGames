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
    [Serializable]
    public struct WorldMetadata
    {
        public long createdUtcTicks;
        public long lastPlayedUtcTicks;
    }

    public struct WorldListItem
    {
        public string Name;
        public long LastPlayedUtcTicks;
        public long CreatedUtcTicks;
    }

    public static class WorldSaveService
    {
        public const string LevelFileName = "level.json";
        public const string MetadataFileName = "world_meta.json";
        const int MaxWorldNameLength = 48;

        public static string GetWorldMetadataPath(string worldName)
        {
            return Path.Combine(GetUserWorldDirectory(worldName), MetadataFileName);
        }

        public static void RecordWorldVisited(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName) || WorldSession.IsBrowsePlaySession)
                return;

            string sanitized = SanitizeWorldName(worldName);
            string dir = GetUserWorldDirectory(sanitized);
            if (!Directory.Exists(dir))
                return;

            try
            {
                WorldMetadata meta = default;
                string metaPath = GetWorldMetadataPath(sanitized);
                if (File.Exists(metaPath))
                {
                    try
                    {
                        string existingJson = File.ReadAllText(metaPath);
                        meta = JsonUtility.FromJson<WorldMetadata>(existingJson);
                    }
                    catch
                    {
                        meta = default;
                    }
                }

                long nowTicks = DateTime.UtcNow.Ticks;
                meta.lastPlayedUtcTicks = nowTicks;
                if (meta.createdUtcTicks <= 0)
                {
                    string levelPath = Path.Combine(dir, LevelFileName);
                    if (File.Exists(levelPath))
                        meta.createdUtcTicks = File.GetCreationTimeUtc(levelPath).Ticks;
                    else
                        meta.createdUtcTicks = nowTicks;
                }

                File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WorldSaveService] Failed to record world visited for '{sanitized}': {ex.Message}");
            }
        }

        public static WorldMetadata ReadWorldMetadata(string worldName)
        {
            string sanitized = SanitizeWorldName(worldName);
            string dir = GetUserWorldDirectory(sanitized);
            string metaPath = GetWorldMetadataPath(sanitized);

            if (File.Exists(metaPath))
            {
                try
                {
                    string json = File.ReadAllText(metaPath);
                    WorldMetadata meta = JsonUtility.FromJson<WorldMetadata>(json);
                    if (meta.lastPlayedUtcTicks > 0)
                        return meta;
                }
                catch
                {
                    // Fall back to file timestamps below
                }
            }

            // Fallback for old worlds without world_meta.json
            string levelPath = Path.Combine(dir, LevelFileName);
            long lastPlayed = 0;
            long created = 0;
            if (File.Exists(levelPath))
            {
                lastPlayed = File.GetLastWriteTimeUtc(levelPath).Ticks;
                created = File.GetCreationTimeUtc(levelPath).Ticks;
            }
            else if (Directory.Exists(dir))
            {
                lastPlayed = Directory.GetLastWriteTimeUtc(dir).Ticks;
                created = Directory.GetCreationTimeUtc(dir).Ticks;
            }

            return new WorldMetadata
            {
                createdUtcTicks = created > 0 ? created : DateTime.UtcNow.Ticks,
                lastPlayedUtcTicks = lastPlayed > 0 ? lastPlayed : DateTime.UtcNow.Ticks
            };
        }

        public static IReadOnlyList<WorldListItem> ListWorldsWithMetadata()
        {
            string root = LevelIoPaths.LevelsDirectory;
            if (!Directory.Exists(root))
                return Array.Empty<WorldListItem>();

            string[] directories = Directory.GetDirectories(root);
            var worlds = new List<WorldListItem>(directories.Length);
            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                string name = Path.GetFileName(directory);
                if (!File.Exists(GetWorldLevelPath(name)))
                    continue;

                WorldMetadata meta = ReadWorldMetadata(name);
                worlds.Add(new WorldListItem
                {
                    Name = name,
                    LastPlayedUtcTicks = meta.lastPlayedUtcTicks,
                    CreatedUtcTicks = meta.createdUtcTicks
                });
            }

            worlds.Sort((a, b) =>
            {
                int cmp = b.LastPlayedUtcTicks.CompareTo(a.LastPlayedUtcTicks);
                if (cmp != 0) return cmp;
                return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            });

            return worlds;
        }

        public static IReadOnlyList<string> ListWorlds()
        {
            var withMeta = ListWorldsWithMetadata();
            var names = new List<string>(withMeta.Count);
            for (int i = 0; i < withMeta.Count; i++)
                names.Add(withMeta[i].Name);
            return names;
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
                RecordWorldVisited(sanitized);
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
                if (!WorldSession.IsBrowsePlaySession)
                    RecordWorldVisited(sanitized);
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
