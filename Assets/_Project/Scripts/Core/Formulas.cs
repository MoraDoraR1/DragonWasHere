using System;

namespace DWH.Core
{
    /// <summary>
    /// 게임 공식 모음. 수치는 받지 않고 인자로만 받는다 (값은 데이터 에셋에 있다).
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

        /// <summary>건물 1개 골드 = round(원본HP × 골드계수 × 유형배율) (3.4)</summary>
        public static double BuildingGold(double rawHp, double goldPerHp, double typeMultiplier) =>
            Round(rawHp * goldPerHp * typeMultiplier);

        /// <summary>실제 인게임 HP = round(원본HP × 허들배율) (3.3)</summary>
        public static double ActualHp(double rawHp, double hurdleMultiplier) =>
            Round(rawHp * hurdleMultiplier);

        /// <summary>목표 반복 횟수 = min(최대, 기본 + 시대당 추가 × (시대-1)) (3.3)</summary>
        public static double TargetRepeat(double baseRepeat, double perEra, double max, int era) =>
            Math.Min(max, baseRepeat + perEra * (era - 1));

        /// <summary>드랍 개수 = round(기준 × (1+스테이지 증가율)^(순번-1) × (1+시대 증가율)^(시대-1)) (4.6)</summary>
        public static int DropCount(double baseCount, double growthPerStage, double growthPerEra, int stageIndex, int era) =>
            (int)Round(baseCount * Math.Pow(1 + growthPerStage, stageIndex - 1) * Math.Pow(1 + growthPerEra, era - 1));

        /// <summary>공중 유닛 골드 = round(일반 골드 + (공격 골드 - 일반 골드) × 비율) (4.5)</summary>
        public static double AirUnitGold(double normalGold, double attackGold, double ratio) =>
            Round(normalGold + (attackGold - normalGold) * ratio);

        /// <summary>업그레이드 비용 = ceil(초기 비용 × 배수^현재레벨) (4.8)</summary>
        public static double UpgradeCost(double initialCost, double multiplier, int currentLevel) =>
            Ceil(initialCost * Math.Pow(multiplier, currentLevel));

        /// <summary>환생 포인트 = floor(sqrt(회차 누적 획득 골드) / 나누는 값) (4.9)</summary>
        public static double RebirthPoints(double runGold, double divisor) =>
            runGold <= 0 ? 0 : Floor(Math.Sqrt(runGold) / divisor);

        /// <summary>피로(t) = 시작값 + floor(t / 간격) × 증가량 (8.1). 초당 기력 감소량과 같다.</summary>
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
