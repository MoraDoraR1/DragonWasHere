using System.Collections.Generic;
using DWH.Data;
using UnityEngine;

namespace DWH.InGame.Dragon
{
    /// <summary>
    /// 드래곤 파츠 조립 좌표 계산 (4.1.5, 9.4).
    /// 이미지 좌표(px, 왼쪽 위 원점, Y 아래로 증가)를 Unity 로컬 좌표(unit, Y 위로 증가)로 바꾼다.
    /// 각 파츠 노드의 원점은 그 파츠의 회전점이고, 몸통 노드의 원점은 몸통 이미지의 왼쪽 위다.
    /// </summary>
    public static class DragonRigLayout
    {
        /// <summary>노드의 로컬 위치: 부모 노드(부모 회전점) 기준으로 부착점까지의 거리</summary>
        public static Vector2 NodeLocalPosition(DragonPartRow part, DragonPartRow parent, float pixelsPerUnit)
        {
            if (parent == null)
                return Vector2.zero;
            var offsetPx = new Vector2((float)(part.attachX - parent.pivotX), (float)(part.attachY - parent.pivotY));
            return ToUnits(offsetPx, pixelsPerUnit);
        }

        /// <summary>
        /// 노드 안에서 스프라이트(가운데 피벗)를 둘 위치. 노드 원점(회전점)이 이미지의 회전점 픽셀에 오게 한다.
        /// </summary>
        public static Vector2 SpriteLocalPosition(DragonPartRow part, Vector2 spriteSizePx, float pixelsPerUnit)
        {
            var centerPx = spriteSizePx / 2f;
            var offsetPx = new Vector2(centerPx.x - (float)part.pivotX, centerPx.y - (float)part.pivotY);
            return ToUnits(offsetPx, pixelsPerUnit);
        }

        /// <summary>
        /// 몸통 이미지 기준 각 파츠 이미지의 왼쪽 위 좌표(px). 9.4의 child.origin = parent.origin + attachPoint - child.pivot.
        /// 조립 검증용이다.
        /// </summary>
        public static Dictionary<string, Vector2> ImageOrigins(IReadOnlyList<DragonPartRow> parts)
        {
            var byName = new Dictionary<string, DragonPartRow>();
            foreach (var p in parts) byName[p.part] = p;

            var origins = new Dictionary<string, Vector2>();
            Vector2 Origin(DragonPartRow p)
            {
                if (origins.TryGetValue(p.part, out var cached)) return cached;
                Vector2 result = string.IsNullOrEmpty(p.parent)
                    ? Vector2.zero
                    : Origin(byName[p.parent]) + new Vector2((float)p.attachX, (float)p.attachY) - new Vector2((float)p.pivotX, (float)p.pivotY);
                origins[p.part] = result;
                return result;
            }
            foreach (var p in parts) Origin(p);
            return origins;
        }

        static Vector2 ToUnits(Vector2 px, float pixelsPerUnit) => new(px.x / pixelsPerUnit, -px.y / pixelsPerUnit);
    }
}
