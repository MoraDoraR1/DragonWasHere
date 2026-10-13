using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame.Drops;
using DWH.OutGame;
using NUnit.Framework;
using UnityEngine;

namespace DWH.Tests
{
    /// <summary>게임 규칙 (가이드 6.4~6.12). 기대값은 데이터 테이블 Ver02 값으로 손 계산했다.</summary>
    public class RulesTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory();

        SaveData NewSave() => SaveSystem.Load(new MemorySaveStorage(), db.StageOrder[0]);

        // ───── 공식 ─────

        [Test]
        public void UpgradeCost_MultiplierAndLinear()
        {
            var awaken = db.Upgrade("upg101");   // 30, ×1.5
            Assert.AreEqual(30, Formulas.UpgradeCost(awaken, 1));
            Assert.AreEqual(45, Formulas.UpgradeCost(awaken, 2));
            Assert.AreEqual(68, Formulas.UpgradeCost(awaken, 3), "67.5 → 올림");
            var stamina = db.Upgrade("upg201");  // 36 + 15(n-1)
            Assert.AreEqual(36, Formulas.UpgradeCost(stamina, 1));
            Assert.AreEqual(51, Formulas.UpgradeCost(stamina, 2));
            Assert.AreEqual(29, Formulas.UpgradeCost(awaken, 1, 0.06), "용의 지혜 6%: ceil(28.2)");
        }

        [Test]
        public void RebirthPoints_SqrtOverDivisor()
        {
            Assert.AreEqual(24, Formulas.RebirthPoints(57600, db.Const("rebirth_rp_divisor")));
            Assert.AreEqual(0, Formulas.RebirthPoints(0, 10));
        }

        [Test]
        public void Fatigue_StartsAt1AndRisesEvery10s()
        {
            var d = db.Dragon;
            Assert.AreEqual(1, Formulas.Fatigue(9.9, d.fatigueStart, d.fatigueIntervalSec, Tuning.FatigueStep));
            Assert.AreEqual(4, Formulas.Fatigue(31, d.fatigueStart, d.fatigueIntervalSec, Tuning.FatigueStep));
        }

        // ───── 능력치 ─────

        [Test]
        public void BaseStats_FromDragonTable()
        {
            var s = PlayerStats.Base(db);
            Assert.AreEqual(20, s.AttackPower);
            Assert.AreEqual(100, s.MaxHp);
            Assert.AreEqual(100, s.MaxStamina);
            Assert.AreEqual(3, s.MoveSpeed, 1e-9, "300 px/s = 3 unit/s");
            Assert.AreEqual(1.5, s.CritMultiplier, 1e-9);
            Assert.AreEqual(1, s.GoldMultiplier);
            CollectionAssert.AreEquivalent(new[] { PassiveSkillId.BodySlam }, s.Skills);
            Assert.AreEqual(GrowthStage.Hatchling, s.Growth.growthStage);
        }

        [Test]
        public void UpgradesAndTraits_ApplyByEffectCode()
        {
            var levels = new System.Collections.Generic.Dictionary<string, int> { ["upg101"] = 3, ["upg201"] = 2, ["upg404"] = 1, ["upg503"] = 2, ["upg506"] = 2 };
            var traits = new System.Collections.Generic.Dictionary<string, int> { ["rbt1"] = 1, ["rbt3"] = 1 };
            var s = PlayerStats.Compute(db, id => levels.TryGetValue(id, out int l) ? l : 0, id => traits.TryGetValue(id, out int l) ? l : 0);
            Assert.AreEqual(20 * 1.6 * 1.2, s.AttackPower, 1e-9, "각성 3레벨 +60%, 태고의 분노 +20% (곱)");
            Assert.AreEqual(100 + 8 + 10, s.MaxStamina, 1e-9, "지구력 2레벨 +8, 영원의 심장 +10 (합)");
            Assert.AreEqual(2.3, s.CritMultiplier, 1e-9, "용의 분노 1레벨: 치명타 배율 = 230%");
            Assert.IsTrue(s.Has(PassiveSkillId.FireBall));
            Assert.AreEqual(0.6 * 1.25, s.FireBallMultiplier, 1e-9, "파이어 볼 기본 60% × 2레벨 배율 1.25");
            Assert.AreEqual(2, s.ShockwaveSides, "충격 범위 2레벨: 양옆 2개");
            Assert.IsFalse(s.Has(PassiveSkillId.Shockwave), "충격파 노드는 아직 0레벨");
        }

        [Test]
        public void LowStamina_BoostsAttack()
        {
            var s = PlayerStats.Compute(db, id => id == "upg206" ? 1 : 0);
            Assert.AreEqual(20, s.AttackAt(50, 100));
            Assert.AreEqual(20 * 1.15, s.AttackAt(25, 100), 1e-9);
        }

        [Test]
        public void Growth_ByRunSpentGold()
        {
            Assert.AreEqual(GrowthStage.Hatchling, Growth.Stage(db, 999).growthStage);
            Assert.AreEqual(GrowthStage.Juvenile, Growth.Stage(db, 1000).growthStage);
            Assert.AreEqual(GrowthStage.Ancient, Growth.Stage(db, 1e8).growthStage);
            Assert.IsNull(Growth.Next(db, 1e8));
            Assert.AreEqual(11, db.Parts(Growth.Stage(db, 1e5).growthId).Count(), "단계마다 파츠 11장");
        }

        // ───── 트리 ─────

        [Test]
        public void Tree_PrereqMaxLevelAndPurchase()
        {
            var save = NewSave();
            var tree = new UpgradeTree(db, save);
            var awaken = db.Upgrade("upg101");
            var stamina = db.Upgrade("upg201");
            Assert.AreEqual(NodeStatus.Locked, tree.Status(stamina));
            Assert.AreEqual(NodeStatus.TooExpensive, tree.Status(awaken));
            save.goldCoin = 1000;
            Assert.IsTrue(tree.TryBuy(awaken));
            Assert.AreEqual(970, save.goldCoin);
            Assert.AreEqual(30, save.runSpentGold);
            Assert.AreEqual(NodeStatus.Affordable, tree.Status(stamina));
            Assert.AreEqual(2, tree.BuyMany(awaken, 2));
            Assert.AreEqual(1000 - 30 - 45 - 68, save.goldCoin);

            var sides = db.Upgrade("upg506");   // 최대 4
            save.SetNodeLevel("upg505", 1);
            save.SetNodeLevel("upg509", 1);
            save.SetNodeLevel("upg507", 1);
            save.SetNodeLevel(sides.upgradeId, 4);
            Assert.AreEqual(NodeStatus.Maxed, tree.Status(sides));
        }

        [Test]
        public void Tree_TwoPrerequisites()
        {
            var save = NewSave();
            var tree = new UpgradeTree(db, save);
            var apocalypse = db.Upgrade("upg513");   // 충격파 + 낙뢰
            save.SetNodeLevel("upg505", 1);
            Assert.IsFalse(tree.IsUnlocked(apocalypse));
            save.SetNodeLevel("upg511", 1);
            Assert.IsTrue(tree.IsUnlocked(apocalypse));
        }

        [Test]
        public void TreeLayout_AllNodesPlacedWithoutOverlap()
        {
            var pos = TreeLayout.Positions(db);
            Assert.AreEqual(28, pos.Count);
            var points = pos.Values.ToList();
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                {
                    var d = points[i] - points[j];
                    Assert.Greater(d.magnitude, 0.7f, "노드가 겹친다");
                    // 같은 열에서 위아래로 붙으면 위 노드 이름(배지 아래)이 아래 노드를 가린다 (DWH-51)
                    if (Mathf.Abs(d.x) < 1f) Assert.GreaterOrEqual(Mathf.Abs(d.y), 1.15f, "위아래 노드 사이에 이름 자리가 없다");
                }
        }

        // ───── 환생 ─────

        [Test]
        public void Rebirth_UnlocksAtWallsMaxTwice()
        {
            var save = NewSave();
            save.currentStage = "stg301";
            Assert.IsFalse(Rebirth.IsUnlocked(db, save));
            save.currentStage = "stg302";
            Assert.IsTrue(Rebirth.IsUnlocked(db, save), "3-2 도달 = 1번째 환생 해금");

            save.runEarnedGold = 57600;
            Assert.AreEqual(24, Rebirth.Perform(db, save));
            Assert.AreEqual("stg101", save.currentStage);
            Assert.AreEqual(1, save.rebirthCount);
            save.currentStage = "stg402";
            Assert.IsFalse(Rebirth.IsUnlocked(db, save), "2번째는 5-2 도달");
            save.currentStage = "stg502";
            Assert.IsTrue(Rebirth.IsUnlocked(db, save));
            Rebirth.Perform(db, save);
            save.currentStage = "stg702";
            Assert.IsFalse(Rebirth.IsUnlocked(db, save), "최대 2회");
        }

        [Test]
        public void Rebirth_ResetsRunKeepsPermanent()
        {
            var save = NewSave();
            save.currentStage = "stg302";
            save.goldCoin = 5000;
            save.runEarnedGold = 10000;
            save.runSpentGold = 200000;
            save.SetNodeLevel("upg101", 10);
            save.SetTraitLevel("rbt4", 1);   // 고룡의 유산 1000
            save.SetTraitLevel("rbt6", 1);   // 자동 각성 1개
            save.SetActiveLevel("act1", 2);
            save.rebirthPoint = 3;
            Rebirth.Perform(db, save);
            Assert.AreEqual(1000, save.goldCoin, "환생 직후 골드 = 고룡의 유산");
            Assert.AreEqual(0, save.GetNodeLevel("upg101"));
            Assert.AreEqual(1, save.GetNodeLevel("upg501"), "자동 각성: 파이어 브레스");
            Assert.AreEqual(0, save.runSpentGold, "성장 단계 초기화");
            Assert.AreEqual(2, save.GetActiveLevel("act1"), "액티브 유지");
            Assert.AreEqual(3 + 10, save.rebirthPoint, "floor(√10000 ÷ 10) = 10");
        }

        [Test]
        public void Traits_CostAndMax()
        {
            var save = NewSave();
            save.rebirthPoint = 100;
            var auto = db.Trait("rbt6");   // 5 + 10(n-1), 최대 2
            Assert.IsTrue(Rebirth.BuyTrait(db, save, "rbt6"));
            Assert.AreEqual(95, save.rebirthPoint);
            Assert.IsTrue(Rebirth.BuyTrait(db, save, "rbt6"));
            Assert.AreEqual(80, save.rebirthPoint);
            Assert.IsFalse(Rebirth.BuyTrait(db, save, "rbt6"), "최대 레벨");
            Assert.AreEqual(2, auto.maxLevel);
        }

        [Test]
        public void Actives_NeedPrereqNodeAndCostPoints()
        {
            var save = NewSave();
            save.rebirthPoint = 10;
            Assert.IsFalse(Rebirth.BuyActive(db, save, ActiveSkillId.Meteor), "연속 파이어 볼 노드 필요");
            save.SetNodeLevel("upg504", 1);
            Assert.IsTrue(Rebirth.BuyActive(db, save, ActiveSkillId.Meteor), "메테오 1단계는 0 P");
            Assert.AreEqual(10, save.rebirthPoint);
            Assert.IsTrue(Rebirth.BuyActive(db, save, ActiveSkillId.Meteor));
            Assert.AreEqual(4, save.rebirthPoint, "2단계 6 P");
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 3 }, Enumerable.Range(1, 5).Select(ActiveSpecs.Uses));
        }

        // ───── 진행·저장 ─────

        [Test]
        public void Settle_ClearAdvancesMvpAndFailureStays()
        {
            var save = NewSave();
            Progression.Settle(db, save, new SessionResult { stageId = "stg101", cleared = false, earnedGold = 50 });
            Assert.AreEqual("stg101", save.currentStage);
            Assert.AreEqual(50, save.goldCoin, "실패해도 골드 지급");
            Progression.Settle(db, save, new SessionResult { stageId = "stg102", cleared = true });
            Assert.AreEqual("stg101", save.currentStage, "도전 스테이지가 아닌 판은 진행을 바꾸지 않는다");
            Progression.Settle(db, save, new SessionResult { stageId = "stg101", cleared = true });
            Assert.AreEqual("stg102", save.currentStage);
            save.currentStage = "stg102";
            Progression.Settle(db, save, new SessionResult { stageId = "stg102", cleared = true });
            Assert.AreEqual("stg201", save.currentStage, "MVP: 1-2 다음은 2-1");
            Assert.AreEqual("stg201", save.highestStage);
        }

        [Test]
        public void Settle_LastStageIsEnding()
        {
            var save = NewSave();
            save.currentStage = "stg702";
            var result = new SessionResult { stageId = "stg702", cleared = true };
            Progression.Settle(db, save, result);
            Assert.IsTrue(result.gameCompleted);
            Assert.IsTrue(save.gameCompleted);
            Assert.AreEqual("stg702", save.currentStage);
        }

        [Test]
        public void Save_RoundTripsAndDropsOldVersion()
        {
            var storage = new MemorySaveStorage();
            var save = SaveSystem.Load(storage, "stg101");
            Assert.IsNotEmpty(save.userId);
            save.goldCoin = 1234;
            save.SetNodeLevel("upg101", 3);
            save.language = "en";
            SaveSystem.Save(storage, save);
            var loaded = SaveSystem.Load(storage, "stg101");
            Assert.AreEqual(1234, loaded.goldCoin);
            Assert.AreEqual(3, loaded.GetNodeLevel("upg101"));
            Assert.AreEqual("en", loaded.language);
            Assert.AreEqual(save.userId, loaded.userId);

            storage.Write("{\"version\":1,\"goldCoin\":99}");
            Assert.AreEqual(0, SaveSystem.Load(storage, "stg101").goldCoin, "옛 버전 저장은 새로 시작");
        }

        // ───── 드랍 ─────

        [Test]
        public void DropGroup_ExpandsAllRowsHighTierFirst()
        {
            var drops = DropPlanner.Expand(db, "drp1302", new System.Random(1));   // 사파이어 4 + 금괴 10
            Assert.AreEqual(14, drops.Count);
            Assert.AreEqual("itm103", drops[0].itemId);
            Assert.AreEqual(4 * 25 + 10 * 5, DropPlanner.TotalGold(drops));
            Assert.AreEqual(150, db.DropGroupGold("drp1302"));
        }

        [Test]
        public void Combo_TierByCount()
        {
            Assert.IsNull(db.Combo(2));
            Assert.AreEqual("cmb1", db.Combo(3).comboId);
            Assert.AreEqual("cmb3", db.Combo(19).comboId);
            Assert.AreEqual("cmb6", db.Combo(999).comboId);
        }
    }
}
