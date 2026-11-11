namespace Game50033
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 5;
        [SerializeField, Min(0f)] private float downedDuration = 15f;
        [SerializeField, Min(1)] private int reviveHealth = 1;
        [SerializeField, Min(0f)] private float revivalImmunityDuration = 1f;

        private PlayerMovement _movement;
        private bool _initialized;
        private float _reviveAt;
        private float _immuneUntil;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDowned { get; private set; }
        public float RevivalRemaining => IsDowned ? Mathf.Max(0f, _reviveAt - Time.time) : 0f;
        public float RevivalImmunityRemaining => Mathf.Max(0f, _immuneUntil - Time.time);
        public bool IsInvulnerable => RevivalImmunityRemaining > 0f || (_movement != null && _movement.IsInvulnerable);

        public event Action<int, int> HealthChanged;
        public event Action Downed;
        public event Action Revived;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _movement = GetComponent<PlayerMovement>();
            CurrentHealth = maxHealth;
            _initialized = true;
        }

        private void Update()
        {
            if (!IsDowned || Time.time < _reviveAt) return;

            IsDowned = false;
            CurrentHealth = Mathf.Clamp(reviveHealth, 1, maxHealth);
            _immuneUntil = Time.time + revivalImmunityDuration;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            Revived?.Invoke();
        }

        public bool TryTakeDamage(int damage)
        {
            if (damage <= 0 || IsDowned || IsInvulnerable) return false;

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            if (CurrentHealth == 0)
            {
                IsDowned = true;
                _reviveAt = Time.time + downedDuration;
                // cancel actions before observers of the health change can start more work.
                Downed?.Invoke();
            }

            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            return true;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDowned || CurrentHealth >= maxHealth) return;

            CurrentHealth = (int)Math.Min(maxHealth, (long)CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }
    }
}
