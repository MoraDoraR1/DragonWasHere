using System;
using System.Collections.Generic;
using DWH.Data;

namespace DWH.Core
{
    /// <summary>
    /// 드래곤 기본 능력치(dragon)에 업그레이드(upgrade)와 환생 특성(rebirth_trait) 효과를 반영한 최종 능력치.
    /// 효과 코드별 적용은 MVP 제작 가이드 6.7 표를 따른다. 거리·속도는 unit 단위로 바꿔 둔다.
    /// </summary>
    public class PlayerStats
    {
        public double AttackPower;
        public double AttacksPerSecond;
        public double MaxHp;
        public double HpRegenPerSecond;
        public double MaxStamina;
        public double FatigueStart;
        public double FatigueInterval;
        /// <summary>건물 1개 파괴마다 기력 회복 (랜드마크 ×5)</summary>
        public double StaminaOnDestroy;
        public double StaminaOnClear;
        /// <summary>기력 25% 이하일 때 공격력 증가율 (효과 7)</summary>
        public double LowStaminaAttack;
        /// <summary>unit/s</summary>
        public double MoveSpeed;
        /// <summary>unit</summary>
        public double JumpHeight;
        /// <summary>점프 성능 배율 (효과 14) [임시 해석]: 지붕 위로 뛰는 여유 높이에 곱한다</summary>
        public double JumpPowerMultiplier = 1;
        /// <summary>체공 시간 배율 (효과 15) [임시 해석]</summary>
        public double AirTimeMultiplier = 1;
        public double JumpChance;
        /// <summary>착지 타격 배율 (dragon.landing_dmg_pct)</summary>
        public double LandingDamage;
        public double CritChance;
        public double CritMultiplier;
        public double KnockbackSeconds;

        /// <summary>골드 획득 배율 = (1 + 탐욕) × (1 + 황금 혈통)</summary>
        public double GoldMultiplier = 1;
        /// <summary>일반 스테이지 건물이 황금화될 확률 (효과 9)</summary>
        public double GoldenChance;
        /// <summary>콤보 1스택마다 골드 보너스율 (효과 10)</summary>
        public double ComboGoldPerStack;
        /// <summary>콤보 골드 스택 상한 (효과 11)</summary>
        public int MaxComboStacks;
        /// <summary>클리어 시 이번 스테이지 골드 × 이 값 추가 (효과 12)</summary>
        public double ClearGoldBonus;
        /// <summary>모든 패시브 쿨타임 배율 = 1 - 감소율 (효과 32)</summary>
        public double CooldownMultiplier = 1;

        // ───── 패시브 스킬 (6.8) ─────
        /// <summary>해금된 패시브 (몸통 박치기는 기본 보유)</summary>
        public readonly HashSet<PassiveSkillId> Skills = new() { PassiveSkillId.BodySlam };
        public double BodySlamMultiplier = 1;
        public double BreathMultiplier = 1;
        /// <summary>화상: 초당 공격력 × 이 값, 3초 (효과 19). 0이면 없음</summary>
        public double BurnRate;
        public double BreathRangeMultiplier = 1;
        public double FireBallMultiplier = 1;
        public int FireBallExtra;
        public double ShockwaveMultiplier = 1;
        /// <summary>충격파가 닿는 양옆 건물 수 (효과 24). 0이면 맞닿은 1개만</summary>
        public int ShockwaveSides;
        /// <summary>돌진 쿨타임(초) (효과 26)</summary>
        public double DashCooldown;
        public double WhirlwindMultiplier = 1;
        public double WhirlwindRangeMultiplier = 1;
        public double WhirlwindExtraSeconds;
        public double LightningMultiplier = 1;
        public int LightningExtra;

        /// <summary>성장 단계 (외형만 바뀐다)</summary>
        public DragonGrowthRow Growth;

        public bool Has(PassiveSkillId skill) => Skills.Contains(skill);

        /// <param name="upgradeLevel">upgrade_id → 현재 레벨</param>
        /// <param name="traitLevel">trait_id → 레벨. null이면 모두 0</param>
        /// <param name="growthGold">성장 단계 기준 골드(이번 회차 업그레이드 누적 사용 골드)</param>
        public static PlayerStats Compute(GameDatabase db, Func<string, int> upgradeLevel, Func<string, int> traitLevel = null, double growthGold = 0)
        {
            var effects = Effects(db, upgradeLevel, traitLevel);
            var d = db.Dragon;
            double E(EffectType t) => effects.Get(t);

            var s = new PlayerStats
            {
                AttackPower = d.atk * (1 + E(EffectType.AttackPower)),
                AttacksPerSecond = d.atkSpeed,
                MaxHp = d.hp * (1 + E(EffectType.MaxHp)),
                MaxStamina = d.stamina + E(EffectType.MaxStamina),
                FatigueStart = d.fatigueStart,
                FatigueInterval = d.fatigueIntervalSec,
                StaminaOnDestroy = d.staminaOnKill + E(EffectType.StaminaOnDestroy),
                StaminaOnClear = d.staminaOnClear + E(EffectType.StaminaOnClear),
                LowStaminaAttack = E(EffectType.LowStaminaAttack),
                MoveSpeed = Tuning.Px(d.moveSpeed) * (1 + E(EffectType.MoveSpeed)),
                JumpHeight = Tuning.Px(d.jumpHeight) * (1 + E(EffectType.JumpPower)),
                JumpPowerMultiplier = 1 + E(EffectType.JumpPower),
                AirTimeMultiplier = 1 + E(EffectType.AirTime),
                JumpChance = d.jumpRate,
                LandingDamage = d.landingDmgPct / 100.0,
                CritChance = Math.Min(1.0, d.critRate + E(EffectType.CritChance)),
                CritMultiplier = effects.Has(EffectType.CritDamage) ? E(EffectType.CritDamage) : d.critDmgPct / 100.0,
                KnockbackSeconds = d.knockbackSec,
                GoldMultiplier = 1 + E(EffectType.GoldGain),
                GoldenChance = E(EffectType.GoldenChance),
                ComboGoldPerStack = E(EffectType.ComboGold),
                MaxComboStacks = (int)Math.Floor(E(EffectType.MaxCombo)),
                ClearGoldBonus = E(EffectType.ClearGold),
                CooldownMultiplier = 1 - E(EffectType.CooldownReduction),
                BodySlamMultiplier = PassiveSpecs.BodySlam.damage * (1 + E(EffectType.BodySlamDamage)),
                BreathMultiplier = PassiveSpecs.FireBreath.damage * (1 + E(EffectType.BreathDamage)),
                BurnRate = E(EffectType.BurnDamage),
                BreathRangeMultiplier = 1 + E(EffectType.BreathRange),
                FireBallMultiplier = PassiveSpecs.FireBall.damage * effects.Get(EffectType.FireBallDamage, 1),
                FireBallExtra = (int)Math.Floor(E(EffectType.FireBallCount)),
                ShockwaveMultiplier = PassiveSpecs.Shockwave.damage * effects.Get(EffectType.ShockwaveDamage, 1),
                ShockwaveSides = (int)Math.Floor(E(EffectType.ShockwaveRange)),
                DashCooldown = E(EffectType.DashCooldown),
                WhirlwindMultiplier = PassiveSpecs.Whirlwind.damage * effects.Get(EffectType.WhirlwindDamage, 1),
                WhirlwindRangeMultiplier = 1 + E(EffectType.WhirlwindRange),
                WhirlwindExtraSeconds = E(EffectType.WhirlwindDuration),
                LightningMultiplier = PassiveSpecs.Lightning.damage * effects.Get(EffectType.LightningDamage, 1),
                LightningExtra = (int)Math.Floor(E(EffectType.LightningCount)),
            };
            s.HpRegenPerSecond = d.hpRegen + s.MaxHp * E(EffectType.HpRegen);

            // 패시브 해금: 그 스킬의 노드(효과 1번이 스킬 효과)가 1레벨 이상 (가이드 6.8)
            foreach (var spec in PassiveSpecs.All)
            {
                if (spec.id == PassiveSkillId.BodySlam) continue;
                var node = db.UpgradeByEffect(spec.unlockEffect);
                if (node != null && upgradeLevel(node.upgradeId) >= 1) s.Skills.Add(spec.id);
            }

            s.Growth = Core.Growth.Stage(db, growthGold);
            return s;
        }

        /// <summary>업그레이드·환생 특성 효과를 효과 코드별로 합친다 (가이드 6.7)</summary>
        public static EffectTotals Effects(GameDatabase db, Func<string, int> upgradeLevel, Func<string, int> traitLevel = null)
        {
            var effects = new EffectTotals(db);
            foreach (var u in db.upgrades)
            {
                int level = upgradeLevel(u.upgradeId);
                if (level <= 0) continue;
                effects.Add(u.effectType1, u.baseValue1, u.addValue1, level);
                if (u.effectType2 != EffectType.None) effects.Add(u.effectType2, u.baseValue2, u.addValue2, level);
            }
            if (traitLevel != null)
            {
                foreach (var t in db.rebirthTraits)
                {
                    int level = traitLevel(t.traitId);
                    if (level > 0) effects.Add(t.effectType, t.baseValue, t.addValue, level);
                }
            }
            return effects;
        }

        /// <summary>업그레이드가 없는 기본 능력치</summary>
        public static PlayerStats Base(GameDatabase db) => Compute(db, _ => 0);

        /// <summary>현재 기력 비율에 맞는 공격력 (광란: 기력 25% 이하이면 증가)</summary>
        public double AttackAt(double stamina, double maxStamina) =>
            LowStaminaAttack > 0 && maxStamina > 0 && stamina <= maxStamina * Tuning.LowStaminaRatio
                ? AttackPower * (1 + LowStaminaAttack)
                : AttackPower;
    }
}
