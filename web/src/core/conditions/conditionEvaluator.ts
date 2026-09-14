import type { ConditionDto } from '../content/dto'
import type { ContentIndex } from '../content/contentIndex'
import type { GameState } from '../state/gameState'

export const ConditionKind = {
  SceneCompleted: 'SceneCompleted',
  EvidenceOwned: 'EvidenceOwned',
  EvidenceRead: 'EvidenceRead',
  PuzzleSolved: 'PuzzleSolved',
  TopicVisited: 'TopicVisited',
  BranchEquals: 'BranchEquals',
  PostDialogueComplete: 'PostDialogueComplete',
  AllRequiredChildrenComplete: 'AllRequiredChildrenComplete',
} as const

const KNOWN_KINDS = new Set<string>(Object.values(ConditionKind))

export function isKnownConditionKind(kind: string): boolean {
  return KNOWN_KINDS.has(kind)
}

/** 조건 판정. 알 수 없는 조건·빈 ID는 fail closed. */
export function evaluateCondition(
  condition: ConditionDto | null | undefined,
  state: GameState,
  index: ContentIndex,
): boolean {
  if (!condition || !condition.kind || !condition.id) return false
  switch (condition.kind) {
    case ConditionKind.SceneCompleted:
      return state.isSceneCompleted(condition.id)
    case ConditionKind.EvidenceOwned:
      return state.owns(condition.id)
    case ConditionKind.EvidenceRead:
      return state.hasRead(condition.id)
    case ConditionKind.PuzzleSolved:
      return state.isPuzzleSolved(condition.id)
    case ConditionKind.TopicVisited:
    case ConditionKind.BranchEquals:
      return state.isTopicVisited(condition.id)
    case ConditionKind.PostDialogueComplete:
      return state.isPostDone(condition.id) || state.isSceneCompleted(condition.id)
    case ConditionKind.AllRequiredChildrenComplete:
      return allRequiredChildrenComplete(condition.id, state, index)
    default:
      return false
  }
}

export function allConditionsMet(
  conditions: ConditionDto[] | null | undefined,
  state: GameState,
  index: ContentIndex,
): boolean {
  if (!conditions) return true
  for (const condition of conditions) {
    if (!evaluateCondition(condition, state, index)) return false
  }
  return true
}

export function allRequiredChildrenComplete(hubSceneId: string, state: GameState, index: ContentIndex): boolean {
  const hub = index.scene(hubSceneId)
  if (!hub || !hub.childSceneIds || hub.childSceneIds.length === 0) return false
  for (const childId of hub.childSceneIds) {
    if (!state.isSceneCompleted(childId)) return false
  }
  return true
}

export function remainingRequiredChildren(hubSceneId: string, state: GameState, index: ContentIndex): string[] {
  const result: string[] = []
  const hub = index.scene(hubSceneId)
  if (!hub?.childSceneIds) return result
  for (const childId of hub.childSceneIds) {
    if (!state.isSceneCompleted(childId)) result.push(childId)
  }
  return result
}
