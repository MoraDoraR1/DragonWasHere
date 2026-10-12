using System.Collections.Generic;
using DWH.Core;
using DWH.InGame.AirUnits;
using DWH.InGame.Buildings;
using DWH.InGame.Stage;
using DWH.UI;
using TMPro;
using UnityEngine;

namespace DWH.InGame.Fx
{
    /// <summary>
    /// 전투 연출 (4.2, 4.12): 대미지 숫자 팝업(한국식 단위, 치명타는 크고 금색), 건물 타격 시 작은 화면 흔들림,
    /// 건물 파괴 시 부스러기와 흔들림. 부서진 건물 스프라이트는 리소스가 없어 생략한다 (9.6).
    /// </summary>
    public class CombatFx : MonoBehaviour
    {
        const int MaxPopups = 40;
        const float PopupSeconds = 0.75f;
        const float PopupRise = 1.2f;
        const int DebrisCount = 7;

        class Popup
        {
            public TextMeshPro text;
            public Vector3 origin;
            public float age;
            public float scale;
        }

        BattleCamera shaker;
        readonly List<Popup> popups = new();
        readonly System.Random random = new(12345);

        /// <summary>떠 있는 팝업 수 (테스트용)</summary>
        public int ActivePopups { get; private set; }
        public int PopupsShown { get; private set; }

        public void Init(Camera camera, IEnumerable<Building> buildings, AirUnitSquad airUnits)
        {
            camera.TryGetComponent(out shaker);
            foreach (var b in buildings)
            {
                b.Damaged += (target, amount, critical) => OnDamaged(target.Hitbox, amount, critical, true);
                b.Destroyed += OnDestroyed;
            }
            if (airUnits != null)
                airUnits.Spawned += unit => unit.Damaged += (target, amount, critical) => OnDamaged(target.Hitbox, amount, critical, false);
        }

        void OnDamaged(Rect box, double amount, bool critical, bool building)
        {
            ShowPopup(new Vector3(box.center.x + (float)(random.NextDouble() - 0.5) * box.width * 0.5f, box.yMax + 0.2f, 0), amount, critical);
            if (building && shaker != null) shaker.Shake(0.06f, critical ? 0.06f : 0.025f);
        }

        void OnDestroyed(Building building)
        {
            var box = building.Hitbox;
            for (int i = 0; i < DebrisCount; i++)
            {
                var go = new GameObject("Debris");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(box.center.x + (float)(random.NextDouble() - 0.5) * box.width, box.yMin + box.height * (float)random.NextDouble(), 0);
                float size = 0.12f + 0.18f * (float)random.NextDouble();
                go.transform.localScale = new Vector3(size, size, 1);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GrayboxSprites.Square;
                float shade = 0.35f + 0.3f * (float)random.NextDouble();
                sr.color = building.IsGilded || building.Type == Data.BuildingType.Golden ? new Color(1f, 0.82f, 0.3f) : new Color(shade + 0.1f, shade, shade - 0.05f);
                sr.sortingOrder = SortingOrders.Drop - 1;
                var v = new Vector2((float)(random.NextDouble() - 0.5) * 6f, 3f + 4f * (float)random.NextDouble());
                go.AddComponent<Debris>().Launch(v, box.yMin);
            }
            if (shaker != null) shaker.Shake(0.15f, 0.08f);
        }

        void ShowPopup(Vector3 position, double amount, bool critical)
        {
            Popup popup = null;
            if (popups.Count >= MaxPopups)
            {
                // 가장 오래된 팝업을 다시 쓴다
                popup = popups[0];
                popups.RemoveAt(0);
            }
            else
            {
                var go = new GameObject("DamagePopup");
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<TextMeshPro>();
                text.font = KoreanFont.Get();
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.outlineWidth = 0.25f;
                text.outlineColor = UiTheme.Ink;
                text.sortingOrder = SortingOrders.Projectile + 10;
                popup = new Popup { text = text };
            }
            popup.text.gameObject.SetActive(true);
            popup.text.text = critical ? NumberFormat.ToKorean(amount) + "!" : NumberFormat.ToKorean(amount);
            popup.text.color = critical ? UiTheme.Gold : Color.white;
            popup.text.fontSize = critical ? 7f : 4.5f;
            popup.text.fontStyle = critical ? FontStyles.Bold : FontStyles.Normal;
            popup.origin = position;
            popup.age = 0;
            popup.scale = critical ? 1.35f : 1f;
            popups.Add(popup);
            PopupsShown++;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            int active = 0;
            foreach (var p in popups)
            {
                if (!p.text.gameObject.activeSelf) continue;
                p.age += dt;
                float t = p.age / PopupSeconds;
                if (t >= 1f)
                {
                    p.text.gameObject.SetActive(false);
                    continue;
                }
                active++;
                p.text.transform.position = p.origin + new Vector3(0, PopupRise * (1f - (1f - t) * (1f - t)), 0);
                // 처음엔 크게 튀었다가 제 크기로, 끝에서 사라진다
                p.text.transform.localScale = Vector3.one * Mathf.Lerp(p.scale * 1.3f, p.scale, Mathf.Clamp01(t * 5f));
                var c = p.text.color;
                c.a = Mathf.Clamp01((1f - t) * 3f);
                p.text.color = c;
            }
            ActivePopups = active;
        }
    }

    /// <summary>튀어 오른 뒤 떨어져 지면에서 사라지는 부스러기</summary>
    public class Debris : MonoBehaviour
    {
        const float Gravity = 18f;
        Vector2 velocity;
        float floor, life = 1.2f;
        float spin;

        public void Launch(Vector2 initialVelocity, float groundY)
        {
            velocity = initialVelocity;
            floor = groundY;
            spin = Random.Range(-720f, 720f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            velocity.y -= Gravity * dt;
            var p = (Vector2)transform.position + velocity * dt;
            if (p.y < floor) { p.y = floor; velocity = new Vector2(velocity.x * 0.5f, -velocity.y * 0.3f); }
            transform.position = p;
            transform.Rotate(0, 0, spin * dt);
            life -= dt;
            if (life <= 0) Destroy(gameObject);
        }
    }
}
