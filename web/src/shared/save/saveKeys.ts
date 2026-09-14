/* 정본 저장 키. 02_CODE_GENERATION_INSTRUCTIONS 10.1과 일치해야 한다. */
export const SAVE_KEY_PREFIX = 'anl.ep01'

export const SAVE_KEYS = {
  autosave: `${SAVE_KEY_PREFIX}.save.autosave`,
  autosaveBackup: `${SAVE_KEY_PREFIX}.save.autosave.bak`,
  slot1: `${SAVE_KEY_PREFIX}.save.slot_01`,
  slot1Backup: `${SAVE_KEY_PREFIX}.save.slot_01.bak`,
  slot2: `${SAVE_KEY_PREFIX}.save.slot_02`,
  slot2Backup: `${SAVE_KEY_PREFIX}.save.slot_02.bak`,
  slot3: `${SAVE_KEY_PREFIX}.save.slot_03`,
  slot3Backup: `${SAVE_KEY_PREFIX}.save.slot_03.bak`,
  settings: `${SAVE_KEY_PREFIX}.settings`,
  meta: `${SAVE_KEY_PREFIX}.meta`,
} as const

export type SaveSlotKey = keyof typeof SAVE_KEYS

export const MANUAL_SLOT_KEYS: SaveSlotKey[] = ['slot1', 'slot2', 'slot3']

export function isStorageAvailable(storage: Storage | undefined): boolean {
  if (!storage) return false
  try {
    const probe = `${SAVE_KEY_PREFIX}.probe`
    storage.setItem(probe, '1')
    storage.removeItem(probe)
    return true
  } catch {
    return false
  }
}

/** 이어하기 가능 여부: 자동 저장 또는 수동 슬롯에 값이 하나라도 있는지 */
export function hasAnySave(storage: Storage | undefined = typeof window === 'undefined' ? undefined : window.localStorage): boolean {
  if (!isStorageAvailable(storage) || !storage) return false
  return [SAVE_KEYS.autosave, SAVE_KEYS.slot1, SAVE_KEYS.slot2, SAVE_KEYS.slot3].some(
    (key) => storage.getItem(key) !== null,
  )
}
