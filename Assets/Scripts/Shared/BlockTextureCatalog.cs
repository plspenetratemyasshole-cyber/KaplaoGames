using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Shared
{
    [CreateAssetMenu(fileName = "BlockTextureCatalog", menuName = "Kaplao/Block Texture Catalog")]
    public sealed class BlockTextureCatalog : ScriptableObject
    {
        const string DefaultResourcePath = "BlockTextureCatalog";

        [Serializable]
        public struct Entry
        {
            public string id;
            public string displayName;
            public Sprite sprite;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        static BlockTextureCatalog cachedInstance;
        Dictionary<string, Entry> lookup;

        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();

        public static BlockTextureCatalog Instance
        {
            get
            {
                if (cachedInstance != null)
                    return cachedInstance;

                cachedInstance = Resources.Load<BlockTextureCatalog>(DefaultResourcePath);
                if (cachedInstance != null)
                    cachedInstance.BuildLookup();

                return cachedInstance;
            }
        }

        public static void SetRuntimeInstance(BlockTextureCatalog catalog)
        {
            cachedInstance = catalog;
            catalog?.BuildLookup();
        }

        void OnEnable() => BuildLookup();

        public void SetEntries(Entry[] newEntries)
        {
            entries = newEntries ?? Array.Empty<Entry>();
            BuildLookup();
        }

        void BuildLookup()
        {
            lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.id))
                    continue;

                lookup[entry.id] = entry;
            }
        }

        public bool TryGetEntry(string textureId, out Entry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(textureId))
                return false;

            if (lookup == null)
                BuildLookup();

            return lookup.TryGetValue(textureId, out entry);
        }

        public bool TryGetSprite(string textureId, out Sprite sprite)
        {
            sprite = null;
            if (!TryGetEntry(textureId, out Entry entry))
                return false;

            sprite = entry.sprite;
            return sprite != null;
        }
    }
}
