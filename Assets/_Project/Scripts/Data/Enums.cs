namespace DWH.Data
{
    public enum BuildingType { Normal, Attack, Landmark, Golden }

    public enum HurdleGrade { Tutorial, Breakthrough, MinorHurdle, Flat, MajorHurdle }

    /// <summary>드랍 개수 곡선을 쓰는 대상 (4.6)</summary>
    public enum DropSource { Normal, Attack, Landmark, Golden, AirUnit1, AirUnit2 }

    /// <summary>인게임 드랍 자원. 값이 클수록 상위 등급 (4.6)</summary>
    public enum DropResource { GoldCoin, GoldBar, Sapphire, Ruby, Diamond, Crown }

    public enum MotionSlot { Head, Body }

    public enum PassiveSkillId { BodySlam, FireBall, FireBreath, Lightning, Shockwave, Whirlwind }

    public enum ActiveSkillId { Meteor, Hurricane, Earthquake }

    /// <summary>환생 특성 (아웃게임 업그레이드 기획서 Ver01 p21). 환생 포인트로 사는 영구 강화</summary>
    public enum RebirthTraitId { PrimalFury, GoldenBlood, EternalHeart, ElderLegacy, DragonWisdom, AutoAwakening }

    public enum RebirthTraitEffect { AttackPercent, GoldPercent, MaxStamina, StartGold, UpgradeDiscount, AutoUnlockSkills }

    public enum UpgradeEffect
    {
        AttackPower, MaxStamina, StaminaOnDestroy, MaxHp, HpRegenPercent, StaminaOnClear,
        MoveSpeed, JumpHeight, CritChance, CritMultiplier,
        GoldBonus, GoldenChance, ChainGoldBonus, ClearGoldBonus,
        SkillLevel, SubUpgradeLevel,
        /// <summary>점프 착지 타격 배율 (중량 강화)</summary>
        LandingDamage,
    }

    /// <summary>
    /// Add: 기본값 + 값 × 레벨 / AddPercent: 기본값 × (1 + 값 × 레벨) / Multiply: 기본값 × 값^레벨
    /// </summary>
    public enum EffectMode { Add, AddPercent, Multiply }

    public enum GrowthStage { Hatchling, Juvenile, Adult, Ancient }

    public enum StarterGrade { Great, Good, Fail }
}
