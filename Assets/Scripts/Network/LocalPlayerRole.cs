using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Network
{
    /// <summary>
    /// Resolves local player role from the spawned prefab (authoritative for UI).
    /// Prefer this over SyncVar alone — SyncVar order bugs can desync role.
    /// </summary>
    public static class LocalPlayerRole
    {
        public static bool TryGet(out PlayerRole role)
        {
            role = PlayerRole.Runner;

            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return false;

            NetworkIdentity identity = NetworkClient.localPlayer;

            // Prefab components are the truth after spawn / ReplacePlayer.
            if (identity.GetComponent<KaplaoCursorServer>() != null)
            {
                role = PlayerRole.Kaplao;
                return true;
            }

            if (identity.GetComponent<RunnerMovementServer>() != null)
            {
                role = PlayerRole.Runner;
                return true;
            }

            if (identity.TryGetComponent(out NetworkPlayer player))
            {
                role = player.role;
                return true;
            }

            return false;
        }

        public static PlayerRole? GetOrNull()
        {
            return TryGet(out PlayerRole role) ? role : null;
        }

        public static bool IsKaplao() => TryGet(out PlayerRole role) && role == PlayerRole.Kaplao;

        public static bool IsRunner() => TryGet(out PlayerRole role) && role == PlayerRole.Runner;
    }
}
