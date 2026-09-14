# 개발 환경 조사 (G2)

조사 시각: 2026-09-14 (UTC+09:00). 사용자 비밀번호·라이선스 파일·토큰은 기록하지 않는다.

## 하드웨어·운영체제

| 항목 | 값 |
| --- | --- |
| OS | macOS 14.6.1 (Build 23G93) |
| 아키텍처 | x86_64 (Intel) |
| 메모리 | 32 GB |
| 디스크 여유 | 약 127 GB (조사 시점) |

## Unity

| 항목 | 값 |
| --- | --- |
| Unity Hub | /Applications/Unity Hub.app (설치됨) |
| 선택 Editor | `/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity` |
| Editor 버전 | 6000.3.22f1 (revision 1c726e1fb402) — Unity 6.3 LTS 계열 설치본 중 최신 패치 |
| `Game/ProjectSettings/ProjectVersion.txt` | `m_EditorVersion: 6000.3.22f1` 로 잠금 |
| 설치된 6.3 계열 | 6000.3.22f1 (6000.3.24f1은 Hub `--releases` 목록에만 존재, 미설치) |
| 기타 설치본 | 2019.x ~ 6000.5.8f1 다수 (본 프로젝트에서 사용하지 않음) |
| PlaybackEngines | MacStandaloneSupport (Mono) 확인 |
| 라이선스 | 기존 활성화로 batchmode 실행 성공 (exit 0). 라이선스 파일 경로·내용은 기록하지 않음 |
| 알려진 로그 | 일부 batch 실행에서 `[Licensing::Client] Error: HandshakeResponse` 메시지가 남지만 프로젝트 생성·컴파일·테스트·빌드는 모두 정상 종료 |

## 패키지 (Packages/manifest.json · packages-lock.json 잠금)

| 패키지 | 버전 | 비고 |
| --- | --- | --- |
| com.unity.ugui | 2.0.0 | 에디터 내장(BuiltInPackages). TextMeshPro 포함 |
| com.unity.textmeshpro | 5.0.0 | 에디터 내장. ugui 2.0.0에 통합되어 있어 매니페스트에서 제거 예정(아래 참조) |
| com.unity.inputsystem | 1.20.0 | 레지스트리 안정 버전 (Unity 6000.0+ 호환) |
| com.unity.test-framework | 1.6.0 | 에디터 내장 |

- `Packages/packages-lock.json`에 실제 해석 결과가 잠겨 있다.
- TMP는 `com.unity.ugui` 2.0.0에 포함되어 별도 textmeshpro 패키지는 사용하지 않는다(에디터가 deprecated 경고를 낸다). 최종 매니페스트에서 제거했다.

## 빌드 도구

| 항목 | 값 |
| --- | --- |
| Xcode | 16.2 (Build 16C5032a) |
| clang | Apple clang 16.0.0 |
| Git | /usr/local/bin/git, 사용자 구성 존재 |
| Python (검증 도구) | /Users/switch/miniconda3/bin/python3 (3.9.19) |
| PIL (아트 검증) | 프로젝트 로컬 `.venv-art` (Python 3.13, Pillow 12.3.0) |
| C# 편집기 | 미설정(unknown). 컴파일·테스트는 Unity batchmode로 수행 |

## 폰트

- 사용 폰트: NanumGothic (Regular/Bold), SIL Open Font License 1.1 — `Game/Assets/_Game/Resources/Fonts/NanumGothic-*.ttf`, 라이선스 원문 `NanumGothic-OFL.txt`.
- 배포 가능한 OFL 폰트를 사용하므로 시스템 폰트 배포 권한 문제는 없다.
- `FONT_SOURCE_REQUIRED` 아님. 글리프 누락 리포트는 `reports/glyph_report.json`(Editor 도구 생성) 참조.

## 아트 인계

- `art_delivery/runtime/` 54개 PNG, `handoff/art/ASSET_READY.json` 기준 G1 통과.
- preflight 결과: `reports/preflight_art.json`, `reports/preflight_art.md` (PASS).

## 미검증/제한

- Windows 빌드 모듈 미설치 → Windows 빌드는 NOT_RUN.
- batchmode에서는 그래픽 장치/셰이더 초기화가 제한되어 TMP 폰트 에셋을 에디터에서 굽지 못하고 런타임 동적 생성으로 대체한다. 실제 창 실행에서 확인한다.
- 스크린리더 지원은 구현·검증하지 않았다.
