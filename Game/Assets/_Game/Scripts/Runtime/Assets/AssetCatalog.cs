using System;
using System.Collections.Generic;
using ANL.Runtime.Diagnostics;
using UnityEngine;

namespace ANL.Runtime.Assets
{
    [Serializable]
    public sealed class AssetRegion
    {
        public string regionId;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class AssetEntry
    {
        public string id;
        public Sprite sprite;
        public string kind;
        public float anchorX = 0.5f;
        public float anchorY = 0.5f;
        public AssetRegion[] regions;
        public string sha256;
    }

    [CreateAssetMenu(menuName = "ANL/Asset Catalog", fileName = "AssetCatalog")]
    public sealed class AssetCatalog : ScriptableObject
    {
        public AssetEntry[] entries;
    }

    public interface IAssetProvider
    {
        Sprite Get(string assetId);
        AssetEntry GetEntry(string assetId);
    }

    public sealed class CatalogAssetProvider : IAssetProvider
    {
        private readonly Dictionary<string, AssetEntry> _entries = new Dictionary<string, AssetEntry>();

        public CatalogAssetProvider(AssetCatalog catalog)
        {
            if (catalog == null || catalog.entries == null) return;
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                var entry = catalog.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                _entries[entry.id] = entry;
            }
        }

        public Sprite Get(string assetId)
        {
            var entry = GetEntry(assetId);
            if (entry == null)
            {
                Diag.Error("asset not found: " + (assetId ?? ""));
                return null;
            }
            if (entry.sprite == null)
            {
                Diag.Error("asset has no sprite: " + entry.id);
                return null;
            }
            return entry.sprite;
        }

        public AssetEntry GetEntry(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            return _entries.TryGetValue(assetId, out var entry) ? entry : null;
        }
    }
}
