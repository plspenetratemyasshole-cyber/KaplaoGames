using System.Collections.Generic;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local Kaplao visuals for Create mode: marquee rectangle and tile selection outlines.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KaplaoCursorServer))]
    [AddComponentMenu("Kaplao/Client/Tile Selection Visuals")]
    public class TileSelectionVisuals : NetworkBehaviour
    {
        [SerializeField] Color marqueeColor = new Color(0.2f, 0.95f, 0.25f, 0.25f);
        [SerializeField] int marqueeSortingOrder = 198;

        KaplaoCursorServer cursorServer;
        KaplaoCursorClient cursorClient;
        SpriteRenderer marqueeRenderer;
        readonly Dictionary<LevelTileServer, bool> outlineState = new();

        void Awake()
        {
            cursorServer = GetComponent<KaplaoCursorServer>();
            cursorClient = GetComponent<KaplaoCursorClient>();
            EnsureMarqueeVisual();
            enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            enabled = true;
            cursorServer.SelectionChanged += RefreshSelectionOutlines;
            KaplaoLocalGameModePredictor.Changed += OnGameModePredicted;
            RefreshSelectionOutlines();
        }

        public override void OnStopLocalPlayer()
        {
            enabled = false;
            if (cursorServer != null)
                cursorServer.SelectionChanged -= RefreshSelectionOutlines;

            KaplaoLocalGameModePredictor.Changed -= OnGameModePredicted;

            ForceClearSelectionOutlines();
            SetMarqueeVisible(false);
        }

        void OnGameModePredicted(HostGameMode _) => RefreshSelectionOutlines();

        void Update()
        {
            if (!isLocalPlayer)
                return;

            UpdateMarquee();
        }

        void UpdateMarquee()
        {
            if (!KaplaoLocalGameModePredictor.IsCreate(cursorServer) ||
                cursorClient == null ||
                !cursorClient.IsSelectionDragging ||
                !cursorClient.ShiftHeld)
            {
                SetMarqueeVisible(false);
                return;
            }

            Vector2 start = cursorClient.SelectionDragStart;
            Vector2 end = cursorClient.SelectionDragEnd;
            ShowMarquee(start, end);
        }

        public void ForceClearSelectionOutlines()
        {
            ClearAllOutlines();
        }

        void RefreshSelectionOutlines()
        {
            if (!isLocalPlayer)
                return;

            var selected = new HashSet<uint>();
            if (cursorServer != null)
            {
                for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
                    selected.Add(cursorServer.SelectedTiles[i]);
            }
            if (selected.Count == 0)
            {
                ClearAllOutlines();
                return;
            }

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            var nextState = new Dictionary<LevelTileServer, bool>();

            for (int i = 0; i < tiles.Length; i++)
            {
                LevelTileServer tile = tiles[i];
                if (tile == null)
                    continue;

                bool shouldHighlight = selected.Contains(tile.netId);
                tile.SetSelectionOutline(shouldHighlight);
                nextState[tile] = shouldHighlight;
            }

            foreach (KeyValuePair<LevelTileServer, bool> entry in outlineState)
            {
                if (entry.Key == null || nextState.ContainsKey(entry.Key))
                    continue;
                entry.Key.SetSelectionOutline(false);
            }

            outlineState.Clear();
            foreach (KeyValuePair<LevelTileServer, bool> entry in nextState)
                outlineState[entry.Key] = entry.Value;
        }

        void ClearAllOutlines()
        {
            foreach (KeyValuePair<LevelTileServer, bool> entry in outlineState)
            {
                if (entry.Key != null)
                    entry.Key.SetSelectionOutline(false);
            }

            outlineState.Clear();

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null)
                    tiles[i].SetSelectionOutline(false);
            }
        }

        void EnsureMarqueeVisual()
        {
            if (marqueeRenderer != null)
                return;

            GameObject marqueeObject = new GameObject("SelectionMarquee");
            marqueeObject.transform.SetParent(null, true);
            marqueeRenderer = marqueeObject.AddComponent<SpriteRenderer>();
            marqueeRenderer.sprite = CreateWhiteSprite();
            marqueeRenderer.color = marqueeColor;
            marqueeRenderer.sortingOrder = marqueeSortingOrder;
            marqueeRenderer.enabled = false;
        }

        void ShowMarquee(Vector2 worldMin, Vector2 worldMax)
        {
            EnsureMarqueeVisual();
            if (marqueeRenderer == null)
                return;

            float minX = Mathf.Min(worldMin.x, worldMax.x);
            float maxX = Mathf.Max(worldMin.x, worldMax.x);
            float minY = Mathf.Min(worldMin.y, worldMax.y);
            float maxY = Mathf.Max(worldMin.y, worldMax.y);

            Vector2 size = new Vector2(Mathf.Max(maxX - minX, 0.05f), Mathf.Max(maxY - minY, 0.05f));
            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

            marqueeRenderer.transform.position = new Vector3(center.x, center.y, 0f);
            marqueeRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            marqueeRenderer.enabled = true;
        }

        void SetMarqueeVisible(bool visible)
        {
            if (marqueeRenderer != null)
                marqueeRenderer.enabled = visible;
        }

        static Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        void OnDestroy()
        {
            if (marqueeRenderer != null)
                Destroy(marqueeRenderer.gameObject);
        }
    }
}
