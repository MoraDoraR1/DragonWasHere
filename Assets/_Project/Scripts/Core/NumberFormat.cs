using System;
using System.Globalization;

namespace DWH.Core
{
    /// <summary>골드·대미지 숫자를 한국식 단위로 표기한다 (임시unity제작.md 4.2). 예: 781만, 1.2억</summary>
    public static class NumberFormat
    {
        static readonly (double value, string suffix)[] Units =
        {
            (1e16, "경"),
            (1e12, "조"),
            (1e8, "억"),
            (1e4, "만"),
        };

        /// <summary>
        /// 1만 미만은 천 단위 쉼표를 붙인 정수로 쓴다.
        /// 1만 이상은 가장 큰 단위로 나눠, 10 미만이면 소수 첫째 자리까지(버림), 10 이상이면 정수(버림)로 쓴다.
        /// </summary>
        public static string ToKorean(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0";

            string sign = value < 0 ? "-" : "";
            double abs = Math.Abs(value);

            foreach (var (unit, suffix) in Units)
            {
                if (abs < unit)
                    continue;

                double scaled = abs / unit;
                string number = scaled < 10
                    ? (Math.Floor(scaled * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture)
                    : Math.Floor(scaled).ToString("0", CultureInfo.InvariantCulture);
                return sign + number + suffix;
            }

            return sign + Math.Floor(abs).ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}
