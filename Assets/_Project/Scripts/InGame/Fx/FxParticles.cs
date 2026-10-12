using System.Collections.Generic;
using UnityEngine;

namespace DWH.InGame.Fx
{
    /// <summary>
    /// 이펙트용 기본 모양 (9.6: 이펙트 리소스 없음). 파일 없이 코드로 만든다.
    /// 부드러운 원(불꽃·연기·먼지), 고리(충격파). 이펙트 그림이 생기면 이 스프라이트만 바꿔 끼우면 된다.
    /// </summary>
    public static class FxSprites
    {
        const int Size = 64;
        static Sprite soft, ring;

        /// <summary>가운데가 진하고 가장자리로 갈수록 투명해지는 흰 원 (지름 1 unit)</summary>
        public static Sprite Soft => soft ??= Make("FxSoft", (d) => Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f));

        /// <summary>테두리만 진한 흰 고리 (지름 1 unit)</summary>
        public static Sprite Ring => ring ??= Make("FxRing", (d) => Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.2f));

        static Sprite Make(string name, System.Func<float, float> alpha)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size * 2f - 1f, dy = (y + 0.5f) / Size * 2f - 1f;
                    float a = alpha(Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }
    }

    /// <summary>
    /// 가벼운 스프라이트 파티클 (불꽃, 연기, 먼지, 충격파 고리, 회오리 바람). 오브젝트는 풀링해 다시 쓴다.
    /// 시간은 Time.deltaTime이라 일시정지·슬로모션을 따른다.
    /// </summary>
    public class FxParticles : MonoBehaviour
    {
        const int MaxParticles = 500;

        class P
        {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float age, life, size0, size1, gravity, drag, spin;
            public Color c0, c1;
        }

        readonly List<P> live = new();
        readonly Stack<P> pool = new();
        public int ActiveCount => live.Count;
        public int Emitted { get; private set; }

        /// <param name="size0">처음 크기(unit)</param>
        /// <param name="size1">끝 크기(unit)</param>
        /// <param name="c0">처음 색 (알파 포함)</param>
        /// <param name="c1">끝 색. 알파가 0이면 서서히 사라진다</param>
        public void Emit(Sprite sprite, Vector2 position, Vector2 velocity, float life, float size0, float size1, Color c0, Color c1,
            float gravity = 0, float drag = 0, int order = SortingOrders.Projectile - 2)
        {
            if (live.Count >= MaxParticles) return;
            var p = pool.Count > 0 ? pool.Pop() : Create();
            p.sr.gameObject.SetActive(true);
            p.sr.sprite = sprite;
            p.sr.sortingOrder = order;
            p.pos = position;
            p.vel = velocity;
            p.age = 0;
            p.life = Mathf.Max(0.01f, life);
            p.size0 = size0;
            p.size1 = size1;
            p.c0 = c0;
            p.c1 = c1;
            p.gravity = gravity;
            p.drag = drag;
            Apply(p, 0);
            live.Add(p);
            Emitted++;
        }

        // ───── 자주 쓰는 묶음 ─────

        static readonly Color FireHot = new(1f, 0.95f, 0.6f, 0.95f);
        static readonly Color FireEnd = new(0.85f, 0.2f, 0.05f, 0f);
        static readonly Color Dust = new(0.78f, 0.7f, 0.58f, 0.75f);
        static readonly Color DustEnd = new(0.6f, 0.55f, 0.48f, 0f);

        static readonly Color FireCore = new(1f, 1f, 0.85f, 1f);
        static readonly Color FireMid = new(1f, 0.6f, 0.15f, 0.9f);

        /// <summary>입에서 뿜는 불줄기 한 줌. direction 쪽으로 range까지 부채꼴로 퍼지며 날아간다</summary>
        public void Fire(Vector2 mouth, Vector2 direction, float range, float scale, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(-0.28f, 0.28f);
                var d = (Vector2)(Quaternion.Euler(0, 0, a * Mathf.Rad2Deg) * direction);
                float life = Random.Range(0.3f, 0.45f);
                float speed = range / life * Random.Range(0.85f, 1.15f);
                bool core = Random.value < 0.35f;
                // 입 근처는 작고 밝게, 멀리 갈수록 커지며 붉게 사라진다
                Emit(FxSprites.Soft, mouth + Random.insideUnitCircle * 0.05f * scale, d * speed, life,
                    (core ? 0.12f : 0.18f) * scale, Random.Range(0.55f, 0.85f) * scale,
                    core ? FireCore : FireMid, FireEnd, drag: 0.6f);
            }
            // 입에서 번쩍이는 불씨
            Emit(FxSprites.Soft, mouth, Vector2.zero, 0.08f, 0.35f * scale, 0.45f * scale, FireHot, new Color(1f, 0.8f, 0.4f, 0f));
            if (Random.value < 0.25f)
                Emit(FxSprites.Soft, mouth + direction * range * 0.8f, new Vector2(0, 0.6f), 0.9f, 0.4f * scale, 1.1f * scale,
                    new Color(0.3f, 0.27f, 0.25f, 0.35f), new Color(0.3f, 0.27f, 0.25f, 0f), order: SortingOrders.Projectile - 3);
        }

        /// <summary>땅에서 피어오르는 먼지</summary>
        public void DustBurst(Vector2 ground, float width, int count, float scale = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = ground + new Vector2(Random.Range(-width / 2, width / 2), 0.05f);
                var vel = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(0.6f, 2f)) * scale;
                Emit(FxSprites.Soft, pos, vel, Random.Range(0.45f, 0.8f), 0.3f * scale, 0.9f * scale, Dust, DustEnd, gravity: -0.5f, drag: 2.5f);
            }
        }

        /// <summary>바닥을 따라 퍼지는 충격파 고리 (납작한 원)</summary>
        public void ShockRing(Vector2 ground, float radius, Color color, float life = 0.4f)
        {
            // 처음부터 발 둘레만큼 크게 시작해 범위 끝까지 빠르게 퍼진다
            Emit(FxSprites.Ring, ground + new Vector2(0, 0.1f), Vector2.zero, life, radius * 0.7f, radius * 2.1f, color, new Color(color.r, color.g, color.b, 0f));
            live[^1].spin = -1; // 표시: 바닥에 납작하게 깔린 고리
            Apply(live[^1], 0);
        }

        /// <summary>발이 땅을 찍는 순간의 섬광 (납작한 빛)</summary>
        public void GroundFlash(Vector2 ground, float width, float scale)
        {
            Emit(FxSprites.Soft, ground + new Vector2(0, 0.15f), Vector2.zero, 0.18f, width * 0.6f, width * 1.4f,
                new Color(1f, 0.97f, 0.85f, 0.95f), new Color(1f, 0.9f, 0.6f, 0f));
            live[^1].spin = -1;
            Apply(live[^1], 0);
            // 위로 튀는 흙덩이
            for (int i = 0; i < 8; i++)
                Emit(FxSprites.Soft, ground + new Vector2(Random.Range(-width / 2, width / 2), 0.1f),
                    new Vector2(Random.Range(-3f, 3f), Random.Range(4f, 7f)) * scale, 0.5f, 0.22f * scale, 0.12f * scale,
                    new Color(0.55f, 0.42f, 0.3f, 1f), new Color(0.5f, 0.4f, 0.3f, 0f), gravity: 22f);
        }

        /// <summary>들이받을 때 접점에서 튀는 별 조각</summary>
        public void Impact(Vector2 point, float scale)
        {
            for (int i = 0; i < 6; i++)
            {
                var vel = Random.insideUnitCircle.normalized * Random.Range(3f, 6f) * scale;
                Emit(FxSprites.Soft, point, vel, 0.22f, 0.22f * scale, 0.05f, new Color(1f, 0.97f, 0.8f, 1f), new Color(1f, 0.8f, 0.4f, 0f), drag: 6f);
            }
            Emit(FxSprites.Soft, point, Vector2.zero, 0.12f, 0.3f * scale, 1.0f * scale, new Color(1f, 1f, 0.9f, 0.9f), new Color(1f, 1f, 1f, 0f));
        }

        P Create()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            return new P { sr = go.AddComponent<SpriteRenderer>() };
        }

        void Apply(P p, float t)
        {
            float size = Mathf.Lerp(p.size0, p.size1, t);
            var tr = p.sr.transform;
            tr.position = p.pos;
            tr.localScale = p.spin < 0 ? new Vector3(size, size * 0.3f, 1) : new Vector3(size, size, 1);
            p.sr.color = Color.Lerp(p.c0, p.c1, t);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.age += dt;
                float t = p.age / p.life;
                if (t >= 1f)
                {
                    p.sr.gameObject.SetActive(false);
                    p.spin = 0;
                    pool.Push(p);
                    live.RemoveAt(i);
                    continue;
                }
                p.vel.y -= p.gravity * dt;
                p.vel *= Mathf.Max(0, 1f - p.drag * dt);
                p.pos += p.vel * dt;
                Apply(p, t);
            }
        }
    }
}
