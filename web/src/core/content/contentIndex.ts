import type {
  BranchDefinitionDto,
  DialogueLineDto,
  EpisodeDefinitionDto,
  EvidenceDefinitionDto,
  EventLogDto,
  GraphFileDto,
  GraphNodeDto,
  HintDefinitionDto,
  MatrixFileDto,
  MatrixRowDto,
  PuzzleDefinitionDto,
  SceneDefinitionDto,
  TestimonyStatementDto,
  UiStringDto,
  EdgeDto,
} from './dto'

export interface ContentIndexData {
  episode: EpisodeDefinitionDto
  scenes: SceneDefinitionDto[]
  dialogues: DialogueLineDto[]
  evidence: EvidenceDefinitionDto[]
  testimonies: TestimonyStatementDto[]
  branches: BranchDefinitionDto[]
  puzzles: PuzzleDefinitionDto[]
  hints: HintDefinitionDto[]
  uiStrings: UiStringDto[]
  graph: GraphFileDto
  matrix: MatrixFileDto
}

/**
 * 로드된 콘텐츠의 읽기 전용 조회 계층. (C# ANL.Core.Content.ContentIndex 이식)
 */
export class ContentIndex {
  readonly episode: EpisodeDefinitionDto
  readonly graph: GraphFileDto
  readonly matrix: MatrixFileDto

  readonly scenes: ReadonlyMap<string, SceneDefinitionDto>
  readonly dialogues: ReadonlyMap<string, DialogueLineDto>
  readonly evidence: ReadonlyMap<string, EvidenceDefinitionDto>
  readonly testimonies: ReadonlyMap<string, TestimonyStatementDto>
  readonly branches: ReadonlyMap<string, BranchDefinitionDto>
  readonly puzzles: ReadonlyMap<string, PuzzleDefinitionDto>
  readonly hints: ReadonlyMap<string, HintDefinitionDto>
  readonly uiStrings: ReadonlyMap<string, UiStringDto>
  readonly text: ReadonlyMap<string, string>

  readonly matrixRows: MatrixRowDto[]

  private readonly nodes: GraphNodeDto[]
  private readonly causal: EdgeDto[]
  private readonly signal: EdgeDto[]
  private readonly events: EventLogDto[]
  private readonly branchOrder: BranchDefinitionDto[]
  private readonly testimonyOrder: TestimonyStatementDto[]
  private readonly hintOrder: HintDefinitionDto[]

  constructor(data: ContentIndexData) {
    this.episode = data.episode
    this.graph = data.graph
    this.matrix = data.matrix
    this.scenes = toMap(data.scenes, (item) => item.id)
    this.dialogues = toMap(data.dialogues, (item) => item.id)
    this.evidence = toMap(data.evidence, (item) => item.id)
    this.testimonies = toMap(data.testimonies, (item) => item.id)
    this.branches = toMap(data.branches, (item) => item.id)
    this.puzzles = toMap(data.puzzles, (item) => item.id)
    this.hints = toMap(data.hints, (item) => item.id)
    this.uiStrings = toMap(data.uiStrings, (item) => item.id)

    this.nodes = data.graph?.nodes ?? []
    this.causal = data.graph?.causalEdges ?? []
    this.signal = data.graph?.signalEdges ?? []
    this.events = data.graph?.eventLog ?? []
    this.matrixRows = data.matrix?.rows ?? []
    this.branchOrder = [...this.branches.values()].sort((a, b) => compareOrdinal(a.id, b.id))
    this.testimonyOrder = [...this.testimonies.values()].sort((a, b) => compareOrdinal(a.id, b.id))
    this.hintOrder = [...this.hints.values()]

    const text = new Map<string, string>()
    for (const line of this.dialogues.values()) put(text, line.textId, line.text)
    for (const item of this.evidence.values()) {
      put(text, item.titleTextId, item.title)
      put(text, item.sourceTextId, item.source)
      put(text, item.bodyTextId, item.body)
      put(text, item.provesTextId, item.proves)
      put(text, item.limitsTextId, item.limits)
    }
    for (const item of this.testimonies.values()) put(text, item.textId, item.text)
    for (const item of this.hints.values()) put(text, item.textId, item.text)
    for (const item of this.uiStrings.values()) put(text, item.id, item.text)
    this.text = text
  }

  scene(id: string | null | undefined): SceneDefinitionDto | null {
    return lookup(this.scenes, id)
  }

  dialogue(id: string | null | undefined): DialogueLineDto | null {
    return lookup(this.dialogues, id)
  }

  evidenceDef(id: string | null | undefined): EvidenceDefinitionDto | null {
    return lookup(this.evidence, id)
  }

  testimony(id: string | null | undefined): TestimonyStatementDto | null {
    return lookup(this.testimonies, id)
  }

  branch(id: string | null | undefined): BranchDefinitionDto | null {
    return lookup(this.branches, id)
  }

  puzzle(id: string | null | undefined): PuzzleDefinitionDto | null {
    return lookup(this.puzzles, id)
  }

  hint(id: string | null | undefined): HintDefinitionDto | null {
    return lookup(this.hints, id)
  }

  ui(id: string | null | undefined): string {
    if (!id) return id ?? ''
    return this.uiStrings.get(id)?.text ?? id
  }

  textOf(id: string | null | undefined): string {
    if (!id) return ''
    return this.text.get(id) ?? ''
  }

  hasEvidence(id: string | null | undefined): boolean {
    return !!id && this.evidence.has(id)
  }

  hasScene(id: string | null | undefined): boolean {
    return !!id && this.scenes.has(id)
  }

  hasPuzzle(id: string | null | undefined): boolean {
    return !!id && this.puzzles.has(id)
  }

  sceneDialogue(sceneId: string, phase: 'pre' | 'post'): DialogueLineDto[] {
    const scene = this.scene(sceneId)
    if (!scene) return []
    const ids = phase === 'pre' ? scene.preDialogueIds : scene.postDialogueIds
    if (!ids) return []
    const result: DialogueLineDto[] = []
    for (const id of ids) {
      const line = this.dialogue(id)
      if (line) result.push(line)
    }
    return result
  }

  branchGroup(groupId: string): BranchDefinitionDto[] {
    return this.branchOrder.filter((branch) => branch.groupId === groupId)
  }

  sceneTestimonies(sceneId: string): TestimonyStatementDto[] {
    return this.testimonyOrder.filter((item) => item.sceneId === sceneId)
  }

  graphNodes(): GraphNodeDto[] {
    return this.nodes
  }

  graphEdges(causal: boolean): EdgeDto[] {
    return causal ? this.causal : this.signal
  }

  eventIndex(eventId: string): number {
    return this.events.findIndex((event) => event.id === eventId)
  }

  eventAt(index: number): EventLogDto | null {
    return index >= 0 && index < this.events.length ? (this.events[index] ?? null) : null
  }

  get eventCount(): number {
    return this.events.length
  }

  allHints(): HintDefinitionDto[] {
    return this.hintOrder
  }
}

function toMap<T>(items: T[], keyOf: (item: T) => string): Map<string, T> {
  const map = new Map<string, T>()
  for (const item of items) {
    const key = keyOf(item)
    if (key) map.set(key, item)
  }
  return map
}

function put(map: Map<string, string>, id: string, text: string): void {
  if (id) map.set(id, text ?? '')
}

function lookup<T>(map: ReadonlyMap<string, T>, id: string | null | undefined): T | null {
  if (!id) return null
  return map.get(id) ?? null
}

export function compareOrdinal(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0
}
