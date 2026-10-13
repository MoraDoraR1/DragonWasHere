# 다녀가 용! MVP 작업 인계 (Ver01)

- 작성: Claude, 2026-10-12 (작업하면서 계속 갱신한다. 맨 아래 "마지막 갱신"을 확인할 것)
- 용도: 사용량 한도 등으로 세션이 끊겼을 때, 다른 계정·세션의 Claude Code가 이 문서만 읽고 이어서 작업한다.

---

> 집 PC로 옮길 때는 `Docs/20261012_집PC작업인계_Ver01_Claude.md`를 먼저 본다 (미커밋 변경 압축 사용법).

## 0. 지금 바로 이어서 할 일 (10-13 오후, 사용량 한도로 중단)

**10-13 늦은 오후 갱신 (미커밋)**: 사용자 요청 "전체 스테이지 밸런스 / 브레스가 공중 유닛을 치게 목 각도 넓히기 / 스타터 착지 후 튕김 완화 / 웹 빌드 Ver03" 처리 완료. 스테이지별 배율 G(엔딩 52판), 목 위로 40도+머리 20도·불길 위 65도·공중 유닛 자동 조준, 지붕에서 0.4칸 폴짝 → 바로 옆 착지(좁은 틈이면 틈 가운데), `bundleVersion` 0.3.0, 빌드 `Builds/20261013_웹빌드_Ver03_Claude.zip`. 테스트 EditMode 73/74(다른 세션 `AnalyticsExitTests` 1개 실패), PlayMode 24 통과·1 제외. 자세한 건 `작업내용.md` 10-13 첫 섹션. 스테이지별 봇 실험은 `BalanceSimulationTests`의 `DWH_SIM_OVERRIDES=<배율 파일>`(줄 형식 `stage_id 항목 배율`, 항목 nhp·ahp·aatk·lhp·latk·uhp·uatk, 예 `Logs/balance_overrides_ovG.txt`).

**상태**: 마지막 커밋 `00c02a8 [MOD]20261013_MVPVer02_Claude`(푸시 안 함, origin보다 1개 앞섬). 그 뒤 미커밋 변경 = 화면 고정(레터박스): `InGame/Stage/LetterboxCamera.cs`(+meta), `UiFactory.Canvas`가 1920×1080 `Frame`을 돌려줌(`CanvasFrame`), `GuideInput` 커서 판정, `GameManager`에서 레터박스 부착, `RulesTests.Letterbox_KeepsSixteenByNine`. 테스트 EditMode 68/68, PlayMode 22 통과·1 제외. 레터박스는 배치모드에서 꺼져 있어 **에디터 Play로 확인 필요**. 자세한 건 `작업내용.md` 10-13 "화면 고정".

**테스트 방식(사용자 지시)**: 테스트는 Sonnet 서브에이전트에 맡긴다. 에디터가 열려 있으면 테스트 사본 `C:\Project\DragonWasHere_Test`에서 (동기화: `robocopy C:\Project\DragonWasHere C:\Project\DragonWasHere_Test /MIR /XD Library Temp Logs Builds obj .git UserSettings /XF *.csproj *.slnx`).

**사용자 요청 10건 (10-13) → 10-13 오후 처리 완료(미커밋). 결과는 `작업내용.md` 10-13 "사용자 요청 10건". 아래는 당시 단서 기록**
1. **파이어 브레스 위치**: 입에서 나오는 건 OK. 머리가 마우스 커서를 따라 돌 때 불길 위치·각도가 엉성함.
   - 단서: `PassiveSkillSystem.EmitBreath`가 `dragon.Animator.Mouth`와 Aim 방향(수평 ±35° 제한)으로 불길을 놓는데, 머리 회전은 `DragonAnimator.LateUpdate`(약 199~208줄: `aimNeck`로 neck 회전, upHead 회전)에서 정해진다. 브레스는 코루틴(Update 단계)에서 놓여 **한 프레임 늦고**, 방향도 머리 실제 각도가 아니라 커서 방향이다. → 불길 배치를 애니메이터 LateUpdate 뒤로 옮기고(예: 애니메이터가 입 위치·머리 방향 `MouthDirection`을 계산해 두고 불길도 그때 갱신), 방향은 머리(upHead) 실제 회전을 쓰기.
2. **건물 체력바**: 드래곤 체력바 UI(HUD의 `gauge-track` + `gauge-fill-health` 스프라이트, `UiFactory.Bar`)와 같은 모양을 쓰되, **크기는 지금 건물 체력바 크기 그대로**. 대상: `InGame/Fx/WorldHealthBar.cs`(SpriteRenderer 기반, 9-slice 경계 주의).
3. **인게임에서 환경설정으로 언어 변경**: 지금은 `SettingsActions.CanChangeLanguage`가 타이틀·아웃게임에서만 true(전투 중 HUD를 다시 만들지 않기 때문). 전투 중에도 바꾸려면 `GameManager.ToggleLanguage`에서 HUD(`BattleHud`)·일시정지 메뉴·결과 화면도 다시 만들거나 문구만 갱신해야 함. 일시정지 상태 유지 주의.
4. **처음 시작하면 언제나 영어**: `SaveData.language` 기본값 "ko" → "en". (기존 저장이 있으면 저장된 언어 유지가 맞는지 확인. "언제나"를 "처음 실행(저장 없음)"으로 해석하는 게 자연스러움)
5. **트리 노드 이름표가 영어 이름으로 꽉 참**: `OutGameScreen.FitNamePlate` 여백(지금 가로 +18, 세로 +8, 최대 `TileSize+40+8`)을 넉넉히. 이름 글자 칸 폭(`TileSize + 40`)도 함께 조정, 노드 간격(`TreeLayout`)과 겹치지 않게.
6. **투사체 리소스 확인**: 공격형 건물·공중 유닛 공통 투사체 리소스가 새로 만들어짐(바탕화면 `DragonWasHere_기획서및리소스/리소스/Fix_Resource/` 아래, 경로 확인 필요). 지금은 `GrayboxSprites.Square` + 색(`GrayboxColors.AirProjectile`)을 쓴다(`AirUnit.Fire`, `BuildingTurret.Fire`). 
7. **데이터 테이블 추가**: 엑셀 resource 시트에 투사체 행 추가(res_id 대역은 기존 규칙 확인), 필요하면 building/unit에 `projectile_res` 같은 컬럼 추가. **엑셀은 그림 요소가 있어 openpyxl로 저장하면 안 됨 → 셀 XML 직접 수정**(10-12 허리케인 frame_count 수정 방식, `작업내용.md` 참고. 새 행·새 컬럼은 sheet XML + sharedStrings + dimension 갱신 필요, 바탕화면 원본 동기화) → `python Docs/Tools/export_csv.py ...` → `Rows.cs`에 컬럼 추가(`[Optional]`) → Build All → 투사체 코드에서 res로 스프라이트 사용.
8. **랜드마크 위 스타터 착륙**: 스타터 낙하가 랜드마크 가운데에 떨어지면 드래곤이 랜드마크 안에 겹쳐 서서, 드래곤은 때리는데 랜드마크 탄은 안 맞는다. → 랜드마크(또는 겹친 건물) 위로 떨어지면 지붕을 점프 공격하듯 타격 후 드래곤을 건물 밖(앞쪽)으로 튕겨 내기. 대상: `StageStarter` 낙하(Drop) 단계 끝, `DragonController`(점프/넉백 로직 `StartJump`, `knockbackTimer` 참고).
9. **건물이 바닥에서 떠 보임**: 근경(바닥) 위에 건물·랜드마크가 붙게 배치. 건물 스프라이트 피벗은 BottomCenter(`ArtScaleSettings` 건물 규칙), 배치 y는 `Tuning.GroundHeight`(2.44 = 바닥 이미지 높이). 그림 아래 투명 여백이나 바닥 그림의 지면선 높이 차이일 가능성 → 바닥 이미지의 실제 지면선 px와 건물 그림 아래 여백을 재서 오프셋(건물별 또는 공통) 적용. 캡처 `Logs/Screens/stage_*.png`로 확인.
10. **밸런스**: 1-2 클리어에 20판 이상 걸림 → 1-2와 전체 밸런스 조정. 근거 자료: `작업내용.md` 10-12 "봇 결과"(실패 대부분 랜드마크 탄에 의한 체력 0), 조정안 B(랜드마크 atk ×0.15, 공격 건물·유닛 atk ×0.5)·C(랜드마크 탄 1발만), jump_rate 0, `Docs/20261012_MVP밸런스_Ver01_Claude.md` 9장 목표(1-2·2-2 약 3판, 7-2 약 6판, 전체 약 44판). 봇: `BalanceSimulationTests`([Explicit], `-testFilter DWH.Tests.BalanceSimulationTests`, 환경 변수 `DWH_SIM_ATK_SCALE`·`DWH_SIM_LM_SCALE`로 미리 실험) → 엑셀 수정(셀 XML) → CSV → Build All → 봇으로 목표 판수 확인. 공중 유닛 ×1.75 이후 1-1도 체력 3으로 아슬아슬.

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
마지막 갱신: 2026-10-13 저녁, Ver02 커밋(00c02a8) 뒤 레터박스 + 요청 10건 처리(미커밋, EditMode 68·PlayMode 24 통과). 남은 일: 메모.md "밸런스 남은 것"
