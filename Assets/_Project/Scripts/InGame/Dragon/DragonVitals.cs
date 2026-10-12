using System;
using DWH.Core;

namespace DWH.InGame.Dragon
{
    /// <summary>드래곤의 체력·기력·피로 (4.1.1, 8.1). MonoBehaviour와 분리해 테스트한다.</summary>
    public class DragonVitals
    {
        readonly double fatigueStart, fatigueInterval, fatigueStep;

        public double MaxHp { get; }
        public double Hp { get; private set; }
        public double HpRegenPerSecond { get; }
        public double MaxStamina { get; }
        public double Stamina { get; private set; }
        /// <summary>스테이지 경과 시간(초). 피로 계산 기준</summary>
        public double Elapsed { get; private set; }

        public double Fatigue => Formulas.Fatigue(Elapsed, fatigueStart, fatigueInterval, fatigueStep);

        /// <summary>기력 소모 배율. 보너스 스테이지는 2배 (3.5)</summary>
        public double DrainMultiplier { get; set; } = 1;
        /// <summary>true면 체력 회복과 기력 회복(포식 등)이 모두 막힌다. 보너스 스테이지 (3.5)</summary>
        public bool RecoveryBlocked { get; set; }
        /// <summary>지금 초당 기력 감소량 = 피로 × 소모 배율</summary>
        public double StaminaDrainPerSecond => Fatigue * DrainMultiplier;
        public bool IsDead => Hp <= 0;
        public bool IsExhausted => Stamina <= 0;
        public bool IsDown => IsDead || IsExhausted;

        public event Action<double> Damaged;

        public DragonVitals(double maxHp, double hpRegenPerSecond, double maxStamina,
            double fatigueStart, double fatigueInterval, double fatigueStep)
        {
            MaxHp = Hp = maxHp;
            HpRegenPerSecond = hpRegenPerSecond;
            MaxStamina = Stamina = maxStamina;
            this.fatigueStart = fatigueStart;
            this.fatigueInterval = fatigueInterval;
            this.fatigueStep = fatigueStep;
        }

        /// <summary>시간을 흘려 기력을 소모한다. 소모량은 8.1의 닫힌 식으로 구간 차이를 계산해 프레임 길이와 무관하다.</summary>
        public void Tick(double deltaTime)
        {
            if (IsDown || deltaTime <= 0)
                return;
            double before = Formulas.StaminaUsed(Elapsed, fatigueStart, fatigueInterval, fatigueStep);
            Elapsed += deltaTime;
            double after = Formulas.StaminaUsed(Elapsed, fatigueStart, fatigueInterval, fatigueStep);
            Stamina = Math.Max(0, Stamina - (after - before) * DrainMultiplier);
            if (!RecoveryBlocked)
                Hp = Math.Min(MaxHp, Hp + HpRegenPerSecond * deltaTime);
        }

        public void TakeDamage(double amount)
        {
            if (IsDown || amount <= 0)
                return;
            Hp = Math.Max(0, Hp - amount);
            Damaged?.Invoke(amount);
        }

        public void RestoreStamina(double amount)
        {
            if (IsDown || amount <= 0 || RecoveryBlocked)
                return;
            Stamina = Math.Min(MaxStamina, Stamina + amount);
        }
    }
}
