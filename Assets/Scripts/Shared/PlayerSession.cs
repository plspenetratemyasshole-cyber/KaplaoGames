using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Local session data for menu and networking (nickname, port, server name, account).
    /// </summary>
    public static class PlayerSession
    {
        const string NicknameKey = "kaplao.player.nickname";
        const string PortKey = "kaplao.server.port";
        const string LanInterfaceIpKey = "kaplao.lan.interface.ip";
        const string UseTcpTransportKey = "kaplao.transport.tcp";
        const string AuthTokenKey = "kaplao.account.token";
        const string AuthEmailKey = "kaplao.account.email";
        const string AuthLoggedInKey = "kaplao.account.loggedIn";
        const string NicknamePresetKey = "kaplao.player.nicknamePreset";

        public const string DefaultNickname = "Player";
        public const ushort DefaultPort = 25565;

        public static string Nickname { get; private set; } = DefaultNickname;
        public static ushort Port { get; private set; } = DefaultPort;
        public static string ServerDisplayName { get; private set; } = "Kaplao Server";
        public static string LanInterfaceIPv4 { get; private set; } = string.Empty;
        public static bool UseTcpTransport { get; private set; }

        public static bool IsLoggedIn { get; private set; }
        public static string AccountEmail { get; private set; } = string.Empty;
        public static string AuthToken { get; private set; } = string.Empty;
        public static string CachedNicknamePreset { get; private set; } = "none";

        public static event System.Action NicknamePresetChanged;

        public static bool IsDefaultNickname =>
            string.Equals(Nickname, DefaultNickname, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Masked email for UI, e.g. as*****@gmail.com
        /// </summary>
        public static string MaskedAccountEmail => MaskEmail(AccountEmail);

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            email = email.Trim();
            int at = email.IndexOf('@');
            if (at <= 0 || at >= email.Length - 1)
                return "***";

            string local = email.Substring(0, at);
            string domain = email.Substring(at);
            string visible = local.Length <= 2 ? local : local.Substring(0, 2);
            int starCount = Mathf.Max(3, local.Length - visible.Length);
            return visible + new string('*', starCount) + domain;
        }

        public static void Load()
        {
            Nickname = PlayerPrefs.GetString(NicknameKey, DefaultNickname);
            Port = (ushort)PlayerPrefs.GetInt(PortKey, DefaultPort);
            LanInterfaceIPv4 = PlayerPrefs.GetString(LanInterfaceIpKey, string.Empty);
            UseTcpTransport = PlayerPrefs.GetInt(UseTcpTransportKey, 0) != 0;
            IsLoggedIn = PlayerPrefs.GetInt(AuthLoggedInKey, 0) != 0;
            AccountEmail = PlayerPrefs.GetString(AuthEmailKey, string.Empty);
            AuthToken = PlayerPrefs.GetString(AuthTokenKey, string.Empty);
            CachedNicknamePreset = PlayerPrefs.GetString(NicknamePresetKey, "none");
            RunnerSkinPreference.Load();
            KaplaoAudioPreference.Load();
            if (IsLoggedIn && (string.IsNullOrWhiteSpace(AuthToken) || string.IsNullOrWhiteSpace(Nickname)))
                ClearAccount();
            UpdateServerDisplayName();
        }

        public static void SetNickname(string nickname)
        {
            if (IsLoggedIn)
                return;

            Nickname = SanitizeNickname(nickname);
            PlayerPrefs.SetString(NicknameKey, Nickname);
            PlayerPrefs.Save();
            UpdateServerDisplayName();
        }

        public static void SetAccount(string nickname, string email, string token)
        {
            Nickname = SanitizeNickname(nickname);
            AccountEmail = (email ?? string.Empty).Trim();
            AuthToken = token ?? string.Empty;
            // Token + nickname is enough to treat as signed in (email may be empty in edge cases).
            IsLoggedIn = !string.IsNullOrWhiteSpace(AuthToken) && !string.IsNullOrWhiteSpace(Nickname);

            PlayerPrefs.SetString(NicknameKey, Nickname);
            PlayerPrefs.SetString(AuthEmailKey, AccountEmail);
            PlayerPrefs.SetString(AuthTokenKey, AuthToken);
            PlayerPrefs.SetInt(AuthLoggedInKey, IsLoggedIn ? 1 : 0);
            PlayerPrefs.Save();
            UpdateServerDisplayName();
        }

        public static void SetCachedNicknamePreset(string preset)
        {
            string normalized = string.IsNullOrWhiteSpace(preset) || preset.Equals("own", System.StringComparison.OrdinalIgnoreCase)
                ? "none"
                : preset.Trim().ToLowerInvariant();

            if (CachedNicknamePreset != normalized)
            {
                CachedNicknamePreset = normalized;
                PlayerPrefs.SetString(NicknamePresetKey, normalized);
                PlayerPrefs.Save();
                NicknamePresetChanged?.Invoke();
            }
        }

        public static void ClearAccount()
        {
            IsLoggedIn = false;
            AccountEmail = string.Empty;
            AuthToken = string.Empty;
            CachedNicknamePreset = "none";
            RunnerSkinPreference.ClearOwnedAdminSkinIds();
            PlayerPrefs.DeleteKey(AuthEmailKey);
            PlayerPrefs.DeleteKey(AuthTokenKey);
            PlayerPrefs.DeleteKey(NicknamePresetKey);
            PlayerPrefs.SetInt(AuthLoggedInKey, 0);
            PlayerPrefs.Save();
            NicknamePresetChanged?.Invoke();
        }

        public static void SignOutKeepLocalNickname()
        {
            ClearAccount();
        }

        public static void SetPort(ushort port)
        {
            Port = port == 0 ? DefaultPort : port;
            PlayerPrefs.SetInt(PortKey, Port);
            PlayerPrefs.Save();
        }

        public static void SetLanInterfaceIPv4(string ipv4)
        {
            LanInterfaceIPv4 = string.IsNullOrWhiteSpace(ipv4) ? string.Empty : ipv4.Trim();
            PlayerPrefs.SetString(LanInterfaceIpKey, LanInterfaceIPv4);
            PlayerPrefs.Save();
        }

        public static void SetUseTcpTransport(bool useTcp)
        {
            UseTcpTransport = useTcp;
            PlayerPrefs.SetInt(UseTcpTransportKey, useTcp ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool TrySetPort(string portText, out ushort port)
        {
            if (!ushort.TryParse(portText, out port))
            {
                port = DefaultPort;
                return false;
            }

            SetPort(port);
            return true;
        }

        public static void UpdateServerDisplayName()
        {
            ServerDisplayName = $"{Nickname}'s Server";
        }

        public static string SanitizeNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return DefaultNickname;

            nickname = nickname.Trim();
            return nickname.Length > 20 ? nickname.Substring(0, 20) : nickname;
        }
    }
}
