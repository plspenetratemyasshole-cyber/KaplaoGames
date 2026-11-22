using System;
using KaplaoGames.Client;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Network
{
    /// <summary>
    /// Shared networked player data. Role and display name are synced from server to all clients.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayer : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnDisplayNameChanged))]
        public string displayName;

        // Keep original SyncVar order: displayName, role. New fields must be appended
        // or Mirror can desync role across clients and swap Kaplao/Runner UI.
        [SyncVar(hook = nameof(OnRoleChanged))]
        public PlayerRole role;

        [SyncVar(hook = nameof(OnAccountEmailChanged))]
        public string accountEmail;

        [SyncVar]
        public bool isModerator;

        [SyncVar]
        public bool isAdmin;

        [SyncVar]
        public string playerPermission = "none";

        [SyncVar(hook = nameof(OnNicknamePresetChanged))]
        public string nicknamePreset = "none";

        /// <summary>
        /// Incremented by the server whenever the preset catalog should be force-refreshed on clients
        /// (e.g. admin changed the style of an existing preset without changing its name).
        /// </summary>
        [SyncVar(hook = nameof(OnNicknamePresetStyleVersionChanged))]
        public int nicknamePresetStyleVersion;

        [SyncVar(hook = nameof(OnIsVerifyingAccountChanged))]
        public bool isVerifyingAccount;

        /// <summary>Server-only preferred runner skin from join (-1 = random).</summary>
        public int preferredSkinIndex = -1;

        /// <summary>Server-only admin-granted skin ids from verify-player.</summary>
        public string[] serverOwnedSkinIds = System.Array.Empty<string>();

        /// <summary>True when verify-player succeeded online for this join.</summary>
        public bool serverSkinOwnershipVerified;

        /// <summary>Server-only auth token for verifying player privileges in real-time.</summary>
        public string serverAuthToken = string.Empty;

        public event Action<string> DisplayNameChanged;
        public event Action<PlayerRole> RoleChanged;
        public event Action<string> AccountEmailChanged;
        public event Action<string> NicknamePresetChanged;
        /// <summary>Fired when the server bumps the preset style version, signaling clients to re-fetch the catalog.</summary>
        public event Action NicknamePresetStyleVersionChanged;
        public event Action<bool> IsVerifyingAccountChanged;

        [Server]
        public void ServerInit(
            string name,
            PlayerRole newRole,
            string email = null,
            bool admin = false,
            string permission = "none",
            string nickPreset = "none",
            string authToken = null)
        {
            displayName = PlayerSession.SanitizeNickname(name);
            if (email != null)
                accountEmail = email.Trim();
            playerPermission = string.IsNullOrWhiteSpace(permission) ? "none" : permission.Trim().ToLowerInvariant();
            nicknamePreset = string.IsNullOrWhiteSpace(nickPreset) ? "none" : nickPreset.Trim().ToLowerInvariant();
            if (nicknamePreset == "own")
                nicknamePreset = "none";
            isAdmin = admin || playerPermission == "admin" || playerPermission == "game_host";
            serverAuthToken = authToken ?? string.Empty;
            // Set role last so UI hooks see email already applied.
            role = newRole;
        }

        [Server]
        public void ServerSetModeratorFlag(bool enabled)
        {
            isModerator = enabled;
        }

        public static bool IsLocalAdmin()
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return false;

            return NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer np) && np.isAdmin;
        }

        [Command]
        public void CmdSendChatMessage(string message)
        {
            LevelSessionServer.Instance?.ServerHandleChatMessage(connectionToClient, message);
        }

        [Command]
        public void CmdSendScriptEventToServer(string eventName, string jsonArgs)
        {
            LevelScript.LevelScriptRuntime.Instance?.DispatchClientEvent(
                eventName,
                new LevelScript.ScriptGameObjectRef(LevelScript.ScriptObjectKind.Player, netId),
                jsonArgs);
        }

        [Command]
        public void CmdModerationDecision(LevelSessionServer.ModerationDecision decision, ulong requestId)
        {
            LevelSessionServer.Instance?.ServerApplyModerationDecision(requestId, decision, connectionToClient);
        }

        /// <summary>
        /// World host or protected admin may toggle own Kaplao/Runner role.
        /// </summary>
        [Server]
        public static bool CanToggleOwnRole(NetworkConnectionToClient conn, NetworkPlayer player)
        {
            if (conn == null || player == null)
                return false;

            if (conn == NetworkServer.localConnection)
                return true;

            return player.isAdmin;
        }

        /// <summary>
        /// Protected account may toggle own Kaplao/Runner role from a non-host client.
        /// </summary>
        [Command]
        public void CmdRequestSelfRoleToggle(int preferredSkinIndex)
        {
            if (!CanToggleOwnRole(connectionToClient, this))
                return;

            KaplaoNetworkManager manager = KaplaoNetworkManager.Instance;
            if (manager == null)
                return;

            this.preferredSkinIndex = preferredSkinIndex < 0 ? -1 : preferredSkinIndex;

            if (role == PlayerRole.Kaplao)
            {
                // Don't pass preferredSkinOverride - let TryDemoteKaplaoToRunner use the saved value.
                manager.TryDemoteKaplaoToRunner(
                    netId,
                    actorNetId: netId,
                    preferredSkinOverride: null);
            }
            else
            {
                manager.TryPromoteRunnerToKaplao(netId, actorNetId: netId);
            }
        }

        /// <summary>
        /// Enter toggles role. Shift+Enter as Kaplao spawns the runner at the current Kaplao position.
        /// Kaplao-&gt;Runner restarts the level script if it was running.
        /// </summary>
        [Command]
        public void CmdRequestEnterRoleToggle(bool spawnAtCurrentKaplaoPosition)
        {
            if (!CanToggleOwnRole(connectionToClient, this))
                return;

            KaplaoNetworkManager manager = KaplaoNetworkManager.Instance;
            if (manager == null)
                return;

            if (role == PlayerRole.Kaplao)
            {
                LevelSessionServer session = LevelSessionServer.Instance;
                bool wasScriptRunning = session != null && session.ScriptRunning;
                if (wasScriptRunning)
                    session.ServerStopScript();

                KaplaoDemotionSpawnMode spawnMode = spawnAtCurrentKaplaoPosition
                    ? KaplaoDemotionSpawnMode.CurrentKaplaoPosition
                    : KaplaoDemotionSpawnMode.LastSpawnOrWorld;

                bool demoted = manager.TryDemoteKaplaoToRunner(
                    netId,
                    actorNetId: netId,
                    preferredSkinOverride: null,
                    spawnMode: spawnMode);

                if (demoted && wasScriptRunning && session != null)
                    session.ServerTryStartScript(session.LevelScript, out _);
            }
            else
            {
                manager.TryPromoteRunnerToKaplao(netId, actorNetId: netId);
            }
        }

        /// <summary>
        /// Protected account may toggle own moderator flag from a non-host client.
        /// </summary>
        [Command]
        public void CmdRequestSelfModeratorToggle()
        {
            if (!isAdmin || LevelSessionServer.Instance == null)
                return;

            LevelSessionServer.Instance.ServerTrySetModeratorForId(displayName, !isModerator, actor: this);
        }

        /// <summary>
        /// Protected developer account may toggle Kaplao for any non-protected player (and self).
        /// </summary>
        [Command]
        public void CmdRequestSetPlayerKaplao(uint targetNetId)
        {
            if (!isAdmin)
                return;

            KaplaoNetworkManager manager = KaplaoNetworkManager.Instance;
            if (manager == null)
                return;

            if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity identity) ||
                identity == null ||
                !identity.TryGetComponent(out NetworkPlayer target))
            {
                return;
            }

            if (target.isAdmin && target.netId != netId)
                return;

            if (target.role == PlayerRole.Kaplao)
                manager.TryDemoteKaplaoToRunner(targetNetId, actorNetId: netId);
            else
                manager.TryPromoteRunnerToKaplao(targetNetId, actorNetId: netId);
        }

        /// <summary>
        /// Protected developer account may set moderator for any non-protected player (and self).
        /// </summary>
        [Command]
        public void CmdRequestSetPlayerModerator(string targetDisplayName, bool enabled)
        {
            if (!isAdmin || LevelSessionServer.Instance == null)
                return;

            if (!LevelSessionServer.Instance.ServerTrySetModeratorForId(targetDisplayName, enabled, actor: this))
                return;
        }

        public override void OnStartClient()
        {
            EnsureVisible();
            if (GetComponent<KaplaoGames.Client.CreateModePlayerVisibility>() == null)
                gameObject.AddComponent<KaplaoGames.Client.CreateModePlayerVisibility>();
            if (GetComponent<KaplaoGames.Client.CreateModePlayerHitboxVisual>() == null)
                gameObject.AddComponent<KaplaoGames.Client.CreateModePlayerHitboxVisual>();
            DisplayNameChanged?.Invoke(displayName);
            RoleChanged?.Invoke(role);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (GetComponent<PlayerRoleToggleInputClient>() == null)
                gameObject.AddComponent<PlayerRoleToggleInputClient>();
        }

        void OnDisplayNameChanged(string oldName, string newName)
        {
            UpdateObjectName();
            DisplayNameChanged?.Invoke(newName);
        }

        void OnAccountEmailChanged(string oldEmail, string newEmail)
        {
            AccountEmailChanged?.Invoke(newEmail);
        }

        void OnNicknamePresetChanged(string oldPreset, string newPreset)
        {
            NicknamePresetChanged?.Invoke(newPreset);
        }

        void OnNicknamePresetStyleVersionChanged(int oldVersion, int newVersion)
        {
            NicknamePresetStyleVersionChanged?.Invoke();
        }

        void OnIsVerifyingAccountChanged(bool oldVal, bool newVal)
        {
            IsVerifyingAccountChanged?.Invoke(newVal);
        }

        [Server]
        public void ServerApplyVerifiedAccountData(
            string verifiedEmail,
            bool verifiedAdmin,
            string verifiedPermission,
            string verifiedNicknamePreset,
            string[] ownedSkinIds,
            bool ownershipVerified)
        {
            if (verifiedEmail != null)
                accountEmail = verifiedEmail.Trim();

            playerPermission = string.IsNullOrWhiteSpace(verifiedPermission) ? "none" : verifiedPermission.Trim().ToLowerInvariant();
            isAdmin = verifiedAdmin || playerPermission == "admin" || playerPermission == "game_host";

            string normalizedPreset = string.IsNullOrWhiteSpace(verifiedNicknamePreset) || verifiedNicknamePreset.Equals("own", StringComparison.OrdinalIgnoreCase)
                ? "none"
                : verifiedNicknamePreset.Trim().ToLowerInvariant();
            nicknamePreset = normalizedPreset;

            serverOwnedSkinIds = ownedSkinIds ?? System.Array.Empty<string>();
            serverSkinOwnershipVerified = ownershipVerified;
            isVerifyingAccount = false;

            if (role == PlayerRole.Runner && TryGetComponent(out RunnerAppearanceClient appearance))
            {
                appearance.ServerAssignCharacter(preferredSkinIndex, serverOwnedSkinIds, serverSkinOwnershipVerified);
            }

            if (isLocalPlayer && !string.IsNullOrWhiteSpace(normalizedPreset))
            {
                PlayerSession.SetCachedNicknamePreset(normalizedPreset);
            }
        }

        [TargetRpc]
        public void TargetCacheVerifiedNicknamePreset(NetworkConnectionToClient target, string verifiedPreset)
        {
            if (!string.IsNullOrWhiteSpace(verifiedPreset))
            {
                PlayerSession.SetCachedNicknamePreset(verifiedPreset);
            }
        }

        void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
        {
            UpdateObjectName();
            RoleChanged?.Invoke(newRole);
            EnsureVisible();
        }

        void UpdateObjectName()
        {
            string roleLabel = role == PlayerRole.Kaplao ? "Kaplao" : "Runner";
            gameObject.name = $"{roleLabel}:{displayName} [netId={netId}]";
        }

        void EnsureVisible()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;

            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
                collider.enabled = true;
        }
    }
}
