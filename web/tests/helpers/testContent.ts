import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { CommandDispatcher } from '../../src/core/commands/commandDispatcher'
import { CommandKinds, Origins, ResultStatus, type CommandResult, type GameCommand } from '../../src/core/commands/commandTypes'
import { planTestAssist } from '../../src/core/commands/testAssistPlanner'
import { allConditionsMet } from '../../src/core/conditions/conditionEvaluator'
import { ContentIndex } from '../../src/core/content/contentIndex'
import type {
  BranchFileDto,
  DialogueFileDto,
  EpisodeFileDto,
  EvidenceFileDto,
  EvidenceSetDto,
  GraphFileDto,
  HintFileDto,
  MatrixFileDto,
  PuzzleDefinitionDto,
  PuzzleFileDto,
  SceneFileDto,
  TestimonyFileDto,
  UiStringFileDto,
} from '../../src/core/content/dto'
import { GameState, Phases } from '../../src/core/state/gameState'

/** Vitest는 web/에서 실행된다. 정본 콘텐츠는 저장소 루트의 content/ep01이다. */
const ROOT = join(process.cwd(), '..')

export const CONTENT_DIR = join(ROOT, 'content', 'ep01')
export const MANIFEST_PATH = join(ROOT, 'handoff', 'art', 'manifest.lock.json')

let commandCounter = 0

function readJson<T>(path: string): T {
  return JSON.parse(readFileSync(path, 'utf8')) as T
}

export function loadTestContent(): ContentIndex {
  const episodeFile = readJson<EpisodeFileDto>(join(CONTENT_DIR, 'episode.json'))
  const scenesFile = readJson<SceneFileDto>(join(CONTENT_DIR, 'scenes.json'))
  const dialoguesFile = readJson<DialogueFileDto>(join(CONTENT_DIR, 'dialogues.ko.json'))
  const evidenceFile = readJson<EvidenceFileDto>(join(CONTENT_DIR, 'evidence.json'))
  const testimoniesFile = readJson<TestimonyFileDto>(join(CONTENT_DIR, 'testimonies.json'))
  const branchesFile = readJson<BranchFileDto>(join(CONTENT_DIR, 'branches.json'))
  const puzzlesFile = readJson<PuzzleFileDto>(join(CONTENT_DIR, 'puzzles.json'))
  const hintsFile = readJson<HintFileDto>(join(CONTENT_DIR, 'hints.ko.json'))
  const uiFile = readJson<UiStringFileDto>(join(CONTENT_DIR, 'ui.ko.json'))
  const graphFile = readJson<GraphFileDto>(join(CONTENT_DIR, 'graph.json'))
  const matrixFile = readJson<MatrixFileDto>(join(CONTENT_DIR, 'matrix.json'))

  return new ContentIndex({
    episode: episodeFile.episode,
    scenes: scenesFile.items,
    dialogues: dialoguesFile.items,
    evidence: evidenceFile.items,
    testimonies: testimoniesFile.items,
    branches: branchesFile.items,
    puzzles: puzzlesFile.items,
    hints: hintsFile.items,
    uiStrings: uiFile.items,
    graph: graphFile,
    matrix: matrixFile,
  })
}

export function testAssetIds(): string[] {
  const manifest = readJson<{ assets: { id: string }[] }>(MANIFEST_PATH)
  return manifest.assets.map((asset) => asset.id)
}

export function freshState(): GameState {
  return new GameState()
}

export function freshDispatcher(index: ContentIndex): { dispatcher: CommandDispatcher; state: GameState } {
  const state = freshState()
  const dispatcher = new CommandDispatcher(index, state)
  const result = send(dispatcher, CommandKinds.StartEpisode)
  if (result.status === ResultStatus.Rejected) {
    throw new Error(`StartEpisode rejected: ${result.reasonCode} ${result.message}`)
  }
  return { dispatcher, state }
}

export interface SendOptions {
  targetId?: string
  ids?: string[]
  slot?: string
  value?: string
  numeric?: number
  origin?: string
}

export function send(dispatcher: CommandDispatcher, type: string, options: SendOptions = {}): CommandResult {
  commandCounter++
  const command: GameCommand = {
    commandId: `testcmd_${commandCounter}`,
    sequence: dispatcher.state.lastCommandSequence + 1,
    sceneId: dispatcher.state.currentSceneId,
    origin: options.origin ?? Origins.TestAssist,
    type,
    ...(options.targetId !== undefined ? { targetId: options.targetId } : {}),
    ...(options.ids !== undefined ? { ids: options.ids } : {}),
    ...(options.slot !== undefined ? { slot: options.slot } : {}),
    ...(options.value !== undefined ? { value: options.value } : {}),
    ...(options.numeric !== undefined ? { numeric: options.numeric } : {}),
  }
  return dispatcher.execute(command)
}

export function expectAccepted(result: CommandResult, what = 'command'): void {
  if (result.status === ResultStatus.Rejected) {
    throw new Error(`${what} rejected: ${result.reasonCode} ${result.message}`)
  }
}

export function firstEvidenceSet(sets: EvidenceSetDto[] | null | undefined): string[] | null {
  if (!sets) return null
  for (const set of sets) {
    if (set?.requiredIds && set.requiredIds.length > 0) return set.requiredIds
  }
  return null
}

export function countOf(ids: string[], id: string): number {
  return ids.filter((value) => value === id).length
}

export function ensureInteraction(dispatcher: CommandDispatcher, state: GameState, puzzleId: string): void {
  const puzzle = dispatcher.index.puzzle(puzzleId)
  if (!puzzle) throw new Error(`EnsureInteraction: unknown puzzle ${puzzleId}`)
  if (state.currentSceneId !== puzzle.sceneId) solveSceneTo(dispatcher, state, puzzle.sceneId)
  stepToInteraction(dispatcher, state)
  if (state.activePuzzleId !== puzzleId) {
    throw new Error(
      `EnsureInteraction(${puzzleId}) failed: scene=${state.currentSceneId} phase=${state.phase} active=${state.activePuzzleId}`,
    )
  }
}

export function solvePuzzle(dispatcher: CommandDispatcher, state: GameState, puzzleId: string): void {
  ensureInteraction(dispatcher, state, puzzleId)
  const plan = planTestAssist(dispatcher.index, state)
  if (plan.commands.length === 0) {
    throw new Error(
      `assist plan unavailable for ${puzzleId}: blocker=${plan.blocker} canRun=${plan.canRun} scene=${state.currentSceneId} phase=${state.phase}`,
    )
  }
  for (const command of plan.commands) {
    const result = dispatcher.execute(command)
    if (result.status === ResultStatus.Rejected) {
      throw new Error(`assist command ${command.type} rejected for ${puzzleId}: ${result.reasonCode} ${result.message}`)
    }
  }
  if (!state.isPuzzleSolved(puzzleId)) {
    throw new Error(`assist plan did not solve ${puzzleId}: blocker=${plan.blocker} phase=${state.phase}`)
  }
}

export function solveSceneTo(dispatcher: CommandDispatcher, state: GameState, targetSceneId: string): void {
  if (!dispatcher.index.hasScene(targetSceneId)) {
    throw new Error(`SolveSceneTo: unknown scene ${targetSceneId}`)
  }
  const trace: string[] = []
  for (let steps = 0; steps < 3000; steps++) {
    if (state.currentSceneId === targetSceneId) return
    trace.push(`${state.currentSceneId}/${state.phase}${state.activePuzzleId ? '/' + state.activePuzzleId : ''}`)
    if (trace.length > 60) trace.shift()

    switch (state.phase) {
      case Phases.PreDialogue:
      case Phases.PostDialogue:
        expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue), `AdvanceDialogue at ${state.currentSceneId}`)
        break
      case Phases.Interaction:
        solveActive(dispatcher, state)
        break
      case Phases.BranchChoice:
        if (!chooseAdvancing(dispatcher, state)) {
          expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(branch) at ${state.currentSceneId}`)
        }
        break
      case Phases.ReadCheck:
        openRequiredReads(dispatcher, state)
        expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(read) at ${state.currentSceneId}`)
        break
      case Phases.ReadyToExit:
      case Phases.Completed: {
        const transition = dispatcher.transitionAvailable()
        if (transition.available) {
          expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene at ${state.currentSceneId}`)
          break
        }
        const scene = dispatcher.index.scene(state.currentSceneId)
        if (isHubLike(scene)) {
          dispatcher.enterScene(state.currentSceneId)
          break
        }
        throw new Error(
          `SolveSceneTo(${targetSceneId}) stuck at ${state.currentSceneId}: ${transition.reason}\ntrace: ${trace.join(' > ')}`,
        )
      }
      case Phases.HubActive:
        if (!tryEnterNextRequiredChild(dispatcher, state)) {
          expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(hub) at ${state.currentSceneId}`)
        }
        break
      default:
        throw new Error(
          `SolveSceneTo(${targetSceneId}) unhandled phase ${state.phase} at ${state.currentSceneId}\ntrace: ${trace.join(' > ')}`,
        )
    }
  }
  throw new Error(`SolveSceneTo(${targetSceneId}) exceeded step limit; last=${state.currentSceneId}/${state.phase}`)
}

export function finishScene(dispatcher: CommandDispatcher, state: GameState): void {
  for (let i = 0; i < 1000; i++) {
    switch (state.phase) {
      case Phases.PreDialogue:
      case Phases.PostDialogue:
        expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue), `AdvanceDialogue at ${state.currentSceneId}`)
        break
      case Phases.BranchChoice:
        if (!chooseAdvancing(dispatcher, state)) {
          expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(branch) at ${state.currentSceneId}`)
        }
        break
      case Phases.ReadCheck:
        openRequiredReads(dispatcher, state)
        expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(read) at ${state.currentSceneId}`)
        break
      default:
        return
    }
  }
  throw new Error(`FinishScene exceeded step limit at ${state.currentSceneId}/${state.phase}`)
}

export function advanceUntilChoiceOrInteraction(dispatcher: CommandDispatcher, state: GameState): void {
  for (let i = 0; i < 500; i++) {
    if (state.phase === Phases.PreDialogue || state.phase === Phases.PostDialogue) {
      expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue), `AdvanceDialogue at ${state.currentSceneId}`)
      continue
    }
    return
  }
  throw new Error(`AdvanceUntilChoiceOrInteraction exceeded step limit at ${state.currentSceneId}/${state.phase}`)
}

export function ensureHubActive(dispatcher: CommandDispatcher, state: GameState, hubId: string): void {
  if (state.currentSceneId !== hubId) solveSceneTo(dispatcher, state, hubId)
  if (state.phase !== Phases.HubActive) {
    finishScene(dispatcher, state)
    if (state.phase !== Phases.HubActive) dispatcher.enterScene(hubId)
  }
  if (state.currentSceneId !== hubId || state.phase !== Phases.HubActive) {
    throw new Error(`EnsureHubActive(${hubId}) failed: scene=${state.currentSceneId} phase=${state.phase}`)
  }
}

function stepToInteraction(dispatcher: CommandDispatcher, state: GameState): void {
  for (let i = 0; i < 500; i++) {
    switch (state.phase) {
      case Phases.PreDialogue:
      case Phases.PostDialogue:
        expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue), `AdvanceDialogue at ${state.currentSceneId}`)
        break
      case Phases.BranchChoice:
        if (!chooseAdvancing(dispatcher, state)) {
          expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(branch) at ${state.currentSceneId}`)
        }
        break
      case Phases.ReadCheck:
        openRequiredReads(dispatcher, state)
        expectAccepted(send(dispatcher, CommandKinds.ContinueScene), `ContinueScene(read) at ${state.currentSceneId}`)
        break
      default:
        return
    }
  }
  throw new Error(`StepToInteraction exceeded step limit at ${state.currentSceneId}/${state.phase}`)
}

function solveActive(dispatcher: CommandDispatcher, state: GameState): void {
  const puzzleId = state.activePuzzleId
  const plan = planTestAssist(dispatcher.index, state)
  if (plan.commands.length === 0) {
    throw new Error(
      `SolveSceneTo assist plan unavailable for ${puzzleId}: blocker=${plan.blocker} scene=${state.currentSceneId}`,
    )
  }
  for (const command of plan.commands) {
    const result = dispatcher.execute(command)
    if (result.status === ResultStatus.Rejected) {
      throw new Error(
        `SolveSceneTo assist command ${command.type} rejected for ${puzzleId}: ${result.reasonCode} ${result.message}`,
      )
    }
  }
  if (!state.isPuzzleSolved(puzzleId)) {
    throw new Error(`SolveSceneTo assist plan did not solve ${puzzleId}: blocker=${plan.blocker}`)
  }
}

export function chooseAdvancing(dispatcher: CommandDispatcher, state: GameState): boolean {
  const scene = dispatcher.index.scene(state.currentSceneId)
  if (!scene || !scene.branchGroupIds) return false
  for (const groupId of scene.branchGroupIds) {
    const group = dispatcher.index.branchGroup(groupId)
    for (const branch of group) {
      if (!branch || !branch.advances) continue
      if (state.isTopicVisited(branch.id)) continue
      if (!allConditionsMet(branch.condition, state, dispatcher.index)) continue
      expectAccepted(send(dispatcher, CommandKinds.ChooseBranch, { targetId: branch.id }), `ChooseBranch ${branch.id}`)
      return true
    }
  }
  return false
}

function tryEnterNextRequiredChild(dispatcher: CommandDispatcher, state: GameState): boolean {
  const hub = dispatcher.index.scene(state.currentSceneId)
  if (!hub || !hub.childSceneIds) return false
  for (const childId of hub.childSceneIds) {
    if (state.isSceneCompleted(childId)) continue
    const child = dispatcher.index.scene(childId)
    if (!child) continue
    if (!allConditionsMet(child.entryConditions, state, dispatcher.index)) continue
    dispatcher.enterScene(childId)
    return true
  }
  return false
}

function openRequiredReads(dispatcher: CommandDispatcher, state: GameState): void {
  const scene = dispatcher.index.scene(state.currentSceneId)
  if (!scene || !scene.requiredReadEvidenceIds) return
  for (const evidenceId of scene.requiredReadEvidenceIds) {
    if (state.hasRead(evidenceId)) continue
    if (!state.owns(evidenceId)) {
      throw new Error(`SolveSceneTo required read ${evidenceId} is not owned at ${state.currentSceneId}`)
    }
    expectAccepted(send(dispatcher, CommandKinds.OpenEvidence, { targetId: evidenceId }), `OpenEvidence ${evidenceId}`)
  }
}

function isHubLike(scene: ReturnType<ContentIndex['scene']>): boolean {
  if (!scene) return false
  return scene.screenType === 'hub' || (!!scene.childSceneIds && scene.childSceneIds.length > 0)
}

/** 콘텐츠 기반 수동 해결 (HI04 대조용) */
export function solveManually(dispatcher: CommandDispatcher, _state: GameState, puzzle: PuzzleDefinitionDto): void {
  switch (puzzle.type) {
    case 'hotspot_set':
      for (const region of puzzle.regions ?? []) {
        expectAccepted(send(dispatcher, CommandKinds.InspectRegion, { targetId: region.regionId }))
      }
      break
    case 'statement_refutation':
      expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: puzzle.targetStatementId }))
      sendFirstEvidence(dispatcher, puzzle.sufficientEvidenceSets, undefined)
      sendFirstChoice(dispatcher, puzzle.correctChoiceIds)
      expectAccepted(send(dispatcher, CommandKinds.PresentEvidence))
      break
    case 'source_mapping':
    case 'trace_mapping':
      for (const pair of puzzle.valuePairs ?? []) {
        expectAccepted(send(dispatcher, CommandKinds.PlaceCard, { slot: pair.key, value: pair.value }))
      }
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'timeline_order':
    case 'chronology':
    case 'deployment_chain':
      placeOrdered(dispatcher, puzzle.orderedIds)
      sendFirstChoice(dispatcher, puzzle.correctChoiceIds)
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'cross_camera':
      sendFirstChoiceWindow(dispatcher, puzzle.correctChoiceIds)
      sendFirstEvidence(dispatcher, puzzle.sufficientEvidenceSets, undefined)
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'argument_choice':
      sendFirstChoice(dispatcher, puzzle.correctChoiceIds)
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'diff_select':
      for (const id of puzzle.correctChoiceIds ?? []) {
        expectAccepted(send(dispatcher, CommandKinds.SelectChoice, { targetId: id }))
      }
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'assumption_link':
      sendFirstChoice(dispatcher, puzzle.correctChoiceIds)
      sendFirstEvidence(dispatcher, puzzle.sufficientEvidenceSets, undefined)
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'matrix_gap': {
      const dims = ['sensor', 'overrideState', 'version', 'condition']
      for (let i = 0; i < dims.length && puzzle.orderedIds && i < puzzle.orderedIds.length; i++) {
        expectAccepted(send(dispatcher, CommandKinds.PlaceCard, { slot: dims[i] ?? '', value: puzzle.orderedIds[i] ?? '' }))
      }
      sendFirstChoice(dispatcher, puzzle.correctChoiceIds)
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    }
    case 'counterfactual':
      for (const id of puzzle.requiredCaseIds ?? []) {
        expectAccepted(send(dispatcher, CommandKinds.RunCounterfactualCase, { targetId: id }))
      }
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'causal_graph':
      for (const edge of puzzle.requiredEdges ?? []) {
        expectAccepted(
          send(dispatcher, CommandKinds.ConnectNodes, { targetId: edge.kind, ids: [edge.from, edge.to] }),
        )
      }
      for (const proof of puzzle.edgeProofRequirements ?? []) {
        sendFirstEvidence(dispatcher, proof.sufficientEvidenceSets, `${proof.from}|${proof.to}|${proof.kind}`)
      }
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    case 'responsibility_matrix':
      for (const proof of puzzle.reportProofRequirements ?? []) {
        expectAccepted(send(dispatcher, CommandKinds.PlaceCard, { slot: proof.section, value: proof.item }))
        sendFirstEvidence(dispatcher, proof.sufficientEvidenceSets, proof.item)
      }
      expectAccepted(
        send(dispatcher, CommandKinds.PlaceCard, { slot: 'limit', value: 'no_intent_or_criminal_verdict' }),
      )
      expectAccepted(send(dispatcher, CommandKinds.SubmitPuzzle))
      break
    default:
      throw new Error(`manual solution missing for puzzle type ${puzzle.type}`)
  }
}

function placeOrdered(dispatcher: CommandDispatcher, orderedIds: string[] | null | undefined): void {
  if (!orderedIds) return
  for (const id of orderedIds) {
    expectAccepted(send(dispatcher, CommandKinds.PlaceCard, { slot: 'order', value: id }))
  }
}

function sendFirstChoice(dispatcher: CommandDispatcher, choiceIds: string[] | null | undefined): void {
  if (!choiceIds || choiceIds.length === 0) return
  expectAccepted(send(dispatcher, CommandKinds.SelectChoice, { targetId: choiceIds[0] ?? '' }))
}

function sendFirstChoiceWindow(dispatcher: CommandDispatcher, choiceIds: string[] | null | undefined): void {
  if (!choiceIds || choiceIds.length === 0) return
  expectAccepted(send(dispatcher, CommandKinds.SelectComparisonWindow, { targetId: choiceIds[0] ?? '' }))
}

function sendFirstEvidence(
  dispatcher: CommandDispatcher,
  sets: EvidenceSetDto[] | null | undefined,
  targetId: string | undefined,
): void {
  const ids = firstEvidenceSet(sets)
  if (!ids) throw new Error(`no sufficient evidence set for target ${targetId ?? '<default>'}`)
  expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { targetId, ids }))
}
