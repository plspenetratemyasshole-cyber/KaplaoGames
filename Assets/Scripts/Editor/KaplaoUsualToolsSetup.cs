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
    public static class KaplaoUsualToolsSetup
    {
        const string BulletPrefabPath = "Assets/Prefabs/Bullet.prefab";
        const string KaplaoPrefabPath = "Assets/Prefabs/Kaplao.prefab";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Kaplao/Setup Usual Tools (Palm + Gun)")]
        public static void SetupUsualTools()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
            if (bulletPrefab == null)
                bulletPrefab = CreateBulletPrefab();

            RegisterBulletInNetworkManager(bulletPrefab);
            AssignBulletOnKaplaoPrefab(bulletPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kaplao] Usual tools setup complete. Assign Palm Cursor Prefab and Gun Shot Clip on Kaplao prefab.");
        }

        static GameObject CreateBulletPrefab()
        {
            GameObject root = new GameObject("Bullet");

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 150;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(0.22f, 1f);

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<KaplaoBulletServer>();
            root.AddComponent<NetworkTransformReliable>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BulletPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void RegisterBulletInNetworkManager(GameObject bulletPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            KaplaoNetworkManager networkManager = Object.FindFirstObjectByType<KaplaoNetworkManager>();
            if (networkManager == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoNetworkManager not found in MainMenu scene.");
                return;
            }

            networkManager.kaplaoBulletPrefab = bulletPrefab;
            if (bulletPrefab != null && !networkManager.spawnPrefabs.Contains(bulletPrefab))
                networkManager.spawnPrefabs.Add(bulletPrefab);

            EditorUtility.SetDirty(networkManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AssignBulletOnKaplaoPrefab(GameObject bulletPrefab)
        {
            GameObject kaplaoRoot = PrefabUtility.LoadPrefabContents(KaplaoPrefabPath);
            KaplaoCursorServer cursorServer = kaplaoRoot.GetComponent<KaplaoCursorServer>();
            if (cursorServer == null)
            {
                PrefabUtility.UnloadPrefabContents(kaplaoRoot);
                return;
            }

            SerializedObject serializedCursor = new SerializedObject(cursorServer);
            serializedCursor.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
            serializedCursor.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(kaplaoRoot, KaplaoPrefabPath);
            PrefabUtility.UnloadPrefabContents(kaplaoRoot);
        }
    }
}
#endif
