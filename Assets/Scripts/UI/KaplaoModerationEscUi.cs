using System;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// ESC player list. Clones PlayerRowTemplate from scroll Content — edit template layout in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoModerationEscUi : MonoBehaviour
    {
        const float RefreshIntervalSeconds = 0.5f;

        [SerializeField] GameObject panelRoot;
        [SerializeField] Transform listContent;
        [SerializeField] GameObject playerRowTemplate;
        [SerializeField] Button moderateChatButton;
        [SerializeField] TextMeshProUGUI moderateChatButtonText;

        float nextRefreshTime;
        NetworkPlayer localNetworkPlayer;
        string lastPlayersFingerprint;
        bool rowTemplateLayoutEnsured;
        bool? lastModerateChatOn;

        void OnEnable()
        {
            nextRefreshTime = 0f;
            lastPlayersFingerprint = null;
            lastModerateChatOn = null;
            TryAutoBind();
            if (Application.isPlaying && playerRowTemplate != null)
                playerRowTemplate.SetActive(false);

            if (moderateChatButton != null)
                moderateChatButton.onClick.AddListener(OnModerateChatClicked);

            KaplaoNicknamePresetCatalog.RequestRefresh();
            KaplaoNicknamePresetCatalog.Changed += OnNicknamePresetsChanged;
        }

        void OnDisable()
        {
            if (moderateChatButton != null)
                moderateChatButton.onClick.RemoveListener(OnModerateChatClicked);

            KaplaoNicknamePresetCatalog.Changed -= OnNicknamePresetsChanged;
        }

        void OnNicknamePresetsChanged()
        {
            lastPlayersFingerprint = null;
            nextRefreshTime = 0f;
        }

        void Update()
        {
            TryAutoBind();

            bool isHost = NetworkServer.active && NetworkClient.isConnected;
            bool isProtected = NetworkPlayer.IsLocalAdmin();
            bool canModerate = isHost || isProtected;
            bool pauseOpen = IsAnyPauseMenuOpen();
            bool canShow = NetworkClient.isConnected && NetworkClient.localPlayer != null && pauseOpen;

            if (panelRoot != null && panelRoot.activeSelf != canShow)
            {
                panelRoot.SetActive(canShow);
                if (canShow)
                    panelRoot.transform.SetAsLastSibling();
            }

            if (!canShow)
                return;

            if (panelRoot == null || !panelRoot.activeInHierarchy)
                return;

            if (localNetworkPlayer == null)
                NetworkClient.localPlayer.TryGetComponent(out localNetworkPlayer);

            if (moderateChatButton != null)
                moderateChatButton.gameObject.SetActive(canModerate);

            if (canModerate)
            {
                bool chatOn = LevelSessionServer.Instance != null && LevelSessionServer.Instance.ModerateChatEnabled;
                if (!lastModerateChatOn.HasValue || lastModerateChatOn.Value != chatOn)
                {
                    lastModerateChatOn = chatOn;
                    SyncModerateChatButton();
                }
            }

            if (Time.time < nextRefreshTime)
                return;

            nextRefreshTime = Time.time + RefreshIntervalSeconds;
            RefreshPlayers(isHost, canModerate);
        }

        void TryAutoBind()
        {
            if (panelRoot == null)
            {
                Canvas canvas = GetComponent<Canvas>();
                if (canvas == null)
                    canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform panel = canvas.transform.Find("HostModerationPanel");
                    if (panel != null)
                        panelRoot = panel.gameObject;
                }
            }

            if (listContent == null && panelRoot != null)
                listContent = panelRoot.transform.Find("PlayersScroll/Viewport/Content");

            if (playerRowTemplate == null && listContent != null)
            {
                Transform template = listContent.Find("PlayerRowTemplate");
                if (template != null)
                    playerRowTemplate = template.gameObject;
            }

            if (!rowTemplateLayoutEnsured && playerRowTemplate != null)
            {
                PlayerRowTemplateLayout.Ensure(playerRowTemplate, rebuildLayout: true);
                rowTemplateLayoutEnsured = true;
            }

            if (moderateChatButton == null && panelRoot != null)
                moderateChatButton = panelRoot.transform.Find("ModerateChatButton")?.GetComponent<Button>();

            if (moderateChatButtonText == null && moderateChatButton != null)
                moderateChatButtonText = moderateChatButton.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        }

        static bool IsAnyPauseMenuOpen()
        {
            if (IsPauseMenuRootOpen())
                return true;

            RunnerPauseMenuUI runnerPause = FindFirstObjectByType<RunnerPauseMenuUI>();
            if (runnerPause == null)
                return false;

            Transform runnerRoot = runnerPause.transform.Find("RunnerPauseMenuRoot");
            if (runnerRoot == null)
            {
                Canvas canvas = runnerPause.GetComponentInParent<Canvas>();
                if (canvas != null)
                    runnerRoot = canvas.transform.Find("RunnerPauseMenuRoot");
            }

            return runnerRoot != null && runnerRoot.gameObject.activeSelf;
        }

        static bool IsPauseMenuRootOpen()
        {
            PauseMenuUI pauseUi = FindFirstObjectByType<PauseMenuUI>();
            if (pauseUi == null)
                return false;

            Transform pauseRoot = pauseUi.transform.Find("PauseMenuRoot");
            if (pauseRoot == null && pauseUi.transform.parent != null)
                pauseRoot = pauseUi.transform.parent.Find("PauseMenuRoot");

            if (pauseRoot == null)
            {
                Canvas canvas = pauseUi.GetComponentInParent<Canvas>();
                if (canvas != null)
                    pauseRoot = canvas.transform.Find("PauseMenuRoot");
            }

            return pauseRoot != null && pauseRoot.gameObject.activeSelf;
        }

        void OnModerateChatClicked()
        {
            if (LevelSessionServer.Instance == null)
                return;

            bool next = !LevelSessionServer.Instance.ModerateChatEnabled;
            if (NetworkServer.active)
            {
                LevelSessionServer.Instance.ServerSetModerateChatEnabled(next);
                SyncModerateChatButton();
                return;
            }

            if (!NetworkPlayer.IsLocalAdmin())
                return;

            LevelSessionServer.Instance.CmdSetModerateChatEnabled(next);
        }

        void SyncModerateChatButton()
        {
            if (LevelSessionServer.Instance == null)
                return;

            bool on = LevelSessionServer.Instance.ModerateChatEnabled;
            if (moderateChatButtonText != null)
                moderateChatButtonText.text = $"Moderate chat: {(on ? "ON" : "OFF")}";

            KaplaoToggleButtonStyle.Apply(moderateChatButton, on);
        }

        void RefreshPlayers(bool isHost, bool canModerate)
        {
            if (listContent == null || playerRowTemplate == null)
                return;

            if (NetworkClient.localPlayer == null ||
                !NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer localNp))
            {
                return;
            }

            localNetworkPlayer = localNp;

            var players = new List<NetworkPlayer>(8);
            if (isHost)
            {
                foreach (var kv in NetworkServer.spawned)
                {
                    if (kv.Value != null && kv.Value.TryGetComponent(out NetworkPlayer np) && np != null)
                        players.Add(np);
                }
            }
            else
            {
                foreach (var kv in NetworkClient.spawned)
                {
                    if (kv.Value != null && kv.Value.TryGetComponent(out NetworkPlayer np) && np != null)
                        players.Add(np);
                }
            }

            players.Sort((a, b) =>
            {
                bool aLocal = a.netId == localNetworkPlayer.netId;
                bool bLocal = b.netId == localNetworkPlayer.netId;
                if (aLocal != bLocal)
                    return aLocal ? -1 : 1;
                return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
            });

            string fingerprint = BuildPlayersFingerprint(players, canModerate);
            if (fingerprint == lastPlayersFingerprint && CountActiveRows() == players.Count)
                return;

            lastPlayersFingerprint = fingerprint;
            ClearActiveRows();

            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer np = players[i];
                BindPlayerRow(
                    Instantiate(playerRowTemplate, listContent, false),
                    np,
                    np.netId == localNetworkPlayer.netId,
                    isHost,
                    canModerate);
            }

            RebuildListLayout();
            StartCoroutine(ReapplyNicknameStylesNextFrame(players));
        }

        System.Collections.IEnumerator ReapplyNicknameStylesNextFrame(List<NetworkPlayer> playersSnapshot)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (listContent == null || playersSnapshot == null)
                yield break;

            for (int i = 0; i < playersSnapshot.Count; i++)
            {
                NetworkPlayer np = playersSnapshot[i];
                if (np == null) continue;
                Transform rowTrans = listContent.Find($"PlayerRow_{np.netId}");
                if (rowTrans != null)
                {
                    TextMeshProUGUI label = FindChildText(rowTrans, "PlayerName");
                    if (label != null)
                    {
                        Client.NicknameStyle.Apply(label, np.accountEmail, np.nicknamePreset);
                        if (label.TryGetComponent(out KaplaoNicknameEasyEffectsApplier applier))
                        {
                            applier.ForceRefreshEffect();
                        }
                    }
                }
            }
        }

        void RebuildListLayout()
        {
            if (listContent is not RectTransform contentRect)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        static void RebuildRowLayout(GameObject row)
        {
            if (row == null)
                return;

            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
        }

        int CountActiveRows()
        {
            if (listContent == null)
                return 0;

            int count = 0;
            for (int i = 0; i < listContent.childCount; i++)
            {
                Transform child = listContent.GetChild(i);
                if (playerRowTemplate != null && child.gameObject == playerRowTemplate)
                    continue;
                count++;
            }

            return count;
        }

        void ClearActiveRows()
        {
            for (int i = listContent.childCount - 1; i >= 0; i--)
            {
                Transform child = listContent.GetChild(i);
                if (playerRowTemplate != null && child.gameObject == playerRowTemplate)
                    continue;

                Destroy(child.gameObject);
            }
        }

        static string BuildPlayersFingerprint(List<NetworkPlayer> players, bool canModerate)
        {
            var sb = new System.Text.StringBuilder(players.Count * 48);
            sb.Append(canModerate ? '1' : '0').Append('|');
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer np = players[i];
                sb.Append(np.netId).Append(':')
                    .Append((int)np.role).Append(':')
                    .Append(np.isModerator ? '1' : '0').Append(':')
                    .Append(np.isAdmin ? '1' : '0').Append(':')
                    .Append(np.playerPermission).Append(':')
                    .Append(np.displayName).Append(':')
                    .Append(np.accountEmail).Append(':')
                    .Append(np.nicknamePreset).Append(';');
            }

            return sb.ToString();
        }

        void BindPlayerRow(
            GameObject row,
            NetworkPlayer np,
            bool isSelf,
            bool isHost,
            bool canModerate)
        {
            row.name = $"PlayerRow_{np.netId}";
            PlayerRowTemplateLayout.Ensure(row, rebuildLayout: false);
            row.SetActive(true);

            bool isKaplao = np.role == PlayerRole.Kaplao;
            bool isModerator = np.isModerator;
            uint netId = np.netId;
            string displayName = np.displayName;
            bool isProtectedTarget = np.isAdmin;
            uint localNetId = localNetworkPlayer != null ? localNetworkPlayer.netId : 0;

            TextMeshProUGUI nameLabel = FindChildText(row.transform, "PlayerName");
            if (nameLabel != null)
            {
                nameLabel.text = displayName;
                NicknameStyle.Apply(nameLabel, np.accountEmail, np.nicknamePreset);
            }

            Button kaplaoBtn = FindChildButton(row.transform, "KaplaoButton");
            Button modBtn = FindChildButton(row.transform, "ModButton");
            Button kickBtn = FindChildButton(row.transform, "KickButton");
            Button banBtn = FindChildButton(row.transform, "BanButton");
            Button banIpBtn = FindChildButton(row.transform, "BanIpButton");

            bool isBrowsePlay = WorldSession.IsBrowsePlaySession ||
                                (LevelSessionServer.Instance != null && LevelSessionServer.Instance.isBrowsePlaySession);
            SetButtonVisible(kaplaoBtn, canModerate && !isBrowsePlay);
            SetButtonVisible(modBtn, canModerate);
            SetButtonVisible(kickBtn, canModerate && !isSelf);
            SetButtonVisible(banBtn, canModerate && !isSelf);
            SetButtonVisible(banIpBtn, canModerate && !isSelf);

            if (kaplaoBtn != null)
            {
                SetButtonLabel(kaplaoBtn, $"Kaplao:{(isKaplao ? "ON" : "OFF")}");
                KaplaoToggleButtonStyle.Apply(kaplaoBtn, isKaplao);
                kaplaoBtn.onClick.RemoveAllListeners();
                kaplaoBtn.onClick.AddListener(() =>
                    OnKaplaoClicked(isSelf, isHost, isProtectedTarget, isKaplao, netId, localNetId));
            }

            if (modBtn != null)
            {
                SetButtonLabel(modBtn, $"Mod:{(isModerator ? "ON" : "OFF")}");
                KaplaoToggleButtonStyle.Apply(modBtn, isModerator);
                modBtn.onClick.RemoveAllListeners();
                modBtn.onClick.AddListener(() =>
                    OnModClicked(isSelf, isHost, isProtectedTarget, isModerator, displayName));
            }

            if (kickBtn != null)
            {
                kickBtn.onClick.RemoveAllListeners();
                kickBtn.onClick.AddListener(() => TryKick(displayName, isProtectedTarget));
            }

            if (banBtn != null)
            {
                banBtn.onClick.RemoveAllListeners();
                banBtn.onClick.AddListener(() => TryBan(displayName, isProtectedTarget));
            }

            if (banIpBtn != null)
            {
                banIpBtn.onClick.RemoveAllListeners();
                banIpBtn.onClick.AddListener(() => TryBanIp(displayName, isProtectedTarget));
            }

            RebuildRowLayout(row);

            // Layout rebuild may regenerate TMP mesh — re-apply nickname Easy Effects after.
            TextMeshProUGUI nameLabelAfter = FindChildText(row.transform, "PlayerName");
            if (nameLabelAfter != null)
                NicknameStyle.Apply(nameLabelAfter, np.accountEmail, np.nicknamePreset);
        }

        static TextMeshProUGUI FindChildText(Transform root, string name)
        {
            Transform child = root.Find(name);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        static Button FindChildButton(Transform root, string name)
        {
            Transform child = root.Find(name);
            return child != null ? child.GetComponent<Button>() : null;
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            TextMeshProUGUI text = button.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.text = label;
        }

        static void SetButtonVisible(Button button, bool visible)
        {
            if (button == null)
                return;

            // With HorizontalLayoutGroup, inactive children are excluded — rebuild row after toggling.
            if (button.gameObject.activeSelf == visible)
                return;

            button.gameObject.SetActive(visible);
        }

        void TryKick(string displayName, bool isProtectedTarget)
        {
            if (isProtectedTarget)
            {
                CantChangeModeUi.ShowCantDo();
                return;
            }

            if (LevelSessionServer.Instance == null)
                return;

            if (NetworkServer.active)
            {
                LevelSessionServer.Instance.ServerKickByDisplayName(displayName);
                return;
            }

            LevelSessionServer.Instance.CmdKickByDisplayName(displayName);
        }

        void TryBan(string displayName, bool isProtectedTarget)
        {
            if (isProtectedTarget)
            {
                CantChangeModeUi.ShowCantDo();
                return;
            }

            if (LevelSessionServer.Instance == null)
                return;

            if (NetworkServer.active)
            {
                LevelSessionServer.Instance.ServerBanByDisplayName(displayName);
                return;
            }

            LevelSessionServer.Instance.CmdBanByDisplayName(displayName);
        }

        void TryBanIp(string displayName, bool isProtectedTarget)
        {
            if (isProtectedTarget)
            {
                CantChangeModeUi.ShowCantDo();
                return;
            }

            if (LevelSessionServer.Instance == null)
                return;

            if (NetworkServer.active)
            {
                LevelSessionServer.Instance.ServerBanIpByDisplayName(displayName);
                return;
            }

            LevelSessionServer.Instance.CmdBanIpByDisplayName(displayName);
        }

        void OnKaplaoClicked(
            bool isSelf,
            bool isHost,
            bool isProtectedTarget,
            bool isKaplao,
            uint netId,
            uint localNetId)
        {
            if (WorldSession.IsBrowsePlaySession || (LevelSessionServer.Instance != null && LevelSessionServer.Instance.isBrowsePlaySession))
            {
                CantChangeModeUi.Show("Copy the level to edit it as Kaplao.");
                return;
            }

            if (isProtectedTarget && !isSelf)
            {
                CantChangeModeUi.Show();
                return;
            }

            if (!isHost)
            {
                if (localNetworkPlayer == null)
                    return;

                if (isSelf)
                {
                    RunnerSkinPreference.Load();
                    localNetworkPlayer.CmdRequestSelfRoleToggle(RunnerSkinPreference.SelectedIndex);
                    return;
                }

                if (NetworkPlayer.IsLocalAdmin())
                    localNetworkPlayer.CmdRequestSetPlayerKaplao(netId);
                return;
            }

            if (KaplaoNetworkManager.Instance == null)
                return;

            if (isKaplao)
            {
                // Don't pass skin override - let TryDemoteKaplaoToRunner use the saved preferredSkinIndex.
                // This preserves admin skin selection correctly.
                KaplaoNetworkManager.Instance.TryDemoteKaplaoToRunner(
                    netId,
                    actorNetId: localNetId,
                    preferredSkinOverride: null);
            }
            else
                KaplaoNetworkManager.Instance.TryPromoteRunnerToKaplao(netId, actorNetId: localNetId);
        }

        void OnModClicked(
            bool isSelf,
            bool isHost,
            bool isProtectedTarget,
            bool isModerator,
            string displayName)
        {
            if (isProtectedTarget && !isSelf)
            {
                CantChangeModeUi.Show();
                return;
            }

            if (!isHost)
            {
                if (localNetworkPlayer == null)
                    return;

                if (isSelf)
                {
                    localNetworkPlayer.CmdRequestSelfModeratorToggle();
                    return;
                }

                if (NetworkPlayer.IsLocalAdmin())
                    localNetworkPlayer.CmdRequestSetPlayerModerator(displayName, !isModerator);
                return;
            }

            if (LevelSessionServer.Instance == null || localNetworkPlayer == null)
                return;

            if (!LevelSessionServer.Instance.ServerTrySetModeratorForId(
                    displayName,
                    !isModerator,
                    actor: localNetworkPlayer))
            {
                CantChangeModeUi.Show();
            }
        }
    }
}
