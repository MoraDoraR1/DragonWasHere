using DWH.Core;
using NUnit.Framework;

namespace DWH.Tests
{
    public class FormulasTests
    {
        [TestCase(12.5, 13)]
        [TestCase(687.5, 688)]   // 1-2 랜드마크 625 × 1.1 (엑셀 허들 설계 시트 값)
        [TestCase(2.49999, 2)]
        [TestCase(12.4999999999, 13)]   // 소수 9자리 밖의 오차는 무시한다 (0.1 곱셈 오차 보정)
        [TestCase(-2.5, -3)]
        public void Round_MatchesExcelRound(double value, double expected)
        {
            Assert.AreEqual(expected, Formulas.Round(value));
        }

        [TestCase(100, 1.0, 10)]
        [TestCase(165, 1.2, 20)]   // 19.8
        [TestCase(625, 1.5, 94)]   // 93.75
        [TestCase(150, 3.0, 45)]
        public void BuildingGold(double rawHp, double typeMultiplier, double expected)
        {
            Assert.AreEqual(expected, Formulas.BuildingGold(rawHp, 0.1, typeMultiplier));
        }

        [TestCase(230, 0.9, 207)]
        [TestCase(250, 1.3, 325)]
        public void ActualHp(double rawHp, double multiplier, double expected)
        {
            Assert.AreEqual(expected, Formulas.ActualHp(rawHp, multiplier));
        }

        [TestCase(1, 2.0)]
        [TestCase(2, 2.5)]
        [TestCase(7, 5.0)]
        public void TargetRepeat_MajorHurdle(int era, double expected)
        {
            Assert.AreEqual(expected, Formulas.TargetRepeat(2, 0.5, 5, era));
        }

        [TestCase(1, 1, 10)]
        [TestCase(27, 7, 25)]
        [TestCase(28, 7, 26)]
        public void DropCount_Normal(int stageIndex, int era, int expected)
        {
            Assert.AreEqual(expected, Formulas.DropCount(10, 0.02, 0.07, stageIndex, era));
        }

        [TestCase(0, 10)]
        [TestCase(1, 12)]   // 11.5 → 올림
        [TestCase(10, 41)]
        public void UpgradeCost_DragonAwakening(int level, double expected)
        {
            Assert.AreEqual(expected, Formulas.UpgradeCost(10, 1.15, level));
        }

        [TestCase(0, 0)]
        [TestCase(2500, 5)]
        [TestCase(30000, 17)]
        [TestCase(270000, 51)]
        [TestCase(2100000, 144)]   // 8.3 표
        public void RebirthPoints(double runGold, double expected)
        {
            Assert.AreEqual(expected, Formulas.RebirthPoints(runGold, 10));
        }

        [TestCase(0, 1)]
        [TestCase(9.9, 1)]
        [TestCase(10, 2)]
        [TestCase(75, 8)]
        public void Fatigue(double elapsed, double expected)
        {
            Assert.AreEqual(expected, Formulas.Fatigue(elapsed, 1, 10, 1));
        }

        // 8.1 표: 경과 시간별 누적 기력 소모
        [TestCase(10, 10)]
        [TestCase(20, 30)]
        [TestCase(30, 60)]
        [TestCase(40, 100)]
        [TestCase(50, 150)]
        [TestCase(60, 210)]
        [TestCase(75, 320)]
        [TestCase(31, 64)]   // 8.1 1-1 검증: 31초 클리어 시 64 소모
        public void StaminaUsed_MatchesGuideTable(double elapsed, double expected)
        {
            Assert.AreEqual(expected, Formulas.StaminaUsed(elapsed, 1, 10, 1), 1e-9);
        }
    }
}
