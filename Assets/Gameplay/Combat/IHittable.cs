namespace Game50033
{
    using UnityEngine;

    public readonly struct HitInfo
    {
        public readonly GameObject Attacker;
        public readonly float Damage;
        public readonly Vector3 Direction;

        public HitInfo(GameObject attacker, float damage, Vector3 direction)
        {
            Attacker = attacker;
            Damage = damage;
            Direction = direction;
        }
    }

    /// <summary>
    /// Anything a player attack can hit (dummies, enemies, breakables...).
    /// </summary>
    public interface IHittable
    {
        void TakeHit(in HitInfo hit);
    }
}
