using DWH.Data;

namespace DWH.Core
{
    /// <summary>일반 스테이지가 끝난 이유 (결과 화면 제목)</summary>
    /// <summary>Retired = 일시정지 메뉴 "세션 끝내기"로 직접 끝냄 (10-14)</summary>
    public enum SessionEnd { Cleared, Defeated, Exhausted, Retired }

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
        /// <summary>보물 산더미: 클리어 시 더해 준 골드 (earnedGold에 포함)</summary>
        public double clearBonusGold;
        public int maxCombo;
        /// <summary>일반 스테이지가 끝난 순간의 체력·기력 (지표 run_end)</summary>
        public double hpLeft;
        public double staminaLeft;
        public float playSeconds;
        /// <summary>이 판으로 마지막 스테이지를 클리어했다 (엔딩)</summary>
        public bool gameCompleted;
    }

    /// <summary>
    /// 판 사이 진행 규칙 (가이드 1장 고정 규칙, 6.4, 6.5).
    /// - "출발"은 항상 현재 도전 스테이지다. 클리어하면 다음 스테이지(MVP: mvp_next_stage_id)가 도전 스테이지가 된다.
    /// - 다음 스테이지가 빈칸이면 엔딩이다. 도전 스테이지는 마지막 스테이지에 머문다.
    /// - 실패해도 그때까지 얻은 골드는 지급한다.
    /// </summary>
    public static class Progression
    {
        public static void Settle(GameDatabase db, SaveData save, SessionResult result)
        {
            save.goldCoin += result.earnedGold;
            save.runEarnedGold += result.earnedGold;

            if (!result.cleared)
                return;

            if (!save.clearedStages.Contains(result.stageId))
                save.clearedStages.Add(result.stageId);

            string next = db.NextStageId(db.Stage(result.stageId));
            if (string.IsNullOrEmpty(next))
            {
                result.gameCompleted = !save.gameCompleted;
                save.gameCompleted = true;
            }
            else if (save.currentStage == result.stageId)
                save.currentStage = next;

            if (db.OrderIndex(save.currentStage) > db.OrderIndex(save.highestStage))
                save.highestStage = save.currentStage;
        }
    }
}
