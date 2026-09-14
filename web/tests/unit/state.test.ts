import { describe, expect, it } from 'vitest'
import { CommandKinds, Origins, ResultStatus } from '../../src/core/commands/commandTypes'
import { evaluateCondition } from '../../src/core/conditions/conditionEvaluator'
import type { ContentIndex } from '../../src/core/content/contentIndex'
import { Phases } from '../../src/core/state/gameState'
import {
  advanceUntilChoiceOrInteraction,
  countOf,
  ensureHubActive,
  ensureInteraction,
  expectAccepted,
  finishScene,
  freshDispatcher,
  freshState,
  loadTestContent,
  send,
  solvePuzzle,
  solveSceneTo,
} from '../helpers/testContent'

function reachInteraction(dispatcher: ReturnType<typeof freshDispatcher>['dispatcher'], state: ReturnType<typeof freshDispatcher>['state'], sceneId: string, puzzleId: string): void {
  solveSceneTo(dispatcher, state, sceneId)
  ensureInteraction(dispatcher, state, puzzleId)
}

function expectSameSet(a: string[], b: string[], label: string): void {
  expect([...a].sort(), label).toEqual([...b].sort())
}

describe('ST — 상태 전이', () => {
  it('ST01 증거 중복 지급은 멱등하다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    dispatcher.enterScene('s140')
    advanceUntilChoiceOrInteraction(dispatcher, state)
    expect(countOf(state.ownedEvidence, 'ev06')).toBe(1)
    expect(countOf(state.ownedEvidence, 'ev07')).toBe(1)

    dispatcher.enterScene('s140')
    dispatcher.enterScene('s140')
    expect(countOf(state.ownedEvidence, 'ev06')).toBe(1)
    expect(countOf(state.ownedEvidence, 'ev07')).toBe(1)
  })

  it('ST02 같은 commandId는 두 번 처리되지 않는다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    const command = {
      commandId: 'st02_duplicate',
      sequence: state.lastCommandSequence + 1,
      sceneId: state.currentSceneId,
      origin: Origins.User,
      type: CommandKinds.AdvanceDialogue,
    }
    const first = dispatcher.execute(command)
    expect(first.status).toBe(ResultStatus.Accepted)
    const second = dispatcher.execute(command)
    expect(second.status).toBe(ResultStatus.Rejected)
    expect(second.reasonCode).toBe('duplicate_command')
  })

  it('ST03 다른 증언으로는 실패하고 맞는 증언으로 해결된다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's150', 'p04')

    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_01' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev06', 'ev07'] }))
    const wrongResult = send(dispatcher, CommandKinds.PresentEvidence)
    expect(wrongResult.status).toBe(ResultStatus.Rejected)
    expect(state.isPuzzleSolved('p04')).toBe(false)

    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    const solvedResult = send(dispatcher, CommandKinds.PresentEvidence)
    expect(solvedResult.status).toBe(ResultStatus.Solved)
    expect(state.isPuzzleSolved('p04')).toBe(true)
  })

  it('ST04 증거 선택 순서는 결과를 바꾸지 않는다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's150', 'p04')
    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev07', 'ev06'] }))
    expect(send(dispatcher, CommandKinds.PresentEvidence).status).toBe(ResultStatus.Solved)
  })

  it('ST05 부분 근거는 Partial이며 해결되지 않는다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's150', 'p04')
    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev06'] }))
    const result = send(dispatcher, CommandKinds.PresentEvidence)
    expect(result.status).toBe(ResultStatus.Partial)
    expect(state.isPuzzleSolved('p04')).toBe(false)
  })

  it('ST06 추가 근거가 있어도 핵심 집합이 포함되면 해결된다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's150', 'p04')
    expect(state.owns('ev05')).toBe(true)
    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st150_02' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev06', 'ev07', 'ev05'] }))
    expect(send(dispatcher, CommandKinds.PresentEvidence).status).toBe(ResultStatus.Solved)
  })

  it('ST07 미보유 증거 선택은 거절된다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's150', 'p04')
    expect(state.owns('ev10')).toBe(false)
    const result = send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev10'] })
    expect(result.status).toBe(ResultStatus.Rejected)
    expect(result.reasonCode).toBe('evidence_not_owned')
  })

  it('ST08 인과판 자기 연결·순환은 거절된다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's610', 'p17')

    expectAccepted(send(dispatcher, CommandKinds.ConnectNodes, { targetId: 'causal', ids: ['n_brake_limit', 'n_fault'] }))
    const cycle = send(dispatcher, CommandKinds.ConnectNodes, { targetId: 'causal', ids: ['n_fault', 'n_brake_limit'] })
    expect(cycle.status).toBe(ResultStatus.Rejected)
    expect(cycle.reasonCode).toBe('cycle_edge')

    const self = send(dispatcher, CommandKinds.ConnectNodes, { targetId: 'causal', ids: ['n_fault', 'n_fault'] })
    expect(self.status).toBe(ResultStatus.Rejected)
    expect(self.reasonCode).toBe('self_edge')

    const draft = state.getDraft('p17', false)
    expect(draft).not.toBeNull()
    expect(draft?.edges.length ?? 0).toBe(1)
  })

  it('ST09 신호선으로 대체한 인과 간선은 해결로 인정되지 않는다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's610', 'p17')
    const puzzle = index.puzzle('p17')
    if (!puzzle) throw new Error('p17 missing')

    for (const edge of puzzle.requiredEdges) {
      const kind = edge.from === 'n_onroad' && edge.to === 'n_brake_limit' ? 'signal' : edge.kind
      expectAccepted(send(dispatcher, CommandKinds.ConnectNodes, { targetId: kind, ids: [edge.from, edge.to] }))
    }
    for (const proof of puzzle.edgeProofRequirements) {
      const ids = proof.sufficientEvidenceSets[0]?.requiredIds
      expect(ids, `p17 edge proof ${proof.from}->${proof.to} needs evidence`).toBeTruthy()
      expectAccepted(
        send(dispatcher, CommandKinds.SelectEvidence, {
          targetId: `${proof.from}|${proof.to}|${proof.kind}`,
          ids: ids ?? [],
        }),
      )
    }
    const submit = send(dispatcher, CommandKinds.SubmitPuzzle)
    expect(submit.status).toBe(ResultStatus.Partial)
    expect(state.isPuzzleSolved('p17')).toBe(false)

    const draft = state.getDraft('p17', false)
    const signalEdge = draft?.edges.some(
      (edge) => edge.from === 'n_onroad' && edge.to === 'n_brake_limit' && edge.kind === 'signal',
    )
    expect(signalEdge).toBe(true)
  })

  it('ST10 사후 노드는 원인 노드가 아니며 p19는 선택 자료를 요구하지 않는다', () => {
    const index = loadTestContent()
    const nodes = index.graphNodes()
    expect(nodes.length).toBeGreaterThan(0)
    for (const node of nodes) {
      const id = node.id ?? ''
      expect(id.toLowerCase().includes('public')).toBe(false)
      expect(id.toLowerCase().includes('postincident')).toBe(false)
      expect(node.kind).not.toBe('publicity')
      for (const evidenceId of node.evidenceIds ?? []) {
        expect(['ev34', 'ev35']).not.toContain(evidenceId)
      }
    }

    const p17 = index.puzzle('p17')
    if (!p17) throw new Error('p17 missing')
    for (const edge of p17.requiredEdges) {
      expect(nodes.some((node) => node.id === edge.from)).toBe(true)
      expect(nodes.some((node) => node.id === edge.to)).toBe(true)
    }

    const p19 = index.puzzle('p19')
    if (!p19) throw new Error('p19 missing')
    for (const evidenceId of p19.requiredEvidenceIds) {
      expect(['ev34', 'ev35']).not.toContain(evidenceId)
    }
    expect(p19.sufficientEvidenceSets).toHaveLength(1)
    expect([...(p19.sufficientEvidenceSets[0]?.requiredIds ?? [])].sort()).toEqual(['ev02', 'ev07', 'ev36'])
  })

  it('ST11 p18은 ev21 단독으로 해결되지 않는다', () => {
    const index = loadTestContent()
    const { dispatcher, state } = freshDispatcher(index)
    reachInteraction(dispatcher, state, 's620', 'p18')

    expectAccepted(send(dispatcher, CommandKinds.SelectStatement, { targetId: 'st620_03' }))
    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev21'] }))
    const kpiOnly = send(dispatcher, CommandKinds.PresentEvidence)
    expect(kpiOnly.status).toBe(ResultStatus.Rejected)
    expect(state.isPuzzleSolved('p18')).toBe(false)

    expectAccepted(send(dispatcher, CommandKinds.SelectEvidence, { ids: ['ev29', 'ev30'] }))
    expect(send(dispatcher, CommandKinds.PresentEvidence).status).toBe(ResultStatus.Solved)
  })

  it('ST12 재구성 수치는 고정값과 일치한다', () => {
    const index = loadTestContent()
    const k = index.graph.kinematics
    expect(k).toBeTruthy()
    expect(k.historicalDistanceMeters).toBeCloseTo(20.28915111, 4)
    expect(k.historicalImpactKmh).toBeCloseTo(27.5168, 4)
    expect(k.fullBrakeStopMeters).toBeCloseTo(14.79021306, 4)
    expect(k.requestToStrongMs).toBe(1360)
    expect(round2(k.historicalDistanceMeters)).toBe('20.29')
    expect(round2(k.historicalImpactKmh)).toBe('27.52')
    expect(round2(k.fullBrakeStopMeters)).toBe('14.79')
  })

  it('ST13 반사실 거리 계산은 프레임 독립이다', () => {
    const first = loadTestContent().graph.kinematics.historicalDistanceMeters
    const second = loadTestContent().graph.kinematics.historicalDistanceMeters
    expect(first).toBe(second)

    const k = loadTestContent().graph.kinematics
    expect(historicalDistance(k)).toBeCloseTo(20.28915111, 4)
    const coarse = integratedDistance(k, 0.001)
    const fine = integratedDistance(k, 0.0005)
    expect(coarse).toBeCloseTo(fine, 6)
    expect(first).toBeCloseTo(coarse, 3)
  })

  it('ST14 s530 거절은 수용과 다른 상태를 만든다', () => {
    const index = loadTestContent()

    const decline = freshDispatcher(index)
    reachBranchChoice(decline.dispatcher, decline.state, 's530', 's500')
    expectAccepted(send(decline.dispatcher, CommandKinds.ChooseBranch, { targetId: 'b_s530_decline' }))
    expect(decline.state.getBranchValue('opt_family_visit')).toBe('true')
    expect(decline.state.getBranchValue('opt_family_material')).toBe('false')
    expect(decline.state.owns('ev35')).toBe(false)

    const accept = freshDispatcher(index)
    reachBranchChoice(accept.dispatcher, accept.state, 's530', 's500')
    expectAccepted(send(accept.dispatcher, CommandKinds.ChooseBranch, { targetId: 'b_s530_accept' }))
    expect(accept.state.getBranchValue('opt_family_visit')).toBe('true')
    expect(accept.state.owns('ev35')).toBe(true)
  })

  it('ST15 두 엔딩 응답은 동일한 보고서 사실을 유지한다', () => {
    const index = loadTestContent()
    const a = runEnding(index, 'b_s700_facts')
    const b = runEnding(index, 'b_s700_prevention')

    expect(a.scene).toBe('s710')
    expect(b.scene).toBe('s710')
    expect(a.state.isPuzzleSolved('p20')).toBe(true)
    expect(b.state.isPuzzleSolved('p20')).toBe(true)

    expectSameSet(a.state.ownedEvidence, b.state.ownedEvidence, 'owned evidence')
    expectSameSet(a.state.solvedPuzzles, b.state.solvedPuzzles, 'solved puzzles')
    expect(endingDraftReport(a.state)).toBe(endingDraftReport(b.state))
    expect(a.state.getBranchValue('ending_tone')).toBe('facts_first')
    expect(b.state.getBranchValue('ending_tone')).toBe('prevention_first')

    const keysA = a.state.branchValues.map((pair) => pair.key).sort()
    const keysB = b.state.branchValues.map((pair) => pair.key).sort()
    expect(keysA).toEqual(keysB)
    for (const key of keysA) {
      if (key === 'ending_tone') continue
      expect(a.state.getBranchValue(key), `branch value ${key}`).toBe(b.state.getBranchValue(key))
    }
  })

  it('ST16 허브 자식 방문 순서는 결과를 바꾸지 않는다', () => {
    const index = loadTestContent()

    const reference = runHubPermutation(index, 's100', 's140', ['s110', 's120', 's130'])
    for (const permutation of permutations(['s110', 's120', 's130']).slice(1)) {
      const result = runHubPermutation(index, 's100', 's140', permutation)
      expect(result.scene).toBe(reference.scene)
      expectSameSet(reference.owned, result.owned, `s100 permutation ${permutation.join(',')}`)
      expectSameSet(reference.solved, result.solved, `s100 permutation ${permutation.join(',')}`)
    }

    const reference200 = runHubPermutation(index, 's200', 's240', ['s210', 's220', 's230'])
    for (const permutation of permutations(['s210', 's220', 's230']).slice(1)) {
      const result = runHubPermutation(index, 's200', 's240', permutation)
      expect(result.scene).toBe(reference200.scene)
      expectSameSet(reference200.owned, result.owned, `s200 permutation ${permutation.join(',')}`)
      expectSameSet(reference200.solved, result.solved, `s200 permutation ${permutation.join(',')}`)
    }
  })

  it('ST17 s411은 s410 완료 뒤에만 열린다', () => {
    const index = loadTestContent()
    const s411 = index.scene('s411')
    expect(s411).not.toBeNull()
    if (!s411) return
    expect(s411.entryConditions.every((condition) => evaluateCondition(condition, freshState(), index))).toBe(false)

    const { dispatcher, state } = freshDispatcher(index)
    solveSceneTo(dispatcher, state, 's400')
    ensureHubActive(dispatcher, state, 's400')

    expect(state.isSceneCompleted('s410')).toBe(false)
    expect(s411.entryConditions.every((condition) => evaluateCondition(condition, state, index))).toBe(false)
    expect(dispatcher.transitionAvailable().available).toBe(false)
    const blocked = send(dispatcher, CommandKinds.ContinueScene)
    expect(blocked.status).toBe(ResultStatus.Rejected)
    expect(state.isSceneVisited('s411')).toBe(false)

    solvePuzzle(dispatcher, state, 'p11')
    finishScene(dispatcher, state)
    expect(state.currentSceneId).toBe('s410')
    expect(state.isSceneCompleted('s410')).toBe(true)
    expect(s411.entryConditions.every((condition) => evaluateCondition(condition, state, index))).toBe(true)

    const unlock = dispatcher.transitionAvailable()
    expect(unlock.available).toBe(true)
    expect(unlock.target).toBe('s411')
    expectAccepted(send(dispatcher, CommandKinds.ContinueScene))
    expect(state.currentSceneId).toBe('s411')
  })

  it('ST18 s510/s540 순서 반전과 선택 방문이 같은 합류에 도달한다', () => {
    const index = loadTestContent()
    const baseline = runCase550(index, ['s510', 's540'])
    const flipped = runCase550(index, ['s540', 's510'])
    const optional = runCase550(index, ['s520', 's530', 's540', 's510'])

    expect(baseline.scene).toBe('s550')
    expect(flipped.scene).toBe('s550')
    expect(optional.scene).toBe('s550')
    expectSameSet(baseline.solved, flipped.solved, 's510/s540 order')
    expectSameSet(baseline.solved, optional.solved, 'optional scenes')
    expect(baseline.solved).toContain('p16')
  })
})

function reachBranchChoice(
  dispatcher: ReturnType<typeof freshDispatcher>['dispatcher'],
  state: ReturnType<typeof freshDispatcher>['state'],
  optionalSceneId: string,
  hubSceneId: string,
): void {
  solveSceneTo(dispatcher, state, hubSceneId)
  ensureHubActive(dispatcher, state, hubSceneId)
  dispatcher.enterScene(optionalSceneId)
  advanceUntilChoiceOrInteraction(dispatcher, state)
}

function round2(value: number): string {
  return (Math.round((value + Number.EPSILON) * 100) / 100).toFixed(2)
}

interface Kinematics {
  speedKmh: number
  actuatorDelaySeconds: number
  weakDurationSeconds: number
  weakDeceleration: number
  strongDurationSeconds: number
  strongDeceleration: number
}

function historicalDistance(k: Kinematics): number {
  let v = k.speedKmh / 3.6
  let distance = v * k.actuatorDelaySeconds
  distance += v * k.weakDurationSeconds - 0.5 * k.weakDeceleration * k.weakDurationSeconds * k.weakDurationSeconds
  v -= k.weakDeceleration * k.weakDurationSeconds
  distance += v * k.strongDurationSeconds - 0.5 * k.strongDeceleration * k.strongDurationSeconds * k.strongDurationSeconds
  return distance
}

function integratedDistance(k: Kinematics, dt: number): number {
  let v = k.speedKmh / 3.6
  let distance = 0
  const delaySteps = Math.round(k.actuatorDelaySeconds / dt)
  for (let i = 0; i < delaySteps; i++) distance += v * dt
  const weakSteps = Math.round(k.weakDurationSeconds / dt)
  for (let i = 0; i < weakSteps; i++) {
    distance += v * dt - 0.5 * k.weakDeceleration * dt * dt
    v -= k.weakDeceleration * dt
  }
  const strongSteps = Math.round(k.strongDurationSeconds / dt)
  for (let i = 0; i < strongSteps; i++) {
    distance += v * dt - 0.5 * k.strongDeceleration * dt * dt
    v -= k.strongDeceleration * dt
  }
  return distance
}

interface RunResult {
  scene: string
  state: ReturnType<typeof freshDispatcher>['state']
  owned: string[]
  solved: string[]
}

function capture(state: ReturnType<typeof freshDispatcher>['state']): RunResult {
  return { scene: state.currentSceneId, state, owned: [...state.ownedEvidence], solved: [...state.solvedPuzzles] }
}

function runEnding(index: ContentIndex, branchId: string): RunResult {
  const { dispatcher, state } = freshDispatcher(index)
  solveSceneTo(dispatcher, state, 's700')
  advanceUntilChoiceOrInteraction(dispatcher, state)
  expect(state.phase).toBe(Phases.BranchChoice)
  expectAccepted(send(dispatcher, CommandKinds.ChooseBranch, { targetId: branchId }))
  expectAccepted(send(dispatcher, CommandKinds.ContinueScene))
  return capture(state)
}

function endingDraftReport(state: ReturnType<typeof freshDispatcher>['state']): string {
  const draft = state.getDraft('p20', false)
  if (!draft) return '<no p20 draft>'
  const placements = draft.reportPlacements.map((item) => `${item.section}:${item.item}`).sort()
  const evidence = draft.reportEvidence
    .map((item) => `${item.item}:${[...item.evidenceIds].sort().join('+')}`)
    .sort()
  return `${placements.join('|')}||${evidence.join('|')}`
}

function runHubPermutation(index: ContentIndex, hubId: string, _targetScene: string, children: string[]): RunResult {
  const { dispatcher, state } = freshDispatcher(index)
  solveSceneTo(dispatcher, state, hubId)
  ensureHubActive(dispatcher, state, hubId)
  for (const childId of children) {
    const child = index.scene(childId)
    expect(child, `hub child must exist: ${childId}`).not.toBeNull()
    if (!child) continue
    expect(
      child.entryConditions.every((condition) => evaluateCondition(condition, state, index)),
      `child ${childId} entry conditions in permutation order`,
    ).toBe(true)
    dispatcher.enterScene(childId)
    solveSceneTo(dispatcher, state, hubId)
    ensureHubActive(dispatcher, state, hubId)
  }
  expectAccepted(send(dispatcher, CommandKinds.ContinueScene))
  return capture(state)
}

function runCase550(index: ContentIndex, order: string[]): RunResult {
  const { dispatcher, state } = freshDispatcher(index)
  solveSceneTo(dispatcher, state, 's500')
  ensureHubActive(dispatcher, state, 's500')
  for (const sceneId of order) {
    dispatcher.enterScene(sceneId)
    solveSceneTo(dispatcher, state, 's500')
    ensureHubActive(dispatcher, state, 's500')
  }
  expectAccepted(send(dispatcher, CommandKinds.ContinueScene))
  return capture(state)
}

function permutations(items: string[]): string[][] {
  const results: string[][] = []
  const permute = (current: string[], index: number): void => {
    if (index === current.length) {
      results.push([...current])
      return
    }
    for (let i = index; i < current.length; i++) {
      const a = current[index] ?? ''
      const b = current[i] ?? ''
      current[index] = b
      current[i] = a
      permute(current, index + 1)
      current[index] = a
      current[i] = b
    }
  }
  permute([...items], 0)
  return results.sort((a, b) => a.join(',').localeCompare(b.join(',')))
}
