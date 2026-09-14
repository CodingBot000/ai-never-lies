using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ANL.Core.Contracts;
using ANL.Core.Content;
using ANL.Core.Validation;

namespace ANL.Editor
{
    public static class ContentValidatorTool
    {
        [MenuItem("ANL/Validate Content")]
        public static void ValidateFromMenu()
        {
            var errors = Validate(out var summary);
            Debug.Log("[ANL] content validation: " + summary);
            if (errors.Count > 0)
                Debug.LogError("[ANL] content errors:\n" + string.Join("\n", errors));
        }

        public static void ValidateBatch()
        {
            var errors = Validate(out var summary);
            Debug.Log("[ANL] content validation: " + summary);
            if (errors.Count > 0)
            {
                Debug.LogError("[ANL] content errors:\n" + string.Join("\n", errors));
                EditorApplication.Exit(1);
            }
            EditorApplication.Exit(0);
        }

        public static List<string> Validate(out string summary)
        {
            var errors = new List<string>();
            var index = LoadIndex(errors);
            if (index == null)
            {
                summary = "index load failed";
                WriteReport(errors, 0, 0, 0);
                return errors;
            }
            var assetIds = LoadAssetIds();
            errors.AddRange(ContentValidatorCore.Validate(index, assetIds));
            summary = $"scenes={index.Scenes.Count} evidence={index.Evidence.Count} puzzles={index.Puzzles.Count} dialogue={index.Dialogues.Count} hints={index.Hints.Count} errors={errors.Count}";
            WriteReport(errors, index.Scenes.Count, index.Evidence.Count, index.Puzzles.Count);
            return errors;
        }

        private static void WriteReport(List<string> errors, int scenes, int evidence, int puzzles)
        {
            Directory.CreateDirectory(ProjectPaths.ReportsDir);
            var path = Path.Combine(ProjectPaths.ReportsDir, "content_validation.json");
            var dto = new ValidationReportDto
            {
                ok = errors.Count == 0,
                sceneCount = scenes,
                evidenceCount = evidence,
                puzzleCount = puzzles,
                errorCount = errors.Count,
                errors = errors.ToArray()
            };
            File.WriteAllText(path, JsonUtility.ToJson(dto, true));
            Debug.Log("[ANL] wrote " + path);
        }

        [System.Serializable]
        private sealed class ValidationReportDto
        {
            public bool ok;
            public int sceneCount;
            public int evidenceCount;
            public int puzzleCount;
            public int errorCount;
            public string[] errors;
        }

        private static string[] LoadAssetIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ANL.Runtime.Assets.AssetCatalog>("Assets/_Game/Resources/AssetCatalog.asset");
            if (catalog?.entries == null) return null;
            var ids = new string[catalog.entries.Length];
            for (int i = 0; i < catalog.entries.Length; i++) ids[i] = catalog.entries[i].id;
            return ids;
        }

        private static ContentIndex LoadIndex(List<string> errors)
        {
            var dir = "Assets/_Game/Content/ep01";
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { dir });
            EpisodeDefinitionDto episode = null;
            var scenes = new List<SceneDefinitionDto>();
            var dialogues = new List<DialogueLineDto>();
            var evidence = new List<EvidenceDefinitionDto>();
            var testimonies = new List<TestimonyStatementDto>();
            var branches = new List<BranchDefinitionDto>();
            var puzzles = new List<PuzzleDefinitionDto>();
            var hints = new List<HintDefinitionDto>();
            var uiStrings = new List<UiStringDto>();
            GraphFileDto graph = null;
            MatrixFileDto matrix = null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (text == null) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                try
                {
                    switch (name)
                    {
                        case "episode":
                            episode = JsonUtility.FromJson<EpisodeFileDto>(text.text).episode;
                            break;
                        case "scenes":
                            scenes.AddRange(JsonUtility.FromJson<SceneFileDto>(text.text).items);
                            break;
                        case "dialogues.ko":
                            dialogues.AddRange(JsonUtility.FromJson<DialogueFileDto>(text.text).items);
                            break;
                        case "evidence":
                            evidence.AddRange(JsonUtility.FromJson<EvidenceFileDto>(text.text).items);
                            break;
                        case "testimonies":
                            testimonies.AddRange(JsonUtility.FromJson<TestimonyFileDto>(text.text).items);
                            break;
                        case "branches":
                            branches.AddRange(JsonUtility.FromJson<BranchFileDto>(text.text).items);
                            break;
                        case "puzzles":
                            puzzles.AddRange(JsonUtility.FromJson<PuzzleFileDto>(text.text).items);
                            break;
                        case "hints.ko":
                            hints.AddRange(JsonUtility.FromJson<HintFileDto>(text.text).items);
                            break;
                        case "ui.ko":
                            uiStrings.AddRange(JsonUtility.FromJson<UiStringFileDto>(text.text).items);
                            break;
                        case "graph":
                            graph = JsonUtility.FromJson<GraphFileDto>(text.text);
                            break;
                        case "matrix":
                            matrix = JsonUtility.FromJson<MatrixFileDto>(text.text);
                            break;
                        default:
                            errors.Add("unexpected content file: " + name);
                            break;
                    }
                }
                catch (System.Exception exception)
                {
                    errors.Add($"parse failed: {name}: {exception.Message}");
                }
            }
            if (episode == null) errors.Add("episode.json missing or invalid");
            if (scenes.Count == 0) errors.Add("scenes.json missing or invalid");
            return new ContentIndex(episode, scenes, dialogues, evidence, testimonies, branches, puzzles, hints, uiStrings, graph, matrix);
        }
    }
}
