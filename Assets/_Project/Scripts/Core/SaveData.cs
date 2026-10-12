using System;
using System.Collections.Generic;

namespace DWH.Core
{
    /// <summary>
    /// 저장 데이터 (가이드 6.12). PlayerPrefs에 JSON 문자열로 저장한다. JsonUtility로 직렬화하므로 Dictionary 대신 키·값 목록을 쓴다.
    /// 키는 테이블 ID다(노드 upgrade_id, 특성 trait_id, 액티브 skill_id, 스테이지 stage_id).
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>2 = 데이터 테이블 Ver02(문자열 ID). 이전 버전 저장은 ID가 달라 새로 시작한다</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;

        // 재화
        public double goldCoin;
        public double rebirthPoint;

        // 업그레이드 (환생하면 노드만 초기화)
        public List<LevelEntry> nodeLevels = new();
        public List<LevelEntry> traitLevels = new();
        public List<LevelEntry> activeSkillLevels = new();

        // 진행
        public string currentStage = "";
        /// <summary>지금까지 도달한 가장 먼 스테이지 (환생해도 유지)</summary>
        public string highestStage = "";
        public List<string> clearedStages = new();
        /// <summary>마지막 스테이지를 클리어해 엔딩을 봤다</summary>
        public bool gameCompleted;

        // 누적
        /// <summary>이번 회차 누적 획득 골드. 환생 포인트 기준</summary>
        public double runEarnedGold;
        /// <summary>이번 회차 업그레이드 누적 사용 골드. 성장 단계 기준 (환생하면 초기화)</summary>
        public double runSpentGold;
        /// <summary>평생 업그레이드 누적 사용 골드 (기록용)</summary>
        public double lifetimeSpentGold;
        public int rebirthCount;
        /// <summary>정산까지 마친 판 수. 첫 판 안내·"바로 다시 출격" 비활성에 쓴다</summary>
        public int playCount;

        // 설정·지표
        public string language = "ko";
        /// <summary>지표용 사용자 ID (게임이 만든 UUID)</summary>
        public string userId = "";
        public SaveSettings settings = new();

        public int GetNodeLevel(string id) => Find(nodeLevels, id)?.level ?? 0;
        public void SetNodeLevel(string id, int level) => Set(nodeLevels, id, level);
        public int GetTraitLevel(string id) => Find(traitLevels, id)?.level ?? 0;
        public void SetTraitLevel(string id, int level) => Set(traitLevels, id, level);
        public int GetActiveLevel(string id) => Find(activeSkillLevels, id)?.level ?? 0;
        public void SetActiveLevel(string id, int level) => Set(activeSkillLevels, id, level);

        static void Set(List<LevelEntry> list, string key, int level)
        {
            var entry = Find(list, key);
            if (entry == null) list.Add(new LevelEntry { key = key, level = level });
            else entry.level = level;
        }

        static LevelEntry Find(List<LevelEntry> list, string key) => list.Find(e => e.key == key);
    }

    [Serializable]
    public class LevelEntry
    {
        public string key;
        public int level;
    }

    [Serializable]
    public class SaveSettings
    {
        public float masterVolume = 1f;
    }
}
