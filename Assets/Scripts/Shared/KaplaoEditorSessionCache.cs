using System.Collections.Generic;
using KaplaoGames.Server;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Preserves Kaplao editor state across Kaplao -> Runner demotion for the same connection.
    /// Restored when that connection is promoted back to Kaplao.
    /// </summary>
    public static class KaplaoEditorSessionCache
    {
        public struct Snapshot
        {
            public HostGameMode GameMode;
            public int EditorLayer;
            public uint[] SelectedNetIds;
            public float RotationSnapDegrees;
            public float PositionSnapUnits;
            public float ScaleSnapUnits;
            public bool HasData;
        }

        static readonly Dictionary<int, Snapshot> ByConnectionId = new();

        public static void Save(int connectionId, KaplaoCursorServer cursor)
        {
            if (cursor == null)
                return;

            var selected = new uint[cursor.SelectedCount];
            for (int i = 0; i < cursor.SelectedCount; i++)
                selected[i] = cursor.SelectedTiles[i];

            ByConnectionId[connectionId] = new Snapshot
            {
                GameMode = cursor.GameMode,
                EditorLayer = cursor.ActiveEditorLayer,
                SelectedNetIds = selected,
                RotationSnapDegrees = cursor.RotationSnapDegrees,
                PositionSnapUnits = cursor.PositionSnapUnits,
                ScaleSnapUnits = cursor.ScaleSnapUnits,
                HasData = true
            };
        }

        public static bool TryTake(int connectionId, out Snapshot snapshot)
        {
            if (ByConnectionId.TryGetValue(connectionId, out snapshot) && snapshot.HasData)
            {
                ByConnectionId.Remove(connectionId);
                return true;
            }

            snapshot = default;
            return false;
        }

        public static void Clear(int connectionId)
        {
            ByConnectionId.Remove(connectionId);
        }
    }
}
