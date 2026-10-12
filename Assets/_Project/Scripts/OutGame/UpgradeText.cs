using System;
using System.Collections.Generic;
using DWH.Data;

namespace DWH.OutGame
{
    /// <summary>업그레이드 노드의 효과를 사람이 읽는 문장으로 만든다 (상세 패널의 "지금 → 다음")</summary>
    public static class UpgradeText
    {
        static readonly HashSet<UpgradeEffect> PercentEffects = new()
        {
            UpgradeEffect.HpRegenPercent, UpgradeEffect.CritChance, UpgradeEffect.CritMultiplier, UpgradeEffect.GoldBonus,
            UpgradeEffect.GoldenChance, UpgradeEffect.ChainGoldBonus, UpgradeEffect.ClearGoldBonus, UpgradeEffect.LandingDamage,
        };

        public static string EffectName(UpgradeNodeRow node) => node.effect switch
        {
            UpgradeEffect.AttackPower => "공격력",
            UpgradeEffect.MaxStamina => "최대 기력",
            UpgradeEffect.StaminaOnDestroy => "파괴 시 기력",
            UpgradeEffect.MaxHp => "최대 체력",
            UpgradeEffect.HpRegenPercent => "초당 체력 회복",
            UpgradeEffect.StaminaOnClear => "클리어 시 기력",
            UpgradeEffect.MoveSpeed => "이동 속도",
            UpgradeEffect.JumpHeight => "점프력",
            UpgradeEffect.CritChance => "치명타 확률",
            UpgradeEffect.CritMultiplier => "치명타 피해",
            UpgradeEffect.GoldBonus => "골드 획득",
            UpgradeEffect.GoldenChance => "황금화 확률",
            UpgradeEffect.ChainGoldBonus => "콤보당 골드",
            UpgradeEffect.ClearGoldBonus => "클리어 보너스 골드",
            UpgradeEffect.SkillLevel => "스킬 레벨",
            UpgradeEffect.SubUpgradeLevel => "단계",
            UpgradeEffect.LandingDamage => "착지 대미지",
            _ => node.effect.ToString(),
        };

        /// <summary>해당 레벨까지 쌓인 효과 합계. 예: 각성 8레벨 → "공격력 ×1.99"</summary>
        public static string Total(UpgradeNodeRow node, int level)
        {
            string name = EffectName(node);
            if (node.effect is UpgradeEffect.SkillLevel)
                return level <= 0 ? "아직 잠겨 있어요" : $"{node.nameKo} Lv {level}";
            if (node.effect is UpgradeEffect.SubUpgradeLevel)
                return $"{name} {level}";
            if (level <= 0)
                return $"{name} 그대로";

            return node.effectMode switch
            {
                EffectMode.Multiply => $"{name} ×{Math.Pow(node.effectValue, level):0.##}",
                EffectMode.AddPercent => $"{name} +{node.effectValue * level * 100:0.#}%",
                _ when PercentEffects.Contains(node.effect) => $"{name} +{node.effectValue * level * 100:0.#}%",
                _ => $"{name} +{node.effectValue * level:0.##}",
            };
        }

        /// <summary>레벨당 효과. 예: "공격력 ×1.09 (곱연산) / 레벨"</summary>
        public static string PerLevel(UpgradeNodeRow node)
        {
            string value = node.effectMode switch
            {
                EffectMode.Multiply => $"×{node.effectValue:0.###} (곱연산)",
                EffectMode.AddPercent => $"+{node.effectValue * 100:0.#}%",
                _ when PercentEffects.Contains(node.effect) => $"+{node.effectValue * 100:0.#}%",
                _ => $"+{node.effectValue:0.##}",
            };
            return node.effect is UpgradeEffect.SkillLevel or UpgradeEffect.SubUpgradeLevel
                ? "레벨당 강화"
                : $"{EffectName(node)} {value} / 레벨";
        }
    }
}
