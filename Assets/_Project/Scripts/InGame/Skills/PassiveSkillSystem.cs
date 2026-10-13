using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWH.Core;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using DWH.InGame.Dragon;
using DWH.InGame.Fx;
using DWH.InGame.Stage;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DWH.InGame.Skills
{
    /// <summary>
    /// 패시브 스킬 자동 발동 (가이드 6.8, 임시unity제작.md 4.3·4.1.5).
    /// - 해금: 스킬 노드가 1레벨 이상(PlayerStats.Skills). 몸통 박치기는 기본 보유이고 DragonController의 충돌 공격이다.
    ///   돌진은 몸통 박치기 하위 업그레이드(효과 26 = 쿨타임)로 생기는 몸통 슬롯 스킬이다.
    /// - 스테이지가 시작되면 모든 쿨타임이 함께 돈다. 쿨타임이 끝나도 대상이 없으면 기다린다. 쿨타임은 종말의 용(효과 32)만큼 줄어든다.
    /// - 머리 슬롯(파이어 볼·브레스·낙뢰)과 몸통 슬롯(돌진·충격파·회오리)은 각각 한 번에 하나만 발동한다.
    ///   같은 슬롯은 대기열 순서대로 const.motion_queue_interval_sec 간격을 두고, 다른 슬롯끼리는 동시에 발동한다.
    /// - 체력이나 기력이 0이 되면 발동 중인 스킬을 즉시 멈춘다. 공중 유닛도 대상이다.
    /// </summary>
    public class PassiveSkillSystem : MonoBehaviour
    {
        const float FireBallSpeed = 14f;
        const float BurnTick = 1f;
        const float DashSpeed = 18f;

        /// <summary>HUD에 보여 줄 스킬 상태</summary>
        public class SkillState
        {
            public PassiveSpecs.Spec spec;
            public string displayName;
            public string icon;
            public MotionSlot slot;
            public float cooldown;
            public float remaining;
            public bool queued;
            public bool active;
            public PassiveSkillId Id => spec.id;
            public float CooldownRatio => cooldown > 0 ? Mathf.Clamp01(remaining / cooldown) : 0f;
        }

        GameDatabase db;
        ArtCatalog art;
        PlayerStats stats;
        DragonController dragon;
        IReadOnlyList<Building> buildings;
        IReadOnlyList<AirUnit> airUnits;
        Camera view;
        BattleCamera shaker;
        System.Random random;
        float slotGap;
        readonly List<SkillState> skills = new();
        readonly Dictionary<MotionSlot, Queue<SkillState>> queues = new()
        {
            [MotionSlot.Head] = new Queue<SkillState>(),
            [MotionSlot.Body] = new Queue<SkillState>(),
        };
        readonly Dictionary<MotionSlot, float> slotFreeAt = new() { [MotionSlot.Head] = 0, [MotionSlot.Body] = 0 };
        float clock;
        bool stopped;

        public IReadOnlyList<SkillState> Skills => skills;
        /// <summary>스킬, 대상, 대미지, 치명타. 테스트와 연출용</summary>
        public event Action<PassiveSkillId, ICombatTarget, double, bool> SkillHit;
        public event Action<SkillState> Cast;

        /// <param name="flyingUnits">공중 유닛 목록. 보너스 스테이지처럼 없으면 null</param>
        public void Init(GameDatabase database, ArtCatalog catalog, PlayerStats playerStats, DragonController owner, IReadOnlyList<Building> stageBuildings,
            IReadOnlyList<AirUnit> flyingUnits, Camera camera, int randomSeed)
        {
            db = database;
            art = catalog;
            airUnits = flyingUnits ?? Array.Empty<AirUnit>();
            stats = playerStats;
            dragon = owner;
            buildings = stageBuildings;
            view = camera;
            camera.TryGetComponent(out shaker);
            random = new System.Random(randomSeed * 31 + 7);
            slotGap = (float)db.Const("motion_queue_interval_sec");

            foreach (var spec in PassiveSpecs.All)
            {
                if (spec.id == PassiveSkillId.BodySlam) continue;
                float cooldown;
                if (spec.id == PassiveSkillId.Dash)
                {
                    if (stats.DashCooldown <= 0) continue;
                    cooldown = (float)stats.DashCooldown;
                }
                else
                {
                    if (!stats.Has(spec.id)) continue;
                    cooldown = spec.cooldown;
                }
                var node = db.UpgradeByEffect(spec.unlockEffect);
                skills.Add(new SkillState
                {
                    spec = spec,
                    displayName = node != null ? db.Text(node.nameStr) : spec.id.ToString(),
                    icon = spec.icon,
                    slot = spec.slot,
                    cooldown = Mathf.Max(0.5f, cooldown * (float)stats.CooldownMultiplier),
                });
            }
            // 스테이지가 시작되면 모든 쿨타임이 함께 시작된다
            foreach (var s in skills) s.remaining = s.cooldown;
        }

        void Update()
        {
            if (dragon == null || stopped) return;
            if (!dragon.IsAlive || !dragon.Controllable)
            {
                StopAll();
                return;
            }

            float dt = Time.deltaTime;
            clock += dt;
            foreach (var s in skills)
            {
                if (s.active || s.queued) continue;
                s.remaining = Mathf.Max(0, s.remaining - dt);
                if (s.remaining <= 0 && HasTarget(s))
                {
                    s.queued = true;
                    queues[s.slot].Enqueue(s);
                }
            }

            foreach (var slot in queues.Keys.ToArray())
            {
                var queue = queues[slot];
                if (queue.Count == 0 || clock < slotFreeAt[slot]) continue;
                var next = queue.Dequeue();
                next.queued = false;
                if (!HasTarget(next)) continue;   // 기다리는 동안 대상이 사라졌다. 다음 프레임에 다시 판정한다
                slotFreeAt[slot] = float.MaxValue;
                StartCoroutine(Run(next));
            }
        }

        void StopAll()
        {
            stopped = true;
            // 중첩 코루틴(각 스킬 본문)까지 멈추도록 이 컴포넌트의 코루틴을 모두 멈춘다
            StopAllCoroutines();
            dragon.IsParrying = false;
            dragon.Animator.StopSkills();
            foreach (var s in skills) s.active = false;
        }

        IEnumerator Run(SkillState s)
        {
            s.active = true;
            s.remaining = s.cooldown;
            Cast?.Invoke(s);
            yield return s.Id switch
            {
                PassiveSkillId.Dash => Dash(),
                PassiveSkillId.FireBall => FireBall(),
                PassiveSkillId.FireBreath => FireBreath(),
                PassiveSkillId.Lightning => Lightning(),
                PassiveSkillId.Shockwave => Shockwave(),
                PassiveSkillId.Whirlwind => Whirlwind(),
                _ => null,
            };
            s.active = false;
            slotFreeAt[s.slot] = clock + slotGap;
        }

        // ───────── 수치 (PlayerStats가 효과를 반영해 둔다) ─────────

        public int FireBallCount => PassiveSpecs.FireBall.hits + stats.FireBallExtra;
        public int LightningBolts => PassiveSpecs.Lightning.hits + stats.LightningExtra;
        public float BreathRange => PassiveSpecs.FireBreath.range * (float)stats.BreathRangeMultiplier;
        public float WhirlwindRange => PassiveSpecs.Whirlwind.range * (float)stats.WhirlwindRangeMultiplier;
        public int WhirlwindSpins => Mathf.Max(1, Mathf.RoundToInt((PassiveSpecs.Whirlwind.hits * PassiveSpecs.Whirlwind.interval
                                                                      + (float)stats.WhirlwindExtraSeconds) / PassiveSpecs.Whirlwind.interval));

        void Hit(PassiveSkillId id, ICombatTarget target, double multiplier)
        {
            if (target == null || !target.IsAlive) return;
            bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
            double amount = Damage.Compute(dragon.AttackPower, multiplier, critical, stats.CritMultiplier);
            target.TakeDamage(amount, critical);
            SkillHit?.Invoke(id, target, amount, critical);
        }

        // ───────── 대상 ─────────

        (float left, float right) ViewRange()
        {
            float half = view.orthographicSize * view.aspect;
            float x = view.transform.position.x;
            return (x - half, x + half);
        }

        bool OnScreen(ICombatTarget t)
        {
            var (l, r) = ViewRange();
            return t.Hitbox.xMax >= l && t.Hitbox.xMin <= r;
        }

        /// <summary>드래곤 충돌 박스에서 건물까지의 틈. 겹치면 0 이하</summary>
        float Gap(Building b)
        {
            var box = dragon.Hitbox;
            return Mathf.Max(b.Left - box.xMax, box.xMin - b.Right);
        }

        bool Ahead(ICombatTarget t) => dragon.Facing > 0 ? t.Hitbox.xMax > dragon.Hitbox.center.x : t.Hitbox.xMin < dragon.Hitbox.center.x;

        IEnumerable<Building> Alive => buildings.Where(b => b.IsAlive);
        IEnumerable<AirUnit> AliveAir => airUnits.Where(u => u.IsAlive);

        /// <summary>파이어 볼 대상: 건물 우선(정면 가까운 순), 그다음 정면 화면 안의 공중 유닛</summary>
        List<ICombatTarget> FireBallTargets() =>
            FrontTargets().Cast<ICombatTarget>()
                .Concat(AliveAir.Where(u => Ahead(u) && OnScreen(u)).OrderBy(u => Mathf.Abs(u.Hitbox.center.x - dragon.Hitbox.center.x)))
                .ToList();

        IEnumerable<AirUnit> AirIn(Rect area) => AliveAir.Where(u => u.Hitbox.Overlaps(area)).ToList();

        /// <summary>정면에서 가까운 순서의 화면 안 건물</summary>
        List<Building> FrontTargets() =>
            Alive.Where(b => Ahead(b) && OnScreen(b)).OrderBy(b => Mathf.Abs(b.CenterX - dragon.Hitbox.center.x)).ToList();

        /// <summary>충격파 대상: 맞닿은(가장 가까운) 건물 1개 + 양옆 건물 ShockwaveSides개씩 (가이드 6.7)</summary>
        List<Building> ShockwaveTargets()
        {
            var alive = Alive.OrderBy(b => b.CenterX).ToList();
            var touching = alive.Where(b => Gap(b) <= PassiveSpecs.Shockwave.range).OrderBy(Gap).FirstOrDefault();
            if (touching == null) return new List<Building>();
            int i = alive.IndexOf(touching), sides = stats.ShockwaveSides;
            int from = Math.Max(0, i - sides), to = Math.Min(alive.Count - 1, i + sides);
            return alive.GetRange(from, to - from + 1);
        }

        bool HasTarget(SkillState s) => s.Id switch
        {
            PassiveSkillId.Dash => FrontTargets().Count > 0,
            PassiveSkillId.FireBall => FireBallTargets().Count > 0,
            PassiveSkillId.FireBreath => Alive.Any(b => Ahead(b) && Gap(b) <= BreathRange) || AirIn(BreathArea(BreathRange)).Any(),
            PassiveSkillId.Lightning => Alive.Any(OnScreen) || AliveAir.Any(OnScreen),
            PassiveSkillId.Shockwave => ShockwaveTargets().Count > 0,
            PassiveSkillId.Whirlwind => Alive.Any(b => Gap(b) <= WhirlwindRange),
            _ => false,
        };

        /// <summary>파이어 브레스가 덮는 영역</summary>
        Rect BreathArea(float range)
        {
            var box = dragon.Hitbox;
            float width = range + box.width * 0.3f;
            float x = dragon.Facing > 0 ? box.xMax - box.width * 0.3f : box.xMin + box.width * 0.3f - width;
            return new Rect(x, box.yMin + box.height * 0.25f, width, box.height * 0.6f);
        }

        // ───────── 스킬 ─────────

        /// <summary>돌진: 앞쪽 건물 N개에 몸통 박치기 대미지를 주고 그 앞까지 전진한다 [임시]</summary>
        IEnumerator Dash()
        {
            var spec = PassiveSpecs.Dash;
            dragon.Animator.Lunge(1.3f);
            dragon.Animator.Kick();
            foreach (var b in FrontTargets().Take(spec.hits))
                Hit(PassiveSkillId.Dash, b, stats.BodySlamMultiplier);
            var fx = dragon.Animator.Fx;

            float gap = dragon.GapAhead();
            float distance = Mathf.Min(gap == float.MaxValue ? 2f : Mathf.Max(0, gap), spec.range);
            while (distance > 0.001f)
            {
                float step = Mathf.Min(distance, DashSpeed * Time.deltaTime);
                dragon.Shift(dragon.Facing * step);
                distance -= step;
                dragon.Animator.Lunge(1.3f);
                if (fx != null)
                {
                    var box = dragon.Hitbox;
                    fx.Emit(FxSprites.Soft, new Vector2(dragon.Facing > 0 ? box.xMin : box.xMax, box.center.y + Random.Range(-0.4f, 0.4f) * box.height),
                        new Vector2(-dragon.Facing * 4f, 0), 0.25f, 0.5f, 0.1f, new Color(1f, 0.95f, 0.75f, 0.6f), new Color(1f, 0.9f, 0.6f, 0f));
                }
                yield return null;
            }
        }

        /// <summary>파이어 볼: 정면에서 가까운 대상 순으로 N발. 불덩이는 대상을 따라 휜다</summary>
        IEnumerator FireBall()
        {
            var spec = PassiveSpecs.FireBall;
            double multiplier = stats.FireBallMultiplier;
            for (int i = 0; i < FireBallCount; i++)
            {
                var targets = FireBallTargets();
                if (targets.Count == 0) yield break;
                var target = targets[i % targets.Count];
                dragon.Animator.PulseJaw();
                SkillFx.Homing(transform.parent, db, art, dragon.Animator.Mouth, target, FireBallSpeed,
                    hit =>
                    {
                        Hit(PassiveSkillId.FireBall, hit, multiplier);
                        SpriteAnim.Play(transform.parent, db, art, FxIds.FireBallExplosion, hit.Hitbox.center, 0.6f);
                    },
                    () => FireBallTargets().FirstOrDefault());
                yield return new WaitForSeconds(spec.interval);
            }
        }

        /// <summary>파이어 브레스: 0.5초마다 정면 사거리 안의 모든 대상. 지옥불(효과 19)이 있으면 화상: 초당 공격력 × v, 3초</summary>
        IEnumerator FireBreath()
        {
            var spec = PassiveSpecs.FireBreath;
            double multiplier = stats.BreathMultiplier;
            dragon.Animator.SetBreathing(true);
            float range = BreathRange;
            // 불길 그림 (DWH-54). 리소스가 없으면(테스트 등) 예전 파티클로 뿜는다
            breathFlame = SkillFx.Breath(transform.parent, db, art, (spec.hits + 1) * spec.interval + 1f);
            float tickTimer = 0;
            int ticks = 0;
            while (ticks < spec.hits)
            {
                EmitBreath(range);
                tickTimer += Time.deltaTime;
                if (tickTimer < spec.interval && ticks > 0) { yield return null; continue; }
                tickTimer = ticks == 0 ? 0 : tickTimer - spec.interval;
                ticks++;
                var inBreath = Alive.Where(b => Ahead(b) && Gap(b) <= range).Cast<ICombatTarget>().Concat(AirIn(BreathArea(range))).ToList();
                foreach (var b in inBreath)
                {
                    Hit(PassiveSkillId.FireBreath, b, multiplier);
                    if (b.IsAlive && stats.BurnRate > 0)
                        Burn.Apply(b, dragon.AttackPower * stats.BurnRate * BurnTick, BurnTick, Mathf.RoundToInt(Tuning.BurnSeconds / BurnTick));
                }
                yield return null;
            }
            // 마지막 틱 뒤에도 불꽃이 잠깐 이어지게 한 박자 더 뿜는다
            for (float t = 0; t < spec.interval; t += Time.deltaTime)
            {
                EmitBreath(range);
                yield return null;
            }
            dragon.Animator.SetBreathing(false);
            if (breathFlame != null) Destroy(breathFlame.gameObject);
            breathFlame = null;
        }

        SpriteAnim breathFlame;
        /// <summary>브레스 그림에서 불길이 차지하는 비율(프레임 폭 기준)과 뿜는 쪽 끝 위치(가운데 기준)</summary>
        const float BreathFlameLength = 0.85f, BreathFlameTip = 0.44f;

        void EmitBreath(float range)
        {
            var fx = dragon.Animator.Fx;
            if (fx == null && breathFlame == null) return;
            float scale = dragon.SizeScale;
            var dir = new Vector2(dragon.Facing, -0.18f).normalized;
            // 마우스로 조준 중이고 조준 지점이 앞쪽이면 그쪽으로 뿜는다 (수평에서 ±35도 안)
            var mouth = dragon.Animator.Mouth;
            if (dragon.Animator.Aim is Vector2 aim && (aim.x - mouth.x) * dragon.Facing > 0.2f)
            {
                float ang = Mathf.Clamp(Mathf.Atan2(aim.y - mouth.y, Mathf.Abs(aim.x - mouth.x)) * Mathf.Rad2Deg, -35f, 35f) * Mathf.Deg2Rad;
                dir = new Vector2(dragon.Facing * Mathf.Cos(ang), Mathf.Sin(ang));
            }
            float reach = Mathf.Abs((dragon.Facing > 0 ? dragon.Hitbox.xMax + range : dragon.Hitbox.xMin - range) - mouth.x);
            if (breathFlame != null)
            {
                // 불길 끝이 입에 오고, 길이가 사거리에 닿게 키운다. 왼쪽을 보면 뒤집는다
                float frameWidth = breathFlame.Renderer.sprite != null ? breathFlame.Renderer.sprite.bounds.size.x : 2.7f;
                float size = Mathf.Max(0.7f * scale, reach / (BreathFlameLength * frameWidth));
                breathFlame.transform.localScale = Vector3.one * size;
                breathFlame.transform.position = mouth + dir * (BreathFlameTip * frameWidth * size);
                breathFlame.Orient(Vector2.right, dir);
                return;
            }
            int count = Mathf.Max(2, Mathf.RoundToInt(Time.deltaTime * 170f));
            fx.Fire(mouth, dir, Mathf.Max(1.6f * scale, reach * 1.3f), scale, count);
        }

        /// <summary>낙뢰: 포효 0.5초 뒤 0.1초 간격으로 화면 안 무작위 대상에 번개</summary>
        IEnumerator Lightning()
        {
            var spec = PassiveSpecs.Lightning;
            double multiplier = stats.LightningMultiplier;
            dragon.Animator.Roar(PassiveSpecs.LightningRoarDelay + 0.3f);
            yield return new WaitForSeconds(PassiveSpecs.LightningRoarDelay);
            for (int i = 0; i < LightningBolts; i++)
            {
                var candidates = Alive.Where(OnScreen).Cast<ICombatTarget>().Concat(AliveAir.Where(OnScreen)).ToList();
                if (candidates.Count == 0) yield break;
                var target = candidates[random.Next(candidates.Count)];
                SkillFx.Bolt(transform.parent, db, art, target.Hitbox);
                Hit(PassiveSkillId.Lightning, target, multiplier);
                yield return new WaitForSeconds(spec.interval);
            }
        }

        /// <summary>충격파: 꼬리로 땅을 내리쳐 맞닿은 건물 + 양옆 건물. 공격 건물은 1초 기절, 화면 0.3초 흔들림</summary>
        IEnumerator Shockwave()
        {
            double multiplier = stats.ShockwaveMultiplier;
            // 준비 동작 동안 드래곤이 움직일 수 있으므로 발동할 때 대상을 정해 둔다
            var targets = ShockwaveTargets();
            yield return new WaitForSeconds(dragon.Animator.Stomp());
            var box = dragon.Hitbox;
            var ground = new Vector2(box.center.x, box.yMin);
            var fx = dragon.Animator.Fx;
            float reach = targets.Count == 0 ? box.width : Mathf.Max(Mathf.Abs(targets.Min(b => b.Left) - ground.x), Mathf.Abs(targets.Max(b => b.Right) - ground.x));
            if (fx != null)
            {
                fx.GroundFlash(ground, box.width, dragon.SizeScale);
                fx.ShockRing(ground, reach, new Color(1f, 0.95f, 0.8f, 1f), 0.5f);
                fx.DustBurst(ground, reach, 22, dragon.SizeScale);
            }
            foreach (var b in targets)
                SpriteAnim.Play(transform.parent, db, art, FxIds.GroundDust, new Vector2(b.CenterX, b.Hitbox.yMin + 0.6f), 0.9f);
            if (shaker != null) shaker.Shake(PassiveSpecs.ShockwaveShakeSeconds, 0.15f);
            foreach (var b in targets)
            {
                if (!b.IsAlive) continue;
                if (b.TryGetComponent(out BuildingTurret turret)) turret.Stun(PassiveSpecs.ShockwaveStunSeconds);
                Hit(PassiveSkillId.Shockwave, b, multiplier);
            }
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>회전 회오리: 회전 1회(0.3초)마다 앞뒤 가장 가까운 대상 각 1개. 도는 동안 탄을 튕겨 낸다. 이동은 그대로</summary>
        IEnumerator Whirlwind()
        {
            var spec = PassiveSpecs.Whirlwind;
            int spins = WhirlwindSpins;
            double multiplier = stats.WhirlwindMultiplier;
            float reach = WhirlwindRange;
            dragon.Animator.Spin(spins * spec.interval, dragon.Facing);
            dragon.IsParrying = true;
            try
            {
                for (int i = 0; i < spins; i++)
                {
                    EmitWind();
                    float cx = dragon.Hitbox.center.x;
                    var front = Alive.Where(b => b.CenterX >= cx && Gap(b) <= reach).OrderBy(Gap).FirstOrDefault();
                    var back = Alive.Where(b => b.CenterX < cx && Gap(b) <= reach).OrderBy(Gap).FirstOrDefault();
                    Hit(PassiveSkillId.Whirlwind, front, multiplier);
                    Hit(PassiveSkillId.Whirlwind, back, multiplier);
                    var box = dragon.Hitbox;
                    foreach (var u in AirIn(new Rect(box.xMin - reach, box.yMin, box.width + reach * 2, box.height * 1.1f)))
                        Hit(PassiveSkillId.Whirlwind, u, multiplier);
                    yield return new WaitForSeconds(spec.interval);
                }
            }
            finally
            {
                dragon.IsParrying = false;
            }
        }

        /// <summary>회오리 바람: 드래곤 둘레를 도는 흰 바람 조각</summary>
        void EmitWind()
        {
            var fx = dragon.Animator.Fx;
            if (fx == null) return;
            var box = dragon.Hitbox;
            var center = box.center + new Vector2(0, box.height * 0.3f);
            float r = box.width * 0.7f;
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f + Random.Range(0f, 0.6f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f);
                var tangent = new Vector2(-dir.y, dir.x) * dragon.Facing;
                fx.Emit(FxSprites.Soft, center + dir * r, tangent * 7f, 0.28f, 0.45f, 0.15f,
                    new Color(0.85f, 1f, 0.95f, 0.7f), new Color(0.8f, 0.95f, 1f, 0f), drag: 2f);
            }
        }
    }

    /// <summary>화상: 일정 시간 간격으로 고정 대미지를 준다. 다시 걸리면 중첩하지 않고 갱신한다</summary>
    public class Burn : MonoBehaviour
    {
        ICombatTarget target;
        double damage;
        float interval, timer;
        int ticksLeft;

        public static void Apply(ICombatTarget victim, double damagePerTick, float tickSeconds, int ticks)
        {
            if (victim is not MonoBehaviour owner) return;
            if (!owner.TryGetComponent(out Burn burn)) burn = owner.gameObject.AddComponent<Burn>();
            burn.target = victim;
            burn.damage = damagePerTick;
            burn.interval = tickSeconds;
            burn.timer = tickSeconds;
            burn.ticksLeft = ticks;
            burn.enabled = true;
        }

        void Update()
        {
            if (target == null || !target.IsAlive || ticksLeft <= 0)
            {
                enabled = false;
                return;
            }
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer += interval;
            ticksLeft--;
            target.TakeDamage(damage, false);
        }
    }
}
