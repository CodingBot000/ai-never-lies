using System;
using System.Collections.Generic;

namespace ANL.Core.Contracts
{
    public static class CommandKinds
    {
        public const string StartEpisode = "StartEpisode";
        public const string AdvanceDialogue = "AdvanceDialogue";
        public const string ChooseTopic = "ChooseTopic";
        public const string InspectRegion = "InspectRegion";
        public const string OpenEvidence = "OpenEvidence";
        public const string ReadEvidence = "ReadEvidence";
        public const string SelectStatement = "SelectStatement";
        public const string PressStatement = "PressStatement";
        public const string SelectEvidence = "SelectEvidence";
        public const string PresentEvidence = "PresentEvidence";
        public const string PlaceCard = "PlaceCard";
        public const string RemoveCard = "RemoveCard";
        public const string ConnectNodes = "ConnectNodes";
        public const string RemoveEdge = "RemoveEdge";
        public const string SelectComparisonWindow = "SelectComparisonWindow";
        public const string RunCounterfactualCase = "RunCounterfactualCase";
        public const string SubmitPuzzle = "SubmitPuzzle";
        public const string SelectChoice = "SelectChoice";
        public const string ChooseBranch = "ChooseBranch";
        public const string ContinueScene = "ContinueScene";
        public const string ReturnToHub = "ReturnToHub";
        public const string SaveSlot = "SaveSlot";
        public const string LoadSlot = "LoadSlot";
        public const string OpenHint = "OpenHint";
        public const string GoToScene = "GoToScene"; // development fixture jumps only
    }

    public static class Origins
    {
        public const string User = "user";
        public const string TestAssist = "test-assist";
        public const string Replay = "replay";
        public const string Fixture = "fixture";
    }

    public static class ResultStatus
    {
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Partial = "Partial";
        public const string Solved = "Solved";
    }

    [Serializable]
    public sealed class GameCommand
    {
        public string commandId;
        public long sequence;
        public string sceneId;
        public string origin;
        public string type;
        public string targetId;
        public string[] ids;
        public string slot;
        public string value;
        public double numeric;
    }

    [Serializable]
    public sealed class DomainEvent
    {
        public string kind;
        public string id;
        public string message;
        public PressLineDto[] lines;
    }

    [Serializable]
    public sealed class CommandResult
    {
        public string status;
        public string reasonCode;
        public string message;
        public List<DomainEvent> events = new List<DomainEvent>();
        public bool Changed;

        public static CommandResult Reject(string reason, string message = "")
        {
            return new CommandResult { status = ResultStatus.Rejected, reasonCode = reason, message = message };
        }

        public static CommandResult Accept(string reason = "", string message = "")
        {
            return new CommandResult { status = ResultStatus.Accepted, reasonCode = reason, message = message, Changed = true };
        }
    }
}
