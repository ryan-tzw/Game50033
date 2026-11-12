namespace Game50033
{
    using System;
    using UnityEngine;

    public readonly struct ResonanceActivation
    {
        public readonly Reverberation Reverberation;
        public readonly ReverberationEffect Effect;
        public readonly Player Creator;
        public readonly Player Resonator;
        public readonly ResonatingAttack Attack;
        public readonly float Strength;
        public readonly Vector3 Position;

        internal ResonanceActivation(Reverberation reverberation, ResonatingAttack attack)
        {
            Reverberation = reverberation;
            Effect = reverberation.Effect;
            Creator = reverberation.Creator;
            Resonator = attack.SourcePlayer;
            Attack = attack;
            Strength = reverberation.Strength;
            Position = reverberation.Volume.transform.TransformPoint(reverberation.Volume.center);
        }
    }

    public static class ResonanceResolver
    {
        public static event Action<ResonanceActivation> Activated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            Activated = null;
        }

        public static bool TryActivate(Reverberation reverberation, ResonatingAttack attack)
        {
            if (reverberation == null || attack == null)
            {
                return false;
            }

            if (!reverberation.IsAvailable || !attack.IsReady)
            {
                return false;
            }

            // Require a different player to activate the REV.
            if (reverberation.Creator == attack.SourcePlayer)
            {
                return false;
            }

            if (!attack.Accepts(reverberation.Effect))
            {
                return false;
            }

            if (!attack.CanHit(reverberation.Volume))
            {
                return false;
            }

            var reverberationVolume = reverberation.Volume;
            var attackVolume = attack.HitVolume;

            // Require a physical overlap even when auto-aim selected this REV.
            if (!Physics.ComputePenetration(
                reverberationVolume,
                reverberationVolume.transform.position,
                reverberationVolume.transform.rotation,
                attackVolume,
                attackVolume.transform.position,
                attackVolume.transform.rotation,
                out _,
                out _))
            {
                return false;
            }

            var activation = new ResonanceActivation(reverberation, attack);
            if (!reverberation.TryConsume())
            {
                return false;
            }

            try
            {
                activation.Effect.Execute(in activation);
                Activated?.Invoke(activation);
            }
            finally
            {
                UnityEngine.Object.Destroy(reverberation.gameObject);
            }

            return true;
        }
    }
}
