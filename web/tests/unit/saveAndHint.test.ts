import { describe, expect, it } from 'vitest'
import { GameSession } from '../../src/app/gameSession'
import { CommandDispatcher } from '../../src/core/commands/commandDispatcher'
import { CommandKinds, Origins, ResultStatus } from '../../src/core/commands/commandTypes'
import { planTestAssist } from '../../src/core/commands/testAssistPlanner'
import { FrozenIds } from '../../src/core/content/frozenIds'
import { HintService } from '../../src/core/hints/hintService'
import { Phases } from '../../src/core/state/gameState'
import { GameState } from '../../src/core/state/gameState'
import { computeContainRect, tryRegionLocalPoint } from '../../src/shared/assets/regionMath'
import { SaveService, saveKeyFor } from '../../src/shared/save/saveService'
import { Diag } from '../../src/shared/diagnostics'
import {
  countOf,
  ensureHubActive,
  ensureInteraction,
  expectAccepted,
  freshDispatcher,
  freshState,
  loadTestContent,
  send,
  solveManually,
  solvePuzzle,
  solveSceneTo,
} from '../helpers/testContent'

const Slot = 'test_sv'
const CONTENT_VERSION = 'ep01.v1.0.0'

function newSaves(): SaveService {
  return new SaveService(CONTENT_VERSION, '')
}

describe('SV — 저장·복원', () => {
  it('SV01 스냅샷 왕복이 컬렉션을 보존한다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's310')
    expect(state.phase).toBe(Phases.PreDialogue)

    const snapshot = state.toSnapshot(CONTENT_VERSION, '', Slot)
    const parsed = JSON.parse(JSON.stringify(snapshot))
    const restored = GameState.fromSnapshot(parsed)

    expect(restored.currentSceneId).toBe(state.currentSceneId)
    expect(restored.phase).toBe(state.phase)
    expect(restored.dialogueCursor).toBe(state.dialogueCursor)
    expect(restored.lastCommandSequence).toBe(state.lastCommandSequence)
    expect([...restored.ownedEvidence].sort()).toEqual([...state.ownedEvidence].sort())
    expect([...restored.readEvidence].sort()).toEqual([...state.readEvidence].sort())
    expect([...restored.visitedScenes].sort()).toEqual([...state.visitedScenes].sort())
    expect([...restored.completedScenes].sort()).toEqual([...state.completedScenes].sort())
    expect([...restored.solvedPuzzles].sort()).toEqual([...state.solvedPuzzles].sort())
    expect(restored.branchValues.length).toBe(state.branchValues.length)
    expect(restored.puzzleDrafts.length).toBe(state.puzzleDrafts.length)
  })

  it('SV02 심문 초안이 저장·복원된다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's150')
    ensureInteraction(dispatcher, state, 'p04')
    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev06', 'ev07'] }))

    expect(saves.save(state, Slot)).toBe(true)
    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Ok')
    if (!snapshot) return
    const restored = GameState.fromSnapshot(snapshot)
    const draft = restored.getDraft('p04', false)
    expect(draft).not.toBeNull()
    expect(draft?.selectedStatementId).toBe('st150_02')
    expect([...(draft?.selectedEvidenceIds ?? [])].sort()).toEqual(['ev06', 'ev07'])
    expect(restored.activePuzzleId).toBe('p04')
    expect(restored.phase).toBe(Phases.Interaction)
  })

  it('SV03 인과판 간선 초안이 저장·복원된다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's610')
    ensureInteraction(dispatcher, state, 'p17')
    expectAccepted(send(dispatcher, CommandKinds.ConnectNodes, { targetId: 'causal', ids: ['n_fault', 'n_false_rear'] }))

    expect(saves.save(state, Slot)).toBe(true)
    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Ok')
    if (!snapshot) return
    const restored = GameState.fromSnapshot(snapshot)
    const draft = restored.getDraft('p17', false)
    expect(draft).not.toBeNull()
    expect(draft?.edges.length).toBe(1)
    expect(draft?.edges[0]?.from).toBe('n_fault')
    expect(draft?.edges[0]?.to).toBe('n_false_rear')
    expect(draft?.edges[0]?.kind).toBe('causal')
  })

  it('SV04 대사 커서가 복원되고 보상이 재실행되지 않는다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's120')
    for (let i = 0; i < 6; i++) expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
    expect(state.dialogueCursor).toBe(6)
    expect(state.owns('ev05')).toBe(true)
    expect(state.phase).toBe(Phases.PreDialogue)

    const ownedBefore = state.ownedEvidence.length
    expect(saves.save(state, Slot)).toBe(true)
    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Ok')
    if (!snapshot) return
    const restored = GameState.fromSnapshot(snapshot)
    expect(restored.dialogueCursor).toBe(6)
    expect(countOf(restored.ownedEvidence, 'ev05')).toBe(1)
    expect(restored.ownedEvidence.length).toBe(ownedBefore)

    const dispatcher2 = new CommandDispatcher(index, restored)
    expectAccepted(send(dispatcher2, CommandKinds.AdvanceDialogue))
    expect(restored.dialogueCursor).toBe(7)
    expect(countOf(restored.ownedEvidence, 'ev05')).toBe(1)
    expect(restored.ownedEvidence.length).toBe(ownedBefore)
  })

  it('SV05 잘린 저장은 손상으로 보고되고 원본이 보존된다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
    expect(saves.save(state, Slot)).toBe(true)

    const key = saveKeyFor(Slot)
    const text = window.localStorage.getItem(key) ?? ''
    window.localStorage.setItem(key, text.slice(0, Math.max(1, Math.floor(text.length / 2))))

    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Corrupt')
    expect(snapshot).toBeNull()
    expect(window.localStorage.getItem(key)).not.toBeNull()
  })

  it('SV06 체크섬 불일치는 손상으로 보고되고 파일이 보존된다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
    expect(saves.save(state, Slot)).toBe(true)

    const key = saveKeyFor(Slot)
    const envelope = JSON.parse(window.localStorage.getItem(key) ?? '{}') as {
      payloadSha256: string
    }
    envelope.payloadSha256 = '0'.repeat(envelope.payloadSha256.length || 64)
    window.localStorage.setItem(key, JSON.stringify(envelope))

    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Corrupt')
    expect(snapshot).toBeNull()
    expect(window.localStorage.getItem(key)).not.toBeNull()
  })

  it('SV07 미래 스키마 버전은 호환불가로 보고된다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { state } = freshDispatcher(index)
    expect(saves.save(state, Slot)).toBe(true)

    const key = saveKeyFor(Slot)
    const envelope = JSON.parse(window.localStorage.getItem(key) ?? '{}') as { schemaVersion: number }
    envelope.schemaVersion = 999
    window.localStorage.setItem(key, JSON.stringify(envelope))

    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Incompatible')
    expect(snapshot).toBeNull()
    expect(window.localStorage.getItem(key)).not.toBeNull()
  })

  it('SV08 알 수 없는 장면 ID는 세션에서 거절되고 상태를 대체하지 않는다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const state = freshState()
    state.currentSceneId = 's999'
    state.phase = Phases.ReadyToExit
    expect(saves.save(state, Slot)).toBe(true)
    expect(saves.load(Slot).outcome).toBe('Ok')

    const session = new GameSession(index, saves, CONTENT_VERSION, '')
    const result = session.loadGame(Slot)
    expect(result.loaded).toBe(false)
    expect(result.outcome).toBe('UnknownIds')
    expect(session.blockingError).toContain('s999')
    expect(session.state.currentSceneId).toBe('')
  })

  it('SV09 잘못된 슬롯 ID는 상태를 건드리지 않고 실패한다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
    const sceneBefore = state.currentSceneId
    const phaseBefore = state.phase
    const sequenceBefore = state.lastCommandSequence
    const ownedBefore = [...state.ownedEvidence]

    Diag.clear()
    expect(saves.save(state, 'bad/slot')).toBe(false)
    expect(Diag.recentErrors.length).toBeGreaterThan(0)
    expect(Diag.recentErrors.some((message) => message.includes('bad/slot'))).toBe(true)

    expect(state.currentSceneId).toBe(sceneBefore)
    expect(state.phase).toBe(phaseBefore)
    expect(state.lastCommandSequence).toBe(sequenceBefore)
    expect([...state.ownedEvidence].sort()).toEqual([...ownedBefore].sort())
  })

  it('SV10 같은 슬롯에서는 마지막 저장이 이긴다', () => {
    const index = loadTestContent()
    const saves = newSaves()
    const { dispatcher, state } = freshDispatcher(index)
    for (let i = 0; i < 2; i++) expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
    expect(saves.save(state, Slot)).toBe(true)
    const sequenceA = state.lastCommandSequence

    expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
    expect(saves.save(state, Slot)).toBe(true)
    const sequenceB = state.lastCommandSequence
    expect(sequenceB).toBeGreaterThan(sequenceA)

    const { outcome, snapshot } = saves.load(Slot)
    expect(outcome).toBe('Ok')
    expect(snapshot?.lastCommandSequence).toBe(sequenceB)
    expect(snapshot?.currentSceneId).toBe(state.currentSceneId)
    expect(snapshot?.phase).toBe(state.phase)
    expect(snapshot?.ownedEvidenceIds.length).toBe(state.ownedEvidence.length)
  })
})

describe('HI — 힌트와 테스트 지원', () => {
  it('HI01 모든 장면에서 힌트 문맥이 내용을 가진다', () => {
    const index = loadTestContent()
    const hints = new HintService(index)
    for (const sceneId of FrozenIds.Scenes) {
      const state = freshState()
      state.currentSceneId = sceneId
      state.phase = Phases.ReadyToExit
      const context = hints.resolve(state)
      const hasContent = !!context.sceneTip || !!context.nextAction || context.levels.length > 0
      expect(hasContent, `scene ${sceneId} must produce a tip, next action, or hint level`).toBe(true)
    }
  })

  it('HI02 모든 퍼즐에 H1/H2/H3 힌트가 정확히 있다', () => {
    const index = loadTestContent()
    for (const puzzleId of FrozenIds.Puzzles) {
      const levels: number[] = []
      for (const hint of index.hints.values()) {
        if (hint.scopeType !== 'puzzle' || hint.scopeId !== puzzleId) continue
        if (!levels.includes(hint.level)) levels.push(hint.level)
      }
      levels.sort()
      expect(levels, `${puzzleId} must have exactly H1/H2/H3`).toEqual([1, 2, 3])
    }
  })

  it('HI03 H3 열람은 퍼즐 해결 상태를 바꾸지 않는다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's150')
    ensureInteraction(dispatcher, state, 'p04')
    const solvedBefore = [...state.solvedPuzzles]

    const result = send(dispatcher, CommandKinds.OpenHint, { numeric: 3 })
    expect(result.status).not.toBe(ResultStatus.Rejected)
    expect([...state.solvedPuzzles].sort()).toEqual([...solvedBefore].sort())

    const usage = state.getHintUsage(state.currentSceneId, false)
    expect(usage).not.toBeNull()
    expect(usage?.maxLevel).toBe(3)
  })

  it('HI04 정답 행동 실행과 콘텐츠 기반 수동 해결이 동일한 결과를 만든다', () => {
    const index = loadTestContent()
    for (const puzzleId of FrozenIds.Puzzles) {
      const puzzle = index.puzzle(puzzleId)
      if (!puzzle) throw new Error(`${puzzleId} missing`)

      const assist = freshDispatcher(index)
      solveSceneTo(assist.dispatcher, assist.state, puzzle.sceneId)
      ensureInteraction(assist.dispatcher, assist.state, puzzleId)

      const checkpoint = assist.state.toSnapshot(CONTENT_VERSION, '', Slot)
      const manualState = GameState.fromSnapshot(JSON.parse(JSON.stringify(checkpoint)))
      const manualDispatcher = new CommandDispatcher(index, manualState)
      expect([...assist.state.ownedEvidence].sort(), `${puzzleId} cloned owned`).toEqual(
        [...manualState.ownedEvidence].sort(),
      )

      solvePuzzle(assist.dispatcher, assist.state, puzzleId)
      solveManually(manualDispatcher, manualState, puzzle)
      expect(manualState.isPuzzleSolved(puzzleId), `${puzzleId} manual solve`).toBe(true)
      expect([...assist.state.ownedEvidence].sort(), `${puzzleId} owned evidence`).toEqual(
        [...manualState.ownedEvidence].sort(),
      )
      expect([...assist.state.solvedPuzzles].sort(), `${puzzleId} solved puzzles`).toEqual(
        [...manualState.solvedPuzzles].sort(),
      )
    }
  })

  it('HI05 필수 증거 없이는 정답 행동 실행이 불가능하다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    expectAccepted(send(dispatcher, CommandKinds.GoToScene, { targetId: 's150', origin: Origins.Fixture }))
    // fixture jump lands in pre dialogue; advance to the interaction
    for (let i = 0; i < 50 && state.activePuzzleId !== 'p04'; i++) {
      if (state.phase === Phases.PreDialogue) expectAccepted(send(dispatcher, CommandKinds.AdvanceDialogue))
      else break
    }
    expect(state.activePuzzleId).toBe('p04')
    expect(state.owns('ev06')).toBe(false)
    expect(state.owns('ev07')).toBe(false)

    const plan = planTestAssist(dispatcher.index, state)
    expect(plan.canRun).toBe(false)
    expect(plan.blocker).toBe('missing_required_evidence')
    expect(plan.commands.length).toBe(0)

    const ownedBefore = state.ownedEvidence.length
    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    const select = send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev06', 'ev07'] })
    expect(select.status).toBe(ResultStatus.Rejected)
    expect(select.reasonCode).toBe('evidence_not_owned')
    const present = send(dispatcher, CommandKinds.PresentEvidence)
    expect(present.status).toBe(ResultStatus.Rejected)
    expect(state.ownedEvidence.length).toBe(ownedBefore)
    expect(state.isPuzzleSolved('p04')).toBe(false)
  })

  it('HI06 허브 힌트는 퍼즐 힌트를 누출하지 않고 s310은 p09를 가리킨다', () => {
    const index = loadTestContent()
    const hints = new HintService(index)

    const hubRun = freshDispatcher(index)
    solveSceneTo(hubRun.dispatcher, hubRun.state, 's100')
    ensureHubActive(hubRun.dispatcher, hubRun.state, 's100')
    const hubContext = hints.resolve(hubRun.state)
    expect(hubContext.scopeType).not.toBe('puzzle')
    expect(hubContext.scopeId).toBe('s100')
    expect(hubContext.nextAction).toBe('s110')
    for (const level of hubContext.levels) {
      expect(level.scopeType, `hub hint ${level.id} must not target a puzzle`).toBe('scene')
    }

    solveSceneTo(hubRun.dispatcher, hubRun.state, 's310')
    ensureInteraction(hubRun.dispatcher, hubRun.state, 'p09')
    const puzzleContext = hints.resolve(hubRun.state)
    expect(puzzleContext.scopeType).toBe('puzzle')
    expect(puzzleContext.scopeId).toBe('p09')
    expect(puzzleContext.levels.length).toBe(3)
  })
})

describe('UI — 좌표 수학', () => {
  it('UI07 contain 표시 영역과 region 히트 판정이 일치한다', () => {
    const full = computeContainRect({ width: 1600, height: 900 }, { width: 1920, height: 1080 })
    expect(full.x).toBeCloseTo(0, 3)
    expect(full.y).toBeCloseTo(0, 3)
    expect(full.width).toBeCloseTo(1600, 3)
    expect(full.height).toBeCloseTo(900, 3)

    const letterbox = computeContainRect({ width: 1600, height: 1200 }, { width: 1920, height: 1080 })
    expect(letterbox.x).toBeCloseTo(0, 3)
    expect(letterbox.y).toBeCloseTo(150, 3)
    expect(letterbox.width).toBeCloseTo(1600, 3)
    expect(letterbox.height).toBeCloseTo(900, 3)

    const degenerate = computeContainRect({ width: 1600, height: 900 }, { width: 0, height: 0 })
    expect(degenerate.width).toBeCloseTo(1600, 3)
    expect(degenerate.height).toBeCloseTo(900, 3)

    const region = { regionId: 'r', x: 0.5, y: 0.5, width: 0.1, height: 0.1 }
    const displayed = { x: 0, y: 0, width: 1600, height: 900 }
    expect(tryRegionLocalPoint({ x: 805, y: 455 }, displayed, region)).toBe(true)
    expect(tryRegionLocalPoint({ x: 800, y: 450 }, displayed, region)).toBe(true)
    expect(tryRegionLocalPoint({ x: 960, y: 540 }, displayed, region)).toBe(true)
    expect(tryRegionLocalPoint({ x: 799, y: 455 }, displayed, region)).toBe(false)
    expect(tryRegionLocalPoint({ x: 805, y: 449 }, displayed, region)).toBe(false)
    expect(tryRegionLocalPoint({ x: 961, y: 540 }, displayed, region)).toBe(false)
    expect(tryRegionLocalPoint({ x: 805, y: 455 }, displayed, null)).toBe(false)

    const wholeRegion = { regionId: 'all', x: 0, y: 0, width: 1, height: 1 }
    const rect = { x: 10, y: 20, width: 160, height: 90 }
    expect(tryRegionLocalPoint({ x: 10, y: 20 }, rect, wholeRegion)).toBe(true)
    expect(tryRegionLocalPoint({ x: 90, y: 65 }, rect, wholeRegion)).toBe(true)
    expect(tryRegionLocalPoint({ x: 170, y: 110 }, rect, wholeRegion)).toBe(true)
    expect(tryRegionLocalPoint({ x: 9, y: 20 }, rect, wholeRegion)).toBe(false)
    expect(tryRegionLocalPoint({ x: 10, y: 111 }, rect, wholeRegion)).toBe(false)
  })
})
