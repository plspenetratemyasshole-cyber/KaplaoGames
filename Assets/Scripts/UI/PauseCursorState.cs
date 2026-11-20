namespace KaplaoGames.UI
{
    /// <summary>
    /// Shared pause flag so gameplay cursor code can yield to the OS cursor.
    /// </summary>
    public static class PauseCursorState
    {
        static int pauseDepth;

        public static bool IsPauseOpen => pauseDepth > 0;

        public static void SetPauseOpen(bool open)
        {
            if (open)
            {
                pauseDepth++;
                return;
            }

            pauseDepth = pauseDepth > 0 ? pauseDepth - 1 : 0;
        }
    }
}
