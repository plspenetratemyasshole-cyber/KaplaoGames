using System;
using System.Collections.Generic;
using KaplaoGames.Shared;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Editor-only selection link groups. Linked tiles are always selected together.
    /// Persisted in level JSON by tile world position.
    /// </summary>
    public sealed class TileSelectionLinks
    {
        readonly List<HashSet<TilePositionKey>> groups = new();
        readonly Dictionary<TilePositionKey, int> tileToGroupIndex = new();

        public void Clear()
        {
            groups.Clear();
            tileToGroupIndex.Clear();
        }

        public void RegisterTile(LevelTileServer tile)
        {
            if (tile == null)
                return;

            TilePositionKey key = TilePositionKey.FromTile(tile);
            if (!tileToGroupIndex.TryGetValue(key, out int groupIndex))
                return;

            if (groupIndex < 0 || groupIndex >= groups.Count)
            {
                tileToGroupIndex.Remove(key);
                return;
            }

            groups[groupIndex].Add(key);
        }

        public void UnregisterTile(LevelTileServer tile)
        {
            if (tile == null)
                return;

            TilePositionKey key = TilePositionKey.FromTile(tile);
            if (!tileToGroupIndex.TryGetValue(key, out int groupIndex))
                return;

            tileToGroupIndex.Remove(key);
            if (groupIndex >= 0 && groupIndex < groups.Count)
                groups[groupIndex].Remove(key);
        }

        /// <summary>
        /// Keeps link groups valid after a tile moves. Links are keyed by world position.
        /// </summary>
        public void NotifyTileMoved(LevelTileServer tile, Vector2 oldPosition)
        {
            if (tile == null)
                return;

            var oldKey = new TilePositionKey(oldPosition.x, oldPosition.y);
            TilePositionKey newKey = TilePositionKey.FromTile(tile);
            if (oldKey.Equals(newKey))
                return;

            if (!tileToGroupIndex.TryGetValue(oldKey, out int groupIndex))
                return;

            if (groupIndex < 0 || groupIndex >= groups.Count)
            {
                tileToGroupIndex.Remove(oldKey);
                return;
            }

            tileToGroupIndex.Remove(oldKey);
            groups[groupIndex].Remove(oldKey);

            // Another linked key may already occupy the destination (stacked tiles).
            if (tileToGroupIndex.ContainsKey(newKey))
            {
                if (groups[groupIndex].Count < 2)
                    RemoveGroupAt(groupIndex);
                return;
            }

            groups[groupIndex].Add(newKey);
            tileToGroupIndex[newKey] = groupIndex;

            if (groups[groupIndex].Count < 2)
                RemoveGroupAt(groupIndex);
        }

        public void LinkTiles(IEnumerable<LevelTileServer> tiles)
        {
            var keys = new HashSet<TilePositionKey>();
            foreach (LevelTileServer tile in tiles)
            {
                if (tile != null)
                    keys.Add(TilePositionKey.FromTile(tile));
            }

            if (keys.Count < 2)
                return;

            var merged = new HashSet<TilePositionKey>();
            var touchedGroups = new HashSet<int>();

            foreach (TilePositionKey key in keys)
            {
                merged.Add(key);
                if (tileToGroupIndex.TryGetValue(key, out int groupIndex))
                    touchedGroups.Add(groupIndex);
            }

            foreach (int groupIndex in touchedGroups)
            {
                if (groupIndex < 0 || groupIndex >= groups.Count)
                    continue;

                foreach (TilePositionKey existing in groups[groupIndex])
                    merged.Add(existing);
            }

            foreach (int groupIndex in touchedGroups)
                RemoveGroupAt(groupIndex);

            int newIndex = groups.Count;
            groups.Add(merged);
            foreach (TilePositionKey key in merged)
                tileToGroupIndex[key] = newIndex;
        }

        public void UnlinkTiles(IEnumerable<LevelTileServer> tiles)
        {
            var affectedGroups = new HashSet<int>();
            foreach (LevelTileServer tile in tiles)
            {
                if (tile == null)
                    continue;

                TilePositionKey key = TilePositionKey.FromTile(tile);
                if (tileToGroupIndex.TryGetValue(key, out int groupIndex))
                    affectedGroups.Add(groupIndex);
            }

            foreach (int groupIndex in affectedGroups)
            {
                if (groupIndex < 0 || groupIndex >= groups.Count)
                    continue;

                HashSet<TilePositionKey> group = groups[groupIndex];
                foreach (LevelTileServer tile in tiles)
                {
                    if (tile == null)
                        continue;

                    TilePositionKey key = TilePositionKey.FromTile(tile);
                    group.Remove(key);
                    tileToGroupIndex.Remove(key);
                }

                if (group.Count < 2)
                    RemoveGroupAt(groupIndex);
            }
        }

        public void CollectLinkedNetIds(uint netId, LevelGridServer grid, HashSet<uint> output)
        {
            if (grid == null || output == null)
                return;

            if (!grid.TryGetTileByNetId(netId, out LevelTileServer tile))
                return;

            TilePositionKey key = TilePositionKey.FromTile(tile);
            if (!tileToGroupIndex.TryGetValue(key, out int groupIndex))
            {
                output.Add(netId);
                return;
            }

            if (groupIndex < 0 || groupIndex >= groups.Count)
            {
                output.Add(netId);
                return;
            }

            foreach (TilePositionKey member in groups[groupIndex])
            {
                if (grid.TryGetTileByPosition(member.X, member.Y, out LevelTileServer memberTile))
                    output.Add(memberTile.netId);
            }

            if (output.Count == 0)
                output.Add(netId);
        }

        public TileLinkGroupRecord[] ExportRecords()
        {
            var records = new List<TileLinkGroupRecord>();
            for (int i = 0; i < groups.Count; i++)
            {
                HashSet<TilePositionKey> group = groups[i];
                if (group.Count < 2)
                    continue;

                var positions = new TilePositionRecord[group.Count];
                int index = 0;
                foreach (TilePositionKey key in group)
                {
                    positions[index++] = new TilePositionRecord { x = key.X, y = key.Y };
                }

                records.Add(new TileLinkGroupRecord { tiles = positions });
            }

            return records.ToArray();
        }

        public void ImportRecords(TileLinkGroupRecord[] records)
        {
            Clear();
            if (records == null)
                return;

            for (int i = 0; i < records.Length; i++)
            {
                TilePositionRecord[] tiles = records[i].tiles;
                if (tiles == null || tiles.Length < 2)
                    continue;

                var group = new HashSet<TilePositionKey>();
                for (int t = 0; t < tiles.Length; t++)
                {
                    var key = new TilePositionKey(tiles[t].x, tiles[t].y);
                    group.Add(key);
                }

                if (group.Count < 2)
                    continue;

                int groupIndex = groups.Count;
                groups.Add(group);
                foreach (TilePositionKey key in group)
                    tileToGroupIndex[key] = groupIndex;
            }
        }

        void RemoveGroupAt(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= groups.Count)
                return;

            foreach (TilePositionKey key in groups[groupIndex])
                tileToGroupIndex.Remove(key);

            groups.RemoveAt(groupIndex);

            var keysToUpdate = new List<TilePositionKey>(tileToGroupIndex.Keys);
            for (int i = 0; i < keysToUpdate.Count; i++)
            {
                TilePositionKey key = keysToUpdate[i];
                if (tileToGroupIndex[key] > groupIndex)
                    tileToGroupIndex[key]--;
            }
        }

        readonly struct TilePositionKey : IEquatable<TilePositionKey>
        {
            public float X { get; }
            public float Y { get; }

            public TilePositionKey(float x, float y)
            {
                X = Mathf.Round(x * 100f) * 0.01f;
                Y = Mathf.Round(y * 100f) * 0.01f;
            }

            public static TilePositionKey FromTile(LevelTileServer tile) =>
                new TilePositionKey(tile.Position.x, tile.Position.y);

            public bool Equals(TilePositionKey other) => X == other.X && Y == other.Y;

            public override bool Equals(object obj) => obj is TilePositionKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(X, Y);
        }
    }
}
