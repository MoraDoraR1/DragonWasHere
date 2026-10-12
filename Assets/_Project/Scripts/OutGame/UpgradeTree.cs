using System;
using System.Linq;
using DWH.Core;
using DWH.Data;

namespace DWH.OutGame
{
    public enum NodeStatus
    {
        /// <summary>효과를 낼 시스템이 아직 없다 (예: 패시브 스킬은 M6 이후)</summary>
        Unavailable,
        /// <summary>선행 노드가 1레벨 이상이 아니다</summary>
        Locked,
        /// <summary>최대 레벨</summary>
        Maxed,
        Affordable,
        TooExpensive,
    }

    /// <summary>
    /// 업그레이드 트리 (4.8). 비용 = ceil(초기 비용 × 배수^현재레벨). 선행 노드가 모두 1레벨 이상이어야 해금된다.
    /// 레벨은 SaveData에 저장하고, 저장이 없으면 노드의 초기 레벨(몸통 박치기는 기본 보유라 1)을 쓴다.
    /// 구매하면 골드 코인을 쓰고, 성장 단계 기준인 누적 사용 골드(4.1.4)를 늘린다.
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

        public UpgradeNodeRow Node(string name) => db.upgradeNodes.rows.First(n => n.node == name);

        public int Level(UpgradeNodeRow node)
        {
            var entry = save.nodeLevels.Find(e => e.key == node.node);
            return entry?.level ?? node.initialLevel;
        }

        public double Cost(UpgradeNodeRow node) => CostAt(node, Level(node));

        /// <summary>현재레벨 → 다음 레벨 비용. 용의 지혜(환생 특성)만큼 싸진다 (올림)</summary>
        double CostAt(UpgradeNodeRow node, int level)
        {
            double cost = Formulas.UpgradeCost(node.initialCost, node.costMultiplier, level);
            double discount = Rebirth.Discount(db, save);
            return discount > 0 ? Math.Ceiling(cost * (1 - discount) - 1e-9) : cost;
        }

        public bool IsUnlocked(UpgradeNodeRow node) =>
            node.prerequisites.All(p => Level(Node(p)) >= 1);

        public bool IsMaxed(UpgradeNodeRow node) => node.maxLevel > 0 && Level(node) >= node.maxLevel;

        public NodeStatus Status(UpgradeNodeRow node)
        {
            if (!node.available) return NodeStatus.Unavailable;
            if (!IsUnlocked(node)) return NodeStatus.Locked;
            if (IsMaxed(node)) return NodeStatus.Maxed;
            return save.goldCoin >= Cost(node) ? NodeStatus.Affordable : NodeStatus.TooExpensive;
        }

        public bool TryBuy(UpgradeNodeRow node)
        {
            if (Status(node) != NodeStatus.Affordable)
                return false;
            double cost = Cost(node);
            save.goldCoin -= cost;
            save.lifetimeSpentGold += cost;
            save.SetNodeLevel(node.node, Level(node) + 1);
            return true;
        }

        /// <summary>구매 수량 "최대"를 나타내는 값</summary>
        public const int MaxQuantity = int.MaxValue;

        /// <summary>
        /// quantity만큼(최대 레벨·보유 골드 안에서) 살 때의 레벨 수와 총비용. 한 레벨도 못 사면 다음 1레벨의 비용을 돌려준다.
        /// </summary>
        public (int levels, double cost) Quote(UpgradeNodeRow node, int quantity)
        {
            int level = Level(node);
            int levels = 0;
            double total = 0;
            while (levels < quantity && (node.maxLevel <= 0 || level + levels < node.maxLevel))
            {
                double next = CostAt(node, level + levels);
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
        public int BuyMany(UpgradeNodeRow node, int quantity)
        {
            int bought = 0;
            while (bought < quantity && TryBuy(node)) bought++;
            return bought;
        }

        public PlayerStats Stats()
        {
            var stats = PlayerStats.Compute(db, Level, t => save.GetTraitLevel(t.ToString()));
            Growth.Apply(db, stats, save.lifetimeSpentGold);
            return stats;
        }
    }
}
