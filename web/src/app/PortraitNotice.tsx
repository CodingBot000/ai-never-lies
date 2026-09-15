import { useEffect, useState } from 'react'

const PORTRAIT_QUERY = '(orientation: portrait) and (max-width: 1024px)'

/** 세로(portrait) 모바일에서 가로 전환을 안내한다. */
export function PortraitNotice() {
  const [portrait, setPortrait] = useState(
    () =>
      typeof window !== 'undefined' &&
      typeof window.matchMedia === 'function' &&
      window.matchMedia(PORTRAIT_QUERY).matches,
  )

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return
    const media = window.matchMedia(PORTRAIT_QUERY)
    const onChange = (): void => setPortrait(media.matches)
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  const startLandscape = async (): Promise<void> => {
    try {
      const element = document.documentElement
      if (typeof element.requestFullscreen === 'function' && !document.fullscreenElement) {
        await element.requestFullscreen()
      }
    } catch {
      // 전체화면이 거부되면 회전 안내만 유지한다.
    }
    try {
      const orientation = window.screen.orientation as unknown as {
        lock?: (orientation: string) => Promise<void>
      }
      if (typeof orientation?.lock === 'function') {
        await orientation.lock('landscape')
      }
    } catch {
      // iOS Safari 등 미지원 브라우저는 사용자가 직접 회전해야 한다.
    }
  }

  if (!portrait) return null
  return (
    <div className="anl-portrait-notice" role="alert">
      <div>
        <p style={{ fontSize: 'var(--anl-text-2xl)', marginBottom: 'var(--anl-space-3)' }}>가로 화면으로 돌려주세요</p>
        <p className="anl-muted">이 게임은 가로 화면에 맞춰져 있습니다.</p>
        <button type="button" className="anl-button anl-button--primary" onClick={startLandscape}>
          가로로 시작
        </button>
        <p className="anl-muted" style={{ marginTop: 'var(--anl-space-2)' }}>
          일부 브라우저에서는 버튼이 동작하지 않으니, 그때는 기기를 직접 가로로 돌려주세요.
        </p>
      </div>
    </div>
  )
}
