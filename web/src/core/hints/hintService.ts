import type { HintDefinitionDto } from '../content/dto'
import type { ContentIndex } from '../content/contentIndex'
import { GameState, Phases } from '../state/gameState'

export interface HintContext {
  scopeType: string
  scopeId: string
  title: string
  sceneTip: string
  nextAction: string
  levels: HintDefinitionDto[]
}

/** 현재 문맥의 잠금된 힌트만 반환한다. 미래 정답을 전체 검색해 노출하지 않는다. */
export class HintService {
  private readonly index: ContentIndex

  constructor(index: ContentIndex) {
    this.index = index
  }

  resolve(state: GameState): HintContext {
    const context: HintContext = {
      scopeType: 'scene',
      scopeId: state.currentSceneId,
      title: '',
      sceneTip: '',
      nextAction: '',
      levels: [],
    }
    const scene = this.index.scene(state.currentSceneId)
    context.title = scene ? scene.id : ''
    context.sceneTip = scene ? scene.tipText : ''

    switch (state.phase) {
      case Phases.Interaction: {
        const puzzle = this.index.puzzle(state.activePuzzleId)
        if (puzzle) {
          context.scopeType = 'puzzle'
          context.scopeId = puzzle.id
          context.title = puzzle.id
          for (const hint of this.index.allHints()) {
            if (hint.scopeType === 'puzzle' && hint.scopeId === puzzle.id) context.levels.push(hint)
          }
          context.levels.sort((a, b) => a.level - b.level)
        }
        break
      }
      case Phases.HubActive: {
        const hub = this.index.scene(state.currentSceneId)
        if (hub?.childSceneIds) {
          for (const childId of hub.childSceneIds) {
            if (!state.isSceneCompleted(childId)) {
              const child = this.index.scene(childId)
              context.nextAction = child ? child.id : childId
              break
            }
          }
        }
        break
      }
      case Phases.ReadCheck: {
        const current = this.index.scene(state.currentSceneId)
        if (current?.requiredReadEvidenceIds) {
          for (const evidenceId of current.requiredReadEvidenceIds) {
            if (!state.hasRead(evidenceId)) {
              const evidence = this.index.evidenceDef(evidenceId)
              context.nextAction = evidence ? evidence.title : evidenceId
              break
            }
          }
        }
        break
      }
      case Phases.BranchChoice: {
        const current = this.index.scene(state.currentSceneId)
        if (current?.advancingGroupIds) {
          for (const groupId of current.advancingGroupIds) {
            const group = this.index.branchGroup(groupId)
            for (const branch of group) {
              if (!state.isTopicVisited(branch.id)) {
                context.nextAction = branch.label
                break
              }
            }
          }
        }
        break
      }
      case Phases.ReadyToExit:
      case Phases.Completed: {
        const current = this.index.scene(state.currentSceneId)
        if (current?.transitions && current.transitions.length > 0) {
          context.nextAction = current.transitions[0]?.target ?? ''
        }
        if (context.levels.length === 0) {
          const hint = this.sceneHint(state.currentSceneId)
          if (hint) context.levels.push(hint)
        }
        break
      }
      default:
        context.nextAction = state.currentSceneId
        break
    }
    if (context.levels.length === 0) {
      const sceneHint = this.sceneHint(state.currentSceneId)
      if (sceneHint) context.levels.push(sceneHint)
    }
    return context
  }

  sceneHint(sceneId: string): HintDefinitionDto | null {
    for (const hint of this.index.allHints()) {
      if (hint.scopeType === 'scene' && hint.scopeId === sceneId && hint.level === 1) return hint
    }
    return null
  }
}
