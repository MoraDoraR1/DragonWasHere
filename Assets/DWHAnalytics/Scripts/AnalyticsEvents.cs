namespace DWH.Analytics
{
    /// <summary>
    /// 이벤트·파라미터 이름 상수. GA4 규칙: 40자 이내, 영문 소문자·숫자·밑줄.
    /// session_start, first_visit, user_engagement 등은 GA4 예약어라 쓰지 않는다.
    /// </summary>
    public static class AnalyticsEvents
    {
        // P0
        public const string GameSessionStart = "game_session_start";
        public const string RunStart = "run_start";
        public const string RunEnd = "run_end";
        public const string UpgradePurchase = "upgrade_purchase";
        public const string OutgameExit = "outgame_exit";
        public const string RebirthUnlocked = "rebirth_unlocked";
        public const string Rebirth = "rebirth";
        public const string GameComplete = "game_complete";

        // 이탈: 브라우저(index.html)가 페이지를 떠날 때 보낸다. 게임 코드에서 직접 보내지 않는다.
        public const string PageExit = "page_exit";

        // 지표 계획 Ver03 신규 (10-14 구현)
        public const string StarterEnd = "starter_end";
        public const string BonusStart = "bonus_start";
        public const string TitleStart = "title_start";
        public const string TutorialStep = "tutorial_step";
        public const string RebirthView = "rebirth_view";
        public const string ClientError = "client_error";
        /// <summary>index.html이 보낸다 (로딩 시작·완료·오류·로딩 중 닫힘)</summary>
        public const string GameLoad = "game_load";

        // 시나리오 검증 보완 (10-14): 언어 바꾸기
        public const string LanguageChange = "language_change";

        // P1 (Ver03에서 위 이벤트·run_end로 흡수, 보내지 않음)
        public const string ResultAction = "result_action";
        public const string BonusEnd = "bonus_end";
        public const string StarterResult = "starter_result";
        public const string GrowthStageUp = "growth_stage_up";
        public const string Heartbeat = "heartbeat";
    }

    public static class AnalyticsParams
    {
        // 공통 (DwhAnalytics가 자동으로 붙임)
        public const string StageId = "stage_id";
        public const string RebirthCount = "rebirth_count";
        public const string GrowthStage = "growth_stage";
        public const string SessionIndex = "session_index";
        public const string BuildVersion = "build_version";
        public const string IsDev = "is_dev";

        // game_session_start
        public const string TotalPlaytimeSec = "total_playtime_sec";
        public const string MaxStage = "max_stage";
        public const string HasSave = "has_save";

        // run_start
        public const string AttemptNo = "attempt_no";
        public const string Entry = "entry";
        public const string GoldHeld = "gold_held";
        public const string NodesOwned = "nodes_owned";

        // run_end
        public const string Result = "result";
        public const string DurationSec = "duration_sec";
        public const string DestroyPct = "destroy_pct";
        public const string GoldEarned = "gold_earned";
        public const string HpLeft = "hp_left";
        public const string StaminaLeft = "stamina_left";
        public const string BonusEntered = "bonus_entered";
        public const string Inputs = "inputs";
        public const string AvgFps = "avg_fps";
        public const string KilledBy = "killed_by";
        public const string BonusGold = "bonus_gold";
        public const string BonusSec = "bonus_sec";
        /// <summary>판이 끝날 때 조작 모드 (mouse/auto, 10-14 추가)</summary>
        public const string GuideMode = "guide_mode";

        // upgrade_purchase
        public const string NodeId = "node_id";
        public const string Cost = "cost";
        public const string GoldAfter = "gold_after";

        // outgame_exit
        public const string TimeInTreeSec = "time_in_tree_sec";
        public const string NodesBought = "nodes_bought";
        public const string GoldLeft = "gold_left";
        public const string AffordableNodes = "affordable_nodes";
        public const string NodesViewed = "nodes_viewed";

        // starter_end
        public const string StarterSec = "starter_sec";

        // title_start
        public const string Misclicks = "misclicks";

        // tutorial_step
        public const string StepIndex = "step_index";
        public const string StepId = "step_id";

        // client_error (message는 GA4에 등록하지 않는다: 자유 문장)
        public const string ErrorType = "error_type";
        public const string Message = "message";

        // 사용자 속성
        public const string HasRebirth = "has_rebirth";

        // rebirth_unlocked / rebirth
        public const string Wall = "wall";
        public const string AttemptsAtWall = "attempts_at_wall";
        public const string RebirthNo = "rebirth_no";
        public const string PointsGained = "points_gained";
        public const string TraitChosen = "trait_chosen";
        public const string AttemptsSinceUnlock = "attempts_since_unlock";
        public const string TimeSinceUnlockSec = "time_since_unlock_sec";

        // game_complete
        public const string TotalRuns = "total_runs";

        // P1
        public const string Action = "action";
        public const string GaugeResult = "gauge_result";
        public const string Damage = "damage";
        public const string Scene = "scene";

        // 모든 이벤트·page_exit 공통 + 사용자 속성: 지금 언어 (ko/en). language_change는 바꾸기 전 언어를 from_lang에
        public const string Lang = "lang";
        public const string FromLang = "from_lang";

        // page_exit에 붙는 이탈 순간 상태 (게임이 1초마다 갱신)
        // 전투·보너스: in_starter, run_sec, hp_pct, stamina_pct, destroy_pct / 둥지: nodes_viewed, nodes_bought, outgame_sec
        public const string InStarter = "in_starter";
        public const string RunSec = "run_sec";
        public const string HpPct = "hp_pct";
        public const string StaminaPct = "stamina_pct";
        public const string OutgameSec = "outgame_sec";

        // page_exit (index.html이 붙임): scene, session_sec, persisted
        public const string SessionSec = "session_sec";
        public const string Persisted = "persisted";
        // game_session_start에 index.html이 덧붙이는 직전 이탈 정보
        public const string ReturnType = "return_type";
        public const string ReturnGapSec = "return_gap_sec";
        public const string PrevExitScene = "prev_exit_scene";
    }

    /// <summary>파라미터 값으로 쓰는 고정 문자열.</summary>
    public static class AnalyticsValues
    {
        public const string EntryDepart = "depart";
        public const string EntryRetryNow = "retry_now";

        public const string ResultClear = "clear";
        public const string ResultFailHp = "fail_hp";
        public const string ResultFailStamina = "fail_stamina";
        /// <summary>일시정지 "세션 끝내기"로 직접 끝냄 (10-14)</summary>
        public const string ResultRetired = "retired";

        // killed_by: 마지막으로 체력을 깎은 것, 기력이 다하면 stamina (run_end)
        public const string KilledByBuilding = "building";
        public const string KilledByLandmark = "landmark";
        public const string KilledByAir = "air";
        public const string KilledByStamina = "stamina";

        // action: tutorial_step(done/skip), rebirth_view(open/cancel)
        public const string ActionDone = "done";
        public const string ActionSkip = "skip";
        public const string ActionOpen = "open";
        public const string ActionCancel = "cancel";

        // guide_mode
        public const string GuideMouse = "mouse";
        public const string GuideAuto = "auto";

        // error_type (Unity 쪽. js_error·promise·webgl_lost는 index.html)
        public const string ErrorUnityException = "unity_exception";
        public const string ErrorSaveFailed = "save_failed";

        public const string ActionRetryNow = "retry_now";
        public const string ActionToTree = "to_tree";

        // scene: 지금 화면 (page_exit의 이탈 화면)
        public const string SceneTitle = "title";
        public const string SceneOutGame = "outgame";
        public const string SceneStage = "stage";
        public const string SceneBonus = "bonus";
        public const string SceneResult = "result";

        // return_type: first(이탈 기록 없음) / quick(60초 안에 다시 실행, 새로고침 추정) / later
        public const string ReturnFirst = "first";
        public const string ReturnQuick = "quick";
        public const string ReturnLater = "later";
    }
}
