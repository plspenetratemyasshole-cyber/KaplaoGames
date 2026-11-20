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
    public static class ExecutorSystemSetup
    {
        const string ExecutorPrefabPath = "Assets/Prefabs/ExecutorTile.prefab";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Kaplao/Setup Executor System")]
        public static void SetupExecutorSystem()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            GameObject executorPrefab = EnsurePrefab();
            WireGameScene(executorPrefab);
            RegisterInNetworkManager(executorPrefab);
            FeatureUiSetup.SetupExecutorUi();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kaplao] Executor system setup complete.");
        }

        static GameObject EnsurePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExecutorPrefabPath);
            if (prefab == null)
                return CreatePrefab();

            GameObject contents = PrefabUtility.LoadPrefabContents(ExecutorPrefabPath);
            bool dirty = false;

            if (contents.GetComponent<LevelTileServer>() == null)
            {
                contents.AddComponent<LevelTileServer>();
                dirty = true;
            }

            if (contents.GetComponent<ExecutorTile>() == null)
            {
                contents.AddComponent<ExecutorTile>();
                dirty = true;
            }

            if (contents.GetComponent<NetworkIdentity>() == null)
            {
                contents.AddComponent<NetworkIdentity>();
                dirty = true;
            }

            if (dirty)
                prefab = PrefabUtility.SaveAsPrefabAsset(contents, ExecutorPrefabPath);

            PrefabUtility.UnloadPrefabContents(contents);
            return prefab;
        }

        static GameObject CreatePrefab()
        {
            GameObject root = new GameObject("ExecutorTile");

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.color = new Color(0.3f, 0.85f, 1f, 0.85f);
            renderer.sortingOrder = 10;

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<LevelTileServer>();
            root.AddComponent<ExecutorTile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExecutorPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void WireGameScene(GameObject executorPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            LevelGridServer grid = Object.FindFirstObjectByType<LevelGridServer>();
            if (grid == null)
            {
                Debug.LogError("[Kaplao] LevelGridServer not found. Run Kaplao/Setup Level System first.");
                return;
            }

            SerializedObject serializedGrid = new SerializedObject(grid);
            serializedGrid.FindProperty("executorPrefab").objectReferenceValue = executorPrefab;
            serializedGrid.ApplyModifiedPropertiesWithoutUndo();

            KaplaoCursorServer cursor = Object.FindFirstObjectByType<KaplaoCursorServer>();
            if (cursor == null)
            {
                GameObject kaplaoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kaplao.prefab");
                if (kaplaoPrefab != null)
                {
                    GameObject kaplaoContents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Kaplao.prefab");
                    cursor = kaplaoContents.GetComponent<KaplaoCursorServer>();
                    if (cursor != null)
                    {
                        SerializedObject so = new SerializedObject(cursor);
                        so.FindProperty("executorTilePrefab").objectReferenceValue = executorPrefab;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(kaplaoContents, "Assets/Prefabs/Kaplao.prefab");
                    PrefabUtility.UnloadPrefabContents(kaplaoContents);
                }
            }
            else
            {
                SerializedObject so = new SerializedObject(cursor);
                so.FindProperty("executorTilePrefab").objectReferenceValue = executorPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RegisterInNetworkManager(GameObject executorPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            KaplaoNetworkManager networkManager = Object.FindFirstObjectByType<KaplaoNetworkManager>();
            if (networkManager == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoNetworkManager not found in MainMenu scene.");
                return;
            }

            if (executorPrefab != null && !networkManager.spawnPrefabs.Contains(executorPrefab))
                networkManager.spawnPrefabs.Add(executorPrefab);

            EditorUtility.SetDirty(networkManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
