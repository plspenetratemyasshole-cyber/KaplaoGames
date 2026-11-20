using System.Collections;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Refreshes tile appearance when editor layer / Create mode changes (client-only).
    /// Does NOT scan all tiles every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class EditorLayerVisuals : MonoBehaviour
    {
        KaplaoCursorServer localCursor;
        bool subscribed;

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
                TryBindCursor();
                yield return wait;
            }
        }

        void TryBindCursor()
        {
            if (NetworkClient.localPlayer == null ||
                !NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursor))
            {
                Unsubscribe();
                return;
            }

            if (localCursor == cursor)
                return;

            Unsubscribe();
            localCursor = cursor;
            localCursor.ActiveEditorLayerChanged += RefreshAllTiles;
            localCursor.HostGameModeChanged += OnHostGameModeChanged;
            KaplaoLocalGameModePredictor.Changed += OnPredictedGameModeChanged;
            subscribed = true;

            if (KaplaoLocalGameModePredictor.IsCreate(localCursor))
                RefreshAllTiles();
        }

        void Unsubscribe()
        {
            if (localCursor != null && subscribed)
            {
                localCursor.ActiveEditorLayerChanged -= RefreshAllTiles;
                localCursor.HostGameModeChanged -= OnHostGameModeChanged;
            }

            KaplaoLocalGameModePredictor.Changed -= OnPredictedGameModeChanged;

            subscribed = false;
            localCursor = null;
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            if (KaplaoLocalGameModePredictor.IsCreate(localCursor))
                RefreshAllTiles();
        }

        void OnPredictedGameModeChanged(HostGameMode mode)
        {
            if (mode == HostGameMode.Create)
                RefreshAllTiles();
        }

        void RefreshAllTiles()
        {
            if (localCursor == null || !KaplaoLocalGameModePredictor.IsCreate(localCursor))
                return;

            if (LevelGridServer.Instance != null)
            {
                LevelGridServer.Instance.ForEachTile(tile => tile.RefreshClientAppearance());
                return;
            }

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].RefreshClientAppearance();
        }
    }
}
