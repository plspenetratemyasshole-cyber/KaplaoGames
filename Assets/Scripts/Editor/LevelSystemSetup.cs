#if UNITY_EDITOR
using KaplaoGames.Network;
using KaplaoGames.Server;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Editor
{
    public static class LevelSystemSetup
    {
        const string BlockPrefabPath = "Assets/Prefabs/BlockTile.prefab";
        const string SpikePrefabPath = "Assets/Prefabs/SpikeTile.prefab";
        const string TriggerPrefabPath = "Assets/Prefabs/TriggerTile.prefab";
        const string ParticlePrefabPath = "Assets/Prefabs/ParticleTile.prefab";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string DefaultSquareSpriteGuid = "207ee8102dd4143d288186ef0be518ee";

        [MenuItem("Kaplao/Setup Level System")]
        public static void SetupLevelSystem()
        {
            EnsureFolders();

            Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                AssetDatabase.GUIDToAssetPath(DefaultSquareSpriteGuid));

            GameObject blockPrefab = CreateOrUpdateTilePrefab(
                BlockPrefabPath,
                "BlockTile",
                squareSprite,
                new Color(0.45f, 0.42f, 0.38f, 1f),
                isTrigger: false);

            GameObject spikePrefab = CreateOrUpdateTilePrefab(
                SpikePrefabPath,
                "SpikeTile",
                squareSprite,
                new Color(0.9f, 0.2f, 0.15f, 1f),
                isTrigger: true);

            GameObject triggerPrefab = CreateOrUpdateTilePrefab(
                TriggerPrefabPath,
                "TriggerTile",
                squareSprite,
                new Color(0.2f, 0.85f, 0.95f, 0.55f),
                isTrigger: true);

            GameObject particlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePrefabPath);

            AddLevelGridToGameScene(blockPrefab, spikePrefab, triggerPrefab, particlePrefab);
            RegisterTilePrefabsInNetworkManager(blockPrefab, spikePrefab, triggerPrefab, particlePrefab);
            AssignKaplaoCursorPrefabs(blockPrefab, spikePrefab, triggerPrefab, particlePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kaplao] Level system setup complete.");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        static GameObject CreateOrUpdateTilePrefab(
            string path,
            string objectName,
            Sprite sprite,
            Color color,
            bool isTrigger)
        {
            GameObject root = new GameObject(objectName);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 10;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = isTrigger;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            NetworkIdentity identity = root.AddComponent<NetworkIdentity>();
            root.AddComponent<LevelTileServer>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return prefab;
        }

        static void AddLevelGridToGameScene(GameObject blockPrefab, GameObject spikePrefab, GameObject triggerPrefab, GameObject particlePrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            LevelGridServer existing = Object.FindFirstObjectByType<LevelGridServer>();
            GameObject gridObject = existing != null
                ? existing.gameObject
                : new GameObject("LevelGrid");

            LevelGridServer grid = gridObject.GetComponent<LevelGridServer>() ??
                                   gridObject.AddComponent<LevelGridServer>();

            if (gridObject.GetComponent<LevelScript.LevelScriptRuntime>() == null)
                gridObject.AddComponent<LevelScript.LevelScriptRuntime>();

            if (gridObject.GetComponent<LevelScript.LevelGroupAnimator>() == null)
                gridObject.AddComponent<LevelScript.LevelGroupAnimator>();

            if (gridObject.GetComponent<LevelScript.LevelScriptTriggerBridge>() == null)
                gridObject.AddComponent<LevelScript.LevelScriptTriggerBridge>();

            if (gridObject.GetComponent<NetworkIdentity>() == null)
                gridObject.AddComponent<NetworkIdentity>();

            if (gridObject.GetComponent<LevelSessionServer>() == null)
                gridObject.AddComponent<LevelSessionServer>();

            if (gridObject.GetComponent<WorldLoadBootstrap>() == null)
                gridObject.AddComponent<WorldLoadBootstrap>();

            SerializedObject serializedGrid = new SerializedObject(grid);
            serializedGrid.FindProperty("blockPrefab").objectReferenceValue = blockPrefab;
            serializedGrid.FindProperty("spikePrefab").objectReferenceValue = spikePrefab;
            serializedGrid.FindProperty("triggerPrefab").objectReferenceValue = triggerPrefab;
            serializedGrid.FindProperty("particlePrefab").objectReferenceValue = particlePrefab;
            serializedGrid.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RegisterTilePrefabsInNetworkManager(GameObject blockPrefab, GameObject spikePrefab, GameObject triggerPrefab, GameObject particlePrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            KaplaoNetworkManager networkManager = Object.FindFirstObjectByType<KaplaoNetworkManager>();
            if (networkManager == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoNetworkManager not found in MainMenu scene.");
                return;
            }

            networkManager.blockTilePrefab = blockPrefab;
            networkManager.spikeTilePrefab = spikePrefab;
            networkManager.triggerTilePrefab = triggerPrefab;
            networkManager.particleTilePrefab = particlePrefab;

            if (blockPrefab != null && !networkManager.spawnPrefabs.Contains(blockPrefab))
                networkManager.spawnPrefabs.Add(blockPrefab);

            if (spikePrefab != null && !networkManager.spawnPrefabs.Contains(spikePrefab))
                networkManager.spawnPrefabs.Add(spikePrefab);

            if (triggerPrefab != null && !networkManager.spawnPrefabs.Contains(triggerPrefab))
                networkManager.spawnPrefabs.Add(triggerPrefab);

            if (particlePrefab != null && !networkManager.spawnPrefabs.Contains(particlePrefab))
                networkManager.spawnPrefabs.Add(particlePrefab);

            EditorUtility.SetDirty(networkManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AssignKaplaoCursorPrefabs(GameObject blockPrefab, GameObject spikePrefab, GameObject triggerPrefab, GameObject particlePrefab)
        {
            const string kaplaoPrefabPath = "Assets/Prefabs/Kaplao.prefab";
            GameObject kaplaoRoot = PrefabUtility.LoadPrefabContents(kaplaoPrefabPath);
            KaplaoCursorServer cursorServer = kaplaoRoot.GetComponent<KaplaoCursorServer>();
            if (cursorServer == null)
            {
                PrefabUtility.UnloadPrefabContents(kaplaoRoot);
                return;
            }

            SerializedObject serializedCursor = new SerializedObject(cursorServer);
            serializedCursor.FindProperty("blockTilePrefab").objectReferenceValue = blockPrefab;
            serializedCursor.FindProperty("spikeTilePrefab").objectReferenceValue = spikePrefab;
            serializedCursor.FindProperty("triggerTilePrefab").objectReferenceValue = triggerPrefab;
            serializedCursor.FindProperty("particleTilePrefab").objectReferenceValue = particlePrefab;
            serializedCursor.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(kaplaoRoot, kaplaoPrefabPath);
            PrefabUtility.UnloadPrefabContents(kaplaoRoot);
        }
    }
}
#endif
