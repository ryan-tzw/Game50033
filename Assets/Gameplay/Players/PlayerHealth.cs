namespace Game50033
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerHealthTuning tuning;

        private Player _player;
        private PlayerMovement _movement;
        private PlayerHitstop _hitstop;
        private float _abilityImmuneUntil;
        private bool _initialized;
        private float _reviveAt;
        private float _immuneUntil;
        private float _postHitImmuneUntil;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => tuning.maxHealth;
        public bool IsDowned { get; private set; }
        public float RevivalRemaining => IsDowned ? Mathf.Max(0f, _reviveAt - Time.time) : 0f;
        public float PostHitInvulnerabilityRemaining => Mathf.Max(0f, _postHitImmuneUntil - Time.time);
        public float RevivalImmunityRemaining => Mathf.Max(0f, _immuneUntil - Time.time);
        public float AbilityInvulnerabilityRemaining => Mathf.Max(0f, _abilityImmuneUntil - (_hitstop != null ? _hitstop.ActionTime : Time.time));
        public bool IsInvulnerable => (_player != null && _player.Session != null && _player.Session.Debug.Invincibility)
            || PostHitInvulnerabilityRemaining > 0f || RevivalImmunityRemaining > 0f || AbilityInvulnerabilityRemaining > 0f
            || (_movement != null && _movement.IsInvulnerable);

        public event Action<int, int> HealthChanged;
        public event Action HealthReset;
        public event Action<int> Damaged;
        internal event Action<IEncounterBoss, int> DamagedByBoss;
        public event Action Downed;
        public event Action Revived;
        // Report real hit attempts rejected specifically by ability immunity.
        public event Action AbilityHitAvoided;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;
            if (tuning == null)
                throw new InvalidOperationException($"{name} requires a player health tuning asset.");

            _player = GetComponent<Player>();
            _movement = GetComponent<PlayerMovement>();
            _hitstop = GetComponent<PlayerHitstop>();
            CurrentHealth = MaxHealth;
            _initialized = true;
        }

        private void Update()
        {
            if (!IsDowned || Time.time < _reviveAt) return;

            IsDowned = false;
            CurrentHealth = Mathf.Clamp(tuning.reviveHealth, 1, MaxHealth);
            _immuneUntil = Time.time + tuning.revivalImmunityDuration;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
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

        internal void ClearPostHitInvulnerability() => _postHitImmuneUntil = 0f;

        private void OnDisable()
        {
            ClearAbilityInvulnerability();
            ClearPostHitInvulnerability();
        }

        public bool TryTakeDamage(int damage)
        {
            return TryTakeDamage(damage, Vector3.zero);
        }

        // incomingFrom points toward the hit source; zero represents damage with no direction.
        public bool TryTakeDamage(int damage, Vector3 incomingFrom) => TryTakeDamage(damage, incomingFrom, null);

        internal bool TryTakeDamage(int damage, Vector3 incomingFrom, IEncounterBoss bossSource)
        {
            Debug.Log($"TakeDamage: movement={(_movement != null)}, dodgeInvulnerable={(_movement != null && _movement.IsInvulnerable)}, healthInvulnerable={IsInvulnerable}", this);
            if (damage > 0 && !IsDowned && AbilityInvulnerabilityRemaining > 0f
                && PostHitInvulnerabilityRemaining <= 0f && RevivalImmunityRemaining <= 0f && (_movement == null || !_movement.IsInvulnerable)
                && !(_player != null && _player.Session != null && _player.Session.Debug.Invincibility))
            {
                AbilityHitAvoided?.Invoke();
                return false;
            }
            if (damage <= 0 || CurrentHealth <= 0 || IsDowned || IsInvulnerable) return false;
            if (incomingFrom != Vector3.zero && _player != null && _player.isActiveAndEnabled
                && _player.Abilities != null && _player.Abilities.TryParryHit(incomingFrom)) return false;

            var applied = Mathf.Min(CurrentHealth, damage);
            CurrentHealth -= applied;
            if (CurrentHealth == 0)
            {
                ClearPostHitInvulnerability();
                IsDowned = true;
                _reviveAt = Time.time + tuning.downedDuration;
                // cancel actions before observers of the health change can start more work.
                Downed?.Invoke();
            }
            else
            {
                _postHitImmuneUntil = Time.time + tuning.postHitInvulnerabilityDuration;
            }

            Damaged?.Invoke(applied);
            if (bossSource != null) DamagedByBoss?.Invoke(bossSource, applied);

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            return true;
        }

        // encounter boundary: full hearts, standing, and no leftover revive timer or immunity.
        public void ResetForEncounter()
        {
            Initialize();
            var wasDowned = IsDowned;
            IsDowned = false;
            CurrentHealth = MaxHealth;
            _reviveAt = 0f;
            _immuneUntil = 0f;
            ClearPostHitInvulnerability();
            _abilityImmuneUntil = 0f;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            HealthReset?.Invoke();
            if (wasDowned) Revived?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDowned || CurrentHealth >= MaxHealth) return;

            CurrentHealth = (int)Math.Min(MaxHealth, (long)CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }
}
