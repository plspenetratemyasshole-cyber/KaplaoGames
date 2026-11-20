using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class LevelFileSerializer
    {
        public static string ToJson(LevelData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static bool TryParse(string json, out LevelData data, out string error)
        {
            data = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Level file is empty.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<LevelData>(json);
            }
            catch (System.Exception ex)
            {
                error = $"Invalid level file: {ex.Message}";
                return false;
            }

            if (data == null)
            {
                error = "Invalid level file.";
                return false;
            }

            if (data.tiles == null)
                data.tiles = System.Array.Empty<LevelTileRecord>();

            return true;
        }
    }
}
