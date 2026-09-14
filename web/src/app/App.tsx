import { useEffect, useState } from 'react'
import { DevelopmentNotice } from '../shared/components/DevelopmentNotice'
import { Modal } from '../shared/components/Modal'
import { hasAnySave } from '../shared/save/saveKeys'
import { SettingsPanel } from '../features/settings/SettingsPanel'
import { TitleScreen } from '../features/title/TitleScreen'
import { applySettings, loadSettings, saveSettings, type AppSettings } from './settings'
import { testToolsEnabled } from './env'
import '../features/settings/settings.css'

type Screen =
  | { kind: 'title' }
  | { kind: 'new-game' }
  | { kind: 'continue' }

export function App() {
  const [screen, setScreen] = useState<Screen>({ kind: 'title' })
  const [settings, setSettings] = useState<AppSettings>(() => loadSettings())
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [canContinue, setCanContinue] = useState(() => hasAnySave())

  useEffect(() => {
    applySettings(settings)
  }, [settings])

  const updateSettings = (patch: Partial<AppSettings>): void => {
    setSettings((previous) => {
      const next = { ...previous, ...patch }
      saveSettings(next)
      return next
    })
  }

  const returnToTitle = (): void => {
    setCanContinue(hasAnySave())
    setScreen({ kind: 'title' })
  }

  return (
    <div className="anl-app">
      <a className="anl-skip-link" href="#anl-main">
        본문으로 건너뛰기
      </a>
      {testToolsEnabled ? (
        <div className="anl-dev-banner" role="status">
          개발 모드 · 테스트 지원(VITE_ANL_TEST_TOOLS)이 켜져 있습니다. 이 표시는 릴리스 빌드에 포함되지 않습니다.
        </div>
      ) : null}
      <main id="anl-main" className="anl-app__main" tabIndex={-1}>
        {screen.kind === 'title' ? (
          <TitleScreen
            canContinue={canContinue}
            onNewGame={() => setScreen({ kind: 'new-game' })}
            onContinue={() => setScreen({ kind: 'continue' })}
            onOpenSettings={() => setSettingsOpen(true)}
          />
        ) : null}
        {screen.kind === 'new-game' ? (
          <DevelopmentNotice
            title="새 사건"
            description="새 게임 흐름(타이틀 → 경고 → s000 → s010)은 웹 이전 W3~W4에서 코어와 함께 연결됩니다. 현재는 타이틀과 기본 내비게이션만 구현된 상태입니다."
            onBack={returnToTitle}
          />
        ) : null}
        {screen.kind === 'continue' ? (
          <DevelopmentNotice
            title="이어하기"
            description="저장 복원 흐름은 웹 이전 W3에서 localStorage 저장 어댑터와 함께 연결됩니다. 현재는 저장 파일이 있어도 복원 화면이 아직 없습니다."
            onBack={returnToTitle}
          />
        ) : null}
      </main>
      {settingsOpen ? (
        <Modal title="설정" onClose={() => setSettingsOpen(false)}>
          <SettingsPanel settings={settings} onChange={updateSettings} />
        </Modal>
      ) : null}
    </div>
  )
}
