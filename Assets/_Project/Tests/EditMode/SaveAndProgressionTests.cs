using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame.Drops;
using NUnit.Framework;

namespace DWH.Tests
{
    public class SaveSystemTests
    {
        [Test]
        public void Load_WithoutSave_StartsAtFirstStage()
        {
            var save = SaveSystem.Load(new MemorySaveStorage(), "1-1");
            Assert.AreEqual("1-1", save.currentStage);
            Assert.AreEqual(0, save.goldCoin);
        }

        [Test]
        public void SaveAndLoad_RoundTrip()
        {
            var storage = new MemorySaveStorage();
            var save = new SaveData { goldCoin = 1234.5, currentStage = "2-3", highestStage = "2-2", rebirthCount = 2 };
            save.clearedStages.Add("1-1");
            save.SetNodeLevel("DragonAwakening", 7);
            SaveSystem.Save(storage, save);

            var loaded = SaveSystem.Load(storage, "1-1");
            Assert.AreEqual(1234.5, loaded.goldCoin);
            Assert.AreEqual("2-3", loaded.currentStage);
            Assert.AreEqual("2-2", loaded.highestStage);
            Assert.AreEqual(2, loaded.rebirthCount);
            CollectionAssert.AreEqual(new[] { "1-1" }, loaded.clearedStages);
            Assert.AreEqual(7, loaded.GetNodeLevel("DragonAwakening"));
            Assert.AreEqual(0, loaded.GetNodeLevel("Greed"));
        }

        [Test]
        public void Load_BrokenJson_StartsFresh()
        {
            var storage = new MemorySaveStorage();
            storage.Write("{ not json");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("저장 데이터를 읽지 못해"));
            var save = SaveSystem.Load(storage, "1-1");
            Assert.AreEqual("1-1", save.currentStage);
        }
    }

    public class ProgressionTests
    {
        static readonly string[] Order = { "1-1", "1-2", "1-3", "1-4" };

        static SessionResult Result(string stage, bool cleared, double gold) =>
            new() { stageId = stage, cleared = cleared, earnedGold = gold };

        [Test]
        public void Clear_AdvancesStageAndAddsGold()
        {
            var save = new SaveData { currentStage = "1-1" };
            Progression.Settle(save, Result("1-1", true, 106), Order);
            Assert.AreEqual("1-2", save.currentStage);
            Assert.AreEqual("1-1", save.highestStage);
            Assert.AreEqual(106, save.goldCoin);
            Assert.AreEqual(106, save.runEarnedGold);
            CollectionAssert.Contains(save.clearedStages, "1-1");
        }

        [Test]
        public void Fail_KeepsStageButStillPaysGold()
        {
            var save = new SaveData { currentStage = "1-2", goldCoin = 10, highestStage = "1-1" };
            Progression.Settle(save, Result("1-2", false, 40), Order);
            Assert.AreEqual("1-2", save.currentStage);
            Assert.AreEqual("1-1", save.highestStage);
            Assert.AreEqual(50, save.goldCoin);
        }

        [Test]
        public void ClearLastStage_StaysOnLastStage()
        {
            var save = new SaveData { currentStage = "1-4", highestStage = "1-3" };
            Progression.Settle(save, Result("1-4", true, 0), Order);
            Assert.AreEqual("1-4", save.currentStage);
            Assert.AreEqual("1-4", save.highestStage);
        }

        [Test]
        public void ClearAgain_DoesNotDuplicate()
        {
            var save = new SaveData { currentStage = "1-1" };
            Progression.Settle(save, Result("1-1", true, 1), Order);
            save.currentStage = "1-1";
            Progression.Settle(save, Result("1-1", true, 1), Order);
            Assert.AreEqual(1, save.clearedStages.Count(s => s == "1-1"));
        }
    }

    public class DropPlannerTests
    {
        [Test]
        public void Expand_ListsHighestResourcesFirstAndMatchesCounts()
        {
            var row = new DropCompositionRow { ruby = 2, sapphire = 1, goldCoin = 3, targetCount = 6 };
            var list = DropPlanner.Expand(row);
            CollectionAssert.AreEqual(new[]
            {
                DropResource.Ruby, DropResource.Ruby, DropResource.Sapphire,
                DropResource.GoldCoin, DropResource.GoldCoin, DropResource.GoldCoin,
            }, list);
            double gold = DropPlanner.TotalGold(list, r => r switch { DropResource.Ruby => 100, DropResource.Sapphire => 25, _ => 1 });
            Assert.AreEqual(228, gold);
        }
    }
}
