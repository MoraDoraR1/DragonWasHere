using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame.Skills;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>패시브 스킬 데이터와 수치 (4.3, 4.8 스킬 노드, 아웃게임 기획서 Ver01 반영분)</summary>
    public class PassiveSkillTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);

        UpgradeNodeRow Node(string name) => db.upgradeNodes.rows.First(n => n.node == name);
        PassiveSkillRow Skill(PassiveSkillId id) => db.passiveSkills.rows.First(s => s.skill == id);

        [TestCase("Skill_Whirlwind", "Skill_BodySlam", 200)]
        [TestCase("Skill_Shockwave", "Skill_Whirlwind", 800)]
        [TestCase("Skill_FireBreath", "DragonAwakening", 50)]
        [TestCase("Skill_FireBall", "Skill_FireBreath", 200)]
        [TestCase("Skill_Lightning", "Skill_FireBall", 800)]
        public void SkillBranches_FollowOutgameDocOrder(string node, string prerequisite, double unlockCost)
        {
            var row = Node(node);
            CollectionAssert.AreEqual(new[] { prerequisite }, row.prerequisites);
            Assert.AreEqual(unlockCost, row.initialCost);
            Assert.AreEqual(0, row.maxLevel, "스킬 노드는 최대 레벨이 없다");
        }

        [Test]
        public void AllNodes_AvailableAfterM6()
        {
            CollectionAssert.IsEmpty(db.upgradeNodes.rows.Where(n => !n.available).Select(n => n.node));
            Assert.IsFalse(db.upgradeNodes.rows.Any(n => n.node == "Soar"), "비상은 중량 강화로 바뀌었다");
        }

        [Test]
        public void Stats_WeightAndSubUpgrades()
        {
            var levels = new System.Collections.Generic.Dictionary<string, int> { ["Weight"] = 3, ["Sub_Burn"] = 2, ["Skill_FireBreath"] = 4 };
            var stats = PlayerStats.Compute(db, n => levels.TryGetValue(n.node, out int l) ? l : n.initialLevel);
            Assert.AreEqual(1.6, stats.LandingDamage, 1e-9, "착지 대미지 = 1 + 0.2 × 3");
            Assert.AreEqual(2, stats.SubLevel(PassiveSkillId.FireBreath));
            Assert.AreEqual(4, stats.SkillLevel(PassiveSkillId.FireBreath));
            Assert.AreEqual(1, stats.SkillLevel(PassiveSkillId.BodySlam), "몸통 박치기는 기본 보유");
            Assert.AreEqual(0, stats.SkillLevel(PassiveSkillId.Lightning));
            Assert.AreEqual(1.0, PlayerStats.Base(db).LandingDamage);
        }

        [Test]
        public void Multiplier_PerLevelBonus()
        {
            Assert.AreEqual(0.6, PassiveSkillSystem.Multiplier(Skill(PassiveSkillId.FireBall), 1), 1e-9);
            Assert.AreEqual(1.2, PassiveSkillSystem.Multiplier(Skill(PassiveSkillId.FireBall), 3), 1e-9, "레벨당 +50%");
            Assert.AreEqual(1.2, PassiveSkillSystem.Multiplier(Skill(PassiveSkillId.FireBreath), 3), 1e-9, "브레스는 레벨당 +100%");
            Assert.AreEqual(1.5, PassiveSkillSystem.Multiplier(Skill(PassiveSkillId.Shockwave), 1), 1e-9);
        }

        [Test]
        public void Lightning_BoltsCapAtEightThenDamageGrows()
        {
            var row = Skill(PassiveSkillId.Lightning);
            Assert.AreEqual(3, PassiveSkillSystem.LightningBolts(row, 1));
            Assert.AreEqual(8, PassiveSkillSystem.LightningBolts(row, 6));
            Assert.AreEqual(8, PassiveSkillSystem.LightningBolts(row, 9));
            Assert.AreEqual(1.5, PassiveSkillSystem.Multiplier(row, 6), 1e-9, "Lv6까지는 번개 수만 는다");
            Assert.AreEqual(3.0, PassiveSkillSystem.Multiplier(row, 8), 1e-9, "Lv6 이후 레벨당 +50%");
        }

        [Test]
        public void Shockwave_RangeGrowsWithSubUpgrade()
        {
            var row = Skill(PassiveSkillId.Shockwave);
            Assert.AreEqual(1.0, row.range);
            CollectionAssert.AreEqual(new[] { 1.5, 2.0, 2.5, 3.0 }, row.subUpgradeValues);
        }
    }
}
