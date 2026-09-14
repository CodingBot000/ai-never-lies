using System;

namespace ANL.Core.Contracts
{
    // ---- shared ----
    [Serializable]
    public sealed class ConditionDto
    {
        public string kind;
        public string id;
    }

    [Serializable]
    public sealed class StringPairDto
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class EvidenceSetDto
    {
        public string[] requiredIds;
    }

    [Serializable]
    public sealed class EdgeDto
    {
        public string from;
        public string to;
        public string kind;
    }

    [Serializable]
    public sealed class EdgeProofDto
    {
        public string from;
        public string to;
        public string kind;
        public EvidenceSetDto[] sufficientEvidenceSets;
    }

    [Serializable]
    public sealed class ReportProofDto
    {
        public string section;
        public string item;
        public EvidenceSetDto[] sufficientEvidenceSets;
    }

    [Serializable]
    public sealed class NumericAnswerDto
    {
        public string key;
        public double value;
        public double tolerance;
    }

    [Serializable]
    public sealed class PressLineDto
    {
        public string speaker;
        public string text;
    }

    // ---- episode.json ----
    [Serializable]
    public sealed class EpisodeFileDto
    {
        public int schemaVersion;
        public EpisodeDefinitionDto episode;
    }

    [Serializable]
    public sealed class EpisodeDefinitionDto
    {
        public string id;
        public string contentVersion;
        public string title;
        public string subtitle;
        public string startSceneId;
        public int saveSchemaVersion;
        public string artContractVersion;
        public string[] requiredPuzzleIds;
        public string[] optionalSceneIds;
    }

    // ---- scenes.json ----
    [Serializable]
    public sealed class SceneFileDto
    {
        public int schemaVersion;
        public SceneDefinitionDto[] items;
    }

    [Serializable]
    public sealed class EvidenceGrantDto
    {
        public string evidenceId;
        public string trigger;
        public string atDialogueId;
    }

    [Serializable]
    public sealed class TransitionDto
    {
        public string target;
        public ConditionDto[] conditions;
    }

    [Serializable]
    public sealed class SceneDefinitionDto
    {
        public string id;
        public int act;
        public string screenType;
        public string backgroundAssetId;
        public ConditionDto[] entryConditions;
        public string[] preDialogueIds;
        public string[] branchGroupIds;
        public string[] advancingGroupIds;
        public string[] interactionIds;
        public string[] postDialogueIds;
        public EvidenceGrantDto[] grants;
        public string[] requiredReadEvidenceIds;
        public string[] childSceneIds;
        public string[] optionalChildSceneIds;
        public TransitionDto[] transitions;
        public string tipText;
    }

    // ---- dialogues.ko.json ----
    [Serializable]
    public sealed class DialogueFileDto
    {
        public int schemaVersion;
        public DialogueLineDto[] items;
    }

    [Serializable]
    public sealed class DialogueLineDto
    {
        public string id;
        public string sceneId;
        public string speakerId;
        public string speaker;
        public string textId;
        public string text;
        public string portraitAssetId;
        public string phase;
    }

    // ---- evidence.json ----
    [Serializable]
    public sealed class EvidenceFileDto
    {
        public int schemaVersion;
        public EvidenceDefinitionDto[] items;
    }

    [Serializable]
    public sealed class EvidenceDefinitionDto
    {
        public string id;
        public string title;
        public string titleTextId;
        public string source;
        public string sourceTextId;
        public string body;
        public string bodyTextId;
        public string proves;
        public string provesTextId;
        public string limits;
        public string limitsTextId;
        public string imageAssetId;
        public string kind;
        public string firstSceneId;
        public string sourceMetadata;
    }

    // ---- testimonies.json ----
    [Serializable]
    public sealed class TestimonyFileDto
    {
        public int schemaVersion;
        public TestimonyStatementDto[] items;
    }

    [Serializable]
    public sealed class TestimonyStatementDto
    {
        public string id;
        public string sceneId;
        public string speakerId;
        public string speaker;
        public string textId;
        public string text;
        public string pressTextId;
        public PressLineDto[] pressResponse;
    }

    // ---- branches.json ----
    [Serializable]
    public sealed class BranchFileDto
    {
        public int schemaVersion;
        public BranchDefinitionDto[] items;
    }

    [Serializable]
    public sealed class EffectDto
    {
        public string kind;
        public string id;
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class BranchDefinitionDto
    {
        public string id;
        public string sceneId;
        public string groupId;
        public string label;
        public ConditionDto[] condition;
        public bool correct;
        public bool advances;
        public PressLineDto[] dialogue;
        public string feedback;
        public EffectDto[] effects;
        public string nextSceneId;
    }

    // ---- puzzles.json ----
    [Serializable]
    public sealed class PuzzleFileDto
    {
        public int schemaVersion;
        public PuzzleDefinitionDto[] items;
    }

    [Serializable]
    public sealed class RegionDto
    {
        public string regionId;
        public string evidenceId;
    }

    [Serializable]
    public sealed class PuzzleDefinitionDto
    {
        public string id;
        public string sceneId;
        public string type;
        public string[] requiredEvidenceIds;
        public string targetStatementId;
        public string[] correctChoiceIds;
        public string[] orderedIds;
        public EvidenceSetDto[] sufficientEvidenceSets;
        public StringPairDto[] valuePairs;
        public EdgeDto[] requiredEdges;
        public EdgeProofDto[] edgeProofRequirements;
        public ReportProofDto[] reportProofRequirements;
        public string[] andNodeIds;
        public NumericAnswerDto[] numericAnswers;
        public string[] requiredCaseIds;
        public RegionDto[] regions;
        public string windowId;
        public string successText;
        public string partialText;
        public string wrongText;
        public string[] evidenceSetCorrectTexts;
    }

    // ---- hints.ko.json ----
    [Serializable]
    public sealed class HintFileDto
    {
        public int schemaVersion;
        public HintDefinitionDto[] items;
    }

    [Serializable]
    public sealed class HintDefinitionDto
    {
        public string id;
        public string scopeType;
        public string scopeId;
        public int level;
        public string textId;
        public string text;
        public string focusTarget;
    }

    // ---- ui.ko.json ----
    [Serializable]
    public sealed class UiStringFileDto
    {
        public int schemaVersion;
        public UiStringDto[] items;
    }

    [Serializable]
    public sealed class UiStringDto
    {
        public string id;
        public string text;
    }

    // ---- graph.json ----
    [Serializable]
    public sealed class GraphFileDto
    {
        public int schemaVersion;
        public GraphNodeDto[] nodes;
        public EdgeDto[] causalEdges;
        public EdgeDto[] signalEdges;
        public EventLogDto[] eventLog;
        public KinematicsDto kinematics;
    }

    [Serializable]
    public sealed class GraphNodeDto
    {
        public string id;
        public string label;
        public string kind;
        public string[] evidenceIds;
    }

    [Serializable]
    public sealed class EventLogDto
    {
        public string id;
        public int relativeMs;
        public string source;
        public string type;
        public string payload;
        public string firstEvidenceId;
        public string display;
    }

    [Serializable]
    public sealed class KinematicsDto
    {
        public double speedKmh;
        public double actuatorDelaySeconds;
        public double weakDurationSeconds;
        public double weakDeceleration;
        public double strongDurationSeconds;
        public double strongDeceleration;
        public double historicalDistanceMeters;
        public double historicalImpactKmh;
        public double fullBrakeStopMeters;
        public double roadDecelerationMin;
        public double roadDecelerationMax;
        public double stopDistanceMinMeters;
        public double stopDistanceMaxMeters;
        public int requestToStrongMs;
        public string note;
    }

    // ---- matrix.json ----
    [Serializable]
    public sealed class MatrixFileDto
    {
        public int schemaVersion;
        public MatrixRowDto[] rows;
        public MatrixDimensionsDto dimensions;
    }

    [Serializable]
    public sealed class MatrixRowDto
    {
        public int index;
        public string sensor;
        public string overrideState;
        public string version;
        public string condition;
        public string verification;
        public string note;
    }

    [Serializable]
    public sealed class MatrixDimensionsDto
    {
        public string[] sensor;
        public string[] overrideState;
        public string[] version;
        public string[] condition;
    }
}
