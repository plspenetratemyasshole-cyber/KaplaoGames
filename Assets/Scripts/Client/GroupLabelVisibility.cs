using System.Collections;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Shows tile group labels only in Create mode.
    /// Applies visibility on role/mode change only — never every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class GroupLabelVisibility : MonoBehaviour
    {
        KaplaoCursorServer cursorServer;
        NetworkPlayer localPlayer;
        bool subscribedMode;
        HostGameMode? lastAppliedMode;
        bool lastWasKaplao;

        void OnEnable()
        {
            StartCoroutine(BindWhenReady());
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        IEnumerator BindWhenReady()
        {
            var wait = new WaitForSecondsRealtime(0.25f);
            while (isActiveAndEnabled)
            {
                SyncBinding();
                yield return wait;
            }
        }

        void SyncBinding()
        {
            if (NetworkClient.localPlayer == null ||
                !NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player))
            {
                if (lastWasKaplao || lastAppliedMode != HostGameMode.Usual)
                    ForceRunnerVisibility();
                UnsubscribeFromCursorServer();
                if (localPlayer != null)
                {
                    localPlayer.RoleChanged -= OnLocalRoleChanged;
                    localPlayer = null;
                }

                lastWasKaplao = false;
                return;
            }

            if (localPlayer != player)
            {
                if (localPlayer != null)
                    localPlayer.RoleChanged -= OnLocalRoleChanged;

                localPlayer = player;
                localPlayer.RoleChanged += OnLocalRoleChanged;
            }

            bool isKaplao = LocalPlayerRole.IsKaplao();
            if (!isKaplao)
            {
                if (lastWasKaplao || lastAppliedMode != HostGameMode.Usual)
                    ForceRunnerVisibility();
                UnsubscribeFromCursorServer();
                lastWasKaplao = false;
                return;
            }

            lastWasKaplao = true;
            KaplaoCursorServer nextServer = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
            if (cursorServer != nextServer)
            {
                UnsubscribeFromCursorServer();
                cursorServer = nextServer;
                if (cursorServer != null)
                {
                    cursorServer.HostGameModeChanged += OnModeChanged;
                    KaplaoLocalGameModePredictor.Changed += OnPredictedModeChanged;
                    subscribedMode = true;
                    ApplyVisibility(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
                }
            }
            else if (cursorServer != null)
            {
                HostGameMode effective = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
                if (!lastAppliedMode.HasValue || effective != lastAppliedMode.Value)
                    ApplyVisibility(effective);
            }
        }

        void OnLocalRoleChanged(PlayerRole role)
        {
            if (LocalPlayerRole.IsKaplao())
            {
                SyncBinding();
                return;
            }

            UnsubscribeFromCursorServer();
            ForceRunnerVisibility();
            lastWasKaplao = false;
        }

        void Unsubscribe()
        {
            UnsubscribeFromCursorServer();

            if (localPlayer != null)
            {
                localPlayer.RoleChanged -= OnLocalRoleChanged;
                localPlayer = null;
            }

            ForceRunnerVisibility();
            lastWasKaplao = false;
        }

        void UnsubscribeFromCursorServer()
        {
            if (cursorServer != null && subscribedMode)
                cursorServer.HostGameModeChanged -= OnModeChanged;

            KaplaoLocalGameModePredictor.Changed -= OnPredictedModeChanged;

            cursorServer = null;
            subscribedMode = false;
        }

        void OnModeChanged(HostGameMode mode)
        {
            ApplyVisibility(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
        }

        void OnPredictedModeChanged(HostGameMode mode)
        {
            ApplyVisibility(mode);
        }

        void ApplyVisibility(HostGameMode mode)
        {
            HostGameModeState.Current = mode;
            lastAppliedMode = mode;
            bool createMode = mode == HostGameMode.Create;
            ForEachTile(tile =>
            {
                tile.SetGroupLabelsVisible(createMode);
                tile.SetTriggerTileVisible(createMode);
            });
        }

        void ForceRunnerVisibility()
        {
            HostGameModeState.Current = HostGameMode.Usual;
            lastAppliedMode = HostGameMode.Usual;
            ForEachTile(tile =>
            {
                tile.SetGroupLabelsVisible(false);
                tile.SetTriggerTileVisible(false);
            });
        }

        static void ForEachTile(System.Action<LevelTileServer> action)
        {
            if (LevelGridServer.Instance != null)
            {
                LevelGridServer.Instance.ForEachTile(action);
                return;
            }

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                action(tiles[i]);
        }
    }
}
