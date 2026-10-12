using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using UnityEngine;

namespace DWH.Data
{
    /// <summary>모든 데이터 테이블의 진입점. 스테이지·건물 단위 계산은 여기서 데이터와 공식을 묶는다.</summary>
    [CreateAssetMenu(menuName = "DWH/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public StageTable stages;
        public HurdleTable hurdles;
        public BuildingTypeTable buildingTypes;
        public DropCurveTable dropCurves;
        public DropResourceTable dropResources;
        public DropCompositionTable dropComposition;
        public AirUnitTable airUnits;
        public AirDropTable airDrop;
        public PassiveSkillTable passiveSkills;
        public ActiveSkillTable activeSkills;
        public RebirthTraitTable rebirthTraits;
        public UpgradeNodeTable upgradeNodes;
        public GrowthTable growth;
        public StageStarterTable stageStarter;
        public DragonPartTable dragonParts;
        public DragonSpeciesData dragon;
        public BalanceSettings balance;

        public StageRow Stage(string stageId) =>
            stages.rows.FirstOrDefault(s => s.stageId == stageId)
            ?? throw new KeyNotFoundException($"스테이지 {stageId}가 없습니다");

        public RebirthTraitRow Trait(RebirthTraitId id) => Find(rebirthTraits.rows, t => t.trait == id, id);

        public ActiveSkillRow Active(ActiveSkillId id, int level) =>
            Find(activeSkills.rows, a => a.skill == id && a.level == level, $"{id} {level}단계");

        public HurdleRow Hurdle(HurdleGrade grade) => Find(hurdles.rows, h => h.hurdle == grade, grade);

        public BuildingTypeRow BuildingTypeInfo(BuildingType type) => Find(buildingTypes.rows, b => b.buildingType == type, type);

        public DropCurveRow DropCurve(DropSource source) => Find(dropCurves.rows, d => d.source == source, source);

        public DropResourceRow Resource(DropResource resource) => Find(dropResources.rows, r => r.resource == resource, resource);

        public DropCompositionRow Composition(BuildingType type, string stageId) =>
            Find(dropComposition.rows, d => d.buildingType == type && d.stageId == stageId, $"{type} {stageId}");

        public AirDropRow AirDrop(int slot, string stageId) =>
            Find(airDrop.rows, d => d.slot == slot && d.stageId == stageId, $"공중 {slot}번 {stageId}");

        public IEnumerable<AirUnitRow> AirUnits(string stageId) => airUnits.rows.Where(a => a.stageId == stageId);

        public double BuildingGold(StageRow stage, BuildingType type) =>
            Formulas.BuildingGold(stage.RawHp(type), balance.goldPerHp, BuildingTypeInfo(type).goldMultiplier);

        public double ActualHp(StageRow stage, BuildingType type) =>
            Formulas.ActualHp(stage.RawHp(type), Hurdle(stage.hurdle).HpMultiplier(type));

        public double TargetRepeat(StageRow stage)
        {
            var hurdle = Hurdle(stage.hurdle);
            return Formulas.TargetRepeat(hurdle.targetRepeat, hurdle.targetRepeatPerEra, hurdle.targetRepeatMax, stage.era);
        }

        public int DropCount(DropSource source, StageRow stage)
        {
            var curve = DropCurve(source);
            return Formulas.DropCount(curve.baseCount, curve.growthPerStage, curve.growthPerEra, stage.index, stage.era);
        }

        /// <summary>공중 유닛 체력: 1번은 일반 건물, 2번은 공격 건물 원본 HP 기준 (4.5)</summary>
        public double AirUnitHp(StageRow stage, int slot) =>
            Formulas.Round((slot == 1 ? stage.normalHp : stage.attackHp) * balance.AirUnitHpMultiplier(slot));

        public double AirUnitDamage(StageRow stage, int slot) =>
            Formulas.Round(stage.attackDamage * balance.AirUnitDamageMultiplier(slot));

        public double AirUnitGold(StageRow stage, int slot) =>
            Formulas.AirUnitGold(BuildingGold(stage, BuildingType.Normal), BuildingGold(stage, BuildingType.Attack), balance.AirUnitGoldRatio(slot));

        static T Find<T>(List<T> rows, Func<T, bool> match, object key) =>
            rows.FirstOrDefault(match) ?? throw new KeyNotFoundException($"{typeof(T).Name}: {key} 항목이 없습니다");
    }
}
