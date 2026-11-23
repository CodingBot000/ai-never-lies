import type { ContentIndex } from '../content/contentIndex'
import type { PuzzleDefinitionDto, PuzzleDraftDto } from '../content/dto'
import type { GameState } from '../state/gameState'

const EDGE_SOURCES: Record<string, string[]> = {
  'n_fault|n_false_rear': ['p07', 'p08'],
  'n_false_rear|n_trusted_rear': ['p06', 'p07'],
  'n_request|n_brake_limit': ['p05', 'p06'],
}

const REPORT_SOURCES: Record<string, string[]> = {
  unsafe_brake_limiting: ['p06', 'p17'],
  radar_fault: ['p17'],
  override_after_warning: ['p09'],
  health_check_removed: ['p11', 'p15'],
  release_conditions_open: ['p12', 'p15'],
  known_risk_continued: ['p18'],
  misleading_public_summary: ['p19'],
}

function evidenceFromInvestigation(id: string, state: GameState, index: ContentIndex): string[] {
  if (!state.isPuzzleSolved(id)) return []
  const draft = state.getDraft(id, false)
  const definition = index.puzzle(id)
  if (!draft?.submitted || !definition) return []
  if (id === 'p17') {
    return definition.edgeProofRequirements.flatMap((requirement) => {
      const proof = draft.edgeProofs.find((item) =>
        item.from === requirement.from && item.to === requirement.to && item.kind === requirement.kind)
      return requirement.sufficientEvidenceSets.find((set) =>
        set.requiredIds.every((evidenceId) => proof?.evidenceIds.includes(evidenceId)))?.requiredIds ?? []
    })
  }
  if (definition.sufficientEvidenceSets.length) {
    return definition.sufficientEvidenceSets.find((set) =>
      set.requiredIds.every((evidenceId) => draft.selectedEvidenceIds.includes(evidenceId)))?.requiredIds ?? []
  }
  // These puzzles require interpreting the respective logs or ordering the required documents.
  return definition.requiredEvidenceIds
}

export function importInvestigation(puzzle: PuzzleDefinitionDto, draft: PuzzleDraftDto, state: GameState, index: ContentIndex): number {
  if (draft.investigationImported) return 0
  const collect = (sourceIds: string[]) => new Set(sourceIds.flatMap((id) => evidenceFromInvestigation(id, state, index)))
  let imported = 0
  if (puzzle.id === 'p17') {
    for (const requirement of puzzle.edgeProofRequirements) {
      const sources = EDGE_SOURCES[`${requirement.from}|${requirement.to}`]
      if (!sources || draft.edges.some((edge) => edge.from === requirement.from && edge.to === requirement.to)) continue
      const known = collect(sources)
      const proof = requirement.sufficientEvidenceSets.find((set) =>
        set.requiredIds.every((id) => known.has(id) && state.owns(id)))
      if (!proof) continue
      // Do not add a link if it conflicts with the player's existing graph.
      const visited = new Set<string>()
      const pending = [requirement.to]
      for (const node of pending) {
        if (visited.has(node)) continue
        visited.add(node)
        for (const edge of draft.edges) if (edge.from === node) pending.push(edge.to)
      }
      if (visited.has(requirement.from)) continue
      const edge = { from: requirement.from, to: requirement.to, kind: requirement.kind }
      draft.edges.push(edge)
      draft.edgeProofs.push({ ...edge, evidenceIds: [...proof.requiredIds] })
      imported++
    }
  }
  if (puzzle.id === 'p20') {
    for (const requirement of puzzle.reportProofRequirements) {
      const sources = REPORT_SOURCES[requirement.item]
      if (!sources || draft.reportEvidence.some((proof) => proof.item === requirement.item)) continue
      const known = collect(sources)
      const proof = requirement.sufficientEvidenceSets.find((set) =>
        set.requiredIds.every((id) => known.has(id) && state.owns(id)))
      if (!proof) continue
      draft.reportEvidence.push({ item: requirement.item, evidenceIds: [...proof.requiredIds] })
      imported++
    }
  }
  if (imported) draft.investigationImported = true
  return imported
}
