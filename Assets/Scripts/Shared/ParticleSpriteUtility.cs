using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class ParticleSpriteUtility
    {
        static Sprite defaultWhiteSquare;

        public static Sprite GetDefaultWhiteSquare()
        {
            if (defaultWhiteSquare != null)
                return defaultWhiteSquare;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            defaultWhiteSquare = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return defaultWhiteSquare;
        }

        public static Sprite ResolveSprite(string textureId)
        {
            if (!string.IsNullOrEmpty(textureId) &&
                RuntimeBlockTextureRegistry.TryGetSprite(textureId, out Sprite runtimeSprite))
            {
                return runtimeSprite;
            }

            if (!string.IsNullOrEmpty(textureId) &&
                BlockTextureCatalog.Instance != null &&
                BlockTextureCatalog.Instance.TryGetSprite(textureId, out Sprite sprite))
            {
                return sprite;
            }

            return GetDefaultWhiteSquare();
        }
    }
}
