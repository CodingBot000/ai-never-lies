import { useState } from 'react'
import type { GameSessionApi } from '../app/useGameSession'
import { CommandKinds, Origins } from '../core/commands/commandTypes'
import { Modal } from '../shared/components/Modal'
import { advanceCurrentScene } from './solveScene'

interface DevPanelProps {
  api: GameSessionApi
  onClose: () => void
}

export function DevPanel({ api, onClose }: DevPanelProps) {
  const { session, index, run, lastResult } = api
  const [sceneId, setSceneId] = useState('s150')
  const [message, setMessage] = useState('')
  if (!session || !index) return null
  const state = session.state

  return (
    <Modal title={index.ui('ui.dev_panel')} onClose={onClose} wide>
      <dl className="anl-dev">
        <dt>{index.ui('ui.dev_scene')}</dt>
        <dd>{state.currentSceneId || '-'}</dd>
        <dt>{index.ui('ui.dev_phase')}</dt>
        <dd>{state.phase}</dd>
        <dt>{index.ui('ui.dev_puzzle')}</dt>
        <dd>{state.activePuzzleId || '-'}</dd>
        <dt>{index.ui('ui.dev_last_result')}</dt>
        <dd>
          {lastResult ? `${lastResult.status} · ${lastResult.reasonCode} · ${lastResult.message}` : '-'}
        </dd>
        <dt>{index.ui('ui.dev_blocker')}</dt>
        <dd>{session.blockingError || '-'}</dd>
        <dt>보유 증거</dt>
        <dd>{state.ownedEvidence.length} / 해결 퍼즐 {state.solvedPuzzles.length} / 완료 씬 {state.completedScenes.length}</dd>
        <dt>완료 씬 목록</dt>
        <dd>{[...state.completedScenes].sort().join(', ')}</dd>
        <dt>미해결 퍼즐</dt>
        <dd>
          {['p01','p02','p03','p04','p05','p06','p07','p08','p09','p10','p11','p12','p13','p14','p15','p16','p17','p18','p19','p20']
            .filter((id) => !state.isPuzzleSolved(id))
            .join(', ') || '-'}
        </dd>
        <dt>명령 시퀀스</dt>
        <dd>{state.lastCommandSequence}</dd>
      </dl>
      <div className="anl-dev__row">
        <label className="anl-check">
          <span>씬 점프 (fixture)</span>
          <input
            type="text"
            value={sceneId}
            onChange={(event) => setSceneId(event.target.value)}
            aria-label="씬 ID"
          />
        </label>
        <button
          type="button"
          className="anl-button anl-button--small"
          onClick={() => {
            const result = run('GoToScene', { targetId: sceneId, origin: Origins.Fixture })
            setMessage(result ? `${result.status} ${result.reasonCode}` : 'no session')
          }}
        >
          {index.ui('ui.dev_fixture_load')}
        </button>
      </div>
      <p className="anl-muted">{index.ui('ui.dev_fixture_warning')}</p>
      <div className="anl-dev__row">
        <button
          type="button"
          className="anl-button anl-button--small"
          onClick={() => {
            const reason = advanceCurrentScene(index, state, session.dispatcher)
            api.refresh()
            setMessage(`진행: ${reason}`)
          }}
        >
          씬 자동 진행
        </button>
        <button
          type="button"
          className="anl-button anl-button--small"
          onClick={() => {
            const result = run(CommandKinds.SaveSlot, { targetId: 'autosave' })
            setMessage(result ? `${result.status} ${result.reasonCode}` : 'no session')
          }}
        >
          자동 저장 실행
        </button>
        <button
          type="button"
          className="anl-button anl-button--small"
          onClick={() => {
            const result = run(CommandKinds.SubmitPuzzle)
            setMessage(result ? `${result.status} ${result.reasonCode}` : 'no session')
          }}
        >
          SubmitPuzzle 진단
        </button>
      </div>
      {message ? <p className="anl-feedback">{message}</p> : null}
    </Modal>
  )
}
