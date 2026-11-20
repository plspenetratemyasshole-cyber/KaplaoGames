using System.Collections.Generic;
using KaplaoGames.Server;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
    public static class LevelScriptGroupUtility
    {
        public static bool TryGetGroupCenter(int group, out float x, out float y)
        {
            x = 0f;
            y = 0f;

            var members = new List<LevelTileServer>();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null && grid.TileCount > 0)
            {
                grid.CollectTilesWithGroup(group, members);
            }
            else
            {
                LevelTileServer[] tiles = Object.FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i] != null && tiles[i].HasGroup(group))
                        members.Add(tiles[i]);
                }
            }

            if (members.Count == 0)
                return false;

            Vector2 center = Vector2.zero;
            for (int i = 0; i < members.Count; i++)
                center += (Vector2)members[i].transform.position;

            center /= members.Count;
            x = center.x;
            y = center.y;
            return true;
        }
    }
}
