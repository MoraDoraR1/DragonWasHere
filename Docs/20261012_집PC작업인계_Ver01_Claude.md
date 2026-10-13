# 다녀가 용! 집 PC 작업 인계 (Ver01)

- 작성: Claude, 2026-10-12 (회사 PC → 집 PC)
- 상황: 마지막 커밋 `5a2f1db [MOD]20261012_MVPVer01_Claude`는 원격(origin/main)에 올라가 있다. 그 뒤 작업(타이틀, 플레이 피드백, QA 티켓)은 **커밋하지 않았다**(사용자 지시: 지금은 커밋하지 않음).
- 같이 볼 문서: `Docs/20261012_MVP작업인계_Ver01_Claude.md`(전체 MVP 인계·코드 지도), `작업내용.md`, `메모.md`

---

## 1. 챙겨 갈 것

| # | 무엇 | 위치(회사 PC) | 크기 | 필수 | 설명 |
|---|---|---|---|---|---|
| 1 | **미커밋 변경 압축** | `바탕화면/DragonWasHere_미커밋변경_20261012.zip` | 10.6MB | 필수 | 커밋 뒤 바뀐 파일과 새 파일 255개(프로젝트 폴더 기준 경로 그대로). 아래 2장 목록 |
| 2 | **기획서·리소스 폴더** | `바탕화면/DragonWasHere_기획서및리소스/` | 869MB | 필수 | 기준 문서 원본(README 규칙 4: Docs 수정 시 여기에도 반영), 엑셀 Ver02, `리소스/Fix_Resource`(아트 원본), 기획서 PDF. 집 PC에서도 **바탕화면에 같은 이름**으로 둔다 |
| 3 | 저장소 | GitHub `origin` (커밋 `5a2f1db`) | 약 200MB | 필수 | 집 PC에서 `git pull` 또는 clone. 따로 복사할 필요 없음 |
| 4 | Claude 메모리 | `C:/Users/user/.claude/projects/C--Project-DragonWasHere/memory/` | 수 KB | 선택 | 작업 방식 메모 4개(커밋은 요청할 때만, 문서 원본 동기화, 문제는 끝까지 직접 해결, 사용량 95%면 인계 문서). 집 PC의 같은 위치(프로젝트 경로가 다르면 폴더 이름이 달라짐)에 넣으면 Claude가 그대로 이어서 따른다. 없어도 README·CLAUDE.md 규칙으로 대부분 충분 |
| 5 | 웹 빌드(팀 테스트용) | `C:/Project/DragonWasHere/Builds/Web_Test.zip` | 52MB | 선택 | 10-12 오전 빌드(타이틀 이전). 새로 빌드할 거면 필요 없음 |

**챙기지 않아도 되는 것**: `Library/`(4GB, 집 PC에서 Unity가 다시 만든다), `Temp/`, `obj/`, `Logs/`(테스트 로그·캡처), `Builds/`, `Assets/_Recovery/`(크래시 복구 씬, 커밋 대상 아님), `Assets/Resources.meta`(빈 폴더 메타).

> 압축 대신 프로젝트 폴더 통째로(`Library`·`Temp`·`obj`·`Logs`·`Builds` 제외, 약 385MB, `.git` 포함) 복사해도 된다. 그때는 3·4단계(압축 풀기)가 필요 없다.

## 2. 압축 안 내용 (미커밋 변경)

**새 파일**
- `Assets/_Project/Scripts/UI/TitleScreen.cs`, `SettingsPopup.cs` (+ .meta) — 타이틀 화면, 환경설정 팝업
- `Assets/_Project/Tests/PlayMode/TitleFlowTests.cs`, `InGameControlTests.cs` (+ .meta)
- `Assets/StreamingAssets/title-background.webm` (+ .meta, `Assets/StreamingAssets.meta`) — 타이틀 배경 영상(WebGL은 URL로 재생)

**수정 파일**
- 스크립트: `GameManager`, `SaveData`, `Tuning`, `UiText`, `AirUnit`, `AirUnitSquad`, `DragonAnimator`, `DragonController`, `AimGuide`, `SpriteAnim`, `ActiveSkillSystem`, `PassiveSkillSystem`, `SkillFx`, `BattleCamera`, `BattleController`, `StageStarter`, `TreeLayout`, `OutGameScreen`, `PauseMenu`, `UiFactory`, `SceneBuilder`(에디터)
- 테스트: `RulesTests`, `MvpBattleTests`, `MvpFlowTests`
- 데이터: `Docs/20261012_데이터테이블_Ver02.xlsx`(허리케인 frame_count 12→18), `Docs/Data/resource.csv`, `Assets/_Project/Data/GameDatabase.asset`, `ArtCatalog.asset`, `Scenes/Game.unity`
- 아트: `Assets/_Project/Art/Fix_Resource/스킬 이펙트/`, `피격 이펙트/` 197개(리소스 교체본 재복사, `건물화재 소화` 새 폴더 포함)
- 문서: `작업내용.md`, `메모.md`, `Docs/20261012_MVP작업인계_Ver01_Claude.md`, 이 문서

## 3. 집 PC 준비

1. **Unity 6000.3.19f1** + **WebGL Build Support** 모듈 (Unity Hub). 다른 버전으로 열지 않는다.
2. **Git** (원격 접근 권한 확인).
3. **Python 3** + 패키지: `pip install openpyxl pillow pymupdf` (엑셀→CSV 변환 `Docs/Tools/export_csv.py`, 이미지 확인, 기획서 PDF 읽기용).
4. (선택) Claude Code 데스크톱 앱 + Claude in Chrome 확장(팀 보드 Neeko.work 읽기용, 로그인된 크롬 필요).

## 4. 집 PC에서 이어 받는 순서

1. 저장소 받기: 이미 있으면 `git pull`, 없으면 `git clone <원격 주소>`. `git log -1`이 `5a2f1db`인지 확인.
2. **Unity를 열기 전에** `DragonWasHere_미커밋변경_20261012.zip`을 프로젝트 폴더 루트에 풀고 **덮어쓰기**(압축 안 경로가 `Assets/...`, `Docs/...` 형태).
3. `git status`로 변경 파일이 회사 PC와 같은지 확인(수정 약 230개 + 새 파일, 2장 목록).
4. `바탕화면/DragonWasHere_기획서및리소스/`를 같은 이름으로 복사.
5. Unity Hub에서 프로젝트 열기(첫 임포트는 Library 생성 때문에 오래 걸린다).
6. 메뉴 **DWH > Build All (Data, Art, Scenes)** 한 번 실행(데이터·아트 카탈로그·UI 테마·씬·StreamingAssets 영상 재생성).
7. Test Runner에서 EditMode 67개, PlayMode 22개 통과 확인(아래 5장). `BalanceSimulationTests`는 [Explicit]이라 제외가 정상.
8. `Assets/_Project/Scenes/Game.unity` 열고 Play → 타이틀 화면부터 나오면 정상.

## 5. 확인된 상태 (10-12 회사 PC 기준)

- 테스트: EditMode **67** 통과, PlayMode **22** 통과(마지막 실행).
- 주의: `MvpFlowTests.FirstRun_ClearsWithBonus_SettlesAndAdvances`는 업그레이드 없는 1-1을 **체력 3** 남기고 겨우 깨는 판이라, 전체 실행에서 가끔 실패한다. 다시 돌리면 통과한다. 밸런스 결정 때 같이 정리할 문제(`메모.md`).
- 배치모드 명령(에디터가 닫혀 있어야 함):
  - 전체 재생성: `Unity.exe -batchmode -nographics -quit -projectPath <프로젝트> -executeMethod DWH.Editor.SceneBuilder.BuildAll -logFile Logs/build_all.log`
  - 테스트: `Unity.exe -batchmode -projectPath <프로젝트> -runTests -testPlatform EditMode|PlayMode -testResults Logs/<이름>.xml -logFile Logs/<이름>.log`
  - 화면 캡처는 `Logs/Screens/*.png` (MvpBattleTests가 시작할 때 폴더를 비우므로, 특정 캡처는 그 테스트 클래스만 따로 돌린다)
  - 빠른 컴파일 확인: `python Tools/compile_check.py` (PlayMode의 `DWH.Analytics.Recent` 오류 2개는 이 도구의 csproj 한계로 원래 나는 것, Unity에서는 정상)

## 6. 커밋 뒤 한 일 (요약, 자세한 건 `작업내용.md` 10-12)

1. **타이틀 화면**: 배경 영상·로고(한/영)·새 게임(기록 있으면 경고 팝업)·이어하기·환경설정(언어·화면 모드·화면 흔들림)·버전. 일시정지에 환경설정·타이틀로 이동.
2. **플레이 피드백**: 일시정지 중 인게임 완전 정지 / 공중 유닛은 드래곤 머리 위 상공을 배회하며(머리보다 0.8 위) 범위 안에서만 공격 / 카메라 진행 방향 앞보기(드래곤 1/3 지점, 방향 전환 1.5초).
3. **QA 티켓 DWH-51~60** (Neeko.work 백로그): 트리 노드 간격·선택 테두리·영어 글자 맞춤, 파이어 브레스 이미지, 파이어볼 크기, 스타터 화면 잘림, 지진 끊김, 허리케인 따라옴, 메테오 방향, 공중 유닛 ×1.75.
4. **QA 2차**: 노드 이름표 바탕(선이 이름을 안 가림), 선택 테두리 아래 변 연장, 좁은 화면에서 전투 카메라가 16:9 가로 폭 유지, 스킬·피격 이펙트 리소스 재적용(허리케인 18장).
5. **파이어볼 ×1.5** (0.6 → 0.9).

## 7. 남은 일 / 확인 필요 (우선순위)

1. **커밋**: 사용자가 요청할 때. 메시지 형식 `[MOD]YYYYMMDD_내용_Claude`(수정·추가·삭제 섞이면 MOD, 본문에 나머지). `Assets/_Recovery/`, `Assets/Resources.meta`는 넣지 않는다.
2. **QA 확인 대기**: DWH-60 "적"을 공중 유닛으로 해석해 ×1.75만 적용(건물 그대로) — 배율·건물 포함 여부 확인. Neeko.work 티켓 상태는 바꾸지 않았다.
3. **리소스**: 새 피격 이펙트 `건물화재 소화`(8장)는 resource 행이 없어 미사용 → 쓸 곳과 res_id 추가 요청. 파이어브레스·낙뢰 폴더에 frames/ 말고 낱장 한 벌이 더 있음(게임은 frames/ 사용) → 중복인지 확인.
4. **밸런스 조정안 결정**(팀 피드백 후): 안 B(랜드마크 atk ×0.15, 공격 건물·유닛 ×0.5) / 안 C(랜드마크 탄 1발만) / jump_rate / 7-2 hp. 근거 `작업내용.md` 10-12 "봇 결과".
5. **데이터 추가 요청**: 공중 유닛 공격 범위(6 unit)·배회 폭(±3.5)·크기(×1.75), 환생 벽, 액티브 세션당 횟수가 코드(`Tuning`)에 임시로 있다.
6. **WebGL 재빌드 후 확인**: 타이틀 영상 URL 재생(사파리 webm 미지원 가능 → 단색 대체), 좁은 브라우저 창에서 전투·스타터 화면.
7. 출시 전 체크리스트(가이드 10장), 일정: 개발 완료 10-14(수), itch.io 출시 10-15(목).

## 8. 집 PC의 Claude에게 (처음 할 말 예시)

> `Docs/20261012_집PC작업인계_Ver01_Claude.md` 읽고 이어서 진행해. 압축은 풀어 뒀어.

Claude는 README 규칙대로 README·작업내용·메모를 먼저 읽고 특이사항을 보고한 뒤 진행한다. 답은 한국어, 커밋은 요청할 때만, `Docs/` 문서를 고치면 바탕화면 원본에도 반영.

---
마지막 갱신: 2026-10-12 (파이어볼 ×1.5까지 반영, 미커밋)
