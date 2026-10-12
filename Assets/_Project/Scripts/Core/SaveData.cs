using System;
using System.Collections.Generic;

namespace DWH.Core
{
    /// <summary>
    /// 저장 데이터 (4.11). JsonUtility로 직렬화하므로 Dictionary 대신 키·값 목록을 쓴다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // 보유 재화
        public double goldCoin;
        public double rebirthPoint;

        // 업그레이드
        public List<LevelEntry> nodeLevels = new();
        public List<LevelEntry> activeSkillLevels = new();
        /// <summary>환생 특성 레벨 (영구, 환생해도 유지)</summary>
        public List<LevelEntry> traitLevels = new();

        // 진행
        public string currentStage = "";
        public string highestStage = "";
        public List<string> clearedStages = new();

        // 누적
        public double lifetimeSpentGold;
        public double runEarnedGold;
        public int rebirthCount;
        /// <summary>정산까지 마친 판 수. 첫 판 조작 안내에 쓴다</summary>
        public int playCount;

        // 설정
        public SaveSettings settings = new();

        // 도감 (4.10, MVP 제외. 필드만 둔다)
        public List<string> unlockedSkins = new();
        public List<string> achievements = new();

        public int GetNodeLevel(string node) => Find(nodeLevels, node)?.level ?? 0;

        public void SetNodeLevel(string node, int level)
        {
            var entry = Find(nodeLevels, node);
            if (entry == null) nodeLevels.Add(new LevelEntry { key = node, level = level });
            else entry.level = level;
        }

        public int GetTraitLevel(string trait) => Find(traitLevels, trait)?.level ?? 0;
        public void SetTraitLevel(string trait, int level) => Set(traitLevels, trait, level);
        public int GetActiveLevel(string skill) => Find(activeSkillLevels, skill)?.level ?? 0;
        public void SetActiveLevel(string skill, int level) => Set(activeSkillLevels, skill, level);

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
