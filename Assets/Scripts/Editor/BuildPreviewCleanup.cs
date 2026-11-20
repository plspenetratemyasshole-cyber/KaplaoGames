using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace KaplaoGames.Editor
{
    /// <summary>
    /// Clears inspector previews before player builds.
    /// This works around editor-side preview disposal errors that can fail a build.
    /// </summary>
    public sealed class BuildPreviewCleanup : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            EditorApplication.delayCall += UnloadEditorPreviewState;
            UnloadEditorPreviewState();
        }

        static void UnloadEditorPreviewState()
        {
            EditorApplication.delayCall -= UnloadEditorPreviewState;

            try
            {
                EditorUtility.UnloadUnusedAssetsImmediate();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[BuildPreviewCleanup] Preview cleanup skipped: {exception.Message}");
            }
        }
    }
}
