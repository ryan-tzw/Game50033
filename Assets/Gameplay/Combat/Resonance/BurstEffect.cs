namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Resonance/Burst Effect", fileName = "BurstEffect")]
    public sealed class BurstEffect : ReverberationEffect
    {
        [SerializeField, Min(0.01f)] private float radius = 1f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField] private LayerMask hitMask = ~0;

        internal override void Execute(in ResonanceActivation activation)
        {
            if (radius <= 0f || damage <= 0f)
            {
                return;
            }

            var attacker = activation.Resonator.gameObject;
            var hits = Physics.OverlapSphere(
                activation.Position,
                radius * activation.Strength,
                hitMask,
                QueryTriggerInteraction.Ignore);
            var targets = new HashSet<IHittable>();

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var target = hit.GetComponentInParent<IHittable>();

                // Hit each target once
                if (target == null || !targets.Add(target))
                {
                    continue;
                }

                var direction = hit.bounds.center - activation.Position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
                target.TakeHit(new HitInfo(attacker, damage * activation.Strength, direction));
            }
        }
    }
}
