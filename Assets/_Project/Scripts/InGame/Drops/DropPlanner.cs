using System.Collections.Generic;
using System.Linq;
using DWH.Data;

namespace DWH.InGame.Drops
{
    /// <summary>드랍 그룹(drop_group)의 모든 행을 실제로 쏟아낼 자원 목록으로 펼친다 (가이드 6.4). 비싼 자원부터 나온다.</summary>
    public static class DropPlanner
    {
        /// <param name="random">min~max 사이 개수를 고를 때 쓴다. 지금 데이터는 min = max</param>
        public static List<DropItemRow> Expand(GameDatabase db, string dropGroup, System.Random random)
        {
            var list = new List<DropItemRow>();
            foreach (var row in db.DropGroup(dropGroup).OrderByDescending(r => db.DropItem(r.itemId).tier))
            {
                var item = db.DropItem(row.itemId);
                int count = row.maxCount > row.minCount ? random.Next(row.minCount, row.maxCount + 1) : row.minCount;
                for (int i = 0; i < count; i++) list.Add(item);
            }
            return list;
        }

        public static double TotalGold(IEnumerable<DropItemRow> drops) => drops.Sum(d => d.goldValue);
    }
}
