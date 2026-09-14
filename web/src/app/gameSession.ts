import { CommandDispatcher } from '../core/commands/commandDispatcher'
import {
  CommandKinds,
  Origins,
  ResultStatus,
  type CommandOrigin,
  type CommandResult,
} from '../core/commands/commandTypes'
import type { ContentIndex } from '../core/content/contentIndex'
import { HintService } from '../core/hints/hintService'
import { GameState } from '../core/state/gameState'
import { Diag } from '../shared/diagnostics'
import type { LoadOutcome, SaveService } from '../shared/save/saveService'

/**
 * 콘텐츠 + 상태 + 저장 + 힌트를 묶는 세션. (C# ANL.Runtime.Presentation.GameSession 이식)
 * UI는 이 세션의 execute만 호출한다.
 */
export class GameSession {
  readonly content: ContentIndex
  readonly saves: SaveService
  readonly contentVersion: string
  readonly artManifestSha256: string
  readonly hints: HintService

  state: GameState
  dispatcher: CommandDispatcher
  blockingError = ''

  onExecuted: ((result: CommandResult | null) => void) | undefined

  private commandCounter = 0

  constructor(content: ContentIndex, saves: SaveService, contentVersion: string, artManifestSha256: string) {
    this.content = content
    this.saves = saves
    this.contentVersion = contentVersion ?? ''
    this.artManifestSha256 = artManifestSha256 ?? ''
    this.hints = new HintService(content)
    this.state = new GameState()
    this.dispatcher = new CommandDispatcher(content, this.state)
    this.wireDispatcher()
  }

  get nextSequence(): number {
    return this.state.lastCommandSequence + 1
  }

  execute(
    type: string,
    options: {
      targetId?: string
      ids?: string[]
      slot?: string
      value?: string
      numeric?: number
    } = {},
  ): CommandResult {
    return this.executeOrigin(Origins.User, type, options)
  }

  executeOrigin(
    origin: CommandOrigin | string,
    type: string,
    options: {
      targetId?: string
      ids?: string[]
      slot?: string
      value?: string
      numeric?: number
    } = {},
  ): CommandResult {
    this.commandCounter++
    const command = {
      commandId: `cmd_${this.commandCounter}`,
      sequence: this.nextSequence,
      sceneId: this.state.currentSceneId,
      origin: origin || Origins.User,
      type,
      ...(options.targetId !== undefined ? { targetId: options.targetId } : {}),
      ...(options.ids !== undefined ? { ids: options.ids } : {}),
      ...(options.slot !== undefined ? { slot: options.slot } : {}),
      ...(options.value !== undefined ? { value: options.value } : {}),
      ...(options.numeric !== undefined ? { numeric: options.numeric } : {}),
    }
    const result = this.dispatcher.execute(command)
    this.handleCheckpoint(result)
    this.onExecuted?.(result)
    return result
  }

  newGame(slotId?: string): CommandResult {
    this.state = new GameState()
    this.dispatcher = new CommandDispatcher(this.content, this.state)
    this.wireDispatcher()
    this.commandCounter = 0
    this.blockingError = ''

    const result = this.execute(CommandKinds.StartEpisode)
    if (result && result.status !== ResultStatus.Rejected && slotId) this.saveManual(slotId)
    return result
  }

  loadGame(slotId: string): { loaded: boolean; outcome: LoadOutcome } {
    const { outcome, snapshot } = this.saves.load(slotId)
    if (outcome !== 'Ok' || !snapshot) return { loaded: false, outcome }

    if (snapshot.currentSceneId && !this.content.hasScene(snapshot.currentSceneId)) {
      this.blockingError = `save references unknown scene: ${snapshot.currentSceneId}`
      Diag.error(this.blockingError)
      return { loaded: false, outcome: 'UnknownIds' }
    }
    if (snapshot.activePuzzleId && !this.content.hasPuzzle(snapshot.activePuzzleId)) {
      this.blockingError = `save references unknown puzzle: ${snapshot.activePuzzleId}`
      Diag.error(this.blockingError)
      return { loaded: false, outcome: 'UnknownIds' }
    }

    this.state = GameState.fromSnapshot(snapshot)
    this.dispatcher = new CommandDispatcher(this.content, this.state)
    this.wireDispatcher()
    this.commandCounter = 0
    this.blockingError = ''
    this.onExecuted?.(null)
    return { loaded: true, outcome: 'Ok' }
  }

  autoSave(): void {
    if (!this.saves.save(this.state, 'autosave')) Diag.warn('GameSession: autosave failed')
  }

  saveManual(slotId: string): boolean {
    return this.saves.save(this.state, slotId)
  }

  private wireDispatcher(): void {
    this.dispatcher.hooks = {
      saveRequested: (slot) => this.saveManual(slot),
      loadRequested: (slot) => this.loadGame(slot).loaded,
    }
  }

  private handleCheckpoint(result: CommandResult | null): void {
    if (!result || result.status === ResultStatus.Rejected) return

    let checkpoint = result.reasonCode === 'branch_advanced'
    if (!checkpoint) {
      for (const event of result.events) {
        if (event.kind === 'evidence_granted' || event.kind === 'puzzle_solved' || event.kind === 'scene_completed') {
          checkpoint = true
          break
        }
      }
    }
    if (checkpoint) this.autoSave()
  }
}
