using System.Linq;
using DWH.Data;

namespace DWH.Core
{
    /// <summary>
    /// 드래곤 성장 단계 (dragon_growth, 가이드 6.7·6.11). 이번 회차 업그레이드 누적 사용 골드가 req_gold 이상이면 그 단계다.
    /// 외형(파츠 세트)만 바뀌고 능력치 보정은 없다. 환생하면 누적값이 초기화되어 해츨링부터 다시 시작한다.
    /// </summary>
    public static class Growth
    {
        public static DragonGrowthRow Stage(GameDatabase db, double spentGold) =>
            db.Growths.Last(g => g.reqGold <= spentGold);

        /// <summary>다음 단계. 마지막 단계면 null</summary>
        public static DragonGrowthRow Next(GameDatabase db, double spentGold) =>
            db.Growths.FirstOrDefault(g => g.reqGold > spentGold);
    }
}
