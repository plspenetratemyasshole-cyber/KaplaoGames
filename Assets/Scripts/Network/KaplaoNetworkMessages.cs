using System;
using System.Net;
using Mirror;

namespace KaplaoGames.Network
{
    public struct JoinGameMessage : NetworkMessage
    {
        public string playerName;
        /// <summary>Legacy — server ignores this for auth; uses verified JWT email instead.</summary>
        public string accountEmail;
        public string authToken;
        /// <summary>-1 = random skin; otherwise library character index.</summary>
        public int preferredSkinIndex;
    }

    public struct KaplaoServerResponse : NetworkMessage
    {
        public IPEndPoint EndPoint { get; set; }

        public Uri uri;
        public long serverId;
        public string serverName;
    }
}
