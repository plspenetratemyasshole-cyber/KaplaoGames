#if UNITY_EDITOR
using System.Text;
using KaplaoGames.Network;
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class MainMenuSceneUiBinder
    {
        const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Kaplao/Bind UI/Main Menu")]
        public static void BindMainMenuUi()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in MainMenu scene.");
                return;
            }

            MainMenuUI menuUi = canvas.GetComponent<MainMenuUI>();
            if (menuUi == null)
                menuUi = canvas.gameObject.AddComponent<MainMenuUI>();

            Transform root = canvas.transform;
            var report = new StringBuilder();
            report.AppendLine("[Kaplao] Main Menu UI bind:");

            SerializedObject serialized = new SerializedObject(menuUi);

            SceneUiBinderUtility.SetObject(serialized, "mainPanel",
                SceneUiBinderUtility.FindRelative(root, "MainPanel")?.gameObject, report, "MainPanel");
            SceneUiBinderUtility.SetObject(serialized, "nicknamePanel",
                SceneUiBinderUtility.FindRelative(root, "NicknamePanel")?.gameObject, report, "NicknamePanel");
            SceneUiBinderUtility.SetObject(serialized, "singlePlayerPanel",
                SceneUiBinderUtility.FindRelative(root, "SinglePlayerPanel")?.gameObject, report, "SinglePlayerPanel");
            SceneUiBinderUtility.SetObject(serialized, "networkPanel",
                SceneUiBinderUtility.FindRelative(root, "NetworkPanel")?.gameObject, report, "NetworkPanel");
            SceneUiBinderUtility.SetObject(serialized, "createWorldNamePanel",
                SceneUiBinderUtility.FindRelative(root, "CreateWorldNamePanel")?.gameObject, report, "CreateWorldNamePanel");
            SceneUiBinderUtility.SetObject(serialized, "deleteWorldConfirmPanel",
                SceneUiBinderUtility.FindRelative(root, "DeleteWorldConfirmPanel")?.gameObject, report, "DeleteWorldConfirmPanel");
            SceneUiBinderUtility.SetObject(serialized, "settingsPanel",
                SceneUiBinderUtility.FindRelative(root, "SettingsPanel")?.gameObject, report, "SettingsPanel (optional)");

            SceneUiBinderUtility.SetObject(serialized, "singlePlayerButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "MainPanel/SinglePlayerButton"), report, "MainPanel/SinglePlayerButton");
            SceneUiBinderUtility.SetObject(serialized, "networkGameButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "MainPanel/NetworkGameButton"), report, "MainPanel/NetworkGameButton");
            SceneUiBinderUtility.SetObject(serialized, "settingsButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "MainPanel/SettingsButton"), report, "MainPanel/SettingsButton (optional)");
            SceneUiBinderUtility.SetObject(serialized, "exitButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "MainPanel/ExitButton"), report, "MainPanel/ExitButton");

            SceneUiBinderUtility.SetObject(serialized, "settingsPortInput",
                SceneUiBinderUtility.GetComponentOnPath<TMP_InputField>(root, "SettingsPanel/PortInput"), report, "SettingsPanel/PortInput (optional)");
            SceneUiBinderUtility.SetObject(serialized, "settingsNetworkDropdown",
                SceneUiBinderUtility.GetComponentOnPath<TMP_Dropdown>(root, "SettingsPanel/NetworkDropdown"), report, "SettingsPanel/NetworkDropdown (optional)");
            SceneUiBinderUtility.SetObject(serialized, "settingsTransportDropdown",
                SceneUiBinderUtility.GetComponentOnPath<TMP_Dropdown>(root, "SettingsPanel/TransportDropdown"), report, "SettingsPanel/TransportDropdown (optional)");
            SceneUiBinderUtility.SetObject(serialized, "settingsBackButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "SettingsPanel/BackButton"), report, "SettingsPanel/BackButton (optional)");

            SceneUiBinderUtility.SetObject(serialized, "nicknameInput",
                SceneUiBinderUtility.GetComponentOnPath<TMP_InputField>(root, "NicknamePanel/NicknameInput"), report, "NicknamePanel/NicknameInput");
            SceneUiBinderUtility.SetObject(serialized, "nicknameContinueButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "NicknamePanel/ContinueButton"), report, "NicknamePanel/ContinueButton");
            SceneUiBinderUtility.SetObject(serialized, "nicknameBackButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "NicknamePanel/BackButton"), report, "NicknamePanel/BackButton");

            SceneUiBinderUtility.SetObject(serialized, "singlePlayerBackButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "SinglePlayerPanel/BackButton"), report, "SinglePlayerPanel/BackButton");
            SceneUiBinderUtility.SetObject(serialized, "worldListContent",
                SceneUiBinderUtility.FindRelative(root, "SinglePlayerPanel/WorldScrollView/Viewport/Content"), report, "SinglePlayerPanel/WorldScrollView/Viewport/Content");
            SceneUiBinderUtility.SetObject(serialized, "worldEntryTemplate",
                SceneUiBinderUtility.FindRelative(root, "SinglePlayerPanel/WorldScrollView/Viewport/Content/WorldEntryTemplate")?.gameObject, report, "WorldEntryTemplate");
            SceneUiBinderUtility.SetObject(serialized, "worldActionsBar",
                SceneUiBinderUtility.FindRelative(root, "SinglePlayerPanel/WorldActionsBar")?.gameObject, report, "SinglePlayerPanel/WorldActionsBar");
            SceneUiBinderUtility.SetObject(serialized, "worldPlayButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "SinglePlayerPanel/WorldActionsBar/PlayButton"), report, "SinglePlayerPanel/WorldActionsBar/PlayButton");
            SceneUiBinderUtility.SetObject(serialized, "worldDeleteButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "SinglePlayerPanel/WorldActionsBar/DeleteButton"), report, "SinglePlayerPanel/WorldActionsBar/DeleteButton");
            SceneUiBinderUtility.SetObject(serialized, "openSavesFolderButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "SinglePlayerPanel/BottomBar/OpenSavesFolderButton"), report, "SinglePlayerPanel/BottomBar/OpenSavesFolderButton");
            SceneUiBinderUtility.SetObject(serialized, "createNewWorldButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "SinglePlayerPanel/BottomBar/CreateNewWorldButton"), report, "SinglePlayerPanel/BottomBar/CreateNewWorldButton");
            SceneUiBinderUtility.SetObject(serialized, "emptyWorldsLabel",
                SceneUiBinderUtility.GetComponentOnPath<TextMeshProUGUI>(root, "SinglePlayerPanel/EmptyStateLabel"), report, "SinglePlayerPanel/EmptyStateLabel (optional)");

            SceneUiBinderUtility.SetObject(serialized, "worldNameInput",
                SceneUiBinderUtility.GetComponentOnPath<TMP_InputField>(root, "CreateWorldNamePanel/WorldNameInput"), report, "CreateWorldNamePanel/WorldNameInput");
            SceneUiBinderUtility.SetObject(serialized, "createWorldContinueButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "CreateWorldNamePanel/ContinueButton"), report, "CreateWorldNamePanel/ContinueButton");
            SceneUiBinderUtility.SetObject(serialized, "createWorldBackButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "CreateWorldNamePanel/BackButton"), report, "CreateWorldNamePanel/BackButton");

            SceneUiBinderUtility.SetObject(serialized, "confirmDeleteWorldButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "DeleteWorldConfirmPanel/DialogBox/ConfirmDeleteButton"), report, "DeleteWorldConfirmPanel/DialogBox/ConfirmDeleteButton");
            SceneUiBinderUtility.SetObject(serialized, "cancelDeleteWorldButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "DeleteWorldConfirmPanel/DialogBox/CancelButton"), report, "DeleteWorldConfirmPanel/DialogBox/CancelButton");

            SceneUiBinderUtility.SetObject(serialized, "networkStatusText",
                SceneUiBinderUtility.GetComponentOnPath<TextMeshProUGUI>(root, "NetworkPanel/StatusText"), report, "NetworkPanel/StatusText");
            SceneUiBinderUtility.SetObject(serialized, "serverListContent",
                SceneUiBinderUtility.FindRelative(root, "NetworkPanel/ServerScrollView/Viewport/Content"), report, "NetworkPanel/ServerScrollView/Viewport/Content");
            SceneUiBinderUtility.SetObject(serialized, "serverEntryTemplate",
                SceneUiBinderUtility.FindRelative(root, "NetworkPanel/ServerScrollView/Viewport/Content/ServerEntryTemplate")?.gameObject, report, "ServerEntryTemplate");
            SceneUiBinderUtility.SetObject(serialized, "refreshServersButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "NetworkPanel/RefreshButton"), report, "NetworkPanel/RefreshButton");
            SceneUiBinderUtility.SetObject(serialized, "ipInput",
                SceneUiBinderUtility.GetComponentOnPath<TMP_InputField>(root, "NetworkPanel/IPInput"), report, "NetworkPanel/IPInput");
            SceneUiBinderUtility.SetObject(serialized, "connectDirectlyButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "NetworkPanel/ConnectDirectlyButton"), report, "NetworkPanel/ConnectDirectlyButton");
            SceneUiBinderUtility.SetObject(serialized, "networkBackButton",
                SceneUiBinderUtility.GetComponentOnPath<Button>(root, "NetworkPanel/BackButton"), report, "NetworkPanel/BackButton");

            KaplaoNetworkManager networkManager = Object.FindFirstObjectByType<KaplaoNetworkManager>();
            SceneUiBinderUtility.SetObject(serialized, "networkManager", networkManager, report, "KaplaoNetworkManager");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            SceneUiBinderUtility.MarkDirty(menuUi, scene);

            Debug.Log(report.ToString());
        }
    }
}
#endif
