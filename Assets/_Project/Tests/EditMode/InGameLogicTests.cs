using System.Linq;
using DWH.Data;
using DWH.InGame.Combat;
using DWH.InGame.Dragon;
using DWH.InGame.Stage;
using NUnit.Framework;
using UnityEngine;

namespace DWH.Tests
{
    public class StageLayoutTests
    {
        static int[] IndicesOf(BuildingType[] order, BuildingType type) =>
            order.Select((t, i) => (t, i)).Where(x => x.t == type).Select(x => x.i).ToArray();

        [Test]
        public void Stage11_AttackInTheMiddle()
        {
            var order = StageLayout.BuildOrder(9, 1, 0);
            Assert.AreEqual(10, order.Length);
            CollectionAssert.AreEqual(new[] { 4 }, IndicesOf(order, BuildingType.Attack));
        }

        [Test]
        public void Stage12_LandmarkCenteredAndAttacksSpread()
        {
            var order = StageLayout.BuildOrder(10, 2, 1);
            CollectionAssert.AreEqual(new[] { 6 }, IndicesOf(order, BuildingType.Landmark));
            CollectionAssert.AreEqual(new[] { 3, 8 }, IndicesOf(order, BuildingType.Attack));   // round(13/3)=4, round(26/3)=9 (1부터)
        }

        [Test]
        public void Stage74_CountsPreserved()
        {
            var order = StageLayout.BuildOrder(36, 15, 1);
            Assert.AreEqual(36, order.Count(t => t == BuildingType.Normal));
            Assert.AreEqual(15, order.Count(t => t == BuildingType.Attack));
            CollectionAssert.AreEqual(new[] { 26 }, IndicesOf(order, BuildingType.Landmark));
        }

        [Test]
        public void AttackSlotTakenByLandmark_MovesToNextFreeSlot()
        {
            var order = StageLayout.BuildOrder(1, 1, 1);   // 랜드마크 1번, 공격 round(1.5)=2 → 1번이 차 있어 2번
            CollectionAssert.AreEqual(new[] { BuildingType.Normal, BuildingType.Landmark, BuildingType.Attack }, order);
        }
    }

    public class DragonRigLayoutTests
    {
        static DragonPartRow Part(string name, string parent, double px, double py, double ax, double ay) =>
            new() { part = name, parent = parent, pivotX = px, pivotY = py, attachX = ax, attachY = ay };

        // 4.1.5 표 일부
        static readonly DragonPartRow Body = Part("Body", "", 0, 0, 0, 0);
        static readonly DragonPartRow Neck = Part("Neck", "Body", 190, 510, 148, 282);
        static readonly DragonPartRow UpHead = Part("UpHead", "Neck", 330, 290, 203, 110);
        static readonly DragonPartRow DownHead = Part("DownHead", "UpHead", 285, 35, 290, 357);
        static readonly DragonPartRow BackLeg = Part("BackLeg", "Body", 135, 100, 584, 387);

        [Test]
        public void ImageOrigins_FollowGuideFormula()
        {
            var origins = DragonRigLayout.ImageOrigins(new[] { Body, Neck, UpHead, DownHead, BackLeg });
            Assert.AreEqual(Vector2.zero, origins["Body"]);
            Assert.AreEqual(new Vector2(449, 287), origins["BackLeg"]);    // (584-135, 387-100)
            Assert.AreEqual(new Vector2(-42, -228), origins["Neck"]);
            Assert.AreEqual(new Vector2(-169, -408), origins["UpHead"]);
            Assert.AreEqual(new Vector2(-164, -86), origins["DownHead"]);
        }

        [Test]
        public void NodeAndSpritePositions_ConvertPixelsToUnitsWithFlippedY()
        {
            var node = DragonRigLayout.NodeLocalPosition(UpHead, Neck, 100);
            Assert.AreEqual(0.13f, node.x, 1e-5f);    // (203 - 190) / 100
            Assert.AreEqual(4.0f, node.y, 1e-5f);     // -(110 - 510) / 100

            var sprite = DragonRigLayout.SpriteLocalPosition(Neck, new Vector2(400, 600), 100);
            Assert.AreEqual(0.1f, sprite.x, 1e-5f);   // (200 - 190) / 100
            Assert.AreEqual(2.1f, sprite.y, 1e-5f);   // -(300 - 510) / 100
        }
    }

    public class DragonVitalsTests
    {
        static DragonVitals Create() => new(100, 0, 100, 1, 10, 1);

        [Test]
        public void Tick_31Seconds_Uses64Stamina_RegardlessOfFrameLength()
        {
            var smallSteps = Create();
            for (int i = 0; i < 31 * 50; i++) smallSteps.Tick(0.02);
            var oneStep = Create();
            oneStep.Tick(31);

            Assert.AreEqual(36, smallSteps.Stamina, 1e-6);   // 8.1: 31초 클리어 시 남은 기력 36
            Assert.AreEqual(36, oneStep.Stamina, 1e-6);
            Assert.AreEqual(4, smallSteps.Fatigue);
        }

        [Test]
        public void Exhausted_At40Seconds()
        {
            var v = Create();
            v.Tick(39.9);
            Assert.IsFalse(v.IsExhausted);
            v.Tick(0.2);
            Assert.IsTrue(v.IsExhausted);
            Assert.AreEqual(0, v.Stamina);
        }

        [Test]
        public void Damage_KillsAtZeroAndStopsTicking()
        {
            var v = Create();
            double reported = 0;
            v.Damaged += d => reported += d;
            v.TakeDamage(60);
            v.TakeDamage(60);
            Assert.IsTrue(v.IsDead);
            Assert.AreEqual(0, v.Hp);
            Assert.AreEqual(120, reported);
            v.Tick(10);
            Assert.AreEqual(100, v.Stamina, "쓰러진 뒤에는 기력이 줄지 않는다");
        }

        [Test]
        public void Regen_CapsAtMax()
        {
            var v = new DragonVitals(100, 5, 100, 1, 10, 1);
            v.TakeDamage(8);
            v.Tick(1);
            Assert.AreEqual(97, v.Hp, 1e-9);
            v.Tick(10);
            Assert.AreEqual(100, v.Hp, 1e-9);
        }
    }

    public class DamageTests
    {
        [Test]
        public void Compute_AppliesSkillAndCrit()
        {
            Assert.AreEqual(20, Damage.Compute(20, 1.0, false, 1.5));
            Assert.AreEqual(30, Damage.Compute(20, 1.0, true, 1.5));
            Assert.AreEqual(18, Damage.Compute(20, 0.6, true, 1.5), 1e-9);
        }

        [Test]
        public void RollCritical_UsesChance()
        {
            Assert.IsFalse(Damage.RollCritical(0, 0));
            Assert.IsTrue(Damage.RollCritical(0.3, 0.29));
            Assert.IsFalse(Damage.RollCritical(0.3, 0.3));
        }
    }
}
