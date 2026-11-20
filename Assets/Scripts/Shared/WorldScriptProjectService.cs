using Mirror;
using KaplaoGames.Server;
using KaplaoGames.LevelScript;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// File-based Level Script project under {World}/Scripts/.
    /// Host keeps disk as source of truth; clients keep an in-memory mirror.
    /// </summary>
    public static class WorldScriptProjectService
    {
        public const string FolderName = "Scripts";
        public const string ServerExtension = ".ks";
        public const string LocalExtension = ".lks";

        public const string EntryFileName = "main.ks";
        public const int MaxRelativePathLength = 180;
        public const int MaxFileBytes = 512 * 1024;

        static readonly Dictionary<string, ScriptNodeState> Nodes =
            new Dictionary<string, ScriptNodeState>(StringComparer.OrdinalIgnoreCase);

        static bool writeDisk = true;
        static readonly Dictionary<string, DateTime> internalWrites =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static void MarkInternalWrite(string relativePath)
        {
            string norm = NormalizeRelativePath(relativePath);
            if (!string.IsNullOrEmpty(norm))
                internalWrites[norm] = DateTime.UtcNow;
        }

        public static bool IsInternalWrite(string relativePath)
        {
            string norm = NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(norm))
                return false;

            if (internalWrites.TryGetValue(norm, out DateTime t))
            {
                if ((DateTime.UtcNow - t).TotalSeconds < 1.0)
                    return true;
                internalWrites.Remove(norm);
            }

            return false;
        }

        public static event Action ProjectChanged;

        public static bool HasProject => Nodes.Count > 0;
        public static bool WriteDiskEnabled => writeDisk;

        public static string GetScriptsDirectory(string worldName) =>
            Path.Combine(WorldSaveService.GetWorldDirectory(worldName), FolderName);

        public static void ClearRuntime()
        {
            Nodes.Clear();
            writeDisk = true;
            WorldScriptDiskWatcher.Instance?.StopWatching();
            ProjectChanged?.Invoke();
        }

        public static void SetWriteDiskEnabled(bool enabled) => writeDisk = enabled;

        public static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            string normalized = relativePath.Replace('\\', '/').Trim().Trim('/');
            while (normalized.Contains("//"))
                normalized = normalized.Replace("//", "/");
            return normalized;
        }

        public static bool TryValidateRelativePath(string relativePath, out string normalized, out string error)
        {
            normalized = NormalizeRelativePath(relativePath);
            error = null;

            if (string.IsNullOrEmpty(normalized))
            {
                error = "Path cannot be empty.";
                return false;
            }

            if (normalized.Length > MaxRelativePathLength)
            {
                error = $"Path must be {MaxRelativePathLength} characters or fewer.";
                return false;
            }

            if (normalized.Contains("..") || normalized.StartsWith("/") || normalized.Contains(":"))
            {
                error = "Path is invalid.";
                return false;
            }

            string[] parts = normalized.Split('/');
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrWhiteSpace(part) || part == "." || part == "..")
                {
                    error = "Path is invalid.";
                    return false;
                }

                for (int c = 0; c < part.Length; c++)
                {
                    if (invalid.Contains(part[c]))
                    {
                        error = "Path contains invalid characters.";
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool IsServerScriptPath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return normalized.EndsWith(ServerExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLocalScriptPath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return normalized.EndsWith(LocalExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryEnsureProjectForWorld(string worldName, string legacyScript, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(worldName))
            {
                error = "No active world.";
                return false;
            }

            writeDisk = true;
            string directory = GetScriptsDirectory(worldName);
            try
            {
                Directory.CreateDirectory(directory);
                string entryPath = Path.Combine(directory, EntryFileName);
                if (!File.Exists(entryPath))
                {
                    string content = !string.IsNullOrWhiteSpace(legacyScript)
                        ? legacyScript
                        : string.Empty;
                    File.WriteAllText(entryPath, content ?? string.Empty, Encoding.UTF8);
                }

                return TryLoadFromDisk(worldName, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryCreateDefaultProject(string worldName, string defaultScript, out string error)
        {
            error = null;
            ClearRuntime();
            writeDisk = true;

            if (!TryValidateRelativePath(EntryFileName, out string entry, out error))
                return false;

            Nodes[entry] = new ScriptNodeState
            {
                RelativePath = entry,
                IsDirectory = false,
                Content = defaultScript ?? string.Empty
            };

            return TryPersistAllToDisk(worldName, out error);
        }

        public static bool TryLoadFromDisk(string worldName, out string error)
        {
            error = null;
            Nodes.Clear();
            writeDisk = true;

            string root = GetScriptsDirectory(worldName);
            if (!Directory.Exists(root))
            {
                error = "Scripts folder not found.";
                return false;
            }

            try
            {
                foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                {
                    string relative = ToRelative(root, dir);
                    if (string.IsNullOrEmpty(relative))
                        continue;
                    Nodes[relative] = new ScriptNodeState
                    {
                        RelativePath = relative,
                        IsDirectory = true,
                        Content = string.Empty
                    };
                }

                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string relative = ToRelative(root, file);
                    if (string.IsNullOrEmpty(relative))
                        continue;

                    string content = File.ReadAllText(file, Encoding.UTF8);
                    Nodes[relative] = new ScriptNodeState
                    {
                        RelativePath = relative,
                        IsDirectory = false,
                        Content = content ?? string.Empty
                    };
                }

                if (!Nodes.ContainsKey(EntryFileName))
                {
                    Nodes[EntryFileName] = new ScriptNodeState
                    {
                        RelativePath = EntryFileName,
                        IsDirectory = false,
                        Content = string.Empty
                    };
                    TryWriteNodeToDisk(worldName, EntryFileName, out _);
                }

                RaiseChanged();
                if (writeDisk)
                    WorldScriptDiskWatcher.EnsureInstance().StartWatching(worldName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void ApplySnapshot(ScriptProjectSnapshot snapshot, bool persistDisk, string worldName = null)
        {
            Nodes.Clear();
            writeDisk = persistDisk;

            if (snapshot?.nodes != null)
            {
                for (int i = 0; i < snapshot.nodes.Length; i++)
                {
                    ScriptNodeRecord record = snapshot.nodes[i];
                    if (record == null || !TryValidateRelativePath(record.path, out string path, out _))
                        continue;

                    Nodes[path] = new ScriptNodeState
                    {
                        RelativePath = path,
                        IsDirectory = record.isDirectory,
                        Content = record.isDirectory ? string.Empty : (record.content ?? string.Empty)
                    };
                }
            }

            if (!Nodes.ContainsKey(EntryFileName))
            {
                Nodes[EntryFileName] = new ScriptNodeState
                {
                    RelativePath = EntryFileName,
                    IsDirectory = false,
                    Content = string.Empty
                };
            }

            if (persistDisk && !string.IsNullOrWhiteSpace(worldName))
                TryPersistAllToDisk(worldName, out _);

            RaiseChanged();
        }

        public static ScriptProjectSnapshot CaptureSnapshot()
        {
            var list = new List<ScriptNodeRecord>(Nodes.Count);
            foreach (ScriptNodeState node in Nodes.Values.OrderBy(n => n.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(new ScriptNodeRecord
                {
                    path = node.RelativePath,
                    isDirectory = node.IsDirectory,
                    content = node.IsDirectory ? string.Empty : (node.Content ?? string.Empty)
                });
            }

            return new ScriptProjectSnapshot { nodes = list.ToArray() };
        }

        public static string CaptureSnapshotJson() =>
            JsonUtility.ToJson(CaptureSnapshot());

        public static bool TryApplySnapshotJson(string json, bool persistDisk, string worldName, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "Empty script project.";
                return false;
            }

            try
            {
                ScriptProjectSnapshot snapshot = JsonUtility.FromJson<ScriptProjectSnapshot>(json);
                ApplySnapshot(snapshot, persistDisk, worldName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static IReadOnlyList<ScriptNodeState> GetSortedNodes()
        {
            return Nodes.Values
                .OrderBy(n => n.IsDirectory ? 0 : 1)
                .ThenBy(n => n.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool TryGetNode(string relativePath, out ScriptNodeState node)
        {
            node = null;
            if (!TryValidateRelativePath(relativePath, out string path, out _))
                return false;
            if (!Nodes.TryGetValue(path, out ScriptNodeState found))
                return false;
            node = found;
            return true;
        }

        public static bool TryReadFile(string relativePath, out string content, out string error)
        {
            content = string.Empty;
            if (!TryValidateRelativePath(relativePath, out string path, out error))
                return false;

            if (!Nodes.TryGetValue(path, out ScriptNodeState node) || node.IsDirectory)
            {
                error = "File not found.";
                return false;
            }

            content = node.Content ?? string.Empty;
            return true;
        }

        public static string GetEntryScriptContent()
        {
            if (TryReadFile(EntryFileName, out string content, out _))
                return content;
            return string.Empty;
        }

        public static bool TryWriteFile(string worldName, string relativePath, string content, out string error)
        {
            if (!TryValidateRelativePath(relativePath, out string path, out error))
                return false;

            if (Nodes.TryGetValue(path, out ScriptNodeState existing) && existing.IsDirectory)
            {
                error = "Cannot write a directory as a file.";
                return false;
            }

            content ??= string.Empty;
            if (Encoding.UTF8.GetByteCount(content) > MaxFileBytes)
            {
                error = "File is too large.";
                return false;
            }

            EnsureParentFolders(path);

            Nodes[path] = new ScriptNodeState
            {
                RelativePath = path,
                IsDirectory = false,
                Content = content
            };

            if (writeDisk && !string.IsNullOrWhiteSpace(worldName) &&
                !TryWriteNodeToDisk(worldName, path, out error))
                return false;

            RaiseChanged();
            return true;
        }

        public static bool TryCreateFile(string worldName, string relativePath, string content, out string error)
        {
            if (!TryValidateRelativePath(relativePath, out string path, out error))
                return false;

            if (Nodes.ContainsKey(path))
            {
                error = "A file or folder with this name already exists.";
                return false;
            }

            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
                path += ServerExtension;

            if (!path.EndsWith(ServerExtension, StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(LocalExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Script files must use {ServerExtension} or {LocalExtension}.";
                return false;
            }

            return TryWriteFile(worldName, path, content ?? string.Empty, out error);
        }

        public static bool TryCreateFolder(string worldName, string relativePath, out string error)
        {
            if (!TryValidateRelativePath(relativePath, out string path, out error))
                return false;

            if (Nodes.ContainsKey(path))
            {
                error = "A file or folder with this name already exists.";
                return false;
            }

            EnsureParentFolders(path);
            Nodes[path] = new ScriptNodeState
            {
                RelativePath = path,
                IsDirectory = true,
                Content = string.Empty
            };

            if (writeDisk && !string.IsNullOrWhiteSpace(worldName))
            {
                try
                {
                    MarkInternalWrite(path);
                    Directory.CreateDirectory(Path.Combine(GetScriptsDirectory(worldName), path.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            RaiseChanged();
            return true;
        }

        public static bool TryDelete(string worldName, string relativePath, out string error)
        {
            if (!TryValidateRelativePath(relativePath, out string path, out error))
                return false;

            if (string.Equals(path, EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Cannot delete main.ks.";
                return false;
            }

            if (!Nodes.ContainsKey(path))
            {
                error = "Not found.";
                return false;
            }

            var toRemove = new List<string>();
            foreach (string key in Nodes.Keys)
            {
                if (string.Equals(key, path, StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                Nodes.Remove(toRemove[i]);

            if (writeDisk && !string.IsNullOrWhiteSpace(worldName))
            {
                MarkInternalWrite(path);
                try
                {
                    string full = Path.Combine(GetScriptsDirectory(worldName), path.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(full))
                        Directory.Delete(full, recursive: true);
                    else if (File.Exists(full))
                        File.Delete(full);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            RaiseChanged();
            return true;
        }

        public static bool TryRename(string worldName, string relativePath, string newName, out string error)
        {
            if (!TryValidateRelativePath(relativePath, out string path, out error))
                return false;

            if (string.Equals(path, EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Cannot rename main.ks.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                error = "Name cannot be empty.";
                return false;
            }

            newName = newName.Trim();
            if (newName.Contains('/') || newName.Contains('\\'))
            {
                error = "Name cannot contain path separators.";
                return false;
            }

            string parent = GetParentPath(path);
            string destination = string.IsNullOrEmpty(parent) ? newName : parent + "/" + newName;
            return TryMove(worldName, path, destination, out error);
        }

        public static bool TryMove(string worldName, string fromRelativePath, string toRelativePath, out string error)
        {
            if (!TryValidateRelativePath(fromRelativePath, out string from, out error))
                return false;
            if (!TryValidateRelativePath(toRelativePath, out string to, out error))
                return false;

            if (string.Equals(from, EntryFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(to, EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Cannot move or overwrite main.ks.";
                return false;
            }

            if (!Nodes.TryGetValue(from, out ScriptNodeState source))
            {
                error = "Source not found.";
                return false;
            }

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                return true;

            if (Nodes.ContainsKey(to) ||
                Nodes.Keys.Any(k => k.StartsWith(to + "/", StringComparison.OrdinalIgnoreCase)))
            {
                error = "Destination already exists.";
                return false;
            }

            if (to.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase))
            {
                error = "Cannot move a folder into itself.";
                return false;
            }

            var moved = new List<ScriptNodeState>();
            var keys = Nodes.Keys.Where(k =>
                    string.Equals(k, from, StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                ScriptNodeState node = Nodes[key];
                string suffix = key.Length == from.Length ? string.Empty : key.Substring(from.Length);
                string newPath = to + suffix;
                moved.Add(new ScriptNodeState
                {
                    RelativePath = newPath,
                    IsDirectory = node.IsDirectory,
                    Content = node.Content
                });
                Nodes.Remove(key);
            }

            EnsureParentFolders(to);
            for (int i = 0; i < moved.Count; i++)
                Nodes[moved[i].RelativePath] = moved[i];

            if (writeDisk && !string.IsNullOrWhiteSpace(worldName))
            {
                MarkInternalWrite(from);
                MarkInternalWrite(to);
                try
                {
                    string root = GetScriptsDirectory(worldName);
                    string fromFull = Path.Combine(root, from.Replace('/', Path.DirectorySeparatorChar));
                    string toFull = Path.Combine(root, to.Replace('/', Path.DirectorySeparatorChar));
                    string toParent = Path.GetDirectoryName(toFull);
                    if (!string.IsNullOrEmpty(toParent))
                        Directory.CreateDirectory(toParent);

                    if (Directory.Exists(fromFull))
                        Directory.Move(fromFull, toFull);
                    else if (File.Exists(fromFull))
                        File.Move(fromFull, toFull);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            RaiseChanged();
            return true;
        }

        public static bool TryPersistAllToDisk(string worldName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(worldName))
            {
                error = "No world.";
                return false;
            }

            try
            {
                string root = GetScriptsDirectory(worldName);
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
                Directory.CreateDirectory(root);

                foreach (ScriptNodeState node in Nodes.Values.OrderBy(n => n.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    string full = Path.Combine(root, node.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (node.IsDirectory)
                    {
                        Directory.CreateDirectory(full);
                    }
                    else
                    {
                        string parent = Path.GetDirectoryName(full);
                        if (!string.IsNullOrEmpty(parent))
                            Directory.CreateDirectory(parent);
                        File.WriteAllText(full, node.Content ?? string.Empty, Encoding.UTF8);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void RevealInExplorer(string relativePath)
        {
            if (!WorldSession.HasActiveWorld)
                return;

            string root = GetScriptsDirectory(WorldSession.ActiveWorldName);
            Directory.CreateDirectory(root);

            string target = root;
            if (TryValidateRelativePath(relativePath, out string path, out _) &&
                Nodes.ContainsKey(path))
            {
                target = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (File.Exists(target))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + target + "\"",
                    UseShellExecute = true
                });
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Directory.Exists(target) ? target : root,
                UseShellExecute = true
            });
#elif UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(Directory.Exists(target) || File.Exists(target) ? target : root);
#else
            Application.OpenURL("file://" + root.Replace("\\", "/"));
#endif
        }

        static void EnsureParentFolders(string path)
        {
            string parent = GetParentPath(path);
            while (!string.IsNullOrEmpty(parent))
            {
                if (!Nodes.ContainsKey(parent))
                {
                    Nodes[parent] = new ScriptNodeState
                    {
                        RelativePath = parent,
                        IsDirectory = true,
                        Content = string.Empty
                    };
                }

                parent = GetParentPath(parent);
            }
        }

        public static string GetParentPath(string path)
        {
            string normalized = NormalizeRelativePath(path);
            int index = normalized.LastIndexOf('/');
            return index <= 0 ? string.Empty : normalized.Substring(0, index);
        }

        public static string CombinePath(string folder, string name)
        {
            folder = NormalizeRelativePath(folder);
            name = (name ?? string.Empty).Trim().Trim('/');
            if (string.IsNullOrEmpty(folder))
                return NormalizeRelativePath(name);
            return NormalizeRelativePath(folder + "/" + name);
        }

        static bool TryWriteNodeToDisk(string worldName, string relativePath, out string error)
        {
            error = null;
            if (!Nodes.TryGetValue(relativePath, out ScriptNodeState node))
            {
                error = "Not found.";
                return false;
            }

            try
            {
                MarkInternalWrite(relativePath);
                string root = GetScriptsDirectory(worldName);
                Directory.CreateDirectory(root);
                string full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (node.IsDirectory)
                {
                    Directory.CreateDirectory(full);
                }
                else
                {
                    string parent = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);
                    File.WriteAllText(full, node.Content ?? string.Empty, Encoding.UTF8);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void ApplyExternalFileChange(string relativePath, string content)
        {
            string path = NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(path))
                return;

            EnsureParentFolders(path);
            Nodes[path] = new ScriptNodeState
            {
                RelativePath = path,
                IsDirectory = false,
                Content = content ?? string.Empty
            };

            if (string.Equals(path, EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                LevelSessionServer.Instance?.ServerSyncEntryScriptFromProject();
            }

            if (LevelSessionServer.Instance != null && NetworkServer.active)
            {
                LevelSessionServer.Instance.ServerBroadcastScriptProject();
            }
            else
            {
                RaiseChanged();
            }

            if (LevelSessionServer.Instance != null && LevelSessionServer.Instance.ScriptRunning)
            {
                LevelSessionServer.Instance.ServerRestartRunningScriptForAll();
            }

            if (LevelScriptLocalRuntime.Instance != null && LevelScriptLocalRuntime.Instance.IsRunning)
            {
                LevelScriptLocalRuntime.Instance.StartLocalScripts();
            }

            KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput($"[LiveSync] '{path}' updated from disk.", KaplaoGames.Client.OutputLogType.Info);
        }

        public static void ApplyExternalDirectory(string relativePath)
        {
            string path = NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(path))
                return;

            EnsureParentFolders(path);
            Nodes[path] = new ScriptNodeState
            {
                RelativePath = path,
                IsDirectory = true,
                Content = string.Empty
            };

            if (LevelSessionServer.Instance != null && NetworkServer.active)
                LevelSessionServer.Instance.ServerBroadcastScriptProject();
            else
                RaiseChanged();
        }

        public static void ApplyExternalDelete(string relativePath)
        {
            string path = NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(path))
                return;

            if (string.Equals(path, EntryFileName, StringComparison.OrdinalIgnoreCase))
            {
                Nodes[EntryFileName] = new ScriptNodeState
                {
                    RelativePath = EntryFileName,
                    IsDirectory = false,
                    Content = string.Empty
                };
            }
            else
            {
                var toRemove = new List<string>();
                foreach (string key in Nodes.Keys)
                {
                    if (string.Equals(key, path, StringComparison.OrdinalIgnoreCase) ||
                        key.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase))
                        toRemove.Add(key);
                }

                for (int i = 0; i < toRemove.Count; i++)
                    Nodes.Remove(toRemove[i]);
            }

            if (LevelSessionServer.Instance != null && NetworkServer.active)
                LevelSessionServer.Instance.ServerBroadcastScriptProject();
            else
                RaiseChanged();

            KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput($"[LiveSync] '{path}' deleted from disk.", KaplaoGames.Client.OutputLogType.Warning);
        }

        public static void ApplyExternalRename(string oldRelativePath, string newRelativePath)
        {
            string oldPath = NormalizeRelativePath(oldRelativePath);
            string newPath = NormalizeRelativePath(newRelativePath);

            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                return;

            if (Nodes.TryGetValue(oldPath, out ScriptNodeState node))
            {
                Nodes.Remove(oldPath);
                EnsureParentFolders(newPath);
                node.RelativePath = newPath;
                Nodes[newPath] = node;
            }

            if (LevelSessionServer.Instance != null && NetworkServer.active)
                LevelSessionServer.Instance.ServerBroadcastScriptProject();
            else
                RaiseChanged();

            KaplaoGames.Client.KaplaoCodeModeUi.Instance?.AppendOutput($"[LiveSync] Renamed '{oldPath}' -> '{newPath}'.", KaplaoGames.Client.OutputLogType.Info);
        }

        public static string ToRelative(string root, string fullPath)
        {
            string relative = fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return NormalizeRelativePath(relative);
        }

        static void RaiseChanged() => ProjectChanged?.Invoke();
    }

    [Serializable]
    public class ScriptProjectSnapshot
    {
        public ScriptNodeRecord[] nodes = Array.Empty<ScriptNodeRecord>();
    }

    [Serializable]
    public class ScriptNodeRecord
    {
        public string path = string.Empty;
        public bool isDirectory;
        public string content = string.Empty;
    }

    public sealed class ScriptNodeState
    {
        public string RelativePath;
        public bool IsDirectory;
        public string Content;
    }
}
