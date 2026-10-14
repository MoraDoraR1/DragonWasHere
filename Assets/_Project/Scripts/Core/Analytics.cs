using System.Collections.Generic;
using System.Linq;
using DWH.Analytics;
using DWH.Data;
using DWH.OutGame;
using UnityEngine;

namespace DWH.Core
{
    /// <summary>
    /// 지표 수집 P0 이벤트 8개 (가이드 8장, 공유 계획 페이지의 이벤트 명세). 게임은 여기만 부른다.
    /// 전송은 수집 묶음(Assets/DWHAnalytics, DwhAnalytics)이 맡는다: WebGL은 jslib → Firebase JS SDK → GA4, 에디터는 콘솔.
    /// 공통 파라미터(stage_id, rebirth_count, growth_stage, session_index, build_version, is_dev)는 DwhAnalytics가 붙인다.
    /// </summary>
    public static class Analytics
    {
        /// <summary>보낸 이벤트 이름 (테스트용, 최근 50개)</summary>
        public static readonly List<string> Recent = new();

        const string UnlockTimeKey = "dwh.rebirth_unlock_time";
        static float outgameEnterTime;
        static int nodesBoughtInTree;

        static void Context(GameDatabase db, SaveData save)
        {
            DwhAnalytics.StageId = db.StageLabel(save.currentStage);
            DwhAnalytics.RebirthCount = save.rebirthCount;
            DwhAnalytics.GrowthStage = (int)Growth.Stage(db, save.runSpentGold).growthStage;
        }

        static void Log(string name, EventParams p)
        {
            DwhAnalytics.Log(name, p);
            Recent.Add(name);
            if (Recent.Count > 50) Recent.RemoveAt(0);
        }

        static string AttemptKey(GameDatabase db, string stageId) => DwhAnalytics.AttemptKey(db.StageLabel(stageId));

        /// <summary>game_session_start: 세이브를 읽은 직후 한 번</summary>
        public static void SessionStart(GameDatabase db, SaveData save)
        {
            Context(db, save);
            DwhAnalytics.StartSession(db.StageLabel(save.highestStage));
            Recent.Add(AnalyticsEvents.GameSessionStart);
        }

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

        /// <summary>아웃게임(트리)에 들어왔다. outgame_exit의 체류 시간·구매 수 기준</summary>
        public static void OutgameEnter()
        {
            outgameEnterTime = Time.unscaledTime;
            nodesBoughtInTree = 0;
        }

        /// <summary>outgame_exit: 트리에서 출격</summary>
        public static void OutgameExit(GameDatabase db, SaveData save, int affordableNodes)
        {
            Context(db, save);
            Log(AnalyticsEvents.OutgameExit, new EventParams()
                .Add(AnalyticsParams.TimeInTreeSec, Mathf.RoundToInt(Time.unscaledTime - outgameEnterTime))
                .Add(AnalyticsParams.NodesBought, nodesBoughtInTree)
                .Add(AnalyticsParams.GoldLeft, save.goldCoin)
                .Add(AnalyticsParams.AffordableNodes, affordableNodes));
        }

        /// <summary>run_start: 출격 (스타터 진입)</summary>
        public static void RunStart(GameDatabase db, SaveData save, bool retry)
        {
            Context(db, save);
            int attempt = DwhAnalytics.IncrementCounter(AttemptKey(db, save.currentStage));
            if (Rebirth.IsUnlocked(db, save)) DwhAnalytics.IncrementCounter("since_unlock");
            Log(AnalyticsEvents.RunStart, new EventParams()
                .Add(AnalyticsParams.AttemptNo, attempt)
                .Add(AnalyticsParams.Entry, retry ? AnalyticsValues.EntryRetryNow : AnalyticsValues.EntryDepart)
                .Add(AnalyticsParams.GoldHeld, save.goldCoin)
                .Add(AnalyticsParams.NodesOwned, save.nodeLevels.Count(n => n.level > 0)));
        }

        /// <summary>run_end: 결과 정산 표시. stage_id는 이번 판 스테이지</summary>
        public static void RunEnd(GameDatabase db, SaveData save, SessionResult r)
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
                .Add(AnalyticsParams.BonusEntered, r.bonusEntered));
        }

        /// <summary>upgrade_purchase: 트리 노드 구매 (여러 레벨을 한 번에 사면 한 번 보낸다)</summary>
        public static void UpgradePurchase(GameDatabase db, SaveData save, UpgradeRow node, int levels, double cost)
        {
            Context(db, save);
            nodesBoughtInTree += levels;
            Log(AnalyticsEvents.UpgradePurchase, new EventParams()
                .Add(AnalyticsParams.NodeId, node.upgradeId)
                .Add(AnalyticsParams.Cost, cost)
                .Add(AnalyticsParams.GoldAfter, save.goldCoin));
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

        /// <summary>rebirth: 환생 실행. trait_chosen = 환생 직전까지 가장 많이 올린 특성</summary>
        public static void RebirthPerformed(GameDatabase db, SaveData save, double points)
        {
            Context(db, save);
            var trait = save.traitLevels.OrderByDescending(t => t.level).FirstOrDefault(t => t.level > 0);
            Log(AnalyticsEvents.Rebirth, new EventParams()
                .Add(AnalyticsParams.RebirthNo, save.rebirthCount)
                .Add(AnalyticsParams.PointsGained, points)
                .Add(AnalyticsParams.TraitChosen, trait?.key ?? "")
                .Add(AnalyticsParams.AttemptsSinceUnlock, DwhAnalytics.GetCounter("since_unlock"))
                .Add(AnalyticsParams.TimeSinceUnlockSec, Mathf.RoundToInt(DwhAnalytics.TotalPlaytimeSec - DwhPrefs.GetFloat(UnlockTimeKey, DwhAnalytics.TotalPlaytimeSec))));
        }

        /// <summary>game_complete: 마지막 스테이지(7-2) 클리어</summary>
        public static void GameComplete(GameDatabase db, SaveData save)
        {
            Context(db, save);
            Log(AnalyticsEvents.GameComplete, new EventParams()
                .Add(AnalyticsParams.TotalPlaytimeSec, Mathf.RoundToInt(DwhAnalytics.TotalPlaytimeSec))
                .Add(AnalyticsParams.TotalRuns, save.playCount));
        }
    }
}
