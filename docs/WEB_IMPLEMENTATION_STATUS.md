# 웹 구현 상태 (W5/W6)

- 기준일: 2026-09-14
- 근거: 커밋 `4b2be83`(W4) 이후 작업, `reports/QA_REPORT.md`, `reports/screenshots/web_w4_*`
- 상태 표기: DONE(구현·검증), PARTIAL(일부), MISSING(없음), NOT_RUN(미실행)

## 1. 범위 요약

| 영역 | 상태 | 근거 |
| --- | --- | --- |
| 43개 서사 씬 진행 | DONE | `walkthrough.spec.ts` 시작→s710 자동 진행, `contentContract` CT01/CT03/CT07 |
| 36개 증거(원문·요약·출처·한계) | DONE | `EvidencePanel`, ev34/35 선택 획득 단위 테스트(ST14) |
| 20개 퍼즐(정답·부분·오답·복귀) | DONE | Vitest ST/CT/HI 43개 + `PuzzleHost` 위젯 15종, 전체 진행 E2E 20/20 |
| 필수·선택 분기 | DONE | 필수 경로 E2E 완주, 선택 장면 s520·s530 수용/거절 E2E(`optional.spec.ts`) |
| H1~H3 힌트 | DONE | `HintPanel`, HI01~HI03 |
| 세이브·로드·자동저장·손상 복구 | DONE | SV01~SV10, `resume.spec.ts`(새로고침 복원) |
| 설정(글자 100/125/150·출력 속도·자동 진행·동작 줄이기) | DONE | `SettingsPanel`, 설정 단위 테스트 |
| 개발 진단(씬 점프·자동 진행·정답 행동) | DONE | `DevPanel`, `check:release`로 릴리스 제외 확인 |
| 사건 기록·엔딩 | DONE | `ArchiveScreen`, ST15 두 엔딩 동일 사실 |
| 54 이미지·region 좌표 연결 | DONE | asset 카탈로그 통합 테스트, UI07, `assets.spec.ts` 404 없음 |
| 실제 입력 완주 2회 | PARTIAL | 자동 브라우저 입력 2종(전체 자동 진행, 1장 클릭 완주). 사람 육안 완주 NOT_RUN |
| Safari/WebKit | NOT_RUN | Chromium만 자동화. Safari 수동 QA 미실행 |
| 오프라인 재실행 | MISSING | 실행 중 외부 요청 0은 E2E로 확인. 서비스워커 없음 → 완전 오프라인 재로드 불가 |
| 키보드 전용 완주·150%·모바일 전 화면 | PARTIAL | Space/Esc·글자 배율·모바일 폭 구현, 전 화면 자동 검수는 미실행 |

## 2. 테스트 증빙

| 구분 | 결과 |
| --- | --- |
| `npm run lint` | PASS (0 warnings) |
| `npm run typecheck` | PASS |
| `npm run test:run` | PASS 61 (EditMode 43 의미 이전 포함) |
| `npm run test:e2e` | PASS 11 (chromium) |
| `npm run build` | PASS (JS 353KB/gzip 106KB, CSS 29KB) |
| `npm run check:release` | PASS (개발 도구 문자열 없음) |
| `validate:content` / `assets:check` / `fonts:check` | PASS |

## 3. 미완료·후속 (다음 작업)

1. 완료: 선택 장면 s520/s530 실제 UI 완주 E2E(`optional.spec.ts`).
2. 완료: 오답→피드백→수정→성공 경로 E2E(`chapter1.spec.ts` p02).
3. 키보드 전용 완주, 150% 글자 전 화면, 모바일 전 화면 스크린샷 검수.
4. Safari 데스크톱 수동 QA.
5. 오프라인 재실행이 필요하면 서비스워커 추가(계약 변경: 오프라인 완주 요구 충족).
6. 사람이 직접 2회 완주(녹화·스크린샷) 후 W5 최종 판정 → W8 Unity 제거.
