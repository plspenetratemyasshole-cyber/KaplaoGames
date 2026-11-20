namespace KaplaoGames.Shared
{
    /// <summary>
    /// Selected world for the upcoming single-player session.
    /// </summary>
    public static class WorldSession
    {
        public static string ActiveWorldName { get; private set; }
        public static bool IsCreatingNewWorld { get; private set; }
        public static bool IsBrowsePlaySession { get; private set; }
        public static bool HostSpawnsAsRunner { get; private set; }

        public static bool HasActiveWorld => !string.IsNullOrWhiteSpace(ActiveWorldName);

        public static void SelectExistingWorld(string worldName)
        {
            ActiveWorldName = WorldSaveService.SanitizeWorldName(worldName);
            IsCreatingNewWorld = false;
            IsBrowsePlaySession = false;
            HostSpawnsAsRunner = false;
        }

        public static void SelectBrowsePlayWorld(string worldName)
        {
            ActiveWorldName = WorldSaveService.SanitizeWorldName(worldName);
            IsCreatingNewWorld = false;
            IsBrowsePlaySession = true;
            HostSpawnsAsRunner = true;
        }

        public static void BeginCreateWorld(string worldName)
        {
            ActiveWorldName = WorldSaveService.SanitizeWorldName(worldName);
            IsCreatingNewWorld = true;
            IsBrowsePlaySession = false;
            HostSpawnsAsRunner = false;
        }

        public static void Clear()
        {
            ActiveWorldName = null;
            IsCreatingNewWorld = false;
            IsBrowsePlaySession = false;
            HostSpawnsAsRunner = false;
        }
    }
}
