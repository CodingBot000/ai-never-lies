import { CommandDispatcher } from '../core/commands/commandDispatcher'
import { CommandKinds, Origins, ResultStatus, type GameCommand } from '../core/commands/commandTypes'
import { planTestAssist } from '../core/commands/testAssistPlanner'
import { allConditionsMet } from '../core/conditions/conditionEvaluator'
import type { ContentIndex } from '../core/content/contentIndex'
import { GameState, Phases } from '../core/state/gameState'

let counter = 0

function send(dispatcher: CommandDispatcher, type: string, options: { targetId?: string; ids?: string[]; slot?: string; value?: string } = {}) {
  counter++
  const command: GameCommand = {
    commandId: `dev_${counter}`,
    sequence: dispatcher.state.lastCommandSequence + 1,
    sceneId: dispatcher.state.currentSceneId,
    origin: Origins.TestAssist,
    type,
    ...options,
  }
  return dispatcher.execute(command)
}

/**
 * 개발 전용: 현재 씬을 자동으로 끝까지 진행한다. (fixture jump + 테스트 지원 조합)
 * 정상 명령 경로만 사용하며 solved 플래그를 직접 세팅하지 않는다.
 */
export function advanceCurrentScene(index: ContentIndex, state: GameState, dispatcher: CommandDispatcher): string {
  const initialScene = state.currentSceneId
  const trace: string[] = []
  for (let step = 0; step < 2000; step++) {
    if (state.currentSceneId !== initialScene) return `scene_changed:${state.currentSceneId}`
    trace.push(`${state.currentSceneId}/${state.phase}`)
    if (trace.length > 40) trace.shift()

    switch (state.phase) {
      case Phases.PreDialogue:
      case Phases.PostDialogue:
        if (send(dispatcher, CommandKinds.AdvanceDialogue).status === ResultStatus.Rejected) return 'stuck:dialogue'
        break
      case Phases.Interaction: {
        const plan = planTestAssist(index, state)
        if (!plan.canRun) return `blocked:${plan.blocker}`
        for (const command of plan.commands) {
          if (dispatcher.execute(command).status === ResultStatus.Rejected) return 'stuck:assist'
        }
        break
      }
      case Phases.BranchChoice: {
        if (!chooseAdvancing(index, state, dispatcher)) {
          if (send(dispatcher, CommandKinds.ContinueScene).status === ResultStatus.Rejected) return 'stuck:branch'
        }
        break
      }
      case Phases.ReadCheck: {
        openRequiredReads(state, dispatcher)
        if (send(dispatcher, CommandKinds.ContinueScene).status === ResultStatus.Rejected) return 'stuck:read'
        break
      }
      case Phases.ReadyToExit:
      case Phases.Completed: {
        if (dispatcher.transitionAvailable().available) {
          if (send(dispatcher, CommandKinds.ContinueScene).status === ResultStatus.Rejected) return 'stuck:transition'
          break
        }
        const scene = index.scene(state.currentSceneId)
        if (scene && (scene.screenType === 'hub' || (scene.childSceneIds?.length ?? 0) > 0)) {
          // hub: 자식 중 진입 가능한 첫 씬으로
          const child = [...(scene.childSceneIds ?? []), ...(scene.optionalChildSceneIds ?? [])].find((childId) => {
            const candidate = index.scene(childId)
            return candidate && !state.isSceneCompleted(childId) && allConditionsMet(candidate.entryConditions, state, index)
          })
          if (child) {
            if (send(dispatcher, CommandKinds.EnterScene, { targetId: child }).status === ResultStatus.Rejected) return 'stuck:hub'
            break
          }
          return 'hub_complete'
        }
        return `scene_complete:${state.currentSceneId}`
      }
      case Phases.HubActive: {
        if (dispatcher.transitionAvailable().available) {
          if (send(dispatcher, CommandKinds.ContinueScene).status === ResultStatus.Rejected) return 'stuck:hub-transition'
          break
        }
        const scene = index.scene(state.currentSceneId)
        const child = [...(scene?.childSceneIds ?? []), ...(scene?.optionalChildSceneIds ?? [])].find((childId) => {
          const candidate = index.scene(childId)
          return candidate && !state.isSceneCompleted(childId) && allConditionsMet(candidate.entryConditions, state, index)
        })
        if (child) {
          if (send(dispatcher, CommandKinds.EnterScene, { targetId: child }).status === ResultStatus.Rejected) return 'stuck:hub'
          break
        }
        return 'hub_complete'
      }
      default:
        return `unhandled:${state.phase}`
    }
  }
  return `limit:${trace.join('>')}`
}

function chooseAdvancing(index: ContentIndex, state: GameState, dispatcher: CommandDispatcher): boolean {
  const scene = index.scene(state.currentSceneId)
  for (const groupId of scene?.branchGroupIds ?? []) {
    for (const branch of index.branchGroup(groupId)) {
      if (!branch.advances || state.isTopicVisited(branch.id)) continue
      if (!allConditionsMet(branch.condition, state, index)) continue
      if (send(dispatcher, CommandKinds.ChooseBranch, { targetId: branch.id }).status === ResultStatus.Rejected) {
        return false
      }
      return true
    }
  }
  return false
}

function openRequiredReads(state: GameState, dispatcher: CommandDispatcher): void {
  const scene = dispatcher.index.scene(state.currentSceneId)
  for (const evidenceId of scene?.requiredReadEvidenceIds ?? []) {
    if (state.hasRead(evidenceId)) continue
    send(dispatcher, CommandKinds.OpenEvidence, { targetId: evidenceId })
  }
}
