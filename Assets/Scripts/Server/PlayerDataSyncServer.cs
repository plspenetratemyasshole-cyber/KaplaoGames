using System;
using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Periodically refreshes player data (nickname preset, admin rights, etc.) from the verification service
    /// while they are actively playing. This ensures real-time updates from the admin panel without requiring
    /// the player to rejoin the level.
    /// 
    /// When the admin changes a player's nickname preset or other settings in the admin panel,
    /// those changes are immediately synced to any currently connected player with that email address.
    /// </summary>
    [AddComponentMenu("Kaplao/Server/Player Data Sync Server")]
    public class PlayerDataSyncServer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Interval in seconds between player data refreshes from the server")]
        float refreshIntervalSeconds = 30f;

        [SerializeField]
        [Tooltip("Whether to log debug info about synced players")]
        bool debugLogging = true;

        float nextRefreshTime;

        void OnEnable()
        {
            nextRefreshTime = Time.time + refreshIntervalSeconds;
        }

        void Update()
        {
            if (!NetworkServer.active)
                return;

            if (Time.time < nextRefreshTime)
                return;

            nextRefreshTime = Time.time + refreshIntervalSeconds;
            StartCoroutine(RefreshAllPlayersData());
        }

        IEnumerator RefreshAllPlayersData()
        {
            // Collect all active players with auth tokens
            var playersToRefresh = new List<(NetworkPlayer player, string email, string authToken)>();

            foreach (var kv in NetworkServer.spawned)
            {
                if (kv.Value == null || !kv.Value.TryGetComponent(out NetworkPlayer player))
                    continue;

                string email = player.accountEmail;
                string authToken = player.serverAuthToken;

                // Only refresh players with email and auth token
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(authToken))
                    continue;

                playersToRefresh.Add((player, email, authToken));
            }

            if (playersToRefresh.Count == 0)
                yield break;

            // Refresh each player's data from the verification service
            foreach (var (player, email, authToken) in playersToRefresh)
            {
                if (player == null)
                    continue;

                yield return RefreshPlayerDataCoroutine(player, authToken);
            }

            // Bump the style-version counter on every active player.
            // This signals all clients to force-refresh the preset catalog, ensuring that
            // admin changes to a preset's style (color, font, effects) are picked up even
            // when the player's preset *name* did not change.
            foreach (var (player, _, _) in playersToRefresh)
            {
                if (player != null)
                    player.nicknamePresetStyleVersion++;
            }

            if (debugLogging)
                Debug.Log($"[PlayerDataSyncServer] Bumped nicknamePresetStyleVersion on {playersToRefresh.Count} player(s) to trigger catalog refresh.");
        }

        IEnumerator RefreshPlayerDataCoroutine(NetworkPlayer player, string authToken)
        {
            if (player == null || string.IsNullOrWhiteSpace(authToken))
                yield break;

            var verifyResult = new KaplaoAccountVerificationService.VerifyResult();
            bool verifyDone = false;

            yield return KaplaoAccountVerificationService.VerifyPlayerCoroutine(
                authToken,
                result =>
                {
                    verifyResult = result;
                    verifyDone = true;
                });

            if (!verifyDone || verifyResult == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[PlayerDataSyncServer] {player?.displayName}: verify coroutine failed or returned null");
                yield break;
            }

            if (!verifyResult.Valid)
            {
                if (debugLogging)
                    Debug.LogWarning($"[PlayerDataSyncServer] {player.displayName}: verification failed (valid=false)");
                yield break;
            }

            if (debugLogging)
                Debug.Log($"[PlayerDataSyncServer] {player.displayName}: verify-player response received:");
            if (debugLogging)
                Debug.Log($"  Email: {verifyResult.Email}, Preset: '{verifyResult.NicknamePreset}' (null={verifyResult.NicknamePreset == null})");

            // Apply updates to the player's synced data
            ApplyPlayerDataUpdates(player, verifyResult);
        }

        [Server]
        void ApplyPlayerDataUpdates(NetworkPlayer player, KaplaoAccountVerificationService.VerifyResult newData)
        {
            if (player == null || newData == null)
                return;

            bool changed = false;

            if (debugLogging)
                Debug.Log($"[PlayerDataSyncServer] {player.displayName}: checking for updates...");
            if (debugLogging)
                Debug.Log($"  Incoming nicknamePreset: '{newData.NicknamePreset}' (null={newData.NicknamePreset == null}, whitespace={string.IsNullOrWhiteSpace(newData.NicknamePreset)})");
            if (debugLogging)
                Debug.Log($"  Current nicknamePreset: '{player.nicknamePreset}'");

            // Update nickname preset
            // Only update if we have valid data from the verification service
            if (!string.IsNullOrWhiteSpace(newData.NicknamePreset) && 
                !string.Equals(newData.NicknamePreset, "own", StringComparison.OrdinalIgnoreCase))
            {
                string newNicknamePreset = newData.NicknamePreset.Trim().ToLowerInvariant();
                
                if (debugLogging)
                    Debug.Log($"  Normalized preset: '{newNicknamePreset}'");
                
                if (player.nicknamePreset != newNicknamePreset)
                {
                    if (debugLogging)
                        Debug.Log(
                            $"[PlayerDataSyncServer] {player.displayName}: nickname preset updated from '{player.nicknamePreset}' to '{newNicknamePreset}'");
                    player.nicknamePreset = newNicknamePreset;
                    changed = true;
                }
                else if (debugLogging)
                    Debug.Log($"  → No change (already '{newNicknamePreset}')");
            }
            else if (debugLogging)
                Debug.Log($"  → Skipping: preset is null/empty/own or already 'none'");

            // Update admin status
            bool newIsAdmin = newData.IsAdmin || newData.Permission == "admin" || newData.Permission == "game_host";
            if (player.isAdmin != newIsAdmin)
            {
                if (debugLogging)
                    Debug.Log(
                        $"[PlayerDataSyncServer] {player.displayName}: admin status changed from {player.isAdmin} to {newIsAdmin}");
                player.isAdmin = newIsAdmin;
                changed = true;
            }

            // Update permission
            string newPermission = string.IsNullOrWhiteSpace(newData.Permission)
                ? "none"
                : newData.Permission.Trim().ToLowerInvariant();
            if (player.playerPermission != newPermission)
            {
                if (debugLogging)
                    Debug.Log(
                        $"[PlayerDataSyncServer] {player.displayName}: permission updated from '{player.playerPermission}' to '{newPermission}'");
                player.playerPermission = newPermission;
                changed = true;
            }

            // Update owned skins
            if (newData.OwnedSkinIds != null && newData.OwnedSkinIds.Length != player.serverOwnedSkinIds.Length)
            {
                if (debugLogging)
                    Debug.Log(
                        $"[PlayerDataSyncServer] {player.displayName}: owned skins updated to {string.Join(",", newData.OwnedSkinIds)}");
                player.serverOwnedSkinIds = newData.OwnedSkinIds;
                changed = true;
            }

            if (changed && debugLogging)
                Debug.Log($"[PlayerDataSyncServer] {player.displayName} data synced successfully");
            else if (!changed && debugLogging)
                Debug.Log($"[PlayerDataSyncServer] {player.displayName}: no changes detected");
        }

        public static PlayerDataSyncServer Instance
        {
            get
            {
                if (_instance == null && !_searched)
                {
                    _instance = FindObjectOfType<PlayerDataSyncServer>();
                    _searched = true;
                }
                return _instance;
            }
        }

        static PlayerDataSyncServer _instance;
        static bool _searched;
    }
}
