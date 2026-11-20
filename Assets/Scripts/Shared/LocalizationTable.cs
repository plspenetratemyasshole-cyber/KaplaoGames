using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Key-based localization. Add entries in Inspector — no if (lang) in gameplay code.
    /// Place asset at Resources/LocalizationTable or assign on GameLocalization.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Kaplao/Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public string english;
            public string russian;
        }

        [SerializeField] Entry[] entries;

        static LocalizationTable cached;

        public static LocalizationTable Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<LocalizationTable>("LocalizationTable");
            return cached;
        }

        public static void ClearCache()
        {
            cached = null;
        }

        public string Get(string key, GameLanguage language)
        {
            if (string.IsNullOrWhiteSpace(key) || entries == null)
                return key ?? string.Empty;

            for (int i = 0; i < entries.Length; i++)
            {
                if (!string.Equals(entries[i].key, key, StringComparison.Ordinal))
                    continue;

                return language == GameLanguage.Russian
                    ? Pick(entries[i].russian, entries[i].english, key)
                    : Pick(entries[i].english, entries[i].russian, key);
            }

            return key;
        }

        static string Pick(string primary, string fallback, string key)
        {
            if (!string.IsNullOrWhiteSpace(primary))
                return primary;
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;
            return key;
        }
    }

    public static class GameLocalization
    {
        public static string Get(string key)
        {
            LocalizationTable table = LocalizationTable.Load();
            if (table == null)
                return key ?? string.Empty;

            return table.Get(key, LanguagePreference.SelectedLanguage);
        }
    }
}
