using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class GridMath
    {
        public const float CellSize = 1f;

        public static Vector2Int WorldToCell(Vector2 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt(world.x / CellSize),
                Mathf.RoundToInt(world.y / CellSize));
        }

        public static Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(cell.x * CellSize, cell.y * CellSize, 0f);
        }

        public static Vector3 TileCellToWorld(Vector2Int cell, TileType type, byte quarterTurns = 0)
        {
            return CellToWorld(cell);
        }
    }
}
