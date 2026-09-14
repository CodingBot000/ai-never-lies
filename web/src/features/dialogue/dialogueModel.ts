import type { ContentIndex } from '../../core/content/contentIndex'
import type { DialogueLineDto, PressLineDto } from '../../core/content/dto'
import type { GameState } from '../../core/state/gameState'
import { Phases } from '../../core/state/gameState'

export interface CurrentLine {
  speaker: string
  text: string
  portraitAssetId: string
  transient: boolean
}

export function scenePhase(state: GameState): 'pre' | 'post' | null {
  if (state.phase === Phases.PreDialogue) return 'pre'
  if (state.phase === Phases.PostDialogue) return 'post'
  return null
}

export function sceneLines(state: GameState, index: ContentIndex): DialogueLineDto[] {
  const phase = scenePhase(state)
  if (!phase) return []
  return index.sceneDialogue(state.currentSceneId, phase)
}

export function resolveCurrentLine(
  state: GameState,
  index: ContentIndex,
  transient: PressLineDto[],
): CurrentLine | null {
  const first = transient[0]
  if (first) {
    return { speaker: first.speaker, text: first.text, portraitAssetId: '', transient: true }
  }
  const lines = sceneLines(state, index)
  if (lines.length === 0) return null
  const cursor = Math.min(Math.max(state.dialogueCursor, 0), lines.length - 1)
  const line = lines[cursor]
  if (!line) return null
  return {
    speaker: line.speaker,
    text: line.text,
    portraitAssetId: line.portraitAssetId,
    transient: false,
  }
}

export function lastPortraitBefore(lines: DialogueLineDto[], cursor: number): string {
  for (let i = Math.min(cursor, lines.length - 1); i >= 0; i--) {
    const line = lines[i]
    if (line?.portraitAssetId) return line.portraitAssetId
  }
  return ''
}
