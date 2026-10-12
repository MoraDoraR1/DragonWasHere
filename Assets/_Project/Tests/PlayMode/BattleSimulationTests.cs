#if UNITY_EDITOR
using System.Collections;
using System.IO;
using DWH.Core;
using DWH.Data;
using DWH.InGame.Skills;
using System.Collections.Generic;
using System.Linq;
using DWH.InGame;
using DWH.InGame.Buildings;
using DWH.InGame.Dragon;
using DWH.InGame.Drops;
using DWH.InGame.Stage;
using DWH.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DWH.Tests
{
    /// <summary>
    /// 실제 전투를 자동 모드(마우스 입력 없음)로 끝까지 돌려 8.1의 밸런스 설계를 검증한다.
    /// 프레임 시간을 1/60초로 고정해 결과가 매번 같다. 진행 중 화면을 Logs/Screens에 저장한다.
    /// </summary>
    public class BattleSimulationTests
    {
        const int FrameRate = 60;
        const float TimeoutSeconds = 180f;
        static readonly string ScreenFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Screens");

        GameDatabase db;
        ArtCatalog art;
        Camera camera;
        BattleController battle;

        [OneTimeSetUp]
        public void ClearOldScreens()
        {
            // 이전 실행의 캡처가 섞이지 않게 비운다
            if (Directory.Exists(ScreenFolder))
                foreach (var file in Directory.GetFiles(ScreenFolder, "*.png")) File.Delete(file);
        }

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Data/GameDatabase.asset");
            art = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/_Project/Data/ArtCatalog.asset");
            Assert.IsNotNull(db, "DWH/Build All을 먼저 실행해야 한다");
            Assert.IsNotNull(art, "DWH/Build All을 먼저 실행해야 한다");
            camera = new GameObject("Test Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Time.captureFramerate = FrameRate;
            Directory.CreateDirectory(ScreenFolder);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (battle != null) Object.Destroy(battle.gameObject);
            Object.Destroy(camera.gameObject);
            foreach (var hud in Object.FindObjectsByType<BattleHud>(FindObjectsSortMode.None)) Object.Destroy(hud.gameObject);
        }

        IEnumerator Run(string stageId, params float[] screenshotTimes)
        {
            battle = BattleController.Create(db, art, stageId, camera, new AutoOnlyInput());
            BattleHud.Create(battle);
            int shot = 0;
            float elapsed = 0;
            while (!battle.IsFinished && elapsed < TimeoutSeconds)
            {
                yield return null;
                elapsed += 1f / FrameRate;
                if (shot < screenshotTimes.Length && battle.Dragon.Vitals.Elapsed >= screenshotTimes[shot])
                    Capture($"{stageId}_{screenshotTimes[shot++]:00}s");
            }
            Capture($"{stageId}_end");
            var v = battle.Dragon.Vitals;
            Debug.Log($"[DWH] {stageId} 결과 {battle.State}: {v.Elapsed:0.0}초, 파괴 {battle.DestroyedCount}/{battle.Buildings.Count}, " +
                      $"체력 {v.Hp:0}/{v.MaxHp:0}, 기력 {v.Stamina:0.0}/{v.MaxStamina:0}, 보너스 {(battle.BonusEligible ? "진입" : "불가")}");
        }

        void Capture(string name)
        {
            var rt = new RenderTexture(960, 540, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ScreenFolder, name + ".png"), tex.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        /// <summary>
        /// M6: 모든 패시브 스킬(3레벨, 하위 업그레이드 1단계)과 점프(몸통 박치기 7레벨 = 점프 확률 30%)를 켜고 1-2를 돌린다.
        /// 스킬마다 한 번 이상 발동해 대상을 때리고, 점프와 회오리 패링이 일어나는지 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator AllPassives_CastHitAndJump()
        {
            var levels = new Dictionary<string, int>
            {
                ["Skill_BodySlam"] = 7, ["Skill_Whirlwind"] = 3, ["Skill_Shockwave"] = 3,
                ["Skill_FireBreath"] = 3, ["Skill_FireBall"] = 3, ["Skill_Lightning"] = 3,
                ["Sub_Dash"] = 1, ["Sub_MoreSpins"] = 1, ["Sub_ShockRange"] = 1, ["Sub_Burn"] = 3, ["Sub_MoreFireBalls"] = 1, ["Sub_ChainLightning"] = 3,
                ["Weight"] = 2, ["HpBoost"] = 8, ["StaminaBoost"] = 5,
            };
            var stats = PlayerStats.Compute(db, n => levels.TryGetValue(n.node, out int l) ? l : n.initialLevel);
            battle = BattleController.Create(db, art, "1-2", camera, new AutoOnlyInput(), BattleMode.Normal, null, stats);
            BattleHud.Create(battle);

            var casts = new Dictionary<PassiveSkillId, int>();
            var hits = new Dictionary<PassiveSkillId, int>();
            int jumps = 0, landings = 0, parried = 0;
            battle.Skills.Cast += st => casts[st.row.skill] = casts.TryGetValue(st.row.skill, out int c) ? c + 1 : 1;
            battle.Skills.SkillHit += (id, _, _, _) => hits[id] = hits.TryGetValue(id, out int c) ? c + 1 : 1;
            battle.Dragon.Jumped += () => jumps++;
            battle.Dragon.Landed += () => landings++;
            int airKills = 0;
            battle.Drops.AirBurst += (_, _) => airKills++;
            Assert.AreEqual(6, battle.Skills.Skills.Count, "돌진 + 스킬 5종");

            bool shot = false;
            float elapsed = 0;
            while (!battle.IsFinished && elapsed < TimeoutSeconds)
            {
                bool parrying = battle.Dragon.IsParrying;
                double hp = battle.Dragon.Vitals.Hp;
                yield return null;
                elapsed += 1f / FrameRate;
                if (parrying && battle.Dragon.IsParrying && battle.Dragon.Vitals.Hp >= hp) parried++;
                if (!shot && battle.Skills.Skills.Count(x => x.active) >= 2)
                {
                    shot = true;
                    Capture("skills_1-2");
                }
            }
            Capture("skills_1-2_end");

            string summary = string.Join(", ", System.Enum.GetValues(typeof(PassiveSkillId)).Cast<PassiveSkillId>()
                .Select(id => $"{id} 발동 {(casts.TryGetValue(id, out int c) ? c : 0)} / 타격 {(hits.TryGetValue(id, out int h) ? h : 0)}"));
            Debug.Log($"[DWH] 패시브 전부 1-2 {battle.State}: {battle.Dragon.Vitals.Elapsed:0.0}초, 파괴 {battle.DestroyedCount}/{battle.Buildings.Count}, " +
                      $"점프 {jumps}, 공중 유닛 {battle.AirUnits.Units.Count}마리 중 격추 {airKills}, {summary}");

            foreach (PassiveSkillId id in System.Enum.GetValues(typeof(PassiveSkillId)))
            {
                Assert.Greater(casts.TryGetValue(id, out int c) ? c : 0, 0, $"{id} 발동");
                // 충격파는 발을 구른 뒤(약 0.29초) 맞히므로, 마지막 건물 직전에 발동하면 클리어로 끊길 수 있다.
                // 충격파 타격은 SkillMotions_PoseAndCapture에서 검사한다
                if (id == PassiveSkillId.Shockwave) continue;
                Assert.Greater(hits.TryGetValue(id, out int h) ? h : 0, 0, $"{id} 타격");
            }
            Assert.Greater(jumps, 0, "점프 확률 30%면 몸통 박치기 중에 점프한다");
            Assert.That(landings, Is.InRange(jumps - 1, jumps), "점프하면 착지한다(전투가 끝날 때 공중이면 하나 모자랄 수 있다)");
            Assert.Greater(parried, 0, "회오리 중에 프레임이 지나간다");
        }

        /// <summary>M8: 공중 유닛이 3초에 나와 화면 위쪽에 자리 잡고 탄을 쏘며, 격추하면 공중 드랍 구성대로 골드를 쏟는다</summary>
        [UnityTest]
        public IEnumerator AirUnits_SpawnFireAndDropGold()
        {
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            BattleHud.Create(battle);
            Assert.IsNotNull(battle.AirUnits);
            double burstGold = -1;
            battle.Drops.AirBurst += (_, gold) => burstGold = gold;

            int maxShots = 0;
            float elapsed = 0;
            while (elapsed < 8f && !battle.IsFinished)
            {
                yield return null;
                elapsed += 1f / FrameRate;
                if (elapsed < 2.9f) Assert.AreEqual(0, battle.AirUnits.Units.Count, "3초 전에는 나오지 않는다");
                maxShots = Mathf.Max(maxShots, Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Count(p => p.name == "AirProjectile"));
            }
            Capture("air_1-1");
            Assert.AreEqual(1, battle.AirUnits.Units.Count, "3초에 1마리");
            var unit = battle.AirUnits.Units[0];
            Assert.IsTrue(unit.IsAlive);
            float half = camera.orthographicSize * camera.aspect;
            Assert.That(unit.transform.position.x, Is.InRange(camera.transform.position.x - half, camera.transform.position.x + half), "화면 안에 머문다");
            float altitude = unit.transform.position.y - (float)db.balance.groundHeight;
            Assert.That(altitude, Is.InRange(db.balance.airUnitAltitudeMin - 0.3, db.balance.airUnitAltitudeMax + 0.3));
            Assert.Greater(maxShots, 0, "드래곤을 향해 탄을 쏜다");

            int stacks = battle.Drops.ChainStacks;
            unit.TakeDamage(unit.MaxHp, false);
            double expected = DropPlanner.TotalGold(DropPlanner.Expand(db.AirDrop(1, "1-1")), r => db.Resource(r).goldValue);
            Assert.AreEqual(expected, burstGold, "공중 드랍 구성 합계 (탐욕 없음)");
            Assert.AreEqual(stacks, battle.Drops.ChainStacks, "공중 유닛은 연쇄 파괴 스택을 쌓지 않는다");
            Debug.Log($"[DWH] 공중 유닛 1-1: {unit.UnitName} 격추 골드 {burstGold}, 최대 동시 탄 {maxShots}");
        }

        /// <summary>M9: 액티브 스킬 3종(5단계)을 1초에 함께 쓴다. 횟수가 줄고, 각 스킬이 대상을 때리며, 0회가 되면 쓰지 못한다</summary>
        [UnityTest]
        public IEnumerator ActiveSkills_UseHitAndConsume()
        {
            battle = BattleController.Create(db, art, "1-2", camera, new AutoOnlyInput());
            BattleHud.Create(battle);
            var levels = new Dictionary<ActiveSkillId, int> { [ActiveSkillId.Meteor] = 5, [ActiveSkillId.Hurricane] = 5, [ActiveSkillId.Earthquake] = 5 };
            var uses = ActiveSkillSystem.FullUses(db, levels);
            battle.Actives.Configure(levels, uses);
            var hits = new Dictionary<ActiveSkillId, int>();
            battle.Actives.SkillHit += (id, _, _, _) => hits[id] = hits.TryGetValue(id, out int c) ? c + 1 : 1;

            for (int i = 0; i < FrameRate; i++) yield return null;
            foreach (ActiveSkillId id in System.Enum.GetValues(typeof(ActiveSkillId)))
                Assert.IsTrue(battle.Actives.Use(id), id.ToString());
            Assert.AreEqual(2, battle.Actives.UsesLeft(ActiveSkillId.Meteor), "5단계 세션당 3회 중 1회 사용");

            bool shot = false;
            for (int i = 0; i < FrameRate * 6 && !battle.IsFinished; i++)
            {
                yield return null;
                if (!shot && i == FrameRate / 2) { shot = true; Capture("actives_1-2"); }
            }
            foreach (ActiveSkillId id in System.Enum.GetValues(typeof(ActiveSkillId)))
                Assert.Greater(hits.TryGetValue(id, out int c) ? c : 0, 0, $"{id} 타격");
            Assert.That(hits[ActiveSkillId.Hurricane], Is.GreaterThanOrEqualTo(1));
            Debug.Log($"[DWH] 액티브 1-2: " + string.Join(", ", hits.Select(kv => $"{kv.Key} 타격 {kv.Value}")) +
                      $", 파괴 {battle.DestroyedCount}/{battle.Buildings.Count}");

            uses[ActiveSkillId.Earthquake] = 0;
            Assert.IsFalse(battle.Actives.Use(ActiveSkillId.Earthquake), "남은 횟수 0이면 쓰지 못한다");
            battle.Actives.Configure(new Dictionary<ActiveSkillId, int>(), uses);
            Assert.IsFalse(battle.Actives.Use(ActiveSkillId.Meteor), "미습득이면 쓰지 못한다");
        }

        /// <summary>M11: 성장 단계가 오르면 드래곤 그림과 충돌 박스가 커지고, 전투 중 대미지 팝업이 뜬다</summary>
        [UnityTest]
        public IEnumerator Growth_ScalesDragonAndPopupsAppear()
        {
            var hatchling = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            float baseHeight = hatchling.Dragon.Hitbox.height;
            Object.DestroyImmediate(hatchling.gameObject);

            var save = new SaveData { currentStage = "1-1", lifetimeSpentGold = 1e8 };
            var stats = new OutGame.UpgradeTree(db, save).Stats();
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput(), BattleMode.Normal, null, stats);
            BattleHud.Create(battle);
            Assert.AreEqual(baseHeight * 1.55f * 1.45f * 1.25f, battle.Dragon.Hitbox.height, baseHeight * 0.01f, "고룡은 해츨링의 약 2.8배");
            Assert.AreEqual((float)db.balance.groundHeight, battle.Dragon.Hitbox.yMin, 0.05f, "커져도 발은 지면에");

            int maxActive = 0;
            for (int i = 0; i < FrameRate * 6; i++)
            {
                yield return null;
                maxActive = Mathf.Max(maxActive, battle.Fx.ActivePopups);
                if (i == FrameRate * 4) Capture("growth_ancient_1-1");
            }
            Assert.Greater(battle.Fx.PopupsShown, 0, "타격마다 대미지 숫자가 뜬다");
            Assert.Greater(maxActive, 0);
            Debug.Log($"[DWH] 고룡 1-1: 충돌 박스 높이 {battle.Dragon.Hitbox.height:0.00} (해츨링 {baseHeight:0.00}), 팝업 {battle.Fx.PopupsShown}개, 파괴 {battle.DestroyedCount}");
        }

        /// <summary>드래곤 주변만 확대해서 찍는다 (동작 확인용)</summary>
        void CaptureZoom(string name, float halfHeight = 2.6f)
        {
            float size = camera.orthographicSize;
            var pos = camera.transform.position;
            var c = battle.Dragon.Hitbox.center;
            camera.orthographicSize = halfHeight;
            camera.transform.position = new Vector3(c.x + battle.Dragon.Facing * 1.2f, c.y + 0.6f, pos.z);
            Capture(name);
            camera.orthographicSize = size;
            camera.transform.position = pos;
        }

        /// <summary>
        /// 스킬 모션 (웹 프로토타입 방식의 파츠 애니메이션): 박치기 때 앞다리 차기, 브레스 때 아래턱 벌림과 불꽃,
        /// 회오리 때 붕 떠서 회전, 충격파 때 몸을 들었다 쿵. 각 순간을 확대해 찍는다.
        /// </summary>
        [UnityTest]
        public IEnumerator SkillMotions_PoseAndCapture()
        {
            var levels = new Dictionary<string, int>
            {
                ["Skill_BodySlam"] = 3, ["Skill_Whirlwind"] = 2, ["Skill_Shockwave"] = 2, ["Skill_FireBreath"] = 2,
                ["Sub_Dash"] = 1, ["HpBoost"] = 10, ["StaminaBoost"] = 5,
            };
            var stats = PlayerStats.Compute(db, n => levels.TryGetValue(n.node, out int l) ? l : n.initialLevel);
            battle = BattleController.Create(db, art, "1-2", camera, new AutoOnlyInput(), BattleMode.Normal, null, stats);
            var anim = battle.Dragon.Animator;
            var front = battle.Dragon.Rig.Node("FrontLeg");
            var visual = battle.Dragon.Rig.Visual;

            var pending = new List<(PassiveSkillId id, float at)>();
            battle.Skills.Cast += st => pending.Add((st.row.skill, Time.time));
            int shockHits = 0;
            battle.Skills.SkillHit += (id, _, _, _) => { if (id == PassiveSkillId.Shockwave) shockHits++; };
            bool kickShot = false, walkShot = false;
            var seen = new HashSet<string>();
            float maxJaw = 0, maxLift = 0, maxKick = 0, maxSpin = 0;
            // 네 동작을 다 찍고, 충격파가 한 번이라도 맞힐 때까지 돌린다 (첫 충격파의 대상이 준비 동작 중에 먼저 부서질 수 있다)
            for (int i = 0; i < FrameRate * 30 && !battle.IsFinished && (seen.Count < 4 || shockHits == 0); i++)
            {
                yield return null;
                maxJaw = Mathf.Max(maxJaw, anim.Jaw);
                maxLift = Mathf.Max(maxLift, visual.localPosition.y);
                maxSpin = Mathf.Max(maxSpin, Mathf.Abs(anim.SpinAngle));
                float kick = Mathf.DeltaAngle(0, front.localEulerAngles.z);
                maxKick = Mathf.Min(maxKick, kick);
                if (!walkShot && i == FrameRate / 2) { walkShot = true; CaptureZoom("motion_walk"); }
                if (!kickShot && kick < -35f) { kickShot = true; CaptureZoom("motion_kick"); }
                foreach (var (id, at) in pending.ToArray())
                {
                    float since = Time.time - at;
                    string key = id switch
                    {
                        PassiveSkillId.FireBreath when since > 0.45f => "breath",
                        PassiveSkillId.Whirlwind when since > 0.2f => "whirlwind",
                        PassiveSkillId.Shockwave when since > 0.42f => "shockwave",
                        PassiveSkillId.BodySlam when since > 0.05f => "dash",
                        _ => null,
                    };
                    if (key == null || !seen.Add(key)) continue;
                    CaptureZoom($"motion_{key}", key == "shockwave" ? 3.6f : 2.6f);
                    if (key == "breath") Assert.Greater(anim.Jaw, 0.6f, "브레스 중에는 아래턱이 벌어져 있다");
                    if (key == "whirlwind") Assert.IsTrue(anim.IsSpinning, "회오리 중에는 회전한다");
                }
            }
            Debug.Log($"[DWH] 스킬 모션: {string.Join(", ", seen)} / 턱 최대 {maxJaw:0.00}, 몸 최대 들림 {maxLift:0.00}, 앞다리 차기 최대 {maxKick:0}도, 회전 최대 {maxSpin:0}도, 파티클 {battle.Particles.Emitted}개");
            CollectionAssert.IsSubsetOf(new[] { "breath", "whirlwind", "shockwave" }, seen.ToArray(), "세 스킬이 발동해야 한다");
            Assert.Less(maxKick, -35f, "박치기 때 앞다리를 앞으로 찬다");
            Assert.Greater(maxLift, 0.2f, "회오리·발 구르기 때 몸이 뜬다");
            Assert.Greater(battle.Particles.Emitted, 50, "불꽃·먼지·바람 파티클");
            Assert.Greater(shockHits, 0, "충격파는 발이 땅에 닿는 순간 범위 안 대상을 친다");
        }

        /// <summary>커서를 한 곳에 둔 유도 입력 (테스트용)</summary>
        class FixedCursorInput : IGuideInput
        {
            public Vector2 Point;
            public bool TryGetGuideX(out float worldX) { worldX = Point.x; return true; }
            public bool TryGetCursor(out Vector2 world) { world = Point; return true; }
        }

        /// <summary>
        /// 마우스 유도 표시: 커서 위치의 조준점과 드래곤 입에서 커서까지의 궤적. 건물을 목표로 고르지 않는다.
        /// 드래곤은 커서 쪽으로 고개를 돌리고, 브레스도 그쪽으로 뿜는다.
        /// </summary>
        [UnityTest]
        public IEnumerator AimGuide_ShowsCursorAndTrajectory()
        {
            var input = new FixedCursorInput();
            battle = BattleController.Create(db, art, "1-2", camera, input);
            var third = battle.Buildings.OrderBy(b => b.CenterX).ElementAt(2);
            input.Point = new Vector2(third.CenterX, third.Hitbox.center.y + 0.3f);
            for (int i = 0; i < FrameRate / 2; i++) yield return null;

            var aim = battle.Aim;
            Assert.IsTrue(aim.Visible, "유도 중에는 조준 표시가 보인다");
            Assert.AreEqual(input.Point, aim.AimPoint, "조준 지점 = 커서 (건물에 붙지 않는다)");
            Assert.AreEqual(aim.AimPoint, battle.Dragon.Animator.Aim.Value, "드래곤이 커서 쪽을 본다");
            Capture("aim_1-2");

            // 지면 아래를 가리키면 지면 위로 올린다
            input.Point = new Vector2(battle.Dragon.Hitbox.center.x + 3f, 0.2f);
            yield return null;
            yield return null;
            Assert.AreEqual(battle.Dragon.Hitbox.yMin + 0.15f, aim.AimPoint.y, 1e-4);

            // 자동 이동이 되면 숨긴다 (전투가 끝나는 경우도 같다)
            battle.Dragon.Controllable = false;
            yield return null;
            Assert.IsFalse(aim.Visible);
            Assert.IsNull(battle.Dragon.Animator.Aim);
        }

        /// <summary>보너스 스테이지: 기력이 2배로 빨리 줄고, 체력 회복·기력 회복(포식 등)이 모두 막힌다</summary>
        [UnityTest]
        public IEnumerator Bonus_DoubleDrainAndNoRecovery()
        {
            var levels = new Dictionary<string, int> { ["Devour"] = 10, ["HpRegen"] = 10, ["HpBoost"] = 5, ["StaminaBoost"] = 5 };
            var stats = PlayerStats.Compute(db, n => levels.TryGetValue(n.node, out int l) ? l : n.initialLevel);
            var vitals = new DragonVitals(stats.MaxHp, stats.HpRegenPerSecond, stats.MaxStamina, db.dragon.fatigueStart, db.dragon.fatigueInterval, db.dragon.fatigueStep);
            vitals.TakeDamage(30);
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput(), BattleMode.Bonus, vitals, stats);
            Assert.AreEqual(2.0, vitals.DrainMultiplier);
            Assert.IsTrue(vitals.RecoveryBlocked);

            double hp = vitals.Hp, stamina = vitals.Stamina, elapsed = vitals.Elapsed;
            int destroyed = 0;
            for (int i = 0; i < FrameRate * 3; i++) yield return null;
            destroyed = battle.DestroyedCount;
            double expectedDrain = Formulas.StaminaUsed(vitals.Elapsed, db.dragon.fatigueStart, db.dragon.fatigueInterval, db.dragon.fatigueStep)
                                   - Formulas.StaminaUsed(elapsed, db.dragon.fatigueStart, db.dragon.fatigueInterval, db.dragon.fatigueStep);
            Assert.AreEqual(stamina - expectedDrain * 2, vitals.Stamina, 1e-6, "기력 소모 2배, 포식 회복 없음");
            Assert.AreEqual(hp, vitals.Hp, 1e-9, "체력 회복 없음");
            Debug.Log($"[DWH] 보너스 제한: 3초 동안 기력 {stamina:0.0} → {vitals.Stamina:0.0} (일반이면 -{expectedDrain:0.0}), 체력 {hp:0} 유지, 파괴 {destroyed}");

            // 일반 스테이지에서는 그대로
            var normal = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput(), BattleMode.Normal, null, stats);
            Assert.AreEqual(1.0, normal.Dragon.Vitals.DrainMultiplier);
            Assert.IsFalse(normal.Dragon.Vitals.RecoveryBlocked);
            Object.DestroyImmediate(normal.gameObject);
        }

        /// <summary>
        /// 회귀: 점프 후 다음 건물과 몸이 겹쳐 착지하면 목표 방향이 0이 되어 공격도 이동도 하지 않고 멈춰 있었다.
        /// 겹친 채 서 있어도 그 건물을 치고, 착지 때는 옆으로 밀려나야 한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Landing_OverlappingBuilding_DoesNotGetStuck()
        {
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            var dragon = battle.Dragon;
            var target = battle.Buildings.OrderBy(b => b.CenterX).ElementAt(1);
            // 드래곤 몸 가운데를 건물 왼쪽 끝에 맞춰 겹쳐 세운다 (멈춤 버그가 났던 배치)
            var p = dragon.transform.position;
            dragon.transform.position = new Vector3(p.x + (target.Left - dragon.Hitbox.center.x), p.y, p.z);
            double hp = target.Hp;
            for (int i = 0; i < FrameRate; i++) yield return null;
            Assert.Less(target.Hp, hp, "겹친 건물을 공격한다");

            // 점프 착지로 다음 건물과 겹쳐도 멈추지 않고 그 건물을 친다
            var next = battle.Buildings.Where(b => b.IsAlive && b.CenterX > dragon.Hitbox.center.x).OrderBy(b => b.CenterX).First();
            dragon.transform.position = new Vector3(dragon.transform.position.x + (next.Left - dragon.Hitbox.center.x) - 1f, p.y, p.z);
            dragon.StartJump();
            for (int i = 0; i < FrameRate * 10 && dragon.IsAirborne; i++) yield return null;
            Assert.IsFalse(dragon.IsAirborne, "지붕에서 튀다가 건물이 부서지면 땅에 내려온다");
            double total = battle.Buildings.Where(b => b.IsAlive).Sum(b => b.Hp);
            for (int i = 0; i < FrameRate; i++) yield return null;
            Assert.Less(battle.Buildings.Sum(b => b.Hp), total, "착지 뒤에도 계속 공격한다");
        }

        /// <summary>
        /// 점프 (웹 프로토타입 방식): 지붕보다 조금 더 높이 올라 건물 한가운데에 떨어지고, 지붕을 밟으면 짓밟기(몸통 박치기 × 1.6 × 착지 대미지)
        /// 후 다시 튀어 오른다. 건물이 부서질 때까지 지붕 위에서 콩콩 뛴다.
        /// </summary>
        [UnityTest]
        public IEnumerator Jump_LandsOnRoofCenterStompsAndBounces()
        {
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            var dragon = battle.Dragon;
            var first = battle.Buildings.OrderBy(b => b.CenterX).First();
            var p = dragon.transform.position;
            dragon.transform.position = new Vector3(p.x + (first.Left - dragon.Hitbox.xMax) - 0.02f, p.y, p.z);
            var stomps = new List<float>();
            float firstStompX = float.NaN;
            dragon.Stomped += b => { if (b == first) { stomps.Add(Time.time); if (float.IsNaN(firstStompX)) firstStompX = dragon.Hitbox.center.x; } };
            double hp = first.Hp;
            dragon.StartJump(first);
            float maxFeet = 0;
            bool bouncedUp = false, shotStomp = false;
            for (int i = 0; i < FrameRate * 3 && stomps.Count < 3 && first.IsAlive; i++)
            {
                yield return null;
                maxFeet = Mathf.Max(maxFeet, dragon.Hitbox.yMin);
                if (stomps.Count > 0 && dragon.Velocity.y > 0) bouncedUp = true;
                if (stomps.Count == 1 && !shotStomp) { shotStomp = true; CaptureZoom("jump_stomp", 2.4f); }
            }
            Debug.Log($"[DWH] 점프 짓밟기: 지붕 {first.Hitbox.yMax:0.00}, 발 최고 {maxFeet:0.00}, 첫 짓밟기 x {firstStompX:0.00} (건물 가운데 {first.CenterX:0.00}), 짓밟기 {stomps.Count}회, 체력 {hp:0} → {first.Hp:0}");
            Assert.Greater(maxFeet, first.Hitbox.yMax + 0.3f, "지붕보다 조금 더 높이 올라간다");
            Assert.Greater(stomps.Count, 0, "지붕을 밟는다");
            Assert.AreEqual(first.CenterX, firstStompX, 0.35f, "건물 한가운데에 떨어진다");
            Assert.IsTrue(bouncedUp, "짓밟은 뒤 다시 튀어 오른다");
            if (stomps.Count >= 2) Assert.GreaterOrEqual(stomps[1] - stomps[0], 0.29f, "같은 건물 짓밟기 간격 0.3초 이상");
            Assert.Less(first.Hp, hp);
        }

        /// <summary>공중에서 지붕보다 낮게 건물 옆에 부딪히면 옆으로 밀려나며 들이받고(몸통 박치기 100%) 반동으로 물러난다</summary>
        [UnityTest]
        public IEnumerator Jump_SideHitPushesBackAndRecoils()
        {
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            var dragon = battle.Dragon;
            var ordered = battle.Buildings.OrderBy(b => b.CenterX).ToList();
            var wall = ordered[0];
            var p = dragon.transform.position;
            dragon.transform.position = new Vector3(p.x + (wall.Left - dragon.Hitbox.xMax) - 0.02f, p.y, p.z);
            double hp = wall.Hp;
            // 멀리 있는 건물을 향해 뛰면 낮은 높이에서 바로 앞 건물 옆면에 부딪힌다
            dragon.StartJump(ordered[3]);
            bool recoiled = false;
            float maxOverlap = 0;
            for (int i = 0; i < FrameRate && dragon.IsAirborne; i++)
            {
                yield return null;
                if (dragon.Hitbox.yMin < wall.Hitbox.yMax - 0.05f)
                    maxOverlap = Mathf.Max(maxOverlap, dragon.Hitbox.xMax - wall.Left);
                if (wall.Hp < hp && dragon.Velocity.x < 0) recoiled = true;
            }
            Debug.Log($"[DWH] 공중 옆 충돌: 체력 {hp:0} → {wall.Hp:0}, 반동 {recoiled}, 지붕 아래에서 최대 파고듦 {maxOverlap:0.00}");
            Assert.Less(wall.Hp, hp, "옆에서 들이받는다");
            Assert.IsTrue(recoiled, "반동으로 물러난다");
            Assert.LessOrEqual(maxOverlap, 0.25f, "지붕보다 낮을 때는 옆면을 뚫고 들어가지 않는다");
        }

        /// <summary>건물과 공중 유닛 아래 체력바: 체력 비율을 따라가고, 줄어든 부분은 흰 잔상이 늦게 따라오며, 파괴되면 사라진다</summary>
        [UnityTest]
        public IEnumerator HealthBars_UnderBuildingsAndAirUnits()
        {
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput());
            yield return null;
            foreach (var b in battle.Buildings)
            {
                var bar = b.GetComponentInChildren<InGame.Fx.WorldHealthBar>();
                Assert.IsNotNull(bar, $"{b.name} 체력바");
                Assert.Less(bar.transform.position.y, b.Hitbox.yMin, "건물 아래");
                Assert.AreEqual(1f, bar.Ratio, 1e-4);
            }
            var target = battle.Buildings.OrderBy(x => x.CenterX).Last();
            var tbar = target.GetComponentInChildren<InGame.Fx.WorldHealthBar>();
            target.TakeDamage(target.MaxHp * 0.4, false);
            yield return null;
            Assert.AreEqual(0.6f, tbar.Ratio, 1e-3, "체력 비율");
            Assert.Greater(tbar.TrailRatio, tbar.Ratio, "줄어든 부분은 잠깐 흰 잔상으로 남는다");
            for (int i = 0; i < FrameRate; i++) yield return null;
            Assert.AreEqual(tbar.Ratio, tbar.TrailRatio, 1e-3, "잔상이 따라 줄어든다");

            // 공중 유닛 (3초에 등장)
            for (int i = 0; i < FrameRate * 4 && battle.AirUnits.Units.Count == 0; i++) yield return null;
            var unit = battle.AirUnits.Units[0];
            yield return null;
            var abar = unit.GetComponentInChildren<InGame.Fx.WorldHealthBar>();
            Assert.IsNotNull(abar, "공중 유닛 체력바");
            Assert.Less(abar.transform.position.y, unit.Hitbox.yMin, "공중 유닛 아래");
            Assert.AreEqual(unit.Hitbox.center.x, abar.transform.position.x, 0.05f, "따라다닌다");
            Capture("healthbars_1-1");

            target.TakeDamage(target.MaxHp, false);
            Assert.IsFalse(tbar.gameObject.activeInHierarchy, "파괴되면 체력바도 사라진다");
        }

        [UnityTest]
        public IEnumerator Stage11_NoUpgrades_ClearsWithStaminaForBonus()
        {
            yield return Run("1-1", 0.1f, 8f, 20f);
            Assert.AreEqual(BattleState.Cleared, battle.State);
            // 8.1: 업그레이드 없이 약 31초에 클리어하고 보너스 스테이지까지 경험할 수 있다
            Assert.That(battle.Dragon.Vitals.Elapsed, Is.InRange(20.0, 45.0));
            Assert.IsTrue(battle.BonusEligible, $"남은 기력 {battle.Dragon.Vitals.Stamina:0.0}");
        }

        [UnityTest]
        public IEnumerator Stage12_NoUpgrades_Fails()
        {
            yield return Run("1-2", 20f);
            // 8.1: 1-2는 업그레이드 없이는 실패해 첫 업그레이드의 필요성을 체감한다.
            // 문서는 기력 소진을 예상했지만, 랜드마크 탄 때문에 체력이 먼저 바닥날 수 있다(T-05). 실패 여부만 확인한다.
            Assert.AreEqual(BattleState.Failed, battle.State);
        }

        [UnityTest]
        public IEnumerator Bonus_GoldenOnly_EndsAsClearWhenStaminaRunsOut()
        {
            // 일반 스테이지를 30초 버틴 상태(피로 4, 기력 40)를 이어받아 보너스를 시작한다
            var vitals = new DragonVitals(db.dragon.maxHp, 0, db.dragon.maxStamina, db.dragon.fatigueStart, db.dragon.fatigueInterval, db.dragon.fatigueStep);
            vitals.Tick(30);
            double staminaAtStart = vitals.Stamina;
            battle = BattleController.Create(db, art, "1-1", camera, new AutoOnlyInput(), BattleMode.Bonus, vitals);
            Assert.AreSame(vitals, battle.Dragon.Vitals);
            Assert.AreEqual(db.Stage("1-1").goldenCount, battle.Buildings.Count);
            foreach (var b in battle.Buildings)
            {
                Assert.AreEqual(BuildingType.Golden, b.Type);
                Assert.AreEqual(db.Stage("1-1").goldenHp, b.MaxHp, "황금 건물에는 허들 배율을 적용하지 않는다");
                Assert.IsNull(b.GetComponent<BuildingTurret>(), "황금 건물은 공격하지 않는다");
                CollectionAssert.Contains(art.Theme(1).golden, b.Sprite);
            }

            yield return Run(battle);
            Assert.AreEqual(BattleState.Cleared, battle.State, "보너스는 기력이 다 떨어져도 클리어");
            Assert.IsTrue(battle.Dragon.Vitals.IsExhausted);
            Assert.AreEqual(db.dragon.maxHp, battle.Dragon.Vitals.Hp, "황금 건물은 공격하지 않으므로 체력이 줄지 않는다");
            Assert.IsFalse(battle.BonusEligible, "보너스에서 다시 보너스로 가지 않는다");
            Debug.Log($"[DWH] 보너스 1-1 (시작 기력 {staminaAtStart:0}): 황금 {battle.DestroyedCount}/{battle.Buildings.Count} 파괴, {battle.Dragon.Vitals.Elapsed - 30:0.0}초");
        }

        IEnumerator Run(BattleController target)
        {
            float elapsed = 0;
            while (!target.IsFinished && elapsed < TimeoutSeconds)
            {
                yield return null;
                elapsed += 1f / FrameRate;
            }
        }

        [UnityTest]
        public IEnumerator Stage74_BuildsWithLandmarkAndArt()
        {
            battle = BattleController.Create(db, art, "7-4", camera, new AutoOnlyInput());
            yield return null;
            Assert.AreEqual(52, battle.Buildings.Count);
            Capture("7-4_start");
            camera.GetComponent<BattleCamera>().enabled = false;
            var landmark = battle.Buildings[26];
            Assert.AreEqual(BuildingType.Landmark, landmark.Type);
            camera.transform.position = new Vector3(landmark.CenterX, camera.transform.position.y, -10);
            yield return null;
            Capture("7-4_landmark");
        }
    }
}
#endif
