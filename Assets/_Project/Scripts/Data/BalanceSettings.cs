using UnityEngine;

namespace DWH.Data
{
    /// <summary>표로 묶이지 않는 단일 수치 모음 (원본: Docs/Data/balance.csv). 각 값의 설명은 CSV에 있다.</summary>
    public class BalanceSettings : ScriptableObject
    {
        public double goldPerHp;
        public double buildingSpacing;
        public double buildingAps;
        public double bonusStaminaThreshold;
        public double bonusStaminaDrainMultiplier;
        public double dropAbsorbDelay;

        public double[] airUnitSpawnTimes;
        public double airUnitAps;
        public double airUnitAltitudeMin;
        public double airUnitAltitudeMax;
        public double airUnitProjectileSpeed;
        public double airUnitFlySpeed;
        public double airUnit1HpMultiplier;
        public double airUnit2HpMultiplier;
        public double airUnit1DamageMultiplier;
        public double airUnit2DamageMultiplier;
        public double airUnit1GoldRatio;
        public double airUnit2GoldRatio;

        public string rebirthUnlockStage;
        public double rebirthPointDivisor;

        public double starterZoneSpeedBase;
        public double starterZoneSpeedPerStage;
        public double starterBarSpeedBase;
        public double starterBarSpeedPerStage;
        public double starterGreatWidthBase;
        public double starterGreatWidthPerStage;
        public double starterGreatWidthMin;
        public double starterGoodWidthRatio;

        public double cameraOrthoSize;
        public double cameraDragonOffsetX;
        public double groundHeight;
        public double parallaxMidFactor;
        public double stageStartMargin;
        public double colliderScaleX;
        public double colliderScaleY;
        public int dragonSortingBase;
        public double mirrorPartTint;
        public double artMarginPx;

        public double attackBuildingRange;
        public double landmarkRange;
        public double projectileSpeed;
        public double attackShotHeight;
        public double[] landmarkShotHeights;
        public double projectileSize;
        public double hitFlashDuration;
        public double endSlowmoDuration;
        public double endSlowmoScale;

        public double dropBurstSpeedMin;
        public double dropBurstSpeedMax;
        public double dropBurstAngle;
        public double dropGravity;
        public double dropAbsorbSpeed;
        public double dropCollectDistance;

        public double chainDestroyWindow;
        public int chainDestroyMaxStacks;

        public double AirUnitHpMultiplier(int slot) => slot == 1 ? airUnit1HpMultiplier : airUnit2HpMultiplier;
        public double AirUnitDamageMultiplier(int slot) => slot == 1 ? airUnit1DamageMultiplier : airUnit2DamageMultiplier;
        public double AirUnitGoldRatio(int slot) => slot == 1 ? airUnit1GoldRatio : airUnit2GoldRatio;
    }
}
