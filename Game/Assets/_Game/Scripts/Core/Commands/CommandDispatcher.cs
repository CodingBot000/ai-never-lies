using System;
using System.Collections.Generic;
using ANL.Core.Conditions;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Puzzles;
using ANL.Core.State;

namespace ANL.Core.Commands
{
    public sealed class CommandDispatcher
    {
        private readonly IContentIndex _index;
        private readonly GameState _state;
        private readonly HashSet<string> _processedCommandIds = new HashSet<string>();
        private readonly HashSet<string> _processedEffects = new HashSet<string>();

        public IContentIndex Index => _index;
        public GameState State => _state;

        public Func<string, bool, bool> SaveRequested;
        public Func<string, bool> LoadRequested;

        public CommandDispatcher(IContentIndex index, GameState state)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            foreach (var id in _state.AppliedEffectIds) _processedEffects.Add(id);
        }

        // ---------------- public ----------------

        public CommandResult Execute(GameCommand command)
        {
            if (command == null) return CommandResult.Reject("null_command");
            if (string.IsNullOrEmpty(command.type)) return CommandResult.Reject("missing_type");
            if (string.IsNullOrEmpty(command.commandId)) return CommandResult.Reject("missing_command_id");
            if (_processedCommandIds.Contains(command.commandId) || command.sequence <= _state.LastCommandSequence)
                return CommandResult.Reject("duplicate_command");
            if (OriginOf(command) != Origins.Fixture && !string.IsNullOrEmpty(command.sceneId))
            {
                if (_state.CurrentSceneId != command.sceneId && command.type != CommandKinds.StartEpisode)
                {
                    // UI may still hold a stale screen; reject quietly without consuming sequence.
                    return CommandResult.Reject("wrong_scene");
                }
            }

            var result = Dispatch(command);
            if (result.status != ResultStatus.Rejected)
            {
                _processedCommandIds.Add(command.commandId);
                _state.LastCommandSequence = command.sequence;
            }
            else if (result.reasonCode == "duplicate_command" || result.reasonCode == "wrong_scene")
            {
                // do not consume sequence
            }
            else
            {
                _processedCommandIds.Add(command.commandId);
                _state.LastCommandSequence = command.sequence;
            }
            return result;
        }

        private static string OriginOf(GameCommand c) => string.IsNullOrEmpty(c.origin) ? Origins.User : c.origin;

        public bool HasUnsolvedInteraction(out string puzzleId)
        {
            puzzleId = "";
            var scene = _index.Scene(_state.CurrentSceneId);
            if (scene?.interactionIds == null) return false;
            for (int i = 0; i < scene.interactionIds.Length; i++)
            {
                if (!_state.IsPuzzleSolved(scene.interactionIds[i]))
                {
                    puzzleId = scene.interactionIds[i];
                    return true;
                }
            }
            return false;
        }

        public bool CanContinue(out string reason)
        {
            reason = "";
            switch (_state.Phase)
            {
                case Phases.PreDialogue:
                case Phases.PostDialogue:
                    reason = "dialogue_not_finished";
                    return false;
                case Phases.Interaction:
                    reason = "puzzle_active";
                    return false;
                case Phases.BranchChoice:
                    reason = "choice_pending";
                    return false;
                case Phases.ReadCheck:
                    return RequiredReadSatisfied(out reason);
                case Phases.ReadyToExit:
                case Phases.Completed:
                    return TransitionAvailable(out _, out reason);
                case Phases.HubActive:
                    return HubContinueAvailable(out _, out reason);
                default:
                    reason = "not_ready";
                    return false;
            }
        }

        // ---------------- dispatch ----------------

        private CommandResult Dispatch(GameCommand c)
        {
            switch (c.type)
            {
                case CommandKinds.StartEpisode: return DoStartEpisode();
                case CommandKinds.AdvanceDialogue: return DoAdvanceDialogue(c);
                case CommandKinds.ContinueScene: return DoContinueScene(c);
                case CommandKinds.ReturnToHub: return DoReturnToHub(c);
                case CommandKinds.ChooseBranch: return DoChooseBranch(c);
                case CommandKinds.ChooseTopic: return DoChooseBranch(c);
                case CommandKinds.OpenEvidence: return DoOpenEvidence(c);
                case CommandKinds.ReadEvidence: return DoOpenEvidence(c);
                case CommandKinds.OpenHint: return DoOpenHint(c);
                case CommandKinds.SaveSlot:
                    SaveRequested?.Invoke(c.targetId ?? "slot_01", c.numeric > 0);
                    return CommandResult.Accept("save_requested");
                case CommandKinds.LoadSlot:
                    if (LoadRequested != null && LoadRequested(c.targetId ?? ""))
                        return CommandResult.Accept("load_requested");
                    return CommandResult.Reject("load_failed");
                case CommandKinds.GoToScene: return DoGoToScene(c);
            }

            if (_state.Phase != Phases.Interaction)
                return CommandResult.Reject("not_interaction");
            var puzzle = _index.Puzzle(_state.ActivePuzzleId);
            if (puzzle == null) return CommandResult.Reject("no_active_puzzle");
            var draft = _state.GetDraft(puzzle.id, true);

            switch (c.type)
            {
                case CommandKinds.InspectRegion: return DoInspectRegion(puzzle, draft, c);
                case CommandKinds.SelectStatement: return DoSelectStatement(puzzle, draft, c);
                case CommandKinds.PressStatement: return DoPressStatement(c);
                case CommandKinds.SelectEvidence: return DoSelectEvidence(puzzle, draft, c);
                case CommandKinds.PresentEvidence: return DoPresent(puzzle, draft, c);
                case CommandKinds.SelectChoice: return DoSelectChoice(puzzle, draft, c);
                case CommandKinds.PlaceCard: return DoPlaceCard(puzzle, draft, c);
                case CommandKinds.RemoveCard: return DoRemoveCard(puzzle, draft, c);
                case CommandKinds.ConnectNodes: return DoConnectNodes(puzzle, draft, c);
                case CommandKinds.RemoveEdge: return DoRemoveEdge(puzzle, draft, c);
                case CommandKinds.SelectComparisonWindow: return DoSelectWindow(puzzle, draft, c);
                case CommandKinds.RunCounterfactualCase: return DoRunCase(puzzle, draft, c);
                case CommandKinds.SubmitPuzzle: return DoSubmit(puzzle, draft, c);
                default:
                    return CommandResult.Reject("unsupported_command", c.type);
            }
        }

        // ---------------- scene flow ----------------

        private CommandResult DoStartEpisode()
        {
            var episode = _index.Episode;
            if (episode == null) return CommandResult.Reject("missing_episode");
            EnterScene(episode.startSceneId);
            var r = CommandResult.Accept("episode_started");
            r.events.Add(new DomainEvent { kind = "scene_entered", id = episode.startSceneId, message = "" });
            return r;
        }

        private CommandResult DoGoToScene(GameCommand c)
        {
            if (!_index.HasScene(c.targetId)) return CommandResult.Reject("unknown_scene", c.targetId);
            EnterScene(c.targetId);
            var r = CommandResult.Accept("scene_entered_fixture");
            r.events.Add(new DomainEvent { kind = "scene_entered", id = c.targetId, message = "fixture" });
            return r;
        }

        public void EnterScene(string sceneId)
        {
            var scene = _index.Scene(sceneId);
            _state.CurrentSceneId = sceneId;
            _state.ActivePuzzleId = "";
            if (!_state.IsSceneVisited(sceneId)) _state.VisitedScenes.Add(sceneId);
            if (scene == null)
            {
                _state.Phase = Phases.Completed;
                return;
            }
            _state.Phase = Phases.Entering;

            if (!_state.IsPreDone(sceneId))
            {
                ApplyGrants(scene, "scene_start");
                if (scene.preDialogueIds != null && scene.preDialogueIds.Length > 0)
                {
                    _state.Phase = Phases.PreDialogue;
                    _state.DialogueCursor = 0;
                    ApplyLineGrants(scene.preDialogueIds[0]);
                }
                else
                {
                    CompletePreDialogue(scene);
                }
                return;
            }

            // revisit: no rewards are replayed
            if (scene.screenType == "hub")
            {
                _state.Phase = Phases.HubActive;
                return;
            }
            if (HasUnsolvedInteraction(out var pending))
            {
                StartPuzzle(pending);
                return;
            }
            if (!_state.IsPostDone(sceneId) && scene.postDialogueIds != null && scene.postDialogueIds.Length > 0 && IsSceneCompleted(sceneId) == false)
            {
                ApplyGrants(scene, "post_start");
                _state.Phase = Phases.PostDialogue;
                _state.DialogueCursor = 0;
                ApplyLineGrants(scene.postDialogueIds[0]);
                return;
            }
            if (RequiredReadOutstanding(scene))
            {
                _state.Phase = Phases.ReadCheck;
                return;
            }
            _state.Phase = IsSceneCompleted(sceneId) ? Phases.Completed : Phases.ReadyToExit;
        }

        private bool IsSceneCompleted(string sceneId) => _state.IsSceneCompleted(sceneId);

        private CommandResult DoAdvanceDialogue(GameCommand c)
        {
            var scene = _index.Scene(_state.CurrentSceneId);
            if (scene == null) return CommandResult.Reject("unknown_scene");
            if (_state.Phase == Phases.PreDialogue)
            {
                _state.DialogueCursor++;
                if (scene.preDialogueIds == null || _state.DialogueCursor >= scene.preDialogueIds.Length)
                {
                    CompletePreDialogue(scene);
                    var r = CommandResult.Accept("pre_dialogue_complete");
                    r.events.Add(PhaseEvent());
                    return r;
                }
                ApplyLineGrants(scene.preDialogueIds[_state.DialogueCursor]);
                return CommandResult.Accept("dialogue_advanced");
            }
            if (_state.Phase == Phases.PostDialogue)
            {
                _state.DialogueCursor++;
                if (scene.postDialogueIds == null || _state.DialogueCursor >= scene.postDialogueIds.Length)
                {
                    CompletePostDialogue(scene);
                    var r = CommandResult.Accept("post_dialogue_complete");
                    r.events.Add(new DomainEvent { kind = "scene_completed", id = scene.id, message = "" });
                    r.events.Add(PhaseEvent());
                    return r;
                }
                ApplyLineGrants(scene.postDialogueIds[_state.DialogueCursor]);
                return CommandResult.Accept("dialogue_advanced");
            }
            return CommandResult.Reject("not_in_dialogue");
        }

        private DomainEvent PhaseEvent() => new DomainEvent { kind = "phase", id = _state.Phase, message = _state.CurrentSceneId };

        private void CompletePreDialogue(SceneDefinitionDto scene)
        {
            if (!_state.IsPreDone(scene.id)) _state.PreDialogueDone.Add(scene.id);
            ApplyGrants(scene, "interaction_start");

            bool hasBranches = scene.branchGroupIds != null && scene.branchGroupIds.Length > 0;
            if (hasBranches)
            {
                _state.Phase = Phases.BranchChoice;
                return;
            }
            ContinueAfterChoices(scene);
        }

        public void ContinueAfterChoices(SceneDefinitionDto scene)
        {
            if (HasUnsolvedInteraction(out var pending))
            {
                StartPuzzle(pending);
                return;
            }
            if (RequiredReadOutstanding(scene))
            {
                _state.Phase = Phases.ReadCheck;
                return;
            }
            if (!_state.IsPostDone(scene.id) && scene.postDialogueIds != null && scene.postDialogueIds.Length > 0)
            {
                ApplyGrants(scene, "post_start");
                _state.Phase = Phases.PostDialogue;
                _state.DialogueCursor = 0;
                ApplyLineGrants(scene.postDialogueIds[0]);
                return;
            }
            FinalizeScene(scene);
        }

        private void FinalizeScene(SceneDefinitionDto scene)
        {
            if (!_state.IsPostDone(scene.id)) _state.PostDialogueDone.Add(scene.id);
            if (!_state.IsSceneCompleted(scene.id)) _state.CompletedScenes.Add(scene.id);
            _state.Phase = Phases.ReadyToExit;
            _state.ActivePuzzleId = "";
        }

        private void CompletePostDialogue(SceneDefinitionDto scene)
        {
            if (!_state.IsPostDone(scene.id)) _state.PostDialogueDone.Add(scene.id);
            if (RequiredReadOutstanding(scene))
            {
                _state.Phase = Phases.ReadCheck;
                return;
            }
            FinalizeScene(scene);
        }

        private void StartPuzzle(string puzzleId)
        {
            _state.ActivePuzzleId = puzzleId;
            _state.Phase = Phases.Interaction;
            _state.GetDraft(puzzleId, true);
        }

        private bool RequiredReadOutstanding(SceneDefinitionDto scene)
        {
            if (scene.requiredReadEvidenceIds == null) return false;
            for (int i = 0; i < scene.requiredReadEvidenceIds.Length; i++)
                if (!_state.HasRead(scene.requiredReadEvidenceIds[i])) return true;
            return false;
        }

        private bool RequiredReadSatisfied(out string reason)
        {
            reason = "";
            var scene = _index.Scene(_state.CurrentSceneId);
            if (scene?.requiredReadEvidenceIds != null)
            {
                for (int i = 0; i < scene.requiredReadEvidenceIds.Length; i++)
                {
                    if (!_state.HasRead(scene.requiredReadEvidenceIds[i]))
                    {
                        reason = "need_read:" + scene.requiredReadEvidenceIds[i];
                        return false;
                    }
                }
            }
            reason = "read_satisfied";
            return true;
        }

        private CommandResult DoContinueScene(GameCommand c)
        {
            switch (_state.Phase)
            {
                case Phases.BranchChoice:
                    var scene = _index.Scene(_state.CurrentSceneId);
                    if (!AllAdvancingGroupsResolved(scene))
                        return CommandResult.Reject("advancing_choice_required");
                    ContinueAfterChoices(scene);
                    return CommandResult.Accept("choices_done", PhaseMessage());
                case Phases.ReadCheck:
                    if (!RequiredReadSatisfied(out var reason)) return CommandResult.Reject(reason);
                    {
                        var s = _index.Scene(_state.CurrentSceneId);
                        ContinueAfterChoices(s);
                        return CommandResult.Accept("read_satisfied", PhaseMessage());
                    }
                case Phases.ReadyToExit:
                case Phases.Completed:
                    return DoTransition();
                case Phases.HubActive:
                    return DoTransition();
                default:
                    return CommandResult.Reject("not_ready", _state.Phase);
            }
        }

        private string PhaseMessage() => _state.CurrentSceneId + ":" + _state.Phase;

        private bool AllAdvancingGroupsResolved(SceneDefinitionDto scene)
        {
            if (scene?.advancingGroupIds == null || scene.advancingGroupIds.Length == 0) return true;
            for (int i = 0; i < scene.advancingGroupIds.Length; i++)
            {
                var group = _index.BranchGroup(scene.advancingGroupIds[i]);
                bool resolved = false;
                for (int j = 0; j < group.Count; j++)
                    if (_state.IsTopicVisited(group[j].id)) { resolved = true; break; }
                if (!resolved) return false;
            }
            return true;
        }

        public bool TransitionAvailable(out string target, out string reason)
        {
            target = "";
            reason = "";
            var scene = _index.Scene(_state.CurrentSceneId);
            if (scene?.transitions == null) { reason = "no_transition"; return false; }
            bool hubLike = scene.screenType == "hub" || scene.childSceneIds != null && scene.childSceneIds.Length > 0;
            for (int i = 0; i < scene.transitions.Length; i++)
            {
                var t = scene.transitions[i];
                if (ConditionEvaluator.AllConditionsMet(t.conditions, _state, _index))
                {
                    target = t.target;
                    return true;
                }
            }
            if (hubLike)
            {
                var remaining = ConditionEvaluator.RemainingRequiredChildren(_state.CurrentSceneId, _state, _index);
                reason = remaining.Count > 0 ? "remaining:" + string.Join(",", remaining) : "no_transition";
            }
            else
            {
                reason = "conditions_not_met";
            }
            return false;
        }

        private bool HubContinueAvailable(out string target, out string reason) => TransitionAvailable(out target, out reason);

        private CommandResult DoTransition()
        {
            if (!TransitionAvailable(out var target, out var reason))
            {
                var r = CommandResult.Reject(reason);
                if (reason.StartsWith("remaining:"))
                    r.message = _index.Ui("ui.hub_all_required_done");
                return r;
            }
            var previous = _state.CurrentSceneId;
            EnterScene(target);
            var result = CommandResult.Accept("scene_transition");
            result.events.Add(new DomainEvent { kind = "scene_entered", id = target, message = previous });
            result.events.Add(PhaseEvent());
            return result;
        }

        private CommandResult DoReturnToHub(GameCommand c)
        {
            string parent = "";
            foreach (var pair in _index.Scenes)
            {
                var children = pair.Value.childSceneIds;
                if (children == null) continue;
                for (int i = 0; i < children.Length; i++)
                    if (children[i] == _state.CurrentSceneId) { parent = pair.Key; break; }
                if (!string.IsNullOrEmpty(parent)) break;
            }
            if (string.IsNullOrEmpty(parent))
            {
                if (c != null && !string.IsNullOrEmpty(c.targetId)) parent = c.targetId;
                else return CommandResult.Reject("no_parent_hub");
            }
            EnterScene(parent);
            var r = CommandResult.Accept("returned_to_hub");
            r.events.Add(new DomainEvent { kind = "scene_entered", id = parent, message = _state.CurrentSceneId });
            return r;
        }

        // ---------------- branches ----------------

        private CommandResult DoChooseBranch(GameCommand c)
        {
            if (_state.Phase != Phases.BranchChoice) return CommandResult.Reject("not_choosing");
            var branch = _index.Branch(c.targetId);
            if (branch == null || branch.sceneId != _state.CurrentSceneId) return CommandResult.Reject("unknown_branch");
            if (_state.IsTopicVisited(branch.id)) return CommandResult.Reject("duplicate_choice");
            if (!ConditionEvaluator.AllConditionsMet(branch.condition, _state, _index))
                return CommandResult.Reject("choice_locked");

            ApplyBranchEffects(branch);

            var result = CommandResult.Accept(branch.advances ? "branch_advanced" : "branch_retry");
            if (branch.dialogue != null && branch.dialogue.Length > 0)
                result.events.Add(new DomainEvent { kind = "branch_dialogue", id = branch.id, lines = branch.dialogue });
            if (!string.IsNullOrEmpty(branch.feedback))
            {
                result.message = branch.feedback;
                result.events.Add(new DomainEvent { kind = "feedback", id = branch.id, message = branch.feedback });
            }
            if (branch.advances)
            {
                var scene = _index.Scene(_state.CurrentSceneId);
                if (AllAdvancingGroupsResolved(scene))
                {
                    ContinueAfterChoices(scene);
                    result.events.Add(PhaseEvent());
                }
                else
                {
                    _state.Phase = Phases.BranchChoice;
                }
            }
            return result;
        }

        private void ApplyBranchEffects(BranchDefinitionDto branch)
        {
            _state.VisitedTopics.Add(branch.id);
            if (branch.effects == null) return;
            for (int i = 0; i < branch.effects.Length; i++)
            {
                var e = branch.effects[i];
                if (e == null) continue;
                switch (e.kind)
                {
                    case "SetBranch":
                        _state.SetBranchValue(e.key, e.value);
                        break;
                    case "GrantEvidence":
                        _state.AddOwned(e.id);
                        break;
                    case "Feedback":
                        break;
                }
            }
        }

        // ---------------- evidence / hints ----------------

        private CommandResult DoOpenEvidence(GameCommand c)
        {
            var evidence = _index.EvidenceDef(c.targetId);
            if (evidence == null) return CommandResult.Reject("unknown_evidence");
            if (!_state.Owns(evidence.id)) return CommandResult.Reject("evidence_not_owned");
            bool newlyRead = _state.MarkRead(evidence.id);
            var result = CommandResult.Accept(newlyRead ? "evidence_read" : "evidence_already_read");
            result.events.Add(new DomainEvent { kind = "evidence_opened", id = evidence.id, message = evidence.title });
            return result;
        }

        private CommandResult DoOpenHint(GameCommand c)
        {
            string scope = string.IsNullOrEmpty(c.targetId) ? _state.CurrentSceneId : c.targetId;
            var usage = _state.GetHintUsage(scope, true);
            int level = (int)c.numeric;
            if (level > usage.maxLevel) usage.maxLevel = level;
            usage.openCount++;
            if (OriginOf(c) == Origins.TestAssist) usage.usedTestAssist = true;
            return CommandResult.Accept("hint_open");
        }

        // ---------------- puzzle commands ----------------

        private CommandResult DoInspectRegion(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            if (puzzle.type != PuzzleTypes.HotspotSet) return CommandResult.Reject("unsupported_command");
            RegionDto region = null;
            if (puzzle.regions != null)
                for (int i = 0; i < puzzle.regions.Length; i++)
                    if (puzzle.regions[i].regionId == c.targetId) { region = puzzle.regions[i]; break; }
            if (region == null) return CommandResult.Reject("unknown_region");

            bool already = false;
            var cards = new List<PlacedCardDto>(draft.placedCards ?? new PlacedCardDto[0]);
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].slot == "region" && cards[i].value == region.regionId) already = true;
            bool granted = false;
            if (!already) cards.Add(new PlacedCardDto { slot = "region", value = region.regionId });
            draft.placedCards = cards.ToArray();
            if (!string.IsNullOrEmpty(region.evidenceId) && _state.AddOwned(region.evidenceId))
                granted = true;

            var evaluation = PuzzleEvaluator.Evaluate(puzzle, draft, _state, _index);
            var result = CommandResult.Accept(already ? "region_already" : "region_inspected");
            result.events.Add(new DomainEvent { kind = "region_inspected", id = region.regionId, message = region.evidenceId });
            if (granted)
                result.events.Add(new DomainEvent { kind = "evidence_granted", id = region.evidenceId, message = "" });
            if (evaluation.Solved)
            {
                SolvePuzzle(puzzle);
                result.status = ResultStatus.Solved;
                result.reasonCode = "puzzle_solved";
                result.message = evaluation.Message;
                result.events.Add(new DomainEvent { kind = "puzzle_solved", id = puzzle.id, message = evaluation.Message });
                result.events.Add(PhaseEvent());
            }
            else
            {
                result.message = already ? (_index.Ui("ui.region_already")) : "";
            }
            return result;
        }

        private CommandResult DoSelectStatement(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            var statement = _index.Testimony(c.targetId);
            if (statement == null || statement.sceneId != puzzle.sceneId) return CommandResult.Reject("unknown_statement");
            draft.selectedStatementId = statement.id;
            var result = CommandResult.Accept("statement_selected");
            result.events.Add(new DomainEvent { kind = "statement_selected", id = statement.id, message = statement.text });
            return result;
        }

        private CommandResult DoPressStatement(GameCommand c)
        {
            var statement = _index.Testimony(c.targetId);
            if (statement == null) return CommandResult.Reject("unknown_statement");
            var result = CommandResult.Accept("statement_pressed");
            if (statement.pressResponse != null && statement.pressResponse.Length > 0)
                result.events.Add(new DomainEvent { kind = "branch_dialogue", id = statement.id, lines = statement.pressResponse });
            return result;
        }

        private CommandResult DoSelectEvidence(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            var ids = c.ids ?? new string[0];
            if (ids.Length > 3) return CommandResult.Reject("too_many_evidence");
            for (int i = 0; i < ids.Length; i++)
            {
                if (!_index.HasEvidence(ids[i])) return CommandResult.Reject("unknown_evidence", ids[i]);
                if (!_state.Owns(ids[i])) return CommandResult.Reject("evidence_not_owned", ids[i]);
            }
            if (puzzle.type == PuzzleTypes.CausalGraph)
            {
                string[] parts = (c.targetId ?? "").Split('|');
                if (parts.Length != 3) return CommandResult.Reject("edge_target_required");
                SetEdgeProof(draft, parts[0], parts[1], parts[2], ids);
                return CommandResult.Accept("edge_evidence_selected");
            }
            if (puzzle.type == PuzzleTypes.ResponsibilityMatrix)
            {
                if (string.IsNullOrEmpty(c.targetId)) return CommandResult.Reject("item_target_required");
                SetReportEvidence(draft, c.targetId, ids);
                return CommandResult.Accept("report_evidence_selected");
            }
            draft.selectedEvidenceIds = ids;
            var result = CommandResult.Accept("evidence_selected");
            result.events.Add(new DomainEvent { kind = "evidence_selected", id = puzzle.id, message = string.Join(",", ids) });
            return result;
        }

        private static void SetEdgeProof(PuzzleDraftDto draft, string from, string to, string kind, string[] ids)
        {
            var list = new List<EdgeProofSelectionDto>(draft.edgeProofs ?? new EdgeProofSelectionDto[0]);
            bool replaced = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].from == from && list[i].to == to && list[i].kind == kind)
                {
                    list[i].evidenceIds = ids;
                    replaced = true;
                    break;
                }
            }
            if (!replaced)
                list.Add(new EdgeProofSelectionDto { from = from, to = to, kind = kind, evidenceIds = ids });
            draft.edgeProofs = list.ToArray();
        }

        private static void SetReportEvidence(PuzzleDraftDto draft, string item, string[] ids)
        {
            var list = new List<ReportEvidenceDto>(draft.reportEvidence ?? new ReportEvidenceDto[0]);
            bool replaced = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].item == item)
                {
                    list[i].evidenceIds = ids;
                    replaced = true;
                    break;
                }
            }
            if (!replaced) list.Add(new ReportEvidenceDto { item = item, evidenceIds = ids });
            draft.reportEvidence = list.ToArray();
        }

        private CommandResult DoPresent(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            if (string.IsNullOrEmpty(draft.selectedStatementId))
                return CommandResult.Reject("need_statement", _index.Ui("ui.statement_need_selection"));
            if (draft.selectedEvidenceIds == null || draft.selectedEvidenceIds.Length == 0)
                return CommandResult.Reject("need_evidence", _index.Ui("ui.statement_need_selection"));
            var evaluation = PuzzleEvaluator.Evaluate(puzzle, draft, _state, _index);
            var result = new CommandResult
            {
                status = evaluation.Status,
                reasonCode = evaluation.Solved ? "puzzle_solved" : (evaluation.Status == ResultStatus.Partial ? "partial" : "wrong"),
                message = evaluation.Message,
                Changed = evaluation.Solved
            };
            if (evaluation.Solved)
            {
                SolvePuzzle(puzzle);
                result.events.Add(new DomainEvent { kind = "puzzle_solved", id = puzzle.id, message = evaluation.Message });
                result.events.Add(PhaseEvent());
            }
            return result;
        }

        private CommandResult DoSelectChoice(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            var ids = new List<string>(draft.choiceIds ?? new string[0]);
            if (ids.Contains(c.targetId)) ids.Remove(c.targetId);
            else
            {
                if (puzzle.type == PuzzleTypes.DiffSelect)
                {
                    // diff keeps a small fixed set; allow up to two selections
                    if (ids.Count >= 2) ids.RemoveAt(0);
                }
                else ids.Clear();
                ids.Add(c.targetId);
            }
            draft.choiceIds = ids.ToArray();
            return CommandResult.Accept("choice_selected");
        }

        private CommandResult DoPlaceCard(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            if (string.IsNullOrEmpty(c.slot)) return CommandResult.Reject("missing_slot");
            var cards = new List<PlacedCardDto>(draft.placedCards ?? new PlacedCardDto[0]);
            if (c.slot == "order")
            {
                bool exists = false;
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i].slot == "order" && cards[i].value == c.value) exists = true;
                if (exists)
                {
                    for (int i = cards.Count - 1; i >= 0; i--)
                        if (cards[i].slot == "order" && cards[i].value == c.value) cards.RemoveAt(i);
                }
                else if (!string.IsNullOrEmpty(c.value))
                {
                    cards.Add(new PlacedCardDto { slot = "order", value = c.value });
                }
            }
            else if (puzzle.type == PuzzleTypes.ResponsibilityMatrix)
            {
                bool exists = false;
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i].slot == c.slot && cards[i].value == c.value) exists = true;
                if (exists)
                {
                    for (int i = cards.Count - 1; i >= 0; i--)
                        if (cards[i].slot == c.slot && cards[i].value == c.value) cards.RemoveAt(i);
                }
                else if (!string.IsNullOrEmpty(c.value))
                {
                    cards.Add(new PlacedCardDto { slot = c.slot, value = c.value });
                }
                SyncReportPlacements(draft, cards);
            }
            else
            {
                bool replaced = false;
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i].slot == c.slot)
                    {
                        cards[i].value = c.value;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced) cards.Add(new PlacedCardDto { slot = c.slot, value = c.value });
            }
            draft.placedCards = cards.ToArray();
            return CommandResult.Accept("card_placed");
        }

        private CommandResult DoRemoveCard(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            var cards = new List<PlacedCardDto>(draft.placedCards ?? new PlacedCardDto[0]);
            for (int i = cards.Count - 1; i >= 0; i--)
                if (cards[i].slot == c.slot && (string.IsNullOrEmpty(c.value) || cards[i].value == c.value))
                    cards.RemoveAt(i);
            draft.placedCards = cards.ToArray();
            if (puzzle.type == PuzzleTypes.ResponsibilityMatrix) SyncReportPlacements(draft, cards);
            return CommandResult.Accept("card_removed");
        }

        private static void SyncReportPlacements(PuzzleDraftDto draft, List<PlacedCardDto> cards)
        {
            var placements = new List<ReportPlacementDto>();
            for (int i = 0; i < cards.Count; i++)
                placements.Add(new ReportPlacementDto { section = cards[i].slot, item = cards[i].value });
            draft.reportPlacements = placements.ToArray();
        }

        private CommandResult DoConnectNodes(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            if (puzzle.type != PuzzleTypes.CausalGraph) return CommandResult.Reject("unsupported_command");
            string from = c.ids != null && c.ids.Length > 0 ? c.ids[0] : c.slot;
            string to = c.ids != null && c.ids.Length > 1 ? c.ids[1] : c.value;
            string kind = c.targetId;
            if (kind != "causal" && kind != "signal") return CommandResult.Reject("bad_edge_kind");
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return CommandResult.Reject("bad_edge");
            if (from == to) return CommandResult.Reject("self_edge", _index.Ui("ui.causal_self"));
            if (!HasNode(from) || !HasNode(to)) return CommandResult.Reject("unknown_node");
            if (WouldCycle(draft, from, to)) return CommandResult.Reject("cycle_edge", _index.Ui("ui.causal_cycle"));
            var edges = new List<EdgeDto>(draft.edges ?? new EdgeDto[0]);
            for (int i = edges.Count - 1; i >= 0; i--)
                if (edges[i].from == from && edges[i].to == to) edges.RemoveAt(i);
            edges.Add(new EdgeDto { from = from, to = to, kind = kind });
            draft.edges = edges.ToArray();
            var result = CommandResult.Accept("edge_connected");
            result.events.Add(new DomainEvent { kind = "edge_connected", id = from + "->" + to, message = kind });
            return result;
        }

        private bool HasNode(string nodeId)
        {
            var nodes = _index.GraphNodes();
            for (int i = 0; i < nodes.Count; i++) if (nodes[i].id == nodeId) return true;
            return false;
        }

        private bool WouldCycle(PuzzleDraftDto draft, string from, string to)
        {
            // after adding from->to, does a path from `to` back to `from` exist?
            var edges = new List<EdgeDto>(draft.edges ?? new EdgeDto[0]);
            var visited = new HashSet<string>();
            return Reaches(edges, to, from, visited);
        }

        private static bool Reaches(List<EdgeDto> edges, string current, string target, HashSet<string> visited)
        {
            if (current == target) return true;
            if (!visited.Add(current)) return false;
            for (int i = 0; i < edges.Count; i++)
                if (edges[i].from == current && Reaches(edges, edges[i].to, target, visited)) return true;
            return false;
        }

        private CommandResult DoRemoveEdge(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            if (puzzle.type != PuzzleTypes.CausalGraph) return CommandResult.Reject("unsupported_command");
            string from = c.ids != null && c.ids.Length > 0 ? c.ids[0] : c.slot;
            string to = c.ids != null && c.ids.Length > 1 ? c.ids[1] : c.value;
            var edges = new List<EdgeDto>(draft.edges ?? new EdgeDto[0]);
            int removed = 0;
            for (int i = edges.Count - 1; i >= 0; i--)
                if (edges[i].from == from && edges[i].to == to) { edges.RemoveAt(i); removed++; }
            draft.edges = edges.ToArray();
            if (removed == 0) return CommandResult.Reject("edge_not_found");
            return CommandResult.Accept("edge_removed");
        }

        private CommandResult DoSelectWindow(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            draft.comparisonWindowId = c.targetId ?? "";
            return CommandResult.Accept("window_selected");
        }

        private CommandResult DoRunCase(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            if (puzzle.type != PuzzleTypes.Counterfactual) return CommandResult.Reject("unsupported_command");
            bool allowed = false;
            if (puzzle.requiredCaseIds != null)
                for (int i = 0; i < puzzle.requiredCaseIds.Length; i++)
                    if (puzzle.requiredCaseIds[i] == c.targetId) allowed = true;
            if (!allowed) return CommandResult.Reject("unknown_case");
            var cases = new List<string>(draft.ranCaseIds ?? new string[0]);
            bool first = !cases.Contains(c.targetId);
            if (first) cases.Add(c.targetId);
            draft.ranCaseIds = cases.ToArray();
            var result = CommandResult.Accept(first ? "case_ran" : "case_already_ran");
            result.events.Add(new DomainEvent { kind = "counterfactual_ran", id = c.targetId, message = "" });
            return result;
        }

        private CommandResult DoSubmit(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, GameCommand c)
        {
            var evaluation = PuzzleEvaluator.Evaluate(puzzle, draft, _state, _index);
            var result = new CommandResult
            {
                status = evaluation.Status,
                reasonCode = evaluation.Solved ? "puzzle_solved" : (evaluation.Status == ResultStatus.Partial ? "partial" : "wrong"),
                message = evaluation.Message,
                Changed = evaluation.Solved
            };
            if (evaluation.Solved)
            {
                SolvePuzzle(puzzle);
                result.events.Add(new DomainEvent { kind = "puzzle_solved", id = puzzle.id, message = evaluation.Message });
                result.events.Add(PhaseEvent());
            }
            return result;
        }

        private void SolvePuzzle(PuzzleDefinitionDto puzzle)
        {
            if (!_state.IsPuzzleSolved(puzzle.id)) _state.SolvedPuzzles.Add(puzzle.id);
            var draft = _state.GetDraft(puzzle.id, false);
            if (draft != null) draft.submitted = true;
            _state.ActivePuzzleId = "";
            var scene = _index.Scene(_state.CurrentSceneId);
            if (scene != null) ContinueAfterChoices(scene);
        }

        // ---------------- grants ----------------

        private void ApplyGrants(SceneDefinitionDto scene, string trigger)
        {
            if (scene.grants == null) return;
            for (int i = 0; i < scene.grants.Length; i++)
            {
                var grant = scene.grants[i];
                if (grant == null || grant.trigger != trigger) continue;
                if (trigger == "dialogue") continue;
                string effectId = "grant:" + scene.id + ":" + grant.evidenceId;
                if (!MarkEffect(effectId)) continue;
                _state.AddOwned(grant.evidenceId);
            }
        }

        private void ApplyLineGrants(string lineId)
        {
            var scene = _index.Scene(_state.CurrentSceneId);
            if (scene?.grants == null) return;
            for (int i = 0; i < scene.grants.Length; i++)
            {
                var grant = scene.grants[i];
                if (grant == null || grant.trigger != "dialogue" || grant.atDialogueId != lineId) continue;
                string effectId = "grant:" + scene.id + ":" + grant.evidenceId;
                if (!MarkEffect(effectId)) continue;
                _state.AddOwned(grant.evidenceId);
            }
        }

        private bool MarkEffect(string effectId)
        {
            if (_processedEffects.Contains(effectId)) return false;
            _processedEffects.Add(effectId);
            _state.MarkApplied(effectId);
            return true;
        }
    }
}
