using System;
using System.Globalization;
using DWH.Data;

namespace DWH.OutGame
{
    /// <summary>
    /// 효과 값을 사람이 읽는 문장으로 만든다 (상세 패널의 "지금 → 다음").
    /// 효과 이름은 string 테이블의 effect_type_name_{코드}, 표기 방식은 enum.csv effect_type의 단위를 따른다.
    /// </summary>
    public static class UpgradeText
    {
        public static string EffectName(GameDatabase db, EffectType effect)
        {
            string key = $"effect_type_name_{(int)effect}";
            return db.HasString(key) ? db.Text(key) : effect.ToString();
        }

        /// <summary>enum desc의 "단위 ..." 부분</summary>
        static string Unit(GameDatabase db, EffectType effect)
        {
            string desc = db.Enum("effect_type", (int)effect).desc ?? "";
            int i = desc.IndexOf("단위", StringComparison.Ordinal);
            return i < 0 ? "" : desc.Substring(i + 2).Trim();
        }

        static string Num(double v) => v.ToString(Math.Abs(v) >= 100 ? "#,0" : "0.##", CultureInfo.InvariantCulture);

        /// <summary>효과 값 표기. 예: +4, +20%, -6%, ×1.25, 3.5%, 7초</summary>
        public static string Value(GameDatabase db, EffectType effect, double v)
        {
            var kind = EffectMath.Kind(db, effect);
            string unit = Unit(db, effect);
            switch (kind)
            {
                case EffectKind.Rate: return $"+{Num(v * 100)}%";
                case EffectKind.Reduction: return $"-{Num(v * 100)}%";
                case EffectKind.Factor:
                    return unit.Contains("배율") ? $"×{Num(v)}" : $"{Num(v * 100)}%";
                default:
                    if (unit.Contains("확률")) return $"+{Num(v * 100)}%";
                    if (unit.EndsWith("초", StringComparison.Ordinal)) return $"{Num(v)}{(Localization.Current == Language.En ? "s" : "초")}";
                    return $"{(v >= 0 ? "+" : "")}{Num(v)}";
            }
        }

        /// <summary>"효과 이름 값" 한 줄. 레벨 0이면 null</summary>
        public static string Line(GameDatabase db, EffectType effect, double baseValue, double addValue, int level) =>
            level <= 0 || effect == EffectType.None ? null : $"{EffectName(db, effect)} {Value(db, effect, EffectMath.LevelValue(baseValue, addValue, level))}";

        /// <summary>노드의 효과 1·2를 레벨 기준으로 적는다. 레벨 0이면 null</summary>
        public static string Total(GameDatabase db, UpgradeRow node, int level)
        {
            string a = Line(db, node.effectType1, node.baseValue1, node.addValue1, level);
            string b = node.effectType2 == EffectType.None ? null : Line(db, node.effectType2, node.baseValue2, node.addValue2, level);
            return a == null ? null : b == null ? a : $"{a}\n{b}";
        }

        public static string Total(GameDatabase db, RebirthTraitRow row, int level) => Line(db, row.effectType, row.baseValue, row.addValue, level);

        public static string Total(GameDatabase db, ActiveSkillRow row, int level)
        {
            string a = Line(db, row.effectType1, row.baseValue1, row.addValue1, level);
            string b = Line(db, row.effectType2, row.baseValue2, row.addValue2, level);
            return a == null ? null : b == null ? a : $"{a}\n{b}";
        }
    }
}
