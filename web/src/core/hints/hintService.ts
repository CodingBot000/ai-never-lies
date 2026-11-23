import type { HintDefinitionDto } from '../content/dto'
import type { ContentIndex } from '../content/contentIndex'
import { GameState, Phases } from '../state/gameState'
import { PUZZLE_OBJECTIVES } from './puzzleObjectives'

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
    context.sceneTip = scene ? '대화를 읽고 새로 확인한 사실을 살펴보세요.' : '새 조사를 시작하거나 저장된 사건을 이어가세요.'

    switch (state.phase) {
      case Phases.Interaction: {
        const puzzle = this.index.puzzle(state.activePuzzleId)
        if (puzzle) {
          context.scopeType = 'puzzle'
          context.scopeId = puzzle.id
          context.title = puzzle.id
          context.sceneTip = PUZZLE_OBJECTIVES[puzzle.id] ?? '현재 조사에서 필요한 근거를 연결하세요.'
          for (const hint of this.index.allHints()) {
            if (hint.scopeType === 'puzzle' && hint.scopeId === puzzle.id) context.levels.push(hint)
          }
          context.levels.sort((a, b) => a.level - b.level)
        }
        break
      }
      case Phases.HubActive: {
        context.sceneTip = '해결하고 싶은 질문에 해당하는 조사 장소를 고르세요.'
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
        context.sceneTip = '증거책에서 아직 읽지 않은 필수 자료를 열어 보세요.'
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
        context.sceneTip = '확인하고 싶은 질문이나 답변을 고르세요.'
        break
      }
      case Phases.ReadyToExit:
      case Phases.Completed: {
        context.sceneTip = '확인한 내용을 정리하고 다음 단계로 이동하세요.'
        break
      }
      default:
        context.nextAction = state.currentSceneId
        break
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
