import { useState } from 'react'
import type { GameSessionApi } from '../../app/useGameSession'
import { Modal } from '../../shared/components/Modal'
import type { LoadOutcome } from '../../shared/save/saveService'

interface SaveLoadPanelProps {
  api: GameSessionApi
  onClose: () => void
  onMessage: (message: string) => void
}

const SLOT_IDS = ['autosave', 'slot_01', 'slot_02', 'slot_03'] as const

const OUTCOME_MESSAGE: Record<LoadOutcome, string> = {
  Ok: '',
  Missing: '',
  Corrupt: 'ui.save_corrupt',
  Incompatible: 'ui.save_incompatible',
  UnknownIds: 'ui.save_unknown_id',
}

export function SaveLoadPanel({ api, onClose, onMessage }: SaveLoadPanelProps) {
  const { session, index } = api
  const [outcome, setOutcome] = useState<LoadOutcome | null>(null)
  const [selected, setSelected] = useState<string>('slot_01')
  if (!session || !index) return null

  const timestamp = (slot: string): string => session.saves.readSlotTimestamp(slot)

  const save = (slot: string): void => {
    if (session.saves.exists(slot) && !window.confirm(index.ui('ui.overwrite_confirm'))) return
    const ok = session.saveManual(slot)
    setOutcome(null)
    onMessage(ok ? index.ui('ui.auto_save_done') : index.ui('ui.save_failed'))
  }

  const load = (slot: string): void => {
    const result = session.loadGame(slot)
    setOutcome(result.outcome)
    if (result.loaded) {
      onMessage(index.ui('ui.load_success'))
      api.refresh()
      onClose()
      return
    }
    if (result.outcome === 'UnknownIds') {
      onMessage(index.ui('ui.save_unknown_id'))
    }
  }

  return (
    <Modal title={index.ui('ui.save_slot')} onClose={onClose}>
      <ul className="anl-save-list">
        {SLOT_IDS.map((slot) => {
          const label = slot === 'autosave' ? index.ui('ui.slot_autosave') : slot
          const savedAt = timestamp(slot)
          const active = selected === slot
          return (
            <li key={slot} className={`anl-save-slot${active ? ' anl-save-slot--active' : ''}`}>
              <button type="button" className="anl-save-slot__select" onClick={() => setSelected(slot)}>
                <strong>{label}</strong>
                <span>{savedAt ? `${index.ui('ui.saved_at')} ${formatTimestamp(savedAt)}` : index.ui('ui.empty_slot')}</span>
              </button>
              <span className="anl-save-slot__actions">
                <button type="button" className="anl-button anl-button--small" onClick={() => save(slot)}>
                  {index.ui('ui.save')}
                </button>
                <button
                  type="button"
                  className="anl-button anl-button--small"
                  disabled={!session.saves.exists(slot)}
                  onClick={() => load(slot)}
                >
                  {index.ui('ui.load')}
                </button>
              </span>
            </li>
          )
        })}
      </ul>
      {outcome && outcome !== 'Ok' && outcome !== 'Missing' ? (
        <div className="anl-save-warning" role="alert">
          <p>{index.ui(OUTCOME_MESSAGE[outcome] ?? 'ui.save_failed')}</p>
          {outcome === 'Corrupt' ? (
            <button
              type="button"
              className="anl-button anl-button--small"
              onClick={() => {
                const restored = session.saves.restoreBackup(selected)
                onMessage(restored ? index.ui('ui.load_success') : index.ui('ui.save_failed'))
              }}
            >
              {index.ui('ui.save_restore_backup')}
            </button>
          ) : null}
        </div>
      ) : null}
    </Modal>
  )
}

function formatTimestamp(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleString('ko-KR')
}
