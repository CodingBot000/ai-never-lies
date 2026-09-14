# CHANGELOG — 《AI는 거짓말을 하지 않는다》 제작 이력

형식: 날짜 · 변경 · 이유 · 영향 · 해결 · 관련 문서/해시.

---

## 2026-09-14 — Unity → Web 전환 (ANL-WEB-MIGRATION W0)

**결정:** 최종 실행 환경을 웹 브라우저로 확정하고 Unity 기반 개발을 종료한다. 정본 계획은 `docs/03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md`.

**이유:** Unity는 최종 실행 환경(서버·설치 없이 실행 가능한 웹)과 맞지 않으며, 기존 Unity 구현은 웹 이전의 참고 자료로만 유지한다.

**영향과 처리:**

| 대상 | 변경 | 보존 |
| --- | --- | --- |
| Unity 프로젝트 `Game/` | 실행·수정·확장 중단, 읽기 전용 참고 자료 | 기준선 커밋 `3dd0f08`, 태그 `unity-final-20260914` |
| `docs/00_MASTER_PLAN.md` | 구현 기준을 React/TypeScript/Vite 웹으로 개정. G1~G5 게이트, 저장(`localStorage`), 아트 경로(`web/public/art/`), 개발 모드 플래그, 참고 자료 갱신 | 사건·ID·대사·정답 등 콘텐츠 정본은 불변 |
| `docs/01_IMAGE_GENERATION_INSTRUCTIONS.md` | 좌표 계약 표현을 웹 CSS 표시 영역 기준으로 개정. 배포 자산 경로를 `web/public/art/`로 명시 | 이미지 54개 바이트·manifest·좌표 수치는 불변 |
| `docs/02_CODE_GENERATION_INSTRUCTIONS.md` | `ANL-CODE-WEB` 2.0.0으로 전면 개정. React/TS/Vite 구조·로직·저장·테스트·빌드 계약을 웹 기준으로 재작성 | 콘텐츠 DTO·명령·퍼즐 판정·고정 ID·테스트 계약 유지 |
| `handoff/art/ASSET_READY.json` | 세 문서 SHA-256 갱신, `verifiedAtUtc` 갱신 | `manifestSha256`, 54개 자산·검수 기록 불변 |
| 콘텐츠 JSON | 위치를 `content/ep01/`로 이전 예정(W2). 내용·ID 불변 | 원본 `Game/Assets/_Game/Content/ep01/`는 이전 완료까지 유지 |
| 아트 PNG | `art_delivery/runtime/ep01/`가 정본. `web/public/art/ep01/`로 동기화 예정(W2) | PNG 바이트 불변, 재생성 없음 |
| 기존 C# Core/테스트 | TypeScript·Vitest로 동작 계약 이전(W3). 기존 코드는 참고만 | 커밋·태그로 보존 |
| Unity 빌드 `Builds/` | 최종 산출물에서 사용하지 않음. W8에서 제거 검토 | `.gitignore`로 미추적 유지 |

**문서 해시 (2026-09-14, W0 시점):**

| 문서 | SHA-256 |
| --- | --- |
| `docs/00_MASTER_PLAN.md` | `00790af9733c81b7dbc2b3dfcb0c913a27aa1d82880276f8ff303e05bd7d1dce` |
| `docs/01_IMAGE_GENERATION_INSTRUCTIONS.md` | `6cc43d420c3d00c8d58987f5c57a780d54d0f251fa61c0dc7cc355b38ec435af` |
| `docs/02_CODE_GENERATION_INSTRUCTIONS.md` | `3b4624c23b59c9ddcdaae92868ed5600070312a0806f93b66a767fcedad8ca18` |
| `handoff/art/manifest.lock.json` | `8d1e4a63829632972187bac6e9538a161dee0b72785859058d5e57ecc1968bc3` (불변) |

**미해결/후속:** W1~W8. Unity 제거는 W5·W6·W7 통과 후에만 수행한다. Unity 소스를 웹 코드가 참조하지 않도록 W2~W4에서 확인한다.

---

## 2026-09-14 — W1 웹 프로젝트 기반 구성

**변경:** `web/`에 React 19 + TypeScript(strict) + Vite 8 프로젝트 생성. oxlint·Vitest·Testing Library·Playwright 구성, 디자인 토큰·나눔고딕 웹 폰트·접근성 기본 스타일·오류 경계·설정(글자 크기 100/125/150%)·타이틀 화면·기본 내비게이션 구현. `VITE_ANL_TEST_TOOLS` 개발/릴리스 경계와 `check:release` 번들 검사 추가.

**검증:** lint·typecheck PASS, Vitest 14, Playwright 3, production build·preview PASS. 보고: `reports/environment.md`.

---

## 2026-09-14 — W2/W3 콘텐츠·아트 이전과 코어 TypeScript 포팅

**변경:**
- 정본 콘텐츠를 `Game/Assets/_Game/Content/ep01`에서 `content/ep01/`로 이전하고 이를 새 정본 위치로 지정. `content/dev/fixtures.json` 분리.
- `tools/validate_content.py`·`check_story_routes.py`·`make_fixtures.py`의 Unity 경로 의존 제거.
- `tools/sync_web_assets.py` 추가: manifest.lock 기반 이미지 54개·콘텐츠 11개를 `web/public/`으로 동기화하고 `assetCatalog.generated.json`(ID→URL·해시·region) 생성. `--check` 드리프트 검사.
- `tools/check_font_glyphs.py` 추가: 나눔고딕이 정본 콘텐츠 전체 문자를 표시할 수 있음을 검증(누락 0). 결과 `reports/glyph_report.json`.
- 코어 로직을 TypeScript로 포팅: DTO·ContentIndex·ContentValidator·GameState·ConditionEvaluator·PuzzleEvaluator(20개 판정기)·CommandDispatcher·TestAssistPlanner·HintService·SHA-256·SaveService(localStorage)·GameSession·정적 콘텐츠 로더.
- 기존 Unity EditMode 테스트 43개의 의미를 Vitest로 이식(ST01~ST18, CT01~CT08, SV01~SV10, HI01~HI06, UI07).

**검증:** Vitest 61 PASS(이식 43 포함), Playwright 5 PASS(54개 이미지·콘텐츠 404 없음 포함), typecheck·lint·build·check:release·validate:content·assets:check·fonts:check PASS.

**문서:** `02_CODE_GENERATION_INSTRUCTIONS.md` 5.4 진행 DTO를 실제 계약과 일치하도록 수정(새 SHA `8fcd66f7…`, ASSET_READY 갱신).

---

## 2026-09-14 — W4 현재 구현 범위의 웹 화면 이전

**변경:**
- `docs/04_WEB_DESIGN_SPEC.md` 작성: 레이아웃·토큰·라우팅·입력·증거·퍼즐 위젯·힌트/저장/설정·접근성 기준.
- 콘텐츠 로더 + 세션을 React에 연결(`useGameSession`), 부팅/오류 화면, 저장·설정 연동.
- 화면 구현: 타이틀/경고, 대화(타이프라이터·선택지·지점 대사), 조사 허브, 포인트앤클릭 조사(region 오버레이), 증거책(region·요약/원문), 심문(증언·추궁·증거 1~3개), 20개 퍼즐 위젯(매핑·순서·비교·선택·인과판·반사실·보고서), 사건 기록, 설정·힌트(H1~H3·개발 모드 정답 행동 실행)·저장/불러오기(백업 복원)·개발 패널(씬 점프·씬 자동 진행·진단).
- 사용자 허브 진입을 `EnterScene` 명령으로 승격(진입 조건·자녀 검증). 기존 UI의 Dispatcher 직접 호출 제거.
- W4 화면 증빙: `reports/screenshots/web_w4_*.png` 10장(타이틀·경고·대화·조사·허브·심문·시험표·인과판·보고서·증거책).

**검증:** Vitest 61 PASS, Playwright 6 PASS(신규 전체 진행 E2E 포함: 시작→s710, 퍼즐 20/20), lint·typecheck·build·check:release PASS(릴리스 번들에 개발 도구 미포함). 화면은 실제 렌더 스크린샷으로 육안 확인.

**남은 W4 후속:** 모든 screenType의 스크린샷 확대, 모바일 폭 전 화면 검수, 키보드 전용 완주 점검은 W5~W7에서 수행.

---

## 2026-09-14 — W5~W7 진행분 (자동 검증)

**변경:** 분기 화면에 `계속` 진행 버튼 추가(비진행 분기만 있는 장면의 진행 막힘 수정). 실제 클릭 기반 1장 완주 E2E(`chapter1.spec.ts`), 새로고침 복원 E2E(`resume.spec.ts`) 추가. `docs/WEB_IMPLEMENTATION_STATUS.md`, `reports/QA_REPORT.md` 작성.

**검증:** Vitest 61, Playwright 9 PASS, lint·typecheck·build·check:release PASS. Vercel production 재배포.

**남은 항목:** 선택 장면 E2E, 오답 복귀 E2E, Safari·키보드·150%·오프라인 검수, 사람 완주 2회 → W5 최종 판정 후 W8 Unity 제거.

---

## 2026-09-14 — W8 Unity 제거

**변경:** `Game/`(310개 추적 파일)과 `Builds/`를 제거했다. 기준선 커밋 `3dd0f08`·태그 `unity-final-20260914`가 로컬·원격에 보존되어 있어 복구 가능하다. `.gitignore`·`.vercelignore`·README의 Unity 참조를 정리하고 `reports/W8_UNITY_REMOVAL.md`에 제거 목록과 검증 결과를 기록했다.

**검증:** `Game/`가 없는 작업 트리에서 콘텐츠·아트 검증 4종, lint, typecheck, Vitest 61, Playwright 11, build, release 경계 검사, preview HTTP 200 전부 PASS.
