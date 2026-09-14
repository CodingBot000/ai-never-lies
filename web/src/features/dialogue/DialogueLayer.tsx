import { useEffect, useMemo, useRef, useState } from 'react'
import type { AppSettings } from '../../app/settings'
import type { GameSessionApi } from '../../app/useGameSession'
import { allConditionsMet } from '../../core/conditions/conditionEvaluator'
import { Phases } from '../../core/state/gameState'
import { resolveCurrentLine } from './dialogueModel'
import './dialogue.css'

interface DialogueLayerProps {
  api: GameSessionApi
  settings: AppSettings
  transient: { speaker: string; text: string }[]
  consumeTransient: () => void
}

const SPEED_MS: Record<AppSettings['textSpeed'], number> = {
  slow: 45,
  normal: 18,
  fast: 8,
  instant: 0,
}

export function DialogueLayer({ api, settings, transient, consumeTransient }: DialogueLayerProps) {
  const { session, index, run } = api
  const state = session?.state
  const line = session && index && state ? resolveCurrentLine(state, index, transient) : null
  const [revealed, setRevealed] = useState(0)
  const timerRef = useRef<number | null>(null)

  useEffect(() => {
    setRevealed(0)
    const text = line?.text ?? ''
    if (!line) return
    const instant = settings.textSpeed === 'instant' || settings.reducedMotion
    if (instant || text.length === 0) {
      setRevealed(text.length)
      return
    }
    const interval = SPEED_MS[settings.textSpeed]
    timerRef.current = window.setInterval(() => {
      setRevealed((value) => {
        if (value >= text.length) {
          if (timerRef.current !== null) window.clearInterval(timerRef.current)
          return value
        }
        return value + 1
      })
    }, interval)
    return () => {
      if (timerRef.current !== null) window.clearInterval(timerRef.current)
    }
  }, [line?.text, line?.speaker, settings.textSpeed, settings.reducedMotion])

  const complete = line !== null && revealed >= line.text.length

  const advance = (): void => {
    if (!session || !state) return
    if (transient.length > 0) {
      consumeTransient()
      return
    }
    if (!complete) {
      setRevealed(line?.text.length ?? 0)
      return
    }
    run('AdvanceDialogue')
  }

  const branches = useMemo(() => {
    if (!index || !state || state.phase !== Phases.BranchChoice) return []
    const scene = index.scene(state.currentSceneId)
    if (!scene) return []
    const groups = scene.branchGroupIds ?? []
    const items: { id: string; label: string; locked: boolean; visited: boolean; advances: boolean }[] = []
    for (const groupId of groups) {
      for (const branch of index.branchGroup(groupId)) {
        items.push({
          id: branch.id,
          label: branch.label,
          locked: !allConditionsMet(branch.condition, state, index),
          visited: state.isTopicVisited(branch.id),
          advances: branch.advances,
        })
      }
    }
    return items
  }, [index, state, api.revision])

  if (!state || !index) return null
  const hasDialogue = line !== null
  const showBranches = state.phase === Phases.BranchChoice

  if (!hasDialogue && !showBranches) return null

  return (
    <div className="anl-dialogue" onClick={advance} role="presentation">
      {hasDialogue && line ? (
        <>
          <div className="anl-dialogue__box">
            <p className="anl-dialogue__speaker">{line.speaker || '내레이션'}</p>
            <p className="anl-dialogue__text" aria-live="polite">
              {line.text.slice(0, revealed)}
            </p>
            {line.text.length > revealed ? <span className="anl-dialogue__cursor" aria-hidden="true" /> : null}
          </div>
          <button
            type="button"
            className="anl-dialogue__next"
            onClick={(event) => {
              event.stopPropagation()
              advance()
            }}
          >
            다음
          </button>
        </>
      ) : null}
      {showBranches ? (
        <div className="anl-dialogue__choices" role="group" aria-label="선택지">
          {branches.map((branch) => (
            <button
              key={branch.id}
              type="button"
              className={`anl-choice${branch.advances ? ' anl-choice--advance' : ''}`}
              disabled={branch.locked || branch.visited}
              onClick={(event) => {
                event.stopPropagation()
                run('ChooseBranch', { targetId: branch.id })
              }}
            >
              <span>{branch.label}</span>
              {branch.visited ? <span className="anl-choice__tag">확인함</span> : null}
              {branch.locked ? <span className="anl-choice__tag">잠김</span> : null}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}
