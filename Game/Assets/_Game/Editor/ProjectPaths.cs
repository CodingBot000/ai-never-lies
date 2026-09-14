using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ANL.Editor
{
    [Serializable]
    public sealed class ArtAnchor
    {
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class ArtRegion
    {
        public string regionId;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class ArtAsset
    {
        public string id;
        public string path;
        public string kind;
        public bool required;
        public int width;
        public int height;
        public bool alphaRequired;
        public string status;
        public int revision;
        public ArtAnchor anchor;
        public ArtRegion[] regions;
        public string fitMode;
        public string sha256;
        public long fileBytes;
        public int actualWidth;
        public int actualHeight;
        public string mode;
    }

    [Serializable]
    public sealed class ArtManifest
    {
        public int schemaVersion;
        public string contractVersion;
        public string episodeId;
        public int requiredAssetCount;
        public ArtAsset[] assets;
    }

    public static class ProjectPaths
    {
        public static string Root
        {
            get
            {
                var dataPath = Application.dataPath;
                return Path.GetFullPath(Path.Combine(dataPath, "..", ".."));
            }
        }

        public static string GameRoot => Path.Combine(Root, "Game");
        public static string ArtSourceRoot => Path.Combine(Root, "art_delivery", "runtime");
        public static string ArtDestRoot => Path.Combine(Application.dataPath, "_Game", "Art");
        public static string ContentDir => Path.Combine(Application.dataPath, "_Game", "Content", "ep01");
        public static string ManifestPath => Path.Combine(Root, "handoff", "art", "manifest.lock.json");
        public static string ReportsDir => Path.Combine(Root, "reports");
    }

    public static class ArtManifestLoader
    {
        public static ArtManifest Load()
        {
            var path = ProjectPaths.ManifestPath;
            if (!File.Exists(path)) throw new FileNotFoundException("art manifest not found", path);
            var json = File.ReadAllText(path);
            var manifest = JsonUtility.FromJson<ArtManifest>(json);
            if (manifest?.assets == null || manifest.assets.Length == 0)
                throw new InvalidOperationException("art manifest is empty: " + path);
            return manifest;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string ManifestSha256()
        {
            if (!File.Exists(ProjectPaths.ManifestPath)) return "";
            return Sha256Hex(File.ReadAllBytes(ProjectPaths.ManifestPath));
        }

        public static ArtAsset Find(ArtManifest manifest, string id)
        {
            if (manifest?.assets == null) return null;
            for (int i = 0; i < manifest.assets.Length; i++)
                if (manifest.assets[i].id == id) return manifest.assets[i];
            return null;
        }

        public static ArtAsset FindByPath(ArtManifest manifest, string path)
        {
            if (manifest?.assets == null) return null;
            for (int i = 0; i < manifest.assets.Length; i++)
                if (manifest.assets[i].path == path) return manifest.assets[i];
            return null;
        }
    }
}
