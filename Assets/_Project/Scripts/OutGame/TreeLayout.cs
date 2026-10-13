using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using UnityEngine;

namespace DWH.OutGame
{
    /// <summary>
    /// 업그레이드 트리 화면 배치 (격자 단위, y는 위로 증가). 테이블에 좌표가 없어서 갈래(category)와 선행 관계로 계산한다.
    /// 공통(용의 각성)이 가운데, 체력·기력은 위, 골드는 왼쪽, 캐릭터 스탯은 오른쪽, 패시브 스킬은 아래로 뻗는다.
    /// 깊이 = 선행 노드 깊이 + 1. 같은 깊이 노드는 선행 노드 위치 순서로 나란히 둔다.
    /// </summary>
    public static class TreeLayout
    {
        public static Dictionary<string, Vector2> Positions(GameDatabase db)
        {
            var depth = new Dictionary<string, int>();
            int Depth(UpgradeRow u)
            {
                if (depth.TryGetValue(u.upgradeId, out int d)) return d;
                var pre = u.Prerequisites();
                d = pre.Length == 0 ? 0 : 1 + pre.Max(p => Depth(db.Upgrade(p)));
                return depth[u.upgradeId] = d;
            }
            foreach (var u in db.upgrades) Depth(u);

            var result = new Dictionary<string, Vector2>();
            foreach (var root in db.upgrades.Where(u => depth[u.upgradeId] == 0))
                result[root.upgradeId] = Vector2.zero;

            foreach (var group in db.upgrades.Where(u => depth[u.upgradeId] > 0).GroupBy(u => u.category))
            {
                // 깊이 순서대로 배치한다. 같은 깊이 안에서는 선행 노드의 가로 위치(갈래 기준) 평균 순
                var offsets = new Dictionary<string, float>();
                foreach (var row in group.GroupBy(u => depth[u.upgradeId]).OrderBy(g => g.Key))
                {
                    var ordered = row.OrderBy(u => u.Prerequisites().Select(p => offsets.TryGetValue(p, out float o) ? o : 0f).DefaultIfEmpty(0f).Average())
                        .ThenBy(u => u.upgradeId, System.StringComparer.Ordinal).ToList();
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        float offset = i - (ordered.Count - 1) / 2f;
                        offsets[ordered[i].upgradeId] = offset;
                        result[ordered[i].upgradeId] = Place(group.Key, row.Key, offset);
                    }
                }
            }
            return result;
        }

        static Vector2 Place(UpgradeCategory category, int depth, float offset) => category switch
        {
            UpgradeCategory.Vitality => new Vector2(offset * 1.15f, depth * 1.2f),
            // 위아래로 붙은 노드는 위 노드 이름(배지 아래)이 아래 노드를 가리지 않게 1.2칸 이상 띄운다 (DWH-51)
            // 가로로 붙은 노드는 넓어진 이름표(최대 186px)가 겹치지 않게 1.15칸 이상 띄운다
            UpgradeCategory.Gold => new Vector2(-depth * 1.15f - 0.3f, offset * 1.2f),
            UpgradeCategory.Stats => new Vector2(depth * 1.15f + 0.3f, offset * 1.2f),
            _ => new Vector2(offset * 1.15f, -depth * 1.25f - 0.15f),
        };
    }
}
