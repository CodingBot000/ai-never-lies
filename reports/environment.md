# 개발 환경 조사 (G2 → W1 웹 기준 갱신)

조사 시각: 2026-09-14 (UTC+09:00). 사용자 비밀번호·라이선스 파일·토큰은 기록하지 않는다.

## 하드웨어·운영체제

| 항목 | 값 |
| --- | --- |
| OS | macOS 14.6.1 (Build 23G93) |
| 아키텍처 | x86_64 (Intel) |
| 메모리 | 32 GB |
| 디스크 여유 | 약 127 GB (조사 시점) |

## 웹 개발 환경 (W1, 2026-09-14 갱신)

| 항목 | 값 |
| --- | --- |
| Node.js | v24.19.0 (`web/.nvmrc` = 24) |
| npm | 11.17.0 |
| React / React DOM | 19.2.8 |
| Vite | 8.3.0 |
| TypeScript | 6.0.2 (strict, noUncheckedIndexedAccess) |
| Vitest | 5.0.0 (+ @vitest/coverage-v8) |
| Testing Library | @testing-library/react 16.3.3, jest-dom 7.0.1, user-event 14.6.7 |
| Playwright | 1.63.0, Chromium 설치 완료 |
| 린터 | oxlint 1.81.0 |
| jsdom | 30.0.1 |
| Python (검증 도구) | /Users/switch/miniconda3/bin/python3 (3.9.19) |
| PIL (아트 검증) | 프로젝트 로컬 `.venv-art` (Python 3.13, Pillow 12.3.0) |
| 브라우저 | Google Chrome 설치, Safari 설치 (수동 QA용) |

의존성 버전은 `web/package-lock.json`으로 잠근다.

## Unity

| 항목 | 값 |
| --- | --- |
| 사용 여부 | **사용하지 않음** (ANL-WEB-MIGRATION W0에서 개발 종료) |
| 기존 프로젝트 | `Game/` — 읽기 전용 참고 자료. 기준선 커밋 `3dd0f08`, 태그 `unity-final-20260914` |
| 기존 설치 | Unity Hub / Editor 6000.3.22f1이 설치되어 있으나 이 프로젝트에서 실행하지 않는다 |

## 폰트

- 사용 폰트: NanumGothic (Regular/Bold), SIL Open Font License 1.1 — `web/public/fonts/NanumGothic-*.ttf`, 라이선스 원문 `NanumGothic-OFL.txt`.
- OFL 폰트이므로 배포 가능. 글리프 누락 검사는 W2에서 `tools/check_font_glyphs.py`로 실행 예정.

## 아트 인계

- `art_delivery/runtime/` 54개 PNG, `handoff/art/ASSET_READY.json` 기준 G1 통과.
- W0에서 세 문서 개정 후 문서 해시 갱신 완료(ASSET_READY PASS).
- preflight 결과: `reports/preflight_art.json`, `reports/preflight_art.md` (PASS).

## 웹 검증 결과 (W1)

| 명령 | 결과 |
| --- | --- |
| `npm run lint` | PASS (0 warnings, 0 errors) |
| `npm run typecheck` | PASS |
| `npm run test:run` | PASS (3 files, 14 tests) |
| `npm run test:e2e` | PASS (3 Chromium tests) |
| `npm run build` | PASS (`dist/` 생성) |
| `npm run check:release` | PASS (테스트 지원 문자열 없음) |
| `npm run preview` | HTTP 200, 타이틀 렌더 확인 |
| 화면 증빙 | `reports/screenshots/w1_title_1920x1080.png`, `w1_title_mobile_390x844.png` |

## 미검증/제한

- Safari 데스크톱 QA는 W7에서 수동으로 수행한다(NOT_RUN, W1 시점).
- Windows 환경 검증은 범위에서 제외한다(웹 정적 빌드이므로 OS 독립).
- 스크린리더 지원은 구현·검증하지 않았다(W4~W7에서 부분 지원·검증 예정).
