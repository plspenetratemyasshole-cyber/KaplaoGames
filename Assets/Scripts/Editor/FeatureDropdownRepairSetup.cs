#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaplaoGames.Editor
{
    public static class FeatureDropdownRepairSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Kaplao/Repair Feature Dropdowns")]
        public static void RepairFeatureDropdowns()
        {
            RepairScene(GameScenePath);
            RepairScene(MainMenuScenePath);
            Debug.Log("[Kaplao] Feature dropdown repair complete.");
        }

        static void RepairScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            EditorUiDropdownUtility.RepairAllInHierarchy(canvas.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
