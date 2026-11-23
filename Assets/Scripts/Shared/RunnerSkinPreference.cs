using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Local runner skin preference from Settings.
    /// -1 = random; 0..N-1 = fixed character from RunnerCharacterLibrary.
    /// </summary>
    public static class RunnerSkinPreference
    {
        public const int RandomIndex = -1;
        const string PrefsKey = "kaplao.runner.skinIndex";
        const string OwnedAdminSkinsPrefsKey = "kaplao.runner.ownedAdminSkins";

        static readonly List<string> OwnedAdminSkinIds = new List<string>();

        public static int SelectedIndex { get; private set; } = RandomIndex;
        public static bool SkinsLoadedFromServer { get; private set; }

        public static bool IsRandom => SelectedIndex < 0;

        public static IReadOnlyList<string> OwnedAdminSkinIdsReadOnly => OwnedAdminSkinIds;

        public static void Load()
        {
            SelectedIndex = PlayerPrefs.GetInt(PrefsKey, RandomIndex);
            if (OwnedAdminSkinIds.Count == 0)
            {
                string cached = PlayerPrefs.GetString(OwnedAdminSkinsPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    string[] tokens = cached.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string t in tokens)
                    {
                        string norm = t.Trim().ToLowerInvariant();
                        if (!string.IsNullOrEmpty(norm) && !OwnedAdminSkinIds.Contains(norm))
                            OwnedAdminSkinIds.Add(norm);
                    }
                }
            }
        }

        public static void SetSelectedIndex(int index)
        {
            SelectedIndex = index < 0 ? RandomIndex : index;
            PlayerPrefs.SetInt(PrefsKey, SelectedIndex);
            PlayerPrefs.Save();
        }

        public static void SetOwnedAdminSkinIds(IEnumerable<string> skinIds)
        {
            OwnedAdminSkinIds.Clear();
            if (skinIds != null)
            {
                foreach (string skinId in skinIds)
                {
                    if (string.IsNullOrWhiteSpace(skinId))
                        continue;

                    string normalized = skinId.Trim().ToLowerInvariant();
                    if (OwnedAdminSkinIds.Contains(normalized))
                        continue;

                    OwnedAdminSkinIds.Add(normalized);
                }
            }

            SkinsLoadedFromServer = true;
            PlayerPrefs.SetString(OwnedAdminSkinsPrefsKey, string.Join(",", OwnedAdminSkinIds));
            PlayerPrefs.Save();
            ClampSelectedIndexToVisible();
        }

        public static void ClearOwnedAdminSkinIds()
        {
            OwnedAdminSkinIds.Clear();
            SkinsLoadedFromServer = false;
            PlayerPrefs.DeleteKey(OwnedAdminSkinsPrefsKey);
            PlayerPrefs.Save();

            if (RunnerSkinCatalog.IsAdminSkinIndex(SelectedIndex))
                SetSelectedIndex(RandomIndex);
        }

        public static bool IsSkinVisibleInSettings(int index)
        {
            if (RunnerSkinCatalog.IsPublicSkinIndex(index))
                return true;

            if (!RunnerSkinCatalog.IsAdminSkinIndex(index))
                return false;

            // Show locally selected admin skin immediately; server validates on world join.
            if (SelectedIndex == index)
                return true;

            return RunnerSkinCatalog.OwnsAdminSkin(OwnedAdminSkinIds, index);
        }

        public static void ClampSelectedIndexToVisible()
        {
            if (SelectedIndex < 0)
                return;

            if (RunnerSkinCatalog.IsPublicSkinIndex(SelectedIndex))
                return;

            if (!SkinsLoadedFromServer)
                return;

            if (RunnerSkinCatalog.OwnsAdminSkin(OwnedAdminSkinIds, SelectedIndex))
                return;

            SetSelectedIndex(RandomIndex);
        }

        public static int ResolveCharacterIndex(int libraryCount)
        {
            if (libraryCount <= 0)
                return 0;

            if (SelectedIndex < 0)
                return UnityEngine.Random.Range(0, Mathf.Min(RunnerSkinCatalog.PublicSkinCount, libraryCount));

            return Mathf.Clamp(SelectedIndex, 0, libraryCount - 1);
        }
    }
}
