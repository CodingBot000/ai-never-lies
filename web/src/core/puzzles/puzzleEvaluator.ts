import type {
  EvidenceSetDto,
  PuzzleDefinitionDto,
  PuzzleDraftDto,
  StringPairDto,
} from '../content/dto'
import type { ContentIndex } from '../content/contentIndex'
import type { GameState } from '../state/gameState'
import { ResultStatus, type CommandStatus } from '../commands/commandTypes'

export const PuzzleTypes = {
  HotspotSet: 'hotspot_set',
  StatementRefutation: 'statement_refutation',
  SourceMapping: 'source_mapping',
  TimelineOrder: 'timeline_order',
  TraceMapping: 'trace_mapping',
  CrossCamera: 'cross_camera',
  Chronology: 'chronology',
  ArgumentChoice: 'argument_choice',
  DiffSelect: 'diff_select',
  AssumptionLink: 'assumption_link',
  MatrixGap: 'matrix_gap',
  DeploymentChain: 'deployment_chain',
  Counterfactual: 'counterfactual',
  CausalGraph: 'causal_graph',
  ResponsibilityMatrix: 'responsibility_matrix',
} as const

const KNOWN_TYPES = new Set<string>(Object.values(PuzzleTypes))

export function isKnownPuzzleType(type: string): boolean {
  return KNOWN_TYPES.has(type)
}

export interface PuzzleEvalResult {
  status: CommandStatus
  message: string
  matchedSetIndex: number
}

function solved(puzzle: PuzzleDefinitionDto, setIndex = -1): PuzzleEvalResult {
  return { status: ResultStatus.Solved, message: puzzle.successText ?? '', matchedSetIndex: setIndex }
}

function partial(message: string): PuzzleEvalResult {
  return { status: ResultStatus.Partial, message, matchedSetIndex: -1 }
}

function wrong(puzzle: PuzzleDefinitionDto, message?: string): PuzzleEvalResult {
  return {
    status: ResultStatus.Rejected,
    message: message && message.length > 0 ? message : (puzzle.wrongText ?? ''),
    matchedSetIndex: -1,
  }
}

function sameOrder(
  expected: string[] | null | undefined,
  placed: PuzzleDraftDto['placedCards'] | null | undefined,
  slot: string,
): boolean {
  if (!expected || expected.length === 0) return false
  const actual = (placed ?? []).filter((card) => card.slot === slot).map((card) => card.value)
  if (actual.length !== expected.length) return false
  for (let i = 0; i < expected.length; i++) {
    if (actual[i] !== expected[i]) return false
  }
  return true
}

function getDraftValue(draft: PuzzleDraftDto, key: string): string {
  const card = (draft.placedCards ?? []).find((item) => item.slot === key)
  if (card) return card.value
  const value = (draft.values ?? []).find((item) => item.key === key)
  return value?.value ?? ''
}

function hasChoice(draft: PuzzleDraftDto, choiceId: string): boolean {
  return (draft.choiceIds ?? []).includes(choiceId)
}

function matchEvidenceSet(sets: EvidenceSetDto[] | null | undefined, selected: string[] | null | undefined): number {
  if (!sets || !selected) return -1
  for (let s = 0; s < sets.length; s++) {
    const set = sets[s]
    if (!set?.requiredIds || set.requiredIds.length === 0) continue
    if (set.requiredIds.every((id) => selected.includes(id))) return s
  }
  return -1
}

function anyRequiredEvidenceSelected(
  sets: EvidenceSetDto[] | null | undefined,
  selected: string[] | null | undefined,
): boolean {
  if (!sets || !selected) return false
  for (const set of sets) {
    if (!set?.requiredIds) continue
    for (const id of set.requiredIds) {
      if (selected.includes(id)) return true
    }
  }
  return false
}

export function hasRequiredEvidence(puzzle: PuzzleDefinitionDto, state: GameState): boolean {
  if (!puzzle.requiredEvidenceIds) return true
  for (const id of puzzle.requiredEvidenceIds) {
    if (!state.owns(id)) return false
  }
  return true
}

/** 퍼즐 정답 판정. 허용된 카드·선택지·간선으로 한정된 결정적 판정. */
export function evaluatePuzzle(
  puzzle: PuzzleDefinitionDto | null | undefined,
  draft: PuzzleDraftDto | null | undefined,
  state: GameState,
  index: ContentIndex,
): PuzzleEvalResult {
  if (!puzzle || !draft) {
    return { status: ResultStatus.Rejected, message: '', matchedSetIndex: -1 }
  }
  if (!isKnownPuzzleType(puzzle.type)) {
    return { status: ResultStatus.Rejected, message: '알 수 없는 퍼즐 형식입니다.', matchedSetIndex: -1 }
  }
  if (!hasRequiredEvidence(puzzle, state)) {
    return {
      status: ResultStatus.Rejected,
      message: '필요한 자료를 아직 보유하지 않았습니다.',
      matchedSetIndex: -1,
    }
  }

  switch (puzzle.type) {
    case PuzzleTypes.HotspotSet:
      return evalHotspot(puzzle, draft)
    case PuzzleTypes.StatementRefutation:
      return evalStatement(puzzle, draft, index)
    case PuzzleTypes.SourceMapping:
    case PuzzleTypes.TraceMapping:
      return evalMapping(puzzle, draft)
    case PuzzleTypes.TimelineOrder:
      return evalTimeline(puzzle, draft)
    case PuzzleTypes.CrossCamera:
      return evalCrossCamera(puzzle, draft)
    case PuzzleTypes.Chronology:
      return evalChronology(puzzle, draft)
    case PuzzleTypes.ArgumentChoice:
      return evalArgumentChoice(puzzle, draft)
    case PuzzleTypes.DiffSelect:
      return evalDiffSelect(puzzle, draft)
    case PuzzleTypes.AssumptionLink:
      return evalAssumptionLink(puzzle, draft)
    case PuzzleTypes.MatrixGap:
      return evalMatrixGap(puzzle, draft)
    case PuzzleTypes.DeploymentChain:
      return evalDeploymentChain(puzzle, draft)
    case PuzzleTypes.Counterfactual:
      return evalCounterfactual(puzzle, draft)
    case PuzzleTypes.CausalGraph:
      return evalCausalGraph(puzzle, draft)
    case PuzzleTypes.ResponsibilityMatrix:
      return evalResponsibility(puzzle, draft)
    default:
      return { status: ResultStatus.Rejected, message: '', matchedSetIndex: -1 }
  }
}

function evalHotspot(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  const visited = new Set<string>()
  for (const card of draft.placedCards ?? []) {
    if (card.slot === 'region') visited.add(card.value)
  }
  if (!puzzle.regions || puzzle.regions.length === 0) return wrong(puzzle)
  for (const region of puzzle.regions) {
    if (!visited.has(region.regionId)) return partial('조사하지 않은 지점이 남아 있습니다.')
  }
  return solved(puzzle)
}

function evalStatement(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, index: ContentIndex): PuzzleEvalResult {
  if (draft.selectedStatementId !== puzzle.targetStatementId) {
    return wrong(puzzle, index.ui('ui.statement_wrong_target'))
  }
  if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
    const anyCorrect = puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))
    if (!anyCorrect) return partial(puzzle.partialText || '결론 선택이 필요합니다.')
  }
  const match = matchEvidenceSet(puzzle.sufficientEvidenceSets, draft.selectedEvidenceIds)
  if (match >= 0) return solved(puzzle, match)
  if (anyRequiredEvidenceSelected(puzzle.sufficientEvidenceSets, draft.selectedEvidenceIds)) {
    return partial(puzzle.partialText || '제출한 근거가 충분하지 않습니다.')
  }
  return wrong(puzzle, puzzle.wrongText || index.ui('ui.statement_unrelated'))
}

function evalMapping(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!puzzle.valuePairs || puzzle.valuePairs.length === 0) return wrong(puzzle)
  let correct = 0
  for (const pair of puzzle.valuePairs) {
    const expected = valueOf(puzzle.valuePairs, pair.key)
    const actual = getDraftValue(draft, pair.key)
    if (expected && expected === actual) correct++
  }
  if (correct === puzzle.valuePairs.length) return solved(puzzle)
  if (correct > 0) return partial('일부 항목이 맞습니다. 나머지도 확인하십시오.')
  return wrong(puzzle)
}

function valueOf(values: StringPairDto[] | null | undefined, key: string): string {
  return values?.find((pair) => pair.key === key)?.value ?? ''
}

function evalTimeline(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!sameOrder(puzzle.orderedIds, draft.placedCards, 'order')) return wrong(puzzle)
  if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
    if (puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))) return solved(puzzle)
    return partial('사건 배치는 맞습니다. 해석을 선택하십시오.')
  }
  return solved(puzzle)
}

function evalChronology(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!sameOrder(puzzle.orderedIds, draft.placedCards, 'order')) return wrong(puzzle)
  if (puzzle.correctChoiceIds) {
    if (puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))) return solved(puzzle)
  }
  return partial('시간 순서는 맞습니다. 최종 상태를 선택하십시오.')
}

function evalCrossCamera(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  const expectedWindow = puzzle.correctChoiceIds?.[0] ?? ''
  if (draft.comparisonWindowId !== expectedWindow) return wrong(puzzle, '같은 시간 구간의 자료를 비교하십시오.')
  const match = matchEvidenceSet(puzzle.sufficientEvidenceSets, draft.selectedEvidenceIds)
  if (match >= 0) return solved(puzzle, match)
  return partial('선택한 구간에 필요한 자료를 모두 연결하지 않았습니다.')
}

function evalArgumentChoice(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (puzzle.correctChoiceIds) {
    if (puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))) return solved(puzzle)
  }
  return wrong(puzzle)
}

function evalDiffSelect(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!puzzle.correctChoiceIds || puzzle.correctChoiceIds.length === 0) return wrong(puzzle)
  const chosen = puzzle.correctChoiceIds.filter((id) => hasChoice(draft, id)).length
  if (chosen === puzzle.correctChoiceIds.length) return solved(puzzle)
  if (chosen > 0) return partial('삭제된 블록은 맞습니다. 비용 설명도 함께 선택하십시오.')
  return wrong(puzzle)
}

function evalAssumptionLink(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (puzzle.correctChoiceIds) {
    const any = puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))
    if (!any) return wrong(puzzle)
  }
  const match = matchEvidenceSet(puzzle.sufficientEvidenceSets, draft.selectedEvidenceIds)
  if (match >= 0) return solved(puzzle, match)
  if (draft.selectedEvidenceIds && draft.selectedEvidenceIds.length > 0) {
    return partial('전제는 맞습니다. 실제 운영에서 깨진 근거를 연결하십시오.')
  }
  return partial('전제를 선택하고 반증 자료를 연결하십시오.')
}

function evalMatrixGap(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!puzzle.orderedIds || puzzle.orderedIds.length !== 4) return wrong(puzzle)
  const dims = ['sensor', 'overrideState', 'version', 'condition']
  for (let i = 0; i < dims.length; i++) {
    const slot = dims[i] ?? ''
    let value = ''
    for (const card of draft.placedCards ?? []) {
      if (card.slot === slot) value = card.value
    }
    if (value !== puzzle.orderedIds[i]) return partial('사고 조건에 해당하는 조합을 선택하십시오.')
  }
  if (puzzle.correctChoiceIds) {
    if (puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))) return solved(puzzle)
  }
  return partial('조합은 맞습니다. 결론을 선택하십시오.')
}

function evalDeploymentChain(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!sameOrder(puzzle.orderedIds, draft.placedCards, 'order')) {
    return partial('설계→설치→조건 이행을 각각 확인하십시오.')
  }
  if (puzzle.correctChoiceIds) {
    if (puzzle.correctChoiceIds.some((id) => hasChoice(draft, id))) return solved(puzzle)
  }
  return partial('연결은 맞습니다. 결론을 선택하십시오.')
}

function evalCounterfactual(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!puzzle.requiredCaseIds) return wrong(puzzle)
  const ran = puzzle.requiredCaseIds.filter((id) => (draft.ranCaseIds ?? []).includes(id)).length
  if (ran === puzzle.requiredCaseIds.length) return solved(puzzle)
  return partial('세 사례를 모두 실행해야 합니다.')
}

function evalCausalGraph(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  if (!puzzle.requiredEdges || puzzle.requiredEdges.length === 0) return wrong(puzzle)
  let connected = 0
  for (const edge of puzzle.requiredEdges) {
    if (hasEdge(draft, edge.from, edge.to, edge.kind)) connected++
  }
  if (connected === 0) return wrong(puzzle)
  if (connected < puzzle.requiredEdges.length) return partial('정답 연결 7개와 신호선 1개를 모두 완성하십시오.')
  if (!puzzle.edgeProofRequirements || puzzle.edgeProofRequirements.length === 0) return solved(puzzle)
  let proved = 0
  for (const req of puzzle.edgeProofRequirements) {
    const selection = findEdgeProof(draft, req.from, req.to, req.kind)
    if (selection && matchEvidenceSet(req.sufficientEvidenceSets, selection.evidenceIds) >= 0) proved++
  }
  if (proved === puzzle.edgeProofRequirements.length) return solved(puzzle)
  return partial(
    `간선 ${proved}/${puzzle.edgeProofRequirements.length}에 근거가 연결되었습니다. 나머지 간선에 근거를 붙이십시오.`,
  )
}

function hasEdge(draft: PuzzleDraftDto, from: string, to: string, kind: string): boolean {
  return (draft.edges ?? []).some((edge) => edge.from === from && edge.to === to && edge.kind === kind)
}

function findEdgeProof(draft: PuzzleDraftDto, from: string, to: string, kind: string) {
  return (draft.edgeProofs ?? []).find((proof) => proof.from === from && proof.to === to && proof.kind === kind) ?? null
}

function evalResponsibility(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto): PuzzleEvalResult {
  const requirements = puzzle.reportProofRequirements
  if (!requirements && (!puzzle.correctChoiceIds || puzzle.correctChoiceIds.length === 0)) return wrong(puzzle)

  const expected = new Map<string, string>()
  if (requirements) {
    for (const req of requirements) expected.set(req.item, req.section)
  }
  expected.set('no_intent_or_criminal_verdict', 'limit')

  const placed = new Map<string, string>()
  for (const item of draft.reportPlacements ?? []) placed.set(item.item, item.section)

  const forbidden = ['no_one_responsible', 'all_ai_innocent', 'intentional_murder_proven', 'publicity_caused_collision']
  for (const item of forbidden) {
    if (placed.has(item)) return wrong(puzzle)
  }

  let correctPlacements = 0
  for (const [item, section] of expected) {
    if (placed.get(item) === section) correctPlacements++
  }

  const proofsNeeded = requirements?.length ?? 0
  let proofsDone = 0
  if (requirements) {
    for (const req of requirements) {
      if (placed.get(req.item) !== req.section) continue
      const ids = findReportEvidence(draft, req.item)
      if (ids && matchEvidenceSet(req.sufficientEvidenceSets, ids) >= 0) proofsDone++
    }
  }
  const limitOk = placed.get('no_intent_or_criminal_verdict') === 'limit'
  let extraItems = false
  for (const item of placed.keys()) {
    if (!expected.has(item)) extraItems = true
  }

  if (correctPlacements === expected.size && proofsDone === proofsNeeded && limitOk && !extraItems) {
    return solved(puzzle)
  }
  if (correctPlacements === 0 && !limitOk) return wrong(puzzle)
  return partial(puzzle.partialText || '수정이 필요한 항목이 있습니다. 구역과 근거를 다시 확인하십시오.')
}

function findReportEvidence(draft: PuzzleDraftDto, item: string): string[] | null {
  return (draft.reportEvidence ?? []).find((entry) => entry.item === item)?.evidenceIds ?? null
}
