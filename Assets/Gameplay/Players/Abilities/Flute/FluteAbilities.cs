namespace Game50033
{
    using UnityEngine;

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FluteAbilities : PlayerAbilities
    {
        [Header("References")]
        [SerializeField] private FluteFlow flow;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private FluteProjectile secondaryProjectilePrefab;
        [SerializeField] private FluteProjectile chargedProjectilePrefab;
        [SerializeField] private Reverberation specialReverberationPrefab;

        [Header("Secondary")]
        [SerializeField, Min(0.01f)] private float secondaryCooldown = 0.5f;
        [SerializeField, Min(0f)] private float secondaryDamage = 2f;
        [SerializeField, Min(0f)] private float secondaryResonanceFlowGain = 15f;

        [Header("Special")]
        [SerializeField, Min(0.01f)] private float specialCooldown = 2f;
        [SerializeField, Min(0.01f)] private float maximumChargeDuration = 1.5f;
        [SerializeField, Min(0f)] private float minimumSpecialDamage = 1f;
        [SerializeField, Min(0f)] private float maximumSpecialDamage = 5f;
        [SerializeField, Min(0.01f)] private float minimumReverberationStrength = 0.5f;
        [SerializeField, Min(0.01f)] private float maximumReverberationStrength = 1.5f;
        [SerializeField, Min(0f)] private float maximumChargeFlowCost = 25f;

        private float _secondaryReadyAt;
        private float _specialReadyAt;
        private float _chargeElapsed;
        private bool _gameplayReady;

        public bool IsCharging { get; private set; }
        public float ChargeNormalized => IsCharging
            ? Mathf.Clamp01(_chargeElapsed / Mathf.Max(0.01f, maximumChargeDuration))
            : 0f;

        private void OnEnable()
        {
            // Ignore the join or confirm press until gameplay input is ready.
            _gameplayReady = false;
            ResonanceResolver.Activated += HandleResonance;
        }

        protected override void OnDisable()
        {
            ResonanceResolver.Activated -= HandleResonance;
            _gameplayReady = false;
            base.OnDisable();
        }

        private bool HasControl
        {
            get
            {
                if (!Player.isActiveAndEnabled || !Player.HasActiveInput)
                {
                    return false;
                }

                if (Player.Health == null || Player.Health.IsDowned)
                {
                    return false;
                }

                if (Player.Movement.IsDodging)
                {
                    return false;
                }

                return flow != null && flow.gameObject == gameObject && flow.isActiveAndEnabled;
            }
        }

        private bool CanStartAbility => _gameplayReady && HasControl && !BlocksAbilities;

        private void LateUpdate()
        {
            // Cancel charging when control is lost instead of treating it as a button release.
            if (!HasControl || (IsCharging && !Player.SpecialInputEnabled))
            {
                ResetAbilityInput();
                return;
            }

            _gameplayReady = true;

            if (!IsCharging)
            {
                return;
            }

            if (Player.SpecialHeld)
            {
                _chargeElapsed = Mathf.Min(maximumChargeDuration, _chargeElapsed + Time.deltaTime);
            }
            else
            {
                ReleaseChargedAttack();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                return;
            }

            ResetAbilityInput();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                return;
            }

            ResetAbilityInput();
        }

        private void ResetAbilityInput()
        {
            _gameplayReady = false;
            Cancel();
        }

        public override bool TryUsePrimary() => false;
        public override bool TryUseDefensive() => false;

        public override bool TryUseSecondary()
        {
            if (!CanStartAbility || Time.time < _secondaryReadyAt)
            {
                return false;
            }

            if (secondaryProjectilePrefab == null || !secondaryProjectilePrefab.IsConfigured)
            {
                return false;
            }

            var position = ProjectilePosition;
            var direction = Player.GetAttackDirection(
                position,
                secondaryProjectilePrefab.MaximumTravelDistance,
                secondaryProjectilePrefab.CollisionMask,
                secondaryProjectilePrefab.HitVolume,
                secondaryProjectilePrefab.ResonatingAttack);

            var projectile = Instantiate(
                secondaryProjectilePrefab,
                position,
                Quaternion.LookRotation(direction, Vector3.up));

            projectile.InitializeSecondary(Player, direction, secondaryDamage);
            _secondaryReadyAt = Time.time + secondaryCooldown;
            return true;
        }

        public override bool TryUseSpecial()
        {
            if (!CanStartAbility || !Player.SpecialPressedThisFrame || Time.time < _specialReadyAt)
            {
                return false;
            }

            if (!IsSpecialConfigured)
            {
                return false;
            }

            IsCharging = true;
            _chargeElapsed = 0f;
            BlocksAbilities = true;
            return true;
        }

        private bool IsSpecialConfigured
        {
            get
            {
                if (chargedProjectilePrefab == null || !chargedProjectilePrefab.IsConfigured)
                {
                    return false;
                }

                if (specialReverberationPrefab == null || !specialReverberationPrefab.gameObject.activeSelf)
                {
                    return false;
                }

                return specialReverberationPrefab.Effect is BurstEffect;
            }
        }

        private Vector3 ProjectilePosition => projectileOrigin != null
            ? projectileOrigin.position
            : transform.position + Vector3.up * 0.25f;

        private void ReleaseChargedAttack()
        {
            var charge = ChargeNormalized;
            if (!IsSpecialConfigured)
            {
                Cancel();
                return;
            }

            var position = ProjectilePosition;
            var direction = Player.GetAttackDirection(
                position,
                chargedProjectilePrefab.MaximumTravelDistance,
                chargedProjectilePrefab.CollisionMask,
                chargedProjectilePrefab.HitVolume);

            var projectile = Instantiate(
                chargedProjectilePrefab,
                position,
                Quaternion.LookRotation(direction, Vector3.up));

            // Spend Flow only for RES at full charge, always fire the charged attack.
            var canResonate = charge >= 1f && flow.TryConsume(maximumChargeFlowCost);
            var damage = Mathf.Lerp(minimumSpecialDamage, maximumSpecialDamage, charge);
            var reverberationStrength = Mathf.Lerp(minimumReverberationStrength, maximumReverberationStrength, charge);

            projectile.InitializeCharged(
                Player,
                direction,
                damage,
                canResonate,
                specialReverberationPrefab,
                reverberationStrength);
            _specialReadyAt = Time.time + specialCooldown;
            Cancel();
        }

        private void HandleResonance(ResonanceActivation activation)
        {
            if (activation.Resonator != Player || activation.Attack == null)
            {
                return;
            }

            if (flow == null || !flow.isActiveAndEnabled)
            {
                return;
            }

            if (!activation.Attack.TryGetComponent(out FluteProjectile projectile))
            {
                return;
            }

            if (!projectile.RewardsSecondaryFlow)
            {
                return;
            }

            flow.Gain(secondaryResonanceFlowGain);
        }

        public override void Cancel()
        {
            IsCharging = false;
            _chargeElapsed = 0f;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }
    }
}
