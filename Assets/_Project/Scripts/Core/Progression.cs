using System.Collections.Generic;

namespace DWH.Core
{
    /// <summary>일반 스테이지가 끝난 이유 (결과 화면 제목)</summary>
    public enum SessionEnd { Cleared, Defeated, Exhausted }

    /// <summary>세션 한 판의 결과. 정산(Progression.Settle)의 입력이다.</summary>
    public class SessionResult
    {
        public string stageId;
        public bool cleared;
        /// <summary>일반 스테이지 종료 사유: 클리어 / 체력 소진 / 기력 소진</summary>
        public SessionEnd end;
        public bool bonusEntered;
        /// <summary>일반 스테이지와 보너스 스테이지를 합친 획득 골드</summary>
        public double earnedGold;
        public int destroyedBuildings;
        public int totalBuildings;
        public double bonusGold;
        public int bonusDestroyed;
        public int bonusTotal;
        /// <summary>보물 더미: 클리어 시 획득 골드에 더해 준 골드 (earnedGold에 포함)</summary>
        public double clearBonusGold;
    }

    /// <summary>
    /// 판 사이 진행 규칙 (1.1, 4.6 결과 정산).
    /// - 클리어하면 다음 스테이지가 도전 스테이지가 된다. 실패하면 그대로다. "출발"은 항상 도전 스테이지로 간다.
    /// - 획득 골드는 클리어와 패배 모두 보유량에 더한다. [임시]
    /// </summary>
    public static class Progression
    {
        public static void Settle(SaveData save, SessionResult result, IReadOnlyList<string> stageOrder)
        {
            save.goldCoin += result.earnedGold;
            save.runEarnedGold += result.earnedGold;

            if (!result.cleared)
                return;

            if (!save.clearedStages.Contains(result.stageId))
                save.clearedStages.Add(result.stageId);

            int index = IndexOf(stageOrder, result.stageId);
            if (index > IndexOf(stageOrder, save.highestStage))
                save.highestStage = result.stageId;

            // 마지막 스테이지를 클리어하면 그 스테이지에 머문다 (이후 환생으로 다시 등반)
            if (index >= 0 && index + 1 < stageOrder.Count && save.currentStage == result.stageId)
                save.currentStage = stageOrder[index + 1];
        }

        static int IndexOf(IReadOnlyList<string> order, string stageId)
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i] == stageId) return i;
            return -1;
        }
    }
}
