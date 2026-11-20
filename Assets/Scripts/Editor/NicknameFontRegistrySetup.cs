#if UNITY_EDITOR
using KaplaoGames.Shared;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Editor
{
    public static class NicknameFontRegistrySetup
    {
        const string AssetPath = "Assets/Resources/KaplaoNicknameFontRegistry.asset";

        [MenuItem("Kaplao/Setup Nickname Font Registry")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            KaplaoNicknameFontRegistry registry =
                AssetDatabase.LoadAssetAtPath<KaplaoNicknameFontRegistry>(AssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<KaplaoNicknameFontRegistry>();
                AssetDatabase.CreateAsset(registry, AssetPath);
            }

            SerializedObject serialized = new SerializedObject(registry);
            serialized.Update();

            SerializedProperty fallbackFont = serialized.FindProperty("fallbackFont");
            if (fallbackFont != null && fallbackFont.objectReferenceValue == null &&
                TMP_Settings.defaultFontAsset != null)
            {
                fallbackFont.objectReferenceValue = TMP_Settings.defaultFontAsset;
            }

            SerializedProperty fonts = serialized.FindProperty("fonts");
            if (fonts != null && fonts.arraySize == 0 && TMP_Settings.defaultFontAsset != null)
            {
                fonts.arraySize = 1;
                SerializedProperty entry = fonts.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("name").stringValue = "default";
                entry.FindPropertyRelative("font").objectReferenceValue = TMP_Settings.defaultFontAsset;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            KaplaoNicknameFontRegistry.ClearCache();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
            Debug.Log(
                "[Kaplao] Nickname Font Registry ready — add fonts with names that match admin preset fontName strings.");
        }
    }
}
#endif
