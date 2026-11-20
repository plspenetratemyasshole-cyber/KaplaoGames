#if UNITY_EDITOR
using KaplaoGames.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Editor
{
    public static class ParallaxBackgroundSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string BackgroundSpritesFolder = "Assets/Sprites/Background/Layers";

        [MenuItem("Kaplao/Setup Parallax Background In Scene")]
        public static void SetupInScene()
        {
            SetupInSceneAtPath(null);
        }

        [MenuItem("Kaplao/Setup Parallax Background In Game Scene")]
        public static void SetupInGameScene()
        {
            SetupInSceneAtPath(GameScenePath);
        }

        static void SetupInSceneAtPath(string scenePath)
        {
            EnsureFolder("Assets/Sprites/Background");
            EnsureFolder(BackgroundSpritesFolder);

            if (!string.IsNullOrEmpty(scenePath))
            {
                Scene active = EditorSceneManager.GetActiveScene();
                if (active.path != scenePath)
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[Kaplao] No active scene.");
                return;
            }

            if (scene.name == "MainMenu")
            {
                Debug.LogWarning("[Kaplao] Parallax belongs in the game scene (SampleScene), not MainMenu.");
            }

            ParallaxBackground existing = Object.FindFirstObjectByType<ParallaxBackground>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Kaplao] ParallaxBackground already exists in the scene.");
                return;
            }

            GameObject root = new GameObject("ParallaxBackground");
            ParallaxBackground parallax = root.AddComponent<ParallaxBackground>();

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = Object.FindFirstObjectByType<Camera>();

            if (mainCamera != null)
            {
                SerializedObject serialized = new SerializedObject(parallax);
                serialized.FindProperty("targetCamera").objectReferenceValue = mainCamera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(root, "Create Parallax Background");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[Kaplao] ParallaxBackground added. Assign sprites to Layers in the Inspector, then Play.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
