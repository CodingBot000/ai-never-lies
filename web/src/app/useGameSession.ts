import { useCallback, useEffect, useReducer, useRef, useState } from 'react'
import { GameSession } from './gameSession'
import type { CommandResult } from '../core/commands/commandTypes'
import type { ContentIndex } from '../core/content/contentIndex'
import { loadContent } from '../content/loadContent'
import { assetManifestSha256 } from '../shared/assets/assetCatalog'
import { SaveService } from '../shared/save/saveService'

export interface GameBoot {
  status: 'loading' | 'error' | 'ready'
  errors: string[]
  session: GameSession | null
}

export interface GameSessionApi {
  boot: GameBoot
  index: ContentIndex | null
  session: GameSession | null
  revision: number
  lastResult: CommandResult | null
  run: (
    type: string,
    options?: { targetId?: string; ids?: string[]; slot?: string; value?: string; numeric?: number; origin?: string },
  ) => CommandResult | null
  refresh: () => void
}

/** 콘텐츠 로드 → 세션 생성 → 명령 실행마다 리렌더 트리거 */
export function useGameSession(): GameSessionApi {
  const [boot, setBoot] = useState<GameBoot>({ status: 'loading', errors: [], session: null })
  const [revision, bump] = useReducer((value: number) => value + 1, 0)
  const lastResult = useRef<CommandResult | null>(null)

  useEffect(() => {
    let cancelled = false
    void (async () => {
      const result = await loadContent()
      if (cancelled) return
      if (!result.ok || !result.index) {
        setBoot({ status: 'error', errors: result.errors, session: null })
        return
      }
      const saves = new SaveService(result.index.episode.contentVersion, assetManifestSha256)
      const session = new GameSession(
        result.index,
        saves,
        result.index.episode.contentVersion,
        assetManifestSha256,
      )
      session.onExecuted = () => bump()
      setBoot({ status: 'ready', errors: [], session })
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const run = useCallback<GameSessionApi['run']>(
    (type, options = {}) => {
      if (!boot.session) return null
      const result = boot.session.executeOrigin(options.origin ?? 'user', type, options)
      lastResult.current = result
      bump()
      return result
    },
    [boot.session],
  )

  const refresh = useCallback(() => bump(), [])

  return {
    boot,
    index: boot.session?.content ?? null,
    session: boot.session,
    revision,
    get lastResult() {
      return lastResult.current
    },
    run,
    refresh,
  }
}
