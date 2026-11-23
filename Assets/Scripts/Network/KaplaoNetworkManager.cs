using System.Collections;
using KaplaoGames.Client;
using KaplaoGames.LevelScript;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Network
{
    /// <summary>
    /// Server-side player connection logic.
    /// The first connected player becomes Kaplao, everyone else is a Runner.
    /// </summary>
    [AddComponentMenu("Kaplao/Network/Kaplao Network Manager")]
    public class KaplaoNetworkManager : NetworkManager
    {
        const string MainMenuSceneName = "MainMenu";
        const string GameSceneName = "SampleScene";

        [Header("Kaplao Game")]
        [Tooltip("Kaplao (overseer) prefab.")]
        public GameObject kaplaoPrefab;

        [Tooltip("Runner prefab.")]
        public GameObject runnerPrefab;

        [Header("Level")]
        [Tooltip("Grid block tile prefab.")]
        public GameObject blockTilePrefab;

        [Tooltip("Grid spike tile prefab.")]
        public GameObject spikeTilePrefab;

        [Tooltip("Grid trigger tile prefab.")]
        public GameObject triggerTilePrefab;

        [Tooltip("Grid particle emitter tile prefab.")]
        public GameObject particleTilePrefab;

        [Tooltip("Kaplao gun bullet prefab (KaplaoBulletServer + NetworkIdentity).")]
        public GameObject kaplaoBulletPrefab;

        [Header("Discovery")]
        public KaplaoNetworkDiscovery networkDiscovery;

        bool kaplaoAssigned;
        int kaplaoCount;
        bool lanAdvertised;
        bool isSinglePlayerHostSession;

        public bool IsSinglePlayerHostSession => isSinglePlayerHostSession;

        public bool IsLanAdvertised => lanAdvertised;

        public static KaplaoNetworkManager Instance => (KaplaoNetworkManager)singleton;

        public override void OnValidate()
        {
            base.OnValidate();

            if (playerPrefab == null && runnerPrefab != null)
                playerPrefab = runnerPrefab;

            RegisterSpawnPrefab(kaplaoPrefab);
            RegisterSpawnPrefab(runnerPrefab);
            RegisterSpawnPrefab(blockTilePrefab);
            RegisterSpawnPrefab(spikeTilePrefab);
            RegisterSpawnPrefab(triggerTilePrefab);
            RegisterSpawnPrefab(particleTilePrefab);
            RegisterSpawnPrefab(kaplaoBulletPrefab);
        }

        public override void Awake()
        {
            if (playerPrefab == null && runnerPrefab != null)
                playerPrefab = runnerPrefab;

            RegisterSpawnPrefab(kaplaoPrefab);
            RegisterSpawnPrefab(runnerPrefab);
            RegisterSpawnPrefab(blockTilePrefab);
            RegisterSpawnPrefab(spikeTilePrefab);
            RegisterSpawnPrefab(triggerTilePrefab);
            RegisterSpawnPrefab(particleTilePrefab);
            RegisterSpawnPrefab(kaplaoBulletPrefab);

            autoCreatePlayer = false;

            if (string.IsNullOrWhiteSpace(offlineScene))
                offlineScene = MainMenuSceneName;

            if (string.IsNullOrWhiteSpace(onlineScene))
                onlineScene = GameSceneName;

            NetworkManagerHUD hud = GetComponent<NetworkManagerHUD>();
            if (hud != null)
                hud.enabled = false;

            if (networkDiscovery == null)
                networkDiscovery = GetComponent<KaplaoNetworkDiscovery>();

            base.Awake();

            NetworkTransportConfig transportConfig = GetComponent<NetworkTransportConfig>();
            if (transportConfig == null)
                transportConfig = gameObject.AddComponent<NetworkTransportConfig>();
            else
                transportConfig.ApplyFromSession();
        }

        void RegisterSpawnPrefab(GameObject prefab)
        {
            if (prefab == null || spawnPrefabs.Contains(prefab))
                return;

            spawnPrefabs.Add(prefab);
        }

        public void ApplySessionPort()
        {
            ushort port = PlayerSession.Port == 0 ? PlayerSession.DefaultPort : PlayerSession.Port;

            if (transport is PortTransport assignedTransport)
                assignedTransport.Port = port;

            if (Transport.active is PortTransport activeTransport)
                activeTransport.Port = port;

            // Keep every PortTransport on this object in sync (KCP + Telepathy).
            PortTransport[] ports = GetComponents<PortTransport>();
            for (int i = 0; i < ports.Length; i++)
            {
                if (ports[i] != null)
                    ports[i].Port = port;
            }
        }

        public ushort GetActiveListenPort()
        {
            if (Transport.active is PortTransport active)
                return active.Port;

            if (transport is PortTransport assigned)
                return assigned.Port;

            return PlayerSession.Port;
        }

        public void StartSinglePlayerHost()
        {
            PlayerSession.UpdateServerDisplayName();
            if (PlayerSession.Port == 0)
                PlayerSession.SetPort(PlayerSession.DefaultPort);

            NetworkTransportConfig.Instance?.ApplyFromSession();
            ApplySessionPort();
            networkAddress = "127.0.0.1";
            lanAdvertised = false;
            isSinglePlayerHostSession = true;
            networkDiscovery?.StopDiscovery();

            ushort port = GetActiveListenPort();
            string transportType = transport != null ? transport.GetType().Name : "<null>";
            Debug.Log($"[KaplaoNetworkManager] StartSinglePlayerHost: port={port} transport={transportType} offlineScene={offlineScene} onlineScene={onlineScene}");

            try
            {
                StartHost();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[KaplaoNetworkManager] StartHost failed: {ex}");
                throw;
            }
        }

        public void OpenToLan()
        {
            if (!NetworkServer.active)
                return;

            PlayerSession.UpdateServerDisplayName();
            networkDiscovery?.AdvertiseServerOnInterface(PlayerSession.LanInterfaceIPv4);
            lanAdvertised = true;
        }

        public void StopLanAdvertisement()
        {
            networkDiscovery?.StopDiscovery();
            lanAdvertised = false;
        }

        public void StartLanClient(System.Uri uri)
        {
            NetworkTransportConfig.Instance?.ApplyFromSession();
            ApplySessionPort();
            networkAddress = uri.Host;
            if (Transport.active is PortTransport portTransport)
                portTransport.Port = (ushort)uri.Port;

            StartClient(uri);
        }

        public void StartDirectClient(string host, ushort port)
        {
            if (string.IsNullOrWhiteSpace(host))
                return;

            NetworkTransportConfig.Instance?.ApplyFromSession();
            networkAddress = host.Trim();
            if (Transport.active is PortTransport portTransport)
                portTransport.Port = port == 0 ? PlayerSession.Port : port;

            StartClient();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            kaplaoAssigned = false;
            kaplaoCount = 0;
            ushort listenPort = GetActiveListenPort();
            PlayerSession.SetPort(listenPort);
            Debug.Log($"[KaplaoNetworkManager] Server listening on port {listenPort} ({(transport != null ? transport.GetType().Name : "null")}).");
            NetworkServer.RegisterHandler<JoinGameMessage>(OnJoinGameMessage);
            KaplaoNicknamePresetCatalog.RequestRefresh(force: true);

            // Ensure player data sync server is running
            if (PlayerDataSyncServer.Instance == null)
            {
                GameObject syncServerGo = new GameObject("[PlayerDataSyncServer]");
                syncServerGo.AddComponent<PlayerDataSyncServer>();
                Debug.Log("[KaplaoNetworkManager] PlayerDataSyncServer initialized for real-time player data updates");
            }
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<JoinGameMessage>();

            LevelSessionServer.Instance?.ServerStopScript();

            if (LevelGridServer.Instance != null)
                LevelGridServer.Instance.ClearAllTiles();

            WorldCustomTextureService.ClearRuntime();

            RunnerSpawnPoint.ClearCache();
            PlayerReconnectPositionCache.Clear();
            PlayerRunnerSpawnCache.ClearAll();

            base.OnStopServer();
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            PlayerSession.Load();
            RunnerSkinPreference.Load();
            KaplaoNicknamePresetCatalog.RequestRefresh(force: true);

            if (string.IsNullOrWhiteSpace(PlayerSession.AuthToken))
            {
                Debug.LogWarning(
                    "[KaplaoNetworkManager] Join without website login (empty auth token). " +
                    "Sign in via Kaplao account on the main menu, then host/join again. " +
                    "Admin rights and nickname presets will not apply.");
            }

            if (!isSinglePlayerHostSession)
            {
                KaplaoPlayerActionLogClient.Log(
                    "server_join",
                    metadataJson: "{\"source\":\"OnClientConnect\"}");
            }

            NetworkClient.Send(new JoinGameMessage
            {
                playerName = PlayerSession.Nickname,
                accountEmail = PlayerSession.AccountEmail,
                authToken = PlayerSession.AuthToken,
                preferredSkinIndex = RunnerSkinPreference.SelectedIndex,
                cachedNicknamePreset = PlayerSession.CachedNicknamePreset
            });
        }

        public override void OnClientNotReady()
        {
            base.OnClientNotReady();

            // ReplacePlayer (promote to Kaplao) can flip ready=false while still in-game.
            // That drops Commands and spams Mirror "Command ... not ready" warnings.
            if (NetworkClient.isConnected && NetworkClient.localPlayer != null)
                NetworkClient.Ready();
        }

        void OnJoinGameMessage(NetworkConnectionToClient conn, JoinGameMessage message)
        {
            if (conn.identity != null)
                return;

            bool needsVerification = !string.IsNullOrWhiteSpace(message.authToken) &&
                                     Application.internetReachability != NetworkReachability.NotReachable;

            string initialPreset = string.IsNullOrWhiteSpace(message.cachedNicknamePreset)
                ? "none"
                : message.cachedNicknamePreset.Trim().ToLowerInvariant();
            if (initialPreset == "own")
                initialPreset = "none";

            string clientEmail = (message.accountEmail ?? string.Empty).Trim();

            // STRICT SECURITY: Spawn immediately with isAdmin = false and permission = "none".
            // Privileges MUST ONLY be granted server-side AFTER a valid online response from VerifyPlayerCoroutine.
            NetworkPlayer spawnedPlayer = SpawnPlayer(
                conn,
                message.playerName,
                clientEmail,
                message.preferredSkinIndex,
                isAdmin: false,
                permission: "none",
                nicknamePreset: initialPreset,
                ownedSkinIds: System.Array.Empty<string>(),
                skinOwnershipVerified: false,
                authToken: message.authToken,
                isVerifying: needsVerification,
                allowUnverifiedAdminSkin: true);

            if (needsVerification && spawnedPlayer != null)
            {
                StartCoroutine(VerifySpawnedPlayerInBackground(conn, spawnedPlayer, message.authToken));
            }
        }

        IEnumerator VerifySpawnedPlayerInBackground(NetworkConnectionToClient conn, NetworkPlayer initialPlayer, string authToken)
        {
            var verifyResult = new KaplaoAccountVerificationService.VerifyResult();
            bool verifyDone = false;

            yield return KaplaoAccountVerificationService.VerifyPlayerCoroutine(
                authToken,
                result =>
                {
                    verifyResult = result;
                    verifyDone = true;
                });

            while (!verifyDone)
                yield return null;

            if (conn == null || conn.identity == null)
                yield break;

            NetworkPlayer currentPlayer = conn.identity.GetComponent<NetworkPlayer>();
            if (currentPlayer == null)
                yield break;

            if (verifyResult.Valid)
            {
                string verifiedEmail = !string.IsNullOrWhiteSpace(verifyResult.Email)
                    ? verifyResult.Email.Trim()
                    : currentPlayer.accountEmail;

                bool verifiedAdmin = verifyResult.IsAdmin;
                string verifiedPermission = string.IsNullOrWhiteSpace(verifyResult.Permission) ? "none" : verifyResult.Permission.Trim();
                string verifiedPreset = string.IsNullOrWhiteSpace(verifyResult.NicknamePreset) ||
                                        string.Equals(verifyResult.NicknamePreset, "own", System.StringComparison.OrdinalIgnoreCase)
                    ? "none"
                    : verifyResult.NicknamePreset.Trim().ToLowerInvariant();

                bool ownershipVerified = (verifyResult.OwnedSkinIds != null && verifyResult.OwnedSkinIds.Length > 0) ||
                                         (conn == NetworkServer.localConnection);

                currentPlayer.ServerApplyVerifiedAccountData(
                    verifiedEmail,
                    verifiedAdmin,
                    verifiedPermission,
                    verifiedPreset,
                    verifyResult.OwnedSkinIds,
                    ownershipVerified);

                currentPlayer.TargetCacheVerifiedNicknamePreset(conn, verifiedPreset);
                currentPlayer.nicknamePresetStyleVersion++;

                Debug.Log(
                    $"[KaplaoNetworkManager] Background verify {verifiedEmail}: valid=True admin={verifiedAdmin} permission={verifiedPermission} preset={verifiedPreset} skins={verifyResult.OwnedSkinIds?.Length ?? 0}");
            }
            else
            {
                currentPlayer.isVerifyingAccount = false;
                currentPlayer.isAdmin = false;
                currentPlayer.playerPermission = "none";

                // If explicit invalid token (revoked / fake token), reset preset to none.
                // If offline / network timeout, keep the cached cosmetic preset.
                if (!verifyResult.NetworkError)
                {
                    currentPlayer.nicknamePreset = "none";
                    currentPlayer.serverSkinRejectedByOnlineCheck = true;
                    if (currentPlayer.role == PlayerRole.Runner && currentPlayer.TryGetComponent(out RunnerAppearanceClient appearance))
                    {
                        appearance.ServerAssignCharacter(
                            currentPlayer.preferredSkinIndex,
                            System.Array.Empty<string>(),
                            ownershipVerified: true,
                            allowUnverifiedAdminSkin: false);
                    }
                }

                Debug.Log(
                    $"[KaplaoNetworkManager] Background verify failed (networkError={verifyResult.NetworkError}). Privileges set to none.");
            }
        }

        [Server]
        NetworkPlayer SpawnPlayer(
            NetworkConnectionToClient conn,
            string playerName,
            string accountEmail,
            int preferredSkinIndex = -1,
            bool isAdmin = false,
            string permission = "none",
            string nicknamePreset = "none",
            string[] ownedSkinIds = null,
            bool skinOwnershipVerified = false,
            string authToken = null,
            bool isVerifying = false,
            bool allowUnverifiedAdminSkin = false)
        {
            // Enforce bans persisted in level.json (nickname and/or IP).
            string sanitizedName = PlayerSession.SanitizeNickname(playerName);
            string sanitizedEmail = string.IsNullOrWhiteSpace(accountEmail)
                ? string.Empty
                : accountEmail.Trim();
            if (LevelSessionServer.Instance != null &&
                LevelSessionServer.Instance.IsConnectionBanned(conn, sanitizedName))
            {
                conn.Disconnect();
                return null;
            }

            // One nickname per server.
            if (IsDisplayNameTaken(sanitizedName))
            {
                Debug.Log($"[KaplaoNetworkManager] Rejected join: nickname '{sanitizedName}' already in use.");
                conn.Disconnect();
                return null;
            }

            bool isKaplao = ShouldSpawnAsKaplao(conn);
            GameObject prefab = isKaplao ? kaplaoPrefab : runnerPrefab;

            if (prefab == null)
            {
                Debug.LogError("[KaplaoNetworkManager] Prefab is not assigned.");
                return null;
            }

            Transform startPos = GetStartPosition();
            Vector3 spawnPosition = startPos != null ? startPos.position : Vector3.zero;
            Quaternion spawnRotation = startPos != null ? startPos.rotation : Quaternion.identity;

            bool restorePosition = PlayerReconnectPositionCache.TryTake(
                sanitizedName,
                out PlayerReconnectPositionCache.Snapshot saved);
            if (restorePosition)
                spawnPosition = new Vector3(saved.Position.x, saved.Position.y, spawnPosition.z);

            GameObject player = Instantiate(prefab, spawnPosition, spawnRotation);
            NetworkPlayer networkPlayer = null;

            if (player.TryGetComponent(out networkPlayer))
            {
                networkPlayer.ServerInit(
                    sanitizedName,
                    isKaplao ? PlayerRole.Kaplao : PlayerRole.Runner,
                    sanitizedEmail,
                    isAdmin,
                    permission,
                    nicknamePreset,
                    authToken);
                networkPlayer.isVerifyingAccount = isVerifying;
                networkPlayer.preferredSkinIndex = preferredSkinIndex;
                networkPlayer.serverOwnedSkinIds = ownedSkinIds ?? System.Array.Empty<string>();
                networkPlayer.serverSkinOwnershipVerified = skinOwnershipVerified;

                if (LevelSessionServer.Instance != null &&
                    LevelSessionServer.Instance.ModeratorIds.Contains(sanitizedName))
                {
                    networkPlayer.ServerSetModeratorFlag(true);
                }
            }

            if (isKaplao)
            {
                kaplaoAssigned = true;
                kaplaoCount++;
            }

            if (!isKaplao && player.TryGetComponent(out RunnerAppearanceClient appearance))
            {
                appearance.ServerAssignCharacter(
                    preferredSkinIndex,
                    ownedSkinIds,
                    skinOwnershipVerified,
                    allowUnverifiedAdminSkin);
            }

            NetworkServer.AddPlayerForConnection(conn, player);

            // After OnStartServer (which resets spawn to the level default), restore reconnect state.
            if (restorePosition &&
                !isKaplao &&
                player.TryGetComponent(out RunnerMovementServer runnerMovement))
            {
                runnerMovement.ServerSetPosition(saved.Position);
                if (saved.HasSpawnPoint)
                    runnerMovement.ServerSetSpawnPoint(saved.SpawnPoint.x, saved.SpawnPoint.y);
            }

            if (!isKaplao && player.TryGetComponent(out NetworkPlayer spawnedRunner))
                LevelScriptRuntime.Instance?.NotifyPlayerAdded(spawnedRunner.netId);

            LevelSessionServer.Instance?.ServerSyncAllCustomTexturesToConnection(conn);
            LevelSessionServer.Instance?.ServerSyncScriptProjectToConnection(conn);
            LevelSessionServer.Instance?.ServerSyncBannedIdsToConnection(conn);

            LevelGroupAnimator.Instance?.SyncScriptedTilesNow();
            LevelGridServer.Instance?.ServerResyncTileObserversForConnection(conn);

            return networkPlayer;
        }

        /// <summary>
        /// Only the listen-server host spawns as Kaplao. Remote clients always join as Runner.
        /// (Old logic: first joiner = Kaplao — broke when Kaplao disconnected and next client became Kaplao.)
        /// </summary>
        [Server]
        bool ShouldSpawnAsKaplao(NetworkConnectionToClient conn)
        {
            if (WorldSession.IsBrowsePlaySession)
                return false;

            if (conn != NetworkServer.localConnection)
                return false;

            if (WorldSession.HostSpawnsAsRunner)
                return false;

            if (kaplaoAssigned)
                return false;

            return true;
        }

        [Server]
        static bool IsDisplayNameTaken(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            foreach (var kv in NetworkServer.spawned)
            {
                if (kv.Value != null &&
                    kv.Value.TryGetComponent(out NetworkPlayer existing) &&
                    !string.IsNullOrEmpty(existing.displayName) &&
                    string.Equals(existing.displayName, displayName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Protected account role may only be changed by that same player (self).
        /// </summary>
        [Server]
        public static bool CanChangeProtectedPlayer(NetworkPlayer target, uint? actorNetId)
        {
            if (target == null || !target.isAdmin)
                return true;

            return actorNetId.HasValue && actorNetId.Value == target.netId;
        }

        [Server]
        public bool TryPromoteRunnerToKaplao(uint runnerNetId, uint? actorNetId = null)
        {
            if (WorldSession.IsBrowsePlaySession)
                return false;

            if (kaplaoPrefab == null)
                return false;

            if (!NetworkServer.spawned.TryGetValue(runnerNetId, out NetworkIdentity runnerIdentity))
                return false;

            if (!runnerIdentity.TryGetComponent(out NetworkPlayer runnerPlayer) ||
                runnerPlayer.role != PlayerRole.Runner)
            {
                return false;
            }

            if (!CanChangeProtectedPlayer(runnerPlayer, actorNetId))
                return false;

            NetworkConnectionToClient conn = runnerIdentity.connectionToClient;
            if (conn == null)
                return false;

            if (runnerIdentity.TryGetComponent(out RunnerMovementServer runnerMovement))
                PlayerRunnerSpawnCache.Save(conn.connectionId, runnerMovement.SpawnPosition);
            else
                PlayerRunnerSpawnCache.Save(conn.connectionId, RunnerSpawnPoint.GetPosition());

            string playerName = runnerPlayer.displayName;
            string playerEmail = runnerPlayer.accountEmail;
            bool playerIsModerator = runnerPlayer.isModerator;
            bool playerIsAdmin = runnerPlayer.isAdmin;
            string playerPermission = runnerPlayer.playerPermission;
            string playerNicknamePreset = runnerPlayer.nicknamePreset;
            string playerAuthToken = runnerPlayer.serverAuthToken;
            bool playerIsVerifying = runnerPlayer.isVerifyingAccount;
            int preferredSkin = runnerPlayer.preferredSkinIndex;
            Vector3 position = runnerIdentity.transform.position;
            Quaternion rotation = runnerIdentity.transform.rotation;

            GameObject kaplaoObject = Instantiate(kaplaoPrefab, position, rotation);
            if (kaplaoObject.TryGetComponent(out NetworkPlayer kaplaoPlayer))
            {
                kaplaoPlayer.ServerInit(
                    playerName,
                    PlayerRole.Kaplao,
                    playerEmail,
                    playerIsAdmin,
                    playerPermission,
                    playerNicknamePreset,
                    playerAuthToken);
                kaplaoPlayer.isVerifyingAccount = playerIsVerifying;
                kaplaoPlayer.ServerSetModeratorFlag(playerIsModerator);
                kaplaoPlayer.preferredSkinIndex = preferredSkin;
                // Preserve admin skin ownership so Kaplao can use them again when becoming Runner.
                kaplaoPlayer.serverOwnedSkinIds = runnerPlayer.serverOwnedSkinIds ?? System.Array.Empty<string>();
                kaplaoPlayer.serverSkinOwnershipVerified = runnerPlayer.serverSkinOwnershipVerified;
            }

            NetworkServer.ReplacePlayerForConnection(conn, kaplaoObject, ReplacePlayerOptions.Destroy);
            kaplaoCount++;

            if (kaplaoObject.TryGetComponent(out KaplaoCursorServer restoredCursor) &&
                KaplaoEditorSessionCache.TryTake(conn.connectionId, out KaplaoEditorSessionCache.Snapshot snapshot))
            {
                restoredCursor.ServerRestoreKaplaoSession(
                    snapshot.GameMode,
                    snapshot.EditorLayer,
                    snapshot.SelectedNetIds,
                    snapshot.RotationSnapDegrees,
                    snapshot.PositionSnapUnits,
                    snapshot.ScaleSnapUnits);
            }

            LevelGridServer.Instance?.ServerResyncTileObserversForConnection(conn);
            LevelGroupAnimator.Instance?.SyncScriptedTilesNow();

            return true;
        }

        [Server]
        static int ResolveRunnerSkinForDemotion(
            NetworkPlayer kaplaoPlayer,
            NetworkConnectionToClient conn,
            int? preferredSkinOverride)
        {
            if (preferredSkinOverride.HasValue)
                return preferredSkinOverride.Value;

            // Always use the saved preferredSkinIndex from when they became Kaplao.
            // This preserves admin skin selection correctly even if local preferences changed.
            // The serverOwnedSkinIds and serverSkinOwnershipVerified are passed separately to validate ownership.
            int savedPreference = kaplaoPlayer != null ? kaplaoPlayer.preferredSkinIndex : RunnerSkinPreference.RandomIndex;

            return savedPreference;
        }

        [Server]
        public bool TryDemoteKaplaoToRunner(
            uint kaplaoNetId,
            uint? actorNetId = null,
            int? preferredSkinOverride = null,
            KaplaoDemotionSpawnMode spawnMode = KaplaoDemotionSpawnMode.LastSpawnOrWorld)
        {
            if (runnerPrefab == null)
                return false;

            if (!NetworkServer.spawned.TryGetValue(kaplaoNetId, out NetworkIdentity kaplaoIdentity))
                return false;

            if (!kaplaoIdentity.TryGetComponent(out NetworkPlayer kaplaoPlayer) ||
                kaplaoPlayer.role != PlayerRole.Kaplao)
            {
                return false;
            }

            if (!CanChangeProtectedPlayer(kaplaoPlayer, actorNetId))
                return false;

            NetworkConnectionToClient conn = kaplaoIdentity.connectionToClient;
            if (conn == null)
                return false;

            if (kaplaoIdentity.TryGetComponent(out KaplaoCursorServer demotedCursor))
                KaplaoEditorSessionCache.Save(conn.connectionId, demotedCursor);

            string playerName = kaplaoPlayer.displayName;
            string playerEmail = kaplaoPlayer.accountEmail;
            bool playerIsModerator = kaplaoPlayer.isModerator;
            bool playerIsAdmin = kaplaoPlayer.isAdmin;
            string playerPermission = kaplaoPlayer.playerPermission;
            string playerNicknamePreset = kaplaoPlayer.nicknamePreset;
            string playerAuthToken = kaplaoPlayer.serverAuthToken;
            bool playerIsVerifying = kaplaoPlayer.isVerifyingAccount;
            int preferredSkin = ResolveRunnerSkinForDemotion(
                kaplaoPlayer,
                conn,
                preferredSkinOverride);

            Vector2 respawnPoint = ResolveRunnerSpawnForDemotion(conn.connectionId);
            Vector3 kaplaoPosition = kaplaoIdentity.transform.position;
            Vector3 spawnPosition = spawnMode == KaplaoDemotionSpawnMode.CurrentKaplaoPosition
                ? kaplaoPosition
                : new Vector3(respawnPoint.x, respawnPoint.y, kaplaoPosition.z);
            Quaternion rotation = kaplaoIdentity.transform.rotation;

            GameObject runnerObject = Instantiate(runnerPrefab, spawnPosition, rotation);
            if (runnerObject.TryGetComponent(out NetworkPlayer runnerNetworkPlayer))
            {
                runnerNetworkPlayer.ServerInit(
                    playerName,
                    PlayerRole.Runner,
                    playerEmail,
                    playerIsAdmin,
                    playerPermission,
                    playerNicknamePreset,
                    playerAuthToken);
                runnerNetworkPlayer.isVerifyingAccount = playerIsVerifying;
                runnerNetworkPlayer.ServerSetModeratorFlag(playerIsModerator);
                runnerNetworkPlayer.preferredSkinIndex = preferredSkin;
                runnerNetworkPlayer.serverOwnedSkinIds = kaplaoPlayer.serverOwnedSkinIds ?? System.Array.Empty<string>();
                runnerNetworkPlayer.serverSkinOwnershipVerified = kaplaoPlayer.serverSkinOwnershipVerified;
                runnerNetworkPlayer.serverSkinRejectedByOnlineCheck = kaplaoPlayer.serverSkinRejectedByOnlineCheck;
            }

            bool allowUnverifiedAdminSkin = !kaplaoPlayer.serverSkinOwnershipVerified && !kaplaoPlayer.serverSkinRejectedByOnlineCheck;
            if (runnerObject.TryGetComponent(out RunnerAppearanceClient appearance))
            {
                appearance.ServerAssignCharacter(
                    preferredSkin,
                    kaplaoPlayer.serverOwnedSkinIds,
                    kaplaoPlayer.serverSkinOwnershipVerified,
                    allowUnverifiedAdminSkin: allowUnverifiedAdminSkin);
            }

            NetworkServer.ReplacePlayerForConnection(conn, runnerObject, ReplacePlayerOptions.Destroy);

            if (runnerObject.TryGetComponent(out RunnerMovementServer demotedRunner))
            {
                demotedRunner.ServerSetSpawnPoint(respawnPoint.x, respawnPoint.y);
                demotedRunner.ServerSetPosition(new Vector2(spawnPosition.x, spawnPosition.y));
            }

            kaplaoCount = Mathf.Max(0, kaplaoCount - 1);
            if (kaplaoCount == 0)
                kaplaoAssigned = false;

            // Ensure level runtime knows about the newly spawned runner.
            if (runnerObject.TryGetComponent(out NetworkPlayer spawnedRunner))
                LevelScriptRuntime.Instance?.NotifyPlayerAdded(spawnedRunner.netId);

            LevelGroupAnimator.Instance?.SyncScriptedTilesNow();
            LevelGridServer.Instance?.ServerResyncTileObserversForConnection(conn);

            return true;
        }

        [Server]
        static Vector2 ResolveRunnerSpawnForDemotion(int connectionId)
        {
            if (PlayerRunnerSpawnCache.TryGet(connectionId, out Vector2 cached))
                return cached;

            return RunnerSpawnPoint.GetPosition();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            KaplaoEditorSessionCache.Clear(conn.connectionId);
            PlayerRunnerSpawnCache.Clear(conn.connectionId);

            if (conn.identity != null)
            {
                if (conn.identity.TryGetComponent(out NetworkPlayer networkPlayer))
                {
                    Vector2 position = conn.identity.transform.position;
                    Vector2? spawnPoint = null;
                    if (networkPlayer.role == PlayerRole.Runner &&
                        conn.identity.TryGetComponent(out RunnerMovementServer runner))
                    {
                        spawnPoint = runner.SpawnPosition;
                    }

                    PlayerReconnectPositionCache.Save(
                        networkPlayer.displayName,
                        position,
                        spawnPoint);

                    if (networkPlayer.role == PlayerRole.Kaplao)
                    {
                        kaplaoCount = Mathf.Max(0, kaplaoCount - 1);
                        if (kaplaoCount == 0)
                            kaplaoAssigned = false;
                    }
                }
            }

            base.OnServerDisconnect(conn);
        }

        public override void OnStopHost()
        {
            isSinglePlayerHostSession = false;
            StopLanAdvertisement();
            WorldSession.Clear();
            base.OnStopHost();
        }

        public override void OnStopClient()
        {
            isSinglePlayerHostSession = false;
            networkDiscovery?.StopDiscovery();
            WorldSession.Clear();
            base.OnStopClient();
        }
    }
}
