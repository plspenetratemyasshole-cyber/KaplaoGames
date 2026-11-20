using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;

namespace KaplaoGames.Client
{
    public static class KaplaoLocalBindingUtility
    {
        public static KaplaoCursorClient ResolveLocalCursorClient()
        {
            KaplaoCursorClient client = KaplaoCursorClient.Local;
            if (client != null)
                return client;

            NetworkIdentity localIdentity = NetworkClient.localPlayer;
            if (localIdentity == null ||
                !localIdentity.TryGetComponent(out NetworkPlayer player) ||
                player.role != PlayerRole.Kaplao ||
                !localIdentity.TryGetComponent(out client))
            {
                return null;
            }

            return client;
        }

        public static bool TryGetLocalKaplao(
            out NetworkIdentity identity,
            out NetworkPlayer player,
            out KaplaoCursorClient client,
            out KaplaoCursorServer server)
        {
            identity = NetworkClient.localPlayer;
            player = null;
            client = null;
            server = null;

            if (identity == null ||
                !identity.TryGetComponent(out player) ||
                player.role != PlayerRole.Kaplao)
            {
                return false;
            }

            identity.TryGetComponent(out server);
            client = ResolveLocalCursorClient();
            return client != null;
        }
    }
}
