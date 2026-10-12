using System;
using System.Linq;
using DWH.Core;
using DWH.Data;

namespace DWH.OutGame
{
    public enum NodeStatus
    {
        /// <summary>선행 노드가 1레벨 이상이 아니다</summary>
        Locked,
        /// <summary>최대 레벨</summary>
        Maxed,
        Affordable,
        TooExpensive,
    }

    /// <summary>
    /// 업그레이드 트리 28노드 (upgrade, 가이드 6.7).
    /// - 해금: prereq_id_1, prereq_id_2가 모두 1레벨 이상. 최대 레벨 max_level(0 = 무제한).
    /// - 비용: Formulas.UpgradeCost에 용의 지혜 감소율을 곱하고 올림.
    /// - 구매하면 골드 코인을 쓰고, 성장 단계 기준인 이번 회차 누적 사용 골드를 늘린다.
    /// </summary>
    public class UpgradeTree
    {
        readonly GameDatabase db;
        readonly SaveData save;

        public UpgradeTree(GameDatabase database, SaveData saveData)
        {
            db = database;
            save = saveData;
        }

        public int Level(UpgradeRow node) => save.GetNodeLevel(node.upgradeId);
        public int Level(string upgradeId) => save.GetNodeLevel(upgradeId);

        /// <summary>현재 레벨 → 다음 레벨 비용</summary>
        public double Cost(UpgradeRow node) => CostAt(node, Level(node) + 1);

        double CostAt(UpgradeRow node, int level) => Formulas.UpgradeCost(node, level, Rebirth.Discount(db, save));

        public bool IsUnlocked(UpgradeRow node) => node.Prerequisites().All(p => Level(p) >= 1);

        public bool IsMaxed(UpgradeRow node) => node.maxLevel > 0 && Level(node) >= node.maxLevel;

        public NodeStatus Status(UpgradeRow node)
        {
            if (!IsUnlocked(node)) return NodeStatus.Locked;
            if (IsMaxed(node)) return NodeStatus.Maxed;
            return save.goldCoin >= Cost(node) ? NodeStatus.Affordable : NodeStatus.TooExpensive;
        }

        public bool TryBuy(UpgradeRow node)
        {
            if (Status(node) != NodeStatus.Affordable)
                return false;
            double cost = Cost(node);
            save.goldCoin -= cost;
            save.runSpentGold += cost;
            save.lifetimeSpentGold += cost;
            save.SetNodeLevel(node.upgradeId, Level(node) + 1);
            return true;
        }

        /// <summary>구매 수량 "최대"를 나타내는 값</summary>
        public const int MaxQuantity = int.MaxValue;

        /// <summary>
        /// quantity만큼(최대 레벨·보유 골드 안에서) 살 때의 레벨 수와 총비용. 한 레벨도 못 사면 다음 1레벨의 비용을 돌려준다.
        /// </summary>
        public (int levels, double cost) Quote(UpgradeRow node, int quantity)
        {
            int level = Level(node);
            int levels = 0;
            double total = 0;
            while (levels < quantity && (node.maxLevel <= 0 || level + levels < node.maxLevel))
            {
                double next = CostAt(node, level + levels + 1);
                if (total + next > save.goldCoin)
                {
                    if (levels == 0) total = next;
                    break;
                }
                total += next;
                levels++;
            }
            return (levels, total);
        }

        /// <summary>quantity만큼 산다. 산 레벨 수를 돌려준다</summary>
        public int BuyMany(UpgradeRow node, int quantity)
        {
            int bought = 0;
            while (bought < quantity && TryBuy(node)) bought++;
            return bought;
        }

        public PlayerStats Stats() => PlayerStats.Compute(db, Level, save.GetTraitLevel, save.runSpentGold);

        /// <summary>노드 효과 n의 레벨 값 (UI 표시용). 0레벨이면 0</summary>
        public static double EffectValue(UpgradeRow node, int effectIndex, int level) => effectIndex == 1
            ? EffectMath.LevelValue(node.baseValue1, node.addValue1, level)
            : EffectMath.LevelValue(node.baseValue2, node.addValue2, level);

        public int AffordableCount() => db.upgrades.Count(n => Status(n) == NodeStatus.Affordable);

        public static Func<string, int> LevelsOf(SaveData save) => save.GetNodeLevel;
    }
}
