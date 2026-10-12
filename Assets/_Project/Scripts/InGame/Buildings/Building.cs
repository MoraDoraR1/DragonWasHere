using System;
using DWH.Data;
using DWH.InGame.Combat;
using UnityEngine;

namespace DWH.InGame.Buildings
{
    /// <summary>
    /// 파괴 대상 건물 (building 행 1개 = 같은 유형 count개). 충돌은 물리 엔진 없이 가로 구간(Left~Right)으로 판정한다.
    /// 황금 건물(유형 4)과 감별안으로 황금화된 건물은 같은 그림을 SpriteFx 셰이더로 황금색으로 칠한다 (가이드 5장).
    /// 피격하면 흰 플래시와 흔들림을 준다 (부서진 상태 이미지가 없어 대체).
    /// </summary>
    public class Building : MonoBehaviour, ICombatTarget
    {
        static readonly int GoldId = Shader.PropertyToID("_Gold");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        const float ShakeAmplitude = 0.06f;

        SpriteRenderer spriteRenderer;
        MaterialPropertyBlock block;
        Vector3 basePosition;
        float flashTimer;

        public BuildingRow Row { get; private set; }
        public BuildingType Type => Row.buildingType;
        /// <summary>감별안으로 황금화된 건물 (효과 9). 체력·공격은 원래대로, 드랍 골드는 ×3 [임시 해석]</summary>
        public bool IsGilded { get; private set; }
        /// <summary>황금색으로 보이는 건물 (보너스 황금 건물 또는 황금화)</summary>
        public bool LooksGolden => IsGilded || Type == BuildingType.Golden;
        public int Order { get; private set; }
        public double MaxHp { get; private set; }
        public double Hp { get; private set; }
        public Sprite Sprite => spriteRenderer.sprite;
        public bool IsAlive => Hp > 0;

        /// <summary>충돌 박스 (월드 좌표)</summary>
        public Rect Hitbox { get; private set; }
        public float Left => Hitbox.xMin;
        public float Right => Hitbox.xMax;
        public float CenterX => Hitbox.center.x;

        public event Action<Building> Destroyed;
        public event Action<Building, double, bool> Damaged;

        public void Init(BuildingRow row, int order, Sprite sprite, Material fxMaterial, int sortingOrder, Rect contentLocal, bool gilded = false)
        {
            Row = row;
            IsGilded = gilded;
            Order = order;
            MaxHp = Hp = row.hp;
            basePosition = transform.position;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            if (fxMaterial != null) spriteRenderer.sharedMaterial = fxMaterial;
            block = new MaterialPropertyBlock();
            ApplyFx(0);

            // 충돌 박스: 그림 경계의 가로 85%(가운데 기준) × 세로 90%(바닥 기준)
            var pos = (Vector2)transform.position;
            float width = contentLocal.width * Tuning.ColliderScaleX;
            float height = contentLocal.height * Tuning.ColliderScaleY;
            Hitbox = new Rect(pos.x + contentLocal.center.x - width / 2f, pos.y + contentLocal.yMin, width, height);
        }

        void ApplyFx(float flash)
        {
            spriteRenderer.GetPropertyBlock(block);
            block.SetFloat(GoldId, LooksGolden ? 1f : 0f);
            block.SetFloat(FlashId, flash);
            spriteRenderer.SetPropertyBlock(block);
        }

        public void TakeDamage(double amount, bool critical)
        {
            if (!IsAlive || amount <= 0)
                return;
            Hp = Math.Max(0, Hp - amount);
            flashTimer = Tuning.HitFlashSeconds * 2f;
            ApplyFx(0.85f);
            Damaged?.Invoke(this, amount, critical);

            if (!IsAlive)
            {
                transform.position = basePosition;
                Destroyed?.Invoke(this);
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (flashTimer <= 0)
                return;
            flashTimer -= Time.deltaTime;
            // 맞는 동안 좌우로 살짝 흔든다 (충돌 박스는 그대로)
            transform.position = basePosition + new Vector3(Mathf.Sin(Time.time * 90f) * ShakeAmplitude * Mathf.Clamp01(flashTimer * 10f), 0, 0);
            if (flashTimer <= 0)
            {
                transform.position = basePosition;
                ApplyFx(0);
            }
            else
                ApplyFx(Mathf.Clamp01(flashTimer / (Tuning.HitFlashSeconds * 2f)) * 0.85f);
        }
    }
}
