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

- `user_id`: 첫 실행 때 만든 무작위 ID(PlayerPrefs). 개인정보 없음.
- 누적 플레이 시간과 실행 횟수(session_index)는 자동으로 셉니다.
- GA4 예약 이벤트(session_start 등)와 겹치지 않게 이름을 정했습니다. 새 이벤트는 `AnalyticsEvents`에 추가합니다.
