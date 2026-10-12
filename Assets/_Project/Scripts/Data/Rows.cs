using System;

// CSV 한 행에 대응하는 데이터 클래스. 필드 이름은 CSV 머리글(snake_case)을 camelCase로 바꾼 것과 같아야 한다.
// 원본 CSV와 출처는 Docs/Data/README.md에 정리되어 있다.
namespace DWH.Data
{
    [Serializable]
    public class StageRow
    {
        public string stageId;
        public int index;
        public int era;
        public int theme;
        public string eraName;
        public string themeName;
        public int normalCount;
        public int attackCount;
        public int landmarkCount;
        public double normalHp;
        public double attackHp;
        public double landmarkHp;
        public double attackDamage;
        public double landmarkDamage;
        public int goldenCount;
        public double goldenHp;
        public HurdleGrade hurdle;
        public double targetRepeat;
        public double upgradeGoldGuide;
        public double expectedGold;

        public int Count(BuildingType type) => type switch
        {
            BuildingType.Normal => normalCount,
            BuildingType.Attack => attackCount,
            BuildingType.Landmark => landmarkCount,
            BuildingType.Golden => goldenCount,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        /// <summary>허들 배율을 곱하기 전의 원본 HP. 골드 계산에 쓴다 (3.2)</summary>
        public double RawHp(BuildingType type) => type switch
        {
            BuildingType.Normal => normalHp,
            BuildingType.Attack => attackHp,
            BuildingType.Landmark => landmarkHp,
            BuildingType.Golden => goldenHp,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    [Serializable]
    public class HurdleRow
    {
        public HurdleGrade hurdle;
        public string nameKo;
        public double normalHpMultiplier;
        public double attackHpMultiplier;
        public double landmarkHpMultiplier;
        public double targetRepeat;
        public double targetRepeatPerEra;
        public double targetRepeatMax;

        /// <summary>황금 건물은 보너스 스테이지 전용이라 허들 배율을 적용하지 않는다</summary>
        public double HpMultiplier(BuildingType type) => type switch
        {
            BuildingType.Normal => normalHpMultiplier,
            BuildingType.Attack => attackHpMultiplier,
            BuildingType.Landmark => landmarkHpMultiplier,
            _ => 1.0,
        };
    }

    [Serializable]
    public class BuildingTypeRow
    {
        public BuildingType buildingType;
        public string nameKo;
        public double goldMultiplier;
    }

    [Serializable]
    public class DropCurveRow
    {
        public DropSource source;
        public string nameKo;
        public double baseCount;
        public double growthPerStage;
        public double growthPerEra;
    }

    [Serializable]
    public class DropResourceRow
    {
        public DropResource resource;
        public string nameKo;
        public double goldValue;
        public string debutStage;
    }

    /// <summary>건물·공중 유닛 1개가 파괴될 때 쏟아지는 자원별 개수</summary>
    [Serializable]
    public class DropCountsRow
    {
        public string stageId;
        public double targetGold;
        public int targetCount;
        public int crown;
        public int diamond;
        public int ruby;
        public int sapphire;
        public int goldBar;
        public int goldCoin;
        public double totalGold;

        public int Count(DropResource resource) => resource switch
        {
            DropResource.GoldCoin => goldCoin,
            DropResource.GoldBar => goldBar,
            DropResource.Sapphire => sapphire,
            DropResource.Ruby => ruby,
            DropResource.Diamond => diamond,
            DropResource.Crown => crown,
            _ => throw new ArgumentOutOfRangeException(nameof(resource)),
        };

        public int TotalCount => crown + diamond + ruby + sapphire + goldBar + goldCoin;
    }

    [Serializable]
    public class DropCompositionRow : DropCountsRow
    {
        public BuildingType buildingType;
    }

    [Serializable]
    public class AirDropRow : DropCountsRow
    {
        public int slot;
    }

    [Serializable]
    public class AirUnitRow
    {
        public string stageId;
        public int slot;
        public string name;
        public int count;
        public double hp;
        public double damage;
        public double gold;
    }

    [Serializable]
    public class PassiveSkillRow
    {
        public PassiveSkillId skill;
        public string nameKo;
        public MotionSlot motionSlot;
        public bool unlockedByDefault;
        public double cooldown;
        public double damageMultiplier;
        public int hitCount;
        public int maxHitCount;
        public double hitInterval;
        public double duration;
        public double range;
        public double perLevelDamageBonus;
        public double perLevelExtra;
        public string subUpgrade;
        public string subUpgradeNameKo;
        public double[] subUpgradeValues;
        public double subUpgradeParam;
        public string notes;
        public string icon;
    }

    [Serializable]
    public class RebirthTraitRow
    {
        public RebirthTraitId trait;
        public string nameKo;
        public RebirthTraitEffect effect;
        /// <summary>레벨당 효과. 시작 골드는 1레벨 값</summary>
        public double effectValue;
        /// <summary>시작 골드의 레벨당 배수 (그 외 0)</summary>
        public double effectGrowth;
        /// <summary>레벨 n 비용 = first_cost + cost_step × (n - 1)</summary>
        public double firstCost;
        public double costStep;
        public int maxLevel;
        public string description;
        public string icon;
    }

    [Serializable]
    public class ActiveSkillRow
    {
        public ActiveSkillId skill;
        public int level;
        public string nameKo;
        public int hitCount;
        public double damageMultiplier;
        public double duration;
        public double tickInterval;
        public int usesPerSession;
        public double costRp;
        public string icon;
    }

    [Serializable]
    public class UpgradeNodeRow
    {
        public string node;
        public string nameKo;
        public UpgradeEffect effect;
        public string target;
        public double effectValue;
        public EffectMode effectMode;
        public int initialLevel;
        public double initialCost;
        public double costMultiplier;
        /// <summary>0이면 무제한</summary>
        public int maxLevel;
        public string[] prerequisites;
        /// <summary>false면 효과를 낼 시스템이 아직 없어 구매할 수 없다 (예: 패시브 스킬은 M6 이후)</summary>
        public bool available;
        /// <summary>트리 화면 배치(격자 단위). 중심 = 용의 각성, y는 아래로 증가</summary>
        public double treeX;
        public double treeY;
        public string description;
        /// <summary>UI 패키지 아이콘 스프라이트 이름 (Art/UIKit/icons)</summary>
        public string icon;
    }

    [Serializable]
    public class GrowthRow
    {
        public GrowthStage growthStage;
        public string nameKo;
        public double requiredSpentGold;
        public double sizeScaleFromPrevious;
        public double statMultiplier;
    }

    /// <summary>드래곤 파츠 조립표 (4.1.5). 좌표는 리소스 이미지 px, 왼쪽 위가 (0,0), Y가 아래로 증가</summary>
    [Serializable]
    public class DragonPartRow
    {
        public string part;
        public string nameKo;
        public int layerOrder;
        public string sprite;
        public string parent;
        public double pivotX;
        public double pivotY;
        public double attachX;
        public double attachY;
        public bool mirror;
    }

    [Serializable]
    public class StageStarterRow
    {
        public StarterGrade grade;
        public string nameKo;
        public double damageMultiplier;
        public int sideCount;
    }
}
