using DWH.Data;
using UnityEngine;

namespace DWH.InGame.Drops
{
    /// <summary>쏟아진 자원 하나. 튀어 올랐다 바닥에 떨어지고, 흡수 대기 시간이 지나면 드래곤에게 날아간다.</summary>
    public class DropPickup : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Vector2 velocity;
        float groundY, gravity, absorbDelay, absorbSpeed, collectDistance, age;

        public DropResource Resource { get; private set; }
        public double Gold { get; private set; }

        public void Launch(DropResource resource, Sprite sprite, double gold, Vector2 origin, Vector2 initialVelocity,
            float ground, float gravityAccel, float delay, float speed, float distance)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = SortingOrders.Drop;
            }
            spriteRenderer.sprite = sprite;
            Resource = resource;
            Gold = gold;
            transform.position = origin;
            velocity = initialVelocity;
            groundY = ground;
            gravity = gravityAccel;
            absorbDelay = delay;
            absorbSpeed = speed;
            collectDistance = distance;
            age = 0;
        }

        /// <summary>한 프레임 움직인다. 드래곤에 닿아 획득하면 true</summary>
        public bool Step(float dt, Vector2 target)
        {
            age += dt;
            Vector2 pos = transform.position;

            if (age < absorbDelay)
            {
                velocity.y -= gravity * dt;
                pos += velocity * dt;
                float floor = groundY + spriteRenderer.bounds.extents.y;
                if (pos.y < floor)
                {
                    pos.y = floor;
                    velocity = Vector2.zero;
                }
            }
            else
            {
                pos = Vector2.MoveTowards(pos, target, absorbSpeed * dt);
            }

            transform.position = pos;
            return age >= absorbDelay && Vector2.Distance(pos, target) <= collectDistance;
        }
    }
}
