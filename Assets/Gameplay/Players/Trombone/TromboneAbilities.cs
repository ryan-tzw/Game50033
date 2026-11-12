namespace Game50033
{
    using UnityEngine;

    public class TromboneAbilities : PlayerAbilities
    {
        [Header("References")]
        [SerializeField] private PlayerMovement Movement;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private TromboneProjectile primaryProjectilePrefab;
        [SerializeField] private TromboneProjectile specialProjectilePrefab;
        [SerializeField] private Reverberation specialReverberationPrefab;

        [Header("Abilities")]
        [SerializeField, Min(0.01f)] private float trombonePrimaryCooldown = 1f;
        [SerializeField, Min(0f)] private float tromboneMinPrimaryDamage = 1f;
        [SerializeField, Min(0f)] private float tromboneMaxPrimaryDamage = 5f;
        [SerializeField, Min(0.01f)] private float tromboneSecondaryCooldown = 5f;
        [SerializeField, Min(0.01f)] private float tromboneDefensiveCooldown = 2f;
        [SerializeField, Min(0.01f)] private float tromboneSpecialCooldown = 3f;
        [SerializeField, Min(0f)] private float tromboneMinSpecialDamage = 2f;
        [SerializeField, Min(0f)] private float tromboneMaxSpecialDamage = 10f;
        [SerializeField, Min(0.01f)] private float tromboneActionDuration = 0.25f;
        private float _trombonePrimaryReadyAt;
        private float _tromboneSecondaryReadyAt;
        private float _tromboneDefensiveReadyAt;
        private float _tromboneSpecialReadyAt;
        private float _tromboneActiveUntil;

        [Header("CommandZone")]
        private TromboneCommandZone _activeCommandZone;
        public GameObject tromboneCommandZone;
        public bool IsInCommandZone => _activeCommandZone != null && _activeCommandZone.IsOwnerInside;

        [Header("Pressure")]
        [SerializeField] private float maxPressure = 100f;
        private float _pressure;
        [SerializeField, Min(0f)] private float consecutiveAttackInterval = 3f;
        private float _attackIntervalCountdown;
        private float _pressureGainAmount => IsInCommandZone ? 1.5f : 1f;
        private float _pressureLoseAmount => IsInCommandZone ? 0.5f : 1f;
        private bool _isMoving => Movement.IsMoving;
        private bool _usedPrimary;
        private bool _usedSpecial;
        [SerializeField, Min(0f)] private float guardLifespan = 5f;
        private float _guardLifespanCountdown;
        private bool _negateNextHit;
        private bool _trombonePrimaryHasRes;
        private bool _isHit;
        private bool _specialAbilityInUse;
        private Vector3 ProjectilePosition => projectileOrigin != null
            ? projectileOrigin.position
            : transform.position + Vector3.up * 0.25f;
        enum TromboneAbilitiesType
        {
            TrombonePrimary,
            TromboneSecondary,
            TromboneDefensive,
            TromboneSpecial
        }
        void Update()
        {
            if (_tromboneActiveUntil > 0f && Time.time >= _tromboneActiveUntil) Cancel();

            _attackIntervalCountdown = Mathf.Max(0f, _attackIntervalCountdown - Time.deltaTime);
            _guardLifespanCountdown = Mathf.Max(0f, _guardLifespanCountdown - Time.deltaTime);

            if (_usedPrimary || _usedSpecial)
            {
                _attackIntervalCountdown = consecutiveAttackInterval;
                _pressure = Mathf.Clamp(_pressure + _pressureGainAmount, 0f, maxPressure);
                _usedPrimary = false;
                _usedSpecial = false;
            }

            if (_attackIntervalCountdown <= 0f || _isMoving)
            {
                _pressure = Mathf.Clamp(_pressure - _pressureLoseAmount, 0f, maxPressure);
            }

            if (_guardLifespanCountdown <= 0f)
            {
                _negateNextHit = false;
            }

            if (_isHit && _negateNextHit)
            {
                // TODO: if player can take dmg, negate attack damage
                _negateNextHit = false;
                _trombonePrimaryHasRes = true;
            }
        }

        public override bool TryUsePrimary()
        {
            _usedPrimary = TryActivate(TromboneAbilitiesType.TrombonePrimary, ref _trombonePrimaryReadyAt, trombonePrimaryCooldown, blockMovement: true, blockDodge: false);
            return _usedPrimary;
        }

        public override bool TryUseSecondary()
        {
            if (!CanCreateCommandZone())
            {
                return false;
            }

            return TryActivate(
                TromboneAbilitiesType.TromboneSecondary,
                ref _tromboneSecondaryReadyAt,
                tromboneSecondaryCooldown,
                blockMovement: false,
                blockDodge: false);
        }

        public override bool TryUseDefensive() =>
            TryActivate(TromboneAbilitiesType.TromboneDefensive, ref _tromboneDefensiveReadyAt, tromboneDefensiveCooldown, blockMovement: false, blockDodge: true);

        public override bool TryUseSpecial() =>
            _usedSpecial = TryActivate(TromboneAbilitiesType.TromboneSpecial, ref _tromboneSpecialReadyAt, tromboneSpecialCooldown, blockMovement: true, blockDodge: true);

        private bool TryActivate(TromboneAbilitiesType ability, ref float readyAt, float cooldown, bool blockMovement, bool blockDodge)
        {
            if (!isActiveAndEnabled || Player.Health.IsDowned || Player.Movement.IsDodging
                || BlocksAbilities || Time.time < readyAt) return false;

            readyAt = Time.time + cooldown;
            _tromboneActiveUntil = Time.time + tromboneActionDuration;
            BlocksMovement = blockMovement;
            BlocksDodge = blockDodge;
            BlocksAbilities = true;

            switch (ability)
            {
                case TromboneAbilitiesType.TrombonePrimary:
                    TrombonePrimaryAbility();
                    break;
                case TromboneAbilitiesType.TromboneSecondary:
                    CreateCommandZone();
                    break;
                case TromboneAbilitiesType.TromboneDefensive:
                    TromboneDefensiveAbility();
                    break;
                case TromboneAbilitiesType.TromboneSpecial:
                    TromboneSpecialAbility();
                    break;
            }

            return true;
        }
        public void TrombonePrimaryAbility()
        {
            var position = ProjectilePosition;
            var direction = Player.GetAttackDirection(
                position,
                primaryProjectilePrefab.MaximumTravelDistance,
                primaryProjectilePrefab.CollisionMask,
                primaryProjectilePrefab.HitVolume,
                primaryProjectilePrefab.ResonatingAttack);

            var projectile = Instantiate(
                primaryProjectilePrefab,
                position,
                Quaternion.LookRotation(direction, Vector3.up));
            var trombonePrimaryDamage = Mathf.Lerp(tromboneMinPrimaryDamage, tromboneMaxPrimaryDamage, _pressure);
            projectile.InitializePrimary(Player, direction, trombonePrimaryDamage, _trombonePrimaryHasRes);
            if (_trombonePrimaryHasRes)
            {
                _trombonePrimaryHasRes = false;
            }

        }
        public void TromboneSecondaryAbility()
        {
            if (!CanCreateCommandZone())
            {
                return;
            }

            CreateCommandZone();
        }

        private void CreateCommandZone()
        {
            var commandZone = Instantiate(tromboneCommandZone, transform.position, Quaternion.identity);
            _activeCommandZone = commandZone.GetComponent<TromboneCommandZone>();
            _activeCommandZone.Initialize(Player);
        }

        private bool CanCreateCommandZone()
        {
            if (_activeCommandZone != null && _activeCommandZone.IsActive)
            {
                return false;
            }

            if (tromboneCommandZone == null || !tromboneCommandZone.activeSelf)
            {
                return false;
            }

            var zone = tromboneCommandZone.GetComponent<TromboneCommandZone>();
            if (zone == null || !zone.enabled)
            {
                return false;
            }

            // Reject an incomplete Sustain setup before starting the cooldown.
            var reverberation = zone.SustainReverberationPrefab;
            if (reverberation == null || !reverberation.enabled || !reverberation.gameObject.activeSelf)
            {
                return false;
            }

            if (!(reverberation.Effect is SustainEffect sustain) || !sustain.IsConfigured)
            {
                return false;
            }

            return zone.GetComponentInChildren<Reverberation>(true) == null;
        }

        protected override void OnDisable()
        {
            if (_activeCommandZone != null)
            {
                _activeCommandZone.CancelZone();
            }

            _activeCommandZone = null;
            base.OnDisable();
        }

        public void TromboneDefensiveAbility()
        {
            // TODO: reduce movement speed
            _negateNextHit = true;
            _guardLifespanCountdown = guardLifespan;
        }
        public void TromboneSpecialAbility()
        {
            var position = ProjectilePosition;
            var direction = Player.GetAttackDirection(
                position,
                specialProjectilePrefab.MaximumTravelDistance,
                specialProjectilePrefab.CollisionMask,
                specialProjectilePrefab.HitVolume);

            var projectile = Instantiate(
                specialProjectilePrefab,
                position,
                Quaternion.LookRotation(direction, Vector3.up));
            var tromboneSpecialDamage = Mathf.Lerp(tromboneMinSpecialDamage, tromboneMaxSpecialDamage, _pressure);
            var tromboneReverberationDamage = tromboneSpecialDamage * 0.5f;
            projectile.InitializeSpecial(Player, direction, tromboneSpecialDamage, specialReverberationPrefab, tromboneReverberationDamage);

            if (_activeCommandZone != null)
            {
                _activeCommandZone.ConsumeZone();
            }
            _activeCommandZone = null;
        }
        public override void Cancel()
        {
            _tromboneActiveUntil = 0f;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }
    }
}
