using System;
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
        public static ISaveStorage DefaultStorage() => new PlayerPrefsSaveStorage();

        /// <summary>저장이 없거나 읽을 수 없거나 옛 버전이면 새 데이터를 만든다. 첫 스테이지로 시작한다.</summary>
        public static SaveData Load(ISaveStorage storage, string firstStage)
        {
            SaveData data = null;
            string json = storage.Read();
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<SaveData>(json); }
                catch (ArgumentException e) { Debug.LogWarning($"[DWH] 저장 데이터를 읽지 못해 새로 시작합니다: {e.Message}"); }
                if (data != null && data.version != SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"[DWH] 저장 버전 {data.version}은 지금 데이터와 맞지 않아 새로 시작합니다");
                    data = null;
                }
            }
            data ??= new SaveData();
            if (string.IsNullOrEmpty(data.currentStage)) data.currentStage = firstStage;
            if (string.IsNullOrEmpty(data.highestStage)) data.highestStage = data.currentStage;
            if (string.IsNullOrEmpty(data.userId)) data.userId = Guid.NewGuid().ToString();
            return data;
        }

        public static void Save(ISaveStorage storage, SaveData data) => storage.Write(JsonUtility.ToJson(data));
    }
}
