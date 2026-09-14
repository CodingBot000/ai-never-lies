using System.Collections.Generic;
using ANL.Core.Contracts;
using ANL.Core.Content;
using ANL.Core.State;

namespace ANL.Core.Conditions
{
    public static class ConditionKind
    {
        public const string SceneCompleted = "SceneCompleted";
        public const string EvidenceOwned = "EvidenceOwned";
        public const string EvidenceRead = "EvidenceRead";
        public const string PuzzleSolved = "PuzzleSolved";
        public const string TopicVisited = "TopicVisited";
        public const string BranchEquals = "BranchEquals";
        public const string PostDialogueComplete = "PostDialogueComplete";
        public const string AllRequiredChildrenComplete = "AllRequiredChildrenComplete";

        public static bool IsKnown(string kind)
        {
            switch (kind)
            {
                case SceneCompleted:
                case EvidenceOwned:
                case EvidenceRead:
                case PuzzleSolved:
                case TopicVisited:
                case BranchEquals:
                case PostDialogueComplete:
                case AllRequiredChildrenComplete:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static class ConditionEvaluator
    {
        public static bool Evaluate(ConditionDto condition, GameState state, IContentIndex index)
        {
            if (condition == null || string.IsNullOrEmpty(condition.kind) || string.IsNullOrEmpty(condition.id))
                return false; // fail closed
            switch (condition.kind)
            {
                case ConditionKind.SceneCompleted:
                    return state.IsSceneCompleted(condition.id);
                case ConditionKind.EvidenceOwned:
                    return state.Owns(condition.id);
                case ConditionKind.EvidenceRead:
                    return state.HasRead(condition.id);
                case ConditionKind.PuzzleSolved:
                    return state.IsPuzzleSolved(condition.id);
                case ConditionKind.TopicVisited:
                case ConditionKind.BranchEquals:
                    return state.IsTopicVisited(condition.id);
                case ConditionKind.PostDialogueComplete:
                    return state.IsPostDone(condition.id) || state.IsSceneCompleted(condition.id);
                case ConditionKind.AllRequiredChildrenComplete:
                    return AllRequiredChildrenComplete(condition.id, state, index);
                default:
                    return false;
            }
        }

        public static bool AllConditionsMet(IEnumerable<ConditionDto> conditions, GameState state, IContentIndex index)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
                if (!Evaluate(condition, state, index)) return false;
            return true;
        }

        public static bool AllRequiredChildrenComplete(string hubSceneId, GameState state, IContentIndex index)
        {
            var hub = index.Scene(hubSceneId);
            if (hub == null || hub.childSceneIds == null || hub.childSceneIds.Length == 0) return false;
            for (int i = 0; i < hub.childSceneIds.Length; i++)
                if (!state.IsSceneCompleted(hub.childSceneIds[i])) return false;
            return true;
        }

        public static List<string> RemainingRequiredChildren(string hubSceneId, GameState state, IContentIndex index)
        {
            var result = new List<string>();
            var hub = index.Scene(hubSceneId);
            if (hub?.childSceneIds == null) return result;
            for (int i = 0; i < hub.childSceneIds.Length; i++)
                if (!state.IsSceneCompleted(hub.childSceneIds[i])) result.Add(hub.childSceneIds[i]);
            return result;
        }
    }
}
