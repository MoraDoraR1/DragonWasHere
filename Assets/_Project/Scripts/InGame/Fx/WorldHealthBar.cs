using System;
using DWH.Data;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Combat;
using DWH.UI;
using UnityEngine;

namespace DWH.InGame.Fx
{
    /// <summary>
    /// 건물·공중 유닛 아래의 체력바 (월드 좌표). 드래곤 체력바(HUD)와 같은 UI 에셋 V15 게이지 그림을 9-slice로 쓴다:
    /// 바탕 gauge-track, 채움 gauge-fill-health(황금 건물은 gauge-fill-goal). 크기는 예전 건물 체력바 그대로 (10-13 사용자 요청).
    /// 일반 건물·공중 유닛은 체력색, 황금 건물(보너스·감별안)은 금색. 맞으면 줄어든 부분이 잠깐 흰색으로 남았다가 따라 줄어든다.
    /// 대상 오브젝트의 자식이라 대상이 파괴되어 꺼지면 같이 사라진다.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        const float Height = 0.16f;
        const float GapBelow = 0.18f;
        const float TrailDelay = 0.35f;
        const float TrailSpeed = 1.5f;
        const int Order = SortingOrders.Building + 5;
        static readonly Color HpColor = new(0.85f, 0.4f, 0.38f);
        static readonly Color GoldColor = new(0.94f, 0.74f, 0.32f);

        ICombatTarget target;
        Func<double> hp;
        double maxHp;
        float width;
        bool below;
        SpriteRenderer back, fill, trail;
        float shown = 1f, trailShown = 1f, trailWait;

        public float Ratio => shown;
        public float TrailRatio => trailShown;

        public static WorldHealthBar Attach(Building building) =>
            Create(building.transform, building, () => building.Hp, building.MaxHp, building.IsGilded || building.Type == BuildingType.Golden);

        public static WorldHealthBar Attach(AirUnit unit) =>
            Create(unit.transform, unit, () => unit.Hp, unit.MaxHp, false);

        static WorldHealthBar Create(Transform parent, ICombatTarget owner, Func<double> currentHp, double max, bool golden)
        {
            var go = new GameObject("HealthBar");
            go.transform.SetParent(parent, false);
            var bar = go.AddComponent<WorldHealthBar>();
            bar.Init(owner, currentHp, max, golden);
            return bar;
        }

        void Init(ICombatTarget owner, Func<double> currentHp, double max, bool golden)
        {
            target = owner;
            hp = currentHp;
            maxHp = Math.Max(1e-9, max);
            width = Mathf.Clamp(owner.Hitbox.width * 0.8f, 0.6f, 3.2f);

            var backSprite = UiTheme.KitSprite("gauge-track");
            var fillSprite = UiTheme.KitSprite(golden ? "gauge-fill-goal" : "gauge-fill-health");
            back = Part("Back", backSprite, backSprite != null ? Color.white : new Color(0.12f, 0.1f, 0.1f, 0.85f), Order);
            trail = Part("Trail", GrayboxSprites.Square, new Color(1f, 0.97f, 0.9f, 0.85f), Order + 1);
            fill = Part("Fill", fillSprite ?? GrayboxSprites.Square, fillSprite != null ? Color.white : golden ? GoldColor : HpColor, Order + 2);
            Place();
            Draw();
        }

        SpriteRenderer Part(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sprite != null && sprite.border != Vector4.zero) sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        /// <summary>대상 충돌 박스 바로 아래 가운데. 건물은 지면 바로 아래 흙 띠, 공중 유닛은 몸 아래</summary>
        void Place()
        {
            var box = target.Hitbox;
            transform.position = new Vector3(box.center.x, box.yMin - GapBelow, 0);
            // 부모(공중 유닛 등)의 크기를 따라가지 않게 한다
            var s = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1);
        }

        void SetBar(SpriteRenderer sr, float ratio, float inset)
        {
            float w = Mathf.Max(0, (width - inset * 2) * ratio);
            float h = Height - inset * 2;
            sr.enabled = ratio > 0.005f;
            float left = -width / 2 + inset;
            if (sr.drawMode == SpriteDrawMode.Sliced)
            {
                // 9-slice 경계가 바 높이보다 두꺼우면 크게 그린 뒤 줄여서 모서리가 겹치지 않게 한다 (HUD의 BorderFit과 같은 방식)
                float k = SliceScaleFor(sr.sprite, h);
                sr.size = new Vector2(Mathf.Max(w, 0.0001f) / k, h / k);
                sr.transform.localScale = new Vector3(k, k, 1);
            }
            else
            {
                var native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
                sr.transform.localScale = new Vector3(w / native.x, h / native.y, 1);
            }
            sr.transform.localPosition = new Vector3(left + w / 2, 0, 0);
        }

        static float SliceScaleFor(Sprite sprite, float height)
        {
            var b = sprite.border;
            float borderUnits = (b.y + b.w) / Mathf.Max(1f, sprite.pixelsPerUnit);
            return borderUnits <= 0 ? 1f : Mathf.Min(1f, height * 0.9f / borderUnits);
        }

        void Draw()
        {
            SetBar(back, 1f, 0);
            SetBar(trail, trailShown, 0.02f);
            SetBar(fill, shown, 0.02f);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            float ratio = Mathf.Clamp01((float)(hp() / maxHp));
            if (ratio < shown)
            {
                shown = ratio;
                trailWait = TrailDelay;
            }
            else shown = ratio;
            if (trailWait > 0) trailWait -= dt;
            else trailShown = Mathf.MoveTowards(trailShown, shown, TrailSpeed * dt);
            if (trailShown < shown) trailShown = shown;

            if (target is AirUnit) Place();
            Draw();
        }
    }
}
