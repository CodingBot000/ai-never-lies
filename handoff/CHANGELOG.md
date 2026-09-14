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
