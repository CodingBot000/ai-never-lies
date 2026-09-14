# 개발 환경 조사 (W7/W8 웹 기준)

조사 시각: 2026-09-15 (UTC+09:00). 비밀번호·토큰·라이선스 파일은 기록하지 않는다.

## 하드웨어·운영체제

| 항목 | 값 |
| --- | --- |
| OS | macOS 26.5.1 (Build 25F80) |
| 아키텍처 | arm64 (Apple Silicon) |
| 메모리 | 64 GB |
| 디스크 여유 | 약 950 GiB (조사 시점) |

## 웹 개발 환경

| 항목 | 값 |
| --- | --- |
| Node.js | v24.5.0 (`web/.nvmrc` = 24) |
| npm | 11.5.1 |
| React / React DOM | 19.3.0 |
| Vite | 8.3.0 |
| TypeScript | 6.0.3 (strict, noUncheckedIndexedAccess) |
| Vitest | 5.0.0 |
| Testing Library | @testing-library/react 16.3.3, jest-dom 7.0.1, user-event 14.6.7 |
| jsdom | 29.1.1 (Node 24.5 지원) |
| Playwright | 1.63.0, Chromium·WebKit 26.6 설치 |
| 린터 | oxlint 1.82.0 |
| Python | 3.14.6 (`/opt/homebrew/bin/python3`) |
| Safari | 26.5, safaridriver 포함 |

의존성은 `web/package-lock.json`으로 잠근다. 테스트에 사용한 Chromium과 WebKit은 Playwright 관리 브라우저다.

## Unity

| 항목 | 값 |
| --- | --- |
| 사용 여부 | 사용하지 않음 |
| 현재 저장소 | `Game/`·`Builds/` 제거 완료 |
| 복구 기준선 | 커밋 `3dd0f08`, 태그 `unity-final-20260914` |

## 아트·폰트

- `art_delivery/runtime/ep01/`: 승인된 PNG 54개.
- 나눔고딕 Regular/Bold, SIL Open Font License 1.1.
- 글리프 663개 검사, 누락 0.
- `handoff/art/ASSET_READY.json`과 manifest·문서 해시 검증 PASS.

## 현재 자동 검증

| 명령 | 결과 |
| --- | --- |
| `npm run lint` | PASS, 경고 0 |
| `npm run typecheck` | PASS |
| `npm run test:run` | PASS 62 |
| `npm run test:e2e` | PASS 11 Chromium |
| `npm run test:e2e:webkit` | PASS 11 WebKit |
| Safari 26.5 production 스모크 | PASS, 타이틀·설정 모달 |
| `npm run test:qa:keyboard` | PASS |
| `npm run test:qa:puzzles` | PASS |
| `npm run test:qa:responsive` | PASS |
| `npm run test:qa:overlays` | PASS |
| `npm run test:qa:offline` | PASS |
| `npm run build` / `check:release` | PASS |

## 사람 확인 필요

- Mac Safari에서 사람이 직접 육안 확인하는 수동 QA.
- 사람 직접 전체 완주 2회와 녹화·스크린샷.
- 최종 사용자 승인.
