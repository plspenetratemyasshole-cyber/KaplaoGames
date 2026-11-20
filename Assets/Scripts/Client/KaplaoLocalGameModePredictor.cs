using System;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.Network;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Instant local HostGameMode for Kaplao so Create/Usual/Code UI and tools
    /// do not wait for SyncVar under high ping.
    /// </summary>
    public static class KaplaoLocalGameModePredictor
    {
        static bool hasOverride;
        static HostGameMode predicted = HostGameMode.Usual;

        public static event Action<HostGameMode> Changed;

        public static bool HasOverride => hasOverride;

        public static HostGameMode GetEffective(KaplaoCursorServer server)
        {
            if (hasOverride)
                return predicted;

            return server != null ? server.GameMode : HostGameMode.Usual;
        }

        public static bool IsCreate(KaplaoCursorServer server) =>
            GetEffective(server) == HostGameMode.Create;

        public static void Predict(HostGameMode mode)
        {
            if (!NetworkClient.active || !LocalPlayerRole.IsKaplao())
                return;

            hasOverride = true;
            predicted = mode;
            HostGameModeState.Current = mode;

            if (mode != HostGameMode.Create)
                KaplaoLocalTransformSession.ForceClear();

            Changed?.Invoke(mode);
        }

        public static void HandleServerSync(HostGameMode mode)
        {
            if (!hasOverride)
            {
                HostGameModeState.Current = mode;
                Changed?.Invoke(mode);
                return;
            }

            if (mode == predicted)
            {
                hasOverride = false;
                HostGameModeState.Current = mode;
                Changed?.Invoke(mode);
                return;
            }

            // Keep predicted mode until the SyncVar catches up.
            HostGameModeState.Current = predicted;
            Changed?.Invoke(predicted);
        }

        public static void Clear()
        {
            if (!hasOverride)
                return;

            hasOverride = false;
        }
    }
}
