using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.LevelScript;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Shared level session state: one script for all Kaplao players.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public class LevelSessionServer : NetworkBehaviour
    {
        public enum ModerationDecision
        {
            Accept = 0,
            Reject = 1,
            Kick = 2,
            Ban = 3
        }

        public const string DefaultLevelScript = @"spawn {
  while (true) {
    move(Group(1), 2, 0, 1.5, ""easeOut"")
    wait(1)
    rotate(Group(1), 1, 90, 2.0, ""linear"")
    wait(1)
  }
}
";

        public static LevelSessionServer Instance { get; private set; }

        [SyncVar(hook = nameof(OnLevelScriptChanged))]
        string syncedLevelScript = string.Empty;

        [SyncVar(hook = nameof(OnScriptRunningChanged))]
        bool syncedScriptRunning;

        [SyncVar(hook = nameof(OnRunnersCanInteractChanged))]
        bool runnersCanInteract = true;

        [SyncVar]
        float syncedWorldSpawnX = 0f;

        [SyncVar]
        float syncedWorldSpawnY = 5f;

        public Vector2 WorldSpawn => new Vector2(syncedWorldSpawnX, syncedWorldSpawnY);

        [SyncVar]
        bool moderateChatEnabled = false;

        // Saved by displayName (nickname), without extra normalization besides Trim().
        // We keep it case-sensitive to match what is stored in level.json.
        readonly HashSet<string> bannedIds = new HashSet<string>();
        readonly HashSet<string> bannedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> moderatorIds = new HashSet<string>();
        /// <summary>Client-side mirror of banned nicknames (server uses <see cref="bannedIds"/>).</summary>
        readonly HashSet<string> clientBannedIds = new HashSet<string>();
        readonly HashSet<string> clientBannedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        struct PendingModeration
        {
            public ulong requestId;
            public string senderDisplayName;
            public string message;
            public NetworkConnectionToClient senderConn;
            public NetworkConnectionToClient moderatorConn;
            public string moderatorDisplayName;
        }

        ulong nextChatModerationRequestId = 1;
        readonly Dictionary<ulong, PendingModeration> pendingChatModerations = new Dictionary<ulong, PendingModeration>();
        readonly List<(NetworkConnectionToClient senderConn, NetworkPlayer senderPlayer, string message, bool isSosaliTrigger)> awaitingModeratorAssignment =
            new List<(NetworkConnectionToClient, NetworkPlayer, string, bool)>();

        public event Action BannedIdsChanged;
        public event Action<string> LevelScriptChanged;
        public event Action<bool> ScriptRunningStateChanged;
        public event Action ScriptProjectChanged;
        public event Action<string> ScriptRuntimeError;
        public event Action<string> ScriptPrint;
        public event Action<string> ScriptWarning;

        const int MaxScriptProjectChunkChars = 24000;
        readonly Dictionary<int, StringBuilder> scriptProjectSyncBuilders = new Dictionary<int, StringBuilder>();
        readonly Dictionary<int, int> scriptProjectSyncExpectedLengths = new Dictionary<int, int>();
        int nextScriptProjectSyncId = 1;

        public string LevelScript => syncedLevelScript ?? string.Empty;
        public bool ScriptRunning => syncedScriptRunning;
        public bool RunnersCanInteract => runnersCanInteract;
        /// <summary>Server HashSet when hosting; client cache when connected as client.</summary>
        public HashSet<string> BannedIds => NetworkServer.active ? bannedIds : clientBannedIds;
        public HashSet<string> BannedIps => NetworkServer.active ? bannedIps : clientBannedIps;
        public HashSet<string> ModeratorIds => moderatorIds;
        public bool ModerateChatEnabled => moderateChatEnabled;

        static bool IsHostOrAdminSender(NetworkConnectionToClient sender)
        {
            if (sender == null)
                return false;

            if (sender == NetworkServer.localConnection)
                return true;

            return sender.identity != null &&
                   sender.identity.TryGetComponent(out NetworkPlayer np) &&
                   np.isAdmin;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LevelSessionServer] Duplicate instance destroyed.");
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        const int MaxCustomTextureChunkBytes = 24000;
        readonly Dictionary<string, List<byte>> customTextureSyncBuilders = new Dictionary<string, List<byte>>();
        readonly Dictionary<string, int> customTextureSyncExpectedLengths = new Dictionary<string, int>();

        [Server]
        public void ServerBroadcastCustomTexture(string textureId, byte[] pngBytes)
        {
            if (string.IsNullOrEmpty(textureId) || pngBytes == null || pngBytes.Length == 0)
                return;

            RpcBeginCustomTextureSync(textureId, pngBytes.Length);
            for (int offset = 0; offset < pngBytes.Length; offset += MaxCustomTextureChunkBytes)
            {
                int length = Math.Min(MaxCustomTextureChunkBytes, pngBytes.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(pngBytes, offset, chunk, 0, length);
                RpcAppendCustomTextureSyncChunk(textureId, chunk);
            }

            RpcFinishCustomTextureSync(textureId);
        }

        [Server]
        public void ServerSyncAllCustomTexturesToConnection(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            foreach (KeyValuePair<string, byte[]> entry in WorldCustomTextureService.CachedEntries)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value == null || entry.Value.Length == 0)
                    continue;

                TargetBeginCustomTextureSync(conn, entry.Key, entry.Value.Length);
                for (int offset = 0; offset < entry.Value.Length; offset += MaxCustomTextureChunkBytes)
                {
                    int length = Math.Min(MaxCustomTextureChunkBytes, entry.Value.Length - offset);
                    var chunk = new byte[length];
                    Buffer.BlockCopy(entry.Value, offset, chunk, 0, length);
                    TargetAppendCustomTextureSyncChunk(conn, entry.Key, chunk);
                }

                TargetFinishCustomTextureSync(conn, entry.Key);
            }
        }

        [ClientRpc]
        void RpcBeginCustomTextureSync(string textureId, int totalLength) =>
            BeginCustomTextureSyncLocal(textureId, totalLength);

        [ClientRpc]
        void RpcAppendCustomTextureSyncChunk(string textureId, byte[] chunk) =>
            AppendCustomTextureSyncLocal(textureId, chunk);

        [ClientRpc]
        void RpcFinishCustomTextureSync(string textureId) =>
            FinishCustomTextureSyncLocal(textureId);

        [TargetRpc]
        void TargetBeginCustomTextureSync(NetworkConnectionToClient conn, string textureId, int totalLength) =>
            BeginCustomTextureSyncLocal(textureId, totalLength);

        [TargetRpc]
        void TargetAppendCustomTextureSyncChunk(NetworkConnectionToClient conn, string textureId, byte[] chunk) =>
            AppendCustomTextureSyncLocal(textureId, chunk);

        [TargetRpc]
        void TargetFinishCustomTextureSync(NetworkConnectionToClient conn, string textureId) =>
            FinishCustomTextureSyncLocal(textureId);

        void BeginCustomTextureSyncLocal(string textureId, int totalLength)
        {
            if (string.IsNullOrEmpty(textureId) || totalLength <= 0)
                return;

            customTextureSyncBuilders[textureId] = new List<byte>(totalLength);
            customTextureSyncExpectedLengths[textureId] = totalLength;
        }

        void AppendCustomTextureSyncLocal(string textureId, byte[] chunk)
        {
            if (string.IsNullOrEmpty(textureId) || chunk == null || chunk.Length == 0)
                return;

            if (!customTextureSyncBuilders.TryGetValue(textureId, out List<byte> builder))
                return;

            builder.AddRange(chunk);
        }

        void FinishCustomTextureSyncLocal(string textureId)
        {
            if (string.IsNullOrEmpty(textureId))
                return;

            if (!customTextureSyncBuilders.TryGetValue(textureId, out List<byte> builder) ||
                !customTextureSyncExpectedLengths.TryGetValue(textureId, out int expectedLength))
            {
                CleanupCustomTextureSync(textureId);
                return;
            }

            if (builder.Count != expectedLength)
            {
                CleanupCustomTextureSync(textureId);
                return;
            }

            // Host already registered when importing / loading the world.
            if (!NetworkServer.active)
                WorldCustomTextureService.TryRegisterFromPng(textureId, builder.ToArray(), out _);

            RefreshTilesWithTexture(textureId);
            CleanupCustomTextureSync(textureId);
        }

        void CleanupCustomTextureSync(string textureId)
        {
            customTextureSyncBuilders.Remove(textureId);
            customTextureSyncExpectedLengths.Remove(textureId);
        }

        static void RefreshTilesWithTexture(string textureId)
        {
            if (string.IsNullOrEmpty(textureId))
                return;

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile != null &&
                    !string.IsNullOrEmpty(tile.TextureId) &&
                    string.Equals(tile.TextureId, textureId, StringComparison.OrdinalIgnoreCase))
                {
                    tile.RefreshClientVisuals();
                }
            }
        }

        [Server]
        public void ServerBroadcastCustomTextureRename(string oldTextureId, string newTextureId)
        {
            if (string.IsNullOrEmpty(oldTextureId) || string.IsNullOrEmpty(newTextureId))
                return;

            RpcRenameCustomTexture(oldTextureId, newTextureId);
        }

        [ClientRpc]
        void RpcRenameCustomTexture(string oldTextureId, string newTextureId)
        {
            if (NetworkServer.active)
                return;

            if (!WorldCustomTextureService.TryRegisterRenameOnClient(oldTextureId, newTextureId))
                return;

            RefreshTilesWithTexture(newTextureId);
        }

        [Server]
        public void ServerSetLevelScript(string script)
        {
            syncedLevelScript = script ?? string.Empty;
            NotifyLevelScriptChanged();
        }

        [Server]
        public void ServerNotifyLevelScriptLoaded()
        {
            NotifyLevelScriptChanged();
        }

        [Server]
        public void ServerSyncEntryScriptFromProject()
        {
            ServerSetLevelScript(WorldScriptProjectService.GetEntryScriptContent());
        }

        [Server]
        public void ServerBroadcastScriptProject()
        {
            if (!NetworkServer.active)
                return;

            string json = WorldScriptProjectService.CaptureSnapshotJson();
            BroadcastScriptProjectJson(json);
            ScriptProjectChanged?.Invoke();
        }

        [Server]
        public void ServerSyncScriptProjectToConnection(NetworkConnectionToClient conn)
        {
            if (conn == null || !NetworkServer.active)
                return;

            string json = WorldScriptProjectService.CaptureSnapshotJson();
            SendScriptProjectJsonToConnection(conn, json);
        }

        [Server]
        public bool ServerTryWriteScriptFile(string relativePath, string content, out string error)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (!WorldScriptProjectService.TryWriteFile(worldName, relativePath, content, out error))
                return false;

            if (string.Equals(
                    WorldScriptProjectService.NormalizeRelativePath(relativePath),
                    WorldScriptProjectService.EntryFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                ServerSetLevelScript(content ?? string.Empty);
            }

            ServerBroadcastScriptProject();
            return true;
        }

        [Server]
        public bool ServerTryCreateScriptFile(string relativePath, string content, out string error)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (!WorldScriptProjectService.TryCreateFile(worldName, relativePath, content, out error))
                return false;

            ServerBroadcastScriptProject();
            return true;
        }

        [Server]
        public bool ServerTryCreateScriptFolder(string relativePath, out string error)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (!WorldScriptProjectService.TryCreateFolder(worldName, relativePath, out error))
                return false;

            ServerBroadcastScriptProject();
            return true;
        }

        [Server]
        public bool ServerTryDeleteScriptNode(string relativePath, out string error)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (!WorldScriptProjectService.TryDelete(worldName, relativePath, out error))
                return false;

            ServerSyncEntryScriptFromProject();
            ServerBroadcastScriptProject();
            return true;
        }

        [Server]
        public bool ServerTryRenameScriptNode(string relativePath, string newName, out string error)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (!WorldScriptProjectService.TryRename(worldName, relativePath, newName, out error))
                return false;

            ServerSyncEntryScriptFromProject();
            ServerBroadcastScriptProject();
            return true;
        }

        [Server]
        public bool ServerTryMoveScriptNode(string fromRelativePath, string toRelativePath, out string error)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (!WorldScriptProjectService.TryMove(worldName, fromRelativePath, toRelativePath, out error))
                return false;

            ServerSyncEntryScriptFromProject();
            ServerBroadcastScriptProject();
            return true;
        }

        void BroadcastScriptProjectJson(string json)
        {
            json ??= string.Empty;
            int syncId = nextScriptProjectSyncId++;
            RpcBeginScriptProjectSync(syncId, json.Length);
            for (int offset = 0; offset < json.Length; offset += MaxScriptProjectChunkChars)
            {
                int length = Math.Min(MaxScriptProjectChunkChars, json.Length - offset);
                RpcAppendScriptProjectSyncChunk(syncId, json.Substring(offset, length));
            }

            RpcFinishScriptProjectSync(syncId);
        }

        void SendScriptProjectJsonToConnection(NetworkConnectionToClient conn, string json)
        {
            json ??= string.Empty;
            int syncId = nextScriptProjectSyncId++;
            TargetBeginScriptProjectSync(conn, syncId, json.Length);
            for (int offset = 0; offset < json.Length; offset += MaxScriptProjectChunkChars)
            {
                int length = Math.Min(MaxScriptProjectChunkChars, json.Length - offset);
                TargetAppendScriptProjectSyncChunk(conn, syncId, json.Substring(offset, length));
            }

            TargetFinishScriptProjectSync(conn, syncId);
        }

        [ClientRpc]
        void RpcBeginScriptProjectSync(int syncId, int totalLength) =>
            BeginScriptProjectSyncLocal(syncId, totalLength);

        [ClientRpc]
        void RpcAppendScriptProjectSyncChunk(int syncId, string chunk) =>
            AppendScriptProjectSyncLocal(syncId, chunk);

        [ClientRpc]
        void RpcFinishScriptProjectSync(int syncId) =>
            FinishScriptProjectSyncLocal(syncId);

        [TargetRpc]
        void TargetBeginScriptProjectSync(NetworkConnectionToClient conn, int syncId, int totalLength) =>
            BeginScriptProjectSyncLocal(syncId, totalLength);

        [TargetRpc]
        void TargetAppendScriptProjectSyncChunk(NetworkConnectionToClient conn, int syncId, string chunk) =>
            AppendScriptProjectSyncLocal(syncId, chunk);

        [TargetRpc]
        void TargetFinishScriptProjectSync(NetworkConnectionToClient conn, int syncId) =>
            FinishScriptProjectSyncLocal(syncId);

        void BeginScriptProjectSyncLocal(int syncId, int totalLength)
        {
            if (NetworkServer.active)
                return;

            if (totalLength < 0)
                return;

            var builder = new StringBuilder(Math.Max(16, totalLength));
            scriptProjectSyncBuilders[syncId] = builder;
            scriptProjectSyncExpectedLengths[syncId] = totalLength;
        }

        void AppendScriptProjectSyncLocal(int syncId, string chunk)
        {
            if (NetworkServer.active)
                return;

            if (!scriptProjectSyncBuilders.TryGetValue(syncId, out StringBuilder builder) || chunk == null)
                return;

            builder.Append(chunk);
        }

        void FinishScriptProjectSyncLocal(int syncId)
        {
            if (NetworkServer.active)
                return;

            if (!scriptProjectSyncBuilders.TryGetValue(syncId, out StringBuilder builder))
                return;

            scriptProjectSyncBuilders.Remove(syncId);
            scriptProjectSyncExpectedLengths.TryGetValue(syncId, out int expected);
            scriptProjectSyncExpectedLengths.Remove(syncId);

            string json = builder.ToString();
            if (expected > 0 && json.Length != expected)
            {
                Debug.LogWarning($"[LevelSessionServer] Script project sync size mismatch ({json.Length}/{expected}).");
                return;
            }

            if (!WorldScriptProjectService.TryApplySnapshotJson(json, persistDisk: false, worldName: null, out string error))
            {
                Debug.LogWarning($"[LevelSessionServer] Failed to apply script project: {error}");
                return;
            }

            ScriptProjectChanged?.Invoke();
        }

        void NotifyLevelScriptChanged()
        {
            LevelScriptChanged?.Invoke(LevelScript);
            if (NetworkServer.active)
                RpcLevelScriptChanged(LevelScript);
        }

        [ClientRpc]
        void RpcLevelScriptChanged(string script)
        {
            if (NetworkServer.active)
                return;

            LevelScriptChanged?.Invoke(script ?? string.Empty);
        }

        [Server]
        public void ServerSetWorldSpawn(float x, float y)
        {
            syncedWorldSpawnX = x;
            syncedWorldSpawnY = y;
            RunnerSpawnPoint.SetOverride(new Vector2(x, y));
        }

        [Server]
        public void ServerSetScriptRunning(bool running)
        {
            syncedScriptRunning = running;
        }

        [Server]
        public bool ServerTryStartScript(string source, out string error)
        {
            error = null;

            if (LevelScriptRuntime.Instance == null)
            {
                error = "Add LevelScriptRuntime to LevelGrid in the scene.";
                return false;
            }

            if (!LevelScriptRuntime.Instance.TryRun(source, out error))
                return false;

            ServerSetLevelScript(source);
            ServerSetScriptRunning(true);
            return true;
        }

        [Server]
        public void ServerStopScript()
        {
            ServerSetScriptRunning(false);
            LevelScriptRuntime.Instance?.StopScript();
        }

        [Server]
        public void ServerReportScriptError(string message)
        {
            ServerSetScriptRunning(false);
            RpcScriptRuntimeError(message ?? string.Empty);
        }

        [ClientRpc]
        void RpcScriptRuntimeError(string message)
        {
            ScriptRuntimeError?.Invoke(message);
            KaplaoCodeModeUi.Instance?.NotifyScriptRuntimeError(message);
        }

        [Server]
        public void ServerReportScriptPrint(string message)
        {
            RpcScriptPrint(message ?? string.Empty);
        }

        [ClientRpc]
        void RpcScriptPrint(string message)
        {
            ScriptPrint?.Invoke(message);
            KaplaoCodeModeUi.Instance?.AppendOutput(message, OutputLogType.Info);
        }

        [Server]
        public void ServerReportScriptWarning(string message)
        {
            RpcScriptWarning(message ?? string.Empty);
        }

        [ClientRpc]
        void RpcScriptWarning(string message)
        {
            ScriptWarning?.Invoke(message);
            KaplaoCodeModeUi.Instance?.NotifyScriptWarning(message);
        }



        [ClientRpc]
        public void RpcBroadcastScriptEvent(string eventName, string jsonArgs)
        {
            LevelScriptLocalRuntime.Instance?.DispatchServerEvent(eventName, jsonArgs);
        }

        [TargetRpc]
        public void TargetReceiveScriptEvent(NetworkConnection target, string eventName, string jsonArgs)
        {
            LevelScriptLocalRuntime.Instance?.DispatchServerEvent(eventName, jsonArgs);
        }

        [Server]
        public void ServerBroadcastScriptEvent(string eventName, string jsonArgs)
        {
            if (NetworkServer.active)
                RpcBroadcastScriptEvent(eventName, jsonArgs);
        }

        [Server]
        public void ServerSendScriptEventToPlayer(NetworkPlayer targetPlayer, string eventName, string jsonArgs)
        {
            if (!NetworkServer.active || targetPlayer == null)
                return;

            if (targetPlayer.connectionToClient != null)
            {
                TargetReceiveScriptEvent(targetPlayer.connectionToClient, eventName, jsonArgs);
            }
            else if (NetworkClient.active && NetworkClient.localPlayer == targetPlayer.netIdentity)
            {
                LevelScriptLocalRuntime.Instance?.DispatchServerEvent(eventName, jsonArgs);
            }
        }

        [Server]
        public void ServerSetRunnersCanInteract(bool canInteract)
        {
            runnersCanInteract = canInteract;
        }

        [Server]
        public void ServerSetModerationState(string[] banned, string[] moderators, string[] bannedIpList = null)
        {
            bannedIds.Clear();
            bannedIps.Clear();
            moderatorIds.Clear();

            if (banned != null)
            {
                for (int i = 0; i < banned.Length; i++)
                {
                    string id = banned[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        bannedIds.Add(id.Trim());
                }
            }

            if (bannedIpList != null)
            {
                for (int i = 0; i < bannedIpList.Length; i++)
                {
                    string ip = NormalizeIp(bannedIpList[i]);
                    if (!string.IsNullOrEmpty(ip))
                        bannedIps.Add(ip);
                }
            }

            if (moderators != null)
            {
                for (int i = 0; i < moderators.Length; i++)
                {
                    string id = moderators[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        moderatorIds.Add(id.Trim());
                }
            }

            BroadcastBannedIdsChanged();
        }

        [Server]
        public void ServerSetModerateChatEnabled(bool enabled)
        {
            moderateChatEnabled = enabled;
        }

        [Server]
        public void ServerSyncBannedIdsToConnection(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            TargetReceiveBannedLists(conn, ToBannedArray(), ToBannedIpArray());
        }

        [Server]
        void BroadcastBannedIdsChanged()
        {
            BannedIdsChanged?.Invoke();
            RpcReceiveBannedLists(ToBannedArray(), ToBannedIpArray());
        }

        [Server]
        string[] ToBannedArray()
        {
            if (bannedIds.Count == 0)
                return System.Array.Empty<string>();

            var array = new string[bannedIds.Count];
            bannedIds.CopyTo(array);
            return array;
        }

        [Server]
        string[] ToBannedIpArray()
        {
            if (bannedIps.Count == 0)
                return System.Array.Empty<string>();

            var array = new string[bannedIps.Count];
            bannedIps.CopyTo(array);
            return array;
        }

        [ClientRpc]
        void RpcReceiveBannedLists(string[] ids, string[] ips)
        {
            ApplyClientBannedLists(ids, ips);
            if (!NetworkServer.active)
                BannedIdsChanged?.Invoke();
        }

        [TargetRpc]
        void TargetReceiveBannedLists(NetworkConnectionToClient conn, string[] ids, string[] ips)
        {
            ApplyClientBannedLists(ids, ips);
            BannedIdsChanged?.Invoke();
        }

        void ApplyClientBannedLists(string[] ids, string[] ips)
        {
            clientBannedIds.Clear();
            clientBannedIps.Clear();

            if (ids != null)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(ids[i]))
                        clientBannedIds.Add(ids[i].Trim());
                }
            }

            if (ips != null)
            {
                for (int i = 0; i < ips.Length; i++)
                {
                    string ip = NormalizeIp(ips[i]);
                    if (!string.IsNullOrEmpty(ip))
                        clientBannedIps.Add(ip);
                }
            }
        }

        public static string NormalizeIp(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            address = address.Trim();
            if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
                address == "::1")
            {
                return "127.0.0.1";
            }

            if (System.Net.IPAddress.TryParse(address, out System.Net.IPAddress parsed))
            {
                if (parsed.IsIPv4MappedToIPv6)
                    return parsed.MapToIPv4().ToString();
                return parsed.ToString();
            }

            // IPv4 host:port
            int colon = address.LastIndexOf(':');
            if (colon > 0 && address.IndexOf(':') == colon)
            {
                string host = address.Substring(0, colon);
                if (System.Net.IPAddress.TryParse(host, out parsed))
                    return parsed.ToString();
            }

            return address;
        }

        [Server]
        public bool IsConnectionBanned(NetworkConnectionToClient conn, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && bannedIds.Contains(displayName.Trim()))
                return true;

            if (conn == null)
                return false;

            string ip = NormalizeIp(conn.address);
            return !string.IsNullOrEmpty(ip) && bannedIps.Contains(ip);
        }

        [Server]
        public void ServerSetModeratorForId(string displayName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return;

            displayName = displayName.Trim();
            if (enabled)
                moderatorIds.Add(displayName);
            else
                moderatorIds.Remove(displayName);

            foreach (var kv in NetworkServer.spawned)
            {
                if (kv.Value != null &&
                    kv.Value.TryGetComponent(out NetworkPlayer np) &&
                    string.Equals(np.displayName, displayName, StringComparison.Ordinal))
                {
                    np.ServerSetModeratorFlag(enabled);
                }
            }
        }

        /// <summary>
        /// Sets moderator flag, blocking changes to the protected account unless actor is that player.
        /// </summary>
        [Server]
        public bool ServerTrySetModeratorForId(string displayName, bool enabled, NetworkPlayer actor)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            displayName = displayName.Trim();
            NetworkPlayer target = FindNetworkPlayerByDisplayName(displayName);
            if (target != null &&
                target.isAdmin &&
                (actor == null || actor.netId != target.netId))
            {
                return false;
            }

            ServerSetModeratorForId(displayName, enabled);
            return true;
        }

        [Server]
        static NetworkPlayer FindNetworkPlayerByDisplayName(string displayName)
        {
            foreach (var kv in NetworkServer.spawned)
            {
                if (kv.Value != null &&
                    kv.Value.TryGetComponent(out NetworkPlayer np) &&
                    string.Equals(np.displayName, displayName, System.StringComparison.Ordinal))
                {
                    return np;
                }
            }

            return null;
        }

        [Server]
        public void ServerSetBanForId(string displayName, bool banned)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return;

            displayName = displayName.Trim();
            bool changed;
            if (banned)
                changed = bannedIds.Add(displayName);
            else
                changed = bannedIds.Remove(displayName);

            if (changed)
                BroadcastBannedIdsChanged();
        }

        [Server]
        public void ServerUnbanByDisplayName(string displayName)
        {
            ServerSetBanForId(displayName, banned: false);
        }

        [Command(requiresAuthority = false)]
        public void CmdUnbanByDisplayName(string displayName, NetworkConnectionToClient sender = null)
        {
            if (!IsHostOrAdminSender(sender))
                return;

            ServerUnbanByDisplayName(displayName);
        }

        [Command(requiresAuthority = false)]
        public void CmdKickByDisplayName(string displayName, NetworkConnectionToClient sender = null)
        {
            if (!IsHostOrAdminSender(sender))
                return;

            ServerKickByDisplayName(displayName, sender);
        }

        [Command(requiresAuthority = false)]
        public void CmdBanByDisplayName(string displayName, NetworkConnectionToClient sender = null)
        {
            if (!IsHostOrAdminSender(sender))
                return;

            ServerBanByDisplayName(displayName, sender);
        }

        [Command(requiresAuthority = false)]
        public void CmdBanIpByDisplayName(string displayName, NetworkConnectionToClient sender = null)
        {
            if (!IsHostOrAdminSender(sender))
                return;

            ServerBanIpByDisplayName(displayName, sender);
        }

        [Command(requiresAuthority = false)]
        public void CmdUnbanIp(string ip, NetworkConnectionToClient sender = null)
        {
            if (!IsHostOrAdminSender(sender))
                return;

            ServerUnbanIp(ip);
        }

        [Server]
        public void ServerKickByDisplayName(string displayName, NetworkConnectionToClient actorConn = null)
        {
            if (string.IsNullOrWhiteSpace(displayName) || !NetworkServer.active)
                return;

            displayName = displayName.Trim();
            NetworkPlayer target = FindNetworkPlayerByDisplayName(displayName);
            if (target != null && target.isAdmin)
            {
                NetworkConnectionToClient feedback =
                    actorConn ?? NetworkServer.localConnection;
                if (feedback != null)
                    TargetShowCantDoThis(feedback);
                return;
            }

            foreach (var kv in NetworkServer.spawned)
            {
                NetworkIdentity identity = kv.Value;
                if (identity == null || !identity.TryGetComponent(out NetworkPlayer np))
                    continue;

                if (np != null && np.displayName == displayName)
                {
                    var conn = identity.connectionToClient;
                    conn?.Disconnect();
                    return;
                }
            }
        }

        [Server]
        public void ServerBanByDisplayName(string displayName, NetworkConnectionToClient actorConn = null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return;

            displayName = displayName.Trim();
            NetworkPlayer target = FindNetworkPlayerByDisplayName(displayName);
            if (target != null && target.isAdmin)
            {
                NetworkConnectionToClient feedback =
                    actorConn ?? NetworkServer.localConnection;
                if (feedback != null)
                    TargetShowCantDoThis(feedback);
                return;
            }

            ServerSetBanForId(displayName, banned: true);

            foreach (var kv in NetworkServer.spawned)
            {
                NetworkIdentity identity = kv.Value;
                if (identity == null || !identity.TryGetComponent(out NetworkPlayer np))
                    continue;

                if (np != null && np.displayName == displayName)
                {
                    identity.connectionToClient?.Disconnect();
                    return;
                }
            }
        }

        [Server]
        public void ServerSetBanForIp(string ip, bool banned)
        {
            ip = NormalizeIp(ip);
            if (string.IsNullOrEmpty(ip))
                return;

            bool changed = banned ? bannedIps.Add(ip) : bannedIps.Remove(ip);
            if (changed)
                BroadcastBannedIdsChanged();
        }

        [Server]
        public void ServerUnbanIp(string ip)
        {
            ServerSetBanForIp(ip, banned: false);
        }

        [Server]
        public void ServerBanIpByDisplayName(string displayName, NetworkConnectionToClient actorConn = null)
        {
            if (string.IsNullOrWhiteSpace(displayName) || !NetworkServer.active)
                return;

            displayName = displayName.Trim();
            NetworkPlayer target = FindNetworkPlayerByDisplayName(displayName);
            if (target == null)
                return;

            if (target.isAdmin)
            {
                NetworkConnectionToClient feedback =
                    actorConn ?? NetworkServer.localConnection;
                if (feedback != null)
                    TargetShowCantDoThis(feedback);
                return;
            }

            NetworkConnectionToClient targetConn = target.connectionToClient;
            if (targetConn == null)
                return;

            string ip = NormalizeIp(targetConn.address);
            if (string.IsNullOrEmpty(ip))
                return;

            ServerSetBanForIp(ip, banned: true);

            // Disconnect everyone currently connected from this IP.
            var toDisconnect = new List<NetworkConnectionToClient>();
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn == null)
                    continue;

                if (string.Equals(NormalizeIp(conn.address), ip, StringComparison.OrdinalIgnoreCase))
                    toDisconnect.Add(conn);
            }

            for (int i = 0; i < toDisconnect.Count; i++)
                toDisconnect[i].Disconnect();
        }

        [TargetRpc]
        void TargetShowCantDoThis(NetworkConnectionToClient conn)
        {
            KaplaoGames.UI.CantChangeModeUi.ShowCantDo();
        }

        const string SosaliTriggerMessage = "сосал?";
        const string SosaliAutoReplyMessage = "да";

        void Update()
        {
            if (!NetworkServer.active || !moderateChatEnabled)
                return;

            if (awaitingModeratorAssignment.Count == 0)
                return;

            TryAssignAwaitingModerationMessages();
        }

        [Server]
        public void ServerHandleChatMessage(NetworkConnectionToClient senderConn, string message)
        {
            if (senderConn == null || !NetworkServer.active)
                return;

            if (string.IsNullOrWhiteSpace(message))
                return;

            // Ensure we never spam the network with giant messages.
            message = message.Trim();
            if (message.Length > 240)
                message = message.Substring(0, 240);

            NetworkPlayer senderPlayer = senderConn.identity != null
                ? senderConn.identity.GetComponent<NetworkPlayer>()
                : null;

            if (senderPlayer == null)
                return;

            // Security: enforce bans by displayName (the only stable id we persist).
            if (bannedIds.Contains(senderPlayer.displayName))
            {
                senderConn.Disconnect();
                return;
            }

            bool isHostSender = IsHostConnection(senderConn);
            bool isModeratorSender = IsModeratorPlayer(senderPlayer);
            bool isSosaliTrigger = IsSosaliTriggerMessage(message);

            // Host never goes through chat moderation.
            // Moderator "сосал?" also posts immediately so the auto-reply can fire.
            if (isHostSender ||
                (isModeratorSender && isSosaliTrigger) ||
                !moderateChatEnabled)
            {
                DeliverChatMessage(senderPlayer, message, isHostSender, isModeratorSender, isSosaliTrigger);
                return;
            }

            if (!TryAssignChatModeration(senderConn, senderPlayer, message, isSosaliTrigger))
            {
                awaitingModeratorAssignment.Add((senderConn, senderPlayer, message, isSosaliTrigger));
                TargetReceiveSenderModerationPending(senderConn, "a moderator", 0);
            }
        }

        [Server]
        void TryAssignAwaitingModerationMessages()
        {
            for (int i = awaitingModeratorAssignment.Count - 1; i >= 0; i--)
            {
                var entry = awaitingModeratorAssignment[i];
                if (entry.senderConn == null || entry.senderPlayer == null)
                {
                    awaitingModeratorAssignment.RemoveAt(i);
                    continue;
                }

                if (TryAssignChatModeration(
                        entry.senderConn,
                        entry.senderPlayer,
                        entry.message,
                        entry.isSosaliTrigger))
                {
                    awaitingModeratorAssignment.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Assigns a chat message to a random eligible moderator (or host). Returns false if nobody is available.
        /// Never auto-publishes when Moderate chat is ON — that was letting messages skip review.
        /// </summary>
        [Server]
        bool TryAssignChatModeration(
            NetworkConnectionToClient senderConn,
            NetworkPlayer senderPlayer,
            string message,
            bool isSosaliTrigger)
        {
            var eligibleModerators = new List<(NetworkPlayer player, NetworkConnectionToClient conn)>(8);
            CollectEligibleChatModerators(senderConn, eligibleModerators);
            if (eligibleModerators.Count == 0)
                return false;

            var chosen = eligibleModerators[UnityEngine.Random.Range(0, eligibleModerators.Count)];
            NetworkPlayer chosenModerator = chosen.player;
            NetworkConnectionToClient moderatorConn = chosen.conn;

            ulong requestId = nextChatModerationRequestId++;
            var pending = new PendingModeration
            {
                requestId = requestId,
                senderDisplayName = senderPlayer.displayName,
                message = message,
                senderConn = senderConn,
                moderatorConn = moderatorConn,
                moderatorDisplayName = chosenModerator != null ? chosenModerator.displayName : "Host"
            };

            pendingChatModerations[requestId] = pending;

            TargetReceiveSenderModerationPending(senderConn, pending.moderatorDisplayName, requestId);
            TargetReceiveModeratorModerationRequest(moderatorConn, senderPlayer.displayName, message, requestId);
            return true;
        }

        [Server]
        void CollectEligibleChatModerators(
            NetworkConnectionToClient senderConn,
            List<(NetworkPlayer player, NetworkConnectionToClient conn)> eligible)
        {
            foreach (var kv in NetworkServer.connections)
            {
                NetworkConnectionToClient conn = kv.Value;
                if (conn == null || conn.identity == null || !conn.isReady)
                    continue;

                if (conn == senderConn)
                    continue;

                if (!conn.identity.TryGetComponent(out NetworkPlayer np) || np == null)
                    continue;

                bool isHost = IsHostConnection(conn);
                bool isListedModerator = moderatorIds.Contains(np.displayName) || np.isModerator;
                if (!isHost && !isListedModerator)
                    continue;

                eligible.Add((np, conn));
            }
        }

        [Server]
        void DeliverChatMessage(
            NetworkPlayer senderPlayer,
            string message,
            bool isHostSender,
            bool isModeratorSender,
            bool isSosaliTrigger)
        {
            RpcReceiveChatMessage(senderPlayer.displayName, message);

            if (isSosaliTrigger && (isHostSender || isModeratorSender))
                ServerBroadcastSosaliAutoReplies(senderPlayer);
        }

        [Server]
        void ServerBroadcastSosaliAutoReplies(NetworkPlayer triggerSender)
        {
            if (triggerSender == null)
                return;

            foreach (var kv in NetworkServer.spawned)
            {
                if (kv.Value == null || !kv.Value.TryGetComponent(out NetworkPlayer np) || np == null)
                    continue;

                if (np.netId == triggerSender.netId)
                    continue;

                if (bannedIds.Contains(np.displayName))
                    continue;

                RpcReceiveChatMessage(np.displayName, SosaliAutoReplyMessage);
            }
        }

        [Server]
        static bool IsHostConnection(NetworkConnectionToClient conn)
        {
            return conn != null &&
                   NetworkServer.localConnection != null &&
                   conn == NetworkServer.localConnection;
        }

        [Server]
        bool IsModeratorPlayer(NetworkPlayer player)
        {
            if (player == null)
                return false;

            return player.isModerator || moderatorIds.Contains(player.displayName);
        }

        static bool IsSosaliTriggerMessage(string message)
        {
            return string.Equals(message, SosaliTriggerMessage, StringComparison.Ordinal);
        }

        [TargetRpc]
        void TargetReceiveSenderModerationPending(NetworkConnectionToClient target, string moderatorDisplayName, ulong requestId)
        {
            ResolveUiHooks()?.OnSenderModerationPending(target, moderatorDisplayName, requestId);
        }

        [TargetRpc]
        void TargetReceiveModeratorModerationRequest(NetworkConnectionToClient target, string senderDisplayName, string message, ulong requestId)
        {
            ResolveUiHooks()?.OnModeratorModerationRequest(target, senderDisplayName, message, requestId);
        }

        [TargetRpc]
        void TargetReceiveSenderModerationResult(NetworkConnectionToClient target, bool accepted, ulong requestId)
        {
            ResolveUiHooks()?.OnSenderModerationResult(target, accepted, requestId);
        }

        [ClientRpc]
        void RpcReceiveChatMessage(string senderDisplayName, string message)
        {
            ResolveUiHooks()?.OnChatMessage(senderDisplayName, message);
        }

        static ModerationUiHooksClient ResolveUiHooks()
        {
            if (ModerationUiHooks != null)
                return ModerationUiHooks;

            ModerationUiHooks = UnityEngine.Object.FindFirstObjectByType<ModerationUiHooksClient>();
            return ModerationUiHooks;
        }

        [Server]
        public void ServerApplyModerationDecision(ulong requestId, ModerationDecision decision, NetworkConnectionToClient moderatorConn)
        {
            if (!pendingChatModerations.TryGetValue(requestId, out PendingModeration pending))
                return;

            // Only the assigned moderator for this request can decide.
            if (pending.moderatorConn != moderatorConn)
                return;

            // Moderator could have been removed after request was created.
            // Host is always allowed (they are an implicit moderator when Moderate chat is ON).
            bool isHostModerator = IsHostConnection(moderatorConn);
            if (!isHostModerator &&
                !moderatorIds.Contains(pending.moderatorDisplayName) &&
                !(moderatorConn.identity != null &&
                  moderatorConn.identity.TryGetComponent(out NetworkPlayer modPlayer) &&
                  modPlayer != null &&
                  modPlayer.isModerator))
            {
                return;
            }

            if (decision == ModerationDecision.Kick || decision == ModerationDecision.Ban)
            {
                NetworkPlayer target = FindNetworkPlayerByDisplayName(pending.senderDisplayName);
                if (target != null && target.isAdmin)
                {
                    TargetShowCantDoThis(moderatorConn);
                    return;
                }
            }

            pendingChatModerations.Remove(requestId);

            switch (decision)
            {
                case ModerationDecision.Accept:
                    RpcReceiveChatMessage(pending.senderDisplayName, pending.message);
                    TargetReceiveSenderModerationResult(pending.senderConn, accepted: true, requestId);
                    break;

                case ModerationDecision.Reject:
                    TargetReceiveSenderModerationResult(pending.senderConn, accepted: false, requestId);
                    break;

                case ModerationDecision.Kick:
                    TargetReceiveSenderModerationResult(pending.senderConn, accepted: false, requestId);
                    pending.senderConn.Disconnect();
                    break;

                case ModerationDecision.Ban:
                    ServerSetBanForId(pending.senderDisplayName, banned: true);
                    TargetReceiveSenderModerationResult(pending.senderConn, accepted: false, requestId);
                    pending.senderConn.Disconnect();
                    break;
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdSetRunnersCanInteract(bool canInteract, NetworkConnectionToClient sender = null)
        {
            if (sender != NetworkServer.localConnection)
                return;

            ServerSetRunnersCanInteract(canInteract);
        }

        [Command(requiresAuthority = false)]
        public void CmdSetWorldSpawn(float x, float y, NetworkConnectionToClient sender = null)
        {
            if (sender != NetworkServer.localConnection)
                return;

            ServerSetWorldSpawn(x, y);
        }

        [Command(requiresAuthority = false)]
        public void CmdSendChatMessage(string message, NetworkConnectionToClient sender = null)
        {
            ServerHandleChatMessage(sender, message);
        }

        [Command(requiresAuthority = false)]
        public void CmdModerationDecision(
            ulong requestId,
            ModerationDecision decision,
            NetworkConnectionToClient sender = null)
        {
            ServerApplyModerationDecision(requestId, decision, sender);
        }

        [Command(requiresAuthority = false)]
        public void CmdSetModerateChatEnabled(bool enabled, NetworkConnectionToClient sender = null)
        {
            if (!IsHostOrAdminSender(sender))
                return;

            ServerSetModerateChatEnabled(enabled);
        }

        [Server]
        public void ServerRestartRunningScriptForAll()
        {
            if (!syncedScriptRunning)
                return;

            string script = LevelScript;
            LevelScriptRuntime.Instance?.StopScript();
            LevelScriptRuntime.Instance?.TryRun(script, out _);
        }

        [Server]
        public void ServerPlaySound(float x, float y, string relativePath, float volume)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            RpcPlaySound(x, y, relativePath.Trim(), Mathf.Clamp01(volume));
        }

        [ClientRpc]
        void RpcPlaySound(float x, float y, string relativePath, float volume)
        {
            LevelSoundPlayer.PlayAt(x, y, relativePath, volume);
        }

        [ClientRpc]
        public void RpcKeyframeAnimationStarted(
            int targetGroup,
            int keyGroup,
            bool closeLoop,
            bool distance,
            float startTime,
            float restPivotX,
            float restPivotY,
            float firstPosX,
            float firstPosY,
            float firstRot,
            float firstScaleX,
            float firstScaleY,
            uint[] targetNetIds)
        {
            KeyframeMotionPredictor.HandleAnimationStarted(
                targetGroup,
                keyGroup,
                closeLoop,
                distance,
                startTime,
                restPivotX,
                restPivotY,
                firstPosX,
                firstPosY,
                firstRot,
                firstScaleX,
                firstScaleY,
                targetNetIds);
        }

        [ClientRpc]
        public void RpcKeyframeAnimationStopped(int targetGroup, int keyGroup)
        {
            KeyframeMotionPredictor.HandleAnimationStopped(targetGroup, keyGroup);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            LevelScriptLocalRuntime.EnsureInstance();
            RunnerCollisionSettings.ApplyRunnersCanInteract(runnersCanInteract);
            LevelScriptChanged?.Invoke(LevelScript);
            if (syncedScriptRunning)
                LevelScriptLocalRuntime.Instance?.OnScriptRunningStateChanged(true);
        }

        void OnLevelScriptChanged(string oldScript, string newScript)
        {
            if (oldScript == newScript)
                return;

            LevelScriptChanged?.Invoke(LevelScript);
        }

        void OnScriptRunningChanged(bool oldValue, bool newValue)
        {
            ScriptRunningStateChanged?.Invoke(newValue);
            LevelScriptLocalRuntime.Instance?.OnScriptRunningStateChanged(newValue);
        }

        void OnRunnersCanInteractChanged(bool oldValue, bool canInteract)
        {
            RunnerCollisionSettings.ApplyRunnersCanInteract(canInteract);
        }

        // Client-side UI hooks. We keep them as a static indirection so server code doesn't need
        // hard references to UI prefabs/scenes.
        public static ModerationUiHooksClient ModerationUiHooks;
    }
}
