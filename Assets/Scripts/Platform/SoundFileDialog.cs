using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KaplaoGames.Platform
{
    public static class SoundFileDialog
    {
        public static string PickImportPath()
        {
#if UNITY_EDITOR
            return EditorUtility.OpenFilePanel("Import Sound", string.Empty, "ogg,mp3");
#elif UNITY_STANDALONE_WIN
            return WindowsFileDialog.OpenAudio(string.Empty, "Import Sound");
#else
            Debug.LogWarning("[Kaplao] Sound import dialog is only supported on Windows.");
            return null;
#endif
        }
    }
}
