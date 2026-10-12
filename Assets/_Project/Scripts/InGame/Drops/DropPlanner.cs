using System;
using System.Collections.Generic;
using DWH.Data;

namespace DWH.InGame.Drops
{
    /// <summary>드랍 구성 표의 한 행을 실제로 쏟아낼 자원 목록으로 펼친다 (4.6). 비싼 자원부터 나온다.</summary>
    public static class DropPlanner
    {
        static readonly DropResource[] HighToLow =
        {
            DropResource.Crown, DropResource.Diamond, DropResource.Ruby,
            DropResource.Sapphire, DropResource.GoldBar, DropResource.GoldCoin,
        };

        public static List<DropResource> Expand(DropCountsRow row)
        {
            var list = new List<DropResource>(row.TotalCount);
            foreach (var resource in HighToLow)
                for (int i = 0; i < row.Count(resource); i++)
                    list.Add(resource);
            return list;
        }

        public static double TotalGold(IEnumerable<DropResource> drops, Func<DropResource, double> goldValue)
        {
            double sum = 0;
            foreach (var d in drops) sum += goldValue(d);
            return sum;
        }
    }
}
