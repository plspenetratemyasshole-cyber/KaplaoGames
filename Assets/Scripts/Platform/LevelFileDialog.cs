using KaplaoGames.Shared;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KaplaoGames.Platform
{
    public static class LevelFileDialog
    {
        public static string PickImportPath()
        {
            string directory = LevelIoPaths.LevelsDirectory;

#if UNITY_EDITOR
            return EditorUtility.OpenFilePanel("Import Level", directory, LevelIoPaths.Extension);
#elif UNITY_STANDALONE_WIN
            return WindowsFileDialog.Open(directory, "Import Level");
#else
            Debug.LogWarning("[Kaplao] Level import dialog is only supported on Windows.");
            return null;
#endif
        }

        public static string PickExportPath()
        {
            string directory = LevelIoPaths.LevelsDirectory;

#if UNITY_EDITOR
            return EditorUtility.SaveFilePanel(
                "Export Level",
                directory,
                System.IO.Path.GetFileNameWithoutExtension(LevelIoPaths.DefaultFileName),
                LevelIoPaths.Extension);
#elif UNITY_STANDALONE_WIN
            return WindowsFileDialog.Save(directory, "Export Level", LevelIoPaths.DefaultFileName);
#else
            Debug.LogWarning("[Kaplao] Level export dialog is only supported on Windows.");
            return null;
#endif
        }
    }
}
