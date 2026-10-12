using System.Linq;
using System.Text;
using DWH.Core;
using DWH.Data;
using DWH.Editor.Data;
using DWH.OutGame;
using NUnit.Framework;
using UnityEngine;

namespace DWH.Tests
{
    public class UpgradeTreeTests
    {
        GameDatabase db;
        SaveData save;
        UpgradeTree tree;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);

        [SetUp]
        public void SetUp()
        {
            save = new SaveData { currentStage = "1-1" };
            tree = new UpgradeTree(db, save);
        }

        UpgradeNodeRow N(string name) => tree.Node(name);

        [Test]
        public void Cost_FollowsFormulaAndCurrentLevel()
        {
            Assert.AreEqual(10, tree.Cost(N("DragonAwakening")));
            save.SetNodeLevel("DragonAwakening", 1);
            Assert.AreEqual(12, tree.Cost(N("DragonAwakening")));   // ceil(11.5)
            Assert.AreEqual(60, tree.Cost(N("Skill_BodySlam")), "몸통 박치기는 기본 1레벨이라 첫 강화가 50 × 1.2");
        }

        [Test]
        public void Status_LockedUntilPrerequisiteLevelOne()
        {
            save.goldCoin = 1000;
            Assert.AreEqual(NodeStatus.Locked, tree.Status(N("StaminaBoost")));
            Assert.IsTrue(tree.TryBuy(N("DragonAwakening")));
            Assert.AreEqual(NodeStatus.Affordable, tree.Status(N("StaminaBoost")));
            Assert.AreEqual(NodeStatus.Locked, tree.Status(N("TreasurePile")), "선행 두 개 모두 필요");
        }

        [Test]
        public void Status_UnavailableNodesCannotBeBought()
        {
            // M6 이후 모든 노드가 구매 가능하다. 시스템이 없는 노드를 흉내 내려고 잠시 막는다
            save.goldCoin = 1e9;
            save.SetNodeLevel("DragonAwakening", 1);
            var node = N("Skill_FireBreath");
            node.available = false;
            try
            {
                Assert.AreEqual(NodeStatus.Unavailable, tree.Status(node));
                Assert.IsFalse(tree.TryBuy(node));
            }
            finally { node.available = true; }
            Assert.AreEqual(NodeStatus.Affordable, tree.Status(node));
            Assert.AreEqual(NodeStatus.Affordable, tree.Status(N("Skill_BodySlam")));
        }

        [Test]
        public void TryBuy_SpendsGoldAndTracksLifetimeSpent()
        {
            save.goldCoin = 25;
            Assert.IsTrue(tree.TryBuy(N("DragonAwakening")));
            Assert.AreEqual(15, save.goldCoin);
            Assert.AreEqual(10, save.lifetimeSpentGold);
            Assert.AreEqual(1, tree.Level(N("DragonAwakening")));
            Assert.IsTrue(tree.TryBuy(N("DragonAwakening")));
            Assert.AreEqual(3, save.goldCoin);
            Assert.AreEqual(NodeStatus.TooExpensive, tree.Status(N("DragonAwakening")));
            Assert.IsFalse(tree.TryBuy(N("DragonAwakening")));
        }

        [Test]
        public void MaxLevel_StopsBuying()
        {
            save.goldCoin = 1e12;
            save.SetNodeLevel("DragonAwakening", 1);
            for (int i = 0; i < 30; i++) tree.TryBuy(N("StaminaBoost"));
            Assert.AreEqual(25, tree.Level(N("StaminaBoost")));
            Assert.AreEqual(NodeStatus.Maxed, tree.Status(N("StaminaBoost")));
        }

        [Test]
        public void Stats_ApplyEachEffectMode()
        {
            save.SetNodeLevel("DragonAwakening", 9);
            save.SetNodeLevel("HpBoost", 4);
            save.SetNodeLevel("StaminaBoost", 3);
            save.SetNodeLevel("FierceDash", 2);
            save.SetNodeLevel("Greed", 2);
            save.SetNodeLevel("CritChance", 5);
            save.SetNodeLevel("CritDamage", 2);
            save.SetNodeLevel("HpRegen", 1);
            save.SetNodeLevel("Skill_BodySlam", 3);
            save.SetTraitLevel("GoldenBlood", 1);
            var s = tree.Stats();

            Assert.AreEqual(20 * System.Math.Pow(1.09, 9), s.AttackPower, 1e-9);   // 8.2 표: 1-2에서 공격력 43
            Assert.AreEqual(43, System.Math.Floor(s.AttackPower));
            Assert.AreEqual(146, System.Math.Floor(s.MaxHp));                        // 8.2 표: 체력 146
            Assert.AreEqual(130, s.MaxStamina, 1e-9);
            Assert.AreEqual(3.3, s.MoveSpeed, 1e-9);
            Assert.AreEqual(1.2 * 1.1, s.GoldMultiplier, 1e-9, "탐욕 2레벨 × 황금 혈통 1레벨");
            Assert.AreEqual(0.05, s.CritChance, 1e-9);
            Assert.AreEqual(1.7, s.CritMultiplier, 1e-9);
            Assert.AreEqual(s.MaxHp * 0.01, s.HpRegenPerSecond, 1e-9);
            Assert.AreEqual(2.0, s.BodySlamMultiplier, 1e-9, "몸통 박치기 3레벨: 100% + 50% × 2");
            Assert.AreEqual(0.1, s.JumpChance, 1e-9);
        }

        [Test]
        public void Stats_NoUpgrades_EqualsBase()
        {
            var s = tree.Stats();
            Assert.AreEqual(db.dragon.attackPower, s.AttackPower);
            Assert.AreEqual(db.dragon.maxHp, s.MaxHp);
            Assert.AreEqual(1.0, s.GoldMultiplier);
            Assert.AreEqual(1.0, s.BodySlamMultiplier);
        }
    }

    /// <summary>
    /// 8.2 업그레이드 시뮬레이션을 공식으로 재현한다: 누적 요구 골드의 50%를 용의 각성, 25%를 체력 강화에 쓴다.
    /// 수치를 바꿀 때마다 이 테스트의 로그로 표를 다시 확인한다 (8장 머리말).
    /// </summary>
    public class BalanceSimulationTests
    {
        // 8.2 표: 스테이지, 누적 요구 골드, 용의 각성 Lv, 공격력, 체력 강화 Lv, 최대 체력
        static readonly (string stage, double cumulative, int awakening, int attack, int hpLevel, int maxHp)[] GuideTable =
        {
            ("1-2", 366, 9, 43, 4, 146),
            ("1-4", 1662, 18, 94, 11, 285),
            ("2-4", 8350, 29, 243, 22, 814),
            ("3-4", 29810, 38, 528, 30, 1745),
            ("4-4", 91436, 46, 1053, 38, 3740),
            ("5-4", 270032, 54, 2099, 46, 8017),
            ("6-4", 764833, 61, 3837, 54, 17182),
            ("7-4", 2144071, 69, 7646, 61, 33483),
        };

        static int LevelsAffordable(UpgradeNodeRow node, double budget)
        {
            int level = 0;
            double spent = 0;
            while (true)
            {
                double next = Formulas.UpgradeCost(node.initialCost, node.costMultiplier, level);
                if (spent + next > budget) return level;
                spent += next;
                level++;
            }
        }

        [Test]
        public void GuideTable_ReproducedByUpgradeCurve()
        {
            var db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);
            var awakening = db.upgradeNodes.rows.First(n => n.node == "DragonAwakening");
            var hp = db.upgradeNodes.rows.First(n => n.node == "HpBoost");
            var log = new StringBuilder("[DWH] 8.2 재현 (스테이지 | 누적 골드 | 각성 Lv | 공격력 | 체력 Lv | 최대 체력)\n");

            foreach (var row in GuideTable)
            {
                int index = db.Stage(row.stage).index;
                double cumulative = db.stages.rows.Where(s => s.index <= index).Sum(s => s.upgradeGoldGuide);
                Assert.AreEqual(row.cumulative, cumulative, 1, $"{row.stage} 누적 요구 골드");

                int aLevel = LevelsAffordable(awakening, cumulative * 0.5);
                int hLevel = LevelsAffordable(hp, cumulative * 0.25);
                double attack = PlayerStats.Apply(db.dragon.attackPower, awakening.effectMode, awakening.effectValue, aLevel);
                double maxHp = PlayerStats.Apply(db.dragon.maxHp, hp.effectMode, hp.effectValue, hLevel);
                log.AppendLine($"{row.stage} | {cumulative:N0} | {aLevel} | {attack:N0} | {hLevel} | {maxHp:N0}");

                Assert.AreEqual(row.awakening, aLevel, $"{row.stage} 용의 각성 레벨");
                Assert.AreEqual(row.hpLevel, hLevel, $"{row.stage} 체력 강화 레벨");
                Assert.AreEqual(row.attack, attack, row.attack * 0.01 + 1, $"{row.stage} 공격력");
                Assert.AreEqual(row.maxHp, maxHp, row.maxHp * 0.01 + 1, $"{row.stage} 최대 체력");
            }
            Debug.Log(log.ToString());
        }
    }
}
