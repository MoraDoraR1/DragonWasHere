#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame;
using DWH.InGame.Dragon;
using DWH.InGame.Stage;
using DWH.InGame.Starter;
using DWH.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 전투 단위 검증 (가이드 M2~M8). 자동 모드(마우스 입력 없음)로 돌리고 프레임 시간을 1/60초로 고정해 결과가 매번 같다.
    /// 화면은 Logs/Screens에 저장한다.
    /// </summary>
    public class MvpBattleTests
    {
        const int FrameRate = 60;
        GameDatabase db;
        ArtCatalog art;
        Camera camera;
        BattleController battle;

        [OneTimeSetUp]
        public void ClearScreens() => TestScreens.Clear();

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            Assert.IsNotNull(db, "DWH/Build All을 먼저 실행해야 한다");
            Assert.IsNotNull(art, "DWH/Build All을 먼저 실행해야 한다");
            UiTheme.Active = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/_Project/Data/UiTheme.asset");
            UiText.Database = db;
            Localization.Current = Language.Ko;
            camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Time.captureFramerate = FrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (battle != null) Object.Destroy(battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            Object.Destroy(camera.gameObject);
        }

        BattleController Create(string stageId, BattleMode mode = BattleMode.Normal, PlayerStats stats = null, IStarterInput starter = null)
        {
            battle = BattleController.Create(db, art, stageId, camera, new AutoOnlyInput(), mode, null, stats, starter);
            BattleHud.Create(battle, art: art);
            return battle;
        }

        IEnumerator RunUntilFinished(float timeout = 150f)
        {
            for (float t = 0; t < timeout && !battle.IsFinished; t += 1f / FrameRate)
                yield return null;
            var v = battle.Dragon.Vitals;
            Debug.Log($"[DWH] {battle.Stage.stageId} {battle.Mode} 결과 {battle.State}: {v.Elapsed:0.0}초, 파괴 {battle.DestroyedCount}/{battle.Buildings.Count}, " +
                      $"체력 {v.Hp:0}/{v.MaxHp:0}, 기력 {v.Stamina:0.0}/{v.MaxStamina:0}, 골드 {battle.Drops.CollectedGold + battle.Drops.PendingGold:0}, 보너스 {(battle.BonusEligible ? "진입" : "불가")}");
        }

        void Destroy()
        {
            Object.Destroy(battle.gameObject);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(canvas.gameObject);
            battle = null;
        }

        static PlayerStats StatsWith(GameDatabase db, Dictionary<string, int> levels) =>
            PlayerStats.Compute(db, id => levels.TryGetValue(id, out int l) ? l : 0);

        [UnityTest]
        public IEnumerator AllMvpStages_BuildFromTablesWithArt()
        {
            foreach (var stageId in db.StageOrder)
            {
                Create(stageId);
                int expected = db.Buildings(stageId).Where(b => b.buildingType != BuildingType.Golden).Sum(b => b.count);
                Assert.AreEqual(expected, battle.Buildings.Count, $"{stageId} 건물 수");
                Assert.IsTrue(battle.Buildings.All(b => b.Sprite != null), $"{stageId} 건물 그림");
                Assert.AreEqual(db.Building(stageId, BuildingType.Normal).hp, battle.Buildings.First(b => b.Type == BuildingType.Normal).MaxHp, "hp는 테이블 최종값");
                Assert.AreEqual(art.Sprite(db.Stage(stageId).bgFarRes), battle.Background.FarSprite);
                if (db.IsBoss(db.Stage(stageId)))
                    Assert.AreEqual(1, battle.Buildings.Count(b => b.Type == BuildingType.Landmark), $"{stageId} 랜드마크");
                yield return TestScreens.Capture(camera, $"stage_{db.StageLabel(stageId)}");
                Destroy();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Bonus_GoldenBuildingsOnly_DoubleDrain()
        {
            Create("stg102", BattleMode.Bonus);
            var golden = db.Building("stg102", BuildingType.Golden);
            Assert.AreEqual(golden.count, battle.Buildings.Count);
            Assert.IsTrue(battle.Buildings.All(b => b.Type == BuildingType.Golden && b.LooksGolden));
            Assert.IsNull(battle.AirUnits, "보너스에는 공중 유닛이 없다");
            Assert.AreEqual(2, battle.Dragon.Vitals.DrainMultiplier);
            Assert.IsTrue(battle.Dragon.Vitals.RecoveryBlocked);
            for (int i = 0; i < FrameRate * 2; i++) yield return null;
            yield return TestScreens.Capture(camera, "bonus_1-2");
            yield return RunUntilFinished();
            Assert.AreEqual(BattleState.Cleared, battle.State, "보너스는 어떻게 끝나도 클리어");
        }

        /// <summary>가이드 9장: 업그레이드 없이 1-1을 첫 판에 클리어하고 보너스에 들어간다</summary>
        [UnityTest]
        public IEnumerator Stage101_NoUpgrades_ClearsAndEntersBonus()
        {
            Create("stg101");
            bool shot = false;
            for (float t = 0; t < 150f && !battle.IsFinished; t += 1f / FrameRate)
            {
                yield return null;
                if (!shot && battle.Dragon.Vitals.Elapsed > 6) { shot = true; yield return TestScreens.Capture(camera, "battle_1-1"); }
            }
            yield return RunUntilFinished();
            Assert.AreEqual(BattleState.Cleared, battle.State);
            Assert.IsTrue(battle.BonusEligible, $"남은 기력 {battle.Dragon.Vitals.Stamina:0.0} (기준 {db.Const("bonus_min_stamina")})");
        }

        [UnityTest]
        public IEnumerator Drops_GoldEqualsDropGroups()
        {
            Create("stg101");
            yield return RunUntilFinished();
            battle.Drops.CollectAll();
            double expected = battle.Buildings.Where(b => !b.IsAlive).Sum(b => db.DropGroupGold(b.Row.dropGroup))
                              + (battle.AirUnits?.Units.Where(u => !u.IsAlive).Sum(u => db.DropGroupGold(u.Row.dropGroup)) ?? 0);
            Assert.AreEqual(expected, battle.Drops.CollectedGold, 1e-6, "업그레이드가 없으면 드랍 골드 = 드랍 그룹 합계");
            Assert.GreaterOrEqual(battle.Drops.MaxCombo, 1);
        }

        [UnityTest]
        public IEnumerator Passives_AllCastAndHit()
        {
            var levels = new Dictionary<string, int>
            {
                ["upg501"] = 1, ["upg502"] = 1, ["upg503"] = 1, ["upg505"] = 1, ["upg506"] = 1, ["upg508"] = 1, ["upg509"] = 1, ["upg511"] = 1,
            };
            var stats = StatsWith(db, levels);
            Create("stg202", stats: stats);
            var hit = new HashSet<PassiveSkillId>();
            battle.Skills.SkillHit += (id, _, _, _) => hit.Add(id);
            bool shot = false, breathShot = false;
            for (int i = 0; i < FrameRate * 30 && !battle.IsFinished; i++)
            {
                yield return null;
                if (!shot && hit.Count >= 3) { shot = true; yield return TestScreens.Capture(camera, "passives_2-2"); }
                // 파이어 브레스 불길 그림 확인용 (DWH-54)
                if (!breathShot && hit.Contains(PassiveSkillId.FireBreath))
                {
                    breathShot = true;
                    for (int k = 0; k < 20; k++) yield return null;   // 불길이 다 커진 뒤
                    yield return TestScreens.Capture(camera, "skill_breath");
                }
            }
            var expected = new[] { PassiveSkillId.Dash, PassiveSkillId.FireBall, PassiveSkillId.FireBreath, PassiveSkillId.Shockwave, PassiveSkillId.Whirlwind, PassiveSkillId.Lightning };
            CollectionAssert.IsSubsetOf(expected, hit, $"발동해서 때린 스킬: {string.Join(", ", hit)}");
        }

        [UnityTest]
        public IEnumerator Actives_UseConsumeAndHit()
        {
            Create("stg102");
            var levels = new Dictionary<ActiveSkillId, int> { [ActiveSkillId.Meteor] = 1, [ActiveSkillId.Hurricane] = 3, [ActiveSkillId.Earthquake] = 1 };
            var uses = DWH.InGame.Skills.ActiveSkillSystem.FullUses(levels);
            battle.Actives.Configure(levels, uses);
            Assert.AreEqual(2, uses[ActiveSkillId.Hurricane], "3단계 = 2회");
            var hit = new HashSet<ActiveSkillId>();
            battle.Actives.SkillHit += (id, _, _, _) => hit.Add(id);
            for (int i = 0; i < FrameRate * 2; i++) yield return null;
            foreach (var id in levels.Keys) Assert.IsTrue(battle.Actives.Use(id));
            Assert.IsFalse(battle.Actives.Use(ActiveSkillId.Meteor), "0회면 쓸 수 없다");
            yield return TestScreens.Capture(camera, "actives_1-2");
            // 메테오 낙하 방향·지진 균열·허리케인 확인용 (DWH-57~59)
            for (int i = 0; i < 14; i++) yield return null;
            yield return TestScreens.Capture(camera, "skill_meteor");
            for (int i = 0; i < FrameRate; i++) yield return null;
            yield return TestScreens.Capture(camera, "skill_quake");
            for (int i = 0; i < FrameRate * 3; i++) yield return null;
            CollectionAssert.AreEquivalent(levels.Keys, hit);
        }

        [UnityTest]
        public IEnumerator Growth_FourStagesUseOwnPartSets()
        {
            float lastHeight = 0;
            foreach (var growth in db.Growths)
            {
                var stats = PlayerStats.Base(db);
                stats.Growth = growth;
                Create("stg101", stats: stats);
                var renderers = battle.Dragon.Rig.GetComponentsInChildren<SpriteRenderer>();
                Assert.AreEqual(11, renderers.Length, $"{growth.growthId} 파츠 11장");
                var partSprites = db.Parts(growth.growthId).Select(p => art.Sprite(p.resId)).ToList();
                Assert.IsTrue(renderers.All(r => partSprites.Contains(r.sprite)), "그 단계의 파츠 세트");
                float height = battle.Dragon.Hitbox.height;
                Assert.Greater(height, lastHeight, "단계가 오를수록 커진다");
                lastHeight = height;
                for (int i = 0; i < 20; i++) yield return null;
                yield return TestScreens.Capture(camera, $"growth_{(int)growth.growthStage}");
                Destroy();
                yield return null;
            }
        }

        class ScriptedStarter : IStarterInput
        {
            readonly StarterGrade want;
            float aiming;
            public ScriptedStarter(StarterGrade grade) => want = grade;

            public bool Pressed(StageStarter s)
            {
                if (s.Phase == StarterPhase.Aiming) return (aiming += Time.deltaTime) >= 1.5f;
                if (s.Phase == StarterPhase.Gauge) return s.Gauge.Judge() == want;
                return false;
            }
        }

        [UnityTest]
        public IEnumerator Starter_GreatHitsTargetAndFiveEachSide()
        {
            Create("stg102", starter: new ScriptedStarter(StarterGrade.Great));
            Dictionary<DWH.InGame.Buildings.Building, double> hp = null;
            battle.Starter.Finished += _ => hp = battle.Buildings.ToDictionary(b => b, b => b.Hp);
            bool gaugeShot = false, resultShot = false;
            for (int i = 0; i < FrameRate * 30 && battle.InStarter; i++)
            {
                yield return null;
                if (!gaugeShot && battle.Starter.Phase == StarterPhase.Gauge) { gaugeShot = true; yield return TestScreens.Capture(camera, "starter_gauge"); }
                if (!resultShot && battle.Starter.Phase == StarterPhase.Result) { resultShot = true; yield return TestScreens.Capture(camera, "starter_result"); }
            }
            Assert.IsFalse(battle.InStarter);
            Assert.AreEqual(StarterGrade.Great, battle.Starter.Grade);
            // 표적 + 양옆 5개씩. 맵 끝이면 있는 건물만
            var ordered = battle.Buildings.OrderBy(b => b.CenterX).ToList();
            int t = ordered.IndexOf(battle.Starter.Target);
            int expected = System.Math.Min(ordered.Count - 1, t + 5) - System.Math.Max(0, t - 5) + 1;
            Assert.AreEqual(expected, battle.Starter.HitCount);
            double damage = PlayerStats.Base(db).AttackPower * 2;
            Assert.AreEqual(expected, hp.Count(kv => kv.Key.MaxHp - kv.Value >= damage - 1e-6 || !kv.Key.IsAlive));
            Assert.AreEqual(Tuning.GroundHeight, battle.Dragon.transform.position.y, 1e-3);
        }
    }
}
#endif
