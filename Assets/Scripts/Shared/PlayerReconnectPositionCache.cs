using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Remembers last world position and spawn point by display name for the current server session,
    /// so reconnecting players return where they left with the same respawn point.
    /// </summary>
    public static class PlayerReconnectPositionCache
    {
        public struct Snapshot
        {
            public Vector2 Position;
            public Vector2 SpawnPoint;
            public bool HasSpawnPoint;
        }

        static readonly Dictionary<string, Snapshot> ByDisplayName =
            new(StringComparer.OrdinalIgnoreCase);

        public static void Save(string displayName, Vector2 position, Vector2? spawnPoint = null)
        {
            string key = PlayerSession.SanitizeNickname(displayName);
            if (string.IsNullOrWhiteSpace(key))
                return;

            ByDisplayName[key] = new Snapshot
            {
                Position = position,
                SpawnPoint = spawnPoint ?? position,
                HasSpawnPoint = spawnPoint.HasValue
            };
        }

        public static bool TryTake(string displayName, out Snapshot snapshot)
        {
            string key = PlayerSession.SanitizeNickname(displayName);
            if (ByDisplayName.TryGetValue(key, out snapshot))
            {
                ByDisplayName.Remove(key);
                return true;
            }

            snapshot = default;
            return false;
        }

        public static void Clear()
        {
            ByDisplayName.Clear();
        }
    }
}
