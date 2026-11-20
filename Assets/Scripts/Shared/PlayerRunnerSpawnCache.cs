using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
  /// <summary>
  /// Remembers runner respawn point per connection while that player is Kaplao,
  /// so demoting back to Runner can use the last spawn instead of world spawn.
  /// </summary>
  public static class PlayerRunnerSpawnCache
  {
    static readonly Dictionary<int, Vector2> ByConnectionId = new();

    public static void Save(int connectionId, Vector2 spawnPoint)
    {
      ByConnectionId[connectionId] = spawnPoint;
    }

    public static bool TryGet(int connectionId, out Vector2 spawnPoint)
    {
      return ByConnectionId.TryGetValue(connectionId, out spawnPoint);
    }

    public static void Clear(int connectionId)
    {
      ByConnectionId.Remove(connectionId);
    }

    public static void ClearAll()
    {
      ByConnectionId.Clear();
    }
  }
}
