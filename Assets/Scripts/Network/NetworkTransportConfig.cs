using KaplaoGames.Shared;
using kcp2k;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Network
{
    /// <summary>
    /// Applies Mirror transport from <see cref="PlayerSession"/> (KCP/UDP or Telepathy/TCP).
    /// Choice is made in-game before host/client start; both sides must match.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/Network/Network Transport Config")]
    public class NetworkTransportConfig : MonoBehaviour
    {
        public static NetworkTransportConfig Instance { get; private set; }

        public static bool IsTcpEnabled => PlayerSession.UseTcpTransport;

        void Awake()
        {
            Instance = this;
            PlayerSession.Load();
            ApplyFromSession();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void SetUseTcp(bool useTcp)
        {
            PlayerSession.SetUseTcpTransport(useTcp);
            if (Instance != null)
                Instance.ApplyFromSession();
            else
                KaplaoNetworkManager.Instance?.ApplySessionPort();
        }

        public void ApplyFromSession()
        {
            KaplaoNetworkManager networkManager = GetComponent<KaplaoNetworkManager>();
            if (networkManager == null)
                networkManager = KaplaoNetworkManager.Instance;

            if (networkManager == null)
                return;

            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning(
                    "[NetworkTransportConfig] Cannot switch transport while connected. Leave to Main Menu first.");
                return;
            }

            if (PlayerSession.Port == 0)
                PlayerSession.SetPort(PlayerSession.DefaultPort);

            if (PlayerSession.UseTcpTransport)
                ApplyTelepathy(networkManager);
            else
                ApplyKcp(networkManager);

            networkManager.ApplySessionPort();
            Debug.Log(
                $"[NetworkTransportConfig] Transport={(PlayerSession.UseTcpTransport ? "Telepathy/TCP" : "KCP/UDP")} port={PlayerSession.Port}");
        }

        static void ApplyTelepathy(KaplaoNetworkManager networkManager)
        {
            KcpTransport kcp = networkManager.GetComponent<KcpTransport>();
            if (kcp != null)
                kcp.enabled = false;

            TelepathyTransport telepathy = networkManager.GetComponent<TelepathyTransport>();
            if (telepathy == null)
                telepathy = networkManager.gameObject.AddComponent<TelepathyTransport>();

            telepathy.enabled = true;
            telepathy.Port = PlayerSession.Port;
            networkManager.transport = telepathy;
            Transport.active = telepathy;

            KaplaoNetworkDiscovery discovery = networkManager.networkDiscovery != null
                ? networkManager.networkDiscovery
                : networkManager.GetComponent<KaplaoNetworkDiscovery>();
            if (discovery != null)
                discovery.transport = telepathy;
        }

        static void ApplyKcp(KaplaoNetworkManager networkManager)
        {
            TelepathyTransport telepathy = networkManager.GetComponent<TelepathyTransport>();
            if (telepathy != null)
                telepathy.enabled = false;

            KcpTransport kcp = networkManager.GetComponent<KcpTransport>();
            if (kcp == null)
                kcp = networkManager.gameObject.AddComponent<KcpTransport>();

            kcp.enabled = true;
            kcp.Port = PlayerSession.Port;
            networkManager.transport = kcp;
            Transport.active = kcp;

            KaplaoNetworkDiscovery discovery = networkManager.networkDiscovery != null
                ? networkManager.networkDiscovery
                : networkManager.GetComponent<KaplaoNetworkDiscovery>();
            if (discovery != null)
                discovery.transport = kcp;
        }
    }
}
