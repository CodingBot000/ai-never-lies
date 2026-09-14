using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using ANL.Runtime.Assets;
using ANL.Runtime.Content;

namespace ANL.Editor
{
    public static class CatalogBuilder
    {
        private const string AssetCatalogPath = "Assets/_Game/Resources/AssetCatalog.asset";
        private const string ContentCatalogPath = "Assets/_Game/Resources/ContentCatalog.asset";
        private const string FontAssetPath = "Assets/_Game/Resources/Fonts/NanumGothic SDF.asset";
        private const string FontResourcePath = "Assets/_Game/Resources/Fonts/NanumGothic-Regular.ttf";
        private const string TmpSettingsPath = "Assets/_Game/Resources/TMP Settings.asset";

        [MenuItem("ANL/Catalogs/Build All")]
        public static void BuildAll()
        {
            BuildAssetCatalog();
            BuildContentCatalog();
            BuildFontAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ANL] catalogs built");
        }

        public static void BuildAssetCatalog()
        {
            var manifest = ArtManifestLoader.Load();
            var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(AssetCatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetCatalogPath));
                catalog = ScriptableObject.CreateInstance<AssetCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetCatalogPath);
            }

            var entries = new AssetEntry[manifest.assets.Length];
            for (int i = 0; i < manifest.assets.Length; i++)
            {
                var asset = manifest.assets[i];
                var spritePath = "Assets/_Game/Art/" + asset.path;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null) Debug.LogWarning("[ANL] sprite missing for " + asset.id + " at " + spritePath);
                var entry = new AssetEntry
                {
                    id = asset.id,
                    sprite = sprite,
                    kind = asset.kind,
                    anchorX = asset.anchor != null ? asset.anchor.x : 0.5f,
                    anchorY = asset.anchor != null ? 1f - asset.anchor.y : 0.5f,
                    sha256 = asset.sha256
                };
                if (asset.regions != null && asset.regions.Length > 0)
                {
                    entry.regions = new AssetRegion[asset.regions.Length];
                    for (int r = 0; r < asset.regions.Length; r++)
                    {
                        entry.regions[r] = new AssetRegion
                        {
                            regionId = asset.regions[r].regionId,
                            x = asset.regions[r].x,
                            y = asset.regions[r].y,
                            width = asset.regions[r].width,
                            height = asset.regions[r].height
                        };
                    }
                }
                entries[i] = entry;
            }
            catalog.entries = entries;
            EditorUtility.SetDirty(catalog);
        }

        public static void BuildContentCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(ContentCatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ContentCatalogPath));
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, ContentCatalogPath);
            }
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Game/Content/ep01" });
            var files = new System.Collections.Generic.List<TextAsset>();
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) files.Add(asset);
            }
            files.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            catalog.files = files.ToArray();
            catalog.artManifestSha256 = ArtManifestLoader.ManifestSha256();
            catalog.contentVersion = "ep01.v1.0.0";
            EditorUtility.SetDirty(catalog);
        }

        public static void BuildFontAsset()
        {
            EnsureTmpSettings();
            if (File.Exists(FontAssetPath)) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontResourcePath);
            if (font == null)
            {
                Debug.LogWarning("[ANL] FONT_SOURCE_REQUIRED: font missing at " + FontResourcePath);
                return;
            }
            TMP_FontAsset fontAsset = null;
            try
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[ANL] TMP font asset creation deferred to runtime (batch graphics unavailable): " + exception.Message);
                return;
            }
            if (fontAsset == null)
            {
                Debug.LogWarning("[ANL] FONT_SOURCE_REQUIRED: TMP font asset creation failed");
                return;
            }
            fontAsset.name = "NanumGothic SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 1; i < fontAsset.atlasTextures.Length; i++)
                    if (fontAsset.atlasTextures[i] != null)
                        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
            }
            if (fontAsset.atlasTexture != null)
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                var defaultFont = serialized.FindProperty("m_defaultFontAsset");
                if (defaultFont != null && defaultFont.objectReferenceValue == null)
                {
                    defaultFont.objectReferenceValue = fontAsset;
                    serialized.ApplyModifiedProperties();
                }
            }
            Debug.Log("[ANL] TMP font asset created: " + FontAssetPath);
        }

        private static void EnsureTmpSettings()
        {
            if (TMP_Settings.instance != null) return;
            var settings = ScriptableObject.CreateInstance<TMP_Settings>();
            AssetDatabase.CreateAsset(settings, TmpSettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TmpSettingsPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[ANL] TMP settings created: " + TmpSettingsPath);
        }
    }
}
