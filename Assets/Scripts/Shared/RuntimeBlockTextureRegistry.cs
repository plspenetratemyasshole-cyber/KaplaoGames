using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    public static class RuntimeBlockTextureRegistry
    {
        static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> OrderedIds = new();

        public static IReadOnlyList<string> Ids => OrderedIds;

        public static bool TryGetSprite(string textureId, out Sprite sprite)
        {
            if (string.IsNullOrEmpty(textureId))
            {
                sprite = null;
                return false;
            }

            return Sprites.TryGetValue(textureId, out sprite);
        }

        public static bool TryGetDisplayName(string textureId, out string displayName)
        {
            displayName = null;
            if (string.IsNullOrEmpty(textureId))
                return false;

            return DisplayNames.TryGetValue(textureId, out displayName);
        }

        public static void Register(string textureId, Sprite sprite, string displayName = null)
        {
            if (string.IsNullOrEmpty(textureId) || sprite == null)
                return;

            if (Sprites.TryGetValue(textureId, out Sprite existing) && existing != null && existing != sprite)
            {
                if (existing.texture != null)
                    UnityEngine.Object.Destroy(existing.texture);
                UnityEngine.Object.Destroy(existing);
            }

            if (!Sprites.ContainsKey(textureId))
                OrderedIds.Add(textureId);

            Sprites[textureId] = sprite;
            DisplayNames[textureId] = string.IsNullOrWhiteSpace(displayName) ? textureId : displayName.Trim();
        }

        public static void Clear()
        {
            foreach (KeyValuePair<string, Sprite> pair in Sprites)
            {
                if (pair.Value == null)
                    continue;

                if (pair.Value.texture != null)
                    UnityEngine.Object.Destroy(pair.Value.texture);
                UnityEngine.Object.Destroy(pair.Value);
            }

            Sprites.Clear();
            DisplayNames.Clear();
            OrderedIds.Clear();
        }

        public static List<BlockTextureCatalog.Entry> BuildPickerEntries()
        {
            var entries = new List<BlockTextureCatalog.Entry>(OrderedIds.Count);
            for (int i = 0; i < OrderedIds.Count; i++)
            {
                string id = OrderedIds[i];
                if (!Sprites.TryGetValue(id, out Sprite sprite) || sprite == null)
                    continue;

                DisplayNames.TryGetValue(id, out string displayName);
                entries.Add(new BlockTextureCatalog.Entry
                {
                    id = id,
                    displayName = string.IsNullOrEmpty(displayName) ? id : displayName,
                    sprite = sprite
                });
            }

            return entries;
        }

        public static bool TryRename(string oldTextureId, string newTextureId)
        {
            if (string.IsNullOrEmpty(oldTextureId) || string.IsNullOrEmpty(newTextureId))
                return false;

            if (!Sprites.TryGetValue(oldTextureId, out Sprite sprite) || sprite == null)
                return false;

            if (Sprites.ContainsKey(newTextureId))
                return false;

            Sprites.Remove(oldTextureId);
            DisplayNames.Remove(oldTextureId);

            for (int i = 0; i < OrderedIds.Count; i++)
            {
                if (string.Equals(OrderedIds[i], oldTextureId, StringComparison.OrdinalIgnoreCase))
                {
                    OrderedIds[i] = newTextureId;
                    break;
                }
            }

            Sprites[newTextureId] = sprite;
            DisplayNames[newTextureId] = newTextureId;
            return true;
        }
    }
}
