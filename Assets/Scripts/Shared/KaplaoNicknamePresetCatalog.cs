using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Downloads nickname preset styles from KaplaoGamesWebsite and caches them for nameplate rendering.
    /// </summary>
    public static class KaplaoNicknamePresetCatalog
    {
        const string CachePrefsKey = "kaplao.nicknamePresets.cache.v1";
        const float MinRefreshSeconds = 15f;

        static readonly Dictionary<string, KaplaoNicknameStyleDto> ByName =
            new Dictionary<string, KaplaoNicknameStyleDto>(StringComparer.OrdinalIgnoreCase);

        static float lastFetchUnscaled = -999f;
        static bool fetchInFlight;
        static MonoBehaviour runnerHost;

        public static event Action Changed;

        public static bool IsNonePreset(string presetName)
        {
            return string.IsNullOrWhiteSpace(presetName) ||
                   string.Equals(presetName.Trim(), "none", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(presetName.Trim(), "own", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetStyle(string presetName, out KaplaoNicknameStyleDto style)
        {
            style = null;
            if (IsNonePreset(presetName))
                return false;

            EnsureLoadedFromCache();
            return ByName.TryGetValue(presetName.Trim(), out style) && style != null;
        }

        public static void RequestRefresh(bool force = false)
        {
            if (!force && Time.unscaledTime - lastFetchUnscaled < MinRefreshSeconds)
                return;

            EnsureRunner();
            if (runnerHost == null)
                return;

            if (fetchInFlight && !force)
                return;

            runnerHost.StartCoroutine(FetchRoutine(force));
        }

        public static IEnumerator RefreshCoroutine(bool force = true)
        {
            yield return FetchRoutine(force);
        }

        static void EnsureRunner()
        {
            if (runnerHost != null)
                return;

            GameObject go = GameObject.Find("KaplaoNicknamePresetCatalogRunner");
            if (go == null)
            {
                go = new GameObject("KaplaoNicknamePresetCatalogRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            runnerHost = go.GetComponent<KaplaoNicknamePresetCatalogRunner>();
            if (runnerHost == null)
                runnerHost = go.AddComponent<KaplaoNicknamePresetCatalogRunner>();
        }

        static IEnumerator FetchRoutine(bool force)
        {
            if (fetchInFlight && !force)
                yield break;

            fetchInFlight = true;
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    EnsureLoadedFromCache();
                    yield break;
                }

                string secret = ResolveGameServerSecret();
                if (string.IsNullOrWhiteSpace(secret))
                {
                    EnsureLoadedFromCache();
                    yield break;
                }

                string url = ResolveWebsiteUrl() + "/api/game/nickname-presets";
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.SetRequestHeader("X-Kaplao-Game-Secret", secret);
                    request.timeout = 12;
                    yield return request.SendWebRequest();

                    string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning(
                            $"[KaplaoNicknamePresetCatalog] Fetch failed: {request.error} ({request.responseCode})");
                        EnsureLoadedFromCache();
                        yield break;
                    }

                    ApplyCatalogJson(body, persist: true);
                    lastFetchUnscaled = Time.unscaledTime;
                }
            }
            finally
            {
                fetchInFlight = false;
            }
        }

        public static void ApplyCatalogJson(string json, bool persist)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            KaplaoNicknamePresetCatalogDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<KaplaoNicknamePresetCatalogDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KaplaoNicknamePresetCatalog] Parse failed: {ex.Message}");
                return;
            }

            ByName.Clear();
            if (dto?.Presets != null)
            {
                for (int i = 0; i < dto.Presets.Count; i++)
                {
                    KaplaoNicknamePresetDto preset = dto.Presets[i];
                    if (preset == null || string.IsNullOrWhiteSpace(preset.Name) || preset.Style == null)
                        continue;

                    string key = preset.Name.Trim().ToLowerInvariant();
                    if (key == "none" || key == "own")
                        continue;

                    ByName[key] = preset.Style;
                }
            }

            if (persist)
            {
                try
                {
                    PlayerPrefs.SetString(CachePrefsKey, json);
                    PlayerPrefs.Save();
                }
                catch
                {
                    // ignore prefs failures
                }
            }

            Changed?.Invoke();
        }

        static void EnsureLoadedFromCache()
        {
            if (ByName.Count > 0)
                return;

            string cached = PlayerPrefs.GetString(CachePrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(cached))
                ApplyCatalogJson(cached, persist: false);
        }

        static string ResolveWebsiteUrl()
        {
            PublicLevelCatalogConfig catalog = PublicLevelCatalogConfig.Load();
            if (catalog != null && !string.IsNullOrWhiteSpace(catalog.WebsiteUrl) &&
                !catalog.WebsiteUrl.Contains("localhost"))
            {
                return catalog.WebsiteUrl.TrimEnd('/');
            }

            if (KaplaoAccountConfig.Instance != null)
            {
                string fromConfig = KaplaoAccountConfig.Instance.WebsiteUrl;
                if (!string.IsNullOrWhiteSpace(fromConfig) && !fromConfig.Contains("localhost"))
                    return fromConfig;
            }

            if (catalog != null && !string.IsNullOrWhiteSpace(catalog.WebsiteUrl))
                return catalog.WebsiteUrl.TrimEnd('/');

            return "http://localhost:3000";
        }

        static string ResolveGameServerSecret()
        {
            PublicLevelCatalogConfig catalog = PublicLevelCatalogConfig.Load();
            if (catalog != null && !string.IsNullOrWhiteSpace(catalog.GameServerSecret))
                return catalog.GameServerSecret;

            if (KaplaoAccountConfig.Instance != null)
                return KaplaoAccountConfig.Instance.GameServerSecret;

            return string.Empty;
        }
    }

    sealed class KaplaoNicknamePresetCatalogRunner : MonoBehaviour
    {
    }
}
