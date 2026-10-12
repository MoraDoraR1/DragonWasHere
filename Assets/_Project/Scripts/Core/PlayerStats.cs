using System;
using System.Collections.Generic;
using DWH.Data;

namespace DWH.Core
{
    /// <summary>
    /// 드래곤 기본 능력치(4.1.1)에 업그레이드(4.8)와 환생 보너스(4.9)를 반영한 최종 능력치.
    /// 효과 적용 방식: Add = 기본 + 값 × 레벨 / AddPercent = 기본 × (1 + 값 × 레벨) / Multiply = 기본 × 값^레벨
    /// </summary>
    public class PlayerStats
    {
        public double AttackPower;
        public double AttacksPerSecond;
        public double MaxHp;
        public double HpRegenPerSecond;
        public double MaxStamina;
        public double StaminaOnDestroy;
        public double StaminaOnClear;
        public double MoveSpeed;
        public double JumpHeight;
        public double JumpChance;
        /// <summary>점프 착지 타격 배율. 기본 1.0, 중량 강화 레벨당 +0.2 (8.6)</summary>
        public double LandingDamage;
        public double CritChance;
        public double CritMultiplier;

        /// <summary>골드 획득 배율 = (1 + 탐욕) × (1 + 황금 혈통) (환생 특성, 4.9)</summary>
        public double GoldMultiplier;
        /// <summary>감별안: 일반 스테이지 건물이 황금화될 확률</summary>
        public double GoldenChance;
        /// <summary>연쇄 파괴: 스택당 보너스 골드 비율</summary>
        public double ChainGoldPerStack;
        /// <summary>보물 더미: 클리어 시 획득 골드에 더하는 비율</summary>
        public double ClearGoldBonus;
        /// <summary>몸통 박치기 대미지 배율 (스킬 배율 × 레벨 강화)</summary>
        public double BodySlamMultiplier;

        /// <summary>성장 단계와 해츨링 대비 크기 (4.1.4). Growth.Apply가 채운다</summary>
        public GrowthStage GrowthStage = GrowthStage.Hatchling;
        public double SizeScale = 1;

        public readonly Dictionary<PassiveSkillId, int> SkillLevels = new();
        /// <summary>스킬별 하위 업그레이드 단계 (돌진, 개수 증가, 화상 …)</summary>
        public readonly Dictionary<PassiveSkillId, int> SubLevels = new();

        public int SkillLevel(PassiveSkillId skill) => SkillLevels.TryGetValue(skill, out int l) ? l : 0;
        public int SubLevel(PassiveSkillId skill) => SubLevels.TryGetValue(skill, out int l) ? l : 0;

        /// <param name="nodeLevel">노드 이름 → 현재 레벨 (초기 레벨 포함)</param>
        /// <param name="traitLevel">환생 특성 → 레벨. null이면 모두 0</param>
        public static PlayerStats Compute(GameDatabase db, Func<UpgradeNodeRow, int> nodeLevel, Func<RebirthTraitId, int> traitLevel = null)
        {
            double Trait(RebirthTraitEffect effect)
            {
                if (traitLevel == null || db.rebirthTraits == null) return 0;
                double sum = 0;
                foreach (var t in db.rebirthTraits.rows)
                    if (t.effect == effect) sum += t.effectValue * traitLevel(t.trait);
                return sum;
            }

            var b = db.dragon;
            var values = new Dictionary<UpgradeEffect, double>
            {
                [UpgradeEffect.AttackPower] = b.attackPower,
                [UpgradeEffect.MaxHp] = b.maxHp,
                [UpgradeEffect.MaxStamina] = b.maxStamina,
                [UpgradeEffect.StaminaOnDestroy] = b.staminaOnDestroy,
                [UpgradeEffect.StaminaOnClear] = b.staminaOnClear,
                [UpgradeEffect.HpRegenPercent] = 0,
                [UpgradeEffect.MoveSpeed] = b.moveSpeed,
                [UpgradeEffect.JumpHeight] = b.jumpHeight,
                [UpgradeEffect.CritChance] = b.critChance,
                [UpgradeEffect.CritMultiplier] = b.critMultiplier,
                [UpgradeEffect.GoldBonus] = 0,
                [UpgradeEffect.GoldenChance] = 0,
                [UpgradeEffect.ChainGoldBonus] = 0,
                [UpgradeEffect.ClearGoldBonus] = 0,
                [UpgradeEffect.LandingDamage] = 1,
            };

            var stats = new PlayerStats();
            foreach (var node in db.upgradeNodes.rows)
            {
                int level = nodeLevel(node);
                if (node.effect == UpgradeEffect.SkillLevel)
                {
                    stats.SkillLevels[(PassiveSkillId)Enum.Parse(typeof(PassiveSkillId), node.target)] = level;
                    continue;
                }
                if (node.effect == UpgradeEffect.SubUpgradeLevel)
                {
                    stats.SubLevels[(PassiveSkillId)Enum.Parse(typeof(PassiveSkillId), node.target)] = level;
                    continue;
                }
                if (level == 0)
                    continue;
                values[node.effect] = Apply(values[node.effect], node.effectMode, node.effectValue, level);
            }

            // 태고의 분노: 모든 공격력 영구 강화 (트리 효과 위에 곱한다)
            stats.AttackPower = values[UpgradeEffect.AttackPower] * (1 + Trait(RebirthTraitEffect.AttackPercent));
            stats.AttacksPerSecond = b.attacksPerSecond;
            stats.MaxHp = values[UpgradeEffect.MaxHp];
            stats.HpRegenPerSecond = b.hpRegenPerSecond + stats.MaxHp * values[UpgradeEffect.HpRegenPercent];
            stats.MaxStamina = values[UpgradeEffect.MaxStamina] + Trait(RebirthTraitEffect.MaxStamina);
            stats.StaminaOnDestroy = values[UpgradeEffect.StaminaOnDestroy];
            stats.StaminaOnClear = values[UpgradeEffect.StaminaOnClear];
            stats.MoveSpeed = values[UpgradeEffect.MoveSpeed];
            stats.JumpHeight = values[UpgradeEffect.JumpHeight];
            stats.CritChance = Math.Min(1.0, values[UpgradeEffect.CritChance]);
            stats.CritMultiplier = values[UpgradeEffect.CritMultiplier];
            stats.GoldMultiplier = (1 + values[UpgradeEffect.GoldBonus]) * (1 + Trait(RebirthTraitEffect.GoldPercent));
            stats.GoldenChance = values[UpgradeEffect.GoldenChance];
            stats.ChainGoldPerStack = values[UpgradeEffect.ChainGoldBonus];
            stats.ClearGoldBonus = values[UpgradeEffect.ClearGoldBonus];
            stats.LandingDamage = values[UpgradeEffect.LandingDamage];

            // 몸통 박치기: 기본 보유(1레벨). 2레벨부터 레벨당 공격력 +50%, 점프 확률 +5% (4.3)
            var bodySlam = db.passiveSkills.rows.Find(s => s.skill == PassiveSkillId.BodySlam);
            int bodySlamLevel = stats.SkillLevels.TryGetValue(PassiveSkillId.BodySlam, out int l) ? Math.Max(1, l) : 1;
            stats.BodySlamMultiplier = bodySlam.damageMultiplier * (1 + bodySlam.perLevelDamageBonus * (bodySlamLevel - 1));
            stats.JumpChance = Math.Min(b.jumpChanceMax, b.jumpChance + bodySlam.perLevelExtra * (bodySlamLevel - 1));
            return stats;
        }

        public static double Apply(double baseValue, EffectMode mode, double value, int level) => mode switch
        {
            EffectMode.Add => baseValue + value * level,
            EffectMode.AddPercent => baseValue * (1 + value * level),
            EffectMode.Multiply => baseValue * Math.Pow(value, level),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        /// <summary>업그레이드가 없는 기본 능력치</summary>
        public static PlayerStats Base(GameDatabase db) => Compute(db, n => n.initialLevel);
    }
}
