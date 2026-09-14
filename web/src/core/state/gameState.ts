import type {
  EdgeDto,
  HintUsageDto,
  PlacedCardDto,
  ProgressSnapshot,
  PuzzleDraftDto,
  StringPairDto,
} from '../content/dto'

export const Phases = {
  Entering: 'entering',
  PreDialogue: 'pre_dialogue',
  BranchChoice: 'branch_choice',
  Interaction: 'interaction',
  PostDialogue: 'post_dialogue',
  ReadCheck: 'read_check',
  HubActive: 'hub_active',
  ReadyToExit: 'ready_to_exit',
  Completed: 'completed',
} as const

export type Phase = (typeof Phases)[keyof typeof Phases]

export function createPuzzleDraft(puzzleId: string): PuzzleDraftDto {
  return {
    puzzleId,
    selectedStatementId: '',
    selectedEvidenceIds: [],
    choiceIds: [],
    placedCards: [],
    edges: [],
    values: [],
    edgeProofs: [],
    reportPlacements: [],
    reportEvidence: [],
    ranCaseIds: [],
    comparisonWindowId: '',
    submitted: false,
  }
}

/** 진행 상태. UI가 직접 수정하지 않고 항상 CommandDispatcher를 통한다. */
export class GameState {
  currentSceneId = ''
  phase: string = Phases.Entering
  dialogueCursor = 0
  activePuzzleId = ''

  readonly ownedEvidence: string[] = []
  readonly readEvidence: string[] = []
  readonly visitedScenes: string[] = []
  readonly completedScenes: string[] = []
  readonly solvedPuzzles: string[] = []
  readonly visitedTopics: string[] = []
  readonly branchValues: StringPairDto[] = []
  readonly puzzleDrafts: PuzzleDraftDto[] = []
  readonly hintUsage: HintUsageDto[] = []
  readonly appliedEffectIds: string[] = []
  readonly preDialogueDone: string[] = []
  readonly postDialogueDone: string[] = []

  lastCommandSequence = 0
  savedAtUtc = ''

  owns(evidenceId: string | null | undefined): boolean {
    return !!evidenceId && this.ownedEvidence.includes(evidenceId)
  }

  hasRead(evidenceId: string | null | undefined): boolean {
    return !!evidenceId && this.readEvidence.includes(evidenceId)
  }

  isSceneVisited(sceneId: string | null | undefined): boolean {
    return !!sceneId && this.visitedScenes.includes(sceneId)
  }

  isSceneCompleted(sceneId: string | null | undefined): boolean {
    return !!sceneId && this.completedScenes.includes(sceneId)
  }

  isPuzzleSolved(puzzleId: string | null | undefined): boolean {
    return !!puzzleId && this.solvedPuzzles.includes(puzzleId)
  }

  isTopicVisited(topicId: string | null | undefined): boolean {
    return !!topicId && this.visitedTopics.includes(topicId)
  }

  isPreDone(sceneId: string | null | undefined): boolean {
    return !!sceneId && this.preDialogueDone.includes(sceneId)
  }

  isPostDone(sceneId: string | null | undefined): boolean {
    return !!sceneId && this.postDialogueDone.includes(sceneId)
  }

  addOwned(evidenceId: string | null | undefined): boolean {
    if (!evidenceId || this.ownedEvidence.includes(evidenceId)) return false
    this.ownedEvidence.push(evidenceId)
    return true
  }

  markRead(evidenceId: string | null | undefined): boolean {
    if (!evidenceId || !this.owns(evidenceId) || this.readEvidence.includes(evidenceId)) return false
    this.readEvidence.push(evidenceId)
    return true
  }

  getBranchValue(key: string): string {
    return this.branchValues.find((pair) => pair.key === key)?.value ?? ''
  }

  setBranchValue(key: string, value: string): void {
    const existing = this.branchValues.find((pair) => pair.key === key)
    if (existing) {
      existing.value = value
      return
    }
    this.branchValues.push({ key, value })
  }

  getDraft(puzzleId: string, create: true): PuzzleDraftDto
  getDraft(puzzleId: string, create: false): PuzzleDraftDto | null
  getDraft(puzzleId: string, create: boolean): PuzzleDraftDto | null {
    const existing = this.puzzleDrafts.find((draft) => draft.puzzleId === puzzleId)
    if (existing) return existing
    if (!create) return null
    const draft = createPuzzleDraft(puzzleId)
    this.puzzleDrafts.push(draft)
    return draft
  }

  getHintUsage(scopeId: string, create: true): HintUsageDto
  getHintUsage(scopeId: string, create: false): HintUsageDto | null
  getHintUsage(scopeId: string, create: boolean): HintUsageDto | null {
    const existing = this.hintUsage.find((usage) => usage.scopeId === scopeId)
    if (existing) return existing
    if (!create) return null
    const usage: HintUsageDto = { scopeId, maxLevel: 0, openCount: 0, usedTestAssist: false }
    this.hintUsage.push(usage)
    return usage
  }

  markApplied(effectId: string | null | undefined): boolean {
    if (!effectId) return true
    if (this.appliedEffectIds.includes(effectId)) return false
    this.appliedEffectIds.push(effectId)
    return true
  }

  toSnapshot(contentVersion: string, artManifestSha256: string, slotId: string): ProgressSnapshot {
    return {
      saveSchemaVersion: 1,
      contentVersion,
      artManifestSha256,
      slotId,
      currentSceneId: this.currentSceneId,
      phase: this.phase,
      dialogueCursor: this.dialogueCursor,
      activePuzzleId: this.activePuzzleId,
      ownedEvidenceIds: [...this.ownedEvidence],
      readEvidenceIds: [...this.readEvidence],
      visitedSceneIds: [...this.visitedScenes],
      completedSceneIds: [...this.completedScenes],
      solvedPuzzleIds: [...this.solvedPuzzles],
      visitedTopicIds: [...this.visitedTopics],
      branchValues: this.branchValues.map((pair) => ({ ...pair })),
      puzzleDrafts: this.puzzleDrafts.map(cloneDraft),
      hintUsage: this.hintUsage.map((usage) => ({ ...usage })),
      appliedEffectIds: [...this.appliedEffectIds],
      preDialogueDoneSceneIds: [...this.preDialogueDone],
      postDialogueDoneSceneIds: [...this.postDialogueDone],
      lastCommandSequence: this.lastCommandSequence,
      savedAtUtc: new Date().toISOString(),
    }
  }

  static fromSnapshot(snapshot: ProgressSnapshot | null | undefined): GameState {
    const state = new GameState()
    if (!snapshot) return state
    state.currentSceneId = snapshot.currentSceneId ?? ''
    state.phase = snapshot.phase || Phases.Entering
    state.dialogueCursor = snapshot.dialogueCursor ?? 0
    state.activePuzzleId = snapshot.activePuzzleId ?? ''
    fill(state.ownedEvidence, snapshot.ownedEvidenceIds)
    fill(state.readEvidence, snapshot.readEvidenceIds)
    fill(state.visitedScenes, snapshot.visitedSceneIds)
    fill(state.completedScenes, snapshot.completedSceneIds)
    fill(state.solvedPuzzles, snapshot.solvedPuzzleIds)
    fill(state.visitedTopics, snapshot.visitedTopicIds)
    if (snapshot.branchValues) {
      for (const pair of snapshot.branchValues) {
        if (pair && pair.key) state.branchValues.push({ ...pair })
      }
    }
    if (snapshot.puzzleDrafts) {
      for (const draft of snapshot.puzzleDrafts) {
        if (draft && draft.puzzleId) state.puzzleDrafts.push(normalizeDraft(draft))
      }
    }
    if (snapshot.hintUsage) {
      for (const usage of snapshot.hintUsage) {
        if (usage && usage.scopeId) state.hintUsage.push({ ...usage })
      }
    }
    fill(state.appliedEffectIds, snapshot.appliedEffectIds)
    fill(state.preDialogueDone, snapshot.preDialogueDoneSceneIds)
    fill(state.postDialogueDone, snapshot.postDialogueDoneSceneIds)
    state.lastCommandSequence = snapshot.lastCommandSequence ?? 0
    state.savedAtUtc = snapshot.savedAtUtc ?? ''
    return state
  }
}

function fill(target: string[], source: string[] | null | undefined): void {
  target.length = 0
  if (!source) return
  for (const value of source) {
    if (value && !target.includes(value)) target.push(value)
  }
}

function cloneDraft(draft: PuzzleDraftDto): PuzzleDraftDto {
  return {
    ...draft,
    selectedEvidenceIds: [...draft.selectedEvidenceIds],
    choiceIds: [...draft.choiceIds],
    placedCards: draft.placedCards.map((card) => ({ ...card })),
    edges: draft.edges.map((edge) => ({ ...edge })),
    values: draft.values.map((pair) => ({ ...pair })),
    edgeProofs: draft.edgeProofs.map((proof) => ({ ...proof, evidenceIds: [...proof.evidenceIds] })),
    reportPlacements: draft.reportPlacements.map((item) => ({ ...item })),
    reportEvidence: draft.reportEvidence.map((item) => ({ ...item, evidenceIds: [...item.evidenceIds] })),
    ranCaseIds: [...draft.ranCaseIds],
  }
}

function normalizeDraft(draft: PuzzleDraftDto): PuzzleDraftDto {
  const base = createPuzzleDraft(draft.puzzleId)
  return {
    ...base,
    ...draft,
    selectedEvidenceIds: draft.selectedEvidenceIds ?? [],
    choiceIds: draft.choiceIds ?? [],
    placedCards: draft.placedCards ?? [],
    edges: draft.edges ?? [],
    values: draft.values ?? [],
    edgeProofs: draft.edgeProofs ?? [],
    reportPlacements: draft.reportPlacements ?? [],
    reportEvidence: draft.reportEvidence ?? [],
    ranCaseIds: draft.ranCaseIds ?? [],
    comparisonWindowId: draft.comparisonWindowId ?? '',
    selectedStatementId: draft.selectedStatementId ?? '',
  }
}

export type PlacedCard = PlacedCardDto
export type Edge = EdgeDto
