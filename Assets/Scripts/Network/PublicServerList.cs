using System;
using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Shared;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Network
{
    [Serializable]
    public class PublicServerEntry
    {
        public string name;
        public string address;
    }

    [Serializable]
    class PublicServerListDto
    {
        public PublicServerEntry[] servers;
    }

    /// <summary>
    /// Fetches the official/public server list from a static JSON URL (e.g. GitHub Gist raw).
    /// Expected JSON: { "servers": [ { "name": "...", "address": "host:port" } ] }
    /// </summary>
    public static class PublicServerList
    {
        public const string DefaultUrl =
            "https://gist.githubusercontent.com/plspenetratemyasshole-cyber/af969860a9658190f665ac2aee1fde7c/raw/servers.json";

        public static IEnumerator Fetch(string url, Action<List<PublicServerEntry>> onSuccess, Action<string> onError)
        {
            string requestUrl = string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim();
            // Bust CDN/browser caches so Refresh always sees the latest Gist edit.
            string separator = requestUrl.Contains("?") ? "&" : "?";
            requestUrl = $"{requestUrl}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            using UnityWebRequest request = UnityWebRequest.Get(requestUrl);
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error ?? "Request failed");
                yield break;
            }

            string json = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                onSuccess?.Invoke(new List<PublicServerEntry>());
                yield break;
            }

            PublicServerListDto dto;
            try
            {
                dto = JsonUtility.FromJson<PublicServerListDto>(json);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Invalid server list JSON: {ex.Message}");
                yield break;
            }

            var result = new List<PublicServerEntry>();
            if (dto?.servers != null)
            {
                for (int i = 0; i < dto.servers.Length; i++)
                {
                    PublicServerEntry entry = dto.servers[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                        continue;

                    entry.name = string.IsNullOrWhiteSpace(entry.name) ? "Public Server" : entry.name.Trim();
                    entry.address = entry.address.Trim();
                    result.Add(entry);
                }
            }

            onSuccess?.Invoke(result);
        }

        public static bool TryParseAddress(string address, out string host, out ushort port)
        {
            host = null;
            port = PlayerSession.DefaultPort;

            if (string.IsNullOrWhiteSpace(address))
                return false;

            string raw = address.Trim();
            int colon = raw.LastIndexOf(':');
            if (colon <= 0 || colon >= raw.Length - 1)
            {
                host = raw;
                return !string.IsNullOrWhiteSpace(host);
            }

            host = raw.Substring(0, colon).Trim();
            string portText = raw.Substring(colon + 1).Trim();
            if (!ushort.TryParse(portText, out port) || port == 0)
                return false;

            return !string.IsNullOrWhiteSpace(host);
        }
    }
}
