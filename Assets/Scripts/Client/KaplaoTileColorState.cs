using System;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Shared place color for blocks and spikes. Wire your RGB UI to this component.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoTileColorState : NetworkBehaviour
    {
        [SerializeField] Color32 defaultColor = new Color32(255, 255, 255, 255);

        Color32 placeColor;

        public Color32 PlaceColor => placeColor;

        public event Action<Color32> ColorChanged;

        void Awake()
        {
            placeColor = defaultColor;
        }

        public void SetRgb(byte red, byte green, byte blue)
        {
            SetColor(new Color32(red, green, blue, 255));
        }

        public void SetRgb(int red, int green, int blue)
        {
            SetRgb(
                (byte)Mathf.Clamp(red, 0, 255),
                (byte)Mathf.Clamp(green, 0, 255),
                (byte)Mathf.Clamp(blue, 0, 255));
        }

        public void SetColor(Color32 color)
        {
            color.a = 255;
            if (placeColor.r == color.r && placeColor.g == color.g && placeColor.b == color.b)
                return;

            placeColor = color;
            ColorChanged?.Invoke(placeColor);
        }
    }
}
