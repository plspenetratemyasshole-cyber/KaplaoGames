using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// In-game HUD: Kaplao-only panels.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSessionUi : MonoBehaviour
    {
        [Header("Kaplao only")]
        [SerializeField] GameObject[] kaplaoOnlyRoots;

        [Header("Legacy UI (hidden at runtime)")]
        [SerializeField] GameObject legacyLeaveButton;
        [SerializeField] GameObject legacyLevelIoPanel;
        [SerializeField] KaplaoLevelIoUi legacyLevelIoUi;

        NetworkPlayer subscribedPlayer;

        void Awake()
        {
            MergeKaplaoOnlyRoots();
            DisableLegacySessionUi();
        }

        void OnEnable()
        {
            StartCoroutine(WatchLocalPlayerRole());
        }

        void OnDisable()
        {
            UnsubscribePlayer();
        }

        void DisableLegacySessionUi()
        {
            if (legacyLeaveButton != null)
                legacyLeaveButton.SetActive(false);

            if (legacyLevelIoPanel != null)
                legacyLevelIoPanel.SetActive(false);

            if (legacyLevelIoUi != null)
                legacyLevelIoUi.enabled = false;
        }

        System.Collections.IEnumerator WatchLocalPlayerRole()
        {
            SetKaplaoOnlyVisible(false);

            uint lastLocalNetId = 0;
            while (true)
            {
                if (NetworkClient.localPlayer == null)
                {
                    if (subscribedPlayer != null)
                        UnsubscribePlayer();

                    SetKaplaoOnlyVisible(false);
                    lastLocalNetId = 0;
                    yield return null;
                    continue;
                }

                if (!NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer networkPlayer))
                {
                    SetKaplaoOnlyVisible(false);
                    yield return null;
                    continue;
                }

                if (subscribedPlayer != networkPlayer || networkPlayer.netId != lastLocalNetId)
                {
                    lastLocalNetId = networkPlayer.netId;
                    SubscribePlayer(networkPlayer);
                }
                else
                {
                    // Prefab-based role wins if SyncVar lags after ReplacePlayer.
                    PlayerRole? resolved = LocalPlayerRole.GetOrNull();
                    if (resolved.HasValue)
                        SetKaplaoOnlyVisible(resolved.Value == PlayerRole.Kaplao);
                }

                yield return null;
            }
        }

        void SubscribePlayer(NetworkPlayer networkPlayer)
        {
            UnsubscribePlayer();
            subscribedPlayer = networkPlayer;
            subscribedPlayer.RoleChanged += OnRoleChanged;
            OnRoleChanged(subscribedPlayer.role);
        }

        void UnsubscribePlayer()
        {
            if (subscribedPlayer == null)
                return;

            subscribedPlayer.RoleChanged -= OnRoleChanged;
            subscribedPlayer = null;
        }

        void OnRoleChanged(PlayerRole role)
        {
            PlayerRole resolved = LocalPlayerRole.GetOrNull() ?? role;
            SetKaplaoOnlyVisible(resolved == PlayerRole.Kaplao);
        }

        void SetKaplaoOnlyVisible(bool visible)
        {
            if (kaplaoOnlyRoots == null)
                return;

            for (int i = 0; i < kaplaoOnlyRoots.Length; i++)
            {
                if (kaplaoOnlyRoots[i] != null)
                    kaplaoOnlyRoots[i].SetActive(visible);
            }
        }

        void MergeKaplaoOnlyRoots()
        {
            var roots = new List<GameObject>();
            AddRoot(roots, kaplaoOnlyRoots);

            if (TryGetComponent(out KaplaoCreateModeUi createModeUi))
                AddRoot(roots, createModeUi.KaplaoOnlyRoot);

            if (TryGetComponent(out KaplaoTileColorUi tileColorUi))
                AddRoot(roots, tileColorUi.KaplaoOnlyRoot);

            if (roots.Count > 0)
                kaplaoOnlyRoots = roots.ToArray();
        }

        static void AddRoot(List<GameObject> roots, GameObject root)
        {
            if (root == null)
                return;

            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] == root)
                    return;
            }

            roots.Add(root);
        }

        static void AddRoot(List<GameObject> roots, GameObject[] extraRoots)
        {
            if (extraRoots == null)
                return;

            for (int i = 0; i < extraRoots.Length; i++)
                AddRoot(roots, extraRoots[i]);
        }
    }
}
