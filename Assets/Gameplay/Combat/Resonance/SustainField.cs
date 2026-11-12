namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class SustainField : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float radius = 1f;
        [SerializeField, Min(0f)] private float damagePerTick = 0.25f;
        [SerializeField, Min(0.01f)] private float tickInterval = 0.5f;
        [SerializeField, Min(0.01f)] private float duration = 3f;
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Optional child GameObject displaying the lingering damage area.")]
        [SerializeField] private GameObject visualRoot;

        private readonly HashSet<IHittable> _hitThisTick = new();
        private float _strength;
        private float _expiresAt;
        private float _nextTickAt;
        private bool _finished;

        public Player Creator { get; private set; }
        public Player SourcePlayer { get; private set; }
        public float RemainingLifetime =>
            ReferenceEquals(SourcePlayer, null) ? 0f : Mathf.Max(0f, _expiresAt - Time.time);

        private void Awake()
        {
            SetVisualActive(false);
        }

        public void Initialize(Player creator, Player resonator, float strength = 1f)
        {
            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            if (resonator == null)
            {
                throw new ArgumentNullException(nameof(resonator));
            }

            if (float.IsNaN(strength) || float.IsInfinity(strength) || strength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(strength));
            }

            // Prevent reuse even after Unity destroys the source player.
            if (!ReferenceEquals(SourcePlayer, null))
            {
                throw new InvalidOperationException("A Sustain field can only be initialized once.");
            }

            Creator = creator;
            SourcePlayer = resonator;
            _strength = strength;
            _expiresAt = Time.time + Mathf.Max(0.01f, duration);
            _nextTickAt = Time.time + Mathf.Max(0.01f, tickInterval);
            SetVisualActive(true);
        }

        private void OnEnable()
        {
            if (ReferenceEquals(SourcePlayer, null) || _finished)
            {
                return;
            }

            _nextTickAt = Time.time + Mathf.Max(0.01f, tickInterval);
            SetVisualActive(Time.time < _expiresAt);
        }

        private void OnDisable()
        {
            SetVisualActive(false);
        }

        private void Update()
        {
            if (ReferenceEquals(SourcePlayer, null) || _finished)
            {
                return;
            }

            if (SourcePlayer == null || Time.time >= _expiresAt)
            {
                _finished = true;
                SetVisualActive(false);
                Destroy(gameObject);
                return;
            }

            if (Time.time < _nextTickAt)
            {
                return;
            }

            _nextTickAt = Time.time + Mathf.Max(0.01f, tickInterval);
            ApplyDamageTick();
        }

        private void ApplyDamageTick()
        {
            if (damagePerTick <= 0f)
            {
                return;
            }

            var attacker = SourcePlayer.gameObject;
            _hitThisTick.Clear();
            var hits = Physics.OverlapSphere(
                transform.position,
                radius * _strength,
                hitMask,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var target = hit.GetComponentInParent<IHittable>();

                if (target == null || !_hitThisTick.Add(target))
                {
                    continue;
                }

                var direction = hit.bounds.center - transform.position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
                target.TakeHit(new HitInfo(attacker, damagePerTick * _strength, direction));
            }
        }

        private void SetVisualActive(bool active)
        {
            if (visualRoot != null && visualRoot != gameObject && visualRoot.transform.IsChildOf(transform))
            {
                visualRoot.SetActive(active);
            }
        }
    }
}
