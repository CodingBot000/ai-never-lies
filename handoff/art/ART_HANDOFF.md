# Episode 01 Art Handoff

상태: `READY`

`docs/00_MASTER_PLAN.md`의 Episode 01 아트 목록과 `docs/01_IMAGE_GENERATION_INSTRUCTIONS.md`의 asset별 프롬프트를 기준으로 54개 런타임 PNG를 생성·정규화·검수했다.

## 산출물

- `art_delivery/runtime/ep01/backgrounds/` — 10개, 1920×1080 RGB
- `art_delivery/runtime/ep01/portraits/` — 29개, 1024×1536 RGBA
- `art_delivery/runtime/ep01/evidence/` — 10개, 지정 크기 RGB
- `art_delivery/runtime/ep01/cg/` — 5개, 1920×1080 RGB
- `art_source/originals/` — 생성 원본 보관본 54개
- `handoff/art/manifest.plan.json` — 계획 목록
- `handoff/art/manifest.lock.json` — 실측·해시·승인 잠금 목록
- `handoff/art/ASSET_READY.json` — 54/54 승인 완료 표식
- `handoff/art/ART_REVIEW.md` — 기계·시각 검수 요약
- `handoff/art/generation_log.json` — 생성 도구와 프롬프트 출처 기록
- `handoff/art/previews/portraits_contact_sheet.png` — 포트레이트 시각 검수용 미리보기

## 검수 결과

- 런타임 PNG: 54개
- 계획/잠금 manifest: 각각 54개
- 승인: 54/54
- 누락·중복 SHA-256·계획 외 PNG·크기/모드/region 오류: 0건
- `ASSET_READY.ready`: `true`

생성 도구는 OpenAI built-in `image_gen`이며, seed는 도구에서 노출되지 않아 기록하지 않았다. 실제 런타임 UI 텍스트·라벨·타임라인은 이미지에 굽지 않고 코드 오버레이가 담당하도록 모든 프롬프트에 무텍스트·무UI 제약을 적용했다.
