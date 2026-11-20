using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class WorldCustomSoundService
    {
        public const string FolderName = "CustomSounds";
        public const string PlaySoundPrefix = "custom/";
        const int MaxSoundIdLength = 48;

        static readonly Dictionary<string, byte[]> CachedBytes = new(StringComparer.OrdinalIgnoreCase);

        public static string GetSoundsDirectory(string worldName) =>
            Path.Combine(WorldSaveService.GetWorldDirectory(worldName), FolderName);

        public static void ClearRuntime()
        {
            CachedBytes.Clear();
        }

        public static bool TrySanitizeSoundId(string rawId, out string soundId, out string error)
        {
            soundId = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(rawId))
            {
                error = "Sound name cannot be empty.";
                return false;
            }

            var builder = new StringBuilder(rawId.Length);
            for (int i = 0; i < rawId.Length; i++)
            {
                char c = rawId[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    builder.Append(c);
                else if (char.IsWhiteSpace(c))
                    builder.Append('_');
            }

            soundId = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(soundId))
            {
                error = "Sound name is invalid.";
                return false;
            }

            if (soundId.Length > MaxSoundIdLength)
            {
                error = $"Sound name must be {MaxSoundIdLength} characters or fewer.";
                return false;
            }

            return true;
        }

        public static string ToPlaySoundPath(string soundId) => PlaySoundPrefix + soundId;

        public static bool IsCustomSoundPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            return relativePath.Replace('\\', '/').StartsWith(PlaySoundPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetCustomSoundId(string relativePath, out string soundId)
        {
            soundId = null;
            if (!IsCustomSoundPath(relativePath))
                return false;

            string normalized = relativePath.Replace('\\', '/').Trim();
            soundId = normalized.Substring(PlaySoundPrefix.Length);
            return !string.IsNullOrWhiteSpace(soundId);
        }

        public static bool TrySaveToWorld(string worldName, string soundId, byte[] audioBytes, string extension, out string error)
        {
            error = null;
            if (!TrySanitizeSoundId(soundId, out string sanitized, out error))
                return false;

            if (audioBytes == null || audioBytes.Length == 0)
            {
                error = "Audio file is empty.";
                return false;
            }

            string ext = NormalizeExtension(extension);
            if (ext != ".ogg" && ext != ".mp3")
            {
                error = "Only .ogg and .mp3 files are supported.";
                return false;
            }

            try
            {
                string directory = GetSoundsDirectory(worldName);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, sanitized + ext);
                File.WriteAllBytes(path, audioBytes);
                CachedBytes[sanitized + ext] = audioBytes;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySaveToActiveWorld(string soundId, byte[] audioBytes, string extension, out string error)
        {
            if (!WorldSession.HasActiveWorld)
            {
                error = "No active world.";
                return false;
            }

            return TrySaveToWorld(WorldSession.ActiveWorldName, soundId, audioBytes, extension, out error);
        }

        public static bool TryResolveCustomFilePath(string relativePath, out string fullPath)
        {
            fullPath = null;
            if (!TryGetCustomSoundId(relativePath, out string soundId) || !WorldSession.HasActiveWorld)
                return false;

            string directory = GetSoundsDirectory(WorldSession.ActiveWorldName);
            if (!Directory.Exists(directory))
                return false;

            string ogg = Path.Combine(directory, soundId + ".ogg");
            if (File.Exists(ogg))
            {
                fullPath = ogg;
                return true;
            }

            string mp3 = Path.Combine(directory, soundId + ".mp3");
            if (File.Exists(mp3))
            {
                fullPath = mp3;
                return true;
            }

            return false;
        }

        static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return ".ogg";

            string ext = extension.Trim().ToLowerInvariant();
            return ext.StartsWith('.') ? ext : "." + ext;
        }
    }
}
