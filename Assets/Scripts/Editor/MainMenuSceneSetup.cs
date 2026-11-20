#if UNITY_EDITOR
using KaplaoGames.Client;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Editor
{
    public static class MainMenuSceneSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Main Menu Scene")]
        public static void SetupMainMenuScene()
        {
            EnsureFolders();
            CreateOrUpdateMainMenuScene();
            CleanupGameScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kaplao] Main menu scene setup complete. Open MainMenu.unity and press Play.");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        static void CreateOrUpdateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject networkManagerObject = new GameObject("NetworkManager");
            KaplaoNetworkManager networkManager = networkManagerObject.AddComponent<KaplaoNetworkManager>();
            KcpTransport transport = networkManagerObject.AddComponent<KcpTransport>();
            KaplaoNetworkDiscovery discovery = networkManagerObject.AddComponent<KaplaoNetworkDiscovery>();
            networkManagerObject.AddComponent<NetworkTransportConfig>();
            NetworkManagerHUD hud = networkManagerObject.AddComponent<NetworkManagerHUD>();
            hud.enabled = false;

            transport.Port = PlayerSession.DefaultPort;
            discovery.transport = transport;
            networkManager.transport = transport;
            networkManager.networkDiscovery = discovery;
            networkManager.dontDestroyOnLoad = true;
            networkManager.offlineScene = "MainMenu";
            networkManager.onlineScene = "SampleScene";
            networkManager.autoCreatePlayer = false;

            AssignPrefabs(networkManager);

            GameObject menuObject = new GameObject("MainMenuUI");
            MainMenuUI menuUI = menuObject.AddComponent<MainMenuUI>();
            AssignMenuPrefabs(menuUI);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        static void AssignMenuPrefabs(MainMenuUI menuUI)
        {
            GameObject runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Runner.prefab");
            GameObject kaplaoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kaplao.prefab");

            SerializedObject serializedMenu = new SerializedObject(menuUI);
            serializedMenu.FindProperty("runnerPrefab").objectReferenceValue = runnerPrefab;
            serializedMenu.FindProperty("kaplaoPrefab").objectReferenceValue = kaplaoPrefab;
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignPrefabs(KaplaoNetworkManager networkManager)
        {
            GameObject runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Runner.prefab");
            GameObject kaplaoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kaplao.prefab");
            GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BlockTile.prefab");
            GameObject spikePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SpikeTile.prefab");
            GameObject triggerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TriggerTile.prefab");

            networkManager.runnerPrefab = runnerPrefab;
            networkManager.kaplaoPrefab = kaplaoPrefab;
            networkManager.playerPrefab = runnerPrefab;
            networkManager.blockTilePrefab = blockPrefab;
            networkManager.spikeTilePrefab = spikePrefab;
            networkManager.triggerTilePrefab = triggerPrefab;

            if (runnerPrefab != null && !networkManager.spawnPrefabs.Contains(runnerPrefab))
                networkManager.spawnPrefabs.Add(runnerPrefab);

            if (kaplaoPrefab != null && !networkManager.spawnPrefabs.Contains(kaplaoPrefab))
                networkManager.spawnPrefabs.Add(kaplaoPrefab);

            if (blockPrefab != null && !networkManager.spawnPrefabs.Contains(blockPrefab))
                networkManager.spawnPrefabs.Add(blockPrefab);

            if (spikePrefab != null && !networkManager.spawnPrefabs.Contains(spikePrefab))
                networkManager.spawnPrefabs.Add(spikePrefab);

            if (triggerPrefab != null && !networkManager.spawnPrefabs.Contains(triggerPrefab))
                networkManager.spawnPrefabs.Add(triggerPrefab);

            EnsurePlayerComponents(runnerPrefab);
            EnsurePlayerComponents(kaplaoPrefab);
        }

        static void EnsurePlayerComponents(GameObject prefab)
        {
            if (prefab == null)
                return;

            GameObject instance = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));

            if (instance.GetComponent<NetworkPlayer>() == null)
                instance.AddComponent<NetworkPlayer>();

            if (instance.GetComponent<PlayerNameplate>() == null)
                instance.AddComponent<PlayerNameplate>();

            if (instance.GetComponent<GameCameraClient>() == null)
                instance.AddComponent<GameCameraClient>();

            PrefabUtility.SaveAsPrefabAsset(instance, AssetDatabase.GetAssetPath(prefab));
            PrefabUtility.UnloadPrefabContents(instance);
        }

        static void CleanupGameScene()
        {
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            GameObject networkManager = GameObject.Find("NetworkManager");
            if (networkManager != null)
                Object.DestroyImmediate(networkManager);

            EditorSceneManager.SaveScene(gameScene);
        }

        static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }
    }
}
#endif
