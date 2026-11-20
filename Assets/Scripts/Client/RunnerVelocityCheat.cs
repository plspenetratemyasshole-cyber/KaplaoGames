using System;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local-only runner cheat: chat command "velocity" toggles speed readout above the runner.
    /// </summary>
    public static class RunnerVelocityCheat
    {
        public static bool ShowVelocity { get; private set; }

        public static event Action Changed;

        public static bool TryHandleChatCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!string.Equals(text.Trim(), "velocity", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!IsLocalRunner())
                return false;

            ShowVelocity = !ShowVelocity;
            EnsureUiOnLocalRunner()?.SetVisible(ShowVelocity);
            Changed?.Invoke();
            return true;
        }

        static RunnerVelocityCheatUi EnsureUiOnLocalRunner()
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return null;

            RunnerVelocityCheatUi ui = NetworkClient.localPlayer.GetComponent<RunnerVelocityCheatUi>();
            if (ui == null)
                ui = NetworkClient.localPlayer.gameObject.AddComponent<RunnerVelocityCheatUi>();

            return ui;
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
