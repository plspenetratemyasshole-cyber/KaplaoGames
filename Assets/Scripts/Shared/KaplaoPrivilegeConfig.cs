using System;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Optional list of privileged emails (non-cosmetic helpers).
    /// Nickname visuals are configured on the website; fonts live in KaplaoNicknameFontRegistry.
    /// </summary>
    [CreateAssetMenu(fileName = "KaplaoPrivilegeConfig", menuName = "Kaplao/Privilege Config")]
    public class KaplaoPrivilegeConfig : ScriptableObject
    {
        public const string ResourcePath = "KaplaoPrivilegeConfig";
        public const string DefaultPrivilegedEmail = "asmoloff55@gmail.com";

        static KaplaoPrivilegeConfig cached;

        [Header("Privileged Emails (optional)")]
        [Tooltip("Legacy helper list. Nickname styling is no longer configured here.")]
        [SerializeField] string[] privilegedEmails = { DefaultPrivilegedEmail };

        public static KaplaoPrivilegeConfig Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<KaplaoPrivilegeConfig>(ResourcePath);
            return cached;
        }

        public static void ClearCache()
        {
            cached = null;
        }

        public bool IsPrivilegedEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim();
            if (privilegedEmails == null || privilegedEmails.Length == 0)
            {
                return string.Equals(email, DefaultPrivilegedEmail, StringComparison.OrdinalIgnoreCase);
            }

            for (int i = 0; i < privilegedEmails.Length; i++)
            {
                string entry = privilegedEmails[i];
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                if (string.Equals(email, entry.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsNonePresetName(string presetName)
        {
            return KaplaoNicknamePresetCatalog.IsNonePreset(presetName);
        }

        [Obsolete("Use KaplaoNicknamePresetCatalog.IsNonePreset / IsNonePresetName.")]
        public static bool IsOwnPresetName(string presetName)
        {
            return IsNonePresetName(presetName);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            cached = this;
        }
#endif
    }
}
