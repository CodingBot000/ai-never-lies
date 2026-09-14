using System.Collections.Generic;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.State;

namespace ANL.Core.Hints
{
    public sealed class HintContext
    {
        public string scopeType = "scene";
        public string scopeId = "";
        public string title = "";
        public string sceneTip = "";
        public string nextAction = "";
        public readonly List<HintDefinitionDto> levels = new List<HintDefinitionDto>();
    }

    public sealed class HintService
    {
        private readonly IContentIndex _index;

        public HintService(IContentIndex index)
        {
            _index = index;
        }

        public HintContext Resolve(GameState state)
        {
            var context = new HintContext();
            var scene = _index.Scene(state.CurrentSceneId);
            context.scopeId = state.CurrentSceneId;
            context.title = scene != null ? scene.id : "";
            context.sceneTip = scene != null ? scene.tipText : "";

            switch (state.Phase)
            {
                case Phases.Interaction:
                {
                    var puzzle = _index.Puzzle(state.ActivePuzzleId);
                    if (puzzle != null)
                    {
                        context.scopeType = "puzzle";
                        context.scopeId = puzzle.id;
                        context.title = puzzle.id;
                        var all = _index.Hints;
                        foreach (var pair in all)
                        {
                            var hint = pair.Value;
                            if (hint.scopeType == "puzzle" && hint.scopeId == puzzle.id)
                                context.levels.Add(hint);
                        }
                        context.levels.Sort((a, b) => a.level.CompareTo(b.level));
                    }
                    break;
                }
                case Phases.HubActive:
                {
                    var hub = _index.Scene(state.CurrentSceneId);
                    if (hub?.childSceneIds != null)
                    {
                        for (int i = 0; i < hub.childSceneIds.Length; i++)
                        {
                            if (!state.IsSceneCompleted(hub.childSceneIds[i]))
                            {
                                var child = _index.Scene(hub.childSceneIds[i]);
                                context.nextAction = child != null ? child.id : hub.childSceneIds[i];
                                break;
                            }
                        }
                    }
                    break;
                }
                case Phases.ReadCheck:
                {
                    var s = _index.Scene(state.CurrentSceneId);
                    if (s?.requiredReadEvidenceIds != null)
                    {
                        for (int i = 0; i < s.requiredReadEvidenceIds.Length; i++)
                        {
                            if (!state.HasRead(s.requiredReadEvidenceIds[i]))
                            {
                                var evidence = _index.EvidenceDef(s.requiredReadEvidenceIds[i]);
                                context.nextAction = evidence != null ? evidence.title : s.requiredReadEvidenceIds[i];
                                break;
                            }
                        }
                    }
                    break;
                }
                case Phases.BranchChoice:
                {
                    var s = _index.Scene(state.CurrentSceneId);
                    if (s?.advancingGroupIds != null)
                    {
                        for (int g = 0; g < s.advancingGroupIds.Length; g++)
                        {
                            var group = _index.BranchGroup(s.advancingGroupIds[g]);
                            for (int i = 0; i < group.Count; i++)
                            {
                                if (!state.IsTopicVisited(group[i].id))
                                {
                                    context.nextAction = group[i].label;
                                    break;
                                }
                            }
                        }
                    }
                    break;
                }
                case Phases.ReadyToExit:
                case Phases.Completed:
                {
                    var s = _index.Scene(state.CurrentSceneId);
                    if (s?.transitions != null && s.transitions.Length > 0)
                        context.nextAction = s.transitions[0].target;
                    if (context.levels.Count == 0)
                        context.levels.Add(HintLevel(state.CurrentSceneId, 1));
                    break;
                }
                default:
                    context.nextAction = state.CurrentSceneId;
                    break;
            }
            if (context.levels.Count == 0)
            {
                var sceneHint = SceneHint(state.CurrentSceneId);
                if (sceneHint != null) context.levels.Add(sceneHint);
            }
            return context;
        }

        public HintDefinitionDto SceneHint(string sceneId)
        {
            foreach (var pair in _index.Hints)
            {
                var hint = pair.Value;
                if (hint.scopeType == "scene" && hint.scopeId == sceneId && hint.level == 1) return hint;
            }
            return null;
        }

        private HintDefinitionDto HintLevel(string sceneId, int level)
        {
            foreach (var pair in _index.Hints)
            {
                var hint = pair.Value;
                if (hint.scopeType == "scene" && hint.scopeId == sceneId && hint.level == level) return hint;
            }
            return null;
        }
    }
}
