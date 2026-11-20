using KaplaoGames.Network;
using KaplaoGames.Shared;
using kcp2k;
using Mirror;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KaplaoGames.UI
{
    /// <summary>
    /// Creates and wires NetworkManager + LAN discovery when missing from the menu scene.
    /// </summary>
    public static class NetworkInfrastructureBootstrap
    {
        public static KaplaoNetworkManager EnsureExists(GameObject runnerPrefab, GameObject kaplaoPrefab)
        {
            KaplaoNetworkManager existing = KaplaoNetworkManager.Instance;
            if (existing != null)
                return existing;

#if UNITY_EDITOR
            if (runnerPrefab == null)
                runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Runner.prefab");

            if (kaplaoPrefab == null)
                kaplaoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kaplao.prefab");
#endif

            if (runnerPrefab == null || kaplaoPrefab == null)
            {
                Debug.LogError($"[NetworkInfrastructureBootstrap] Missing prefabs. runnerPrefab={(runnerPrefab ? "OK" : "NULL")} kaplaoPrefab={(kaplaoPrefab ? "OK" : "NULL")}");
                return null;
            }

            GameObject networkObject = new GameObject("NetworkManager");
            Object.DontDestroyOnLoad(networkObject);

            KaplaoNetworkManager networkManager = networkObject.AddComponent<KaplaoNetworkManager>();
            KcpTransport transport = networkObject.AddComponent<KcpTransport>();
            KaplaoNetworkDiscovery discovery = networkObject.AddComponent<KaplaoNetworkDiscovery>();
            networkObject.AddComponent<NetworkTransportConfig>();
            NetworkManagerHUD hud = networkObject.AddComponent<NetworkManagerHUD>();
            hud.enabled = false;

            transport.Port = PlayerSession.DefaultPort;
            discovery.transport = transport;

            networkManager.transport = transport;
            networkManager.networkDiscovery = discovery;
            networkManager.runnerPrefab = runnerPrefab;
            networkManager.kaplaoPrefab = kaplaoPrefab;
            networkManager.playerPrefab = runnerPrefab;
            networkManager.dontDestroyOnLoad = true;
            networkManager.offlineScene = "MainMenu";
            networkManager.onlineScene = "SampleScene";
            networkManager.autoCreatePlayer = false;

            if (runnerPrefab != null && !networkManager.spawnPrefabs.Contains(runnerPrefab))
                networkManager.spawnPrefabs.Add(runnerPrefab);

            if (kaplaoPrefab != null && !networkManager.spawnPrefabs.Contains(kaplaoPrefab))
                networkManager.spawnPrefabs.Add(kaplaoPrefab);

            return networkManager;
        }
    }
}
