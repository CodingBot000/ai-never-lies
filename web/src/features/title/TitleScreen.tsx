import { useState } from 'react'
import { APP_TITLE, ART_CONTRACT_VERSION, CONTENT_VERSION, EPISODE_SUBTITLE, SAVE_SCHEMA_VERSION } from '../../app/env'
import { Modal } from '../../shared/components/Modal'
import './title.css'

interface TitleScreenProps {
  canContinue: boolean
  archiveEnabled: boolean
  onNewGame: () => void
  onContinue: () => void
  onArchive: () => void
  onSettings: () => void
}

export function TitleScreen({
  canContinue,
  archiveEnabled,
  onNewGame,
  onContinue,
  onArchive,
  onSettings,
}: TitleScreenProps) {
  const [infoOpen, setInfoOpen] = useState(false)

  return (
    <section className="anl-title" aria-labelledby="anl-title-name">
      <div className="anl-title__layout">
        <div className="anl-title__copy">
          <p className="anl-title__eyebrow">교통사고 조사 비주얼 노벨</p>
          <h1 id="anl-title-name" className="anl-title__name">
            {APP_TITLE}
          </h1>
          <p className="anl-title__subtitle">{EPISODE_SUBTITLE}</p>
          <p className="anl-title__tagline">
            기록은 남아 있다. 그러나 기록이 말하는 순서는 하나가 아니다. 1.8초 전의 명령을 추적한다.
          </p>
        </div>
        <ul className="anl-title__menu">
          <li className="anl-title__item">
            <button type="button" className="anl-menu-button anl-menu-button--primary" onClick={onNewGame}>
              <span>새 게임</span>
              <span className="anl-menu-button__hint">처음부터 조사 시작</span>
            </button>
          </li>
          <li className="anl-title__item">
            <button type="button" className="anl-menu-button" onClick={onContinue} disabled={!canContinue}>
              <span>이어하기</span>
              <span className="anl-menu-button__hint">자동 저장</span>
            </button>
            {!canContinue ? <p className="anl-title__reason">저장된 진행이 없습니다.</p> : null}
          </li>
          <li className="anl-title__item">
            <button type="button" className="anl-menu-button" onClick={onArchive} disabled={!archiveEnabled}>
              <span>사건 기록</span>
              <span className="anl-menu-button__hint">Episode 01 완료 후 열립니다</span>
            </button>
          </li>
          <li className="anl-title__item">
            <button type="button" className="anl-menu-button" onClick={onSettings}>
              <span>설정</span>
              <span className="anl-menu-button__hint">글자 크기·출력 속도</span>
            </button>
          </li>
          <li className="anl-title__item">
            <button type="button" className="anl-menu-button" onClick={() => setInfoOpen(true)}>
              <span>정보</span>
              <span className="anl-menu-button__hint">버전·라이선스</span>
            </button>
          </li>
        </ul>
      </div>
      <footer className="anl-title__footer">
        <span>콘텐츠 {CONTENT_VERSION}</span>
        <span>아트 계약 {ART_CONTRACT_VERSION}</span>
        <span>저장 스키마 v{SAVE_SCHEMA_VERSION}</span>
      </footer>
      {infoOpen ? (
        <Modal title="정보" onClose={() => setInfoOpen(false)}>
          <p>《{APP_TITLE}》 {EPISODE_SUBTITLE}</p>
          <p>콘텐츠 버전 {CONTENT_VERSION} · 아트 계약 {ART_CONTRACT_VERSION} · 저장 스키마 v{SAVE_SCHEMA_VERSION}</p>
          <p>
            이 작품의 인물·회사·사건·조사기관은 허구입니다. 게임 실행 중 외부 AI API·인터넷 조회를 사용하지 않습니다.
          </p>
          <p>
            한국어 글꼴: 나눔고딕(NanumGothic), SIL Open Font License 1.1. 라이선스 원문은 배포
            `fonts/NanumGothic-OFL.txt`에 포함됩니다.
          </p>
        </Modal>
      ) : null}
    </section>
  )
}
