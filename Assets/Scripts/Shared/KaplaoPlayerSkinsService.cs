using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Shared
{
    public struct PlayerSkinsFetchResult
    {
        public bool Success;
        public string[] SkinIds;
    }

    /// <summary>
    /// Fetches admin-granted runner skins for the signed-in account (Settings UI).
    /// </summary>
    public static class KaplaoPlayerSkinsService
    {
        public static IEnumerator FetchOwnedSkinsCoroutine(Action<PlayerSkinsFetchResult> onComplete)
        {
            var result = new PlayerSkinsFetchResult
            {
                Success = false,
                SkinIds = Array.Empty<string>(),
            };

            if (onComplete == null)
                yield break;

            if (!PlayerSession.IsLoggedIn || string.IsNullOrWhiteSpace(PlayerSession.AuthToken))
            {
                onComplete(result);
                yield break;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                onComplete(result);
                yield break;
            }

            string url = ResolvePlayerSkinsUrl();
            string bodyJson = "{\"token\":\"" + EscapeJsonString(PlayerSession.AuthToken.Trim()) + "\"}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 12;
                yield return request.SendWebRequest();

                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"[KaplaoPlayerSkins] HTTP failed: {request.error} ({request.responseCode}) body={Truncate(responseBody, 200)}");
                    onComplete(result);
                    yield break;
                }

                if (!ExtractJsonBool(responseBody, "ok"))
                {
                    onComplete(result);
                    yield break;
                }

                result.Success = true;
                result.SkinIds = ExtractJsonStringArray(responseBody, "skinIds");
                onComplete(result);
            }
        }

        static string ResolvePlayerSkinsUrl()
        {
            PublicLevelCatalogConfig catalog = PublicLevelCatalogConfig.Load();
            if (catalog != null && !string.IsNullOrWhiteSpace(catalog.WebsiteUrl))
                return catalog.WebsiteUrl.TrimEnd('/') + "/api/game/player-skins";

            if (KaplaoAccountConfig.Instance != null &&
                !string.IsNullOrWhiteSpace(KaplaoAccountConfig.Instance.WebsiteUrl))
            {
                return KaplaoAccountConfig.Instance.WebsiteUrl.TrimEnd('/') + "/api/game/player-skins";
            }

            return "http://localhost:3000/api/game/player-skins";
        }

        static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value ?? string.Empty;
            return value.Substring(0, max) + "…";
        }

        static bool ExtractJsonBool(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return false;

            var match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            return string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        static string[] ExtractJsonStringArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return Array.Empty<string>();

            var arrayMatch = Regex.Match(
                json,
                $"\"{Regex.Escape(key)}\"\\s*:\\s*\\[(.*?)\\]",
                RegexOptions.Singleline);
            if (!arrayMatch.Success)
                return Array.Empty<string>();

            var values = new List<string>();
            var itemMatches = Regex.Matches(
                arrayMatch.Groups[1].Value,
                "\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.CultureInvariant);
            for (int i = 0; i < itemMatches.Count; i++)
            {
                string value = itemMatches[i].Groups[1].Value
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Trim()
                    .ToLowerInvariant();
                if (!string.IsNullOrEmpty(value))
                    values.Add(value);
            }

            return values.ToArray();
        }
    }
}
