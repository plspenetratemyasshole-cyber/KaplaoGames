#if UNITY_EDITOR
using System.Text;
using KaplaoGames.Client;
using KaplaoGames.Server;
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class SampleSceneUiBinder
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Bind UI/Sample Scene")]
        public static void BindSampleSceneUi()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[Kaplao] Sample Scene UI bind:");

            BindPauseMenuUi(canvas, report);
            BindRunnerPauseMenuUi(canvas, report);
            BindGameSessionUi(canvas, report);
            BindWorldLoadBootstrap(report);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(report.ToString());
        }

        static void BindPauseMenuUi(Canvas canvas, StringBuilder report)
        {
            PauseMenuUI pauseMenuUi = canvas.GetComponent<PauseMenuUI>();
            if (pauseMenuUi == null)
                pauseMenuUi = canvas.gameObject.AddComponent<PauseMenuUI>();

            Transform root = canvas.transform;
            SerializedObject serialized = new SerializedObject(pauseMenuUi);

            SceneUiBinderUtility.SetObject(serialized, "pauseMenuRoot",
                SceneUiBinderUtility.FindRelative(root, "PauseMenuRoot")?.gameObject, report, "PauseMenuRoot");
            SceneUiBinderUtility.SetObject(serialized, "pauseMenuPanel",
                SceneUiBinderUtility.FindRelative(root, "PauseMenuRoot/PauseMenuPanel")?.gameObject, report, "PauseMenuRoot/PauseMenuPanel");
            SceneUiBinderUtility.SetObject(serialized, "saveSuccessPanel",
                SceneUiBinderUtility.FindRelative(root, "PauseMenuRoot/SaveSuccessPanel")?.gameObject, report, "PauseMenuRoot/SaveSuccessPanel");
            SceneUiBinderUtility.SetObject(serialized, "exitWithoutSavePanel",
                SceneUiBinderUtility.FindRelative(root, "PauseMenuRoot/ExitWithoutSavePanel")?.gameObject, report, "PauseMenuRoot/ExitWithoutSavePanel");

            SceneUiBinderUtility.SetObject(serialized, "resumeButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/PauseMenuPanel/ResumeButton"), report, "PauseMenuPanel/ResumeButton");
            SceneUiBinderUtility.SetObject(serialized, "saveButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/PauseMenuPanel/SaveButton"), report, "PauseMenuPanel/SaveButton");
            SceneUiBinderUtility.SetObject(serialized, "saveAndExitButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/PauseMenuPanel/SaveAndExitButton"), report, "PauseMenuPanel/SaveAndExitButton");
            SceneUiBinderUtility.SetObject(serialized, "exitButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/PauseMenuPanel/ExitButton"), report, "PauseMenuPanel/ExitButton");
            SceneUiBinderUtility.SetObject(serialized, "openToLanButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/PauseMenuPanel/OpenToLanButton"), report, "PauseMenuPanel/OpenToLanButton");

            SceneUiBinderUtility.SetObject(serialized, "saveSuccessCloseButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/SaveSuccessPanel/CloseButton"), report, "SaveSuccessPanel/CloseButton");
            SceneUiBinderUtility.SetObject(serialized, "confirmExitButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/ExitWithoutSavePanel/ConfirmExitButton"), report, "ExitWithoutSavePanel/ConfirmExitButton");
            SceneUiBinderUtility.SetObject(serialized, "cancelExitButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "PauseMenuRoot/ExitWithoutSavePanel/CancelButton"), report, "ExitWithoutSavePanel/CancelButton");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pauseMenuUi);
        }

        static void BindRunnerPauseMenuUi(Canvas canvas, StringBuilder report)
        {
            RunnerPauseMenuUI runnerPauseMenuUi = canvas.GetComponent<RunnerPauseMenuUI>();
            if (runnerPauseMenuUi == null)
                runnerPauseMenuUi = canvas.gameObject.AddComponent<RunnerPauseMenuUI>();

            Transform root = canvas.transform;
            SerializedObject serialized = new SerializedObject(runnerPauseMenuUi);

            SceneUiBinderUtility.SetObject(serialized, "pauseMenuRoot",
                SceneUiBinderUtility.FindRelative(root, "RunnerPauseMenuRoot")?.gameObject, report, "RunnerPauseMenuRoot");
            SceneUiBinderUtility.SetObject(serialized, "resumeButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "RunnerPauseMenuRoot/RunnerPauseMenuPanel/ResumeButton"), report, "RunnerPauseMenuPanel/ResumeButton");
            SceneUiBinderUtility.SetObject(serialized, "exitButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "RunnerPauseMenuRoot/RunnerPauseMenuPanel/ExitButton"), report, "RunnerPauseMenuPanel/ExitButton");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runnerPauseMenuUi);
        }

        static void BindGameSessionUi(Canvas canvas, StringBuilder report)
        {
            GameSessionUi sessionUi = canvas.GetComponent<GameSessionUi>();
            if (sessionUi == null)
            {
                report.AppendLine("  [skip] GameSessionUi not found on Canvas.");
                return;
            }

            Transform root = canvas.transform;
            SerializedObject serialized = new SerializedObject(sessionUi);

            SceneUiBinderUtility.SetObject(serialized, "legacyLeaveButton",
                SceneUiBinderUtility.FindRelative(root, "LeaveButton")?.gameObject, report, "LeaveButton (legacy, optional)");
            SceneUiBinderUtility.SetObject(serialized, "legacyLevelIoPanel",
                SceneUiBinderUtility.FindRelative(root, "CreateModeUI/LevelIoPanel")?.gameObject, report, "CreateModeUI/LevelIoPanel (legacy, optional)");

            KaplaoLevelIoUi levelIoUi = canvas.GetComponent<KaplaoLevelIoUi>();
            SceneUiBinderUtility.SetObject(serialized, "legacyLevelIoUi", levelIoUi, report, "KaplaoLevelIoUi (legacy, optional)");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sessionUi);
        }

        static void BindWorldLoadBootstrap(StringBuilder report)
        {
            LevelGridServer grid = Object.FindFirstObjectByType<LevelGridServer>();
            if (grid == null)
            {
                report.AppendLine("  [missing] LevelGridServer not found.");
                return;
            }

            if (grid.GetComponent<WorldLoadBootstrap>() == null)
            {
                grid.gameObject.AddComponent<WorldLoadBootstrap>();
                report.AppendLine("  [ok] WorldLoadBootstrap added to LevelGrid");
            }
            else
            {
                report.AppendLine("  [ok] WorldLoadBootstrap already on LevelGrid");
            }

            EditorUtility.SetDirty(grid);
        }
    }
}
#endif
