import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { CommandKinds, Origins, ResultStatus } from '../../src/core/commands/commandTypes'
import { evaluateCondition } from '../../src/core/conditions/conditionEvaluator'
import { validateContent } from '../../src/core/content/contentValidator'
import { FrozenIds } from '../../src/core/content/frozenIds'
import { evaluatePuzzle } from '../../src/core/puzzles/puzzleEvaluator'
import { Phases } from '../../src/core/state/gameState'
import {
  CONTENT_DIR,
  ensureInteraction,
  finishScene,
  freshDispatcher,
  freshState,
  loadTestContent,
  send,
  solveSceneTo,
  testAssetIds,
} from '../helpers/testContent'

const OPTIONAL_EVIDENCE = new Set(['ev34', 'ev35'])

function idsFromFile(fileName: string, key: string): string[] {
  const parsed = JSON.parse(readFileSync(join(CONTENT_DIR, fileName), 'utf8')) as Record<string, unknown>
  const items = parsed[key] as { id: string }[]
  return items.map((item) => item.id)
}

describe('CT — 콘텐츠 계약', () => {
  it('CT01 고정 ID 집합과 개수가 정확하고 중복이 없다', () => {
    const index = loadTestContent()

    expect(index.scenes.size).toBe(43)
    expect(index.evidence.size).toBe(36)
    expect(index.puzzles.size).toBe(20)

    expect([...index.scenes.keys()].sort()).toEqual([...FrozenIds.Scenes].sort())
    expect([...index.evidence.keys()].sort()).toEqual([...FrozenIds.Evidence].sort())
    expect([...index.puzzles.keys()].sort()).toEqual([...FrozenIds.Puzzles].sort())

    for (const [fileName, key] of [
      ['scenes.json', 'items'],
      ['evidence.json', 'items'],
      ['puzzles.json', 'items'],
    ] as const) {
      const ids = idsFromFile(fileName, key)
      expect(new Set(ids).size, `${fileName} must not contain duplicate ids`).toBe(ids.length)
    }
  })

  it('CT02 콘텐츠 검증 오류가 0이다', () => {
    const index = loadTestContent()
    const errors = validateContent(index, testAssetIds())
    expect(errors, errors.join('\n')).toEqual([])

    for (const puzzle of index.puzzles.values()) {
      for (const set of puzzle.sufficientEvidenceSets ?? []) {
        expect(set, `${puzzle.id}.sufficientEvidenceSets`).toBeTruthy()
        expect(set.requiredIds, `${puzzle.id} requiredIds must be serialized`).toBeTruthy()
      }
      for (const proof of puzzle.edgeProofRequirements ?? []) {
        for (const set of proof.sufficientEvidenceSets ?? []) {
          expect(set, `${puzzle.id}.edgeProofRequirements requiredIds`).toBeTruthy()
        }
      }
      for (const proof of puzzle.reportProofRequirements ?? []) {
        for (const set of proof.sufficientEvidenceSets ?? []) {
          expect(set, `${puzzle.id}.reportProofRequirements requiredIds`).toBeTruthy()
        }
      }
    }
  })

  it('CT03 퍼즐 진입 시 필수 증거를 이미 보유하고 있다', () => {
    const index = loadTestContent()

    const p04Run = freshDispatcher(index)
    solveSceneTo(p04Run.dispatcher, p04Run.state, 's150')
    ensureInteraction(p04Run.dispatcher, p04Run.state, 'p04')
    expect(p04Run.state.owns('ev02')).toBe(true)
    expect(p04Run.state.owns('ev06')).toBe(true)
    expect(p04Run.state.owns('ev07')).toBe(true)
    expect(p04Run.state.isPuzzleSolved('p04')).toBe(false)

    for (const puzzleId of FrozenIds.Puzzles) {
      const puzzle = index.puzzle(puzzleId)
      if (!puzzle) throw new Error(`${puzzleId} missing`)
      const run = freshDispatcher(index)
      solveSceneTo(run.dispatcher, run.state, puzzle.sceneId)
      ensureInteraction(run.dispatcher, run.state, puzzleId)
      for (const evidenceId of puzzle.requiredEvidenceIds ?? []) {
        expect(run.state.owns(evidenceId), `${puzzleId} at ${puzzle.sceneId}: ${evidenceId}`).toBe(true)
      }
      expect(run.state.isPuzzleSolved(puzzle.id), `${puzzleId} must remain unsolved`).toBe(false)
    }
  })

  it('CT04 s150의 ev08/ev09는 퍼즐 해결 뒤에만 지급된다', () => {
    const index = loadTestContent()
    const s150 = index.scene('s150')
    expect(s150).not.toBeNull()
    if (!s150) return

    let ev08Post = false
    let ev09Post = false
    for (const grant of s150.grants ?? []) {
      if (grant.evidenceId === 'ev08' && grant.trigger === 'post_start') ev08Post = true
      if (grant.evidenceId === 'ev09' && grant.trigger === 'post_start') ev09Post = true
    }
    expect(ev08Post).toBe(true)
    expect(ev09Post).toBe(true)

    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's150')
    ensureInteraction(dispatcher, state, 'p04')
    expect(state.owns('ev08')).toBe(false)
    expect(state.owns('ev09')).toBe(false)

    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev06', 'ev07'] }))
    expect(send(dispatcher, CommandKinds.PresentEvidence).status).toBe(ResultStatus.Solved)
    expect(state.isPuzzleSolved('p04')).toBe(true)
    expect(state.owns('ev08')).toBe(true)
    expect(state.owns('ev09')).toBe(true)
  })

  it('CT05 선택 장면 없이 완주하면 필수 증거를 모두 보유한다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's710')
    finishScene(dispatcher, state)
    expect(state.currentSceneId).toBe('s710')
    expect(state.isSceneCompleted('s710')).toBe(true)

    for (const id of FrozenIds.Evidence) {
      if (OPTIONAL_EVIDENCE.has(id)) {
        expect(state.owns(id), `optional evidence ${id} must not be granted`).toBe(false)
      } else {
        expect(state.owns(id), `mandatory evidence ${id} must be owned`).toBe(true)
      }
    }
    for (const puzzleId of FrozenIds.Puzzles) {
      expect(state.isPuzzleSolved(puzzleId), `${puzzleId} must be solved`).toBe(true)
    }
  })

  it('CT06 퍼즐 진행 중에는 AdvanceDialogue가 거절된다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's150')
    ensureInteraction(dispatcher, state, 'p04')

    const cursorBefore = state.dialogueCursor
    const result = send(dispatcher, CommandKinds.AdvanceDialogue)
    expect(result.status).toBe(ResultStatus.Rejected)
    expect(result.reasonCode).toBe('not_in_dialogue')
    expect(state.phase).toBe(Phases.Interaction)
    expect(state.dialogueCursor).toBe(cursorBefore)
    expect(state.isPostDone('s150')).toBe(false)

    const s150 = index.scene('s150')
    for (const lineId of s150?.postDialogueIds ?? []) {
      expect(index.dialogue(lineId), `post dialogue ${lineId} must exist`).not.toBeNull()
    }
  })

  it('CT07 s640은 p20 없이 진입하고 내부에서 p20을 푼다', () => {
    const index = loadTestContent()
    const s640 = index.scene('s640')
    expect(s640).not.toBeNull()
    if (!s640) return

    for (const condition of s640.entryConditions ?? []) {
      expect(condition.kind === 'PuzzleSolved' && condition.id === 'p20', 's640 must not require p20').toBe(false)
    }
    expect(s640.interactionIds ?? []).toContain('p20')

    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's640')
    ensureInteraction(dispatcher, state, 'p20')

    for (let i = 1; i <= 19; i++) {
      const id = `p${String(i).padStart(2, '0')}`
      expect(state.isPuzzleSolved(id), `${id} must be solved before reaching s640`).toBe(true)
    }
    expect(state.isPuzzleSolved('p20')).toBe(false)
  })

  it('CT08 알 수 없는 조건·퍼즐 타입은 fail closed이고 잘못된 활성 퍼즐은 거절된다', () => {
    const index = loadTestContent()
    const state = freshState()

    expect(evaluateCondition(null, state, index)).toBe(false)
    expect(evaluateCondition({ kind: 'NoSuchKind', id: 'x' }, state, index)).toBe(false)
    expect(evaluateCondition({ kind: 'EvidenceOwned', id: '' }, state, index)).toBe(false)
    expect(evaluateCondition({ kind: '', id: 'x' }, state, index)).toBe(false)

    const unknownPuzzle = {
      id: 'pXX',
      sceneId: 's000',
      type: 'quantum_entanglement',
      requiredEvidenceIds: [],
      targetStatementId: '',
      correctChoiceIds: [],
      orderedIds: [],
      sufficientEvidenceSets: [],
      valuePairs: [],
      requiredEdges: [],
      edgeProofRequirements: [],
      reportProofRequirements: [],
      andNodeIds: [],
      numericAnswers: [],
      requiredCaseIds: [],
      regions: [],
      windowId: '',
      successText: 'x',
      partialText: '',
      wrongText: '',
      evidenceSetCorrectTexts: [],
    }
    const draft = {
      puzzleId: 'pXX',
      selectedStatementId: '',
      selectedEvidenceIds: [],
      choiceIds: [],
      placedCards: [],
      edges: [],
      values: [],
      edgeProofs: [],
      reportPlacements: [],
      reportEvidence: [],
      ranCaseIds: [],
      comparisonWindowId: '',
      submitted: false,
    }
    const evaluation = evaluatePuzzle(unknownPuzzle, draft, state, index)
    expect(evaluation.status).toBe(ResultStatus.Rejected)

    const run = freshDispatcher(index)
    run.state.phase = Phases.Interaction
    run.state.activePuzzleId = 'p_nonexistent'
    const result = run.dispatcher.execute({
      commandId: 'ct08_unknown',
      sequence: run.state.lastCommandSequence + 1,
      sceneId: run.state.currentSceneId,
      origin: Origins.TestAssist,
      type: CommandKinds.SubmitPuzzle,
    })
    expect(result.status).toBe(ResultStatus.Rejected)
    expect(result.reasonCode).toBe('no_active_puzzle')
  })
})

function expectAccepted(result: { status: string; reasonCode: string; message: string }): void {
  expect(result.status, `${result.reasonCode} ${result.message}`).not.toBe(ResultStatus.Rejected)
}
