using System.Linq;
using DWH.Data;
using DWH.Editor.Data;
using DWH.InGame.AirUnits;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>공중 유닛 등장 스케줄 (4.5)</summary>
    public class AirUnitTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);

        [Test]
        public void Schedule_OddStage_AllSlotOne()
        {
            var schedule = AirUnitSquad.Schedule(db.AirUnits("1-1"), db.balance.airUnitSpawnTimes);
            CollectionAssert.AreEqual(new[] { 3f, 10f, 25f }, schedule.Select(s => s.time).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, schedule.Select(s => s.row.slot).ToArray());
            Assert.AreEqual("볏 익룡", schedule[0].row.name);
        }

        [Test]
        public void Schedule_EvenStage_SlotTwoLast()
        {
            var schedule = AirUnitSquad.Schedule(db.AirUnits("1-2"), db.balance.airUnitSpawnTimes);
            CollectionAssert.AreEqual(new[] { 1, 1, 2 }, schedule.Select(s => s.row.slot).ToArray());
            Assert.AreEqual("깃털 활공룡", schedule[2].row.name);
            Assert.AreEqual(132, schedule[2].row.hp);
        }

        [Test]
        public void Schedule_EveryStageHasThreeUnits()
        {
            foreach (var stage in db.stages.rows)
                Assert.AreEqual(3, AirUnitSquad.Schedule(db.AirUnits(stage.stageId), db.balance.airUnitSpawnTimes).Count, stage.stageId);
        }

        [Test]
        public void AirUnitBalanceKeys_Loaded()
        {
            Assert.AreEqual(3.0, db.balance.airUnitAltitudeMin);
            Assert.AreEqual(4.5, db.balance.airUnitAltitudeMax);
            Assert.AreEqual(4.0, db.balance.airUnitProjectileSpeed);
        }
    }
}
