import type { ContentIndex } from '../content/contentIndex'
import type { BranchDefinitionDto, SceneDefinitionDto } from '../content/dto'
import type { GameState } from '../state/gameState'

export function isRequiredBranchResolved(scene: SceneDefinitionDto, state: GameState, index: ContentIndex): boolean {
  return (scene.advancingGroupIds ?? []).every((groupId) =>
    index.branchGroup(groupId).some((branch) => branch.advances && state.isTopicVisited(branch.id)),
  )
}

export function isAutomaticFamilyIntroduction(branchId: string): boolean {
  return branchId === 'b_s700_visited' || branchId === 'b_s700_unvisited'
}

export function branchResponseLines(branch: BranchDefinitionDto) {
  return branch.feedback
    ? [...(branch.dialogue ?? []), { speaker: '조사 기록', text: branch.feedback }]
    : branch.dialogue ?? []
}
