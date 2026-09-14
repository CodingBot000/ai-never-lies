import type { ReactNode } from 'react'
import './notice.css'

interface DevelopmentNoticeProps {
  title: string
  description: string
  onBack: () => void
  children?: ReactNode
}

/** 아직 구현되지 않은 화면의 정직한 개발 상태 표시 */
export function DevelopmentNotice({ title, description, onBack, children }: DevelopmentNoticeProps) {
  return (
    <section className="anl-notice" aria-labelledby="anl-notice-title">
      <div className="anl-notice__panel">
        <p className="anl-notice__badge">개발 중</p>
        <h1 id="anl-notice-title">{title}</h1>
        <p className="anl-notice__description">{description}</p>
        {children}
        <button type="button" className="anl-menu-button" onClick={onBack}>
          타이틀로 돌아가기
        </button>
      </div>
    </section>
  )
}
