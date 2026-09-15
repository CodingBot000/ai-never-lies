import { useCallback, useEffect, useMemo, useState } from 'react'
import type { AppSettings } from './settings'
import type { GameSessionApi } from './useGameSession'
import type { PressLineDto, SceneDefinitionDto } from '../core/content/dto'
import { Phases } from '../core/state/gameState'
import { assetUrl } from '../shared/assets/assetCatalog'
import { sceneLabel } from '../shared/i18n/sceneLabels'
import { formatContentText } from '../shared/i18n/contentText'
import { Modal } from '../shared/components/Modal'
import { isEditableTarget } from '../shared/keyboard/isEditableTarget'
import { ArchiveScreen } from '../features/archive/ArchiveScreen'
import { DialogueLayer } from '../features/dialogue/DialogueLayer'
import { lastPortraitBefore, resolveCurrentLine, sceneLines } from '../features/dialogue/dialogueModel'
import { EvidencePanel } from '../features/evidence/EvidencePanel'
import { HintPanel } from '../features/hints/HintPanel'
import { HubScreen } from '../features/hub/HubScreen'
import { PuzzleHost } from '../features/puzzles/PuzzleHost'
import { SaveLoadPanel } from '../features/save/SaveLoadPanel'
import { SettingsPanel } from '../features/settings/SettingsPanel'
import { TitleScreen } from '../features/title/TitleScreen'
import type { ModalKind, ScreenProps } from '../features/types'
import { DevPanel } from '../dev/DevPanel'
import { testToolsEnabled } from './env'
import './shell.css'

interface GameShellProps {
  api: GameSessionApi
  settings: AppSettings
  onSettingsChange: (patch: Partial<AppSettings>) => void
}

export function GameShell({ api, settings, onSettingsChange }: GameShellProps) {
  const { session, index, run } = api
  const [modal, setModal] = useState<ModalKind | null>(null)
  const [transient, setTransient] = useState<PressLineDto[]>([])
  const [feedback, setFeedback] = useState('')
  const [goalOpen, setGoalOpen] = useState(false)
  const [archiveOpen, setArchiveOpen] = useState(false)
  const [log, setLog] = useState<{ speaker: string; text: string }[]>([])
  const [lastSaveMessage, setLastSaveMessage] = useState('')

  const state = session?.state
  const scene = state && index ? index.scene(state.currentSceneId) : null

  const currentLine = useMemo(() => {
    if (!state || !index) return null
    return resolveCurrentLine(state, index, transient)
  }, [state, index, transient, api.revision])

  useEffect(() => {
    if (!currentLine || currentLine.transient || !currentLine.text) return
    setLog((previous) => {
      const last = previous[previous.length - 1]
      if (last && last.text === currentLine.text && last.speaker === currentLine.speaker) return previous
      return [...previous, { speaker: currentLine.speaker, text: currentLine.text }]
    })
  }, [currentLine?.text, currentLine?.speaker, currentLine?.transient])

  const inDialogue =
    state?.phase === Phases.PreDialogue || state?.phase === Phases.PostDialogue || state?.phase === Phases.BranchChoice
  const showTitle = !state?.currentSceneId || scene?.screenType === 'title'
  const titleCaseReady =
    scene?.screenType === 'title' && (state?.phase === Phases.ReadyToExit || state?.phase === Phases.Completed)

  const pushTransient = useCallback((lines: PressLineDto[]) => {
    if (lines.length > 0) setTransient((previous) => [...previous, ...lines])
  }, [])

  const handleFeedback = useCallback((message: string) => {
    setFeedback(message)
  }, [])

  const screenProps: ScreenProps = {
    api,
    onTransient: pushTransient,
    openModal: setModal,
    feedback: handleFeedback,
  }

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent): void => {
      if (isEditableTarget(event.target)) return
      if (event.key === 'Escape') {
        if (modal) {
          setModal(null)
        } else {
          setModal('menu')
        }
        return
      }
      if (event.key === ' ' && !modal && inDialogue) {
        event.preventDefault()
        if (transient.length > 0) setTransient((previous) => previous.slice(1))
        else run('AdvanceDialogue')
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [modal, inDialogue, transient.length, run])

  if (!session || !index || !state) return null

  const backgroundAsset = backgroundFor(state, scene)
  const backgroundUrl = backgroundAsset ? assetUrl(backgroundAsset) : null
  const lines = sceneLines(state, index)
  const interactionCue = state.phase === Phases.Interaction ? (INTERACTION_PORTRAIT_CUES[state.currentSceneId] ?? '') : ''
  const portraitAsset =
    interactionCue ||
    (inDialogue && state.phase !== Phases.BranchChoice
      ? currentLine?.portraitAssetId || lastPortraitBefore(lines, state.dialogueCursor)
      : '')
  const portraitUrl = portraitAsset ? assetUrl(portraitAsset) : null

  const newGame = (): void => {
    if (!window.confirm(index.ui('ui.new_game_confirm'))) return
    session.newGame()
    session.autoSave()
    setTransient([])
    setLog([])
    api.refresh()
  }

  const continueGame = (): void => {
    setModal('saves')
  }

  return (
    <div
      className="anl-shell"
      data-current-scene={state.currentSceneId}
      data-phase={state.phase}
      data-last-result={api.lastResult?.status ?? ''}
      data-last-reason={api.lastResult?.reasonCode ?? ''}
    >
      {testToolsEnabled ? (
        <div className="anl-dev-banner" role="status">
          {index.ui('ui.test_build_badge')} · {index.ui('ui.settings_test_tools')} {index.ui('ui.settings_on')} ·
          릴리스 빌드에는 포함되지 않습니다.
        </div>
      ) : null}
      <a className="anl-skip-link" href="#anl-main">
        본문으로 건너뛰기
      </a>
      {showTitle ? (
        <TitleScreen
          canContinue={session.saves.exists('autosave') || ['slot_01', 'slot_02', 'slot_03'].some((slot) => session.saves.exists(slot))}
          onNewGame={() => {
            if (titleCaseReady) {
              run('ContinueScene')
            } else {
              newGame()
            }
          }}
          onContinue={continueGame}
          onArchive={() => setArchiveOpen(true)}
          onSettings={() => setModal('menu')}
          archiveEnabled={false}
        />
      ) : (
        <div className="anl-stage">
          {backgroundUrl ? <img className="anl-stage__bg" src={backgroundUrl} alt="" /> : null}
          {scene?.id === 's010' && backgroundAsset === 'cg_emergency_before' ? (
            <div className="anl-stage-record-warning" role="status" aria-label="차내 경고 표시">
              <strong>EMERGENCY MANEUVER</strong>
              <span>기록 동기화 T-1.79</span>
            </div>
          ) : null}
          {portraitUrl ? <img className="anl-stage__portrait" src={portraitUrl} alt="" /> : null}
          <main id="anl-main" className="anl-stage__main">
            {state.phase === Phases.Interaction ? <PuzzleHost {...screenProps} /> : null}
            {scene && (scene.screenType === 'hub' || state.phase === Phases.HubActive) && state.phase !== Phases.Interaction ? (
              <HubScreen {...screenProps} />
            ) : null}
            {scene &&
            scene.id !== 's000' &&
            scene.screenType !== 'hub' &&
            state.phase !== Phases.Interaction &&
            state.phase !== Phases.HubActive &&
            (scene.screenType === 'case_archive' || scene.id === 's710') ? (
              <ArchiveScreen {...screenProps} />
            ) : null}
            {scene &&
            scene.screenType !== 'title' &&
            scene.screenType !== 'hub' &&
            state.phase !== Phases.Interaction &&
            state.phase !== Phases.HubActive &&
            state.phase !== Phases.PreDialogue &&
            state.phase !== Phases.PostDialogue &&
            state.phase !== Phases.BranchChoice &&
            scene.screenType !== 'case_archive' &&
            scene.id !== 's710' ? (
              <TransitionPanel {...screenProps} />
            ) : null}
          </main>
        </div>
      )}
      {inDialogue || transient.length > 0 ? (
        <DialogueLayer
          api={api}
          settings={settings}
          transient={transient}
          consumeTransient={() => setTransient((previous) => previous.slice(1))}
        />
      ) : null}

      <div className="anl-hud" role="toolbar" aria-label="게임 메뉴">
        {state.currentSceneId ? (
          <>
            <button type="button" className="anl-hud__button" onClick={() => setGoalOpen((value) => !value)}>
              {index.ui('ui.current_objective')}
            </button>
            <button type="button" className="anl-hud__button" onClick={() => setModal('evidence')}>
              {index.ui('ui.evidence')}
            </button>
            <button type="button" className="anl-hud__button" onClick={() => setModal('log')}>
              {index.ui('ui.notebook')}
            </button>
            <button type="button" className="anl-hud__button" onClick={() => setModal('hints')}>
              {index.ui('ui.hints')}
            </button>
            <button type="button" className="anl-hud__button" onClick={() => setModal('menu')}>
              {index.ui('ui.menu')}
            </button>
          </>
        ) : null}
      </div>

      {goalOpen ? (
        <div className="anl-goal" role="status">
          <strong>{index.ui('ui.current_objective')}</strong>
          <p>{goalText(api)}</p>
          <button type="button" className="anl-button anl-button--small" onClick={() => setGoalOpen(false)}>
            {index.ui('ui.close')}
          </button>
        </div>
      ) : null}

      {feedback ? (
        <div className="anl-toast" role="status" aria-live="polite">
          <span>{feedback}</span>
          <button type="button" className="anl-button anl-button--small" onClick={() => setFeedback('')}>
            {index.ui('ui.close')}
          </button>
        </div>
      ) : null}

      {lastSaveMessage ? (
        <div className="anl-toast anl-toast--save" role="status" aria-live="polite">
          {lastSaveMessage}
        </div>
      ) : null}

      {modal === 'evidence' ? <EvidencePanel api={api} onClose={() => setModal(null)} /> : null}
      {modal === 'hints' ? <HintPanel api={api} onClose={() => setModal(null)} /> : null}
      {modal === 'saves' ? (
        <SaveLoadPanel
          api={api}
          onClose={() => setModal(null)}
          onMessage={setLastSaveMessage}
        />
      ) : null}
      {modal === 'log' ? (
        <Modal title={index.ui('ui.notebook')} onClose={() => setModal(null)} wide>
          <ol className="anl-log">
            {log.length === 0 ? <li className="anl-muted">기록된 대사가 없습니다.</li> : null}
            {log.map((entry, position) => (
              <li key={`${position}-${entry.text.slice(0, 12)}`}>
                <strong>{entry.speaker}</strong>
                <p>{entry.text}</p>
              </li>
            ))}
          </ol>
        </Modal>
      ) : null}
      {modal === 'menu' ? (
        <Modal title={index.ui('ui.menu')} onClose={() => setModal(null)}>
          <SettingsPanel settings={settings} onChange={onSettingsChange} />
          <div className="anl-menu-actions">
            <button type="button" className="anl-button" onClick={() => setModal('saves')}>
              {index.ui('ui.save_slot')}
            </button>
            <button
              type="button"
              className="anl-button"
              onClick={() => {
                setModal(null)
                if (window.confirm(index.ui('ui.new_game_confirm'))) {
                  session.newGame()
                  session.autoSave()
                  api.refresh()
                }
              }}
            >
              {index.ui('ui.new_case')}
            </button>
            <button type="button" className="anl-button" onClick={() => setModal('log')}>
              {index.ui('ui.notebook')}
            </button>
            {testToolsEnabled ? (
              <button type="button" className="anl-button" onClick={() => setModal('dev')}>
                {index.ui('ui.dev_panel')}
              </button>
            ) : null}
            <button type="button" className="anl-button anl-button--primary" onClick={() => setModal(null)}>
              {index.ui('ui.close')}
            </button>
          </div>
        </Modal>
      ) : null}
      {modal === 'dev' && testToolsEnabled ? <DevPanel api={api} onClose={() => setModal(null)} /> : null}
      {archiveOpen ? (
        <Modal title={index.ui('ui.archive_title')} onClose={() => setArchiveOpen(false)} wide>
          <ArchiveScreen {...screenProps} />
        </Modal>
      ) : null}
    </div>
  )
}

function TransitionPanel({ api, openModal }: ScreenProps) {
  const { session, index, run } = api
  if (!session || !index) return null
  const state = session.state
  const scene = index.scene(state.currentSceneId)
  if (!scene) return null
  const parent = [...index.scenes.values()].find((candidate) => candidate.childSceneIds?.includes(scene.id))?.id
  const transition = session.dispatcher.transitionAvailable()
  const blocked = !transition.available && !parent

  return (
    <div className="anl-transition">
      <div className="anl-transition__panel">
        <h1>{sceneLabel(scene.id)}</h1>
        {scene.tipText ? <p className="anl-muted">{formatContentText(scene.tipText, index)}</p> : null}
        <div className="anl-transition__actions">
          <button
            type="button"
            className="anl-button anl-button--primary"
            data-action="continue-scene"
            disabled={!transition.available && !parent}
            onClick={() => {
              if (transition.available) {
                run('ContinueScene')
              } else {
                run('ReturnToHub')
              }
            }}
          >
            {transition.available ? index.ui('ui.continue_scene') : parent ? index.ui('ui.return_to_hub') : index.ui('ui.continue_scene')}
          </button>
          {blocked ? (
            <>
              <p className="anl-feedback">{blockedMessage(index, state, transition.reason)}</p>
              <button type="button" className="anl-button" onClick={() => openModal('evidence')}>
                {index.ui('ui.evidence')} {index.ui('ui.confirm')}
              </button>
            </>
          ) : null}
        </div>
      </div>
    </div>
  )
}

function blockedMessage(index: NonNullable<GameSessionApi['index']>, state: GameSessionState, reason: string): string {
  if (reason.startsWith('need_read:')) {
    const id = reason.slice('need_read:'.length)
    return `필수 자료를 열람하세요: ${index.evidenceDef(id)?.title ?? id}`
  }
  if (reason.startsWith('remaining:')) return index.ui('ui.hub_locked_reason')
  if (reason === 'conditions_not_met') {
    const scene = index.scene(state.currentSceneId)
    const unread = (scene?.requiredReadEvidenceIds ?? [])
      .filter((id) => !state.hasRead(id))
      .map((id) => index.evidenceDef(id)?.title ?? id)
    if (unread.length > 0) return `필수 자료를 열람하세요: ${unread.join(', ')}`
    return '아직 진행 조건이 충족되지 않았습니다. 필요한 조사와 증거를 확인하세요.'
  }
  return index.ui('ui.interaction_hint')
}

type GameSessionState = GameSessionApi['session'] extends { state: infer S } | null ? S : never

function goalText(api: GameSessionApi): string {
  const session = api.session
  const index = api.index
  if (!session || !index) return ''
  const context = session.hints.resolve(session.state)
  const parts: string[] = []
  if (context.sceneTip) parts.push(context.sceneTip)
  if (context.nextAction) parts.push(context.nextAction)
  if (context.levels[0]) parts.push(context.levels[0].text)
  return parts.join(' · ') || index.ui('ui.interaction_hint')
}

/** 마스터 12.1 표정 cue 중 대사가 아닌 추궁/쟁점 순간에 쓰는 초상 */
const INTERACTION_PORTRAIT_CUES: Record<string, string> = {
  s310: 'chr_min_angry',
  s420: 'chr_lee_irritated',
  s620: 'chr_seo_unsettled',
}

function backgroundFor(state: { currentSceneId: string; dialogueCursor: number }, scene: SceneDefinitionDto | null): string {
  if (!scene) return 'bg_bureau_night'
  if (scene.id === 's650') return 'cg_hearing_wide'
  if (scene.id === 's710') return 'cg_epilogue_window'
  if (scene.id === 's010') {
    if (state.dialogueCursor < 2) return 'cg_ride_before'
    if (state.dialogueCursor < 5) return 'cg_emergency_before'
    return 'cg_scene_after'
  }
  return scene.backgroundAssetId
}
