using System;
using KaplaoGames.Server;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Receives network moderation/chat events from <see cref="LevelSessionServer"/> and exposes them for UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class ModerationUiHooksClient : MonoBehaviour
    {
        public event Action<string, ulong> SenderModerationPending;
        public event Action<string, string, ulong> ModeratorModerationRequest;
        public event Action<bool, ulong> SenderModerationResult;
        public event Action<string, string> ChatMessageReceived;

        void Awake()
        {
            RegisterHooks();
        }

        void OnEnable()
        {
            RegisterHooks();
        }

        void RegisterHooks()
        {
            if (LevelSessionServer.ModerationUiHooks != null && LevelSessionServer.ModerationUiHooks != this)
            {
                Debug.LogWarning("[ModerationUiHooksClient] Duplicate instance destroyed.");
                Destroy(this);
                return;
            }

            LevelSessionServer.ModerationUiHooks = this;
        }

        void OnDestroy()
        {
            if (LevelSessionServer.ModerationUiHooks == this)
                LevelSessionServer.ModerationUiHooks = null;
        }

        // Called from TargetRPC/ClientRPC inside LevelSessionServer.
        public void OnSenderModerationPending(NetworkConnectionToClient target, string moderatorDisplayName, ulong requestId)
        {
            SenderModerationPending?.Invoke(moderatorDisplayName, requestId);
        }

        public void OnModeratorModerationRequest(NetworkConnectionToClient target, string senderDisplayName, string message, ulong requestId)
        {
            ModeratorModerationRequest?.Invoke(senderDisplayName, message, requestId);
        }

        public void OnSenderModerationResult(NetworkConnectionToClient target, bool accepted, ulong requestId)
        {
            SenderModerationResult?.Invoke(accepted, requestId);
        }

        public void OnChatMessage(string senderDisplayName, string message)
        {
            ChatMessageReceived?.Invoke(senderDisplayName, message);
        }
    }
}

