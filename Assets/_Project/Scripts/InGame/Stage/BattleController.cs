using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Dragon;
using DWH.InGame.Drops;
using DWH.InGame.Fx;
using DWH.InGame.Skills;
using DWH.InGame.Starter;
using UnityEngine;

namespace DWH.InGame.Stage
{
    public enum BattleState { Playing, Cleared, Failed }

    public enum BattleMode { Normal, Bonus }

    /// <summary>
    /// 스테이지 한 판. 데이터(stage, building, unit)로 레벨을 자동 생성하고 승패를 판정한다. 종료 시 슬로모션 후 Ended를 알린다.
    /// - 일반: 클리어 = 모든 건물 파괴 / 패배 = 체력 0 또는 기력 0 (가이드 6.1)
    /// - 보너스 (가이드 6.6): 황금 건물(유형 4)만 나오고 공중 유닛이 없다. 기력 감소 2배, 체력·기력 회복 없음.
    ///   체력·기력·피로는 일반 스테이지에서 이어받는다. 기력 0, 체력 0, 황금 건물 전부 파괴 중 무엇으로 끝나도 클리어다.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        readonly List<Building> buildings = new();
        readonly List<BuildingTurret> turrets = new();
        GameDatabase db;

        public GameDatabase Database => db;
        public StageRow Stage { get; private set; }
        /// <summary>진행 순서상 1부터 센 순번. 시드에 쓴다</summary>
        public int StageNumber { get; private set; }
        public BattleMode Mode { get; private set; }
        public PlayerStats Stats { get; private set; }
        public DragonController Dragon { get; private set; }
        public DropSystem Drops { get; private set; }
        public PassiveSkillSystem Skills { get; private set; }
        /// <summary>액티브 스킬. GameManager가 Configure로 단계와 남은 횟수를 넣는다</summary>
        public ActiveSkillSystem Actives { get; private set; }
        /// <summary>대미지 팝업·부스러기·타격 흔들림</summary>
        public CombatFx Fx { get; private set; }
        public FxParticles Particles { get; private set; }
        /// <summary>마우스 유도 표시: 커서 조준점과 입에서 커서까지 궤적</summary>
        public AimGuide Aim { get; private set; }
        /// <summary>스테이지 스타터. 스타터 입력을 넘긴 일반 스테이지에서만 만든다. 끝나면 전투가 시작된다</summary>
        public StageStarter Starter { get; private set; }
        public bool InStarter => Starter != null && Starter.Phase != StarterPhase.Done;
        /// <summary>공중 유닛. 보너스 스테이지에서는 null</summary>
        public AirUnitSquad AirUnits { get; private set; }
        public ParallaxBackground Background { get; private set; }
        public IReadOnlyList<Building> Buildings => buildings;
        public BattleState State { get; private set; } = BattleState.Playing;
        public int DestroyedCount => buildings.Count(b => !b.IsAlive);
        public int RemainingCount => buildings.Count(b => b.IsAlive);
        public float StageLeft { get; private set; }
        public float StageRight { get; private set; }
        /// <summary>종료 연출(슬로모션)까지 끝났는지</summary>
        public bool IsFinished { get; private set; }

        /// <summary>일반 스테이지를 클리어한 순간 기력이 const.bonus_min_stamina 이상이면 보너스 스테이지 진입 (가이드 6.6)</summary>
        public bool BonusEligible => Mode == BattleMode.Normal && State == BattleState.Cleared
                                     && Dragon.Vitals.Stamina >= db.Const("bonus_min_stamina")
                                     && db.Building(Stage.stageId, BuildingType.Golden) is { count: > 0 };

        public event Action<BattleController> Ended;

        /// <param name="stats">업그레이드가 반영된 능력치. null이면 업그레이드 없는 기본 능력치</param>
        /// <param name="carriedVitals">보너스 스테이지에서 이어받는 체력·기력·피로. null이면 새로 시작한다</param>
        /// <param name="starterInput">스테이지 스타터 클릭 입력. null이면 스타터 없이 바로 전투한다(테스트·보너스)</param>
        public static BattleController Create(GameDatabase db, ArtCatalog art, string stageId, Camera camera, IGuideInput input,
            BattleMode mode = BattleMode.Normal, DragonVitals carriedVitals = null, PlayerStats stats = null, IStarterInput starterInput = null)
        {
            var battle = new GameObject($"Battle {stageId} {mode}").AddComponent<BattleController>();
            battle.Build(db, art, db.Stage(stageId), camera, input, mode, carriedVitals, stats ?? PlayerStats.Base(db));
            if (starterInput != null && mode == BattleMode.Normal)
                battle.BeginStarter(db, art, camera, starterInput);
            return battle;
        }

        /// <summary>스타터가 끝날 때까지 드래곤·적·스킬을 멈춘다</summary>
        void BeginStarter(GameDatabase database, ArtCatalog art, Camera camera, IStarterInput starterInput)
        {
            SetCombatActive(false);
            Starter = new GameObject("StageStarter").AddComponent<StageStarter>();
            Starter.transform.SetParent(transform, false);
            Starter.Finished += _ => SetCombatActive(true);
            Starter.Init(database, art, this, camera, camera.GetComponent<BattleCamera>(), starterInput);
        }

        void SetCombatActive(bool active)
        {
            Dragon.Controllable = active;
            foreach (var t in turrets) t.Enabled = active;
            if (AirUnits != null) AirUnits.Enabled = active;
            Skills.enabled = active;
            Actives.enabled = active;
        }

        void Build(GameDatabase database, ArtCatalog art, StageRow stage, Camera camera, IGuideInput input,
            BattleMode mode, DragonVitals carriedVitals, PlayerStats stats)
        {
            db = database;
            Stage = stage;
            StageNumber = db.OrderIndex(stage.stageId) + 1;
            Mode = mode;
            Stats = stats;
            bool bonus = mode == BattleMode.Bonus;
            // 감별안 황금화 판정용. 스테이지마다 같은 결과가 나오게 시드를 고정한다
            var gildRandom = new System.Random(StageNumber * 7919);

            camera.orthographic = true;
            camera.orthographicSize = Tuning.CameraOrthoSize;
            camera.transform.position = new Vector3(0, camera.orthographicSize, -10);

            float groundY = Tuning.StandHeight;
            float dragonStartX = 0f;

            // 건물 배치 (가이드 6.2): 간격 const.building_spacing_px, 랜드마크는 가운데, 공격 건물은 고르게 분산
            var buildingRoot = new GameObject("Buildings").transform;
            buildingRoot.SetParent(transform, false);
            float spacing = Tuning.Px(db.Const("building_spacing_px"));
            float cursor = dragonStartX + Tuning.StageStartMargin;
            var normal = db.Building(stage.stageId, BuildingType.Normal);
            var attack = db.Building(stage.stageId, BuildingType.Attack);
            var landmark = db.Building(stage.stageId, BuildingType.Landmark);
            var golden = db.Building(stage.stageId, BuildingType.Golden);
            var order = bonus
                ? Enumerable.Repeat(BuildingType.Golden, golden?.count ?? 0).ToArray()
                : StageLayout.BuildOrder(normal?.count ?? 0, attack?.count ?? 0, landmark?.count ?? 0);
            for (int i = 0; i < order.Length; i++)
            {
                var type = order[i];
                var row = type switch
                {
                    BuildingType.Attack => attack,
                    BuildingType.Landmark => landmark,
                    BuildingType.Golden => golden,
                    _ => normal,
                };
                // 감별안: 일반 스테이지의 일반·공격 건물이 확률로 황금화된다. 랜드마크는 제외 (효과 9)
                bool gilded = !bonus && (type is BuildingType.Normal or BuildingType.Attack)
                              && gildRandom.NextDouble() < stats.GoldenChance;
                // 그림은 res_id_1~3 중 시드(스테이지 순번 × 1000 + 건물 순번)로 고른다
                var sprite = art.Sprite(ArtCatalog.Pick(row.ResIds(), StageNumber, i));
                var content = ContentRect(sprite, Tuning.BuildingArtMarginPx);

                var go = new GameObject($"{i:00}_{type}");
                go.transform.SetParent(buildingRoot, false);
                go.transform.position = new Vector3(cursor - content.xMin, groundY, 0);
                cursor = go.transform.position.x + content.xMax + spacing;

                var building = go.AddComponent<Building>();
                building.Init(row, i, sprite, art.spriteFx, SortingOrders.Building, content, gilded);
                buildings.Add(building);
            }
            StageLeft = dragonStartX - Tuning.StageStartMargin;
            StageRight = cursor - spacing + Tuning.StageStartMargin;

            Dragon = new GameObject("Dragon").AddComponent<DragonController>();
            Dragon.transform.SetParent(transform, false);
            Dragon.transform.position = new Vector3(dragonStartX, groundY, 0);
            Dragon.Init(db, art, stats, input, buildings, StageNumber, carriedVitals);
            Dragon.Vitals.DrainMultiplier = bonus ? db.Const("bonus_stamina_drain_mult") : 1;
            Dragon.Vitals.RecoveryBlocked = bonus;

            // 포식: 건물 1개 파괴마다 기력 회복, 랜드마크는 ×5 (효과 3)
            foreach (var b in buildings)
                b.Destroyed += x => Dragon.Vitals.RestoreStamina(stats.StaminaOnDestroy * (x.Type == BuildingType.Landmark ? Tuning.LandmarkStaminaMultiplier : 1));

            // 공격 건물은 지면 직선 탄, 랜드마크는 세 높이 수평 탄 (가이드 6.2)
            foreach (var building in buildings)
            {
                if (building.Type is not (BuildingType.Attack or BuildingType.Landmark) || building.Row.atk <= 0) continue;
                bool isLandmark = building.Type == BuildingType.Landmark;
                var turret = building.gameObject.AddComponent<BuildingTurret>();
                turret.Init(building, Dragon, camera, GrayboxSprites.Square, building.Row.atk, (float)building.Row.atkSpeed,
                    Tuning.Px(double.IsNaN(building.Row.atkRange) ? 0 : building.Row.atkRange),
                    Tuning.ProjectileSpeed, Tuning.ProjectileSize,
                    isLandmark ? Tuning.LandmarkShotHeights : new[] { Tuning.AttackShotHeight }, db, art);
                turrets.Add(turret);
            }

            Drops = new GameObject("Drops").AddComponent<DropSystem>();
            Drops.transform.SetParent(transform, false);
            Drops.Init(db, art, stats, Dragon, buildings, StageNumber);

            // 배경 3층: 원경 고정, 중경 바닥의 1/3 속도, 바닥은 카메라와 1:1 (가이드 6.5)
            Background = new GameObject("Background").AddComponent<ParallaxBackground>();
            Background.transform.SetParent(transform, false);
            Background.Init(camera, art.Sprite(stage.bgFarRes), art.Sprite(stage.bgMidRes), art.Sprite(stage.bgGroundRes), Tuning.ParallaxMidFactor);

            if (!camera.TryGetComponent(out BattleCamera follow))
                follow = camera.gameObject.AddComponent<BattleCamera>();
            follow.Init(camera, Dragon.transform, () => Dragon.Facing, StageLeft, StageRight);

            // 공중 유닛: 일반 스테이지에만 나온다. 클리어 조건에는 들어가지 않는다
            if (!bonus)
            {
                AirUnits = new GameObject("AirUnits").AddComponent<AirUnitSquad>();
                AirUnits.transform.SetParent(transform, false);
                AirUnits.Init(db, art, stage, Dragon, camera, StageLeft, StageRight);
                AirUnits.Spawned += unit => unit.Destroyed += Drops.OnAirUnitDowned;
                Dragon.SetAirTargets(AirUnits.Units);
            }

            // 건물·공중 유닛 아래 체력바
            foreach (var b in buildings) WorldHealthBar.Attach(b);
            if (AirUnits != null) AirUnits.Spawned += unit => WorldHealthBar.Attach(unit);

            Fx = new GameObject("CombatFx").AddComponent<CombatFx>();
            Fx.transform.SetParent(transform, false);
            Fx.Init(camera, buildings, AirUnits, (float)db.Const("damage_popup_sec"), db, art);

            // 파츠 동작과 함께 쓰는 파티클 (불꽃, 먼지, 충격파, 타격 튐)
            Particles = new GameObject("Particles").AddComponent<FxParticles>();
            Particles.transform.SetParent(transform, false);
            Dragon.Animator.Fx = Particles;
            Dragon.HitBuilding += (b, _, crit) =>
            {
                var box = Dragon.Hitbox;
                float x = Dragon.Facing > 0 ? b.Left : b.Right;
                Particles.Impact(new Vector2(x, box.yMin + box.height * 0.45f), (crit ? 1.4f : 1f) * Dragon.SizeScale);
            };
            Aim = new GameObject("AimGuide").AddComponent<AimGuide>();
            Aim.transform.SetParent(transform, false);
            Aim.Init(Dragon, input);
            Dragon.Stomped += b => Particles.DustBurst(new Vector2(Dragon.Hitbox.center.x, b.Hitbox.yMax), Dragon.Hitbox.width * 0.8f, 6, Dragon.SizeScale);
            Dragon.Landed += () => Particles.DustBurst(new Vector2(Dragon.Hitbox.center.x, groundY), Dragon.Hitbox.width, 8, Dragon.SizeScale);

            // 패시브 스킬. 카메라 흔들림을 쓰므로 카메라 설정 뒤에 만든다
            Skills = new GameObject("PassiveSkills").AddComponent<PassiveSkillSystem>();
            Skills.transform.SetParent(transform, false);
            Skills.Init(db, art, stats, Dragon, buildings, AirUnits != null ? AirUnits.Units : null, camera, StageNumber);

            Actives = new GameObject("ActiveSkills").AddComponent<ActiveSkillSystem>();
            Actives.transform.SetParent(transform, false);
            Actives.Init(db, art, stats, Dragon, buildings, AirUnits != null ? AirUnits.Units : null, camera, StageNumber);
        }

        /// <summary>스프라이트 로컬 경계에서 위·좌·우 투명 여백을 뺀 그림 영역 (바닥 여백은 0)</summary>
        static Rect ContentRect(Sprite sprite, float marginPx)
        {
            var b = sprite.bounds;
            float m = marginPx / sprite.pixelsPerUnit;
            return Rect.MinMaxRect(b.min.x + m, b.min.y, b.max.x - m, b.max.y - m);
        }

        void Update()
        {
            if (State != BattleState.Playing || InStarter) return;

            bool allDestroyed = buildings.All(b => !b.IsAlive);
            if (Mode == BattleMode.Bonus)
            {
                if (allDestroyed || !Dragon.IsAlive)
                    End(BattleState.Cleared);
            }
            else if (allDestroyed)
            {
                // 승전의 포효: 클리어 순간 기력 회복. 보너스 진입 판정 전에 준다 (효과 6)
                Dragon.Vitals.RestoreStamina(Stats.StaminaOnClear);
                End(BattleState.Cleared);
            }
            else if (!Dragon.IsAlive)
                End(BattleState.Failed);
        }

        void End(BattleState result)
        {
            State = result;
            Dragon.Controllable = false;
            foreach (var t in turrets) t.Enabled = false;
            if (AirUnits != null) AirUnits.Enabled = false;
            StartCoroutine(FinishAfterSlowMotion());
        }

        IEnumerator FinishAfterSlowMotion()
        {
            Time.timeScale = Tuning.EndSlowmoScale;
            yield return new WaitForSecondsRealtime((float)db.Const("end_slowmo_sec"));
            Time.timeScale = 1f;
            IsFinished = true;
            Ended?.Invoke(this);
        }

        void OnDestroy()
        {
            if (State != BattleState.Playing && !IsFinished)
                Time.timeScale = 1f;
        }
    }
}
