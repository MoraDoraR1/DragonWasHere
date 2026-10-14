using System;
using System.Collections.Generic;
using System.Linq;
using DWH.Data;
using UnityEngine;

namespace DWH.Core
{
    /// <summary>저장소. WebGL 빌드에서 깨지지 않게 PlayerPrefs(JSON 문자열)에 저장한다 (가이드 2장, 6.12)</summary>
    public interface ISaveStorage
    {
        string Read();
        void Write(string json);
        void Delete();
    }

    public class PlayerPrefsSaveStorage : ISaveStorage
    {
        readonly string key;

        public PlayerPrefsSaveStorage(string key = "DWH.Save") => this.key = key;

        public string Read() => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        public void Write(string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public void Delete() => PlayerPrefs.DeleteKey(key);
    }

    /// <summary>
    /// 웹 빌드 저장소: 브라우저 localStorage(고정 키). PlayerPrefs는 웹에서 페이지 주소별로 저장돼 itch.io에 새 빌드를 올리면
    /// (업로드마다 주소가 바뀜) 진행이 사라졌다. localStorage는 도메인 단위라 빌드가 바뀌어도 이어진다 (10-13 사용자 요청).
    /// - 읽기: localStorage → 없으면 예전 PlayerPrefs 저장을 읽어 localStorage로 옮긴다.
    /// - 쓰기: localStorage와 PlayerPrefs 둘 다 (localStorage가 막힌 브라우저 대비).
    /// </summary>
    public class WebLocalSaveStorage : ISaveStorage
    {
        /// <summary>localStorage 키. itch.io는 여러 게임이 같은 도메인(html-classic.itch.zone)을 써서 게임 이름을 넣는다</summary>
        public const string LocalKey = "DragonWasHere.DWH.Save";
        readonly PlayerPrefsSaveStorage fallback = new();

        public string Read()
        {
            string json = LocalGet(LocalKey);
            if (!string.IsNullOrEmpty(json)) return json;
            json = fallback.Read();
            if (!string.IsNullOrEmpty(json)) LocalSet(LocalKey, json);
            return json;
        }

        public void Write(string json)
        {
            // localStorage가 막혔으면(사파리 개인정보 모드 등) 지표 client_error로 알린다. PlayerPrefs에는 그대로 저장한다
            if (!LocalSet(LocalKey, json)) Analytics.ClientError(DWH.Analytics.AnalyticsValues.ErrorSaveFailed, "localStorage");
            fallback.Write(json);
        }

        public void Delete()
        {
            LocalRemove(LocalKey);
            fallback.Delete();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern string DwhStorage_Get(string key);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int DwhStorage_Set(string key, string value);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void DwhStorage_Remove(string key);
        static string LocalGet(string key) => DwhStorage_Get(key);
        static bool LocalSet(string key, string value) => DwhStorage_Set(key, value) != 0;
        static void LocalRemove(string key) => DwhStorage_Remove(key);
#else
        static string LocalGet(string key) => null;
        static bool LocalSet(string key, string value) => true;
        static void LocalRemove(string key) { }
#endif
    }

    /// <summary>테스트용 메모리 저장소</summary>
    public class MemorySaveStorage : ISaveStorage
    {
        public string Json { get; private set; }
        public string Read() => Json;
        public void Write(string json) => Json = json;
        public void Delete() => Json = null;
    }

    public static class SaveSystem
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static ISaveStorage DefaultStorage() => new WebLocalSaveStorage();
#else
        public static ISaveStorage DefaultStorage() => new PlayerPrefsSaveStorage();
#endif

        /// <summary>
        /// 저장이 없거나 읽을 수 없으면 새 데이터를 만든다. 첫 스테이지로 시작한다.
        /// 빌드가 바뀌어도 진행을 잇는다: 저장 버전이 달라도 버리지 않고 읽는다. 단, 문자열 ID 이전(Ver01, 숫자 ID) 저장만 새로 시작한다.
        /// 테이블에서 없어진 스테이지·노드는 <see cref="Migrate"/>가 정리한다.
        /// </summary>
        public static SaveData Load(ISaveStorage storage, string firstStage)
        {
            SaveData data = null;
            string json = storage.Read();
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<SaveData>(json); }
                catch (ArgumentException e) { Debug.LogWarning($"[DWH] 저장 데이터를 읽지 못해 새로 시작합니다: {e.Message}"); }
                if (data != null && data.version < SaveData.MinCompatibleVersion)
                {
                    Debug.LogWarning($"[DWH] 저장 버전 {data.version}은 지금 데이터와 ID가 달라 새로 시작합니다");
                    data = null;
                }
            }
            data ??= new SaveData();
            if (string.IsNullOrEmpty(data.currentStage)) data.currentStage = firstStage;
            if (string.IsNullOrEmpty(data.highestStage)) data.highestStage = data.currentStage;
            if (string.IsNullOrEmpty(data.userId)) data.userId = Guid.NewGuid().ToString();
            data.nodeLevels ??= new();
            data.traitLevels ??= new();
            data.activeSkillLevels ??= new();
            data.clearedStages ??= new();
            data.settings ??= new();
            data.tutorialsDone ??= new();
            // 버전 3(튜토리얼) 이전 저장: 이미 해 본 사람에게는 시작 튜토리얼을 다시 띄우지 않는다
            if (data.version < 3 && data.playCount > 0)
            {
                data.MarkTutorial("starter");
                data.MarkTutorial("ingame");
                data.MarkTutorial("settle");
                data.MarkTutorial("upgrade");
                if (data.rebirthCount > 0) data.MarkTutorial("rebirth");
            }
            data.version = SaveData.CurrentVersion;
            return data;
        }

        /// <summary>
        /// 이전 빌드 저장을 지금 데이터 테이블에 맞춘다. 진행은 최대한 살리고 지금 없는 것만 정리한다.
        /// - 없는 스테이지: 현재 → 첫 스테이지, 최고 → 현재. 클리어 목록에서 뺀다.
        /// - 없는 업그레이드·특성·액티브: 뺀다. 최대 레벨이 줄었으면 최대로 맞춘다(쓴 재화는 돌려주지 않는다).
        /// 바뀐 게 있으면 true
        /// </summary>
        public static bool Migrate(SaveData data, GameDatabase db)
        {
            bool changed = false;
            if (db.OrderIndex(data.currentStage) < 0) { data.currentStage = db.StageOrder[0]; changed = true; }
            if (db.OrderIndex(data.highestStage) < db.OrderIndex(data.currentStage)) { data.highestStage = data.currentStage; changed = true; }
            changed |= data.clearedStages.RemoveAll(s => db.OrderIndex(s) < 0) > 0;
            changed |= Clean(data.nodeLevels, id => db.upgrades.FirstOrDefault(u => u.upgradeId == id)?.maxLevel);
            changed |= Clean(data.traitLevels, id => db.rebirthTraits.FirstOrDefault(t => t.traitId == id)?.maxLevel);
            changed |= Clean(data.activeSkillLevels, id => db.activeSkills.FirstOrDefault(a => a.skillId == id)?.maxLevel);
            return changed;
        }

        /// <summary>maxLevel: 없는 ID면 null, 0이면 무제한</summary>
        static bool Clean(List<LevelEntry> list, Func<string, int?> maxLevel)
        {
            bool changed = list.RemoveAll(e => e == null || maxLevel(e.key) == null) > 0;
            foreach (var e in list)
            {
                int max = maxLevel(e.key).Value;
                if (max > 0 && e.level > max) { e.level = max; changed = true; }
                if (e.level < 0) { e.level = 0; changed = true; }
            }
            return changed;
        }

        public static void Save(ISaveStorage storage, SaveData data) => storage.Write(JsonUtility.ToJson(data));
    }
}
