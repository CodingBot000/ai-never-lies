# 《AI는 거짓말을 하지 않는다》 코드 생성·개발 지시문
## 이미지 완료 후 실행 · 최초 환경 설정부터 Episode 01 전체 빌드까지

문서 ID: `ANL-CODE`; 버전: `1.0.0`; 작성 기준일: `2026-09-14`.
콘텐츠: `ep01.v1.0.0`; 아트 계약: `art.v1`.
필수 동반 문서: `00_MASTER_PLAN.md`, `01_IMAGE_GENERATION_INSTRUCTIONS.md`.

> 이 세션의 시작 조건은 이미지 작업 완료다. **게임 개발은 아직 전혀 되어 있지 않다.** 존재하지 않는 프로젝트 구조를 전제로 하지 않는다. 단, 중간 작업을 이어받는 경우 실제 폴더를 확인하고 이미 있는 소스를 함부로 초기화하지 않는다.
> 최종 결과는 시작 화면부터 첫 사건 기록까지 실제 플레이 가능한 게임, 로컬 저장, 모든 팁·테스트 지원, 검증 리포트, 실행 가능한 호스트 빌드다. 새 개발계획만 반복 작성하고 구현을 끝내지 말 것.

## 0. 코드 세션에 그대로 전달할 실행 지시

```text
세 문서를 읽고 버전·해시·이미지 인계를 검증하라.
ASSET_READY가 유효하고 모든 필수 이미지가 실제로 존재할 때만 개발을 시작하라.
현재 프로젝트는 없으므로 Unity 설치 상태 확인부터 최초 프로젝트와 저장소를 설정하라.
Unity 6.3 LTS/C#/2D/uGUI를 기준으로 하되 정확한 패치와 패키지를 실제 환경에서 잠가라.
마스터의 43개 서사 씬, 36개 증거, 20개 퍼즐, 분기·대사·정답을 누락 없이 구현하라.
개발 도구가 Codex일 필요는 없다. 특정 코딩 에이전트의 독점 기능에 의존하지 마라.
게임 실행 중 LLM, 생성형 이미지, 외부 AI API, 인터넷 조회를 호출하지 마라.
이미지 생성/재디자인/파일명 변경은 이 세션의 업무가 아니다.
게임 전체 화면을 생성 이미지 한 장으로 붙여서 버튼만 올리는 데모를 만들지 마라.
과거 대화가 없어도 세 문서와 인계 파일만으로 작업을 완료하라.
정답 힌트와 테스트 자동해결은 정상 명령 처리 경로를 사용하라.
테스트 미실행을 PASS로, 소스 작성만을 빌드 성공으로 보고하지 마라.
환경 인증이나 라이선스 권한을 우회하지 말고 필요한 상태와 실행 불가 항목을 기록하라.
작업 도중 기능을 줄여야 할 것 같으면 먼저 장애를 해소하라. 첫 사건 전체 범위를 임의로 잘라내지 마라.
```

## 1. 절대 계약과 기술 선택

### 1.1 기능 범위
정확한 사건·대사·분기·정답은 마스터에 있다. 이 문서는 구현을 위한 것이다. 사용자 요청의 “프로덕션급 디테일”을 복잡한 인프라나 많은 의존성으로 대체하지 않는다. 중요한 것은 재현 가능한 진행, 올바른 증거 연결, 막힘 없는 UI, 저장 복원, 이미지 매핑, 실제 검증이다.

엔진은 Unity 6.3 LTS 계열로 기준화한다. 공식 지원 안내에서 6.3 LTS가 확인되며, 프로젝트 시작 시 호환 패치 버전을 확인해 `ProjectVersion.txt`에 잠근다. “최신”이라는 부동 조건으로 작업 중 자동 업그레이드하지 않는다. 이 버전 선택은 본 프로젝트의 고정 기준이고 Unity가 모든 신규 프로젝트에 LTS만 권장한다는 뜻이 아니다. [U1]

| 구역 | 선택 | 제외 |
| --- | --- | --- |
| 렌더링 | 2D 정적 일러스트 + uGUI | 3D 모델, 물리 차량, 포스트프로세스 필수화 |
| 텍스트 | TextMeshProUGUI, 한국어 글리프 검증 | 문서 글자를 이미지에 굽기 |
| 입력 | Input System의 UI 입력 | 구/신 입력 모듈 중복 |
| 콘텐츠 | 로컬 JSON → 검증된 DTO → 읽기 전용 런타임 데이터 | 원격 DB, 즉석 LLM 대사 |
| 상태 | 순수 C# 명령·Reducer·판정기 | UI 컴포넌트마다 독립 진행 플래그 |
| 저장 | persistentDataPath 아래 JSON 스냅샷·백업 | PlayerPrefs에 전체 게임 상태 저장 |
| 아트 | 잠긴 PNG의 ID 기반 SpriteCatalog | 파일명 추측, 런타임 외부 다운로드 |
| 테스트 | EditMode + PlayMode + 실제 화면/완주 | 정답 점프만으로 QA 대체 |
| 빌드 | 현재 호스트 데스크톱 우선, Windows/macOS 구성 | 이번 단계에서 모바일·콘솔·웹 동시 보장 |

uGUI를 사용하는 것은 이 게임의 2D 표정·패널 전환·프리팹 조립에 대한 선택이다. Unity는 uGUI와 UI Toolkit을 모두 제공하고 용도별 차이가 있다. 임의로 두 런타임 UI 시스템을 혼합하지 않는다. [U2] TextMesh Pro 문서는 uGUI 문서에서 확인하며 별도 preview 패키지를 무조건 추가하지 않는다. [U10]

### 1.2 지켜야 할 비기능 원칙
완주에 네트워크가 필요 없어야 한다. 시작 직후 서버 로그인·모델 키·광고 SDK 설정을 요구하지 않는다. 실제 개인정보를 로그에 수집하지 않는다. 매 프레임 전체 UI와 JSON을 다시 만들지 않는다. 에러는 사용자가 다음 행동을 알 수 있는 메시지와 개발자용 원인 기록으로 나눈다. 빌드 성공·실행 성공·완주 성공을 서로 다른 항목으로 보고한다.

## 2. G1 이미지 인계 검증 — 프로젝트 생성보다 먼저

다음 순서로 수행한다. 이 과정은 읽기/검사 도구 작업이며 아직 게임 코드 개발 단계가 아니다.

1. 세 MD와 `handoff/art/ASSET_READY.json`의 존재·버전을 확인한다.
2. READY의 세 문서 SHA와 실제 MD 바이트 해시를 비교한다. 개행을 임의로 바꾼 뒤 검증하지 않는다.
3. `ready=true`, `contractVersion=art.v1`, `episodeId=ep01`, required/approved count=54인지 확인한다.
4. `manifest.lock.json` 실제 해시와 READY의 manifestSha256을 비교한다.
5. plan·lock·마스터의 ID 집합과 경로가 같은지 확인한다. 개수만 맞으면 통과시키지 않는다.
6. 각 실제 PNG의 해시·파일 바이트·해상도·알파·필수 regionId·시각 검수 기록을 검사한다.
7. 동일 해시로 서로 다른 표정을 채운 파일, 깨진 PNG, 대소문자만 다른 파일, 외부 symlink·상위 디렉터리 경로를 거부한다.
8. 검사 결과를 `reports/preflight_art.json`과 `.md`에 기록한다.

누락된 자산은 붉은 물음표 이미지로 대체하지 않는다. 개발 시작 전 `BLOCKED_ART_CONTRACT`와 ID·기대 경로·실측 상태를 보고한다. 이미 실행한 코드 단계에서 결함이 발견되면 원본 PNG를 임의 수정하지 말고 아트 재인계를 요청한다. 기술 검사와 외형 검수는 구분한다. 전 파일 존재만으로 “시각 검수 완료”라고 쓰지 않는다.

### 2.1 코드가 참고할 실제 파일 위치
```text
SOURCE: <project-root>/art_delivery/runtime/<manifest.path>
IMPORT: <project-root>/Game/Assets/_Game/Art/<manifest.path>
```
인계 manifest의 path는 `ep01/backgrounds/...png`처럼 상대경로다. Unity의 `Assets` 경로가 아니다. 런타임에서 운영체제 파일 경로를 조합해 Sprite를 읽는 방식이 아니라, **Editor 임포트 시 ID→Sprite 참조 카탈로그를 만든다**. 소스 파일 바이트를 바꾸지 않고 복사하며 실제 `.meta` 파일은 Unity가 생성한다.

## 3. 최초 개발환경 설정

### 3.1 환경 조사와 보고
먼저 `reports/environment.md`를 작성한다. 실제 OS, CPU 아키텍처, Unity Hub·Editor 설치 경로/버전, 대상 빌드 모듈, Git, 선택한 C# 편집기·디버거, Python(검증 도구용) 가용 여부를 기록한다. 확인하지 못한 항목은 unknown/NOT_RUN이다. 사용자 계정·비밀번호·라이선스 파일·토큰을 보고서에 넣지 않는다.

macOS ARM 환경이면 해당 아키텍처에 맞는 Unity를 선택하고, Windows 타깃을 검증하지 않았는데 검증 완료라고 표시하지 않는다. 설치·활성화·빌드 모듈 다운로드는 사용자가 허용한 환경에서 수행한다. Unity 계정 인증이 필요하면 우회하지 않는다. 설치되어 있지 않은 Editor를 PATH에 있다고 가정하지 않는다.

`UNITY_EDITOR`는 실제 실행파일 경로를 찾아 설정하고 `PROJECT_ROOT`는 세 문서가 있는 폴더의 절대 경로다. 아래 명령의 변수값을 가짜 예시 경로로 남긴 채 실행 성공이라고 보고하지 않는다. 명령행 옵션의 실제 동작은 해당 버전 문서를 참고한다. [U3]

```bash
# G1 검사 성공 후에만 수행한다.
# UNITY_EDITOR는 설치된 6000.3.x 편집기의 실제 실행파일을 가리킨다.
export PROJECT_ROOT="$(pwd)"
: "${UNITY_EDITOR:?실제 Unity Editor 경로를 먼저 설정해야 합니다}"
mkdir -p "$PROJECT_ROOT/reports" "$PROJECT_ROOT/Builds"

# Game 폴더가 없는 새 작업에서만 사용한다.
# 이미 Game/ProjectSettings/ProjectVersion.txt가 있으면 기존 프로젝트를 읽고 이어간다.
"$UNITY_EDITOR" -batchmode -quit   -createProject "$PROJECT_ROOT/Game"   -logFile "$PROJECT_ROOT/reports/project-create.log"
```

GUI의 2D 템플릿으로 시작해도 되지만 둘을 동시에 수행해 덮어쓰지 않는다. 빈 프로젝트로 시작한 경우 Editor 설정 도구에서 2D 모드·Orthographic 카메라·UI 중심 씬을 구성한다. 렌더 파이프라인 전환을 목표로 삼지 않는다. 새 프로젝트 생성 로그와 `ProjectVersion.txt`를 확인한다.

### 3.2 패키지와 프로젝트 설정
UPM에서 현재 편집기와 호환되는 안정 버전의 uGUI, Input System, Test Framework를 확인한다. 설치 버전을 `Packages/manifest.json`·`packages-lock.json`으로 잠그고 environment 보고서에도 기록한다. 이 문서의 기술 링크에 붙은 패키지 버전을 강제 설치하지 않는다. beta·preview를 기능 구현 편의로 추가하지 않는다.

UI 입력은 씬에 **EventSystem 한 개 + InputSystemUIInputModule 한 개**로 구성한다. 중복 StandaloneInputModule을 남기지 않는다. UI 액션 맵의 Point/Click/Scroll/Move/Submit/Cancel을 바인딩하고 Dialogue Advance·Hints·Evidence·Menu는 별도 액션으로 둔다. uGUI와 Input System 연결은 공식 UI 지원 문서를 기준으로 한다. [U5]

Player Settings의 회사명·제품명·Bundle Identifier는 초기부터 고정된 프로젝트 식별자로 정하고 기록한다. 저장 경로와 연관되므로 중간에 바꾸지 않는다. 화면 기본 1920×1080, 창 모드 허용, 타깃 60fps, 백그라운드 실행은 기본 OFF. 강제 종료 직전 비동기 저장이 끝날 것이라고 가정하지 않고 평소 체크포인트를 사용한다.

### 3.3 Git과 파일 관리
```text
커밋: Assets/, Packages/, ProjectSettings/, *.meta,
       세 MD, 잠긴 이미지 인계 메타데이터, 검증 스크립트, 테스트
미커밋: Library/, Temp/, Obj/, Logs/, UserSettings/, Build 산출물,
       로컬 환경 변수, 계정·라이선스·비밀키, 자동 생성 캐시
```

PNG 관리에 Git LFS를 사용할 경우 실제 설치 여부를 확인하고 설정을 커밋한다. LFS 포인터만 있고 실제 이미지 바이트가 없는 checkout은 G1에서 실패시킨다. 사용자 요청 없이 원격 저장소 생성·공개 업로드·push를 하지 않는다. 아트 원본과 후보를 런타임 Assets에 넣지 않는다.

### 3.4 폰트
권한이 확인된 한국어 글꼴을 프로젝트에서 사용할 수 있는 경로로 준비한다. 시스템 폰트의 배포 권한을 추정하지 않는다. 이 문서 패키지에는 폰트 바이너리가 없다. 폰트가 준비되지 않았으면 `FONT_SOURCE_REQUIRED`로 기록하고 누락 글리프를 해결하기 전 완료 처리하지 않는다.

정본 대사·증거·UI 문자열에서 실제 문자 집합을 추출해 TextMeshPro 폰트 에셋을 만든다. 한글 완성형, 숫자, 문장부호, ℃·²·±·→ 등 사용 기호를 확인한다. 전체 유니코드를 무조건 단일 거대 아틀라스에 굽지 않는다. 정적 기본 집합+필요한 보완 방식으로 설정하고 glyph 누락 리포트를 만든다. 임의 폰트 변경으로 문자가 나오더라도 줄바꿈과 UI 치수를 다시 검사한다. [U10]

## 4. 권장 프로젝트 구조와 책임

```text
Game/
  Assets/
    _Game/
      Scenes/Main.unity
      Art/ep01/...                       # G1 승인 PNG 복사본과 .meta
      Content/ep01/
        episode.json
        scenes.json
        dialogues.ko.json
        evidence.json
        testimonies.json
        puzzles.json
        hints.ko.json
        branches.json
        graph.json
        fixtures.json
      Content/common/ui.ko.json
      Catalogs/AssetCatalog.asset
      Fonts/
      Prefabs/
        AppRoot.prefab
        RootCanvas.prefab
        Screens/
        Components/
      Scripts/
        Core/Contracts/
        Core/State/
        Core/Commands/
        Core/Conditions/
        Core/Puzzles/
        Core/Hints/
        Core/Validation/
        Runtime/Bootstrap/
        Runtime/Content/
        Runtime/Assets/
        Runtime/Persistence/
        Runtime/Presentation/
        Runtime/Diagnostics/
        UI/Screens/
        UI/Components/
        UI/Input/
      Editor/
        ProjectBootstrap.cs
        ArtImporter.cs
        CatalogBuilder.cs
        ContentValidator.cs
        BuildEntry.cs
      Tests/EditMode/
      Tests/PlayMode/
  Packages/
  ProjectSettings/
tools/
  validate_art.py
  validate_content.py
  check_story_routes.py
reports/
  environment.md
  preflight_art.json
  content_validation.json
  route_validation.json
  test-editmode.xml
  test-playmode.xml
  screenshots/
  QA_REPORT.md
  IMPLEMENTATION_STATUS.md
Builds/
README.md
THIRD_PARTY_NOTICES.md
```

파일 이름과 모듈 분리는 구현 편의로 정교화할 수 있지만 콘텐츠/상태/표현/저장은 분리한다. 복잡한 DI 프레임워크·범용 비주얼 스크립팅 엔진·전체 ECS를 도입하지 않는다. 앱 부트스트랩에서 명시적으로 의존성을 연결하면 충분하다. 한 `.unity` 씬의 UI host에서 데이터 기반 서사 씬을 바꾼다.

### 4.1 어셈블리 경계
- `ANL.Core`: UnityEngine 의존 없는 DTO·상태·명령·조건·퍼즐 판정·힌트 해석·검증. 순수 C# 테스트 가능.
- `ANL.Runtime`: Unity 연결, 콘텐츠 로딩, 카탈로그, 저장 어댑터, 프레젠터.
- `ANL.UI`: UI 컴포넌트와 입력. 명령만 보냄. 핵심 진행 상태를 직접 수정하지 않음.
- `ANL.Editor`: Editor 전용 부트스트랩·임포트·빌드. 플레이어 빌드에 포함하지 않음.
- 테스트 어셈블리: EditMode/PlayMode 각각. 개발 도구는 define과 폴더/asmdef로 배포 경계 확인.

### 4.2 핵심 클래스와 역할
| 구성 | 책임 | 금지 |
| --- | --- | --- |
| ContentRepository | JSON 로드·버전·참조 검사, 불변 정의 제공 | UI에서 파일 직접 읽기 |
| GameStore | 유일한 진행 상태와 변경 이벤트 | 여러 static progress singleton |
| CommandDispatcher | 문맥·진입 조건·중복 명령 검증 | 입력 이벤트에서 플래그 직접 대입 |
| StoryReducer | 결정적인 상태 변경, 도메인 이벤트 생성 | 파일 IO·Unity API 호출 |
| ConditionEvaluator | 허용 조건 enum 판정 | 문자열 eval·알 수 없는 조건 true |
| PuzzleEvaluatorRegistry | 타입별 정답·부분 정답·오답 판정 | 텍스트 키워드 대충 비교 |
| HintService | 현재 장면/퍼즐의 잠금된 힌트 반환 | 미래 정답을 전체 검색해 노출 |
| TestAssistExecutor | 정상 사용자 명령 시퀀스 실행 | solved 플래그 직접 세팅 |
| SaveService | 원자적 스냅샷·백업·버전 복원 | PlayerPrefs에 전체 JSON 저장 |
| ScenePresenter | 상태→화면·대사·아트 ID 매핑 | 정답 판정을 UI 안에 작성 |
| AssetProvider | 검증된 ID→Sprite 참조 제공 | file prefix로 추측 검색 |
| ModalStack | 포커스·입력·닫기 순서 | 중첩 패널 뒤 클릭 전달 |
| Diagnostics | 상태 이벤트·오류 원인 기록 | 사용자 개인 파일 자동 수집 |

## 5. 콘텐츠 데이터 계약

### 5.1 JSON과 DTO
모든 정본 콘텐츠는 로컬 파일이다. JSON은 `{ "schemaVersion": 1, "items": [...] }` 같은 최상위 객체로 감싼다. Unity JsonUtility는 정의된 구조의 직렬화에 맞는 도구이므로, 저장/콘텐츠 DTO를 public field·구체 타입·배열/리스트로 구성한다. 임의 Dictionary·다형성 object 그래프·Unity Object 참조를 저장 JSON에 넣지 않는다. 런타임 조회용 Dictionary는 로드 후 생성한다. [U9]

JSON을 읽었다는 것만으로 검증 완료가 아니다. required 필드, 공백 ID, 중복, enum 범위, 참조, 정답의 증거 접근성, 누락 대사를 ContentValidator로 확인한다. 데이터 파일 이름이 바뀌어도 내부 영구 ID는 유지한다. 별도 외부 JSON 라이브러리를 도입할 경우 필요성과 라이선스·패키지 버전을 기록하고 자동 타입 복원 기능을 사용하지 않는다.

| 정의 | 필수 필드 |
| --- | --- |
| EpisodeDefinition | id, contentVersion, startSceneId, scenes, requiredPuzzleIds |
| SceneDefinition | id, act, screenType, backgroundAssetId, entryConditions, preDialogueIds, interactionIds, postDialogueIds, transitions |
| DialogueLine | id, speakerId, textId, portraitAssetId 또는 null, phase, optionalCondition |
| EvidenceDefinition | id, titleTextId, sourceTextId, bodyTextId, provesTextId, limitsTextId, imageAssetId 또는 null, kind, sourceMetadata |
| TestimonyStatement | id, sceneId, speakerId, textId, pressTextId |
| PuzzleDefinition | id, sceneId, type, requiredEvidenceIds, targetStatementId, solution, partialResponses, wrongResponses, hintIds, onSuccessEffects |
| BranchDefinition | id, sceneId, condition, textIds, effects, nextSceneId 또는 resumeCursor |
| HintDefinition | id, scopeType, scopeId, level, textId, requiredConditions, focusTarget |
| GraphDefinition | nodes, typedEdges, andNodeIds, requiredEvidenceByEdge |
| UIString | id, locale, text |

`bodyTextId`가 제목만 가리키면 안 된다. 마스터 증거 본문의 숫자·시점·원인/한계가 모두 들어가야 한다. 영문 기술필드는 원본 보기 탭에 남길 수 있지만 기본 보기에는 한국어 설명을 제공한다.

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
마스터 answer 객체는 의미 정본이다. 직렬화 형식은 다음과 같이 정규화한다. 자유로운 object를 JsonUtility에 바로 넣지 않는다.

```csharp
[Serializable]
public sealed class EvidenceSetDto
{
    public string[] requiredIds;
}

[Serializable]
public sealed class StringPairDto
{
    public string key;
    public string value;
}

[Serializable]
public sealed class EdgeDto
{
    public string from;
    public string to;
    public string kind; // causal 또는 signal
}

[Serializable]
public sealed class NumericAnswerDto
{
    public string key;
    public double value;
    public double tolerance;
}

[Serializable]
public sealed class EdgeProofDto
{
    public string from;
    public string to;
    public string kind;
    public EvidenceSetDto[] sufficientEvidenceSets;
}

[Serializable]
public sealed class ReportProofDto
{
    public string section;
    public string item;
    public EvidenceSetDto[] sufficientEvidenceSets;
}

[Serializable]
public sealed class SolutionDto
{
    public string[] correctChoiceIds;
    public string[] orderedIds;
    public EvidenceSetDto[] sufficientEvidenceSets;
    public StringPairDto[] valuePairs;
    public EdgeDto[] requiredEdges;
    public EdgeProofDto[] edgeProofRequirements;
    public ReportProofDto[] reportProofRequirements;
    public string[] andNodeIds;
    public NumericAnswerDto[] numericAnswers;
    public string[] requiredCaseIds;
}
```

`evidenceSets`는 최소 충분 근거 집합이다. 정답 집합이 선택에 포함되어 있고 선택 자료가 모두 보유 자료라면 성공한다. 추가 자료 때문에 정답을 틀렸다고 하지는 않되 UI에서 핵심으로 사용된 자료만 강조한다. 최대 3개 제출로 무차별 전체 선택을 막는다. p19는 세 자료가 모두 필요하다. 타깃 증언 ID가 다르면 자료가 맞아도 성공하지 않는다. 날짜·수치·한국어 원문 문자열을 부분 일치 검색해 판정하지 않는다.

### 5.4 진행 상태 DTO
```csharp
[Serializable]
public sealed class ProgressSnapshot
{
    public int saveSchemaVersion;
    public string contentVersion;
    public string artManifestSha256;
    public string slotId;
    public string currentSceneId;
    public string phase;
    public int dialogueCursor;
    public string activePuzzleId;
    public string[] ownedEvidenceIds;
    public string[] readEvidenceIds;
    public string[] visitedSceneIds;
    public string[] completedSceneIds;
    public string[] solvedPuzzleIds;
    public string[] visitedTopicIds;
    public StringPairDto[] branchValues;
    public PuzzleDraftDto[] puzzleDrafts;
    public HintUsageDto[] hintUsage;
    public string[] appliedEffectIds;
    public long lastCommandSequence;
    public string savedAtUtc;
}
```

`PuzzleDraftDto`에는 타입, 선택된 증언·증거, 카드 순서, 연결선, 비교 시점, 실험 실행 목록, 보고서 구역 배치를 저장한다. `HintUsageDto`에는 scopeId·최대 열람 수준·실행한 도움 종류·사용 횟수를 저장한다. 정의는 구체 타입으로 구현하며 `object draft`로 뭉개지 않는다. 장면 완료와 현재 화면은 별개이므로 허브를 재방문해도 자식 완료가 사라지지 않는다.

## 6. 진행 엔진

### 6.1 명령 타입
허용되는 대표 명령은 `StartEpisode`, `AdvanceDialogue`, `ChooseTopic`, `InspectRegion`, `OpenEvidence`, `ReadEvidence`, `SelectStatement`, `PressStatement`, `SelectEvidence`, `PresentEvidence`, `PlaceCard`, `ConnectNodes`, `RemoveEdge`, `SelectComparisonWindow`, `RunCounterfactualCase`, `SubmitPuzzle`, `ChooseBranch`, `ContinueScene`, `ReturnToHub`, `SaveSlot`, `LoadSlot`, `OpenHint`다.

모든 명령에 `commandId`, `sequence`, `sceneId`, `origin`(user/test-assist/replay)을 넣는다. `origin=test-assist`라도 정답 판정과 접근 조건은 완화하지 않는다. 반환은 `Accepted / Rejected / Partial / Solved`와 reasonCode·변경 이벤트다. UI 문구와 내부 reasonCode를 분리한다.

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

코루틴으로 페이드가 끝난 뒤 임의 플래그를 바꾸지 않는다. 상태 전환은 먼저 확정하고 UI는 이를 표현한다. 전환 중 명령 재진입을 막되 취소·에러 시 입력 잠금이 반드시 해제돼야 한다. 저장 실패는 진행 상태를 조용히 초기화하지 않고 저장 재시도 상태로 표시한다.

### 6.3 진입 조건과 완료 조건
조건은 enum 기반 원자 조건과 AND/OR 묶음으로 만든다. 지원: SceneCompleted, EvidenceOwned, EvidenceRead, PuzzleSolved, TopicVisited, BranchEquals, PostDialogueComplete, AllRequiredChildrenComplete. 알 수 없는 타입은 검증 오류이며 false로 판정한다. `!string.IsNullOrEmpty(condition)`이면 true 같은 편의 처리는 금지한다.

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

### 7.1 인과판
카드의 위치는 자유롭게 옮길 수 있지만 정답은 좌표가 아니라 노드/간선이다. `from→to` 방향을 분명히 표시한다. causal와 signal은 색뿐 아니라 라벨·선 모양으로 구분한다. AND 노드의 두 입력이 하나만 연결되면 부분 완성으로 표시한다. 중간 저장 시 카드 좌표와 연결선 초안을 보존하고, 불가능한 연결을 그리면 즉시 이유를 보여준다.

마스터 p17의 `proofRequirements` 8개를 간선별 `EdgeProofDto`로 정규화한다. 각 항목의 evidenceSets는 sufficientEvidenceSets로 매핑한다. causal/signal 종류도 비교하며 한 증거가 여러 간선을 뒷받침해도 소비하거나 중복 사용을 금지하지 않는다. 최종 완료는 연결 형태뿐 아니라 핵심 간선에 근거가 붙어 있어야 한다. 자동 배치 기능은 공간 정리만 하고 정답 간선을 몰래 만들지 않는다. 테스트 정답 행동 실행만 명시적으로 정답 간선을 구성한다.

### 7.2 반사실 비교
`historical`, `gate_restored`, `dispatch_blocked` 세 사례만 구현한다. 표와 타임라인은 정해진 자료에서 나온다. 물리 Update 루프로 차를 달리게 하지 않는다. 재생 속도가 달라도 수치가 달라지면 안 된다. 공식 계산을 순수 함수로 검산하고 결과 표시는 2자리 반올림한다. 실제 요청 -8.1, 실제 강한 감속 -6.2를 별도 라벨로 유지한다. 배차 차단은 충돌 시각 그래프가 아니라 “운행 없음” 패널이다.

### 7.3 보고서
마스터 14장의 고정 항목 팔레트를 사용하고 p20 proofRequirements를 `ReportProofDto(section,item,sufficientEvidenceSets)` 배열로 변환한다. 한계 항목에는 증거를 강제로 붙이지 않는다. p20 정답에 쓰인 자료는 퍼즐 requiredEvidenceIds에 모두 포함한다.

직접 원인/기여 요인/조직 관리/사고 후 정보 공개/판단 한계의 다섯 구역이다. 어느 인물이 악인인지 선택하는 버튼으로 대체하지 않는다. 행위·인지·권한·근거를 보여준다. 채택 전에는 수정 가능한 초안, 채택 후에는 읽기 전용 결과다. 다른 최종 대사 선택이 보고서 사실을 바꾸면 안 된다.

## 8. 화면 조립 상세

### 8.1 Main과 기본 프리팹
`Main.unity`에는 AppRoot, EventSystem, RootCanvas가 있다. RootCanvas는 배경층, 인물층, 화면 콘텐츠층, 대화층, 글로벌 HUD, 모달층, 개발 오버레이층으로 나눈다. 비활성 화면은 Raycast를 차단하지 않도록 관리한다. 같은 서비스를 가진 AppRoot를 중복 생성하지 않는다.

Editor `ProjectBootstrap`은 메뉴 항목과 batch entry를 제공해 동일한 씬·프리팹·설정을 재현 가능하게 만든다. 재실행 시 기존 수동 수정 프리팹을 무조건 지우지 않는다. 생성한 파일 목록·버전 표식을 남기고 필요한 경우 안전한 변경만 적용한다. YAML을 임의 GUID로 직접 써서 Editor에서 깨지는 프리팹을 만들지 않는다.

### 8.2 해상도와 타이포그래피
Canvas Scaler는 Scale With Screen Size, reference 1920×1080, match는 초기 0.5로 시작하고 실제 화면에서 조정한다. 이 설정은 해상도 대응 도구이지 읽기 쉬움의 보증이 아니므로 1280×720/1920×1080/2560×1440/16:10에서 검수한다. [U7]

글자 100/125/150%를 제공한다. 최소 버튼 클릭 영역은 표시 기준 44px 상당을 목표로 하고 목록 간 간격을 확보한다. 긴 한국어 문장은 스크롤과 적절한 줄바꿈으로 수용한다. 증거 뷰에 작은 JSON을 기본으로 가득 채우지 않는다. 요약 탭이 기본이고 원본은 별도 탭이다. 원본 코드나 로그를 편집/실행하는 기능은 없다.

대사 상자에는 현재 화자명·직책, 본문, 다음 표시가 있다. 인물 반신이 대사와 경쟁하지 않게 하며 눈·입을 가리지 않는다. UI 문자열과 본문에는 rich text를 기본 비활성화하거나 제한된 포맷만 허용한다. 외부 저장 파일의 문자열을 TextMeshPro 마크업으로 그대로 실행하지 않는다.

### 8.3 아트 임포트와 카탈로그
`ArtImporter`는 PNG를 `Assets/_Game/Art/<path>`에 복사한 후 textureType=Sprite, single sprite, sRGB, mipmap OFF, Read/Write OFF, Full Rect mesh, 예상 filter 설정으로 임포트한다. 포트레이트 pivot은 Unity 좌표 `(0.5,0.0)`인 하단 중심, 일반 증거·배경은 중심으로 설정한다. manifest anchor는 이미지 좌상단 원점이므로 `(x,y)`를 Unity pivot으로 변환할 때 `(x,1-y)`를 적용한다. `(0.5,1.0)`을 변환 없이 Sprite pivot에 복사하지 않는다. 원본 투명 여백을 자의적으로 tight trim해 표정이 점프하게 만들지 않는다.

`AssetCatalog.asset`에 id·Sprite·kind·anchor·regions·원본 해시를 담는다. 게임 JSON은 Sprite를 직접 참조하지 않고 assetId만 가진다. 에디터에서 카탈로그 참조 누락과 실제 임포트 크기를 검증한다. 작은 썸네일을 위해 새 생성 이미지를 요구하지 말고 같은 Sprite를 contain 표시하거나 Editor 파생 썸네일을 만든다.

이 사건 규모에서는 직접 Sprite 참조 카탈로그로 단순하게 시작한다. 이것은 자산 지연 로딩을 보장하지 않는다. 종속 텍스처가 함께 로드될 수 있으므로 실제 메모리를 측정한다. 예산을 넘으면 동일 `IAssetProvider` 경계 안에서 그룹 로딩을 개선할 수 있지만 파일명·스토리·아트 계약을 바꾸지 않는다. 측정 없이 Addressables·원격 CDN부터 도입하지 않는다.

### 8.4 단서 영역 좌표 변환
manifest region은 이미지 좌상단 기준 정규화다. UI에 contain으로 표시된 **실제 이미지 사각형**을 구한 뒤 변환한다. 전체 패널에는 레터박스가 있을 수 있다.

```text
displayLeft, displayTop, displayWidth, displayHeight = 실제 표시된 이미지 영역
regionLeft = displayLeft + normalizedX * displayWidth
regionTop  = displayTop  - normalizedY * displayHeight  # Unity local Y가 위로 증가할 때
regionWidth  = normalizedWidth  * displayWidth
regionHeight = normalizedHeight * displayHeight
```

좌표계를 명시하고 네 모서리·중앙·레터박스 단위 테스트를 작성한다. `RectTransformUtility` 등으로 포인터 좌표를 같은 로컬 공간에 변환한다. p01은 bg_crosswalk_dawn의 crosswalk/signal/vehicle 영역, ev05는 ev_cabin_base.warning_display, ev06은 ev_front_base.pedestrian을 사용한다. 경고문·인식 박스·값은 코드로 올린다.

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

예: p04는 SelectStatement(st150_02), SelectEvidence(ev06), SelectEvidence(ev07), PresentEvidence 순서다. p17은 카드를 슬롯에 배치하고 허용된 일곱 인과선과 한 신호선을 연결한 뒤 간선 근거를 연결하고 SubmitPuzzle을 호출한다. 한 줄로 `solvedPuzzleIds.Add`하거나 이후 씬으로 이동시키지 않는다.

### 9.3 개발 패널과 릴리스 경계
개발 빌드는 현재 scene/phase/puzzle, 보유·읽음·완료 집합, 직전 명령 결과, 현재 목표와 블로커를 표시할 수 있다. 불변 픽스처로 점프하는 기능은 `LoadScenarioFixture`로 구분해 테스트 오염 표시를 남긴다. 임의 점프 후 완주를 일반 세이브로 저장하지 않는다. `ANL_TEST_TOOLS`가 없는 릴리스에서는 패널·정답 행동 실행·픽스처 로더가 접근 불가이며 가능하면 코드도 제외된다.

## 10. 저장·복구·버전

### 10.1 파일 위치
저장은 `Application.persistentDataPath`를 사용하며 프로젝트 소스 폴더나 OS별 하드코딩 경로에 쓰지 않는다. 이 API가 제공하는 영속 경로는 플랫폼마다 다르다. 실제 해석된 경로를 개발 보고서에만 표시한다. [U6]

```text
<persistentDataPath>/saves/
  autosave.json
  autosave.bak
  slot_01.json
  slot_01.bak
  slot_02.json
  slot_03.json
<persistentDataPath>/settings.json
```

### 10.2 저장 형식과 쓰기
Envelope에 schemaVersion·contentVersion·payloadJson·payloadSha256를 둔다. payloadJson을 한 번 직렬화한 UTF-8 바이트의 해시를 계산하고, 해시 검증 뒤 DTO를 읽는다. 객체 키 순서가 달라져 해시가 우연히 바뀌지 않도록 저장한 문자열 자체를 검증한다. 체크섬은 손상 탐지이며 악의적 조작 방지/암호화가 아니다.

동일 폴더 임시 파일에 쓰기→flush→기존 저장을 백업→지원되는 원자 교체 순서로 처리한다. OS별 파일 교체 실패를 처리하고 원본과 백업을 모두 잃지 않게 한다. 저장 요청은 직렬 큐로 합치며 이전 스냅샷이 최신 스냅샷을 덮어쓰지 않게 sequence를 비교한다. 너무 잦은 증거 스크롤마다 저장하지 않는다.

### 10.3 복원
파일 크기 상한·JSON 파싱·체크섬·버전·ID·상태 불변 조건을 검증한다. 알 수 없는 미래 버전은 읽기 중단 및 원본 유지. 손상 시 백업 복원 제안. 백업도 없으면 새 게임 선택을 제공하되 사용자 동의 없이 삭제하지 않는다. 새 슬롯은 기존 슬롯 덮어쓰기 확인을 거친다.

중간 퍼즐에서 저장한 경우 선택된 증언·자료·간선·실험 실행목록과 phase를 복원한다. 타이핑 중 저장은 현재 줄을 완전히 보이는 상태로 복원하고 다음 줄로 건너뛰지 않는다. 애니메이션은 안전한 정지 상태에서 재개하고 진행 이벤트를 재실행하지 않는다. 인물 표정은 저장 상태와 대사 정의로 재계산하되 그림 파일 순서에 의존하지 않는다.

### 10.4 저장 시뮬레이션 실패 사례
디스크 쓰기 실패, 빈 파일, 잘린 JSON, 유효 JSON이지만 없는 ID, 성공 후 대사 도중 종료, 퍼즐 간선 편집 도중 종료, 같은 증거 재획득, s530 거절 후 로드, s700 응답 직후 종료를 반드시 시험한다. 실패해도 게임 상태를 몰래 초기화하지 않는다.

## 11. 구현 단계와 게이트

각 단계는 실제 코드·실행 결과와 함께 기록한다. 다음 단계로 넘어가기 전에 치명적 차단 오류를 해소한다. 작업 중 별도 계획 문서를 장황하게 다시 쓰는 대신 `IMPLEMENTATION_STATUS.md`에 완료/진행/차단과 근거 파일만 유지한다.

| 단계 | 구현 | 통과 기준 |
| --- | --- | --- |
| C0 | 아트·문서 preflight | 54개 ID/경로/해시/크기/검수 및 READY 일치 |
| C1 | Unity 프로젝트·패키지·폰트·Main·Bootstrap | 실제 Editor 열기·컴파일·빈 UI 실행, 환경 로그 |
| C2 | 아트 임포트·Catalog·공통 UI·Modal | 배경/인물/증거 이미지 실제 로딩, 한글·알파·좌표 검사 |
| C3 | DTO·ContentValidator·Store·Dispatcher·조건 | 가짜 UI 없이 순수 상태 테스트; 증거 중복·분기 조건 통과 |
| C4 | 대화·허브·조사·증거책·심문·저장 | s000~s170 실제 클릭 완주와 중간 저장 복원 |
| C5 | 비교·실행 경로·정비·운영 심문 | s200~s340, 다른 조사 순서와 부분 정답 검증 |
| C6 | diff·시험표·배포·보고 자료 | s400~s460, 누락 전제와 승인조건 순환 잠금 없음 |
| C7 | 반사실·선택 면담·최종 심리·인과판·보고 | s500~s710 전체 완주·동일 사실 엔딩 |
| C8 | 모든 팁·정답 행동 실행·개발 패널 | 43씬 팁, 20퍼즐 H1~H3, 정상 경로와 결과 동등 |
| C9 | 회귀·UI·성능·손상 저장·릴리스 경계 | 필수 테스트·스크린샷·실제 빌드·미실행 항목 명시 |

C4에서 전 구간 기능의 뼈대를 만들 수 있지만 거기서 “MVP 완료”라고 종료하지 않는다. G1을 이미 통과했으므로 어느 단계에서도 임시 아트가 필요하지 않다. 미구현 화면은 내부 개발 상태로 명시하며 최종 납품에서는 0개여야 한다.

## 12. 자동 테스트 설계

### 12.1 정적 콘텐츠 검증
`validate_content.py`와 Editor 검사기로 다음을 확인한다: 43씬·36증거·20퍼즐·54아트 ID, 참조 누락, 중복 문자열 ID, 빈 본문, 고정된 정답/힌트 누락, 존재하지 않는 이미지, p04의 증거 접근 가능성, s640의 순환 잠금, 불가능한 phase, 없는 분기 target, 원인/신호선 종류, 선택 증거를 필수로 요구하는 실수.

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
| SV05 | 부분 저장 | 잘린 파일→백업 제안, 자동 삭제 없음 |
| SV06 | 체크섬 오류 | 변조/손상 탐지 및 원본 보존 |
| SV07 | 미래 버전 | 호환불가 안내, 조용한 리셋 없음 |
| SV08 | 없는 ID | 마이그레이션/오류 안내, 엉뚱한 씬 대체 없음 |
| SV09 | 쓰기 실패 | 진행 유지·미저장 표시·재시도 |
| SV10 | 동시 저장 | 오래된 스냅샷이 최신 저장 덮지 않음 |
| UI01 | 해상도 | 720p/1080p/1440p/16:10 정보·버튼 잘림 없음 |
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
| BL02 | 릴리스 경계 | test assist·fixture load·debug panel 접근 불가 |
| BL03 | 오프라인 | 네트워크 차단 상태에서 시작~종료 완주 |
| BL04 | 재시작 | 배포 실행파일 종료 후 저장 이어하기 |
| BL05 | 아트 무결성 | 빌드 전 원본·임포트 ID·참조 검증 |
| BL06 | 실제 빌드 | 종료 코드·BuildReport·실행 캡처 모두 존재 |

### 12.3 경로 탐색 테스트
허브 상태를 논리적으로 압축한 탐색기로 가능한 조사 순서를 생성한다. 맹목적인 전체 클릭 공간 탐색이 아니라, 미완료 필수 조사 선택·해당 퍼즐 정답 행동·합류 상태를 검증하는 경로 집합을 만든다. 모든 허용 경로에서 최종 필수 증거와 퍼즐 집합이 같아야 한다. 선택 질문과 개인 자료 수신 여부는 별도 조합으로 검사한다.

필수 상태마다 Save→Load를 삽입한 경로를 실행한다. p17의 간선 하나만 배치한 상태, p04의 증거 하나만 고른 상태, p16의 실험 하나만 실행한 상태도 저장한다. 정상 완주와 정답 행동 실행 완주의 상태 차이는 도움 사용 이력과 명령 출처 등 허용 필드뿐이어야 한다.

### 12.4 Unity 명령행 테스트 예
테스트는 Editor의 실제 라이선스·패키지가 준비된 환경에서 실행한다. 아래 명령은 테스트 프레임워크의 `-runTests`, `-testPlatform`, `-testResults`를 사용한다. [U4] 테스트가 끝나기 전에 강제 종료되는 설정을 피하고 XML의 실행 개수가 0이면 PASS로 처리하지 않는다.

```bash
"$UNITY_EDITOR" -batchmode   -projectPath "$PROJECT_ROOT/Game"   -runTests -testPlatform EditMode   -testResults "$PROJECT_ROOT/reports/test-editmode.xml"   -logFile "$PROJECT_ROOT/reports/test-editmode.log"

"$UNITY_EDITOR" -batchmode   -projectPath "$PROJECT_ROOT/Game"   -runTests -testPlatform PlayMode   -testResults "$PROJECT_ROOT/reports/test-playmode.xml"   -logFile "$PROJECT_ROOT/reports/test-playmode.log"
```

화면 렌더 검증은 `-nographics`로 대체하지 않는다. GUI가 없는 환경에서 논리 테스트만 가능하면 화면·입력·시각 검수를 NOT_RUN으로 남긴다. 같은 프로젝트를 Editor GUI와 별도 batch 프로세스로 동시에 열어 파일 잠금 충돌을 만들지 않는다.

### 12.5 실제 화면 검수
다음 대표 화면을 실제 실행 중 저장한다: s000 타이틀, s120 대화/팁, s150 증거 제시, s160 타임라인, s220 동기화 비교, s310 부분 근거, s410 diff, s430 커버리지, s510 실험, s610 인과판 중간/완료, s640 보고서, s700 엔딩, 설정 150% 화면, 저장 손상 안내.

스크린샷 파일명은 예를 들어 `reports/screenshots/s150_evidence_1920x1080.png`. 빌드·콘텐츠 버전·해상도·재현 절차를 함께 기록한다. 개념 시안 이미지를 실행 스크린샷 대신 제출하지 않는다. 화면을 실제로 보지 않았으면 시각 PASS라고 쓰지 않는다.

## 13. 성능·안정성·접근성

텍스트와 정적 이미지 중심이므로 복잡한 최적화보다 불필요한 작업을 먼저 없앤다. 활성 화면 밖의 비싼 레이아웃 갱신, 매 프레임 텍스트 재할당, 반복 JSON 로드, 대화 기록 전체 재생성을 피한다. 로그·증거 목록은 풀링 또는 가시 범위 업데이트를 적용할 수 있다. 프로파일링 없이 프레임 드롭 원인을 추측하지 않는다.

데스크톱 기준 목표: 안정적 60fps, 일반 선택/증거 열기 후 100ms 이내의 즉각적 UI 반응, 한 화면을 닫고 열 때 꾸준한 메모리 증가 없음. 전체 플레이어 메모리는 초기 768MB 이하를 목표로 측정하되 OS·에디터가 아닌 **독립 실행 플레이어와 기기 정보**를 같이 적는다. 이 숫자는 이미 달성했다는 주장이 아니다. 초과 시 텍스처 임포트·참조 수명·UI 재생성을 먼저 분석한다.

색각 상태와 무관하게 선택·실패·위험을 텍스트·아이콘·선 모양으로 중복 표기한다. 150% 글자, 키보드, 클릭-클릭 카드 배치, 무음, 동작 줄이기를 점검한다. 스크린리더 완전 지원은 구현·테스트하지 않았다면 지원한다고 표시하지 않는다.

## 14. 빌드와 실행 문서

`BuildEntry`는 현재 호스트 대상 개발 빌드·릴리스 빌드를 명시적으로 구분한다. target·output path를 신뢰된 인자로 받고 경로가 프로젝트 외부의 임의 디렉터리를 덮어쓰지 않게 한다. BuildPipeline/BuildReport로 결과를 확인하고 실패하면 non-zero 종료 코드를 반환한다. [U8]

```bash
# 아래 메서드는 개발자가 구현할 프로젝트 전용 entry다.
"$UNITY_EDITOR" -batchmode -quit   -projectPath "$PROJECT_ROOT/Game"   -executeMethod ANL.Editor.BuildEntry.BuildHostDevelopment   -logFile "$PROJECT_ROOT/reports/build-host-development.log"
```

기존 빌드 산출물을 지우는 스크립트는 해당 Builds 하위의 명시적 대상만 다룬다. `rm -rf`를 넓은 사용자 경로에 사용하지 않는다. macOS 서명·공증·스토어 배포, Windows 코드서명 인증서 준비는 별도 권한·배포 단계이며 이번 로컬 플레이 검증과 혼동하지 않는다.

README에는 실제 Editor 버전, 필요한 모듈, 처음 여는 방법, Main 씬, 콘텐츠 검사·테스트·빌드 명령, 아트 인계 검증, 저장 위치 확인법, 테스트 팁 사용법, 릴리스 도구 제외, 알려진 이슈를 적는다. “코드를 열어 알아서 실행”으로 끝내지 않는다.

### 14.1 최종 제출물
- Unity 프로젝트 전체(캐시 제외)와 실제 호스트 빌드.
- 모든 콘텐츠 JSON 및 아트 ID→Sprite 카탈로그.
- 편집기/패키지 잠금과 재현 가능한 부트스트랩·검사·테스트·빌드 엔트리.
- `QA_REPORT.md`: 환경, 실행 명령, 결과 XML, 스크린샷, 실제 완주 경로, 저장 검증, 알려진 제한.
- `IMPLEMENTATION_STATUS.md`: 구현 범위·완료 상태·누락 0 여부와 근거.
- 폰트·패키지·외부 사용 자원의 권한을 확인한 범위만 적은 `THIRD_PARTY_NOTICES.md`.

### 14.2 최종 완료 기준
43개 서사 씬, 36개 증거(선택 획득 포함), 20개 퍼즐, 54개 아트가 정의와 연결돼 있다. 전체 필수 경로와 선택 분기가 실행된다. 모든 진행 화면의 팁과 모든 퍼즐의 H1/H2/H3 및 테스트 정답 행동 실행이 동작한다. 저장·복원·오답 재시도·조사 순서 변경이 막히지 않는다. 런타임 AI·서버가 없고 오프라인 완주 가능하다. 실제 테스트와 빌드 증빙이 존재한다.

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

첫 작업은 문서·아트·환경의 실제 상태 확인이다. 사용자에게 다시 “어떤 게임을 만들까요”라고 질문하지 않는다. 엔진의 설치·라이선스 같은 필수 환경이 없는 경우만 정확히 보고한다. 선택적인 패키지 취향이나 폴더 이름 때문에 실행을 미루지 않는다.

마지막 보고에서는 생성/변경 파일, 실제 실행 방법, 전체 사건 완주 여부, 테스트 결과, 확인한 화면, 빌드 경로, 미검증·알려진 문제를 구분한다. 빈 템플릿·가짜 PASS·예시 경로·미완성 대사가 남아 있으면 해당 항목을 완료로 표시하지 않는다. 버전·해시·파일 인계를 포함해 다른 세션이 바로 이어받을 수 있게 한다.

## 기술 참고 자료

확인일: 2026-09-14. 다음은 엔진 사용법의 근거이며 이야기 속 차량·조사기관의 사실성 근거가 아니다. 패키지 문서의 숫자는 문서를 조회한 버전이다. 설치 시 편집기와 호환되는 릴리스를 확인해 실제 버전을 잠그며, 이 링크의 버전을 무조건 설치하지 않는다.

- [U1] Unity 6 지원 정책: `https://unity.com/releases/unity-6/support`
- [U2] Unity 6.3 UI 시스템 비교: `https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html`
- [U3] Unity Editor 명령행: `https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html`
- [U4] Unity Test Framework 명령행: `https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html`
- [U5] Input System UI 지원: `https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/UISupport.html`
- [U6] persistentDataPath: `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html`
- [U7] Canvas Scaler: `https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html`
- [U8] 플레이어 빌드 자동화: `https://docs.unity3d.com/6000.3/Documentation/Manual/BuildPlayerPipeline.html`
- [U9] Unity JSON 직렬화: `https://docs.unity3d.com/6000.3/Documentation/Manual/json-serialization.html`
- [U10] uGUI의 TextMesh Pro 문서: `https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html`
