using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KaplaoGames.Platform
{
    public static class PngFileDialog
    {
        public static string PickImportPath()
        {
#if UNITY_EDITOR
            return EditorUtility.OpenFilePanel("Import PNG", string.Empty, "png");
#elif UNITY_STANDALONE_WIN
            return WindowsFileDialog.OpenPng(string.Empty, "Import PNG");
#else
            Debug.LogWarning("[Kaplao] PNG import dialog is only supported on Windows.");
            return null;
#endif
        }
    }
}
