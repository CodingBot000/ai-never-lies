import { describe, expect, it } from 'vitest'
import { SAVE_KEYS } from '../../src/shared/save/saveKeys'
import {
  applySettings,
  DEFAULT_SETTINGS,
  loadSettings,
  saveSettings,
  type AppSettings,
} from '../../src/app/settings'

function createMemoryStorage(initial: Record<string, string> = {}): Storage {
  const map = new Map<string, string>(Object.entries(initial))
  return {
    get length() {
      return map.size
    },
    clear: () => map.clear(),
    getItem: (key: string) => map.get(key) ?? null,
    key: (index: number) => Array.from(map.keys())[index] ?? null,
    removeItem: (key: string) => {
      map.delete(key)
    },
    setItem: (key: string, value: string) => {
      map.set(key, value)
    },
  }
}

describe('앱 설정', () => {
  it('저장이 없으면 기본값을 돌려준다', () => {
    expect(loadSettings(createMemoryStorage())).toEqual(DEFAULT_SETTINGS)
  })

  it('저장 후 다시 읽으면 같은 값이다', () => {
    const storage = createMemoryStorage()
    const settings: AppSettings = {
      textSize: '150',
      textSpeed: 'instant',
      autoAdvance: true,
      reducedMotion: true,
    }
    expect(saveSettings(settings, storage)).toBe(true)
    expect(loadSettings(storage)).toEqual(settings)
  })

  it('손상된 JSON이면 기본값으로 복구한다', () => {
    const storage = createMemoryStorage({ [SAVE_KEYS.settings]: '{ broken' })
    expect(loadSettings(storage)).toEqual(DEFAULT_SETTINGS)
  })

  it('알 수 없는 값은 기본값으로 대체한다', () => {
    const storage = createMemoryStorage({
      [SAVE_KEYS.settings]: JSON.stringify({ textSize: '999', textSpeed: 'turbo', autoAdvance: 'yes' }),
    })
    expect(loadSettings(storage)).toEqual(DEFAULT_SETTINGS)
  })

  it('applySettings는 html data 속성을 갱신한다', () => {
    applySettings({ textSize: '125', textSpeed: 'normal', autoAdvance: false, reducedMotion: true })
    expect(document.documentElement.dataset.textSize).toBe('125')
    expect(document.documentElement.dataset.reducedMotion).toBe('true')
  })
})
