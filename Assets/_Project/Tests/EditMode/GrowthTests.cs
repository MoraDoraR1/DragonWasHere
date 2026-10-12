using DWH.Core;
using DWH.Data;
using DWH.Editor.Data;
using DWH.OutGame;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>성장 단계 (4.1.4)</summary>
    public class GrowthTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);

        [TestCase(0, GrowthStage.Hatchling, 1.0)]
        [TestCase(999, GrowthStage.Hatchling, 1.0)]
        [TestCase(1000, GrowthStage.Juvenile, 1.55)]
        [TestCase(100000, GrowthStage.Adult, 1.55 * 1.45)]
        [TestCase(1e8, GrowthStage.Ancient, 1.55 * 1.45 * 1.25)]
        public void StageAndSize_ByLifetimeSpentGold(double spent, GrowthStage stage, double scale)
        {
            Assert.AreEqual(stage, Growth.Stage(db, spent).growthStage);
            Assert.AreEqual(scale, Growth.SizeScale(db, spent), 1e-9);
        }

        [Test]
        public void Next_IsNullAtAncient()
        {
            Assert.AreEqual(GrowthStage.Juvenile, Growth.Next(db, 0).growthStage);
            Assert.IsNull(Growth.Next(db, 2e8));
        }

        [Test]
        public void UpgradeTreeStats_IncludeGrowth()
        {
            var save = new SaveData { currentStage = "1-1", lifetimeSpentGold = 150000 };
            var s = new UpgradeTree(db, save).Stats();
            Assert.AreEqual(GrowthStage.Adult, s.GrowthStage);
            Assert.AreEqual(1.55 * 1.45, s.SizeScale, 1e-9);
            Assert.AreEqual(2 * 1.55 * 1.45, s.JumpHeight, 1e-9, "점프 높이는 몸집에 비례");
            Assert.AreEqual(20, s.AttackPower, 1e-9, "능력치 보정은 1.0 (T-06)");
        }
    }
}
