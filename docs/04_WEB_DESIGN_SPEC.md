# 웹 UI 디자인 명세 (W4 구현 기준)

- 문서 ID: `ANL-WEB-DESIGN`; 버전: `1.0.0`; 기준일: `2026-09-14`.
- 근거: `00_MASTER_PLAN.md` 4장·12장, 기존 Unity 화면 증빙(`reports/screenshots/`), 아트 54개, `content/ep01/ui.ko.json`.
- 구현 기준 승인: 사용자가 웹 이전 작업을 전면 승인한 상태로, 본 명세를 구현 기준으로 고정한다. 변경 시 `handoff/CHANGELOG.md`에 기록한다.

## 1. 레이아웃

```text
┌──────────────────────────────────────────────┐
│ 배경층 (scene.backgroundAssetId, cover)        │
│  ┌ 인물층 (portraitAssetId, 하단 중심 anchor) ┐│
│  ├ 화면 콘텐츠층 (조사·심문·퍼즐·보고·기록)   ┤│
│  ├ 대화층 (하단 250~310px, 실제 HTML 텍스트)  ┤│
│  └ HUD 상단 우측: 현재 목표 / 증거 / 수첩 / 팁 / 메뉴
│ 모달층 · 개발 오버레이층                         │
└──────────────────────────────────────────────┘
```

- 기준 비율 16:9(1920×1080). 배경은 cover, 증거·인물은 contain. 표시 영역은 CSS 픽셀로 계산한다.
- 1280×720, 1920×1080, 2560×1440, 16:10, 모바일 폭(≥360px)에서 본문·버튼이 잘리지 않는다.
- 모바일 폭에서는 HUD를 축소하고 대화창 높이를 유동으로, 퍼즐 패널은 세로 스크롤로 전환한다.

## 2. 색·타이포

- `web/src/shared/styles/tokens.css`의 토큰을 사용한다. 청록=선택/정보, 주황=주의, 적색=모순/위험. 색만으로 상태를 구분하지 않고 아이콘·라벨을 병기한다.
- 본문 30~34px 상당(rem 스케일), 보조 라벨 최소 22px 상당. 글자 크기 100/125/150%를 지원한다.
- 대사·증거 본문은 실제 HTML 텍스트. 이미지에 텍스트를 굽지 않고 `dangerouslySetInnerHTML`을 쓰지 않는다.

## 3. 화면 라우팅 (screenType)

| 조건 | 화면 |
| --- | --- |
| phase = pre_dialogue/post_dialogue | 대화 레이어 (화자·본문·다음, 클릭/Space) |
| phase = branch_choice | 대화 레이어 + 선택지 목록 |
| phase = interaction | 퍼즐 호스트(활성 퍼즐 type별 위젯) |
| phase = read_check / ready_to_exit / completed | 진행 패널(계속·조사 허브로) |
| screenType = title | 타이틀 메뉴(새 사건·이어하기·사건 기록·설정·정보) |
| screenType = hub | 조사 허브(필수/선택 자식 카드) |
| screenType = investigation | 포인트앤클릭 조사 + region 오버레이 |
| screenType = case_archive | 사건 기록 |
| 그 외 | 배경 + 조사 허브 복귀/계속 버튼 |

## 4. 입력

- 마우스: 클릭으로 모든 행동 가능. 드래그는 선택 기능이며 클릭→슬롯 클릭으로 동일하게 해결 가능해야 한다.
- 키보드: Tab 이동, Enter 확인, Space 대사 다음, Esc 최상단 모달 닫기. 텍스트 입력 중 단축키 비활성.
- 터치: 동일 게임 명령으로 연결. 최소 조작 영역 44px.
- 더블클릭·연타로 증거 중복 지급·씬 이중 진입이 발생하지 않는다(commandId 멱등성).
- 개발 모드 한정: `VITE_ANL_TEST_TOOLS=1`이면 정답 행동 실행·개발 패널·픽스처 로더 노출. 릴리스에는 포함되지 않는다.

## 5. 증거 표시

- 목록은 보유 자료만. 미획득 증거의 제목·썸네일·힌트를 노출하지 않는다.
- 상세: 요약 기본, 원문 탭, 출처, 확인할 수 있는 것(proves), 아직 알 수 없는 것(limits), 열람 표시.
- 이미지가 있는 증거는 region 오버레이를 코드로 그린다. 좌표는 `regionMath.ts`로 manifest 정규화 값을 표시 사각형에 변환한다. 경고문·인식 박스·수치는 코드 텍스트다.
- 종류 필터: 전체/문서/영상 기록/로그/개인 자료.

## 6. 퍼즐 위젯

| type | 위젯 |
| --- | --- |
| hotspot_set | 조사 화면 region 클릭 |
| statement_refutation | 증언 카드 목록 + 추궁 + 증거 1~3개 선택 + 제출 |
| source_mapping / trace_mapping | 슬롯-카드 매핑(클릭 배치) |
| timeline_order / chronology / deployment_chain | 순서 카드 배치 + 해석/결론 선택 |
| cross_camera | 비교 구간 선택 + 자료 연결 |
| argument_choice / diff_select / matrix_gap | 선택지 버튼(복수/단수) |
| assumption_link | 전제 선택 + 근거 자료 연결 |
| counterfactual | 사례 실행 카드 3개 + 결과표 |
| causal_graph | 카드 배치·연결선·간선별 근거 |
| responsibility_matrix | 다섯 구역 배치 + 항목당 근거 1~3개 + 채택 |

- 부분 정답·오답 모두 피드백 문구를 표시하고 수정 후 재제출할 수 있다.
- 정답 판정은 `core/puzzles`만 수행하며 위젯에 정답을 하드코딩하지 않는다.
- 모든 위젯은 같은 `PuzzleDraftDto`를 명령으로만 수정한다.

## 7. 힌트·저장·설정

- 팁: 장면 팁 + 퍼즐 H1/H2/H3 + 개발 모드 정답 행동 실행. H3 열람만으로 solved가 되지 않는다.
- 저장: 수동 슬롯 3 + 자동 1. 시각·씬 표시, 덮어쓰기 확인, 손상 시 백업 복원 안내.
- 설정: 글자 크기(100/125/150), 출력 속도(느림/보통/빠름/즉시), 자동 진행, 동작 줄이기. 설정은 즉시 적용되고 저장된다.

## 8. 접근성

- 오류 경계로 렌더 오류 시 복구 안내. 모달 포커스 트랩·Esc 닫기·복귀 포커스.
- `prefers-reduced-motion`과 설정의 동작 줄이기에서 페이드·흔들림을 제거해도 정보가 유지된다.
- 상태 변화는 `aria-live` 영역으로 안내한다(저장 완료·오답 피드백).
