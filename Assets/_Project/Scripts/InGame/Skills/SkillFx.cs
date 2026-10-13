using System;
using System.Collections.Generic;
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
            var fx = SpriteAnim.Play(parent, db, art, FxIds.Hurricane, center, 1f, Order, lifetime);
            if (fx != null)
            {
                // 그림 높이가 기둥 높이에 맞게 키운다 (10-12 리소스 교체로 프레임이 540×540 → 400×340이 되어 실제 크기로 계산)
                float drawn = fx.Renderer.sprite != null ? fx.Renderer.sprite.bounds.size.y : 2.7f;
                fx.transform.localScale = Vector3.one * (size.y / Mathf.Max(0.01f, drawn));
                fx.LoopFor(lifetime);
                return fx.gameObject;
            }
            return Quad(parent, "FxHurricane", center, size, new Color(0.7f, 0.9f, 1f, 0.35f), lifetime);
        }

        /// <summary>파이어볼 크기 (QA: 0.35 → 0.6 → 10-12 사용자 요청 ×1.5 = 0.9)</summary>
        public const float FireBallScale = 0.9f;
        /// <summary>지진 균열 한 장 크기와 간격. 그림의 균열은 프레임 폭의 약 2/3이라 간격을 그보다 좁혀 끊김 없이 잇는다</summary>
        const float QuakeScale = 1.6f, QuakeSpacing = 2.3f;

        /// <summary>
        /// 지진: 지면 위에 균열을 끊김 없이 한 줄로 깔고 지속 시간 동안 반복 재생한다 (DWH-57).
        /// 돌려받은 조각들을 호출하는 쪽이 카메라를 따라 옮긴다. 균열 그림은 프레임 세로 가운데에 있어 지면 높이에 맞춘다
        /// </summary>
        public static List<SpriteAnim> QuakeStrip(Transform parent, GameDatabase db, ArtCatalog art, float left, float right, float groundY, float duration)
        {
            var pieces = new List<SpriteAnim>();
            int count = Mathf.CeilToInt((right - left) / QuakeSpacing) + 2;
            for (int i = 0; i < count; i++)
            {
                var fx = SpriteAnim.Play(parent, db, art, FxIds.Earthquake, new Vector2(left + i * QuakeSpacing, groundY), QuakeScale, Order, duration);
                if (fx == null) break;
                fx.LoopFor(duration);
                pieces.Add(fx);
            }
            if (pieces.Count == 0)
                Quad(parent, "FxQuake", new Vector2((left + right) / 2, groundY), new Vector2(right - left, 0.5f), new Color(0.6f, 0.45f, 0.3f, 0.5f), duration);
            return pieces;
        }

        /// <summary>지진 균열 조각을 화면 왼쪽부터 다시 깐다 (카메라가 움직여도 화면 전체를 덮는다)</summary>
        public static void LayQuakeStrip(List<SpriteAnim> pieces, float left, float groundY)
        {
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null) pieces[i].transform.position = new Vector3(left + i * QuakeSpacing, groundY, 0);
        }

        /// <summary>파이어 브레스 불길 그림 (res8000005). 앞쪽 끝이 입에 오게 호출하는 쪽이 매 프레임 놓는다</summary>
        public static SpriteAnim Breath(Transform parent, GameDatabase db, ArtCatalog art, float lifetime)
        {
            var fx = SpriteAnim.Play(parent, db, art, FxIds.FireBreath, Vector2.zero, 1f, Order, lifetime);
            if (fx == null) return null;
            fx.LoopFor(lifetime);
            // 0~3 커지고, 4~8 유지, 9~11 줄어든다 → 커진 뒤에는 4~8만 반복
            fx.LoopRange(4, 8);
            return fx;
        }

        /// <summary>파이어 볼: 대상을 따라 휘어 날아가는 불덩이</summary>
        public static void Homing(Transform parent, GameDatabase db, ArtCatalog art, Vector2 origin, ICombatTarget target, float speed,
            Action<ICombatTarget> onHit, Func<ICombatTarget> retarget)
        {
            // 불덩이 크기 (DWH-55: 잘 안 보인다 → 0.6, 이후 ×1.5)
            var fx = SpriteAnim.Play(parent, db, art, FxIds.FireBall, origin, FireBallScale, Order, 4f);
            var go = fx != null ? fx.gameObject : Quad(parent, "FxFireBall", origin, new Vector2(FireBallScale, FireBallScale), new Color(1f, 0.5f, 0.15f), 4f);
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
