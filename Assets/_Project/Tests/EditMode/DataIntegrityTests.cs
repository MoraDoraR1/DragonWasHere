using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWH.Data;
using DWH.Editor.Data;
using NUnit.Framework;

namespace DWH.Tests
{
    /// <summary>
    /// 데이터 검증 (MVP 제작 가이드 3.3): 참조 무결성, 리소스 파일 존재, MVP 경로.
    /// CSV를 바로 읽어 검사하므로 엑셀을 고치고 CSV를 다시 만든 뒤 이 테스트만 돌려도 된다.
    /// </summary>
    public class DataIntegrityTests
    {
        GameDatabase db;

        [OneTimeSetUp]
        public void Load() => db = CsvImporter.LoadInMemory();

        static void AssertNone(IEnumerable<string> problems, string what)
        {
            var list = problems.ToList();
            Assert.IsEmpty(list, $"{what} {list.Count}건:\n{string.Join("\n", list.Take(40))}");
        }

        [Test]
        public void AllTables_Loaded()
        {
            Assert.AreEqual(20, CsvImporter.Tables.Length);
            Assert.AreEqual(1, db.dragons.Count);
            Assert.AreEqual(28, db.stages.Count);
            Assert.AreEqual(28, db.upgrades.Count, "업그레이드 트리 28노드");
            Assert.AreEqual(6, db.rebirthTraits.Count);
            Assert.AreEqual(3, db.activeSkills.Count);
            Assert.AreEqual(4, db.dragonGrowths.Count);
        }

        [Test]
        public void StringKeys_Exist()
        {
            var keys = new List<(string table, string key)>();
            keys.AddRange(db.dragons.SelectMany(d => new[] { ("dragon", d.nameStr), ("dragon", d.descStr) }));
            keys.AddRange(db.dragonGrowths.Select(g => ("dragon_growth", g.nameStr)));
            keys.AddRange(db.eras.SelectMany(e => new[] { ("era", e.nameStr), ("era", e.descStr) }));
            keys.AddRange(db.themes.Select(t => ("theme", t.nameStr)));
            keys.AddRange(db.stages.Select(s => ("stage", s.nameStr)));
            keys.AddRange(db.units.Select(u => ("unit", u.nameStr)));
            keys.AddRange(db.dropItems.SelectMany(i => new[] { ("drop_item", i.nameStr), ("drop_item", i.descStr) }));
            keys.AddRange(db.starterGrades.Select(g => ("starter_grade", g.nameStr)));
            keys.AddRange(db.combos.Select(c => ("combo", c.praiseStr)));
            keys.AddRange(db.upgrades.SelectMany(u => new[] { ("upgrade", u.nameStr), ("upgrade", u.descStr) }));
            keys.AddRange(db.rebirthTraits.SelectMany(t => new[] { ("rebirth_trait", t.nameStr), ("rebirth_trait", t.descStr) }));
            keys.AddRange(db.activeSkills.SelectMany(a => new[] { ("active_skill", a.nameStr), ("active_skill", a.descStr) }));
            AssertNone(keys.Where(k => !db.HasString(k.key)).Select(k => $"{k.table}: {k.key}"), "string에 없는 문구 키");
        }

        [Test]
        public void ResourceIds_Exist()
        {
            var ids = new List<(string table, string id)>();
            ids.AddRange(db.dragons.Select(d => ("dragon", d.resId)));
            ids.AddRange(db.dragonGrowths.Select(g => ("dragon_growth", g.resId)));
            ids.AddRange(db.dragonParts.Select(p => ("dragon_part", p.resId)));
            ids.AddRange(db.stages.SelectMany(s => new[] { ("stage", s.bgFarRes), ("stage", s.bgMidRes), ("stage", s.bgGroundRes) }));
            ids.AddRange(db.buildings.SelectMany(b => b.ResIds().Select(r => ("building", r))));
            ids.AddRange(db.units.Select(u => ("unit", u.resId)));
            ids.AddRange(db.dropItems.Select(i => ("drop_item", i.resId)));
            ids.AddRange(db.starterGrades.Select(g => ("starter_grade", g.resId)));
            ids.AddRange(db.combos.Where(c => !string.IsNullOrEmpty(c.resId)).Select(c => ("combo", c.resId)));
            ids.AddRange(db.upgrades.Select(u => ("upgrade", u.resId)));
            ids.AddRange(db.rebirthTraits.Select(t => ("rebirth_trait", t.resId)));
            ids.AddRange(db.activeSkills.Select(a => ("active_skill", a.resId)));
            AssertNone(ids.Where(i => !db.HasResource(i.id)).Select(i => $"{i.table}: {i.id}"), "resource에 없는 리소스 ID");
        }

        [Test]
        public void ForeignKeys_Exist()
        {
            var stageIds = new HashSet<string>(db.stages.Select(s => s.stageId));
            var groups = new HashSet<string>(db.dropGroups.Select(d => d.dropGroup));
            var items = new HashSet<string>(db.dropItems.Select(i => i.itemId));
            var problems = new List<string>();
            problems.AddRange(db.upgrades.SelectMany(u => u.Prerequisites()).Where(p => !db.HasUpgrade(p)).Select(p => $"upgrade.prereq {p}"));
            problems.AddRange(db.activeSkills.Where(a => !db.HasUpgrade(a.prereqUpgradeId)).Select(a => $"active_skill.prereq {a.prereqUpgradeId}"));
            problems.AddRange(db.buildings.Where(b => !stageIds.Contains(b.stageId)).Select(b => $"building.stage {b.stageId}"));
            problems.AddRange(db.units.Where(u => !stageIds.Contains(u.stageId)).Select(u => $"unit.stage {u.stageId}"));
            problems.AddRange(db.starterStages.Where(s => !stageIds.Contains(s.stageId)).Select(s => $"starter_stage.stage {s.stageId}"));
            problems.AddRange(db.stages.Where(s => !string.IsNullOrEmpty(s.nextStageId) && !stageIds.Contains(s.nextStageId)).Select(s => $"stage.next {s.nextStageId}"));
            problems.AddRange(db.stages.Where(s => !string.IsNullOrEmpty(s.mvpNextStageId) && !stageIds.Contains(s.mvpNextStageId)).Select(s => $"stage.mvp_next {s.mvpNextStageId}"));
            problems.AddRange(db.stages.Where(s => db.themes.All(t => t.themeId != s.themeId)).Select(s => $"stage.theme {s.themeId}"));
            problems.AddRange(db.themes.Where(t => db.eras.All(e => e.eraId != t.eraId)).Select(t => $"theme.era {t.eraId}"));
            problems.AddRange(db.buildings.Where(b => !groups.Contains(b.dropGroup)).Select(b => $"building.drop_group {b.dropGroup}"));
            problems.AddRange(db.units.Where(u => !groups.Contains(u.dropGroup)).Select(u => $"unit.drop_group {u.dropGroup}"));
            problems.AddRange(db.dropGroups.Where(d => !items.Contains(d.itemId)).Select(d => $"drop_group.item {d.itemId}"));
            problems.AddRange(db.dragonParts.Where(p => !p.IsRoot && db.dragonParts.All(q => q.partId != p.parentPartId)).Select(p => $"dragon_part.parent {p.parentPartId}"));
            problems.AddRange(db.dragonParts.Where(p => db.dragonGrowths.All(g => g.growthId != p.growthId)).Select(p => $"dragon_part.growth {p.growthId}"));
            AssertNone(problems, "참조 무결성 위반");
        }

        [Test]
        public void ResourceFiles_ExistInArtFolder()
        {
            string root = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "Assets", "_Project", "Art");
            AssertNone(db.resources.Where(r => !File.Exists(Path.Combine(root, r.resPath))).Select(r => $"{r.resId} {r.resPath}"), "Art 폴더에 없는 리소스 파일");
        }

        [Test]
        public void MvpPath_14StagesFromStg101ToStg702()
        {
            Assert.IsTrue(db.MvpMode, "const.mvp_mode = 1");
            var orders = db.stages.Where(s => s.mvpOrder > 0).Select(s => s.mvpOrder).OrderBy(o => o).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, 14).ToList(), orders, "mvp_order 1~14가 빠짐없이 이어진다");
            var order = db.StageOrder;
            Assert.AreEqual(14, order.Count);
            Assert.AreEqual("stg101", order[0]);
            Assert.AreEqual("stg702", order[^1]);
            for (int i = 0; i < order.Count; i++)
                Assert.AreEqual(i + 1, db.Stage(order[i]).mvpOrder, $"{order[i]}의 mvp_order");
            // x-2가 보스, 라벨은 {시대}-{stage_no}
            Assert.IsTrue(order.Where((_, i) => i % 2 == 1).All(id => db.IsBoss(db.Stage(id))));
            Assert.AreEqual("3-2", db.StageLabel("stg302"));
        }

        [Test]
        public void MvpStages_HaveBuildingsUnitsAndStarter()
        {
            var problems = new List<string>();
            foreach (var id in db.StageOrder)
            {
                if (db.Building(id, BuildingType.Normal) == null) problems.Add($"{id}: 일반 건물 행 없음");
                if (db.Building(id, BuildingType.Golden) == null) problems.Add($"{id}: 황금 건물 행 없음");
                if (db.Units(id).Count() != 3) problems.Add($"{id}: 공중 유닛 {db.Units(id).Count()}행");
                if (db.starterStages.All(s => s.stageId != id)) problems.Add($"{id}: starter_stage 없음");
                if (db.IsBoss(db.Stage(id)) && db.Building(id, BuildingType.Landmark) == null) problems.Add($"{id}: 보스인데 랜드마크 없음");
            }
            AssertNone(problems, "MVP 스테이지 데이터 누락");
        }

        [Test]
        public void EffectOps_MatchEnumDescription_AndHaveNames()
        {
            var problems = new List<string>();
            void Check(string where, EffectType effect, ValueOp op)
            {
                if (effect == EffectType.None) return;
                if (EffectMath.ExpectedOp(db, effect) != op) problems.Add($"{where}: 효과 {(int)effect} 연산 {op} (enum은 {EffectMath.ExpectedOp(db, effect)})");
                if (!db.HasString($"effect_type_name_{(int)effect}")) problems.Add($"{where}: effect_type_name_{(int)effect} 없음");
            }
            foreach (var u in db.upgrades) { Check(u.upgradeId, u.effectType1, u.valueOp1); Check(u.upgradeId, u.effectType2, u.valueOp2); }
            foreach (var t in db.rebirthTraits) Check(t.traitId, t.effectType, t.valueOp);
            foreach (var a in db.activeSkills) { Check(a.skillId, a.effectType1, a.valueOp1); Check(a.skillId, a.effectType2, a.valueOp2); }
            AssertNone(problems, "효과 정의 문제");
        }

        [Test]
        public void ConstKeysUsedByCode_Exist()
        {
            string[] keys =
            {
                "bonus_min_stamina", "bonus_stamina_drain_mult", "building_spacing_px", "drop_absorb_delay_sec", "end_slowmo_sec",
                "motion_queue_interval_sec", "mouse_idle_sec", "damage_popup_sec", "combo_keep_sec", "starter_intro_sec", "starter_zoom_sec",
                "starter_fly_lap_sec", "starter_alert_sec", "starter_result_sec", "starter_leave_sec", "starter_drop_sec", "mvp_mode", "rebirth_rp_divisor",
            };
            AssertNone(keys.Where(k => db.consts.All(c => c.constKey != k)), "const에 없는 키");
        }

        [Test]
        public void Strings_HaveKoreanAndEnglish()
        {
            AssertNone(db.strings.Where(s => string.IsNullOrWhiteSpace(s.ko) || string.IsNullOrWhiteSpace(s.en)).Select(s => s.key), "한/영 중 빈 문구");
            AssertNone(UiText.Keys.Where(k =>
            {
                Localization.Current = Language.Ko;
                bool ko = !string.IsNullOrWhiteSpace(UiText.Get(k)) && UiText.Get(k) != k;
                Localization.Current = Language.En;
                bool en = !string.IsNullOrWhiteSpace(UiText.Get(k)) && UiText.Get(k) != k;
                Localization.Current = Language.Ko;
                return !(ko && en);
            }), "UiText 빈 문구");
        }

        [Test]
        public void RebirthWalls_AreMvpStages()
        {
            foreach (var wall in Tuning.RebirthWalls)
                Assert.GreaterOrEqual(db.OrderIndex(wall), 0, $"{wall}가 진행 순서에 없다");
        }

        [Test]
        public void PassiveSkills_HaveUnlockNodes()
        {
            foreach (var spec in PassiveSpecs.All)
                Assert.IsNotNull(db.UpgradeByEffect(spec.unlockEffect), $"{spec.id}: 효과 {spec.unlockEffect}가 1번 효과인 노드가 없다");
        }
    }
}
