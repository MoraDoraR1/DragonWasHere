using System;

// 데이터 테이블(Docs/20261012_데이터테이블_Ver02.xlsx → Docs/Data/*.csv) 한 행에 대응하는 클래스.
// 필드 이름은 CSV 머리글(snake_case)을 camelCase로 바꾼 것과 같아야 한다. CsvImporter가 이름으로 연결한다.
// - ID는 문자열이다(stg101, upg505). 테이블끼리는 ID로 연결된다(엑셀 ERD 시트).
// - enum 컬럼은 Enums.cs의 C# enum으로 읽는다. 값은 enum.csv와 같다.
// - [Optional] 컬럼의 빈칸은 "해당 없음"이다: 문자열 "", 정수·enum 0(None), 실수 NaN, bool false.
//   [Optional]이 없는 컬럼이 비어 있으면 임포트가 실패한다.
namespace DWH.Data
{
    /// <summary>빈칸을 허용하는 컬럼. 빈칸은 "해당 없음"으로 읽는다</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OptionalAttribute : Attribute { }

    [Serializable]
    public class ConstRow
    {
        public string constKey;
        public string value;
        public string valueType;
        [Optional] public string unit;
        public string desc;
    }

    [Serializable]
    public class EnumRow
    {
        public string enumType;
        public int value;
        public string name;
        [Optional] public string desc;
    }

    [Serializable]
    public class DragonRow
    {
        public string dragonId;
        public string nameStr;
        public string descStr;
        public double hp;
        public double hpRegen;
        public double stamina;
        public double fatigueStart;
        public double fatigueIntervalSec;
        public double staminaOnKill;
        public double staminaOnClear;
        public double atk;
        public double atkSpeed;
        /// <summary>px/s</summary>
        public double moveSpeed;
        /// <summary>px</summary>
        public double jumpHeight;
        public double jumpRate;
        public double landingDmgPct;
        public double critRate;
        public double critDmgPct;
        public double knockbackSec;
        public string resId;
    }

    [Serializable]
    public class DragonGrowthRow
    {
        public string growthId;
        public string dragonId;
        public GrowthStage growthStage;
        public string nameStr;
        /// <summary>이번 회차 업그레이드 누적 사용 골드가 이 값 이상이면 이 단계</summary>
        public double reqGold;
        public double sizePct;
        public string resId;
    }

    /// <summary>드래곤 파츠 조립표. 좌표는 이미지 px, 왼쪽 위가 (0,0), y는 아래로 증가. 몸통은 부모·좌표가 없다</summary>
    [Serializable]
    public class DragonPartRow
    {
        public string partId;
        public string growthId;
        /// <summary>1 = 맨 앞. sortingOrder = 20 - layer_order</summary>
        public int layerOrder;
        public DragonPartType partType;
        /// <summary>반대편 파츠. 같은 스프라이트를 어둡게 쓴다</summary>
        public bool isBack;
        public string resId;
        [Optional] public int pivotX;
        [Optional] public int pivotY;
        [Optional] public string parentPartId;
        [Optional] public int attachX;
        [Optional] public int attachY;

        public bool IsRoot => string.IsNullOrEmpty(parentPartId);
        /// <summary>리그 노드 이름. 반대편은 _B를 붙인다 (예: FrontLeg_B)</summary>
        public string NodeName => isBack ? partType + "_B" : partType.ToString();
    }

    [Serializable]
    public class EraRow
    {
        public string eraId;
        public string nameStr;
        public string descStr;
    }

    [Serializable]
    public class ThemeRow
    {
        public string themeId;
        public string eraId;
        public ThemeSide side;
        public string nameStr;
    }

    [Serializable]
    public class StageRow
    {
        public string stageId;
        public string themeId;
        /// <summary>시대 안 번호 1~4. HUD 표기 "{시대}-{stage_no}"</summary>
        public int stageNo;
        public StageType stageType;
        [Optional] public string nextStageId;
        public string nameStr;
        public string bgFarRes;
        public string bgMidRes;
        public string bgGroundRes;
        /// <summary>MVP 진행 순서 1~14. 0 = MVP 제외</summary>
        [Optional] public int mvpOrder;
        [Optional] public string mvpNextStageId;
        [Optional] public StageType mvpStageType;
    }

    /// <summary>스테이지의 건물 유형별 1행. hp는 허들·벽 배율이 들어간 최종값이다</summary>
    [Serializable]
    public class BuildingRow
    {
        public string buildingId;
        public string stageId;
        public BuildingType buildingType;
        public int count;
        public double hp;
        public double atk;
        public double atkSpeed;
        /// <summary>px. 공격하지 않는 건물은 0</summary>
        [Optional] public double atkRange;
        public string dropGroup;
        public string resId1;
        [Optional] public string resId2;
        [Optional] public string resId3;

        public string[] ResIds()
        {
            var list = new System.Collections.Generic.List<string>(3) { resId1 };
            if (!string.IsNullOrEmpty(resId2)) list.Add(resId2);
            if (!string.IsNullOrEmpty(resId3)) list.Add(resId3);
            return list.ToArray();
        }
    }

    /// <summary>공중 유닛. 스테이지당 3행이고 spawn_sec에 1마리씩 나온다</summary>
    [Serializable]
    public class UnitRow
    {
        public string unitId;
        public string stageId;
        public int slot;
        public double spawnSec;
        public string nameStr;
        public double hp;
        public double atk;
        public double atkSpeed;
        /// <summary>px/s</summary>
        public double moveSpeed;
        public string dropGroup;
        public string resId;
    }

    [Serializable]
    public class DropItemRow
    {
        public string itemId;
        public int tier;
        public string nameStr;
        public string descStr;
        public double goldValue;
        public string resId;
    }

    [Serializable]
    public class DropGroupRow
    {
        public string dropGroup;
        public string itemId;
        public int minCount;
        public int maxCount;
    }

    [Serializable]
    public class StarterGradeRow
    {
        public string gradeId;
        public StarterGrade grade;
        public string nameStr;
        public double dmgPct;
        public int sideCount;
        [Optional] public string gaugeColor;
        public string resId;
    }

    [Serializable]
    public class StarterStageRow
    {
        public string stageId;
        public double zoneSpeed;
        public double barSpeed;
        public double greatWidth;
        public double goodWidth;
    }

    [Serializable]
    public class ComboRow
    {
        public string comboId;
        public int minCombo;
        public string praiseStr;
        public string textColor;
        [Optional] public string resId;
    }

    [Serializable]
    public class UpgradeRow
    {
        public string upgradeId;
        public string nameStr;
        public string descStr;
        public UpgradeCategory category;
        [Optional] public string prereqId1;
        [Optional] public string prereqId2;
        /// <summary>0 = 무제한</summary>
        public int maxLevel;
        public double baseCost;
        [Optional] public double addCost;
        /// <summary>있으면 n레벨 비용 = base_cost × cost_mult^(n-1), 없으면(NaN) base_cost + add_cost × (n-1)</summary>
        [Optional] public double costMult;
        public EffectType effectType1;
        public double baseValue1;
        public double addValue1;
        public ValueOp valueOp1;
        [Optional] public EffectType effectType2;
        [Optional] public double baseValue2;
        [Optional] public double addValue2;
        [Optional] public ValueOp valueOp2;
        public string resId;

        public string[] Prerequisites()
        {
            var list = new System.Collections.Generic.List<string>(2);
            if (!string.IsNullOrEmpty(prereqId1)) list.Add(prereqId1);
            if (!string.IsNullOrEmpty(prereqId2)) list.Add(prereqId2);
            return list.ToArray();
        }
    }

    [Serializable]
    public class RebirthTraitRow
    {
        public string traitId;
        public string nameStr;
        public string descStr;
        public int maxLevel;
        public double baseCost;
        public double addCost;
        public EffectType effectType;
        public double baseValue;
        public double addValue;
        public ValueOp valueOp;
        public string resId;
    }

    [Serializable]
    public class ActiveSkillRow
    {
        public string skillId;
        public string nameStr;
        public string descStr;
        public int hotkey;
        public string prereqUpgradeId;
        public int maxLevel;
        public double baseCost;
        public double addCost;
        public EffectType effectType1;
        public double baseValue1;
        public double addValue1;
        public ValueOp valueOp1;
        public EffectType effectType2;
        public double baseValue2;
        public double addValue2;
        public ValueOp valueOp2;
        public SkillTarget targetType;
        public ActivationType activationType;
        public string resId;

        public ActiveSkillId Id => (ActiveSkillId)hotkey;
    }

    [Serializable]
    public class StringRow
    {
        public string key;
        public string ko;
        public string en;
    }

    [Serializable]
    public class ResourceRow
    {
        public string resId;
        public ResType type;
        public string resName;
        /// <summary>리소스/ 폴더 기준 경로. Unity에서는 Assets/_Project/Art/ + res_path</summary>
        public string resPath;
        [Optional] public bool loop;
        [Optional] public double volume;
        [Optional] public int frameCount;
        [Optional] public double frameRate;
        public string memo;
    }
}
