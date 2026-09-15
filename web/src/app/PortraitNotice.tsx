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

  if (!portrait) return null
  return (
    <div className="anl-portrait-notice" role="alert">
      <div>
        <p style={{ fontSize: 'var(--anl-text-2xl)', marginBottom: 'var(--anl-space-3)' }}>가로 화면으로 돌려주세요</p>
        <p className="anl-muted">이 게임은 가로 화면에 맞춰져 있습니다.</p>
      </div>
    </div>
  )
}
