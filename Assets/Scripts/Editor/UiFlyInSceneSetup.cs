#if UNITY_EDITOR
using KaplaoGames.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Editor
{
    public static class UiFlyInSceneSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        static readonly string[] GameScenePanelNames =
        {
            "CreateModeUI",
            "CodeModeOverlay",
            "TileColorPanel",
            "LevelIoPanel",
            "HostSettingsUI"
        };

        [MenuItem("Kaplao/Setup Main Menu Fly-In Animations")]
        public static void SetupMainMenuFlyIn()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in MainMenu scene.");
                return;
            }

            UiFlyInChildren flyIn = canvas.GetComponent<UiFlyInChildren>();
            if (flyIn == null)
                flyIn = canvas.gameObject.AddComponent<UiFlyInChildren>();

            SerializedObject serialized = new SerializedObject(flyIn);
            serialized.FindProperty("searchRoot").objectReferenceValue = canvas.transform as RectTransform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(flyIn);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kaplao] Main Menu fly-in added on Canvas (UiFlyInChildren).");
        }

        [MenuItem("Kaplao/Setup Game Scene Fly-In Animations")]
        public static void SetupGameSceneFlyIn()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform canvasTransform = canvas.transform;
            var panelRects = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < GameScenePanelNames.Length; i++)
            {
                Transform panel = canvasTransform.Find(GameScenePanelNames[i]);
                if (panel == null)
                {
                    Debug.LogWarning($"[Kaplao] Panel '{GameScenePanelNames[i]}' not found under Canvas.");
                    continue;
                }

                if (panel is RectTransform rect)
                    panelRects.Add(rect);
            }

            GameObject hostObject = GameObject.Find("UiFlyInPanelGroup");
            if (hostObject == null)
                hostObject = new GameObject("UiFlyInPanelGroup");

            UiFlyInPanelGroup group = hostObject.GetComponent<UiFlyInPanelGroup>();
            if (group == null)
                group = hostObject.AddComponent<UiFlyInPanelGroup>();

            group.SetPanels(panelRects.ToArray());

            SerializedObject serialized = new SerializedObject(group);
            serialized.FindProperty("panels").arraySize = panelRects.Count;
            for (int i = 0; i < panelRects.Count; i++)
                serialized.FindProperty("panels").GetArrayElementAtIndex(i).objectReferenceValue = panelRects[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Kaplao] Game scene fly-in configured for {panelRects.Count} panels.");
        }
    }
}
#endif
