import { useState } from 'react'
import type { GameSessionApi } from '../../app/useGameSession'
import { planTestAssist } from '../../core/commands/testAssistPlanner'
import { Modal } from '../../shared/components/Modal'
import { testToolsEnabled } from '../../app/env'
import { formatContentText } from '../../shared/i18n/contentText'

interface HintPanelProps {
  api: GameSessionApi
  onClose: () => void
}

export function HintPanel({ api, onClose }: HintPanelProps) {
  const { session, index, run } = api
  const [assistMessage, setAssistMessage] = useState('')
  if (!session || !index) return null
  const state = session.state
  const context = session.hints.resolve(state)
  const usage = state.getHintUsage(context.scopeId, false)
  const levels = context.levels

  const plan = planTestAssist(index, state)

  return (
    <Modal title={index.ui('ui.hints')} onClose={onClose}>
      <div className="anl-hint">
        <p className="anl-hint__scene">{context.sceneTip ? formatContentText(context.sceneTip, index) : index.ui('ui.tip_scene_help')}</p>
        {context.nextAction ? (
          <p className="anl-hint__next">
            <strong>{index.ui('ui.hint_scene')}:</strong> {context.nextAction}
          </p>
        ) : null}
        {levels.length === 0 ? <p className="anl-muted">{index.ui('ui.no_active_puzzle')}</p> : null}
        {levels.map((hint) => {
          const opened = (usage?.maxLevel ?? 0) >= hint.level
          return (
            <div key={hint.id} className="anl-hint__level">
              <button
                type="button"
                className="anl-button anl-button--small"
                onClick={() => run('OpenHint', { targetId: context.scopeId, numeric: hint.level })}
                disabled={opened}
              >
                {hint.level === 1 ? index.ui('ui.hint_h1') : hint.level === 2 ? index.ui('ui.hint_h2') : index.ui('ui.hint_h3')}
              </button>
              {opened ? <p>{hint.text}</p> : <p className="anl-muted">{index.ui('ui.hint_locked')}</p>}
            </div>
          )
        })}
        {testToolsEnabled ? (
          <div className="anl-hint__assist">
            <button
              type="button"
              className="anl-button"
              disabled={!plan.canRun}
              onClick={() => {
                if (!window.confirm(index.ui('ui.test_assist_confirm'))) return
                for (const command of plan.commands) {
                  const result = session.dispatcher.execute(command)
                  api.refresh()
                  if (result.status === 'Rejected') {
                    setAssistMessage(`${result.reasonCode}: ${result.message}`)
                    return
                  }
                }
                setAssistMessage('정답 행동을 실행했습니다.')
              }}
            >
              {index.ui('ui.test_assist')}
            </button>
            {!plan.canRun ? <p className="anl-muted">{index.ui('ui.test_assist_missing')} ({plan.blocker})</p> : null}
            {assistMessage ? <p className="anl-feedback">{assistMessage}</p> : null}
          </div>
        ) : null}
      </div>
    </Modal>
  )
}
