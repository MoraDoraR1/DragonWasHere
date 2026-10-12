using System;
using System.Collections.Generic;
using DWH.Core;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using UnityEngine;

namespace DWH.InGame.Dragon
{
    /// <summary>
    /// 드래곤 이동, 몸통 박치기, 점프 (4.1.2, 8.5.1, 8.6).
    /// - 목표 x(마우스 커서 또는 자동 모드의 가장 가까운 남은 건물)를 향해 이동한다.
    /// - 건물은 통과하지 못한다. 앞 건물에 닿으면 멈추고, 목표가 그 건물 너머에 있으면 몸통 박치기를 한다.
    /// - 타격할 때마다 뒤로 조금 밀린다(넉백).
    /// - 몸통 박치기가 타격할 때마다 점프 확률로 뛰어오른다. 체공 중에도 땅과 같은 이동 속도로 목표 쪽으로 움직이고, 지면 탄에 맞지 않으며, 착지할 때 주변을 내리찍는다.
    /// 패시브 스킬(PassiveSkillSystem)은 이 컴포넌트 밖에서 쿨타임마다 발동한다.
    /// </summary>
    public class DragonController : MonoBehaviour
    {
        DragonSpeciesData species;
        PlayerStats stats;
        IGuideInput input;
        IReadOnlyList<Building> buildings;
        IReadOnlyList<AirUnit> airTargets;
        readonly HashSet<AirUnit> hitThisJump = new();
        Rect localHitbox;
        System.Random random;

        float attackTimer;
        float knockbackTimer;
        float facing = 1f;
        float groundY;
        float jumpCooldownTimer;
        Vector2 velocity;
        Building jumpTarget;
        readonly Dictionary<Building, float> hitCooldown = new();
        const float LandingRadius = 0.5f;

        // 점프 물리 (웹 프로토타입 v0.3 값을 1 unit = 100 px로 옮김) [임시]
        const float Gravity = 15f;           // 1500 px/s²
        const float JumpClearance = 0.45f;   // 지붕 위 여유 높이 (× 크기)
        const float MinJumpHeight = 0.5f;    // 최소 점프 높이 (× 크기)
        const float MaxJumpVx = 9f;
        const float AirControl = 1.2f;       // 공중 가로 속도를 맞추는 빠르기
        const float BounceSpeed = 3.6f;      // 짓밟은 뒤 다시 튀어 오르는 속도 (360 px/s)
        const float StompWindow = 0.16f;     // 지붕 아래 이만큼까지는 위에서 밟은 것으로 본다
        const float StompMultiplier = 1.6f;  // 짓밟기 = 몸통 박치기 × 1.6
        const float StompCooldown = 0.3f;    // 같은 건물 짓밟기 간격
        const float SideHitCooldown = 0.45f; // 공중 옆 들이받기 간격
        const float LandingImpactMin = 4.2f; // 이보다 빠르게 떨어져야 착지 타격 (420 px/s)

        public DragonVitals Vitals { get; private set; }
        public DragonRig Rig { get; private set; }
        /// <summary>파츠 애니메이션 (걷기, 차기, 브레스, 회오리, 발 구르기)</summary>
        public DragonAnimator Animator { get; private set; }
        public bool Controllable { get; set; } = true;
        public bool IsAlive => Vitals != null && !Vitals.IsDown;
        public bool IsGuided { get; private set; }
        public bool IsAttacking { get; private set; }
        /// <summary>점프 체공 중 (8.6). 지붕 위에서 튀는 동안도 포함한다. 지면 탄에 맞지 않는다</summary>
        public bool IsAirborne { get; private set; }
        /// <summary>회전 회오리 중. 몸에 닿는 탄을 튕겨 낸다 (4.3)</summary>
        public bool IsParrying { get; set; }
        /// <summary>바라보는 방향. 1 = 오른쪽, -1 = 왼쪽</summary>
        public float Facing => facing;
        /// <summary>공중 속도 (테스트·연출용)</summary>
        public Vector2 Velocity => velocity;

        /// <summary>충돌 박스 (월드 좌표)</summary>
        public Rect Hitbox => new(localHitbox.position + (Vector2)transform.position, localHitbox.size);

        public event Action<Building, double, bool> HitBuilding;
        public event Action<AirUnit, double, bool> HitAirUnit;
        public event Action<double> Hurt;
        public event Action Jumped;
        public event Action Landed;
        /// <summary>지붕 짓밟기 (연출용)</summary>
        public event Action<Building> Stomped;

        public PlayerStats Stats => stats;

        /// <param name="playerStats">업그레이드가 반영된 능력치 (4.8)</param>
        /// <param name="carriedVitals">보너스 스테이지처럼 이전 전투의 상태를 이어받을 때. null이면 능력치로 새로 만든다</param>
        public void Init(GameDatabase db, ArtCatalog art, PlayerStats playerStats, IGuideInput guideInput, IReadOnlyList<Building> stageBuildings,
            int randomSeed, DragonVitals carriedVitals = null)
        {
            species = db.dragon;
            stats = playerStats;
            input = guideInput;
            buildings = stageBuildings;
            random = new System.Random(randomSeed);
            groundY = transform.position.y;

            Vitals = carriedVitals ?? new DragonVitals(stats.MaxHp, stats.HpRegenPerSecond, stats.MaxStamina,
                species.fatigueStart, species.fatigueInterval, species.fatigueStep);

            Rig = new GameObject("Rig").AddComponent<DragonRig>();
            Rig.transform.SetParent(transform, false);
            // 성장 단계 크기 (4.1.4). 조립 전에 키워 두면 충돌 박스도 같이 커진다
            Rig.transform.localScale = Vector3.one * (float)stats.SizeScale;
            Rig.Build(db.dragonParts.rows, art, (float)db.balance.mirrorPartTint, db.balance.dragonSortingBase);

            var b = Rig.LocalBounds;
            float w = b.width * (float)db.balance.colliderScaleX;
            float h = b.height * (float)db.balance.colliderScaleY;
            localHitbox = new Rect(b.center.x - w / 2f, b.yMin, w, h);

            Animator = gameObject.AddComponent<DragonAnimator>();
            Animator.Init(Rig, groundY, (float)stats.SizeScale);
        }

        /// <summary>점프 중 몸통으로 칠 수 있는 공중 유닛 목록 (8.6)</summary>
        public void SetAirTargets(IReadOnlyList<AirUnit> units) => airTargets = units;

        public void TakeHit(double damage, Projectile source)
        {
            // 전투가 끝난 뒤(슬로모션 중)에 날아든 탄은 맞지 않는다. 클리어 직후 쓰러져 보너스 진입이 뒤집히지 않게 한다
            if (!IsAlive || !Controllable) return;
            if (IsParrying) return;
            Vitals.TakeDamage(damage);
            Hurt?.Invoke(damage);
        }

        void Update()
        {
            if (Vitals == null || !IsAlive || !Controllable)
            {
                IsAttacking = false;
                return;
            }

            float dt = Time.deltaTime;
            Vitals.Tick(dt);
            if (!IsAlive) return;

            attackTimer -= dt;
            jumpCooldownTimer -= dt;
            if (IsAirborne)
            {
                UpdateJump(dt);
                IsAttacking = false;
                return;
            }
            if (knockbackTimer > 0)
            {
                knockbackTimer -= dt;
                Move(-facing * (float)(species.knockbackDistance / species.knockbackDuration) * dt);
                IsAttacking = false;
                return;
            }

            IsGuided = input.TryGetGuideX(out float targetX);
            if (!IsGuided && !TryGetNearestBuildingX(out targetX))
            {
                IsAttacking = false;
                return;
            }

            float tolerance = (float)species.contactTolerance;
            var box = Hitbox;
            float direction = targetX > box.xMax ? 1f : targetX < box.xMin ? -1f : 0f;
            IsAttacking = false;
            if (direction == 0)
            {
                // 목표 건물과 몸이 겹쳐 방향이 정해지지 않으면(예: 착지 직후) 겹친 건물을 그대로 친다
                var overlapped = Overlapping(box);
                if (overlapped == null) return;
                IsAttacking = true;
                if (attackTimer <= 0) Strike(overlapped);
                return;
            }

            facing = direction;
            Rig.SetFacing(direction);

            var blocker = NearestBlocker(direction, box);
            float gap = blocker == null ? float.MaxValue : direction > 0 ? blocker.Left - box.xMax : box.xMin - blocker.Right;

            if (blocker != null && gap <= tolerance)
            {
                IsAttacking = true;
                if (attackTimer <= 0)
                    Strike(blocker);
                return;
            }

            float step = (float)stats.MoveSpeed * dt;
            Move(direction * Mathf.Min(step, Mathf.Max(0, gap)));
        }

        void Strike(Building target)
        {
            bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
            double amount = Damage.Compute(stats.AttackPower, stats.BodySlamMultiplier, critical, stats.CritMultiplier);
            target.TakeDamage(amount, critical);
            HitBuilding?.Invoke(target, amount, critical);
            Animator.Kick();
            attackTimer = 1f / (float)stats.AttacksPerSecond;

            // 점프: 타격할 때마다 점프 확률로 판정한다 (8.6)
            if (jumpCooldownTimer <= 0 && stats.JumpChance > 0 && random.NextDouble() < stats.JumpChance)
            {
                StartJump(target);
                return;
            }
            if (target.IsAlive)
                knockbackTimer = (float)species.knockbackDuration;
        }

        /// <summary>
        /// 점프 (8.6, 웹 프로토타입 v0.3의 jumpOnto 방식). 목표 건물 지붕보다 조금 더 높이(여유 0.45 × 크기) 뛰어올라
        /// 건물 한가운데에 떨어지도록 가로 속도를 정한 포물선. 이후에는 중력과 속도로 움직인다.
        /// </summary>
        public void StartJump() => StartJump(null);

        public void StartJump(Building target)
        {
            if (IsAirborne || !IsAlive) return;
            target ??= NearestBlocker(facing, Hitbox) ?? NearestAlive();
            IsAirborne = true;
            knockbackTimer = 0;
            hitThisJump.Clear();
            jumpTarget = target;
            float size = (float)stats.SizeScale;
            float feet = transform.position.y;
            if (target != null)
            {
                float top = target.Hitbox.yMax;
                float clear = JumpClearance * size;
                float h = Mathf.Max(MinJumpHeight * size, top - feet) + clear;
                float vy = Mathf.Sqrt(2f * Gravity * h);
                float t = vy / Gravity + Mathf.Sqrt(2f * clear / Gravity);
                velocity = new Vector2(Mathf.Clamp((target.CenterX - Hitbox.center.x) / t, -MaxJumpVx, MaxJumpVx), vy);
            }
            else
                velocity = new Vector2(facing * (float)stats.MoveSpeed * 0.9f, Mathf.Sqrt(2f * Gravity * MinJumpHeight * size));
            Jumped?.Invoke();
        }

        /// <summary>
        /// 공중 이동 (중력·속도). 지붕을 위에서 밟으면 짓밟기 후 다시 튀어 오르고(건물마다 0.3초 간격),
        /// 지붕보다 낮은 높이로 건물 옆에 부딪히면 옆으로 밀려나며 들이받고 반동으로 물러난다. 땅에 닿으면 착지한다.
        /// </summary>
        void UpdateJump(float dt)
        {
            // 목표 건물을 향한 포물선이 아니면(짓밟고 튀어 오른 뒤 등) 가로 속도를 목표 쪽 이동 속도의 90%로 서서히 맞춘다
            if (jumpTarget == null || !jumpTarget.IsAlive)
            {
                jumpTarget = null;
                float dir = AirDirection();
                float want = dir * (float)stats.MoveSpeed * 0.9f;
                velocity.x += (want - velocity.x) * Mathf.Min(1f, AirControl * dt);
            }
            if (velocity.x != 0)
            {
                facing = Mathf.Sign(velocity.x);
                Rig.SetFacing(facing);
            }
            float prevFeet = transform.position.y;
            velocity.y -= Gravity * dt;
            transform.position += (Vector3)(velocity * dt);
            StrikeAirUnits();
            CollideBuildings(prevFeet);

            if (transform.position.y > groundY) return;

            // 땅에 착지
            float impact = -velocity.y;
            var p = transform.position;
            p.y = groundY;
            transform.position = p;
            velocity = Vector2.zero;
            IsAirborne = false;
            jumpTarget = null;
            jumpCooldownTimer = (float)species.jumpCooldown;
            // 착지 타격: 착지 지점 0.5 unit 안의 건물에 공격력 × 착지 대미지 (중량 강화). 살짝 내려앉을 때는 없다
            if (impact >= LandingImpactMin)
            {
                var box = Hitbox;
                foreach (var b in buildings)
                {
                    if (!b.IsAlive) continue;
                    float gap = Mathf.Max(b.Left - box.xMax, box.xMin - b.Right);
                    if (gap > LandingRadius) continue;
                    bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
                    double amount = Damage.Compute(stats.AttackPower, stats.LandingDamage, critical, stats.CritMultiplier);
                    b.TakeDamage(amount, critical);
                    HitBuilding?.Invoke(b, amount, critical);
                }
            }
            Landed?.Invoke();
        }

        /// <summary>공중에서 나아갈 방향: 커서 유도 중이면 커서 쪽, 아니면 가장 가까운 건물 쪽(그 위에 있으면 0)</summary>
        float AirDirection()
        {
            var box = Hitbox;
            IsGuided = input.TryGetGuideX(out float targetX);
            if (!IsGuided && !TryGetNearestBuildingX(out targetX)) return 0;
            return targetX > box.xMax ? 1f : targetX < box.xMin ? -1f : 0f;
        }

        /// <summary>공중에서 건물과의 충돌: 위에서 밟기(짓밟기 + 튀어 오르기) / 옆에서 부딪히기(밀려남 + 들이받기 + 반동)</summary>
        void CollideBuildings(float prevFeet)
        {
            foreach (var b in buildings)
            {
                if (!b.IsAlive) continue;
                var box = Hitbox;
                if (box.xMax <= b.Left || box.xMin >= b.Right) continue;
                float top = b.Hitbox.yMax;
                float feet = box.yMin;
                if (feet >= top) continue;

                // 위에서 밟기: 떨어지는 중이고 직전 프레임에 발이 지붕 근처(0.16 아래까지)보다 위에 있었다
                if (velocity.y < 0 && prevFeet >= top - StompWindow)
                {
                    if (CooldownReady(b, StompCooldown))
                    {
                        bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
                        double amount = Damage.Compute(stats.AttackPower, stats.BodySlamMultiplier * StompMultiplier * stats.LandingDamage, critical, stats.CritMultiplier);
                        b.TakeDamage(amount, critical);
                        HitBuilding?.Invoke(b, amount, critical);
                        Stomped?.Invoke(b);
                        Animator.Kick();
                    }
                    // 지붕 위에 올려놓고 다시 튀어 오른다 (건물이 부서지면 그대로 떨어진다)
                    var p = transform.position;
                    p.y += top - feet;
                    transform.position = p;
                    velocity.y = BounceSpeed * Mathf.Sqrt((float)stats.SizeScale);
                    jumpTarget = null;
                    continue;
                }

                // 뛰어오를 목표 건물로 올라가는 중에는 옆면에 걸리지 않는다 (발이 지붕에 닿기 전에 몸이 먼저 겹친다)
                if (b == jumpTarget && velocity.y > 0) continue;

                // 옆에서 부딪히기: 가까운 옆으로 밀어내고, 들이받은 뒤 반동으로 물러난다
                float side = box.center.x < b.CenterX ? -1f : 1f;
                Move(side < 0 ? b.Left - box.xMax : b.Right - box.xMin);
                if (CooldownReady(b, SideHitCooldown))
                {
                    bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
                    double amount = Damage.Compute(stats.AttackPower, stats.BodySlamMultiplier, critical, stats.CritMultiplier);
                    b.TakeDamage(amount, critical);
                    HitBuilding?.Invoke(b, amount, critical);
                    Animator.Kick();
                    velocity.x = side * (float)stats.MoveSpeed * 0.9f;
                }
                else if (velocity.x * side < 0) velocity.x = 0;
                jumpTarget = null;
            }
        }

        bool CooldownReady(Building b, float seconds)
        {
            if (hitCooldown.TryGetValue(b, out float until) && Time.time < until) return false;
            hitCooldown[b] = Time.time + seconds;
            return true;
        }

        Building NearestAlive()
        {
            Building best = null;
            float bestD = float.MaxValue, cx = Hitbox.center.x;
            foreach (var b in buildings)
            {
                if (!b.IsAlive) continue;
                float d = Mathf.Abs(b.CenterX - cx);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        /// <summary>체공 중 몸통이 공중 유닛과 겹치면 몸통 박치기 1회(100%). 같은 유닛은 점프 1회에 한 번만 (8.6)</summary>
        void StrikeAirUnits()
        {
            if (airTargets == null) return;
            var box = Hitbox;
            foreach (var unit in airTargets)
            {
                if (!unit.IsAlive || hitThisJump.Contains(unit) || !unit.Hitbox.Overlaps(box)) continue;
                hitThisJump.Add(unit);
                bool critical = Damage.RollCritical(stats.CritChance, random.NextDouble());
                double amount = Damage.Compute(stats.AttackPower, 1.0, critical, stats.CritMultiplier);
                unit.TakeDamage(amount, critical);
                HitAirUnit?.Invoke(unit, amount, critical);
            }
        }

        void Move(float dx) => transform.position += new Vector3(dx, 0, 0);

        /// <summary>스킬(돌진)이 드래곤을 옮긴다. 앞 건물을 뚫고 지나가지 않게 호출하는 쪽이 거리를 정한다</summary>
        public void Shift(float dx) => Move(dx);

        /// <summary>바라보는 방향으로 다음 건물까지의 틈. 건물이 없으면 float.MaxValue</summary>
        public float GapAhead()
        {
            var box = Hitbox;
            var blocker = NearestBlocker(facing, box);
            if (blocker == null) return float.MaxValue;
            return facing > 0 ? blocker.Left - box.xMax : box.xMin - blocker.Right;
        }

        bool TryGetNearestBuildingX(out float x)
        {
            x = 0;
            float best = float.MaxValue;
            float cx = Hitbox.center.x;
            foreach (var b in buildings)
            {
                if (!b.IsAlive) continue;
                float d = Mathf.Abs(b.CenterX - cx);
                if (d < best) { best = d; x = b.CenterX; }
            }
            return best < float.MaxValue;
        }

        /// <summary>몸과 가로로 겹친 살아 있는 건물 (겹침이 가장 큰 것)</summary>
        Building Overlapping(Rect box)
        {
            Building best = null;
            float bestOverlap = 0.01f;
            foreach (var b in buildings)
            {
                if (!b.IsAlive) continue;
                float overlap = Mathf.Min(box.xMax, b.Right) - Mathf.Max(box.xMin, b.Left);
                if (overlap > bestOverlap) { bestOverlap = overlap; best = b; }
            }
            return best;
        }

        Building NearestBlocker(float direction, Rect box)
        {
            Building best = null;
            float bestGap = float.MaxValue;
            foreach (var b in buildings)
            {
                if (!b.IsAlive) continue;
                float gap = direction > 0 ? b.Left - box.xMax : box.xMin - b.Right;
                // 이미 겹친 건물(gap이 음수)도 가장 가까운 것으로 본다
                if (gap < -box.width) continue;
                if (gap < bestGap) { bestGap = gap; best = b; }
            }
            return best;
        }
    }
}
