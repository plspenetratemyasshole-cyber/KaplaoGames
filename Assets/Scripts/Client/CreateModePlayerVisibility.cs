using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Hides player body renderers while a local Kaplao is in Create mode with Show players off,
    /// and always hides the local Kaplao body in Create/Code modes.
    /// Colliders are disabled with the body so invisible Kaplao/runners do not block movement.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    public class CreateModePlayerVisibility : MonoBehaviour
    {
        NetworkPlayer networkPlayer;
        KaplaoCursorServer cursorServer;
        KaplaoCursorServer localViewerCursor;
        Renderer[] renderers;
        bool[] rendererWasEnabled;
        Collider2D[] colliders;
        bool[] colliderWasEnabled;
        bool lastHide;

        void Awake()
        {
            networkPlayer = GetComponent<NetworkPlayer>();
            cursorServer = GetComponent<KaplaoCursorServer>();
            CacheRenderers();
            CacheColliders();
        }

        void OnEnable()
        {
            if (cursorServer != null)
                cursorServer.HostGameModeChanged += OnModeChanged;
            if (networkPlayer != null)
                networkPlayer.RoleChanged += OnRoleChanged;
            CreateModeEditorSettings.Changed += OnSettingsChanged;
            Apply();
        }

        void OnDisable()
        {
            if (cursorServer != null)
                cursorServer.HostGameModeChanged -= OnModeChanged;
            if (networkPlayer != null)
                networkPlayer.RoleChanged -= OnRoleChanged;
            CreateModeEditorSettings.Changed -= OnSettingsChanged;
            UnsubscribeLocalViewer();
            Restore();
        }

        void Update()
        {
            EnsureLocalViewerSubscription();
            bool hide = ShouldHide();
            if (hide != lastHide)
                Apply();
        }

        void OnModeChanged(HostGameMode _) => Apply();
        void OnRoleChanged(PlayerRole _) => Apply();
        void OnSettingsChanged() => Apply();
        void OnLocalViewerModeChanged(HostGameMode _) => Apply();

        void EnsureLocalViewerSubscription()
        {
            KaplaoCursorServer viewer = null;
            if (NetworkClient.localPlayer != null)
                NetworkClient.localPlayer.TryGetComponent(out viewer);

            if (viewer == localViewerCursor)
                return;

            UnsubscribeLocalViewer();
            localViewerCursor = viewer;
            if (localViewerCursor != null)
                localViewerCursor.HostGameModeChanged += OnLocalViewerModeChanged;
        }

        void UnsubscribeLocalViewer()
        {
            if (localViewerCursor != null)
                localViewerCursor.HostGameModeChanged -= OnLocalViewerModeChanged;
            localViewerCursor = null;
        }

        void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            rendererWasEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                rendererWasEnabled[i] = renderers[i] != null && renderers[i].enabled;
        }

        void CacheColliders()
        {
            colliders = GetComponentsInChildren<Collider2D>(true);
            colliderWasEnabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
                colliderWasEnabled[i] = colliders[i] != null && colliders[i].enabled;
        }

        void Apply()
        {
            if (renderers == null || renderers.Length == 0)
                CacheRenderers();
            if (colliders == null || colliders.Length == 0)
                CacheColliders();

            bool hide = ShouldHide();
            lastHide = hide;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                if (IsEditorHitboxOverlayRenderer(renderers[i]))
                    continue;

                if (hide)
                {
                    if (renderers[i].enabled)
                        rendererWasEnabled[i] = true;
                    renderers[i].enabled = false;
                }
                else if (rendererWasEnabled[i])
                {
                    renderers[i].enabled = true;
                }
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                // Palm / tool cursors are managed separately; never touch them here.
                if (IsPalmOrToolCursorCollider(colliders[i]))
                    continue;

                if (hide)
                {
                    if (colliders[i].enabled)
                        colliderWasEnabled[i] = true;
                    colliders[i].enabled = false;
                }
                else if (colliderWasEnabled[i])
                {
                    colliders[i].enabled = true;
                }
            }
        }

        static bool IsPalmOrToolCursorCollider(Collider2D collider)
        {
            Transform root = collider.transform;
            while (root != null)
            {
                string objectName = root.name;
                if (objectName == "PalmCursor" ||
                    objectName == "GrabCursor" ||
                    objectName == "Crosshair" ||
                    objectName == "TilePreview")
                    return true;
                root = root.parent;
            }

            return false;
        }

        static bool IsEditorHitboxOverlayRenderer(Renderer renderer)
        {
            Transform root = renderer.transform;
            while (root != null)
            {
                string objectName = root.name;
                if (objectName == "PlayerHitboxOutline" ||
                    objectName == "PlayerHitboxTrail" ||
                    objectName.StartsWith("Trail_", System.StringComparison.Ordinal))
                    return true;
                root = root.parent;
            }

            return false;
        }

        void Restore()
        {
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null &&
                        rendererWasEnabled[i] &&
                        !IsEditorHitboxOverlayRenderer(renderers[i]))
                        renderers[i].enabled = true;
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null &&
                        colliderWasEnabled[i] &&
                        !IsPalmOrToolCursorCollider(colliders[i]))
                        colliders[i].enabled = true;
                }
            }
        }

        bool ShouldHide()
        {
            if (cursorServer != null)
            {
                HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
                if (mode == HostGameMode.Create || mode == HostGameMode.Code)
                    return true;
            }

            if (!IsLocalViewerInCreateMode())
                return false;

            return !CreateModeEditorSettings.ShowPlayers;
        }

        static bool IsLocalViewerInCreateMode()
        {
            if (NetworkClient.localPlayer == null)
                return false;

            if (!NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer localCursor))
                return false;

            return KaplaoLocalGameModePredictor.IsCreate(localCursor);
        }
    }
}
