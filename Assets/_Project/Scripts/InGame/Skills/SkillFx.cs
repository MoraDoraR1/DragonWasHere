using System;
using DWH.InGame.Combat;
using UnityEngine;

namespace DWH.InGame.Skills
{
    /// <summary>
    /// 패시브 스킬 그레이박스 연출 (9.6: 스킬 이펙트 리소스 없음). 흰 사각형 스프라이트에 색을 입혀 모양만 낸다.
    /// 모든 연출은 전투 오브젝트 아래에 만들어 전투를 정리할 때 함께 지워진다. 오래 남지 않게 수명을 둔다.
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

        /// <summary>돌진 잔상</summary>
        public static void Streak(Transform parent, Rect dragon, float facing, Color color) =>
            Quad(parent, "FxDash", new Vector2(dragon.center.x + facing * dragon.width * 0.6f, dragon.center.y), new Vector2(dragon.width * 1.6f, dragon.height * 0.5f), color, 0.25f);

        /// <summary>낙뢰: 대상 위로 세로 번개</summary>
        public static void Bolt(Transform parent, Rect target, Color color) =>
            Quad(parent, "FxBolt", new Vector2(target.center.x, target.yMin + 4f), new Vector2(0.25f, 8f), color, 0.18f);

        /// <summary>충격파: 지면을 따라 퍼지는 띠</summary>
        public static void Ring(Transform parent, Rect dragon, float radius, Color color) =>
            Quad(parent, "FxShock", new Vector2(dragon.center.x, dragon.yMin + 0.15f), new Vector2(dragon.width + radius * 2, 0.3f), color, 0.35f);

        /// <summary>파이어 브레스 부채꼴(사각형으로 근사). PlaceCone으로 위치를 맞춘다</summary>
        public static GameObject Cone(Transform parent, Color color) => Quad(parent, "FxBreath", Vector2.zero, Vector2.one, color, 4f);

        public static void PlaceCone(GameObject cone, Rect dragon, float facing, float range)
        {
            if (cone == null) return;
            float width = range + dragon.width * 0.3f;
            float x = facing > 0 ? dragon.xMax - dragon.width * 0.3f + width / 2 : dragon.xMin + dragon.width * 0.3f - width / 2;
            cone.transform.position = new Vector3(x, dragon.yMin + dragon.height * 0.55f, 0);
            cone.transform.localScale = new Vector3(width, dragon.height * 0.6f, 1);
        }

        /// <summary>회전 회오리: 드래곤 둘레를 도는 사각형</summary>
        public static GameObject Spinner(Transform dragonTransform, Rect dragon, Color color)
        {
            var go = Quad(dragonTransform, "FxWhirl", dragon.center, new Vector2(dragon.width * 1.3f, dragon.height * 1.1f), color, 5f);
            go.AddComponent<FxSpin>();
            return go;
        }

        /// <summary>메테오 본체 (액티브). 호출하는 쪽이 위치를 옮긴다</summary>
        public static GameObject Meteor(Transform parent, Vector2 at, Color color) => Quad(parent, "FxMeteor", at, new Vector2(0.6f, 0.6f), color, 2f);

        /// <summary>충돌 폭발</summary>
        public static void Burst(Transform parent, Vector2 at, float size, Color color) => Quad(parent, "FxBurst", at, new Vector2(size, size), color, 0.3f);

        /// <summary>허리케인 기둥 (액티브). 호출하는 쪽이 위치를 옮긴다</summary>
        public static GameObject Column(Transform parent, Vector2 center, Vector2 size, Color color, float lifetime) =>
            Quad(parent, "FxHurricane", center, size, color, lifetime);

        /// <summary>지진: 화면 폭의 지면 진동 띠</summary>
        public static void Ground(Transform parent, float centerX, float width, float groundY, Color color) =>
            Quad(parent, "FxQuake", new Vector2(centerX, groundY), new Vector2(width, 0.5f), color, 0.35f);

        /// <summary>파이어 볼: 대상을 따라 휘어 날아가는 불덩이</summary>
        public static void Homing(Transform parent, Vector2 origin, ICombatTarget target, float speed, Color color,
            Action<ICombatTarget> onHit, Func<ICombatTarget> retarget)
        {
            var go = Quad(parent, "FxFireBall", origin, new Vector2(0.35f, 0.35f), color, 4f);
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

    public class FxSpin : MonoBehaviour
    {
        void Update() => transform.Rotate(0, 0, 900f * Time.deltaTime);
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

            if (target.Hitbox.Contains(pos) || aim.magnitude < 0.2f)
            {
                onHit?.Invoke(target);
                Destroy(gameObject);
            }
        }
    }
}
