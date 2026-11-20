using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class WorldCustomTextureService
    {
        public const string FolderName = "CustomTextures";
        const int MaxTextureIdLength = 48;

        static readonly Dictionary<string, byte[]> CachedPngBytes = new(StringComparer.OrdinalIgnoreCase);

        public static string GetTexturesDirectory(string worldName) =>
            Path.Combine(WorldSaveService.GetWorldDirectory(worldName), FolderName);

        public static void ClearRuntime()
        {
            CachedPngBytes.Clear();
            RuntimeBlockTextureRegistry.Clear();
        }

        public static bool TrySanitizeTextureId(string rawId, out string textureId, out string error)
        {
            textureId = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(rawId))
            {
                error = "Texture name cannot be empty.";
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

            textureId = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(textureId))
            {
                error = "Texture name is invalid.";
                return false;
            }

            if (textureId.Length > MaxTextureIdLength)
            {
                error = $"Texture name must be {MaxTextureIdLength} characters or fewer.";
                return false;
            }

            if (BlockTextureCatalog.Instance != null &&
                BlockTextureCatalog.Instance.TryGetSprite(textureId, out _))
            {
                error = "This name is already used by a built-in texture.";
                return false;
            }

            return true;
        }

        public static bool IsCustomTexture(string textureId)
        {
            if (string.IsNullOrEmpty(textureId))
                return false;

            if (BlockTextureCatalog.Instance != null &&
                BlockTextureCatalog.Instance.TryGetSprite(textureId, out _))
            {
                return false;
            }

            return RuntimeBlockTextureRegistry.TryGetSprite(textureId, out _);
        }

        public static bool TryRenameTexture(string oldTextureId, string newTextureId, out string sanitizedNewId, out string error)
        {
            sanitizedNewId = string.Empty;
            error = null;
            if (!IsCustomTexture(oldTextureId))
            {
                error = "Only custom world textures can be renamed.";
                return false;
            }

            if (!TrySanitizeTextureId(newTextureId, out sanitizedNewId, out error))
                return false;

            if (string.Equals(oldTextureId, sanitizedNewId, StringComparison.OrdinalIgnoreCase))
            {
                error = "New name is the same as the current name.";
                return false;
            }

            if (RuntimeBlockTextureRegistry.TryGetSprite(sanitizedNewId, out _) ||
                (BlockTextureCatalog.Instance != null && BlockTextureCatalog.Instance.TryGetSprite(sanitizedNewId, out _)))
            {
                error = "A texture with this name already exists.";
                return false;
            }

            if (!WorldSession.HasActiveWorld)
            {
                error = "No active world.";
                return false;
            }

            string directory = GetTexturesDirectory(WorldSession.ActiveWorldName);
            string oldPath = Path.Combine(directory, oldTextureId + ".png");
            string newPath = Path.Combine(directory, sanitizedNewId + ".png");

            if (!File.Exists(oldPath))
            {
                error = "Texture file not found on disk.";
                return false;
            }

            if (File.Exists(newPath))
            {
                error = "Target texture file already exists.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);
                File.Move(oldPath, newPath);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (CachedPngBytes.TryGetValue(oldTextureId, out byte[] pngBytes))
            {
                CachedPngBytes.Remove(oldTextureId);
                CachedPngBytes[sanitizedNewId] = pngBytes;
            }
            else
            {
                try
                {
                    CachedPngBytes[sanitizedNewId] = File.ReadAllBytes(newPath);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            if (!RuntimeBlockTextureRegistry.TryRename(oldTextureId, sanitizedNewId))
            {
                error = "Failed to update runtime texture registry.";
                return false;
            }

            return true;
        }

        public static bool TryRegisterRenameOnClient(string oldTextureId, string newTextureId)
        {
            if (string.IsNullOrEmpty(oldTextureId) || string.IsNullOrEmpty(newTextureId))
                return false;

            if (CachedPngBytes.TryGetValue(oldTextureId, out byte[] pngBytes))
            {
                CachedPngBytes.Remove(oldTextureId);
                CachedPngBytes[newTextureId] = pngBytes;
            }

            return RuntimeBlockTextureRegistry.TryRename(oldTextureId, newTextureId);
        }

        public static bool TryGetCachedBytes(string textureId, out byte[] pngBytes) =>
            CachedPngBytes.TryGetValue(textureId, out pngBytes);

        public static IReadOnlyCollection<KeyValuePair<string, byte[]>> CachedEntries => CachedPngBytes;

        public static bool TryRegisterFromPng(string textureId, byte[] pngBytes, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(textureId) || pngBytes == null || pngBytes.Length == 0)
            {
                error = "Texture data is empty.";
                return false;
            }

            if (!PngImportUtility.TryLoadTexture(pngBytes, out Texture2D texture, out error))
                return false;

            Sprite sprite;
            try
            {
                sprite = CreateStretchedTileSprite(texture);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }

            if (sprite == null)
            {
                error = "Failed to create sprite.";
                return false;
            }

            CachedPngBytes[textureId] = pngBytes;
            RuntimeBlockTextureRegistry.Register(textureId, sprite, textureId);
            return true;
        }

        public static bool TrySaveToActiveWorld(string textureId, byte[] pngBytes, out string error)
        {
            error = null;
            if (!WorldSession.HasActiveWorld)
            {
                error = "No active world.";
                return false;
            }

            return TrySaveToWorld(WorldSession.ActiveWorldName, textureId, pngBytes, out error);
        }

        public static bool TrySaveToWorld(string worldName, string textureId, byte[] pngBytes, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(worldName) || string.IsNullOrEmpty(textureId) || pngBytes == null)
            {
                error = "Invalid texture save request.";
                return false;
            }

            try
            {
                string directory = GetTexturesDirectory(worldName);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, textureId + ".png");
                File.WriteAllBytes(path, pngBytes);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static int LoadActiveWorldIntoRegistry()
        {
            if (!WorldSession.HasActiveWorld)
                return 0;

            return LoadWorldIntoRegistry(WorldSession.ActiveWorldName);
        }

        public static int LoadWorldIntoRegistry(string worldName)
        {
            ClearRuntime();
            if (string.IsNullOrEmpty(worldName))
                return 0;

            string directory = GetTexturesDirectory(worldName);
            if (!Directory.Exists(directory))
                return 0;

            string[] files = Directory.GetFiles(directory, "*.png");
            int loaded = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string id = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(id))
                    continue;

                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    if (TryRegisterFromPng(id, bytes, out _))
                        loaded++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Kaplao] Failed to load custom texture '{id}': {ex.Message}");
                }
            }

            return loaded;
        }

        public static Sprite CreateStretchedTileSprite(Texture2D source)
        {
            if (source == null)
                return null;

            int width = source.width;
            int height = source.height;
            if (width <= 0 || height <= 0)
                return null;

            int size = Mathf.Max(width, height);
            var square = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] destination = new Color32[size * size];
            Color32[] sourcePixels = source.GetPixels32();

            for (int y = 0; y < size; y++)
            {
                int srcY = Mathf.Clamp((y * height) / size, 0, height - 1);
                for (int x = 0; x < size; x++)
                {
                    int srcX = Mathf.Clamp((x * width) / size, 0, width - 1);
                    destination[y * size + x] = sourcePixels[srcY * width + srcX];
                }
            }

            square.SetPixels32(destination);
            square.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            PngImportUtility.ApplyNoSmoothing(square);
            return Sprite.Create(square, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
