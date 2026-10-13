using System;
using DWH.Data;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.AirUnits
{
    /// <summary>
    /// 공중 유닛 (4.5, 8.5.2). 화면 오른쪽 밖에서 날아든 뒤 드래곤 머리 위를 배회한다:
    /// 드래곤 기준 ±Tuning.AirUnitHoverRange 안에서 다음 자리를 골라(1.2~2.8초마다, 고도도 조금씩) 가감속하며 날아가고,
    /// 맵 밖으로는 나가지 않고, 드래곤과 부딪히지 않게 드래곤(서 있을 때) 머리 위 상공에서만 난다. 멈춰 있을 때는 드래곤 쪽을 본다.
    /// 드래곤이 공격 범위(가로 거리 Tuning.AirUnitAttackRange) 안에 있고 자기가 화면 안에 있을 때만
    /// 공격 속도(atk_speed)대로 드래곤의 현재 위치를 조준한 대각선 탄을 쏜다. 클리어 조건에는 들어가지 않는다.
    /// </summary>
    public class AirUnit : MonoBehaviour, ICombatTarget
    {
        static readonly Color FlashColor = new(1f, 0.6f, 0.6f);
        const float BobAmplitude = 0.2f;
        const float BobSpeed = 2.2f;
        const float FlashSeconds = 0.1f;
        /// <summary>가감속 (이동 속도 × 이 값 /초)</summary>
        const float Acceleration = 1.6f;
        /// <summary>목표 자리 이 거리 안에서 감속한다</summary>
        const float ArriveDistance = 1.5f;
        /// <summary>다음 자리를 고르는 간격(초)과 고도 흔들림 폭</summary>
        const float WanderMinSeconds = 1.2f, WanderMaxSeconds = 2.8f, WanderAltitude = 0.5f;

        SpriteRenderer spriteRenderer;
        DragonController dragon;
        Camera view;
        Sprite bulletSprite;
        float altitude, groundY, flySpeed, interval, shotSpeed, shotSize, cooldown, flashTimer, bobPhase;
        float patrolLeft, patrolRight, wanderOffsetX, wanderAltitude, wanderTimer;
        Vector2 velocity, basePosition;
        Vector2 size;

        /// <summary>unit 테이블 행 (체력, 공격, 공격 속도, 이동 속도, 드랍 그룹)</summary>
        public UnitRow Row { get; private set; }
        public int Slot => Row.slot;
        /// <summary>현재 언어의 유닛 이름</summary>
        public string UnitName { get; private set; }
        public double MaxHp { get; private set; }
        public double Hp { get; private set; }
        public double AttackDamage { get; private set; }
        public bool IsAlive => Hp > 0;
        public bool Enabled { get; set; } = true;
        public Rect Hitbox => new((Vector2)transform.position - size / 2, size);

        public event Action<AirUnit> Destroyed;
        public event Action<AirUnit, double, bool> Damaged;

        /// <param name="phase">유닛마다 일렁임이 겹치지 않게 하는 값</param>
        /// <param name="flightAltitude">지면 위 비행 고도(unit)</param>
        /// <param name="left">왕복할 맵 왼쪽 끝(월드 x)</param>
        /// <param name="right">왕복할 맵 오른쪽 끝(월드 x)</param>
        public void Init(UnitRow row, string unitName, Sprite sprite, DragonController target, Camera camera,
            float phase, float flightAltitude, float ground, float left, float right, Sprite projectile, int sortingOrder)
        {
            Row = row;
            UnitName = unitName;
            MaxHp = Hp = row.hp;
            AttackDamage = row.atk;
            float speed = Tuning.Px(row.moveSpeed);
            float attacksPerSecond = (float)row.atkSpeed;
            float projectileSpeed = Tuning.AirUnitProjectileSpeed;
            float projectileSize = Tuning.ProjectileSize;
            dragon = target;
            view = camera;
            altitude = flightAltitude;
            groundY = ground;
            flySpeed = speed;
            interval = 1f / attacksPerSecond;
            cooldown = interval;
            shotSpeed = projectileSpeed;
            shotSize = projectileSize;
            bulletSprite = projectile;
            bobPhase = phase * 7f;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            // 크기 (DWH-60: 작아 보인다 → ×1.75). 충돌 박스는 그림 경계의 80% (투명 여백 제외 근사)
            transform.localScale = Vector3.one * Tuning.AirUnitScale;
            size = (sprite != null ? (Vector2)sprite.bounds.size * 0.8f : Vector2.one) * Tuning.AirUnitScale;

            // 화면 오른쪽 밖에서 시작해 왼쪽으로 날아 들어온다
            float margin = size.x / 2;
            patrolLeft = left + margin;
            patrolRight = Mathf.Max(patrolLeft, right - margin);
            var (_, viewRight) = ViewRange();
            basePosition = new Vector2(viewRight + size.x, groundY + altitude);
            transform.position = basePosition;
            velocity = new Vector2(-flySpeed, 0);
            PickWanderSpot();
        }

        /// <summary>
        /// 날 수 있는 가장 낮은 중심 높이: 드래곤이 서 있을 때 머리(지면 + 몸 높이) + 여유 + 자기 몸 절반.
        /// 드래곤이 성장해 커지면 같이 올라간다. 점프 중 높이는 쓰지 않는다(점프 몸통 박치기 8.6은 그대로 가능)
        /// </summary>
        float MinFlightY()
        {
            float dragonHeight = dragon != null ? dragon.Hitbox.height : 0f;
            return groundY + dragonHeight + Tuning.AirUnitHeadClearance + size.y / 2f;
        }

        /// <summary>드래곤 머리 위 근처에서 다음에 갈 자리를 고른다</summary>
        void PickWanderSpot()
        {
            wanderOffsetX = UnityEngine.Random.Range(-Tuning.AirUnitHoverRange, Tuning.AirUnitHoverRange);
            wanderAltitude = UnityEngine.Random.Range(-WanderAltitude, WanderAltitude);
            wanderTimer = UnityEngine.Random.Range(WanderMinSeconds, WanderMaxSeconds);
        }

        (float left, float right) ViewRange()
        {
            float half = view.orthographicSize * view.aspect;
            float x = view.transform.position.x;
            return (x - half, x + half);
        }

        public void TakeDamage(double amount, bool critical)
        {
            if (!IsAlive || amount <= 0) return;
            Hp = Math.Max(0, Hp - amount);
            flashTimer = FlashSeconds;
            spriteRenderer.color = FlashColor;
            Damaged?.Invoke(this, amount, critical);
            if (!IsAlive)
            {
                Destroyed?.Invoke(this);
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (flashTimer > 0 && (flashTimer -= dt) <= 0)
                spriteRenderer.color = Color.white;

            // 배회: 드래곤 머리 위 근처의 자리로 가감속하며 날아가고, 도착하거나 시간이 되면 다음 자리를 고른다
            float dragonX = dragon != null ? dragon.Hitbox.center.x : basePosition.x;
            float floor = MinFlightY();
            var spot = new Vector2(Mathf.Clamp(dragonX + wanderOffsetX, patrolLeft, patrolRight),
                Mathf.Max(groundY + altitude + wanderAltitude, floor + Mathf.Abs(wanderAltitude)));
            var toSpot = spot - basePosition;
            float distance = toSpot.magnitude;
            if ((wanderTimer -= dt) <= 0 || distance < 0.3f) PickWanderSpot();
            var desired = distance > 0.001f ? toSpot / distance * flySpeed * Mathf.Clamp01(distance / ArriveDistance) : Vector2.zero;
            velocity = Vector2.MoveTowards(velocity, desired, flySpeed * Acceleration * dt);
            basePosition += velocity * dt;
            basePosition.x = Mathf.Clamp(basePosition.x, patrolLeft, Mathf.Max(patrolRight, basePosition.x));
            // 날아가는 중에도 드래곤 머리 높이 밑으로는 내려가지 않는다 (일렁임 폭까지 포함)
            basePosition.y = Mathf.Max(basePosition.y, floor + BobAmplitude);
            var pos = basePosition + new Vector2(0, Mathf.Sin(Time.time * BobSpeed + bobPhase) * BobAmplitude);
            transform.position = pos;
            // 그림은 왼쪽을 본다. 날아가는 쪽, 거의 멈춰 있으면 드래곤 쪽을 본다
            spriteRenderer.flipX = Mathf.Abs(velocity.x) > flySpeed * 0.25f ? velocity.x > 0 : dragonX > pos.x;

            if (!Enabled || dragon == null || !dragon.IsAlive) return;
            // 공격 속도는 그대로 두고, 범위 밖에서는 쏘지 않는다(쿨다운이 찼으면 범위에 들어오자마자 쏜다)
            if (cooldown > 0) cooldown -= dt;
            if (cooldown > 0 || !InAttackRange(pos)) return;
            cooldown = interval;
            Fire(pos);
        }

        /// <summary>드래곤과의 가로 거리가 공격 범위 안이고, 자기가 화면 안에 보일 때</summary>
        public bool InAttackRange(Vector2 pos)
        {
            var (left, right) = ViewRange();
            bool onScreen = pos.x - size.x / 2 <= right && pos.x + size.x / 2 >= left;
            return onScreen && Mathf.Abs(dragon.Hitbox.center.x - pos.x) <= Tuning.AirUnitAttackRange;
        }

        void Fire(Vector2 origin)
        {
            var go = new GameObject("AirProjectile");
            go.transform.SetParent(transform.parent, false);
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bulletSprite;
            sr.color = GrayboxColors.AirProjectile;
            sr.sortingOrder = SortingOrders.Projectile;
            go.transform.localScale = Vector3.one * shotSize;
            // 드래곤의 현재 위치를 조준한 대각선 탄. 지면 탄이 아니라 점프로 피할 수 없다(패링은 된다)
            var aim = dragon.Hitbox.center - origin;
            go.AddComponent<Projectile>().Launch(aim, shotSpeed, AttackDamage, false, dragon, view);
        }
    }
}
