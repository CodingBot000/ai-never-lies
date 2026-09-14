# ANL Web — 《AI는 거짓말을 하지 않는다》 Episode 01

React + TypeScript + Vite 기반의 정적 웹 게임. 서버·로그인·인터넷 연결 없이 완주 가능한 빌드를 목표로 한다.

- 콘텐츠 정본: `<root>/content/ep01/` (W2에서 이전)
- 아트 정본: `<root>/art_delivery/runtime/ep01/`, 배포 사본 `web/public/art/ep01/`
- 지시문: `docs/00_MASTER_PLAN.md`, `docs/01_IMAGE_GENERATION_INSTRUCTIONS.md`, `docs/02_CODE_GENERATION_INSTRUCTIONS.md`, `docs/03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md`

## 요구 환경

- Node.js 24 이상 (`.nvmrc` 참조), npm 11 이상
- 브라우저: Chromium 계열(개발·E2E), Safari(수동 QA)
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

## 알려진 제한 (W1 시점)

- 타이틀·기본 내비게이션까지만 구현되어 있다. 새 게임/이어하기는 개발 상태 화면을 표시한다.
- 콘텐츠·아트·코어 로직·게임 화면은 W2~W4에서 구현한다.
- Unity 소스는 W8에서 제거됐다(기준선 태그 `unity-final-20260914`).
