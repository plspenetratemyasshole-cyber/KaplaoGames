#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KaplaoGames.Editor
{
    public static class PrivilegeGradientGenerator
    {
        [MenuItem("Kaplao/Generate Nickname Gradient Textures", false, 200)]
        public static void GenerateTexturesMenu()
        {
            EditorUtility.DisplayDialog(
                "Deprecated",
                "Nickname gradients are configured in the website admin with Easy Text Effects. Use Kaplao/Setup Nickname Font Registry for fonts only.",
                "OK");
        }
    }
}
#endif
