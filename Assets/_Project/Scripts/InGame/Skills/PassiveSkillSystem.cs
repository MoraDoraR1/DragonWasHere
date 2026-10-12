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
using DWH.InGame.Stage;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DWH.InGame.Skills
{
    /// <summary>
    /// 패시브 스킬 자동 발동 (4.3, 4.1.5 모션 슬롯).
    /// - 스테이지가 시작되면 모든 쿨타임이 함께 돈다. 쿨타임이 끝나도 사거리 안에 대상이 없으면 기다린다.
    /// - 머리 슬롯(파이어 볼·브레스·낙뢰)과 몸통 슬롯(돌진·충격파·회오리)은 각각 한 번에 하나만 발동한다.
    ///   같은 슬롯은 대기열 순서대로 0.3초 간격을 두고, 다른 슬롯끼리는 동시에 발동한다.
    /// - 체력이나 기력이 0이 되면 발동 중인 스킬을 즉시 멈춘다.
    /// 몸통 박치기 자체는 DragonController의 충돌 공격이다. 여기서는 하위 업그레이드 "돌진"만 다룬다.
    /// 공중 유닛도 대상이다: 범위·투사체가 겹치면 맞고, 지정형(파이어 볼·낙뢰)은 후보에 넣는다. 파이어 볼은 건물을 먼저 노린다 (4.3).
    /// </summary>
    public class PassiveSkillSystem : MonoBehaviour
    {
        const float SlotGap = 0.3f;
        const float FireBallSpeed = 14f;
        const float BurnTick = 1f;
        const int BurnTicks = 3;
        const float DashSpeed = 18f;

        /// <summary>HUD에 보여 줄 스킬 상태</summary>
        public class SkillState
        {
            public PassiveSkillRow row;
            /// <summary>돌진처럼 하위 업그레이드로 생기는 발동 스킬이면 그 이름</summary>
            public string displayName;
            public MotionSlot slot;
            public float cooldown;
            public float remaining;
            public bool queued;
            public bool active;
            public int level;
            public int sub;
            public float CooldownRatio => cooldown > 0 ? Mathf.Clamp01(remaining / cooldown) : 0f;
        }

        GameDatabase db;
        PlayerStats stats;
        DragonController dragon;
        IReadOnlyList<Building> buildings;
        IReadOnlyList<AirUnit> airUnits;
        Camera view;
        BattleCamera shaker;
        System.Random random;
        readonly List<SkillState> skills = new();
        readonly Dictionary<MotionSlot, Queue<SkillState>> queues = new()
        {
            [MotionSlot.Head] = new Queue<SkillState>(),
            [MotionSlot.Body] = new Queue<SkillState>(),
        };
        readonly Dictionary<MotionSlot, float> slotFreeAt = new() { [MotionSlot.Head] = 0, [MotionSlot.Body] = 0 };
        readonly List<Coroutine> running = new();
        float clock;
        bool stopped;

        public IReadOnlyList<SkillState> Skills => skills;
        /// <summary>스킬 이름, 대상, 대미지, 치명타. 테스트와 연출용</summary>
        public event Action<PassiveSkillId, ICombatTarget, double, bool> SkillHit;
        public event Action<SkillState> Cast;

        /// <param name="flyingUnits">공중 유닛 목록. 보너스 스테이지처럼 없으면 null</param>
        public void Init(GameDatabase database, PlayerStats playerStats, DragonController owner, IReadOnlyList<Building> stageBuildings,
            IReadOnlyList<AirUnit> flyingUnits, Camera camera, int randomSeed)
        {
            airUnits = flyingUnits ?? Array.Empty<AirUnit>();
            db = database;
            stats = playerStats;
            dragon = owner;
            buildings = stageBuildings;
            view = camera;
            camera.TryGetComponent(out shaker);
            random = new System.Random(randomSeed * 31 + 7);

            foreach (var row in db.passiveSkills.rows)
            {
                int level = stats.SkillLevel(row.skill);
                int sub = stats.SubLevel(row.skill);
                if (row.skill == PassiveSkillId.BodySlam)
                {
                    // 몸통 박치기는 충돌 공격이라 쿨타임이 없다. 돌진(하위 업그레이드)을 배웠으면 몸통 슬롯 스킬로 넣는다
                    if (sub > 0)
                        skills.Add(new SkillState { row = row, displayName = row.subUpgradeNameKo, slot = MotionSlot.Body,
                            cooldown = (float)row.subUpgradeParam, level = Math.Max(1, level), sub = sub });
                    continue;
                }
                if (level <= 0) continue;
                skills.Add(new SkillState { row = row, displayName = row.nameKo, slot = row.motionSlot,
                    cooldown = (float)row.cooldown, level = level, sub = sub });
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
                running.Add(StartCoroutine(Run(next)));
            }
        }

        void StopAll()
        {
            stopped = true;
            // 중첩 코루틴(각 스킬 본문)까지 멈추도록 이 컴포넌트의 코루틴을 모두 멈춘다
            StopAllCoroutines();
            running.Clear();
            dragon.IsParrying = false;
            dragon.Animator.StopSkills();
            foreach (var s in skills) s.active = false;
        }

        IEnumerator Run(SkillState s)
        {
            s.active = true;
            s.remaining = s.cooldown;
            Cast?.Invoke(s);
            yield return s.row.skill switch
            {
                PassiveSkillId.BodySlam => Dash(s),
                PassiveSkillId.FireBall => FireBall(s),
                PassiveSkillId.FireBreath => FireBreath(s),
                PassiveSkillId.Lightning => Lightning(s),
                PassiveSkillId.Shockwave => Shockwave(s),
                PassiveSkillId.Whirlwind => Whirlwind(s),
                _ => null,
            };
            s.active = false;
            slotFreeAt[s.slot] = clock + SlotGap;
        }

        // ───────── 수치 ─────────

        /// <summary>스킬 배율 = 기본 배율 × (1 + 레벨당 보너스 × (레벨 - 1)). 낙뢰는 번개 수가 다 찬 뒤부터 보너스가 붙는다</summary>
        public static double Multiplier(PassiveSkillRow row, int level)
        {
            if (row.skill == PassiveSkillId.Lightning)
            {
                int levelsToCap = row.perLevelExtra > 0 ? (int)Math.Ceiling((row.maxHitCount - row.hitCount) / row.perLevelExtra) : 0;
                int extraLevels = Math.Max(0, level - 1 - levelsToCap);
                return row.damageMultiplier * (1 + row.perLevelDamageBonus * extraLevels);
            }
            return row.damageMultiplier * (1 + row.perLevelDamageBonus * (level - 1));
        }

        /// <summary>낙뢰 번개 수: 3 → 레벨마다 +1 → 최대 8</summary>
        public static int LightningBolts(PassiveSkillRow row, int level) =>
            Math.Min(row.maxHitCount, row.hitCount + (int)(row.perLevelExtra * (level - 1)));

        static double SubValue(SkillState s, double fallback) =>
            s.sub > 0 && s.row.subUpgradeValues.Length > 0 ? s.row.subUpgradeValues[Math.Min(s.sub, s.row.subUpgradeValues.Length) - 1] : fallback;

        void Hit(SkillState s, ICombatTarget target, double multiplier)
        {
            if (target == null || !target.IsAlive) return;
            bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
            double amount = Damage.Compute(stats.AttackPower, multiplier, critical, stats.CritMultiplier);
            target.TakeDamage(amount, critical);
            SkillHit?.Invoke(s.row.skill, target, amount, critical);
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

        /// <summary>범위가 겹치는 공중 유닛</summary>
        IEnumerable<AirUnit> AirIn(Rect area) => AliveAir.Where(u => u.Hitbox.Overlaps(area)).ToList();

        /// <summary>정면에서 가까운 순서의 화면 안 대상</summary>
        List<Building> FrontTargets() =>
            Alive.Where(b => Ahead(b) && OnScreen(b)).OrderBy(b => Mathf.Abs(b.CenterX - dragon.Hitbox.center.x)).ToList();

        bool HasTarget(SkillState s) => s.row.skill switch
        {
            PassiveSkillId.BodySlam => FrontTargets().Count > 0,
            PassiveSkillId.FireBall => FireBallTargets().Count > 0,
            PassiveSkillId.FireBreath => Alive.Any(b => Ahead(b) && Gap(b) <= s.row.range) || AirIn(BreathArea(s.row.range)).Any(),
            PassiveSkillId.Lightning => Alive.Any(OnScreen) || AliveAir.Any(OnScreen),
            PassiveSkillId.Shockwave => Alive.Any(b => Gap(b) <= ShockRadius(s)),
            PassiveSkillId.Whirlwind => Alive.Any(b => Gap(b) <= s.row.range),
            _ => false,
        };

        float ShockRadius(SkillState s) => (float)SubValue(s, s.row.range);

        /// <summary>파이어 브레스가 덮는 영역 (SkillFx.PlaceCone과 같은 사각형)</summary>
        Rect BreathArea(double range)
        {
            var box = dragon.Hitbox;
            float width = (float)range + box.width * 0.3f;
            float x = dragon.Facing > 0 ? box.xMax - box.width * 0.3f : box.xMin + box.width * 0.3f - width;
            return new Rect(x, box.yMin + box.height * 0.25f, width, box.height * 0.6f);
        }

        // ───────── 스킬 ─────────

        /// <summary>돌진: 앞쪽에 남은 건물 N개에 100%를 주고 그 앞까지 전진한다 (4.3, 구현은 [임시])</summary>
        IEnumerator Dash(SkillState s)
        {
            int count = (int)SubValue(s, 1);
            // 몸을 크게 들이밀며 돌진한다
            dragon.Animator.Lunge(1.3f);
            dragon.Animator.Kick();
            foreach (var b in FrontTargets().Take(count))
                Hit(s, b, s.row.damageMultiplier);
            var fx = dragon.Animator.Fx;

            float gap = dragon.GapAhead();
            float distance = Mathf.Min(gap == float.MaxValue ? 2f : Mathf.Max(0, gap), 3f);
            while (distance > 0.001f)
            {
                float step = Mathf.Min(distance, DashSpeed * Time.deltaTime);
                dragon.Shift(dragon.Facing * step);
                distance -= step;
                dragon.Animator.Lunge(1.3f);
                if (fx != null)
                {
                    var box = dragon.Hitbox;
                    fx.Emit(Fx.FxSprites.Soft, new Vector2(dragon.Facing > 0 ? box.xMin : box.xMax, box.center.y + Random.Range(-0.4f, 0.4f) * box.height),
                        new Vector2(-dragon.Facing * 4f, 0), 0.25f, 0.5f, 0.1f, new Color(1f, 0.95f, 0.75f, 0.6f), new Color(1f, 0.9f, 0.6f, 0f));
                }
                yield return null;
            }
        }

        /// <summary>파이어 볼: 정면에서 가까운 대상 순으로 N발. 불덩이는 대상을 따라 휜다</summary>
        IEnumerator FireBall(SkillState s)
        {
            int count = (int)SubValue(s, s.row.hitCount);
            double multiplier = Multiplier(s.row, s.level);
            for (int i = 0; i < count; i++)
            {
                var targets = FireBallTargets();
                if (targets.Count == 0) yield break;
                var target = targets[i % targets.Count];
                // 입을 벌리며 입에서 불덩이를 뱉는다
                dragon.Animator.PulseJaw();
                var origin = dragon.Animator.Mouth;
                SkillFx.Homing(transform.parent, origin, target, FireBallSpeed, new Color(1f, 0.5f, 0.15f),
                    hit => Hit(s, hit, multiplier), () => FireBallTargets().FirstOrDefault());
                yield return new WaitForSeconds((float)s.row.hitInterval);
            }
        }

        /// <summary>파이어 브레스: 0.5초마다 정면 부채꼴 사거리 안의 모든 대상. 화상은 확률로 3초간 1초마다 브레스 대미지의 10%</summary>
        IEnumerator FireBreath(SkillState s)
        {
            double multiplier = Multiplier(s.row, s.level);
            double burnChance = s.sub > 0 ? SubValue(s, 0) : 0;
            // 아래턱을 벌리고 입에서 불을 뿜는다. 대미지는 0.5초 간격, 불꽃은 매 프레임
            dragon.Animator.SetBreathing(true);
            float range = (float)s.row.range;
            float tickTimer = 0;
            int ticks = 0;
            while (ticks < s.row.hitCount)
            {
                EmitBreath(range);
                tickTimer += Time.deltaTime;
                if (tickTimer < s.row.hitInterval && ticks > 0) { yield return null; continue; }
                tickTimer = ticks == 0 ? 0 : tickTimer - (float)s.row.hitInterval;
                ticks++;
                var inBreath = Alive.Where(b => Ahead(b) && Gap(b) <= range).Cast<ICombatTarget>().Concat(AirIn(BreathArea(range))).ToList();
                foreach (var b in inBreath)
                {
                    bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
                    double amount = Damage.Compute(stats.AttackPower, multiplier, critical, stats.CritMultiplier);
                    b.TakeDamage(amount, critical);
                    SkillHit?.Invoke(s.row.skill, b, amount, critical);
                    if (b.IsAlive && random.NextDouble() < burnChance)
                        Burn.Apply(b, amount * s.row.subUpgradeParam, BurnTick, BurnTicks);
                }
                yield return null;
            }
            // 마지막 틱 뒤에도 불꽃이 잠깐 이어지게 한 박자 더 뿜는다
            for (float t = 0; t < s.row.hitInterval; t += Time.deltaTime)
            {
                EmitBreath(range);
                yield return null;
            }
            dragon.Animator.SetBreathing(false);
        }

        void EmitBreath(float range)
        {
            var fx = dragon.Animator.Fx;
            if (fx == null) return;
            float scale = (float)stats.SizeScale;
            var dir = new Vector2(dragon.Facing, -0.18f).normalized;
            // 마우스로 조준 중이고 조준 지점이 앞쪽이면 그쪽으로 뿜는다 (수평에서 ±35도 안)
            var mouthNow = dragon.Animator.Mouth;
            if (dragon.Animator.Aim is Vector2 aim && (aim.x - mouthNow.x) * dragon.Facing > 0.2f)
            {
                float ang = Mathf.Clamp(Mathf.Atan2(aim.y - mouthNow.y, Mathf.Abs(aim.x - mouthNow.x)) * Mathf.Rad2Deg, -35f, 35f) * Mathf.Deg2Rad;
                dir = new Vector2(dragon.Facing * Mathf.Cos(ang), Mathf.Sin(ang));
            }
            int count = Mathf.Max(2, Mathf.RoundToInt(Time.deltaTime * 170f));
            // 불꽃이 브레스 판정 끝(드래곤 앞 range)까지 닿게 날린다
            var mouth = dragon.Animator.Mouth;
            float reach = Mathf.Abs((dragon.Facing > 0 ? dragon.Hitbox.xMax + range : dragon.Hitbox.xMin - range) - mouth.x);
            // 판정 범위 끝보다 조금 더 뻗어 불바다처럼 보이게 한다
            fx.Fire(mouth, dir, Mathf.Max(1.6f * scale, reach * 1.3f), scale, count);
        }

        /// <summary>낙뢰: 포효 0.5초 뒤 0.1초 간격으로 화면 안 무작위 대상에 번개. 연쇄 번개는 확률로 양옆 대상에 20%</summary>
        IEnumerator Lightning(SkillState s)
        {
            const float RoarDelay = 0.5f;
            double multiplier = Multiplier(s.row, s.level);
            double chainChance = s.sub > 0 ? SubValue(s, 0) : 0;
            // 고개를 들고 포효한 뒤 번개가 떨어진다
            dragon.Animator.Roar(RoarDelay + 0.3f);
            yield return new WaitForSeconds(RoarDelay);
            int bolts = LightningBolts(s.row, s.level);
            for (int i = 0; i < bolts; i++)
            {
                var candidates = Alive.Where(OnScreen).Cast<ICombatTarget>().Concat(AliveAir.Where(OnScreen)).ToList();
                if (candidates.Count == 0) yield break;
                var target = candidates[random.Next(candidates.Count)];
                SkillFx.Bolt(transform.parent, target.Hitbox, new Color(1f, 0.95f, 0.5f));
                Hit(s, target, multiplier);
                if (target is Building struck && random.NextDouble() < chainChance)
                {
                    foreach (var side in Neighbors(struck))
                    {
                        SkillFx.Bolt(transform.parent, side.Hitbox, new Color(0.7f, 0.85f, 1f));
                        Hit(s, side, multiplier * s.row.subUpgradeParam);
                    }
                }
                yield return new WaitForSeconds((float)s.row.hitInterval);
            }
        }

        IEnumerable<Building> Neighbors(Building target)
        {
            var left = Alive.Where(b => b != target && b.CenterX < target.CenterX).OrderByDescending(b => b.CenterX).FirstOrDefault();
            var right = Alive.Where(b => b != target && b.CenterX > target.CenterX).OrderBy(b => b.CenterX).FirstOrDefault();
            if (left != null) yield return left;
            if (right != null) yield return right;
        }

        /// <summary>충격파: 드래곤 앞뒤 범위 안의 모든 대상에 150%. 공격 건물은 1초 기절, 화면 0.3초 흔들림</summary>
        IEnumerator Shockwave(SkillState s)
        {
            const float StunSeconds = 1f;
            const float ShakeSeconds = 0.3f;
            float radius = ShockRadius(s);
            double multiplier = Multiplier(s.row, s.level);
            // 몸을 들었다가 발로 땅을 쿵 찍는다. 발이 닿는 순간 충격파가 퍼진다.
            // 준비 동작 동안 드래곤이 움직일 수 있으므로 발동할 때 범위 안에 있던 대상도 기억해 둔다
            var atCast = Alive.Where(b => Gap(b) <= radius).ToList();
            yield return new WaitForSeconds(dragon.Animator.Stomp());
            var box = dragon.Hitbox;
            var ground = new Vector2(box.center.x, box.yMin);
            var fx = dragon.Animator.Fx;
            if (fx != null)
            {
                float reach = box.width / 2 + radius;
                fx.GroundFlash(ground, box.width, (float)stats.SizeScale);
                fx.ShockRing(ground, reach, new Color(1f, 0.95f, 0.8f, 1f), 0.5f);
                fx.ShockRing(ground, reach * 0.65f, new Color(0.95f, 0.75f, 0.45f, 0.95f), 0.35f);
                fx.DustBurst(ground, box.width + radius, 22, (float)stats.SizeScale);
            }
            if (shaker != null) shaker.Shake(ShakeSeconds, 0.15f);
            var inRange = Alive.Where(b => Gap(b) <= radius).ToList();
            foreach (var b in atCast.Where(b => b.IsAlive && !inRange.Contains(b))) inRange.Add(b);
            foreach (var b in inRange)
            {
                if (b.TryGetComponent(out BuildingTurret turret)) turret.Stun(StunSeconds);
                Hit(s, b, multiplier);
            }
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>회전 회오리: 회전 1회(0.3초)마다 앞뒤 가장 가까운 대상 각 1개에 150%. 도는 동안 탄을 튕겨 낸다. 이동은 그대로</summary>
        IEnumerator Whirlwind(SkillState s)
        {
            int spins = (int)SubValue(s, s.row.hitCount);
            double multiplier = Multiplier(s.row, s.level);
            float reach = (float)s.row.range;
            // 붕 떠서 날개를 펴고 제자리에서 빙글빙글 돈다 (이동은 그대로)
            dragon.Animator.Spin(spins * (float)s.row.hitInterval, dragon.Facing);
            dragon.IsParrying = true;
            try
            {
                for (int i = 0; i < spins; i++)
                {
                    EmitWind();
                    float cx = dragon.Hitbox.center.x;
                    var front = Alive.Where(b => b.CenterX >= cx && Gap(b) <= reach).OrderBy(Gap).FirstOrDefault();
                    var back = Alive.Where(b => b.CenterX < cx && Gap(b) <= reach).OrderBy(Gap).FirstOrDefault();
                    Hit(s, front, multiplier);
                    Hit(s, back, multiplier);
                    // 꼬리가 휩쓰는 범위에 겹친 공중 유닛도 맞는다
                    var box = dragon.Hitbox;
                    foreach (var u in AirIn(new Rect(box.xMin - reach, box.yMin, box.width + reach * 2, box.height * 1.1f)))
                        Hit(s, u, multiplier);
                    yield return new WaitForSeconds((float)s.row.hitInterval);
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
                fx.Emit(Fx.FxSprites.Soft, center + dir * r, tangent * 7f, 0.28f, 0.45f, 0.15f,
                    new Color(0.85f, 1f, 0.95f, 0.7f), new Color(0.8f, 0.95f, 1f, 0f), drag: 2f);
            }
        }
    }

    /// <summary>화상: 일정 시간 간격으로 고정 대미지를 준다. 다시 걸리면 중첩하지 않고 갱신한다 (4.3)</summary>
    public class Burn : MonoBehaviour
    {
        ICombatTarget target;
        double damage;
        float interval, timer;
        int ticksLeft;

        /// <param name="building">건물 또는 공중 유닛 (MonoBehaviour)</param>
        public static void Apply(ICombatTarget building, double damagePerTick, float tickSeconds, int ticks)
        {
            if (building is not MonoBehaviour owner) return;
            if (!owner.TryGetComponent(out Burn burn)) burn = owner.gameObject.AddComponent<Burn>();
            burn.target = building;
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
