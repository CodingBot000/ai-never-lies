import type { GameSessionApi } from '../app/useGameSession'
import type { PressLineDto } from '../core/content/dto'

export type ModalKind = 'evidence' | 'hints' | 'saves' | 'menu' | 'log' | 'dev'

export interface ScreenProps {
  api: GameSessionApi
  onTransient: (lines: PressLineDto[]) => void
  openModal: (modal: ModalKind) => void
  feedback: (message: string) => void
}
