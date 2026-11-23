using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Stable runner skin indices and admin-grant skin ids (must match KaplaoGamesWebsite runner-skins.ts).
    /// </summary>
    public static class RunnerSkinCatalog
    {
        public const int PublicSkinCount = 6;

        public static readonly string[] AdminSkinIds =
        {
            "admin_dude_monster",
            "admin_owlet_monster",
            "admin_pink_monster",
        };

        public static int AdminSkinCount => AdminSkinIds.Length;
        public static int TotalSkinCount => PublicSkinCount + AdminSkinCount;

        public static bool IsPublicSkinIndex(int index) => index >= 0 && index < PublicSkinCount;

        public static bool IsAdminSkinIndex(int index) =>
            index >= PublicSkinCount && index < TotalSkinCount;

        public static string GetAdminSkinId(int index)
        {
            if (!IsAdminSkinIndex(index))
                return null;

            return AdminSkinIds[index - PublicSkinCount];
        }

        public static int GetAdminSkinIndex(string skinId)
        {
            if (string.IsNullOrWhiteSpace(skinId))
                return -1;

            string key = skinId.Trim().ToLowerInvariant();
            for (int i = 0; i < AdminSkinIds.Length; i++)
            {
                if (AdminSkinIds[i] == key)
                    return PublicSkinCount + i;
            }

            return -1;
        }

        public static bool OwnsAdminSkin(IReadOnlyList<string> ownedSkinIds, int index)
        {
            string skinId = GetAdminSkinId(index);
            if (skinId == null || ownedSkinIds == null)
                return false;

            for (int i = 0; i < ownedSkinIds.Count; i++)
            {
                if (string.Equals(ownedSkinIds[i], skinId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsSkinVisibleInSettings(int index, IReadOnlyList<string> ownedSkinIds)
        {
            if (IsPublicSkinIndex(index))
                return true;

            return IsAdminSkinIndex(index) && OwnsAdminSkin(ownedSkinIds, index);
        }

        public static int PickRandomPublicIndex()
        {
            return UnityEngine.Random.Range(0, PublicSkinCount);
        }

        public static int ResolveServerSkinIndex(
            int preferredIndex,
            IReadOnlyList<string> ownedSkinIds,
            bool ownershipVerified,
            bool allowUnverifiedAdminSkin = false)
        {
            if (preferredIndex < 0)
                return PickRandomPublicIndex();

            if (IsPublicSkinIndex(preferredIndex))
                return preferredIndex;

            if (IsAdminSkinIndex(preferredIndex))
            {
                if (ownershipVerified)
                    return OwnsAdminSkin(ownedSkinIds, preferredIndex) ? preferredIndex : PickRandomPublicIndex();

                if (!ownershipVerified && allowUnverifiedAdminSkin)
                    return preferredIndex;

                return PickRandomPublicIndex();
            }

            return PickRandomPublicIndex();
        }
    }
}
