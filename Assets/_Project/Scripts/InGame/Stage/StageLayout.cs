using System;
using DWH.Core;
using DWH.Data;

namespace DWH.InGame.Stage
{
    /// <summary>스테이지의 건물 순서를 정한다 (3.4 레벨 구축).</summary>
    public static class StageLayout
    {
        /// <summary>
        /// 왼쪽부터의 건물 유형 순서.
        /// - 랜드마크는 전체의 가운데(0부터 센 floor(N/2)번째)에 둔다.
        /// - 공격 건물 i번(0부터)은 1부터 센 round((i+1) × N / (공격수+1))번째 칸에 둔다. [임시]
        ///   그 칸이 이미 차 있으면 오른쪽의 가장 가까운 빈칸, 없으면 왼쪽 빈칸에 둔다.
        /// - 나머지는 일반 건물이다.
        /// </summary>
        public static BuildingType[] BuildOrder(int normalCount, int attackCount, int landmarkCount)
        {
            if (normalCount < 0 || attackCount < 0 || landmarkCount is < 0 or > 1)
                throw new ArgumentOutOfRangeException();

            int total = normalCount + attackCount + landmarkCount;
            var order = new BuildingType?[total];

            if (landmarkCount == 1)
                order[total / 2] = BuildingType.Landmark;

            for (int i = 0; i < attackCount; i++)
            {
                int slot = (int)Formulas.Round((i + 1) * (double)total / (attackCount + 1)) - 1;
                order[FreeSlotNear(order, Math.Clamp(slot, 0, total - 1))] = BuildingType.Attack;
            }

            var result = new BuildingType[total];
            for (int i = 0; i < total; i++)
                result[i] = order[i] ?? BuildingType.Normal;
            return result;
        }

        static int FreeSlotNear(BuildingType?[] order, int slot)
        {
            for (int i = slot; i < order.Length; i++)
                if (order[i] == null) return i;
            for (int i = slot - 1; i >= 0; i--)
                if (order[i] == null) return i;
            throw new InvalidOperationException("빈칸이 없습니다");
        }
    }
}
