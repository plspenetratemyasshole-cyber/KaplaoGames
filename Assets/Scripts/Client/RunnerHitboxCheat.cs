using System;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local-only runner cheat: chat command "Hitbox" toggles collider outlines
    /// (tiles + players), without Hitbox Trail.
    /// </summary>
    public static class RunnerHitboxCheat
    {
        public static bool ShowHitboxes { get; private set; }

        public static event Action Changed;

        public static bool TryHandleChatCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!string.Equals(text.Trim(), "Hitbox", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!IsLocalRunner())
                return false;

            ShowHitboxes = !ShowHitboxes;
            Changed?.Invoke();
            return true;
        }

        static bool IsLocalRunner()
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return false;

            return NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player) &&
                   player.role == PlayerRole.Runner;
        }
    }
}
