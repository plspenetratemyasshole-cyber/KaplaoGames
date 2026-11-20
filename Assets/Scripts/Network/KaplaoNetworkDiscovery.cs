using System;
using System.Net;
using System.Net.Sockets;
using KaplaoGames.Shared;
using Mirror;
using Mirror.Discovery;
using UnityEngine;

namespace KaplaoGames.Network
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/Network/Kaplao Network Discovery")]
    public class KaplaoNetworkDiscovery : NetworkDiscoveryBase<ServerRequest, KaplaoServerResponse>
    {
        string advertiseInterfaceIPv4 = string.Empty;

        public void AdvertiseServerOnInterface(string interfaceIPv4)
        {
            advertiseInterfaceIPv4 = string.IsNullOrWhiteSpace(interfaceIPv4)
                ? string.Empty
                : interfaceIPv4.Trim();

            if (!SupportedOnThisPlatform)
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");

            StopDiscovery();

            // Always listen on all interfaces. Binding to one adapter blocks discovery
            // broadcasts that arrive on other local interfaces (common with VPN/tun).
            serverUdpClient = new UdpClient(serverBroadcastListenPort)
            {
                EnableBroadcast = true,
                MulticastLoopback = false
            };

            _ = ServerListenAsync();
        }

        protected override KaplaoServerResponse ProcessRequest(ServerRequest request, IPEndPoint endpoint)
        {
            try
            {
                Uri uri = BuildAdvertisedUri();
                return new KaplaoServerResponse
                {
                    serverId = ServerId,
                    uri = uri,
                    serverName = PlayerSession.ServerDisplayName
                };
            }
            catch (NotImplementedException)
            {
                Debug.LogError($"Transport {transport} does not support network discovery.");
                throw;
            }
        }

        Uri BuildAdvertisedUri()
        {
            Transport advertiseTransport = Transport.active != null ? Transport.active : transport;
            if (advertiseTransport == null)
                throw new InvalidOperationException("No active transport to advertise.");

            Uri uri = advertiseTransport.ServerUri();
            string hostIp = !string.IsNullOrWhiteSpace(advertiseInterfaceIPv4)
                ? advertiseInterfaceIPv4
                : PlayerSession.LanInterfaceIPv4;

            if (string.IsNullOrWhiteSpace(hostIp))
                return uri;

            var builder = new UriBuilder(uri)
            {
                Host = hostIp
            };
            return builder.Uri;
        }

        protected override ServerRequest GetRequest() => new ServerRequest();

        protected override void ProcessResponse(KaplaoServerResponse response, IPEndPoint endpoint)
        {
            response.EndPoint = endpoint;

            // Keep the IP chosen by the host. Replacing it with the UDP source address
            // makes manual interface selection look broken (OS may reply via default route).
            if (!TryGetUriHostIPv4(response.uri, out _))
            {
                UriBuilder realUri = new UriBuilder(response.uri)
                {
                    Host = response.EndPoint.Address.ToString()
                };
                response.uri = realUri.Uri;
            }

            OnServerFound.Invoke(response);
        }

        static bool TryGetUriHostIPv4(Uri uri, out string ipv4)
        {
            ipv4 = null;
            if (uri == null || string.IsNullOrWhiteSpace(uri.Host))
                return false;

            if (!IPAddress.TryParse(uri.Host, out IPAddress address))
                return false;

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            ipv4 = address.ToString();
            return true;
        }
    }
}
