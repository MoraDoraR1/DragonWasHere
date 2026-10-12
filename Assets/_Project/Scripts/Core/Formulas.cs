using System;
using DWH.Data;

namespace DWH.Core
{
    /// <summary>
    /// 게임 공식 모음. 수치는 인자나 데이터 행으로만 받는다 (값은 데이터 테이블에 있다).
    /// 반올림은 기획 엑셀과 같게 0.5에서 올림(ROUND)을 쓴다.
    /// </summary>
    public static class Formulas
    {
        // 0.1 같은 소수 곱셈의 부동소수점 오차(예: 12.499999999)가 반올림 결과를 바꾸지 않게 먼저 자른다
        const int NoiseDigits = 9;

        /// <summary>엑셀 ROUND(x, 0)과 같다. 0.5는 0에서 먼 쪽으로 올린다.</summary>
        public static double Round(double value) =>
            Math.Round(Math.Round(value, NoiseDigits), MidpointRounding.AwayFromZero);

        public static double Ceil(double value) => Math.Ceiling(Math.Round(value, NoiseDigits));

        public static double Floor(double value) => Math.Floor(Math.Round(value, NoiseDigits));

        /// <summary>
        /// 업그레이드 n레벨 비용 (가이드 6.7). cost_mult가 있으면 base × mult^(n-1), 없으면 base + add × (n-1).
        /// 용의 지혜(비용 감소율)를 곱하고 올림한다.
        /// </summary>
        public static double UpgradeCost(UpgradeRow row, int level, double discount = 0)
        {
            double cost = !double.IsNaN(row.costMult)
                ? row.baseCost * Math.Pow(row.costMult, level - 1)
                : row.baseCost + (double.IsNaN(row.addCost) ? 0 : row.addCost) * (level - 1);
            return Ceil(cost * (1 - discount));
        }

        /// <summary>환생 특성·액티브 n레벨 비용 = base + add × (n-1) (가이드 6.10)</summary>
        public static double LinearCost(double baseCost, double addCost, int level) => baseCost + addCost * (level - 1);

        /// <summary>환생 포인트 = floor(√(회차 누적 획득 골드) ÷ 나누는 값) (가이드 6.10)</summary>
        public static double RebirthPoints(double runGold, double divisor) =>
            runGold <= 0 ? 0 : Floor(Math.Sqrt(runGold) / divisor);

        /// <summary>피로(t) = 시작값 + floor(t / 간격) × 증가량 (임시unity제작.md 8.1). 초당 기력 감소량과 같다.</summary>
        public static double Fatigue(double elapsed, double start, double interval, double step) =>
            start + Math.Floor(elapsed / interval) * step;

        /// <summary>0초부터 elapsed초까지 소모한 기력 합계 (8.1의 닫힌 식)</summary>
        public static double StaminaUsed(double elapsed, double start, double interval, double step)
        {
            if (elapsed <= 0)
                return 0;
            double k = Math.Floor(elapsed / interval);
            // 완료된 구간 k개: 구간 i(0부터)의 피로는 start + i×step
            double completed = interval * (k * start + step * k * (k - 1) / 2);
            double partial = (elapsed - interval * k) * (start + k * step);
            return completed + partial;
        }
    }
}
