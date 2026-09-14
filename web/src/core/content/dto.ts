/**
 * 콘텐츠·진행 DTO. `content/ep01/*.json`의 실제 필드 이름과 1:1로 맞춘다.
 * (기존 Unity C# `ANL.Core.Contracts`의 동작 계약을 그대로 옮긴 것)
 */

// ---- shared ----
export interface ConditionDto {
  kind: string
  id: string
}

export interface StringPairDto {
  key: string
  value: string
}

export interface EvidenceSetDto {
  requiredIds: string[]
}

export type EdgeKind = 'causal' | 'signal'

export interface EdgeDto {
  from: string
  to: string
  kind: string
}

export interface EdgeProofDto {
  from: string
  to: string
  kind: string
  sufficientEvidenceSets: EvidenceSetDto[]
}

export interface ReportProofDto {
  section: string
  item: string
  sufficientEvidenceSets: EvidenceSetDto[]
}

export interface NumericAnswerDto {
  key: string
  value: number
  tolerance: number
}

export interface PressLineDto {
  speaker: string
  text: string
}

// ---- episode.json ----
export interface EpisodeDefinitionDto {
  id: string
  contentVersion: string
  title: string
  subtitle: string
  startSceneId: string
  saveSchemaVersion: number
  artContractVersion: string
  requiredPuzzleIds: string[]
  optionalSceneIds: string[]
}

export interface EpisodeFileDto {
  schemaVersion: number
  episode: EpisodeDefinitionDto
}

// ---- scenes.json ----
export interface EvidenceGrantDto {
  evidenceId: string
  trigger: string
  atDialogueId: string
}

export interface TransitionDto {
  target: string
  conditions: ConditionDto[]
}

export interface SceneDefinitionDto {
  id: string
  act: number
  screenType: string
  backgroundAssetId: string
  entryConditions: ConditionDto[]
  preDialogueIds: string[]
  branchGroupIds: string[]
  advancingGroupIds: string[]
  interactionIds: string[]
  postDialogueIds: string[]
  grants: EvidenceGrantDto[]
  requiredReadEvidenceIds: string[]
  childSceneIds: string[]
  optionalChildSceneIds: string[]
  transitions: TransitionDto[]
  tipText: string
}

export interface SceneFileDto {
  schemaVersion: number
  items: SceneDefinitionDto[]
}

// ---- dialogues.ko.json ----
export interface DialogueLineDto {
  id: string
  sceneId: string
  speakerId: string
  speaker: string
  textId: string
  text: string
  portraitAssetId: string
  phase: string
}

export interface DialogueFileDto {
  schemaVersion: number
  items: DialogueLineDto[]
}

// ---- evidence.json ----
export interface EvidenceDefinitionDto {
  id: string
  title: string
  titleTextId: string
  source: string
  sourceTextId: string
  body: string
  bodyTextId: string
  proves: string
  provesTextId: string
  limits: string
  limitsTextId: string
  imageAssetId: string
  kind: string
  firstSceneId: string
  sourceMetadata: string
}

export interface EvidenceFileDto {
  schemaVersion: number
  items: EvidenceDefinitionDto[]
}

// ---- testimonies.json ----
export interface TestimonyStatementDto {
  id: string
  sceneId: string
  speakerId: string
  speaker: string
  textId: string
  text: string
  pressTextId: string
  pressResponse: PressLineDto[]
}

export interface TestimonyFileDto {
  schemaVersion: number
  items: TestimonyStatementDto[]
}

// ---- branches.json ----
export interface EffectDto {
  kind: string
  id: string
  key: string
  value: string
}

export interface BranchDefinitionDto {
  id: string
  sceneId: string
  groupId: string
  label: string
  condition: ConditionDto[]
  correct: boolean
  advances: boolean
  dialogue: PressLineDto[]
  feedback: string
  effects: EffectDto[]
  nextSceneId: string
}

export interface BranchFileDto {
  schemaVersion: number
  items: BranchDefinitionDto[]
}

// ---- puzzles.json ----
export interface RegionDto {
  regionId: string
  evidenceId: string
}

export interface PuzzleDefinitionDto {
  id: string
  sceneId: string
  type: string
  requiredEvidenceIds: string[]
  targetStatementId: string
  correctChoiceIds: string[]
  orderedIds: string[]
  sufficientEvidenceSets: EvidenceSetDto[]
  valuePairs: StringPairDto[]
  requiredEdges: EdgeDto[]
  edgeProofRequirements: EdgeProofDto[]
  reportProofRequirements: ReportProofDto[]
  andNodeIds: string[]
  numericAnswers: NumericAnswerDto[]
  requiredCaseIds: string[]
  regions: RegionDto[]
  windowId: string
  successText: string
  partialText: string
  wrongText: string
  evidenceSetCorrectTexts: string[]
}

export interface PuzzleFileDto {
  schemaVersion: number
  items: PuzzleDefinitionDto[]
}

// ---- hints.ko.json ----
export interface HintDefinitionDto {
  id: string
  scopeType: string
  scopeId: string
  level: number
  textId: string
  text: string
  focusTarget: string
}

export interface HintFileDto {
  schemaVersion: number
  items: HintDefinitionDto[]
}

// ---- ui.ko.json ----
export interface UiStringDto {
  id: string
  text: string
}

export interface UiStringFileDto {
  schemaVersion: number
  items: UiStringDto[]
}

// ---- graph.json ----
export interface GraphNodeDto {
  id: string
  label: string
  kind: string
  evidenceIds: string[]
}

export interface EventLogDto {
  id: string
  relativeMs: number
  source: string
  type: string
  payload: string
  firstEvidenceId: string
  display: string
}

export interface KinematicsDto {
  speedKmh: number
  actuatorDelaySeconds: number
  weakDurationSeconds: number
  weakDeceleration: number
  strongDurationSeconds: number
  strongDeceleration: number
  historicalDistanceMeters: number
  historicalImpactKmh: number
  fullBrakeStopMeters: number
  roadDecelerationMin: number
  roadDecelerationMax: number
  stopDistanceMinMeters: number
  stopDistanceMaxMeters: number
  requestToStrongMs: number
  note: string
}

export interface GraphFileDto {
  schemaVersion: number
  nodes: GraphNodeDto[]
  causalEdges: EdgeDto[]
  signalEdges: EdgeDto[]
  eventLog: EventLogDto[]
  kinematics: KinematicsDto
}

// ---- matrix.json ----
export interface MatrixRowDto {
  index: number
  sensor: string
  overrideState: string
  version: string
  condition: string
  verification: string
  note: string
}

export interface MatrixDimensionsDto {
  sensor: string[]
  overrideState: string[]
  version: string[]
  condition: string[]
}

export interface MatrixFileDto {
  schemaVersion: number
  rows: MatrixRowDto[]
  dimensions: MatrixDimensionsDto
}

// ---- progress / save ----
export interface PlacedCardDto {
  slot: string
  value: string
}

export interface EdgeProofSelectionDto {
  from: string
  to: string
  kind: string
  evidenceIds: string[]
}

export interface ReportPlacementDto {
  section: string
  item: string
}

export interface ReportEvidenceDto {
  item: string
  evidenceIds: string[]
}

export interface PuzzleDraftDto {
  puzzleId: string
  selectedStatementId: string
  selectedEvidenceIds: string[]
  choiceIds: string[]
  placedCards: PlacedCardDto[]
  edges: EdgeDto[]
  values: StringPairDto[]
  edgeProofs: EdgeProofSelectionDto[]
  reportPlacements: ReportPlacementDto[]
  reportEvidence: ReportEvidenceDto[]
  ranCaseIds: string[]
  comparisonWindowId: string
  submitted: boolean
}

export interface HintUsageDto {
  scopeId: string
  maxLevel: number
  openCount: number
  usedTestAssist: boolean
}

export interface ProgressSnapshot {
  saveSchemaVersion: number
  contentVersion: string
  artManifestSha256: string
  slotId: string
  currentSceneId: string
  phase: string
  dialogueCursor: number
  activePuzzleId: string
  ownedEvidenceIds: string[]
  readEvidenceIds: string[]
  visitedSceneIds: string[]
  completedSceneIds: string[]
  solvedPuzzleIds: string[]
  visitedTopicIds: string[]
  branchValues: StringPairDto[]
  puzzleDrafts: PuzzleDraftDto[]
  hintUsage: HintUsageDto[]
  appliedEffectIds: string[]
  preDialogueDoneSceneIds: string[]
  postDialogueDoneSceneIds: string[]
  lastCommandSequence: number
  savedAtUtc: string
}

export interface SaveEnvelopeDto {
  schemaVersion: number
  contentVersion: string
  payloadJson: string
  payloadSha256: string
  savedAtUtc: string
  slotId: string
}
