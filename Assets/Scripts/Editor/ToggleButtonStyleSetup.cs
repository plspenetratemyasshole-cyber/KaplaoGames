#if UNITY_EDITOR
using KaplaoGames.UI;
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Editor
{
    public static class ToggleButtonStyleSetup
    {
        const string AssetPath = "Assets/Resources/KaplaoToggleButtonStyle.asset";

        [MenuItem("Kaplao/Setup Toggle Button Style")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            KaplaoToggleButtonStyle style = AssetDatabase.LoadAssetAtPath<KaplaoToggleButtonStyle>(AssetPath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<KaplaoToggleButtonStyle>();
                AssetDatabase.CreateAsset(style, AssetPath);
            }

            KaplaoToggleButtonStyle.ClearCache();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            Selection.activeObject = style;
            EditorGUIUtility.PingObject(style);
            Debug.Log(
                "[Kaplao] Toggle Button Style ready — set Off Color / On Color (and optional text colors). " +
                "Used by pause ModerateChat/Kaplao/Mod and Create Mode buttons. " +
                "For Mode/Panel folders: add Kaplao Folder Toggle Button and assign targets.");
        }
    }
}
#endif
