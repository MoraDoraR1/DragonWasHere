using System;
using System.Collections.Generic;

namespace DWH.Data
{
    /// <summary>
    /// 효과 값을 모으는 방법. enum.csv effect_type의 desc("합산 · 단위 기력", "배수 · 단위 증가율" …)에서 정한다.
    /// - Sum: 값을 더한다
    /// - Rate: 증가율. 여러 출처를 (1 + v)로 곱해 합친 증가율로 돌려준다
    /// - Reduction: 감소율. (1 - v)로 곱해 합친 감소율로 돌려준다
    /// - Factor: 배율·보너스율·확률 등. 값을 곱한다(출처가 하나면 그 값)
    /// </summary>
    public enum EffectKind { Sum, Rate, Reduction, Factor }

    public static class EffectMath
    {
        /// <summary>레벨 n의 효과 값 = base + add × (n - 1). 0레벨이면 효과 없음</summary>
        public static double LevelValue(double baseValue, double addValue, int level) =>
            level <= 0 ? 0 : baseValue + (double.IsNaN(addValue) ? 0 : addValue) * (level - 1);

        public static EffectKind Kind(GameDatabase db, EffectType effect)
        {
            string desc = db.Enum("effect_type", (int)effect).desc ?? "";
            if (desc.StartsWith("합산", StringComparison.Ordinal)) return EffectKind.Sum;
            if (desc.Contains("증가율")) return EffectKind.Rate;
            if (desc.Contains("감소율")) return EffectKind.Reduction;
            return EffectKind.Factor;
        }

        /// <summary>desc 앞부분이 가리키는 연산 (합산 = Add, 배수 = Multiply). 테이블의 value_op와 같아야 한다</summary>
        public static ValueOp ExpectedOp(GameDatabase db, EffectType effect) =>
            Kind(db, effect) == EffectKind.Sum ? ValueOp.Add : ValueOp.Multiply;
    }

    /// <summary>업그레이드·환생 특성·액티브의 효과를 효과 코드별로 모은다. 적용은 가이드 6.7 표를 따른다</summary>
    public class EffectTotals
    {
        readonly GameDatabase db;
        readonly Dictionary<EffectType, double> totals = new();
        readonly Dictionary<EffectType, EffectKind> kinds = new();

        public EffectTotals(GameDatabase database) => db = database;

        public void Add(EffectType effect, double value)
        {
            if (effect == EffectType.None) return;
            if (!kinds.TryGetValue(effect, out var kind)) kinds[effect] = kind = EffectMath.Kind(db, effect);
            bool has = totals.TryGetValue(effect, out double current);
            totals[effect] = kind switch
            {
                EffectKind.Sum => (has ? current : 0) + value,
                EffectKind.Rate => (1 + (has ? current : 0)) * (1 + value) - 1,
                EffectKind.Reduction => 1 - (1 - (has ? current : 0)) * (1 - value),
                _ => (has ? current : 1) * value,
            };
        }

        public void Add(EffectType effect, double baseValue, double addValue, int level)
        {
            if (level > 0) Add(effect, EffectMath.LevelValue(baseValue, addValue, level));
        }

        public bool Has(EffectType effect) => totals.ContainsKey(effect);

        /// <summary>합친 값. 효과가 없으면 fallback</summary>
        public double Get(EffectType effect, double fallback = 0) => totals.TryGetValue(effect, out double v) ? v : fallback;
    }
}
