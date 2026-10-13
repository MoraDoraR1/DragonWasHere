# 다녀가 용! MVP 작업 인계 (Ver01)

- 작성: Claude, 2026-10-12 (작업하면서 계속 갱신한다. 맨 아래 "마지막 갱신"을 확인할 것)
- 용도: 사용량 한도 등으로 세션이 끊겼을 때, 다른 계정·세션의 Claude Code가 이 문서만 읽고 이어서 작업한다.

---

> 집 PC로 옮길 때는 `Docs/20261012_집PC작업인계_Ver01_Claude.md`를 먼저 본다 (미커밋 변경 압축 사용법).

## 1. 이어받는 Claude가 먼저 할 일

1. `README.md`(작업 규칙), `작업내용.md`, `메모.md`를 읽는다 (CLAUDE.md가 자동으로 불러온다).
2. 이 문서의 3장(현재 상태)과 4장(다음 할 일)을 확인한다.
3. 사용자에게 특이사항을 먼저 보고한다(README 규칙 1). 모든 답은 한국어.
4. **커밋은 사용자가 요청할 때만**. 기준 문서(`Docs/` md)를 고치면 바탕화면 원본(`Desktop/DragonWasHere_기획서및리소스/` 같은 이름 파일)에도 같은 내용을 반영한다.
5. 사용량 한도가 95%에 닿으면 작업을 멈추고 이 문서를 갱신한다(사용자 요청).

## 2. 기준 문서와 사용자 결정

| 순위 | 문서 |
|---|---|
| 1 | `Docs/20261012_데이터테이블_Ver02.xlsx` → `Docs/Data/*.csv` (모든 수치·ID·문구·리소스 경로) |
| 2 | `Docs/20261012_UnityMVP제작가이드_Ver01_Claude.md` (MVP 범위, 작업 순서 M0~M10, 10장 출시 체크리스트) |
| 3 | `Docs/20261012_MVP밸런스_Ver01_Claude.md` |
| 4 | `Docs/임시unity제작.md` (동작 세부만. 수치·경로는 옛 값) |

10-12 사용자 결정: ① Ver02 스키마로 데이터 계층 전면 교체 ② Fix_Resource 전체 복사 + 옛 아트 삭제 ③ 가이드를 Docs/로 복사해 1순위 기준 ④ 지표 수집은 M10에 포함.

일정: 개발 완료 10-14(수), itch.io 출시 10-15(목).

## 3. 현재 상태 (10-12)

- **M1~M9 완료** (상세는 `작업내용.md` 10-12 "MVP 전환"). 커밋은 아직 안 했다(마지막 커밋 `1b26602`).
- 테스트: EditMode 67개, PlayMode 17개 모두 통과(10-12, 피드백 반영 후).
- **M10 진행 중**
  - 지표: 바탕화면 `DWH_AnalyticsTest` 프로젝트의 수집 묶음(`Assets/DWHAnalytics/`, `Assets/WebGLTemplates/DragonFirebase/`)을 복사했고, `DWH.Runtime.asmdef`가 `DWH.Analytics`를 참조한다. 게임 쪽 `Assets/_Project/Scripts/Core/Analytics.cs`가 P0 8개(game_session_start, run_start, run_end, upgrade_purchase, outgame_exit, rebirth_unlocked, rebirth, game_complete)를 계획 페이지 명세 파라미터로 보낸다. firebase-config.js는 테스트용 프로젝트(temporary-project-just-for) 값이 들어 있다.
  - WebGL 빌드: 메뉴 `DWH > Analytics > Build Web (Team Test)` 또는 배치 `-executeMethod DWH.Analytics.Editor.DwhWebBuild.BuildTestBatch` → `Builds/Web_Test.zip`. **10-12 성공**, 브라우저에서 동작 확인. Emscripten clang이 가끔 0xC0000005로 죽는데 다시 돌리면 이어서 된다(캐시).
  - 밸런스 봇 시뮬레이션: `Tests/PlayMode/BalanceSimulationTests.cs`([Explicit]) 완료. 결과와 조정안은 `작업내용.md` 10-12 "봇 결과". 팀 피드백 뒤 조정안(B·C) 결정 대기. 실행: `-runTests -testPlatform PlayMode -testFilter DWH.Tests.BalanceSimulationTests` → `Logs/balance_sim.md`

## 4. 다음 할 일 (우선순위 순)

1. ~~WebGL 빌드~~ (완료)
2. 팀 피드백을 받아 밸런스 조정안 적용: 엑셀 수정 → CSV → BuildAll → 봇 재실행(`DWH_SIM_ATK_SCALE`/`DWH_SIM_LM_SCALE`로 미리 실험 가능).
3. 가이드 10장 출시 전 체크리스트를 하나씩 확인하고 `메모.md`에 결과 기록.
4. `메모.md` "MVP" 섹션의 기획 확인 항목을 사용자에게 질문(UI 문구 테이블화, 1-1 체력·보너스 피로, jump_rate 0, 데이터 추가 요청, [임시 해석]).
5. 사용자 요청 시 커밋. 메시지 형식 `[태그]YYYYMMDD_내용_Claude` (수정·추가·삭제가 섞이면 `[MOD]`, 본문에 나머지).

## 5. 작업 방법 메모

- Unity: `C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe` (WebGL 모듈 있음). 에디터가 켜져 있으면 배치모드가 안 되니 사용자에게 닫아 달라고 한다.
- 전체 재생성: `-batchmode -nographics -quit -projectPath C:/Project/DragonWasHere -executeMethod DWH.Editor.SceneBuilder.BuildAll -logFile Logs/build_all.log` (ArtScaleSettings 생성 → PPU 재임포트 → CSV 임포트 → 아트 카탈로그 → TMP → UI 테마 → 씬)
- 테스트: `-batchmode -projectPath ... -runTests -testPlatform EditMode|PlayMode -testResults Logs/editmode.xml -logFile Logs/editmode.log`. PlayMode 캡처는 `Logs/Screens/*.png`(화면 확인용).
- 빠른 컴파일 확인: `python Tools/compile_check.py` (Unity가 만든 csproj 기준. 새 asmdef가 생기면 Unity에서 csproj를 다시 만들어야 한다)
- 엑셀을 고치면: `python Docs/Tools/export_csv.py Docs/20261012_데이터테이블_Ver02.xlsx Docs/Data` → BuildAll.
- Bash heredoc 안의 `\\`가 `\`로 바뀌는 문제가 있다. 긴 파이썬 패치는 파일로 써서 실행한다.

## 6. 코드 지도 (Assets/_Project)

| 위치 | 내용 |
|---|---|
| `Scripts/Data/` | `Rows.cs`(테이블 20종 행), `Enums.cs`, `GameDatabase.cs`(조회·MVP 진행 순서), `Effects.cs`(효과 합산), `Tuning.cs`(테이블에 없는 값·패시브/액티브 기본값), `UiText.cs`(화면 문구 한/영), `Localization.cs` |
| `Scripts/Core/` | `GameManager`(상태 머신·저장 시점), `PlayerStats`(효과 → 능력치), `Formulas`, `Progression`, `Growth`, `SaveData/SaveSystem`(PlayerPrefs JSON v2), `Analytics` |
| `Scripts/OutGame/` | `UpgradeTree`, `Rebirth`(벽 3-2·5-2, 최대 2회), `UpgradeText`, `TreeLayout`(트리 좌표 자동 계산) |
| `Scripts/InGame/` | `Stage/BattleController`(레벨 생성·승패), `Dragon/`(이동·점프·파츠 리그), `Buildings/`, `AirUnits/`, `Drops/`(드랍·콤보), `Skills/`(패시브·액티브·이펙트), `Starter/`, `Fx/`(`SpriteAnim` 프레임 이펙트), `ArtCatalog`(res_id → 스프라이트) |
| `Scripts/UI/` | `BattleHud`, `OutGameScreen`, `RebirthPanel`, `ResultScreen`, `PauseMenu`, `UiFactory`/`UiTheme`(UI 에셋 V15) |
| `Editor/` | `CsvImporter`, `ArtCatalogBuilder`, `ArtScaleSettings`(PPU), `ArtImportPostprocessor`(알파 정리), `UiThemeBuilder`, `SceneBuilder` |
| `Art/Fix_Resource/` | 리소스 원본 복사본(res_path 그대로), `Art/Shaders/SpriteFx`(황금화·흰 플래시) |
| `Tests/` | EditMode: `DataIntegrityTests`, `RulesTests`, `StarterGaugeTests`, `InGameLogicTests` 등 / PlayMode: `MvpBattleTests`, `MvpFlowTests` |

---
마지막 갱신: 2026-10-12, MVP Ver01 커밋 뒤 타이틀 화면 + 플레이 피드백 3차 + QA 티켓 DWH-51~60 + QA 2차(트리 이름표·테두리·좁은 화면·이펙트 리소스 재적용) 반영(미커밋, EditMode 67·PlayMode 22 통과). 밸런스 조정안 팀 피드백 대기
