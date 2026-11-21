using System.Collections;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Loads the selected world when the game scene starts on the server.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldLoadBootstrap : MonoBehaviour
    {
        [SerializeField] bool loadOnStart = true;

        void Start()
        {
            KaplaoNicknamePresetCatalog.RequestRefresh();

            if (!loadOnStart || !NetworkServer.active)
                return;

            StartCoroutine(LoadWhenReady());
        }

        IEnumerator LoadWhenReady()
        {
            if (!WorldSession.HasActiveWorld)
                yield break;

            float timeout = 5f;
            while ((LevelGridServer.Instance == null || LevelSessionServer.Instance == null) && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
            {
                Debug.LogError("[WorldLoadBootstrap] LevelGridServer not found.");
                yield break;
            }

            if (LevelSessionServer.Instance == null)
                Debug.LogWarning("[WorldLoadBootstrap] LevelSessionServer not found; script state may not load.");

            if (WorldSession.IsCreatingNewWorld)
            {
                if (!WorldSaveService.WorldExists(WorldSession.ActiveWorldName) &&
                    !WorldSaveService.TryCreateWorld(WorldSession.ActiveWorldName, out string createError))
                {
                    Debug.LogError($"[WorldLoadBootstrap] Failed to create world: {createError}");
                    yield break;
                }

                WorldCustomTextureService.ClearRuntime();
                WorldScriptProjectService.ClearRuntime();
                grid.ClearAllTiles();
                if (!WorldScriptProjectService.TryCreateDefaultProject(
                        WorldSession.ActiveWorldName,
                        LevelSessionServer.DefaultLevelScript,
                        out string scriptCreateError))
                {
                    Debug.LogError($"[WorldLoadBootstrap] Failed to create Scripts project: {scriptCreateError}");
                }

                string entryScript = WorldScriptProjectService.GetEntryScriptContent();
                LevelSessionServer.Instance?.ServerSetLevelScript(entryScript);
                LevelSessionServer.Instance?.ServerBroadcastScriptProject();
                KaplaoCursorServer.ServerResetAllKaplaoPlaceColors();
                Debug.Log($"[WorldLoadBootstrap] Created new world '{WorldSession.ActiveWorldName}'.");
                if (!WorldSession.IsBrowsePlaySession && WorldSession.HasActiveWorld)
                    WorldSaveService.RecordWorldVisited(WorldSession.ActiveWorldName);
                TryLogSinglePlayerWorldEnter();
                yield break;
            }

            if (!WorldSaveService.TryLoadWorld(WorldSession.ActiveWorldName, out LevelData data, out string loadError))
            {
                Debug.LogError($"[WorldLoadBootstrap] Failed to load world '{WorldSession.ActiveWorldName}': {loadError}");
                yield break;
            }

            int customTextures = WorldCustomTextureService.LoadActiveWorldIntoRegistry();
            if (!WorldScriptProjectService.TryEnsureProjectForWorld(
                    WorldSession.ActiveWorldName,
                    data.script,
                    out string scriptLoadError))
            {
                Debug.LogWarning($"[WorldLoadBootstrap] Scripts project: {scriptLoadError}");
            }

            // Prefer Scripts/main.ks; fall back to legacy level.json.script already migrated above.
            data.script = WorldScriptProjectService.GetEntryScriptContent();
            grid.ImportLevelData(data);
            yield return null;
            LevelSessionServer.Instance?.ServerNotifyLevelScriptLoaded();
            LevelSessionServer.Instance?.ServerBroadcastScriptProject();
            KaplaoCursorServer.ServerResetAllKaplaoPlaceColors();
            Debug.Log($"[WorldLoadBootstrap] Loaded world '{WorldSession.ActiveWorldName}'. tiles={(data.tiles != null ? data.tiles.Length : 0)} customTextures={customTextures}.");
            if (!WorldSession.IsBrowsePlaySession && WorldSession.HasActiveWorld)
                WorldSaveService.RecordWorldVisited(WorldSession.ActiveWorldName);
            TryLogSinglePlayerWorldEnter();

            if (WorldSession.IsBrowsePlaySession)
                yield return TryAutoStartBrowsePlayScript();
        }

        IEnumerator TryAutoStartBrowsePlayScript()
        {
            // Let imported tiles settle before script queries them.
            yield return null;

            float timeout = 5f;
            while (LevelScriptRuntime.Instance == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == null || LevelScriptRuntime.Instance == null)
            {
                Debug.LogWarning("[WorldLoadBootstrap] Browse play: cannot auto-start script — runtime not ready.");
                yield break;
            }

            string script = session.LevelScript ?? string.Empty;
            if (session.ServerTryStartScript(script, out string error))
                Debug.Log("[WorldLoadBootstrap] Browse play: level script started automatically.");
            else
                Debug.LogWarning($"[WorldLoadBootstrap] Browse play: script not started ({error}).");
        }

        static void TryLogSinglePlayerWorldEnter()
        {
            if (WorldSession.IsBrowsePlaySession)
                return;

            KaplaoNetworkManager manager = KaplaoNetworkManager.Instance;
            if (manager == null || !manager.IsSinglePlayerHostSession)
                return;

            KaplaoPlayerActionLogClient.Log(
                "singleplayer_world_enter",
                WorldSession.ActiveWorldName,
                "{\"source\":\"WorldLoadBootstrap\"}");
        }
    }
}
