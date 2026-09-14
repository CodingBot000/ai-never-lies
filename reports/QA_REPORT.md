# QA 리포트 (웹, 자동 검증 완료)

- 기준일: 2026-09-15
- 대상: `web/` production build와 개발 테스트 빌드
- 브라우저: Chromium 143, Playwright WebKit 26.6
- 뷰포트: 1920×1080, 390×844, 텍스트 150%

## 결과

| 검증 | 명령 | 결과 |
| --- | --- | --- |
| 린트 | `npm run lint` | PASS, 경고 0 |
| 타입 | `npm run typecheck` | PASS |
| 단위·통합 | `npm run test:run` | PASS 62 |
| Chromium E2E | `npm run test:e2e` | PASS 11 |
| WebKit E2E | `npm run test:e2e:webkit` | PASS 11 |
| 실제 Safari 스모크 | Safari 26.5 / production preview | PASS, 타이틀·설정 모달 렌더/입력 |
| 키보드 전체 완주 | `npm run test:qa:keyboard` | PASS, 포인터 0회, s000→s710 |
| 퍼즐 피드백 | `npm run test:qa:puzzles` | PASS, p01~p20 오답·수정 유지, 부분답 6종 |
| 150%·반응형 | `npm run test:qa:responsive` | PASS, 126개 화면 |
| 증거 오버레이 | `npm run test:qa:overlays` | PASS, 10 context × 2 뷰포트 |
| 완전 오프라인 | `npm run test:qa:offline` | PASS, 네트워크 차단 재로드·전체 자산 접근 |
| 정적 검증 | `validate:content`·`assets:check`·`fonts:check`·`check_story_routes.py` | PASS |
| 빌드·경계 | `npm run build` + `npm run check:release` | PASS |

## 기능·회귀 범위

- `title.spec.ts`: 타이틀, 콘솔 오류, 모바일 오버플로, 새 게임 경고.
- `assets.spec.ts`: 54개 이미지·11개 콘텐츠 JSON 404 없음.
- `chapter1.spec.ts`: 실제 클릭으로 1장 진행, p02 오답→수정→성공.
- `optional.spec.ts`: s520, s530 수용/거절과 ev34/ev35 지급 조건.
- `resume.spec.ts`: 새로고침 뒤 자동 저장 복원.
- `walkthrough.spec.ts`: s000→s710, 퍼즐 20/20.
- `keyboard.qa.ts`: 포인터 이벤트 없이 정상 UI 명령으로 전 구간 완주.
- `puzzle-feedback.qa.ts`: 20개 퍼즐별 브라우저 오답과 콘텐츠에 정의된 부분답.
- `responsive.qa.ts`: 43개 장면과 20개 퍼즐을 데스크톱 150%·모바일 150%로 전수 렌더링.
- `overlays.qa.ts`: 차량·보행자·차내 경고·CCTV·레이더·정비·메시지 오버레이.
- `offline.qa.ts`: 서비스워커 제어 후 네트워크를 끊고 앱 재로드, 54개 아트·11개 콘텐츠·폰트 fetch.
- 실제 Safari 26.5: production 첫 화면과 설정 모달을 열어 접근성 트리·화면 렌더를 확인.

## 수정 과정에서 잡힌 결함

- p20 보고서 구역 ID 불일치로 수동 해결이 불가능했던 문제.
- p07·p15 부분답 문구가 정본 콘텐츠와 달랐던 문제.
- 입력 필드 포커스 중 전역 단축키가 작동할 수 있던 문제.
- 서비스워커 폰트 캐시의 `Vary: Origin` 불일치.
- 서비스워커 즉시 활성화가 열린 탭의 이전 해시 캐시를 삭제하던 업데이트 경쟁 상태.

## 화면 증빙

- `reports/screenshots/qa-responsive/`: 126개 장면·퍼즐 반응형 PNG.
- `reports/screenshots/qa-overlays/`: 20개 증거 오버레이 PNG.
- PNG는 로컬 QA 산출물이며 Git 커밋에서는 제외한다.

## 사람 확인 필요

- 실제 Safari에서 사람 육안 QA.
- 사람이 직접 수행한 전체 완주 2회와 녹화·스크린샷.
- 조작감·가독성·분위기·내용 표현에 대한 최종 사용자 승인.

위 세 항목 외 자동화·구현 잔여는 없다.
