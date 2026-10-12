using UnityEngine;

namespace DWH.InGame.Drops
{
    /// <summary>쏟아진 자원 하나. 튀어 올랐다 바닥에 떨어지고, 흡수 대기 시간이 지나면 드래곤에게 날아간다.</summary>
    public class DropPickup : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Vector2 velocity;
        float groundY, absorbDelay, age;

        /// <summary>drop_item.item_id</summary>
        public string ItemId { get; private set; }
        public double Gold { get; private set; }

        public void Launch(string itemId, Sprite sprite, double gold, Vector2 origin, Vector2 initialVelocity, float ground, float delay)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = SortingOrders.Drop;
            }
            spriteRenderer.sprite = sprite;
            ItemId = itemId;
            Gold = gold;
            transform.position = origin;
            velocity = initialVelocity;
            groundY = ground;
            absorbDelay = delay;
            age = 0;
        }

        /// <summary>한 프레임 움직인다. 드래곤에 닿아 획득하면 true</summary>
        public bool Step(float dt, Vector2 target)
        {
            age += dt;
            Vector2 pos = transform.position;

            if (age < absorbDelay)
            {
                velocity.y -= Data.Tuning.DropGravity * dt;
                pos += velocity * dt;
                float floor = groundY + (spriteRenderer.sprite != null ? spriteRenderer.bounds.extents.y : 0);
                if (pos.y < floor)
                {
                    pos.y = floor;
                    velocity = Vector2.zero;
                }
            }
            else
            {
                pos = Vector2.MoveTowards(pos, target, Data.Tuning.DropAbsorbSpeed * dt);
            }

            transform.position = pos;
            return age >= absorbDelay && Vector2.Distance(pos, target) <= Data.Tuning.DropCollectDistance;
        }
    }
}
