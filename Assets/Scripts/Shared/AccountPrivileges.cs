using System;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Privileged email helpers. Nickname cosmetics come from website presets + font registry.
    /// </summary>
    public static class AccountPrivileges
    {
        public const string ProtectedEmail = KaplaoPrivilegeConfig.DefaultPrivilegedEmail;

        public static bool HasPrivilegedNicknameStyle(string email, string nicknamePreset = "none")
        {
            return KaplaoNicknamePresetCatalog.TryGetStyle(nicknamePreset, out _);
        }

        public static bool IsProtectedEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            KaplaoPrivilegeConfig config = KaplaoPrivilegeConfig.Load();
            if (config != null)
                return config.IsPrivilegedEmail(email);

            return string.Equals(
                email.Trim(),
                KaplaoPrivilegeConfig.DefaultPrivilegedEmail,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
