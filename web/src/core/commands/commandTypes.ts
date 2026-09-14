import type { PressLineDto } from '../content/dto'

export const CommandKinds = {
  StartEpisode: 'StartEpisode',
  AdvanceDialogue: 'AdvanceDialogue',
  ChooseTopic: 'ChooseTopic',
  InspectRegion: 'InspectRegion',
  OpenEvidence: 'OpenEvidence',
  ReadEvidence: 'ReadEvidence',
  SelectStatement: 'SelectStatement',
  PressStatement: 'PressStatement',
  SelectEvidence: 'SelectEvidence',
  PresentEvidence: 'PresentEvidence',
  PlaceCard: 'PlaceCard',
  RemoveCard: 'RemoveCard',
  ConnectNodes: 'ConnectNodes',
  RemoveEdge: 'RemoveEdge',
  SelectComparisonWindow: 'SelectComparisonWindow',
  RunCounterfactualCase: 'RunCounterfactualCase',
  SubmitPuzzle: 'SubmitPuzzle',
  SelectChoice: 'SelectChoice',
  ChooseBranch: 'ChooseBranch',
  ContinueScene: 'ContinueScene',
  ReturnToHub: 'ReturnToHub',
  SaveSlot: 'SaveSlot',
  LoadSlot: 'LoadSlot',
  OpenHint: 'OpenHint',
  GoToScene: 'GoToScene',
} as const

export type CommandType = (typeof CommandKinds)[keyof typeof CommandKinds]

export const Origins = {
  User: 'user',
  TestAssist: 'test-assist',
  Replay: 'replay',
  Fixture: 'fixture',
} as const

export type CommandOrigin = (typeof Origins)[keyof typeof Origins]

export const ResultStatus = {
  Accepted: 'Accepted',
  Rejected: 'Rejected',
  Partial: 'Partial',
  Solved: 'Solved',
} as const

export type CommandStatus = (typeof ResultStatus)[keyof typeof ResultStatus]

export interface GameCommand {
  commandId: string
  sequence: number
  sceneId: string
  origin: string
  type: string
  targetId?: string
  ids?: string[]
  slot?: string
  value?: string
  numeric?: number
}

export interface DomainEvent {
  kind: string
  id: string
  message: string
  lines?: PressLineDto[]
}

export interface CommandResult {
  status: CommandStatus
  reasonCode: string
  message: string
  events: DomainEvent[]
  changed: boolean
}

export function reject(reason: string, message = ''): CommandResult {
  return { status: ResultStatus.Rejected, reasonCode: reason, message, events: [], changed: false }
}

export function accept(reason = '', message = ''): CommandResult {
  return { status: ResultStatus.Accepted, reasonCode: reason, message, events: [], changed: true }
}
