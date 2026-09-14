using System;

namespace ANL.Core.Contracts
{
    [Serializable]
    public sealed class ProgressSnapshot
    {
        public int saveSchemaVersion;
        public string contentVersion;
        public string artManifestSha256;
        public string slotId;
        public string currentSceneId;
        public string phase;
        public int dialogueCursor;
        public string activePuzzleId;
        public string[] ownedEvidenceIds;
        public string[] readEvidenceIds;
        public string[] visitedSceneIds;
        public string[] completedSceneIds;
        public string[] solvedPuzzleIds;
        public string[] visitedTopicIds;
        public StringPairDto[] branchValues;
        public PuzzleDraftDto[] puzzleDrafts;
        public HintUsageDto[] hintUsage;
        public string[] appliedEffectIds;
        public string[] preDialogueDoneSceneIds;
        public string[] postDialogueDoneSceneIds;
        public long lastCommandSequence;
        public string savedAtUtc;
    }

    [Serializable]
    public sealed class PuzzleDraftDto
    {
        public string puzzleId;
        public string selectedStatementId;
        public string[] selectedEvidenceIds;
        public string[] choiceIds;
        public PlacedCardDto[] placedCards;
        public EdgeDto[] edges;
        public StringPairDto[] values;
        public EdgeProofSelectionDto[] edgeProofs;
        public ReportPlacementDto[] reportPlacements;
        public ReportEvidenceDto[] reportEvidence;
        public string[] ranCaseIds;
        public string comparisonWindowId;
        public bool submitted;
    }

    [Serializable]
    public sealed class PlacedCardDto
    {
        public string slot;
        public string value;
    }

    [Serializable]
    public sealed class EdgeProofSelectionDto
    {
        public string from;
        public string to;
        public string kind;
        public string[] evidenceIds;
    }

    [Serializable]
    public sealed class ReportPlacementDto
    {
        public string section;
        public string item;
    }

    [Serializable]
    public sealed class ReportEvidenceDto
    {
        public string item;
        public string[] evidenceIds;
    }

    [Serializable]
    public sealed class HintUsageDto
    {
        public string scopeId;
        public int maxLevel;
        public int openCount;
        public bool usedTestAssist;
    }

    [Serializable]
    public sealed class SaveEnvelopeDto
    {
        public int schemaVersion;
        public string contentVersion;
        public string payloadJson;
        public string payloadSha256;
        public string savedAtUtc;
        public string slotId;
    }
}
