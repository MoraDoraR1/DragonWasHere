using DWH.Data;
using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.Buildings
{
    /// <summary>
    /// 공격 건물과 랜드마크의 공격 (3.4, 8.5.2). 사거리 안에 드래곤이 있으면 공격 속도마다 드래곤 쪽으로 수평 탄을 쏜다.
    /// 탄 높이는 드래곤 충돌 박스 높이 대비 비율이다. 공격 건물은 1발, 랜드마크는 아래·중간·위 3발.
    /// 높이 1 미만은 지면 탄(점프로 회피), 1 이상은 서 있는 드래곤 머리 위로 지나간다.
    /// </summary>
    public class BuildingTurret : MonoBehaviour
    {
        Building building;
        DragonController dragon;
        Camera view;
        Sprite projectileSprite;
        GameDatabase db;
        ArtCatalog art;
        float range, interval, speed, size;
        double damage;
        float[] shotHeights;
        float cooldown;

        public bool Enabled { get; set; } = true;
        float stunTimer;

        /// <summary>충격파 기절 (4.3). 기절하는 동안 쏘지 않는다</summary>
        public void Stun(float seconds) => stunTimer = Mathf.Max(stunTimer, seconds);
        public bool IsStunned => stunTimer > 0;

        public void Init(Building owner, DragonController target, Camera camera, Sprite bulletSprite,
            double attackDamage, float attacksPerSecond, float attackRange, float projectileSpeed, float projectileSize, float[] heightRatios,
            GameDatabase database = null, ArtCatalog catalog = null)
        {
            db = database;
            art = catalog;
            building = owner;
            dragon = target;
            view = camera;
            projectileSprite = bulletSprite;
            damage = attackDamage;
            interval = 1f / attacksPerSecond;
            range = attackRange;
            speed = projectileSpeed;
            size = projectileSize;
            shotHeights = heightRatios;
        }

        void Update()
        {
            if (!Enabled || !building.IsAlive || dragon == null || !dragon.IsAlive)
                return;

            if (stunTimer > 0)
            {
                stunTimer -= Time.deltaTime;
                return;
            }
            cooldown -= Time.deltaTime;
            float gap = Mathf.Max(building.Left - dragon.Hitbox.xMax, dragon.Hitbox.xMin - building.Right, 0f);
            if (gap > range || cooldown > 0)
                return;

            cooldown = interval;
            var box = dragon.Hitbox;
            float side = box.center.x < building.CenterX ? -1f : 1f;
            float x = side < 0 ? building.Left : building.Right;
            foreach (float ratio in shotHeights)
                Fire(new Vector2(x, box.yMin + box.height * ratio), new Vector2(side, 0), groundShot: ratio < 1f);
        }

        void Fire(Vector2 origin, Vector2 direction, bool groundShot)
        {
            // 전투 오브젝트 아래에 두어 전투를 정리할 때 날아가던 탄도 함께 지운다
            var go = ProjectileVisual.Create(building.transform.parent, db, art, ProjectileVisual.BuildingConst, origin,
                projectileSprite, GrayboxColors.Projectile, size);
            var shot = go.AddComponent<Projectile>();
            shot.Shooter = building != null && building.Type == BuildingType.Landmark ? "landmark" : "building";
            shot.Launch(direction, speed, damage, groundShot, dragon, view);
            ProjectileVisual.Orient(go, direction);
        }
    }
}
