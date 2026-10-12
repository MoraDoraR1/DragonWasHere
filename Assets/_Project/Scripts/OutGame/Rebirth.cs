using System;
using System.Linq;
using DWH.Core;
using DWH.Data;

namespace DWH.OutGame
{
    /// <summary>
    /// 환생 (가이드 1장 환생 벽, 6.10).
    /// - 해금: n번째 환생은 n번째 벽(3-2, 5-2)에 도달하면 열린다. 최대 = 벽 수(2회).
    /// - 포인트 = floor(√(이번 회차 누적 획득 골드) ÷ const.rebirth_rp_divisor).
    /// - 초기화: 업그레이드 레벨, 골드, 진행(1-1부터), 성장 단계(이번 회차 누적 사용 골드).
    /// - 유지: 환생 포인트, 환생 특성, 액티브 습득 단계, 환생 횟수, 최고 스테이지.
    /// </summary>
    public static class Rebirth
    {
        public static int MaxRebirths => Tuning.RebirthWalls.Length;

        /// <summary>다음 환생을 여는 벽 스테이지. 더 환생할 수 없으면 null</summary>
        public static string NextWall(SaveData save) => save.rebirthCount < MaxRebirths ? Tuning.RebirthWalls[save.rebirthCount] : null;

        public static bool IsUnlocked(GameDatabase db, SaveData save)
        {
            string wall = NextWall(save);
            return wall != null && db.OrderIndex(save.currentStage) >= db.OrderIndex(wall);
        }

        public static double PointsIfRebirthNow(GameDatabase db, SaveData save) =>
            Formulas.RebirthPoints(save.runEarnedGold, db.Const("rebirth_rp_divisor"));

        /// <summary>환생을 실행하고 받은 포인트를 돌려준다. 해금 전이면 0</summary>
        public static double Perform(GameDatabase db, SaveData save)
        {
            if (!IsUnlocked(db, save)) return 0;
            double points = PointsIfRebirthNow(db, save);
            save.rebirthPoint += points;
            save.rebirthCount++;
            save.nodeLevels.Clear();
            save.clearedStages.Clear();
            save.currentStage = db.StageOrder[0];
            save.runEarnedGold = 0;
            save.runSpentGold = 0;
            save.gameCompleted = false;

            var effects = TraitEffects(db, save);
            // 고룡의 유산: 환생 직후 골드 (효과 33)
            save.goldCoin = Formulas.Round(effects.Get(EffectType.RebirthStartGold));
            // 자동 각성: 패시브 노드 v개를 1레벨로 해금 (파이어 브레스 → 파이어 볼) (효과 35)
            int auto = (int)Math.Floor(effects.Get(EffectType.AutoUnlockSkills));
            foreach (var node in AutoUnlockOrder(db).Take(auto))
                save.SetNodeLevel(node.upgradeId, Math.Max(1, save.GetNodeLevel(node.upgradeId)));
            return points;
        }

        /// <summary>자동 각성 순서: 파이어 브레스 → 파이어 볼 [임시] (가이드 6.7 효과 35)</summary>
        public static UpgradeRow[] AutoUnlockOrder(GameDatabase db) =>
            new[] { db.UpgradeByEffect(EffectType.BreathDamage), db.UpgradeByEffect(EffectType.FireBallDamage) }.Where(n => n != null).ToArray();

        // ───── 환생 특성 ─────

        public static EffectTotals TraitEffects(GameDatabase db, SaveData save) =>
            PlayerStats.Effects(db, _ => 0, save.GetTraitLevel);

        public static int TraitLevel(SaveData save, RebirthTraitRow row) => save.GetTraitLevel(row.traitId);

        /// <summary>다음 레벨 비용 = base + add × (n - 1)</summary>
        public static double TraitCost(RebirthTraitRow row, int currentLevel) => Formulas.LinearCost(row.baseCost, row.addCost, currentLevel + 1);

        public static bool TraitMaxed(RebirthTraitRow row, int level) => level >= row.maxLevel;

        public static bool BuyTrait(GameDatabase db, SaveData save, string traitId)
        {
            var row = db.Trait(traitId);
            int level = TraitLevel(save, row);
            double cost = TraitCost(row, level);
            if (TraitMaxed(row, level) || save.rebirthPoint < cost) return false;
            save.rebirthPoint -= cost;
            save.SetTraitLevel(row.traitId, level + 1);
            return true;
        }

        /// <summary>용의 지혜: 업그레이드 비용 감소율 (효과 34)</summary>
        public static double Discount(GameDatabase db, SaveData save) =>
            Math.Min(0.9, TraitEffects(db, save).Get(EffectType.UpgradeDiscount));

        // ───── 액티브 스킬 ─────

        public static int ActiveLevel(SaveData save, ActiveSkillRow row) => save.GetActiveLevel(row.skillId);

        /// <summary>습득·강화 조건: prereq_upgrade_id 노드가 1레벨 이상</summary>
        public static bool ActivePrereqMet(SaveData save, ActiveSkillRow row) => save.GetNodeLevel(row.prereqUpgradeId) >= 1;

        /// <summary>다음 단계 비용 (습득 = 1단계) = base + add × (n - 1)</summary>
        public static double ActiveCost(ActiveSkillRow row, int currentLevel) => Formulas.LinearCost(row.baseCost, row.addCost, currentLevel + 1);

        public static bool CanBuyActive(SaveData save, ActiveSkillRow row)
        {
            int level = ActiveLevel(save, row);
            return level < row.maxLevel && ActivePrereqMet(save, row) && save.rebirthPoint >= ActiveCost(row, level);
        }

        public static bool BuyActive(GameDatabase db, SaveData save, ActiveSkillId id)
        {
            var row = db.Active(id);
            if (!CanBuyActive(save, row)) return false;
            int level = ActiveLevel(save, row);
            save.rebirthPoint -= ActiveCost(row, level);
            save.SetActiveLevel(row.skillId, level + 1);
            return true;
        }
    }
}
