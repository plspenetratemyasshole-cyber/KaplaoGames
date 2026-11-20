#if UNITY_EDITOR
using KaplaoGames.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace KaplaoGames.Editor
{
    /// <summary>
    /// Attaches LevelScriptCodeEditorEnhancer. Prefer Kaplao/Setup Code Mode IDE for full UI.
    /// </summary>
    public static class CodeIdeEnhancerSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Code IDE Enhancements")]
        public static void SetupCodeIdeEnhancements()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found.");
                return;
            }

            Transform codeOverlay = canvas.transform.Find("CodeModeOverlay");
            Transform codeInputTransform = codeOverlay != null ? codeOverlay.Find("CodeInputField") : null;

            if (codeInputTransform == null)
            {
                Debug.LogError("[Kaplao] CodeInputField not found. Run Kaplao/Setup Code Mode IDE first.");
                return;
            }

            TMP_InputField codeInput = codeInputTransform.GetComponent<TMP_InputField>();
            if (codeInput == null)
            {
                Debug.LogError("[Kaplao] CodeInputField has no TMP_InputField component.");
                return;
            }

            if (codeInput.GetComponent<LevelScriptCodeEditorEnhancer>() == null)
                codeInput.gameObject.AddComponent<LevelScriptCodeEditorEnhancer>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Code IDE enhancements setup complete. For full file-tree IDE run Kaplao/Setup Code Mode IDE.");
        }
    }
}
#endif
