using System;
using System.Collections.Generic;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Validation;
using ANL.Runtime.Assets;
using ANL.Runtime.Diagnostics;
using UnityEngine;

namespace ANL.Runtime.Content
{
    public sealed class ContentLoadResult
    {
        public ContentIndex Index;
        public List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0 && Index != null;
    }

    public static class ContentRepository
    {
        private const string ContentCatalogPath = "ContentCatalog";
        private const string AssetCatalogPath = "AssetCatalog";
        private const string FallbackFolder = "Content/ep01";

        private static readonly HashSet<string> KnownFiles = new HashSet<string>
        {
            "episode.json",
            "scenes.json",
            "dialogues.ko.json",
            "evidence.json",
            "testimonies.json",
            "branches.json",
            "puzzles.json",
            "hints.ko.json",
            "ui.ko.json",
            "graph.json",
            "matrix.json"
        };

        public static ContentLoadResult Load()
        {
            var result = new ContentLoadResult();

            var catalog = Resources.Load<ContentCatalog>(ContentCatalogPath);
            var files = new List<TextAsset>();
            if (catalog != null && catalog.files != null)
            {
                for (int i = 0; i < catalog.files.Length; i++)
                    if (catalog.files[i] != null) files.Add(catalog.files[i]);
            }
            if (files.Count == 0)
            {
                var fallback = Resources.LoadAll<TextAsset>(FallbackFolder);
                if (fallback != null) files.AddRange(fallback);
                if (files.Count == 0)
                {
                    Fail(result, "ContentCatalog missing");
                    return result;
                }
            }

            EpisodeFileDto episodeFile = null;
            SceneFileDto scenesFile = null;
            DialogueFileDto dialoguesFile = null;
            EvidenceFileDto evidenceFile = null;
            TestimonyFileDto testimoniesFile = null;
            BranchFileDto branchesFile = null;
            PuzzleFileDto puzzlesFile = null;
            HintFileDto hintsFile = null;
            UiStringFileDto uiFile = null;
            GraphFileDto graphFile = null;
            MatrixFileDto matrixFile = null;
            var seen = new HashSet<string>();

            for (int i = 0; i < files.Count; i++)
            {
                var asset = files[i];
                string rawName = asset.name;
                string key = Normalize(rawName);
                switch (key)
                {
                    case "episode": episodeFile = Parse<EpisodeFileDto>(asset, result); break;
                    case "scenes": scenesFile = Parse<SceneFileDto>(asset, result); break;
                    case "dialogues.ko": dialoguesFile = Parse<DialogueFileDto>(asset, result); break;
                    case "evidence": evidenceFile = Parse<EvidenceFileDto>(asset, result); break;
                    case "testimonies": testimoniesFile = Parse<TestimonyFileDto>(asset, result); break;
                    case "branches": branchesFile = Parse<BranchFileDto>(asset, result); break;
                    case "puzzles": puzzlesFile = Parse<PuzzleFileDto>(asset, result); break;
                    case "hints.ko": hintsFile = Parse<HintFileDto>(asset, result); break;
                    case "ui.ko": uiFile = Parse<UiStringFileDto>(asset, result); break;
                    case "graph": graphFile = Parse<GraphFileDto>(asset, result); break;
                    case "matrix": matrixFile = Parse<MatrixFileDto>(asset, result); break;
                    default:
                        Fail(result, "unexpected content file: " + rawName);
                        break;
                }
                if (KnownFiles.Contains(key + ".json")) seen.Add(key + ".json");
            }

            foreach (var required in KnownFiles)
                if (!seen.Contains(required))
                    Fail(result, "missing content file: " + required);

            var index = new ContentIndex(
                episodeFile != null ? episodeFile.episode : null,
                scenesFile != null ? scenesFile.items : null,
                dialoguesFile != null ? dialoguesFile.items : null,
                evidenceFile != null ? evidenceFile.items : null,
                testimoniesFile != null ? testimoniesFile.items : null,
                branchesFile != null ? branchesFile.items : null,
                puzzlesFile != null ? puzzlesFile.items : null,
                hintsFile != null ? hintsFile.items : null,
                uiFile != null ? uiFile.items : null,
                graphFile,
                matrixFile);
            result.Index = index;

            var validationErrors = ContentValidatorCore.Validate(index, LoadAssetIds());
            for (int i = 0; i < validationErrors.Count; i++)
                Fail(result, "content validation: " + validationErrors[i]);

            return result;
        }

        private static string[] LoadAssetIds()
        {
            var catalog = Resources.Load<AssetCatalog>(AssetCatalogPath);
            if (catalog == null || catalog.entries == null) return null;
            var ids = new List<string>();
            for (int i = 0; i < catalog.entries.Length; i++)
                if (catalog.entries[i] != null && !string.IsNullOrEmpty(catalog.entries[i].id)) ids.Add(catalog.entries[i].id);
            return ids.Count > 0 ? ids.ToArray() : null;
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 5)
                : name;
        }

        private static T Parse<T>(TextAsset asset, ContentLoadResult result) where T : class
        {
            try
            {
                var parsed = JsonUtility.FromJson<T>(asset.text);
                if (parsed == null) Fail(result, "content parse failed: " + asset.name + ": empty result");
                return parsed;
            }
            catch (Exception ex)
            {
                Fail(result, "content parse failed: " + asset.name + ": " + ex.Message);
                return null;
            }
        }

        private static void Fail(ContentLoadResult result, string message)
        {
            result.Errors.Add(message);
            Diag.Error(message);
        }
    }
}
