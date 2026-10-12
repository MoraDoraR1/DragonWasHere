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
    /// 스테이지 한 판. 데이터로 레벨을 자동 생성하고 승패를 판정한다. 종료 시 슬로모션 후 Ended를 알린다 (4.2).
    /// - 일반: 클리어 = 모든 건물 파괴 / 패배 = 체력 0 또는 기력 0 (1.1)
    /// - 보너스 (3.5): 황금 건물만 나오고 공격하지 않는다. 체력·기력·피로는 일반 스테이지에서 이어받는다.
    ///   기력 0, 체력 0, 황금 건물 전부 파괴 중 무엇으로 끝나도 클리어로 처리한다.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        readonly List<Building> buildings = new();
        readonly List<BuildingTurret> turrets = new();
        GameDatabase db;

        public StageRow Stage { get; private set; }
        public BattleMode Mode { get; private set; }
        public PlayerStats Stats { get; private set; }
        public DragonController Dragon { get; private set; }
        public DropSystem Drops { get; private set; }
        public PassiveSkillSystem Skills { get; private set; }
        /// <summary>액티브 스킬 (4.4). GameManager가 Configure로 단계와 남은 횟수를 넣는다</summary>
        public ActiveSkillSystem Actives { get; private set; }
        /// <summary>대미지 팝업·부스러기·타격 흔들림 (4.2)</summary>
        public CombatFx Fx { get; private set; }
        public FxParticles Particles { get; private set; }
        /// <summary>마우스 유도 표시: 커서 조준점과 입에서 커서까지 궤적</summary>
        public AimGuide Aim { get; private set; }
        /// <summary>스테이지 스타터 (4.7). 스타터 입력을 넘긴 일반 스테이지에서만 만든다. 끝나면 전투가 시작된다</summary>
        public StageStarter Starter { get; private set; }
        public bool InStarter => Starter != null && Starter.Phase != StarterPhase.Done;
        /// <summary>공중 유닛 (4.5). 보너스 스테이지에서는 null</summary>
        public AirUnitSquad AirUnits { get; private set; }
        public ParallaxBackground Background { get; private set; }
        public IReadOnlyList<Building> Buildings => buildings;
        public BattleState State { get; private set; } = BattleState.Playing;
        public int DestroyedCount => buildings.Count(b => !b.IsAlive);
        public float StageLeft { get; private set; }
        public float StageRight { get; private set; }
        /// <summary>종료 연출(슬로모션)까지 끝났는지</summary>
        public bool IsFinished { get; private set; }

        /// <summary>일반 스테이지를 클리어했고 남은 기력이 기준 이상이면 보너스 스테이지 진입 (1.1, 3.5)</summary>
        public bool BonusEligible => Mode == BattleMode.Normal && State == BattleState.Cleared
                                     && Dragon.Vitals.Stamina >= db.balance.bonusStaminaThreshold;

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
                battle.BeginStarter(db, camera, starterInput);
            return battle;
        }

        /// <summary>스타터가 끝날 때까지 드래곤·적·스킬을 멈춘다</summary>
        void BeginStarter(GameDatabase database, Camera camera, IStarterInput starterInput)
        {
            SetCombatActive(false);
            Starter = new GameObject("StageStarter").AddComponent<StageStarter>();
            Starter.transform.SetParent(transform, false);
            Starter.Finished += _ => SetCombatActive(true);
            Starter.Init(database, this, camera, camera.GetComponent<BattleCamera>(), starterInput);
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
            Mode = mode;
            Stats = stats;
            // 감별안 황금화 판정용. 스테이지마다 같은 결과가 나오게 시드를 고정한다
            var gildRandom = new System.Random(stage.index * 7919);
            var balance = db.balance;

            camera.orthographic = true;
            camera.orthographicSize = (float)balance.cameraOrthoSize;
            camera.transform.position = new Vector3(0, camera.orthographicSize, -10);

            float groundY = (float)balance.groundHeight;
            float dragonStartX = 0f;

            // 건물 배치: 스프라이트 경계(여백 제외) 사이 간격을 일정하게 둔다 (3.4, 9.3)
            var buildingRoot = new GameObject("Buildings").transform;
            buildingRoot.SetParent(transform, false);
            float cursor = dragonStartX + (float)balance.stageStartMargin;
            int attackOrder = 0;
            int gildedOrder = 0;
            var order = mode == BattleMode.Bonus
                ? Enumerable.Repeat(BuildingType.Golden, stage.goldenCount).ToArray()
                : StageLayout.BuildOrder(stage.normalCount, stage.attackCount, stage.landmarkCount);
            for (int i = 0; i < order.Length; i++)
            {
                var type = order[i];
                int typeOrder = type == BuildingType.Attack ? attackOrder++ : 0;
                // 감별안: 일반 스테이지의 일반·공격 건물이 확률로 황금화된다. 랜드마크는 제외 (4.8)
                bool gilded = mode == BattleMode.Normal && (type is BuildingType.Normal or BuildingType.Attack)
                              && gildRandom.NextDouble() < stats.GoldenChance;
                var sprite = gilded
                    ? art.GildedSprite(stage.theme, type, stage.index, i, gildedOrder++)
                    : art.BuildingSprite(stage.theme, type, stage.index, i, typeOrder);
                var content = ContentRect(sprite, (float)balance.artMarginPx);

                var go = new GameObject($"{i:00}_{type}");
                go.transform.SetParent(buildingRoot, false);
                go.transform.position = new Vector3(cursor - content.xMin, groundY, 0);
                cursor = go.transform.position.x + content.xMax + (float)balance.buildingSpacing;

                var building = go.AddComponent<Building>();
                double gold = gilded
                    ? Formulas.BuildingGold(stage.RawHp(type), balance.goldPerHp, db.BuildingTypeInfo(BuildingType.Golden).goldMultiplier)
                    : db.BuildingGold(stage, type);
                building.Init(type, i, db.ActualHp(stage, type), gold, sprite, SortingOrders.Building,
                    content, (float)balance.colliderScaleX, (float)balance.colliderScaleY, (float)balance.hitFlashDuration, gilded);
                buildings.Add(building);
            }
            StageLeft = dragonStartX - (float)balance.stageStartMargin;
            StageRight = cursor + (float)balance.stageStartMargin;

            Dragon = new GameObject("Dragon").AddComponent<DragonController>();
            Dragon.transform.SetParent(transform, false);
            Dragon.transform.position = new Vector3(dragonStartX, groundY, 0);
            Dragon.Init(db, art, stats, input, buildings, stage.index, carriedVitals);
            // 보너스 스테이지는 매우 제한적이다: 기력이 2배로 빨리 줄고, 체력·기력 회복(포식, 체력 회복 등)이 모두 막힌다 (3.5)
            bool bonus = mode == BattleMode.Bonus;
            Dragon.Vitals.DrainMultiplier = bonus ? balance.bonusStaminaDrainMultiplier : 1;
            Dragon.Vitals.RecoveryBlocked = bonus;

            // 포식: 건물 파괴 시 기력 회복 (4.8)
            foreach (var b in buildings)
                b.Destroyed += _ => Dragon.Vitals.RestoreStamina(stats.StaminaOnDestroy);

            foreach (var building in buildings)
            {
                if (building.Type is not (BuildingType.Attack or BuildingType.Landmark)) continue;
                bool landmark = building.Type == BuildingType.Landmark;
                var turret = building.gameObject.AddComponent<BuildingTurret>();
                turret.Init(building, Dragon, camera, GrayboxSprites.Square,
                    landmark ? stage.landmarkDamage : stage.attackDamage,
                    (float)balance.buildingAps,
                    (float)(landmark ? balance.landmarkRange : balance.attackBuildingRange),
                    (float)balance.projectileSpeed, (float)balance.projectileSize,
                    landmark ? Array.ConvertAll(balance.landmarkShotHeights, h => (float)h) : new[] { (float)balance.attackShotHeight });
                turrets.Add(turret);
            }

            Drops = new GameObject("Drops").AddComponent<DropSystem>();
            Drops.transform.SetParent(transform, false);
            Drops.Init(db, art, stage, Dragon, buildings, stats.GoldMultiplier, stats.ChainGoldPerStack);

            var era = art.Era(stage.era);
            var background = Background = new GameObject("Background").AddComponent<ParallaxBackground>();
            background.transform.SetParent(transform, false);
            background.Init(camera, era.far, era.mid, era.ground, (float)balance.parallaxMidFactor);

            if (!camera.TryGetComponent(out BattleCamera follow))
                follow = camera.gameObject.AddComponent<BattleCamera>();
            follow.Init(camera, Dragon.transform, (float)balance.cameraDragonOffsetX, StageLeft, StageRight);

            // 공중 유닛 (4.5): 일반 스테이지에만 나온다. 클리어 조건에는 들어가지 않는다
            if (mode == BattleMode.Normal)
            {
                AirUnits = new GameObject("AirUnits").AddComponent<AirUnitSquad>();
                AirUnits.transform.SetParent(transform, false);
                AirUnits.Init(db, art, stage, Dragon, camera);
                AirUnits.Spawned += unit => unit.Destroyed += Drops.OnAirUnitDowned;
                Dragon.SetAirTargets(AirUnits.Units);
            }

            // 건물·공중 유닛 아래 체력바
            foreach (var b in buildings) WorldHealthBar.Attach(b);
            if (AirUnits != null) AirUnits.Spawned += unit => WorldHealthBar.Attach(unit);

            Fx = new GameObject("CombatFx").AddComponent<CombatFx>();
            Fx.transform.SetParent(transform, false);
            Fx.Init(camera, buildings, AirUnits);

            // 파츠 동작과 함께 쓰는 파티클 (불꽃, 먼지, 충격파, 타격 튐)
            Particles = new GameObject("Particles").AddComponent<FxParticles>();
            Particles.transform.SetParent(transform, false);
            Dragon.Animator.Fx = Particles;
            Dragon.HitBuilding += (b, _, crit) =>
            {
                var box = Dragon.Hitbox;
                float x = Dragon.Facing > 0 ? b.Left : b.Right;
                Particles.Impact(new Vector2(x, box.yMin + box.height * 0.45f), (crit ? 1.4f : 1f) * (float)Stats.SizeScale);
            };
            Aim = new GameObject("AimGuide").AddComponent<AimGuide>();
            Aim.transform.SetParent(transform, false);
            Aim.Init(Dragon, input);
            Dragon.Stomped += b => Particles.DustBurst(new Vector2(Dragon.Hitbox.center.x, b.Hitbox.yMax), Dragon.Hitbox.width * 0.8f, 6, (float)Stats.SizeScale);
            Dragon.Landed += () => Particles.DustBurst(new Vector2(Dragon.Hitbox.center.x, groundY), Dragon.Hitbox.width, 8, (float)Stats.SizeScale);

            // 패시브 스킬 (4.3). 카메라 흔들림을 쓰므로 카메라 설정 뒤에 만든다
            Skills = new GameObject("PassiveSkills").AddComponent<PassiveSkillSystem>();
            Skills.transform.SetParent(transform, false);
            Skills.Init(db, stats, Dragon, buildings, AirUnits != null ? AirUnits.Units : null, camera, stage.index);

            Actives = new GameObject("ActiveSkills").AddComponent<ActiveSkillSystem>();
            Actives.transform.SetParent(transform, false);
            Actives.Init(db, stats, Dragon, buildings, AirUnits != null ? AirUnits.Units : null, camera, stage.index);
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
                // 승전보: 스테이지 클리어 시 기력 회복. 보너스 진입 판정 전에 준다 (4.8)
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
            Time.timeScale = (float)db.balance.endSlowmoScale;
            yield return new WaitForSecondsRealtime((float)db.balance.endSlowmoDuration);
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
