using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;

namespace DWH.OutGame
{
    /// <summary>
    /// 환생 (4.9, 아웃게임 업그레이드 기획서 Ver01).
    /// - 1-4(원시 보스)를 클리어한 적이 있으면 할 수 있다.
    /// - 포인트 = floor(sqrt(이번 회차 누적 획득 골드) / 10).
    /// - 초기화: 업그레이드 트리, 보유 골드, 스테이지 진행(1-1부터). 유지: 환생 포인트, 환생 특성, 액티브 스킬, 누적 사용 골드, 최고 기록.
    /// - 환생 포인트로 환생 특성(영구 강화 6종)과 액티브 스킬(습득·강화)을 산다.
    /// </summary>
    public static class Rebirth
    {
        public const int MaxActiveLevel = 5;

        public static bool IsUnlocked(GameDatabase db, SaveData save, IReadOnlyList<string> stageOrder)
        {
            if (save.rebirthCount > 0) return true;
            int unlock = IndexOf(stageOrder, db.balance.rebirthUnlockStage);
            return unlock >= 0 && IndexOf(stageOrder, save.highestStage) >= unlock;
        }

        public static double PointsIfRebirthNow(GameDatabase db, SaveData save) =>
            Formulas.RebirthPoints(save.runEarnedGold, db.balance.rebirthPointDivisor);

        /// <summary>환생을 실행하고 받은 포인트를 돌려준다. 해금 전이면 0</summary>
        public static double Perform(GameDatabase db, SaveData save, IReadOnlyList<string> stageOrder)
        {
            if (!IsUnlocked(db, save, stageOrder)) return 0;
            double points = PointsIfRebirthNow(db, save);
            save.rebirthPoint += points;
            save.rebirthCount++;
            save.nodeLevels.Clear();
            save.clearedStages.Clear();
            save.currentStage = stageOrder[0];
            save.runEarnedGold = 0;
            save.goldCoin = StartGold(db, save);

            // 자동 각성: 패시브 스킬을 해금한 채 시작한다 (1레벨 파이어 브레스, 2레벨 회전 회오리까지)
            int auto = TraitLevel(save, RebirthTraitId.AutoAwakening);
            if (auto >= 1) save.SetNodeLevel("Skill_FireBreath", 1);
            if (auto >= 2) save.SetNodeLevel("Skill_Whirlwind", 1);
            return points;
        }

        // ───── 환생 특성 ─────

        public static int TraitLevel(SaveData save, RebirthTraitId id) => save.GetTraitLevel(id.ToString());

        /// <summary>다음 레벨 비용 = 첫 비용 + 추가 비용 × 현재 레벨</summary>
        public static double TraitCost(RebirthTraitRow row, int currentLevel) => row.firstCost + row.costStep * currentLevel;

        public static bool TraitMaxed(RebirthTraitRow row, int level) => level >= row.maxLevel;

        public static bool BuyTrait(GameDatabase db, SaveData save, RebirthTraitId id)
        {
            var row = db.Trait(id);
            int level = TraitLevel(save, id);
            double cost = TraitCost(row, level);
            if (TraitMaxed(row, level) || save.rebirthPoint < cost) return false;
            save.rebirthPoint -= cost;
            save.SetTraitLevel(id.ToString(), level + 1);
            return true;
        }

        /// <summary>특성 효과 합계 (레벨 × 레벨당 효과). 시작 골드는 StartGold</summary>
        public static double TraitTotal(RebirthTraitRow row, int level) => row.effect == RebirthTraitEffect.StartGold
            ? (level <= 0 ? 0 : row.effectValue * Math.Pow(row.effectGrowth, level - 1))
            : row.effectValue * level;

        public static double StartGold(GameDatabase db, SaveData save)
        {
            var row = db.rebirthTraits.rows.FirstOrDefault(t => t.effect == RebirthTraitEffect.StartGold);
            return row == null ? 0 : Formulas.Round(TraitTotal(row, TraitLevel(save, row.trait)));
        }

        /// <summary>용의 지혜: 업그레이드 비용 할인율</summary>
        public static double Discount(GameDatabase db, SaveData save)
        {
            if (db.rebirthTraits == null) return 0;
            var row = db.rebirthTraits.rows.FirstOrDefault(t => t.effect == RebirthTraitEffect.UpgradeDiscount);
            return row == null ? 0 : Math.Min(0.9, TraitTotal(row, TraitLevel(save, row.trait)));
        }

        // ───── 액티브 스킬 ─────

        public static int ActiveLevel(SaveData save, ActiveSkillId id) => save.GetActiveLevel(id.ToString());

        /// <summary>다음 단계 비용 (습득 = 1단계). 최대 단계면 0</summary>
        public static double ActiveCost(GameDatabase db, SaveData save, ActiveSkillId id)
        {
            int level = ActiveLevel(save, id);
            return level >= MaxActiveLevel ? 0 : db.Active(id, level + 1).costRp;
        }

        public static bool BuyActive(GameDatabase db, SaveData save, ActiveSkillId id)
        {
            int level = ActiveLevel(save, id);
            if (level >= MaxActiveLevel) return false;
            double cost = db.Active(id, level + 1).costRp;
            if (save.rebirthPoint < cost) return false;
            save.rebirthPoint -= cost;
            save.SetActiveLevel(id.ToString(), level + 1);
            return true;
        }

        static int IndexOf(IReadOnlyList<string> order, string id)
        {
            for (int i = 0; i < order.Count; i++) if (order[i] == id) return i;
            return -1;
        }
    }
}
