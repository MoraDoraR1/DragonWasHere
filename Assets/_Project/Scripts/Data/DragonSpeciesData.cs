using UnityEngine;

namespace DWH.Data
{
    /// <summary>
    /// 드래곤 기본 능력치 (4.1.1, 원본: Docs/Data/dragon_base.csv).
    /// 드래곤 종류는 보류 상태라 1종만 쓰지만, 종을 추가할 수 있게 분리해 둔다 (2장).
    /// </summary>
    public class DragonSpeciesData : ScriptableObject
    {
        public string speciesId;
        public double attackPower;
        public double attacksPerSecond;
        public double maxHp;
        public double hpRegenPerSecond;
        public double maxStamina;
        public double fatigueStart;
        public double fatigueInterval;
        public double fatigueStep;
        public double staminaOnDestroy;
        public double staminaOnClear;
        public double moveSpeed;
        public double jumpHeight;
        public double jumpChance;
        public double jumpChanceMax;
        public double jumpCooldown;
        public double critChance;
        public double critMultiplier;
        public double knockbackDuration;
        public double autoMoveIdleSeconds;
        public double knockbackDistance;
        public double contactTolerance;
    }
}
