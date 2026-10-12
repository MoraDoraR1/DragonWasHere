using System.Linq;
using DWH.Data;

namespace DWH.Core
{
    /// <summary>
    /// 드래곤 성장 단계 (4.1.4). 누적 사용 골드(평생, 환생해도 유지)로 해츨링 → 청소년 → 성체 → 고룡이 된다.
    /// 크기는 이전 단계 대비 배율을 곱해 나간다(청소년 ×1.55, 성체 ×1.45, 고룡 ×1.25). 능력치 보정은 기본 1.0 (T-06).
    /// </summary>
    public static class Growth
    {
        public static GrowthRow Stage(GameDatabase db, double lifetimeSpent) =>
            db.growth.rows.OrderBy(g => g.requiredSpentGold).Last(g => g.requiredSpentGold <= lifetimeSpent);

        /// <summary>다음 단계. 마지막 단계면 null</summary>
        public static GrowthRow Next(GameDatabase db, double lifetimeSpent) =>
            db.growth.rows.OrderBy(g => g.requiredSpentGold).FirstOrDefault(g => g.requiredSpentGold > lifetimeSpent);

        /// <summary>해츨링 대비 크기 배율</summary>
        public static double SizeScale(GameDatabase db, double lifetimeSpent) =>
            db.growth.rows.Where(g => g.requiredSpentGold <= lifetimeSpent).Aggregate(1.0, (s, g) => s * g.sizeScaleFromPrevious);

        /// <summary>능력치에 성장 단계를 반영한다: 크기, 점프 높이(몸집에 비례), 공격력·체력 보정</summary>
        public static void Apply(GameDatabase db, PlayerStats stats, double lifetimeSpent)
        {
            var stage = Stage(db, lifetimeSpent);
            stats.GrowthStage = stage.growthStage;
            stats.SizeScale = SizeScale(db, lifetimeSpent);
            stats.JumpHeight *= stats.SizeScale;
            stats.AttackPower *= stage.statMultiplier;
            stats.MaxHp *= stage.statMultiplier;
        }
    }
}
