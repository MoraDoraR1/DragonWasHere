using System;
using DWH.Data;
using DWH.InGame.Combat;
using UnityEngine;

namespace DWH.InGame.Buildings
{
    /// <summary>파괴 대상 건물. 충돌은 물리 엔진 없이 가로 구간(Left~Right)으로 판정한다 (4.2 AABB).</summary>
    public class Building : MonoBehaviour, ICombatTarget
    {
        static readonly Color FlashColor = new(1f, 0.55f, 0.55f);

        SpriteRenderer spriteRenderer;
        float flashDuration;
        float flashTimer;

        public BuildingType Type { get; private set; }
        /// <summary>감별안으로 황금화된 건물. 체력·공격은 원래대로, 골드와 드랍은 황금 기준 (4.8)</summary>
        public bool IsGilded { get; private set; }
        /// <summary>골드·드랍 계산에 쓰는 유형. 황금화되면 Golden</summary>
        public BuildingType RewardType => IsGilded ? BuildingType.Golden : Type;
        public int Order { get; private set; }
        public double MaxHp { get; private set; }
        public double Hp { get; private set; }
        public double Gold { get; private set; }
        public Sprite Sprite => spriteRenderer.sprite;
        public bool IsAlive => Hp > 0;

        /// <summary>충돌 박스 (월드 좌표)</summary>
        public Rect Hitbox { get; private set; }
        public float Left => Hitbox.xMin;
        public float Right => Hitbox.xMax;
        public float CenterX => Hitbox.center.x;

        public event Action<Building> Destroyed;
        public event Action<Building, double, bool> Damaged;

        public void Init(BuildingType type, int order, double hp, double gold, Sprite sprite, int sortingOrder,
            Rect contentLocal, float colliderScaleX, float colliderScaleY, float flashSeconds, bool gilded = false)
        {
            Type = type;
            IsGilded = gilded;
            Order = order;
            MaxHp = Hp = hp;
            Gold = gold;
            flashDuration = flashSeconds;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;

            // 충돌 박스: 그림 경계의 가로 85%(가운데 기준) × 세로 90%(바닥 기준) (9.3)
            var pos = (Vector2)transform.position;
            float width = contentLocal.width * colliderScaleX;
            float height = contentLocal.height * colliderScaleY;
            Hitbox = new Rect(pos.x + contentLocal.center.x - width / 2f, pos.y + contentLocal.yMin, width, height);
        }

        public void TakeDamage(double amount, bool critical)
        {
            if (!IsAlive || amount <= 0)
                return;
            Hp = Math.Max(0, Hp - amount);
            flashTimer = flashDuration;
            spriteRenderer.color = FlashColor;
            Damaged?.Invoke(this, amount, critical);

            if (!IsAlive)
            {
                Destroyed?.Invoke(this);
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (flashTimer <= 0)
                return;
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0)
                spriteRenderer.color = Color.white;
        }
    }
}
