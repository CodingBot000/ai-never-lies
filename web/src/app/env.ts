export const APP_TITLE = 'AI는 거짓말을 하지 않는다'
export const EPISODE_SUBTITLE = 'Episode 01 — 1.8초 전의 명령'

export const CONTENT_VERSION = 'ep01.v1.0.0'
export const SAVE_SCHEMA_VERSION = 1
export const ART_CONTRACT_VERSION = 'art.v1'

/** Vite 개발 서버 여부 */
export const isDevelopment = import.meta.env.DEV

/**
 * 테스트 지원(정답 행동 실행·개발 패널·픽스처 로더) 노출 여부.
 * 개발 모드에서 VITE_ANL_TEST_TOOLS=1일 때만 켜진다. 릴리스 빌드에는 포함되지 않는다.
 */
export const testToolsEnabled =
  isDevelopment && import.meta.env.VITE_ANL_TEST_TOOLS === '1'
