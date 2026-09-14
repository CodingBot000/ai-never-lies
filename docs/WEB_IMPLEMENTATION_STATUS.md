# 웹 구현 상태 (W5~W8)

- 기준일: 2026-09-15
- 근거: `reports/QA_REPORT.md`, Chromium·WebKit·오프라인·키보드·반응형 QA
- 상태 표기: DONE(구현·자동 검증), NOT_RUN(사람 확인 필요)

## 1. 범위 요약

| 영역 | 상태 | 근거 |
| --- | --- | --- |
| 43개 서사 씬 진행 | DONE | `walkthrough.spec.ts`, `keyboard.qa.ts` 시작→s710 |
| 36개 증거와 선택 획득 | DONE | `EvidencePanel`, ST14, `optional.spec.ts` |
| 20개 퍼즐 정답·오답·부분답·복귀 | DONE | Vitest 62개, `puzzle-feedback.qa.ts`, 키보드 완주 |
| 필수·선택 분기 | DONE | 필수 경로, s520·s530 수용/거절 E2E |
| H1~H3 힌트 | DONE | `HintPanel`, HI01~HI06 |
| 저장·복원·손상 복구 | DONE | SV01~SV10, `resume.spec.ts` |
| 설정·동작 줄이기 | DONE | 설정 단위 테스트와 반응형 QA |
| 사건 기록·엔딩 | DONE | `ArchiveScreen`, ST15, 키보드 완주 |
| 54개 이미지·region·코드 오버레이 | DONE | 카탈로그 계약, 10개 context 데스크톱·모바일 렌더 QA |
| 오프라인 재실행 | DONE | production 서비스워커, 73개 파일 선캐시, 네트워크 차단 재로드·65개 사건 자산 fetch PASS |
| 키보드 전용 자동 완주 | DONE | 포인터 이벤트 0회로 새 게임→s710 PASS |
| 150%·모바일 전 화면 | DONE | 43씬+20퍼즐×2 뷰포트=126개 화면, 잘림·가로 넘침 0 |
| Chromium E2E | DONE | 11/11 PASS |
| WebKit E2E | DONE | 11/11 PASS (Playwright WebKit 26.6) |
| 실제 Safari 자동 스모크 | DONE | Safari 26.5 production 타이틀·설정 모달 렌더/입력 확인 |
| 사람 직접 완주 2회 | NOT_RUN | 실제 사람이 수행하고 녹화·스크린샷을 남겨야 함 |
| 실제 Safari 사람 수동 QA | NOT_RUN | Safari 자동 스모크와 별도로 사람 육안 전체 확인 필요 |
| 최종 사용자 승인 | NOT_RUN | 위 두 사람 작업 뒤 최종 승인 |

## 2. 자동 검증 증빙

| 구분 | 결과 |
| --- | --- |
| `npm run lint` | PASS, 경고 0 |
| `npm run typecheck` | PASS |
| `npm run test:run` | PASS 62 |
| `npm run test:e2e` | PASS 11 Chromium |
| `npm run test:e2e:webkit` | PASS 11 WebKit |
| `npm run test:qa:keyboard` | PASS, 포인터 입력 0회 전체 완주 |
| `npm run test:qa:puzzles` | PASS, p01~p20 오답 + 정의된 부분답 6종 |
| `npm run test:qa:responsive` | PASS, 126개 화면 |
| `npm run test:qa:overlays` | PASS, 10개 오버레이 context × 2 뷰포트 |
| `npm run test:qa:offline` | PASS, 완전 오프라인 재로드 |
| `npm run build` / `check:release` | PASS, 서비스워커 포함 production build |
| 콘텐츠·라우트·아트·폰트 검사 | PASS |

## 3. 이번 보완에서 발견·수정한 실제 결함

1. p20 보고서 UI 구역 ID가 정본의 `contributions`·`postIncident`와 달라 일반 사용자 입력으로 해결할 수 없던 문제를 수정했다.
2. p07·p15 부분답이 콘텐츠의 `partialText` 대신 판정기 고정 문구를 반환하던 문제를 수정했다.
3. 텍스트 입력 중 Space/Esc 전역 단축키가 실행될 수 있던 문제를 막았다.
4. 증거 이미지의 경고문·인식 박스·수치·식별값·메시지 오버레이를 구현했다.
5. 서비스워커 폰트 캐시가 `Vary: Origin` 때문에 오프라인 fetch에서 누락되던 문제를 수정했다.
6. 새 서비스워커가 열린 탭의 이전 해시 캐시를 즉시 삭제해 업데이트 중 빈 화면이 될 수 있던 문제를 표준 waiting 활성화로 수정했다.

## 4. 사람에게 남은 작업

자동화·구현 잔여는 0개다. 아래는 AI가 사람 수행으로 표시할 수 없는 항목이다.

1. Mac Safari에서 사람이 직접 전체 화면과 입력을 육안 확인한다.
2. 사람이 마우스 경로 1회, 키보드 경로 1회 등 총 2회 처음부터 엔딩까지 완주하고 녹화·스크린샷을 남긴다.
3. 가독성·조작감·분위기·내용 표현을 최종 승인한다.

W8 Unity 제거는 위 사람 검수보다 먼저 수행되어 원래 단계 순서와 달랐지만, 보존 태그 `unity-final-20260914`가 있고 Unity 없는 상태에서 모든 자동 검증이 다시 통과했다. Unity 복구는 필요하지 않다.
