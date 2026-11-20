using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Fire-and-forget player action logs to KaplaoGamesWebsite (Timeweb / Supabase backend).
    /// Old game builds without this class simply skip logging.
    /// </summary>
    public static class KaplaoPlayerActionLogClient
    {
        public static void Log(
            string action,
            string levelTitle = null,
            string metadataJson = null)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            MonoBehaviour host = ResolveCoroutineHost();
            if (host == null)
                return;

            host.StartCoroutine(SendLogCoroutine(action.Trim(), levelTitle, metadataJson));
        }

        static MonoBehaviour ResolveCoroutineHost()
        {
            if (KaplaoGames.Network.KaplaoNetworkManager.Instance != null)
                return KaplaoGames.Network.KaplaoNetworkManager.Instance;

            return UnityEngine.Object.FindFirstObjectByType<KaplaoAccountClient>();
        }

        static IEnumerator SendLogCoroutine(string action, string levelTitle, string metadataJson)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                yield break;

            string secret = ResolveGameServerSecret();
            if (string.IsNullOrWhiteSpace(secret))
                yield break;

            string url = ResolveLogUrl();
            string nickname = PlayerSession.Nickname ?? string.Empty;
            string email = PlayerSession.IsLoggedIn ? PlayerSession.AccountEmail ?? string.Empty : string.Empty;
            string body = BuildJsonBody(action, nickname, email, levelTitle, metadataJson);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Kaplao-Game-Secret", secret);
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[KaplaoPlayerActionLog] Failed ({action}): {request.error} ({request.responseCode})");
            }
        }

        static string BuildJsonBody(
            string action,
            string nickname,
            string email,
            string levelTitle,
            string metadataJson)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"action\":\"").Append(EscapeJson(action)).Append('"');
            sb.Append(",\"nickname\":\"").Append(EscapeJson(nickname)).Append('"');
            sb.Append(",\"email\":\"").Append(EscapeJson(email)).Append('"');

            if (!string.IsNullOrWhiteSpace(levelTitle))
                sb.Append(",\"levelTitle\":\"").Append(EscapeJson(levelTitle.Trim())).Append('"');

            if (!string.IsNullOrWhiteSpace(metadataJson))
                sb.Append(",\"metadata\":").Append(metadataJson.Trim());

            sb.Append('}');
            return sb.ToString();
        }

        static string ResolveLogUrl()
        {
            // Always use the Next.js website route (Supabase edge has verify-player only).
            return ResolveWebsiteUrl() + "/api/game/player-log";
        }

        static string ResolveWebsiteUrl()
        {
            PublicLevelCatalogConfig catalog = PublicLevelCatalogConfig.Load();
            if (catalog != null && !string.IsNullOrWhiteSpace(catalog.WebsiteUrl))
                return catalog.WebsiteUrl.TrimEnd('/');

            if (KaplaoAccountConfig.Instance != null &&
                !string.IsNullOrWhiteSpace(KaplaoAccountConfig.Instance.WebsiteUrl))
            {
                return KaplaoAccountConfig.Instance.WebsiteUrl.TrimEnd('/');
            }

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

        static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
