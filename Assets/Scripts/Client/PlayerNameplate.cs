using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Shows the player nickname above runners and Kaplao cursors in world space.
    /// Assign a child nameplate in the prefab, or leave empty to auto-create one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    [AddComponentMenu("Kaplao/Client/Player Nameplate")]
    public class PlayerNameplate : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] float heightOffset = 1.35f;
        [SerializeField] Vector2 canvasSize = new Vector2(2.5f, 0.5f);
        [SerializeField] float canvasScale = 0.01f;

        [Header("Optional Prefab References")]
        [SerializeField] RectTransform nameplateRoot;
        [SerializeField] TextMeshProUGUI label;

        NetworkPlayer networkPlayer;
        KaplaoCursorServer cursorServer;
        RectTransform canvasRect;

        void Awake()
        {
            networkPlayer = GetComponent<NetworkPlayer>();
            cursorServer = GetComponent<KaplaoCursorServer>();
        }

        void OnEnable()
        {
            if (networkPlayer == null)
                return;

            networkPlayer.DisplayNameChanged += OnDisplayNameChanged;
            networkPlayer.RoleChanged += OnRoleChanged;
            networkPlayer.AccountEmailChanged += OnAccountEmailChanged;
            networkPlayer.NicknamePresetChanged += OnNicknamePresetChanged;
            networkPlayer.NicknamePresetStyleVersionChanged += OnNicknamePresetStyleVersionChanged;
            if (cursorServer != null)
                cursorServer.HostGameModeChanged += OnHostGameModeChanged;
            CreateModeEditorSettings.Changed += OnEditorSettingsChanged;
            KaplaoNicknamePresetCatalog.Changed += OnNicknameCatalogChanged;
        }

        void Start()
        {
            EnsureNameplate();
            KaplaoNicknamePresetCatalog.RequestRefresh();
            Refresh();
        }

        void OnDisable()
        {
            if (networkPlayer != null)
            {
                networkPlayer.DisplayNameChanged -= OnDisplayNameChanged;
                networkPlayer.RoleChanged -= OnRoleChanged;
                networkPlayer.AccountEmailChanged -= OnAccountEmailChanged;
                networkPlayer.NicknamePresetChanged -= OnNicknamePresetChanged;
                networkPlayer.NicknamePresetStyleVersionChanged -= OnNicknamePresetStyleVersionChanged;
            }

            if (cursorServer != null)
                cursorServer.HostGameModeChanged -= OnHostGameModeChanged;

            CreateModeEditorSettings.Changed -= OnEditorSettingsChanged;
            KaplaoNicknamePresetCatalog.Changed -= OnNicknameCatalogChanged;
        }

        void LateUpdate()
        {
            if (canvasRect == null)
                return;

            canvasRect.position = transform.position + Vector3.up * heightOffset;

            if (Camera.main != null)
                canvasRect.rotation = Camera.main.transform.rotation;
        }

        void OnDisplayNameChanged(string _) => Refresh();
        void OnRoleChanged(PlayerRole _) => Refresh();
        void OnAccountEmailChanged(string _) => Refresh();
        void OnNicknamePresetChanged(string _) 
        {
            // Refresh the preset catalog to ensure we have the latest style definition
            // in case the admin updated the preset style on the website
            KaplaoNicknamePresetCatalog.RequestRefresh(force: true);
            Refresh();
        }
        void OnNicknamePresetStyleVersionChanged()
        {
            // Server bumped the style version — the preset style was changed in the admin panel
            // without changing the preset name. Force a catalog re-fetch so the new style
            // is downloaded and applied to all nameplates immediately.
            KaplaoNicknamePresetCatalog.RequestRefresh(force: true);
            Refresh();
        }
        void OnNicknameCatalogChanged() => Refresh();
        void OnHostGameModeChanged(HostGameMode _) => Refresh();
        void OnEditorSettingsChanged() => Refresh();

        void Refresh()
        {
            if (label == null || networkPlayer == null)
                return;

            bool hideForEditor = ShouldHideForEditorMode();
            bool show = !hideForEditor &&
                        (networkPlayer.role == PlayerRole.Runner || networkPlayer.role == PlayerRole.Kaplao) &&
                        !string.IsNullOrWhiteSpace(networkPlayer.displayName);

            if (nameplateRoot != null)
                nameplateRoot.gameObject.SetActive(show);
            else
                label.gameObject.SetActive(show);

            if (show)
            {
                NicknameStyle.EnsureWorldSpaceTmpCanvas(label.GetComponentInParent<Canvas>());
                label.text = networkPlayer.displayName;
                NicknameStyle.Apply(label, networkPlayer.accountEmail, networkPlayer.nicknamePreset);
            }
        }

        bool ShouldHideForEditorMode()
        {
            if (cursorServer != null)
            {
                HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
                if (mode == HostGameMode.Create || mode == HostGameMode.Code)
                    return true;
            }

            if (IsLocalViewerInCreateMode() && !CreateModeEditorSettings.ShowPlayers)
                return true;

            return false;
        }

        static bool IsLocalViewerInCreateMode()
        {
            if (NetworkClient.localPlayer == null)
                return false;

            if (!NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer localCursor))
                return false;

            return KaplaoLocalGameModePredictor.IsCreate(localCursor);
        }

        void EnsureNameplate()
        {
            if (label != null)
            {
                canvasRect = label.rectTransform.parent as RectTransform;
                if (canvasRect == null)
                    canvasRect = label.rectTransform;

                NicknameStyle.EnsureWorldSpaceTmpCanvas(label.GetComponentInParent<Canvas>());
                return;
            }

            CreateNameplate();
        }

        void CreateNameplate()
        {
            GameObject root = new GameObject("Nameplate");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * heightOffset;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 100;
            NicknameStyle.EnsureWorldSpaceTmpCanvas(canvas);

            canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = canvasSize;
            canvasRect.localScale = Vector3.one * canvasScale;
            nameplateRoot = canvasRect;

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(root.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            label = textObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 36;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            // Outline is applied via material; custom privilege fonts break with leftover outline.
            label.outlineWidth = 0f;

            if (TMP_Settings.defaultFontAsset != null)
                NicknameStyle.AssignFont(label, TMP_Settings.defaultFontAsset);
        }
    }
}
