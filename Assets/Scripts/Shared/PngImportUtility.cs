using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public struct PngImportColorSlice
    {
        public Color32 Color;
        public Vector2 Scale;
        public Vector2 Position;
        public int CellX;
        public int CellY;
    }

    public static class PngImportUtility
    {
        public static bool TryLoadTexture(byte[] pngBytes, out Texture2D texture, out string error)
        {
            texture = null;
            error = null;

            if (pngBytes == null || pngBytes.Length == 0)
            {
                error = "PNG data is empty.";
                return false;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(pngBytes, markNonReadable: false))
            {
                Object.Destroy(texture);
                texture = null;
                error = "Failed to decode PNG.";
                return false;
            }

            ApplyNoSmoothing(texture);
            return true;
        }

        public static void ApplyNoSmoothing(Texture2D texture)
        {
            if (texture == null)
                return;

            texture.filterMode = FilterMode.Point;
            texture.anisoLevel = 0;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.mipMapBias = -10f;
        }

        public static bool TryComputeCellScales(
            int imageWidth,
            int imageHeight,
            int tilesX,
            int tilesY,
            out float scaleX,
            out float scaleY,
            out string error)
        {
            scaleX = 1f;
            scaleY = 1f;
            error = null;

            if (imageWidth <= 0 || imageHeight <= 0)
            {
                error = "Image size is invalid.";
                return false;
            }

            if (tilesX <= 0 || tilesY <= 0)
            {
                error = "Tile count must be greater than zero.";
                return false;
            }

            int cellPixelW = imageWidth / tilesX;
            int cellPixelH = imageHeight / tilesY;
            if (cellPixelW <= 0 || cellPixelH <= 0)
            {
                error = "Tile count is too high for this image.";
                return false;
            }

            float minCell = Mathf.Min(cellPixelW, cellPixelH);
            scaleX = cellPixelW / minCell;
            scaleY = cellPixelH / minCell;
            return true;
        }

        public static bool TryBuildColorSlices(
            Texture2D texture,
            int tilesX,
            int tilesY,
            Vector2 worldCenter,
            List<PngImportColorSlice> output,
            out string error)
        {
            error = null;
            output?.Clear();

            if (texture == null)
            {
                error = "Texture is missing.";
                return false;
            }

            int imageWidth = texture.width;
            int imageHeight = texture.height;
            if (!TryComputeCellScales(imageWidth, imageHeight, tilesX, tilesY, out float scaleX, out float scaleY, out error))
                return false;

            int cellPixelW = imageWidth / tilesX;
            int cellPixelH = imageHeight / tilesY;
            float gridWidth = tilesX * scaleX;
            float gridHeight = tilesY * scaleY;
            Color32[] allPixels = texture.GetPixels32();

            for (int iy = 0; iy < tilesY; iy++)
            {
                for (int ix = 0; ix < tilesX; ix++)
                {
                    if (!TryAverageCellColor(allPixels, imageWidth, tilesY, ix, iy, cellPixelW, cellPixelH, out Color32 color))
                        continue;

                    float posX = worldCenter.x - gridWidth * 0.5f + (ix + 0.5f) * scaleX;
                    float posY = worldCenter.y + gridHeight * 0.5f - (iy + 0.5f) * scaleY;

                    output.Add(new PngImportColorSlice
                    {
                        Color = color,
                        Scale = new Vector2(scaleX, scaleY),
                        Position = new Vector2(posX, posY),
                        CellX = ix,
                        CellY = iy
                    });
                }
            }

            if (output.Count == 0)
            {
                error = "PNG has no visible tiles.";
                return false;
            }

            return true;
        }

        static bool TryAverageCellColor(
            Color32[] pixels,
            int imageWidth,
            int tilesY,
            int cellX,
            int cellY,
            int cellPixelW,
            int cellPixelH,
            out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);
            int x0 = cellX * cellPixelW;
            int y0 = (tilesY - 1 - cellY) * cellPixelH;
            int imageHeight = tilesY * cellPixelH;

            long sumR = 0;
            long sumG = 0;
            long sumB = 0;
            int count = 0;

            for (int y = 0; y < cellPixelH; y++)
            {
                int row = y0 + y;
                if (row < 0 || row >= imageHeight)
                    continue;

                for (int x = 0; x < cellPixelW; x++)
                {
                    int col = x0 + x;
                    if (col < 0 || col >= imageWidth)
                        continue;

                    Color32 pixel = pixels[row * imageWidth + col];
                    if (pixel.a == 0)
                        continue;

                    sumR += pixel.r;
                    sumG += pixel.g;
                    sumB += pixel.b;
                    count++;
                }
            }

            if (count == 0)
                return false;

            color = new Color32(
                (byte)(sumR / count),
                (byte)(sumG / count),
                (byte)(sumB / count),
                255);
            return true;
        }
    }
}
