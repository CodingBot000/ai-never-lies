using System.IO;
using UnityEditor;
using UnityEngine;

namespace ANL.Editor
{
    public static class ArtImporter
    {
        [MenuItem("ANL/Art/Import Runtime Art")]
        public static void ImportAll()
        {
            var sourceRoot = ProjectPaths.ArtSourceRoot;
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException("runtime art folder missing: " + sourceRoot);
            var manifest = ArtManifestLoader.Load();

            int copied = 0;
            foreach (var file in Directory.GetFiles(sourceRoot, "*.png", SearchOption.AllDirectories))
            {
                var relative = file.Substring(sourceRoot.Length + 1).Replace('\\', '/');
                var dest = Path.Combine(ProjectPaths.ArtDestRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                var srcInfo = new FileInfo(file);
                var dstInfo = new FileInfo(dest);
                if (!dest.Contains(".meta"))
                {
                    File.Copy(file, dest, true);
                    copied++;
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters(manifest);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[ANL] art import complete: copied={copied}, assets={manifest.assets.Length}");
        }

        private static void ConfigureImporters(ArtManifest manifest)
        {
            var guidRoot = ProjectPaths.ArtDestRoot.Substring(Application.dataPath.Length - "Assets".Length).Replace('\\', '/');
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { guidRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var relative = path.StartsWith(guidRoot + "/") ? path.Substring(guidRoot.Length + 1) : null;
                if (relative == null) continue;
                var asset = ArtManifestLoader.FindByPath(manifest, relative);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var isPortrait = asset != null && asset.kind == "portrait";
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.alphaIsTransparency = importer.DoesSourceTextureHaveAlpha();
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                if (isPortrait && asset.anchor != null)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(asset.anchor.x, 1f - asset.anchor.y);
                }
                else
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    settings.spritePivot = new Vector2(0.5f, 0.5f);
                }
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
