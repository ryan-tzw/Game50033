namespace Game50033
{
    using UnityEngine;

    public readonly struct HitInfo
    {
        public readonly GameObject Attacker;
        public readonly float Damage;
        public readonly Vector3 Direction;
        public readonly float ResonanceDamage;

        public HitInfo(GameObject attacker, float damage, Vector3 direction, float resonanceDamage = 0f)
        {
            Attacker = attacker;
            Damage = damage;
            Direction = direction;
            ResonanceDamage = resonanceDamage;
        }

        internal static HitInfo FromResonatingAttack(GameObject attacker, float damage, Vector3 direction,
            float unboostedDamage, bool isReplay) =>
            new(attacker, damage, direction, isReplay ? damage : Mathf.Max(0f, damage - unboostedDamage));
    }

    /// <summary>
    /// Anything a player attack can hit (dummies, enemies, breakables...).
    /// </summary>
    public interface IHittable
    {
        void TakeHit(in HitInfo hit);
    }
}
