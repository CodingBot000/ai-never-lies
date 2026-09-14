# QA 리포트 (웹, W7 진행분)

- 기준일: 2026-09-14, 대상: `web/` production build
- 브라우저: Chromium 143 (Playwright 1.63, 데스크톱 1920×1080·모바일 390×844)
- 실행 명령과 결과는 아래 표. 미실행 항목은 `docs/WEB_IMPLEMENTATION_STATUS.md` 참조.

| 검증 | 명령 | 결과 |
| --- | --- | --- |
| 린트 | `npm run lint` | PASS 0 warnings |
| 타입 | `npm run typecheck` | PASS |
| 단위·통합 | `npm run test:run` | PASS 61 |
| E2E | `npm run test:e2e` | PASS 9 |
| 정적 검증 | `validate:content`·`assets:check`·`fonts:check` | PASS |
| 빌드·경계 | `npm run build` + `npm run check:release` | PASS |
| 배포 | Vercel production (`ai-never-lies.vercel.app`) | READY, `/`·콘텐츠·이미지·폰트 200 |

## E2E 커버리지

- `title.spec.ts`: 타이틀·콘솔 오류 0·모바일 오버플로 0·새 게임 경고.
- `assets.spec.ts`: 54 이미지·콘텐츠 JSON 404 없음, 추측 경로 미제공.
- `chapter1.spec.ts`: 실제 클릭으로 경고→s010→s020 분기·필수 열람→s100 허브→s110 조사(p01)→s120 심문(p02), 외부 도메인 요청 0.
- `resume.spec.ts`: 새 게임→새로고침→이어하기→자동 저장 복원.
- `walkthrough.spec.ts`: s000→s710 자동 진행, 퍼즐 20/20, 콘솔 오류 0.
- `screenshots.spec.ts`: W4 대표 화면 10장 (`reports/screenshots/web_w4_*`).

## 알려진 제한

- Safari·Firefox 자동 검증 없음(Chromium만).
- 사람 육안 완주·150% 전 화면·키보드 전용 완주 미실행.
- 서비스워커 없음 → 최초 로드 후에는 같은 출처 자원만 사용하지만 완전 오프라인 재로드는 불가.
