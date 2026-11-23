using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using KaplaoGames.Server;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Shared
{
    [Serializable]
    public class PublicLevelEntry
    {
        public string id;
        public string title;
        public string author_nick;
        public string author_email;
        public string storage_path;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public struct BrowseLevelCacheMeta
    {
        public string levelId;
        public string storagePath;
        public string versionToken; // storage_path + updated_at/created_at
        public string title;
        public long lastPlayedUtcTicks;
    }

    [Serializable]
    class PublicLevelListDto
    {
        public PublicLevelEntry[] data;
    }

    [Serializable]
    class CloudflareUploadUrlResponse
    {
        public string storage_path;
        public string upload_url;
        public string error;
    }

    [Serializable]
    class TimewebUploadUrlResponse
    {
        public bool ok;
        public string storage_path;
        public string upload_url;
        public string error;
    }

    [Serializable]
    class TimewebDownloadUrlResponse
    {
        public bool ok;
        public string download_url;
        public string error;
    }

    public static class PublicLevelService
    {
        const string S3StoragePrefix = "s3:";

        public enum LevelInstallTarget
        {
            UserLibrary,
            BrowsePlay
        }

        public static IEnumerator FetchPage(int offset, string search, Action<List<PublicLevelEntry>> onSuccess, Action<string> onError)
        {
            PublicLevelCatalogConfig config = PublicLevelCatalogConfig.Load();
            if (config == null || !config.IsConfigured)
            {
                onError?.Invoke("Public level catalog is not configured.");
                yield break;
            }

            string url = $"{config.RestBaseUrl}{config.levelsTable}?select=id,title,author_nick,author_email,storage_path,created_at&order=created_at.desc&offset={offset}&limit={config.pageSize}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&title=ilike.*{UnityWebRequest.EscapeURL(search.Trim())}*";

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("apikey", config.anonKey);
            request.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error ?? "Failed to fetch levels.");
                yield break;
            }

            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                onSuccess?.Invoke(new List<PublicLevelEntry>());
                yield break;
            }

            if (json.TrimStart().StartsWith("["))
                json = "{\"data\":" + json + "}";

            PublicLevelListDto dto = JsonUtility.FromJson<PublicLevelListDto>(json);
            onSuccess?.Invoke(dto?.data != null ? new List<PublicLevelEntry>(dto.data) : new List<PublicLevelEntry>());
        }

        public static IEnumerator PublishActiveWorld(string title, string authorNick, Action<float> onProgress, Action<string> onSuccess, Action<string> onError)
        {
            PublicLevelCatalogConfig config = PublicLevelCatalogConfig.Load();
            if (config == null || !config.IsConfigured)
            {
                onError?.Invoke("Public level catalog is not configured.");
                yield break;
            }

            if (!NetworkServer.active || LevelGridServer.Instance == null)
            {
                onError?.Invoke("Only the host can publish.");
                yield break;
            }

            if (!WorldSaveService.TrySaveActiveWorldFromGrid(out string saveError))
                Debug.LogWarning($"[PublicLevelService] Save before publish failed: {saveError}");

            string tempZip = Path.Combine(Path.GetTempPath(), $"kaplao_publish_{Guid.NewGuid():N}.zip");
            try
            {
                ZipWorldFolder(WorldSession.ActiveWorldName, tempZip);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                yield break;
            }

            byte[] bytes = File.ReadAllBytes(tempZip);
            string objectPath = null;
            string uploadError = null;
            yield return UploadLevelArchive(config, bytes, onProgress, path => objectPath = path, err => uploadError = err);
            TryDelete(tempZip);

            if (!string.IsNullOrEmpty(uploadError) || string.IsNullOrEmpty(objectPath))
            {
                onError?.Invoke(uploadError ?? "Upload failed.");
                yield break;
            }

            string insertError = null;
            string authorEmail = PlayerSession.IsLoggedIn ? PlayerSession.AccountEmail : string.Empty;
            yield return InsertMetadata(config, title, authorNick, authorEmail, objectPath, msg => onSuccess?.Invoke(msg), err => insertError = err);
            if (!string.IsNullOrEmpty(insertError))
                onError?.Invoke(insertError);
        }

        public static bool IsOwnedByCurrentPlayer(PublicLevelEntry entry)
        {
            if (entry == null || !PlayerSession.IsLoggedIn)
                return false;

            if (!string.IsNullOrWhiteSpace(entry.author_email) &&
                !string.IsNullOrWhiteSpace(PlayerSession.AccountEmail))
            {
                return string.Equals(
                    entry.author_email.Trim(),
                    PlayerSession.AccountEmail.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(entry.author_email) &&
                !string.IsNullOrWhiteSpace(entry.author_nick))
            {
                return string.Equals(
                    PlayerSession.SanitizeNickname(entry.author_nick),
                    PlayerSession.Nickname,
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        static IEnumerator InsertMetadata(
            PublicLevelCatalogConfig config,
            string title,
            string authorNick,
            string authorEmail,
            string objectPath,
            Action<string> onSuccess,
            Action<string> onError)
        {
            string payload = JsonUtility.ToJson(new PublicLevelInsertPayload
            {
                title = title,
                author_nick = authorNick,
                author_email = string.IsNullOrWhiteSpace(authorEmail) ? string.Empty : authorEmail.Trim(),
                storage_path = objectPath
            });

            string url = config.RestBaseUrl + config.levelsTable;
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", config.anonKey);
            request.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                onError?.Invoke(BuildHttpError(request, "Failed to register level metadata."));
            else
            {
                KaplaoPlayerActionLogClient.Log(
                    "level_published",
                    title,
                    "{\"source\":\"PublicLevelService.InsertMetadata\"}");
                onSuccess?.Invoke("Level published.");
            }
        }

        static IEnumerator UploadLevelArchive(
            PublicLevelCatalogConfig config,
            byte[] bytes,
            Action<float> onProgress,
            Action<string> onPath,
            Action<string> onError)
        {
            switch (config.fileStorage)
            {
                case PublicLevelFileStorage.TimewebS3:
                    yield return UploadViaTimewebWebsite(config, bytes, onProgress, onPath, onError);
                    yield break;

                case PublicLevelFileStorage.CloudflareR2Worker:
                    yield return UploadViaCloudflarePresigned(config, bytes, onProgress, onPath, onError);
                    yield break;

                default:
                    yield return UploadViaSupabase(config, bytes, onProgress, onPath, onError);
                    yield break;
            }
        }

        static IEnumerator UploadViaTimewebWebsite(
            PublicLevelCatalogConfig config,
            byte[] bytes,
            Action<float> onProgress,
            Action<string> onPath,
            Action<string> onError)
        {
            string secret = config.GameServerSecret;
            if (!config.IsCatalogConfigured || string.IsNullOrWhiteSpace(secret))
            {
                onError?.Invoke("Supabase catalog or Game Server Secret is not configured.");
                yield break;
            }

            onProgress?.Invoke(0.05f);

            string askUrl = config.LevelFunctionsBaseUrl + "/levels-upload-url";
            TimewebUploadUrlResponse parsed = null;
            string askError = null;

            using (UnityWebRequest ask = new UnityWebRequest(askUrl, UnityWebRequest.kHttpVerbPOST))
            {
                ask.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                ask.downloadHandler = new DownloadHandlerBuffer();
                ask.SetRequestHeader("Content-Type", "application/json");
                ask.SetRequestHeader("apikey", config.anonKey);
                ask.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
                ask.SetRequestHeader("X-Kaplao-Game-Secret", secret);
                ask.timeout = 30;

                yield return ask.SendWebRequest();
                if (ask.result != UnityWebRequest.Result.Success)
                {
                    askError = BuildHttpError(ask, $"Failed to get Timeweb upload URL ({askUrl}).");
                    yield break;
                }

                parsed = JsonUtility.FromJson<TimewebUploadUrlResponse>(ask.downloadHandler.text);
            }

            if (!string.IsNullOrEmpty(askError))
            {
                onError?.Invoke(askError);
                yield break;
            }

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.upload_url) || string.IsNullOrWhiteSpace(parsed.storage_path))
            {
                onError?.Invoke(string.IsNullOrWhiteSpace(parsed?.error)
                    ? "Supabase function returned an invalid upload URL response."
                    : parsed.error);
                yield break;
            }

            onProgress?.Invoke(0.15f);

            KaplaoHttpBytesResponse putResponse = default;
            yield return KaplaoHttpClientHelper.PutBytes(
                parsed.upload_url,
                bytes,
                "application/zip",
                response => putResponse = response,
                progress => onProgress?.Invoke(0.15f + progress * 0.85f));

            if (!putResponse.Success && KaplaoHttpClientHelper.IsDnsFailure(putResponse.Error))
            {
                using (UnityWebRequest put = new UnityWebRequest(parsed.upload_url, UnityWebRequest.kHttpVerbPUT))
                {
                    put.uploadHandler = new UploadHandlerRaw(bytes);
                    put.downloadHandler = new DownloadHandlerBuffer();
                    put.SetRequestHeader("Content-Type", "application/zip");
                    put.timeout = 300;

                    UnityWebRequestAsyncOperation op = put.SendWebRequest();
                    while (!op.isDone)
                    {
                        onProgress?.Invoke(0.15f + put.uploadProgress * 0.85f);
                        yield return null;
                    }

                    if (put.result == UnityWebRequest.Result.Success)
                        putResponse = new KaplaoHttpBytesResponse { Success = true, StatusCode = (long)put.responseCode };
                    else
                        putResponse = new KaplaoHttpBytesResponse { Success = false, Error = put.error ?? put.downloadHandler?.text };
                }
            }

            if (!putResponse.Success)
            {
                onError?.Invoke(KaplaoHttpClientHelper.FormatBytesError(putResponse, "Timeweb upload failed."));
                yield break;
            }

            onProgress?.Invoke(1f);
            onPath?.Invoke(parsed.storage_path);
        }

        static IEnumerator UploadViaSupabase(
            PublicLevelCatalogConfig config,
            byte[] bytes,
            Action<float> onProgress,
            Action<string> onPath,
            Action<string> onError)
        {
            string objectPath = $"{Guid.NewGuid():N}.zip";
            string url = $"{config.StorageBaseUrl}{config.storageBucket}/{objectPath}";
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", config.anonKey);
            request.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
            request.SetRequestHeader("Content-Type", "application/zip");

            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone)
            {
                onProgress?.Invoke(request.uploadProgress);
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
                onError?.Invoke(BuildHttpError(request, "Upload failed."));
            else
            {
                onProgress?.Invoke(1f);
                onPath?.Invoke(objectPath);
            }
        }

        static IEnumerator UploadViaCloudflarePresigned(
            PublicLevelCatalogConfig config,
            byte[] bytes,
            Action<float> onProgress,
            Action<string> onPath,
            Action<string> onError)
        {
            string askUrl = config.WorkerBaseUrl + "/upload-url";
            using (UnityWebRequest ask = new UnityWebRequest(askUrl, UnityWebRequest.kHttpVerbPOST))
            {
                ask.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
                ask.downloadHandler = new DownloadHandlerBuffer();
                ask.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(config.cloudflareUploadToken))
                    ask.SetRequestHeader("X-Upload-Token", config.cloudflareUploadToken);

                yield return ask.SendWebRequest();
                if (ask.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(BuildHttpError(ask, "Failed to get upload URL from Cloudflare Worker."));
                    yield break;
                }

                CloudflareUploadUrlResponse parsed = JsonUtility.FromJson<CloudflareUploadUrlResponse>(ask.downloadHandler.text);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.upload_url) || string.IsNullOrWhiteSpace(parsed.storage_path))
                {
                    onError?.Invoke(string.IsNullOrWhiteSpace(parsed?.error)
                        ? "Cloudflare Worker returned an invalid upload URL response."
                        : parsed.error);
                    yield break;
                }

                using (UnityWebRequest put = new UnityWebRequest(parsed.upload_url, UnityWebRequest.kHttpVerbPUT))
                {
                    put.uploadHandler = new UploadHandlerRaw(bytes);
                    put.downloadHandler = new DownloadHandlerBuffer();
                    put.SetRequestHeader("Content-Type", "application/zip");

                    UnityWebRequestAsyncOperation op = put.SendWebRequest();
                    while (!op.isDone)
                    {
                        onProgress?.Invoke(put.uploadProgress);
                        yield return null;
                    }

                    if (put.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke(BuildHttpError(put, "R2 upload failed."));
                        yield break;
                    }

                    onProgress?.Invoke(1f);
                    onPath?.Invoke(parsed.storage_path);
                }
            }
        }

        public const string BrowsePlayCacheMetaFileName = "public_level_cache.json";

        public static string GetVersionToken(PublicLevelEntry entry)
        {
            if (entry == null) return string.Empty;
            string stamp = !string.IsNullOrWhiteSpace(entry.updated_at) ? entry.updated_at : (entry.created_at ?? string.Empty);
            return $"{entry.id}|{entry.storage_path}|{stamp}";
        }

        public static bool IsCachedInBrowsePlay(PublicLevelEntry entry, out string worldName)
        {
            worldName = null;
            if (entry == null) return false;
            string sanitized = WorldSaveService.SanitizeWorldName(entry.title);
            if (!WorldSaveService.TryValidateWorldName(sanitized, out string validName, out _))
                return false;

            string dir = WorldSaveService.GetBrowsePlayWorldDirectory(validName);
            string levelPath = Path.Combine(dir, LevelIoPaths.DefaultFileName);
            string metaPath = Path.Combine(dir, BrowsePlayCacheMetaFileName);

            if (!File.Exists(levelPath) || !File.Exists(metaPath))
                return false;

            try
            {
                string json = File.ReadAllText(metaPath);
                BrowseLevelCacheMeta meta = JsonUtility.FromJson<BrowseLevelCacheMeta>(json);
                string currentToken = GetVersionToken(entry);
                if (meta.levelId == entry.id && meta.storagePath == entry.storage_path && meta.versionToken == currentToken)
                {
                    worldName = validName;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static void TouchBrowsePlayCache(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName)) return;
            string sanitized = WorldSaveService.SanitizeWorldName(worldName);
            string dir = WorldSaveService.GetBrowsePlayWorldDirectory(sanitized);
            string metaPath = Path.Combine(dir, BrowsePlayCacheMetaFileName);
            if (File.Exists(metaPath))
            {
                try
                {
                    string json = File.ReadAllText(metaPath);
                    BrowseLevelCacheMeta meta = JsonUtility.FromJson<BrowseLevelCacheMeta>(json);
                    meta.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
                    File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PublicLevelService] Failed to touch cache meta: {ex.Message}");
                }
            }
        }

        public static bool TryCopyFromBrowsePlayCache(PublicLevelEntry entry, out string copiedWorldName)
        {
            copiedWorldName = null;
            if (entry == null) return false;
            if (!IsCachedInBrowsePlay(entry, out string sourceWorldName))
                return false;

            string sourceDir = WorldSaveService.GetBrowsePlayWorldDirectory(sourceWorldName);
            string destWorldName = WorldSaveService.SanitizeWorldName(entry.title);
            if (!WorldSaveService.TryValidateWorldName(destWorldName, out destWorldName, out _))
                return false;

            string destDir = WorldSaveService.GetUserWorldDirectory(destWorldName);
            try
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
                Directory.CreateDirectory(destDir);

                foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(sourceDir, file);
                    if (string.Equals(rel, BrowsePlayCacheMetaFileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string destFile = Path.Combine(destDir, rel);
                    string parent = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.Copy(file, destFile, true);
                }

                WorldSaveService.RecordWorldVisited(destWorldName, ignoreBrowseFlag: true);
                copiedWorldName = destWorldName;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PublicLevelService] Failed to copy from cache: {ex.Message}");
                return false;
            }
        }

        public static void EnforceBrowsePlayCacheLimit(int maxCachedLevels = 20)
        {
            try
            {
                string root = LevelIoPaths.BrowsePlayDirectory;
                if (!Directory.Exists(root)) return;

                string[] dirs = Directory.GetDirectories(root);
                if (dirs.Length <= maxCachedLevels) return;

                var list = new List<(string dir, long lastPlayed)>();
                foreach (string d in dirs)
                {
                    string metaPath = Path.Combine(d, BrowsePlayCacheMetaFileName);
                    long ticks = 0;
                    if (File.Exists(metaPath))
                    {
                        try
                        {
                            BrowseLevelCacheMeta m = JsonUtility.FromJson<BrowseLevelCacheMeta>(File.ReadAllText(metaPath));
                            ticks = m.lastPlayedUtcTicks;
                        }
                        catch {}
                    }
                    if (ticks <= 0)
                    {
                        ticks = Directory.GetLastWriteTimeUtc(d).Ticks;
                    }
                    list.Add((d, ticks));
                }

                list.Sort((a, b) => a.lastPlayed.CompareTo(b.lastPlayed));

                string activeDir = WorldSession.HasActiveWorld
                    ? WorldSaveService.GetBrowsePlayWorldDirectory(WorldSession.ActiveWorldName)
                    : null;

                int toRemove = list.Count - maxCachedLevels;
                for (int i = 0; i < list.Count && toRemove > 0; i++)
                {
                    string targetDir = list[i].dir;
                    if (!string.IsNullOrEmpty(activeDir) && string.Equals(Path.GetFullPath(targetDir), Path.GetFullPath(activeDir), StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        Directory.Delete(targetDir, true);
                        toRemove--;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[PublicLevelService] Failed to evict cache folder {targetDir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PublicLevelService] EnforceBrowsePlayCacheLimit failed: {ex.Message}");
            }
        }

        public static IEnumerator DownloadAndInstall(
            PublicLevelEntry entry,
            Action<float> onProgress,
            Action<string> onSuccess,
            Action<string> onError,
            LevelInstallTarget target = LevelInstallTarget.UserLibrary)
        {
            yield return DownloadAndInstallInternal(entry, entry != null ? entry.storage_path : null, entry != null ? entry.title : null, onProgress, onSuccess, onError, target);
        }

        public static IEnumerator DownloadAndInstall(
            string storagePath,
            string title,
            Action<float> onProgress,
            Action<string> onSuccess,
            Action<string> onError,
            LevelInstallTarget target = LevelInstallTarget.UserLibrary)
        {
            yield return DownloadAndInstallInternal(null, storagePath, title, onProgress, onSuccess, onError, target);
        }

        static IEnumerator DownloadAndInstallInternal(
            PublicLevelEntry entry,
            string storagePath,
            string title,
            Action<float> onProgress,
            Action<string> onSuccess,
            Action<string> onError,
            LevelInstallTarget target)
        {
            PublicLevelCatalogConfig config = PublicLevelCatalogConfig.Load();
            if (config == null || !config.IsConfigured)
            {
                onError?.Invoke("Public level catalog is not configured.");
                yield break;
            }

            string url = BuildDownloadUrl(config, storagePath);
            byte[] downloadedBytes = null;
            string downloadError = null;

            if (UsesWebsiteDownload(config, storagePath))
            {
                using (UnityWebRequest meta = UnityWebRequest.Get(url))
                {
                    meta.SetRequestHeader("apikey", config.anonKey);
                    meta.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
                    meta.timeout = 30;
                    yield return meta.SendWebRequest();

                    if (meta.result != UnityWebRequest.Result.Success)
                    {
                        downloadError = BuildHttpError(meta, "Failed to get download URL.");
                        yield break;
                    }

                    TimewebDownloadUrlResponse parsed = JsonUtility.FromJson<TimewebDownloadUrlResponse>(meta.downloadHandler.text);
                    if (parsed == null || string.IsNullOrWhiteSpace(parsed.download_url))
                    {
                        downloadError = string.IsNullOrWhiteSpace(parsed?.error)
                            ? "Supabase function returned an invalid download URL response."
                            : parsed.error;
                        yield break;
                    }

                    url = parsed.download_url;
                }

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 300;
                    UnityWebRequestAsyncOperation op = request.SendWebRequest();
                    while (!op.isDone)
                    {
                        onProgress?.Invoke(request.downloadProgress);
                        yield return null;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                        downloadError = BuildHttpError(request, "Download failed.");
                    else
                        downloadedBytes = request.downloadHandler.data;
                }
            }
            else
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    if (UsesSupabaseDirectDownload(config, storagePath))
                    {
                        request.SetRequestHeader("apikey", config.anonKey);
                        request.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
                    }

                    UnityWebRequestAsyncOperation op = request.SendWebRequest();
                    while (!op.isDone)
                    {
                        onProgress?.Invoke(request.downloadProgress);
                        yield return null;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                        downloadError = BuildHttpError(request, "Download failed.");
                    else
                        downloadedBytes = request.downloadHandler.data;
                }
            }

            if (!string.IsNullOrEmpty(downloadError) || downloadedBytes == null || downloadedBytes.Length == 0)
            {
                onError?.Invoke(downloadError ?? "Download failed.");
                yield break;
            }

            string tempZip = Path.Combine(Path.GetTempPath(), $"kaplao_download_{Guid.NewGuid():N}.zip");
            File.WriteAllBytes(tempZip, downloadedBytes);
            string worldName = WorldSaveService.SanitizeWorldName(title);
            if (!WorldSaveService.TryValidateWorldName(worldName, out worldName, out string nameError))
            {
                onError?.Invoke(nameError);
                TryDelete(tempZip);
                yield break;
            }

            try
            {
                string dest = target == LevelInstallTarget.BrowsePlay
                    ? WorldSaveService.GetBrowsePlayWorldDirectory(worldName)
                    : WorldSaveService.GetUserWorldDirectory(worldName);
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.CreateDirectory(dest);
                ZipFile.ExtractToDirectory(tempZip, dest);

                if (target == LevelInstallTarget.BrowsePlay)
                {
                    BrowseLevelCacheMeta meta = new BrowseLevelCacheMeta
                    {
                        levelId = entry != null ? entry.id : string.Empty,
                        storagePath = storagePath,
                        versionToken = entry != null ? GetVersionToken(entry) : $"{storagePath}|{DateTime.UtcNow.Ticks}",
                        title = title,
                        lastPlayedUtcTicks = DateTime.UtcNow.Ticks
                    };
                    File.WriteAllText(Path.Combine(dest, BrowsePlayCacheMetaFileName), JsonUtility.ToJson(meta, true));
                    EnforceBrowsePlayCacheLimit(20);
                }
                else
                {
                    WorldSaveService.RecordWorldVisited(worldName, ignoreBrowseFlag: true);
                }

                onSuccess?.Invoke(worldName);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
            finally
            {
                TryDelete(tempZip);
            }
        }

        static string BuildDownloadUrl(PublicLevelCatalogConfig config, string storagePath)
        {
            string path = (storagePath ?? string.Empty).Trim();

            if (UsesWebsiteDownload(config, path))
            {
                return $"{config.LevelFunctionsBaseUrl}/levels-download?path={UnityWebRequest.EscapeURL(path)}";
            }

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return path;

            if (config.fileStorage == PublicLevelFileStorage.CloudflareR2Worker)
                return $"{config.WorkerBaseUrl}/download/{path.TrimStart('/')}";

            return $"{config.StorageBaseUrl}{config.storageBucket}/{path.TrimStart('/')}";
        }

        static bool UsesWebsiteDownload(PublicLevelCatalogConfig config, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (path.StartsWith(S3StoragePrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            if (path.StartsWith("pixeldrain:", StringComparison.OrdinalIgnoreCase))
                return true;

            return config.fileStorage == PublicLevelFileStorage.TimewebS3;
        }

        static bool UsesSupabaseDirectDownload(PublicLevelCatalogConfig config, string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                return false;

            if (UsesWebsiteDownload(config, storagePath))
                return false;

            if (storagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return false;

            return config.fileStorage == PublicLevelFileStorage.SupabaseStorage;
        }

        static string SanitizeFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "level";

            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(title.Trim());
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                if (Array.IndexOf(invalid, sb[i]) >= 0 || sb[i] < 32)
                    sb[i] = '_';
            }

            string name = sb.ToString();
            return string.IsNullOrWhiteSpace(name) ? "level" : name;
        }

        static string BuildHttpError(UnityWebRequest request, string fallback)
        {
            string body = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (!string.IsNullOrWhiteSpace(body) && body.Length < 400)
                return $"{fallback} ({request.responseCode}): {body}";
            if (!string.IsNullOrWhiteSpace(request.error))
                return $"{fallback} ({request.responseCode}): {request.error}";
            return $"{fallback} ({request.responseCode})";
        }

        static void ZipWorldFolder(string worldName, string zipPath)
        {
            string source = WorldSaveService.GetWorldDirectory(worldName);
            if (!Directory.Exists(source))
                throw new InvalidOperationException("World folder not found.");

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(source, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        public static IEnumerator DeleteFromCatalog(
            string levelId,
            string confirmTitle,
            Action onSuccess,
            Action<string> onError)
        {
            PublicLevelCatalogConfig config = PublicLevelCatalogConfig.Load();
            if (config == null || !config.IsCatalogConfigured)
            {
                onError?.Invoke("Public level catalog is not configured.");
                yield break;
            }

            if (!PlayerSession.IsLoggedIn || string.IsNullOrWhiteSpace(PlayerSession.AuthToken))
            {
                onError?.Invoke("Sign in via Kaplao account to delete public levels.");
                yield break;
            }

            string secret = config.GameServerSecret;
            if (string.IsNullOrWhiteSpace(secret))
            {
                onError?.Invoke("Game server secret is not configured.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(levelId) || string.IsNullOrWhiteSpace(confirmTitle))
            {
                onError?.Invoke("Level selection is invalid.");
                yield break;
            }

            string payload = JsonUtility.ToJson(new LevelDeletePayload
            {
                token = PlayerSession.AuthToken.Trim(),
                levelId = levelId.Trim(),
                confirmTitle = confirmTitle.Trim()
            });

            string[] urls = ResolveDeleteUrls(config);
            string lastError = null;
            for (int i = 0; i < urls.Length; i++)
            {
                bool last = i == urls.Length - 1;
                bool success = false;
                string attemptError = null;
                yield return DeleteFromCatalogUrl(
                    config,
                    urls[i],
                    payload,
                    secret,
                    () => success = true,
                    err => attemptError = err);

                if (success)
                {
                    onSuccess?.Invoke();
                    yield break;
                }

                lastError = attemptError;
                if (!last && IsSessionAuthFailure(attemptError))
                    continue;

                if (!last)
                    continue;

                onError?.Invoke(string.IsNullOrWhiteSpace(lastError) ? "Delete failed." : lastError);
                yield break;
            }

            onError?.Invoke(string.IsNullOrWhiteSpace(lastError) ? "Delete failed." : lastError);
        }

        static IEnumerator DeleteFromCatalogUrl(
            PublicLevelCatalogConfig config,
            string url,
            string payload,
            string secret,
            Action onSuccess,
            Action<string> onError)
        {
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Kaplao-Game-Secret", secret);
            if (url.IndexOf("/functions/v1/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                request.SetRequestHeader("apikey", config.anonKey);
                request.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
            }

            request.timeout = 30;
            yield return request.SendWebRequest();

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(BuildHttpError(request, "Delete failed."));
                yield break;
            }

            LevelDeleteResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<LevelDeleteResponse>(body);
            }
            catch
            {
                /* fall through */
            }

            if (parsed != null && parsed.ok)
            {
                onSuccess?.Invoke();
                yield break;
            }

            string message = parsed != null && !string.IsNullOrWhiteSpace(parsed.error)
                ? parsed.error
                : ExtractJsonString(body, "error");

            if (string.IsNullOrWhiteSpace(message))
                message = "Delete failed.";

            onError?.Invoke(message);
        }

        static string[] ResolveDeleteUrls(PublicLevelCatalogConfig config)
        {
            var urls = new List<string>();
            if (config.IsCatalogConfigured)
                urls.Add(config.LevelFunctionsBaseUrl + "/levels-delete");

            string website = config.WebsiteUrl + "/api/game/levels-delete";
            if (!urls.Contains(website))
                urls.Add(website);

            return urls.ToArray();
        }

        static bool IsSessionAuthFailure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return false;

            string lower = error.ToLowerInvariant();
            return lower.Contains("invalid or expired session") ||
                   lower.Contains("sign in required") ||
                   lower.Contains("(401)");
        }

        static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                json,
                $"\"{System.Text.RegularExpressions.Regex.Escape(key)}\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success)
                return null;

            return match.Groups[1].Value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        [Serializable]
        struct LevelDeletePayload
        {
            public string token;
            public string levelId;
            public string confirmTitle;
        }

        [Serializable]
        class LevelDeleteResponse
        {
            public bool ok;
            public string error;
            public string deletedId;
        }

        [Serializable]
        struct PublicLevelInsertPayload
        {
            public string title;
            public string author_nick;
            public string author_email;
            public string storage_path;
        }
    }
}
