import type { EvidenceSetDto, PuzzleDefinitionDto } from '../content/dto'
import type { ContentIndex } from '../content/contentIndex'
import { hasRequiredEvidence, PuzzleTypes } from '../puzzles/puzzleEvaluator'
import { GameState, Phases } from '../state/gameState'
import { CommandKinds, Origins, type GameCommand } from './commandTypes'

export interface AssistPlan {
  canRun: boolean
  blocker: string
  commands: GameCommand[]
}

type Maker = (type: string) => GameCommand

/**
 * 개발 모드 정답 행동 실행 계획. solved 플래그를 직접 세팅하지 않고
 * 정상 사용자 명령 시퀀스를 만들어 반환한다.
 */
export function planTestAssist(index: ContentIndex, state: GameState): AssistPlan {
  const plan: AssistPlan = { canRun: false, blocker: '', commands: [] }
  if (state.phase !== Phases.Interaction || !state.activePuzzleId) {
    plan.blocker = 'no_active_puzzle'
    return plan
  }
  const puzzle = index.puzzle(state.activePuzzleId)
  if (!puzzle) {
    plan.blocker = 'unknown_puzzle'
    return plan
  }
  if (!hasRequiredEvidence(puzzle, state)) {
    plan.blocker = 'missing_required_evidence'
    return plan
  }

  let sequence = state.lastCommandSequence
  let counter = 0
  const make: Maker = (type) => {
    counter++
    return {
      commandId: `assist_${puzzle.id}_${counter}`,
      sequence: ++sequence,
      sceneId: state.currentSceneId,
      origin: Origins.TestAssist,
      type,
    }
  }

  switch (puzzle.type) {
    case PuzzleTypes.HotspotSet:
      for (const region of puzzle.regions ?? []) {
        const command = make(CommandKinds.InspectRegion)
        command.targetId = region.regionId
        plan.commands.push(command)
      }
      break
    case PuzzleTypes.StatementRefutation: {
      const statement = make(CommandKinds.SelectStatement)
      statement.targetId = puzzle.targetStatementId
      plan.commands.push(statement)
      const evidence = make(CommandKinds.SelectEvidence)
      const set = firstSet(puzzle.sufficientEvidenceSets)
      if (!set) return blocked(plan, 'missing_solution')
      evidence.ids = set
      plan.commands.push(evidence)
      if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
        const choice = make(CommandKinds.SelectChoice)
        choice.targetId = puzzle.correctChoiceIds[0]
        plan.commands.push(choice)
      }
      plan.commands.push(make(CommandKinds.PresentEvidence))
      break
    }
    case PuzzleTypes.SourceMapping:
    case PuzzleTypes.TraceMapping:
      return mappingPlan(plan, puzzle, make)
    case PuzzleTypes.TimelineOrder: {
      if (!puzzle.orderedIds) return blocked(plan, 'missing_solution')
      for (const id of puzzle.orderedIds) {
        const place = make(CommandKinds.PlaceCard)
        place.slot = 'order'
        place.value = id
        plan.commands.push(place)
      }
      if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
        const choice = make(CommandKinds.SelectChoice)
        choice.targetId = puzzle.correctChoiceIds[0]
        plan.commands.push(choice)
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.CrossCamera: {
      const window = make(CommandKinds.SelectComparisonWindow)
      window.targetId = puzzle.correctChoiceIds?.[0] ?? ''
      plan.commands.push(window)
      const evidence = make(CommandKinds.SelectEvidence)
      const set = firstSet(puzzle.sufficientEvidenceSets)
      if (!set) return blocked(plan, 'missing_solution')
      evidence.ids = set
      plan.commands.push(evidence)
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.Chronology: {
      if (!puzzle.orderedIds) return blocked(plan, 'missing_solution')
      for (const id of puzzle.orderedIds) {
        const place = make(CommandKinds.PlaceCard)
        place.slot = 'order'
        place.value = id
        plan.commands.push(place)
      }
      if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
        const choice = make(CommandKinds.SelectChoice)
        choice.targetId = puzzle.correctChoiceIds[0]
        plan.commands.push(choice)
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.ArgumentChoice: {
      const choice = make(CommandKinds.SelectChoice)
      choice.targetId = puzzle.correctChoiceIds?.[0] ?? ''
      plan.commands.push(choice)
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.DiffSelect: {
      if (!puzzle.correctChoiceIds) return blocked(plan, 'missing_solution')
      for (const id of puzzle.correctChoiceIds) {
        const choice = make(CommandKinds.SelectChoice)
        choice.targetId = id
        plan.commands.push(choice)
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.AssumptionLink: {
      const choice = make(CommandKinds.SelectChoice)
      choice.targetId = puzzle.correctChoiceIds?.[0] ?? ''
      plan.commands.push(choice)
      const evidence = make(CommandKinds.SelectEvidence)
      const set = firstSet(puzzle.sufficientEvidenceSets)
      if (!set) return blocked(plan, 'missing_solution')
      evidence.ids = set
      plan.commands.push(evidence)
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.MatrixGap: {
      const dims = ['sensor', 'overrideState', 'version', 'condition']
      if (!puzzle.orderedIds || puzzle.orderedIds.length !== dims.length) {
        return blocked(plan, 'missing_solution')
      }
      for (let i = 0; i < dims.length; i++) {
        const place = make(CommandKinds.PlaceCard)
        place.slot = dims[i] ?? ''
        place.value = puzzle.orderedIds[i] ?? ''
        plan.commands.push(place)
      }
      if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
        const choice = make(CommandKinds.SelectChoice)
        choice.targetId = puzzle.correctChoiceIds[0]
        plan.commands.push(choice)
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.DeploymentChain: {
      if (!puzzle.orderedIds) return blocked(plan, 'missing_solution')
      for (const id of puzzle.orderedIds) {
        const place = make(CommandKinds.PlaceCard)
        place.slot = 'order'
        place.value = id
        plan.commands.push(place)
      }
      if (puzzle.correctChoiceIds && puzzle.correctChoiceIds.length > 0) {
        const choice = make(CommandKinds.SelectChoice)
        choice.targetId = puzzle.correctChoiceIds[0]
        plan.commands.push(choice)
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.Counterfactual: {
      if (!puzzle.requiredCaseIds) return blocked(plan, 'missing_solution')
      for (const id of puzzle.requiredCaseIds) {
        const run = make(CommandKinds.RunCounterfactualCase)
        run.targetId = id
        plan.commands.push(run)
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.CausalGraph: {
      if (!puzzle.requiredEdges) return blocked(plan, 'missing_solution')
      for (const edge of puzzle.requiredEdges) {
        const connect = make(CommandKinds.ConnectNodes)
        connect.ids = [edge.from, edge.to]
        connect.targetId = edge.kind
        plan.commands.push(connect)
      }
      if (puzzle.edgeProofRequirements) {
        for (const proof of puzzle.edgeProofRequirements) {
          const select = make(CommandKinds.SelectEvidence)
          select.targetId = `${proof.from}|${proof.to}|${proof.kind}`
          const set = firstSet(proof.sufficientEvidenceSets)
          if (!set) return blocked(plan, 'missing_solution')
          select.ids = set
          plan.commands.push(select)
        }
      }
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    case PuzzleTypes.ResponsibilityMatrix: {
      if (!puzzle.reportProofRequirements) return blocked(plan, 'missing_solution')
      for (const proof of puzzle.reportProofRequirements) {
        const place = make(CommandKinds.PlaceCard)
        place.slot = proof.section
        place.value = proof.item
        plan.commands.push(place)
        const select = make(CommandKinds.SelectEvidence)
        select.targetId = proof.item
        const set = firstSet(proof.sufficientEvidenceSets)
        if (!set) return blocked(plan, 'missing_solution')
        select.ids = set
        plan.commands.push(select)
      }
      const limit = make(CommandKinds.PlaceCard)
      limit.slot = 'limit'
      limit.value = 'no_intent_or_criminal_verdict'
      plan.commands.push(limit)
      plan.commands.push(make(CommandKinds.SubmitPuzzle))
      break
    }
    default:
      return blocked(plan, 'unsupported_puzzle_type')
  }

  plan.canRun = plan.commands.length > 0
  if (!plan.canRun && !plan.blocker) plan.blocker = 'empty_plan'
  return plan
}

function blocked(plan: AssistPlan, blocker: string): AssistPlan {
  plan.blocker = blocker
  plan.canRun = false
  return plan
}

function mappingPlan(plan: AssistPlan, puzzle: PuzzleDefinitionDto, make: Maker): AssistPlan {
  if (!puzzle.valuePairs) return blocked(plan, 'missing_solution')
  for (const pair of puzzle.valuePairs) {
    const place = make(CommandKinds.PlaceCard)
    place.slot = pair.key
    place.value = pair.value
    plan.commands.push(place)
  }
  plan.commands.push(make(CommandKinds.SubmitPuzzle))
  plan.canRun = plan.commands.length > 0
  return plan
}

function firstSet(sets: EvidenceSetDto[] | null | undefined): string[] | null {
  if (!sets) return null
  for (const set of sets) {
    if (set?.requiredIds && set.requiredIds.length > 0) return set.requiredIds
  }
  return null
}
