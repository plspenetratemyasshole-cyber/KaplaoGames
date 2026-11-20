using System.Collections.Generic;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class KeyframePathVisualizer : MonoBehaviour
    {
        [SerializeField] Material lineMaterial;
        [SerializeField] float lineWidth = 0.08f;
        [SerializeField] Color lineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        readonly Dictionary<int, LineRenderer> linesByGroup = new();
        readonly List<KeyframeAnimator.KeyframeSample> sampleBuffer = new();
        readonly List<Vector3> pointBuffer = new();

        void LateUpdate()
        {
            if (!ShouldShow())
            {
                HideAll();
                return;
            }

            DrawAllPaths();
        }

        static bool ShouldShow()
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return false;

            if (!NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer np) || np.role != PlayerRole.Kaplao)
                return false;

            KaplaoCursorServer cursor = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
            return cursor != null && KaplaoLocalGameModePredictor.IsCreate(cursor);
        }

        void DrawAllPaths()
        {
            var grouped = new Dictionary<int, List<(int id, long createdAt, uint netId, KeyframeAnimator.KeyframeSample sample)>>();
            EnumerateTiles(tile =>
            {
                if (tile == null || tile.TileType != TileType.Keyframe || !tile.TryGetComponent(out KeyframeTile kf))
                    return;

                LevelEaseUtility.TryParse(kf.Anim, out LevelEase ease);
                var sample = new KeyframeAnimator.KeyframeSample
                {
                    Position = tile.Position,
                    Rotation = tile.PlacementRotationDegrees,
                    Scale = tile.Scale,
                    Duration = kf.Duration,
                    Ease = ease,
                    Curve = kf.Curve
                };

                uint netId = tile.netIdentity != null ? tile.netIdentity.netId : 0u;
                IReadOnlyList<int> groups = tile.CachedGroupIds;
                for (int i = 0; i < groups.Count; i++)
                {
                    int group = groups[i];
                    if (!grouped.TryGetValue(group, out var list))
                    {
                        list = new List<(int, long, uint, KeyframeAnimator.KeyframeSample)>();
                        grouped[group] = list;
                    }

                    list.Add((kf.SpawnId, tile.CreatedAtUnixMs, netId, sample));
                }
            });

            var used = new HashSet<int>();
            foreach (var entry in grouped)
            {
                entry.Value.Sort((a, b) =>
                {
                    int cmp = a.id.CompareTo(b.id);
                    if (cmp != 0) return cmp;
                    cmp = a.createdAt.CompareTo(b.createdAt);
                    if (cmp != 0) return cmp;
                    return a.netId.CompareTo(b.netId);
                });
                if (entry.Value.Count < 2) continue;

                used.Add(entry.Key);
                sampleBuffer.Clear();
                for (int i = 0; i < entry.Value.Count; i++)
                    sampleBuffer.Add(entry.Value[i].sample);

                bool closeLoop = KeyframeAnimator.GetCloseLoopForGroup(entry.Key);
                KeyframeAnimator.BuildPathPoints(sampleBuffer, pointBuffer, closeLoop);
                LineRenderer line = GetOrCreateLine(entry.Key);
                line.positionCount = pointBuffer.Count;
                line.SetPositions(pointBuffer.ToArray());
                line.gameObject.SetActive(true);
            }

            foreach (KeyValuePair<int, LineRenderer> pair in linesByGroup)
            {
                if (!used.Contains(pair.Key))
                    pair.Value.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// LevelGridServer tile registry is server-populated; clients must scan spawned tiles.
        /// </summary>
        static void EnumerateTiles(System.Action<LevelTileServer> callback)
        {
            if (callback == null)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null && grid.TileCount > 0)
            {
                grid.ForEachTile(callback);
                return;
            }

            if (!NetworkClient.active)
                return;

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity != null && identity.TryGetComponent(out LevelTileServer tile))
                    callback(tile);
            }
        }

        LineRenderer GetOrCreateLine(int group)
        {
            if (linesByGroup.TryGetValue(group, out LineRenderer existing) && existing != null)
                return existing;

            GameObject go = new GameObject($"KeyframePath_{group}");
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.sortingOrder = 50;
            linesByGroup[group] = line;
            return line;
        }

        void HideAll()
        {
            foreach (LineRenderer line in linesByGroup.Values)
            {
                if (line != null) line.gameObject.SetActive(false);
            }
        }
    }
}
