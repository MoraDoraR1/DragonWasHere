using System;
using DWH.Data;

namespace DWH.InGame.Starter
{
    /// <summary>
    /// 스테이지 스타터 게이지 (4.7). 순수 계산이라 EditMode에서 검증한다.
    /// 게이지 폭을 1로 보고, 판정 영역(Good 폭, 가운데에 Great 폭)과 판정 바가 서로 다른 속도로 왕복한다.
    /// 판정 바가 영역 가운데에서 Great 폭/2 안이면 Great, Good 폭/2 안이면 Good, 아니면 Fail.
    /// </summary>
    public class StarterGauge
    {
        public float ZoneSpeed { get; }
        public float BarSpeed { get; }
        public float GreatWidth { get; }
        public float GoodWidth { get; }

        /// <summary>판정 영역(Good 폭) 왼쪽 끝 위치 [0, 1 - Good 폭]</summary>
        public float ZoneStart { get; private set; }
        /// <summary>판정 바 위치 [0, 1]</summary>
        public float Bar { get; private set; }
        public float ZoneCenter => ZoneStart + GoodWidth / 2f;

        float zoneTravel, barTravel;

        public StarterGauge(float zoneSpeed, float barSpeed, float greatWidth, float goodWidth)
        {
            ZoneSpeed = zoneSpeed;
            BarSpeed = barSpeed;
            GreatWidth = greatWidth;
            GoodWidth = Math.Min(goodWidth, 1f);
            // 영역은 가운데, 바는 왼쪽 끝에서 시작한다
            zoneTravel = (1f - GoodWidth) / 2f;
            Advance(0);
        }

        /// <summary>스테이지 순번 n(1..28)의 난이도 (4.7 공식)</summary>
        public static StarterGauge ForStage(BalanceSettings b, int stageIndex)
        {
            int k = Math.Max(0, stageIndex - 1);
            float great = (float)Math.Max(b.starterGreatWidthMin, b.starterGreatWidthBase - b.starterGreatWidthPerStage * k);
            return new StarterGauge(
                (float)(b.starterZoneSpeedBase + b.starterZoneSpeedPerStage * k),
                (float)(b.starterBarSpeedBase + b.starterBarSpeedPerStage * k),
                great, great * (float)b.starterGoodWidthRatio);
        }

        public void Advance(float deltaTime)
        {
            zoneTravel += ZoneSpeed * deltaTime;
            barTravel += BarSpeed * deltaTime;
            ZoneStart = PingPong(zoneTravel, 1f - GoodWidth);
            Bar = PingPong(barTravel, 1f);
        }

        public StarterGrade Judge() => Judge(Bar, ZoneCenter, GreatWidth, GoodWidth);

        public static StarterGrade Judge(float bar, float zoneCenter, float greatWidth, float goodWidth)
        {
            float d = Math.Abs(bar - zoneCenter);
            if (d <= greatWidth / 2f) return StarterGrade.Great;
            if (d <= goodWidth / 2f) return StarterGrade.Good;
            return StarterGrade.Fail;
        }

        static float PingPong(float t, float length)
        {
            if (length <= 0) return 0;
            float m = t % (length * 2f);
            if (m < 0) m += length * 2f;
            return m <= length ? m : length * 2f - m;
        }
    }
}
