using DWH.Data;
using UnityEngine;

namespace DWH.InGame.Fx
{
    /// <summary>
    /// 프레임 이펙트 재생 (resource type 80: frame_count장을 frame_rate로, loop면 반복). 반복 이펙트는 lifetime이 지나면 서서히 사라진다.
    /// 전투 오브젝트 아래에 만들어 전투를 정리할 때 함께 지워진다.
    /// </summary>
    public class SpriteAnim : MonoBehaviour
    {
        Sprite[] frames;
        float fps, age, lifetime;
        bool loop;
        SpriteRenderer sr;
        Color baseColor = Color.white;

        public SpriteRenderer Renderer => sr;

        /// <param name="lifetime">반복 이펙트를 유지할 시간(초). 반복이 아니면 마지막 프레임에서 끝난다</param>
        public static SpriteAnim Play(Transform parent, GameDatabase db, ArtCatalog art, string resId, Vector2 position,
            float scale = 1f, int sortingOrder = SortingOrders.Projectile - 1, float lifetime = 2f, Color? tint = null)
        {
            if (art == null || !art.Has(resId)) return null;
            var res = db.Resource(resId);
            var go = new GameObject($"Fx_{resId}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var anim = go.AddComponent<SpriteAnim>();
            anim.sr = go.AddComponent<SpriteRenderer>();
            anim.sr.sortingOrder = sortingOrder;
            anim.frames = art.Frames(resId);
            anim.fps = res.frameRate > 0 ? (float)res.frameRate : 24f;
            anim.loop = res.loop;
            anim.lifetime = lifetime;
            anim.baseColor = tint ?? Color.white;
            anim.sr.color = anim.baseColor;
            anim.sr.sprite = anim.frames[0];
            return anim;
        }

        /// <summary>반복이 아닌 이펙트도 지정 시간 동안 반복한다 (날아가는 불덩이, 지속 스킬)</summary>
        public void LoopFor(float seconds)
        {
            loop = true;
            lifetime = seconds;
        }

        /// <summary>한 번 재생하는 데 걸리는 시간</summary>
        public float Duration => frames.Length / fps;

        void Update()
        {
            age += Time.deltaTime;
            int frame = Mathf.FloorToInt(age * fps);
            if (!loop && frame >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = frames[frame % frames.Length];
            if (loop)
            {
                float fade = Mathf.Clamp01((lifetime - age) / 0.4f);
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);
                if (age >= lifetime) Destroy(gameObject);
            }
        }
    }

    /// <summary>피격·스킬 이펙트 리소스 ID (resource type 80)</summary>
    public static class FxIds
    {
        public const string Meteor = "res8000001";
        public const string Hurricane = "res8000002";
        public const string Earthquake = "res8000003";
        public const string FireBall = "res8000004";
        public const string FireBreath = "res8000005";
        public const string LightningGold = "res8000006";
        public const string LightningBlue = "res8000007";
        public const string HitStrong = "res8000010";
        public const string BuildingFire = "res8000011";
        public const string GroundDust = "res8000012";
        public const string MeteorExplosion = "res8000013";
        public const string HitNormal = "res8000014";
        public const string Smoke = "res8000015";
        public const string Landing = "res8000016";
        public const string FireBallExplosion = "res8000017";
    }
}
