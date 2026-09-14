# 《AI는 거짓말을 하지 않는다》 코드 생성·개발 지시문
## 웹 이전 완료 후 연속 개발 · React/TypeScript/Vite 기준 Episode 01 전체 구현

문서 ID: `ANL-CODE-WEB`; 버전: `2.0.0`; 작성 기준일: `2026-09-14`.
콘텐츠: `ep01.v1.0.0`; 아트 계약: `art.v1`.
필수 동반 문서: `00_MASTER_PLAN.md`, `01_IMAGE_GENERATION_INSTRUCTIONS.md`, `03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md`.
기술 기준 충돌 시 `03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md`가 우선한다.

> Unity 기반 개발은 종료됐다. 이 문서는 웹(React + TypeScript + Vite) 기준의 코드 개발 지시문이다. 기존 Unity 소스와 테스트는 참고 자료일 뿐 실행·수정·확장하지 않는다.
> 최종 결과는 시작 화면부터 사건 기록까지 실제 브라우저에서 플레이 가능한 정적 웹 게임, 로컬 저장, 모든 팁·테스트 지원, 검증 리포트, production `dist/` 빌드다. 새 계획만 반복 작성하고 구현을 끝내지 말 것.

## 0. 코드 세션에 그대로 전달할 실행 지시

```text
세 문서와 03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md를 읽고 버전·해시·이미지 인계를 검증하라.
ASSET_READY가 유효하고 모든 필수 이미지가 실제로 존재할 때만 구현을 이어가라.
최종 실행 환경은 브라우저다. Unity Editor·런타임·빌드를 실행하거나 사용하지 마라.
React + TypeScript(strict) + Vite를 기준으로 하고 정확한 패키지 버전을 lockfile로 잠가라.
마스터의 43개 서사 씬, 36개 증거, 20개 퍼즐, 분기·대사·정답을 누락 없이 구현하라.
개발 도구가 Codex일 필요는 없다. 특정 코딩 에이전트의 독점 기능에 의존하지 마라.
게임 실행 중 LLM, 생성형 이미지, 외부 AI API, 인터넷 조회를 호출하지 마라.
게임 완주에 로그인·서버·DB·인터넷 연결을 요구하지 마라.
이미지 생성/재디자인/파일명 변경은 이 세션의 업무가 아니다.
게임 전체 화면을 생성 이미지 한 장으로 붙여서 버튼만 올리는 데모를 만들지 마라.
텍스트를 이미지에 굽지 말고 실제 HTML로 렌더링하라.
과거 대화가 없어도 세 문서와 인계 파일만으로 작업을 완료하라.
정답 힌트와 테스트 자동해결은 정상 명령 처리 경로를 사용하라.
테스트 미실행을 PASS로, 소스 작성만을 빌드 성공으로 보고하지 마라.
작업 도중 기능을 줄여야 할 것 같으면 먼저 장애를 해소하라. 첫 사건 전체 범위를 임의로 잘라내지 마라.
```

## 1. 절대 계약과 기술 선택

### 1.1 기능 범위
정확한 사건·대사·분기·정답은 마스터에 있다. 이 문서는 구현을 위한 것이다. 사용자 요청의 “프로덕션급 디테일”을 복잡한 인프라나 많은 의존성으로 대체하지 않는다. 중요한 것은 재현 가능한 진행, 올바른 증거 연결, 막힘 없는 UI, 저장 복원, 이미지 매핑, 실제 검증이다.

정확한 패키지 버전은 작업 시작 시 실제 환경에서 확인하고 `package-lock.json`으로 고정한다. “최신”이라는 부동 조건으로 작업 중 자동 업그레이드하지 않는다.

| 구역 | 선택 | 제외 |
| --- | --- | --- |
| 애플리케이션 | React + TypeScript(strict) + Vite | Unity, 별도 게임 엔진, 데스크톱 런타임 |
| 렌더링 | CSS 기반 2D 정적 일러스트·패널 전환, 실제 HTML 텍스트 | 3D 모델, 물리 차량, WebGL 전용 렌더러, 텍스트 이미지 굽기 |
| 콘텐츠 | 로컬 JSON → 스키마·참조 검증 → 읽기 전용 런타임 데이터 | 원격 DB, 즉석 LLM 대사 |
| 상태 | 순수 TypeScript 명령·결정적 Reducer·판정기 | UI 컴포넌트마다 독립 진행 플래그 |
| 저장 | 버전 있는 JSON 스냅샷, `localStorage`, 백업 | 전체 상태를 쿠키·URL·서버에 저장 |
| 아트 | 정적 PNG + asset ID→URL 카탈로그 | 파일명 추측, 런타임 외부 다운로드 |
| 스타일 | CSS 토큰·반응형 레이아웃 | 캔버스에 UI 전체를 그리는 방식 |
| 테스트 | Vitest + Testing Library + Playwright | 정답 점프만으로 QA 대체 |
| 빌드 | 정적 `dist/` (서버 로직 없음) | 서버 사이드 렌더링 필수화, 모바일·콘솔 동시 보장 |

### 1.2 지켜야 할 비기능 원칙
완주에 네트워크가 필요 없어야 한다. 시작 직후 서버 로그인·모델 키·광고 SDK 설정을 요구하지 않는다. 실제 개인정보를 로그에 수집하지 않는다. 상태 변경마다 전체 앱과 전체 JSON을 다시 만들지 않는다. 에러는 사용자가 다음 행동을 알 수 있는 메시지와 개발자용 원인 기록으로 나눈다. 빌드 성공·브라우저 실행 성공·전체 완주 성공을 서로 다른 항목으로 보고한다.

## 2. G1 이미지 인계 검증 — 구현 착수 전 기준

웹 구현은 이미 시작됐지만, 콘텐츠·아트 이전과 화면 구현 전에 다음을 다시 확인한다. 이 과정은 읽기/검사 도구 작업이다.

1. 세 MD와 `handoff/art/ASSET_READY.json`의 존재·버전을 확인한다.
2. READY의 세 문서 SHA와 실제 MD 바이트 해시를 비교한다. 개행을 임의로 바꾼 뒤 검증하지 않는다.
3. `ready=true`, `contractVersion=art.v1`, `episodeId=ep01`, required/approved count=54인지 확인한다.
4. `manifest.lock.json` 실제 해시와 READY의 manifestSha256을 비교한다.
5. plan·lock·마스터의 ID 집합과 경로가 같은지 확인한다. 개수만 맞으면 통과시키지 않는다.
6. 각 실제 PNG의 해시·파일 바이트·해상도·알파·필수 regionId·시각 검수 기록을 검사한다.
7. 동일 해시로 서로 다른 표정을 채운 파일, 깨진 PNG, 대소문자만 다른 파일, 외부 symlink·상위 디렉터리 경로를 거부한다.
8. 검사 결과를 `reports/preflight_art.json`과 `.md`에 기록한다.

누락된 자산은 붉은 물음표 이미지로 대체하지 않는다. 구현 전 `BLOCKED_ART_CONTRACT`와 ID·기대 경로·실측 상태를 보고한다. 이미 구현된 코드 단계에서 결함이 발견되면 원본 PNG를 임의 수정하지 말고 아트 재인계를 요청한다. 기술 검사와 외형 검수는 구분한다. 전 파일 존재만으로 “시각 검수 완료”라고 쓰지 않는다.

### 2.1 코드가 참고할 실제 파일 위치
```text
SOURCE(정본): <project-root>/art_delivery/runtime/<manifest.path>
WEB(배포):   <project-root>/web/public/art/<manifest.path>
CATALOG:     <project-root>/web/src/shared/assets/assetCatalog.ts
```

인계 manifest의 path는 `ep01/backgrounds/...png`처럼 상대경로다. 코드는 파일 경로를 문자열 조합하거나 파일명을 추측하지 않고, 동기화 스크립트가 manifest.lock.json에서 생성한 **asset ID→URL 카탈로그**로만 로딩한다. 원본 PNG 바이트를 바꾸지 않고 복사하며, 배포 경로의 파일은 `tools/` 동기화 스크립트로만 갱신한다.

## 3. 개발환경 설정 (웹)

### 3.1 환경 조사와 보고
먼저 `reports/environment.md`를 갱신한다. 실제 OS, CPU 아키텍처, Node·npm 버전, 브라우저 버전(Chromium 계열·Safari), Git, Python(검증 도구용), 선택한 편집기를 기록한다. 확인하지 못한 항목은 unknown/NOT_RUN이다. 사용자 계정·비밀번호·토큰을 보고서에 넣지 않는다. Unity 관련 항목은 “사용하지 않음”으로 명시한다.

```bash
# 프로젝트 루트에서
node --version && npm --version
cd web && npm ci          # lockfile 기준 설치. lockfile이 없으면 npm install 후 커밋
npm run dev               # 개발 서버
```

설치되어 있지 않은 Node 버전을 가정하지 않는다. `.nvmrc` 또는 `package.json`의 `engines`로 기준 버전을 기록한다.

### 3.2 패키지와 프로젝트 설정
`web/package.json`에 실제 사용하는 의존성만 둔다. Vite, React, TypeScript, Vitest, Testing Library, Playwright, ESLint(또는 동등 린터) 정도로 시작하고 상태 관리·스타일·테스트 유틸 라이브러리는 필요할 때만 추가한다. beta·preview를 기능 구현 편의로 추가하지 않는다. 설치 버전은 `package-lock.json`으로 잠그고 environment 보고서에도 기록한다.

`tsconfig.json`은 `strict: true`, `noUncheckedIndexedAccess`, `noImplicitOverride`를 포함한 엄격 설정을 사용한다. `any`는 경계(외부 JSON 파싱 직후)에만 허용하고 즉시 검증된 타입으로 좁힌다. 린트 규칙에서 `any` 암묵 사용, floating promise, 사용하지 않는 변수를 오류로 처리한다.

`vite.config.ts`에서 개발·릴리스 모드를 분리한다. 테스트 전용 기능은 `import.meta.env.DEV`와 `VITE_ANL_TEST_TOOLS` 플래그를 함께 확인한다. 릴리스 빌드(`npm run build`)는 `VITE_ANL_TEST_TOOLS` 없이 수행하며, 테스트 도구 문자열이 번들에 없음을 검사한다.

저장 키 접두사와 프로젝트 식별자는 초기부터 고정한다: `anl.ep01.*`. 중간에 바꾸지 않는다.

### 3.3 Git과 파일 관리
```text
커밋: web/ 소스·설정·lockfile, content/ep01/ 정본 JSON,
       세 MD, 잠긴 이미지 인계 메타데이터, tools/ 검증 스크립트, 테스트
미커밋: web/node_modules/, web/dist/, web/coverage/,
       web/playwright-report/, web/test-results/, 로컬 환경 변수, 계정·비밀키
```

`web/public/art/`의 배포 이미지는 정본 `art_delivery/runtime/`에서 동기화한 복사본이다. 이미지 원본·후보를 저장소 구조와 무관한 임의 위치에 중복 생성하지 않는다. 사용자 요청 없이 원격 저장소 생성·공개 업로드·push를 하지 않는다.

### 3.4 폰트
권한이 확인된 한국어 글꼴(NanumGothic, SIL OFL 1.1)을 `web/public/fonts/`에 복사하고 라이선스 원문을 함께 둔다. 시스템 폰트의 배포 권한을 추정하지 않는다. `@font-face`로 등록하고 `font-display: swap`을 사용한다.

정본 대사·증거·UI 문자열에서 실제 문자 집합을 추출해 글리프 누락을 검사한다. 한글 완성형, 숫자, 문장부호, ℃·²·±·→ 등 사용 기호를 확인한다. 검사 스크립트(예: `tools/check_font_glyphs.py`)는 폰트 cmap 기준으로 누락 문자 목록을 `reports/glyph_report.json`에 기록한다. 누락 글리프가 있으면 폴백 폰트를 지정하거나 폰트를 교체하기 전 완료 처리하지 않는다. 로드 실패 시에도 읽을 수 있는 폴백 스택을 둔다.

## 4. 프로젝트 구조와 책임

```text
<project-root>/
  content/
    ep01/                          # 엔진 독립 정본 콘텐츠 JSON
      episode.json
      scenes.json
      dialogues.ko.json
      evidence.json
      testimonies.json
      puzzles.json
      hints.ko.json
      branches.json
      graph.json
      matrix.json
      ui.ko.json
  art_delivery/runtime/ep01/       # 아트 정본
  web/
    public/
      art/ep01/                    # 동기화된 정적 배포 이미지
      content/ep01/                # 동기화된 정적 콘텐츠 JSON (빌드 입력)
      fonts/
    src/
      app/                         # 앱 부트스트랩과 최상위 조립
        App.tsx
        routes.ts                  # 화면 라우팅/스크린 매핑
        ErrorBoundary.tsx
      core/                        # React·DOM 비의존 순수 로직
        commands/                  # 명령 타입·검증·결과
        conditions/                # 진행 조건 판정
        content/                   # DTO·타입·인덱스·스키마 검증
        puzzles/                   # 20개 퍼즐 판정
        state/                     # GameState와 저장 스냅샷·직렬화
        hints/                     # 힌트 서비스
      features/
        title/
        dialogue/
        hub/
        investigation/
        evidence/
        puzzles/
        report/
        archive/
        settings/
      shared/
        assets/                    # asset ID→URL 카탈로그·프리로더
        components/                # 공통 UI(버튼·모달·툴팁 등)
        styles/                    # 토큰·전역 스타일·폰트
        save/                      # localStorage 어댑터(백업·버전)
      content/                     # 정본 JSON 로더·검증 진입점
    tests/
      unit/
      integration/
      e2e/
    package.json
    tsconfig.json
    vite.config.ts
    playwright.config.ts
  tools/
    validate_art.py
    validate_content.py
    check_story_routes.py
    sync_web_assets.py
    check_font_glyphs.py
  reports/
  handoff/
  docs/
```

파일 이름과 모듈 분리는 구현 편의로 정교화할 수 있지만 `core`(순수 로직)/`features`(화면)/`shared`(공용)/저장은 분리한다. 복잡한 DI 프레임워크·범용 상태 라이브러리·자체 DSL을 도입하지 않는다. 앱 부트스트랩에서 명시적으로 의존성을 연결하면 충분하다. 하나의 스크린 호스트가 데이터 기반 서사 씬을 바꾼다.

### 4.1 계층 경계
- `core/*`: React·DOM·브라우저 객체 의존이 없는 순수 TypeScript. DTO·상태·명령·조건·퍼즐 판정·힌트 해석·검증. Vitest 단위 테스트 가능.
- `features/*`: 화면과 상호작용. `core`의 명령만 보내고 진행 상태를 직접 수정하지 않는다.
- `shared/*`: 자산 카탈로그·공통 컴포넌트·스타일·저장 어댑터. 판정 로직을 담지 않는다.
- `app/*`: 부트스트랩·최상위 조립·오류 경계·개발/릴리스 경계.
- 테스트: `tests/unit`(순수), `tests/integration`(로더·저장·자산), `tests/e2e`(Playwright). 개발 도구는 `import.meta.env` 플래그로 배포 경계를 확인한다.

의존 방향은 `app → features → core`이며 `core → features` 역방향 import를 금지한다. 린트 규칙 또는 테스트로 확인한다.

### 4.2 핵심 모듈과 역할
| 구성 | 책임 | 금지 |
| --- | --- | --- |
| ContentRepository | JSON 로드·버전·참조 검사, 불변 정의 제공 | UI에서 파일 직접 읽기 |
| GameStore | 유일한 진행 상태와 변경 이벤트 | 여러 전역 progress 싱글턴 |
| CommandDispatcher | 문맥·진입 조건·중복 명령 검증 | 입력 핸들러에서 플래그 직접 대입 |
| StoryReducer | 결정적인 상태 변경, 도메인 이벤트 생성 | DOM·타이머·localStorage 호출 |
| ConditionEvaluator | 허용 조건 enum 판정 | 문자열 eval·알 수 없는 조건 true |
| PuzzleEvaluatorRegistry | 타입별 정답·부분 정답·오답 판정 | 텍스트 키워드 대충 비교 |
| HintService | 현재 장면/퍼즐의 잠금된 힌트 반환 | 미래 정답을 전체 검색해 노출 |
| TestAssistExecutor | 정상 사용자 명령 시퀀스 실행 | solved 플래그 직접 세팅 |
| SaveService | 직렬화·체크섬·백업·버전 복원 | 함수·DOM·React 객체를 저장 JSON에 포함 |
| ScenePresenter | 상태→화면·대사·아트 ID 매핑 | 정답 판정을 UI 안에 작성 |
| AssetProvider | 검증된 ID→URL 제공 | file prefix로 추측 검색 |
| ModalStack | 포커스·입력·닫기 순서 | 중첩 패널 뒤 클릭 전달 |
| Diagnostics | 상태 이벤트·오류 원인 기록 | 사용자 개인정보 자동 수집 |

## 5. 콘텐츠 데이터 계약

### 5.1 JSON과 DTO
모든 정본 콘텐츠는 로컬 파일이다. JSON은 `{ "schemaVersion": 1, "items": [...] }` 같은 최상위 객체로 감싼다. TypeScript 타입은 `core/content/dto.ts`에 선언하고, 로딩 직후 런타임 검증기로 required 필드·공백 ID·중복·enum 범위·참조를 확인한다. 타입 단언(`as`)만으로 검증을 대신하지 않는다.

저장 JSON에는 함수·DOM 노드·React 엘리먼트·`Date` 객체·`Map`/`Set`을 그대로 넣지 않는다. 배열·문자열·숫자·불리언·null로만 직렬화하고, 조회용 `Map`/`Set`은 로드 후 생성한다.

| 정의 | 필수 필드 |
| --- | --- |
| EpisodeDefinition | id, contentVersion, title, subtitle, startSceneId, saveSchemaVersion, artContractVersion, requiredPuzzleIds, optionalSceneIds |
| SceneDefinition | id, act, screenType, backgroundAssetId, entryConditions, preDialogueIds, branchGroupIds, advancingGroupIds, interactionIds, postDialogueIds, grants, requiredReadEvidenceIds, childSceneIds, optionalChildSceneIds, transitions, tipText |
| DialogueLine | id, sceneId, speakerId, speaker, textId, text, portraitAssetId, phase |
| EvidenceDefinition | id, title, titleTextId, source, sourceTextId, body, bodyTextId, proves, provesTextId, limits, limitsTextId, imageAssetId, kind, firstSceneId, sourceMetadata |
| TestimonyStatement | id, sceneId, speakerId, speaker, textId, text, pressTextId, pressResponse |
| PuzzleDefinition | id, sceneId, type, requiredEvidenceIds, targetStatementId, correctChoiceIds, orderedIds, sufficientEvidenceSets, valuePairs, requiredEdges, edgeProofRequirements, reportProofRequirements, andNodeIds, numericAnswers, requiredCaseIds, regions, windowId, successText, partialText, wrongText |
| BranchDefinition | id, sceneId, groupId, label, condition, correct, advances, dialogue, feedback, effects, nextSceneId |
| HintDefinition | id, scopeType, scopeId, level, textId, text, focusTarget |
| GraphDefinition | nodes, causalEdges, signalEdges, eventLog, kinematics |
| MatrixDefinition | dimensions, rows |
| UIString | id, text |

`body`/`bodyTextId`가 제목만 가리키면 안 된다. 마스터 증거 본문의 숫자·시점·원인/한계가 모두 들어가야 한다. 영문 기술 필드는 원본 보기 탭에 남길 수 있지만 기본 보기에는 한국어 설명을 제공한다.

데이터 파일 이름이 바뀌어도 내부 영구 ID는 유지한다. 별도 외부 JSON 라이브러리를 도입할 경우 필요성과 라이선스·패키지 버전을 기록하고 자동 타입 복원 기능을 사용하지 않는다.

### 5.2 씬 예시 — s150
다음은 형식 예시다. 본문 L번호와 pre/post 분할은 마스터 정본이 우선이다. `PresentEvidence`를 성공해야 후반 대사와 새 증거 인계가 열린다.

```json
{
  "id": "s150",
  "act": 1,
  "screenType": "hearing",
  "backgroundAssetId": "bg_hearing_room",
  "entryConditions": [
    {
      "kind": "SceneCompleted",
      "id": "s140"
    }
  ],
  "preDialogueIds": [
    "dlg_s150_001",
    "dlg_s150_002"
  ],
  "interactionIds": [
    "p04"
  ],
  "postDialogueIds": [
    "dlg_s150_003",
    "dlg_s150_004",
    "dlg_s150_005",
    "dlg_s150_006",
    "dlg_s150_007",
    "dlg_s150_008",
    "dlg_s150_009"
  ],
  "onPostDialogueActions": [
    {
      "atDialogueId": "dlg_s150_008",
      "action": "AdmitEvidence",
      "ids": [
        "ev08",
        "ev09"
      ]
    }
  ],
  "transitions": [
    {
      "target": "s160",
      "conditions": [
        {
          "kind": "PuzzleSolved",
          "id": "p04"
        },
        {
          "kind": "EvidenceOwned",
          "id": "ev08"
        },
        {
          "kind": "EvidenceOwned",
          "id": "ev09"
        },
        {
          "kind": "PostDialogueComplete",
          "id": "s150"
        }
      ]
    }
  ]
}
```

실제 s150 대사는 L009까지다. 예시 데이터와 본문 개수를 검증한다. 알 수 없는 `dlg_s150_010` 등을 조용히 건너뛰지 않는다. scene/phase cursor를 저장하면 대화 종료 후 성공 대사가 먼저 나오는 문제를 막을 수 있다.

### 5.3 퍼즐 정답 데이터의 정규화
마스터 answer 객체는 의미 정본이다. 직렬화 형식은 다음과 같이 정규화한다. 자유로운 object를 그대로 판정기에 넣지 않는다.

```ts
export interface EvidenceSetDto { requiredIds: string[]; }
export interface StringPairDto { key: string; value: string; }
export interface EdgeDto { from: string; to: string; kind: 'causal' | 'signal'; }
export interface NumericAnswerDto { key: string; value: number; tolerance: number; }
export interface EdgeProofDto {
  from: string;
  to: string;
  kind: 'causal' | 'signal';
  sufficientEvidenceSets: EvidenceSetDto[];
}
export interface ReportProofDto {
  section: string;
  item: string;
  sufficientEvidenceSets: EvidenceSetDto[];
}
export interface SolutionDto {
  correctChoiceIds: string[];
  orderedIds: string[];
  sufficientEvidenceSets: EvidenceSetDto[];
  valuePairs: StringPairDto[];
  requiredEdges: EdgeDto[];
  edgeProofRequirements: EdgeProofDto[];
  reportProofRequirements: ReportProofDto[];
  andNodeIds: string[];
  numericAnswers: NumericAnswerDto[];
  requiredCaseIds: string[];
}
```

`evidenceSets`는 최소 충분 근거 집합이다. 정답 집합이 선택에 포함되어 있고 선택 자료가 모두 보유 자료라면 성공한다. 추가 자료 때문에 정답을 틀렸다고 하지는 않되 UI에서 핵심으로 사용된 자료만 강조한다. 최대 3개 제출로 무차별 전체 선택을 막는다. p19는 세 자료가 모두 필요하다. 타깃 증언 ID가 다르면 자료가 맞아도 성공하지 않는다. 날짜·수치·한국어 원문 문자열을 부분 일치 검색해 판정하지 않는다.

### 5.4 진행 상태 DTO
```ts
export interface PuzzleDraftDto {
  puzzleId: string;
  type: string;
  selectedStatementId: string;
  selectedEvidenceIds: string[];
  orderedIds: string[];
  placedCardIds: string[];
  edges: EdgeDto[];
  selectedWindowId: string;
  executedCaseIds: string[];
  reportPlacements: StringPairDto[];
}

export interface HintUsageDto {
  scopeId: string;
  maxLevel: number;
  usedKinds: string[];
  useCount: number;
}

export interface ProgressSnapshot {
  saveSchemaVersion: number;
  contentVersion: string;
  artManifestSha256: string;
  slotId: string;
  currentSceneId: string;
  phase: string;
  dialogueCursor: number;
  activePuzzleId: string;
  ownedEvidenceIds: string[];
  readEvidenceIds: string[];
  visitedSceneIds: string[];
  completedSceneIds: string[];
  solvedPuzzleIds: string[];
  visitedTopicIds: string[];
  branchValues: StringPairDto[];
  puzzleDrafts: PuzzleDraftDto[];
  hintUsage: HintUsageDto[];
  appliedEffectIds: string[];
  lastCommandSequence: number;
  savedAtUtc: string;
  testAssistUsed: boolean;
}
```

`PuzzleDraftDto`에는 타입, 선택된 증언·증거, 카드 순서, 연결선, 비교 시점, 실험 실행 목록, 보고서 구역 배치를 저장한다. `HintUsageDto`에는 scopeId·최대 열람 수준·실행한 도움 종류·사용 횟수를 저장한다. 정의는 구체 타입으로 구현하며 `Record<string, unknown>`으로 뭉개지 않는다. 장면 완료와 현재 화면은 별개이므로 허브를 재방문해도 자식 완료가 사라지지 않는다.

## 6. 진행 엔진

### 6.1 명령 타입
허용되는 대표 명령은 `StartEpisode`, `AdvanceDialogue`, `ChooseTopic`, `InspectRegion`, `OpenEvidence`, `ReadEvidence`, `SelectStatement`, `PressStatement`, `SelectEvidence`, `PresentEvidence`, `PlaceCard`, `ConnectNodes`, `RemoveEdge`, `SelectComparisonWindow`, `RunCounterfactualCase`, `SubmitPuzzle`, `ChooseBranch`, `ContinueScene`, `ReturnToHub`, `SaveSlot`, `LoadSlot`, `OpenHint`다.

명령은 TypeScript discriminated union으로 선언한다. 모든 명령에 `commandId`, `sequence`, `sceneId`, `origin`(`'user' | 'test-assist' | 'replay'`)을 넣는다. `origin=test-assist`라도 정답 판정과 접근 조건은 완화하지 않는다. 반환은 `Accepted / Rejected / Partial / Solved`와 reasonCode·변경 이벤트다. UI 문구와 내부 reasonCode를 분리한다.

```ts
export type CommandOrigin = 'user' | 'test-assist' | 'replay';

export interface CommandEnvelope<C> {
  commandId: string;
  sequence: number;
  sceneId: string;
  origin: CommandOrigin;
  command: C;
}

export interface CommandResult {
  status: 'Accepted' | 'Rejected' | 'Partial' | 'Solved';
  reasonCode: string;
  events: DomainEvent[];
}
```

### 6.2 상태 변경 순서
```text
입력
 → 해당 모달·화면이 입력을 소유하는지 확인
 → 현재 scene/phase와 명령 접근 조건 검사
 → 이미 처리한 commandId/effectId 여부 검사
 → 순수 판정기 평가
 → 상태 변경 + 도메인 이벤트 원자적 구성
 → Store에 한 번 반영
 → 필요한 저장 체크포인트 예약
 → 프레젠터가 변경 부분만 갱신
```

Reducer는 순수 함수다. `setTimeout`/`requestAnimationFrame`으로 페이드가 끝난 뒤 임의 플래그를 바꾸지 않는다. 상태 전환은 먼저 확정하고 UI는 이를 표현한다. 전환 중 명령 재진입을 막되 취소·에러 시 입력 잠금이 반드시 해제돼야 한다. 저장 실패는 진행 상태를 조용히 초기화하지 않고 저장 재시도 상태로 표시한다.

### 6.3 진입 조건과 완료 조건
조건은 enum 기반 원자 조건과 AND/OR 묶음으로 만든다. 지원: SceneCompleted, EvidenceOwned, EvidenceRead, PuzzleSolved, TopicVisited, BranchEquals, PostDialogueComplete, AllRequiredChildrenComplete. 알 수 없는 타입은 검증 오류이며 false로 판정한다. `if (!condition) return true` 같은 편의 처리는 금지한다.

허브의 child들은 완료 후에도 재방문할 수 있다. 재방문에서는 새로운 증거 지급 이벤트를 다시 실행하지 않는다. scene entry에 부작용을 몰아넣지 말고 인계·열람·성공 이벤트로 나눈다. 마스터의 증거 획득 트리거 표를 구현한다. 특히 s150의 ev08/09는 p04 성공 뒤, s530의 ev35는 동의 선택 때만 지급한다.

s640 진입 시 p01~p19 완료가 필요하고, p20은 그 화면에서 푼다. `p01~p20 완료`를 s640의 진입 조건으로 넣으면 순환 잠금이 된다. 전체 20개 완료 검사는 **보고서 제출 후 사건 완료 전**에 한다.

### 6.4 대사와 증거 공개 순서
마스터의 L번호와 interaction 삽입 위치를 그대로 컴파일한다. 자료 패널은 해금된 원문을 보여줄 수 있지만 성공 후 플레이어 추리 대사는 미리 보여주지 않는다. 대화기록에는 실제 재생된 줄만 들어간다. 글자 즉시 출력은 현재 줄을 완성할 뿐 다음 퍼즐·선택지를 자동으로 통과하지 않는다.

이미 표시한 성공 반응을 postDialogue에서 또 보여주지 않도록 textId를 공유하거나 한 위치에서만 재생한다. 삭제 가능한 것은 의미가 완전히 같은 중복 재생뿐이며, 새로운 사실·유족 동의·한계 설명을 축약해 빼지 않는다.

## 7. 퍼즐 판정기별 구현

| 타입 | 판정 | UI/오답 세부 |
| --- | --- | --- |
| hotspot_set | 필요한 regionId 방문 집합 포함 | 보이지 않는 픽셀 탐색 금지, 영역 표시 켜기 |
| statement_refutation | 타깃 statementId + 최소 충분 근거 집합 | 다른 주장/부분 근거/무관 자료를 구분 |
| source_mapping | source slot→evidence ID 일치 | 큰 신뢰도 수치 선택 게임으로 축소 금지 |
| timeline_order | 사건 ID 순서 + 해석 선택 | 시각 차이는 자동 계산, ms 정수 |
| trace_mapping | 모듈·요청·허용·실제값 키 일치 | 요청값과 실제 감속 혼동 설명 |
| cross_camera | 동일 구간 + 두 영상·레이더 | 단일 카메라로 결론 내리면 부분 반응 |
| chronology | 세 기록 순서 + 마지막 승인 상태 | 임시 수리=영구 승인 오답 |
| argument_choice | 정당한 결론 choiceId | 선택 근거를 한 문장으로 설명 |
| diff_select | 제거 블록 ID | 원본 코드 편집기·실행 기능 없음 |
| assumption_link | 전제 키 + 반증 자료 | 아키텍처 문서와 실제 운영 연결 |
| matrix_gap | 누락 조합의 차원 ID 집합 | 총 횟수가 답이 아님 |
| deployment_chain | 설계·설치·미완료 조건 연결 | 최신 버전 추정 금지 |
| counterfactual | 3개 허용 사례 실행 + 결과 해석 | 임의 차량 설정 입력을 받는 시뮬레이터 아님 |
| causal_graph | 타입 포함 간선 집합·AND 결합·근거 | 순환·자기 연결·사후→사전 차단 |
| responsibility_matrix | 다섯 구역과 근거의 정합 | 책임 없음·모두 동일·고의 확정 오답 |

같은 타입의 퍼즐은 데이터로 재사용하고 p04만 UI 코드에 하드코딩하지 않는다. 반면 게임 전체를 위해 범용 자연어 논리 판정기를 만들 필요도 없다. 허용된 카드·선택지·간선으로 한정된 **결정적 판정**이 목적이다.

```ts
export type PuzzleVerdict = 'Solved' | 'Partial' | 'Wrong' | 'Rejected';

export interface PuzzleEvaluation {
  verdict: PuzzleVerdict;
  reasonCode: string;
  highlightIds: string[];
}

export type PuzzleEvaluator = (
  puzzle: PuzzleDefinition,
  draft: PuzzleDraftDto,
  state: ProgressSnapshot
) => PuzzleEvaluation;

export const puzzleEvaluators: Record<string, PuzzleEvaluator>;
```

### 7.1 인과판
카드의 위치는 자유롭게 옮길 수 있지만 정답은 좌표가 아니라 노드/간선이다. `from→to` 방향을 분명히 표시한다. causal와 signal은 색뿐 아니라 라벨·선 모양으로 구분한다. AND 노드의 두 입력이 하나만 연결되면 부분 완성으로 표시한다. 중간 저장 시 카드 좌표와 연결선 초안을 보존하고, 불가능한 연결을 그리면 즉시 이유를 보여준다.

마스터 p17의 `proofRequirements` 8개를 간선별 `EdgeProofDto`로 정규화한다. 각 항목의 evidenceSets는 sufficientEvidenceSets로 매핑한다. causal/signal 종류도 비교하며 한 증거가 여러 간선을 뒷받침해도 소비하거나 중복 사용을 금지하지 않는다. 최종 완료는 연결 형태뿐 아니라 핵심 간선에 근거가 붙어 있어야 한다. 자동 배치 기능은 공간 정리만 하고 정답 간선을 몰래 만들지 않는다. 테스트 정답 행동 실행만 명시적으로 정답 간선을 구성한다.

### 7.2 반사실 비교
`historical`, `gate_restored`, `dispatch_blocked` 세 사례만 구현한다. 표와 타임라인은 정해진 자료에서 나온다. 물리 시뮬레이션 루프로 차를 달리게 하지 않는다. 애니메이션 프레임률이 달라도 수치가 달라지면 안 된다. 공식 계산을 순수 함수로 검산하고 `Number.EPSILON`이 아니라 명시한 오차 범위로 비교하며 결과 표시는 2자리 반올림한다. 실제 요청 -8.1, 실제 강한 감속 -6.2를 별도 라벨로 유지한다. 배차 차단은 충돌 시각 그래프가 아니라 “운행 없음” 패널이다.

### 7.3 보고서
마스터 14장의 고정 항목 팔레트를 사용하고 p20 proofRequirements를 `ReportProofDto(section,item,sufficientEvidenceSets)` 배열로 변환한다. 한계 항목에는 증거를 강제로 붙이지 않는다. p20 정답에 쓰인 자료는 퍼즐 requiredEvidenceIds에 모두 포함한다.

직접 원인/기여 요인/조직 관리/사고 후 정보 공개/판단 한계의 다섯 구역이다. 어느 인물이 악인인지 선택하는 버튼으로 대체하지 않는다. 행위·인지·권한·근거를 보여준다. 채택 전에는 수정 가능한 초안, 채택 후에는 읽기 전용 결과다. 다른 최종 대사 선택이 보고서 사실을 바꾸면 안 된다.

## 8. 화면 조립 상세

### 8.1 App과 화면 조립
`App.tsx`는 ErrorBoundary, 전역 스타일, 스크린 호스트, 모달 스택, 개발 오버레이를 조립한다. 레이어는 배경층, 인물층, 화면 콘텐츠층, 대화층, 글로벌 HUD, 모달층, 개발 오버레이층으로 나눈다. 비활성 화면은 `pointer-events`와 `aria-hidden`으로 입력·접근에서 제외한다. 같은 서비스를 가진 스토어를 중복 생성하지 않는다.

화면 전환은 라우터 없이 상태 기반으로 매핑해도 되고, URL 동기화가 필요하면 해시 라우팅을 쓴다. 새로고침 시 저장 복원이 URL보다 우선한다. React `StrictMode`에서 중복 effect가 실행돼도 명령이 중복 처리되지 않아야 한다(commandId 멱등성).

### 8.2 해상도와 타이포그래피
1920×1080을 기준 비율로 두고 CSS `clamp()`·컨테이너 쿼리·미디어 쿼리로 1280×720, 1920×1080, 2560×1440, 16:10, 모바일 폭을 대응한다. 고정 캔버스 축소 방식이 아니라 실제 HTML 레이아웃으로 구현한다. 단계적 확대를 위해 글자 크기 100/125/150%를 앱 설정으로 제공하고 `rem` 기반으로 적용한다.

최소 버튼 클릭 영역은 표시 기준 44px 상당을 목표로 하고 목록 간 간격을 확보한다. 긴 한국어 문장은 스크롤과 적절한 줄바꿈으로 수용한다. 증거 뷰에 작은 JSON을 기본으로 가득 채우지 않는다. 요약 탭이 기본이고 원본은 별도 탭이다. 원본 코드나 로그를 편집/실행하는 기능은 없다.

대사 상자에는 현재 화자명·직책, 본문, 다음 표시가 있다. 인물 반신이 대사와 경쟁하지 않게 하며 눈·입을 가리지 않는다. UI 문자열과 본문에는 HTML 삽입을 금지하고 React 텍스트 노드로만 렌더링한다. 외부 저장 파일의 문자열을 `dangerouslySetInnerHTML`로 실행하지 않는다.

### 8.3 아트 동기화와 카탈로그
`tools/sync_web_assets.py`(또는 동등 스크립트)가 `manifest.lock.json`을 읽어 `art_delivery/runtime/<path>` → `web/public/art/<path>`로 복사하고, `web/src/shared/assets/assetCatalog.ts`에 ID→URL·kind·anchor·regions·원본 해시를 생성한다. 게임 JSON은 URL을 직접 담지 않고 assetId만 가진다. 런타임에서 파일명을 조합하거나 glob으로 검색하지 않는다.

카탈로그 생성 후 실제 파일 존재·해시를 검증하고, 불일치가 있으면 sync 실패로 처리한다. 작은 썸네일을 위해 새 생성 이미지를 요구하지 말고 같은 이미지를 CSS로 축소 표시한다.

이 사건 규모에서는 ID→URL 직접 참조로 단순하게 시작한다. 성능 예산을 넘으면 동일 `AssetProvider` 경계 안에서 그룹 프리로드를 개선할 수 있지만 파일명·스토리·아트 계약을 바꾸지 않는다. 측정 없이 CDN·압축 포맷 변환·서비스워커부터 도입하지 않는다.

### 8.4 단서 영역 좌표 변환
manifest region은 이미지 좌상단 기준 정규화(`x,y,width,height`, 0~1)다. UI에 contain으로 표시된 **실제 이미지 사각형**을 구한 뒤 변환한다. 전체 패널에는 레터박스가 있을 수 있다.

```text
displayLeft, displayTop, displayWidth, displayHeight = 실제 표시된 이미지 영역
regionLeft = displayLeft + normalizedX * displayWidth
regionTop  = displayTop  + normalizedY * displayHeight   # 웹은 Y가 아래로 증가
regionWidth  = normalizedWidth  * displayWidth
regionHeight = normalizedHeight * displayHeight
```

좌표계를 명시하고 네 모서리·중앙·레터박스 단위 테스트를 작성한다. `getBoundingClientRect()`로 표시 영역을 구해 같은 CSS 픽셀 공간에서 계산한다. p01은 bg_crosswalk_dawn의 crosswalk/signal/vehicle 영역, ev05는 ev_cabin_base.warning_display, ev06은 ev_front_base.pedestrian을 사용한다. 경고문·인식 박스·값은 코드로 올린다.

영상형 증거는 마스터 14.5에 따른 기준 프레임+기록 주석 재생이다. 동영상 파일을 추가 생성하거나 정적 PNG를 실제 원본 영상인 것처럼 자동 이동하지 않는다. p05의 초기 카드와 후반 통합 시간축은 같은 이벤트 데이터에서 소유 증거로 필터링한다. 인물·CG의 실제 사용 위치는 마스터 12.1 cue 표를 적용한다.

### 8.5 화면별 수용 기준
| 화면 | 최소 완성 기준 |
| --- | --- |
| 시작 | 새 게임·이어하기·설정·기록, 손상 저장 처리, 경고 확인 |
| 허브 | 미완료·완료·잠금 구분, 잠금 이유, 현재 목표 |
| 조사 | 핫스폿 설명·확인 표시·재열람, 조사표시 토글 |
| 증거책 | 보유 자료만 노출, 종류 필터, 원문/요약/출처/한계, 최근 획득 표시 |
| 심문 | 증언 선택·추궁·다중 증거·부분 정답·원래 위치 복귀 |
| 로그 | 선택 시점 유지, 요청/실행 구분, 원본 보기, 생성 출처 |
| 비교 | 두 자료 동일 구간 정렬·오차 표기·판단 제출 |
| 타임라인 | 카드 위치·시각·간격·해석, 클릭 대체조작 |
| 변경 비교 | 삭제 블록 강조·성능 대기 설명, 원본 코드 실행 금지 |
| 시험표 | 해당 조합을 눈으로 고를 수 있는 표, 총 횟수와 범위 구분 |
| 반사실 | 세 시나리오·가정·결과·한계, 미실행 사례 상태 |
| 인과판 | typed edge·AND·간선별 근거·초안 저장·수정 |
| 보고서 | 다섯 구역·근거·제출 확인·읽기 전용 결과 |
| 팁 | 장면별 다음 행동과 H1/H2/H3·테스트 정답 실행 |
| 설정 | 글자 크기·출력 속도·동작 줄이기·오디오 OFF에서도 진행 |
| 사건 기록/엔딩 | 완료 증거·퍼즐·분기 요약, 타이틀 복귀 |

## 9. 팁과 테스트 지원

### 9.1 현재 도움 문맥 결정
가장 위 UI의 `InteractionContext`를 확인한다. 활성 퍼즐이 있으면 그 퍼즐, 허브이면 남은 필수 조사, 문서이면 해당 장면의 문서 확인 항목, 대화이면 다음 진행 행동, 종료 화면이면 기록/재시작을 안내한다. 단순히 전체 미해결 퍼즐 중 첫 번째를 골라 보여주지 않는다. 다른 장소의 질문을 보다가 미래 정답이 나오는 버그를 막는다.

힌트는 마스터의 고정 문장이다. 런타임 LLM에게 힌트를 작성시키지 않는다. H3를 열어도 퍼즐 해결 상태는 바뀌지 않는다. 초급 모드·팁 사용 여부로 캐릭터를 비난하거나 엔딩을 나쁘게 만들지 않는다.

### 9.2 정답 행동 실행
```text
사용자 “정답 행동 실행” 선택
 → 테스트 도구 활성/현재 문맥 확인
 → 현재 퍼즐의 자료 접근 조건 확인
 → 불충족이면 획득 위치 안내; 증거 생성 금지
 → 정상 명령 리스트 생성
 → 한 명령씩 동일 Dispatcher로 실행
 → 각 결과 검증; 실패 시 즉시 멈추고 원인 표시
 → 정상 성공 이벤트·대사·저장 처리
 → hint usage에 test_assist 기록
```

예: p04는 SelectStatement(st150_02), SelectEvidence(ev06), SelectEvidence(ev07), PresentEvidence 순서다. p17은 카드를 슬롯에 배치하고 허용된 일곱 인과선과 한 신호선을 연결한 뒤 간선 근거를 연결하고 SubmitPuzzle을 호출한다. 한 줄로 `solvedPuzzleIds.push` 하거나 이후 씬으로 이동시키지 않는다.

### 9.3 개발 패널과 릴리스 경계
개발 모드는 현재 scene/phase/puzzle, 보유·읽음·완료 집합, 직전 명령 결과, 현재 목표와 블로커를 표시할 수 있다. 불변 픽스처로 점프하는 기능은 `LoadScenarioFixture`로 구분해 테스트 오염 표시를 남긴다. 임의 점프 후 완주를 일반 세이브로 저장하지 않는다. `VITE_ANL_TEST_TOOLS`가 없는 릴리스에서는 패널·정답 행동 실행·픽스처 로더가 접근 불가이며, Vite의 `import.meta.env` 분기로 코드가 번들에서 제거된다. 릴리스 검증 테스트는 `dist/` 번들에 test-assist 문자열·픽스처 UI 문자열이 없음을 assert한다.

## 10. 저장·복구·버전 (localStorage)

### 10.1 저장 위치와 키
브라우저 `localStorage`를 사용한다. 서버·파일 시스템·쿠키에 진행 상태를 쓰지 않는다. 키 구조는 다음과 같다.

```text
anl.ep01.save.autosave        # 자동 저장
anl.ep01.save.autosave.bak    # 직전 유효 자동 저장 백업
anl.ep01.save.slot_01
anl.ep01.save.slot_01.bak
anl.ep01.save.slot_02
anl.ep01.save.slot_03
anl.ep01.settings             # 화면·접근성 설정
anl.ep01.meta                 # 마지막 사용 슬롯·스키마 버전
```

`localStorage` 사용 불가(프라이빗 모드·용량 초과)를 감지하면 메모리 저장으로 조용히 대체하지 않고 사용자에게 저장 불가 상태를 알리며 진행은 계속할 수 있게 한다.

### 10.2 저장 형식과 쓰기
Envelope에 `schemaVersion`, `contentVersion`, `payloadJson`, `payloadSha256`을 둔다. payload의 안정적인 키 순서로 직렬화한 문자열의 해시를 계산하고, 해시 검증 뒤 DTO를 읽는다. 체크섬은 손상 탐지이며 악의적 조작 방지/암호화가 아니다. SHA-256은 Web Crypto(`crypto.subtle.digest`)를 사용하고, 실패 시 명시적 오류를 낸다.

쓰기 순서는 새 값을 임시 키에 문자열로 쓴 뒤 검증하고, 기존 값을 `.bak` 키로 복사한 다음 본 키를 교체하는 순서로 처리한다. 저장 요청은 직렬 큐로 합치며 이전 스냅샷이 최신 스냅샷을 덮어쓰지 않게 sequence를 비교한다. 증거 스크롤마다 저장하지 않는다.

### 10.3 복원
크기 상한·JSON 파싱·체크섬·버전·ID·상태 불변 조건을 검증한다. 알 수 없는 미래 버전은 읽기 중단 및 원본 유지. 손상 시 백업 복원을 제안한다. 백업도 없으면 새 게임 선택을 제공하되 사용자 동의 없이 삭제하지 않는다. 새 슬롯은 기존 슬롯 덮어쓰기 확인을 거친다.

중간 퍼즐에서 저장한 경우 선택된 증언·자료·간선·실험 실행목록과 phase를 복원한다. 타이핑 중 저장은 현재 줄을 완전히 보이는 상태로 복원하고 다음 줄로 건너뛰지 않는다. 애니메이션은 안전한 정지 상태에서 재개하고 진행 이벤트를 재실행하지 않는다. 인물 표정은 저장 상태와 대사 정의로 재계산하되 그림 파일 순서에 의존하지 않는다.

### 10.4 저장 시뮬레이션 실패 사례
용량 초과, 빈 값, 잘린 JSON, 유효 JSON이지만 없는 ID, 체크섬 불일치, 백업만 남은 상태, 성공 후 대사 도중 종료, 퍼즐 간선 편집 도중 종료, 같은 증거 재획득, s530 거절 후 로드, s700 응답 직후 종료를 반드시 시험한다. 실패해도 게임 상태를 몰래 초기화하지 않는다.

## 11. 구현 단계와 게이트 (03 문서 W단계 기준)

각 단계는 실제 코드·실행 결과와 함께 기록한다. 다음 단계로 넘어가기 전에 치명적 차단 오류를 해소한다. 작업 중 별도 계획 문서를 장황하게 다시 쓰는 대신 `reports/IMPLEMENTATION_STATUS.md`와 `docs/WEB_IMPLEMENTATION_STATUS.md`에 완료/진행/차단과 근거 파일만 유지한다. 세부 W단계 정의와 승인 게이트는 `03_WEB_MIGRATION_AND_CONTINUATION_PLAN.md`가 정본이다.

| 단계 | 구현 | 통과 기준 |
| --- | --- | --- |
| W0 | 문서 웹 개정·기준선 보존 | 세 문서에 유효 Unity 지시 없음, 해시·CHANGELOG 갱신 |
| W1 | 웹 프로젝트·lint·typecheck·test·build·타이틀 | 개발 서버 렌더·모든 검증 명령 성공 |
| W2 | 콘텐츠·아트 동기화·스키마·카탈로그 | 43/36/20 수 일치, 54 이미지 ID 조회, 404 없음 |
| W3 | core 포팅(상태·조건·명령·퍼즐·힌트·저장) | 기존 EditMode 의미의 Vitest 전부 통과 |
| W4 | 화면 이전(타이틀~엔딩·진단 패널) | 실제 브라우저에서 모든 사용자 행동 가능 |
| W5 | 웹 이전 완료 판정 | 기능·테스트·규칙 동등성 확인 |
| W6 | 미완료 작업 연속 개발 | `PARTIAL`·`MISSING` 0 |
| W7 | 브라우저 QA·배포 빌드 | Chromium·Safari·모바일·저장 복원·20퍼즐·오프라인 |
| W8 | Unity 제거 | `Game/` 없이 build·전체 테스트 통과 |

W4에서 전 구간 기능의 뼈대를 만들 수 있지만 거기서 “MVP 완료”라고 종료하지 않는다. G1을 이미 통과했으므로 어느 단계에서도 임시 아트가 필요하지 않다. 미구현 화면은 내부 개발 상태로 명시하며 최종 납품에서는 0개여야 한다.

## 12. 자동 테스트 설계

### 12.1 정적 콘텐츠 검증
`tools/validate_content.py`와 TypeScript 검증기로 다음을 확인한다: 43씬·36증거·20퍼즐·54아트 ID, 참조 누락, 중복 문자열 ID, 빈 본문, 고정된 정답/힌트 누락, 존재하지 않는 이미지, p04의 증거 접근 가능성, s640의 순환 잠금, 불가능한 phase, 없는 분기 target, 원인/신호선 종류, 선택 증거를 필수로 요구하는 실수.

퍼즐 성공 전 postDialogue가 접근 가능한 경로를 찾고 실패로 처리한다. 직렬 진행표뿐 아니라 허브의 허용 방문 순열을 검증한다. 텍스트 길이·글리프 목록·포맷 토큰이 깨졌는지도 검사한다. 정적 분석이 모든 UI 버그를 증명한다고 주장하지 않는다.

### 12.2 순수 상태 테스트
아래 테스트는 최소 계약이며 테스트 개수를 채우는 대신 중요한 불변 조건을 실제로 assert한다. 성공·부분 정답·완전 오답·중복 명령·로드 후 재실행을 각각 분리한다.

| 테스트 ID | 대상 | 기대 결과 |
| --- | --- | --- |
| CT01 | 세 콘텐츠 ID 집합 | 43/36/20개의 정확한 ID; 중복·누락 없음 |
| CT02 | 아트 참조 | 모든 참조가 54개 카탈로그에 존재 |
| CT03 | 정답 접근성 | 퍼즐 진입 전에 requiredEvidence를 확보 가능 |
| CT04 | 증거 획득 순서 | s150 추가 자료는 p04 성공 뒤 획득 |
| CT05 | 선택 증거 | ev34·ev35 없이도 완주 |
| CT06 | 대사 phase | 정답 전 성공 대사·대화기록 노출 없음 |
| CT07 | s640 순환 잠금 | p01~19로 진입, p20은 안에서 해결 |
| CT08 | 불명 조건 | unknown enum/빈 ID는 fail closed |
| ST01 | 증거 중복 지급 | 같은 effect 두 번 실행해도 한 ID |
| ST02 | 명령 중복 | 같은 commandId로 두 번 진행 안 함 |
| ST03 | 잘못된 증언 | 맞는 증거라도 다른 statementId면 실패 |
| ST04 | 근거 순서 | ev06/ev07 제시 순서 반대도 동일 성공 |
| ST05 | 부분 근거 | ev06만 제시하면 Partial, 해결 안 됨 |
| ST06 | 추가 근거 | 필요 집합+보유 추가 자료 1개는 핵심 근거로 성공 |
| ST07 | 미보유 자료 | ID를 외부 입력해도 제출 거절 |
| ST08 | 인과판 순환 | 자기 연결·순환·중복 edge 거절 |
| ST09 | 선 종류 | 신호선과 책임/인과선 혼동 시 실패 |
| ST10 | 사후 왜곡 | 발표 편집을 사고 이전 원인으로 연결 불가 |
| ST11 | KPI 단독 | p18 KPI만 제출해 관리 인지 입증 안 됨 |
| ST12 | 재구성 수치 | 20.28915m·27.5168km/h·14.79021m 오차 범위 통과 |
| ST13 | 프레임 독립 | 반사실 재생속도 달라도 결과 일정 |
| ST14 | 유족 동의 | 자료 거절이면 visit=true, ev35 미보유 |
| ST15 | 두 응답 엔딩 | facts/prevention 모두 보고서 사실 동일 |
| ST16 | 허브 순열 | s100 6순열·s200 6순열 모두 같은 합류 상태 |
| ST17 | 개발 허브 순서 | s411>s410 제약을 지킨 모든 허용 순서 |
| ST18 | 최종 준비 순서 | s510/s540 순서 반전·선택 면담 유무 |
| HI01 | 장면 팁 | 43씬 각각 현재 필요한 행동 제공 |
| HI02 | 퍼즐 팁 | 20퍼즐 H1/H2/H3 누락 없음 |
| HI03 | 정답 보기 | H3 열람만으로 solved 변경 없음 |
| HI04 | 정답 실행 | 모든 퍼즐에서 정상 성공 결과와 동일 |
| HI05 | 증거 누락 | 자동해결이 증거 지급하거나 미래로 점프 안 함 |
| HI06 | 문맥 | 문서/허브/심문 모달에서 다른 씬 팁 안 뜸 |
| SV01 | 진행 round-trip | 모든 required checkpoint serialize/deserialize 동일 |
| SV02 | 중간 심문 | 선택 증언·자료·스크롤 복원 |
| SV03 | 중간 인과판 | 카드 위치·typed edges·근거 보존 |
| SV04 | 중간 대사 | 현재 줄 유지, 보상 재실행 없음 |
| SV05 | 부분 저장 | 잘린 값→백업 제안, 자동 삭제 없음 |
| SV06 | 체크섬 오류 | 변조/손상 탐지 및 원본 보존 |
| SV07 | 미래 버전 | 호환불가 안내, 조용한 리셋 없음 |
| SV08 | 없는 ID | 마이그레이션/오류 안내, 엉뚱한 씬 대체 없음 |
| SV09 | 쓰기 실패 | 진행 유지·미저장 표시·재시도 |
| SV10 | 동시 저장 | 오래된 스냅샷이 최신 저장 덮지 않음 |
| UI01 | 해상도 | 720p/1080p/1440p/16:10/모바일 폭 정보·버튼 잘림 없음 |
| UI02 | 글자 크기 | 100/125/150%에서 대사·문서·팁 사용 가능 |
| UI03 | 키보드 | 마우스 없이 기본 퍼즐·심문·종료 가능 |
| UI04 | 모달 포커스 | 모달 뒤 입력·중복 Esc·키 전파 없음 |
| UI05 | 연타 | 다음·제시 더블클릭으로 씬 건너뛰지 않음 |
| UI06 | 인물 표정 | 모든 차분 눈높이·크롭·알파 확인 |
| UI07 | 단서 좌표 | contain 레터박스에서도 bbox/hotspot 일치 |
| UI08 | 스포일러 | 미획득 증거 제목/썸네일/힌트 숨김 |
| UI09 | 대화 로그 | 미재생 성공 대사 없음, 뒤로 보기 정상 |
| UI10 | 무음·동작 감소 | 소리·흔들림 없이도 모든 단서 이해 |
| BL01 | 개발 빌드 | 팁·개발 패널 접근과 표시 정상 |
| BL02 | 릴리스 경계 | test assist·fixture load·debug panel 문자열이 `dist/`에 없음 |
| BL03 | 오프라인 | 네트워크 차단 상태에서 시작~종료 완주 |
| BL04 | 재시작 | 브라우저 탭 종료 후 저장 이어하기 |
| BL05 | 아트 무결성 | 빌드 전 원본·동기화 ID·참조 검증 |
| BL06 | 실제 빌드 | `npm run build` 성공·산출물 크기·미리보기 실행 존재 |

### 12.3 경로 탐색 테스트
허브 상태를 논리적으로 압축한 탐색기(순수 TS)로 가능한 조사 순서를 생성한다. 맹목적인 전체 클릭 공간 탐색이 아니라, 미완료 필수 조사 선택·해당 퍼즐 정답 행동·합류 상태를 검증하는 경로 집합을 만든다. 모든 허용 경로에서 최종 필수 증거와 퍼즐 집합이 같아야 한다. 선택 질문과 개인 자료 수신 여부는 별도 조합으로 검사한다.

필수 상태마다 Save→Load를 삽입한 경로를 실행한다. p17의 간선 하나만 배치한 상태, p04의 증거 하나만 고른 상태, p16의 실험 하나만 실행한 상태도 저장한다. 정상 완주와 정답 행동 실행 완주의 상태 차이는 도움 사용 이력과 명령 출처 등 허용 필드뿐이어야 한다.

### 12.4 테스트·검증 명령
```bash
cd web
npm run lint                              # 정적 린트
npm run typecheck                         # tsc --noEmit (strict)
npm run test -- --run                     # Vitest 단위·통합
npm run test:coverage                     # 커버리지 리포트
npm run test:e2e                          # Playwright (브라우저 설치 필요)
npm run build                             # production 정적 빌드
npm run preview                           # dist 미리보기 서버
npm run validate:content                  # tools/validate_content.py
npm run assets:sync -- --check            # 아트 동기화·드리프트 검사
```

테스트가 0개 실행됐으면 PASS로 처리하지 않는다. Playwright 브라우저 바이너리를 설치할 수 없는 환경이면 E2E를 NOT_RUN으로 남기고 대체 증빙을 기록한다. `npm run preview`로 실제 `dist/`를 열어 해시 라우팅·정적 자산 로딩을 확인한다.

### 12.5 실제 화면 검수
다음 대표 화면을 실제 실행 중 저장한다: s000 타이틀, s120 대화/팁, s150 증거 제시, s160 타임라인, s220 동기화 비교, s310 부분 근거, s410 diff, s430 커버리지, s510 실험, s610 인과판 중간/완료, s640 보고서, s700 엔딩, 설정 150% 화면, 저장 손상 안내, 모바일 폭 화면.

스크린샷 파일명은 예를 들어 `reports/screenshots/s150_evidence_1920x1080.png`. 빌드·콘텐츠 버전·해상도·브라우저·재현 절차를 함께 기록한다. 개념 시안 이미지를 실행 스크린샷 대신 제출하지 않는다. 화면을 실제로 보지 않았으면 시각 PASS라고 쓰지 않는다.

## 13. 성능·안정성·접근성

텍스트와 정적 이미지 중심이므로 복잡한 최적화보다 불필요한 작업을 먼저 없앤다. 활성 화면 밖의 비싼 레이아웃 갱신, 매 렌더 텍스트 재할당, 반복 JSON 파싱, 대화 기록 전체 재생성을 피한다. 목록은 필요한 범위만 렌더링한다. 프로파일링 없이 프레임 드롭 원인을 추측하지 않는다.

데스크톱 기준 목표: 일반 선택/증거 열기 후 100ms 이내의 즉각적 UI 반응, 애니메이션 중 60fps 근접, 화면을 닫고 열 때 꾸준한 메모리 증가 없음, 초기 로드 시 필수 콘텐츠·아트가 렌더를 막지 않음. production 자산 총량과 초기 번들 크기를 기록하고, 초과 시 이미지 프리로드 순서·코드 분할을 먼저 분석한다. 이 숫자는 이미 달성했다는 주장이 아니다.

색각 상태와 무관하게 선택·실패·위험을 텍스트·아이콘·선 모양으로 중복 표기한다. 150% 글자, 키보드 포커스, 클릭-클릭 카드 배치, 무음, 동작 줄이기(prefers-reduced-motion), 색 대비를 점검한다. 모달 포커스 트랩과 `Esc` 닫기를 구현한다. 스크린리더 완전 지원은 구현·테스트하지 않았다면 지원한다고 표시하지 않는다.

## 14. 빌드와 실행 문서

`npm run build`는 Vite production 빌드로 `web/dist/`를 만든다. 서버 로직 없이 정적 호스팅으로 실행 가능해야 하며, 하위 경로 배포 시 `base` 설정을 기록한다. 개발 빌드와 릴리스 빌드를 명시적으로 구분하고, 릴리스 빌드에는 테스트 도구가 포함되지 않는다.

```bash
cd web
npm run build
npm run preview          # http://localhost:4173 에서 dist 확인
```

빌드 산출물을 지우는 스크립트는 `web/dist/` 등 명시적 대상만 다룬다. `rm -rf`를 넓은 사용자 경로에 사용하지 않는다. 스토어 배포·코드서명은 별도 권한·배포 단계이며 이번 로컬 검증과 혼동하지 않는다.

README에는 실제 Node·npm 버전, 처음 실행 방법(`npm ci`, `npm run dev`), 콘텐츠 검사·테스트·빌드 명령, 아트 동기화 검증, 저장 위치(localStorage 키), 테스트 팁 사용법, 릴리스 도구 제외, 알려진 이슈를 적는다. “코드를 열어 알아서 실행”으로 끝내지 않는다.

### 14.1 최종 제출물
- `web/` 소스 전체(node_modules 제외)와 잠긴 lockfile, 정적 `dist/` 빌드.
- 엔진 독립 정본 콘텐츠 JSON과 asset ID→URL 카탈로그.
- 재현 가능한 동기화·검증·테스트·빌드 명령.
- `reports/QA_REPORT.md`: 환경, 실행 명령, 테스트 결과, 스크린샷, 실제 완주 경로, 저장 검증, 알려진 제한.
- `docs/WEB_IMPLEMENTATION_STATUS.md`: 구현 범위·완료 상태·누락 0 여부와 근거.
- 폰트·패키지·외부 사용 자원의 권한을 확인한 범위만 적은 `THIRD_PARTY_NOTICES.md`.

### 14.2 최종 완료 기준
43개 서사 씬, 36개 증거(선택 획득 포함), 20개 퍼즐, 54개 아트가 정의와 연결돼 있다. 전체 필수 경로와 선택 분기가 실제 브라우저 입력으로 실행된다. 모든 진행 화면의 팁과 모든 퍼즐의 H1/H2/H3 및 테스트 정답 행동 실행이 동작한다. 저장·복원·오답 재시도·조사 순서 변경이 막히지 않는다. 런타임 AI·서버가 없고 오프라인 완주 가능하다. Unity 없이 production 빌드와 전체 테스트가 통과한다.

환경상 실행하지 못한 항목은 구현 완료와 별도로 미검증을 적는다. 사용자가 전달받는 결과에서 완료와 미실행을 숨기지 않는다. 코드가 있다는 이유로 “프로덕션 준비 완료”라고 말하지 않는다.

## 15. 고정 ID 집합 — 추출 검증용

이 블록은 마스터와 같은 데이터에서 작성된 대조용이다. 새로운 콘텐츠의 권위가 아니다. 마스터 변경 시 세 문서를 같은 버전으로 갱신한다. 생성할 `frozen_ids.json`에 그대로 옮겨 자동 검증 기준으로 사용할 수 있다.

```json
{
  "documentVersion": "1.0.0",
  "episodeId": "ep01",
  "sceneIds": [
    "s000",
    "s010",
    "s020",
    "s100",
    "s110",
    "s120",
    "s130",
    "s140",
    "s150",
    "s160",
    "s170",
    "s200",
    "s210",
    "s220",
    "s230",
    "s240",
    "s300",
    "s310",
    "s320",
    "s330",
    "s340",
    "s400",
    "s410",
    "s411",
    "s420",
    "s430",
    "s440",
    "s450",
    "s460",
    "s500",
    "s510",
    "s520",
    "s530",
    "s540",
    "s550",
    "s600",
    "s610",
    "s620",
    "s630",
    "s640",
    "s650",
    "s700",
    "s710"
  ],
  "evidenceIds": [
    "ev01",
    "ev02",
    "ev03",
    "ev04",
    "ev05",
    "ev06",
    "ev07",
    "ev08",
    "ev09",
    "ev10",
    "ev11",
    "ev12",
    "ev13",
    "ev14",
    "ev15",
    "ev16",
    "ev17",
    "ev18",
    "ev19",
    "ev20",
    "ev21",
    "ev22",
    "ev23",
    "ev24",
    "ev25",
    "ev26",
    "ev27",
    "ev28",
    "ev29",
    "ev30",
    "ev31",
    "ev32",
    "ev33",
    "ev34",
    "ev35",
    "ev36"
  ],
  "puzzleIds": [
    "p01",
    "p02",
    "p03",
    "p04",
    "p05",
    "p06",
    "p07",
    "p08",
    "p09",
    "p10",
    "p11",
    "p12",
    "p13",
    "p14",
    "p15",
    "p16",
    "p17",
    "p18",
    "p19",
    "p20"
  ],
  "assetIds": [
    "bg_bureau_night",
    "bg_interview_room",
    "bg_neovia_meeting",
    "bg_hearing_room",
    "bg_crosswalk_rain",
    "bg_crosswalk_dawn",
    "bg_maintenance_bay",
    "bg_development_lab",
    "bg_operations_room",
    "bg_hearing_corridor",
    "chr_nari_neutral",
    "chr_nari_distressed",
    "chr_nari_remembering",
    "chr_han_composed",
    "chr_han_guarded",
    "chr_han_strained",
    "chr_han_admitting",
    "chr_min_defensive",
    "chr_min_angry",
    "chr_min_cornered",
    "chr_min_resigned",
    "chr_yun_firm",
    "chr_yun_uneasy",
    "chr_yun_resolved",
    "chr_park_analytical",
    "chr_park_defensive",
    "chr_park_shaken",
    "chr_park_regretful",
    "chr_lee_confident",
    "chr_lee_irritated",
    "chr_lee_shaken",
    "chr_seo_calm",
    "chr_seo_stern",
    "chr_seo_unsettled",
    "chr_seo_accepting",
    "chr_cho_neutral",
    "chr_cho_firm",
    "chr_mira_reserved",
    "chr_mira_grief",
    "ev_front_base",
    "ev_rear_empty",
    "ev_junction_wide",
    "ev_cabin_base",
    "ev_radar_module",
    "ev_sensor_mount",
    "ev_vehicle_side",
    "ev_victim_photo",
    "ev_phone_base",
    "ev_service_workbench",
    "cg_ride_before",
    "cg_emergency_before",
    "cg_scene_after",
    "cg_hearing_wide",
    "cg_epilogue_window"
  ]
}
```

## 16. 첫 작업과 마지막 보고의 형식

첫 작업은 문서·아트·저장소·환경의 실제 상태 확인이다. 사용자에게 다시 “어떤 게임을 만들까요”라고 질문하지 않는다. Node·브라우저 같은 필수 환경이 없는 경우만 정확히 보고한다. 선택적인 패키지 취향이나 폴더 이름 때문에 실행을 미루지 않는다.

마지막 보고에서는 생성/변경 파일, 실제 실행 방법, 전체 사건 완주 여부, 테스트 결과, 확인한 화면, 빌드 경로, 미검증·알려진 문제를 구분한다. 빈 템플릿·가짜 PASS·예시 경로·미완성 대사가 남아 있으면 해당 항목을 완료로 표시하지 않는다. 버전·해시·파일 인계를 포함해 다른 세션이 바로 이어받을 수 있게 한다.

## 기술 참고 자료

확인일: 2026-09-14. 다음은 웹 기술 사용법의 근거이며 이야기 속 차량·조사기관의 사실성 근거가 아니다. 문서의 숫자는 조회 시점 버전이다. 작업 시작 시 실제 환경에서 호환 버전을 확인하고 lockfile로 고정하며, 이 링크의 버전을 무조건 설치하지 않는다.

- [W1] React 문서: `https://react.dev/`
- [W2] TypeScript 핸드북: `https://www.typescriptlang.org/docs/handbook/intro.html`
- [W3] Vite 가이드: `https://vite.dev/guide/`
- [W4] Vitest 가이드: `https://vitest.dev/guide/`
- [W5] Testing Library React: `https://testing-library.com/docs/react-testing-library/intro/`
- [W6] Playwright 소개: `https://playwright.dev/docs/intro`
- [W7] MDN localStorage: `https://developer.mozilla.org/ko/docs/Web/API/Window/localStorage`
- [W8] MDN Web Storage API: `https://developer.mozilla.org/ko/docs/Web/API/Web_Storage_API`
- [W9] React 오류 경계: `https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary`
- [W10] WAI-ARIA Authoring Practices: `https://www.w3.org/WAI/ARIA/apg/`
- [W11] 웹 콘텐츠 접근성 지침(WCAG) 2.2: `https://www.w3.org/TR/WCAG22/`
- [W12] MDN Web Crypto API: `https://developer.mozilla.org/ko/docs/Web/API/Web_Crypto_API`
