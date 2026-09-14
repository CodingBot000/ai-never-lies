import { SAVE_KEYS } from '../shared/save/saveKeys'

export type TextSize = '100' | '125' | '150'
export type TextSpeed = 'slow' | 'normal' | 'fast' | 'instant'

export interface AppSettings {
  textSize: TextSize
  textSpeed: TextSpeed
  autoAdvance: boolean
  reducedMotion: boolean
}

export const DEFAULT_SETTINGS: AppSettings = {
  textSize: '100',
  textSpeed: 'normal',
  autoAdvance: false,
  reducedMotion: false,
}

const TEXT_SIZES: TextSize[] = ['100', '125', '150']
const TEXT_SPEEDS: TextSpeed[] = ['slow', 'normal', 'fast', 'instant']

function getStorage(): Storage | undefined {
  return typeof window === 'undefined' ? undefined : window.localStorage
}

export function loadSettings(storage: Storage | undefined = getStorage()): AppSettings {
  if (!storage) return { ...DEFAULT_SETTINGS }
  try {
    const raw = storage.getItem(SAVE_KEYS.settings)
    if (!raw) return { ...DEFAULT_SETTINGS }
    const parsed: unknown = JSON.parse(raw)
    if (typeof parsed !== 'object' || parsed === null) return { ...DEFAULT_SETTINGS }
    const record = parsed as Record<string, unknown>
    const textSize = TEXT_SIZES.includes(record.textSize as TextSize)
      ? (record.textSize as TextSize)
      : DEFAULT_SETTINGS.textSize
    const textSpeed = TEXT_SPEEDS.includes(record.textSpeed as TextSpeed)
      ? (record.textSpeed as TextSpeed)
      : DEFAULT_SETTINGS.textSpeed
    return {
      textSize,
      textSpeed,
      autoAdvance: record.autoAdvance === true,
      reducedMotion: record.reducedMotion === true,
    }
  } catch {
    return { ...DEFAULT_SETTINGS }
  }
}

export function saveSettings(settings: AppSettings, storage: Storage | undefined = getStorage()): boolean {
  if (!storage) return false
  try {
    storage.setItem(SAVE_KEYS.settings, JSON.stringify(settings))
    return true
  } catch {
    return false
  }
}

export function applySettings(settings: AppSettings): void {
  if (typeof document === 'undefined') return
  document.documentElement.dataset.textSize = settings.textSize
  document.documentElement.dataset.reducedMotion = settings.reducedMotion ? 'true' : 'false'
}
