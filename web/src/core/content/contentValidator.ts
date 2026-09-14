import { isKnownConditionKind } from '../conditions/conditionEvaluator'
import { isKnownPuzzleType, PuzzleTypes } from '../puzzles/puzzleEvaluator'
import type { ContentIndex } from './contentIndex'
import { FrozenIds } from './frozenIds'

/**
 * 정적 콘텐츠 검증. (C# ANL.Core.Validation.ContentValidatorCore 이식)
 * 반환된 문자열 배열이 비어 있으면 통과다.
 */
export function validateContent(index: ContentIndex, assetIds: string[] | null = null): string[] {
  const errors: string[] = []
  const evidenceSet = new Set<string>(FrozenIds.Evidence)

  if (assetIds) {
    const assetSet = new Set(assetIds)
    for (const [sceneId, scene] of index.scenes) {
      if (scene.backgroundAssetId && !assetSet.has(scene.backgroundAssetId)) {
        errors.push(`scene ${sceneId}: unknown background asset ${scene.backgroundAssetId}`)
      }
    }
    for (const [dialogueId, line] of index.dialogues) {
      if (line.portraitAssetId && !assetSet.has(line.portraitAssetId)) {
        errors.push(`dialogue ${dialogueId}: unknown portrait asset ${line.portraitAssetId}`)
      }
    }
    for (const [evidenceId, evidence] of index.evidence) {
      if (evidence.imageAssetId && !assetSet.has(evidence.imageAssetId)) {
        errors.push(`evidence ${evidenceId}: unknown image asset ${evidence.imageAssetId}`)
      }
    }
  }

  if (index.episode.id !== 'ep01') errors.push(`episode id must be ep01, got ${index.episode.id}`)
  if (!index.episode.startSceneId || !index.hasScene(index.episode.startSceneId)) {
    errors.push('episode startSceneId is missing or unknown')
  }
  if (!index.episode.requiredPuzzleIds || index.episode.requiredPuzzleIds.length !== 20) {
    errors.push('episode requiredPuzzleIds must contain 20 puzzles')
  } else {
    for (const id of FrozenIds.Puzzles) {
      if (!index.episode.requiredPuzzleIds.includes(id)) {
        errors.push(`episode requiredPuzzleIds missing ${id}`)
      }
    }
  }

  checkFrozenSet('scene', FrozenIds.Scenes, [...index.scenes.keys()], errors)
  checkFrozenSet('evidence', FrozenIds.Evidence, [...index.evidence.keys()], errors)
  checkFrozenSet('puzzle', FrozenIds.Puzzles, [...index.puzzles.keys()], errors)

  // scene references
  for (const [sceneId, scene] of index.scenes) {
    checkDialogueRefs(index, sceneId, scene.preDialogueIds, errors)
    checkDialogueRefs(index, sceneId, scene.postDialogueIds, errors)
    if (scene.grants) {
      for (const grant of scene.grants) {
        if (!evidenceSet.has(grant.evidenceId)) {
          errors.push(`scene ${sceneId}: grant references unknown evidence ${grant.evidenceId}`)
        }
        if (grant.trigger === 'dialogue' && !index.dialogues.has(grant.atDialogueId ?? '')) {
          errors.push(`scene ${sceneId}: dialogue grant references unknown line ${grant.atDialogueId}`)
        }
      }
    }
    if (scene.requiredReadEvidenceIds) {
      for (const id of scene.requiredReadEvidenceIds) {
        if (!evidenceSet.has(id)) errors.push(`scene ${sceneId}: requiredRead references unknown evidence ${id}`)
      }
    }
    if (scene.interactionIds) {
      for (const id of scene.interactionIds) {
        if (!index.puzzles.has(id)) errors.push(`scene ${sceneId}: unknown puzzle ${id}`)
      }
    }
    if (scene.transitions) {
      for (const transition of scene.transitions) {
        if (!index.hasScene(transition.target)) {
          errors.push(`scene ${sceneId}: transition targets unknown scene ${transition.target}`)
        }
        if (transition.conditions) {
          for (const condition of transition.conditions) {
            if (!isKnownConditionKind(condition.kind)) {
              errors.push(`scene ${sceneId}: unknown condition kind ${condition.kind}`)
            }
          }
        }
      }
    }
    if (scene.branchGroupIds) {
      for (const groupId of scene.branchGroupIds) {
        const group = index.branchGroup(groupId)
        if (group.length === 0) errors.push(`scene ${sceneId}: branch group ${groupId} is empty`)
        for (const branch of group) {
          if (branch.sceneId !== sceneId) {
            errors.push(`branch ${branch.id}: sceneId ${branch.sceneId} does not match group scene ${sceneId}`)
          }
        }
      }
    }
    if (scene.advancingGroupIds) {
      for (const groupId of scene.advancingGroupIds) {
        const group = index.branchGroup(groupId)
        const hasAdvancing = group.some((branch) => branch.advances)
        if (!hasAdvancing) errors.push(`scene ${sceneId}: advancing group ${groupId} has no advancing branch`)
      }
    }
    if (scene.childSceneIds) {
      for (const child of scene.childSceneIds) {
        if (!index.hasScene(child)) errors.push(`scene ${sceneId}: unknown child scene ${child}`)
      }
    }
  }

  // branch references
  for (const [branchId, branch] of index.branches) {
    if (!index.hasScene(branch.sceneId)) errors.push(`branch ${branchId}: unknown scene ${branch.sceneId}`)
    if (branch.effects) {
      for (const effect of branch.effects) {
        if (effect.kind === 'GrantEvidence' && !evidenceSet.has(effect.id)) {
          errors.push(`branch ${branchId}: grant references unknown evidence ${effect.id}`)
        }
        if (effect.kind !== 'GrantEvidence' && effect.kind !== 'SetBranch' && effect.kind !== 'Feedback') {
          errors.push(`branch ${branchId}: unknown effect kind ${effect.kind}`)
        }
      }
    }
  }

  // puzzle references and structure
  for (const [puzzleId, puzzle] of index.puzzles) {
    if (!index.hasScene(puzzle.sceneId)) errors.push(`puzzle ${puzzleId}: unknown scene ${puzzle.sceneId}`)
    if (!isKnownPuzzleType(puzzle.type)) errors.push(`puzzle ${puzzleId}: unknown type ${puzzle.type}`)
    if (puzzle.requiredEvidenceIds) {
      for (const id of puzzle.requiredEvidenceIds) {
        if (!evidenceSet.has(id)) errors.push(`puzzle ${puzzleId}: unknown required evidence ${id}`)
      }
    }
    if (puzzle.targetStatementId && !index.testimonies.has(puzzle.targetStatementId)) {
      errors.push(`puzzle ${puzzleId}: unknown target statement ${puzzle.targetStatementId}`)
    }
    if (puzzle.type === PuzzleTypes.StatementRefutation && !puzzle.targetStatementId) {
      errors.push(`puzzle ${puzzleId}: statement_refutation requires a target statement`)
    }
    if (puzzle.sufficientEvidenceSets) {
      for (const set of puzzle.sufficientEvidenceSets) {
        if (set?.requiredIds) {
          for (const id of set.requiredIds) {
            if (!evidenceSet.has(id)) {
              errors.push(`puzzle ${puzzleId}: sufficient set references unknown evidence ${id}`)
            }
          }
        }
      }
    }
    if (puzzle.requiredEdges) {
      for (const edge of puzzle.requiredEdges) {
        if (!hasNode(index, edge.from) || !hasNode(index, edge.to)) {
          errors.push(`puzzle ${puzzleId}: edge references unknown node ${edge.from}->${edge.to}`)
        }
        if (edge.kind !== 'causal' && edge.kind !== 'signal') {
          errors.push(`puzzle ${puzzleId}: edge ${edge.from}->${edge.to} has unknown kind ${edge.kind}`)
        }
      }
    }
    if (!puzzle.successText) errors.push(`puzzle ${puzzleId}: success text is empty`)
    const scene = index.scene(puzzle.sceneId)
    if (scene && (!scene.interactionIds || !scene.interactionIds.includes(puzzleId))) {
      errors.push(`puzzle ${puzzleId}: scene ${puzzle.sceneId} does not list it as interaction`)
    }
  }

  // testimony references
  for (const [testimonyId, testimony] of index.testimonies) {
    if (!index.hasScene(testimony.sceneId)) errors.push(`testimony ${testimonyId}: unknown scene ${testimony.sceneId}`)
    if (!testimony.text) errors.push(`testimony ${testimonyId}: empty text`)
  }

  // hints coverage
  let sceneHints = 0
  let puzzleHintLevels = 0
  const puzzleHintIds = new Map<string, Set<number>>()
  for (const [hintId, hint] of index.hints) {
    if (hint.scopeType === 'scene') {
      sceneHints++
      if (!index.hasScene(hint.scopeId)) errors.push(`hint ${hintId}: unknown scene ${hint.scopeId}`)
    } else if (hint.scopeType === 'puzzle') {
      puzzleHintLevels++
      if (!index.puzzles.has(hint.scopeId)) errors.push(`hint ${hintId}: unknown puzzle ${hint.scopeId}`)
      let levels = puzzleHintIds.get(hint.scopeId)
      if (!levels) {
        levels = new Set<number>()
        puzzleHintIds.set(hint.scopeId, levels)
      }
      levels.add(hint.level)
    }
    if (!hint.text) errors.push(`hint ${hintId}: empty text`)
  }
  if (sceneHints < 43) errors.push(`scene hints must cover 43 scenes, got ${sceneHints}`)
  for (const id of FrozenIds.Puzzles) {
    const levels = puzzleHintIds.get(id)
    if (!levels || levels.size < 3) errors.push(`puzzle ${id}: missing H1/H2/H3 hints`)
  }
  if (puzzleHintLevels < 60) errors.push(`puzzle hints must be at least 60, got ${puzzleHintLevels}`)

  // evidence acquisition coverage
  const granted = new Set<string>()
  for (const scene of index.scenes.values()) {
    if (scene.grants) {
      for (const grant of scene.grants) granted.add(grant.evidenceId)
    }
    if (scene.interactionIds) {
      for (const puzzleId of scene.interactionIds) {
        const puzzle = index.puzzle(puzzleId)
        if (puzzle?.regions) {
          for (const region of puzzle.regions) {
            if (region.evidenceId) granted.add(region.evidenceId)
          }
        }
      }
    }
  }
  for (const branch of index.branches.values()) {
    if (branch.effects) {
      for (const effect of branch.effects) {
        if (effect.kind === 'GrantEvidence') granted.add(effect.id)
      }
    }
  }
  for (const id of FrozenIds.Evidence) {
    if (!granted.has(id)) errors.push(`evidence ${id}: never granted by any scene, branch, or region`)
  }

  // specific contract checks
  const s150 = index.scene('s150')
  if (s150) {
    let ev08 = false
    let ev09 = false
    for (const grant of s150.grants ?? []) {
      if (grant.evidenceId === 'ev08' && grant.trigger === 'post_start') ev08 = true
      if (grant.evidenceId === 'ev09' && grant.trigger === 'post_start') ev09 = true
    }
    if (!ev08 || !ev09) errors.push('s150 must grant ev08/ev09 on post_start')
  }
  const s640 = index.scene('s640')
  if (s640?.entryConditions) {
    for (const condition of s640.entryConditions) {
      if (condition.kind === 'PuzzleSolved' && condition.id === 'p20') {
        errors.push('s640 entry must not require p20 (circular lock)')
      }
    }
  }
  const accept = index.branch('b_s530_accept')
  if (!accept?.effects?.some((effect) => effect.kind === 'GrantEvidence' && effect.id === 'ev35')) {
    errors.push('b_s530_accept must grant ev35')
  }

  // text completeness
  for (const [dialogueId, line] of index.dialogues) {
    if (!line.text) errors.push(`dialogue ${dialogueId}: empty text`)
  }
  for (const [evidenceId, evidence] of index.evidence) {
    if (!evidence.body) errors.push(`evidence ${evidenceId}: empty body`)
    if (!evidence.proves) errors.push(`evidence ${evidenceId}: empty proves`)
    if (!evidence.limits) errors.push(`evidence ${evidenceId}: empty limits`)
  }

  return errors
}

function hasNode(index: ContentIndex, nodeId: string): boolean {
  return index.graphNodes().some((node) => node.id === nodeId)
}

function checkDialogueRefs(index: ContentIndex, sceneId: string, ids: string[] | null | undefined, errors: string[]): void {
  if (!ids) return
  for (const id of ids) {
    if (!index.dialogues.has(id)) errors.push(`scene ${sceneId}: unknown dialogue ${id}`)
  }
}

function checkFrozenSet(label: string, expected: readonly string[], actual: string[], errors: string[]): void {
  const expectedSet = new Set(expected)
  const actualSet = new Set(actual)
  for (const id of expected) {
    if (!actualSet.has(id)) errors.push(`${label} missing: ${id}`)
  }
  for (const id of actualSet) {
    if (!expectedSet.has(id)) errors.push(`${label} unexpected: ${id}`)
  }
  if (actual.length !== actualSet.size) errors.push(`${label} contains duplicate ids`)
}
