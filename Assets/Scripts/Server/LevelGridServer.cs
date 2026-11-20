using System;
using System.Collections.Generic;
using System.Linq;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Server-authoritative grid of placed blocks and spikes.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelGridServer : MonoBehaviour
    {
        static LevelGridServer instance;

        public static LevelGridServer Instance => instance;

        [SerializeField] GameObject blockPrefab;
        [SerializeField] GameObject spikePrefab;
        [SerializeField] GameObject triggerPrefab;
        [SerializeField] GameObject particlePrefab;
        [SerializeField] GameObject keyframePrefab;
        [SerializeField] GameObject executorPrefab;

        public GameObject BlockPrefab => blockPrefab;
        public GameObject SpikePrefab => spikePrefab;
        public GameObject TriggerPrefab => triggerPrefab;
        public GameObject ParticlePrefab => particlePrefab;
        public GameObject KeyframePrefab => keyframePrefab;
        public GameObject ExecutorPrefab => executorPrefab;

        readonly Dictionary<uint, GameObject> tiles = new();
        readonly Dictionary<int, float> observerResyncCooldownUntil = new();
        readonly Dictionary<int, List<LevelTileServer>> tilesByGroup = new();
        readonly TileSelectionLinks selectionLinks = new();

        public TileSelectionLinks SelectionLinks => selectionLinks;

        const float ObserverResyncCooldownSeconds = 1.5f;

        int GetNextKeyframeSpawnId()
        {
            int maxId = -1;
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out KeyframeTile keyframe))
                    continue;
                if (keyframe.SpawnId > maxId)
                    maxId = keyframe.SpawnId;
            }

            return maxId + 1;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[LevelGridServer] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            TryAssignMissingPrefabsFromNetworkManager();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        void TryAssignMissingPrefabsFromNetworkManager()
        {
            KaplaoNetworkManager manager = KaplaoNetworkManager.Instance;
            if (manager == null)
                return;

            if (blockPrefab == null) blockPrefab = manager.blockTilePrefab;
            if (spikePrefab == null) spikePrefab = manager.spikeTilePrefab;
            if (triggerPrefab == null) triggerPrefab = manager.triggerTilePrefab;
            if (particlePrefab == null) particlePrefab = manager.particleTilePrefab;
        }

        public bool TryGetTile(Vector2Int cell, out LevelTileServer tile)
        {
            tile = null;
            float bestDistance = float.MaxValue;
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer candidate))
                    continue;

                if (candidate.Cell != cell)
                    continue;

                float dist = ((Vector2)candidate.transform.position - (Vector2)GridMath.CellToWorld(cell)).sqrMagnitude;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    tile = candidate;
                }
            }

            return tile != null;
        }

        public bool HasTileAtCellAndLayer(Vector2Int cell, int editorLayer, uint excludeNetId = 0)
        {
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer candidate))
                    continue;

                if (candidate.Cell != cell || candidate.EditorLayer != editorLayer)
                    continue;

                if (excludeNetId != 0 && candidate.netId == excludeNetId)
                    continue;

                return true;
            }

            return false;
        }

        public bool TryGetTileByPosition(float x, float y, out LevelTileServer tile)
        {
            tile = null;
            float roundedX = Mathf.Round(x * 100f) * 0.01f;
            float roundedY = Mathf.Round(y * 100f) * 0.01f;
            float bestDistance = 0.01f;

            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer candidate))
                    continue;

                Vector2 position = candidate.Position;
                float dx = Mathf.Abs(position.x - roundedX);
                float dy = Mathf.Abs(position.y - roundedY);
                if (dx > bestDistance || dy > bestDistance)
                    continue;

                tile = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetTileByNetId(uint netId, out LevelTileServer tile)
        {
            tile = null;
            if (!tiles.TryGetValue(netId, out GameObject tileObject) || tileObject == null)
                return false;

            return tileObject.TryGetComponent(out tile);
        }

        public List<LevelTileServer> GetTilesInWorldRect(Vector2 worldMin, Vector2 worldMax)
        {
            float minX = Mathf.Min(worldMin.x, worldMax.x);
            float maxX = Mathf.Max(worldMin.x, worldMax.x);
            float minY = Mathf.Min(worldMin.y, worldMax.y);
            float maxY = Mathf.Max(worldMin.y, worldMax.y);

            var result = new List<LevelTileServer>();
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer tile))
                    continue;

                Vector2 p = tile.Position;
                if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY)
                    result.Add(tile);
            }

            return result;
        }

        public List<Vector2Int> GetCellsInWorldRect(Vector2 worldMin, Vector2 worldMax)
        {
            Vector2Int minCell = GridMath.WorldToCell(worldMin);
            Vector2Int maxCell = GridMath.WorldToCell(worldMax);

            int minX = Mathf.Min(minCell.x, maxCell.x);
            int maxX = Mathf.Max(minCell.x, maxCell.x);
            int minY = Mathf.Min(minCell.y, maxCell.y);
            int maxY = Mathf.Max(minCell.y, maxCell.y);

            var result = new List<Vector2Int>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (TryGetTile(cell, out _))
                        result.Add(cell);
                }
            }

            return result;
        }

        public HashSet<int> CollectAllGroups()
        {
            var groups = new HashSet<int>();
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer tile))
                    continue;

                TileGroups.AddNumbers(groups, TileGroups.ParseNumbers(tile.GroupsDisplay));
            }

            return groups;
        }

        public string CollectGroupsFromCells(IEnumerable<Vector2Int> cells)
        {
            var groups = new HashSet<int>();
            foreach (Vector2Int cell in cells)
            {
                if (!TryGetTile(cell, out LevelTileServer tile))
                    continue;

                TileGroups.AddNumbers(groups, TileGroups.ParseNumbers(tile.GroupsDisplay));
            }

            return TileGroups.Join(groups);
        }

        /// <returns>Spawned tile netId, or 0 on failure.</returns>
        public uint TryPlaceTile(
            TileType type,
            Vector2 position,
            float rotationDegrees,
            Color32 color,
            string groups = "",
            byte quarterTurns = 0,
            float transparency = 1f,
            bool noHitbox = false,
            string textureId = "",
            int sortingOrder = 0,
            int editorLayer = 0)
        {
            if (!NetworkServer.active || type == TileType.None)
                return 0;

            TryAssignMissingPrefabsFromNetworkManager();

            GameObject prefab = type switch
            {
                TileType.Block => blockPrefab,
                TileType.Spike => spikePrefab,
                TileType.Trigger => triggerPrefab,
                TileType.Particle => particlePrefab,
                TileType.Keyframe => keyframePrefab != null ? keyframePrefab : triggerPrefab,
                TileType.Executor => executorPrefab != null ? executorPrefab : triggerPrefab,
                _ => null
            };
            if (prefab == null)
            {
                if (type == TileType.Particle)
                    Debug.LogWarning("[LevelGridServer] ParticleTile prefab is not assigned. Run Kaplao/Setup Level System.");
                else
                    Debug.LogWarning("[LevelGridServer] Tile prefab is not assigned.");
                return 0;
            }

            GameObject tileObject = Instantiate(prefab, new Vector3(position.x, position.y, 0f), Quaternion.Euler(0f, 0f, rotationDegrees));

            if (tileObject.TryGetComponent(out LevelTileServer tileServer))
                tileServer.ServerInit(position, rotationDegrees, type, color, groups, quarterTurns, transparency, noHitbox, textureId: textureId, sortingOrder: sortingOrder, editorLayer: editorLayer);

            if (type == TileType.Keyframe)
            {
                if (!tileObject.TryGetComponent(out KeyframeTile keyframeTile))
                    keyframeTile = tileObject.AddComponent<KeyframeTile>();
                keyframeTile.ServerApply(new KeyframeRecord
                {
                    duration = 1f,
                    anim = "linear",
                    spawnId = GetNextKeyframeSpawnId()
                });
            }

            if (type == TileType.Executor)
            {
                if (!tileObject.TryGetComponent(out ExecutorTile executorTile))
                    executorTile = tileObject.AddComponent<ExecutorTile>();
                executorTile.ServerApplyDefaults();
            }

            NetworkServer.Spawn(tileObject);
            if (tileObject.TryGetComponent(out NetworkIdentity identity))
            {
                RegisterExistingTile(tileObject);
                return identity.netId;
            }

            return 0;
        }

        public void RegisterExistingTile(GameObject tileObject)
        {
            if (tileObject == null || !tileObject.TryGetComponent(out NetworkIdentity identity))
                return;

            tiles[identity.netId] = tileObject;
            if (tileObject.TryGetComponent(out LevelTileServer tile))
            {
                selectionLinks.RegisterTile(tile);
                AddTileToGroupIndex(tile);
            }
        }

        public int TileCount => tiles.Count;

        public void ForEachTile(System.Action<LevelTileServer> callback)
        {
            if (callback == null)
                return;

            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject != null && tileObject.TryGetComponent(out LevelTileServer tile))
                    callback(tile);
            }
        }

        public void CollectTilesWithGroup(int group, List<LevelTileServer> output)
        {
            if (output == null)
                return;

            output.Clear();
            if (!tilesByGroup.TryGetValue(group, out List<LevelTileServer> bucket) || bucket == null)
                return;

            for (int i = 0; i < bucket.Count; i++)
            {
                LevelTileServer tile = bucket[i];
                if (tile != null)
                    output.Add(tile);
            }
        }

        public void NotifyTileGroupsChanged(LevelTileServer tile, string oldGroups, string newGroups)
        {
            if (tile == null)
                return;

            RemoveTileFromParsedGroups(tile, oldGroups);
            AddTileToParsedGroups(tile, newGroups);
            LevelScriptTriggerBridge.Instance?.InvalidateWatchCache();
        }

        void AddTileToGroupIndex(LevelTileServer tile)
        {
            if (tile == null)
                return;

            AddTileToParsedGroups(tile, tile.GroupsDisplay);
        }

        void RemoveTileFromGroupIndex(LevelTileServer tile)
        {
            if (tile == null)
                return;

            RemoveTileFromParsedGroups(tile, tile.GroupsDisplay);
        }

        void AddTileToParsedGroups(LevelTileServer tile, string groupsCsv)
        {
            int[] groups = TileGroups.ParseNumbers(groupsCsv);
            for (int i = 0; i < groups.Length; i++)
            {
                int group = groups[i];
                if (!tilesByGroup.TryGetValue(group, out List<LevelTileServer> bucket))
                {
                    bucket = new List<LevelTileServer>(8);
                    tilesByGroup[group] = bucket;
                }

                if (!bucket.Contains(tile))
                    bucket.Add(tile);
            }
        }

        void RemoveTileFromParsedGroups(LevelTileServer tile, string groupsCsv)
        {
            int[] groups = TileGroups.ParseNumbers(groupsCsv);
            for (int i = 0; i < groups.Length; i++)
            {
                if (!tilesByGroup.TryGetValue(groups[i], out List<LevelTileServer> bucket) || bucket == null)
                    continue;

                bucket.Remove(tile);
                if (bucket.Count == 0)
                    tilesByGroup.Remove(groups[i]);
            }
        }

        public void LinkSelectedTiles(IEnumerable<uint> netIds)
        {
            var tilesToLink = new List<LevelTileServer>();
            foreach (uint netId in netIds)
            {
                if (TryGetTileByNetId(netId, out LevelTileServer tile))
                    tilesToLink.Add(tile);
            }

            selectionLinks.LinkTiles(tilesToLink);
        }

        public void UnlinkSelectedTiles(IEnumerable<uint> netIds)
        {
            var tilesToUnlink = new List<LevelTileServer>();
            foreach (uint netId in netIds)
            {
                if (TryGetTileByNetId(netId, out LevelTileServer tile))
                    tilesToUnlink.Add(tile);
            }

            selectionLinks.UnlinkTiles(tilesToUnlink);
        }

        public void ExpandSelectionWithLinks(uint netId, HashSet<uint> output)
        {
            selectionLinks.CollectLinkedNetIds(netId, this, output);
        }

        public void NotifyTilePositionChanged(LevelTileServer tile, Vector2 oldPosition)
        {
            if (tile == null)
                return;

            selectionLinks.NotifyTileMoved(tile, oldPosition);
        }

        public uint TryPlaceTile(
            TileType type,
            Vector2Int cell,
            Color32 color,
            string groups = "",
            byte quarterTurns = 0,
            float transparency = 1f,
            bool noHitbox = false,
            int editorLayer = 0)
        {
            Vector2 position = GridMath.TileCellToWorld(cell, type, quarterTurns);
            float rotationDegrees = type == TileType.Spike ? -quarterTurns * 90f : 0f;
            return TryPlaceTile(type, position, rotationDegrees, color, groups, quarterTurns, transparency, noHitbox, editorLayer: editorLayer);
        }

        [Server]
        public void ServerResyncTileObserversForConnection(NetworkConnectionToClient conn)
        {
            if (conn == null || !conn.isReady)
                return;

            if (observerResyncCooldownUntil.TryGetValue(conn.connectionId, out float cooldownUntil) &&
                Time.unscaledTime < cooldownUntil)
            {
                return;
            }

            observerResyncCooldownUntil[conn.connectionId] = Time.unscaledTime + ObserverResyncCooldownSeconds;

            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out NetworkIdentity identity))
                    continue;

                KaplaoNetworkObserverUtil.ForceResyncIdentityForConnection(identity, conn);
            }
        }

        public bool TryRemoveTile(Vector2Int cell)
        {
            if (!NetworkServer.active)
                return false;

            if (!TryGetTile(cell, out LevelTileServer tile) || tile == null)
                return false;

            GameObject tileObject = tile.gameObject;
            if (!tileObject.TryGetComponent(out NetworkIdentity identity))
                return false;

            selectionLinks.UnregisterTile(tile);
            RemoveTileFromGroupIndex(tile);

            NetworkServer.Destroy(tileObject);
            tiles.Remove(identity.netId);
            return true;
        }

        public bool TryRemoveTileByNetId(uint netId)
        {
            if (!NetworkServer.active)
                return false;

            if (!tiles.TryGetValue(netId, out GameObject tileObject) || tileObject == null)
                return false;

            if (tileObject.TryGetComponent(out LevelTileServer tile))
            {
                selectionLinks.UnregisterTile(tile);
                RemoveTileFromGroupIndex(tile);
            }

            NetworkServer.Destroy(tileObject);
            tiles.Remove(netId);
            return true;
        }

        public int TryRemoveTiles(IEnumerable<Vector2Int> cells)
        {
            int removed = 0;
            foreach (Vector2Int cell in cells)
            {
                if (TryRemoveTile(cell))
                    removed++;
            }

            return removed;
        }

        public int TryRemoveTilesByNetIds(IEnumerable<uint> netIds)
        {
            int removed = 0;
            foreach (uint netId in netIds)
            {
                if (TryRemoveTileByNetId(netId))
                    removed++;
            }

            return removed;
        }

        public int TrySetTilesColor(IEnumerable<Vector2Int> cells, Color32 color, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (Vector2Int cell in cells)
            {
                if (!TryGetTile(cell, out LevelTileServer tile))
                    continue;

                tile.ServerSetColor(color, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesColorByNetIds(IEnumerable<uint> netIds, Color32 color, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                tile.ServerSetColor(color, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesTextureByNetIds(IEnumerable<uint> netIds, string textureId, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                if (tile.TileType == TileType.Particle)
                {
                    tile.ServerSetTexture(textureId, updateBaseState);
                    if (tile.TryGetComponent(out ParticleEmitterTile emitter))
                        emitter.ServerSetTexture(textureId);
                    updated++;
                    continue;
                }

                if (tile.TileType != TileType.Block && tile.TileType != TileType.Spike)
                    continue;

                tile.ServerSetTexture(textureId, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TryReplaceTextureIdEverywhere(string oldTextureId, string newTextureId)
        {
            if (string.IsNullOrEmpty(oldTextureId) || string.IsNullOrEmpty(newTextureId))
                return 0;

            int updated = 0;
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer tile))
                    continue;

                if (!string.Equals(tile.TextureId, oldTextureId, StringComparison.OrdinalIgnoreCase))
                    continue;

                tile.ServerSetTexture(newTextureId);
                if (tile.TryGetComponent(out ParticleEmitterTile emitter))
                    emitter.ServerSetTexture(newTextureId);

                updated++;
            }

            return updated;
        }

        public int TrySetTilesSortingOrderByNetIds(IEnumerable<uint> netIds, int sortingOrder)
        {
            int updated = 0;
            int clamped = Mathf.Clamp(sortingOrder, -10, 10);
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                tile.ServerSetSortingOrder(clamped);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesGroups(IEnumerable<Vector2Int> cells, string groups)
        {
            string normalized = TileGroups.Normalize(groups);
            int updated = 0;
            foreach (Vector2Int cell in cells)
            {
                if (!TryGetTile(cell, out LevelTileServer tile))
                    continue;

                tile.ServerSetGroups(normalized);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesGroupsByNetIds(IEnumerable<uint> netIds, string groups)
        {
            string normalized = TileGroups.Normalize(groups);
            int updated = 0;
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                tile.ServerSetGroups(normalized);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesTransparency(IEnumerable<Vector2Int> cells, float transparency, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (Vector2Int cell in cells)
            {
                if (!TryGetTile(cell, out LevelTileServer tile))
                    continue;

                tile.ServerSetTransparency(transparency, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesTransparencyByNetIds(IEnumerable<uint> netIds, float transparency, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                tile.ServerSetTransparency(transparency, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesEditorLayerByNetIds(IEnumerable<uint> netIds, int editorLayer)
        {
            int updated = 0;
            int clamped = Mathf.Clamp(editorLayer, 0, 100);
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                tile.ServerSetEditorLayer(clamped);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesNoHitbox(IEnumerable<Vector2Int> cells, bool noHitbox, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (Vector2Int cell in cells)
            {
                if (!TryGetTile(cell, out LevelTileServer tile))
                    continue;

                tile.ServerSetNoHitbox(noHitbox, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TrySetTilesNoHitboxByNetIds(IEnumerable<uint> netIds, bool noHitbox, bool updateBaseState = true)
        {
            int updated = 0;
            foreach (uint netId in netIds)
            {
                if (!TryGetTileByNetId(netId, out LevelTileServer tile))
                    continue;

                tile.ServerSetNoHitbox(noHitbox, updateBaseState);
                updated++;
            }

            return updated;
        }

        public int TrySetGroupVisibility(int group, bool visible)
        {
            if (!NetworkServer.active)
                return 0;

            int updated = 0;
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer tile))
                    continue;

                if (!tile.HasGroup(group))
                    continue;

                tile.ServerSetTransparency(visible ? 1f : 0f, updateBaseState: false);
                updated++;
            }

            return updated;
        }

        public int TrySetGroupHitbox(int group, bool enabled)
        {
            if (!NetworkServer.active)
                return 0;

            int updated = 0;
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer tile))
                    continue;

                if (!tile.HasGroup(group))
                    continue;

                tile.ServerSetNoHitbox(!enabled, updateBaseState: false);
                updated++;
            }

            return updated;
        }

        public LevelData ExportLevelData()
        {
            var records = new List<LevelTileRecord>(tiles.Count);
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null || !tileObject.TryGetComponent(out LevelTileServer tile))
                    continue;

                Color32 color = tile.TileColor;
                ParticleEmitterRecord particle = default;
                KeyframeRecord keyframe = default;
                ExecutorRecord executor = default;
                if (tile.TileType == TileType.Particle &&
                    tileObject.TryGetComponent(out ParticleEmitterTile particleTile))
                {
                    particle = particleTile.ExportRecord();
                }

                if (tile.TileType == TileType.Keyframe &&
                    tileObject.TryGetComponent(out KeyframeTile keyframeTile))
                {
                    keyframe = keyframeTile.ExportRecord();
                }

                if (tile.TileType == TileType.Executor &&
                    tileObject.TryGetComponent(out ExecutorTile executorTile))
                {
                    executor = executorTile.ExportRecord();
                }

                records.Add(new LevelTileRecord
                {
                    x = Mathf.Round(tile.Position.x * 100f) * 0.01f,
                    y = Mathf.Round(tile.Position.y * 100f) * 0.01f,
                    rotationDegrees = Mathf.Round(tile.transform.eulerAngles.z * 100f) * 0.01f,
                    scaleX = Mathf.Round(tile.Scale.x * 100f) * 0.01f,
                    scaleY = Mathf.Round(tile.Scale.y * 100f) * 0.01f,
                    type = tile.TileType,
                    r = color.r,
                    g = color.g,
                    b = color.b,
                    textureId = tile.TextureId ?? string.Empty,
                    sortingOrder = tile.SortingOrder,
                    groups = tile.GroupsDisplay,
                    quarterTurns = tile.QuarterTurns,
                    transparency = tile.Transparency,
                    noHitbox = tile.HasNoHitbox,
                    editorLayer = tile.EditorLayer,
                    particle = particle,
                    createdAt = tile.CreatedAtUnixMs,
                    isSpawnPoint = tile.IsSpawnPoint,
                    keyframe = keyframe,
                    executor = executor
                });
            }

            LevelSessionServer session = LevelSessionServer.Instance;
            Vector2 spawn = session != null ? session.WorldSpawn : new Vector2(0f, 5f);

            return new LevelData
            {
                version = 10,
                // Runtime undo/import snapshots still carry entry script; world save clears this.
                script = session != null ? session.LevelScript ?? string.Empty : string.Empty,
                tiles = records.ToArray(),
                linkGroups = selectionLinks.ExportRecords(),
                worldSpawnX = spawn.x,
                worldSpawnY = spawn.y,
                bannedIds = LevelSessionServer.Instance != null
                    ? LevelSessionServer.Instance.BannedIds.ToArray()
                    : Array.Empty<string>(),
                bannedIps = LevelSessionServer.Instance != null
                    ? LevelSessionServer.Instance.BannedIps.ToArray()
                    : Array.Empty<string>(),
                // Moderators are session-only and are never persisted.
                moderatorIds = Array.Empty<string>()
            };
        }

        public void ImportLevelData(LevelData data)
        {
            if (!NetworkServer.active || data == null)
                return;

            ClearAllTiles();

            LevelTileRecord[] records = data.tiles ?? System.Array.Empty<LevelTileRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                LevelTileRecord record = records[i];
                if (record.type == TileType.None)
                    continue;

                var position = new Vector2(record.x, record.y);
                float scaleX = record.scaleX > 0f ? record.scaleX : 1f;
                float scaleY = record.scaleY > 0f ? record.scaleY : 1f;
                var color = new Color32(record.r, record.g, record.b, 255);
                float transparency = data.version < 7
                    ? (record.invisible ? 0f : 1f)
                    : Mathf.Clamp01(record.transparency);

                uint netId = TryPlaceTile(
                    record.type,
                    position,
                    record.rotationDegrees,
                    color,
                    record.groups ?? string.Empty,
                    record.quarterTurns,
                    transparency,
                    record.noHitbox,
                    record.textureId,
                    record.sortingOrder,
                    record.editorLayer);
                if (netId != 0 && TryGetTileByNetId(netId, out LevelTileServer importedTile))
                {
                    importedTile.ServerSetTransform(position, record.rotationDegrees, new Vector2(scaleX, scaleY), roundValues: false);

                    if (record.type == TileType.Particle &&
                        importedTile.TryGetComponent(out ParticleEmitterTile particleTile))
                    {
                        particleTile.ServerApply(record.particle);
                    }

                    if (record.createdAt > 0)
                        importedTile.ServerSetCreatedAtUnixMs(record.createdAt);

                    if (record.type == TileType.Trigger && record.isSpawnPoint)
                        importedTile.ServerSetSpawnPoint(true);

                    if (record.type == TileType.Keyframe &&
                        importedTile.TryGetComponent(out KeyframeTile keyframeTile))
                    {
                        keyframeTile.ServerApply(record.keyframe);
                    }

                    if (record.type == TileType.Executor &&
                        importedTile.TryGetComponent(out ExecutorTile executorTile))
                    {
                        executorTile.ServerApply(record.executor);
                    }
                }
            }

            selectionLinks.ImportRecords(data.linkGroups);
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject != null && tileObject.TryGetComponent(out LevelTileServer tile))
                    selectionLinks.RegisterTile(tile);
            }

            LevelSessionServer session = LevelSessionServer.Instance;
            if (session != null)
            {
                string script = string.IsNullOrEmpty(data.script) ? string.Empty : data.script;
                if (!string.IsNullOrWhiteSpace(script) || !WorldScriptProjectService.HasProject)
                {
                    string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
                    if (!string.IsNullOrWhiteSpace(worldName))
                    {
                        WorldScriptProjectService.TryEnsureProjectForWorld(worldName, script, out _);
                        if (!string.IsNullOrWhiteSpace(script))
                            WorldScriptProjectService.TryWriteFile(worldName, WorldScriptProjectService.EntryFileName, script, out _);
                    }
                    else if (!WorldScriptProjectService.HasProject)
                    {
                        WorldScriptProjectService.ApplySnapshot(new ScriptProjectSnapshot
                        {
                            nodes = new[]
                            {
                                new ScriptNodeRecord
                                {
                                    path = WorldScriptProjectService.EntryFileName,
                                    isDirectory = false,
                                    content = script
                                }
                            }
                        }, persistDisk: false);
                    }
                }

                session.ServerSetLevelScript(WorldScriptProjectService.GetEntryScriptContent());
                session.ServerBroadcastScriptProject();
                if (data.version >= 9)
                    session.ServerSetWorldSpawn(data.worldSpawnX, data.worldSpawnY);
            }

            // Only bans are persisted. Moderators are always session-only.
            if (data.version >= 8)
            {
                LevelSessionServer.Instance?.ServerSetModerationState(
                    data.bannedIds ?? Array.Empty<string>(),
                    Array.Empty<string>(),
                    data.bannedIps ?? Array.Empty<string>());
            }
        }

        public void ClearAllTiles()
        {
            foreach (GameObject tileObject in tiles.Values)
            {
                if (tileObject == null)
                    continue;

                if (NetworkServer.active)
                    NetworkServer.Destroy(tileObject);
                else
                    Destroy(tileObject);
            }

            tiles.Clear();
            tilesByGroup.Clear();
            selectionLinks.Clear();
        }
    }
}
