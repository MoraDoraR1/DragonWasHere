namespace DWH.Data
{
    // 데이터 테이블 enum 컬럼은 정수다. 값의 의미는 Docs/Data/enum.csv의 (enum_type, value)에 있고, 여기 값과 같게 맞춘다.
    // 0은 테이블에 없는 값이다. 빈칸(해당 없음)인 선택 컬럼은 0(None)으로 읽는다.

    /// <summary>enum building_type</summary>
    public enum BuildingType { None = 0, Normal = 1, Attack = 2, Landmark = 3, Golden = 4 }

    /// <summary>enum stage_type (MVP는 mvp_stage_type을 같은 값으로 쓴다)</summary>
    public enum StageType { None = 0, Normal = 1, Boss = 2 }

    /// <summary>enum growth_stage</summary>
    public enum GrowthStage { None = 0, Hatchling = 1, Juvenile = 2, Adult = 3, Ancient = 4 }

    /// <summary>enum dragon_part_type</summary>
    public enum DragonPartType { None = 0, Body = 1, Neck = 2, UpHead = 3, DownHead = 4, FrontLeg = 5, BackLeg = 6, Wing = 7, Tail = 8 }

    /// <summary>enum side: 1 앞 테마(x-1, x-2), 2 뒤 테마(x-3, x-4)</summary>
    public enum ThemeSide { Common = 0, Front = 1, Back = 2 }

    /// <summary>enum starter_grade</summary>
    public enum StarterGrade { None = 0, Great = 1, Good = 2, Fail = 3 }

    /// <summary>enum res_type</summary>
    public enum ResType
    {
        None = 0,
        BackgroundFar = 10, BackgroundMid = 11, BackgroundGround = 12,
        BuildingNormal = 20, BuildingAttack = 21, Landmark = 22,
        AirUnit = 30,
        DragonPortrait = 40, DragonPart = 41,
        DropItem = 50,
        Ui = 60, Presentation = 61,
        Bgm = 70, Sfx = 71,
        Vfx = 80,
        Video = 90, Font = 91,
    }

    /// <summary>enum upgrade_category: 트리의 갈래</summary>
    public enum UpgradeCategory { None = 0, Common = 1, Vitality = 2, Gold = 3, Stats = 4, PassiveSkill = 5 }

    /// <summary>enum value_op: 1 합산, 2 배수</summary>
    public enum ValueOp { None = 0, Add = 1, Multiply = 2 }

    /// <summary>enum skill_target</summary>
    public enum SkillTarget { None = 0, PerTarget = 1, Collision = 2, Screen = 3 }

    /// <summary>enum activation_type</summary>
    public enum ActivationType { None = 0, Instant = 1, Duration = 2 }

    /// <summary>enum effect_type. 적용 방법은 MVP 제작 가이드 6.7 표</summary>
    public enum EffectType
    {
        None = 0,
        AttackPower = 1,
        MaxStamina = 2,
        StaminaOnDestroy = 3,
        MaxHp = 4,
        HpRegen = 5,
        StaminaOnClear = 6,
        LowStaminaAttack = 7,
        GoldGain = 8,
        GoldenChance = 9,
        ComboGold = 10,
        MaxCombo = 11,
        ClearGold = 12,
        MoveSpeed = 13,
        JumpPower = 14,
        AirTime = 15,
        CritChance = 16,
        CritDamage = 17,
        BreathDamage = 18,
        BurnDamage = 19,
        BreathRange = 20,
        FireBallDamage = 21,
        FireBallCount = 22,
        ShockwaveDamage = 23,
        ShockwaveRange = 24,
        BodySlamDamage = 25,
        DashCooldown = 26,
        WhirlwindDamage = 27,
        WhirlwindRange = 28,
        WhirlwindDuration = 29,
        LightningDamage = 30,
        LightningCount = 31,
        CooldownReduction = 32,
        RebirthStartGold = 33,
        UpgradeDiscount = 34,
        AutoUnlockSkills = 35,
        ActiveDamage = 36,
        MeteorCount = 37,
        ActiveDuration = 38,
    }

    // ───── 테이블에 없는 게임 내부 구분 ─────

    /// <summary>드랍을 쏟는 대상. 골드 보너스 적용 규칙이 다르다</summary>
    public enum DropSource { Building, AirUnit }

    /// <summary>모션 슬롯 (임시unity제작.md 4.1.5). 같은 슬롯 스킬은 순서대로 발동한다</summary>
    public enum MotionSlot { Head, Body }

    /// <summary>패시브 스킬 6종 + 돌진(몸통 박치기 하위 업그레이드)</summary>
    public enum PassiveSkillId { BodySlam, Dash, FireBreath, FireBall, Shockwave, Whirlwind, Lightning }

    /// <summary>액티브 스킬 3종. active_skill.hotkey(1·2·3)와 순서가 같다</summary>
    public enum ActiveSkillId { Meteor = 1, Hurricane = 2, Earthquake = 3 }
}
