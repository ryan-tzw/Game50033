namespace Game50033
{
    using static PlayerAbilityStats;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FluteAbilities : PlayerAbilities
    {
        public enum Action { None, Primary, Secondary, Charge, Release, Evasive }

        [SerializeField] private FluteTuning tuning;
        [SerializeField] private FluteFlow flow;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private FluteProjectile primaryProjectilePrefab;
        [SerializeField] private FluteProjectile secondaryProjectilePrefab;
        [SerializeField] private FluteProjectile chargedProjectilePrefab;
        [SerializeField] private Reverberation specialReverberationPrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip primarySound;
        [SerializeField] private AudioClip secondarySound;
        [SerializeField] private AudioClip defensiveSound;
        [SerializeField] private AudioClip specialSound;
        [Tooltip("Plays when charging but stops when the charge is released or cancelled")]
        [SerializeField] private AudioSource chargeSource;
        [SerializeField] private AudioClip chargeSound;

        private FluteWeaponVisual _visual;
        private float _secondaryReadyAt;
        private float _specialReadyAt;
        private float _defensiveReadyAt;
        private float _actionStartedAt;
        private float _activeDuration;
        private float _recoveryDuration;
        private float _chargeElapsed;
        private float _speedRewardUntil;
        private bool _gameplayReady;
        private bool _evasionRewarded;
        private int _shotIndex;
        private bool _specialRequiresRelease = true;
        private bool _paused;
        private Vector3 _movementVelocity;

        internal bool SpecialRequiresRelease => _specialRequiresRelease;

        public Action CurrentAction { get; private set; }
        public Vector3 ActionDirection { get; private set; } = Vector3.forward;
        public bool IsCharging => CurrentAction == Action.Charge;
        public bool IsEvasive => CurrentAction == Action.Evasive;
        public bool EvasionSucceeded => IsEvasive && _evasionRewarded;
        public Vector3 ChargePosition => ProjectilePosition;
        public float SpecialFlowCost => tuning.special.flowCost;
        public float ChargeNormalized => IsCharging
            ? Mathf.Clamp01(_chargeElapsed / Mathf.Max(0.01f, tuning.special.chargeDuration)) : 0f;
        public float ActionProgress => Mathf.Clamp01(
            (Player.Hitstop.ActionTime - _actionStartedAt) / Mathf.Max(0.01f, _activeDuration + _recoveryDuration));
        public float SecondaryCooldownRemaining => Mathf.Max(0f, _secondaryReadyAt - Time.time);
        public float SpecialCooldownRemaining => Mathf.Max(0f, _specialReadyAt - Time.time);
        public float DefensiveCooldownRemaining => Mathf.Max(0f, _defensiveReadyAt - Time.time);

        public override bool BlocksDefensive => IsEvasive || CurrentAction == Action.Release;
        public override float MovementSpeedMultiplier => Player.Hitstop.ActionTime < _speedRewardUntil
            ? tuning.defensive.successSpeedMultiplier : 1f;
        public override PlayerAttackPhase AnimationPhase
        {
            get
            {
                if (IsCharging)
                {
                    return PlayerAttackPhase.Windup;
                }
                if (CurrentAction == Action.None)
                {
                    return PlayerAttackPhase.None;
                }
                return Player.Hitstop.ActionTime - _actionStartedAt < _activeDuration
                    ? PlayerAttackPhase.Active : PlayerAttackPhase.Recovery;
            }
        }

        private Vector3 ProjectilePosition => projectileOrigin != null
            ? projectileOrigin.position : transform.position + Vector3.up * 0.25f;

        private bool HasControl => Application.isFocused && !_paused
            && Player.isActiveAndEnabled && Player.HasActiveInput
            && !Player.Health.IsDowned && !Player.Movement.IsDodging
            && flow != null && flow.isActiveAndEnabled;

        private bool CanStartAbility => _gameplayReady && HasControl
            && !Player.Hitstop.IsStopped && !BlocksAbilities;

        public override Vector3 GetMovementVelocity(Vector3 desiredVelocity, float deltaTime)
        {
            if (!Player.CanMove || !Player.HasActiveInput || Player.Movement.IsDodging)
            {
                _movementVelocity = Vector3.zero;
                return _movementVelocity;
            }
            if (Player.Movement.IsDisplacing)
            {
                // Keep Defensive's existing raw-input walking plus fixed displacement.
                _movementVelocity = desiredVelocity;
                return _movementVelocity;
            }

            var settings = tuning.movement;
            var desiredSpeed = desiredVelocity.magnitude;
            var speed = _movementVelocity.magnitude;
            if (desiredSpeed < 0.0001f
                || Vector3.Dot(_movementVelocity, desiredVelocity) < -0.0001f * speed * desiredSpeed)
            {
                // Opposing input brakes to a real stop before starting the new direction.
                _movementVelocity = Vector3.MoveTowards(_movementVelocity, Vector3.zero,
                    settings.deceleration * deltaTime);
                return _movementVelocity;
            }

            var direction = speed > 0.0001f
                ? Vector3.RotateTowards(_movementVelocity / speed, desiredVelocity / desiredSpeed,
                    settings.turnRate * Mathf.Deg2Rad * deltaTime, 0f)
                : desiredVelocity / desiredSpeed;
            speed = Mathf.MoveTowards(speed, desiredSpeed,
                (speed < desiredSpeed ? settings.acceleration : settings.deceleration) * deltaTime);
            _movementVelocity = direction * speed;
            return _movementVelocity;
        }

        private void OnEnable()
        {
            if (tuning == null || flow == null)
            {
                Debug.LogError("FluteAbilities requires Flute tuning and Flow.", this);
                enabled = false;
                return;
            }

            Player.Initialize();
            _visual = GetComponent<FluteWeaponVisual>();
            _gameplayReady = false;
            _specialRequiresRelease = true;
            ResonanceResolver.Activated += HandleResonance;
            Player.Health.AbilityHitAvoided += HandleAvoidedHit;
            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        protected override void OnDisable()
        {
            ResonanceResolver.Activated -= HandleResonance;
            if (Player.Health != null)
            {
                Player.Health.AbilityHitAvoided -= HandleAvoidedHit;
            }
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            _gameplayReady = false;
            base.OnDisable();
        }

        private void LateUpdate()
        {
            // Preserve the join/confirm guard and cancel without firing when control disappears.
            if (!HasControl || (IsCharging && !Player.SpecialInputEnabled))
            {
                _gameplayReady = false;
                Cancel();
                return;
            }
            _gameplayReady = true;
            if (Player.SpecialInputEnabled && !Player.SpecialHeld)
            {
                _specialRequiresRelease = false;
            }
            else if (!IsCharging && Player.SpecialPressedThisFrame)
            {
                // Require another press when an attack or cooldown rejected this press edge.
                _specialRequiresRelease = true;
            }

            if (Player.Hitstop.IsStopped)
            {
                return;
            }
            if (IsCharging)
            {
                if (Player.SpecialHeld)
                {
                    _chargeElapsed = Mathf.Min(tuning.special.chargeDuration,
                        _chargeElapsed + Player.Hitstop.ActionDeltaTime);
                }
                else
                {
                    ReleaseChargedAttack();
                }
            }
            else if (CurrentAction != Action.None && ActionProgress >= 1f)
            {
                EndAction();
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (sfxSource != null && sfxSource.isActiveAndEnabled && clip != null) sfxSource.PlayOneShot(clip);
        }

        public override bool TryUsePrimary()
        {
            if (!CanStartAbility
                || primaryProjectilePrefab == null || !primaryProjectilePrefab.IsConfigured)
            {
                return false;
            }

            var position = ProjectilePosition;
            var target = FluteTargeting.FindTarget(Player, position, tuning.primary.range,
                primaryProjectilePrefab.CollisionMask, primaryProjectilePrefab.HitVolume);
            var direction = Player.GetAttackDirection(position, tuning.primary.range,
                primaryProjectilePrefab.CollisionMask, primaryProjectilePrefab.HitVolume);

            if (target != null)
            {
                // Alternate the opening arc
                var side = _shotIndex++ % 2 == 0 ? 1f : -1f;
                direction = Quaternion.AngleAxis(tuning.primary.launchAngle * side, Vector3.up) * direction;
            }

            var projectile = Spawn(primaryProjectilePrefab, position, direction);
            var damage = Mathf.Lerp(tuning.primary.minimumDamage, tuning.primary.maximumDamage, flow.Normalized);
            projectile.InitializePrimary(Player, direction, target, damage);
            BeginAction(Action.Primary, direction, tuning.primary.activeTime, tuning.primary.recovery);
            PlaySound(primarySound);
            return true;
        }

        public override bool TryUseSecondary()
        {
            if (!CanStartAbility || (!IgnoreCooldowns && Time.time < _secondaryReadyAt)
                || secondaryProjectilePrefab == null || !secondaryProjectilePrefab.IsConfigured)
            {
                return false;
            }

            var position = ProjectilePosition;
            var target = FluteTargeting.FindTarget(Player, position, secondaryProjectilePrefab.MaximumTravelDistance,
                secondaryProjectilePrefab.CollisionMask, secondaryProjectilePrefab.HitVolume,
                secondaryProjectilePrefab.ResonatingAttack);
            var direction = Player.GetAttackDirection(position, secondaryProjectilePrefab.MaximumTravelDistance,
                secondaryProjectilePrefab.CollisionMask, secondaryProjectilePrefab.HitVolume,
                secondaryProjectilePrefab.ResonatingAttack);
            if (target != null && target.TryGetComponent<Reverberation>(out _))
            {
                // Keep compatible teammate REV priority local to Flute's Secondary.
                var offset = FluteTargeting.GetAimPoint(target, position) - position;
                if (offset.sqrMagnitude > 0.0001f)
                {
                    direction = offset.normalized;
                }
            }
            Spawn(secondaryProjectilePrefab, position, direction).InitializeSecondary(Player, direction, tuning.secondary.damage,
                secondaryProjectilePrefab);
            _secondaryReadyAt = IgnoreCooldowns ? 0f : Time.time + tuning.secondary.cooldown;
            BeginAction(Action.Secondary, direction, tuning.secondary.activeTime, tuning.secondary.recovery);
            PlaySound(secondarySound);
            return true;
        }

        public override bool TryUseSpecial()
        {
            if (!CanStartAbility || _specialRequiresRelease || !Player.SpecialPressedThisFrame
                || (!IgnoreCooldowns && Time.time < _specialReadyAt) || !IsSpecialConfigured)
            {
                return false;
            }

            _chargeElapsed = 0f;
            _specialRequiresRelease = true;
            BeginAction(Action.Charge, Player.AimDirection, tuning.special.chargeDuration, 0f);
            if (chargeSource != null && chargeSource.isActiveAndEnabled && chargeSound != null)
            {
                chargeSource.clip = chargeSound;
                chargeSource.Play();
            }
            return true;
        }

        internal bool IsSpecialConfigured => chargedProjectilePrefab != null && chargedProjectilePrefab.IsConfigured
            && specialReverberationPrefab != null && specialReverberationPrefab.gameObject.activeSelf
            && specialReverberationPrefab.Effect is BurstEffect;

        private void ReleaseChargedAttack()
        {
            if (!IsSpecialConfigured)
            {
                Cancel();
                return;
            }

            var charge = ChargeNormalized;
            var settings = tuning.special;
            var position = ProjectilePosition;
            var direction = Player.GetAttackDirection(position, chargedProjectilePrefab.MaximumTravelDistance,
                chargedProjectilePrefab.CollisionMask, chargedProjectilePrefab.HitVolume);
            var projectile = Spawn(chargedProjectilePrefab, position, direction);

            // Spend Flow only after release, cancelled charges shouldn't spend or create REV.
            var canResonate = charge >= 1f && flow.TryConsume(settings.flowCost);
            projectile.InitializeCharged(Player, direction,
                Mathf.Lerp(settings.minimumDamage, settings.maximumDamage, charge), canResonate,
                specialReverberationPrefab, Mathf.Lerp(settings.minimumRevStrength, settings.maximumRevStrength, charge),
                chargedProjectilePrefab);
            projectile.SetChargePresentation(charge);
            _specialReadyAt = IgnoreCooldowns ? 0f : Time.time + settings.cooldown;
            BeginAction(Action.Release, direction, settings.activeTime, settings.recovery);
            if (chargeSource != null) chargeSource.Stop();
            PlaySound(specialSound);
            if (_visual != null)
            {
                _visual.PlayRelease(charge, canResonate);
            }
        }

        public override bool TryUseDefensive()
        {
            if (!_gameplayReady || !HasControl || !Player.CanUseDefensive
                || Player.Movement.IsDisplacing || (!IgnoreCooldowns && Time.time < _defensiveReadyAt))
            {
                return false;
            }

            var direction = Player.Movement.MoveDirection;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = -Player.AimDirection;
            }
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.back;

            if (tuning.defensive.distance > 0f && !Player.Movement.TryStartDisplacement(direction,
                tuning.defensive.distance, tuning.defensive.movementDuration, tuning.defensive.movementCurve))
            {
                return false;
            }

            // Replace a charge or flourish without refunding already-spent attack cooldowns.
            _chargeElapsed = 0f;
            _evasionRewarded = false;
            _defensiveReadyAt = IgnoreCooldowns ? 0f : Time.time + tuning.defensive.cooldown;
            BeginAction(Action.Evasive, direction, tuning.defensive.invulnerabilityDuration, tuning.defensive.recovery);
            if (chargeSource != null) chargeSource.Stop();
            PlaySound(defensiveSound);
            Player.Health.GrantAbilityInvulnerability(tuning.defensive.invulnerabilityDuration);
            return true;
        }

        private void HandleAvoidedHit()
        {
            if (!IsEvasive || _evasionRewarded || !HasControl || Player.Health.AbilityInvulnerabilityRemaining <= 0f)
            {
                return;
            }

            // Reward a real damage attempt rejected during this Evasive State.
            _evasionRewarded = true;
            flow.Gain(tuning.defensive.successFlowGain);
            _speedRewardUntil = Player.Hitstop.ActionTime + tuning.defensive.successSpeedDuration;
            if (_visual != null)
            {
                _visual.PlayEvasionSuccess();
            }
        }

        private void HandleResonance(ResonanceActivation activation)
        {
            if (activation.Resonator != Player || activation.Attack == null || !flow.isActiveAndEnabled)
            {
                return;
            }
            if (activation.Attack.TryGetComponent(out FluteProjectile projectile) && projectile.TryClaimSecondaryFlowReward())
            {
                flow.Gain(tuning.secondary.resonanceFlowGain);
            }
        }

        private FluteProjectile Spawn(FluteProjectile prefab, Vector3 position, Vector3 direction)
        {
            return Instantiate(prefab, position, Quaternion.LookRotation(direction, Vector3.up));
        }

        private void BeginAction(Action action, Vector3 direction, float activeDuration, float recoveryDuration)
        {
            CurrentAction = action;
            ActionDirection = direction;
            _actionStartedAt = Player.Hitstop.ActionTime;
            _activeDuration = activeDuration;
            _recoveryDuration = recoveryDuration;
            BlocksMovement = action == Action.Evasive;
            BlocksAbilities = true;
            BlocksDodge = action == Action.Evasive;
        }

        private void EndAction()
        {
            if (chargeSource != null) chargeSource.Stop();
            if (IsEvasive)
            {
                Player.Health.ClearAbilityInvulnerability();
                Player.Movement.CancelDisplacement();
            }
            CurrentAction = Action.None;
            _chargeElapsed = 0f;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }

        public override void Cancel()
        {
            _movementVelocity = Vector3.zero;
            EndAction();
            _specialRequiresRelease = true;
            _speedRewardUntil = 0f;
            _evasionRewarded = false;
            if (_visual != null)
            {
                _visual.Cancel();
            }
        }

        public override PlayerAbilityStats GetAbilityStats(PlayerAbility ability)
        {
            if (tuning == null || !GetAbilityState(ability).Supported) return default;
            return ability switch
            {
                PlayerAbility.Primary => new(("Damage by Flow", Range(tuning.primary.minimumDamage, tuning.primary.maximumDamage)),
                    ("Cooldown", "None")),
                PlayerAbility.Secondary => new(("Damage", Number(tuning.secondary.damage)),
                    ("Cooldown", Seconds(tuning.secondary.cooldown))),
                PlayerAbility.Special => new(("Charged damage", Range(tuning.special.minimumDamage, tuning.special.maximumDamage)),
                    ("Cooldown", Seconds(tuning.special.cooldown))),
                PlayerAbility.Defensive => new(("Invulnerability", Seconds(tuning.defensive.invulnerabilityDuration)),
                    ("Cooldown", Seconds(tuning.defensive.cooldown))),
                _ => default
            };
        }

        public override PlayerAbilityState GetAbilityState(PlayerAbility ability) => ability switch
        {
            PlayerAbility.Primary when tuning != null && primaryProjectilePrefab != null && primaryProjectilePrefab.IsConfigured
                => new PlayerAbilityState(0f, 0f),
            PlayerAbility.Secondary when tuning != null && secondaryProjectilePrefab != null && secondaryProjectilePrefab.IsConfigured
                => new PlayerAbilityState(SecondaryCooldownRemaining, tuning.secondary.cooldown),
            PlayerAbility.Defensive when tuning != null
                => new PlayerAbilityState(DefensiveCooldownRemaining, tuning.defensive.cooldown),
            PlayerAbility.Special when tuning != null && IsSpecialConfigured
                => new PlayerAbilityState(SpecialCooldownRemaining, tuning.special.cooldown),
            _ => default
        };

        public override void ClearCooldowns()
        {
            _secondaryReadyAt = 0f;
            _specialReadyAt = 0f;
            _defensiveReadyAt = 0f;
        }

        public override void ResetForEncounter()
        {
            base.ResetForEncounter();
            flow.ResetForEncounter();
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            _gameplayReady = false;
            Cancel();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _gameplayReady = false;
                Cancel();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                _gameplayReady = false;
                Cancel();
            }
        }
    }
}
