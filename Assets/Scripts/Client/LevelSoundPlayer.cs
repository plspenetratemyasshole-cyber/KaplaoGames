using System;
using System.Collections;
using System.IO;
using KaplaoGames.Shared;
using UnityEngine;
using UnityEngine.Networking;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Plays Minecraft-style sounds from StreamingAssets/MinecraftSounds/.
    /// Preview uses a single AudioSource; PlayAt uses spatial one-shots at world position.
    /// </summary>
    public static class LevelSoundPlayer
    {
        public const string SoundsRootFolderName = "MinecraftSounds";

        static LevelSoundPlayerHost host;

        public static string GetStreamingSoundsRootPath() =>
            Path.Combine(Application.streamingAssetsPath, SoundsRootFolderName);

        public static string ResolveAudioPath(string relativePath)
        {
            if (WorldCustomSoundService.TryResolveCustomFilePath(relativePath, out string customPath))
                return customPath;

            string normalized = relativePath.Replace('\\', '/');
            return Path.Combine(GetStreamingSoundsRootPath(), normalized);
        }

        public static void Stop()
        {
            if (host != null)
                host.StopPlayback();
        }

        public static void PlayAt(float x, float y, string relativePath, float volume)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            EnsureHost();
            host.PlayAt(new Vector3(x, y, 0f), relativePath.Trim(), Mathf.Clamp01(volume));
        }

        public static void Preview(string relativePath, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            EnsureHost();
            host.Play(relativePath.Trim(), Mathf.Clamp01(volume));
        }

        static void EnsureHost()
        {
            if (host != null)
                return;

            GameObject hostObject = new GameObject("LevelSoundPlayerHost");
            UnityEngine.Object.DontDestroyOnLoad(hostObject);
            host = hostObject.AddComponent<LevelSoundPlayerHost>();
        }

        sealed class LevelSoundPlayerHost : MonoBehaviour
        {
            AudioSource source;
            Coroutine playRoutine;

            void Awake()
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
            }

            public void StopPlayback()
            {
                if (playRoutine != null)
                {
                    StopCoroutine(playRoutine);
                    playRoutine = null;
                }

                if (source != null)
                    source.Stop();
            }

            public void Play(string relativePath, float volume)
            {
                StopPlayback();
                playRoutine = StartCoroutine(PlayRoutine(relativePath, volume));
            }

            public void PlayAt(Vector3 position, string relativePath, float volume)
            {
                KaplaoAudioPreference.Load();
                StartCoroutine(PlayAtRoutine(position, relativePath, volume));
            }

            IEnumerator PlayAtRoutine(Vector3 position, string relativePath, float volume)
            {
                AudioClip clip = null;
                yield return LoadClip(relativePath, loaded => clip = loaded);
                if (clip == null)
                    yield break;

                AudioSource.PlayClipAtPoint(clip, position, volume * KaplaoAudioPreference.SfxVolume);
            }

            IEnumerator PlayRoutine(string relativePath, float volume)
            {
                AudioClip clip = null;
                yield return LoadClip(relativePath, loaded => clip = loaded);
                playRoutine = null;

                if (clip == null)
                    yield break;

                source.clip = clip;
                source.volume = volume * KaplaoAudioPreference.SfxVolume;
                source.Play();
            }

            IEnumerator LoadClip(string relativePath, Action<AudioClip> onComplete)
            {
                string normalized = relativePath.Replace('\\', '/');
                string fullPath = ResolveAudioPath(normalized);
                string url = fullPath.Contains("://") ? fullPath : "file:///" + fullPath.Replace('\\', '/');

                using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, ResolveAudioType(normalized));
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Kaplao] Failed to load sound '{relativePath}': {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                    Debug.LogWarning($"[Kaplao] Sound '{relativePath}' loaded empty clip.");

                onComplete?.Invoke(clip);
            }
        }

        static AudioType ResolveAudioType(string relativePath)
        {
            string extension = Path.GetExtension(relativePath).ToLowerInvariant();
            return extension switch
            {
                ".wav" => AudioType.WAV,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.OGGVORBIS
            };
        }
    }
}
