using DWH.Analytics;
using DWH.Core;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>이탈 지표: 화면 전환이 page_exit 컨텍스트(scene)로 넘어가는지.</summary>
    public class AnalyticsExitTests
    {
        [TestCase(GameState.Title, "title")]
        [TestCase(GameState.OutGame, "outgame")]
        [TestCase(GameState.Stage, "stage")]
        [TestCase(GameState.Bonus, "bonus")]
        [TestCase(GameState.Result, "result")]
        public void SceneChanged_SetsExitScene(GameState state, string expected)
        {
            Core.Analytics.SceneChanged(state);
            Assert.AreEqual(expected, DwhAnalytics.Scene);
            StringAssert.Contains($"\"scene\":\"{expected}\"", DwhAnalytics.ExitContext().ToJson());
        }

        [Test]
        public void ExitContext_HasCommonParams()
        {
            DwhAnalytics.StageId = "2-2";
            string json = DwhAnalytics.ExitContext().ToJson();
            foreach (var key in new[] { "stage_id", "rebirth_count", "growth_stage", "session_index", "build_version", "is_dev" })
                StringAssert.Contains($"\"{key}\":", json);
            StringAssert.Contains("\"stage_id\":\"2-2\"", json);
        }
    }
}
