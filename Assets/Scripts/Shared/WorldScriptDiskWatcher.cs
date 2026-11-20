using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KaplaoGames.Client;
using KaplaoGames.LevelScript;
using KaplaoGames.Server;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Watches the active world's Scripts/ directory on disk and live-syncs
    /// any external changes (from Antigravity, VSCode, etc.) into the running game session.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldScriptDiskWatcher : MonoBehaviour
    {
        public static WorldScriptDiskWatcher Instance { get; private set; }

        enum ChangeType
        {
            ChangedOrCreated,
            Deleted,
            Renamed
        }

        struct PendingChange
        {
            public ChangeType Type;
            public string RelativePath;
            public string OldRelativePath;
            public string FullPath;
            public float Timestamp;
        }

        FileSystemWatcher watcher;
        string watchingWorldName;
        string watchingDirectory;

        readonly object lockObj = new object();
        readonly Dictionary<string, PendingChange> pendingChanges = new Dictionary<string, PendingChange>(StringComparer.OrdinalIgnoreCase);

        const float DebounceTime = 0.08f; // 80 ms

        public string ActiveWorldName => watchingWorldName;
        public string ActiveDirectory => watchingDirectory;
        public bool IsWatching => watcher != null && watcher.EnableRaisingEvents;

        public static WorldScriptDiskWatcher EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("WorldScriptDiskWatcher");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<WorldScriptDiskWatcher>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            StopWatching();
        }

        public void StartWatching(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName))
                return;

            string directory = WorldScriptProjectService.GetScriptsDirectory(worldName);
            if (string.Equals(watchingWorldName, worldName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(watchingDirectory, directory, StringComparison.OrdinalIgnoreCase) &&
                IsWatching)
            {
                return;
            }

            StopWatching();

            watchingWorldName = worldName;
            watchingDirectory = directory;

            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                watcher.Created += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Deleted += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemRenamed;

                WriteSessionMarker(worldName, directory, active: true);
                Debug.Log($"[WorldScriptDiskWatcher] Started live disk sync on '{directory}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WorldScriptDiskWatcher] Failed to start watcher: {ex.Message}");
            }
        }

        public void StopWatching()
        {
            if (watcher != null)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnFileSystemEvent;
                    watcher.Changed -= OnFileSystemEvent;
                    watcher.Deleted -= OnFileSystemEvent;
                    watcher.Renamed -= OnFileSystemRenamed;
                    watcher.Dispose();
                }
                catch { }
                watcher = null;
            }

            lock (lockObj)
            {
                pendingChanges.Clear();
            }

            if (!string.IsNullOrEmpty(watchingWorldName))
            {
                WriteSessionMarker(watchingWorldName, watchingDirectory, active: false);
            }

            watchingWorldName = null;
            watchingDirectory = null;
        }

        void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(watchingDirectory) || string.IsNullOrEmpty(e.FullPath))
                return;

            string fileName = Path.GetFileName(e.FullPath);
            if (string.IsNullOrEmpty(fileName) || fileName.StartsWith(".") || fileName.EndsWith("~") || fileName.EndsWith(".tmp") || fileName.EndsWith(".crswap"))
                return;

            string relative = WorldScriptProjectService.ToRelative(watchingDirectory, e.FullPath);
            if (string.IsNullOrEmpty(relative))
                return;

            bool isScript = WorldScriptProjectService.IsServerScriptPath(relative) || WorldScriptProjectService.IsLocalScriptPath(relative);
            bool isDir = Directory.Exists(e.FullPath);

            if (!isScript && !isDir && e.ChangeType != WatcherChangeTypes.Deleted)
                return;

            lock (lockObj)
            {
                pendingChanges[relative] = new PendingChange
                {
                    Type = e.ChangeType == WatcherChangeTypes.Deleted ? ChangeType.Deleted : ChangeType.ChangedOrCreated,
                    RelativePath = relative,
                    FullPath = e.FullPath,
                    Timestamp = Time.realtimeSinceStartup
                };
            }
        }

        void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            if (string.IsNullOrEmpty(watchingDirectory) || string.IsNullOrEmpty(e.FullPath))
                return;

            string oldRel = WorldScriptProjectService.ToRelative(watchingDirectory, e.OldFullPath);
            string newRel = WorldScriptProjectService.ToRelative(watchingDirectory, e.FullPath);

            if (string.IsNullOrEmpty(newRel))
                return;

            lock (lockObj)
            {
                pendingChanges[newRel] = new PendingChange
                {
                    Type = ChangeType.Renamed,
                    RelativePath = newRel,
                    OldRelativePath = oldRel,
                    FullPath = e.FullPath,
                    Timestamp = Time.realtimeSinceStartup
                };
            }
        }

        void Update()
        {
            if (pendingChanges.Count == 0)
                return;

            List<PendingChange> ready = null;
            float now = Time.realtimeSinceStartup;

            lock (lockObj)
            {
                List<string> keysToRemove = null;
                foreach (var kvp in pendingChanges)
                {
                    if (now - kvp.Value.Timestamp >= DebounceTime)
                    {
                        ready ??= new List<PendingChange>();
                        ready.Add(kvp.Value);
                        keysToRemove ??= new List<string>();
                        keysToRemove.Add(kvp.Key);
                    }
                }

                if (keysToRemove != null)
                {
                    for (int i = 0; i < keysToRemove.Count; i++)
                        pendingChanges.Remove(keysToRemove[i]);
                }
            }

            if (ready != null)
            {
                for (int i = 0; i < ready.Count; i++)
                    ProcessChange(ready[i]);
            }
        }

        void ProcessChange(PendingChange change)
        {
            if (WorldScriptProjectService.IsInternalWrite(change.RelativePath))
                return;

            switch (change.Type)
            {
                case ChangeType.Deleted:
                    WorldScriptProjectService.ApplyExternalDelete(change.RelativePath);
                    break;

                case ChangeType.Renamed:
                    WorldScriptProjectService.ApplyExternalRename(change.OldRelativePath, change.RelativePath);
                    break;

                case ChangeType.ChangedOrCreated:
                    if (Directory.Exists(change.FullPath))
                    {
                        WorldScriptProjectService.ApplyExternalDirectory(change.RelativePath);
                    }
                    else if (File.Exists(change.FullPath))
                    {
                        if (TryReadTextWithRetry(change.FullPath, out string content))
                        {
                            WorldScriptProjectService.ApplyExternalFileChange(change.RelativePath, content);
                        }
                    }
                    break;
            }
        }

        static bool TryReadTextWithRetry(string path, out string content, int maxRetries = 6, int delayMs = 25)
        {
            content = null;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (!File.Exists(path))
                        return false;

                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        content = reader.ReadToEnd();
                        return true;
                    }
                }
                catch (IOException)
                {
                    if (i == maxRetries - 1)
                        return false;
                    System.Threading.Thread.Sleep(delayMs);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        void WriteSessionMarker(string worldName, string scriptsDir, bool active)
        {
            try
            {
                string markerPath = Path.Combine(LevelIoPaths.LevelsDirectory, ".active_session.json");
                if (!active)
                {
                    if (File.Exists(markerPath))
                        File.Delete(markerPath);
                    return;
                }

                string json = "{\n" +
                              $"  \"worldName\": \"{worldName}\",\n" +
                              $"  \"scriptsDirectory\": \"{scriptsDir.Replace("\\", "\\\\")}\",\n" +
                              $"  \"active\": true,\n" +
                              $"  \"pid\": {System.Diagnostics.Process.GetCurrentProcess().Id},\n" +
                              $"  \"updatedAt\": \"{DateTime.UtcNow:O}\"\n" +
                              "}";
                File.WriteAllText(markerPath, json, Encoding.UTF8);
            }
            catch { }
        }
    }
}
