using System;
using System.Collections.Generic;
using System.IO;
using ANL.Core.Commands;
using ANL.Core.Conditions;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.State;
using UnityEngine;

namespace ANL.Tests.EditMode
{
    public static class TestContent
    {
        private static long _commandCounter;

        [Serializable]
        private sealed class ManifestLockDto
        {
            public int schemaVersion;
            public ManifestAssetDto[] assets;
        }

        [Serializable]
        private sealed class ManifestAssetDto
        {
            public string id;
            public string path;
            public string kind;
            public int width;
            public int height;
            public bool alphaRequired;
            public string status;
            public string sha256;
        }

        public static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static string ContentDir =>
            Path.Combine(Application.dataPath, "_Game", "Content", "ep01");

        public static string ManifestPath =>
            Path.Combine(ProjectRoot, "handoff", "art", "manifest.lock.json");

        public static ContentIndex Load()
        {
            var dir = ContentDir;
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException("content dir not found: " + dir);

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

            var files = new[]
            {
                "episode.json", "scenes.json", "dialogues.ko.json", "evidence.json",
                "testimonies.json", "branches.json", "puzzles.json", "hints.ko.json",
                "ui.ko.json", "graph.json", "matrix.json"
            };
            for (int i = 0; i < files.Length; i++)
            {
                var path = Path.Combine(dir, files[i]);
                if (!File.Exists(path))
                    throw new FileNotFoundException("content file not found", path);
                switch (files[i])
                {
                    case "episode.json":
                        episode = Parse<EpisodeFileDto>(path).episode;
                        break;
                    case "scenes.json":
                        scenes.AddRange(Parse<SceneFileDto>(path).items);
                        break;
                    case "dialogues.ko.json":
                        dialogues.AddRange(Parse<DialogueFileDto>(path).items);
                        break;
                    case "evidence.json":
                        evidence.AddRange(Parse<EvidenceFileDto>(path).items);
                        break;
                    case "testimonies.json":
                        testimonies.AddRange(Parse<TestimonyFileDto>(path).items);
                        break;
                    case "branches.json":
                        branches.AddRange(Parse<BranchFileDto>(path).items);
                        break;
                    case "puzzles.json":
                        puzzles.AddRange(Parse<PuzzleFileDto>(path).items);
                        break;
                    case "hints.ko.json":
                        hints.AddRange(Parse<HintFileDto>(path).items);
                        break;
                    case "ui.ko.json":
                        uiStrings.AddRange(Parse<UiStringFileDto>(path).items);
                        break;
                    case "graph.json":
                        graph = Parse<GraphFileDto>(path);
                        break;
                    case "matrix.json":
                        matrix = Parse<MatrixFileDto>(path);
                        break;
                }
            }

            if (episode == null) throw new InvalidOperationException("episode.json did not contain an episode");
            return new ContentIndex(episode, scenes, dialogues, evidence, testimonies, branches, puzzles, hints, uiStrings, graph, matrix);
        }

        public static string[] AssetIds()
        {
            var path = ManifestPath;
            if (!File.Exists(path))
                throw new FileNotFoundException("art manifest not found", path);
            var dto = JsonUtility.FromJson<ManifestLockDto>(File.ReadAllText(path));
            if (dto == null || dto.assets == null)
                throw new InvalidOperationException("art manifest contained no assets: " + path);
            var ids = new string[dto.assets.Length];
            for (int i = 0; i < dto.assets.Length; i++)
                ids[i] = dto.assets[i] != null ? dto.assets[i].id : null;
            return ids;
        }

        public static GameState FreshState()
        {
            return new GameState();
        }

        public static CommandDispatcher FreshDispatcher(ContentIndex index, out GameState state)
        {
            state = FreshState();
            var dispatcher = new CommandDispatcher(index, state);
            var result = Send(dispatcher, CommandKinds.StartEpisode);
            if (result.status == ResultStatus.Rejected)
                throw new InvalidOperationException("StartEpisode rejected: " + result.reasonCode + " " + result.message);
            return dispatcher;
        }

        public static CommandResult Send(
            CommandDispatcher d,
            string type,
            string targetId = null,
            string[] ids = null,
            string slot = null,
            string value = null,
            double numeric = 0,
            string origin = "test-assist")
        {
            var command = new GameCommand
            {
                commandId = "testcmd_" + (++_commandCounter),
                sequence = d.State.LastCommandSequence + 1,
                sceneId = d.State.CurrentSceneId,
                origin = origin,
                type = type,
                targetId = targetId,
                ids = ids,
                slot = slot,
                value = value,
                numeric = numeric
            };
            return d.Execute(command);
        }

        public static string[] FirstEvidenceSet(EvidenceSetDto[] sets)
        {
            if (sets == null) return null;
            for (int i = 0; i < sets.Length; i++)
                if (sets[i] != null && sets[i].requiredIds != null && sets[i].requiredIds.Length > 0)
                    return sets[i].requiredIds;
            return null;
        }

        public static int CountOf(List<string> ids, string id)
        {
            int count = 0;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) count++;
            return count;
        }

        public static void EnsureInteraction(CommandDispatcher d, GameState state, string puzzleId)
        {
            var puzzle = d.Index.Puzzle(puzzleId);
            if (puzzle == null)
                throw new InvalidOperationException("EnsureInteraction: unknown puzzle " + puzzleId);
            if (state.CurrentSceneId != puzzle.sceneId)
                SolveSceneTo(d, state, puzzle.sceneId);
            StepToInteraction(d, state);
            if (state.ActivePuzzleId != puzzleId)
                throw new InvalidOperationException(
                    "EnsureInteraction(" + puzzleId + ") failed: scene=" + state.CurrentSceneId +
                    " phase=" + state.Phase + " active=" + state.ActivePuzzleId);
        }

        public static void SolvePuzzle(CommandDispatcher d, GameState state, string puzzleId)
        {
            EnsureInteraction(d, state, puzzleId);
            var plan = TestAssistPlanner.Plan(d.Index, state);
            if (plan.Commands.Count == 0)
                throw new InvalidOperationException(
                    "assist plan unavailable for " + puzzleId + ": blocker=" + plan.Blocker +
                    " canRun=" + plan.CanRun + " scene=" + state.CurrentSceneId + " phase=" + state.Phase +
                    " owned=[" + string.Join(",", state.OwnedEvidence.ToArray()) + "]");
            for (int i = 0; i < plan.Commands.Count; i++)
            {
                var result = d.Execute(plan.Commands[i]);
                if (result.status == ResultStatus.Rejected)
                    throw new InvalidOperationException(
                        "assist command " + plan.Commands[i].type + " rejected for " + puzzleId +
                        ": " + result.reasonCode + " " + result.message);
            }
            if (!state.IsPuzzleSolved(puzzleId))
                throw new InvalidOperationException(
                    "assist plan did not solve " + puzzleId + ": blocker=" + plan.Blocker +
                    " canRun=" + plan.CanRun + " scene=" + state.CurrentSceneId +
                    " phase=" + state.Phase + " active=" + state.ActivePuzzleId);
        }

        public static void SolveSceneTo(CommandDispatcher d, GameState state, string targetSceneId)
        {
            if (!d.Index.HasScene(targetSceneId))
                throw new InvalidOperationException("SolveSceneTo: unknown scene " + targetSceneId);
            var trace = new List<string>();
            for (int steps = 0; steps < 3000; steps++)
            {
                if (state.CurrentSceneId == targetSceneId) return;
                trace.Add(state.CurrentSceneId + "/" + state.Phase +
                    (string.IsNullOrEmpty(state.ActivePuzzleId) ? "" : "/" + state.ActivePuzzleId));
                if (trace.Count > 60) trace.RemoveAt(0);

                switch (state.Phase)
                {
                    case Phases.PreDialogue:
                    case Phases.PostDialogue:
                        ExpectAccepted(d, Send(d, CommandKinds.AdvanceDialogue), "AdvanceDialogue at " + state.CurrentSceneId);
                        break;
                    case Phases.Interaction:
                        SolveActive(d, state);
                        break;
                    case Phases.BranchChoice:
                        if (!ChooseAdvancing(d, state))
                            ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(branch) at " + state.CurrentSceneId);
                        break;
                    case Phases.ReadCheck:
                        OpenRequiredReads(d, state);
                        ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(read) at " + state.CurrentSceneId);
                        break;
                    case Phases.ReadyToExit:
                    case Phases.Completed:
                    {
                        if (d.TransitionAvailable(out _, out var transitionReason))
                        {
                            ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene at " + state.CurrentSceneId);
                            break;
                        }
                        var scene = d.Index.Scene(state.CurrentSceneId);
                        if (IsHubLike(scene))
                        {
                            d.EnterScene(scene.id);
                            break;
                        }
                        throw new InvalidOperationException(
                            "SolveSceneTo(" + targetSceneId + ") stuck at " + state.CurrentSceneId +
                            ": " + transitionReason + "\n" + Trace(trace));
                    }
                    case Phases.HubActive:
                        if (!TryEnterNextRequiredChild(d, state))
                            ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(hub) at " + state.CurrentSceneId);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "SolveSceneTo(" + targetSceneId + ") unhandled phase " + state.Phase +
                            " at " + state.CurrentSceneId + "\n" + Trace(trace));
                }
            }
            throw new InvalidOperationException(
                "SolveSceneTo(" + targetSceneId + ") exceeded step limit; last=" +
                state.CurrentSceneId + "/" + state.Phase + "\n" + Trace(trace));
        }

        public static void FinishScene(CommandDispatcher d, GameState state)
        {
            for (int i = 0; i < 1000; i++)
            {
                switch (state.Phase)
                {
                    case Phases.PreDialogue:
                    case Phases.PostDialogue:
                        ExpectAccepted(d, Send(d, CommandKinds.AdvanceDialogue), "AdvanceDialogue at " + state.CurrentSceneId);
                        break;
                    case Phases.BranchChoice:
                        if (!ChooseAdvancing(d, state))
                            ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(branch) at " + state.CurrentSceneId);
                        break;
                    case Phases.ReadCheck:
                        OpenRequiredReads(d, state);
                        ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(read) at " + state.CurrentSceneId);
                        break;
                    default:
                        return;
                }
            }
            throw new InvalidOperationException("FinishScene exceeded step limit at " + state.CurrentSceneId + "/" + state.Phase);
        }

        public static void AdvanceUntilChoiceOrInteraction(CommandDispatcher d, GameState state)
        {
            for (int i = 0; i < 500; i++)
            {
                if (state.Phase == Phases.PreDialogue || state.Phase == Phases.PostDialogue)
                {
                    ExpectAccepted(d, Send(d, CommandKinds.AdvanceDialogue), "AdvanceDialogue at " + state.CurrentSceneId);
                    continue;
                }
                return;
            }
            throw new InvalidOperationException("AdvanceUntilChoiceOrInteraction exceeded step limit at " + state.CurrentSceneId + "/" + state.Phase);
        }

        public static void EnsureHubActive(CommandDispatcher d, GameState state, string hubId)
        {
            if (state.CurrentSceneId != hubId)
                SolveSceneTo(d, state, hubId);
            if (state.Phase != Phases.HubActive)
            {
                FinishScene(d, state);
                if (state.Phase != Phases.HubActive)
                    d.EnterScene(hubId);
            }
            if (state.CurrentSceneId != hubId || state.Phase != Phases.HubActive)
                throw new InvalidOperationException(
                    "EnsureHubActive(" + hubId + ") failed: scene=" + state.CurrentSceneId + " phase=" + state.Phase);
        }

        private static void StepToInteraction(CommandDispatcher d, GameState state)
        {
            for (int i = 0; i < 500; i++)
            {
                switch (state.Phase)
                {
                    case Phases.PreDialogue:
                    case Phases.PostDialogue:
                        ExpectAccepted(d, Send(d, CommandKinds.AdvanceDialogue), "AdvanceDialogue at " + state.CurrentSceneId);
                        break;
                    case Phases.BranchChoice:
                        if (!ChooseAdvancing(d, state))
                            ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(branch) at " + state.CurrentSceneId);
                        break;
                    case Phases.ReadCheck:
                        OpenRequiredReads(d, state);
                        ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(read) at " + state.CurrentSceneId);
                        break;
                    default:
                        return;
                }
            }
            throw new InvalidOperationException("StepToInteraction exceeded step limit at " + state.CurrentSceneId + "/" + state.Phase);
        }

        private static void SolveActive(CommandDispatcher d, GameState state)
        {
            var puzzleId = state.ActivePuzzleId;
            var plan = TestAssistPlanner.Plan(d.Index, state);
            if (plan.Commands.Count == 0)
                throw new InvalidOperationException(
                    "SolveSceneTo assist plan unavailable for " + puzzleId + ": blocker=" + plan.Blocker +
                    " canRun=" + plan.CanRun + " scene=" + state.CurrentSceneId + " phase=" + state.Phase +
                    " owned=[" + string.Join(",", state.OwnedEvidence.ToArray()) + "]");
            for (int i = 0; i < plan.Commands.Count; i++)
            {
                var result = d.Execute(plan.Commands[i]);
                if (result.status == ResultStatus.Rejected)
                    throw new InvalidOperationException(
                        "SolveSceneTo assist command " + plan.Commands[i].type + " rejected for " + puzzleId +
                        ": " + result.reasonCode + " " + result.message);
            }
            if (!state.IsPuzzleSolved(puzzleId))
                throw new InvalidOperationException(
                    "SolveSceneTo assist plan did not solve " + puzzleId + ": blocker=" + plan.Blocker +
                    " canRun=" + plan.CanRun + " scene=" + state.CurrentSceneId + " phase=" + state.Phase);
        }

        private static bool ChooseAdvancing(CommandDispatcher d, GameState state)
        {
            var scene = d.Index.Scene(state.CurrentSceneId);
            if (scene == null || scene.branchGroupIds == null) return false;
            for (int g = 0; g < scene.branchGroupIds.Length; g++)
            {
                var group = d.Index.BranchGroup(scene.branchGroupIds[g]);
                for (int i = 0; i < group.Count; i++)
                {
                    var branch = group[i];
                    if (branch == null || !branch.advances) continue;
                    if (state.IsTopicVisited(branch.id)) continue;
                    if (!ConditionEvaluator.AllConditionsMet(branch.condition, state, d.Index)) continue;
                    ExpectAccepted(d, Send(d, CommandKinds.ChooseBranch, branch.id), "ChooseBranch " + branch.id);
                    return true;
                }
            }
            return false;
        }

        private static bool TryEnterNextRequiredChild(CommandDispatcher d, GameState state)
        {
            var hub = d.Index.Scene(state.CurrentSceneId);
            if (hub == null || hub.childSceneIds == null) return false;
            for (int i = 0; i < hub.childSceneIds.Length; i++)
            {
                var childId = hub.childSceneIds[i];
                if (state.IsSceneCompleted(childId)) continue;
                var child = d.Index.Scene(childId);
                if (child == null) continue;
                if (!ConditionEvaluator.AllConditionsMet(child.entryConditions, state, d.Index)) continue;
                d.EnterScene(childId);
                return true;
            }
            return false;
        }

        private static void OpenRequiredReads(CommandDispatcher d, GameState state)
        {
            var scene = d.Index.Scene(state.CurrentSceneId);
            if (scene == null || scene.requiredReadEvidenceIds == null) return;
            for (int i = 0; i < scene.requiredReadEvidenceIds.Length; i++)
            {
                var evidenceId = scene.requiredReadEvidenceIds[i];
                if (state.HasRead(evidenceId)) continue;
                if (!state.Owns(evidenceId))
                    throw new InvalidOperationException(
                        "SolveSceneTo required read " + evidenceId + " is not owned at " + state.CurrentSceneId);
                ExpectAccepted(d, Send(d, CommandKinds.OpenEvidence, evidenceId), "OpenEvidence " + evidenceId);
            }
        }

        private static bool IsHubLike(SceneDefinitionDto scene)
        {
            if (scene == null) return false;
            return scene.screenType == "hub" || (scene.childSceneIds != null && scene.childSceneIds.Length > 0);
        }

        private static void ExpectAccepted(CommandDispatcher d, CommandResult result, string what)
        {
            if (result.status == ResultStatus.Rejected)
                throw new InvalidOperationException(what + " rejected: " + result.reasonCode + " " + result.message);
        }

        private static string Trace(List<string> trace)
        {
            return "trace: " + string.Join(" > ", trace.ToArray());
        }

        private static T Parse<T>(string path) where T : class
        {
            var parsed = JsonUtility.FromJson<T>(File.ReadAllText(path));
            if (parsed == null)
                throw new InvalidOperationException("parse failed (empty): " + path);
            return parsed;
        }
    }
}
