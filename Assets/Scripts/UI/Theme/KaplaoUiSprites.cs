using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace KaplaoGames.UI.Theme
{
    /// <summary>
    /// Procedural and cached 9-slice high-resolution UI sprites generator (SDF-based antialiasing).
    /// Loads from Resources/UI/ModernTheme/ if available, with procedural fallback.
    /// </summary>
    public static class KaplaoUiSprites
    {
        public const string ResourcesThemeFolder = "UI/ModernTheme/";
        public const string AssetsThemeFolder = "Assets/Resources/UI/ModernTheme/";

        static readonly Dictionary<string, Sprite> SpriteCache = new();
        static readonly Dictionary<string, Texture2D> TextureCache = new();

        public static Sprite RoundedRect(int radius = 14)
        {
            string key = $"rounded_rect_r{radius}";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "rounded_rect");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            int size = 64;
            Texture2D tex = CreateSdfRoundedRectTexture(size, size, 14, 0);
            Vector4 border = new Vector4(18, 18, 18, 18);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = $"KaplaoUi_RoundedRect_r{radius}";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite RoundedBorder(int radius = 14, int thickness = 2)
        {
            string key = $"rounded_border_r{radius}_t{thickness}";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "rounded_border");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            int size = 64;
            Texture2D tex = CreateSdfRoundedBorderTexture(size, size, 14, thickness);
            Vector4 border = new Vector4(18, 18, 18, 18);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = $"KaplaoUi_RoundedBorder_r{radius}_t{thickness}";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite Pill()
        {
            const string key = "pill";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "pill");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            const int radius = 24;
            const int size = radius * 2 + 6;
            Texture2D tex = CreateSdfRoundedRectTexture(size, size, radius, 0);
            Vector4 border = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "KaplaoUi_Pill";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite SoftShadow(int radius = 18, int spread = 16)
        {
            string key = $"soft_shadow_r{radius}_s{spread}";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "soft_shadow");
            if (res != null && radius == 18 && spread == 16)
            {
                SpriteCache[key] = res;
                return res;
            }

            int totalPad = radius + spread + 4;
            int size = totalPad * 2;
            Texture2D tex = CreateSoftShadowTexture(size, size, radius, spread);
            Vector4 border = new Vector4(totalPad, totalPad, totalPad, totalPad);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = $"KaplaoUi_SoftShadow_r{radius}_s{spread}";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite TopGradientOverlay()
        {
            const string key = "top_gradient_overlay";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "top_glow");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            const int size = 64;
            Texture2D tex = CreateSdfTopGlowTexture(size, size, 14);
            Vector4 border = new Vector4(16, 16, 16, 16);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "KaplaoUi_TopGradientOverlay";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite Circle()
        {
            const string key = "circle";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "circle");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            const int size = 64;
            const float radius = 29f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            TextureCache[key] = tex;

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "KaplaoUi_Circle";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite Checkmark()
        {
            const string key = "checkmark";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "checkmark");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            const int size = 48;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            // Draw crisp checkmark line segments: P0 -> P1 -> P2
            Vector2 p0 = new Vector2(10f, 22f);
            Vector2 p1 = new Vector2(20f, 12f);
            Vector2 p2 = new Vector2(38f, 34f);
            const float strokeWidth = 3.6f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    float d0 = DistanceToSegment(pt, p0, p1);
                    float d1 = DistanceToSegment(pt, p1, p2);
                    float minD = Mathf.Min(d0, d1);
                    float alpha = Mathf.Clamp01((strokeWidth * 0.5f + 0.5f) - minD);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            TextureCache[key] = tex;

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "KaplaoUi_Checkmark";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite ChevronDown()
        {
            const string key = "chevron_down";
            if (TryGetCached(key, out Sprite cached)) return cached;

            Sprite res = Resources.Load<Sprite>(ResourcesThemeFolder + "chevron_down");
            if (res != null)
            {
                SpriteCache[key] = res;
                return res;
            }

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            Vector2 p0 = new Vector2(8f, 19f);
            Vector2 p1 = new Vector2(16f, 11f);
            Vector2 p2 = new Vector2(24f, 19f);
            const float strokeWidth = 2.4f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    float d0 = DistanceToSegment(pt, p0, p1);
                    float d1 = DistanceToSegment(pt, p1, p2);
                    float minD = Mathf.Min(d0, d1);
                    float alpha = Mathf.Clamp01((strokeWidth * 0.5f + 0.5f) - minD);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            TextureCache[key] = tex;

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "KaplaoUi_ChevronDown";
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[key] = sprite;
            return sprite;
        }

        static bool TryGetCached(string key, out Sprite sprite)
        {
            if (SpriteCache.TryGetValue(key, out sprite) && sprite != null)
                return true;
            return false;
        }

        static Texture2D CreateSdfRoundedRectTexture(int width, int height, float radius, float inset)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            float halfW = (width - 1) * 0.5f;
            float halfH = (height - 1) * 0.5f;
            float innerW = halfW - radius;
            float innerH = halfH - radius;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = Mathf.Max(0f, Mathf.Abs(y - halfH) - innerH);
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - halfW) - innerW);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - d);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D CreateSdfRoundedBorderTexture(int width, int height, float radius, float thickness)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            float halfW = (width - 1) * 0.5f;
            float halfH = (height - 1) * 0.5f;
            float innerW = halfW - radius;
            float innerH = halfH - radius;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = Mathf.Max(0f, Mathf.Abs(y - halfH) - innerH);
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - halfW) - innerW);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerAlpha = Mathf.Clamp01(radius + 0.5f - d);
                    float innerAlpha = Mathf.Clamp01(d - (radius - thickness - 0.5f));
                    float alpha = outerAlpha * innerAlpha;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D CreateSoftShadowTexture(int width, int height, float radius, float spread)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            float halfW = (width - 1) * 0.5f;
            float halfH = (height - 1) * 0.5f;
            float innerW = halfW - radius - spread;
            float innerH = halfH - radius - spread;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = Mathf.Max(0f, Mathf.Abs(y - halfH) - innerH);
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - halfW) - innerW);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 0f;
                    if (d <= radius)
                    {
                        alpha = 1f;
                    }
                    else if (d <= radius + spread)
                    {
                        float t = (d - radius) / spread;
                        // Smooth cubic falloff
                        alpha = Mathf.Clamp01(1f - (3f * t * t - 2f * t * t * t));
                    }
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D CreateSdfTopGlowTexture(int width, int height, float radius)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;

            float halfW = (width - 1) * 0.5f;
            float halfH = (height - 1) * 0.5f;
            float innerW = halfW - radius;
            float innerH = halfH - radius;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = Mathf.Max(0f, Mathf.Abs(y - halfH) - innerH);
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - halfW) - innerW);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float sdfAlpha = Mathf.Clamp01(radius + 0.5f - d);

                    // Gradient only in the top half
                    float topGradAlpha = 0f;
                    if (y >= height / 2)
                    {
                        float t = (float)(y - height / 2) / (height - 1 - height / 2);
                        topGradAlpha = Mathf.Pow(t, 1.4f);
                    }

                    float alpha = sdfAlpha * topGradAlpha;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 projection = a + t * ab;
            return Vector2.Distance(p, projection);
        }

        public static void ClearCache()
        {
            SpriteCache.Clear();
            TextureCache.Clear();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Generates and exports all modern theme 9-slice textures to Assets/Resources/UI/ModernTheme/.
        /// Configures TextureImporter with appropriate sprite borders and bilinear filter.
        /// </summary>
        public static void ExportSpritesToResources()
        {
            if (!Directory.Exists(AssetsThemeFolder))
                Directory.CreateDirectory(AssetsThemeFolder);

            // 1. rounded_rect.png (size=64x64, r=14)
            SaveTextureAndImportSprite("rounded_rect", CreateSdfRoundedRectTexture(64, 64, 14, 0), new Vector4(18, 18, 18, 18));

            // 2. rounded_border.png (size=64x64, r=14, t=2)
            SaveTextureAndImportSprite("rounded_border", CreateSdfRoundedBorderTexture(64, 64, 14, 2), new Vector4(18, 18, 18, 18));

            // 3. pill.png (r=24)
            SaveTextureAndImportSprite("pill", CreateSdfRoundedRectTexture(54, 54, 24, 0), new Vector4(26, 26, 26, 26));

            // 4. soft_shadow.png (r=18, s=16)
            int shadowPad = 18 + 16 + 4;
            SaveTextureAndImportSprite("soft_shadow", CreateSoftShadowTexture(shadowPad * 2, shadowPad * 2, 18, 16), new Vector4(shadowPad, shadowPad, shadowPad, shadowPad));

            // 5. top_glow.png (size=64x64, r=14)
            SaveTextureAndImportSprite("top_glow", CreateSdfTopGlowTexture(64, 64, 14), new Vector4(16, 16, 16, 16));

            // 6. circle.png
            SaveTextureAndImportSprite("circle", CreateCircleTexture(64, 29f), Vector4.zero);

            // 7. checkmark.png
            SaveTextureAndImportSprite("checkmark", CreateCheckmarkTexture(48), Vector4.zero);

            // 8. chevron_down.png
            SaveTextureAndImportSprite("chevron_down", CreateChevronDownTexture(32), Vector4.zero);

            AssetDatabase.Refresh();
            ClearCache();
            Debug.Log("[KaplaoUiSprites] Modern UI Theme sprites exported successfully to " + AssetsThemeFolder);
        }

        static Texture2D CreateCircleTexture(int size, float radius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D CreateCheckmarkTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 p0 = new Vector2(10f, 22f);
            Vector2 p1 = new Vector2(20f, 12f);
            Vector2 p2 = new Vector2(38f, 34f);
            const float strokeWidth = 3.6f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    float d0 = DistanceToSegment(pt, p0, p1);
                    float d1 = DistanceToSegment(pt, p1, p2);
                    float minD = Mathf.Min(d0, d1);
                    float alpha = Mathf.Clamp01((strokeWidth * 0.5f + 0.5f) - minD);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D CreateChevronDownTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 p0 = new Vector2(8f, 19f);
            Vector2 p1 = new Vector2(16f, 11f);
            Vector2 p2 = new Vector2(24f, 19f);
            const float strokeWidth = 2.4f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    float d0 = DistanceToSegment(pt, p0, p1);
                    float d1 = DistanceToSegment(pt, p1, p2);
                    float minD = Mathf.Min(d0, d1);
                    float alpha = Mathf.Clamp01((strokeWidth * 0.5f + 0.5f) - minD);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static void SaveTextureAndImportSprite(string fileName, Texture2D texture, Vector4 border)
        {
            string path = Path.Combine(AssetsThemeFolder, fileName + ".png");
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
#endif
    }
}
