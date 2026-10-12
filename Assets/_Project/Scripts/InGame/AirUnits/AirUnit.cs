using System;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.AirUnits
{
    /// <summary>
    /// 공중 유닛 (4.5, 8.5.2). 화면 오른쪽 밖에서 날아들어 화면 위쪽의 자기 자리(화면 기준 가로 비율, 지면 위 고도)를 지킨다.
    /// 카메라가 움직여도 화면 안에 남고, 처치될 때까지 0.5 aps로 드래곤의 현재 위치를 조준한 대각선 탄을 쏜다.
    /// 클리어 조건에는 들어가지 않는다.
    /// </summary>
    public class AirUnit : MonoBehaviour, ICombatTarget
    {
        static readonly Color FlashColor = new(1f, 0.6f, 0.6f);
        const float BobAmplitude = 0.2f;
        const float BobSpeed = 2.2f;
        const float FlashSeconds = 0.1f;

        SpriteRenderer spriteRenderer;
        DragonController dragon;
        Camera view;
        Sprite bulletSprite;
        float screenFraction, altitude, groundY, flySpeed, interval, shotSpeed, shotSize, cooldown, flashTimer, bobPhase;
        bool arrived;
        Vector2 size;

        public int Slot { get; private set; }
        public string UnitName { get; private set; }
        public double MaxHp { get; private set; }
        public double Hp { get; private set; }
        public double AttackDamage { get; private set; }
        public bool IsAlive => Hp > 0;
        public bool Enabled { get; set; } = true;
        public Rect Hitbox => new((Vector2)transform.position - size / 2, size);

        public event Action<AirUnit> Destroyed;
        public event Action<AirUnit, double, bool> Damaged;

        /// <param name="fraction">화면 왼쪽 끝 0 ~ 오른쪽 끝 1 중 머무를 가로 위치</param>
        /// <param name="flightAltitude">지면 위 비행 고도(unit)</param>
        public void Init(int slot, string unitName, double hp, double damage, Sprite sprite, DragonController target, Camera camera,
            float fraction, float flightAltitude, float ground, float speed, float attacksPerSecond, float projectileSpeed,
            float projectileSize, Sprite projectile, int sortingOrder)
        {
            Slot = slot;
            UnitName = unitName;
            MaxHp = Hp = hp;
            AttackDamage = damage;
            dragon = target;
            view = camera;
            screenFraction = fraction;
            altitude = flightAltitude;
            groundY = ground;
            flySpeed = speed;
            interval = 1f / attacksPerSecond;
            cooldown = interval;
            shotSpeed = projectileSpeed;
            shotSize = projectileSize;
            bulletSprite = projectile;
            bobPhase = fraction * 7f;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            // 충돌 박스: 그림 경계의 80% (투명 여백 제외 근사)
            size = sprite != null ? (Vector2)sprite.bounds.size * 0.8f : Vector2.one;

            // 화면 오른쪽 밖에서 시작한다
            var (_, right) = ViewRange();
            transform.position = new Vector3(right + size.x, groundY + altitude, 0);
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

            var (left, right) = ViewRange();
            var home = new Vector2(Mathf.Lerp(left, right, screenFraction),
                groundY + altitude + Mathf.Sin(Time.time * BobSpeed + bobPhase) * BobAmplitude);
            Vector2 pos = transform.position;
            if (!arrived)
            {
                pos = Vector2.MoveTowards(pos, home, (flySpeed + CameraSpeed()) * dt);
                arrived = (pos - home).sqrMagnitude < 0.01f;
            }
            else
                pos = Vector2.Lerp(pos, home, 1f - Mathf.Exp(-8f * dt));
            transform.position = pos;

            if (!Enabled || dragon == null || !dragon.IsAlive) return;
            bool onScreen = pos.x - size.x / 2 <= right && pos.x + size.x / 2 >= left;
            if (!onScreen) return;
            cooldown -= dt;
            if (cooldown > 0) return;
            cooldown = interval;
            Fire(pos);
        }

        float lastCameraX = float.NaN;

        float CameraSpeed()
        {
            float x = view.transform.position.x;
            float speed = float.IsNaN(lastCameraX) || Time.deltaTime <= 0 ? 0 : Mathf.Abs(x - lastCameraX) / Time.deltaTime;
            lastCameraX = x;
            return speed;
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
