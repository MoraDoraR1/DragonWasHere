using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DWH.Data
{
    /// <summary>
    /// 데이터 테이블 20종 (Docs/Data/*.csv). DWH/Import Data(CsvImporter)가 채운다.
    /// 조회 함수는 처음 쓸 때 ID 색인을 만든다. 없는 ID를 찾으면 바로 예외를 던져 데이터 오류를 드러낸다.
    /// </summary>
    [CreateAssetMenu(menuName = "DWH/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<ConstRow> consts = new();
        public List<EnumRow> enums = new();
        public List<DragonRow> dragons = new();
        public List<DragonGrowthRow> dragonGrowths = new();
        public List<DragonPartRow> dragonParts = new();
        public List<EraRow> eras = new();
        public List<ThemeRow> themes = new();
        public List<StageRow> stages = new();
        public List<BuildingRow> buildings = new();
        public List<UnitRow> units = new();
        public List<DropItemRow> dropItems = new();
        public List<DropGroupRow> dropGroups = new();
        public List<StarterGradeRow> starterGrades = new();
        public List<StarterStageRow> starterStages = new();
        public List<ComboRow> combos = new();
        public List<UpgradeRow> upgrades = new();
        public List<RebirthTraitRow> rebirthTraits = new();
        public List<ActiveSkillRow> activeSkills = new();
        public List<StringRow> strings = new();
        public List<ResourceRow> resources = new();

        // ───── 색인 ─────

        [NonSerialized] Dictionary<string, ConstRow> constIndex;
        [NonSerialized] Dictionary<string, StringRow> stringIndex;
        [NonSerialized] Dictionary<string, ResourceRow> resourceIndex;
        [NonSerialized] Dictionary<string, StageRow> stageIndex;
        [NonSerialized] Dictionary<string, UpgradeRow> upgradeIndex;
        [NonSerialized] List<string> stageOrder;

        /// <summary>임포트로 내용이 바뀌면 색인을 다시 만든다</summary>
        public void Invalidate()
        {
            constIndex = null;
            stringIndex = null;
            resourceIndex = null;
            stageIndex = null;
            upgradeIndex = null;
            stageOrder = null;
        }

        void OnEnable() => Invalidate();

        static Dictionary<string, T> Index<T>(List<T> rows, Func<T, string> key, string table)
        {
            var dict = new Dictionary<string, T>(rows.Count);
            foreach (var row in rows)
            {
                if (dict.ContainsKey(key(row))) throw new InvalidOperationException($"{table}: ID {key(row)}가 중복됩니다");
                dict[key(row)] = row;
            }
            return dict;
        }

        static T Get<T>(Dictionary<string, T> index, string id, string table) =>
            id != null && index.TryGetValue(id, out var row) ? row : throw new KeyNotFoundException($"{table}에 {id}가 없습니다");

        // ───── const ─────

        public bool HasConst(string key) => consts.Exists(c => c.constKey == key);

        public ConstRow ConstRow(string key)
        {
            constIndex ??= Index(consts, c => c.constKey, "const");
            return Get(constIndex, key, "const");
        }

        public double Const(string key) => double.Parse(ConstRow(key).value, CultureInfo.InvariantCulture);
        public int ConstInt(string key) => (int)Math.Round(Const(key));
        public bool ConstBool(string key) => ConstRow(key).value.Trim() is "1" or "true" or "TRUE" or "True";

        // ───── 문구·리소스 ─────

        public bool HasString(string key)
        {
            stringIndex ??= Index(strings, s => s.key, "string");
            return key != null && stringIndex.ContainsKey(key);
        }

        /// <summary>문구 키의 현재 언어 문구 (Localization.Current). 영문이 비어 있으면 한글을 쓴다</summary>
        public string Text(string key)
        {
            stringIndex ??= Index(strings, s => s.key, "string");
            var row = Get(stringIndex, key, "string");
            return Localization.Current == Language.En && !string.IsNullOrEmpty(row.en) ? row.en : row.ko;
        }

        public ResourceRow Resource(string resId)
        {
            resourceIndex ??= Index(resources, r => r.resId, "resource");
            return Get(resourceIndex, resId, "resource");
        }

        public bool HasResource(string resId)
        {
            resourceIndex ??= Index(resources, r => r.resId, "resource");
            return resId != null && resourceIndex.ContainsKey(resId);
        }

        /// <summary>enum.csv의 (enum_type, value) 행</summary>
        public EnumRow Enum(string enumType, int value) =>
            enums.FirstOrDefault(e => e.enumType == enumType && e.value == value)
            ?? throw new KeyNotFoundException($"enum에 ({enumType}, {value})가 없습니다");

        // ───── 드래곤 ─────

        /// <summary>MVP는 1종(불 드래곤)이다</summary>
        public DragonRow Dragon => dragons[0];

        /// <summary>성장 단계 (요구 골드 오름차순)</summary>
        public IEnumerable<DragonGrowthRow> Growths => dragonGrowths.Where(g => g.dragonId == Dragon.dragonId).OrderBy(g => g.reqGold);

        public IEnumerable<DragonPartRow> Parts(string growthId) => dragonParts.Where(p => p.growthId == growthId);

        // ───── 스테이지 ─────

        public StageRow Stage(string stageId)
        {
            stageIndex ??= Index(stages, s => s.stageId, "stage");
            return Get(stageIndex, stageId, "stage");
        }

        /// <summary>const.mvp_mode = 1이면 MVP 14스테이지, 0이면 28스테이지 전체(CBT)</summary>
        public bool MvpMode => ConstBool("mvp_mode");

        /// <summary>진행 순서. MVP는 mvp_order = 1에서 mvp_next_stage_id를, CBT는 첫 스테이지에서 next_stage_id를 따라간다</summary>
        public IReadOnlyList<string> StageOrder
        {
            get
            {
                if (stageOrder != null) return stageOrder;
                bool mvp = MvpMode;
                var start = mvp ? stages.First(s => s.mvpOrder == 1) : stages[0];
                var order = new List<string>();
                for (var s = start; s != null;)
                {
                    if (order.Contains(s.stageId)) throw new InvalidOperationException($"stage: 진행 순서가 {s.stageId}에서 순환합니다");
                    order.Add(s.stageId);
                    string next = NextStageId(s);
                    s = string.IsNullOrEmpty(next) ? null : Stage(next);
                }
                return stageOrder = order;
            }
        }

        /// <summary>클리어하면 갈 다음 스테이지. 빈칸이면 엔딩</summary>
        public string NextStageId(StageRow stage) => MvpMode ? stage.mvpNextStageId : stage.nextStageId;

        public bool IsBoss(StageRow stage) => (MvpMode ? stage.mvpStageType : stage.stageType) == StageType.Boss;

        /// <summary>진행 순서상 0부터 센 순번. 순서에 없으면 -1</summary>
        public int OrderIndex(string stageId)
        {
            var order = StageOrder;
            for (int i = 0; i < order.Count; i++) if (order[i] == stageId) return i;
            return -1;
        }

        public ThemeRow Theme(StageRow stage) => themes.First(t => t.themeId == stage.themeId);
        public EraRow Era(StageRow stage) => eras.First(e => e.eraId == Theme(stage).eraId);

        /// <summary>시대 번호 1~7 (era 테이블 순서)</summary>
        public int EraNumber(StageRow stage) => eras.IndexOf(Era(stage)) + 1;

        /// <summary>HUD 표기 "{시대}-{stage_no}" (예: 3-2)</summary>
        public string StageLabel(StageRow stage) => $"{EraNumber(stage)}-{stage.stageNo}";
        public string StageLabel(string stageId) => StageLabel(Stage(stageId));

        public IEnumerable<BuildingRow> Buildings(string stageId) => buildings.Where(b => b.stageId == stageId);

        public BuildingRow Building(string stageId, BuildingType type) => buildings.FirstOrDefault(b => b.stageId == stageId && b.buildingType == type);

        /// <summary>공중 유닛 (등장 시각 순)</summary>
        public IEnumerable<UnitRow> Units(string stageId) => units.Where(u => u.stageId == stageId).OrderBy(u => u.spawnSec);

        public StarterStageRow StarterStage(string stageId) =>
            starterStages.FirstOrDefault(s => s.stageId == stageId) ?? throw new KeyNotFoundException($"starter_stage에 {stageId}가 없습니다");

        public StarterGradeRow StarterGrade(StarterGrade grade) =>
            starterGrades.FirstOrDefault(g => g.grade == grade) ?? throw new KeyNotFoundException($"starter_grade에 {grade}가 없습니다");

        // ───── 드랍 ─────

        public IEnumerable<DropGroupRow> DropGroup(string dropGroup) => dropGroups.Where(d => d.dropGroup == dropGroup);

        public DropItemRow DropItem(string itemId) =>
            dropItems.FirstOrDefault(i => i.itemId == itemId) ?? throw new KeyNotFoundException($"drop_item에 {itemId}가 없습니다");

        /// <summary>드랍 그룹 1회의 골드 합계 (min = max이므로 고정값. 다르면 평균)</summary>
        public double DropGroupGold(string dropGroup) =>
            DropGroup(dropGroup).Sum(d => DropItem(d.itemId).goldValue * (d.minCount + d.maxCount) / 2.0);

        // ───── 콤보 ─────

        /// <summary>콤보 수에 맞는 칭찬 단계. min_combo 미만이면 null</summary>
        public ComboRow Combo(int count) => combos.Where(c => c.minCombo <= count).OrderByDescending(c => c.minCombo).FirstOrDefault();

        // ───── 아웃게임 ─────

        public UpgradeRow Upgrade(string upgradeId)
        {
            upgradeIndex ??= Index(upgrades, u => u.upgradeId, "upgrade");
            return Get(upgradeIndex, upgradeId, "upgrade");
        }

        public bool HasUpgrade(string upgradeId)
        {
            upgradeIndex ??= Index(upgrades, u => u.upgradeId, "upgrade");
            return upgradeId != null && upgradeIndex.ContainsKey(upgradeId);
        }

        /// <summary>효과 1번이 이 효과인 업그레이드 노드 (예: 파이어 볼 피해 배율 → 파이어 볼 노드). 없으면 null</summary>
        public UpgradeRow UpgradeByEffect(EffectType effect) => upgrades.FirstOrDefault(u => u.effectType1 == effect);

        public RebirthTraitRow Trait(string traitId) =>
            rebirthTraits.FirstOrDefault(t => t.traitId == traitId) ?? throw new KeyNotFoundException($"rebirth_trait에 {traitId}가 없습니다");

        public ActiveSkillRow Active(ActiveSkillId id) =>
            activeSkills.FirstOrDefault(a => a.Id == id) ?? throw new KeyNotFoundException($"active_skill에 hotkey {(int)id}가 없습니다");
    }
}
