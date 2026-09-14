using System;
using System.Collections.Generic;
using System.Globalization;
using ANL.Core.Commands;
using ANL.Core.Conditions;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.State;
using NUnit.Framework;

namespace ANL.Tests.EditMode
{
    public class StateTests
    {
        [Test]
        public void ST01_DuplicateEvidenceGrantsAreIdempotent()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            dispatcher.EnterScene("s140");
            TestContent.AdvanceUntilChoiceOrInteraction(dispatcher, state);
            Assert.AreEqual(1, TestContent.CountOf(state.OwnedEvidence, "ev06"), "ev06 granted once after first completion");
            Assert.AreEqual(1, TestContent.CountOf(state.OwnedEvidence, "ev07"), "ev07 granted once after first completion");

            dispatcher.EnterScene("s140");
            dispatcher.EnterScene("s140");
            Assert.AreEqual(1, TestContent.CountOf(state.OwnedEvidence, "ev06"), "ev06 must stay unique after re-entry");
            Assert.AreEqual(1, TestContent.CountOf(state.OwnedEvidence, "ev07"), "ev07 must stay unique after re-entry");
        }

        [Test]
        public void ST02_DuplicateCommandIsRejected()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            var command = new GameCommand
            {
                commandId = "st02_duplicate",
                sequence = state.LastCommandSequence + 1,
                sceneId = state.CurrentSceneId,
                origin = Origins.User,
                type = CommandKinds.AdvanceDialogue
            };

            var first = dispatcher.Execute(command);
            Assert.AreEqual(ResultStatus.Accepted, first.status, "first execution must be accepted");
            var second = dispatcher.Execute(command);
            Assert.AreEqual(ResultStatus.Rejected, second.status, "second execution must be rejected");
            Assert.AreEqual("duplicate_command", second.reasonCode, "rejection reason must be duplicate_command");
        }

        [Test]
        public void ST03_WrongStatementIsRejectedAndCorrectStatementSolves()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_01"));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev06", "ev07" }));
            var wrong = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Rejected, wrong.status, "wrong statement must be rejected, got " + wrong.status);
            Assert.IsFalse(state.IsPuzzleSolved("p04"), "p04 must not be solved by the wrong statement");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_02"));
            var solved = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Solved, solved.status, "correct statement must solve p04, got " + solved.status);
            Assert.IsTrue(state.IsPuzzleSolved("p04"), "p04 must be solved");
        }

        [Test]
        public void ST04_EvidenceSelectionOrderDoesNotMatter()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_02"));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev07", "ev06" }));
            var result = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Solved, result.status, "reversed evidence order must still solve, got " + result.status);
        }

        [Test]
        public void ST05_PartialEvidenceReturnsPartial()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_02"));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev06" }));
            var result = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Partial, result.status, "single required evidence must be partial, got " + result.status);
            Assert.IsFalse(state.IsPuzzleSolved("p04"), "p04 must not be solved by partial evidence");
        }

        [Test]
        public void ST06_ExtraEvidenceStillSolves()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");
            Assert.IsTrue(state.Owns("ev05"), "ev05 must be owned from the s120 dialogue grant");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st150_02"));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev06", "ev07", "ev05" }));
            var result = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Solved, result.status, "extra evidence must still solve, got " + result.status);
        }

        [Test]
        public void ST07_SelectingUnownedEvidenceIsRejected()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s150", "p04");
            Assert.IsFalse(state.Owns("ev10"), "ev10 must not be owned at s150");

            var result = TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev10" });
            Assert.AreEqual(ResultStatus.Rejected, result.status, "unowned evidence must be rejected");
            Assert.AreEqual("evidence_not_owned", result.reasonCode, "rejection reason must be evidence_not_owned");
        }

        [Test]
        public void ST08_CausalCyclesAndSelfEdgesAreRejected()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s610", "p17");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ConnectNodes, "causal", new[] { "n_brake_limit", "n_fault" }));
            var cycle = TestContent.Send(dispatcher, CommandKinds.ConnectNodes, "causal", new[] { "n_fault", "n_brake_limit" });
            Assert.AreEqual(ResultStatus.Rejected, cycle.status, "causal cycle must be rejected");
            Assert.AreEqual("cycle_edge", cycle.reasonCode, "cycle rejection reason must be cycle_edge");

            var self = TestContent.Send(dispatcher, CommandKinds.ConnectNodes, "causal", new[] { "n_fault", "n_fault" });
            Assert.AreEqual(ResultStatus.Rejected, self.status, "self edge must be rejected");
            Assert.AreEqual("self_edge", self.reasonCode, "self edge rejection reason must be self_edge");

            var draft = state.GetDraft("p17", false);
            Assert.IsNotNull(draft, "p17 draft must exist");
            Assert.AreEqual(1, draft.edges != null ? draft.edges.Length : 0, "only the first edge may remain in the draft");
        }

        [Test]
        public void ST09_SignalKindOnCausalPairDoesNotCountAsRequiredCausalEdge()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s610", "p17");
            var puzzle = index.Puzzle("p17");

            for (int i = 0; i < puzzle.requiredEdges.Length; i++)
            {
                var edge = puzzle.requiredEdges[i];
                var kind = edge.from == "n_onroad" && edge.to == "n_brake_limit" ? "signal" : edge.kind;
                ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ConnectNodes, kind, new[] { edge.from, edge.to }));
            }

            for (int i = 0; i < puzzle.edgeProofRequirements.Length; i++)
            {
                var proof = puzzle.edgeProofRequirements[i];
                var ids = TestContent.FirstEvidenceSet(proof.sufficientEvidenceSets);
                Assert.IsNotNull(ids, "p17 edge proof " + proof.from + "->" + proof.to + " needs an evidence set");
                ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence,
                    proof.from + "|" + proof.to + "|" + proof.kind, ids));
            }

            var submit = TestContent.Send(dispatcher, CommandKinds.SubmitPuzzle);
            Assert.AreEqual(ResultStatus.Partial, submit.status,
                "one wrong-kind edge must yield Partial, got " + submit.status + " " + submit.reasonCode);
            Assert.IsFalse(state.IsPuzzleSolved("p17"), "p17 must not be solved with a signal-kind causal edge");

            var draft = state.GetDraft("p17", false);
            bool signalEdge = false;
            if (draft.edges != null)
                for (int i = 0; i < draft.edges.Length; i++)
                    if (draft.edges[i].from == "n_onroad" && draft.edges[i].to == "n_brake_limit" && draft.edges[i].kind == "signal")
                        signalEdge = true;
            Assert.IsTrue(signalEdge, "dispatcher must accept the per-edge signal kind");
        }

        [Test]
        public void ST10_NoPublicityNodeCanBeUsedAsCauseAndP19DoesNotNeedIt()
        {
            var index = TestContent.Load();
            var nodes = index.GraphNodes();
            Assert.IsTrue(nodes.Count > 0, "graph must contain nodes");
            for (int i = 0; i < nodes.Count; i++)
            {
                var id = nodes[i].id ?? "";
                Assert.IsFalse(id.IndexOf("public", StringComparison.OrdinalIgnoreCase) >= 0,
                    "graph node " + id + " must not be a publicity node");
                Assert.IsFalse(id.IndexOf("postincident", StringComparison.OrdinalIgnoreCase) >= 0,
                    "graph node " + id + " must not be a post-incident node");
                Assert.IsFalse(nodes[i].kind == "publicity", "graph node " + id + " must not have publicity kind");
                if (nodes[i].evidenceIds != null)
                    for (int j = 0; j < nodes[i].evidenceIds.Length; j++)
                        Assert.IsFalse(nodes[i].evidenceIds[j] == "ev34" || nodes[i].evidenceIds[j] == "ev35",
                            "graph node " + id + " must not reference optional family evidence " + nodes[i].evidenceIds[j]);
            }

            var p17 = index.Puzzle("p17");
            for (int i = 0; i < p17.requiredEdges.Length; i++)
            {
                Assert.IsTrue(HasNode(index, p17.requiredEdges[i].from), "p17 edge from-node must exist: " + p17.requiredEdges[i].from);
                Assert.IsTrue(HasNode(index, p17.requiredEdges[i].to), "p17 edge to-node must exist: " + p17.requiredEdges[i].to);
            }

            var p19 = index.Puzzle("p19");
            Assert.IsNotNull(p19.requiredEvidenceIds, "p19 must declare required evidence");
            for (int i = 0; i < p19.requiredEvidenceIds.Length; i++)
                Assert.IsFalse(p19.requiredEvidenceIds[i] == "ev34" || p19.requiredEvidenceIds[i] == "ev35",
                    "p19 must not require optional family evidence " + p19.requiredEvidenceIds[i]);
            Assert.IsNotNull(p19.sufficientEvidenceSets, "p19 must declare sufficient sets");
            Assert.AreEqual(1, p19.sufficientEvidenceSets.Length, "p19 must have exactly one sufficient set");
            Assert.IsNotNull(p19.sufficientEvidenceSets[0], "p19 sufficient sets must not contain null entries");
            Assert.IsNotNull(p19.sufficientEvidenceSets[0].requiredIds,
                "p19 sufficient sets must be serialized as {\"requiredIds\":[...]} so Unity JsonUtility populates them");
            CollectionAssert.AreEquivalent(new[] { "ev36", "ev07", "ev02" }, p19.sufficientEvidenceSets[0].requiredIds,
                "p19 success must rely on ev36/ev07/ev02 only");
        }

        [Test]
        public void ST11_P18NeedsTheKpiEvidenceSetNotJustEv21()
        {
            var index = TestContent.Load();
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            ReachInteraction(dispatcher, state, "s620", "p18");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectStatement, "st620_03"));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev21" }));
            var kpiOnly = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Rejected, kpiOnly.status,
                "ev21 alone must not solve p18, got " + kpiOnly.status + " " + kpiOnly.reasonCode);
            Assert.IsFalse(state.IsPuzzleSolved("p18"), "p18 must remain unsolved with ev21 only");

            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.SelectEvidence, ids: new[] { "ev29", "ev30" }));
            var solved = TestContent.Send(dispatcher, CommandKinds.PresentEvidence);
            Assert.AreEqual(ResultStatus.Solved, solved.status, "ev29+ev30 must solve p18, got " + solved.status);
        }

        [Test]
        public void ST12_ReconstructionKinematicsMatchFrozenValues()
        {
            var index = TestContent.Load();
            var k = index.Graph.kinematics;
            Assert.IsNotNull(k, "graph kinematics must exist");

            Assert.AreEqual(20.28915111, k.historicalDistanceMeters, 1e-4, "historicalDistanceMeters");
            Assert.AreEqual(27.5168, k.historicalImpactKmh, 1e-4, "historicalImpactKmh");
            Assert.AreEqual(14.79021306, k.fullBrakeStopMeters, 1e-4, "fullBrakeStopMeters");
            Assert.AreEqual(1360, k.requestToStrongMs, "requestToStrongMs");

            Assert.AreEqual("20.29", Round2(k.historicalDistanceMeters), "displayed historical distance");
            Assert.AreEqual("27.52", Round2(k.historicalImpactKmh), "displayed impact speed");
            Assert.AreEqual("14.79", Round2(k.fullBrakeStopMeters), "displayed full brake stop");
        }

        [Test]
        public void ST13_HistoricalDistanceIsFrameIndependent()
        {
            var index = TestContent.Load();
            var first = index.Graph.kinematics.historicalDistanceMeters;
            var second = TestContent.Load().Graph.kinematics.historicalDistanceMeters;
            Assert.AreEqual(first, second, 0.0, "kinematics must be static data with no randomness");

            var k = index.Graph.kinematics;
            double analytic = HistoricalDistance(k);
            Assert.AreEqual(20.28915111, analytic, 1e-4, "closed form must reproduce the stored distance");

            double coarse = IntegratedDistance(k, 0.001);
            double fine = IntegratedDistance(k, 0.0005);
            Assert.AreEqual(coarse, fine, 1e-6, "step count must not change the result");
            Assert.AreEqual(first, coarse, 1e-3, "integrated distance must match stored distance");
        }

        [Test]
        public void ST14_S530DeclineKeepsEv35UnlikeAccept()
        {
            var index = TestContent.Load();

            var declineDispatcher = TestContent.FreshDispatcher(index, out var declineState);
            ReachBranchChoice(declineDispatcher, declineState, "s530", "s500");
            ExpectAccepted(TestContent.Send(declineDispatcher, CommandKinds.ChooseBranch, "b_s530_decline"));
            Assert.AreEqual("true", declineState.GetBranchValue("opt_family_visit"), "decline must record the family visit");
            Assert.AreEqual("false", declineState.GetBranchValue("opt_family_material"), "decline must record material refusal");
            Assert.IsFalse(declineState.Owns("ev35"), "decline must not grant ev35");

            var acceptDispatcher = TestContent.FreshDispatcher(index, out var acceptState);
            ReachBranchChoice(acceptDispatcher, acceptState, "s530", "s500");
            ExpectAccepted(TestContent.Send(acceptDispatcher, CommandKinds.ChooseBranch, "b_s530_accept"));
            Assert.AreEqual("true", acceptState.GetBranchValue("opt_family_visit"), "accept must record the family visit");
            Assert.IsTrue(acceptState.Owns("ev35"), "accept must grant ev35");
        }

        [Test]
        public void ST15_BothEndingsKeepIdenticalReportFacts()
        {
            var index = TestContent.Load();

            var a = RunEnding(index, "b_s700_facts");
            var b = RunEnding(index, "b_s700_prevention");

            Assert.AreEqual("s710", a.scene, "facts ending must reach s710");
            Assert.AreEqual("s710", b.scene, "prevention ending must reach s710");
            Assert.IsTrue(a.state.IsPuzzleSolved("p20"), "p20 must be solved in run A");
            Assert.IsTrue(b.state.IsPuzzleSolved("p20"), "p20 must be solved in run B");

            AssertSameSet(a.state.OwnedEvidence, b.state.OwnedEvidence, "owned evidence must be identical for both endings");
            AssertSameSet(a.state.SolvedPuzzles, b.state.SolvedPuzzles, "solved puzzles must be identical for both endings");
            Assert.AreEqual(EndingDraftReport(a.state), EndingDraftReport(b.state), "p20 report facts must be identical");

            Assert.AreEqual("facts_first", a.state.GetBranchValue("ending_tone"), "run A ending tone");
            Assert.AreEqual("prevention_first", b.state.GetBranchValue("ending_tone"), "run B ending tone");

            var keysA = new List<string>();
            var keysB = new List<string>();
            for (int i = 0; i < a.state.BranchValues.Count; i++) keysA.Add(a.state.BranchValues[i].key);
            for (int i = 0; i < b.state.BranchValues.Count; i++) keysB.Add(b.state.BranchValues[i].key);
            keysA.Sort(StringComparer.Ordinal);
            keysB.Sort(StringComparer.Ordinal);
            CollectionAssert.AreEqual(keysA, keysB, "both endings must set the same branch value keys");
            for (int i = 0; i < keysA.Count; i++)
            {
                var key = keysA[i];
                if (key == "ending_tone") continue;
                Assert.AreEqual(a.state.GetBranchValue(key), b.state.GetBranchValue(key),
                    "branch value " + key + " must not differ between endings");
            }
        }

        [Test]
        public void ST16_HubChildOrderDoesNotChangeResults()
        {
            var index = TestContent.Load();

            var reference = RunHubPermutation(index, "s100", "s140", new[] { "s110", "s120", "s130" });
            var permutations = Permutations(new[] { "s110", "s120", "s130" });
            for (int i = 1; i < permutations.Count; i++)
            {
                var result = RunHubPermutation(index, "s100", "s140", permutations[i].ToArray());
                Assert.AreEqual(reference.scene, result.scene, "permutation must reach the same scene");
                AssertSameSet(reference.owned, result.owned, "s100 permutation " + string.Join(",", permutations[i].ToArray()) + " owned evidence");
                AssertSameSet(reference.solved, result.solved, "s100 permutation " + string.Join(",", permutations[i].ToArray()) + " solved puzzles");
            }

            var reference200 = RunHubPermutation(index, "s200", "s240", new[] { "s210", "s220", "s230" });
            var permutations200 = Permutations(new[] { "s210", "s220", "s230" });
            for (int i = 1; i < permutations200.Count; i++)
            {
                var result = RunHubPermutation(index, "s200", "s240", permutations200[i].ToArray());
                Assert.AreEqual(reference200.scene, result.scene, "permutation must reach the same scene");
                AssertSameSet(reference200.owned, result.owned, "s200 permutation " + string.Join(",", permutations200[i].ToArray()) + " owned evidence");
                AssertSameSet(reference200.solved, result.solved, "s200 permutation " + string.Join(",", permutations200[i].ToArray()) + " solved puzzles");
            }
        }

        [Test]
        public void ST17_S411UnlocksOnlyAfterS410Completes()
        {
            var index = TestContent.Load();
            var s411 = index.Scene("s411");
            Assert.IsNotNull(s411, "s411 must exist");
            Assert.IsFalse(ConditionEvaluator.AllConditionsMet(s411.entryConditions, TestContent.FreshState(), index),
                "s411 entry conditions must not be met by an empty state");

            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s400");
            TestContent.EnsureHubActive(dispatcher, state, "s400");

            Assert.IsFalse(state.IsSceneCompleted("s410"), "s410 must not be completed before p11 is solved");
            Assert.IsFalse(ConditionEvaluator.AllConditionsMet(s411.entryConditions, state, index),
                "s411 entry conditions must fail while s410 is incomplete");
            Assert.IsFalse(dispatcher.TransitionAvailable(out _, out _), "s400 transition must be unavailable until all children are complete");
            var blocked = TestContent.Send(dispatcher, CommandKinds.ContinueScene);
            Assert.AreEqual(ResultStatus.Rejected, blocked.status, "ContinueScene must be rejected while children remain");
            Assert.IsFalse(state.IsSceneVisited("s411"), "s411 must not be reachable before p11 is solved");

            TestContent.SolvePuzzle(dispatcher, state, "p11");
            TestContent.FinishScene(dispatcher, state);
            Assert.AreEqual("s410", state.CurrentSceneId, "p11 completes inside s410");
            Assert.IsTrue(state.IsSceneCompleted("s410"), "s410 must be completed after p11 is solved");
            Assert.IsTrue(ConditionEvaluator.AllConditionsMet(s411.entryConditions, state, index),
                "s411 entry conditions must be met after s410 completes");

            string unlockTarget;
            Assert.IsTrue(dispatcher.TransitionAvailable(out unlockTarget, out _),
                "s410 must have an enabled transition after p11");
            Assert.AreEqual("s411", unlockTarget, "s410 must advance to s411");
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ContinueScene));
            Assert.AreEqual("s411", state.CurrentSceneId, "s411 must be reachable after s410 completes");
        }

        [Test]
        public void ST18_S510S540OrderFlipAndOptionalVariationReachS550()
        {
            var index = TestContent.Load();

            var baseline = RunCase550(index, new[] { "s510", "s540" });
            var flipped = RunCase550(index, new[] { "s540", "s510" });
            var optional = RunCase550(index, new[] { "s520", "s530", "s540", "s510" });

            Assert.AreEqual("s550", baseline.scene, "baseline order must reach s550");
            Assert.AreEqual("s550", flipped.scene, "flipped order must reach s550");
            Assert.AreEqual("s550", optional.scene, "optional variation must reach s550");
            AssertSameSet(baseline.solved, flipped.solved, "s510/s540 order must not change solved puzzles");
            AssertSameSet(baseline.solved, optional.solved, "optional scenes must not change solved puzzles");
            Assert.IsTrue(baseline.solved.Contains("p16"), "p16 must be solved");
        }

        private static void ReachInteraction(CommandDispatcher dispatcher, GameState state, string sceneId, string puzzleId)
        {
            TestContent.SolveSceneTo(dispatcher, state, sceneId);
            TestContent.EnsureInteraction(dispatcher, state, puzzleId);
        }

        private static void ReachBranchChoice(CommandDispatcher dispatcher, GameState state, string optionalSceneId, string hubSceneId)
        {
            TestContent.SolveSceneTo(dispatcher, state, hubSceneId);
            TestContent.EnsureHubActive(dispatcher, state, hubSceneId);
            dispatcher.EnterScene(optionalSceneId);
            TestContent.AdvanceUntilChoiceOrInteraction(dispatcher, state);
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

        private static string Round2(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static double HistoricalDistance(KinematicsDto k)
        {
            double v = k.speedKmh / 3.6;
            double distance = v * k.actuatorDelaySeconds;
            distance += v * k.weakDurationSeconds - 0.5 * k.weakDeceleration * k.weakDurationSeconds * k.weakDurationSeconds;
            v -= k.weakDeceleration * k.weakDurationSeconds;
            distance += v * k.strongDurationSeconds - 0.5 * k.strongDeceleration * k.strongDurationSeconds * k.strongDurationSeconds;
            return distance;
        }

        private static double IntegratedDistance(KinematicsDto k, double dt)
        {
            double v = k.speedKmh / 3.6;
            double distance = 0.0;
            int delaySteps = (int)Math.Round(k.actuatorDelaySeconds / dt);
            for (int i = 0; i < delaySteps; i++) distance += v * dt;
            int weakSteps = (int)Math.Round(k.weakDurationSeconds / dt);
            for (int i = 0; i < weakSteps; i++)
            {
                distance += v * dt - 0.5 * k.weakDeceleration * dt * dt;
                v -= k.weakDeceleration * dt;
            }
            int strongSteps = (int)Math.Round(k.strongDurationSeconds / dt);
            for (int i = 0; i < strongSteps; i++)
            {
                distance += v * dt - 0.5 * k.strongDeceleration * dt * dt;
                v -= k.strongDeceleration * dt;
            }
            return distance;
        }

        private sealed class RunResult
        {
            public CommandDispatcher dispatcher;
            public GameState state;
            public string scene = "";
            public List<string> owned = new List<string>();
            public List<string> solved = new List<string>();
        }

        private static RunResult Capture(CommandDispatcher dispatcher, GameState state, string scene)
        {
            return new RunResult
            {
                dispatcher = dispatcher,
                state = state,
                scene = scene,
                owned = new List<string>(state.OwnedEvidence),
                solved = new List<string>(state.SolvedPuzzles)
            };
        }

        private static RunResult RunEnding(ContentIndex index, string branchId)
        {
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s700");
            TestContent.AdvanceUntilChoiceOrInteraction(dispatcher, state);
            Assert.AreEqual(Phases.BranchChoice, state.Phase, "s700 must offer a branch choice");
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ChooseBranch, branchId));
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ContinueScene));
            return Capture(dispatcher, state, state.CurrentSceneId);
        }

        private static string EndingDraftReport(GameState state)
        {
            var draft = state.GetDraft("p20", false);
            if (draft == null) return "<no p20 draft>";
            var placements = new List<string>();
            if (draft.reportPlacements != null)
                for (int i = 0; i < draft.reportPlacements.Length; i++)
                    placements.Add(draft.reportPlacements[i].section + ":" + draft.reportPlacements[i].item);
            var evidence = new List<string>();
            if (draft.reportEvidence != null)
                for (int i = 0; i < draft.reportEvidence.Length; i++)
                {
                    var ids = new List<string>(draft.reportEvidence[i].evidenceIds ?? new string[0]);
                    ids.Sort(StringComparer.Ordinal);
                    evidence.Add(draft.reportEvidence[i].item + ":" + string.Join("+", ids.ToArray()));
                }
            placements.Sort(StringComparer.Ordinal);
            evidence.Sort(StringComparer.Ordinal);
            return string.Join("|", placements.ToArray()) + "||" + string.Join("|", evidence.ToArray());
        }

        private static RunResult RunHubPermutation(ContentIndex index, string hubId, string targetScene, string[] children)
        {
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, hubId);
            TestContent.EnsureHubActive(dispatcher, state, hubId);
            for (int i = 0; i < children.Length; i++)
            {
                var child = index.Scene(children[i]);
                Assert.IsNotNull(child, "hub child must exist: " + children[i]);
                Assert.IsTrue(ConditionEvaluator.AllConditionsMet(child.entryConditions, state, index),
                    "child " + children[i] + " entry conditions must be met when visited in permutation order");
                dispatcher.EnterScene(children[i]);
                TestContent.SolveSceneTo(dispatcher, state, hubId);
                TestContent.EnsureHubActive(dispatcher, state, hubId);
            }
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ContinueScene));
            return Capture(dispatcher, state, state.CurrentSceneId);
        }

        private static RunResult RunCase550(ContentIndex index, string[] order)
        {
            var dispatcher = TestContent.FreshDispatcher(index, out var state);
            TestContent.SolveSceneTo(dispatcher, state, "s500");
            TestContent.EnsureHubActive(dispatcher, state, "s500");
            for (int i = 0; i < order.Length; i++)
            {
                dispatcher.EnterScene(order[i]);
                TestContent.SolveSceneTo(dispatcher, state, "s500");
                TestContent.EnsureHubActive(dispatcher, state, "s500");
            }
            ExpectAccepted(TestContent.Send(dispatcher, CommandKinds.ContinueScene));
            return Capture(dispatcher, state, state.CurrentSceneId);
        }

        private static List<List<string>> Permutations(string[] items)
        {
            var results = new List<List<string>>();
            Permute(items, 0, results);
            results.Sort((a, b) => string.CompareOrdinal(string.Join(",", a.ToArray()), string.Join(",", b.ToArray())));
            return results;
        }

        private static void Permute(string[] items, int index, List<List<string>> results)
        {
            if (index == items.Length)
            {
                results.Add(new List<string>(items));
                return;
            }
            for (int i = index; i < items.Length; i++)
            {
                var tmp = items[index];
                items[index] = items[i];
                items[i] = tmp;
                Permute(items, index + 1, results);
                tmp = items[index];
                items[index] = items[i];
                items[i] = tmp;
            }
        }

        private static bool HasNode(IContentIndex index, string nodeId)
        {
            var nodes = index.GraphNodes();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == nodeId) return true;
            return false;
        }
    }
}
