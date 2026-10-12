using System.IO;
using UnityEngine;

namespace DWH.Core
{
    /// <summary>저장소. PC는 파일, WebGL은 PlayerPrefs 문자열에 저장한다 (4.11, 0.1: System.IO는 플랫폼별로 분기)</summary>
    public interface ISaveStorage
    {
        string Read();
        void Write(string json);
        void Delete();
    }

    public class FileSaveStorage : ISaveStorage
    {
        readonly string path;

        public FileSaveStorage(string fileName = "save.json") =>
            path = Path.Combine(Application.persistentDataPath, fileName);

        public string Read() => File.Exists(path) ? File.ReadAllText(path) : null;

        public void Write(string json)
        {
            // 쓰는 도중 종료돼도 기존 저장이 깨지지 않게 임시 파일에 쓰고 바꾼다
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public void Delete()
        {
            if (File.Exists(path)) File.Delete(path);
        }
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
        public static ISaveStorage DefaultStorage() =>
            Application.platform == RuntimePlatform.WebGLPlayer ? new PlayerPrefsSaveStorage() : new FileSaveStorage();

        /// <summary>저장이 없거나 읽을 수 없으면 새 데이터를 만든다. 첫 스테이지로 시작한다.</summary>
        public static SaveData Load(ISaveStorage storage, string firstStage)
        {
            SaveData data = null;
            string json = storage.Read();
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<SaveData>(json); }
                catch (System.ArgumentException e) { Debug.LogWarning($"[DWH] 저장 데이터를 읽지 못해 새로 시작합니다: {e.Message}"); }
            }
            data ??= new SaveData();
            if (string.IsNullOrEmpty(data.currentStage)) data.currentStage = firstStage;
            return data;
        }

        public static void Save(ISaveStorage storage, SaveData data) => storage.Write(JsonUtility.ToJson(data, true));
    }
}
