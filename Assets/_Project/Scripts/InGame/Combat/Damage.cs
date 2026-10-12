using UnityEngine;

namespace DWH.InGame.Combat
{
    /// <summary>대미지 = 공격력 × 스킬 배율 × (치명타 시 치명타 배율) (4.2)</summary>
    public static class Damage
    {
        public static double Compute(double attackPower, double skillMultiplier, bool critical, double critMultiplier) =>
            attackPower * skillMultiplier * (critical ? critMultiplier : 1.0);

        /// <summary>roll은 [0, 1) 난수. 테스트에서 값을 넣어 결과를 고정한다.</summary>
        public static bool RollCritical(double critChance, double roll) => roll < critChance;
    }

    /// <summary>대미지를 받는 대상. 건물과 공중 유닛이 구현한다 (5장 아키텍처)</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(double amount, bool critical);
    }

    /// <summary>스킬이 겨눌 수 있는 대상: 충돌 박스(월드 좌표)가 있는 IDamageable</summary>
    public interface ICombatTarget : IDamageable
    {
        Rect Hitbox { get; }
    }
}
