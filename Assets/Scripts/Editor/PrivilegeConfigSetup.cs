#if UNITY_EDITOR
using KaplaoGames.Shared;
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Editor
{
    public static class PrivilegeConfigSetup
    {
        const string AssetPath = "Assets/Resources/KaplaoPrivilegeConfig.asset";

        [MenuItem("Kaplao/Setup Privilege Config")]
        public static void SetupPrivilegeConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            KaplaoPrivilegeConfig config = AssetDatabase.LoadAssetAtPath<KaplaoPrivilegeConfig>(AssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<KaplaoPrivilegeConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
            }

            SerializedObject serialized = new SerializedObject(config);
            serialized.Update();
            SerializedProperty emails = serialized.FindProperty("privilegedEmails");
            if (emails != null && emails.arraySize == 0)
            {
                emails.arraySize = 1;
                emails.GetArrayElementAtIndex(0).stringValue = KaplaoPrivilegeConfig.DefaultPrivilegedEmail;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            KaplaoPrivilegeConfig.ClearCache();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log(
                "[Kaplao] Privilege Config ready (emails only). Nickname fonts: Kaplao/Setup Nickname Font Registry. Effects: website admin.");

            NicknameFontRegistrySetup.Setup();
        }
    }
}
#endif
