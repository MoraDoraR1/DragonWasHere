using System.Globalization;
using UnityEngine;

namespace DWH.Analytics
{
    /// <summary>
    /// 지표용 작은 저장소 (사용자 ID·세션 번호·누적 플레이 시간·카운터).
    /// 웹에서 PlayerPrefs는 페이지 주소별로 따로 저장돼 itch.io에 새 빌드를 올리면(업로드마다 주소가 바뀜) 사용자 ID가 새로 생겨
    /// 같은 사람이 새 유저로 잡혔다. 웹 빌드는 브라우저 localStorage("DragonWasHere." + 키)에 두고, 처음 읽을 때 예전 PlayerPrefs 값을 옮긴다.
    /// 에디터·다른 플랫폼은 PlayerPrefs 그대로. localStorage 함수는 Assets/_Project/Plugins/WebGL/DwhStorage.jslib (10-13)
    /// </summary>
    public static class DwhPrefs
    {
        const string LocalPrefix = "DragonWasHere.";

        public static string GetString(string key, string fallback = "") => Read(key) ?? (PlayerPrefs.HasKey(key) ? Migrate(key, PlayerPrefs.GetString(key)) : fallback);

        public static int GetInt(string key, int fallback = 0) =>
            int.TryParse(Read(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v
            : PlayerPrefs.HasKey(key) ? int.Parse(Migrate(key, PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture) : fallback;

        public static float GetFloat(string key, float fallback = 0f) =>
            float.TryParse(Read(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v
            : PlayerPrefs.HasKey(key) ? float.Parse(Migrate(key, PlayerPrefs.GetFloat(key).ToString("R", CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture) : fallback;

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            Write(key, value);
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Write(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            Write(key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            Remove(key);
        }

        public static void Save() => PlayerPrefs.Save();

        static string Migrate(string key, string value)
        {
            Write(key, value);
            return value;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern string DwhStorage_Get(string key);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int DwhStorage_Set(string key, string value);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void DwhStorage_Remove(string key);
        static string Read(string key) => DwhStorage_Get(LocalPrefix + key);
        static void Write(string key, string value) => DwhStorage_Set(LocalPrefix + key, value);
        static void Remove(string key) => DwhStorage_Remove(LocalPrefix + key);
#else
        static string Read(string key) => null;
        static void Write(string key, string value) { }
        static void Remove(string key) { }
#endif
    }
}
