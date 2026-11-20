using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public struct KaplaoHttpTextResponse
    {
        public bool Success;
        public long StatusCode;
        public string Body;
        public string Error;
    }

    public struct KaplaoHttpBytesResponse
    {
        public bool Success;
        public long StatusCode;
        public byte[] Body;
        public string Error;
    }

    /// <summary>
    /// HTTP on the Unity main thread. Task.Run breaks DNS on some Windows setups.
    /// </summary>
    public static class KaplaoHttpClientHelper
    {
        static bool networkInit;

        static void EnsureNetworkInit()
        {
            if (networkInit)
                return;

            networkInit = true;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try { AppContext.SetSwitch("System.Net.Dns.DisableIPv6", true); }
            catch { /* ignore */ }
#endif
        }

        static HttpClient CreateClient(TimeSpan timeout, bool followRedirects)
        {
            EnsureNetworkInit();

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = followRedirects,
                UseProxy = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            if (WebRequest.DefaultWebProxy != null)
                handler.Proxy = WebRequest.DefaultWebProxy;

            return new HttpClient(handler) { Timeout = timeout };
        }

        public static IEnumerator WarmUpDns(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                yield break;

            string host = uri.Host;
            if (IPAddress.TryParse(host, out _))
                yield break;

            Task<IPAddress[]> dnsTask = Dns.GetHostAddressesAsync(host);
            while (!dnsTask.IsCompleted)
                yield return null;

            if (dnsTask.IsFaulted)
            {
                Debug.LogWarning(
                    $"[KaplaoHttp] DNS warmup failed for {host}: {dnsTask.Exception?.GetBaseException().Message}");
            }
            else if (dnsTask.Result != null && dnsTask.Result.Length > 0)
            {
                Debug.Log($"[KaplaoHttp] DNS warmup ok for {host}: {dnsTask.Result[0]}");
            }
        }

        public static bool IsDnsFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("NameResolutionFailure", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Cannot resolve destination host", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("No such host is known", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static IEnumerator PostJson(
            string url,
            string json,
            IReadOnlyDictionary<string, string> headers,
            Action<KaplaoHttpTextResponse> onComplete)
        {
            yield return WarmUpDns(url);

            Task<KaplaoHttpTextResponse> task = PostJsonOnceAsync(url, json, headers);
            while (!task.IsCompleted)
                yield return null;

            onComplete?.Invoke(task.IsFaulted
                ? new KaplaoHttpTextResponse { Success = false, Error = task.Exception?.GetBaseException().Message ?? "Request failed." }
                : task.Result);
        }

        public static IEnumerator PutBytes(
            string url,
            byte[] bytes,
            string contentType,
            Action<KaplaoHttpBytesResponse> onComplete,
            Action<float> onProgress = null)
        {
            onProgress?.Invoke(0.1f);
            yield return WarmUpDns(url);

            Task<KaplaoHttpBytesResponse> task = PutBytesOnceAsync(url, bytes, contentType);
            while (!task.IsCompleted)
                yield return null;

            onProgress?.Invoke(1f);
            onComplete?.Invoke(task.IsFaulted
                ? new KaplaoHttpBytesResponse { Success = false, Error = task.Exception?.GetBaseException().Message ?? "Request failed." }
                : task.Result);
        }

        public static IEnumerator GetBytes(
            string url,
            IReadOnlyDictionary<string, string> headers,
            Action<KaplaoHttpBytesResponse> onComplete,
            Action<float> onProgress = null)
        {
            onProgress?.Invoke(0.05f);
            yield return WarmUpDns(url);

            Task<KaplaoHttpBytesResponse> task = GetBytesOnceAsync(url, headers);
            while (!task.IsCompleted)
                yield return null;

            onProgress?.Invoke(1f);
            onComplete?.Invoke(task.IsFaulted
                ? new KaplaoHttpBytesResponse { Success = false, Error = task.Exception?.GetBaseException().Message ?? "Request failed." }
                : task.Result);
        }

        static async Task<KaplaoHttpTextResponse> PostJsonOnceAsync(
            string url,
            string json,
            IReadOnlyDictionary<string, string> headers)
        {
            try
            {
                using HttpClient client = CreateClient(TimeSpan.FromSeconds(30), followRedirects: false);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(json ?? "{}", Encoding.UTF8, "application/json");
                ApplyHeaders(request, headers);

                using HttpResponseMessage response = await client.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                return new KaplaoHttpTextResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (long)response.StatusCode,
                    Body = body,
                    Error = response.IsSuccessStatusCode ? null : body
                };
            }
            catch (Exception ex)
            {
                return new KaplaoHttpTextResponse { Success = false, Error = ex.GetBaseException().Message };
            }
        }

        static async Task<KaplaoHttpBytesResponse> PutBytesOnceAsync(string url, byte[] bytes, string contentType)
        {
            try
            {
                using HttpClient client = CreateClient(TimeSpan.FromMinutes(5), followRedirects: true);
                using var content = new ByteArrayContent(bytes ?? Array.Empty<byte>());
                content.Headers.TryAddWithoutValidation("Content-Type", contentType ?? "application/octet-stream");

                using HttpResponseMessage response = await client.PutAsync(url, content);
                string errorBody = response.IsSuccessStatusCode
                    ? null
                    : await response.Content.ReadAsStringAsync();

                return new KaplaoHttpBytesResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (long)response.StatusCode,
                    Error = errorBody
                };
            }
            catch (Exception ex)
            {
                return new KaplaoHttpBytesResponse { Success = false, Error = ex.GetBaseException().Message };
            }
        }

        static async Task<KaplaoHttpBytesResponse> GetBytesOnceAsync(string url, IReadOnlyDictionary<string, string> headers)
        {
            try
            {
                using HttpClient client = CreateClient(TimeSpan.FromMinutes(5), followRedirects: true);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyHeaders(request, headers);

                using HttpResponseMessage response = await client.SendAsync(request);
                byte[] body = await response.Content.ReadAsByteArrayAsync();
                string errorBody = response.IsSuccessStatusCode
                    ? null
                    : Encoding.UTF8.GetString(body ?? Array.Empty<byte>());

                return new KaplaoHttpBytesResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (long)response.StatusCode,
                    Body = body,
                    Error = errorBody
                };
            }
            catch (Exception ex)
            {
                return new KaplaoHttpBytesResponse { Success = false, Error = ex.GetBaseException().Message };
            }
        }

        static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null)
                return;

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue;

                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        public static string FormatTextError(KaplaoHttpTextResponse response, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(response.Error) && response.Error.Length < 400)
                return $"{fallback} ({response.StatusCode}): {response.Error}";
            if (!string.IsNullOrWhiteSpace(response.Error))
                return $"{fallback} ({response.StatusCode})";
            return $"{fallback} ({response.StatusCode})";
        }

        public static string FormatBytesError(KaplaoHttpBytesResponse response, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(response.Error) && response.Error.Length < 400)
                return $"{fallback} ({response.StatusCode}): {response.Error}";
            if (!string.IsNullOrWhiteSpace(response.Error))
                return $"{fallback} ({response.StatusCode})";
            return $"{fallback} ({response.StatusCode})";
        }
    }
}
