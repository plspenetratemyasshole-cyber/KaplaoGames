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
    /// <summary>
    /// Adds Keyframe tile prefab, grid references, and network spawn registration.
    /// Does not touch Create Mode UI or other level prefabs.
    /// </summary>
    public static class KeyframeSystemSetup
    {
        const string KeyframePrefabPath = "Assets/Prefabs/KeyframeTile.prefab";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string DefaultSquareSpriteGuid = "207ee8102dd4143d288186ef0be518ee";

        [MenuItem("Kaplao/Setup Keyframe System")]
        public static void SetupKeyframeSystem()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                AssetDatabase.GUIDToAssetPath(DefaultSquareSpriteGuid));

            GameObject keyframePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyframePrefabPath);
            if (keyframePrefab == null)
            {
                keyframePrefab = CreateKeyframePrefab(squareSprite);
            }
            else if (keyframePrefab.GetComponent<KeyframeTile>() == null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(KeyframePrefabPath);
                contents.AddComponent<KeyframeTile>();
                keyframePrefab = PrefabUtility.SaveAsPrefabAsset(contents, KeyframePrefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            WireGameScene(keyframePrefab);
            RegisterInNetworkManager(keyframePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kaplao] Keyframe system setup complete.");
        }

        static GameObject CreateKeyframePrefab(Sprite sprite)
        {
            GameObject root = new GameObject("KeyframeTile");

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.85f, 0.2f, 0.85f);
            renderer.sortingOrder = 10;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<LevelTileServer>();
            root.AddComponent<KeyframeTile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, KeyframePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void WireGameScene(GameObject keyframePrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            LevelGridServer grid = Object.FindFirstObjectByType<LevelGridServer>();
            if (grid == null)
            {
                Debug.LogError("[Kaplao] LevelGridServer not found. Run Kaplao/Setup Level System first.");
                return;
            }

            if (grid.GetComponent<LevelScript.KeyframeAnimator>() == null)
                grid.gameObject.AddComponent<LevelScript.KeyframeAnimator>();

            SerializedObject serializedGrid = new SerializedObject(grid);
            serializedGrid.FindProperty("keyframePrefab").objectReferenceValue = keyframePrefab;
            serializedGrid.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RegisterInNetworkManager(GameObject keyframePrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            KaplaoNetworkManager networkManager = Object.FindFirstObjectByType<KaplaoNetworkManager>();
            if (networkManager == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoNetworkManager not found in MainMenu scene.");
                return;
            }

            if (keyframePrefab != null && !networkManager.spawnPrefabs.Contains(keyframePrefab))
                networkManager.spawnPrefabs.Add(keyframePrefab);

            EditorUtility.SetDirty(networkManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
