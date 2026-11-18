namespace Game50033
{
    using System;
    using System.Collections.Generic;
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
            if (!CanActivate(reverberation, attack) || attack.HitVolume is not SphereCollider sphere) return false;
            var center = sphere.transform.TransformPoint(sphere.center);
            var scale = sphere.transform.lossyScale;
            var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return ReverberationOverlap.OverlapsSweep(reverberation, center, center, radius, Camera.main)
                && Consume(reverberation, attack);
        }

        internal static void ActivateAlongSweep(ResonatingAttack attack, Vector3 start, Vector3 end, float radius)
        {
            var fields = Reverberation.Active;
            if (fields.Count == 0 || !attack.IsReady) return;
            var camera = Camera.main;
            while (attack.IsReady)
            {
                Reverberation first = null;
                var firstFraction = 1f;
                for (var i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    if (!CanActivate(field, attack)
                        || !ReverberationOverlap.OverlapsSweep(field, start, Vector3.Lerp(start, end, firstFraction), radius, camera))
                        continue;

                    // only measure the entry when more than one field lies on this step.
                    if (first != null) firstFraction = Mathf.Min(firstFraction, EntryFraction(first, firstFraction));
                    var fraction = first == null ? firstFraction : EntryFraction(field, firstFraction);
                    if (first == null || fraction < firstFraction)
                    {
                        first = field;
                        firstFraction = fraction;
                    }
                }
                if (first == null || !Consume(first, attack)) return;
            }

            float EntryFraction(Reverberation field, float limit)
            {
                if (ReverberationOverlap.OverlapsSweep(field, start, start, radius, camera)) return 0f;
                var lower = 0f;
                for (var i = 0; i < 10; i++)
                {
                    var middle = (lower + limit) * 0.5f;
                    if (ReverberationOverlap.OverlapsSweep(field, start, Vector3.Lerp(start, end, middle), radius, camera))
                        limit = middle;
                    else
                        lower = middle;
                }
                return limit;
            }
        }

        internal static bool TryActivateTriangle(Reverberation field, ResonatingAttack attack,
            Vector3 tip, Vector3 left, Vector3 right, Camera camera)
        {
            return CanActivate(field, attack) && ReverberationOverlap.OverlapsTriangle(field, tip, left, right, camera)
                && Consume(field, attack);
        }

        internal static void ActivateInGroundCircle(ResonatingAttack attack, Vector3 centre, float radius)
        {
            var activeFields = Reverberation.Active;
            if (!attack.IsReady || activeFields.Count == 0) return;

            // a reaction may change the live list; only fields present at this impact belong to the smash.
            var fields = new List<Reverberation>(activeFields);
            foreach (var field in fields)
            {
                if (CanActivate(field, attack) && ReverberationOverlap.OverlapsGroundCircle(field, centre, radius))
                    Consume(field, attack);
            }
        }

        private static bool CanActivate(Reverberation reverberation, ResonatingAttack attack)
        {
            return reverberation != null && attack != null && reverberation.IsAvailable && attack.IsReady
                && reverberation.Creator != attack.SourcePlayer && attack.Accepts(reverberation.Effect)
                && attack.CanHit(reverberation.Volume);
        }

        private static bool Consume(Reverberation reverberation, ResonatingAttack attack)
        {
            var activation = new ResonanceActivation(reverberation, attack);
            if (!reverberation.TryConsume())
            {
                return false;
            }

            activation.Effect.Execute(in activation);
            Activated?.Invoke(activation);
            return true;
        }
    }
}
