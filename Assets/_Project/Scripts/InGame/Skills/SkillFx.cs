using System;
using DWH.Data;
using DWH.InGame.Combat;
using DWH.InGame.Fx;
using UnityEngine;

namespace DWH.InGame.Skills
{
    /// <summary>
    /// 스킬 연출. 이펙트 리소스(resource type 80)가 있으면 프레임 애니메이션을, 없으면(테스트 등) 색 사각형을 쓴다.
    /// 모든 연출은 전투 오브젝트 아래에 만들어 전투를 정리할 때 함께 지워진다.
    /// </summary>
    public static class SkillFx
    {
        const int Order = SortingOrders.Projectile - 1;

        static GameObject Quad(Transform parent, string name, Vector2 center, Vector2 size, Color color, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GrayboxSprites.Square;
            sr.color = color;
            sr.sortingOrder = Order;
            go.AddComponent<FxFade>().Init(lifetime);
            return go;
        }

        /// <summary>낙뢰: 대상 위에 떨어지는 번개 (P3 번개 골드, 256×512 프레임). 아래 끝을 대상 바닥 쪽에 맞춘다</summary>
        public static void Bolt(Transform parent, GameDatabase db, ArtCatalog art, Rect target)
        {
            const float Scale = 1.4f;
            var fx = SpriteAnim.Play(parent, db, art, FxIds.LightningGold, Vector2.zero, Scale, Order);
            if (fx == null)
            {
                Quad(parent, "FxBolt", new Vector2(target.center.x, target.yMin + 4f), new Vector2(0.25f, 8f), new Color(1f, 0.95f, 0.5f), 0.18f);
                return;
            }
            float half = fx.Renderer.sprite.bounds.extents.y * Scale;
            fx.transform.position = new Vector2(target.center.x, target.yMin + target.height * 0.3f + half);
        }

        /// <summary>메테오 본체 (액티브). 호출하는 쪽이 위치를 옮긴다</summary>
        public static GameObject Meteor(Transform parent, GameDatabase db, ArtCatalog art, Vector2 at)
        {
            var fx = SpriteAnim.Play(parent, db, art, FxIds.Meteor, at, 0.9f, Order, 5f);
            if (fx != null) fx.LoopFor(5f);
            return fx != null ? fx.gameObject : Quad(parent, "FxMeteor", at, new Vector2(0.6f, 0.6f), new Color(1f, 0.45f, 0.2f), 2f);
        }

        /// <summary>충돌 폭발</summary>
        public static void Burst(Transform parent, GameDatabase db, ArtCatalog art, string resId, Vector2 at, float size, Color fallback)
        {
            if (SpriteAnim.Play(parent, db, art, resId, at, size / 2.7f * 1.6f, Order) == null)
                Quad(parent, "FxBurst", at, new Vector2(size, size), fallback, 0.3f);
        }

        /// <summary>허리케인 기둥 (액티브). 호출하는 쪽이 위치를 옮긴다</summary>
        public static GameObject Column(Transform parent, GameDatabase db, ArtCatalog art, Vector2 center, Vector2 size, float lifetime)
        {
            var fx = SpriteAnim.Play(parent, db, art, FxIds.Hurricane, center, size.y / 2.7f, Order, lifetime);
            if (fx != null)
            {
                fx.LoopFor(lifetime);
                return fx.gameObject;
            }
            return Quad(parent, "FxHurricane", center, size, new Color(0.7f, 0.9f, 1f, 0.35f), lifetime);
        }

        /// <summary>지진: 화면 폭의 지면 진동</summary>
        public static void Ground(Transform parent, GameDatabase db, ArtCatalog art, float centerX, float width, float groundY)
        {
            int pieces = Mathf.Max(1, Mathf.CeilToInt(width / 4f));
            bool any = false;
            for (int i = 0; i < pieces; i++)
            {
                float x = centerX - width / 2 + (i + 0.5f) * width / pieces;
                any |= SpriteAnim.Play(parent, db, art, FxIds.Earthquake, new Vector2(x, groundY + 0.8f), 1.2f, Order) != null;
            }
            if (!any) Quad(parent, "FxQuake", new Vector2(centerX, groundY), new Vector2(width, 0.5f), new Color(0.6f, 0.45f, 0.3f, 0.5f), 0.35f);
        }

        /// <summary>파이어 볼: 대상을 따라 휘어 날아가는 불덩이</summary>
        public static void Homing(Transform parent, GameDatabase db, ArtCatalog art, Vector2 origin, ICombatTarget target, float speed,
            Action<ICombatTarget> onHit, Func<ICombatTarget> retarget)
        {
            var fx = SpriteAnim.Play(parent, db, art, FxIds.FireBall, origin, 0.35f, Order, 4f);
            var go = fx != null ? fx.gameObject : Quad(parent, "FxFireBall", origin, new Vector2(0.35f, 0.35f), new Color(1f, 0.5f, 0.15f), 4f);
            if (fx != null) fx.LoopFor(4f);
            go.AddComponent<HomingShot>().Launch(target, speed, onHit, retarget);
        }
    }

    /// <summary>수명이 다하면 서서히 사라진다</summary>
    public class FxFade : MonoBehaviour
    {
        float lifetime, age;
        SpriteRenderer sr;
        Color baseColor;

        public void Init(float seconds)
        {
            lifetime = seconds;
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
        }

        void Update()
        {
            age += Time.deltaTime;
            float fadeStart = lifetime * 0.6f;
            if (age > fadeStart)
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(1 - (age - fadeStart) / (lifetime - fadeStart)));
            if (age >= lifetime) Destroy(gameObject);
        }
    }

    /// <summary>유도 투사체. 대상이 먼저 부서지면 다음 대상으로 바꾸고, 없으면 사라진다</summary>
    public class HomingShot : MonoBehaviour
    {
        const float TurnRate = 720f;
        ICombatTarget target;
        float speed;
        Action<ICombatTarget> onHit;
        Func<ICombatTarget> retarget;
        Vector2 direction = Vector2.right;

        public void Launch(ICombatTarget building, float moveSpeed, Action<ICombatTarget> hit, Func<ICombatTarget> nextTarget)
        {
            target = building;
            speed = moveSpeed;
            onHit = hit;
            retarget = nextTarget;
            // 처음엔 살짝 위로 쏘아 올린 뒤 대상 쪽으로 휜다
            direction = new Vector2(Mathf.Sign(building.Hitbox.center.x - transform.position.x), 0.8f).normalized;
        }

        void Update()
        {
            if (target == null || !target.IsAlive)
            {
                target = retarget?.Invoke();
                if (target == null) { Destroy(gameObject); return; }
            }
            Vector2 pos = transform.position;
            Vector2 aim = target.Hitbox.center - pos;
            float angle = Vector2.SignedAngle(direction, aim);
            direction = Quaternion.Euler(0, 0, Mathf.Clamp(angle, -TurnRate * Time.deltaTime, TurnRate * Time.deltaTime)) * direction;
            pos += direction * (speed * Time.deltaTime);
            transform.position = pos;
            transform.right = direction;

            if (target.Hitbox.Contains(pos) || aim.magnitude < 0.2f)
            {
                onHit?.Invoke(target);
                Destroy(gameObject);
            }
        }
    }
}
