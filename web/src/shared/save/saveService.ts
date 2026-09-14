import type { ProgressSnapshot, SaveEnvelopeDto } from '../../core/content/dto'
import { sha256Hex } from '../../core/state/sha256'
import { GameState } from '../../core/state/gameState'
import { Diag } from '../diagnostics'

export type LoadOutcome = 'Ok' | 'Missing' | 'Corrupt' | 'Incompatible' | 'UnknownIds'

export interface LoadResult {
  outcome: LoadOutcome
  snapshot: ProgressSnapshot | null
}

export const SAVE_KEY_PREFIX = 'anl.ep01.save'
export const MANUAL_SLOTS = ['slot_01', 'slot_02', 'slot_03'] as const
export const AUTOSAVE_SLOT = 'autosave'
export const ALL_SLOTS = [...MANUAL_SLOTS, AUTOSAVE_SLOT] as const

const CURRENT_SCHEMA_VERSION = 1
const SLOT_ID_PATTERN = /^[A-Za-z0-9_-]{1,32}$/

export function isValidSlotId(slotId: string): boolean {
  return SLOT_ID_PATTERN.test(slotId)
}

export function saveKeyFor(slotId: string): string {
  return `${SAVE_KEY_PREFIX}.${slotId}`
}

function getDefaultStorage(): Storage | undefined {
  try {
    return typeof window === 'undefined' ? undefined : window.localStorage
  } catch {
    return undefined
  }
}

/**
 * localStorage 저장·복원. (C# ANL.Runtime.Persistence.SaveService 이식)
 * 체크섬·백업·버전 검사를 수행하며 손상 시 조용히 초기화하지 않는다.
 */
export class SaveService {
  private readonly contentVersion: string
  private readonly artManifestSha256: string
  private readonly storage: Storage | undefined

  constructor(contentVersion: string, artManifestSha256: string, storage: Storage | undefined = getDefaultStorage()) {
    this.contentVersion = contentVersion ?? ''
    this.artManifestSha256 = artManifestSha256 ?? ''
    this.storage = storage
  }

  get storageAvailable(): boolean {
    if (!this.storage) return false
    try {
      const probe = `${SAVE_KEY_PREFIX}.probe`
      this.storage.setItem(probe, '1')
      this.storage.removeItem(probe)
      return true
    } catch {
      return false
    }
  }

  save(state: GameState | null, slotId: string): boolean {
    if (!state) {
      Diag.error('SaveService.Save: state is null')
      return false
    }
    if (!slotId || !isValidSlotId(slotId)) {
      Diag.error(`SaveService.Save failed for ${slotId}: invalid slot id`)
      return false
    }
    if (!this.storageAvailable || !this.storage) {
      Diag.error(`SaveService.Save failed for ${slotId}: storage unavailable`)
      return false
    }

    try {
      const snapshot = state.toSnapshot(this.contentVersion, this.artManifestSha256, slotId)
      const payloadJson = JSON.stringify(snapshot)
      const envelope: SaveEnvelopeDto = {
        schemaVersion: CURRENT_SCHEMA_VERSION,
        contentVersion: this.contentVersion,
        payloadJson,
        payloadSha256: sha256Hex(payloadJson),
        savedAtUtc: new Date().toISOString(),
        slotId,
      }
      const envelopeJson = JSON.stringify(envelope)

      const key = saveKeyFor(slotId)
      const tmpKey = `${key}.tmp`
      const bakKey = `${key}.bak`

      this.storage.setItem(tmpKey, envelopeJson)
      if (sha256Hex(this.storage.getItem(tmpKey) ?? '') !== sha256Hex(envelopeJson)) {
        this.storage.removeItem(tmpKey)
        Diag.error(`SaveService.Save failed for ${slotId}: temp verification failed`)
        return false
      }

      try {
        const existing = this.storage.getItem(key)
        if (existing !== null) {
          this.storage.setItem(bakKey, existing)
        }
        this.storage.setItem(key, envelopeJson)
      } catch (replaceError) {
        Diag.error(`SaveService.Save failed for ${slotId}: ${(replaceError as Error).message}`)
        this.storage.removeItem(tmpKey)
        return false
      }
      this.storage.removeItem(tmpKey)
      state.savedAtUtc = envelope.savedAtUtc
      return true
    } catch (error) {
      Diag.error(`SaveService.Save failed for ${slotId}: ${(error as Error).message}`)
      return false
    }
  }

  load(slotId: string): LoadResult {
    if (!slotId || !isValidSlotId(slotId)) return { outcome: 'Missing', snapshot: null }
    if (!this.storage) return { outcome: 'Missing', snapshot: null }

    const key = saveKeyFor(slotId)
    const raw = this.storage.getItem(key)
    if (raw === null) return { outcome: 'Missing', snapshot: null }

    const direct = this.tryLoadText(raw)
    if (direct.outcome === 'Ok' || direct.outcome === 'Incompatible') return direct

    const backupRaw = this.storage.getItem(`${key}.bak`)
    if (backupRaw !== null) {
      const backup = this.tryLoadText(backupRaw)
      if (backup.outcome === 'Ok') {
        Diag.warn(`SaveService: recovered slot ${slotId} from backup`)
        return backup
      }
    }
    return { outcome: 'Corrupt', snapshot: null }
  }

  private tryLoadText(text: string): LoadResult {
    try {
      const parsed: unknown = JSON.parse(text)
      if (typeof parsed !== 'object' || parsed === null) return { outcome: 'Corrupt', snapshot: null }
      const envelope = parsed as Partial<SaveEnvelopeDto>
      if (!envelope.payloadJson) return { outcome: 'Corrupt', snapshot: null }
      if ((envelope.schemaVersion ?? 0) > CURRENT_SCHEMA_VERSION) return { outcome: 'Incompatible', snapshot: null }
      if ((envelope.contentVersion ?? '') !== this.contentVersion) return { outcome: 'Incompatible', snapshot: null }
      if (sha256Hex(envelope.payloadJson).toLowerCase() !== (envelope.payloadSha256 ?? '').toLowerCase()) {
        return { outcome: 'Corrupt', snapshot: null }
      }
      const snapshot = JSON.parse(envelope.payloadJson) as ProgressSnapshot
      if (!snapshot || typeof snapshot !== 'object') return { outcome: 'Corrupt', snapshot: null }
      return { outcome: 'Ok', snapshot }
    } catch (error) {
      Diag.warn(`SaveService: read failed: ${(error as Error).message}`)
      return { outcome: 'Corrupt', snapshot: null }
    }
  }

  /** 손상된 저장을 사용자 동의 없이 삭제하지 않는다. 복구용 원본 백업만 노출한다. */
  backupTextFor(slotId: string): string | null {
    if (!this.storage || !isValidSlotId(slotId)) return null
    return this.storage.getItem(`${saveKeyFor(slotId)}.bak`)
  }

  /** 백업이 유효하면 본 슬롯으로 복원한다. 사용자가 명시적으로 선택했을 때만 호출한다. */
  restoreBackup(slotId: string): boolean {
    if (!this.storage || !isValidSlotId(slotId)) return false
    const backup = this.backupTextFor(slotId)
    if (backup === null) return false
    const verified = this.tryLoadText(backup)
    if (verified.outcome !== 'Ok') return false
    this.storage.setItem(saveKeyFor(slotId), backup)
    return true
  }

  exists(slotId: string): boolean {
    if (!this.storage || !isValidSlotId(slotId)) return false
    return this.storage.getItem(saveKeyFor(slotId)) !== null
  }

  listSlots(): string[] {
    return [...ALL_SLOTS]
  }

  readSlotTimestamp(slotId: string): string {
    const raw = this.storage?.getItem(saveKeyFor(slotId))
    if (!raw) return ''
    try {
      const envelope = JSON.parse(raw) as Partial<SaveEnvelopeDto>
      return envelope.savedAtUtc ?? ''
    } catch {
      return ''
    }
  }

  remove(slotId: string): void {
    if (!this.storage || !isValidSlotId(slotId)) return
    const key = saveKeyFor(slotId)
    this.storage.removeItem(key)
    this.storage.removeItem(`${key}.bak`)
    this.storage.removeItem(`${key}.tmp`)
  }
}
