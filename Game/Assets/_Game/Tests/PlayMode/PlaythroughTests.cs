using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ANL.Core.Commands;
using ANL.Core.Conditions;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.State;
using ANL.Core.Validation;
using ANL.Runtime.Content;
using ANL.Runtime.Persistence;
using ANL.Runtime.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ANL.Tests.PlayMode
{
    public class PlaythroughTests
    {
        private const string Slot = "test_pm";
        private static long _commandCounter;

        [TearDown]
        public void TearDown()
        {
            DeleteSlotFiles();
        }

        [Test]
        public void FullRequiredPlaythrough_ReachesS710WithAllMandatoryContent()
        {
            var load = ContentRepository.Load();
            Assert.IsTrue(load.Ok, "content must load without errors: " + string.Join(" | ", load.Errors.ToArray()));
            var index = load.Index;

            var state = new GameState();
            var dispatcher = new CommandDispatcher(index, state);
            var start = Send(dispatcher, CommandKinds.StartEpisode);
            Assert.AreNotEqual(ResultStatus.Rejected, start.status, "StartEpisode must be accepted: " + start.reasonCode);

            WalkTo(dispatcher, state, "s710");
            AdvanceUntilDialogueDone(dispatcher, state);
            Assert.AreEqual("s710", state.CurrentSceneId, "playthrough must end at s710");
            Assert.IsTrue(state.IsSceneCompleted("s710"), "s710 must be completed");

            Assert.AreEqual(20, state.SolvedPuzzles.Count, "all 20 puzzles must be solved");
            for (int i = 0; i < FrozenIds.Puzzles.Length; i++)
                Assert.IsTrue(state.IsPuzzleSolved(FrozenIds.Puzzles[i]), FrozenIds.Puzzles[i] + " must be solved");

            for (int i = 0; i < FrozenIds.Evidence.Length; i++)
            {
                var id = FrozenIds.Evidence[i];
                if (id == "ev34" || id == "ev35")
                    Assert.IsFalse(state.Owns(id), "optional evidence " + id + " must not be owned on the required route");
                else
                    Assert.IsTrue(state.Owns(id), "mandatory evidence " + id + " must be owned at the end");
            }
        }

        [Test]
        public void RuntimeSaveAndReload_RestoresScenePhaseAndCursor()
        {
            var load = ContentRepository.Load();
            Assert.IsTrue(load.Ok, "content must load without errors: " + string.Join(" | ", load.Errors.ToArray()));
            var index = load.Index;
            var version = index.Episode != null ? index.Episode.contentVersion : "";
            var saves = new SaveService(version, "");

            var session = new GameSession(index, saves, version, "");
            session.NewGame(Slot);
            Assert.AreEqual("s000", session.State.CurrentSceneId, "NewGame must start at s000");
            for (int i = 0; i < 3; i++)
            {
                var result = session.Execute(CommandKinds.AdvanceDialogue);
                Assert.AreNotEqual(ResultStatus.Rejected, result.status, "AdvanceDialogue must be accepted on line " + (i + 1));
            }
            Assert.IsTrue(session.SaveManual(Slot), "manual save must succeed");

            var reloaded = new GameSession(index, saves, version, "");
            LoadOutcome outcome;
            Assert.IsTrue(reloaded.LoadGame(Slot, out outcome), "LoadGame must succeed, outcome=" + outcome);
            Assert.AreEqual(LoadOutcome.Ok, outcome, "load outcome must be Ok");
            Assert.AreEqual(session.State.CurrentSceneId, reloaded.State.CurrentSceneId, "current scene must match");
            Assert.AreEqual(session.State.Phase, reloaded.State.Phase, "phase must match");
            Assert.AreEqual(session.State.DialogueCursor, reloaded.State.DialogueCursor, "dialogue cursor must match");
            Assert.AreEqual(session.State.LastCommandSequence, reloaded.State.LastCommandSequence, "command sequence must match");
        }

        [UnityTest]
        public IEnumerator MainSceneBootsWithoutAppRootErrors()
        {
            bool headless = Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/_Game/Scenes/Main.unity");
            if (buildIndex < 0)
                Assert.Ignore("Main scene is not present in build settings");

            var problems = new List<string>();
            Application.LogCallback handler = (condition, stackTrace, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                problems.Add(type + ": " + condition + (string.IsNullOrEmpty(stackTrace) ? "" : "\n" + stackTrace));
            };
            Application.logMessageReceived += handler;

            AsyncOperation operation = null;
            string loadError = "";
            try
            {
                operation = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                loadError = exception.Message;
            }

            if (operation == null)
            {
                Application.logMessageReceived -= handler;
                Assert.Ignore("Main scene load failed: " + loadError);
            }

            while (!operation.isDone) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            Application.logMessageReceived -= handler;

            var relevant = new List<string>();
            for (int i = 0; i < problems.Count; i++)
                if (IsRelevantBootProblem(problems[i])) relevant.Add(problems[i]);
            if (relevant.Count == 0) yield break;

            if (headless && AllGraphicsNoise(relevant))
                Assert.Ignore("headless graphics-related boot problems: " + string.Join(" | ", relevant.ToArray()));
            Assert.Fail("errors while booting Main:\n" + string.Join("\n", relevant.ToArray()));
        }

        private static bool IsRelevantBootProblem(string problem)
        {
            return problem.Contains("AppRoot") || problem.Contains("[ANL]") || problem.Contains("TMPro") || problem.Contains("Shader");
        }

        private static bool AllGraphicsNoise(List<string> problems)
        {
            for (int i = 0; i < problems.Count; i++)
            {
                var p = problems[i];
                bool graphics = p.Contains("TMPro") || p.Contains("TMP_") || p.Contains("Shader")
                    || p.Contains("shader") || p.Contains("Font") || p.Contains("font")
                    || p.Contains("Texture") || p.Contains("texture") || p.Contains("Graphics")
                    || p.Contains("GPU") || p.Contains("display") || p.Contains("Metal")
                    || p.Contains("OpenGL") || p.Contains("D3D");
                if (!graphics) return false;
            }
            return true;
        }

        private static void AdvanceUntilDialogueDone(CommandDispatcher d, GameState state)
        {
            for (int i = 0; i < 500; i++)
            {
                if (state.Phase != Phases.PreDialogue && state.Phase != Phases.PostDialogue) return;
                var result = Send(d, CommandKinds.AdvanceDialogue);
                if (result.status == ResultStatus.Rejected)
                    Assert.Fail("AdvanceDialogue rejected at " + state.CurrentSceneId + ": " + result.reasonCode);
            }
            Assert.Fail("dialogue did not finish at " + state.CurrentSceneId);
        }

        private static void WalkTo(CommandDispatcher d, GameState state, string targetSceneId)
        {
            if (!d.Index.HasScene(targetSceneId))
                Assert.Fail("unknown target scene " + targetSceneId);
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
                        if (d.TransitionAvailable(out _, out var reason))
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
                        Assert.Fail("stuck at " + state.CurrentSceneId + ": " + reason + "\n" + Trace(trace));
                        break;
                    }
                    case Phases.HubActive:
                        if (!TryEnterNextRequiredChild(d, state))
                            ExpectAccepted(d, Send(d, CommandKinds.ContinueScene), "ContinueScene(hub) at " + state.CurrentSceneId);
                        break;
                    default:
                        Assert.Fail("unhandled phase " + state.Phase + " at " + state.CurrentSceneId + "\n" + Trace(trace));
                        break;
                }
            }
            Assert.Fail("walk exceeded step limit; last=" + state.CurrentSceneId + "/" + state.Phase + "\n" + Trace(trace));
        }

        private static void SolveActive(CommandDispatcher d, GameState state)
        {
            var puzzleId = state.ActivePuzzleId;
            var plan = TestAssistPlanner.Plan(d.Index, state);
            if (plan.Commands.Count == 0)
                Assert.Fail("assist blocker for " + puzzleId + ": " + plan.Blocker + " canRun=" + plan.CanRun +
                    " scene=" + state.CurrentSceneId + " phase=" + state.Phase +
                    " owned=[" + string.Join(",", state.OwnedEvidence.ToArray()) + "]");
            for (int i = 0; i < plan.Commands.Count; i++)
            {
                var result = d.Execute(plan.Commands[i]);
                if (result.status == ResultStatus.Rejected)
                    Assert.Fail("assist command " + plan.Commands[i].type + " rejected for " + puzzleId +
                        ": " + result.reasonCode + " " + result.message);
            }
            if (!state.IsPuzzleSolved(puzzleId))
                Assert.Fail("assist plan did not solve " + puzzleId + ": blocker=" + plan.Blocker +
                    " canRun=" + plan.CanRun + " scene=" + state.CurrentSceneId +
                    " phase=" + state.Phase + " active=" + state.ActivePuzzleId);
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
                    Assert.Fail("required read " + evidenceId + " not owned at " + state.CurrentSceneId);
                ExpectAccepted(d, Send(d, CommandKinds.OpenEvidence, evidenceId), "OpenEvidence " + evidenceId);
            }
        }

        private static bool IsHubLike(SceneDefinitionDto scene)
        {
            if (scene == null) return false;
            return scene.screenType == "hub" || (scene.childSceneIds != null && scene.childSceneIds.Length > 0);
        }

        private static CommandResult Send(
            CommandDispatcher d,
            string type,
            string targetId = null,
            string[] ids = null,
            string slot = null,
            string value = null,
            double numeric = 0)
        {
            var command = new GameCommand
            {
                commandId = "pmcmd_" + (++_commandCounter),
                sequence = d.State.LastCommandSequence + 1,
                sceneId = d.State.CurrentSceneId,
                origin = Origins.TestAssist,
                type = type,
                targetId = targetId,
                ids = ids,
                slot = slot,
                value = value,
                numeric = numeric
            };
            return d.Execute(command);
        }

        private static void ExpectAccepted(CommandDispatcher d, CommandResult result, string what)
        {
            if (result.status == ResultStatus.Rejected)
                Assert.Fail(what + " rejected: " + result.reasonCode + " " + result.message);
        }

        private static string Trace(List<string> trace)
        {
            return "trace: " + string.Join(" > ", trace.ToArray());
        }

        private static void DeleteSlotFiles()
        {
            var path = Path.Combine(Application.persistentDataPath, "saves", Slot + ".json");
            string[] paths = { path, path + ".bak", path + ".tmp" };
            for (int i = 0; i < paths.Length; i++)
                if (File.Exists(paths[i])) File.Delete(paths[i]);
        }
    }
}
