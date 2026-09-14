import type { PuzzleDefinitionDto, PuzzleDraftDto, SceneDefinitionDto } from '../content/dto'
import type { ContentIndex } from '../content/contentIndex'
import {
  allConditionsMet,
  remainingRequiredChildren,
} from '../conditions/conditionEvaluator'
import { evaluatePuzzle, PuzzleTypes } from '../puzzles/puzzleEvaluator'
import { GameState, Phases } from '../state/gameState'
import {
  accept,
  CommandKinds,
  Origins,
  reject,
  ResultStatus,
  type CommandResult,
  type DomainEvent,
  type GameCommand,
} from './commandTypes'

export interface DispatcherHooks {
  saveRequested?: (slot: string, manual: boolean) => boolean
  loadRequested?: (slot: string) => boolean
}

/**
 * 명령 검증·상태 전이의 유일한 통로. (C# ANL.Core.Commands.CommandDispatcher 이식)
 */
export class CommandDispatcher {
  readonly index: ContentIndex
  readonly state: GameState
  hooks: DispatcherHooks = {}

  private readonly processedCommandIds = new Set<string>()
  private readonly processedEffects = new Set<string>()

  constructor(index: ContentIndex, state: GameState) {
    this.index = index
    this.state = state
    for (const id of state.appliedEffectIds) this.processedEffects.add(id)
  }

  execute(command: GameCommand | null | undefined): CommandResult {
    if (!command) return reject('null_command')
    if (!command.type) return reject('missing_type')
    if (!command.commandId) return reject('missing_command_id')
    if (this.processedCommandIds.has(command.commandId) || command.sequence <= this.state.lastCommandSequence) {
      return reject('duplicate_command')
    }
    if (originOf(command) !== Origins.Fixture && command.sceneId) {
      if (this.state.currentSceneId !== command.sceneId && command.type !== CommandKinds.StartEpisode) {
        return reject('wrong_scene')
      }
    }

    const result = this.dispatch(command)
    if (result.status !== ResultStatus.Rejected) {
      this.processedCommandIds.add(command.commandId)
      this.state.lastCommandSequence = command.sequence
    } else if (result.reasonCode === 'duplicate_command' || result.reasonCode === 'wrong_scene') {
      // sequence not consumed
    } else {
      this.processedCommandIds.add(command.commandId)
      this.state.lastCommandSequence = command.sequence
    }
    return result
  }

  hasUnsolvedInteraction(): { has: boolean; puzzleId: string } {
    const scene = this.index.scene(this.state.currentSceneId)
    if (!scene?.interactionIds) return { has: false, puzzleId: '' }
    for (const puzzleId of scene.interactionIds) {
      if (!this.state.isPuzzleSolved(puzzleId)) return { has: true, puzzleId }
    }
    return { has: false, puzzleId: '' }
  }

  canContinue(): { can: boolean; reason: string } {
    switch (this.state.phase) {
      case Phases.PreDialogue:
      case Phases.PostDialogue:
        return { can: false, reason: 'dialogue_not_finished' }
      case Phases.Interaction:
        return { can: false, reason: 'puzzle_active' }
      case Phases.BranchChoice:
        return { can: false, reason: 'choice_pending' }
      case Phases.ReadCheck:
        return this.requiredReadSatisfied()
      case Phases.ReadyToExit:
      case Phases.Completed:
        return { can: this.transitionAvailable().available, reason: this.transitionAvailable().reason }
      case Phases.HubActive:
        return { can: this.transitionAvailable().available, reason: this.transitionAvailable().reason }
      default:
        return { can: false, reason: 'not_ready' }
    }
  }

  private dispatch(command: GameCommand): CommandResult {
    switch (command.type) {
      case CommandKinds.StartEpisode:
        return this.doStartEpisode()
      case CommandKinds.AdvanceDialogue:
        return this.doAdvanceDialogue()
      case CommandKinds.ContinueScene:
        return this.doContinueScene()
      case CommandKinds.ReturnToHub:
        return this.doReturnToHub(command)
      case CommandKinds.ChooseBranch:
      case CommandKinds.ChooseTopic:
        return this.doChooseBranch(command)
      case CommandKinds.OpenEvidence:
      case CommandKinds.ReadEvidence:
        return this.doOpenEvidence(command)
      case CommandKinds.OpenHint:
        return this.doOpenHint(command)
      case CommandKinds.SaveSlot: {
        this.hooks.saveRequested?.(command.targetId ?? 'slot_01', (command.numeric ?? 0) > 0)
        return accept('save_requested')
      }
      case CommandKinds.LoadSlot:
        if (this.hooks.loadRequested?.(command.targetId ?? '')) return accept('load_requested')
        return reject('load_failed')
      case CommandKinds.GoToScene:
        return this.doGoToScene(command)
      default:
        break
    }

    if (this.state.phase !== Phases.Interaction) return reject('not_interaction')
    const puzzle = this.index.puzzle(this.state.activePuzzleId)
    if (!puzzle) return reject('no_active_puzzle')
    const draft = this.state.getDraft(puzzle.id, true)

    switch (command.type) {
      case CommandKinds.InspectRegion:
        return this.doInspectRegion(puzzle, draft, command)
      case CommandKinds.SelectStatement:
        return this.doSelectStatement(puzzle, draft, command)
      case CommandKinds.PressStatement:
        return this.doPressStatement(command)
      case CommandKinds.SelectEvidence:
        return this.doSelectEvidence(puzzle, draft, command)
      case CommandKinds.PresentEvidence:
        return this.doSubmit(puzzle, draft)
      case CommandKinds.SelectChoice:
        return this.doSelectChoice(puzzle, draft, command)
      case CommandKinds.PlaceCard:
        return this.doPlaceCard(puzzle, draft, command)
      case CommandKinds.RemoveCard:
        return this.doRemoveCard(puzzle, draft, command)
      case CommandKinds.ConnectNodes:
        return this.doConnectNodes(puzzle, draft, command)
      case CommandKinds.RemoveEdge:
        return this.doRemoveEdge(puzzle, draft, command)
      case CommandKinds.SelectComparisonWindow:
        return this.doSelectWindow(draft, command)
      case CommandKinds.RunCounterfactualCase:
        return this.doRunCase(puzzle, draft, command)
      case CommandKinds.SubmitPuzzle:
        return this.doSubmit(puzzle, draft)
      default:
        return reject('unsupported_command', command.type)
    }
  }

  // ---------------- scene flow ----------------

  private doStartEpisode(): CommandResult {
    const episode = this.index.episode
    if (!episode) return reject('missing_episode')
    this.enterScene(episode.startSceneId)
    const result = accept('episode_started')
    result.events.push({ kind: 'scene_entered', id: episode.startSceneId, message: '' })
    return result
  }

  private doGoToScene(command: GameCommand): CommandResult {
    if (!command.targetId || !this.index.hasScene(command.targetId)) return reject('unknown_scene', command.targetId ?? '')
    this.enterScene(command.targetId)
    const result = accept('scene_entered_fixture')
    result.events.push({ kind: 'scene_entered', id: command.targetId, message: 'fixture' })
    return result
  }

  enterScene(sceneId: string): void {
    const scene = this.index.scene(sceneId)
    this.state.currentSceneId = sceneId
    this.state.activePuzzleId = ''
    if (!this.state.isSceneVisited(sceneId)) this.state.visitedScenes.push(sceneId)
    if (!scene) {
      this.state.phase = Phases.Completed
      return
    }
    this.state.phase = Phases.Entering

    if (!this.state.isPreDone(sceneId)) {
      this.applyGrants(scene, 'scene_start')
      if (scene.preDialogueIds && scene.preDialogueIds.length > 0) {
        this.state.phase = Phases.PreDialogue
        this.state.dialogueCursor = 0
        this.applyLineGrants(scene.preDialogueIds[0] ?? '')
      } else {
        this.completePreDialogue(scene)
      }
      return
    }

    if (scene.screenType === 'hub') {
      this.state.phase = Phases.HubActive
      return
    }
    const pending = this.hasUnsolvedInteraction()
    if (pending.has) {
      this.startPuzzle(pending.puzzleId)
      return
    }
    if (
      !this.state.isPostDone(sceneId) &&
      scene.postDialogueIds &&
      scene.postDialogueIds.length > 0 &&
      !this.state.isSceneCompleted(sceneId)
    ) {
      this.applyGrants(scene, 'post_start')
      this.state.phase = Phases.PostDialogue
      this.state.dialogueCursor = 0
      this.applyLineGrants(scene.postDialogueIds[0] ?? '')
      return
    }
    if (this.requiredReadOutstanding(scene)) {
      this.state.phase = Phases.ReadCheck
      return
    }
    this.state.phase = this.state.isSceneCompleted(sceneId) ? Phases.Completed : Phases.ReadyToExit
  }

  private doAdvanceDialogue(): CommandResult {
    const scene = this.index.scene(this.state.currentSceneId)
    if (!scene) return reject('unknown_scene')
    if (this.state.phase === Phases.PreDialogue) {
      this.state.dialogueCursor++
      if (!scene.preDialogueIds || this.state.dialogueCursor >= scene.preDialogueIds.length) {
        this.completePreDialogue(scene)
        const result = accept('pre_dialogue_complete')
        result.events.push(this.phaseEvent())
        return result
      }
      this.applyLineGrants(scene.preDialogueIds[this.state.dialogueCursor] ?? '')
      return accept('dialogue_advanced')
    }
    if (this.state.phase === Phases.PostDialogue) {
      this.state.dialogueCursor++
      if (!scene.postDialogueIds || this.state.dialogueCursor >= scene.postDialogueIds.length) {
        this.completePostDialogue(scene)
        const result = accept('post_dialogue_complete')
        result.events.push({ kind: 'scene_completed', id: scene.id, message: '' })
        result.events.push(this.phaseEvent())
        return result
      }
      this.applyLineGrants(scene.postDialogueIds[this.state.dialogueCursor] ?? '')
      return accept('dialogue_advanced')
    }
    return reject('not_in_dialogue')
  }

  private phaseEvent(): DomainEvent {
    return { kind: 'phase', id: this.state.phase, message: this.state.currentSceneId }
  }

  private completePreDialogue(scene: SceneDefinitionDto): void {
    if (!this.state.isPreDone(scene.id)) this.state.preDialogueDone.push(scene.id)
    this.applyGrants(scene, 'interaction_start')

    const hasBranches = !!scene.branchGroupIds && scene.branchGroupIds.length > 0
    if (hasBranches) {
      this.state.phase = Phases.BranchChoice
      return
    }
    this.continueAfterChoices(scene)
  }

  continueAfterChoices(scene: SceneDefinitionDto): void {
    const pending = this.hasUnsolvedInteraction()
    if (pending.has) {
      this.startPuzzle(pending.puzzleId)
      return
    }
    if (this.requiredReadOutstanding(scene)) {
      this.state.phase = Phases.ReadCheck
      return
    }
    if (!this.state.isPostDone(scene.id) && scene.postDialogueIds && scene.postDialogueIds.length > 0) {
      this.applyGrants(scene, 'post_start')
      this.state.phase = Phases.PostDialogue
      this.state.dialogueCursor = 0
      this.applyLineGrants(scene.postDialogueIds[0] ?? '')
      return
    }
    this.finalizeScene(scene)
  }

  private finalizeScene(scene: SceneDefinitionDto): void {
    if (!this.state.isPostDone(scene.id)) this.state.postDialogueDone.push(scene.id)
    if (!this.state.isSceneCompleted(scene.id)) this.state.completedScenes.push(scene.id)
    this.state.phase = Phases.ReadyToExit
    this.state.activePuzzleId = ''
  }

  private completePostDialogue(scene: SceneDefinitionDto): void {
    if (!this.state.isPostDone(scene.id)) this.state.postDialogueDone.push(scene.id)
    if (this.requiredReadOutstanding(scene)) {
      this.state.phase = Phases.ReadCheck
      return
    }
    this.finalizeScene(scene)
  }

  private startPuzzle(puzzleId: string): void {
    this.state.activePuzzleId = puzzleId
    this.state.phase = Phases.Interaction
    this.state.getDraft(puzzleId, true)
  }

  private requiredReadOutstanding(scene: SceneDefinitionDto): boolean {
    if (!scene.requiredReadEvidenceIds) return false
    for (const evidenceId of scene.requiredReadEvidenceIds) {
      if (!this.state.hasRead(evidenceId)) return true
    }
    return false
  }

  private requiredReadSatisfied(): { can: boolean; reason: string } {
    const scene = this.index.scene(this.state.currentSceneId)
    if (scene?.requiredReadEvidenceIds) {
      for (const evidenceId of scene.requiredReadEvidenceIds) {
        if (!this.state.hasRead(evidenceId)) {
          return { can: false, reason: `need_read:${evidenceId}` }
        }
      }
    }
    return { can: true, reason: 'read_satisfied' }
  }

  private doContinueScene(): CommandResult {
    switch (this.state.phase) {
      case Phases.BranchChoice: {
        const scene = this.index.scene(this.state.currentSceneId)
        if (!scene) return reject('unknown_scene')
        if (!this.allAdvancingGroupsResolved(scene)) return reject('advancing_choice_required')
        this.continueAfterChoices(scene)
        return accept('choices_done', this.phaseMessage())
      }
      case Phases.ReadCheck: {
        const read = this.requiredReadSatisfied()
        if (!read.can) return reject(read.reason)
        const scene = this.index.scene(this.state.currentSceneId)
        if (!scene) return reject('unknown_scene')
        this.continueAfterChoices(scene)
        return accept('read_satisfied', this.phaseMessage())
      }
      case Phases.ReadyToExit:
      case Phases.Completed:
        return this.doTransition()
      case Phases.HubActive:
        return this.doTransition()
      default:
        return reject('not_ready', this.state.phase)
    }
  }

  private phaseMessage(): string {
    return `${this.state.currentSceneId}:${this.state.phase}`
  }

  private allAdvancingGroupsResolved(scene: SceneDefinitionDto | null): boolean {
    if (!scene?.advancingGroupIds || scene.advancingGroupIds.length === 0) return true
    for (const groupId of scene.advancingGroupIds) {
      const group = this.index.branchGroup(groupId)
      const resolved = group.some((branch) => this.state.isTopicVisited(branch.id))
      if (!resolved) return false
    }
    return true
  }

  transitionAvailable(): { available: boolean; target: string; reason: string } {
    const scene = this.index.scene(this.state.currentSceneId)
    if (!scene?.transitions) return { available: false, target: '', reason: 'no_transition' }
    const hubLike = scene.screenType === 'hub' || (!!scene.childSceneIds && scene.childSceneIds.length > 0)
    for (const transition of scene.transitions) {
      if (allConditionsMet(transition.conditions, this.state, this.index)) {
        return { available: true, target: transition.target, reason: '' }
      }
    }
    if (hubLike) {
      const remaining = remainingRequiredChildren(this.state.currentSceneId, this.state, this.index)
      const reason = remaining.length > 0 ? `remaining:${remaining.join(',')}` : 'no_transition'
      return { available: false, target: '', reason }
    }
    return { available: false, target: '', reason: 'conditions_not_met' }
  }

  private doTransition(): CommandResult {
    const transition = this.transitionAvailable()
    if (!transition.available) {
      const result = reject(transition.reason)
      if (transition.reason.startsWith('remaining:')) {
        result.message = this.index.ui('ui.hub_all_required_done')
      }
      return result
    }
    const previous = this.state.currentSceneId
    this.enterScene(transition.target)
    const result = accept('scene_transition')
    result.events.push({ kind: 'scene_entered', id: transition.target, message: previous })
    result.events.push(this.phaseEvent())
    return result
  }

  private doReturnToHub(command: GameCommand): CommandResult {
    let parent = ''
    for (const [sceneId, scene] of this.index.scenes) {
      const children = scene.childSceneIds
      if (!children) continue
      if (children.includes(this.state.currentSceneId)) {
        parent = sceneId
        break
      }
    }
    if (!parent) {
      if (command.targetId) parent = command.targetId
      else return reject('no_parent_hub')
    }
    this.enterScene(parent)
    const result = accept('returned_to_hub')
    result.events.push({ kind: 'scene_entered', id: parent, message: this.state.currentSceneId })
    return result
  }

  // ---------------- branches ----------------

  private doChooseBranch(command: GameCommand): CommandResult {
    if (this.state.phase !== Phases.BranchChoice) return reject('not_choosing')
    const branch = command.targetId ? this.index.branch(command.targetId) : null
    if (!branch || branch.sceneId !== this.state.currentSceneId) return reject('unknown_branch')
    if (this.state.isTopicVisited(branch.id)) return reject('duplicate_choice')
    if (!allConditionsMet(branch.condition, this.state, this.index)) return reject('choice_locked')

    this.applyBranchEffects(branch)

    const result = accept(branch.advances ? 'branch_advanced' : 'branch_retry')
    if (branch.dialogue && branch.dialogue.length > 0) {
      result.events.push({ kind: 'branch_dialogue', id: branch.id, message: '', lines: branch.dialogue })
    }
    if (branch.feedback) {
      result.message = branch.feedback
      result.events.push({ kind: 'feedback', id: branch.id, message: branch.feedback })
    }
    if (branch.advances) {
      const scene = this.index.scene(this.state.currentSceneId)
      if (scene && this.allAdvancingGroupsResolved(scene)) {
        this.continueAfterChoices(scene)
        result.events.push(this.phaseEvent())
      } else {
        this.state.phase = Phases.BranchChoice
      }
    }
    return result
  }

  private applyBranchEffects(branch: import('../content/dto').BranchDefinitionDto): void {
    this.state.visitedTopics.push(branch.id)
    if (!branch.effects) return
    for (const effect of branch.effects) {
      if (!effect) continue
      switch (effect.kind) {
        case 'SetBranch':
          this.state.setBranchValue(effect.key, effect.value)
          break
        case 'GrantEvidence':
          this.state.addOwned(effect.id)
          break
        case 'Feedback':
          break
        default:
          break
      }
    }
  }

  // ---------------- evidence / hints ----------------

  private doOpenEvidence(command: GameCommand): CommandResult {
    const evidence = this.index.evidenceDef(command.targetId)
    if (!evidence) return reject('unknown_evidence')
    if (!this.state.owns(evidence.id)) return reject('evidence_not_owned')
    const newlyRead = this.state.markRead(evidence.id)
    const result = accept(newlyRead ? 'evidence_read' : 'evidence_already_read')
    result.events.push({ kind: 'evidence_opened', id: evidence.id, message: evidence.title })
    return result
  }

  private doOpenHint(command: GameCommand): CommandResult {
    const scope = command.targetId || this.state.currentSceneId
    const usage = this.state.getHintUsage(scope, true)
    const level = Math.trunc(command.numeric ?? 0)
    if (level > usage.maxLevel) usage.maxLevel = level
    usage.openCount++
    if (originOf(command) === Origins.TestAssist) usage.usedTestAssist = true
    return accept('hint_open')
  }

  // ---------------- puzzle commands ----------------

  private doInspectRegion(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    if (puzzle.type !== PuzzleTypes.HotspotSet) return reject('unsupported_command')
    const region = puzzle.regions?.find((item) => item.regionId === command.targetId) ?? null
    if (!region) return reject('unknown_region')

    const cards = [...(draft.placedCards ?? [])]
    const already = cards.some((card) => card.slot === 'region' && card.value === region.regionId)
    let granted = false
    if (!already) cards.push({ slot: 'region', value: region.regionId })
    draft.placedCards = cards
    if (region.evidenceId && this.state.addOwned(region.evidenceId)) granted = true

    const evaluation = evaluatePuzzle(puzzle, draft, this.state, this.index)
    const result = accept(already ? 'region_already' : 'region_inspected')
    result.events.push({ kind: 'region_inspected', id: region.regionId, message: region.evidenceId })
    if (granted) result.events.push({ kind: 'evidence_granted', id: region.evidenceId, message: '' })
    if (evaluation.status === ResultStatus.Solved) {
      this.solvePuzzle(puzzle)
      result.status = ResultStatus.Solved
      result.reasonCode = 'puzzle_solved'
      result.message = evaluation.message
      result.events.push({ kind: 'puzzle_solved', id: puzzle.id, message: evaluation.message })
      result.events.push(this.phaseEvent())
    } else {
      result.message = already ? this.index.ui('ui.region_already') : ''
    }
    return result
  }

  private doSelectStatement(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    const statement = this.index.testimony(command.targetId)
    if (!statement || statement.sceneId !== puzzle.sceneId) return reject('unknown_statement')
    draft.selectedStatementId = statement.id
    const result = accept('statement_selected')
    result.events.push({ kind: 'statement_selected', id: statement.id, message: statement.text })
    return result
  }

  private doPressStatement(command: GameCommand): CommandResult {
    const statement = this.index.testimony(command.targetId)
    if (!statement) return reject('unknown_statement')
    const result = accept('statement_pressed')
    if (statement.pressResponse && statement.pressResponse.length > 0) {
      result.events.push({ kind: 'branch_dialogue', id: statement.id, message: '', lines: statement.pressResponse })
    }
    return result
  }

  private doSelectEvidence(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    const ids = command.ids ?? []
    if (ids.length > 3) return reject('too_many_evidence')
    for (const id of ids) {
      if (!this.index.hasEvidence(id)) return reject('unknown_evidence', id)
      if (!this.state.owns(id)) return reject('evidence_not_owned', id)
    }
    if (puzzle.type === PuzzleTypes.CausalGraph) {
      const parts = (command.targetId ?? '').split('|')
      if (parts.length !== 3) return reject('edge_target_required')
      setEdgeProof(draft, parts[0] ?? '', parts[1] ?? '', parts[2] ?? '', ids)
      return accept('edge_evidence_selected')
    }
    if (puzzle.type === PuzzleTypes.ResponsibilityMatrix) {
      if (!command.targetId) return reject('item_target_required')
      setReportEvidence(draft, command.targetId, ids)
      return accept('report_evidence_selected')
    }
    draft.selectedEvidenceIds = ids
    const result = accept('evidence_selected')
    result.events.push({ kind: 'evidence_selected', id: puzzle.id, message: ids.join(',') })
    return result
  }

  private doSubmit(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): CommandResult {
    const evaluation = evaluatePuzzle(puzzle, draft, this.state, this.index)
    const result: CommandResult = {
      status: evaluation.status,
      reasonCode:
        evaluation.status === ResultStatus.Solved
          ? 'puzzle_solved'
          : evaluation.status === ResultStatus.Partial
            ? 'partial'
            : 'wrong',
      message: evaluation.message,
      events: [],
      changed: evaluation.status === ResultStatus.Solved,
    }
    if (evaluation.status === ResultStatus.Solved) {
      this.solvePuzzle(puzzle)
      result.events.push({ kind: 'puzzle_solved', id: puzzle.id, message: evaluation.message })
      result.events.push(this.phaseEvent())
    }
    return result
  }

  private doSelectChoice(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    const ids = [...(draft.choiceIds ?? [])]
    const targetId = command.targetId ?? ''
    const index = ids.indexOf(targetId)
    if (index >= 0) {
      ids.splice(index, 1)
    } else {
      if (puzzle.type === PuzzleTypes.DiffSelect) {
        if (ids.length >= 2) ids.shift()
      } else {
        ids.length = 0
      }
      ids.push(targetId)
    }
    draft.choiceIds = ids
    return accept('choice_selected')
  }

  private doPlaceCard(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    const slot = command.slot ?? ''
    const value = command.value ?? ''
    if (!slot) return reject('missing_slot')
    const cards = [...(draft.placedCards ?? [])]
    if (slot === 'order') {
      const exists = cards.some((card) => card.slot === 'order' && card.value === value)
      if (exists) {
        for (let i = cards.length - 1; i >= 0; i--) {
          if (cards[i]?.slot === 'order' && cards[i]?.value === value) cards.splice(i, 1)
        }
      } else if (value) {
        cards.push({ slot: 'order', value })
      }
    } else if (puzzle.type === PuzzleTypes.ResponsibilityMatrix) {
      const exists = cards.some((card) => card.slot === slot && card.value === value)
      if (exists) {
        for (let i = cards.length - 1; i >= 0; i--) {
          if (cards[i]?.slot === slot && cards[i]?.value === value) cards.splice(i, 1)
        }
      } else if (value) {
        cards.push({ slot, value })
      }
      syncReportPlacements(draft, cards)
    } else {
      const existing = cards.find((card) => card.slot === slot)
      if (existing) existing.value = value
      else cards.push({ slot, value })
    }
    draft.placedCards = cards
    return accept('card_placed')
  }

  private doRemoveCard(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    const cards = [...(draft.placedCards ?? [])]
    const slot = command.slot ?? ''
    const value = command.value ?? ''
    for (let i = cards.length - 1; i >= 0; i--) {
      const card = cards[i]
      if (card && card.slot === slot && (!value || card.value === value)) cards.splice(i, 1)
    }
    draft.placedCards = cards
    if (puzzle.type === PuzzleTypes.ResponsibilityMatrix) syncReportPlacements(draft, cards)
    return accept('card_removed')
  }

  private doConnectNodes(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    if (puzzle.type !== PuzzleTypes.CausalGraph) return reject('unsupported_command')
    const from = command.ids?.[0] ?? command.slot ?? ''
    const to = command.ids?.[1] ?? command.value ?? ''
    const kind = command.targetId ?? ''
    if (kind !== 'causal' && kind !== 'signal') return reject('bad_edge_kind')
    if (!from || !to) return reject('bad_edge')
    if (from === to) return reject('self_edge', this.index.ui('ui.causal_self'))
    if (!this.hasNode(from) || !this.hasNode(to)) return reject('unknown_node')
    if (this.wouldCycle(draft, from, to)) return reject('cycle_edge', this.index.ui('ui.causal_cycle'))
    const edges = [...(draft.edges ?? [])]
    for (let i = edges.length - 1; i >= 0; i--) {
      if (edges[i]?.from === from && edges[i]?.to === to) edges.splice(i, 1)
    }
    edges.push({ from, to, kind })
    draft.edges = edges
    const result = accept('edge_connected')
    result.events.push({ kind: 'edge_connected', id: `${from}->${to}`, message: kind })
    return result
  }

  private hasNode(nodeId: string): boolean {
    return this.index.graphNodes().some((node) => node.id === nodeId)
  }

  private wouldCycle(draft: PuzzleDraftDto, from: string, to: string): boolean {
    return reaches(draft.edges ?? [], to, from, new Set<string>())
  }

  private doRemoveEdge(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    if (puzzle.type !== PuzzleTypes.CausalGraph) return reject('unsupported_command')
    const from = command.ids?.[0] ?? command.slot ?? ''
    const to = command.ids?.[1] ?? command.value ?? ''
    const edges = [...(draft.edges ?? [])]
    let removed = 0
    for (let i = edges.length - 1; i >= 0; i--) {
      if (edges[i]?.from === from && edges[i]?.to === to) {
        edges.splice(i, 1)
        removed++
      }
    }
    draft.edges = edges
    if (removed === 0) return reject('edge_not_found')
    return accept('edge_removed')
  }

  private doSelectWindow(draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    draft.comparisonWindowId = command.targetId ?? ''
    return accept('window_selected')
  }

  private doRunCase(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, command: GameCommand): CommandResult {
    if (puzzle.type !== PuzzleTypes.Counterfactual) return reject('unsupported_command')
    const allowed = puzzle.requiredCaseIds?.includes(command.targetId ?? '') ?? false
    if (!allowed) return reject('unknown_case')
    const cases = [...(draft.ranCaseIds ?? [])]
    const first = !cases.includes(command.targetId ?? '')
    if (first) cases.push(command.targetId ?? '')
    draft.ranCaseIds = cases
    const result = accept(first ? 'case_ran' : 'case_already_ran')
    result.events.push({ kind: 'counterfactual_ran', id: command.targetId ?? '', message: '' })
    return result
  }

  private solvePuzzle(puzzle: PuzzleDefinitionDto): void {
    if (!this.state.isPuzzleSolved(puzzle.id)) this.state.solvedPuzzles.push(puzzle.id)
    const draft = this.state.getDraft(puzzle.id, false)
    if (draft) draft.submitted = true
    this.state.activePuzzleId = ''
    const scene = this.index.scene(this.state.currentSceneId)
    if (scene) this.continueAfterChoices(scene)
  }

  // ---------------- grants ----------------

  private applyGrants(scene: SceneDefinitionDto, trigger: string): void {
    if (!scene.grants) return
    for (const grant of scene.grants) {
      if (!grant || grant.trigger !== trigger) continue
      if (trigger === 'dialogue') continue
      const effectId = `grant:${scene.id}:${grant.evidenceId}`
      if (!this.markEffect(effectId)) continue
      this.state.addOwned(grant.evidenceId)
    }
  }

  private applyLineGrants(lineId: string): void {
    const scene = this.index.scene(this.state.currentSceneId)
    if (!scene?.grants) return
    for (const grant of scene.grants) {
      if (!grant || grant.trigger !== 'dialogue' || grant.atDialogueId !== lineId) continue
      const effectId = `grant:${scene.id}:${grant.evidenceId}`
      if (!this.markEffect(effectId)) continue
      this.state.addOwned(grant.evidenceId)
    }
  }

  private markEffect(effectId: string): boolean {
    if (this.processedEffects.has(effectId)) return false
    this.processedEffects.add(effectId)
    this.state.markApplied(effectId)
    return true
  }
}

function originOf(command: GameCommand): string {
  return command.origin || Origins.User
}

function setEdgeProof(draft: PuzzleDraftDto, from: string, to: string, kind: string, ids: string[]): void {
  const list = [...(draft.edgeProofs ?? [])]
  const existing = list.find((proof) => proof.from === from && proof.to === to && proof.kind === kind)
  if (existing) existing.evidenceIds = ids
  else list.push({ from, to, kind, evidenceIds: ids })
  draft.edgeProofs = list
}

function setReportEvidence(draft: PuzzleDraftDto, item: string, ids: string[]): void {
  const list = [...(draft.reportEvidence ?? [])]
  const existing = list.find((entry) => entry.item === item)
  if (existing) existing.evidenceIds = ids
  else list.push({ item, evidenceIds: ids })
  draft.reportEvidence = list
}

function syncReportPlacements(draft: PuzzleDraftDto, cards: { slot: string; value: string }[]): void {
  draft.reportPlacements = cards.map((card) => ({ section: card.slot, item: card.value }))
}

function reaches(
  edges: { from: string; to: string }[],
  current: string,
  target: string,
  visited: Set<string>,
): boolean {
  if (current === target) return true
  if (visited.has(current)) return false
  visited.add(current)
  for (const edge of edges) {
    if (edge.from === current && reaches(edges, edge.to, target, visited)) return true
  }
  return false
}
