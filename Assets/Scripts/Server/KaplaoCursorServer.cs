using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KaplaoGames.Client;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Server-side Kaplao cursor: position, tool mode, runner grabbing, building, and synced visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public class KaplaoCursorServer : NetworkBehaviour
    {
        const float SingleSelectDragThreshold = 0.12f;

        [Header("Grab")]
        [SerializeField] LayerMask runnerMask = ~0;
        [SerializeField] float throwVelocityMultiplier = 0.22f;
        [SerializeField] float minThrowSpeed = 2.5f;
        [SerializeField] float maxThrowSpeed = 200f;
        [SerializeField] float throwVelocitySmoothing = 14f;

        [Header("Grab cursor")]
        [SerializeField] Sprite releasedSprite;
        [SerializeField] Sprite pressedSprite;
        [SerializeField] float grabCursorScale = 0.35f;

        [Header("Palm tool (Usual)")]
        [Tooltip("Palm prefab with PolygonCollider2D / BoxCollider2D and kinematic Rigidbody2D. Runners are pushed by the collider, not by click-grab.")]
        [SerializeField] GameObject palmCursorPrefab;
        [SerializeField] int palmCursorSortingOrder = 201;

        [Header("Gun tool (Usual)")]
        [Tooltip("Bullet prefab (KaplaoBulletServer + NetworkIdentity + NetworkTransformReliable). Also add to NetworkManager → Kaplao Bullet Prefab.")]
        [SerializeField] GameObject bulletPrefab;
        [Tooltip("Crosshair sprite — created automatically at the cursor. Nothing else to assign.")]
        [SerializeField] Sprite crosshairSprite;
        [SerializeField] float crosshairScale = 1f;
        [Tooltip("Played only for the Kaplao who fired. Runners do not hear it.")]
        [SerializeField] AudioClip gunShotClip;
        [SerializeField] float gunShotVolume = 1f;
        [SerializeField] float gunMuzzleOffset = 0.35f;
        [SerializeField] int crosshairSortingOrder = 202;

        [Header("Build cursor")]
        [Tooltip("Block tile prefab — sprite is taken from its SpriteRenderer.")]
        [SerializeField] GameObject blockTilePrefab;
        [Tooltip("Spike tile prefab — sprite is taken from its SpriteRenderer.")]
        [SerializeField] GameObject spikeTilePrefab;
        [Tooltip("Trigger tile prefab — sprite is taken from its SpriteRenderer.")]
        [SerializeField] GameObject triggerTilePrefab;
        [Tooltip("Particle tile prefab — sprite is taken from its SpriteRenderer.")]
        [SerializeField] GameObject particleTilePrefab;
        [Tooltip("Executor tile prefab — sprite is taken from its SpriteRenderer.")]
        [SerializeField] GameObject executorTilePrefab;
        [SerializeField] Sprite deleteCursorSprite;
        [SerializeField] float tileCursorScale = 1f;
        [SerializeField] Color deleteCursorFallbackColor = new Color(0.95f, 0.2f, 0.2f, 0.85f);

        [Header("Visuals")]
        [SerializeField] Transform grabCursorTransform;
        [SerializeField] SpriteRenderer grabCursorRenderer;
        [SerializeField] Transform tilePreviewTransform;
        [SerializeField] SpriteRenderer tilePreviewRenderer;
        [SerializeField] int grabCursorSortingOrder = 200;
        [SerializeField] int tilePreviewSortingOrder = 199;

        [SyncVar(hook = nameof(OnCursorPositionChanged))]
        Vector2 cursorWorldPosition;

        [SyncVar(hook = nameof(OnCursorPressedChanged))]
        bool cursorPressed;

        [SyncVar(hook = nameof(OnToolModeChanged))]
        KaplaoToolMode cursorToolMode = KaplaoToolMode.Grab;

        [SyncVar(hook = nameof(OnPlaceColorChanged))]
        Color32 syncedPlaceColor = new Color32(255, 255, 255, 255);

        [SyncVar(hook = nameof(OnSpikeQuarterTurnsChanged))]
        byte syncedSpikeQuarterTurns;

        [SyncVar(hook = nameof(OnHostGameModeChanged))]
        HostGameMode hostGameMode = HostGameMode.Usual;

        [SyncVar(hook = nameof(OnSelectionMarqueeChanged))]
        bool selectionMarqueeActive;

        [SyncVar(hook = nameof(OnSelectionMarqueeChanged))]
        Vector2 selectionMarqueeStart;

        [SyncVar(hook = nameof(OnSelectionMarqueeChanged))]
        Vector2 selectionMarqueeEnd;

        [SyncVar(hook = nameof(OnActiveCreateTransformChanged))]
        CreateTransformMode syncedActiveCreateTransform;

        [SyncVar(hook = nameof(OnActiveEditorLayerChanged))]
        int syncedActiveEditorLayer = -1;

        [SyncVar]
        float syncedRotationSnapDegrees = CreateModeSnapSettings.DefaultRotationSnapDegrees;

        [SyncVar]
        float syncedPositionSnapUnits = CreateModeSnapSettings.DefaultPositionSnapUnits;

        [SyncVar]
        float syncedScaleSnapUnits = CreateModeSnapSettings.DefaultScaleSnapUnits;

        [SyncVar(hook = nameof(OnHideCursorChanged))]
        bool syncedHideCursor;

        [SyncVar(hook = nameof(OnSelectionFilterChanged))]
        string syncedSelectionFilterJson = string.Empty;

        CreateModeSelectionFilter selectionFilter = CreateModeSelectionFilter.CreateDefault();

        readonly List<byte> customSoundImportBuilder = new();
        int customSoundImportExpectedLength = -1;
        int customSoundImportReceivedLength;
        string customSoundImportId = string.Empty;
        string customSoundImportExtension = ".ogg";

        public CreateModeSelectionFilter SelectionFilter => selectionFilter;
        public bool HasActiveSelectionFilter => selectionFilter != null && !selectionFilter.IsDefault;

        [Server]
        public void ServerSetSelectionFilter(CreateModeSelectionFilter filter)
        {
            selectionFilter = filter ?? CreateModeSelectionFilter.CreateDefault();
            syncedSelectionFilterJson = SelectionFilterPayload.Serialize(selectionFilter);
        }

        void OnSelectionFilterChanged(string oldValue, string newValue)
        {
            selectionFilter = SelectionFilterPayload.Deserialize(newValue);
        }

        public readonly SyncList<uint> SelectedTiles = new SyncList<uint>();

        public event Action SelectionChanged;
        public event Action<HostGameMode> HostGameModeChanged;
        public event Action ActiveEditorLayerChanged;

        public HostGameMode GameMode => hostGameMode;
        public bool ScriptRunning =>
            LevelSessionServer.Instance != null
                ? LevelSessionServer.Instance.ScriptRunning
                : false;
        public int SelectedCount => SelectedTiles.Count;
        public bool SelectionMarqueeActive => selectionMarqueeActive;
        public Vector2 SelectionMarqueeStart => selectionMarqueeStart;
        public Vector2 SelectionMarqueeEnd => selectionMarqueeEnd;
        public CreateTransformMode ActiveCreateTransform => syncedActiveCreateTransform;
        public int ActiveEditorLayer => syncedActiveEditorLayer;
        public float RotationSnapDegrees => syncedRotationSnapDegrees;
        public float PositionSnapUnits => syncedPositionSnapUnits;
        public float ScaleSnapUnits => syncedScaleSnapUnits;
        public bool HideCursor => syncedHideCursor;

        struct TileTransformBaseline
        {
            public Vector2 Position;
            public float Rotation;
            public Vector2 Scale;
        }

        Sprite blockCursorSprite;
        Sprite spikeCursorSprite;
        Sprite triggerCursorSprite;
        Sprite particleCursorSprite;
        Sprite executorCursorSprite;
        Color blockCursorColor = Color.white;
        Color spikeCursorColor = Color.white;
        Color triggerCursorColor = Color.white;
        Color particleCursorColor = Color.white;
        RunnerMovementServer grabbedRunner;
        Vector2 grabOffset;
        Vector2 previousCursorWorldPosition;
        Vector2 currentCursorVelocity;
        Vector2 smoothedCursorVelocity;
        Vector2 releaseThrowVelocity;
        bool hasPreviousCursorPosition;
        Vector2Int? lastPaintCell;
        KaplaoToolMode lastPaintToolMode = KaplaoToolMode.Grab;
        Color32 lastAppliedSelectionColor;
        bool selectionDragActive;
        Vector2 selectionDragStartWorld;
        bool selectionMarqueeUsedThisDrag;
        bool selectionShiftWasHeld;
        const float SelectionCyclePointEpsilonSqr = 0.0025f;
        Vector2 selectionCyclePoint;
        readonly List<uint> selectionCycleNetIds = new();
        int selectionCycleIndex;
        readonly List<LevelTileServer> tilesAtPointBuffer = new();
        TransformAxisConstraint transformAxisConstraint;
        Vector2 transformPivot;
        Vector2 transformStartCursorPos;
        float transformStartCursorAngle;
        float transformStartCursorDistance;
        bool transformCursorInitialized;
        readonly Dictionary<uint, TileTransformBaseline> transformBaselines = new();
        readonly Stack<LevelUndoEntry> undoHistory = new();
        bool applyingUndoSnapshot;
        bool suppressingSelectionUndo;
        const int MaxUndoHistory = 64;
        readonly List<ClipboardTileRecord> clipboardTiles = new();
        readonly List<int[]> clipboardLinkGroups = new();
        bool clipboardHasData;
        const int MaxImportChunkChars = 32000;
        readonly StringBuilder importJsonBuilder = new();
        int importJsonExpectedLength = -1;
        int importJsonReceivedLength;
        const int MaxPngImportChunkBytes = 24000;
        readonly List<byte> pngImportBuilder = new();
        int pngImportExpectedLength = -1;
        int pngImportReceivedLength;
        int pngImportTilesX;
        int pngImportTilesY;
        float pngImportCenterX;
        float pngImportCenterY;
        readonly List<byte> customTextureImportBuilder = new();
        int customTextureImportExpectedLength = -1;
        int customTextureImportReceivedLength;
        string customTextureImportId = string.Empty;
        const float GunShotCooldown = 0.15f;
        KaplaoCursorClient cursorClient;
        Transform palmCursorInstance;
        SpriteRenderer[] palmRenderers;
        Rigidbody2D palmBody;
        Transform crosshairTransform;
        SpriteRenderer crosshairRenderer;
        AudioSource shootAudioSource;
        float nextGunShotTime;

        void Awake()
        {
            cursorClient = GetComponent<KaplaoCursorClient>();
            ResolveCursorSprites();
            EnsureCursorVisual();
            EnsureShootAudio();
            SelectedTiles.Callback += OnSelectedTilesChanged;
            enabled = false;
        }

        public override void OnStartServer()
        {
            enabled = true;
        }

        public override void OnStartClient()
        {
            enabled = true;
            ResolveCursorSprites();
            EnsureCursorVisual();
            ApplyVisualState();
            SelectionChanged?.Invoke();
        }

        void LateUpdate()
        {
            if (isOwned)
                ApplyVisualState();
        }

        void FixedUpdate()
        {
            if (!isClient)
                return;

            ApplyClientPalmPhysics();
        }

        void OnDestroy()
        {
            SelectedTiles.Callback -= OnSelectedTilesChanged;

            if (grabCursorTransform != null)
                Destroy(grabCursorTransform.gameObject);

            if (tilePreviewTransform != null)
                Destroy(tilePreviewTransform.gameObject);

            if (palmCursorInstance != null)
                Destroy(palmCursorInstance.gameObject);

            if (crosshairTransform != null)
                Destroy(crosshairTransform.gameObject);
        }

        public bool IsTileSelected(uint netId)
        {
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (SelectedTiles[i] == netId)
                    return true;
            }

            return false;
        }

        [Server]
        public void SetHostGameMode(HostGameMode mode)
        {
            if (hostGameMode == mode)
                return;

            hostGameMode = mode;
            suppressingSelectionUndo = true;
            try
            {
                ClearSelection();
            }
            finally
            {
                suppressingSelectionUndo = false;
            }

            ReleaseGrab(false);
            selectionDragActive = false;
            selectionMarqueeActive = false;
            selectionMarqueeUsedThisDrag = false;
            selectionShiftWasHeld = false;
            CancelCreateTransform();
        }

        [Server]
        public void RunLevelScript(string source)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
            {
                NotifyScriptResult(false, "Level session is not available.");
                return;
            }

            // Always run entry file; ignore editor buffer unless project is empty.
            string entry = WorldScriptProjectService.GetEntryScriptContent();
            if (string.IsNullOrWhiteSpace(entry) && !string.IsNullOrWhiteSpace(source))
                entry = source;

            bool ok = session.ServerTryStartScript(entry, out string error);
            NotifyScriptResult(ok, ok ? "Script running." : error);
        }

        [Server]
        public void StopLevelScript()
        {
            LevelSessionServer.Instance?.ServerStopScript();
            NotifyScriptResult(true, "Script stopped.");
        }

        [Server]
        public void HandleScriptEvent(string eventName, string jsonArgs)
        {
            var playerRef = new LevelScript.ScriptGameObjectRef(LevelScript.ScriptObjectKind.Player, netId);
            LevelScript.LevelScriptRuntime.Instance?.DispatchClientEvent(eventName, playerRef, jsonArgs);
        }

        [Server]
        public void ServerResetPlaceColorToWhite()
        {
            syncedPlaceColor = new Color32(255, 255, 255, 255);
        }

        public static void ServerResetAllKaplaoPlaceColors()
        {
            if (!NetworkServer.active)
                return;

            KaplaoCursorServer[] cursors = FindObjectsByType<KaplaoCursorServer>(FindObjectsSortMode.None);
            for (int i = 0; i < cursors.Length; i++)
            {
                if (cursors[i] != null)
                    cursors[i].ServerResetPlaceColorToWhite();
            }
        }

        [Server]
        public void ImportLevel(string json)
        {
            ResetImportTransfer();

            if (!LevelFileSerializer.TryParse(json, out LevelData data, out string error))
            {
                NotifyLevelImportResult(false, error);
                return;
            }

            CaptureUndoSnapshot();
            LevelScript.LevelScriptRuntime.Instance?.StopScript();
            LevelSessionServer.Instance?.ServerStopScript();
            LevelGridServer.Instance?.ImportLevelData(data);
            ClearSelection();
            ServerResetPlaceColorToWhite();
            bool hasScript = !string.IsNullOrWhiteSpace(data.script);
            string message = hasScript
                ? $"Imported {data.tiles.Length} tiles and level script."
                : $"Imported {data.tiles.Length} tiles.";
            NotifyLevelImportResult(true, message);
        }

        [Server]
        public void BeginImportLevel(int totalLength)
        {
            ResetImportTransfer();
            if (totalLength < 0)
            {
                NotifyLevelImportResult(false, "Invalid import size.");
                return;
            }

            importJsonExpectedLength = totalLength;
            if (importJsonBuilder.Capacity < totalLength)
                importJsonBuilder.Capacity = totalLength;
        }

        [Server]
        public void AppendImportLevelChunk(string chunk)
        {
            if (importJsonExpectedLength < 0)
                return;

            if (string.IsNullOrEmpty(chunk))
                return;

            if (chunk.Length > MaxImportChunkChars)
            {
                ResetImportTransfer();
                NotifyLevelImportResult(false, "Import chunk is too large.");
                return;
            }

            importJsonBuilder.Append(chunk);
            importJsonReceivedLength += chunk.Length;

            if (importJsonReceivedLength > importJsonExpectedLength)
            {
                ResetImportTransfer();
                NotifyLevelImportResult(false, "Import transfer exceeded expected size.");
            }
        }

        [Server]
        public void FinishImportLevel()
        {
            if (importJsonExpectedLength < 0)
                return;

            if (importJsonReceivedLength != importJsonExpectedLength)
            {
                ResetImportTransfer();
                NotifyLevelImportResult(false, "Import transfer incomplete.");
                return;
            }

            string json = importJsonBuilder.ToString();
            ResetImportTransfer();
            ImportLevel(json);
        }

        [Server]
        void ResetImportTransfer()
        {
            importJsonExpectedLength = -1;
            importJsonReceivedLength = 0;
            importJsonBuilder.Clear();
        }

        [Server]
        public void BeginPngImport(int totalLength, int tilesX, int tilesY, float centerX, float centerY)
        {
            ResetPngImportTransfer();
            if (totalLength <= 0)
            {
                NotifyPngImportResult(false, "Invalid PNG size.");
                return;
            }

            if (tilesX <= 0 || tilesY <= 0)
            {
                NotifyPngImportResult(false, "Tile count must be greater than zero.");
                return;
            }

            pngImportExpectedLength = totalLength;
            pngImportTilesX = tilesX;
            pngImportTilesY = tilesY;
            pngImportCenterX = centerX;
            pngImportCenterY = centerY;
            if (pngImportBuilder.Capacity < totalLength)
                pngImportBuilder.Capacity = totalLength;
        }

        [Server]
        public void AppendPngImportChunk(byte[] chunk)
        {
            if (pngImportExpectedLength < 0 || chunk == null || chunk.Length == 0)
                return;

            if (chunk.Length > MaxPngImportChunkBytes)
            {
                ResetPngImportTransfer();
                NotifyPngImportResult(false, "PNG chunk is too large.");
                return;
            }

            pngImportBuilder.AddRange(chunk);
            pngImportReceivedLength += chunk.Length;

            if (pngImportReceivedLength > pngImportExpectedLength)
            {
                ResetPngImportTransfer();
                NotifyPngImportResult(false, "PNG transfer exceeded expected size.");
            }
        }

        [Server]
        public void FinishPngImport()
        {
            if (pngImportExpectedLength < 0)
                return;

            if (pngImportReceivedLength != pngImportExpectedLength)
            {
                ResetPngImportTransfer();
                NotifyPngImportResult(false, "PNG transfer incomplete.");
                return;
            }

            byte[] pngBytes = pngImportBuilder.ToArray();
            int tilesX = pngImportTilesX;
            int tilesY = pngImportTilesY;
            var center = new Vector2(pngImportCenterX, pngImportCenterY);
            ResetPngImportTransfer();
            ImportPng(pngBytes, tilesX, tilesY, center);
        }

        [Server]
        void ResetPngImportTransfer()
        {
            pngImportExpectedLength = -1;
            pngImportReceivedLength = 0;
            pngImportTilesX = 0;
            pngImportTilesY = 0;
            pngImportCenterX = 0f;
            pngImportCenterY = 0f;
            pngImportBuilder.Clear();
        }

        [Server]
        public void ImportPng(byte[] pngBytes, int tilesX, int tilesY, Vector2 worldCenter)
        {
            if (hostGameMode != HostGameMode.Create)
            {
                NotifyPngImportResult(false, "PNG import is only available in Create mode.");
                return;
            }

            if (!PngImportUtility.TryLoadTexture(pngBytes, out Texture2D texture, out string loadError))
            {
                NotifyPngImportResult(false, loadError);
                return;
            }

            var slices = new List<PngImportColorSlice>();
            try
            {
                if (!PngImportUtility.TryBuildColorSlices(texture, tilesX, tilesY, worldCenter, slices, out string sliceError))
                {
                    NotifyPngImportResult(false, sliceError);
                    return;
                }
            }
            finally
            {
                Destroy(texture);
            }

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
            {
                NotifyPngImportResult(false, "Level grid is not available.");
                return;
            }

            CaptureUndoSnapshot();
            AbortCreateTransformIfActive();

            int editorLayer = GetPlacementEditorLayer();
            var placed = new List<uint>(slices.Count);

            for (int i = 0; i < slices.Count; i++)
            {
                PngImportColorSlice slice = slices[i];
                uint newId = grid.TryPlaceTile(
                    TileType.Block,
                    slice.Position,
                    0f,
                    slice.Color,
                    editorLayer: editorLayer,
                    textureId: string.Empty);

                if (newId == 0 || !grid.TryGetTileByNetId(newId, out LevelTileServer placedTile))
                    continue;

                placedTile.ServerSetTransform(slice.Position, 0f, slice.Scale, roundValues: false);
                placed.Add(newId);
            }

            if (placed.Count == 0)
            {
                if (undoHistory.Count > 0)
                    undoHistory.Pop();
                NotifyPngImportResult(false, "No tiles were placed.");
                return;
            }

            suppressingSelectionUndo = true;
            try
            {
                SetSelection(placed);
            }
            finally
            {
                suppressingSelectionUndo = false;
            }

            NotifyPngImportResult(true, $"Imported {placed.Count} color tiles from PNG.");
        }

        [Server]
        public void BeginCustomTextureImport(string textureId, int totalLength)
        {
            ResetCustomTextureImportTransfer();
            if (!WorldCustomTextureService.TrySanitizeTextureId(textureId, out string sanitizedId, out string sanitizeError))
            {
                NotifyCustomTextureImportResult(false, sanitizeError);
                return;
            }

            if (totalLength <= 0)
            {
                NotifyCustomTextureImportResult(false, "Invalid PNG size.");
                return;
            }

            customTextureImportId = sanitizedId;
            customTextureImportExpectedLength = totalLength;
            if (customTextureImportBuilder.Capacity < totalLength)
                customTextureImportBuilder.Capacity = totalLength;
        }

        [Server]
        public void AppendCustomTextureImportChunk(byte[] chunk)
        {
            if (customTextureImportExpectedLength < 0 || chunk == null || chunk.Length == 0)
                return;

            if (chunk.Length > MaxPngImportChunkBytes)
            {
                ResetCustomTextureImportTransfer();
                NotifyCustomTextureImportResult(false, "Texture chunk is too large.");
                return;
            }

            customTextureImportBuilder.AddRange(chunk);
            customTextureImportReceivedLength += chunk.Length;

            if (customTextureImportReceivedLength > customTextureImportExpectedLength)
            {
                ResetCustomTextureImportTransfer();
                NotifyCustomTextureImportResult(false, "Texture transfer exceeded expected size.");
            }
        }

        [Server]
        public void FinishCustomTextureImport()
        {
            if (customTextureImportExpectedLength < 0)
                return;

            if (customTextureImportReceivedLength != customTextureImportExpectedLength)
            {
                ResetCustomTextureImportTransfer();
                NotifyCustomTextureImportResult(false, "Texture transfer incomplete.");
                return;
            }

            string textureId = customTextureImportId;
            byte[] pngBytes = customTextureImportBuilder.ToArray();
            ResetCustomTextureImportTransfer();
            ImportCustomTexture(textureId, pngBytes);
        }

        [Server]
        void ResetCustomTextureImportTransfer()
        {
            customTextureImportExpectedLength = -1;
            customTextureImportReceivedLength = 0;
            customTextureImportId = string.Empty;
            customTextureImportBuilder.Clear();
        }

        [Server]
        public void ImportCustomTexture(string textureId, byte[] pngBytes)
        {
            if (hostGameMode != HostGameMode.Create)
            {
                NotifyCustomTextureImportResult(false, "Texture import is only available in Create mode.");
                return;
            }

            if (!WorldCustomTextureService.TrySanitizeTextureId(textureId, out string sanitizedId, out string sanitizeError))
            {
                NotifyCustomTextureImportResult(false, sanitizeError);
                return;
            }

            if (!WorldCustomTextureService.TryRegisterFromPng(sanitizedId, pngBytes, out string registerError))
            {
                NotifyCustomTextureImportResult(false, registerError);
                return;
            }

            if (!WorldCustomTextureService.TrySaveToActiveWorld(sanitizedId, pngBytes, out string saveError))
            {
                NotifyCustomTextureImportResult(false, saveError);
                return;
            }

            LevelSessionServer.Instance?.ServerBroadcastCustomTexture(sanitizedId, pngBytes);
            NotifyCustomTextureImportResult(true, $"Imported texture '{sanitizedId}'.");
        }

        [Server]
        public void RenameCustomTexture(string oldTextureId, string newTextureId)
        {
            if (hostGameMode != HostGameMode.Create)
            {
                NotifyCustomTextureRenameResult(false, "Rename is only available in Create mode.");
                return;
            }

            if (!WorldCustomTextureService.TryRenameTexture(oldTextureId, newTextureId, out string sanitizedNewId, out string error))
            {
                NotifyCustomTextureRenameResult(false, error);
                return;
            }

            int updatedTiles = LevelGridServer.Instance?.TryReplaceTextureIdEverywhere(oldTextureId, sanitizedNewId) ?? 0;
            LevelSessionServer.Instance?.ServerBroadcastCustomTextureRename(oldTextureId, sanitizedNewId);
            NotifyCustomTextureRenameResult(true, $"Renamed to '{sanitizedNewId}' ({updatedTiles} tiles updated).");
        }

        [TargetRpc]
        void TargetPngImportResult(NetworkConnectionToClient conn, bool success, string message)
        {
            KaplaoPngImportUi ui = FindFirstObjectByType<KaplaoPngImportUi>();
            if (ui != null)
                ui.SetStatus(message);
        }

        [Server]
        void NotifyPngImportResult(bool success, string message)
        {
            if (connectionToClient != null)
                TargetPngImportResult(connectionToClient, success, message);
        }

        [TargetRpc]
        void TargetCustomTextureImportResult(NetworkConnectionToClient conn, bool success, string message)
        {
            KaplaoTextureImportUi ui = FindFirstObjectByType<KaplaoTextureImportUi>();
            if (ui != null)
                ui.SetStatus(message);
        }

        [Server]
        void NotifyCustomTextureImportResult(bool success, string message)
        {
            if (connectionToClient != null)
                TargetCustomTextureImportResult(connectionToClient, success, message);
        }

        [Server]
        public void BeginCustomSoundImport(string soundId, string extension, int totalLength)
        {
            ResetCustomSoundImportTransfer();
            if (!WorldCustomSoundService.TrySanitizeSoundId(soundId, out string sanitizedId, out string sanitizeError))
            {
                NotifyCustomSoundImportResult(false, sanitizeError);
                return;
            }

            if (totalLength <= 0)
            {
                NotifyCustomSoundImportResult(false, "Invalid audio size.");
                return;
            }

            customSoundImportId = sanitizedId;
            customSoundImportExtension = string.IsNullOrWhiteSpace(extension) ? ".ogg" : extension;
            customSoundImportExpectedLength = totalLength;
            if (customSoundImportBuilder.Capacity < totalLength)
                customSoundImportBuilder.Capacity = totalLength;
        }

        [Server]
        public void AppendCustomSoundImportChunk(byte[] chunk)
        {
            if (customSoundImportExpectedLength < 0 || chunk == null || chunk.Length == 0)
                return;

            customSoundImportBuilder.AddRange(chunk);
            customSoundImportReceivedLength += chunk.Length;

            if (customSoundImportReceivedLength > customSoundImportExpectedLength)
            {
                ResetCustomSoundImportTransfer();
                NotifyCustomSoundImportResult(false, "Sound transfer exceeded expected size.");
            }
        }

        [Server]
        public void FinishCustomSoundImport()
        {
            if (customSoundImportExpectedLength < 0)
                return;

            if (customSoundImportReceivedLength != customSoundImportExpectedLength)
            {
                ResetCustomSoundImportTransfer();
                NotifyCustomSoundImportResult(false, "Sound transfer incomplete.");
                return;
            }

            string soundId = customSoundImportId;
            string extension = customSoundImportExtension;
            byte[] audioBytes = customSoundImportBuilder.ToArray();
            ResetCustomSoundImportTransfer();
            ImportCustomSound(soundId, extension, audioBytes);
        }

        [Server]
        void ResetCustomSoundImportTransfer()
        {
            customSoundImportExpectedLength = -1;
            customSoundImportReceivedLength = 0;
            customSoundImportId = string.Empty;
            customSoundImportExtension = ".ogg";
            customSoundImportBuilder.Clear();
        }

        [Server]
        public void ImportCustomSound(string soundId, string extension, byte[] audioBytes)
        {
            if (hostGameMode != HostGameMode.Create)
            {
                NotifyCustomSoundImportResult(false, "Sound import is only available in Create mode.");
                return;
            }

            if (!WorldCustomSoundService.TrySanitizeSoundId(soundId, out string sanitizedId, out string sanitizeError))
            {
                NotifyCustomSoundImportResult(false, sanitizeError);
                return;
            }

            if (!WorldCustomSoundService.TrySaveToActiveWorld(sanitizedId, audioBytes, extension, out string saveError))
            {
                NotifyCustomSoundImportResult(false, saveError);
                return;
            }

            NotifyCustomSoundImportResult(true, $"Imported sound '{sanitizedId}'. Use playSound(\"custom/{sanitizedId}\", ...).");
        }

        [TargetRpc]
        void TargetCustomSoundImportResult(NetworkConnectionToClient conn, bool success, string message)
        {
            KaplaoSoundImportUi ui = FindFirstObjectByType<KaplaoSoundImportUi>();
            if (ui != null)
                ui.SetStatus(message);
        }

        [Server]
        void NotifyCustomSoundImportResult(bool success, string message)
        {
            if (connectionToClient != null)
                TargetCustomSoundImportResult(connectionToClient, success, message);
        }

        [TargetRpc]
        void TargetCustomTextureRenameResult(NetworkConnectionToClient conn, bool success, string message)
        {
            KaplaoTileTextureUi ui = FindFirstObjectByType<KaplaoTileTextureUi>();
            if (ui != null)
                ui.SetRenameStatus(message, success);
        }

        [Server]
        void NotifyCustomTextureRenameResult(bool success, string message)
        {
            if (connectionToClient != null)
                TargetCustomTextureRenameResult(connectionToClient, success, message);
        }

        [Server]
        public void UndoLastLevelEdit()
        {
            if (undoHistory.Count == 0)
                return;

            LevelUndoEntry entry = undoHistory.Pop();
            if (entry.Kind == LevelUndoKind.Selection)
            {
                suppressingSelectionUndo = true;
                try
                {
                    SetSelection(entry.SelectionNetIds ?? System.Array.Empty<uint>());
                }
                finally
                {
                    suppressingSelectionUndo = false;
                }

                if (connectionToClient != null)
                TargetRefreshSelectionVisuals(connectionToClient);
                return;
            }

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null || entry.LevelSnapshot == null)
                return;

            bool wasScriptRunning = LevelSessionServer.Instance != null &&
                                    LevelSessionServer.Instance.ScriptRunning;

            applyingUndoSnapshot = true;
            suppressingSelectionUndo = true;
            try
            {
                LevelScriptRuntime.Instance?.AbortRunningScriptWithoutRestore();
                grid.ImportLevelData(entry.LevelSnapshot);
                ClearSelection();

                if (wasScriptRunning && LevelSessionServer.Instance != null)
                {
                    string script = entry.LevelSnapshot.script ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(script))
                        LevelSessionServer.Instance.ServerTryStartScript(script, out _);
                }
            }
            finally
            {
                applyingUndoSnapshot = false;
                suppressingSelectionUndo = false;
            }

            if (connectionToClient != null)
                TargetRefreshSelectionVisuals(connectionToClient);
        }

        [TargetRpc]
        void TargetRefreshSelectionVisuals(NetworkConnectionToClient conn)
        {
            SelectionChanged?.Invoke();
        }

        [Server]
        public void SetLevelScript(string script)
        {
            string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : null;
            if (WorldScriptProjectService.HasProject || WorldSession.HasActiveWorld)
            {
                WorldScriptProjectService.TryWriteFile(
                    worldName,
                    WorldScriptProjectService.EntryFileName,
                    script ?? string.Empty,
                    out _);
            }

            LevelSessionServer.Instance?.ServerSetLevelScript(script);
            LevelSessionServer.Instance?.ServerBroadcastScriptProject();
        }

        [Server]
        public void WriteScriptFile(string relativePath, string content)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            if (!session.ServerTryWriteScriptFile(relativePath, content, out string error) &&
                !string.IsNullOrEmpty(error))
            {
                NotifyScriptResult(false, error);
            }
        }

        [Server]
        public void CreateScriptFile(string relativePath, string content)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            if (!session.ServerTryCreateScriptFile(relativePath, content, out string error) &&
                !string.IsNullOrEmpty(error))
            {
                NotifyScriptResult(false, error);
            }
        }

        [Server]
        public void CreateScriptFolder(string relativePath)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            if (!session.ServerTryCreateScriptFolder(relativePath, out string error) &&
                !string.IsNullOrEmpty(error))
            {
                NotifyScriptResult(false, error);
            }
        }

        [Server]
        public void DeleteScriptNode(string relativePath)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            if (!session.ServerTryDeleteScriptNode(relativePath, out string error) &&
                !string.IsNullOrEmpty(error))
            {
                NotifyScriptResult(false, error);
            }
        }

        [Server]
        public void RenameScriptNode(string relativePath, string newName)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            if (!session.ServerTryRenameScriptNode(relativePath, newName, out string error) &&
                !string.IsNullOrEmpty(error))
            {
                NotifyScriptResult(false, error);
            }
        }

        [Server]
        public void MoveScriptNode(string fromRelativePath, string toRelativePath)
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null)
                return;

            if (!session.ServerTryMoveScriptNode(fromRelativePath, toRelativePath, out string error) &&
                !string.IsNullOrEmpty(error))
            {
                NotifyScriptResult(false, error);
            }
        }

        [Server]
        public void PromoteRunner(uint runnerNetId)
        {
            KaplaoNetworkManager.Instance?.TryPromoteRunnerToKaplao(runnerNetId, actorNetId: netId);
        }

        [Server]
        public void SendChatMessage(string message)
        {
            LevelSessionServer.Instance?.ServerHandleChatMessage(connectionToClient, message);
        }

        [Server]
        public void ApplyChatModerationDecision(ulong requestId, LevelSessionServer.ModerationDecision decision)
        {
            LevelSessionServer.Instance?.ServerApplyModerationDecision(requestId, decision, connectionToClient);
        }

        [TargetRpc]
        void TargetScriptResult(NetworkConnectionToClient conn, bool success, string message)
        {
            KaplaoCodeModeUi ui = FindFirstObjectByType<KaplaoCodeModeUi>();
            if (ui != null)
                ui.SetStatus(message);
        }

        [TargetRpc]
        void TargetLevelImportResult(NetworkConnectionToClient conn, bool success, string message)
        {
            KaplaoLevelIoUi ui = FindFirstObjectByType<KaplaoLevelIoUi>();
            if (ui != null)
                ui.SetStatus(message);
        }

        [TargetRpc]
        void TargetShowPromotePanel(NetworkConnectionToClient conn, uint runnerNetId, string runnerName)
        {
            KaplaoPromoteRunnerUi ui = FindFirstObjectByType<KaplaoPromoteRunnerUi>();
            if (ui != null)
                ui.Show(runnerNetId, runnerName);
        }

        [TargetRpc]
        void TargetShowCantChangeMode(NetworkConnectionToClient conn)
        {
            CantChangeModeUi.Show();
        }

        public Vector3 GetToolPreviewWorldPosition()
        {
            if (tilePreviewTransform != null && tilePreviewRenderer != null && tilePreviewRenderer.enabled)
                return tilePreviewTransform.position;

            return GetVisualWorldPosition();
        }

        public float GetToolPreviewLabelWorldOffset()
        {
            if (tilePreviewRenderer != null && tilePreviewRenderer.enabled)
                return tilePreviewRenderer.bounds.extents.y + 0.28f;

            return 0.7f;
        }

        Vector2 GetVisualWorldPosition()
        {
            if (isOwned && cursorClient != null)
                return cursorClient.LocalWorldPosition;

            return cursorWorldPosition;
        }

        [Server]
        void NotifyScriptResult(bool success, string message)
        {
            if (connectionToClient != null)
                TargetScriptResult(connectionToClient, success, message);
        }

        [Server]
        void NotifyLevelImportResult(bool success, string message)
        {
            if (connectionToClient != null)
                TargetLevelImportResult(connectionToClient, success, message);
        }

        [Server]
        void CaptureUndoSnapshot()
        {
            if (applyingUndoSnapshot)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            PushUndoEntry(new LevelUndoEntry
            {
                Kind = LevelUndoKind.LevelSnapshot,
                LevelSnapshot = CloneLevelData(grid.ExportLevelData())
            });
        }

        [Server]
        void CaptureSelectionUndo()
        {
            if (applyingUndoSnapshot || suppressingSelectionUndo)
                return;

            var ids = new uint[SelectedTiles.Count];
            for (int i = 0; i < SelectedTiles.Count; i++)
                ids[i] = SelectedTiles[i];

            PushUndoEntry(new LevelUndoEntry
            {
                Kind = LevelUndoKind.Selection,
                SelectionNetIds = ids
            });
        }

        [Server]
        void PushUndoEntry(LevelUndoEntry entry)
        {
            undoHistory.Push(entry);

            while (undoHistory.Count > MaxUndoHistory)
            {
                var trimmed = new Stack<LevelUndoEntry>(MaxUndoHistory);
                int keep = MaxUndoHistory;
                foreach (LevelUndoEntry existing in undoHistory)
                {
                    if (keep-- <= 0)
                        break;
                    trimmed.Push(existing);
                }
                undoHistory.Clear();
                foreach (LevelUndoEntry existing in trimmed)
                    undoHistory.Push(existing);
            }
        }

        static LevelData CloneLevelData(LevelData source)
        {
            LevelTileRecord[] sourceTiles = source.tiles ?? Array.Empty<LevelTileRecord>();
            var copiedTiles = new LevelTileRecord[sourceTiles.Length];
            for (int i = 0; i < sourceTiles.Length; i++)
                copiedTiles[i] = sourceTiles[i];

            TileLinkGroupRecord[] sourceLinks = source.linkGroups ?? Array.Empty<TileLinkGroupRecord>();
            var copiedLinks = new TileLinkGroupRecord[sourceLinks.Length];
            for (int i = 0; i < sourceLinks.Length; i++)
            {
                TilePositionRecord[] positions = sourceLinks[i].tiles ?? Array.Empty<TilePositionRecord>();
                var copiedPositions = new TilePositionRecord[positions.Length];
                for (int p = 0; p < positions.Length; p++)
                    copiedPositions[p] = positions[p];

                copiedLinks[i] = new TileLinkGroupRecord { tiles = copiedPositions };
            }

            return new LevelData
            {
                version = source.version,
                script = source.script,
                tiles = copiedTiles,
                linkGroups = copiedLinks,
                worldSpawnX = source.worldSpawnX,
                worldSpawnY = source.worldSpawnY,
                bannedIds = source.bannedIds ?? Array.Empty<string>(),
                bannedIps = source.bannedIps ?? Array.Empty<string>(),
                moderatorIds = Array.Empty<string>()
            };
        }

        [Server]
        public void ApplyInput(KaplaoCursorInput input)
        {
            if (hostGameMode == HostGameMode.Code)
            {
                cursorWorldPosition = input.worldPosition;
                return;
            }
            UpdateCursorVelocity(input.worldPosition);
            cursorWorldPosition = input.worldPosition;
            cursorPressed = input.primaryHeld;
            cursorToolMode = input.toolMode;
            syncedSpikeQuarterTurns = input.spikeQuarterTurns;

            if (hostGameMode == HostGameMode.Create)
            {
                ApplyCreateModeInput(input);
                return;
            }

            ApplyUsualModeInput(input);
        }

        [Server]
        void ApplyUsualModeInput(KaplaoCursorInput input)
        {
            switch (input.toolMode)
            {
                case KaplaoToolMode.Gun:
                    ReleaseGrab(false);
                    ApplyGunInput(input);
                    break;

                case KaplaoToolMode.Palm:
                    ReleaseGrab(false);
                    break;

                case KaplaoToolMode.Grab:
                default:
                    ApplyGrabInput(input);
                    break;
            }
        }

        [Server]
        void ApplyGrabInput(KaplaoCursorInput input)
        {
            if (input.primaryPressed)
                TryBeginGrab(input.worldPosition);

            if (input.primaryHeld)
                DragGrabbedRunner(input.worldPosition);
            else
                ReleaseGrab(true);
        }

        [Server]
        void ApplyGunInput(KaplaoCursorInput input)
        {
            if (!input.primaryPressed)
                return;

            if (Time.time < nextGunShotTime)
                return;

            if (bulletPrefab == null)
            {
                Debug.LogWarning("[KaplaoCursorServer] Bullet Prefab is not assigned on Kaplao.");
                return;
            }

            Vector2 aimPoint = input.worldPosition;
            Vector2 origin = transform.position;
            Vector2 direction = aimPoint - origin;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;

            direction.Normalize();
            Vector2 spawnPosition = origin + direction * gunMuzzleOffset;
            GameObject bulletObject = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

            if (!bulletObject.TryGetComponent(out NetworkIdentity _))
            {
                Debug.LogWarning("[KaplaoCursorServer] Bullet prefab needs NetworkIdentity.");
                Destroy(bulletObject);
                return;
            }

            if (bulletObject.TryGetComponent(out KaplaoBulletServer bullet))
                bullet.ServerInit(direction, this);

            NetworkServer.Spawn(bulletObject);
            nextGunShotTime = Time.time + GunShotCooldown;

            if (connectionToClient != null)
                TargetPlayGunShot(connectionToClient);
        }

        [TargetRpc]
        void TargetPlayGunShot(NetworkConnectionToClient conn)
        {
            if (gunShotClip == null)
                return;

            EnsureShootAudio();
            if (shootAudioSource != null)
                shootAudioSource.PlayOneShot(gunShotClip, gunShotVolume);
        }

        [Server]
        void ApplyCreateModeInput(KaplaoCursorInput input)
        {
            ReleaseGrab(false);

            KaplaoToolMode createTool = input.toolMode == KaplaoToolMode.Grab
                ? KaplaoToolMode.Select
                : input.toolMode;

            if (createTool == KaplaoToolMode.Block ||
                createTool == KaplaoToolMode.Spike ||
                createTool == KaplaoToolMode.Trigger ||
                createTool == KaplaoToolMode.Particle ||
                createTool == KaplaoToolMode.Keyframe ||
                createTool == KaplaoToolMode.Executor)
                syncedPlaceColor = input.placeColor;

            if (createTool == KaplaoToolMode.Select)
            {
                if (syncedActiveCreateTransform != CreateTransformMode.None)
                {
                    HandleActiveTransformInput(input);
                    return;
                }

                if (input.transformRotatePressed)
                    BeginCreateTransform(CreateTransformMode.Rotate);
                else if (input.transformMovePressed)
                    BeginCreateTransform(CreateTransformMode.Move);
                else if (input.transformScalePressed)
                    BeginCreateTransform(CreateTransformMode.Scale);

                if (syncedActiveCreateTransform != CreateTransformMode.None)
                {
                    HandleActiveTransformInput(input);
                    return;
                }

                HandleSelectionInput(input);
                return;
            }

            if (createTool == KaplaoToolMode.Delete)
            {
                HandleDeleteInput(input);
                return;
            }

            // Shift = paint while dragging (ignore pan/click rule).
            if (input.shiftHeld)
            {
                if (!input.primaryHeld)
                {
                    lastPaintCell = null;
                    return;
                }

                Vector2 paintPoint = input.worldPosition;
                if (lastPaintToolMode != createTool)
                {
                    lastPaintCell = null;
                    lastPaintToolMode = createTool;
                }

                Vector2Int dragCell = GridMath.WorldToCell(paintPoint);
                if (lastPaintCell.HasValue && lastPaintCell.Value == dragCell)
                    return;

                lastPaintCell = dragCell;
                PlaceCreateToolTile(createTool, dragCell, input);
                return;
            }

            // Without Shift: place only on click-release (not after camera pan).
            if (!input.primaryReleased)
            {
                if (!input.primaryHeld)
                    lastPaintCell = null;
                return;
            }

            lastPaintCell = null;
            lastPaintToolMode = createTool;
            PlaceCreateToolTile(createTool, GridMath.WorldToCell(input.worldPosition), input);
        }

        [Server]
        void PlaceCreateToolTile(KaplaoToolMode createTool, Vector2Int paintCell, KaplaoCursorInput input)
        {
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            switch (createTool)
            {
                case KaplaoToolMode.Block:
                    TryPlaceTileOnGrid(grid, TileType.Block, paintCell, 0f, input.placeColor);
                    break;
                case KaplaoToolMode.Spike:
                    TryPlaceTileOnGrid(grid, TileType.Spike, paintCell, -input.spikeQuarterTurns * 90f, input.placeColor, input.spikeQuarterTurns);
                    break;
                case KaplaoToolMode.Trigger:
                    TryPlaceTileOnGrid(grid, TileType.Trigger, paintCell, 0f, input.placeColor);
                    break;
                case KaplaoToolMode.Particle:
                    TryPlaceTileOnGrid(grid, TileType.Particle, paintCell, 0f, input.placeColor);
                    break;
                case KaplaoToolMode.Keyframe:
                    TryPlaceTileOnGrid(grid, TileType.Keyframe, paintCell, 0f, input.placeColor);
                    break;
                case KaplaoToolMode.Executor:
                    TryPlaceTileOnGrid(grid, TileType.Executor, paintCell, 0f, input.placeColor);
                    break;
            }
        }

        [Server]
        void HandleDeleteInput(KaplaoCursorInput input)
        {
            if (input.shiftHeld && input.primaryHeld)
            {
                Vector2 point = input.worldPosition;
                Vector2Int paintCell = GridMath.WorldToCell(point);

                if (lastPaintToolMode != KaplaoToolMode.Delete)
                {
                    lastPaintCell = null;
                    lastPaintToolMode = KaplaoToolMode.Delete;
                }

                if (lastPaintCell.HasValue && lastPaintCell.Value == paintCell)
                    return;

                lastPaintCell = paintCell;
                TryDeleteTile(point);
                return;
            }

            if (input.primaryReleased)
            {
                lastPaintCell = null;
                TryDeleteTile(input.worldPosition);
            }
        }

        [Server]
        public void ServerBeginCreateTransform(CreateTransformMode mode) => BeginCreateTransform(mode);

        [Server]
        public void ServerFinalizeCreateTransform(
            uint[] netIds,
            Vector2[] positions,
            float[] rotations,
            Vector2[] scales)
        {
            if (hostGameMode != HostGameMode.Create)
                return;

            if (netIds == null || positions == null || rotations == null || scales == null)
                return;

            int count = Mathf.Min(
                netIds.Length,
                Mathf.Min(positions.Length, Mathf.Min(rotations.Length, scales.Length)));
            if (count == 0)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            // Begin may still be in flight under high ping — capture undo once.
            if (syncedActiveCreateTransform == CreateTransformMode.None)
                CaptureUndoSnapshot();

            for (int i = 0; i < count; i++)
            {
                if (!grid.TryGetTileByNetId(netIds[i], out LevelTileServer tile))
                    continue;

                tile.ServerSetTransform(positions[i], rotations[i], scales[i], roundValues: false);
            }

            ClearCreateTransformState();
        }

        [Server]
        public void ServerCancelCreateTransform(
            uint[] netIds,
            Vector2[] positions,
            float[] rotations,
            Vector2[] scales)
        {
            if (hostGameMode != HostGameMode.Create)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null &&
                netIds != null &&
                positions != null &&
                rotations != null &&
                scales != null)
            {
                int count = Mathf.Min(
                    netIds.Length,
                    Mathf.Min(positions.Length, Mathf.Min(rotations.Length, scales.Length)));
                for (int i = 0; i < count; i++)
                {
                    if (!grid.TryGetTileByNetId(netIds[i], out LevelTileServer tile))
                        continue;

                    tile.ServerSetTransform(positions[i], rotations[i], scales[i], roundValues: false);
                }
            }
            else if (syncedActiveCreateTransform != CreateTransformMode.None)
            {
                CancelCreateTransform();
                return;
            }

            bool hadTransform = syncedActiveCreateTransform != CreateTransformMode.None;
            ClearCreateTransformState();
            if (hadTransform && undoHistory.Count > 0)
                undoHistory.Pop();
        }

        [Server]
        void BeginCreateTransform(CreateTransformMode mode)
        {
            if (SelectedTiles.Count == 0 || syncedActiveCreateTransform != CreateTransformMode.None)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            if (!TryComputeSelectionPivot(grid, out Vector2 pivot))
                return;

            transformBaselines.Clear();
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile))
                    continue;

                transformBaselines[SelectedTiles[i]] = new TileTransformBaseline
                {
                    Position = tile.Position,
                    Rotation = tile.PlacementRotationDegrees,
                    Scale = tile.Scale
                };
            }

            if (transformBaselines.Count == 0)
                return;

            CaptureUndoSnapshot();
            syncedActiveCreateTransform = mode;
            transformAxisConstraint = TransformAxisConstraint.None;
            transformPivot = pivot;
            transformCursorInitialized = false;
        }

        [Server]
        void HandleActiveTransformInput(KaplaoCursorInput input)
        {
            if (input.axisXPressed)
                transformAxisConstraint = TransformAxisConstraint.X;
            else if (input.axisYPressed)
                transformAxisConstraint = TransformAxisConstraint.Y;

            // Confirm/cancel are committed by CmdFinalize / CmdCancel with absolute poses
            // so high-ping clients do not snap to a delayed cursor delta.
            if (input.primaryReleased || input.secondaryReleased)
                return;

            UpdateActiveCreateTransform(input.worldPosition);
        }

        [Server]
        void UpdateActiveCreateTransform(Vector2 cursorPosition)
        {
            if (syncedActiveCreateTransform == CreateTransformMode.None || transformBaselines.Count == 0)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            if (!transformCursorInitialized)
            {
                transformStartCursorPos = cursorPosition;
                Vector2 dir = cursorPosition - transformPivot;
                transformStartCursorAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transformStartCursorDistance = Mathf.Max(dir.magnitude, 0.001f);
                transformCursorInitialized = true;
                return;
            }

            switch (syncedActiveCreateTransform)
            {
                case CreateTransformMode.Move:
                    ApplyMoveTransform(grid, cursorPosition);
                    break;
                case CreateTransformMode.Rotate:
                    ApplyRotateTransform(grid, cursorPosition);
                    break;
                case CreateTransformMode.Scale:
                    ApplyScaleTransform(grid, cursorPosition);
                    break;
            }
        }

        [Server]
        void ApplyMoveTransform(LevelGridServer grid, Vector2 cursorPosition)
        {
            Vector2 delta = cursorPosition - transformStartCursorPos;
            if (transformAxisConstraint == TransformAxisConstraint.X)
                delta.y = 0f;
            else if (transformAxisConstraint == TransformAxisConstraint.Y)
                delta.x = 0f;

            delta.x = CreateModeSnapSettings.SnapDelta(delta.x, syncedPositionSnapUnits);
            delta.y = CreateModeSnapSettings.SnapDelta(delta.y, syncedPositionSnapUnits);

            ApplyGroupTransformFromDelta(grid, delta, 0f, 1f, null);
        }

        [Server]
        void ApplyRotateTransform(LevelGridServer grid, Vector2 cursorPosition)
        {
            Vector2 dir = cursorPosition - transformPivot;
            if (dir.sqrMagnitude < 0.0001f)
                return;

            float cursorAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float deltaAngle = cursorAngle - transformStartCursorAngle;
            deltaAngle = CreateModeSnapSettings.SnapDelta(deltaAngle, syncedRotationSnapDegrees);

            ApplyGroupTransformFromDelta(grid, Vector2.zero, deltaAngle, 1f, null);
        }

        [Server]
        void ApplyScaleTransform(LevelGridServer grid, Vector2 cursorPosition)
        {
            Vector2 fromPivot = cursorPosition - transformPivot;
            float currentDistance = Mathf.Max(fromPivot.magnitude, 0.001f);
            float rawFactor = currentDistance / transformStartCursorDistance;
            float snappedFactor = ComputeSnappedUniformScaleFactor(rawFactor);

            ApplyGroupTransformFromDelta(grid, Vector2.zero, 0f, snappedFactor, transformAxisConstraint);
        }

        [Server]
        void ApplyGroupTransformFromDelta(
            LevelGridServer grid,
            Vector2 moveDelta,
            float rotateDeltaDegrees,
            float uniformScaleFactor,
            TransformAxisConstraint? scaleAxis)
        {
            foreach (KeyValuePair<uint, TileTransformBaseline> entry in transformBaselines)
            {
                if (!grid.TryGetTileByNetId(entry.Key, out LevelTileServer tile))
                    continue;

                TileTransformBaseline baseline = entry.Value;
                Vector2 newPosition = baseline.Position + moveDelta;
                float newRotation = baseline.Rotation;
                Vector2 newScale = baseline.Scale;

                if (Mathf.Abs(rotateDeltaDegrees) > 0.0001f)
                {
                    Vector2 offset = baseline.Position - transformPivot;
                    Vector2 rotatedOffset = RotateVector2(offset, rotateDeltaDegrees);
                    newPosition = transformPivot + rotatedOffset;
                    newRotation = baseline.Rotation + rotateDeltaDegrees;
                }

                if (Mathf.Abs(uniformScaleFactor - 1f) > 0.0001f)
                {
                    TransformAxisConstraint axis = scaleAxis ?? TransformAxisConstraint.None;
                    Vector2 offset = baseline.Position - transformPivot;

                    switch (axis)
                    {
                        case TransformAxisConstraint.X:
                            newPosition = baseline.Position + new Vector2(offset.x * (uniformScaleFactor - 1f), 0f);
                            newScale = new Vector2(
                                CreateModeSnapSettings.SnapScale(baseline.Scale.x * uniformScaleFactor, syncedScaleSnapUnits),
                                baseline.Scale.y);
                            break;
                        case TransformAxisConstraint.Y:
                            newPosition = baseline.Position + new Vector2(0f, offset.y * (uniformScaleFactor - 1f));
                            newScale = new Vector2(
                                baseline.Scale.x,
                                CreateModeSnapSettings.SnapScale(baseline.Scale.y * uniformScaleFactor, syncedScaleSnapUnits));
                            break;
                        default:
                            newPosition = transformPivot + offset * uniformScaleFactor;
                            newScale = baseline.Scale * uniformScaleFactor;
                            newScale = new Vector2(
                                CreateModeSnapSettings.SnapScale(newScale.x, syncedScaleSnapUnits),
                                CreateModeSnapSettings.SnapScale(newScale.y, syncedScaleSnapUnits));
                            break;
                    }
                }

                tile.ServerSetTransform(newPosition, newRotation, newScale, roundValues: false);
            }
        }

        float ComputeSnappedUniformScaleFactor(float rawFactor)
        {
            float snappedDelta = CreateModeSnapSettings.SnapDelta(
                rawFactor - 1f,
                syncedScaleSnapUnits);
            return Mathf.Max(0.01f, 1f + snappedDelta);
        }

        [Server]
        public void ServerSetSnapSettings(float rotationDegrees, float positionUnits, float scaleUnits)
        {
            syncedRotationSnapDegrees = Mathf.Max(0.0001f, rotationDegrees);
            syncedPositionSnapUnits = Mathf.Max(0.0001f, positionUnits);
            syncedScaleSnapUnits = Mathf.Max(0.0001f, scaleUnits);
        }

        [Server]
        public void ServerSetHideCursor(bool hideCursor)
        {
            syncedHideCursor = hideCursor;
        }

        [Server]
        public void ServerRestoreKaplaoSession(
            HostGameMode mode,
            int editorLayer,
            uint[] selectedNetIds,
            float rotationSnapDegrees,
            float positionSnapUnits,
            float scaleSnapUnits)
        {
            ServerSetSnapSettings(rotationSnapDegrees, positionSnapUnits, scaleSnapUnits);
            syncedActiveEditorLayer = editorLayer;

            ReleaseGrab(false);
            selectionDragActive = false;
            selectionMarqueeActive = false;
            selectionMarqueeUsedThisDrag = false;
            selectionShiftWasHeld = false;
            CancelCreateTransform();

            hostGameMode = mode;

            suppressingSelectionUndo = true;
            try
            {
                SelectedTiles.Clear();
                if (mode == HostGameMode.Create &&
                    selectedNetIds != null &&
                    selectedNetIds.Length > 0)
                {
                    LevelGridServer grid = LevelGridServer.Instance;
                    var expanded = new HashSet<uint>();
                    for (int i = 0; i < selectedNetIds.Length; i++)
                    {
                        uint netId = selectedNetIds[i];
                        if (grid == null)
                        {
                            expanded.Add(netId);
                            continue;
                        }

                        if (grid.TryGetTileByNetId(netId, out _))
                            grid.ExpandSelectionWithLinks(netId, expanded);
                    }

                    foreach (uint netId in expanded)
                        SelectedTiles.Add(netId);
                }
            }
            finally
            {
                suppressingSelectionUndo = false;
            }

            SelectionChanged?.Invoke();
            if (connectionToClient != null)
                TargetRefreshSelectionVisuals(connectionToClient);
        }

        [Server]
        void ConfirmCreateTransform()
        {
            if (syncedActiveCreateTransform == CreateTransformMode.None)
                return;

            ClearCreateTransformState();
        }

        [Server]
        void CancelCreateTransform()
        {
            if (syncedActiveCreateTransform == CreateTransformMode.None)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null)
            {
                foreach (KeyValuePair<uint, TileTransformBaseline> entry in transformBaselines)
                {
                    if (!grid.TryGetTileByNetId(entry.Key, out LevelTileServer tile))
                        continue;

                    TileTransformBaseline baseline = entry.Value;
                    tile.ServerSetTransform(baseline.Position, baseline.Rotation, baseline.Scale, roundValues: false);
                }
            }

            ClearCreateTransformState();
            if (undoHistory.Count > 0)
                undoHistory.Pop();
        }

        [Server]
        void ClearCreateTransformState()
        {
            syncedActiveCreateTransform = CreateTransformMode.None;
            transformAxisConstraint = TransformAxisConstraint.None;
            transformBaselines.Clear();
            transformStartCursorPos = Vector2.zero;
            transformStartCursorAngle = 0f;
            transformStartCursorDistance = 0f;
            transformCursorInitialized = false;
        }

        [Server]
        void AbortCreateTransformIfActive()
        {
            if (syncedActiveCreateTransform != CreateTransformMode.None)
                CancelCreateTransform();
        }

        [Server]
        bool TryComputeSelectionPivot(LevelGridServer grid, out Vector2 pivot)
        {
            pivot = Vector2.zero;
            int count = 0;
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile))
                    continue;

                pivot += tile.Position;
                count++;
            }

            if (count == 0)
                return false;

            pivot /= count;
            return true;
        }

        static Vector2 RotateVector2(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
        }

        [Server]
        void HandleSelectionInput(KaplaoCursorInput input)
        {
            if (input.primaryPressed)
            {
                selectionDragActive = true;
                selectionDragStartWorld = input.selectionDragStart;
                selectionMarqueeUsedThisDrag = input.shiftHeld;
                selectionShiftWasHeld = input.shiftHeld;
                selectionMarqueeActive = input.shiftHeld;
                selectionMarqueeStart = selectionDragStartWorld;
                selectionMarqueeEnd = input.worldPosition;
                return;
            }

            if (selectionDragActive && input.selectionDragging)
            {
                selectionMarqueeEnd = input.worldPosition;

                if (input.shiftHeld)
                {
                    selectionMarqueeUsedThisDrag = true;
                    selectionMarqueeActive = true;
                    selectionShiftWasHeld = true;
                    return;
                }

                // Shift released mid-marquee: commit tiles already inside the green rectangle.
                if (selectionMarqueeUsedThisDrag && selectionShiftWasHeld)
                {
                    CommitMarqueeSelection(selectionDragStartWorld, selectionMarqueeEnd, input.ctrlHeld);
                    selectionDragActive = false;
                    selectionMarqueeActive = false;
                    selectionMarqueeUsedThisDrag = false;
                    selectionShiftWasHeld = false;
                    return;
                }

                selectionMarqueeActive = false;
                selectionShiftWasHeld = false;
                return;
            }

            if (selectionDragActive && input.primaryReleased)
            {
                Vector2 dragEnd = input.worldPosition;
                bool additive = input.ctrlHeld;
                bool marqueeMode = selectionMarqueeUsedThisDrag || input.shiftHeld;

                if (Vector2.Distance(selectionDragStartWorld, dragEnd) <= SingleSelectDragThreshold)
                {
                    if (TryGetRunnerAt(dragEnd, out NetworkPlayer runner))
                    {
                        if (runner.isAdmin &&
                            runner.netId != netId)
                        {
                            TargetShowCantChangeMode(connectionToClient);
                        }
                        else
                        {
                            TargetShowPromotePanel(connectionToClient, runner.netId, runner.displayName);
                        }

                        selectionDragActive = false;
                        selectionMarqueeActive = false;
                        selectionMarqueeUsedThisDrag = false;
                        selectionShiftWasHeld = false;
                        return;
                    }

                    ApplyPointSelection(dragEnd, additive);
                }
                else if (marqueeMode)
                {
                    CommitMarqueeSelection(selectionDragStartWorld, dragEnd, additive);
                }
                else
                {
                    ApplyPointSelection(dragEnd, additive);
                }

                selectionDragActive = false;
                selectionMarqueeActive = false;
                selectionMarqueeUsedThisDrag = false;
                selectionShiftWasHeld = false;
            }
        }

        [Server]
        void CommitMarqueeSelection(Vector2 start, Vector2 end, bool additive)
        {
            List<uint> ids = GetTilesInRectSelection(start, end);
            if (additive)
                AddToSelection(ids);
            else
                SetSelection(ids);
        }

        [Server]
        void ApplySingleSelection(uint netId, bool additive)
        {
            var expanded = new HashSet<uint>();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null)
                grid.ExpandSelectionWithLinks(netId, expanded);
            else
                expanded.Add(netId);

            if (!additive)
            {
                SetSelection(expanded);
                return;
            }

            CaptureSelectionUndo();
            bool changed = false;
            foreach (uint id in expanded)
            {
                if (IsTileSelected(id))
                    continue;

                AbortCreateTransformIfActive();
                SelectedTiles.Add(id);
                changed = true;
            }

            if (changed)
            {
                SelectionChanged?.Invoke();
                if (connectionToClient != null)
                    TargetRefreshSelectionVisuals(connectionToClient);
            }
        }

        [Server]
        void AddToSelection(IEnumerable<uint> ids)
        {
            LevelGridServer grid = LevelGridServer.Instance;
            var toAdd = new List<uint>();
            foreach (uint id in ids)
            {
                var expanded = new HashSet<uint>();
                if (grid != null)
                    grid.ExpandSelectionWithLinks(id, expanded);
                else
                    expanded.Add(id);

                foreach (uint linkedId in expanded)
                {
                    if (IsTileSelected(linkedId))
                        continue;
                    toAdd.Add(linkedId);
                }
            }

            if (toAdd.Count == 0)
                return;

            CaptureSelectionUndo();
            AbortCreateTransformIfActive();
            foreach (uint linkedId in toAdd)
                SelectedTiles.Add(linkedId);

            SelectionChanged?.Invoke();
            if (connectionToClient != null)
                TargetRefreshSelectionVisuals(connectionToClient);
        }

        bool IsTileSelectable(LevelTileServer tile)
        {
            if (tile == null)
                return false;

            if (syncedActiveEditorLayer >= 0 && tile.EditorLayer != syncedActiveEditorLayer)
                return false;

            if (LevelScriptRuntime.Instance != null && LevelScriptRuntime.Instance.IsRunning)
            {
                if (tile.IsAttachedToScriptPivot)
                    return false;

                KeyframeAnimator keyframeAnimator = KeyframeAnimator.Instance;
                if (keyframeAnimator != null && keyframeAnimator.IsTileAnimating(tile))
                    return false;

                ExecutorRuntime executorRuntime = ExecutorRuntime.Instance;
                if (executorRuntime != null && executorRuntime.IsTileInActiveMotion(tile))
                    return false;

                LevelGroupAnimator groupAnimator = LevelGroupAnimator.Instance;
                if (groupAnimator != null && groupAnimator.IsTileInMotion(tile))
                    return false;
            }

            if (selectionFilter == null || selectionFilter.IsDefault)
                return true;

            return selectionFilter.MatchesTile(
                tile.TileType,
                tile.TileColor,
                tile.CachedGroupIds,
                tile.CreatedAtUnixMs,
                tile.Transparency,
                tile.PlacementRotationDegrees,
                tile.Position,
                tile.Scale,
                tile.SortingOrder);
        }

        [Server]
        void ApplyPointSelection(Vector2 worldPoint, bool additive)
        {
            GetTilesAtPoint(worldPoint, tilesAtPointBuffer);
            if (tilesAtPointBuffer.Count == 0)
                return;

            if (additive || tilesAtPointBuffer.Count == 1)
            {
                ApplySingleSelection(tilesAtPointBuffer[0].netId, additive);
                ResetSelectionCycle(worldPoint, tilesAtPointBuffer);
                return;
            }

            bool samePoint = (worldPoint - selectionCyclePoint).sqrMagnitude <= SelectionCyclePointEpsilonSqr;
            bool sameCandidates = samePoint && SelectionCycleMatches(tilesAtPointBuffer);
            if (!sameCandidates)
            {
                selectionCycleIndex = 0;
                ApplySingleSelection(tilesAtPointBuffer[0].netId, false);
                ResetSelectionCycle(worldPoint, tilesAtPointBuffer);
                return;
            }

            selectionCycleIndex = (selectionCycleIndex + 1) % tilesAtPointBuffer.Count;
            ApplySingleSelection(tilesAtPointBuffer[selectionCycleIndex].netId, false);
        }

        [Server]
        void ResetSelectionCycle(Vector2 worldPoint, List<LevelTileServer> tiles)
        {
            selectionCyclePoint = worldPoint;
            selectionCycleIndex = 0;
            selectionCycleNetIds.Clear();
            for (int i = 0; i < tiles.Count; i++)
                selectionCycleNetIds.Add(tiles[i].netId);
        }

        bool SelectionCycleMatches(List<LevelTileServer> tiles)
        {
            if (tiles.Count != selectionCycleNetIds.Count)
                return false;

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].netId != selectionCycleNetIds[i])
                    return false;
            }

            return true;
        }

        [Server]
        void GetTilesAtPoint(Vector2 worldPoint, List<LevelTileServer> results)
        {
            results.Clear();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            List<LevelTileServer> nearby = grid.GetTilesInWorldRect(worldPoint - Vector2.one * 0.6f, worldPoint + Vector2.one * 0.6f);
            float maxDistSqr = 0.6f * 0.6f;
            for (int i = 0; i < nearby.Count; i++)
            {
                LevelTileServer candidate = nearby[i];
                if (!IsTileSelectable(candidate))
                    continue;

                float dist = (candidate.Position - worldPoint).sqrMagnitude;
                if (dist > maxDistSqr)
                    continue;

                results.Add(candidate);
            }

            results.Sort((a, b) =>
            {
                int order = b.SortingOrder.CompareTo(a.SortingOrder);
                if (order != 0)
                    return order;

                return a.netId.CompareTo(b.netId);
            });
        }

        [Server]
        bool TryGetTopTileAtPoint(Vector2 worldPoint, out LevelTileServer tile)
        {
            tile = null;
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return false;

            List<LevelTileServer> nearby = grid.GetTilesInWorldRect(worldPoint - Vector2.one * 0.6f, worldPoint + Vector2.one * 0.6f);
            float best = float.MaxValue;
            for (int i = 0; i < nearby.Count; i++)
            {
                LevelTileServer candidate = nearby[i];
                if (!IsTileSelectable(candidate))
                    continue;

                float dist = (candidate.Position - worldPoint).sqrMagnitude;
                if (dist > 0.6f * 0.6f || dist >= best)
                    continue;

                best = dist;
                tile = candidate;
            }

            return tile != null;
        }

        [Server]
        List<uint> GetTilesInRectSelection(Vector2 start, Vector2 end)
        {
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return new List<uint>();

            List<LevelTileServer> tiles = grid.GetTilesInWorldRect(start, end);
            var ids = new List<uint>(tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!IsTileSelectable(tiles[i]))
                    continue;

                ids.Add(tiles[i].netId);
            }

            var expanded = new HashSet<uint>();
            for (int i = 0; i < ids.Count; i++)
                grid.ExpandSelectionWithLinks(ids[i], expanded);

            return new List<uint>(expanded);
        }

        [Server]
        public void ApplyColorToSelectedTiles(Color32 color)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            lastAppliedSelectionColor = color;
            LevelGridServer.Instance?.TrySetTilesColorByNetIds(SelectedTiles, color);
        }

        [Server]
        public void ApplyTextureToSelectedTiles(string textureId)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.TrySetTilesTextureByNetIds(SelectedTiles, textureId);
        }

        [Server]
        public void ApplyParticleSettingsToSelectedTiles(ParticleEmitterRecord record)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Particle)
                    continue;

                if (tile.TryGetComponent(out ParticleEmitterTile emitter))
                    emitter.ServerApply(record);
            }
        }

        [Server]
        public void ApplyKeyframeSettingsToSelectedTiles(KeyframeRecord record)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Keyframe)
                    continue;

                if (tile.TryGetComponent(out KeyframeTile keyframe))
                    keyframe.ServerApply(record);
            }
        }

        [Server]
        public void ApplyKeyframeSettingsPatchToSelectedTiles(KeyframePatchRecord patch)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            // Nothing to patch.
            if (!patch.hasDuration && !patch.hasAnim && !patch.hasSpawnId && !patch.hasCurve &&
                !patch.hasCloseLoop && !patch.hasDistance)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            if (patch.hasDistance && patch.distance)
                patch = EnrichDistancePatchFromSelection(grid, patch, SelectedTiles);

            bool applyGroupWide = patch.hasCloseLoop || patch.hasDistance;
            if (!applyGroupWide && (patch.hasDuration || patch.hasAnim))
            {
                for (int i = 0; i < SelectedTiles.Count; i++)
                {
                    if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                        continue;

                    if (tile.TileType != TileType.Keyframe)
                        continue;

                    IReadOnlyList<int> groups = tile.CachedGroupIds;
                    for (int g = 0; g < groups.Count; g++)
                    {
                        if (KeyframeAnimator.GetDistanceForGroup(groups[g]))
                        {
                            applyGroupWide = true;
                            break;
                        }
                    }

                    if (applyGroupWide)
                        break;
                }
            }

            if (applyGroupWide)
            {
                ApplyKeyframePatchToSelectedGroups(grid, patch);
                return;
            }

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Keyframe)
                    continue;

                if (tile.TryGetComponent(out KeyframeTile keyframe))
                    keyframe.ServerApplyPatch(patch);
            }
        }

        static KeyframePatchRecord EnrichDistancePatchFromSelection(
            LevelGridServer grid,
            KeyframePatchRecord patch,
            IReadOnlyList<uint> selectedTiles)
        {
            for (int i = 0; i < selectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(selectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Keyframe ||
                    !tile.TryGetComponent(out KeyframeTile keyframe))
                    continue;

                patch.hasDuration = true;
                patch.duration = keyframe.Duration;
                patch.hasAnim = true;
                patch.anim = keyframe.Anim;
                return patch;
            }

            return patch;
        }

        void ApplyKeyframePatchToSelectedGroups(LevelGridServer grid, KeyframePatchRecord patch)
        {
            var groupIds = new HashSet<int>();
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Keyframe)
                    continue;

                IReadOnlyList<int> groups = tile.CachedGroupIds;
                for (int g = 0; g < groups.Count; g++)
                    groupIds.Add(groups[g]);
            }

            var keyframes = new List<LevelTileServer>();
            foreach (int groupId in groupIds)
            {
                keyframes.Clear();
                grid.CollectTilesWithGroup(groupId, keyframes);
                for (int i = 0; i < keyframes.Count; i++)
                {
                    LevelTileServer tile = keyframes[i];
                    if (tile == null || tile.TileType != TileType.Keyframe ||
                        !tile.TryGetComponent(out KeyframeTile keyframe))
                        continue;

                    keyframe.ServerApplyPatch(patch);
                }
            }
        }

        [Server]
        public void ApplyExecutorSettingsToSelectedTiles(ExecutorTileSettings settings)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            if (settings == null || !settings.HasAnyField)
                return;

            var netIds = new uint[SelectedTiles.Count];
            for (int i = 0; i < SelectedTiles.Count; i++)
                netIds[i] = SelectedTiles[i];

            ApplyExecutorSettingsToTiles(netIds, settings);
        }

        [Server]
        public void ApplyExecutorSettingsToTiles(uint[] netIds, ExecutorTileSettings settings)
        {
            if (hostGameMode != HostGameMode.Create || netIds == null || netIds.Length == 0)
                return;

            if (settings == null || !settings.HasAnyField)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < netIds.Length; i++)
            {
                if (!grid.TryGetTileByNetId(netIds[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Executor)
                    continue;

                if (tile.TryGetComponent(out ExecutorTile executor))
                    executor.ServerApplySettings(settings);
            }
        }

        [Server]
        public void ApplyExecutorStatesToSelectedTiles(ExecutorGroupState start, ExecutorGroupState end)
        {
            if (SelectedTiles.Count == 0)
                return;

            var netIds = new uint[SelectedTiles.Count];
            for (int i = 0; i < SelectedTiles.Count; i++)
                netIds[i] = SelectedTiles[i];

            ApplyExecutorStatesToTiles(netIds, start, end);
        }

        [Server]
        public void ApplyExecutorStatesToTiles(uint[] netIds, ExecutorGroupState start, ExecutorGroupState end)
        {
            if (hostGameMode != HostGameMode.Create || netIds == null || netIds.Length == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < netIds.Length; i++)
            {
                if (!grid.TryGetTileByNetId(netIds[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Executor)
                    continue;

                if (tile.TryGetComponent(out ExecutorTile executor))
                    executor.ServerApplyStates(start, end);
            }
        }

        [Server]
        public void ApplySpawnPointToSelectedTriggers(bool isSpawnPoint)
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Trigger)
                    continue;

                tile.ServerSetSpawnPoint(isSpawnPoint);
            }

            LevelScriptRuntime.Instance?.RefreshTileSpawnPointHandlers();
        }

        [Server]
        public void ApplySortingOrderToSelectedTiles(int sortingOrder)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.TrySetTilesSortingOrderByNetIds(SelectedTiles, sortingOrder);
        }

        [Server]
        public void SetSelection(IEnumerable<uint> netIds)
        {
            CaptureSelectionUndo();
            AbortCreateTransformIfActive();
            SelectedTiles.Clear();

            LevelGridServer grid = LevelGridServer.Instance;
            var expanded = new HashSet<uint>();
            foreach (uint netId in netIds)
            {
                if (grid != null)
                    grid.ExpandSelectionWithLinks(netId, expanded);
                else
                    expanded.Add(netId);
            }

            foreach (uint netId in expanded)
                SelectedTiles.Add(netId);

            SelectionChanged?.Invoke();
            if (connectionToClient != null)
                TargetRefreshSelectionVisuals(connectionToClient);
        }

        [Server]
        public void ClearSelection()
        {
            bool hadSelection = SelectedTiles.Count > 0 || syncedActiveCreateTransform != CreateTransformMode.None;
            if (hadSelection)
            {
                CaptureSelectionUndo();
                AbortCreateTransformIfActive();
                SelectedTiles.Clear();
            }

            // Always refresh owning client visuals — SyncList clear can leave local outlines stuck
            // (especially after ReplacePlayer / second Kaplao).
            SelectionChanged?.Invoke();
            if (connectionToClient != null)
                TargetRefreshSelectionVisuals(connectionToClient);
        }

        [Server]
        public void DeleteSelectedTiles()
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.TryRemoveTilesByNetIds(SelectedTiles);
            suppressingSelectionUndo = true;
            try
            {
                ClearSelection();
            }
            finally
            {
                suppressingSelectionUndo = false;
            }
        }

        [Server]
        public void SetSelectedTilesGroups(string groups)
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            string normalized = TileGroups.Normalize(groups);
            LevelGridServer.Instance?.TrySetTilesGroupsByNetIds(SelectedTiles, normalized);
        }

        [Server]
        public void AddGroupToSelectedTiles(int group)
        {
            if (SelectedTiles.Count == 0 || group < 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                var numbers = new HashSet<int>(TileGroups.ParseNumbers(tile.GroupsDisplay));
                if (!numbers.Add(group))
                    continue;

                tile.ServerSetGroups(TileGroups.Join(numbers));
            }
        }

        [Server]
        public void RemoveGroupFromSelectedTiles(int group)
        {
            if (SelectedTiles.Count == 0 || group < 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                var numbers = new HashSet<int>(TileGroups.ParseNumbers(tile.GroupsDisplay));
                if (!numbers.Remove(group))
                    continue;

                tile.ServerSetGroups(TileGroups.Join(numbers));
            }
        }

        public int FindNextFreeGroupId()
        {
            var used = new HashSet<int>();
            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                TileGroups.AddNumbers(used, TileGroups.ParseNumbers(tiles[i].GroupsDisplay));

            for (int id = 1; id < 100000; id++)
            {
                if (!used.Contains(id))
                    return id;
            }

            return 1;
        }

        /// <summary>
        /// Fills all groups present on the selection. Groups in <paramref name="onAllTiles"/>
        /// exist on every selected tile; others are partial.
        /// </summary>
        public void CollectSelectedGroupStates(List<int> allGroups, HashSet<int> onAllTiles)
        {
            allGroups.Clear();
            onAllTiles.Clear();

            if (SelectedTiles.Count == 0)
                return;

            HashSet<int> union = null;
            HashSet<int> intersection = null;
            LevelGridServer grid = LevelGridServer.Instance;

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                LevelTileServer tile = null;
                if (grid != null)
                    grid.TryGetTileByNetId(SelectedTiles[i], out tile);
                if (tile == null)
                    tile = FindTileByNetId(SelectedTiles[i]);
                if (tile == null)
                    continue;

                var tileGroups = new HashSet<int>(TileGroups.ParseNumbers(tile.GroupsDisplay));
                if (union == null)
                {
                    union = new HashSet<int>(tileGroups);
                    intersection = new HashSet<int>(tileGroups);
                }
                else
                {
                    union.UnionWith(tileGroups);
                    intersection.IntersectWith(tileGroups);
                }
            }

            if (union == null)
                return;

            var sorted = new List<int>(union);
            sorted.Sort();
            allGroups.AddRange(sorted);
            foreach (int g in intersection)
                onAllTiles.Add(g);
        }

        [Server]
        public void SetSelectedTilesTransparency(float transparency)
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.TrySetTilesTransparencyByNetIds(SelectedTiles, Mathf.Clamp01(transparency));
        }

        [Server]
        public void SetSelectedTilesEditorLayer(int editorLayer)
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.TrySetTilesEditorLayerByNetIds(SelectedTiles, editorLayer);
        }

        [Server]
        public void StepEditorLayer(int delta)
        {
            if (hostGameMode != HostGameMode.Create)
                return;

            if (delta == 0)
                return;

            int next;
            if (syncedActiveEditorLayer < 0)
            {
                next = delta > 0 ? 0 : 100;
            }
            else
            {
                next = syncedActiveEditorLayer + delta;
                if (next < 0)
                    next = -1;
                else if (next > 100)
                    next = -1;
            }

            syncedActiveEditorLayer = next;
        }

        [Server]
        public void LinkSelectedTiles()
        {
            if (SelectedTiles.Count < 2)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.LinkSelectedTiles(SelectedTiles);
        }

        [Server]
        public void UnlinkSelectedTiles()
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.UnlinkSelectedTiles(SelectedTiles);
        }

        public string GetActiveEditorLayerLabel()
        {
            return syncedActiveEditorLayer < 0 ? "All" : syncedActiveEditorLayer.ToString();
        }

        [Server]
        public void SetSelectedTilesNoHitbox(bool noHitbox)
        {
            if (SelectedTiles.Count == 0)
                return;

            CaptureUndoSnapshot();
            LevelGridServer.Instance?.TrySetTilesNoHitboxByNetIds(SelectedTiles, noHitbox);
        }

        [Server]
        public void MoveSelectedTilesBy(float dx, float dy, uint[] clientNetIds = null)
        {
            if (hostGameMode != HostGameMode.Create)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            var ids = new List<uint>();
            if (SelectedTiles.Count > 0)
            {
                for (int i = 0; i < SelectedTiles.Count; i++)
                    ids.Add(SelectedTiles[i]);
            }
            else if (clientNetIds != null && clientNetIds.Length > 0)
            {
                for (int i = 0; i < clientNetIds.Length; i++)
                {
                    uint netId = clientNetIds[i];
                    if (netId == 0 || !grid.TryGetTileByNetId(netId, out _))
                        continue;
                    ids.Add(netId);
                }

                if (ids.Count == 0)
                    return;

                SetSelection(ids);
            }

            if (ids.Count == 0)
                return;

            CaptureUndoSnapshot();
            AbortCreateTransformIfActive();

            for (int i = 0; i < ids.Count; i++)
            {
                if (!grid.TryGetTileByNetId(ids[i], out LevelTileServer tile))
                    continue;

                tile.ServerSetTransform(
                    tile.Position + new Vector2(dx, dy),
                    tile.PlacementRotationDegrees,
                    tile.Scale,
                    roundValues: false);
            }
        }

        [Server]
        public void CopySelectedTilesToClipboard()
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            clipboardTiles.Clear();
            clipboardLinkGroups.Clear();
            Vector2 sum = Vector2.zero;
            int count = 0;
            var netIdToClipboardIndex = new Dictionary<uint, int>();

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                uint netId = SelectedTiles[i];
                if (!grid.TryGetTileByNetId(netId, out LevelTileServer tile) || tile == null)
                    continue;

                Vector2 position = tile.Position;
                sum += position;
                count++;

                var record = new ClipboardTileRecord
                {
                    Type = tile.TileType,
                    OffsetFromCenter = position,
                    RotationDegrees = tile.PlacementRotationDegrees,
                    Scale = tile.Scale,
                    Color = tile.TileColor,
                    Groups = tile.GroupsDisplay,
                    QuarterTurns = tile.QuarterTurns,
                    Transparency = tile.Transparency,
                    NoHitbox = tile.HasNoHitbox,
                    TextureId = tile.TextureId,
                    SortingOrder = tile.SortingOrder,
                    EditorLayer = tile.EditorLayer
                };

                if (tile.TileType == TileType.Particle &&
                    tile.TryGetComponent(out ParticleEmitterTile emitter))
                {
                    record.HasParticle = true;
                    record.Particle = emitter.ExportRecord();
                }

                netIdToClipboardIndex[netId] = clipboardTiles.Count;
                clipboardTiles.Add(record);
            }

            if (count == 0)
            {
                clipboardHasData = false;
                return;
            }

            Vector2 center = sum / count;
            for (int i = 0; i < clipboardTiles.Count; i++)
            {
                ClipboardTileRecord record = clipboardTiles[i];
                record.OffsetFromCenter -= center;
                clipboardTiles[i] = record;
            }

            var processedNetIds = new HashSet<uint>();
            foreach (KeyValuePair<uint, int> entry in netIdToClipboardIndex)
            {
                if (!processedNetIds.Add(entry.Key))
                    continue;

                var linked = new HashSet<uint>();
                grid.ExpandSelectionWithLinks(entry.Key, linked);
                if (linked.Count < 2)
                    continue;

                var indices = new List<int>(linked.Count);
                foreach (uint linkedNetId in linked)
                {
                    processedNetIds.Add(linkedNetId);
                    if (netIdToClipboardIndex.TryGetValue(linkedNetId, out int clipboardIndex))
                        indices.Add(clipboardIndex);
                }

                if (indices.Count >= 2)
                    clipboardLinkGroups.Add(indices.ToArray());
            }

            clipboardHasData = true;
        }

        [Server]
        public void PasteClipboardTiles(Vector2 worldCenter)
        {
            if (hostGameMode != HostGameMode.Create || !clipboardHasData || clipboardTiles.Count == 0)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            CaptureUndoSnapshot();
            AbortCreateTransformIfActive();

            // Keep fractional offsets so multi-tile layouts don't collapse onto one cell.
            Vector2 pasteCenter = new Vector2(
                Mathf.Round(worldCenter.x * 100f) * 0.01f,
                Mathf.Round(worldCenter.y * 100f) * 0.01f);
            var pastedByClipboardIndex = new uint[clipboardTiles.Count];
            var pasted = new List<uint>(clipboardTiles.Count);

            for (int i = 0; i < clipboardTiles.Count; i++)
            {
                ClipboardTileRecord record = clipboardTiles[i];
                Vector2 position = pasteCenter + record.OffsetFromCenter;

                uint newId = grid.TryPlaceTile(
                    record.Type,
                    position,
                    record.RotationDegrees,
                    record.Color,
                    record.Groups,
                    record.QuarterTurns,
                    record.Transparency,
                    record.NoHitbox,
                    record.TextureId,
                    record.SortingOrder,
                    record.EditorLayer);
                pastedByClipboardIndex[i] = newId;
                if (newId == 0 || !grid.TryGetTileByNetId(newId, out LevelTileServer placedTile))
                    continue;

                placedTile.ServerSetTransform(position, record.RotationDegrees, record.Scale, roundValues: false);

                if (record.HasParticle &&
                    placedTile.TryGetComponent(out ParticleEmitterTile emitter))
                {
                    emitter.ServerApply(record.Particle);
                }

                pasted.Add(newId);
            }

            for (int g = 0; g < clipboardLinkGroups.Count; g++)
            {
                int[] indices = clipboardLinkGroups[g];
                if (indices == null || indices.Length < 2)
                    continue;

                var linkedNetIds = new List<uint>(indices.Length);
                for (int i = 0; i < indices.Length; i++)
                {
                    int clipboardIndex = indices[i];
                    if (clipboardIndex < 0 || clipboardIndex >= pastedByClipboardIndex.Length)
                        continue;

                    uint netId = pastedByClipboardIndex[clipboardIndex];
                    if (netId != 0)
                        linkedNetIds.Add(netId);
                }

                if (linkedNetIds.Count >= 2)
                    grid.LinkSelectedTiles(linkedNetIds);
            }

            if (pasted.Count == 0)
            {
                if (undoHistory.Count > 0)
                    undoHistory.Pop();
                return;
            }

            suppressingSelectionUndo = true;
            try
            {
                SetSelection(pasted);
            }
            finally
            {
                suppressingSelectionUndo = false;
            }
        }

        [Server]
        public void DuplicateSelectedTilesRight()
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0)
                return;

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            var sources = new List<LevelTileServer>(SelectedTiles.Count);
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                if (!grid.TryGetTileByNetId(SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                sources.Add(tile);
                float x = tile.Position.x;
                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;
            }

            if (sources.Count == 0)
                return;

            // Shift the whole selection clear of its own bounds. Do not gate on cell
            // occupancy — close/overlapping tiles (same cell or <0.5 apart) would
            // otherwise make every candidate offset fail and Ctrl+D would do nothing.
            float selectionWidth = Mathf.Max(0f, maxX - minX);
            Vector2 offset = new Vector2(selectionWidth + GridMath.CellSize, 0f);

            CaptureUndoSnapshot();
            AbortCreateTransformIfActive();

            var duplicated = new List<uint>(sources.Count);
            var sourceToDuplicate = new Dictionary<uint, uint>(sources.Count);

            for (int i = 0; i < sources.Count; i++)
            {
                LevelTileServer tile = sources[i];
                Vector2 position = tile.Position + offset;
                float rotation = tile.PlacementRotationDegrees;
                Vector2 scale = tile.Scale;

                uint newId = grid.TryPlaceTile(
                    tile.TileType,
                    position,
                    rotation,
                    tile.TileColor,
                    tile.GroupsDisplay,
                    tile.QuarterTurns,
                    tile.Transparency,
                    tile.HasNoHitbox,
                    tile.TextureId,
                    tile.SortingOrder,
                    tile.EditorLayer);
                if (newId == 0 ||
                    !grid.TryGetTileByNetId(newId, out LevelTileServer duplicatedTile))
                {
                    continue;
                }

                duplicatedTile.ServerSetTransform(position, rotation, scale, roundValues: false);

                if (tile.TileType == TileType.Particle &&
                    tile.TryGetComponent(out ParticleEmitterTile sourceEmitter) &&
                    duplicatedTile.TryGetComponent(out ParticleEmitterTile duplicatedEmitter))
                {
                    duplicatedEmitter.ServerApply(sourceEmitter.ExportRecord());
                }

                sourceToDuplicate[tile.netId] = newId;
                duplicated.Add(newId);
            }

            if (duplicated.Count == 0)
            {
                if (undoHistory.Count > 0)
                    undoHistory.Pop();
                return;
            }

            var processedSources = new HashSet<uint>();
            foreach (KeyValuePair<uint, uint> pair in sourceToDuplicate)
            {
                if (!processedSources.Add(pair.Key))
                    continue;

                var linked = new HashSet<uint>();
                grid.ExpandSelectionWithLinks(pair.Key, linked);
                if (linked.Count < 2)
                    continue;

                var duplicateLinkIds = new List<uint>();
                foreach (uint linkedNetId in linked)
                {
                    processedSources.Add(linkedNetId);
                    if (sourceToDuplicate.TryGetValue(linkedNetId, out uint duplicateId))
                        duplicateLinkIds.Add(duplicateId);
                }

                if (duplicateLinkIds.Count >= 2)
                    grid.LinkSelectedTiles(duplicateLinkIds);
            }

            suppressingSelectionUndo = true;
            try
            {
                SetSelection(duplicated);
            }
            finally
            {
                suppressingSelectionUndo = false;
            }
        }

        public string GetSelectedTilesGroupsSummary()
        {
            var groups = new HashSet<int>();
            for (int i = 0; i < SelectedTiles.Count; i++)
                AddGroupsForTile(SelectedTiles[i], groups);

            return TileGroups.Join(groups);
        }

        public string GetAllLevelGroupsSummary()
        {
            var groups = new HashSet<int>();
            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                TileGroups.AddNumbers(groups, TileGroups.ParseNumbers(tiles[i].GroupsDisplay));

            return TileGroups.Join(groups);
        }

        public bool AreAllSelectedTilesFullyTransparent()
        {
            return AreAllSelectedTilesMatching(tile => tile.Transparency <= 0f);
        }

        public float GetSelectedTilesTransparency()
        {
            if (SelectedTiles.Count == 0)
                return 1f;

            float value = 1f;
            bool hasValue = false;
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                LevelTileServer tile = FindTileByNetId(SelectedTiles[i]);
                if (tile == null)
                    continue;

                if (!hasValue)
                {
                    value = tile.Transparency;
                    hasValue = true;
                    continue;
                }

                if (!Mathf.Approximately(value, tile.Transparency))
                    return float.NaN;
            }

            return hasValue ? value : 1f;
        }

        public bool TryGetSelectedTilesEditorLayer(out int editorLayer)
        {
            editorLayer = 0;
            if (SelectedTiles.Count == 0)
                return false;

            bool hasValue = false;
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                LevelTileServer tile = FindTileByNetId(SelectedTiles[i]);
                if (tile == null)
                    continue;

                if (!hasValue)
                {
                    editorLayer = tile.EditorLayer;
                    hasValue = true;
                    continue;
                }

                if (editorLayer != tile.EditorLayer)
                    return false;
            }

            return hasValue;
        }

        public bool AreAllSelectedTilesNoHitbox()
        {
            return AreAllSelectedTilesMatching(tile => tile.HasNoHitbox);
        }

        public bool TryGetSingleSelectedTile(out uint netId, out Vector2 position, out float rotationDegrees, out Vector2 scale, out int sortingOrder)
        {
            netId = 0;
            position = Vector2.zero;
            rotationDegrees = 0f;
            scale = Vector2.one;
            sortingOrder = 0;

            if (SelectedTiles.Count != 1)
                return false;

            netId = SelectedTiles[0];
            LevelTileServer tile = FindTileByNetId(netId);
            if (tile == null)
                return false;

            position = tile.Position;
            rotationDegrees = tile.PlacementRotationDegrees;
            scale = tile.Scale;
            sortingOrder = tile.SortingOrder;
            return true;
        }

        public bool TryGetSingleSelectedTileObject(out LevelTileServer tile)
        {
            tile = null;
            if (SelectedTiles.Count != 1)
                return false;

            tile = FindTileByNetId(SelectedTiles[0]);
            return tile != null;
        }

        bool AreAllSelectedTilesMatching(System.Func<LevelTileServer, bool> predicate)
        {
            if (SelectedTiles.Count == 0)
                return false;

            bool foundAny = false;
            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                LevelTileServer tile = FindTileByNetId(SelectedTiles[i]);
                if (tile == null)
                    continue;

                foundAny = true;
                if (!predicate(tile))
                    return false;
            }

            return foundAny;
        }

        LevelTileServer FindTileByNetId(uint netId)
        {
            if (NetworkServer.active && LevelGridServer.Instance != null &&
                LevelGridServer.Instance.TryGetTileByNetId(netId, out LevelTileServer serverTile))
            {
                return serverTile;
            }

            if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) &&
                identity != null &&
                identity.TryGetComponent(out LevelTileServer clientTile))
            {
                return clientTile;
            }

            return null;
        }

        void AddGroupsForTile(uint netId, HashSet<int> groups)
        {
            if (NetworkServer.active && LevelGridServer.Instance != null &&
                LevelGridServer.Instance.TryGetTileByNetId(netId, out LevelTileServer serverTile))
            {
                TileGroups.AddNumbers(groups, TileGroups.ParseNumbers(serverTile.GroupsDisplay));
            }
        }

        [Server]
        public void SetSelectedTileTransform(uint netId, float x, float y, float rotationDegrees, float scaleX, float scaleY, int sortingOrder)
        {
            ApplyTransformPatchToTile(netId, TileTransformPatchRecord.Full(x, y, rotationDegrees, scaleX, scaleY, sortingOrder));
        }

        [Server]
        public void ApplyTransformPatchToTile(uint netId, TileTransformPatchRecord patch)
        {
            if (hostGameMode != HostGameMode.Create || !patch.HasAnyField)
                return;

            LevelTileServer tile = FindTileByNetId(netId);
            if (tile == null)
                return;

            CaptureUndoSnapshot();
            AbortCreateTransformIfActive();
            ApplyTransformPatchToTileInternal(tile, patch);
        }

        [Server]
        public void ApplyTransformPatchToSelectedTiles(TileTransformPatchRecord patch)
        {
            if (hostGameMode != HostGameMode.Create || SelectedTiles.Count == 0 || !patch.HasAnyField)
                return;

            CaptureUndoSnapshot();
            AbortCreateTransformIfActive();

            for (int i = 0; i < SelectedTiles.Count; i++)
            {
                LevelTileServer tile = FindTileByNetId(SelectedTiles[i]);
                if (tile == null)
                    continue;

                ApplyTransformPatchToTileInternal(tile, patch);
            }
        }

        [Server]
        void ApplyTransformPatchToTileInternal(LevelTileServer tile, TileTransformPatchRecord patch)
        {
            if (tile == null || !patch.HasAnyField || tile.IsAttachedToScriptPivot)
                return;

            patch.MergeOnto(
                tile.Position,
                tile.PlacementRotationDegrees,
                tile.Scale,
                tile.SortingOrder,
                out Vector2 position,
                out float rotation,
                out Vector2 scale,
                out int sortingOrder);

            tile.ServerSetTransform(position, rotation, scale, roundValues: false);
            if (patch.hasSortingOrder)
                tile.ServerSetSortingOrder(sortingOrder);
        }

        [Server]
        bool TryGetRunnerAt(Vector2 worldPosition, out NetworkPlayer runnerPlayer)
        {
            runnerPlayer = null;
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition, runnerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                NetworkPlayer networkPlayer = hit.GetComponentInParent<NetworkPlayer>();
                if (networkPlayer == null || networkPlayer.role != PlayerRole.Runner)
                    continue;

                if (networkPlayer.GetComponentInParent<RunnerMovementServer>() == null)
                    continue;

                runnerPlayer = networkPlayer;
                return true;
            }

            return false;
        }

        [Server]
        bool TryBeginGrab(Vector2 worldPosition)
        {
            if (grabbedRunner != null)
                return false;

            if (!TryGetRunnerAt(worldPosition, out NetworkPlayer networkPlayer))
                return false;

            RunnerMovementServer runner = networkPlayer.GetComponentInParent<RunnerMovementServer>();
            if (runner == null)
                return false;

            grabbedRunner = runner;
            grabOffset = (Vector2)runner.transform.position - worldPosition;
            releaseThrowVelocity = Vector2.zero;
            runner.SetGrabbed(true);
            return true;
        }

        [Server]
        void TryPlaceTileOnGrid(LevelGridServer grid, TileType tileType, Vector2Int cell, float rotationDegrees, Color32 color, byte spikeQuarterTurns = 0)
        {
            int editorLayer = GetPlacementEditorLayer();
            if (grid.HasTileAtCellAndLayer(cell, editorLayer))
                return;

            Vector2 position = GridMath.TileCellToWorld(cell, tileType, spikeQuarterTurns);
            TryPlaceTile(tileType, position, rotationDegrees, color, spikeQuarterTurns);
        }

        [Server]
        void TryPlaceTile(TileType tileType, Vector2 position, float rotationDegrees, Color32 color, byte spikeQuarterTurns = 0)
        {
            CaptureUndoSnapshot();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                return;

            uint tileNetId = grid.TryPlaceTile(tileType, position, rotationDegrees, color, editorLayer: GetPlacementEditorLayer());

            if (tileNetId != 0 && connectionToClient != null)
                TargetConfirmTileSpawned(connectionToClient, tileNetId);
        }

        [TargetRpc]
        void TargetConfirmTileSpawned(NetworkConnectionToClient conn, uint tileNetId)
        {
            StartCoroutine(ConfirmTileSpawnedLocally(tileNetId));
        }

        static IEnumerator ConfirmTileSpawnedLocally(uint tileNetId)
        {
            for (int i = 0; i < 120; i++)
            {
                if (NetworkClient.spawned.TryGetValue(tileNetId, out NetworkIdentity identity) &&
                    identity.TryGetComponent(out LevelTileServer tile))
                {
                    tile.RefreshClientVisuals();
                    yield break;
                }

                yield return null;
            }

            if (NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorClient cursorClient))
            {
                cursorClient.CmdRequestLevelObserverResync();
            }
        }

        [Server]
        void TryDeleteTile(Vector2 worldPoint)
        {
            CaptureUndoSnapshot();
            if (!TryGetTopTileAtPoint(worldPoint, out LevelTileServer tile))
                return;
            LevelGridServer.Instance?.TryRemoveTileByNetId(tile.netId);
        }

        [Server]
        void DragGrabbedRunner(Vector2 worldPosition)
        {
            if (grabbedRunner == null)
                return;

            grabbedRunner.ServerSetPosition(worldPosition + grabOffset);
            releaseThrowVelocity = smoothedCursorVelocity;
        }

        [Server]
        void ReleaseGrab(bool applyThrow)
        {
            if (grabbedRunner == null)
                return;

            Vector2 throwVelocity = Vector2.zero;
            if (applyThrow)
            {
                throwVelocity = releaseThrowVelocity * throwVelocityMultiplier;
                float speed = throwVelocity.magnitude;
                if (speed < minThrowSpeed)
                    throwVelocity = Vector2.zero;
                else if (speed > maxThrowSpeed)
                    throwVelocity = throwVelocity.normalized * maxThrowSpeed;
            }

            grabbedRunner.SetGrabbed(false);
            if (throwVelocity != Vector2.zero)
                grabbedRunner.ServerApplyThrowVelocity(throwVelocity);

            grabbedRunner = null;
            releaseThrowVelocity = Vector2.zero;
        }

        [Server]
        public void ReleaseGrabbedRunner(RunnerMovementServer runner)
        {
            if (grabbedRunner != runner)
                return;

            grabbedRunner = null;
            releaseThrowVelocity = Vector2.zero;
        }

        [Server]
        void UpdateCursorVelocity(Vector2 worldPosition)
        {
            if (!hasPreviousCursorPosition)
            {
                previousCursorWorldPosition = worldPosition;
                currentCursorVelocity = Vector2.zero;
                hasPreviousCursorPosition = true;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            currentCursorVelocity = (worldPosition - previousCursorWorldPosition) / dt;
            float blend = 1f - Mathf.Exp(-throwVelocitySmoothing * dt);
            smoothedCursorVelocity = Vector2.Lerp(smoothedCursorVelocity, currentCursorVelocity, blend);
            previousCursorWorldPosition = worldPosition;
        }

        void OnSelectedTilesChanged(SyncList<uint>.Operation op, int itemIndex, uint oldItem, uint newItem)
        {
            SelectionChanged?.Invoke();
        }

        void OnHostGameModeChanged(HostGameMode oldMode, HostGameMode newMode)
        {
            HostGameModeChanged?.Invoke(newMode);
            ApplyVisualState();

            if (!isClient)
                return;

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].RefreshClientAppearance();
        }

        void OnSelectionMarqueeChanged(Vector2 _, Vector2 __) { }
        void OnSelectionMarqueeChanged(bool _, bool __) { }
        int GetPlacementEditorLayer() => syncedActiveEditorLayer < 0 ? 0 : syncedActiveEditorLayer;

        void OnActiveEditorLayerChanged(int _, int __)
        {
            ActiveEditorLayerChanged?.Invoke();
            if (!isClient)
                return;

            LevelTileServer[] tiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].RefreshClientAppearance();
        }

        void OnActiveCreateTransformChanged(CreateTransformMode _, CreateTransformMode __) { }
        void OnCursorPositionChanged(Vector2 oldPosition, Vector2 newPosition) => ApplyVisualState();
        void OnCursorPressedChanged(bool oldPressed, bool newPressed) => ApplyVisualState();
        void OnToolModeChanged(KaplaoToolMode oldMode, KaplaoToolMode newMode) => ApplyVisualState();
        void OnPlaceColorChanged(Color32 oldColor, Color32 newColor) => ApplyVisualState();
        void OnSpikeQuarterTurnsChanged(byte oldTurns, byte newTurns) => ApplyVisualState();

        void OnHideCursorChanged(bool _, bool __) => ApplyVisualState();

        void ApplyVisualState()
        {
            if (syncedHideCursor || (isLocalPlayer && PauseCursorState.IsPauseOpen))
            {
                HideGameplayCursorVisuals();
                return;
            }

            ApplyGrabCursorVisual();
            ApplyPalmCursorVisual();
            ApplyCrosshairVisual();
            ApplyTilePreviewVisual();
        }

        void HideGameplayCursorVisuals()
        {
            if (grabCursorRenderer != null)
                grabCursorRenderer.enabled = false;

            DisablePalmCursorPhysics();

            if (crosshairRenderer != null)
                crosshairRenderer.enabled = false;

            if (tilePreviewRenderer != null)
                tilePreviewRenderer.enabled = false;
        }

        void ApplyClientPalmPhysics()
        {
            if (syncedHideCursor ||
                hostGameMode != HostGameMode.Usual ||
                cursorToolMode != KaplaoToolMode.Palm)
            {
                DisablePalmCursorPhysics();
                return;
            }

            if (palmCursorInstance == null || !palmCursorInstance.gameObject.activeSelf || palmBody == null)
                return;

            palmBody.MovePosition(GetVisualWorldPosition());
        }

        static void StripNetworkComponents(GameObject root)
        {
            NetworkBehaviour[] behaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = behaviours.Length - 1; i >= 0; i--)
                Destroy(behaviours[i]);

            NetworkIdentity[] identities = root.GetComponentsInChildren<NetworkIdentity>(true);
            for (int i = identities.Length - 1; i >= 0; i--)
                Destroy(identities[i]);
        }

        void EnsureShootAudio()
        {
            if (shootAudioSource != null)
                return;

            shootAudioSource = GetComponent<AudioSource>();
            if (shootAudioSource == null)
                shootAudioSource = gameObject.AddComponent<AudioSource>();

            shootAudioSource.playOnAwake = false;
            shootAudioSource.spatialBlend = 0f;
        }

        void EnsurePalmCursorVisual()
        {
            if (palmCursorPrefab == null)
                return;

            if (palmCursorInstance == null)
            {
                GameObject instance = Instantiate(palmCursorPrefab);
                instance.name = "PalmCursor";
                palmCursorInstance = instance.transform;
                StripNetworkComponents(instance);

                foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true))
                    collider.enabled = true;

                palmBody = instance.GetComponent<Rigidbody2D>();
                if (palmBody == null)
                    palmBody = instance.AddComponent<Rigidbody2D>();

                palmBody.bodyType = RigidbodyType2D.Kinematic;
                palmBody.gravityScale = 0f;
                palmBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                palmBody.useFullKinematicContacts = true;
                palmBody.interpolation = RigidbodyInterpolation2D.Interpolate;

                palmRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < palmRenderers.Length; i++)
                    palmRenderers[i].sortingOrder = palmCursorSortingOrder;
            }
        }

        void EnsureCrosshairVisual()
        {
            if (crosshairTransform == null)
            {
                Transform existing = transform.Find("Crosshair");
                crosshairTransform = existing != null ? existing : CreateVisualChild("Crosshair").transform;
            }

            if (crosshairTransform.parent != null)
                crosshairTransform.SetParent(null, true);

            if (crosshairRenderer == null)
                crosshairRenderer = crosshairTransform.GetComponent<SpriteRenderer>();

            if (crosshairRenderer == null)
                crosshairRenderer = crosshairTransform.gameObject.AddComponent<SpriteRenderer>();

            crosshairRenderer.sortingOrder = crosshairSortingOrder;
        }

        void ApplyPalmCursorVisual()
        {
            bool showPalm = hostGameMode == HostGameMode.Usual && cursorToolMode == KaplaoToolMode.Palm;

            if (!showPalm || palmCursorPrefab == null)
            {
                DisablePalmCursorPhysics();
                return;
            }

            EnsurePalmCursorVisual();
            if (palmCursorInstance == null)
                return;

            palmCursorInstance.gameObject.SetActive(true);
            foreach (Collider2D collider in palmCursorInstance.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = true;

            if (palmBody != null)
            {
                palmBody.simulated = true;
                palmBody.position = GetVisualWorldPosition();
            }
        }

        void DisablePalmCursorPhysics()
        {
            if (palmCursorInstance != null)
            {
                foreach (Collider2D collider in palmCursorInstance.GetComponentsInChildren<Collider2D>(true))
                    collider.enabled = false;

                palmCursorInstance.gameObject.SetActive(false);
            }

            if (palmBody != null)
                palmBody.simulated = false;
        }

        void ApplyCrosshairVisual()
        {
            EnsureCrosshairVisual();
            if (crosshairTransform == null || crosshairRenderer == null)
                return;

            bool showCrosshair = hostGameMode == HostGameMode.Usual && cursorToolMode == KaplaoToolMode.Gun;
            if (!showCrosshair)
            {
                crosshairRenderer.enabled = false;
                return;
            }

            Vector2 visualPosition = GetVisualWorldPosition();
            crosshairTransform.position = new Vector3(visualPosition.x, visualPosition.y, 0f);
            crosshairTransform.localScale = Vector3.one * crosshairScale;
            crosshairRenderer.sprite = crosshairSprite;
            crosshairRenderer.color = Color.white;
            crosshairRenderer.enabled = crosshairSprite != null;
        }

        void ApplyGrabCursorVisual()
        {
            if (grabCursorTransform == null)
                return;

            if (hostGameMode == HostGameMode.Create)
            {
                if (grabCursorRenderer != null)
                    grabCursorRenderer.enabled = false;
                return;
            }

            Vector2 visualPosition = GetVisualWorldPosition();
            grabCursorTransform.position = new Vector3(visualPosition.x, visualPosition.y, 0f);

            if (grabCursorRenderer == null)
                return;

            bool showGrabCursor = hostGameMode != HostGameMode.Code &&
                                  (cursorToolMode == KaplaoToolMode.Select ||
                                   cursorToolMode == KaplaoToolMode.Block ||
                                   cursorToolMode == KaplaoToolMode.Spike ||
                                   cursorToolMode == KaplaoToolMode.Trigger ||
                                   cursorToolMode == KaplaoToolMode.Particle ||
                                   (hostGameMode == HostGameMode.Usual && cursorToolMode == KaplaoToolMode.Grab));

            if (!showGrabCursor)
            {
                grabCursorRenderer.enabled = false;
                return;
            }

            grabCursorRenderer.color = Color.white;
            grabCursorRenderer.sprite = cursorPressed ? pressedSprite : releasedSprite;
            grabCursorTransform.localScale = Vector3.one * grabCursorScale;
            grabCursorRenderer.enabled = grabCursorRenderer.sprite != null;
        }

        Vector2 GetSnappedPreviewPosition()
        {
            Vector2 raw = GetVisualWorldPosition();
            Vector2Int cell = GridMath.WorldToCell(raw);

            return cursorToolMode switch
            {
                KaplaoToolMode.Spike => (Vector2)GridMath.TileCellToWorld(cell, TileType.Spike, syncedSpikeQuarterTurns),
                KaplaoToolMode.Block => (Vector2)GridMath.CellToWorld(cell),
                KaplaoToolMode.Trigger => (Vector2)GridMath.CellToWorld(cell),
                KaplaoToolMode.Particle => (Vector2)GridMath.CellToWorld(cell),
                KaplaoToolMode.Keyframe => (Vector2)GridMath.CellToWorld(cell),
                KaplaoToolMode.Executor => (Vector2)GridMath.CellToWorld(cell),
                _ => raw
            };
        }

        void ApplyTilePreviewVisual()
        {
            if (tilePreviewTransform == null || tilePreviewRenderer == null)
                return;

            bool showTilePreview = hostGameMode != HostGameMode.Code &&
                                   (cursorToolMode == KaplaoToolMode.Block ||
                                    cursorToolMode == KaplaoToolMode.Spike ||
                                    cursorToolMode == KaplaoToolMode.Trigger ||
                                   cursorToolMode == KaplaoToolMode.Particle ||
                                   cursorToolMode == KaplaoToolMode.Keyframe ||
                                   cursorToolMode == KaplaoToolMode.Executor ||
                                   cursorToolMode == KaplaoToolMode.Delete);

            if (!showTilePreview)
            {
                tilePreviewRenderer.enabled = false;
                return;
            }

            Vector2 visualPosition = GetSnappedPreviewPosition();
            tilePreviewTransform.position = new Vector3(visualPosition.x, visualPosition.y, 0f);
            tilePreviewRenderer.color = Color.white;

            switch (cursorToolMode)
            {
                case KaplaoToolMode.Block:
                    tilePreviewRenderer.sprite = blockCursorSprite;
                    tilePreviewRenderer.color = syncedPlaceColor;
                    tilePreviewTransform.localScale = Vector3.one * tileCursorScale;
                    tilePreviewTransform.rotation = Quaternion.identity;
                    tilePreviewRenderer.enabled = blockCursorSprite != null;
                    break;

                case KaplaoToolMode.Spike:
                    tilePreviewRenderer.sprite = spikeCursorSprite;
                    tilePreviewRenderer.color = syncedPlaceColor;
                    tilePreviewTransform.localScale = Vector3.one * tileCursorScale;
                    tilePreviewTransform.rotation = Quaternion.Euler(0f, 0f, -syncedSpikeQuarterTurns * 90f);
                    tilePreviewRenderer.enabled = spikeCursorSprite != null;
                    break;

                case KaplaoToolMode.Trigger:
                    tilePreviewRenderer.sprite = triggerCursorSprite;
                    tilePreviewRenderer.color = syncedPlaceColor;
                    tilePreviewTransform.localScale = Vector3.one * tileCursorScale;
                    tilePreviewTransform.rotation = Quaternion.identity;
                    tilePreviewRenderer.enabled = triggerCursorSprite != null;
                    break;

                case KaplaoToolMode.Particle:
                    tilePreviewRenderer.sprite = particleCursorSprite;
                    tilePreviewRenderer.color = syncedPlaceColor;
                    tilePreviewTransform.localScale = Vector3.one * tileCursorScale;
                    tilePreviewTransform.rotation = Quaternion.identity;
                    tilePreviewRenderer.enabled = particleCursorSprite != null;
                    break;

                case KaplaoToolMode.Keyframe:
                    tilePreviewRenderer.sprite = triggerCursorSprite;
                    tilePreviewRenderer.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                    tilePreviewTransform.localScale = Vector3.one * tileCursorScale;
                    tilePreviewTransform.rotation = Quaternion.identity;
                    tilePreviewRenderer.enabled = triggerCursorSprite != null;
                    break;

                case KaplaoToolMode.Executor:
                    tilePreviewRenderer.sprite = executorCursorSprite != null ? executorCursorSprite : triggerCursorSprite;
                    tilePreviewRenderer.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                    tilePreviewTransform.localScale = Vector3.one * tileCursorScale;
                    tilePreviewTransform.rotation = Quaternion.identity;
                    tilePreviewRenderer.enabled = tilePreviewRenderer.sprite != null;
                    break;

                case KaplaoToolMode.Delete:
                    tilePreviewRenderer.sprite = deleteCursorSprite != null ? deleteCursorSprite : pressedSprite;
                    tilePreviewRenderer.color = deleteCursorSprite != null ? Color.white : deleteCursorFallbackColor;
                    tilePreviewTransform.localScale = Vector3.one * grabCursorScale;
                    tilePreviewTransform.rotation = Quaternion.identity;
                    tilePreviewRenderer.enabled = tilePreviewRenderer.sprite != null;
                    break;
            }
        }

        void ResolveCursorSprites()
        {
            TryGetPrefabVisual(blockTilePrefab, out blockCursorSprite, out blockCursorColor);
            TryGetPrefabVisual(spikeTilePrefab, out spikeCursorSprite, out spikeCursorColor);
            TryGetPrefabVisual(triggerTilePrefab, out triggerCursorSprite, out triggerCursorColor);
            TryGetPrefabVisual(particleTilePrefab, out particleCursorSprite, out particleCursorColor);

            if (blockCursorSprite == null && LevelGridServer.Instance != null)
                TryGetPrefabVisual(LevelGridServer.Instance.BlockPrefab, out blockCursorSprite, out blockCursorColor);

            if (spikeCursorSprite == null && LevelGridServer.Instance != null)
                TryGetPrefabVisual(LevelGridServer.Instance.SpikePrefab, out spikeCursorSprite, out spikeCursorColor);

            if (triggerCursorSprite == null && LevelGridServer.Instance != null)
                TryGetPrefabVisual(LevelGridServer.Instance.TriggerPrefab, out triggerCursorSprite, out triggerCursorColor);

            if (particleCursorSprite == null && LevelGridServer.Instance != null)
                TryGetPrefabVisual(LevelGridServer.Instance.ParticlePrefab, out particleCursorSprite, out particleCursorColor);

            Color executorCursorColor;
            TryGetPrefabVisual(executorTilePrefab, out executorCursorSprite, out executorCursorColor);
            if (executorCursorSprite == null && LevelGridServer.Instance != null)
                TryGetPrefabVisual(LevelGridServer.Instance.ExecutorPrefab, out executorCursorSprite, out executorCursorColor);
        }

        static bool TryGetPrefabVisual(GameObject prefab, out Sprite sprite, out Color color)
        {
            sprite = null;
            color = Color.white;

            if (prefab == null)
                return false;

            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>() ??
                                      prefab.GetComponentInChildren<SpriteRenderer>();

            if (renderer == null)
                return false;

            sprite = renderer.sprite;
            color = renderer.color;
            return sprite != null;
        }

        void EnsureCursorVisual()
        {
            EnsureGrabCursorVisual();
            EnsureTilePreviewVisual();
        }

        void EnsureGrabCursorVisual()
        {
            if (grabCursorTransform == null)
            {
                Transform existing = transform.Find("Cursor");
                grabCursorTransform = existing != null ? existing : CreateVisualChild("GrabCursor").transform;
            }

            if (grabCursorTransform.parent != null)
                grabCursorTransform.SetParent(null, true);

            if (grabCursorRenderer == null)
                grabCursorRenderer = grabCursorTransform.GetComponent<SpriteRenderer>();

            if (grabCursorRenderer == null)
                grabCursorRenderer = grabCursorTransform.gameObject.AddComponent<SpriteRenderer>();

            grabCursorRenderer.sortingOrder = grabCursorSortingOrder;
        }

        void EnsureTilePreviewVisual()
        {
            if (tilePreviewTransform == null)
            {
                Transform existing = transform.Find("TilePreview");
                tilePreviewTransform = existing != null ? existing : CreateVisualChild("TilePreview").transform;
            }

            if (tilePreviewTransform.parent != null)
                tilePreviewTransform.SetParent(null, true);

            if (tilePreviewRenderer == null)
                tilePreviewRenderer = tilePreviewTransform.GetComponent<SpriteRenderer>();

            if (tilePreviewRenderer == null)
                tilePreviewRenderer = tilePreviewTransform.gameObject.AddComponent<SpriteRenderer>();

            tilePreviewRenderer.sortingOrder = tilePreviewSortingOrder;
        }

        GameObject CreateVisualChild(string objectName)
        {
            return new GameObject(objectName);
        }

        void OnDisable()
        {
            if (!isServer)
                return;

            ReleaseGrab(false);
        }
    }
}
