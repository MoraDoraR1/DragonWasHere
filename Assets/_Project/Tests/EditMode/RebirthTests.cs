using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame.Skills;
using DWH.OutGame;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>환생, 환생 특성, 액티브 스킬 습득 (4.9, 4.4, 아웃게임 업그레이드 기획서 Ver01)</summary>
    public class RebirthTests
    {
        GameDatabase db;
        string[] order;
        SaveData save;

        [OneTimeSetUp]
        public void Load()
        {
            db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);
            order = db.stages.rows.Select(s => s.stageId).ToArray();
        }

        [SetUp]
        public void SetUp() => save = new SaveData { currentStage = "1-1" };

        [Test]
        public void Traits_MatchOutgameDocTable()
        {
            var expected = new Dictionary<RebirthTraitId, (double first, double step, int max)>
            {
                [RebirthTraitId.PrimalFury] = (1, 1, 50),
                [RebirthTraitId.GoldenBlood] = (1, 1, 50),
                [RebirthTraitId.EternalHeart] = (2, 2, 20),
                [RebirthTraitId.ElderLegacy] = (3, 5, 10),
                [RebirthTraitId.DragonWisdom] = (3, 5, 10),
                [RebirthTraitId.AutoAwakening] = (5, 10, 2),
            };
            Assert.AreEqual(6, db.rebirthTraits.rows.Count);
            foreach (var (id, e) in expected.Select(kv => (kv.Key, kv.Value)))
            {
                var row = db.Trait(id);
                Assert.AreEqual(e.first, row.firstCost, id.ToString());
                Assert.AreEqual(e.step, row.costStep, id.ToString());
                Assert.AreEqual(e.max, row.maxLevel, id.ToString());
            }
        }

        [Test]
        public void Unlock_AfterClearingPrimitiveBoss()
        {
            Assert.IsFalse(Rebirth.IsUnlocked(db, save, order));
            save.highestStage = "1-3";
            Assert.IsFalse(Rebirth.IsUnlocked(db, save, order));
            save.highestStage = "1-4";
            Assert.IsTrue(Rebirth.IsUnlocked(db, save, order));
            Assert.AreEqual(0, Rebirth.Perform(db, new SaveData { currentStage = "1-1" }, order), "해금 전에는 환생하지 않는다");
        }

        [Test]
        public void Perform_ResetsRunAndKeepsPermanentProgress()
        {
            save.highestStage = "2-1";
            save.currentStage = "2-2";
            save.clearedStages.AddRange(new[] { "1-1", "1-2", "1-3", "1-4", "2-1" });
            save.goldCoin = 999;
            save.runEarnedGold = 2500;
            save.lifetimeSpentGold = 4000;
            save.rebirthPoint = 3;
            save.SetNodeLevel("DragonAwakening", 12);
            save.SetTraitLevel("PrimalFury", 2);
            save.SetActiveLevel("Meteor", 1);

            double points = Rebirth.Perform(db, save, order);

            Assert.AreEqual(5, points, "floor(sqrt(2500) / 10)");
            Assert.AreEqual(8, save.rebirthPoint);
            Assert.AreEqual(1, save.rebirthCount);
            Assert.AreEqual("1-1", save.currentStage);
            Assert.IsEmpty(save.clearedStages);
            Assert.IsEmpty(save.nodeLevels);
            Assert.AreEqual(0, save.goldCoin);
            Assert.AreEqual(0, save.runEarnedGold);
            Assert.AreEqual("2-1", save.highestStage, "최고 기록은 유지");
            Assert.AreEqual(4000, save.lifetimeSpentGold, "누적 사용 골드(성장 단계)는 유지");
            Assert.AreEqual(2, save.GetTraitLevel("PrimalFury"));
            Assert.AreEqual(1, save.GetActiveLevel("Meteor"));
            Assert.IsTrue(Rebirth.IsUnlocked(db, save, order), "한 번 환생하면 계속 열려 있다");
        }

        [Test]
        public void Perform_AppliesStartGoldAndAutoAwakening()
        {
            save.highestStage = "1-4";
            save.SetTraitLevel("ElderLegacy", 3);
            save.SetTraitLevel("AutoAwakening", 2);
            Rebirth.Perform(db, save, order);
            Assert.AreEqual(450, save.goldCoin, "50 × 3^2");
            Assert.AreEqual(1, save.GetNodeLevel("Skill_FireBreath"));
            Assert.AreEqual(1, save.GetNodeLevel("Skill_Whirlwind"));
        }

        [Test]
        public void TraitCost_LinearIncrease()
        {
            var fury = db.Trait(RebirthTraitId.PrimalFury);
            Assert.AreEqual(1, Rebirth.TraitCost(fury, 0));
            Assert.AreEqual(2, Rebirth.TraitCost(fury, 1));
            var legacy = db.Trait(RebirthTraitId.ElderLegacy);
            CollectionAssert.AreEqual(new double[] { 3, 8, 13 }, Enumerable.Range(0, 3).Select(l => Rebirth.TraitCost(legacy, l)).ToArray());
            double total = db.rebirthTraits.rows.Sum(r => Enumerable.Range(0, r.maxLevel).Sum(l => Rebirth.TraitCost(r, l)));
            Assert.AreEqual(3500, total, 1, "특성을 모두 최대로 올리는 비용 (4.9 메모)");
        }

        [Test]
        public void BuyTrait_SpendsPointsAndStopsAtMax()
        {
            save.rebirthPoint = 30;
            Assert.IsTrue(Rebirth.BuyTrait(db, save, RebirthTraitId.AutoAwakening));
            Assert.AreEqual(25, save.rebirthPoint);
            Assert.IsTrue(Rebirth.BuyTrait(db, save, RebirthTraitId.AutoAwakening));
            Assert.AreEqual(10, save.rebirthPoint);
            Assert.IsFalse(Rebirth.BuyTrait(db, save, RebirthTraitId.AutoAwakening), "최대 2레벨");
            save.rebirthPoint = 0;
            Assert.IsFalse(Rebirth.BuyTrait(db, save, RebirthTraitId.PrimalFury), "포인트 부족");
        }

        [Test]
        public void Traits_ApplyToStatsAndCosts()
        {
            var tree = new UpgradeTree(db, save);
            save.SetTraitLevel("PrimalFury", 4);
            save.SetTraitLevel("GoldenBlood", 5);
            save.SetTraitLevel("EternalHeart", 2);
            var s = tree.Stats();
            Assert.AreEqual(20 * 1.2, s.AttackPower, 1e-9, "태고의 분노 4레벨 = +20%");
            Assert.AreEqual(1.5, s.GoldMultiplier, 1e-9, "황금 혈통 5레벨 = +50%");
            Assert.AreEqual(110, s.MaxStamina, 1e-9, "영원의 심장 2레벨 = +10");

            Assert.AreEqual(10, tree.Cost(tree.Node("DragonAwakening")));
            save.SetTraitLevel("DragonWisdom", 10);
            Assert.AreEqual(0.3, Rebirth.Discount(db, save), 1e-9);
            Assert.AreEqual(7, tree.Cost(tree.Node("DragonAwakening")), "ceil(10 × 0.7)");
        }

        [Test]
        public void ActiveSkills_LearnAndUpgradeWithPoints()
        {
            save.rebirthPoint = 200;
            Assert.AreEqual(5, Rebirth.ActiveCost(db, save, ActiveSkillId.Meteor));
            for (int i = 0; i < 5; i++) Assert.IsTrue(Rebirth.BuyActive(db, save, ActiveSkillId.Meteor));
            Assert.AreEqual(200 - (5 + 10 + 20 + 40 + 80), save.rebirthPoint);
            Assert.AreEqual(5, Rebirth.ActiveLevel(save, ActiveSkillId.Meteor));
            Assert.IsFalse(Rebirth.BuyActive(db, save, ActiveSkillId.Meteor), "최대 5단계");

            var uses = ActiveSkillSystem.FullUses(db, new Dictionary<ActiveSkillId, int>
                { [ActiveSkillId.Meteor] = 5, [ActiveSkillId.Hurricane] = 2, [ActiveSkillId.Earthquake] = 0 });
            Assert.AreEqual(3, uses[ActiveSkillId.Meteor]);
            Assert.AreEqual(1, uses[ActiveSkillId.Hurricane]);
            Assert.IsFalse(uses.ContainsKey(ActiveSkillId.Earthquake), "미습득 스킬은 횟수가 없다");
        }
    }
}
