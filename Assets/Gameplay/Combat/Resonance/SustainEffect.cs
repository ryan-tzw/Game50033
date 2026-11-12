namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Resonance/Sustain Effect", fileName = "SustainEffect")]
    public sealed class SustainEffect : ReverberationEffect
    {
        [SerializeField, Min(0.01f)] private float explosionRadius = 0.6f;
        [SerializeField, Min(0f)] private float explosionDamage = 0.5f;
        [SerializeField] private LayerMask explosionHitMask = ~0;
        [SerializeField] private SustainField lingeringFieldPrefab;
        [SerializeField] private GameObject explosionVisualPrefab;
        [SerializeField, Min(0.01f)] private float explosionVisualLifetime = 0.2f;

        public bool IsConfigured
        {
            get
            {
                if (lingeringFieldPrefab == null || !lingeringFieldPrefab.enabled)
                {
                    return false;
                }

                if (!lingeringFieldPrefab.gameObject.activeSelf)
                {
                    return false;
                }

                return lingeringFieldPrefab.GetComponentInChildren<Reverberation>(true) == null;
            }
        }

        internal override void Execute(in ResonanceActivation activation)
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "not configured",
                    this);
                return;
            }

            if (explosionVisualPrefab != null)
            {
                var visual = Instantiate(explosionVisualPrefab, activation.Position, Quaternion.identity);
                Destroy(visual, Mathf.Max(0.01f, explosionVisualLifetime));
            }

            ApplyExplosionDamage(in activation);

            // Keep the field separate so consuming the REV does not remove it.
            var field = Instantiate(lingeringFieldPrefab, activation.Position, Quaternion.identity);
            field.Initialize(activation.Creator, activation.Resonator, activation.Strength);
        }

        private void ApplyExplosionDamage(in ResonanceActivation activation)
        {
            var attacker = activation.Resonator.gameObject;
            var targets = new HashSet<IHittable>();
            var hits = Physics.OverlapSphere(
                activation.Position,
                explosionRadius * activation.Strength,
                explosionHitMask,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var target = hit.GetComponentInParent<IHittable>();
                if (target == null || !targets.Add(target))
                {
                    continue;
                }

                var direction = hit.bounds.center - activation.Position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

                if (explosionDamage > 0f)
                {
                    target.TakeHit(new HitInfo(attacker, explosionDamage * activation.Strength, direction));
                }
            }
        }
    }
}
