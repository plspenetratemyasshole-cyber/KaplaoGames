using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Kaplao input — free movement on the X/Y plane (side view).
    /// </summary>
    public struct KaplaoInput
    {
        public float horizontal;
        public float vertical;
        public bool shiftHeld;
    }

    /// <summary>
    /// Kaplao cursor input — mouse position, primary button, and active tool mode.
    /// </summary>
    public struct KaplaoCursorInput
    {
        public Vector2 worldPosition;
        public bool primaryHeld;
        public bool primaryPressed;
        public bool primaryReleased;
        public bool shiftHeld;
        public bool ctrlHeld;
        public KaplaoToolMode toolMode;
        public Color32 placeColor;
        public Vector2 selectionDragStart;
        public bool selectionDragging;
        public bool secondaryPressed;
        public bool secondaryReleased;
        public bool transformRotatePressed;
        public bool transformMovePressed;
        public bool transformScalePressed;
        public bool axisXPressed;
        public bool axisYPressed;
        public byte spikeQuarterTurns;
    }

    /// <summary>
    /// Runner input — platformer controls.
    /// </summary>
    public struct RunnerInput
    {
        public float horizontal;
        public bool jump;
        public bool fastFall;
    }
}
