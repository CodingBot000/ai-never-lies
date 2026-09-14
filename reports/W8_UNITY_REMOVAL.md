# W8 — Unity 제거 기록

- 기준일: 2026-09-14
- 보존: 커밋 `3dd0f08`(Unity 기준선), 태그 `unity-final-20260914` (원격 푸시 완료)
- 정본 위치: 콘텐츠 `content/ep01/`, 아트 `art_delivery/runtime/` (Unity 밖)
- 임시 백업: `$TMPDIR/anl-unity-backup/Game`, `.../Builds` (검증 완료 후 폐기 가능)

## 제거 전 검증 (Game/·Builds/ 를 작업 트리에서 제외한 상태)

| 검증 | 결과 |
| --- | --- |
| `validate_content.py` | PASS (checks 22, failed 0) |
| `check_story_routes.py` | PASS (routes 37, errors 0) |
| `sync_web_assets.py --check` | PASS (verified 65, issues 0) |
| `check_font_glyphs.py` | PASS (누락 0) |
| `npm run lint` / `typecheck` | PASS |
| `npm run test:run` | PASS 61 |
| `npm run test:e2e` | PASS 11 |
| `npm run build` / `check:release` | PASS |
| `npm run preview` | HTTP 200 |

## 제거 대상 (기록)

- `Game/` 전체(310개 추적 파일 + Unity 생성 캐시: Library·Temp·Logs·UserSettings)
  - Unity Editor/Runtime/uGUI C# 코드, `.unity` 씬, Prefab, `.meta`, ProjectSettings, Packages
  - Unity 테스트 코드(EditMode/PlayMode) — 웹 Vitest/Playwright로 대체 완료
- `Builds/` (macOS Unity 개발 빌드, 비추적)
- 코드·도구에서 `Game/` 경로 참조 없음(검색 0건). 콘텐츠 도구 3종은 이미 `content/ep01` 사용.

## 제거 후 재검증

`reports/ROUTE_VERIFICATION` 대신 아래 결과로 갈음한다. 제거 커밋 후 재실행 결과는 커밋 로그와 `reports/QA_REPORT.md`를 따른다.
