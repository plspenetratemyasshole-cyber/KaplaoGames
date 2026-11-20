using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class TileColorPalette
    {
        public static readonly Color32 DefaultPlace = new Color32(255, 255, 255, 255);

        /// <summary>Curated swatches for Create mode (readable on grid backgrounds).</summary>
        public static readonly Color32[] Presets =
        {
            new Color32(245, 245, 245, 255), // white
            new Color32(170, 170, 170, 255), // light gray
            new Color32(115, 107, 97, 255),  // stone (default)
            new Color32(74, 68, 62, 255),    // dark stone
            new Color32(42, 42, 48, 255),      // charcoal
            new Color32(18, 18, 22, 255),      // near black

            new Color32(220, 72, 72, 255),     // red
            new Color32(235, 130, 60, 255),    // orange
            new Color32(240, 200, 64, 255),    // yellow
            new Color32(110, 200, 88, 255),    // green
            new Color32(72, 170, 220, 255),    // sky blue
            new Color32(92, 110, 220, 255),    // blue
            new Color32(150, 92, 210, 255),    // purple
            new Color32(230, 110, 180, 255),   // pink

            new Color32(168, 118, 78, 255),    // wood
            new Color32(92, 148, 108, 255),    // moss
            new Color32(64, 188, 188, 255),    // teal
            new Color32(240, 240, 160, 255),   // cream
        };

        public static bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }
    }
}
