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

        private Player _player;
        private PlayerMovement _movement;
        private PlayerHitstop _hitstop;
        private float _abilityImmuneUntil;
        private bool _initialized;
        private float _reviveAt;
        private float _immuneUntil;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDowned { get; private set; }
        public float RevivalRemaining => IsDowned ? Mathf.Max(0f, _reviveAt - Time.time) : 0f;
        public float RevivalImmunityRemaining => Mathf.Max(0f, _immuneUntil - Time.time);
        public float AbilityInvulnerabilityRemaining => Mathf.Max(0f, _abilityImmuneUntil - (_hitstop != null ? _hitstop.ActionTime : Time.time));
        public bool IsInvulnerable => (_player != null && _player.Session != null && _player.Session.Debug.Invincibility)
            || RevivalImmunityRemaining > 0f || AbilityInvulnerabilityRemaining > 0f
            || (_movement != null && _movement.IsInvulnerable);

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

            _player = GetComponent<Player>();
            _movement = GetComponent<PlayerMovement>();
            _hitstop = GetComponent<PlayerHitstop>();
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

        // one active ability owns this deadline; clearing it never removes dodge or revival immunity.
        public void GrantAbilityInvulnerability(float duration)
        {
            Initialize();
            if (!isActiveAndEnabled || IsDowned || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            var now = _hitstop != null ? _hitstop.ActionTime : Time.time;
            _abilityImmuneUntil = Mathf.Max(_abilityImmuneUntil, now + duration);
        }

        public void ClearAbilityInvulnerability() => _abilityImmuneUntil = 0f;

        private void OnDisable() => ClearAbilityInvulnerability();

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
