# ANL Web — 《AI는 거짓말을 하지 않는다》 Episode 01

React + TypeScript + Vite 기반의 완성형 정적 웹 게임. 최초 production 로드에서 전체 사건 자산을 선캐시하며 이후 인터넷 연결 없이 재실행·완주할 수 있다.

- 콘텐츠 정본: `<root>/content/ep01/`
- 아트 정본: `<root>/art_delivery/runtime/ep01/`, 배포 사본 `web/public/art/ep01/`
- 지시문: `docs/00_MASTER_PLAN.md`, `docs/01_IMAGE_GENERATION_INSTRUCTIONS.md`, `docs/02_CODE_GENERATION_INSTRUCTIONS.md`, `docs/03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md`

## 요구 환경

- Node.js 24 이상 (`.nvmrc` 참조), npm 11 이상
- 브라우저: Chromium 계열, Safari/WebKit
- Python 3.9+ (콘텐츠·아트 검증 도구, `.venv-art` 사용)

## 처음 실행

```bash
npm ci
npm run dev            # http://localhost:5173
```

## 검증 명령

```bash
npm run lint           # oxlint
npm run typecheck      # tsc -b (strict)
npm run test:run       # Vitest 단위·통합
npm run test:coverage  # 커버리지
npm run test:e2e       # Playwright (npx playwright install chromium 필요)
npm run test:e2e:webkit
npm run test:qa:keyboard
npm run test:qa:puzzles
npm run test:qa:responsive
npm run test:qa:overlays
npm run test:qa:offline
npm run test:qa       # 위 QA 5종 전체 실행
npm run build          # production 정적 빌드 -> dist/
npm run preview        # dist/ 미리보기 http://localhost:4173
npm run check:release  # dist/ 릴리스 경계 검사 (테스트 지원 문자열 제거 확인)
```

## 개발·릴리스 모드

- 테스트 지원(정답 행동 실행·개발 패널·픽스처 로더)은 개발 모드에서 `VITE_ANL_TEST_TOOLS=1`일 때만 켜진다.

```bash
VITE_ANL_TEST_TOOLS=1 npm run dev
```

- 릴리스 빌드는 플래그 없이 수행하며 `npm run check:release`로 번들 경계를 검사한다.

## 저장

- `localStorage` 키 접두사 `anl.ep01.*` (자동 저장 1, 수동 슬롯 3, 백업, 설정)
- 저장 스키마·체크섬·백업·복구 규칙은 `docs/02_CODE_GENERATION_INSTRUCTIONS.md` 10장이 정본

## 폰트

- 나눔고딕(NanumGothic) Regular/Bold, SIL Open Font License 1.1 — `public/fonts/`, 라이선스 원문 포함

## production 오프라인

- `npm run build`가 `dist/sw.js`를 생성하고 73개 정적 파일을 버전별 Cache Storage에 선캐시한다.
- `npm run test:qa:offline`은 production 서버를 연 뒤 네트워크를 차단하고 앱·54개 아트·11개 콘텐츠·폰트를 재요청한다.
- 서비스워커는 production에서만 등록되며 개발 서버에는 영향을 주지 않는다.

## 현재 상태와 남은 확인

- Episode 01의 43개 장면, 36개 증거, 20개 퍼즐, 선택 분기와 엔딩이 구현됐다.
- Chromium·WebKit 자동 E2E, 키보드 완주, 150%·모바일 전 화면, 오프라인 재로드가 통과했다.
- 사람에게 남은 항목은 실제 Safari 육안 QA, 사람 직접 완주 2회, 최종 사용자 승인이다.
- Unity 소스는 W8에서 제거됐다. 복구 기준선은 태그 `unity-final-20260914`다.
