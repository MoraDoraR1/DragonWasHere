using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using UnityEngine;

namespace DWH.InGame.Dragon
{
    /// <summary>
    /// 드래곤 파츠 8종을 11개 레이어로 조립한다 (dragon_part, 가이드 5장). 성장 단계가 바뀌면 그 단계의 파츠 세트로 통째로 다시 만든다.
    /// 노드 이름은 파츠 종류(Body, Neck, UpHead …)이고 반대편은 _B를 붙인다.
    /// 계층: Visual(방향 뒤집기) → Body → 파츠 노드(원점 = 회전점) → 스프라이트.
    /// 조립이 끝나면 그림의 바닥 가운데가 이 오브젝트의 원점에 오도록 Body를 옮긴다.
    /// </summary>
    public class DragonRig : MonoBehaviour
    {
        readonly Dictionary<string, Transform> nodes = new();
        Transform visual;

        /// <summary>오른쪽(원점 기준) 바라보기 상태의 그림 경계. 로컬 좌표</summary>
        public Rect LocalBounds { get; private set; }

        public Transform Node(string part) => nodes[part];
        public bool HasNode(string part) => nodes.ContainsKey(part);

        /// <summary>방향 뒤집기용 루트. DragonAnimator가 몸 전체 기울기·회전·띄우기에 쓴다</summary>
        public Transform Visual => visual;

        /// <summary>파츠의 스프라이트 렌더러 (입 위치 계산 등)</summary>
        public SpriteRenderer PartRenderer(string part) => nodes[part].GetComponentInChildren<SpriteRenderer>();

        public void Build(IReadOnlyList<DragonPartRow> parts, ArtCatalog art)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            var byName = parts.ToDictionary(p => p.partId);
            foreach (var part in parts.OrderBy(p => Depth(p, byName)))
            {
                var sprite = art.Sprite(part.resId);
                float ppu = sprite.pixelsPerUnit;

                var node = new GameObject(part.NodeName).transform;
                var parentRow = part.IsRoot ? null : byName[part.parentPartId];
                node.SetParent(parentRow == null ? visual : nodes[parentRow.NodeName], false);
                node.localPosition = DragonRigLayout.NodeLocalPosition(part, parentRow, ppu);
                nodes[part.NodeName] = node;

                var spriteObject = new GameObject("Sprite");
                spriteObject.transform.SetParent(node, false);
                spriteObject.transform.localPosition = DragonRigLayout.SpriteLocalPosition(part, sprite.rect.size, ppu);
                var sr = spriteObject.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = Tuning.DragonSortingBase - part.layerOrder;
                if (part.isBack)
                    sr.color = new Color(Tuning.BackPartTint, Tuning.BackPartTint, Tuning.BackPartTint);
            }

            // 원본 그림은 왼쪽을 바라본다. 진행 방향(오른쪽)을 보게 뒤집는다 (9.4)
            SetFacing(1);
            var bounds = WorldBounds();
            var body = nodes[parts.First(p => p.IsRoot).NodeName];
            body.position += new Vector3(transform.position.x - bounds.center.x, transform.position.y - bounds.min.y, 0);

            bounds = WorldBounds();
            LocalBounds = new Rect(bounds.min.x - transform.position.x, bounds.min.y - transform.position.y, bounds.size.x, bounds.size.y);
        }

        /// <summary>direction &gt; 0이면 오른쪽, &lt; 0이면 왼쪽을 바라본다</summary>
        public void SetFacing(float direction)
        {
            if (direction == 0) return;
            var s = visual.localScale;
            s.x = direction > 0 ? -1f : 1f;
            visual.localScale = s;
        }

        Bounds WorldBounds()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        static int Depth(DragonPartRow p, Dictionary<string, DragonPartRow> byName) =>
            p.IsRoot ? 0 : 1 + Depth(byName[p.parentPartId], byName);
    }
}
