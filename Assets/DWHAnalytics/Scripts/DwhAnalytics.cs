using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DWH.Analytics
{
    /// <summary>
    /// 인게임 지표 전송.
    /// - WebGL 빌드: .jslib → 브라우저의 Firebase JS SDK(GA4)로 보낸다.
    /// - 에디터·기타 플랫폼: 콘솔에만 찍는다.
    ///
    /// 게임 쪽 사용법
    /// 1) 진행 상태가 바뀔 때 Context 값 갱신: DwhAnalytics.StageId = "3-2"; RebirthCount; GrowthStage
    /// 2) 세이브를 읽은 뒤 한 번: DwhAnalytics.StartSession(maxStage)
    /// 3) 각 시점에서: DwhAnalytics.Log(AnalyticsEvents.RunEnd, new EventParams().Add(...))
    ///
    /// 팀 테스트 빌드는 DWH_DEV 심볼(또는 Development Build)로 만들면 is_dev=1, DebugView에 표시된다.
    /// </summary>
    public static class DwhAnalytics
    {
        // ── 공통 컨텍스트: 게임이 갱신한다 ──
        public static string StageId = "1-1";
        public static int RebirthCount;
        public static int GrowthStage = 1;

        /// <summary>지금 화면 (AnalyticsValues.Scene*). SetScene으로 바꾼다. 브라우저가 page_exit에 쓴다.</summary>
        public static string Scene { get; private set; } = AnalyticsValues.SceneTitle;

        public static string UserId { get; private set; }
        public static int SessionIndex { get; private set; }
        public static bool IsDev { get; private set; }
        public static string BuildVersion => Application.version;

        /// <summary>콘솔 출력 여부. 에디터와 dev 빌드에서 기본 켜짐.</summary>
        public static bool LogToConsole = true;

        /// <summary>마지막으로 보낸 이벤트 (테스트 화면 표시용).</summary>
        public static event Action<string, string> OnLogged;

        const string KeyUserId = "dwh.uid";
        const string KeySessionIndex = "dwh.session_index";
        const string KeyPlaytime = "dwh.playtime_sec";
        const string KeyCounterPrefix = "dwh.c.";

        static readonly Regex NamePattern = new Regex("^[a-z][a-z0-9_]{0,39}$");
        static bool _initialized;
        static bool _sessionStarted;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DwhFb_Init(string userId, string userPropsJson, int isDev);
        [DllImport("__Internal")] static extern void DwhFb_LogEvent(string name, string paramsJson);
        [DllImport("__Internal")] static extern void DwhFb_SetUserProps(string userPropsJson);
        [DllImport("__Internal")] static extern void DwhFb_SetContext(string contextJson);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            Init();
        }

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

#if DWH_DEV
            IsDev = true;
#else
            IsDev = Debug.isDebugBuild;
#endif
            LogToConsole = Application.isEditor || IsDev;

            UserId = DwhPrefs.GetString(KeyUserId, "");
            if (string.IsNullOrEmpty(UserId))
            {
                UserId = Guid.NewGuid().ToString("N");
                DwhPrefs.SetString(KeyUserId, UserId);
            }
            SessionIndex = DwhPrefs.GetInt(KeySessionIndex, 0) + 1;
            DwhPrefs.SetInt(KeySessionIndex, SessionIndex);
            DwhPrefs.Save();

            PlaytimeTracker.Create();

            var props = new EventParams()
                .Add(AnalyticsParams.BuildVersion, BuildVersion)
                .Add(AnalyticsParams.IsDev, IsDev);
#if UNITY_WEBGL && !UNITY_EDITOR
            DwhFb_Init(UserId, props.ToJson(), IsDev ? 1 : 0);
#endif
            if (LogToConsole) Debug.Log($"[DWH Analytics] init uid={UserId} session={SessionIndex} dev={IsDev} {props.ToJson()}");
        }

        /// <summary>세이브를 읽은 직후 한 번 호출. 한 실행에 한 번만 보낸다.</summary>
        public static void StartSession(string maxStage)
        {
            if (_sessionStarted) return;
            _sessionStarted = true;
            SetUserProperty(AnalyticsParams.MaxStage, maxStage);
            Log(AnalyticsEvents.GameSessionStart, new EventParams()
                .Add(AnalyticsParams.TotalPlaytimeSec, Mathf.FloorToInt(TotalPlaytimeSec))
                .Add(AnalyticsParams.MaxStage, maxStage));
        }

        public static void Log(string eventName, EventParams p = null)
        {
            if (!_initialized) Init();
            if (!NamePattern.IsMatch(eventName))
            {
                Debug.LogWarning($"[DWH Analytics] 이벤트 이름 규칙 위반: {eventName}");
                return;
            }

            p ??= new EventParams();
            if (!p.Has(AnalyticsParams.StageId)) p.Add(AnalyticsParams.StageId, StageId);
            p.Add(AnalyticsParams.RebirthCount, RebirthCount)
             .Add(AnalyticsParams.GrowthStage, GrowthStage)
             .Add(AnalyticsParams.SessionIndex, SessionIndex)
             .Add(AnalyticsParams.BuildVersion, BuildVersion)
             .Add(AnalyticsParams.IsDev, IsDev);

            if (p.Count > 25) Debug.LogWarning($"[DWH Analytics] {eventName}: 파라미터 {p.Count}개. GA4는 25개까지만 받는다.");

            string json = p.ToJson();
#if UNITY_WEBGL && !UNITY_EDITOR
            DwhFb_LogEvent(eventName, json);
#endif
            if (LogToConsole) Debug.Log($"[DWH Analytics] {eventName} {json}");
            OnLogged?.Invoke(eventName, json);
            PushContext();
        }

        /// <summary>화면이 바뀔 때 호출. 브라우저가 이탈 순간에 이 화면과 공통 파라미터를 page_exit로 보낸다.</summary>
        public static void SetScene(string scene)
        {
            // Init은 하지 않는다(실행 시 BeforeSceneLoad에서 이미 한다). 에디트모드 테스트에서 DontDestroyOnLoad를 부르지 않게.
            Scene = string.IsNullOrEmpty(scene) ? AnalyticsValues.SceneTitle : scene;
            PushContext();
        }

        /// <summary>page_exit에 붙일 공통 파라미터 + 지금 화면. 이벤트를 보낼 때마다, 화면이 바뀔 때마다 갱신한다.</summary>
        public static EventParams ExitContext() => new EventParams()
            .Add(AnalyticsParams.Scene, Scene)
            .Add(AnalyticsParams.StageId, StageId)
            .Add(AnalyticsParams.RebirthCount, RebirthCount)
            .Add(AnalyticsParams.GrowthStage, GrowthStage)
            .Add(AnalyticsParams.SessionIndex, SessionIndex)
            .Add(AnalyticsParams.BuildVersion, BuildVersion)
            .Add(AnalyticsParams.IsDev, IsDev);

        static void PushContext()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DwhFb_SetContext(ExitContext().ToJson());
#endif
        }

        /// <summary>유저 속성 (이름 24자, 값 36자 이내). rebirth_count, max_stage 등.</summary>
        public static void SetUserProperty(string name, string value)
        {
            if (!_initialized) Init();
            var props = new EventParams().Add(name, value ?? "");
#if UNITY_WEBGL && !UNITY_EDITOR
            DwhFb_SetUserProps(props.ToJson());
#endif
            if (LogToConsole) Debug.Log($"[DWH Analytics] user_props {props.ToJson()}");
        }

        // ── 파라미터 계산 도우미 ──

        /// <summary>저장되는 카운터를 1 올리고 새 값을 돌려준다. 예: 스테이지별 시도 횟수.</summary>
        public static int IncrementCounter(string key)
        {
            int v = DwhPrefs.GetInt(KeyCounterPrefix + key, 0) + 1;
            DwhPrefs.SetInt(KeyCounterPrefix + key, v);
            DwhPrefs.Save();
            return v;
        }

        public static int GetCounter(string key) => DwhPrefs.GetInt(KeyCounterPrefix + key, 0);

        public static void ResetCounter(string key)
        {
            DwhPrefs.DeleteKey(KeyCounterPrefix + key);
            DwhPrefs.Save();
        }

        /// <summary>회차·스테이지별 시도 횟수 키. 환생하면 새로 센다.</summary>
        public static string AttemptKey(string stageId) => $"attempt.r{RebirthCount}.{stageId}";

        /// <summary>모든 실행을 합친 누적 플레이 시간(초).</summary>
        public static float TotalPlaytimeSec => PlaytimeTracker.TotalSec;

        // ── 누적 플레이 시간 ──
        class PlaytimeTracker : MonoBehaviour
        {
            static PlaytimeTracker _instance;
            static float _baseSec;
            static float _sessionSec;
            float _sinceSave;

            public static float TotalSec => _baseSec + _sessionSec;

            public static void Create()
            {
                if (_instance != null) return;
                _baseSec = DwhPrefs.GetFloat(KeyPlaytime, 0f);
                var go = new GameObject("[DWH Analytics]");
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PlaytimeTracker>();
            }

            void Update()
            {
                _sessionSec += Time.unscaledDeltaTime;
                _sinceSave += Time.unscaledDeltaTime;
                // 웹에서는 탭을 닫을 때 저장이 보장되지 않아 주기적으로 저장한다.
                if (_sinceSave >= 15f) Save();
            }

            void OnApplicationPause(bool paused) { if (paused) Save(); }
            void OnApplicationQuit() { Save(); }

            void Save()
            {
                _sinceSave = 0f;
                DwhPrefs.SetFloat(KeyPlaytime, TotalSec);
                DwhPrefs.Save();
            }
        }
    }
}
