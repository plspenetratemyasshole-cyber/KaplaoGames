using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Resolves the shared runner spawn point from NetworkStartPosition.
    /// </summary>
    public static class RunnerSpawnPoint
    {
        static Vector2? cachedPosition;
        static Vector2? overridePosition;

        [Server]
        public static void SetOverride(Vector2 position)
        {
            overridePosition = position;
            cachedPosition = position;
        }

        [Server]
        public static Vector2 GetPosition()
        {
            if (cachedPosition.HasValue)
                return cachedPosition.Value;

            if (overridePosition.HasValue)
            {
                cachedPosition = overridePosition.Value;
                return cachedPosition.Value;
            }

            if (LevelSessionServer.Instance != null)
            {
                cachedPosition = LevelSessionServer.Instance.WorldSpawn;
                return cachedPosition.Value;
            }

            NetworkStartPosition startPosition = Object.FindFirstObjectByType<NetworkStartPosition>();
            if (startPosition != null)
            {
                cachedPosition = startPosition.transform.position;
                return cachedPosition.Value;
            }

            if (NetworkManager.singleton != null)
            {
                Transform startTransform = NetworkManager.singleton.GetStartPosition();
                if (startTransform != null)
                {
                    cachedPosition = startTransform.position;
                    return cachedPosition.Value;
                }
            }

            cachedPosition = new Vector2(0f, 5f);
            return cachedPosition.Value;
        }

        public static void ClearCache()
        {
            cachedPosition = null;
            overridePosition = null;
        }
    }
}
