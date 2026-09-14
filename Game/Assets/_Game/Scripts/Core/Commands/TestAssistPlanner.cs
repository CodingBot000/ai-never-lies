using System.Collections.Generic;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Puzzles;
using ANL.Core.State;

namespace ANL.Core.Commands
{
    public sealed class AssistPlan
    {
        public bool CanRun;
        public string Blocker = "";
        public readonly List<GameCommand> Commands = new List<GameCommand>();
    }

    public static class TestAssistPlanner
    {
        public static AssistPlan Plan(IContentIndex index, GameState state)
        {
            var plan = new AssistPlan();
            if (state.Phase != Phases.Interaction || string.IsNullOrEmpty(state.ActivePuzzleId))
            {
                plan.Blocker = "no_active_puzzle";
                return plan;
            }
            var puzzle = index.Puzzle(state.ActivePuzzleId);
            if (puzzle == null)
            {
                plan.Blocker = "unknown_puzzle";
                return plan;
            }
            if (!PuzzleEvaluator.HasRequiredEvidence(puzzle, state))
            {
                plan.Blocker = "missing_required_evidence";
                return plan;
            }

            long seq = state.LastCommandSequence;
            int n = 0;
            System.Func<string, GameCommand> make = type =>
            {
                n++;
                return new GameCommand
                {
                    commandId = "assist_" + puzzle.id + "_" + n,
                    sequence = ++seq,
                    sceneId = state.CurrentSceneId,
                    origin = Origins.TestAssist,
                    type = type
                };
            };

            switch (puzzle.type)
            {
                case PuzzleTypes.HotspotSet:
                    if (puzzle.regions != null)
                        foreach (var region in puzzle.regions)
                        {
                            var cmd = make(CommandKinds.InspectRegion);
                            cmd.targetId = region.regionId;
                            plan.Commands.Add(cmd);
                        }
                    break;
                case PuzzleTypes.StatementRefutation:
                {
                    var s = make(CommandKinds.SelectStatement);
                    s.targetId = puzzle.targetStatementId;
                    plan.Commands.Add(s);
                    var e = make(CommandKinds.SelectEvidence);
                    e.ids = FirstSet(puzzle.sufficientEvidenceSets);
                    if (e.ids == null) { plan.Blocker = "missing_solution"; return plan; }
                    plan.Commands.Add(e);
                    if (puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0)
                    {
                        var ch = make(CommandKinds.SelectChoice);
                        ch.targetId = puzzle.correctChoiceIds[0];
                        plan.Commands.Add(ch);
                    }
                    plan.Commands.Add(make(CommandKinds.PresentEvidence));
                    break;
                }
                case PuzzleTypes.SourceMapping: return MappingPlan(plan, puzzle, make);
                case PuzzleTypes.TraceMapping: return MappingPlan(plan, puzzle, make);
                case PuzzleTypes.TimelineOrder:
                {
                    if (puzzle.orderedIds == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var id in puzzle.orderedIds)
                    {
                        var p = make(CommandKinds.PlaceCard);
                        p.slot = "order";
                        p.value = id;
                        plan.Commands.Add(p);
                    }
                    if (puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0)
                    {
                        var ch = make(CommandKinds.SelectChoice);
                        ch.targetId = puzzle.correctChoiceIds[0];
                        plan.Commands.Add(ch);
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.CrossCamera:
                {
                    var w = make(CommandKinds.SelectComparisonWindow);
                    w.targetId = puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0 ? puzzle.correctChoiceIds[0] : "";
                    plan.Commands.Add(w);
                    var e = make(CommandKinds.SelectEvidence);
                    e.ids = FirstSet(puzzle.sufficientEvidenceSets);
                    if (e.ids == null) { plan.Blocker = "missing_solution"; return plan; }
                    plan.Commands.Add(e);
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.Chronology:
                {
                    if (puzzle.orderedIds == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var id in puzzle.orderedIds)
                    {
                        var p = make(CommandKinds.PlaceCard);
                        p.slot = "order";
                        p.value = id;
                        plan.Commands.Add(p);
                    }
                    if (puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0)
                    {
                        var ch = make(CommandKinds.SelectChoice);
                        ch.targetId = puzzle.correctChoiceIds[0];
                        plan.Commands.Add(ch);
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.ArgumentChoice:
                {
                    var ch = make(CommandKinds.SelectChoice);
                    ch.targetId = puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0 ? puzzle.correctChoiceIds[0] : "";
                    plan.Commands.Add(ch);
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.DiffSelect:
                {
                    if (puzzle.correctChoiceIds == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var id in puzzle.correctChoiceIds)
                    {
                        var ch = make(CommandKinds.SelectChoice);
                        ch.targetId = id;
                        plan.Commands.Add(ch);
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.AssumptionLink:
                {
                    var ch = make(CommandKinds.SelectChoice);
                    ch.targetId = puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0 ? puzzle.correctChoiceIds[0] : "";
                    plan.Commands.Add(ch);
                    var e = make(CommandKinds.SelectEvidence);
                    e.ids = FirstSet(puzzle.sufficientEvidenceSets);
                    if (e.ids == null) { plan.Blocker = "missing_solution"; return plan; }
                    plan.Commands.Add(e);
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.MatrixGap:
                {
                    string[] dims = { "sensor", "overrideState", "version", "condition" };
                    if (puzzle.orderedIds == null || puzzle.orderedIds.Length != dims.Length) { plan.Blocker = "missing_solution"; return plan; }
                    for (int i = 0; i < dims.Length; i++)
                    {
                        var p = make(CommandKinds.PlaceCard);
                        p.slot = dims[i];
                        p.value = puzzle.orderedIds[i];
                        plan.Commands.Add(p);
                    }
                    if (puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0)
                    {
                        var ch = make(CommandKinds.SelectChoice);
                        ch.targetId = puzzle.correctChoiceIds[0];
                        plan.Commands.Add(ch);
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.DeploymentChain:
                {
                    if (puzzle.orderedIds == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var id in puzzle.orderedIds)
                    {
                        var p = make(CommandKinds.PlaceCard);
                        p.slot = "order";
                        p.value = id;
                        plan.Commands.Add(p);
                    }
                    if (puzzle.correctChoiceIds != null && puzzle.correctChoiceIds.Length > 0)
                    {
                        var ch = make(CommandKinds.SelectChoice);
                        ch.targetId = puzzle.correctChoiceIds[0];
                        plan.Commands.Add(ch);
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.Counterfactual:
                {
                    if (puzzle.requiredCaseIds == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var id in puzzle.requiredCaseIds)
                    {
                        var c = make(CommandKinds.RunCounterfactualCase);
                        c.targetId = id;
                        plan.Commands.Add(c);
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.CausalGraph:
                {
                    if (puzzle.requiredEdges == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var edge in puzzle.requiredEdges)
                    {
                        var c = make(CommandKinds.ConnectNodes);
                        c.ids = new[] { edge.from, edge.to };
                        c.targetId = edge.kind;
                        plan.Commands.Add(c);
                    }
                    if (puzzle.edgeProofRequirements != null)
                    {
                        foreach (var proof in puzzle.edgeProofRequirements)
                        {
                            var s = make(CommandKinds.SelectEvidence);
                            s.targetId = proof.from + "|" + proof.to + "|" + proof.kind;
                            s.ids = FirstSet(proof.sufficientEvidenceSets);
                            if (s.ids == null) { plan.Blocker = "missing_solution"; return plan; }
                            plan.Commands.Add(s);
                        }
                    }
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                case PuzzleTypes.ResponsibilityMatrix:
                {
                    if (puzzle.reportProofRequirements == null) { plan.Blocker = "missing_solution"; return plan; }
                    foreach (var proof in puzzle.reportProofRequirements)
                    {
                        var p = make(CommandKinds.PlaceCard);
                        p.slot = proof.section;
                        p.value = proof.item;
                        plan.Commands.Add(p);
                        var s = make(CommandKinds.SelectEvidence);
                        s.targetId = proof.item;
                        s.ids = FirstSet(proof.sufficientEvidenceSets);
                        if (s.ids == null) { plan.Blocker = "missing_solution"; return plan; }
                        plan.Commands.Add(s);
                    }
                    var limit = make(CommandKinds.PlaceCard);
                    limit.slot = "limit";
                    limit.value = "no_intent_or_criminal_verdict";
                    plan.Commands.Add(limit);
                    plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
                    break;
                }
                default:
                    plan.Blocker = "unsupported_puzzle_type";
                    return plan;
            }

            plan.CanRun = plan.Commands.Count > 0;
            if (!plan.CanRun && string.IsNullOrEmpty(plan.Blocker)) plan.Blocker = "empty_plan";
            return plan;
        }

        private static AssistPlan MappingPlan(AssistPlan plan, PuzzleDefinitionDto puzzle, System.Func<string, GameCommand> make)
        {
            if (puzzle.valuePairs == null) { plan.Blocker = "missing_solution"; return plan; }
            foreach (var pair in puzzle.valuePairs)
            {
                var p = make(CommandKinds.PlaceCard);
                p.slot = pair.key;
                p.value = pair.value;
                plan.Commands.Add(p);
            }
            plan.Commands.Add(make(CommandKinds.SubmitPuzzle));
            plan.CanRun = plan.Commands.Count > 0;
            return plan;
        }

        private static string[] FirstSet(EvidenceSetDto[] sets)
        {
            if (sets == null) return null;
            for (int i = 0; i < sets.Length; i++)
                if (sets[i]?.requiredIds != null && sets[i].requiredIds.Length > 0)
                    return sets[i].requiredIds;
            return null;
        }
    }
}
