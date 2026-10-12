using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using DWH.Editor.Data;
using NUnit.Framework;
using UnityEngine;

namespace DWH.Tests
{
    /// <summary>
    /// Docs/Data/*.csv를 읽어, 공식으로 다시 계산한 값이 기획 데이터(엑셀에서 내보낸 표)와 일치하는지 확인한다.
    /// CSV나 공식을 고친 뒤 이 테스트가 깨지면 둘 중 하나가 기획과 어긋난 것이다.
    /// </summary>
    public class DataConsistencyTests
    {
        static readonly BuildingType[] BuildingTypes = (BuildingType[])Enum.GetValues(typeof(BuildingType));
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory(CsvImporter.DefaultCsvFolder);

        [OneTimeTearDown]
        public void Unload()
        {
            foreach (var field in typeof(GameDatabase).GetFields())
                if (field.GetValue(db) is ScriptableObject so) UnityEngine.Object.DestroyImmediate(so);
            UnityEngine.Object.DestroyImmediate(db);
        }

        IEnumerable<(StageRow stage, BuildingType type)> BuildingsPresent() =>
            from stage in db.stages.rows
            from type in BuildingTypes
            where stage.Count(type) > 0
            select (stage, type);

        [Test]
        public void Stages_Are28InOrder()
        {
            Assert.AreEqual(28, db.stages.rows.Count);
            for (int i = 0; i < 28; i++)
            {
                var s = db.stages.rows[i];
                Assert.AreEqual(i + 1, s.index, s.stageId);
                Assert.AreEqual(i / 4 + 1, s.era, s.stageId);
                Assert.AreEqual(i / 2 + 1, s.theme, s.stageId);
                Assert.AreEqual($"{s.era}-{i % 4 + 1}", s.stageId);
            }
        }

        [Test]
        public void Stages_LandmarkOnlyOnEvenStages()
        {
            foreach (var s in db.stages.rows)
                Assert.AreEqual(s.index % 2 == 0 ? 1 : 0, s.landmarkCount, s.stageId);
        }

        [Test]
        public void Stages_TargetRepeatMatchesHurdleFormula()
        {
            foreach (var s in db.stages.rows)
                Assert.AreEqual(s.targetRepeat, db.TargetRepeat(s), 1e-9, s.stageId);
        }

        [Test]
        public void BuildingGold_MatchesDropCompositionTargetGold()
        {
            var checkedCount = 0;
            foreach (var (stage, type) in BuildingsPresent())
            {
                Assert.AreEqual(db.Composition(type, stage.stageId).targetGold, db.BuildingGold(stage, type), $"{type} {stage.stageId}");
                checkedCount++;
            }
            Assert.AreEqual(db.dropComposition.rows.Count, checkedCount, "드랍 구성 표의 모든 행을 확인해야 한다");
        }

        [Test]
        public void DropCount_MatchesDropCompositionTargetCount()
        {
            foreach (var (stage, type) in BuildingsPresent())
            {
                var source = (DropSource)Enum.Parse(typeof(DropSource), type.ToString());
                Assert.AreEqual(db.Composition(type, stage.stageId).targetCount, db.DropCount(source, stage), $"{type} {stage.stageId}");
            }
        }

        [Test]
        public void DropCount_MatchesAirDropTargetCount()
        {
            foreach (var row in db.airDrop.rows)
            {
                var source = row.slot == 1 ? DropSource.AirUnit1 : DropSource.AirUnit2;
                Assert.AreEqual(row.targetCount, db.DropCount(source, db.Stage(row.stageId)), $"공중 {row.slot}번 {row.stageId}");
            }
        }

        [Test]
        public void DropRows_CountsAndGoldAddUp()
        {
            foreach (var row in db.dropComposition.rows.Cast<DropCountsRow>().Concat(db.airDrop.rows))
            {
                string where = $"{row.stageId} {(row as DropCompositionRow)?.buildingType.ToString() ?? "공중"}";
                Assert.AreEqual(row.targetCount, row.TotalCount, where);

                double gold = Enum.GetValues(typeof(DropResource)).Cast<DropResource>()
                    .Sum(r => row.Count(r) * db.Resource(r).goldValue);
                Assert.AreEqual(row.totalGold, gold, where);

                // 개수를 먼저 정하고 인접 2등급으로 채우므로 골드가 재화 단위로 양자화된다(엑셀 「골드 오차」 열, 최대 약 13%).
                // 오차는 쓰인 재화 중 가장 비싼 단가의 2배를 넘지 않는다(실측 최대 1.8배).
                double topUnit = Enum.GetValues(typeof(DropResource)).Cast<DropResource>()
                    .Where(r => row.Count(r) > 0).Max(r => db.Resource(r).goldValue);
                Assert.AreEqual(row.targetGold, row.totalGold, topUnit * 2, where);
            }
        }

        [Test]
        public void DropRows_NeverUseResourceBeforeDebut()
        {
            foreach (var row in db.dropComposition.rows.Cast<DropCountsRow>().Concat(db.airDrop.rows))
            {
                int stageIndex = db.Stage(row.stageId).index;
                foreach (DropResource r in Enum.GetValues(typeof(DropResource)))
                {
                    if (row.Count(r) > 0)
                        Assert.GreaterOrEqual(stageIndex, db.Stage(db.Resource(r).debutStage).index, $"{r} {row.stageId}");
                }
            }
        }

        [Test]
        public void AirUnits_StatsMatchFormulas()
        {
            Assert.AreEqual(42, db.airUnits.rows.Count, "홀수 스테이지 1종 + 짝수 스테이지 2종");
            foreach (var unit in db.airUnits.rows)
            {
                var stage = db.Stage(unit.stageId);
                string where = $"{unit.name} {unit.stageId}";
                Assert.AreEqual(unit.hp, db.AirUnitHp(stage, unit.slot), where);
                Assert.AreEqual(unit.damage, db.AirUnitDamage(stage, unit.slot), where);
                Assert.AreEqual(unit.gold, db.AirUnitGold(stage, unit.slot), where);
                Assert.AreEqual(unit.gold, db.AirDrop(unit.slot, unit.stageId).targetGold, where);
            }
        }

        [Test]
        public void AirUnits_SpawnCountsFollowSchedule()
        {
            int slots = db.balance.airUnitSpawnTimes.Length;
            foreach (var stage in db.stages.rows)
            {
                var units = db.AirUnits(stage.stageId).ToList();
                Assert.AreEqual(slots, units.Sum(u => u.count), stage.stageId);
                Assert.AreEqual(stage.index % 2 == 0 ? 2 : 1, units.Count, stage.stageId);
            }
        }

        [Test]
        public void UpgradeNodes_PrerequisitesExistAndHaveNoCycle()
        {
            var nodes = db.upgradeNodes.rows.ToDictionary(n => n.node);
            Assert.AreEqual(nodes.Count, db.upgradeNodes.rows.Count, "노드 이름 중복");
            Assert.AreEqual(14, db.upgradeNodes.rows.Count(n => n.effect != UpgradeEffect.SkillLevel && n.effect != UpgradeEffect.SubUpgradeLevel));

            foreach (var node in nodes.Values)
            {
                var visiting = new HashSet<string>();
                void Visit(string name)
                {
                    Assert.IsTrue(nodes.ContainsKey(name), $"{node.node}의 선행 노드 {name}가 없다");
                    Assert.IsTrue(visiting.Add(name), $"{node.node}에서 순환 참조");
                    foreach (var pre in nodes[name].prerequisites) Visit(pre);
                    visiting.Remove(name);
                }
                Visit(node.node);
            }
        }

        [Test]
        public void SkillNodes_TargetExistingSkills()
        {
            var skills = new HashSet<string>(Enum.GetNames(typeof(PassiveSkillId)));
            foreach (var node in db.upgradeNodes.rows.Where(n => n.effect is UpgradeEffect.SkillLevel or UpgradeEffect.SubUpgradeLevel))
                Assert.IsTrue(skills.Contains(node.target), node.node);
            foreach (PassiveSkillId skill in Enum.GetValues(typeof(PassiveSkillId)))
                Assert.AreEqual(1, db.upgradeNodes.rows.Count(n => n.effect == UpgradeEffect.SkillLevel && n.target == skill.ToString()), skill.ToString());
        }

        [Test]
        public void ActiveSkills_HaveFiveLevelsEach()
        {
            foreach (ActiveSkillId skill in Enum.GetValues(typeof(ActiveSkillId)))
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, db.activeSkills.rows.Where(r => r.skill == skill).Select(r => r.level).ToArray(), skill.ToString());
        }

        [Test]
        public void KeyValueData_Loaded()
        {
            Assert.AreEqual(20, db.dragon.attackPower);
            Assert.AreEqual(1.5, db.dragon.critMultiplier);
            CollectionAssert.AreEqual(new[] { 3.0, 10.0, 25.0 }, db.balance.airUnitSpawnTimes);
            Assert.AreEqual("1-4", db.balance.rebirthUnlockStage);
        }
    }
}
