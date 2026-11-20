using System;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Saved UI language. Wire a Settings dropdown to SetLanguage / SelectedLanguage.
    /// </summary>
    public static class LanguagePreference
    {
        const string PrefKey = "kaplao_language";

        public static event Action Changed;

        public static GameLanguage SelectedLanguage
        {
            get
            {
                int raw = PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.English);
                if (raw < 0 || raw > (int)GameLanguage.Russian)
                    return GameLanguage.English;
                return (GameLanguage)raw;
            }
        }

        public static void SetLanguage(GameLanguage language)
        {
            if (SelectedLanguage == language)
                return;

            PlayerPrefs.SetInt(PrefKey, (int)language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Load() { /* PlayerPrefs read on demand */ }
    }
}
