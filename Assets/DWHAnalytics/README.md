# DWH Analytics (다녀가 용! 지표 수집)

itch.io Web(WebGL) 빌드에서 Firebase(GA4)로 플레이 기록을 보내는 묶음입니다.

## MVP 프로젝트에 넣는 방법

1. 이 프로젝트의 두 폴더를 MVP 프로젝트 `Assets/` 아래에 그대로 복사합니다.
   - `Assets/DWHAnalytics/`
   - `Assets/WebGLTemplates/DragonFirebase/`
2. `Assets/WebGLTemplates/DragonFirebase/firebase-config.js`에 Firebase 웹 앱 설정 값을 넣습니다.
3. 게임 코드가 asmdef를 쓰면 그 asmdef의 References에 `DWH.Analytics`를 추가합니다. asmdef를 안 쓰면 할 일 없습니다.
4. 빌드는 메뉴 `DWH > Analytics > Build Web (Team Test)` 또는 `Build Web (Release)`.
   결과: `Builds/Web_Test.zip` → itch.io에 그대로 업로드.

## 게임 코드에서 쓰는 법

```csharp
using DWH.Analytics;

// 진행 상태가 바뀔 때마다 (모든 이벤트에 자동으로 붙음)
DwhAnalytics.StageId = "3-2";
DwhAnalytics.RebirthCount = 1;
DwhAnalytics.GrowthStage = 2;

// 세이브를 읽은 직후 한 번
DwhAnalytics.StartSession(maxStage: "3-2");

// 각 시점
int attempt = DwhAnalytics.IncrementCounter(DwhAnalytics.AttemptKey(stageId));
DwhAnalytics.Log(AnalyticsEvents.RunStart, new EventParams()
    .Add(AnalyticsParams.AttemptNo, attempt)
    .Add(AnalyticsParams.Entry, AnalyticsValues.EntryDepart)
    .Add(AnalyticsParams.GoldHeld, gold)
    .Add(AnalyticsParams.NodesOwned, nodesOwned));
```

## 동작

| 환경 | 결과 |
|---|---|
| 에디터 | Console에 `[DWH Analytics]` 로그만 |
| Web 빌드, firebase-config.js 비어 있음 | 브라우저 콘솔에만 |
| Web 팀 테스트 빌드 (DWH_DEV) | Firebase 전송 + `is_dev=1` + DebugView 표시 |
| Web 출시 빌드 | Firebase 전송, `is_dev=0` |

- `user_id`: 첫 실행 때 만든 무작위 ID. 개인정보 없음. 웹 빌드는 브라우저 localStorage(`DragonWasHere.dwh.uid`)에 둬서 itch.io에 새 빌드를 올려도(주소가 바뀜) 같은 사용자로 이어진다. 세션 번호·누적 플레이 시간·카운터도 같다(`DwhPrefs`, 10-13).
- 누적 플레이 시간과 실행 횟수(session_index)는 자동으로 셉니다.
- 이탈: index.html이 페이지를 떠날 때(pagehide) `page_exit`(scene·session_sec·persisted + 공통 파라미터)를 보낸다. 화면은 `DwhAnalytics.SetScene`으로 알려 준다. 다음 실행의 `game_session_start`에는 `return_type`(first/quick=60초 안 복귀·새로고침 추정/later), `return_gap_sec`, `prev_exit_scene`이 붙는다. 새로고침·탭 닫기·브라우저 종료는 떠나는 순간 구분할 수 없다.
- GA4 예약 이벤트(session_start 등)와 겹치지 않게 이름을 정했습니다. 새 이벤트는 `AnalyticsEvents`에 추가합니다.
