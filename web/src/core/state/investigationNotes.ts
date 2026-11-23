import type { ContentIndex } from '../content/contentIndex'
import type { GameState } from './gameState'

export function investigationNotes(state: GameState, index: ContentIndex): { title: string; detail: string }[] {
  const notes: { title: string; detail: string }[] = []
  const companyRequested = state.isTopicVisited('b_s130_origin')
  const passengerVisit = state.visitedScenes.indexOf('s120')
  const companyVisit = state.visitedScenes.indexOf('s130')
  if (companyRequested && state.getBranchValue('s130_warning_presented') === 'true' &&
      passengerVisit >= 0 && passengerVisit < companyVisit && state.owns('ev05')) {
    notes.push({ title: '회사 면담 · 차내 경고를 먼저 제시함', detail: '경고가 켜졌다는 사실을 확인했다. 당시 무엇을 인식했는지는 요청한 원본 기록과 출처를 대조한다.' })
  } else if (companyRequested) {
    notes.push({
      title: '회사 면담 · 원본과 출처부터 요청함',
      detail: state.owns('ev05')
        ? '면담 뒤 휴대전화 영상을 확보했다. 차내 경고가 있었지만 어떤 대상을 인식했는지는 원본 기록으로 확인한다.'
        : '발표 분석표가 만들어진 조건과 당시 차 안의 반응을 별도로 확인한다. 승객이 본 자료는 아직 확보하지 못했다.',
    })
  }
  if (state.isTopicVisited('b_s210_crosscheck')) {
    notes.push({ title: '제동 경로 · 영상과 입력을 교차 확인함', detail: '두 영상에서 접근 차량이 확인되지 않았다. 조정기가 사용한 후방 입력의 출처를 확인할 필요가 있다.' })
  }
  if (state.isTopicVisited('b_s220_trace')) {
    notes.push({ title: '후방 기록 · 실행 경로부터 확인함', detail: '후방 입력이 제동 한계에 반영됐으므로 영상이 그 입력을 뒷받침하는지 비교한다.' })
  }
  for (const id of state.solvedPuzzles) {
    const puzzle = index.puzzle(id)
    if (!puzzle?.successText) continue
    const draft = state.getDraft(id, false)
    const proof = puzzle.sufficientEvidenceSets.find((set) =>
      set.requiredIds.every((evidenceId) => draft?.selectedEvidenceIds.includes(evidenceId)))
    const sources = proof?.requiredIds.map((evidenceId) => index.evidenceDef(evidenceId)?.title).filter(Boolean)
    notes.push({
      title: sources?.length ? `입증 근거 · ${sources.join(', ')}` : '조사에서 확인한 사실',
      detail: puzzle.successText,
    })
  }
  return notes
}
