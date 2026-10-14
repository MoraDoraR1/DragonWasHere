using System.Collections.Generic;
using System.Linq;
using DWH.Analytics;
using DWH.Data;
using DWH.InGame.Stage;
using DWH.OutGame;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DWH.Core
{
    /// <summary>
    /// 지표 수집. 게임은 여기만 부른다 (가이드 8장 P0 8개 + 지표 계획 Ver03 신규, 10-14).
    /// 전송은 수집 묶음(Assets/DWHAnalytics, DwhAnalytics)이 맡는다: WebGL은 jslib → Firebase JS SDK → GA4, 에디터는 콘솔.
    /// 공통 파라미터(stage_id, rebirth_count, growth_stage, session_index, build_version, is_dev)는 DwhAnalytics가 붙인다.
    /// game_load·client_error(JS 오류)·page_exit는 브라우저(index.html)가 보낸다.
    /// </summary>
    public static class Analytics
    {
        /// <summary>보낸 이벤트 이름 (테스트용, 최근 50개)</summary>
        public static readonly List<string> Recent = new();

        const string UnlockTimeKey = "dwh.rebirth_unlock_time";
        /// <summary>client_error는 세션당 이만큼, 같은 오류는 한 번만 (Ver03 전송 상한)</summary>
        const int MaxErrorsPerSession = 5;

        static float outgameEnterTime;
        static int nodesBoughtInTree, nodesViewedInTree;
        static float titleShownTime;
        static int titleMisclicks;
        static float tutorialPageTime;
        static int tutorialStepIndex;
        static bool rebirthViewOpen;
        static readonly HashSet<string> sentErrors = new();

        // 판 동안 모으는 값 (run_end inputs·avg_fps)
        static int runInputs, fpsFrames;
        static float fpsSeconds;

        static void Context(GameDatabase db, SaveData save)
        {
            DwhAnalytics.StageId = db.StageLabel(save.currentStage);
            DwhAnalytics.RebirthCount = save.rebirthCount;
            DwhAnalytics.GrowthStage = (int)Growth.Stage(db, save.runSpentGold).growthStage;
            DwhAnalytics.Lang = Localization.Code(Localization.Current);
        }

        static void Log(string name, EventParams p)
        {
            DwhAnalytics.Log(name, p);
            Recent.Add(name);
            if (Recent.Count > 50) Recent.RemoveAt(0);
        }

        static string AttemptKey(GameDatabase db, string stageId) => DwhAnalytics.AttemptKey(db.StageLabel(stageId));

        /// <summary>game_session_start: 세이브를 읽은 직후 한 번. 환생한 적이 있으면 사용자 속성 has_rebirth</summary>
        public static void SessionStart(GameDatabase db, SaveData save, bool hasSave)
        {
            Context(db, save);
            DwhAnalytics.StartSession(db.StageLabel(save.highestStage), hasSave);
            if (save.rebirthCount > 0) DwhAnalytics.SetUserProperty(AnalyticsParams.HasRebirth, "1");
            DwhAnalytics.SetUserProperty(AnalyticsParams.Lang, DwhAnalytics.Lang);
            Recent.Add(AnalyticsEvents.GameSessionStart);
        }

        /// <summary>language_change: 언어를 바꿨다 (타이틀·설정). lang은 바꾼 뒤, from_lang은 바꾸기 전</summary>
        public static void LanguageChange(GameDatabase db, SaveData save, Language from)
        {
            Context(db, save);
            DwhAnalytics.SetUserProperty(AnalyticsParams.Lang, DwhAnalytics.Lang);
            Log(AnalyticsEvents.LanguageChange, new EventParams()
                .Add(AnalyticsParams.FromLang, Localization.Code(from))
                .Add(AnalyticsParams.Scene, DwhAnalytics.Scene));
        }

        static float exitStateAt = -999f;
        static GameState exitStateFor = (GameState)(-1);

        /// <summary>
        /// 매 프레임 (GameManager). page_exit에 붙일 이탈 순간 상태를 1초마다(화면이 바뀌면 바로) 갱신한다.
        /// 전투·보너스: 스타터 중인지, 판 경과 시간, 체력·기력 %, 파괴율 / 둥지: 본·산 노드 수, 머문 시간 / 그 밖: 없음
        /// </summary>
        public static void ExitTick(GameState state, BattleController battle, float runSeconds)
        {
            if (state == exitStateFor && Time.unscaledTime - exitStateAt < 1f) return;
            exitStateFor = state;
            exitStateAt = Time.unscaledTime;
            var p = new EventParams();
            if (state is GameState.Stage or GameState.Bonus && battle != null)
            {
                p.Add(AnalyticsParams.InStarter, battle.InStarter)
                 .Add(AnalyticsParams.RunSec, Mathf.RoundToInt(runSeconds))
                 .Add(AnalyticsParams.DestroyPct, battle.Buildings.Count > 0 ? Mathf.RoundToInt(100f * battle.DestroyedCount / battle.Buildings.Count) : 0);
                var vitals = battle.Dragon != null ? battle.Dragon.Vitals : null;
                if (vitals != null)
                    p.Add(AnalyticsParams.HpPct, Pct(vitals.Hp, vitals.MaxHp))
                     .Add(AnalyticsParams.StaminaPct, Pct(vitals.Stamina, vitals.MaxStamina));
            }
            else if (state == GameState.OutGame)
            {
                p.Add(AnalyticsParams.NodesViewed, nodesViewedInTree)
                 .Add(AnalyticsParams.NodesBought, nodesBoughtInTree)
                 .Add(AnalyticsParams.OutgameSec, Mathf.RoundToInt(Time.unscaledTime - outgameEnterTime));
            }
            DwhAnalytics.SetExitState(p);
        }

        static int Pct(double value, double max) => max > 0 ? Mathf.RoundToInt((float)(100 * value / max)) : 0;

        /// <summary>화면 전환. 브라우저가 이탈(page_exit) 때 이 화면을 보낸다.</summary>
        public static void SceneChanged(GameState state)
        {
            DwhAnalytics.SetScene(state switch
            {
                GameState.OutGame => AnalyticsValues.SceneOutGame,
                GameState.Stage => AnalyticsValues.SceneStage,
                GameState.Bonus => AnalyticsValues.SceneBonus,
                GameState.Result => AnalyticsValues.SceneResult,
                _ => AnalyticsValues.SceneTitle,
            });
        }

        // ───── 타이틀 ─────

        /// <summary>타이틀이 보이기 시작했다 (title_start의 duration_sec·misclicks 기준)</summary>
        public static void TitleShown()
        {
            titleShownTime = Time.unscaledTime;
            titleMisclicks = 0;
        }

        /// <summary>타이틀에서 버튼이 아닌 곳을 눌렀다</summary>
        public static void TitleMisclick() => titleMisclicks++;

        /// <summary>title_start: 새 게임·이어하기를 눌렀다</summary>
        public static void TitleStart(GameDatabase db, SaveData save)
        {
            Context(db, save);
            Log(AnalyticsEvents.TitleStart, new EventParams()
                .Add(AnalyticsParams.DurationSec, Mathf.RoundToInt(Time.unscaledTime - titleShownTime))
                .Add(AnalyticsParams.Misclicks, titleMisclicks));
        }

        // ───── 아웃게임(트리) ─────

        /// <summary>아웃게임(트리)에 들어왔다. outgame_exit의 체류 시간·구매 수·본 노드 수 기준</summary>
        public static void OutgameEnter()
        {
            outgameEnterTime = Time.unscaledTime;
            nodesBoughtInTree = 0;
            nodesViewedInTree = 0;
        }

        /// <summary>트리에서 노드를 골라 상세를 봤다 (outgame_exit nodes_viewed)</summary>
        public static void NodeViewed() => nodesViewedInTree++;

        /// <summary>outgame_exit: 트리에서 출격</summary>
        public static void OutgameExit(GameDatabase db, SaveData save, int affordableNodes)
        {
            Context(db, save);
            Log(AnalyticsEvents.OutgameExit, new EventParams()
                .Add(AnalyticsParams.DurationSec, Mathf.RoundToInt(Time.unscaledTime - outgameEnterTime))
                .Add(AnalyticsParams.NodesBought, nodesBoughtInTree)
                .Add(AnalyticsParams.GoldHeld, save.goldCoin)
                .Add(AnalyticsParams.AffordableNodes, affordableNodes)
                .Add(AnalyticsParams.NodesViewed, nodesViewedInTree));
        }

        /// <summary>upgrade_purchase: 트리 노드 구매 (여러 레벨을 한 번에 사면 한 번 보낸다)</summary>
        public static void UpgradePurchase(GameDatabase db, SaveData save, UpgradeRow node, int levels, double cost)
        {
            Context(db, save);
            nodesBoughtInTree += levels;
            Log(AnalyticsEvents.UpgradePurchase, new EventParams()
                .Add(AnalyticsParams.NodeId, node.upgradeId)
                .Add(AnalyticsParams.Cost, cost)
                .Add(AnalyticsParams.GoldHeld, save.goldCoin));
        }

        // ───── 판 ─────

        /// <summary>run_start: 출격 (스타터 진입). 판 동안 입력 수·프레임을 새로 센다</summary>
        public static void RunStart(GameDatabase db, SaveData save, bool retry)
        {
            Context(db, save);
            runInputs = 0;
            fpsFrames = 0;
            fpsSeconds = 0;
            int attempt = DwhAnalytics.IncrementCounter(AttemptKey(db, save.currentStage));
            if (Rebirth.IsUnlocked(db, save)) DwhAnalytics.IncrementCounter("since_unlock");
            Log(AnalyticsEvents.RunStart, new EventParams()
                .Add(AnalyticsParams.AttemptNo, attempt)
                .Add(AnalyticsParams.Entry, retry ? AnalyticsValues.EntryRetryNow : AnalyticsValues.EntryDepart)
                .Add(AnalyticsParams.GoldHeld, save.goldCoin)
                .Add(AnalyticsParams.NodesOwned, save.nodeLevels.Count(n => n.level > 0)));
        }

        /// <summary>전투 중 매 프레임 (GameManager). 마우스·키 입력 수와, 탭이 보이는 동안의 프레임을 센다</summary>
        public static void RunTick()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) runInputs++;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) runInputs++;
            if (Application.isFocused && Time.timeScale > 0)
            {
                fpsFrames++;
                fpsSeconds += Time.unscaledDeltaTime;
            }
        }

        /// <summary>starter_end: 스타터 판정이 끝났다 (중간에 그만두면 보내지 않는다)</summary>
        public static void StarterEnd(GameDatabase db, SaveData save, StarterGrade grade, double damage, float seconds)
        {
            Context(db, save);
            Log(AnalyticsEvents.StarterEnd, new EventParams()
                .Add(AnalyticsParams.GaugeResult, grade.ToString().ToLowerInvariant())
                .Add(AnalyticsParams.Damage, damage)
                .Add(AnalyticsParams.StarterSec, Mathf.RoundToInt(seconds)));
        }

        /// <summary>bonus_start: 보너스 스테이지 진입</summary>
        public static void BonusStart(GameDatabase db, SaveData save)
        {
            Context(db, save);
            Log(AnalyticsEvents.BonusStart, new EventParams());
        }

        /// <summary>run_end: 결과 정산 표시. stage_id는 이번 판 스테이지</summary>
        public static void RunEnd(GameDatabase db, SaveData save, SessionResult r, int affordableNodes, bool mouseGuide)
        {
            Context(db, save);
            Log(AnalyticsEvents.RunEnd, new EventParams()
                .Add(AnalyticsParams.StageId, db.StageLabel(r.stageId))
                .Add(AnalyticsParams.Result, r.end switch
                {
                    SessionEnd.Cleared => AnalyticsValues.ResultClear,
                    SessionEnd.Defeated => AnalyticsValues.ResultFailHp,
                    SessionEnd.Retired => AnalyticsValues.ResultRetired,
                    _ => AnalyticsValues.ResultFailStamina,
                })
                .Add(AnalyticsParams.DurationSec, Mathf.RoundToInt(r.playSeconds))
                .Add(AnalyticsParams.DestroyPct, r.totalBuildings > 0 ? Mathf.RoundToInt(100f * r.destroyedBuildings / r.totalBuildings) : 0)
                .Add(AnalyticsParams.GoldEarned, r.earnedGold)
                .Add(AnalyticsParams.HpLeft, Mathf.RoundToInt((float)r.hpLeft))
                .Add(AnalyticsParams.StaminaLeft, Mathf.RoundToInt((float)r.staminaLeft))
                .Add(AnalyticsParams.BonusEntered, r.bonusEntered)
                .Add(AnalyticsParams.Inputs, runInputs)
                .Add(AnalyticsParams.AvgFps, fpsSeconds > 0 ? Mathf.RoundToInt(fpsFrames / fpsSeconds) : 0)
                .Add(AnalyticsParams.KilledBy, r.killedBy ?? "")
                .Add(AnalyticsParams.BonusGold, r.bonusGold)
                .Add(AnalyticsParams.BonusSec, Mathf.RoundToInt(r.bonusSeconds))
                .Add(AnalyticsParams.AffordableNodes, affordableNodes)
                .Add(AnalyticsParams.GuideMode, mouseGuide ? AnalyticsValues.GuideMouse : AnalyticsValues.GuideAuto));
        }

        // ───── 튜토리얼 ─────

        /// <summary>튜토리얼 쪽이 보이기 시작했다 (tutorial_step duration_sec 기준)</summary>
        public static void TutorialPageShown() => tutorialPageTime = Time.unscaledTime;

        /// <summary>
        /// tutorial_step: 튜토리얼 한 쪽을 넘겼다(done) 또는 SKIP했다(skip). step_index는 새 게임부터 센 순번,
        /// step_id는 튜토리얼 종류 + 쪽 (예: ingame_2)
        /// </summary>
        public static void TutorialStep(GameDatabase db, SaveData save, string stepId, bool skipped)
        {
            Context(db, save);
            tutorialStepIndex++;
            Log(AnalyticsEvents.TutorialStep, new EventParams()
                .Add(AnalyticsParams.StepIndex, tutorialStepIndex)
                .Add(AnalyticsParams.StepId, stepId)
                .Add(AnalyticsParams.Action, skipped ? AnalyticsValues.ActionSkip : AnalyticsValues.ActionDone)
                .Add(AnalyticsParams.DurationSec, Mathf.RoundToInt(Time.unscaledTime - tutorialPageTime)));
            tutorialPageTime = Time.unscaledTime;
        }

        /// <summary>새 게임: 튜토리얼 순번을 처음부터</summary>
        public static void TutorialReset() => tutorialStepIndex = 0;

        // ───── 환생 ─────

        /// <summary>rebirth_view open: 환생 탭을 열었다</summary>
        public static void RebirthViewOpen(GameDatabase db, SaveData save, double pointsIfNow)
        {
            Context(db, save);
            rebirthViewOpen = true;
            Log(AnalyticsEvents.RebirthView, new EventParams()
                .Add(AnalyticsParams.Action, AnalyticsValues.ActionOpen)
                .Add(AnalyticsParams.PointsGained, pointsIfNow));
        }

        /// <summary>rebirth_view cancel: 환생하지 않고 환생 탭을 떠났다 (환생하면 보내지 않는다)</summary>
        public static void RebirthViewClosed(GameDatabase db, SaveData save, double pointsIfNow)
        {
            if (!rebirthViewOpen) return;
            rebirthViewOpen = false;
            Context(db, save);
            Log(AnalyticsEvents.RebirthView, new EventParams()
                .Add(AnalyticsParams.Action, AnalyticsValues.ActionCancel)
                .Add(AnalyticsParams.PointsGained, pointsIfNow));
        }

        /// <summary>rebirth_unlocked: 첫 환생 벽(3-2) 도달. 첫 환생 뒤에는 늘 열려 있어 다시 보내지 않는다</summary>
        public static void RebirthUnlocked(GameDatabase db, SaveData save)
        {
            Context(db, save);
            string wall = Rebirth.NextWall(save);
            DwhPrefs.SetFloat(UnlockTimeKey, DwhAnalytics.TotalPlaytimeSec);
            DwhAnalytics.ResetCounter("since_unlock");
            Log(AnalyticsEvents.RebirthUnlocked, new EventParams()
                .Add(AnalyticsParams.Wall, wall != null ? db.StageLabel(wall) : "")
                .Add(AnalyticsParams.AttemptsAtWall, wall != null ? DwhAnalytics.GetCounter(AttemptKey(db, wall)) : 0));
        }

        /// <summary>rebirth: 환생 실행. trait_chosen = 환생 직전까지 가장 많이 올린 특성 (rebirth_no는 공통 rebirth_count와 겹쳐 뺐다)</summary>
        public static void RebirthPerformed(GameDatabase db, SaveData save, double points)
        {
            Context(db, save);
            rebirthViewOpen = false;
            var trait = save.traitLevels.OrderByDescending(t => t.level).FirstOrDefault(t => t.level > 0);
            Log(AnalyticsEvents.Rebirth, new EventParams()
                .Add(AnalyticsParams.PointsGained, points)
                .Add(AnalyticsParams.TraitChosen, trait?.key ?? "")
                .Add(AnalyticsParams.AttemptsSinceUnlock, DwhAnalytics.GetCounter("since_unlock"))
                .Add(AnalyticsParams.TimeSinceUnlockSec, Mathf.RoundToInt(DwhAnalytics.TotalPlaytimeSec - DwhPrefs.GetFloat(UnlockTimeKey, DwhAnalytics.TotalPlaytimeSec))));
            DwhAnalytics.SetUserProperty(AnalyticsParams.HasRebirth, "1");
        }

        /// <summary>game_complete: 마지막 스테이지(7-2) 클리어</summary>
        public static void GameComplete(GameDatabase db, SaveData save)
        {
            Context(db, save);
            Log(AnalyticsEvents.GameComplete, new EventParams()
                .Add(AnalyticsParams.TotalPlaytimeSec, Mathf.RoundToInt(DwhAnalytics.TotalPlaytimeSec))
                .Add(AnalyticsParams.TotalRuns, save.playCount));
        }

        // ───── 오류 ─────

        /// <summary>
        /// client_error (Unity 쪽): 예외·저장 실패. 같은 오류는 한 번, 세션당 MaxErrorsPerSession번까지.
        /// message는 GA4에 등록하지 않는 자유 문장이라 100자로 자른다(DebugView·BigQuery에서만 본다)
        /// </summary>
        public static void ClientError(string errorType, string message)
        {
            string key = errorType + "|" + message;
            if (sentErrors.Count >= MaxErrorsPerSession || !sentErrors.Add(key)) return;
            Log(AnalyticsEvents.ClientError, new EventParams()
                .Add(AnalyticsParams.ErrorType, errorType)
                .Add(AnalyticsParams.Scene, DwhAnalytics.Scene)
                .Add(AnalyticsParams.Message, message != null && message.Length > 100 ? message.Substring(0, 100) : message ?? ""));
        }

        /// <summary>Unity 예외를 client_error로 보낸다 (실제 실행에서 한 번 건다)</summary>
        public static void WatchExceptions()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception) ClientError(AnalyticsValues.ErrorUnityException, condition);
        }
    }
}
