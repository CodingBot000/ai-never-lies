using System;
using System.Collections.Generic;
using System.IO;
using ANL.Core.Commands;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Hints;
using ANL.Core.Puzzles;
using ANL.Core.State;
using ANL.Core.Validation;
using ANL.Runtime.Assets;
using ANL.Runtime.Diagnostics;
using ANL.Runtime.Persistence;
using ANL.Runtime.Presentation;
using ANL.UI.Screens;
using NUnit.Framework;
using UnityEngine;

namespace ANL.Tests.EditMode
{
    public class SaveAndHintTests
    {
        private const string Slot = "test_sv";

        private ContentIndex _index;
        private SaveService _saves;
        private string _contentVersion;

        [SetUp]
        public void SetUp()
        {
            _index = TestContent.Load();
            _contentVersion = _index.Episode != null ? _index.Episode.contentVersion : "";
            _saves = new SaveService(_contentVersion, "");
        }

        [TearDown]
        public void TearDown()
        {
            DeleteSlot(Slot);
        }

        [Test]
        public void SV01_SnapshotRoundTripPreservesCollections()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s310");
            Assert.AreEqual(Phases.PreDialogue, state.Phase, "s310 must stop in pre dialogue");

            var snapshot = state.ToSnapshot(_contentVersion, "", Slot);
            var parsed = JsonUtility.FromJson<ProgressSnapshot>(JsonUtility.ToJson(snapshot, false));
            var restored = GameState.FromSnapshot(parsed);

            Assert.AreEqual(state.CurrentSceneId, restored.CurrentSceneId, "currentSceneId");
            Assert.AreEqual(state.Phase, restored.Phase, "phase");
            Assert.AreEqual(state.DialogueCursor, restored.DialogueCursor, "dialogueCursor");
            Assert.AreEqual(state.LastCommandSequence, restored.LastCommandSequence, "lastCommandSequence");
            AssertSameSet(state.OwnedEvidence, restored.OwnedEvidence, "ownedEvidenceIds");
            AssertSameSet(state.ReadEvidence, restored.ReadEvidence, "readEvidenceIds");
            AssertSameSet(state.VisitedScenes, restored.VisitedScenes, "visitedSceneIds");
            AssertSameSet(state.CompletedScenes, restored.CompletedScenes, "completedSceneIds");
            AssertSameSet(state.SolvedPuzzles, restored.SolvedPuzzles, "solvedPuzzleIds");
            Assert.AreEqual(state.BranchValues.Count, restored.BranchValues.Count, "branchValues count");
            Assert.AreEqual(state.PuzzleDrafts.Count, restored.PuzzleDrafts.Count, "puzzleDrafts count");
        }

        [Test]
        public void SV02_HearingDraftSurvivesSaveAndLoad()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_02"));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev06", "ev07" }));

            Assert.IsTrue(_saves.Save(state, Slot), "save must succeed");
            Assert.AreEqual(LoadOutcome.Ok, _saves.Load(Slot, out var snapshot), "load must succeed");

            var restored = GameState.FromSnapshot(snapshot);
            var draft = restored.GetDraft("p04", false);
            Assert.IsNotNull(draft, "p04 draft must be restored");
            Assert.AreEqual("st150_02", draft.selectedStatementId, "selected statement must survive");
            CollectionAssert.AreEquivalent(new[] { "ev06", "ev07" }, draft.selectedEvidenceIds, "selected evidence must survive");
            Assert.AreEqual("p04", restored.ActivePuzzleId, "active puzzle must survive");
            Assert.AreEqual(Phases.Interaction, restored.Phase, "phase must survive");
        }

        [Test]
        public void SV03_CausalDraftEdgesSurviveSaveAndLoad()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ReachInteraction(dispatcher, state, "s610", "p17");
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ConnectNodes, "causal", new[] { "n_fault", "n_false_rear" }));

            Assert.IsTrue(_saves.Save(state, Slot), "save must succeed");
            Assert.AreEqual(LoadOutcome.Ok, _saves.Load(Slot, out var snapshot), "load must succeed");

            var restored = GameState.FromSnapshot(snapshot);
            var draft = restored.GetDraft("p17", false);
            Assert.IsNotNull(draft, "p17 draft must be restored");
            Assert.AreEqual(1, draft.edges != null ? draft.edges.Length : 0, "edge count must survive");
            Assert.AreEqual("n_fault", draft.edges[0].from, "edge from must survive");
            Assert.AreEqual("n_false_rear", draft.edges[0].to, "edge to must survive");
            Assert.AreEqual("causal", draft.edges[0].kind, "edge kind must survive");
        }

        [Test]
        public void SV04_MidDialogueCursorSurvivesAndGrantsAreNotReplayed()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s120");
            for (int i = 0; i < 6; i++)
                ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue));
            Assert.AreEqual(6, state.DialogueCursor, "cursor must reach the ev05 grant line");
            Assert.IsTrue(state.Owns("ev05"), "ev05 must be granted at dlg_s120_007");
            Assert.AreEqual(Phases.PreDialogue, state.Phase, "s120 pre dialogue must still be running");

            int ownedBefore = state.OwnedEvidence.Count;
            Assert.IsTrue(_saves.Save(state, Slot), "save must succeed");
            Assert.AreEqual(LoadOutcome.Ok, _saves.Load(Slot, out var snapshot), "load must succeed");
            var restored = GameState.FromSnapshot(snapshot);
            Assert.AreEqual(6, restored.DialogueCursor, "dialogue cursor must survive");
            Assert.AreEqual(1, TestContent.CountOf(restored.OwnedEvidence, "ev05"), "ev05 must be owned exactly once after reload");
            Assert.AreEqual(ownedBefore, restored.OwnedEvidence.Count, "evidence count must not change on reload");

            var replay = new CommandDispatcher(_index, restored);
            ExpectAccepted(TestContent.Send(replay, CommandKinds.AdvanceDialogue));
            Assert.AreEqual(7, restored.DialogueCursor, "one advance after reload must move exactly one line");
            Assert.AreEqual(1, TestContent.CountOf(restored.OwnedEvidence, "ev05"), "ev05 must not be granted twice");
            Assert.AreEqual(ownedBefore, restored.OwnedEvidence.Count, "no reward may be replayed after reload");
        }

        [Test]
        public void SV05_TruncatedSaveIsCorruptAndFileIsPreserved()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue));
            Assert.IsTrue(_saves.Save(state, Slot), "save must succeed");

            var path = _saves.ResolvedPathFor(Slot);
            var text = File.ReadAllText(path);
            File.WriteAllText(path, text.Substring(0, Math.Max(1, text.Length / 2)));

            var outcome = _saves.Load(Slot, out var snapshot);
            Assert.AreEqual(LoadOutcome.Corrupt, outcome, "truncated save must be reported as Corrupt");
            Assert.IsNull(snapshot, "corrupt load must not produce a snapshot");
            Assert.IsTrue(File.Exists(path), "the original save file must never be silently deleted");
        }

        [Test]
        public void SV06_ChecksumMismatchIsCorruptAndFileIsPreserved()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue));
            Assert.IsTrue(_saves.Save(state, Slot), "save must succeed");

            var path = _saves.ResolvedPathFor(Slot);
            var envelope = JsonUtility.FromJson<SaveEnvelopeDto>(File.ReadAllText(path));
            Assert.IsNotNull(envelope, "envelope must parse");
            envelope.payloadSha256 = new string('0', string.IsNullOrEmpty(envelope.payloadSha256) ? 64 : envelope.payloadSha256.Length);
            File.WriteAllText(path, JsonUtility.ToJson(envelope, false));

            var outcome = _saves.Load(Slot, out var snapshot);
            Assert.AreEqual(LoadOutcome.Corrupt, outcome, "checksum mismatch must be reported as Corrupt");
            Assert.IsNull(snapshot, "corrupt load must not produce a snapshot");
            Assert.IsTrue(File.Exists(path), "the modified file must still exist for recovery inspection");
        }

        [Test]
        public void SV07_FutureSchemaVersionIsIncompatible()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            Assert.IsTrue(_saves.Save(state, Slot), "save must succeed");

            var path = _saves.ResolvedPathFor(Slot);
            var envelope = JsonUtility.FromJson<SaveEnvelopeDto>(File.ReadAllText(path));
            Assert.IsNotNull(envelope, "envelope must parse");
            envelope.schemaVersion = 999;
            File.WriteAllText(path, JsonUtility.ToJson(envelope, false));

            var outcome = _saves.Load(Slot, out var snapshot);
            Assert.AreEqual(LoadOutcome.Incompatible, outcome, "future schema must be Incompatible");
            Assert.IsNull(snapshot, "incompatible load must not produce a snapshot");
            Assert.IsTrue(File.Exists(path), "incompatible save must not be deleted or reset");
        }

        [Test]
        public void SV08_UnknownSceneIdIsDetectedWithoutCrashing()
        {
            var state = TestContent.FreshState();
            state.CurrentSceneId = "s999";
            state.Phase = Phases.ReadyToExit;
            Assert.IsTrue(_saves.Save(state, Slot), "save of the synthetic state must succeed");
            Assert.AreEqual(LoadOutcome.Ok, _saves.Load(Slot, out _), "raw load must still succeed");

            GameSession session = null;
            bool loaded = false;
            LoadOutcome outcome = LoadOutcome.Missing;
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*save references unknown scene.*"));
            Assert.DoesNotThrow(() =>
            {
                session = new GameSession(_index, _saves, _contentVersion, "");
                loaded = session.LoadGame(Slot, out outcome);
            }, "loading a save with an unknown scene must not throw");

            Assert.IsFalse(loaded, "unknown scene save must not be loaded");
            Assert.AreEqual(LoadOutcome.UnknownIds, outcome, "outcome must be UnknownIds");
            Assert.IsNotNull(session, "session must have been constructed");
            Assert.IsTrue(session.BlockingError.Contains("s999"), "BlockingError must name the unknown scene, got: " + session.BlockingError);
            Assert.AreEqual("", session.State.CurrentSceneId, "session state must not be replaced by the bad save");
        }

        [Test]
        public void SV09_InvalidSlotIdFailsWithoutTouchingState()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue));
            var sceneBefore = state.CurrentSceneId;
            var phaseBefore = state.Phase;
            var sequenceBefore = state.LastCommandSequence;
            var ownedBefore = new List<string>(state.OwnedEvidence);

            Diag.Clear();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*SaveService.Save failed for bad/slot.*"));
            Assert.IsFalse(_saves.Save(state, "bad/slot"), "save with a path separator in the slot must fail");
            Assert.Greater(Diag.RecentErrors.Count, 0, "a failed save must be reported through Diag");
            bool named = false;
            for (int i = 0; i < Diag.RecentErrors.Count; i++)
                if (Diag.RecentErrors[i].Contains("bad/slot")) named = true;
            Assert.IsTrue(named, "Diag must name the failing slot");

            Assert.AreEqual(sceneBefore, state.CurrentSceneId, "scene must be unchanged after a failed save");
            Assert.AreEqual(phaseBefore, state.Phase, "phase must be unchanged after a failed save");
            Assert.AreEqual(sequenceBefore, state.LastCommandSequence, "sequence must be unchanged after a failed save");
            AssertSameSet(ownedBefore, state.OwnedEvidence, "evidence must be unchanged after a failed save");
        }

        [Test]
        public void SV10_LastSaveWinsForTheSameSlot()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            for (int i = 0; i < 2; i++)
                ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue));
            Assert.IsTrue(_saves.Save(state, Slot), "first save must succeed");
            long sequenceA = state.LastCommandSequence;

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue));
            Assert.IsTrue(_saves.Save(state, Slot), "second save must succeed");
            long sequenceB = state.LastCommandSequence;
            Assert.Greater(sequenceB, sequenceA, "second state must have a newer sequence");

            Assert.AreEqual(LoadOutcome.Ok, _saves.Load(Slot, out var snapshot), "load must succeed");
            Assert.AreEqual(sequenceB, snapshot.lastCommandSequence, "newest save must win");
            Assert.AreEqual(state.CurrentSceneId, snapshot.currentSceneId, "newest save scene must win");
            Assert.AreEqual(state.Phase, snapshot.phase, "newest save phase must win");
            Assert.AreEqual(state.OwnedEvidence.Count, snapshot.ownedEvidenceIds != null ? snapshot.ownedEvidenceIds.Length : 0,
                "newest save evidence must win");
        }

        [Test]
        public void HI01_HintServiceResolvesEveryScene()
        {
            var hints = new HintService(_index);
            for (int i = 0; i < FrozenIds.Scenes.Length; i++)
            {
                var state = TestContent.FreshState();
                state.CurrentSceneId = FrozenIds.Scenes[i];
                state.Phase = Phases.ReadyToExit;

                HintContext context = null;
                Assert.DoesNotThrow(() => context = hints.Resolve(state), "Resolve must not throw for " + FrozenIds.Scenes[i]);
                Assert.IsNotNull(context, "Resolve must return a context for " + FrozenIds.Scenes[i]);
                bool hasContent = !string.IsNullOrEmpty(context.sceneTip)
                    || !string.IsNullOrEmpty(context.nextAction)
                    || context.levels.Count > 0;
                Assert.IsTrue(hasContent,
                    "scene " + FrozenIds.Scenes[i] + " must produce a tip, next action, or hint level");
            }
        }

        [Test]
        public void HI02_EveryPuzzleHasExactlyThreeHintLevels()
        {
            for (int i = 0; i < FrozenIds.Puzzles.Length; i++)
            {
                var puzzleId = FrozenIds.Puzzles[i];
                var levels = new List<int>();
                foreach (var pair in _index.Hints)
                {
                    if (pair.Value.scopeType != "puzzle" || pair.Value.scopeId != puzzleId) continue;
                    if (!levels.Contains(pair.Value.level)) levels.Add(pair.Value.level);
                }
                levels.Sort();
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, levels,
                    puzzleId + " must have exactly H1/H2/H3 hints, got [" + string.Join(",", levels.ConvertAll(v => v.ToString()).ToArray()) + "]");
            }
        }

        [Test]
        public void HI03_OpeningH3DoesNotChangeSolvedPuzzles()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");
            var solvedBefore = new List<string>(state.SolvedPuzzles);

            var result = TestContent.Send(dispatcher, CommandKinds.OpenHint, numeric: 3);
            Assert.AreNotEqual(ResultStatus.Rejected, result.status, "OpenHint must be accepted");
            AssertSameSet(solvedBefore, state.SolvedPuzzles, "OpenHint must not solve any puzzle");

            var usage = state.GetHintUsage(state.CurrentSceneId, false);
            Assert.IsNotNull(usage, "hint usage must be recorded");
            Assert.AreEqual(3, usage.maxLevel, "opening H3 must record maxLevel 3");
        }

        [Test]
        public void HI04_AssistSolutionsMatchContentDrivenSolutions()
        {
            for (int i = 0; i < FrozenIds.Puzzles.Length; i++)
            {
                var puzzleId = FrozenIds.Puzzles[i];
                var puzzle = _index.Puzzle(puzzleId);

                var dispatcher = TestContent.FreshDispatcher(_index, out var state);
                TestContent.SolveSceneTo(dispatcher, state, puzzle.sceneId);
                TestContent.EnsureInteraction(dispatcher, state, puzzleId);

                var checkpoint = state.ToSnapshot(_contentVersion, "", Slot);
                var manualState = GameState.FromSnapshot(JsonUtility.FromJson<ProgressSnapshot>(JsonUtility.ToJson(checkpoint, false)));
                var manualDispatcher = new CommandDispatcher(_index, manualState);
                AssertSameSet(state.OwnedEvidence, manualState.OwnedEvidence, puzzleId + " cloned state owned evidence");

                TestContent.SolvePuzzle(dispatcher, state, puzzleId);
                SolveManually(manualDispatcher, manualState, puzzle);
                Assert.IsTrue(manualState.IsPuzzleSolved(puzzleId), puzzleId + " manual content-driven solution must solve");
                AssertSameSet(state.OwnedEvidence, manualState.OwnedEvidence, puzzleId + " owned evidence must match assist vs manual");
                AssertSameSet(state.SolvedPuzzles, manualState.SolvedPuzzles, puzzleId + " solved puzzles must match assist vs manual");
            }
        }

        [Test]
        public void HI05_AssistCannotSolveWithoutReachingRequiredEvidence()
        {
            var dispatcher = TestContent.FreshDispatcher(_index, out var state);
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.GoToScene, targetId: "s150", origin: Origins.Fixture));
            TestContent.AdvanceUntilChoiceOrInteraction(dispatcher, state);
            Assert.AreEqual("p04", state.ActivePuzzleId, "fixture jump must activate p04");
            Assert.IsFalse(state.Owns("ev06"), "fixture jump must not grant ev06");
            Assert.IsFalse(state.Owns("ev07"), "fixture jump must not grant ev07");

            var plan = TestAssistPlanner.Plan(dispatcher.Index, state);
            Assert.IsFalse(plan.CanRun, "assist must not be able to run without required evidence");
            Assert.AreEqual("missing_required_evidence", plan.Blocker, "assist blocker must be missing_required_evidence");
            Assert.AreEqual(0, plan.Commands.Count, "assist must not emit commands without required evidence");

            int ownedBefore = state.OwnedEvidence.Count;
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_02"));
            var select = TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev06", "ev07" });
            Assert.AreEqual(ResultStatus.Rejected, select.status, "selecting unowned evidence must be rejected");
            Assert.AreEqual("evidence_not_owned", select.reasonCode, "rejection reason must be evidence_not_owned");
            var present = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Rejected, present.status, "presenting without evidence must be rejected");
            Assert.AreEqual(ownedBefore, state.OwnedEvidence.Count, "no evidence may be granted by failed assist commands");
            Assert.IsFalse(state.IsPuzzleSolved("p04"), "p04 must remain unsolved");
        }

        [Test]
        public void HI06_HubHintsDoNotLeakPuzzleHintsAndS310HintsAreP09()
        {
            var hints = new HintService(_index);

            var dispatcher = TestContent.FreshDispatcher(_index, out var hubState);
            TestContent.SolveSceneTo(dispatcher, hubState, "s100");
            TestContent.EnsureHubActive(dispatcher, hubState, "s100");
            var hubContext = hints.Resolve(hubState);
            Assert.AreNotEqual("puzzle", hubContext.scopeType, "hub hints must not be puzzle scoped");
            Assert.AreEqual("s100", hubContext.scopeId, "hub hint scope must be the hub scene");
            Assert.AreEqual("s110", hubContext.nextAction, "hub next action must be the first incomplete child");
            for (int i = 0; i < hubContext.levels.Count; i++)
                Assert.AreEqual("scene", hubContext.levels[i].scopeType,
                    "hub hint level " + hubContext.levels[i].id + " must not target a puzzle");

            ReachInteraction(dispatcher, hubState, "s310", "p09");
            var puzzleContext = hints.Resolve(hubState);
            Assert.AreEqual("puzzle", puzzleContext.scopeType, "s310 interaction must resolve as a puzzle hint");
            Assert.AreEqual("p09", puzzleContext.scopeId, "s310 interaction hint scope must be p09");
            Assert.AreEqual(3, puzzleContext.levels.Count, "p09 must expose exactly 3 hint levels");
        }

        [Test]
        public void UI07_ContainRectAndRegionLocalPointMath()
        {
            var full = InvestigationScreen.ComputeContainRect(new Vector2(1600f, 900f), new Vector2(1920f, 1080f));
            Assert.AreEqual(0f, full.x, 0.001f, "full contain x");
            Assert.AreEqual(0f, full.y, 0.001f, "full contain y");
            Assert.AreEqual(1600f, full.width, 0.001f, "full contain width");
            Assert.AreEqual(900f, full.height, 0.001f, "full contain height");

            var letterbox = InvestigationScreen.ComputeContainRect(new Vector2(1600f, 1200f), new Vector2(1920f, 1080f));
            Assert.AreEqual(0f, letterbox.x, 0.001f, "letterbox x");
            Assert.AreEqual(150f, letterbox.y, 0.001f, "letterbox top/bottom bar");
            Assert.AreEqual(1600f, letterbox.width, 0.001f, "letterbox width");
            Assert.AreEqual(900f, letterbox.height, 0.001f, "letterbox height");

            var degenerate = InvestigationScreen.ComputeContainRect(new Vector2(1600f, 900f), Vector2.zero);
            Assert.AreEqual(1600f, degenerate.width, 0.001f, "degenerate image must fall back to the panel width");
            Assert.AreEqual(900f, degenerate.height, 0.001f, "degenerate image must fall back to the panel height");

            var region = new AssetRegion { regionId = "r", x = 0.5f, y = 0.5f, width = 0.1f, height = 0.1f };
            var displayed = new Rect(0f, 0f, 1600f, 900f);
            Assert.IsTrue(InvestigationScreen.TryRegionLocalPoint(new Vector2(805f, 455f), displayed, region),
                "center of the mapped region must be inside");
            Assert.IsTrue(InvestigationScreen.TryRegionLocalPoint(new Vector2(800f, 450f), displayed, region),
                "top-left boundary must be inclusive");
            Assert.IsTrue(InvestigationScreen.TryRegionLocalPoint(new Vector2(960f, 540f), displayed, region),
                "bottom-right boundary must be inclusive");
            Assert.IsFalse(InvestigationScreen.TryRegionLocalPoint(new Vector2(799f, 455f), displayed, region),
                "point left of the region must be outside");
            Assert.IsFalse(InvestigationScreen.TryRegionLocalPoint(new Vector2(805f, 449f), displayed, region),
                "point above the region must be outside");
            Assert.IsFalse(InvestigationScreen.TryRegionLocalPoint(new Vector2(961f, 540f), displayed, region),
                "point right of the region must be outside");
            Assert.IsFalse(InvestigationScreen.TryRegionLocalPoint(new Vector2(805f, 455f), displayed, null),
                "null region must be outside");

            var wholeRegion = new AssetRegion { regionId = "all", x = 0f, y = 0f, width = 1f, height = 1f };
            var rect = new Rect(10f, 20f, 160f, 90f);
            Assert.IsTrue(InvestigationScreen.TryRegionLocalPoint(new Vector2(10f, 20f), rect, wholeRegion), "top-left corner");
            Assert.IsTrue(InvestigationScreen.TryRegionLocalPoint(new Vector2(90f, 65f), rect, wholeRegion), "center");
            Assert.IsTrue(InvestigationScreen.TryRegionLocalPoint(new Vector2(170f, 110f), rect, wholeRegion), "bottom-right corner");
            Assert.IsFalse(InvestigationScreen.TryRegionLocalPoint(new Vector2(9f, 20f), rect, wholeRegion), "left of rect");
            Assert.IsFalse(InvestigationScreen.TryRegionLocalPoint(new Vector2(10f, 111f), rect, wholeRegion), "below rect");
        }

        private static void ReachInteraction(CommandDispatcher dispatcher, GameState state, string sceneId, string puzzleId)
        {
            TestContent.SolveSceneTo(dispatcher, state, sceneId);
            TestContent.EnsureInteraction(dispatcher, state, puzzleId);
        }

        private static void ExpectAccepted(CommandResult result)
        {
            Assert.IsNotNull(result, "command result must not be null");
            Assert.AreNotEqual(ResultStatus.Rejected, result.status,
                "command must be accepted but was rejected: " + result.reasonCode + " " + result.message);
        }

        private static void AssertSameSet(List<string> expected, List<string> actual, string label)
        {
            var a = new List<string>(expected);
            var b = new List<string>(actual);
            a.Sort(StringComparer.Ordinal);
            b.Sort(StringComparer.Ordinal);
            CollectionAssert.AreEqual(a, b, label);
        }

        private static void SolveManually(CommandDispatcher d, GameState state, PuzzleDefinitionDto puzzle)
        {
            switch (puzzle.type)
            {
                case PuzzleTypes.HotspotSet:
                    if (puzzle.regions != null)
                        for (int i = 0; i < puzzle.regions.Length; i++)
                            ExpectAccepted(TestContent.Send(d, CommandKinds.InspectRegion, puzzle.regions[i].regionId));
                    break;
                case PuzzleTypes.StatementRefutation:
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SelectStatement, puzzle.targetStatementId));
                    SendFirstEvidence(d, puzzle.sufficientEvidenceSets, null);
                    SendFirstChoice(d, puzzle.correctChoiceIds);
                    ExpectAccepted(TestContent.Send(d, CommandKinds.PresentEvidence));
                    break;
                case PuzzleTypes.SourceMapping:
                case PuzzleTypes.TraceMapping:
                    if (puzzle.valuePairs != null)
                        for (int i = 0; i < puzzle.valuePairs.Length; i++)
                            ExpectAccepted(TestContent.Send(d, CommandKinds.PlaceCard, slot: puzzle.valuePairs[i].key, value: puzzle.valuePairs[i].value));
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.TimelineOrder:
                case PuzzleTypes.Chronology:
                case PuzzleTypes.DeploymentChain:
                    PlaceOrdered(d, puzzle.orderedIds);
                    SendFirstChoice(d, puzzle.correctChoiceIds);
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.CrossCamera:
                    SendFirstChoiceWindow(d, puzzle.correctChoiceIds);
                    SendFirstEvidence(d, puzzle.sufficientEvidenceSets, null);
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.ArgumentChoice:
                    SendFirstChoice(d, puzzle.correctChoiceIds);
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.DiffSelect:
                    if (puzzle.correctChoiceIds != null)
                        for (int i = 0; i < puzzle.correctChoiceIds.Length; i++)
                            ExpectAccepted(TestContent.Send(d, CommandKinds.SelectChoice, puzzle.correctChoiceIds[i]));
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.AssumptionLink:
                    SendFirstChoice(d, puzzle.correctChoiceIds);
                    SendFirstEvidence(d, puzzle.sufficientEvidenceSets, null);
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.MatrixGap:
                {
                    string[] dims = { "sensor", "overrideState", "version", "condition" };
                    for (int i = 0; i < dims.Length && puzzle.orderedIds != null && i < puzzle.orderedIds.Length; i++)
                        ExpectAccepted(TestContent.Send(d, CommandKinds.PlaceCard, slot: dims[i], value: puzzle.orderedIds[i]));
                    SendFirstChoice(d, puzzle.correctChoiceIds);
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.Counterfactual:
                    if (puzzle.requiredCaseIds != null)
                        for (int i = 0; i < puzzle.requiredCaseIds.Length; i++)
                            ExpectAccepted(TestContent.Send(d, CommandKinds.RunCounterfactualCase, puzzle.requiredCaseIds[i]));
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.CausalGraph:
                    if (puzzle.requiredEdges != null)
                        for (int i = 0; i < puzzle.requiredEdges.Length; i++)
                            ExpectAccepted(TestContent.Send(d, CommandKinds.ConnectNodes,
                                puzzle.requiredEdges[i].kind, new[] { puzzle.requiredEdges[i].from, puzzle.requiredEdges[i].to }));
                    if (puzzle.edgeProofRequirements != null)
                        for (int i = 0; i < puzzle.edgeProofRequirements.Length; i++)
                        {
                            var proof = puzzle.edgeProofRequirements[i];
                            SendFirstEvidence(d, proof.sufficientEvidenceSets, proof.from + "|" + proof.to + "|" + proof.kind);
                        }
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                case PuzzleTypes.ResponsibilityMatrix:
                    if (puzzle.reportProofRequirements != null)
                        for (int i = 0; i < puzzle.reportProofRequirements.Length; i++)
                        {
                            var proof = puzzle.reportProofRequirements[i];
                            ExpectAccepted(TestContent.Send(d, CommandKinds.PlaceCard, slot: proof.section, value: proof.item));
                            SendFirstEvidence(d, proof.sufficientEvidenceSets, proof.item);
                        }
                    ExpectAccepted(TestContent.Send(d, CommandKinds.PlaceCard, slot: "limit", value: "no_intent_or_criminal_verdict"));
                    ExpectAccepted(TestContent.Send(d, CommandKinds.SubmitPuzzle));
                    break;
                default:
                    Assert.Fail("manual solution missing for puzzle type " + puzzle.type);
                    break;
            }
        }

        private static void PlaceOrdered(CommandDispatcher d, string[] orderedIds)
        {
            if (orderedIds == null) return;
            for (int i = 0; i < orderedIds.Length; i++)
                ExpectAccepted(TestContent.Send(d, CommandKinds.PlaceCard, slot: "order", value: orderedIds[i]));
        }

        private static void SendFirstChoice(CommandDispatcher d, string[] choiceIds)
        {
            if (choiceIds == null || choiceIds.Length == 0) return;
            ExpectAccepted(TestContent.Send(d, CommandKinds.SelectChoice, choiceIds[0]));
        }

        private static void SendFirstChoiceWindow(CommandDispatcher d, string[] choiceIds)
        {
            if (choiceIds == null || choiceIds.Length == 0) return;
            ExpectAccepted(TestContent.Send(d, CommandKinds.SelectComparisonWindow, choiceIds[0]));
        }

        private static void SendFirstEvidence(CommandDispatcher d, EvidenceSetDto[] sets, string targetId)
        {
            var ids = TestContent.FirstEvidenceSet(sets);
            if (ids == null) throw new InvalidOperationException("no sufficient evidence set for target " + (targetId ?? "<default>"));
            ExpectAccepted(TestContent.Send(d, CommandKinds.SelectEvidence, targetId, ids));
        }

        private void DeleteSlot(string slot)
        {
            var path = Path.Combine(Application.persistentDataPath, "saves", slot + ".json");
            string[] paths = { path, path + ".bak", path + ".tmp" };
            for (int i = 0; i < paths.Length; i++)
                if (File.Exists(paths[i])) File.Delete(paths[i]);
        }
    }
}
