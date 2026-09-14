using System;
using System.Collections.Generic;
using ANL.Core.Contracts;

namespace ANL.Core.State
{
    public static class Phases
    {
        public const string Entering = "entering";
        public const string PreDialogue = "pre_dialogue";
        public const string BranchChoice = "branch_choice";
        public const string Interaction = "interaction";
        public const string PostDialogue = "post_dialogue";
        public const string ReadCheck = "read_check";
        public const string HubActive = "hub_active";
        public const string ReadyToExit = "ready_to_exit";
        public const string Completed = "completed";
    }

    public sealed class GameState
    {
        public string CurrentSceneId = "";
        public string Phase = Phases.Entering;
        public int DialogueCursor;
        public string ActivePuzzleId = "";

        public readonly List<string> OwnedEvidence = new List<string>();
        public readonly List<string> ReadEvidence = new List<string>();
        public readonly List<string> VisitedScenes = new List<string>();
        public readonly List<string> CompletedScenes = new List<string>();
        public readonly List<string> SolvedPuzzles = new List<string>();
        public readonly List<string> VisitedTopics = new List<string>();
        public readonly List<StringPairDto> BranchValues = new List<StringPairDto>();
        public readonly List<PuzzleDraftDto> PuzzleDrafts = new List<PuzzleDraftDto>();
        public readonly List<HintUsageDto> HintUsage = new List<HintUsageDto>();
        public readonly List<string> AppliedEffectIds = new List<string>();
        public readonly List<string> PreDialogueDone = new List<string>();
        public readonly List<string> PostDialogueDone = new List<string>();

        public long LastCommandSequence;
        public string SavedAtUtc = "";

        public bool Owns(string evidenceId) => evidenceId != null && OwnedEvidence.Contains(evidenceId);
        public bool HasRead(string evidenceId) => evidenceId != null && ReadEvidence.Contains(evidenceId);
        public bool IsSceneVisited(string sceneId) => sceneId != null && VisitedScenes.Contains(sceneId);
        public bool IsSceneCompleted(string sceneId) => sceneId != null && CompletedScenes.Contains(sceneId);
        public bool IsPuzzleSolved(string puzzleId) => puzzleId != null && SolvedPuzzles.Contains(puzzleId);
        public bool IsTopicVisited(string topicId) => topicId != null && VisitedTopics.Contains(topicId);
        public bool IsPreDone(string sceneId) => sceneId != null && PreDialogueDone.Contains(sceneId);
        public bool IsPostDone(string sceneId) => sceneId != null && PostDialogueDone.Contains(sceneId);

        public bool AddOwned(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId) || OwnedEvidence.Contains(evidenceId)) return false;
            OwnedEvidence.Add(evidenceId);
            return true;
        }

        public bool MarkRead(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId) || !Owns(evidenceId) || ReadEvidence.Contains(evidenceId)) return false;
            ReadEvidence.Add(evidenceId);
            return true;
        }

        public string GetBranchValue(string key)
        {
            for (int i = 0; i < BranchValues.Count; i++)
                if (BranchValues[i].key == key) return BranchValues[i].value;
            return "";
        }

        public void SetBranchValue(string key, string value)
        {
            for (int i = 0; i < BranchValues.Count; i++)
            {
                if (BranchValues[i].key == key)
                {
                    BranchValues[i].value = value;
                    return;
                }
            }
            BranchValues.Add(new StringPairDto { key = key, value = value });
        }

        public PuzzleDraftDto GetDraft(string puzzleId, bool create)
        {
            for (int i = 0; i < PuzzleDrafts.Count; i++)
                if (PuzzleDrafts[i].puzzleId == puzzleId) return PuzzleDrafts[i];
            if (!create) return null;
            var draft = new PuzzleDraftDto
            {
                puzzleId = puzzleId,
                selectedStatementId = "",
                selectedEvidenceIds = new string[0],
                choiceIds = new string[0],
                placedCards = new PlacedCardDto[0],
                edges = new EdgeDto[0],
                values = new StringPairDto[0],
                edgeProofs = new EdgeProofSelectionDto[0],
                reportPlacements = new ReportPlacementDto[0],
                reportEvidence = new ReportEvidenceDto[0],
                ranCaseIds = new string[0],
                comparisonWindowId = ""
            };
            PuzzleDrafts.Add(draft);
            return draft;
        }

        public HintUsageDto GetHintUsage(string scopeId, bool create)
        {
            for (int i = 0; i < HintUsage.Count; i++)
                if (HintUsage[i].scopeId == scopeId) return HintUsage[i];
            if (!create) return null;
            var usage = new HintUsageDto { scopeId = scopeId, maxLevel = 0, openCount = 0 };
            HintUsage.Add(usage);
            return usage;
        }

        public bool MarkApplied(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return true;
            if (AppliedEffectIds.Contains(effectId)) return false;
            AppliedEffectIds.Add(effectId);
            return true;
        }

        public ProgressSnapshot ToSnapshot(string contentVersion, string artManifestSha256, string slotId)
        {
            return new ProgressSnapshot
            {
                saveSchemaVersion = 1,
                contentVersion = contentVersion,
                artManifestSha256 = artManifestSha256,
                slotId = slotId,
                currentSceneId = CurrentSceneId,
                phase = Phase,
                dialogueCursor = DialogueCursor,
                activePuzzleId = ActivePuzzleId,
                ownedEvidenceIds = OwnedEvidence.ToArray(),
                readEvidenceIds = ReadEvidence.ToArray(),
                visitedSceneIds = VisitedScenes.ToArray(),
                completedSceneIds = CompletedScenes.ToArray(),
                solvedPuzzleIds = SolvedPuzzles.ToArray(),
                visitedTopicIds = VisitedTopics.ToArray(),
                branchValues = BranchValues.ToArray(),
                puzzleDrafts = PuzzleDrafts.ToArray(),
                hintUsage = HintUsage.ToArray(),
                appliedEffectIds = AppliedEffectIds.ToArray(),
                preDialogueDoneSceneIds = PreDialogueDone.ToArray(),
                postDialogueDoneSceneIds = PostDialogueDone.ToArray(),
                lastCommandSequence = LastCommandSequence,
                savedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        public static GameState FromSnapshot(ProgressSnapshot snap)
        {
            var state = new GameState();
            if (snap == null) return state;
            state.CurrentSceneId = snap.currentSceneId ?? "";
            state.Phase = string.IsNullOrEmpty(snap.phase) ? Phases.Entering : snap.phase;
            state.DialogueCursor = snap.dialogueCursor;
            state.ActivePuzzleId = snap.activePuzzleId ?? "";
            Fill(state.OwnedEvidence, snap.ownedEvidenceIds);
            Fill(state.ReadEvidence, snap.readEvidenceIds);
            Fill(state.VisitedScenes, snap.visitedSceneIds);
            Fill(state.CompletedScenes, snap.completedSceneIds);
            Fill(state.SolvedPuzzles, snap.solvedPuzzleIds);
            Fill(state.VisitedTopics, snap.visitedTopicIds);
            FillPairs(state.BranchValues, snap.branchValues);
            FillDrafts(state.PuzzleDrafts, snap.puzzleDrafts);
            FillUsage(state.HintUsage, snap.hintUsage);
            Fill(state.AppliedEffectIds, snap.appliedEffectIds);
            Fill(state.PreDialogueDone, snap.preDialogueDoneSceneIds);
            Fill(state.PostDialogueDone, snap.postDialogueDoneSceneIds);
            state.LastCommandSequence = snap.lastCommandSequence;
            state.SavedAtUtc = snap.savedAtUtc ?? "";
            return state;
        }

        private static void Fill(List<string> target, string[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (!string.IsNullOrEmpty(source[i]) && !target.Contains(source[i])) target.Add(source[i]);
        }

        private static void FillPairs(List<StringPairDto> target, StringPairDto[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null && !string.IsNullOrEmpty(source[i].key)) target.Add(source[i]);
        }

        private static void FillDrafts(List<PuzzleDraftDto> target, PuzzleDraftDto[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null && !string.IsNullOrEmpty(source[i].puzzleId)) target.Add(source[i]);
        }

        private static void FillUsage(List<HintUsageDto> target, HintUsageDto[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null && !string.IsNullOrEmpty(source[i].scopeId)) target.Add(source[i]);
        }
    }
}
