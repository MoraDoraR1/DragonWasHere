using DWH.Data;
using DWH.InGame.Fx;
using UnityEngine;

namespace DWH.InGame.Buildings
{
    /// <summary>
    /// 투사체 그림. 공격 건물·랜드마크와 공중 유닛은 각각 공통 투사체 리소스(const.projectile_res_building / projectile_res_air_unit)를 쓴다.
    /// 그림(16장 반복)은 공이 왼쪽·꼬리가 오른쪽이라 왼쪽으로 날아가는 모양 → 실제 날아가는 방향에 맞춰 뒤집고 돌린다.
    /// 리소스가 없으면(테스트 등) 예전처럼 색 사각형을 쓴다.
    /// </summary>
    public static class ProjectileVisual
    {
        public const string BuildingConst = "projectile_res_building";
        public const string AirUnitConst = "projectile_res_air_unit";
        const float Lifetime = 30f;

        /// <summary>투사체 오브젝트를 만든다. 호출하는 쪽이 Projectile을 붙이고 Orient로 방향을 맞춘다</summary>
        public static GameObject Create(Transform parent, GameDatabase db, ArtCatalog art, string constKey, Vector2 origin,
            Sprite fallbackSprite, Color fallbackColor, float fallbackSize)
        {
            string resId = db != null && db.HasConst(constKey) ? db.ConstRow(constKey).value : null;
            var anim = !string.IsNullOrEmpty(resId) ? SpriteAnim.Play(parent, db, art, resId, origin, Tuning.ProjectileSpriteScale, SortingOrders.Projectile, Lifetime) : null;
            if (anim != null)
            {
                anim.LoopFor(Lifetime);
                anim.name = "Projectile";
                return anim.gameObject;
            }
            var go = new GameObject("Projectile");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fallbackSprite;
            sr.color = fallbackColor;
            sr.sortingOrder = SortingOrders.Projectile;
            go.transform.localScale = Vector3.one * fallbackSize;
            return go;
        }

        /// <summary>그림이 있으면 날아가는 방향으로 뒤집고 돌린다 (그림은 왼쪽을 향함)</summary>
        public static void Orient(GameObject go, Vector2 direction)
        {
            if (go.TryGetComponent(out SpriteAnim anim)) anim.Orient(Vector2.left, direction);
        }
    }
}
