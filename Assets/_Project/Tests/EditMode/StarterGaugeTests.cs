using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame.Starter;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>스테이지 스타터 게이지 난이도와 판정 (4.7)</summary>
    public class StarterGaugeTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);

        [Test]
        public void Difficulty_FollowsStageFormula()
        {
            var first = StarterGauge.ForStage(db.balance, 1);
            Assert.AreEqual(0.8f, first.ZoneSpeed, 1e-5);
            Assert.AreEqual(1.2f, first.BarSpeed, 1e-5);
            Assert.AreEqual(0.15f, first.GreatWidth, 1e-5);
            Assert.AreEqual(0.375f, first.GoodWidth, 1e-5, "Good = Great × 2.5");

            var last = StarterGauge.ForStage(db.balance, 28);
            Assert.AreEqual(0.8f + 0.05f * 27, last.ZoneSpeed, 1e-5);
            Assert.AreEqual(1.2f + 0.05f * 27, last.BarSpeed, 1e-5);
            Assert.AreEqual(0.069f, last.GreatWidth, 1e-5, "max(0.06, 0.15 - 0.003 × 27)");
        }

        [Test]
        public void Judge_ByDistanceFromZoneCenter()
        {
            Assert.AreEqual(StarterGrade.Great, StarterGauge.Judge(0.5f, 0.5f, 0.15f, 0.375f));
            Assert.AreEqual(StarterGrade.Great, StarterGauge.Judge(0.57f, 0.5f, 0.15f, 0.375f));
            Assert.AreEqual(StarterGrade.Good, StarterGauge.Judge(0.6f, 0.5f, 0.15f, 0.375f));
            Assert.AreEqual(StarterGrade.Good, StarterGauge.Judge(0.68f, 0.5f, 0.15f, 0.375f));
            Assert.AreEqual(StarterGrade.Fail, StarterGauge.Judge(0.7f, 0.5f, 0.15f, 0.375f));
        }

        [Test]
        public void Advance_KeepsZoneAndBarInsideGauge()
        {
            var gauge = StarterGauge.ForStage(db.balance, 14);
            for (int i = 0; i < 2000; i++)
            {
                gauge.Advance(1f / 60);
                Assert.That(gauge.Bar, Is.InRange(0f, 1f));
                Assert.That(gauge.ZoneStart, Is.InRange(0f, 1f - gauge.GoodWidth + 1e-5f));
            }
        }

        [Test]
        public void StarterTable_GradesMatchDoc()
        {
            var great = db.stageStarter.rows.Find(r => r.grade == StarterGrade.Great);
            var good = db.stageStarter.rows.Find(r => r.grade == StarterGrade.Good);
            var fail = db.stageStarter.rows.Find(r => r.grade == StarterGrade.Fail);
            Assert.AreEqual((2.0, 5), (great.damageMultiplier, great.sideCount));
            Assert.AreEqual((1.3, 2), (good.damageMultiplier, good.sideCount));
            Assert.AreEqual(0, fail.damageMultiplier);
        }
    }
}
