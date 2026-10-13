using System.Collections.Generic;
using DWH.InGame.Dragon;
using UnityEngine;

namespace DWH.InGame.Fx
{
    /// <summary>
    /// 마우스 유도 표시 (8.5.1, 웹 프로토타입 v0.3 참고). 커서가 어디를 가리키는지만 보여 준다(건물을 목표로 고르지 않는다).
    /// - 조준점: 커서 위치에 깜빡이는 원과 십자 눈금.
    /// - 궤적: 드래곤 입에서 커서까지 흐르는 점선 곡선.
    /// 드래곤은 커서 쪽으로 고개를 돌리고, 파이어 브레스도 그쪽으로 뿜는다(DragonAnimator.Aim).
    /// 유도 중(커서를 움직이고 있을 때)에만 보이고, 자동 이동이나 스타터·종료 연출 중에는 숨긴다.
    /// </summary>
    public class AimGuide : MonoBehaviour
    {
        const int Dots = 14;
        const float DotSize = 0.2f;
        const float FlowSpeed = 1.4f;
        const int Order = SortingOrders.Projectile + 5;
        static readonly Color Cream = new(1f, 0.95f, 0.8f, 0.9f);

        DragonController dragon;
        IGuideInput input;
        Transform root;
        SpriteRenderer ring, ringShadow;
        readonly List<SpriteRenderer> ticks = new();
        readonly List<SpriteRenderer> tickShadows = new();
        readonly List<SpriteRenderer> dots = new();
        readonly List<SpriteRenderer> dotShadows = new();
        static readonly Color Shadow = new(0.15f, 0.1f, 0.08f, 0.55f);
        float time;
        Sprite reticle;

        public bool Visible { get; private set; }
        public Vector2 Cursor { get; private set; }
        public Vector2 AimPoint { get; private set; }

        public void Init(DragonController owner, IGuideInput guideInput)
        {
            dragon = owner;
            input = guideInput;
            root = new GameObject("AimGuide").transform;
            root.SetParent(transform, false);

            // 밝은 하늘에서도 보이게 어두운 그림자를 한 겹 아래에 깐다
            ringShadow = Sprite("RingShadow", FxSprites.Ring, Order - 1);
            ring = Sprite("Ring", FxSprites.Ring);
            for (int i = 0; i < 4; i++) tickShadows.Add(Sprite($"TickShadow{i}", GrayboxSprites.Square, Order - 1));
            for (int i = 0; i < 4; i++) ticks.Add(Sprite($"Tick{i}", GrayboxSprites.Square));
            for (int i = 0; i < Dots; i++) dotShadows.Add(Sprite($"DotShadow{i}", FxSprites.Soft, Order - 1));
            for (int i = 0; i < Dots; i++) dots.Add(Sprite($"Dot{i}", FxSprites.Soft));

            // UI 패키지의 조준점 아이콘(target-reticle)이 있으면 원과 눈금 대신 쓴다
            reticle = DWH.UI.UiTheme.KitSprite("target-reticle");
            if (reticle != null)
            {
                ring.sprite = reticle;
                ringShadow.sprite = reticle;
                foreach (var t in ticks) t.gameObject.SetActive(false);
                foreach (var t in tickShadows) t.gameObject.SetActive(false);
            }
            SetVisible(false);
        }

        SpriteRenderer Sprite(string name, Sprite sprite, int order = Order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            root.gameObject.SetActive(visible);
            if (!visible && dragon != null && dragon.Animator != null) dragon.Animator.Aim = null;
        }

        void LateUpdate()
        {
            // 일시정지 중에는 조준선과 드래곤 시선을 그대로 둔다
            if (dragon == null || Time.timeScale <= 0) return;
            Vector2 cursor = default;
            bool show = dragon.Controllable && dragon.IsAlive && dragon.IsGuided && input.TryGetCursor(out cursor);
            if (!show)
            {
                if (Visible) SetVisible(false);
                return;
            }
            if (!Visible) SetVisible(true);
            time += Time.unscaledDeltaTime;
            Cursor = cursor;

            // 커서 지점 (지면 아래로는 내려가지 않는다)
            AimPoint = new Vector2(cursor.x, Mathf.Max(cursor.y, dragon.Hitbox.yMin + 0.15f));
            dragon.Animator.Aim = AimPoint;

            DrawCrosshair(cursor);
            DrawTrajectory(dragon.Animator.Mouth, AimPoint, Cream);
        }

        void DrawCrosshair(Vector2 at)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 8f);
            float r = 0.42f + 0.1f * pulse;
            ring.transform.position = at;
            ringShadow.transform.position = at;
            if (reticle != null)
            {
                // 아이콘 원본 크기(unit) 대비로 맞춘다. 원래 색(금색) 그대로, 그림자는 조금 크게
                float native = reticle.bounds.size.x;
                ring.transform.localScale = Vector3.one * (r * 2.4f / native);
                ring.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * pulse);
                ringShadow.transform.localScale = Vector3.one * ((r * 2.4f + 0.1f) / native);
                ringShadow.color = Shadow;
                return;
            }
            ring.transform.localScale = Vector3.one * r * 2f;
            ring.color = new Color(1f, 0.95f, 0.8f, 0.7f + 0.3f * pulse);
            ringShadow.transform.localScale = Vector3.one * (r * 2f + 0.12f);
            ringShadow.color = Shadow;
            var dirs = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
            for (int i = 0; i < 4; i++)
            {
                var t = ticks[i];
                t.transform.position = at + dirs[i] * (r + 0.2f);
                t.transform.localScale = i < 2 ? new Vector3(0.3f, 0.08f, 1) : new Vector3(0.08f, 0.3f, 1);
                t.color = ring.color;
                var sh = tickShadows[i];
                sh.transform.position = t.transform.position;
                sh.transform.localScale = t.transform.localScale + new Vector3(0.06f, 0.06f, 0);
                sh.color = Shadow;
            }
        }

        /// <summary>입에서 조준 지점까지 위로 살짝 휘는 곡선. 점들이 조준 지점 쪽으로 흘러간다</summary>
        void DrawTrajectory(Vector2 from, Vector2 to, Color color)
        {
            float dist = Vector2.Distance(from, to);
            var control = (from + to) / 2f + Vector2.up * Mathf.Min(1.2f, dist * 0.2f);
            float flow = time * FlowSpeed % 1f;
            for (int i = 0; i < Dots; i++)
            {
                float t = (i + flow) / Dots;
                var p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * to;
                var dot = dots[i];
                dot.transform.position = p;
                dot.transform.localScale = Vector3.one * DotSize * Mathf.Lerp(0.7f, 1.15f, t);
                // 양 끝은 흐리게
                float edge = Mathf.Clamp01(Mathf.Min(t, 1 - t) * 6f);
                dot.color = new Color(color.r, color.g, color.b, edge);
                var sh = dotShadows[i];
                sh.transform.position = p;
                sh.transform.localScale = dot.transform.localScale * 1.45f;
                sh.color = new Color(Shadow.r, Shadow.g, Shadow.b, Shadow.a * edge);
            }
        }

    }
}
