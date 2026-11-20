using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Asks KaplaoGamesWebsite to verify a JWT and whether the account is an admin.
    /// Host/server must call this — never trust client-supplied email for privileges.
    /// </summary>
    public static class KaplaoAccountVerificationService
    {
        public class VerifyResult
        {
            public bool Valid;
            public string Email;
            public string Nickname;
            public bool IsAdmin;
            public string Permission;
            public string[] Rights = Array.Empty<string>();
            public bool CanDeleteLevels;
            public string NicknamePreset;
            public string[] OwnedSkinIds = Array.Empty<string>();
        }

        public static IEnumerator VerifyPlayerCoroutine(string authToken, Action<VerifyResult> onComplete)
        {
            var result = new VerifyResult();
            if (onComplete == null)
                yield break;

            if (string.IsNullOrWhiteSpace(authToken))
            {
                Debug.LogWarning("[KaplaoAccountVerification] Empty auth token — player is not signed in.");
                onComplete(result);
                yield break;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning(
                    "[KaplaoAccountVerification] No network — skipping online verification (no admin / nickname preset).");
                onComplete(result);
                yield break;
            }

            string secret = ResolveGameServerSecret();
            if (string.IsNullOrWhiteSpace(secret))
            {
                Debug.LogWarning("[KaplaoAccountVerification] Game server secret is empty — admin verification disabled.");
                onComplete(result);
                yield break;
            }

            string[] urls = ResolveVerifyUrls();
            for (int i = 0; i < urls.Length; i++)
            {
                bool last = i == urls.Length - 1;
                yield return VerifyAgainstUrl(urls[i], authToken.Trim(), secret, result, last);
                if (result.Valid)
                {
                    onComplete(result);
                    yield break;
                }
            }

            onComplete(result);
        }

        static IEnumerator VerifyAgainstUrl(
            string url,
            string authToken,
            string secret,
            VerifyResult result,
            bool logFailure)
        {
            string bodyJson = "{\"token\":\"" + EscapeJsonString(authToken) + "\"}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);
            bool useSupabaseFunction = url.IndexOf("/functions/v1/", StringComparison.OrdinalIgnoreCase) >= 0;
            PublicLevelCatalogConfig catalog = PublicLevelCatalogConfig.Load();

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Kaplao-Game-Secret", secret);
                if (useSupabaseFunction && catalog != null && !string.IsNullOrWhiteSpace(catalog.anonKey))
                {
                    request.SetRequestHeader("apikey", catalog.anonKey);
                    request.SetRequestHeader("Authorization", "Bearer " + catalog.anonKey);
                }

                request.timeout = 12;
                yield return request.SendWebRequest();

                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"[KaplaoAccountVerification] HTTP failed ({url}): {request.error} ({request.responseCode}) body={Truncate(responseBody, 300)}");
                    yield break;
                }

                bool valid = ExtractJsonBool(responseBody, "valid");
                if (!valid)
                {
                    Debug.LogWarning($"[KaplaoAccountVerification] Invalid token response from {url}: {Truncate(responseBody, 300)}");
                    yield break;
                }

                result.Valid = true;
                result.Email = ExtractJsonString(responseBody, "email") ?? string.Empty;
                result.Nickname = ExtractJsonString(responseBody, "nickname") ?? string.Empty;
                result.IsAdmin = ExtractJsonBool(responseBody, "isAdmin");
                result.Permission = ExtractJsonString(responseBody, "permission");
                result.Rights = ExtractJsonStringArray(responseBody, "rights");
                result.CanDeleteLevels = ExtractJsonBool(responseBody, "canDeleteLevels") ||
                                         HasRight(result.Rights, "level_purge") ||
                                         CanDeletePublicLevels(result.Permission);
                if (string.IsNullOrWhiteSpace(result.Permission))
                    result.Permission = result.IsAdmin ? "admin" : "none";
                if (!result.IsAdmin && HasRight(result.Rights, "game_host"))
                    result.IsAdmin = true;
                result.NicknamePreset = ExtractJsonString(responseBody, "nicknamePreset");
                if (string.IsNullOrWhiteSpace(result.NicknamePreset) ||
                    string.Equals(result.NicknamePreset, "own", StringComparison.OrdinalIgnoreCase))
                    result.NicknamePreset = "none";
                result.OwnedSkinIds = ExtractJsonStringArray(responseBody, "ownedSkinIds");

                Debug.Log(
                    $"[KaplaoAccountVerification] {result.Email} permission={result.Permission} rights=[{string.Join(",", result.Rights)}] preset={result.NicknamePreset} admin={result.IsAdmin} skins={result.OwnedSkinIds.Length}");
            }
        }

        static string[] ResolveVerifyUrls()
        {
            var urls = new System.Collections.Generic.List<string>();
            PublicLevelCatalogConfig catalog = PublicLevelCatalogConfig.Load();
            if (catalog != null && catalog.IsCatalogConfigured)
                urls.Add(catalog.LevelFunctionsBaseUrl + "/verify-player");

            string website = ResolveWebsiteUrl() + "/api/game/verify-player";
            if (!urls.Contains(website))
                urls.Add(website);

            return urls.ToArray();
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

        static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value ?? string.Empty;
            return value.Substring(0, max) + "…";
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

        static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        public static bool CanDeletePublicLevels(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return false;

            string normalized = permission.Trim().ToLowerInvariant();
            return normalized == "admin" || normalized == "level_mod" || normalized == "level_purge";
        }

        public static bool CanDeletePublicLevels(VerifyResult result)
        {
            if (result == null)
                return false;
            if (result.CanDeleteLevels)
                return true;
            if (HasRight(result.Rights, "level_purge"))
                return true;
            return CanDeletePublicLevels(result.Permission);
        }

        public static bool HasRight(string[] rights, string right)
        {
            if (rights == null || rights.Length == 0 || string.IsNullOrWhiteSpace(right))
                return false;

            for (int i = 0; i < rights.Length; i++)
            {
                if (string.Equals(rights[i], right, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            var match = Regex.Match(
                json,
                $"\"{Regex.Escape(key)}\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return null;

            return match.Groups[1].Value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
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

            var values = new System.Collections.Generic.List<string>();
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
