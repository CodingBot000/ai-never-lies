using System;
using System.Collections.Generic;
using ANL.Core.Contracts;
using ANL.Core.Content;
using ANL.Core.State;

namespace ANL.Core.Puzzles
{
    public static class PuzzleTypes
    {
        public const string HotspotSet = "hotspot_set";
        public const string StatementRefutation = "statement_refutation";
        public const string SourceMapping = "source_mapping";
        public const string TimelineOrder = "timeline_order";
        public const string TraceMapping = "trace_mapping";
        public const string CrossCamera = "cross_camera";
        public const string Chronology = "chronology";
        public const string ArgumentChoice = "argument_choice";
        public const string DiffSelect = "diff_select";
        public const string AssumptionLink = "assumption_link";
        public const string MatrixGap = "matrix_gap";
        public const string DeploymentChain = "deployment_chain";
        public const string Counterfactual = "counterfactual";
        public const string CausalGraph = "causal_graph";
        public const string ResponsibilityMatrix = "responsibility_matrix";
    }

    public sealed class PuzzleEvalResult
    {
        public string Status;
        public string Message = "";
        public int MatchedSetIndex = -1;
        public bool Solved => Status == ResultStatus.Solved;
    }

    public static class PuzzleEvaluator
    {
        private static readonly HashSet<string> KnownTypes = new HashSet<string>
        {
            PuzzleTypes.HotspotSet, PuzzleTypes.StatementRefutation, PuzzleTypes.SourceMapping,
            PuzzleTypes.TimelineOrder, PuzzleTypes.TraceMapping, PuzzleTypes.CrossCamera,
            PuzzleTypes.Chronology, PuzzleTypes.ArgumentChoice, PuzzleTypes.DiffSelect,
            PuzzleTypes.AssumptionLink, PuzzleTypes.MatrixGap, PuzzleTypes.DeploymentChain,
            PuzzleTypes.Counterfactual, PuzzleTypes.CausalGraph, PuzzleTypes.ResponsibilityMatrix
        };

        public static bool IsKnownType(string type) => KnownTypes.Contains(type);

        public static PuzzleEvalResult Evaluate(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameState state, IContentIndex index)
        {
            if (puzzle == null || draft == null)
                return new PuzzleEvalResult { Status = ResultStatus.Rejected, Message = "" };
            if (!IsKnownType(puzzle.type))
                return new PuzzleEvalResult { Status = ResultStatus.Rejected, Message = "알 수 없는 퍼즐 형식입니다." };
            if (!HasRequiredEvidence(puzzle, state))
                return new PuzzleEvalResult { Status = ResultStatus.Rejected, Message = "필요한 자료를 아직 보유하지 않았습니다." };

            switch (puzzle.type)
            {
                case PuzzleTypes.HotspotSet: return EvalHotspot(puzzle, draft, index);
                case PuzzleTypes.StatementRefutation: return EvalStatement(puzzle, draft, index);
                case PuzzleTypes.SourceMapping: return EvalMapping(puzzle, draft);
                case PuzzleTypes.TimelineOrder: return EvalTimeline(puzzle, draft);
                case PuzzleTypes.TraceMapping: return EvalMapping(puzzle, draft);
                case PuzzleTypes.CrossCamera: return EvalCrossCamera(puzzle, draft);
                case PuzzleTypes.Chronology: return EvalChronology(puzzle, draft);
                case PuzzleTypes.ArgumentChoice: return EvalArgumentChoice(puzzle, draft);
                case PuzzleTypes.DiffSelect: return EvalDiffSelect(puzzle, draft);
                case PuzzleTypes.AssumptionLink: return EvalAssumptionLink(puzzle, draft, state);
                case PuzzleTypes.MatrixGap: return EvalMatrixGap(puzzle, draft);
                case PuzzleTypes.DeploymentChain: return EvalDeploymentChain(puzzle, draft);
                case PuzzleTypes.Counterfactual: return EvalCounterfactual(puzzle, draft);
                case PuzzleTypes.CausalGraph: return EvalCausalGraph(puzzle, draft, state);
                case PuzzleTypes.ResponsibilityMatrix: return EvalResponsibility(puzzle, draft, state, index);
                default: return new PuzzleEvalResult { Status = ResultStatus.Rejected, Message = "" };
            }
        }

        public static bool HasRequiredEvidence(PuzzleDefinitionDto puzzle, GameState state)
        {
            if (puzzle.requiredEvidenceIds == null) return true;
            for (int i = 0; i < puzzle.requiredEvidenceIds.Length; i++)
                if (!state.Owns(puzzle.requiredEvidenceIds[i])) return false;
            return true;
        }

        private static PuzzleEvalResult Solved(PuzzleDefinitionDto p, int setIndex = -1)
        {
            return new PuzzleEvalResult { Status = ResultStatus.Solved, Message = p.successText ?? "", MatchedSetIndex = setIndex };
        }

        private static PuzzleEvalResult Partial(string message)
        {
            return new PuzzleEvalResult { Status = ResultStatus.Partial, Message = message };
        }

        private static PuzzleEvalResult Wrong(PuzzleDefinitionDto p, string message = null)
        {
            return new PuzzleEvalResult { Status = ResultStatus.Rejected, Message = string.IsNullOrEmpty(message) ? (p.wrongText ?? "") : message };
        }

        private static bool SameOrder(string[] expected, List<PlacedCardDto> placed, string slot)
        {
            if (expected == null || expected.Length == 0) return false;
            var actual = new List<string>();
            for (int i = 0; i < placed.Count; i++)
                if (placed[i].slot == slot) actual.Add(placed[i].value);
            if (actual.Count != expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i]) return false;
            return true;
        }

        private static string GetValue(StringPairDto[] values, string key)
        {
            if (values == null) return "";
            for (int i = 0; i < values.Length; i++)
                if (values[i].key == key) return values[i].value;
            return "";
        }

        private static string GetDraftValue(PuzzleDraftDto draft, string key)
        {
            if (draft.placedCards != null)
                for (int i = 0; i < draft.placedCards.Length; i++)
                    if (draft.placedCards[i].slot == key) return draft.placedCards[i].value;
            if (draft.values != null)
                for (int i = 0; i < draft.values.Length; i++)
                    if (draft.values[i].key == key) return draft.values[i].value;
            return "";
        }

        private static bool HasChoice(PuzzleDraftDto draft, string choiceId)
        {
            if (draft.choiceIds == null) return false;
            for (int i = 0; i < draft.choiceIds.Length; i++)
                if (draft.choiceIds[i] == choiceId) return true;
            return false;
        }

        private static int MatchEvidenceSet(EvidenceSetDto[] sets, string[] selected)
        {
            if (sets == null || selected == null) return -1;
            for (int s = 0; s < sets.Length; s++)
            {
                var set = sets[s];
                if (set?.requiredIds == null || set.requiredIds.Length == 0) continue;
                bool containsAll = true;
                for (int i = 0; i < set.requiredIds.Length; i++)
                {
                    bool found = false;
                    for (int j = 0; j < selected.Length; j++)
                        if (selected[j] == set.requiredIds[i]) { found = true; break; }
                    if (!found) { containsAll = false; break; }
                }
                if (containsAll) return s;
            }
            return -1;
        }

        private static bool AnyRequiredEvidenceSelected(EvidenceSetDto[] sets, string[] selected)
        {
            if (sets == null || selected == null) return false;
            for (int s = 0; s < sets.Length; s++)
            {
                var set = sets[s];
                if (set?.requiredIds == null) continue;
                for (int i = 0; i < set.requiredIds.Length; i++)
                    for (int j = 0; j < selected.Length; j++)
                        if (selected[j] == set.requiredIds[i]) return true;
            }
            return false;
        }

        private static PuzzleEvalResult EvalHotspot(PuzzleDefinitionDto p, PuzzleDraftDto d, IContentIndex index)
        {
            var visited = new HashSet<string>();
            if (d.placedCards != null)
                for (int i = 0; i < d.placedCards.Length; i++)
                    if (d.placedCards[i].slot == "region") visited.Add(d.placedCards[i].value);
            if (p.regions == null || p.regions.Length == 0) return Wrong(p);
            for (int i = 0; i < p.regions.Length; i++)
                if (!visited.Contains(p.regions[i].regionId))
                    return Partial("조사하지 않은 지점이 남아 있습니다.");
            return Solved(p);
        }

        private static PuzzleEvalResult EvalStatement(PuzzleDefinitionDto p, PuzzleDraftDto d, IContentIndex index)
        {
            if (d.selectedStatementId != p.targetStatementId)
            {
                string msg = index != null ? index.Ui("ui.statement_wrong_target") : "다른 주장입니다.";
                return Wrong(p, msg);
            }
            if (p.correctChoiceIds != null && p.correctChoiceIds.Length > 0)
            {
                bool anyCorrect = false;
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) { anyCorrect = true; break; }
                if (!anyCorrect)
                    return Partial(string.IsNullOrEmpty(p.partialText) ? "결론 선택이 필요합니다." : p.partialText);
            }
            int match = MatchEvidenceSet(p.sufficientEvidenceSets, d.selectedEvidenceIds);
            if (match >= 0) return Solved(p, match);
            if (AnyRequiredEvidenceSelected(p.sufficientEvidenceSets, d.selectedEvidenceIds))
                return Partial(string.IsNullOrEmpty(p.partialText) ? "제출한 근거가 충분하지 않습니다." : p.partialText);
            string wrong = string.IsNullOrEmpty(p.wrongText)
                ? (index != null ? index.Ui("ui.statement_unrelated") : "관련 없는 자료입니다.")
                : p.wrongText;
            return Wrong(p, wrong);
        }

        private static PuzzleEvalResult EvalMapping(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            if (p.valuePairs == null || p.valuePairs.Length == 0) return Wrong(p);
            int correct = 0;
            for (int i = 0; i < p.valuePairs.Length; i++)
            {
                var expected = GetValue(p.valuePairs, p.valuePairs[i].key);
                var actual = GetDraftValue(d, p.valuePairs[i].key);
                if (!string.IsNullOrEmpty(expected) && expected == actual) correct++;
            }
            if (correct == p.valuePairs.Length) return Solved(p);
            if (correct > 0) return Partial("일부 항목이 맞습니다. 나머지도 확인하십시오.");
            return Wrong(p);
        }

        private static PuzzleEvalResult EvalTimeline(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            bool orderOk = SameOrder(p.orderedIds, d.placedCards != null ? new List<PlacedCardDto>(d.placedCards) : new List<PlacedCardDto>(), "order");
            if (!orderOk) return Wrong(p);
            if (p.correctChoiceIds != null && p.correctChoiceIds.Length > 0)
            {
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) return Solved(p);
                return Partial("사건 배치는 맞습니다. 해석을 선택하십시오.");
            }
            return Solved(p);
        }

        private static PuzzleEvalResult EvalChronology(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            bool orderOk = SameOrder(p.orderedIds, d.placedCards != null ? new List<PlacedCardDto>(d.placedCards) : new List<PlacedCardDto>(), "order");
            if (!orderOk) return Wrong(p);
            if (p.correctChoiceIds != null)
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) return Solved(p);
            return Partial("시간 순서는 맞습니다. 최종 상태를 선택하십시오.");
        }

        private static PuzzleEvalResult EvalCrossCamera(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            string expectedWindow = p.correctChoiceIds != null && p.correctChoiceIds.Length > 0 ? p.correctChoiceIds[0] : "";
            if (d.comparisonWindowId != expectedWindow)
                return Wrong(p, "같은 시간 구간의 자료를 비교하십시오.");
            int match = MatchEvidenceSet(p.sufficientEvidenceSets, d.selectedEvidenceIds);
            if (match >= 0) return Solved(p, match);
            return Partial("선택한 구간에 필요한 자료를 모두 연결하지 않았습니다.");
        }

        private static PuzzleEvalResult EvalArgumentChoice(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            if (p.correctChoiceIds != null)
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) return Solved(p);
            return Wrong(p);
        }

        private static PuzzleEvalResult EvalDiffSelect(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            if (p.correctChoiceIds == null || p.correctChoiceIds.Length == 0) return Wrong(p);
            int chosen = 0;
            for (int i = 0; i < p.correctChoiceIds.Length; i++)
                if (HasChoice(d, p.correctChoiceIds[i])) chosen++;
            if (chosen == p.correctChoiceIds.Length) return Solved(p);
            if (chosen > 0) return Partial("삭제된 블록은 맞습니다. 비용 설명도 함께 선택하십시오.");
            return Wrong(p);
        }

        private static PuzzleEvalResult EvalAssumptionLink(PuzzleDefinitionDto p, PuzzleDraftDto d, GameState s)
        {
            if (p.correctChoiceIds != null)
            {
                bool any = false;
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) any = true;
                if (!any) return Wrong(p);
            }
            int match = MatchEvidenceSet(p.sufficientEvidenceSets, d.selectedEvidenceIds);
            if (match >= 0) return Solved(p, match);
            if (d.selectedEvidenceIds != null && d.selectedEvidenceIds.Length > 0)
                return Partial("전제는 맞습니다. 실제 운영에서 깨진 근거를 연결하십시오.");
            return Partial("전제를 선택하고 반증 자료를 연결하십시오.");
        }

        private static PuzzleEvalResult EvalMatrixGap(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            if (p.orderedIds == null || p.orderedIds.Length != 4) return Wrong(p);
            string[] dims = { "sensor", "overrideState", "version", "condition" };
            for (int i = 0; i < dims.Length; i++)
            {
                string value = "";
                if (d.placedCards != null)
                    for (int j = 0; j < d.placedCards.Length; j++)
                        if (d.placedCards[j].slot == dims[i]) value = d.placedCards[j].value;
                if (value != p.orderedIds[i])
                    return Partial("사고 조건에 해당하는 조합을 선택하십시오.");
            }
            if (p.correctChoiceIds != null)
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) return Solved(p);
            return Partial("조합은 맞습니다. 결론을 선택하십시오.");
        }

        private static PuzzleEvalResult EvalDeploymentChain(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            bool orderOk = SameOrder(p.orderedIds, d.placedCards != null ? new List<PlacedCardDto>(d.placedCards) : new List<PlacedCardDto>(), "order");
            if (!orderOk) return Partial("설계→설치→조건 이행을 각각 확인하십시오.");
            if (p.correctChoiceIds != null)
                for (int i = 0; i < p.correctChoiceIds.Length; i++)
                    if (HasChoice(d, p.correctChoiceIds[i])) return Solved(p);
            return Partial("연결은 맞습니다. 결론을 선택하십시오.");
        }

        private static PuzzleEvalResult EvalCounterfactual(PuzzleDefinitionDto p, PuzzleDraftDto d)
        {
            if (p.requiredCaseIds == null) return Wrong(p);
            int ran = 0;
            for (int i = 0; i < p.requiredCaseIds.Length; i++)
                if (HasRanCase(d, p.requiredCaseIds[i])) ran++;
            if (ran == p.requiredCaseIds.Length) return Solved(p);
            return Partial("세 사례를 모두 실행해야 합니다.");
        }

        private static bool HasRanCase(PuzzleDraftDto d, string caseId)
        {
            if (d.ranCaseIds == null) return false;
            for (int i = 0; i < d.ranCaseIds.Length; i++)
                if (d.ranCaseIds[i] == caseId) return true;
            return false;
        }

        private static PuzzleEvalResult EvalCausalGraph(PuzzleDefinitionDto p, PuzzleDraftDto d, GameState s)
        {
            if (p.requiredEdges == null || p.requiredEdges.Length == 0) return Wrong(p);
            int connected = 0;
            for (int i = 0; i < p.requiredEdges.Length; i++)
                if (HasEdge(d, p.requiredEdges[i].from, p.requiredEdges[i].to, p.requiredEdges[i].kind)) connected++;
            if (connected == 0) return Wrong(p);
            if (connected < p.requiredEdges.Length) return Partial("정답 연결 7개와 신호선 1개를 모두 완성하십시오.");
            if (p.edgeProofRequirements == null || p.edgeProofRequirements.Length == 0) return Solved(p);
            int proved = 0;
            for (int i = 0; i < p.edgeProofRequirements.Length; i++)
            {
                var req = p.edgeProofRequirements[i];
                var selection = FindEdgeProof(d, req.from, req.to, req.kind);
                if (selection != null && MatchEvidenceSet(req.sufficientEvidenceSets, selection.evidenceIds) >= 0) proved++;
            }
            if (proved == p.edgeProofRequirements.Length) return Solved(p);
            return Partial("간선 " + proved + "/" + p.edgeProofRequirements.Length + "에 근거가 연결되었습니다. 나머지 간선에 근거를 붙이십시오.");
        }

        private static bool HasEdge(PuzzleDraftDto d, string from, string to, string kind)
        {
            if (d.edges == null) return false;
            for (int i = 0; i < d.edges.Length; i++)
                if (d.edges[i].from == from && d.edges[i].to == to && d.edges[i].kind == kind) return true;
            return false;
        }

        private static EdgeProofSelectionDto FindEdgeProof(PuzzleDraftDto d, string from, string to, string kind)
        {
            if (d.edgeProofs == null) return null;
            for (int i = 0; i < d.edgeProofs.Length; i++)
                if (d.edgeProofs[i].from == from && d.edgeProofs[i].to == to && d.edgeProofs[i].kind == kind)
                    return d.edgeProofs[i];
            return null;
        }

        private static PuzzleEvalResult EvalResponsibility(PuzzleDefinitionDto p, PuzzleDraftDto d, GameState s, IContentIndex index)
        {
            if (p.reportProofRequirements == null && (p.correctChoiceIds == null || p.correctChoiceIds.Length == 0))
                return Wrong(p);
            var expected = new Dictionary<string, string>();
            if (p.reportProofRequirements != null)
                for (int i = 0; i < p.reportProofRequirements.Length; i++)
                    expected[p.reportProofRequirements[i].item] = p.reportProofRequirements[i].section;
            expected["no_intent_or_criminal_verdict"] = "limit";

            var placed = new Dictionary<string, string>();
            if (d.reportPlacements != null)
                for (int i = 0; i < d.reportPlacements.Length; i++)
                    placed[d.reportPlacements[i].item] = d.reportPlacements[i].section;

            string[] forbidden = { "no_one_responsible", "all_ai_innocent", "intentional_murder_proven", "publicity_caused_collision" };
            for (int i = 0; i < forbidden.Length; i++)
                if (placed.ContainsKey(forbidden[i])) return Wrong(p);

            int correctPlacements = 0;
            foreach (var pair in expected)
            {
                if (!placed.TryGetValue(pair.Key, out var section)) continue;
                if (section == pair.Value) correctPlacements++;
            }
            int proofsNeeded = p.reportProofRequirements != null ? p.reportProofRequirements.Length : 0;
            int proofsDone = 0;
            if (p.reportProofRequirements != null)
            {
                for (int i = 0; i < p.reportProofRequirements.Length; i++)
                {
                    var req = p.reportProofRequirements[i];
                    if (!placed.TryGetValue(req.item, out var section) || section != req.section) continue;
                    var ids = FindReportEvidence(d, req.item);
                    if (ids != null && MatchEvidenceSet(req.sufficientEvidenceSets, ids) >= 0) proofsDone++;
                }
            }
            bool limitOk = placed.TryGetValue("no_intent_or_criminal_verdict", out var limitSection) && limitSection == "limit";
            bool extraItems = false;
            foreach (var pair in placed)
                if (!expected.ContainsKey(pair.Key)) extraItems = true;

            if (correctPlacements == expected.Count && proofsDone == proofsNeeded && limitOk && !extraItems)
                return Solved(p);
            if (correctPlacements == 0 && !limitOk)
                return Wrong(p);
            return Partial(string.IsNullOrEmpty(p.partialText) ? "수정이 필요한 항목이 있습니다. 구역과 근거를 다시 확인하십시오." : p.partialText);
        }

        private static string[] FindReportEvidence(PuzzleDraftDto d, string item)
        {
            if (d.reportEvidence == null) return null;
            for (int i = 0; i < d.reportEvidence.Length; i++)
                if (d.reportEvidence[i].item == item) return d.reportEvidence[i].evidenceIds;
            return null;
        }
    }
}
