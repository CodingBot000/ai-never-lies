using System;
using System.Collections.Generic;
using System.IO;
using ANL.Core.Conditions;
using ANL.Core.Contracts;
using ANL.Core.Puzzles;
using ANL.Core.State;
using ANL.Core.Validation;
using NUnit.Framework;
using UnityEngine;

namespace ANL.Tests.EditMode
{
    public class ContentContractTests
    {
        private static readonly HashSet<string> OptionalEvidence = new HashSet<string> { "ev34", "ev35" };

        [Test]
        public void CT01_FrozenIdSetsAreExactAndDuplicateFree()
        {
            var index = TestContent.Load();

            Assert.AreEqual(43, index.Scenes.Count, "scene count");
            Assert.AreEqual(36, index.Evidence.Count, "evidence count");
            Assert.AreEqual(20, index.Puzzles.Count, "puzzle count");

            CollectionAssert.AreEquivalent(FrozenIds.Scenes, index.Scenes.Keys, "scene ids must equal FrozenIds.Scenes");
            CollectionAssert.AreEquivalent(FrozenIds.Evidence, index.Evidence.Keys, "evidence ids must equal FrozenIds.Evidence");
            CollectionAssert.AreEquivalent(FrozenIds.Puzzles, index.Puzzles.Keys, "puzzle ids must equal FrozenIds.Puzzles");

            AssertNoDuplicates(ReadIds<SceneFileDto, SceneDefinitionDto>(Path.Combine(TestContent.ContentDir, "scenes.json"), f => f.items, s => s.id), "scenes.json");
            AssertNoDuplicates(ReadIds<EvidenceFileDto, EvidenceDefinitionDto>(Path.Combine(TestContent.ContentDir, "evidence.json"), f => f.items, e => e.id), "evidence.json");
            AssertNoDuplicates(ReadIds<PuzzleFileDto, PuzzleDefinitionDto>(Path.Combine(TestContent.ContentDir, "puzzles.json"), f => f.items, p => p.id), "puzzles.json");
        }

        [Test]
        public void CT02_ContentValidatorReportsZeroErrors()
        {
            var index = TestContent.Load();
            var errors = ContentValidatorCore.Validate(index, TestContent.AssetIds());
            Assert.AreEqual(0, errors.Count,
                "content validation errors (" + errors.Count + "):\n" + string.Join("\n", errors.ToArray()));

            foreach (var pair in index.Puzzles)
            {
                var puzzle = pair.Value;
                AssertEvidenceSets(puzzle.sufficientEvidenceSets, puzzle.id + ".sufficientEvidenceSets");
                if (puzzle.edgeProofRequirements != null)
                    for (int i = 0; i < puzzle.edgeProofRequirements.Length; i++)
                        AssertEvidenceSets(puzzle.edgeProofRequirements[i].sufficientEvidenceSets,
                            puzzle.id + ".edgeProofRequirements[" + i + "].sufficientEvidenceSets");
                if (puzzle.reportProofRequirements != null)
                    for (int i = 0; i < puzzle.reportProofRequirements.Length; i++)
                        AssertEvidenceSets(puzzle.reportProofRequirements[i].sufficientEvidenceSets,
                            puzzle.id + ".reportProofRequirements[" + i + "].sufficientEvidenceSets");
            }
        }

        private static void AssertEvidenceSets(EvidenceSetDto[] sets, string label)
        {
            if (sets == null) return;
            for (int i = 0; i < sets.Length; i++)
            {
                Assert.IsNotNull(sets[i], label + "[" + i + "] must not be null");
                Assert.IsNotNull(sets[i].requiredIds,
                    label + "[" + i + "] must be serialized as {\"requiredIds\":[...]} so Unity JsonUtility populates it");
            }
        }

        [Test]
        public void CT03_PuzzleRequiredEvidenceIsOwnedWhenPuzzleBecomesActive()
        {
            var index = TestContent.Load();

            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s150");
            TestContent.EnsureInteraction(dispatcher, state, "p04");
            Assert.IsTrue(state.Owns("ev02"), "p04: ev02 must be owned before solving");
            Assert.IsTrue(state.Owns("ev06"), "p04: ev06 must be owned before solving");
            Assert.IsTrue(state.Owns("ev07"), "p04: ev07 must be owned before solving");
            Assert.IsFalse(state.IsPuzzleSolved("p04"), "p04 must not be solved while checking access");

            for (int i = 0; i < FrozenIds.Puzzles.Length; i++)
            {
                var puzzle = index.Puzzle(FrozenIds.Puzzles[i]);
                var run = TestContent.FreshDispatcher(index, out var runState);
                TestContent.SolveSceneTo(run, runState, puzzle.sceneId);
                TestContent.EnsureInteraction(run, runState, puzzle.id);
                if (puzzle.requiredEvidenceIds == null) continue;
                for (int j = 0; j < puzzle.requiredEvidenceIds.Length; j++)
                {
                    var id = puzzle.requiredEvidenceIds[j];
                    Assert.IsTrue(runState.Owns(id),
                        puzzle.id + " at " + puzzle.sceneId + ": required evidence " + id +
                        " must be owned before the puzzle can be solved");
                }
                Assert.IsFalse(runState.IsPuzzleSolved(puzzle.id),
                    puzzle.id + " must remain unsolved while only access is checked");
            }
        }

        [Test]
        public void CT04_S150PostStartGrantsArriveOnlyAfterPuzzleSolved()
        {
            var index = TestContent.Load();
            var s150 = index.Scene("s150");
            Assert.IsNotNull(s150, "s150 must exist");

            bool ev08Post = false;
            bool ev09Post = false;
            var grants = s150.grants != null ? s150.grants : new EvidenceGrantDto[0];
            for (int i = 0; i < grants.Length; i++)
            {
                if (grants[i].evidenceId == "ev08" && grants[i].trigger == "post_start") ev08Post = true;
                if (grants[i].evidenceId == "ev09" && grants[i].trigger == "post_start") ev09Post = true;
            }
            Assert.IsTrue(ev08Post, "s150 ev08 grant must use trigger post_start");
            Assert.IsTrue(ev09Post, "s150 ev09 grant must use trigger post_start");

            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s150");
            TestContent.EnsureInteraction(dispatcher, state, "p04");
            Assert.IsFalse(state.Owns("ev08"), "ev08 must not be owned before p04 is solved");
            Assert.IsFalse(state.Owns("ev09"), "ev09 must not be owned before p04 is solved");

            TestContent.SolvePuzzle(dispatcher, state, "p04");
            Assert.IsTrue(state.IsPuzzleSolved("p04"), "p04 must be solved");
            Assert.IsTrue(state.Owns("ev08"), "ev08 must be granted after p04 is solved");
            Assert.IsTrue(state.Owns("ev09"), "ev09 must be granted after p04 is solved");
        }

        [Test]
        public void CT05_FullRunWithoutOptionalScenesOwnsAllMandatoryEvidence()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s710");
            TestContent.FinishScene(dispatcher, state);

            Assert.AreEqual("s710", state.CurrentSceneId, "run must end at s710");
            Assert.IsTrue(state.IsSceneCompleted("s710"), "s710 must be completed");

            for (int i = 0; i < FrozenIds.Evidence.Length; i++)
            {
                var id = FrozenIds.Evidence[i];
                if (OptionalEvidence.Contains(id))
                    Assert.IsFalse(state.Owns(id), "optional evidence " + id + " must not be granted without visiting s520/s530");
                else
                    Assert.IsTrue(state.Owns(id), "mandatory evidence " + id + " must be owned after the full run");
            }

            for (int i = 0; i < FrozenIds.Puzzles.Length; i++)
                Assert.IsTrue(state.IsPuzzleSolved(FrozenIds.Puzzles[i]), FrozenIds.Puzzles[i] + " must be solved");
        }

        [Test]
        public void CT06_AdvanceDialogueIsRejectedDuringPuzzleInteraction()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s150");
            TestContent.EnsureInteraction(dispatcher, state, "p04");

            int cursorBefore = state.DialogueCursor;
            var result = TestContent.Send(dispatcher, CommandKinds.AdvanceDialogue);
            Assert.AreEqual(ResultStatus.Rejected, result.status,
                "AdvanceDialogue must be rejected in interaction phase, got " + result.status + " " + result.reasonCode);
            Assert.IsTrue(result.reasonCode == "not_in_dialogue",
                "rejection reason should be not_in_dialogue, got " + result.reasonCode);
            Assert.AreEqual(Phases.Interaction, state.Phase, "phase must stay interaction");
            Assert.AreEqual(cursorBefore, state.DialogueCursor, "dialogue cursor must not move during interaction");
            Assert.IsFalse(state.IsPostDone("s150"), "s150 post dialogue must not be done before p04 is solved");

            var s150 = index.Scene("s150");
            if (s150.postDialogueIds != null)
                for (int i = 0; i < s150.postDialogueIds.Length; i++)
                    Assert.IsNotNull(index.Dialogue(s150.postDialogueIds[i]),
                        "post dialogue " + s150.postDialogueIds[i] + " must exist but not be played");
        }

        [Test]
        public void CT07_S640DoesNotRequireP20AndIsReachedWithP20Unsolved()
        {
            var index = TestContent.Load();
            var s640 = index.Scene("s640");
            Assert.IsNotNull(s640, "s640 must exist");

            if (s640.entryConditions != null)
                for (int i = 0; i < s640.entryConditions.Length; i++)
                    Assert.IsFalse(s640.entryConditions[i].kind == ConditionKind.PuzzleSolved && s640.entryConditions[i].id == "p20",
                        "s640 entryConditions must not contain PuzzleSolved:p20");

            Assert.IsNotNull(s640.interactionIds, "s640 must list interactions");
            CollectionAssert.Contains(s640.interactionIds, "p20", "s640 must list p20 as its interaction");

            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s640");
            TestContent.EnsureInteraction(dispatcher, state, "p20");

            for (int i = 1; i <= 19; i++)
            {
                var id = "p" + i.ToString("00");
                Assert.IsTrue(state.IsPuzzleSolved(id), id + " must be solved before reaching s640");
            }
            Assert.IsFalse(state.IsPuzzleSolved("p20"), "p20 must be unsolved when s640 starts");
        }

        [Test]
        public void CT08_ConditionsFailClosedAndUnknownPuzzleTypeIsRejected()
        {
            var index = TestContent.Load();
            var state = TestContent.FreshState();

            Assert.IsFalse(ConditionEvaluator.Evaluate(null, state, index), "null condition must fail closed");
            Assert.IsFalse(ConditionEvaluator.Evaluate(new ConditionDto { kind = "NoSuchKind", id = "x" }, state, index),
                "unknown condition kind must fail closed");
            Assert.IsFalse(ConditionEvaluator.Evaluate(new ConditionDto { kind = ConditionKind.EvidenceOwned, id = "" }, state, index),
                "empty condition id must fail closed");
            Assert.IsFalse(ConditionEvaluator.Evaluate(new ConditionDto { kind = "", id = "x" }, state, index),
                "empty condition kind must fail closed");
            Assert.IsFalse(ConditionKind.IsKnown("Bogus"), "ConditionKind.IsKnown(Bogus) must be false");

            var unknown = new PuzzleDefinitionDto { id = "pXX", type = "quantum_entanglement", successText = "x" };
            var evaluation = PuzzleEvaluator.Evaluate(unknown, new PuzzleDraftDto { puzzleId = "pXX" }, state, index);
            Assert.AreEqual(ResultStatus.Rejected, evaluation.Status,
                "unknown puzzle type must evaluate to Rejected, got " + evaluation.Status);

            state.Phase = Phases.Interaction;
            state.ActivePuzzleId = "p_nonexistent";
            var dispatcher = new ANL.Core.Commands.CommandDispatcher(index, state);
            var result = dispatcher.Execute(new GameCommand
            {
                commandId = "ct08_unknown",
                sequence = 1,
                sceneId = state.CurrentSceneId,
                origin = Origins.TestAssist,
                type = CommandKinds.SubmitPuzzle
            });
            Assert.AreEqual(ResultStatus.Rejected, result.status, "dispatcher must reject commands for an unknown active puzzle");
            Assert.AreEqual("no_active_puzzle", result.reasonCode, "rejection reason should be no_active_puzzle");
        }

        private static void AssertNoDuplicates(string[] ids, string label)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < ids.Length; i++)
                Assert.IsTrue(seen.Add(ids[i]), label + " contains duplicate id " + ids[i]);
        }

        private static string[] ReadIds<TFile, TItem>(string path, Func<TFile, TItem[]> items, Func<TItem, string> key)
        {
            var file = JsonUtility.FromJson<TFile>(File.ReadAllText(path));
            var array = items(file);
            var ids = new string[array.Length];
            for (int i = 0; i < array.Length; i++) ids[i] = key(array[i]);
            return ids;
        }
    }
}
