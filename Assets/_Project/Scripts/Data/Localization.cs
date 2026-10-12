using System;

namespace DWH.Data
{
    public enum Language { Ko, En }

    /// <summary>현재 언어. 모든 문구는 string 테이블에서 이 언어로 읽는다 (가이드 6.11)</summary>
    public static class Localization
    {
        static Language current = Language.Ko;

        public static event Action<Language> Changed;

        public static Language Current
        {
            get => current;
            set
            {
                if (current == value) return;
                current = value;
                Changed?.Invoke(value);
            }
        }

        public static string Code(Language language) => language == Language.En ? "en" : "ko";

        public static Language Parse(string code) => code == "en" ? Language.En : Language.Ko;
    }
}
