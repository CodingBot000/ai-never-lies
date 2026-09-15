import { useEffect, useState } from 'react'
import { GameShell } from './GameShell'
import { applySettings, loadSettings, saveSettings, type AppSettings } from './settings'
import { useGameSession } from './useGameSession'
import { ErrorBoundary } from './ErrorBoundary'
import { PortraitNotice } from './PortraitNotice'
import { APP_TITLE } from './env'
import './shell.css'

export function App() {
  const api = useGameSession()
  const [settings, setSettings] = useState<AppSettings>(() => loadSettings())

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

  if (api.boot.status === 'loading') {
    return (
      <div className="anl-boot" role="status">
        <h1>{APP_TITLE}</h1>
        <p>콘텐츠와 이미지를 불러오는 중입니다…</p>
      </div>
    )
  }

  if (api.boot.status === 'error' || !api.session) {
    return (
      <div className="anl-boot anl-boot--error" role="alert">
        <h1>{APP_TITLE}</h1>
        <p>콘텐츠를 불러오지 못했습니다. 아래 항목을 확인한 뒤 새로고침해 주세요.</p>
        <ul>
          {api.boot.errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
        <button type="button" onClick={() => window.location.reload()}>
          새로고침
        </button>
      </div>
    )
  }

  return (
    <ErrorBoundary>
      <PortraitNotice />
      <GameShell api={api} settings={settings} onSettingsChange={updateSettings} />
    </ErrorBoundary>
  )
}
