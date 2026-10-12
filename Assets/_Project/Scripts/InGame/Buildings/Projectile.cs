using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.Buildings
{
    /// <summary>
    /// 건물이 쏘는 직선 탄 (8.5.2). 건물은 통과하고 드래곤에 맞으면 사라진다. 카메라 밖으로 나가면 제거한다.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        Vector2 velocity;
        double damage;
        DragonController target;
        Camera view;

        /// <summary>지면 높이로 날아가는 탄. 점프 중인 드래곤은 맞지 않는다</summary>
        public bool IsGroundShot { get; private set; }

        public void Launch(Vector2 direction, float speed, double damageAmount, bool groundShot, DragonController dragon, Camera camera)
        {
            velocity = direction.normalized * speed;
            damage = damageAmount;
            IsGroundShot = groundShot;
            target = dragon;
            view = camera;
            transform.right = direction;
        }

        void Update()
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);

            // 점프 중에는 지면 탄을 피한다 (8.6)
            bool dodged = target != null && target.IsAirborne && IsGroundShot;
            if (target != null && target.IsAlive && !dodged && target.Hitbox.Contains(transform.position))
            {
                target.TakeHit(damage, this);
                Destroy(gameObject);
                return;
            }

            if (view != null)
            {
                var vp = view.WorldToViewportPoint(transform.position);
                if (vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f)
                    Destroy(gameObject);
            }
        }
    }
}
