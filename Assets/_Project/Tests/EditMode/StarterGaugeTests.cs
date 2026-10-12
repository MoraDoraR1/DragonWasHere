using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame.Starter;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>스테이지 스타터 게이지 (starter_stage, starter_grade, 가이드 6.9)</summary>
    public class StarterGaugeTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory();

        [Test]
        public void Gauge_UsesStarterStageRow()
        {
            var first = StarterGauge.ForStage(db.StarterStage("stg101"));
            Assert.AreEqual(0.8f, first.ZoneSpeed, 1e-5);
            Assert.AreEqual(1.2f, first.BarSpeed, 1e-5);
            Assert.AreEqual(0.15f, first.GreatWidth, 1e-5);
            Assert.AreEqual(0.375f, first.GoodWidth, 1e-5);
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
            var gauge = StarterGauge.ForStage(db.StarterStage("stg702"));
            for (int i = 0; i < 2000; i++)
            {
                gauge.Advance(1f / 60);
                Assert.That(gauge.Bar, Is.InRange(0f, 1f));
                Assert.That(gauge.ZoneStart, Is.InRange(0f, 1f - gauge.GoodWidth + 1e-5f));
            }
        }

        [Test]
        public void Grades_MatchTable()
        {
            Assert.AreEqual((200.0, 5), (db.StarterGrade(StarterGrade.Great).dmgPct, db.StarterGrade(StarterGrade.Great).sideCount));
            Assert.AreEqual((130.0, 2), (db.StarterGrade(StarterGrade.Good).dmgPct, db.StarterGrade(StarterGrade.Good).sideCount));
            Assert.AreEqual(0, db.StarterGrade(StarterGrade.Fail).dmgPct);
        }
    }
}
