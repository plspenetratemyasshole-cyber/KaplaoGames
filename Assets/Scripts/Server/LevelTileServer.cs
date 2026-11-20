using System;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Placed level tile. Spike tiles kill runners on contact.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class LevelTileServer : NetworkBehaviour
    {
        static bool IsLocalKaplaoCreateMode()
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return HostGameModeState.Current == HostGameMode.Create;

            if (!LocalPlayerRole.IsKaplao())
                return false;

            KaplaoCursorServer cursor = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
            return KaplaoLocalGameModePredictor.IsCreate(cursor);
        }

        // Base (pre-script) state snapshot. Used to restore on StopScript().
        bool baseInitialized;
        bool baseNoHitbox;
        float baseTransparency = 1f;
        Vector2 restPosition;
        float restRotationDegrees;
        Vector2 restScale = Vector2.one;
        Color32 baseColor;
        string baseTextureId;

        [SyncVar(hook = nameof(OnTileColorChanged))]
        Color32 tileColor;

        [SyncVar(hook = nameof(OnTextureIdChanged))]
        string syncedTextureId = string.Empty;

        [SyncVar]
        TileType syncedTileType;

        [SyncVar(hook = nameof(OnTransformFloatChanged))]
        float syncedPositionX;

        [SyncVar(hook = nameof(OnTransformFloatChanged))]
        float syncedPositionY;

        [SyncVar(hook = nameof(OnTransformFloatChanged))]
        float syncedRotationDegrees;

        [SyncVar(hook = nameof(OnTransformFloatChanged))]
        float syncedScaleX = 1f;

        [SyncVar(hook = nameof(OnTransformFloatChanged))]
        float syncedScaleY = 1f;

        [SyncVar(hook = nameof(OnSortingOrderChanged))]
        int syncedSortingOrder;

        [SyncVar(hook = nameof(OnGroupsChanged))]
        string syncedGroups = string.Empty;

        [SyncVar(hook = nameof(OnQuarterTurnsChanged))]
        byte syncedQuarterTurns;

        [SyncVar(hook = nameof(OnTransparencyChanged))]
        float syncedTransparency = 1f;

        [SyncVar(hook = nameof(OnEditorLayerChanged))]
        int syncedEditorLayer;

        [SyncVar(hook = nameof(OnNoHitboxChanged))]
        bool syncedNoHitbox;

        [SyncVar(hook = nameof(OnScriptTransformChanged))]
        float syncedScriptOffsetX;

        [SyncVar(hook = nameof(OnScriptTransformChanged))]
        float syncedScriptOffsetY;

        [SyncVar(hook = nameof(OnScriptTransformChanged))]
        float syncedScriptRotationDegrees;

        [SyncVar(hook = nameof(OnScriptTransformChanged))]
        float syncedScriptScaleX = 1f;

        [SyncVar(hook = nameof(OnScriptTransformChanged))]
        float syncedScriptScaleY = 1f;

        [SyncVar(hook = nameof(OnPhysicsEnabledChanged))]
        bool syncedPhysicsEnabled;

        [SyncVar]
        long syncedCreatedAtUnixMs;

        [SyncVar(hook = nameof(OnSpawnPointChanged))]
        bool syncedIsSpawnPoint;

        public bool PhysicsEnabled => syncedPhysicsEnabled;
        public long CreatedAtUnixMs => syncedCreatedAtUnixMs;
        public bool IsSpawnPoint => syncedIsSpawnPoint;
        public TileType TileType => syncedTileType;
        public bool IsSpike => syncedTileType == TileType.Spike;
        public bool IsTrigger => syncedTileType == TileType.Trigger;
        public bool IsParticle => syncedTileType == TileType.Particle;
        public bool IsKeyframe => syncedTileType == TileType.Keyframe;
        public bool IsExecutor => syncedTileType == TileType.Executor;
        public float Transparency => syncedTransparency;
        public bool IsInvisible => syncedTransparency <= 0f;
        public int EditorLayer => syncedEditorLayer;
        public bool HasNoHitbox => syncedNoHitbox;
        public string GroupsDisplay => syncedGroups;
        public Color32 TileColor => tileColor;
        public string TextureId => syncedTextureId;
        public Vector2 Position => new Vector2(syncedPositionX, syncedPositionY);
        public Vector2 Scale => new Vector2(syncedScaleX, syncedScaleY);
        public Vector2 RestPosition => baseInitialized ? restPosition : Position;
        public float RestRotationDegrees => baseInitialized ? restRotationDegrees : PlacementRotationDegrees;
        public Vector2 RestScale => baseInitialized ? restScale : Scale;
        public Vector2Int Cell => GridMath.WorldToCell(Position);
        public byte QuarterTurns => syncedQuarterTurns;
        public float PlacementRotationDegrees => syncedRotationDegrees;
        public int SortingOrder => syncedSortingOrder;

        int[] cachedGroupIds = System.Array.Empty<int>();
        bool groupCacheDirty = true;

        public IReadOnlyList<int> CachedGroupIds
        {
            get
            {
                EnsureGroupCache();
                return cachedGroupIds;
            }
        }

        public bool HasGroup(int group)
        {
            EnsureGroupCache();
            for (int i = 0; i < cachedGroupIds.Length; i++)
            {
                if (cachedGroupIds[i] == group)
                    return true;
            }

            return false;
        }

        void EnsureGroupCache()
        {
            if (!groupCacheDirty)
                return;

            cachedGroupIds = TileGroups.ParseNumbers(syncedGroups);
            groupCacheDirty = false;
        }

        public void InvalidateGroupCache()
        {
            groupCacheDirty = true;
        }

        public Vector3 GetBaseWorldPosition()
        {
            return new Vector3(syncedPositionX, syncedPositionY, 0f);
        }

        public void ApplyScriptTransform(Vector2 worldOffset, float rotationDegrees)
        {
            ApplyScriptTransform(worldOffset, rotationDegrees, 1f, 1f);
        }

        public void ApplyScriptTransform(Vector2 worldOffset, float rotationDegrees, float scaleX, float scaleY)
        {
            scriptWorldOffset = worldOffset;
            scriptRotationDegrees = rotationDegrees;
            scriptScaleX = scaleX;
            scriptScaleY = scaleY;
            if (isServer)
            {
                syncedScriptOffsetX = worldOffset.x;
                syncedScriptOffsetY = worldOffset.y;
                syncedScriptRotationDegrees = rotationDegrees;
                syncedScriptScaleX = scaleX;
                syncedScriptScaleY = scaleY;
            }
            ApplyWorldTransform();
        }

        [Server]
        public void ServerApplyScriptWorldDelta(Vector2 worldDelta)
        {
            if (worldDelta.sqrMagnitude <= 0f)
                return;

            if (scriptPivot != null)
            {
                transform.position += (Vector3)worldDelta;
                // Network sync is flushed on a throttled LateUpdate tick.
                return;
            }

            ApplyScriptTransform(scriptWorldOffset + worldDelta, scriptRotationDegrees);
        }

        [Server]
        public void ServerSyncScriptStateFromCurrentTransform(bool roundValues = true)
        {
            Vector3 basePosition = GetBaseWorldPosition();
            Vector3 worldPosition = transform.position;
            float worldRotation = transform.eulerAngles.z;

            Vector2 offset = worldPosition - basePosition;
            float rotationDelta = Mathf.DeltaAngle(PlacementRotationDegrees, worldRotation);
            Vector3 lossy = transform.lossyScale;
            float scaleX = syncedScaleX > 0.0001f ? lossy.x / syncedScaleX : 1f;
            float scaleY = syncedScaleY > 0.0001f ? lossy.y / syncedScaleY : 1f;

            if (roundValues)
            {
                offset.x = Mathf.Round(offset.x * 20f) * 0.05f;
                offset.y = Mathf.Round(offset.y * 20f) * 0.05f;
                rotationDelta = Mathf.Round(rotationDelta * 20f) * 0.05f;
                scaleX = Mathf.Round(scaleX * 20f) * 0.05f;
                scaleY = Mathf.Round(scaleY * 20f) * 0.05f;
            }

            scriptWorldOffset = offset;
            scriptRotationDegrees = rotationDelta;
            scriptScaleX = scaleX;
            scriptScaleY = scaleY;

            // Avoid dirtying SyncVars every frame when the pose did not change.
            // Coarse epsilon after rounding: continuous script motion used to flood Mirror.
            float epsilon = roundValues ? 0.04f : 0.0001f;
            if (Mathf.Abs(syncedScriptOffsetX - offset.x) <= epsilon &&
                Mathf.Abs(syncedScriptOffsetY - offset.y) <= epsilon &&
                Mathf.Abs(Mathf.DeltaAngle(syncedScriptRotationDegrees, rotationDelta)) <= epsilon &&
                Mathf.Abs(syncedScriptScaleX - scaleX) <= epsilon &&
                Mathf.Abs(syncedScriptScaleY - scaleY) <= epsilon)
            {
                return;
            }

            syncedScriptOffsetX = offset.x;
            syncedScriptOffsetY = offset.y;
            syncedScriptRotationDegrees = rotationDelta;
            syncedScriptScaleX = scaleX;
            syncedScriptScaleY = scaleY;
        }

        public void SetGroupLabelsVisible(bool visible)
        {
            groupLabelsVisible = visible;
            ApplyAppearanceFlags();
        }

        public void SetTriggerTileVisible(bool visible)
        {
            if (!IsTrigger && !IsParticle && !IsKeyframe && !IsExecutor)
                return;

            EnsureVisuals();
            ApplyAppearanceFlags();
        }

        SpriteRenderer spriteRenderer;
        Sprite defaultSprite;
        TextMeshProUGUI groupLabel;
        SpriteRenderer selectionOutline;
        LineRenderer hitboxOutline;
        Collider2D[] tileColliders;
        bool groupLabelsVisible = true;
        bool cameraCulled;
        bool logicalSpriteVisible = true;
        Vector2 scriptWorldOffset;
        float scriptRotationDegrees;
        float scriptScaleX = 1f;
        float scriptScaleY = 1f;
        Transform scriptPivot;
        Rigidbody2D physicsBody;

        public bool IsAttachedToScriptPivot => scriptPivot != null;
        public Transform ScriptPivotTransform => scriptPivot;

        /// <summary>
        /// Client-only: hide renderers outside the camera frustum (keeps colliders/network intact).
        /// </summary>
        public void SetCameraCulled(bool culled)
        {
            if (cameraCulled == culled)
                return;

            cameraCulled = culled;
            ApplyRendererEnabledState();
            ApplyHitboxOutline();
        }

        public void AttachToScriptPivot(Transform pivot)
        {
            if (pivot == null)
                return;

            scriptPivot = pivot;
            transform.SetParent(pivot, true);
        }

        public void DetachFromScriptPivot()
        {
            if (scriptPivot == null)
                return;

            Vector3 worldPosition = transform.position;
            Quaternion worldRotation = transform.rotation;
            Vector3 lossyScale = transform.lossyScale;
            transform.SetParent(null, true);
            scriptPivot = null;

            Vector3 basePosition = GetBaseWorldPosition();
            scriptWorldOffset = worldPosition - basePosition;
            float placementRotation = PlacementRotationDegrees;
            scriptRotationDegrees = Mathf.DeltaAngle(placementRotation, worldRotation.eulerAngles.z);
            scriptScaleX = syncedScaleX > 0.0001f ? lossyScale.x / syncedScaleX : 1f;
            scriptScaleY = syncedScaleY > 0.0001f ? lossyScale.y / syncedScaleY : 1f;
            if (isServer)
            {
                syncedScriptScaleX = scriptScaleX;
                syncedScriptScaleY = scriptScaleY;
            }
            ApplyWorldTransform();
        }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                defaultSprite = spriteRenderer.sprite;
            tileColliders = GetComponents<Collider2D>();
            physicsBody = GetComponent<Rigidbody2D>();
            EnsureVisuals();
            ApplyPhysicsBodyState();
        }

        [Server]
        public void ServerEnablePhysics()
        {
            DetachFromScriptPivot();
            syncedPhysicsEnabled = true;
            ApplyPhysicsBodyState();
        }

        [Server]
        public void ServerDisablePhysics()
        {
            if (!syncedPhysicsEnabled)
                return;

            syncedPhysicsEnabled = false;
            ApplyPhysicsBodyState();
            ApplyWorldTransform();
        }

        void FixedUpdate()
        {
            if (!isServer || !syncedPhysicsEnabled)
                return;

            ServerSyncScriptStateFromCurrentTransform(roundValues: false);
        }

        [Server]
        public void ServerBeginScriptDrivenMotion()
        {
            if (physicsBody == null)
                physicsBody = GetComponent<Rigidbody2D>();

            // Static bodies do not reliably follow animated poses / temporary parents.
            if (physicsBody != null && !syncedPhysicsEnabled)
            {
                physicsBody.bodyType = RigidbodyType2D.Kinematic;
                physicsBody.simulated = true;
            }
        }

        [Server]
        public void ServerEndScriptDrivenMotion()
        {
            ApplyPhysicsBodyState();
            ApplyWorldTransform();
        }

        void ApplyPhysicsBodyState()
        {
            if (physicsBody == null)
                physicsBody = GetComponent<Rigidbody2D>();

            if (physicsBody == null)
                return;

            if (!syncedPhysicsEnabled)
            {
                physicsBody.bodyType = RigidbodyType2D.Static;
                physicsBody.simulated = true;
                return;
            }

            DetachFromScriptPivot();
            transform.SetParent(null, true);

            physicsBody.bodyType = isServer ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            physicsBody.gravityScale = 1f;
            physicsBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            physicsBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            physicsBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            physicsBody.simulated = true;

            if (isServer)
            {
                physicsBody.position = transform.position;
                physicsBody.rotation = transform.eulerAngles.z;
            }
        }

        void OnPhysicsEnabledChanged(bool oldValue, bool newValue)
        {
            ApplyPhysicsBodyState();
            if (!newValue)
                ApplyWorldTransform();
        }

        void OnSpawnPointChanged(bool oldValue, bool newValue)
        {
            if (KaplaoLocalEditPredictor.HandleServerSpawnPointSync(this, newValue))
                return;

            LevelScriptRuntime.Instance?.RefreshTileSpawnPointHandlers();
        }

        [Server]
        public void ServerSetCreatedAtUnixMs(long unixMs)
        {
            if (unixMs > 0)
                syncedCreatedAtUnixMs = unixMs;
        }

        [Server]
        public void ServerSetSpawnPoint(bool isSpawnPoint)
        {
            if (!IsTrigger)
                return;

            syncedIsSpawnPoint = isSpawnPoint;
        }

        [Server]
        public void ServerInit(
            Vector2 position,
            float rotationDegrees,
            TileType type,
            Color32 color,
            string groups = "",
            byte quarterTurns = 0,
            float transparency = 1f,
            bool noHitbox = false,
            float scaleX = 1f,
            float scaleY = 1f,
            string textureId = "",
            int sortingOrder = 0,
            int editorLayer = 0)
        {
            syncedPositionX = position.x;
            syncedPositionY = position.y;
            syncedRotationDegrees = rotationDegrees;
            syncedScaleX = Mathf.Max(0.01f, scaleX);
            syncedScaleY = Mathf.Max(0.01f, scaleY);
            syncedTileType = type;
            tileColor = color;
            syncedTextureId = NormalizeTextureId(textureId);
            syncedGroups = TileGroups.Normalize(groups);
            InvalidateGroupCache();
            syncedQuarterTurns = quarterTurns;
            syncedTransparency = Mathf.Clamp01(transparency);
            syncedNoHitbox = noHitbox;
            syncedSortingOrder = Mathf.Clamp(sortingOrder, -10, 10);
            syncedEditorLayer = Mathf.Clamp(editorLayer, 0, 100);

            if (syncedCreatedAtUnixMs <= 0)
                syncedCreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            CaptureBaseStateIfNeeded();

            ApplyWorldTransform();
        }

        [Server]
        public void ServerCaptureBaseState()
        {
            baseInitialized = true;
            baseNoHitbox = syncedNoHitbox;
            baseTransparency = syncedTransparency;
            baseColor = tileColor;
            baseTextureId = syncedTextureId;
            restPosition = new Vector2(syncedPositionX, syncedPositionY);
            restRotationDegrees = syncedRotationDegrees;
            restScale = new Vector2(syncedScaleX, syncedScaleY);
        }

        [Server]
        void CaptureBaseStateIfNeeded()
        {
            if (baseInitialized)
                return;

            ServerCaptureBaseState();
        }

        [Server]
        public void ServerRestoreBaseState()
        {
            CaptureBaseStateIfNeeded();
            ServerDisablePhysics();
            DetachFromScriptPivot();

            syncedPositionX = restPosition.x;
            syncedPositionY = restPosition.y;
            syncedRotationDegrees = restRotationDegrees;
            syncedScaleX = restScale.x;
            syncedScaleY = restScale.y;

            scriptWorldOffset = Vector2.zero;
            scriptRotationDegrees = 0f;
            scriptScaleX = 1f;
            scriptScaleY = 1f;
            syncedScriptOffsetX = 0f;
            syncedScriptOffsetY = 0f;
            syncedScriptRotationDegrees = 0f;
            syncedScriptScaleX = 1f;
            syncedScriptScaleY = 1f;
            syncedNoHitbox = baseNoHitbox;
            syncedTransparency = baseTransparency;
            tileColor = baseColor;
            syncedTextureId = baseTextureId;
            ApplyWorldTransform();
            ApplyAppearanceFlags();
        }

        public override void OnStartClient()
        {
            CreateModeEditorSettings.Changed += OnEditorSettingsChanged;
            RunnerHitboxCheat.Changed += OnEditorSettingsChanged;
            RefreshClientVisuals();
        }

        public override void OnStopClient()
        {
            CreateModeEditorSettings.Changed -= OnEditorSettingsChanged;
            RunnerHitboxCheat.Changed -= OnEditorSettingsChanged;
        }

        void OnEditorSettingsChanged()
        {
            ApplyHitboxOutline();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            TryBootstrapSceneTile();
        }

        [Server]
        void TryBootstrapSceneTile()
        {
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            if (syncedTileType == TileType.None)
                BootstrapFromSceneTransform();

            grid.RegisterExistingTile(gameObject);
        }

        [Server]
        void BootstrapFromSceneTransform()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            Vector2 position = transform.position;
            float rotation = transform.eulerAngles.z;
            Vector3 scale = transform.localScale;
            Color32 color = spriteRenderer != null
                ? (Color32)spriteRenderer.color
                : new Color32(115, 107, 97, 255);
            int sorting = spriteRenderer != null ? spriteRenderer.sortingOrder : 0;

            ServerInit(
                position,
                rotation,
                DetectBootstrapTileType(),
                color,
                sortingOrder: sorting,
                scaleX: scale.x,
                scaleY: scale.y);
        }

        TileType DetectBootstrapTileType()
        {
            string lowerName = gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("spike"))
                return TileType.Spike;
            if (lowerName.Contains("trigger"))
                return TileType.Trigger;
            return TileType.Block;
        }

        public void RefreshClientVisuals()
        {
            EnsureVisuals();

            if (syncedTileType == TileType.None)
                return;

            groupLabelsVisible = HostGameModeState.Current == HostGameMode.Create;
            ApplyColor(tileColor);
            ApplyTexture(syncedTextureId);
            ApplyGroups(syncedGroups);
            ApplyWorldTransform();
            ApplyAppearanceFlags();
        }

        [Server]
        public void ServerSetColor(Color32 color, bool updateBaseState = true)
        {
            tileColor = color;
            if (updateBaseState)
            {
                if (baseInitialized)
                    baseColor = color;
                else
                    ServerCaptureBaseState();
            }
        }

        [Server]
        public void ServerSetTexture(string textureId, bool updateBaseState = true)
        {
            syncedTextureId = NormalizeTextureId(textureId);
            if (updateBaseState)
            {
                if (baseInitialized)
                    baseTextureId = syncedTextureId;
                else
                    ServerCaptureBaseState();
            }
        }

        [Server]
        public void ServerSetSortingOrder(int sortingOrder)
        {
            syncedSortingOrder = Mathf.Clamp(sortingOrder, -10, 10);
        }

        static string NormalizeTextureId(string textureId) =>
            string.IsNullOrWhiteSpace(textureId) ? string.Empty : textureId.Trim();

        [Server]
        public void ServerSetGroups(string groups)
        {
            string oldGroups = syncedGroups;
            syncedGroups = TileGroups.Normalize(groups);
            InvalidateGroupCache();
            if (!string.Equals(oldGroups, syncedGroups, System.StringComparison.Ordinal))
                LevelGridServer.Instance?.NotifyTileGroupsChanged(this, oldGroups, syncedGroups);
        }

        [Server]
        public void ServerSetTransparency(float transparency, bool updateBaseState = true)
        {
            syncedTransparency = Mathf.Clamp01(transparency);
            if (updateBaseState)
            {
                if (baseInitialized)
                    baseTransparency = syncedTransparency;
                else
                    ServerCaptureBaseState();
            }
        }

        [Server]
        public void ServerSetEditorLayer(int editorLayer)
        {
            syncedEditorLayer = Mathf.Clamp(editorLayer, 0, 100);
        }

        [Server]
        public void ServerSetNoHitbox(bool noHitbox, bool updateBaseState = true)
        {
            syncedNoHitbox = noHitbox;
            if (updateBaseState)
            {
                if (baseInitialized)
                    baseNoHitbox = noHitbox;
                else
                    ServerCaptureBaseState();
            }
        }

        [Server]
        public void ServerSetTransform(Vector2 position, float rotationDegrees, Vector2? scale = null, bool roundValues = true)
        {
            Vector2 oldPosition = Position;

            if (roundValues)
            {
                syncedPositionX = Mathf.Round(position.x * 100f) * 0.01f;
                syncedPositionY = Mathf.Round(position.y * 100f) * 0.01f;
                syncedRotationDegrees = Mathf.Round(rotationDegrees * 100f) * 0.01f;
            }
            else
            {
                syncedPositionX = position.x;
                syncedPositionY = position.y;
                syncedRotationDegrees = rotationDegrees;
            }

            if (scale.HasValue)
            {
                float sx = Mathf.Max(0.01f, scale.Value.x);
                float sy = Mathf.Max(0.01f, scale.Value.y);
                if (roundValues)
                {
                    syncedScaleX = Mathf.Round(sx * 100f) * 0.01f;
                    syncedScaleY = Mathf.Round(sy * 100f) * 0.01f;
                }
                else
                {
                    syncedScaleX = sx;
                    syncedScaleY = sy;
                }
            }

            ApplyWorldTransform();
            LevelGridServer.Instance?.NotifyTilePositionChanged(this, oldPosition);
        }

        [Server]
        public void ServerSetScale(Vector2 scale, bool roundValues = false)
        {
            float sx = Mathf.Max(0.01f, scale.x);
            float sy = Mathf.Max(0.01f, scale.y);
            if (roundValues)
            {
                syncedScaleX = Mathf.Round(sx * 100f) * 0.01f;
                syncedScaleY = Mathf.Round(sy * 100f) * 0.01f;
            }
            else
            {
                syncedScaleX = sx;
                syncedScaleY = sy;
            }

            ApplyWorldTransform();
        }

        public void SetSelectionOutline(bool enabled)
        {
            EnsureVisuals();
            if (selectionOutline != null)
                selectionOutline.enabled = enabled;
        }

        void OnTileColorChanged(Color32 oldColor, Color32 newColor)
        {
            if (KaplaoLocalEditPredictor.HandleServerColorSync(this, newColor))
                return;
            ApplyColor(newColor);
        }

        void OnTransformFloatChanged(float _, float __)
        {
            if (KaplaoLocalEditPredictor.HandleServerTransformSync(this))
                return;
            ApplyWorldTransform();
        }

        void OnTextureIdChanged(string oldTextureId, string newTextureId)
        {
            if (KaplaoLocalEditPredictor.HandleServerTextureSync(this, newTextureId))
                return;
            ApplyTexture(newTextureId);
        }

        void OnGroupsChanged(string oldGroups, string newGroups)
        {
            InvalidateGroupCache();
            LevelGridServer.Instance?.NotifyTileGroupsChanged(this, oldGroups, newGroups);
            if (KaplaoLocalEditPredictor.HandleServerGroupsSync(this, newGroups))
                return;
            ApplyGroups(newGroups);
        }

        void OnQuarterTurnsChanged(byte oldTurns, byte newTurns) => ApplyWorldTransform();

        void OnSortingOrderChanged(int oldValue, int newValue)
        {
            if (KaplaoLocalEditPredictor.HandleServerAppearanceSync(this))
                return;
            ApplySortingOrder();
        }

        void OnTransparencyChanged(float oldValue, float newValue)
        {
            if (KaplaoLocalEditPredictor.HandleServerAppearanceSync(this))
                return;
            ApplyAppearanceFlags();
        }

        void OnEditorLayerChanged(int oldValue, int newValue)
        {
            if (KaplaoLocalEditPredictor.HandleServerAppearanceSync(this))
                return;
            ApplyAppearanceFlags();
        }

        void OnNoHitboxChanged(bool oldValue, bool newValue)
        {
            if (KaplaoLocalEditPredictor.HandleServerAppearanceSync(this))
                return;
            ApplyAppearanceFlags();
        }

        void OnScriptTransformChanged(float _, float __)
        {
            scriptWorldOffset = new Vector2(syncedScriptOffsetX, syncedScriptOffsetY);
            scriptRotationDegrees = syncedScriptRotationDegrees;
            scriptScaleX = syncedScriptScaleX;
            scriptScaleY = syncedScriptScaleY;
            ApplyWorldTransform();
        }

        const float InactiveLayerAlphaDivisor = 4f;
        const float CreateModeInvisibleAlpha = 0.5f;

        public static bool ExecutorEditStatesActive { get; set; }
        public static int ExecutorEditStatesTargetGroup { get; set; } = -1;

        public void RefreshClientAppearance()
        {
            if (!NetworkClient.active)
                return;

            ApplyAppearanceFlags();
        }

        float ComputeClientSpriteAlpha(bool createMode)
        {
            float alpha = syncedTransparency;
            if (createMode && alpha <= 0f)
                alpha = CreateModeInvisibleAlpha;

            if (createMode && ExecutorEditStatesActive)
            {
                bool isTarget = ExecutorEditStatesTargetGroup >= 0 && HasGroup(ExecutorEditStatesTargetGroup) && !IsExecutor;
                alpha = isTarget ? 0f : alpha * 0.5f;
            }

            if (createMode &&
                NetworkClient.active &&
                NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursor) &&
                KaplaoLocalGameModePredictor.IsCreate(cursor))
            {
                int activeLayer = cursor.ActiveEditorLayer;
                if (activeLayer >= 0 && syncedEditorLayer != activeLayer)
                    alpha /= InactiveLayerAlphaDivisor;
            }

            return Mathf.Clamp01(alpha);
        }

        void ApplyAppearanceFlags()
        {
            EnsureVisuals();

            bool createMode = IsLocalKaplaoCreateMode();
            bool isEditorOnlyTile = IsTrigger || IsParticle || IsKeyframe || IsExecutor;
            bool showEditorOnlyInCreate = isEditorOnlyTile && createMode;
            bool fullyTransparent = syncedTransparency <= 0f;
            logicalSpriteVisible = !fullyTransparent && (!isEditorOnlyTile || showEditorOnlyInCreate);

            if (spriteRenderer != null)
            {
                Color baseColor = tileColor;
                baseColor.a = ComputeClientSpriteAlpha(createMode);
                spriteRenderer.color = baseColor;
            }

            ApplyRendererEnabledState();
            ApplyColliderState();
            ApplyHitboxOutline();

            ApplyGroups(syncedGroups);
        }

        void ApplyRendererEnabledState()
        {
            bool show = logicalSpriteVisible && !cameraCulled;

            if (spriteRenderer != null)
                spriteRenderer.enabled = show;

            if (selectionOutline != null && selectionOutline.enabled && cameraCulled)
                selectionOutline.enabled = false;

            if (hitboxOutline != null && cameraCulled)
                hitboxOutline.enabled = false;

            if (groupLabel != null)
            {
                // Labels use GameObject active state; keep TMP in sync when culled.
                if (cameraCulled && groupLabel.gameObject.activeSelf)
                    groupLabel.canvasRenderer.cull = true;
                else if (groupLabel.canvasRenderer != null)
                    groupLabel.canvasRenderer.cull = false;
            }
        }

        void ApplyColliderState()
        {
            if (tileColliders == null || tileColliders.Length == 0)
                tileColliders = GetComponents<Collider2D>();

            bool collidersEnabled = !syncedNoHitbox;
            if ((IsKeyframe || IsExecutor) && !IsLocalKaplaoCreateMode())
                collidersEnabled = false;
            // Spikes must be triggers: solid spike colliders let runners stand/jump off the tip.
            bool forceTrigger = IsTrigger || IsParticle || IsSpike;
            for (int i = 0; i < tileColliders.Length; i++)
            {
                Collider2D collider = tileColliders[i];
                if (collider == null)
                    continue;

                // Trigger/particle/spike tiles must stay as overlap zones so runners cannot stand on them.
                if (forceTrigger)
                    collider.isTrigger = true;

                collider.enabled = collidersEnabled;
            }
        }

        void ApplyWorldTransform()
        {
            if (scriptPivot != null)
                return;

            Vector3 basePosition = GetBaseWorldPosition();
            Vector3 worldPosition = basePosition + (Vector3)scriptWorldOffset;
            float worldRotation = syncedRotationDegrees + scriptRotationDegrees;

            if (syncedPhysicsEnabled)
            {
                if (!isServer)
                {
                    transform.position = worldPosition;
                    transform.rotation = Quaternion.Euler(0f, 0f, worldRotation);
                    if (physicsBody != null)
                    {
                        physicsBody.position = worldPosition;
                        physicsBody.rotation = worldRotation;
                    }
                }

                transform.localScale = new Vector3(
                    syncedScaleX * syncedScriptScaleX,
                    syncedScaleY * syncedScriptScaleY,
                    1f);
                ApplyHitboxOutline();
                return;
            }

            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, worldRotation);
            transform.localScale = new Vector3(
                syncedScaleX * syncedScriptScaleX,
                syncedScaleY * syncedScriptScaleY,
                1f);

            // Keep temporary Kinematic bodies (script-driven motion) in sync.
            if (physicsBody != null && physicsBody.bodyType == RigidbodyType2D.Kinematic)
            {
                physicsBody.position = worldPosition;
                physicsBody.rotation = worldRotation;
            }

            ApplyHitboxOutline();
        }

        void ApplyColor(Color32 color)
        {
            if (spriteRenderer == null)
                return;

            Color displayColor = color;
            displayColor.a = ComputeClientSpriteAlpha(IsLocalKaplaoCreateMode());
            spriteRenderer.color = displayColor;
        }

        void ApplyTexture(string textureId)
        {
            if (spriteRenderer == null)
                return;

            Sprite sprite = ResolveSprite(textureId);
            spriteRenderer.sprite = sprite;

            if (selectionOutline != null)
                selectionOutline.sprite = sprite;
        }

        Sprite ResolveSprite(string textureId)
        {
            if (!string.IsNullOrEmpty(textureId) &&
                RuntimeBlockTextureRegistry.TryGetSprite(textureId, out Sprite runtimeSprite))
            {
                return runtimeSprite;
            }

            if (!string.IsNullOrEmpty(textureId) &&
                BlockTextureCatalog.Instance != null &&
                BlockTextureCatalog.Instance.TryGetSprite(textureId, out Sprite catalogSprite))
            {
                return catalogSprite;
            }

            return defaultSprite;
        }

        void ApplySortingOrder()
        {
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = syncedSortingOrder;

            if (selectionOutline != null)
                selectionOutline.sortingOrder = syncedSortingOrder + 2;

            if (hitboxOutline != null)
                hitboxOutline.sortingOrder = syncedSortingOrder + 3;

            if (groupLabel != null)
            {
                Canvas canvas = groupLabel.GetComponentInParent<Canvas>();
                if (canvas != null)
                    canvas.sortingOrder = syncedSortingOrder + 6;
            }
        }

        void ApplyHitboxOutline()
        {
            EnsureVisuals();
            if (hitboxOutline == null)
                return;

            bool showOutline = !cameraCulled &&
                               !syncedNoHitbox &&
                               ((IsLocalKaplaoCreateMode() && CreateModeEditorSettings.ShowHitboxes) ||
                                RunnerHitboxCheat.ShowHitboxes);
            hitboxOutline.enabled = showOutline;
            if (!showOutline)
                return;

            hitboxOutline.startColor = IsTrigger || IsParticle
                ? new Color(0.2f, 0.95f, 0.25f, 0.95f)
                : IsSpike
                    ? new Color(1f, 0.2f, 0.2f, 0.95f)
                    : new Color(0.2f, 0.55f, 1f, 0.95f);
            hitboxOutline.endColor = hitboxOutline.startColor;
            hitboxOutline.sortingOrder = syncedSortingOrder + 3;

            Vector3[] localPoints = BuildHitboxOutlinePoints();
            if (localPoints == null || localPoints.Length < 2)
            {
                hitboxOutline.enabled = false;
                return;
            }

            hitboxOutline.useWorldSpace = true;
            hitboxOutline.alignment = LineAlignment.View;
            hitboxOutline.positionCount = localPoints.Length;
            for (int i = 0; i < localPoints.Length; i++)
                hitboxOutline.SetPosition(i, transform.TransformPoint(localPoints[i]));
        }

        Vector3[] BuildHitboxOutlinePoints()
        {
            if (tileColliders == null || tileColliders.Length == 0)
                tileColliders = GetComponents<Collider2D>();

            Collider2D source = null;
            for (int i = 0; i < tileColliders.Length; i++)
            {
                if (tileColliders[i] != null)
                {
                    source = tileColliders[i];
                    break;
                }
            }

            if (source == null)
                return null;

            if (source is PolygonCollider2D polygon && polygon.pathCount > 0)
            {
                Vector2[] path = polygon.GetPath(0);
                if (path == null || path.Length == 0)
                    return null;

                Vector2 colliderOffset = polygon.offset;
                var points = new Vector3[path.Length + 1];
                for (int i = 0; i < path.Length; i++)
                    points[i] = path[i] + colliderOffset;
                points[path.Length] = points[0];
                return points;
            }

            if (source is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                Vector2 offset = box.offset;
                return new[]
                {
                    new Vector3(offset.x - half.x, offset.y - half.y, 0f),
                    new Vector3(offset.x - half.x, offset.y + half.y, 0f),
                    new Vector3(offset.x + half.x, offset.y + half.y, 0f),
                    new Vector3(offset.x + half.x, offset.y - half.y, 0f),
                    new Vector3(offset.x - half.x, offset.y - half.y, 0f)
                };
            }

            Bounds bounds = source.bounds;
            Vector3 min = transform.InverseTransformPoint(bounds.min);
            Vector3 max = transform.InverseTransformPoint(bounds.max);
            return new[]
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(min.x, max.y, 0f),
                new Vector3(max.x, max.y, 0f),
                new Vector3(max.x, min.y, 0f),
                new Vector3(min.x, min.y, 0f)
            };
        }

        void ApplyGroups(string groups)
        {
            EnsureVisuals();
            if (groupLabel == null)
                return;

            bool createMode = HostGameModeState.Current == HostGameMode.Create;
            bool hasGroups = createMode
                ? !string.IsNullOrEmpty(groups)
                : (groupLabelsVisible && !string.IsNullOrEmpty(groups) && syncedTransparency > 0f);
            groupLabel.gameObject.SetActive(hasGroups);
            if (hasGroups)
                groupLabel.text = groups;
        }

        void EnsureVisuals()
        {
            if (groupLabel == null)
            {
                Transform existingRoot = transform.Find("GroupLabel");
                if (existingRoot != null)
                {
                    groupLabel = existingRoot.GetComponentInChildren<TextMeshProUGUI>();
                    if (groupLabel == null)
                        Destroy(existingRoot.gameObject);
                }

                if (groupLabel == null)
                    groupLabel = CreateGroupLabel();
            }

            if (selectionOutline == null)
            {
                Transform existing = transform.Find("SelectionOutline");
                if (existing != null)
                    selectionOutline = existing.GetComponent<SpriteRenderer>();

                if (selectionOutline == null)
                {
                    GameObject outlineObject = new GameObject("SelectionOutline");
                    outlineObject.transform.SetParent(transform, false);
                    outlineObject.transform.localPosition = Vector3.zero;
                    outlineObject.transform.localScale = Vector3.one * 1.08f;
                    selectionOutline = outlineObject.AddComponent<SpriteRenderer>();
                    selectionOutline.sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
                    selectionOutline.color = new Color(0.2f, 0.95f, 0.25f, 0.55f);
                    selectionOutline.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 12;
                    selectionOutline.enabled = false;
                }
            }

            if (selectionOutline != null && spriteRenderer != null && selectionOutline.sprite == null)
                selectionOutline.sprite = spriteRenderer.sprite;

            if (hitboxOutline == null)
            {
                Transform existing = transform.Find("HitboxOutline");
                if (existing != null)
                    hitboxOutline = existing.GetComponent<LineRenderer>();

                if (hitboxOutline == null)
                {
                    GameObject outlineObject = new GameObject("HitboxOutline");
                    outlineObject.transform.SetParent(transform, false);
                    hitboxOutline = outlineObject.AddComponent<LineRenderer>();
                    hitboxOutline.useWorldSpace = true;
                    hitboxOutline.loop = false;
                    hitboxOutline.widthMultiplier = 0.04f;
                    hitboxOutline.numCornerVertices = 2;
                    hitboxOutline.numCapVertices = 2;
                    hitboxOutline.textureMode = LineTextureMode.Stretch;
                    hitboxOutline.alignment = LineAlignment.View;
                    hitboxOutline.material = new Material(Shader.Find("Sprites/Default"));
                    hitboxOutline.enabled = false;
                }
            }

            ApplySortingOrder();
        }

        TextMeshProUGUI CreateGroupLabel()
        {
            GameObject root = new GameObject("GroupLabel");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 10) + 6;

            RectTransform canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1f, 0.35f);
            canvasRect.localScale = Vector3.one * 0.01f;

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28f;
            label.color = Color.black;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            label.gameObject.SetActive(false);
            return label;
        }

        void OnTriggerEnter2D(Collider2D other) => TryKillRunner(other);

        void OnTriggerStay2D(Collider2D other) => TryKillRunner(other);

        void OnCollisionEnter2D(Collision2D collision) => TryKillRunner(collision.collider);

        void OnCollisionStay2D(Collision2D collision) => TryKillRunner(collision.collider);

        void TryKillRunner(Collider2D other)
        {
            if (!NetworkServer.active || !IsSpike || syncedNoHitbox)
                return;

            if (!other.TryGetComponent(out NetworkPlayer networkPlayer) ||
                networkPlayer.role != PlayerRole.Runner)
                return;

            if (!other.TryGetComponent(out RunnerMovementServer runner))
                return;

            runner.RespawnAtSpawn(other.transform.position);
        }

        public void ClientApplyPredictedTransform(Vector2 position, float rotationDegrees, Vector2 scale)
        {
            if (scriptPivot != null)
                return;

            Vector3 worldPosition = new Vector3(position.x, position.y, 0f) + (Vector3)scriptWorldOffset;
            float worldRotation = rotationDegrees + scriptRotationDegrees;
            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, worldRotation);
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
            ApplyHitboxOutline();
        }

        /// <summary>
        /// Client-only pose override for keyframe prediction between server sync ticks.
        /// </summary>
        public void ClientApplyPredictedScriptPose(
            Vector2 worldPosition,
            float worldRotation,
            Vector2 worldScale)
        {
            if (!NetworkClient.active || NetworkServer.active)
                return;

            Vector3 basePosition = GetBaseWorldPosition();
            scriptWorldOffset = new Vector2(worldPosition.x, worldPosition.y) - new Vector2(basePosition.x, basePosition.y);
            scriptRotationDegrees = Mathf.DeltaAngle(PlacementRotationDegrees, worldRotation);
            scriptScaleX = syncedScaleX > 0.0001f ? worldScale.x / syncedScaleX : 1f;
            scriptScaleY = syncedScaleY > 0.0001f ? worldScale.y / syncedScaleY : 1f;

            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, worldRotation);
            transform.localScale = new Vector3(
                syncedScaleX * scriptScaleX,
                syncedScaleY * scriptScaleY,
                1f);
            ApplyHitboxOutline();
        }

        public void ClientApplyPredictedColor(Color32 color) => ApplyColor(color);

        public void ClientApplyPredictedTexture(string textureId) => ApplyTexture(textureId);

        public void ClientApplyPredictedGroups(string groups) => ApplyGroups(groups);

        public void ClientApplyPredictedAppearance(
            float transparency,
            int editorLayer,
            bool noHitbox,
            int sortingOrder)
        {
            EnsureVisuals();

            bool createMode = IsLocalKaplaoCreateMode();
            bool isEditorOnlyTile = IsTrigger || IsParticle || IsKeyframe || IsExecutor;
            bool showEditorOnlyInCreate = isEditorOnlyTile && createMode;
            bool fullyTransparent = transparency <= 0f;
            logicalSpriteVisible = !fullyTransparent && (!isEditorOnlyTile || showEditorOnlyInCreate);

            if (spriteRenderer != null)
            {
                Color baseColor = tileColor;
                float alpha = transparency;
                if (createMode && alpha <= 0f)
                    alpha = CreateModeInvisibleAlpha;

                if (createMode &&
                    NetworkClient.active &&
                    NetworkClient.localPlayer != null &&
                    NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursor) &&
                    KaplaoLocalGameModePredictor.IsCreate(cursor))
                {
                    int activeLayer = cursor.ActiveEditorLayer;
                    if (activeLayer >= 0 && editorLayer != activeLayer)
                        alpha /= InactiveLayerAlphaDivisor;
                }

                baseColor.a = Mathf.Clamp01(alpha);
                spriteRenderer.color = baseColor;
            }

            ApplyRendererEnabledState();

            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = sortingOrder;
            if (selectionOutline != null)
                selectionOutline.sortingOrder = sortingOrder + 2;
            if (hitboxOutline != null)
                hitboxOutline.sortingOrder = sortingOrder + 3;

            if (tileColliders == null || tileColliders.Length == 0)
                tileColliders = GetComponents<Collider2D>();

            bool collidersEnabled = !noHitbox;
            bool forceTrigger = IsTrigger || IsParticle || IsSpike;
            for (int i = 0; i < tileColliders.Length; i++)
            {
                Collider2D collider = tileColliders[i];
                if (collider == null)
                    continue;

                if (forceTrigger)
                    collider.isTrigger = true;

                collider.enabled = collidersEnabled;
            }

            ApplyHitboxOutline();
        }

        public void ClientApplyPredictedHidden(bool hidden)
        {
            EnsureVisuals();
            if (spriteRenderer != null)
                spriteRenderer.enabled = !hidden;
        }
    }
}
