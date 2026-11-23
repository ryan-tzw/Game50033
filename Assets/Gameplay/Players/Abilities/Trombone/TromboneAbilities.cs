namespace Game50033
{
    using static PlayerAbilityStats;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class TromboneAbilities : PlayerAbilities
    {
        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private TromboneProjectile primaryProjectilePrefab;
        [SerializeField] private TromboneProjectile specialProjectilePrefab;
        [SerializeField] private Reverberation specialReverberationPrefab;
        [SerializeField] private TromboneCommandZone commandZonePrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip primarySound;
        [SerializeField] private AudioClip secondarySound;
        [SerializeField] private AudioClip defensiveSound;
        [SerializeField] private AudioClip specialSound;

        private TromboneCommandZone _commandZone;
        private TromboneWeaponVisual _weaponVisual;

        private float _primaryReadyAt;
        private float _primaryCooldownDuration;
        private float _secondaryReadyAt;
        private float _defensiveReadyAt;
        private float _specialReadyAt;

        private float _activeUntil;
        private float _guardUntil;
        private float _pressure;
        private bool _primaryResonates;

        private bool _specialPending;
        private float _specialReleaseAt;
        private float _specialWindupDuration;
        private float _specialRecoveryDuration;
        private float _specialDamage;
        private float _specialPressure;
        private Vector3 _specialDirection;

        private Vector3 _previousPosition;
        private bool _skipMovementSample;

        private float ActionTime => Player.Hitstop.ActionTime;

        private Vector3 ProjectilePosition => projectileOrigin != null
            ? projectileOrigin.position : transform.position + Vector3.up * 0.25f;

        private bool HasControl => Player.isActiveAndEnabled && Player.HasActiveInput && !Player.Health.IsDowned;

        private bool CanActivate => isActiveAndEnabled && HasControl && !Player.Movement.IsDodging
            && !Player.IsHitStunned && !Player.Hitstop.IsStopped && !BlocksAbilities;

        public float Pressure => _pressure;
        public float PressureNormalized => _pressure / tuning.pressure.maximum;

        public bool IsInCommandZone => _commandZone != null && _commandZone.IsOwnerInside;
        public bool IsGuarding => _guardUntil > ActionTime;
        public bool PrimaryResonates => _primaryResonates;

        public bool IsPrimaryReady => CanActivate && CooldownReady(_primaryReadyAt);

        public bool IsPreparingSpecial => _specialPending;
        public float SpecialWindupProgress => _specialPending
            ? 1f - Mathf.Clamp01((_specialReleaseAt - ActionTime) / _specialWindupDuration) : 0f;

        public override float MovementSpeedMultiplier => IsGuarding ? tuning.defensive.movementSpeedMultiplier : 1f;

        private void OnEnable()
        {
            Player.Initialize();
            _weaponVisual = GetComponent<TromboneWeaponVisual>();

            Player.Health.AbilityHitAvoided += HandleGuardedHit;
            SceneManager.activeSceneChanged += HandleSceneChanged;

            _primaryCooldownDuration = tuning.primary.cooldown;
            ResetMovementTracking();
        }

        protected override void OnDisable()
        {
            Player.Health.AbilityHitAvoided -= HandleGuardedHit;
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            ClearCommandZone();
            _pressure = 0f;
            base.OnDisable();
        }

        private void Update()
        {
            if (!HasControl)
            {
                Cancel();
                ClearCommandZone();
                return;
            }

            if (_specialPending && ActionTime >= _specialReleaseAt) ReleaseSpecial();
            if (_activeUntil > 0f && ActionTime >= _activeUntil) EndAction();
            if (_guardUntil > 0f && !IsGuarding) EndGuard();
        }

        private void LateUpdate()
        {
            if (!HasControl)
            {
                ResetMovementTracking();
                return;
            }

            // Sample actual XZ displacement after movement, using the same approach as Flute Flow.
            var movement = transform.position - _previousPosition;
            movement.y = 0f;
            _previousPosition = transform.position;

            if (_skipMovementSample)
            {
                _skipMovementSample = false;
                return;
            }

            var settings = tuning.pressure;
            var deltaTime = Player.Hitstop.ActionDeltaTime;

            if (Player.Hitstop.IsStopped || deltaTime <= 0f
                || movement.sqrMagnitude > settings.teleportDistance * settings.teleportDistance) return;

            var moving = Player.Movement.IsDodging
                || movement.magnitude / deltaTime >= settings.minimumMovementSpeed;
            var inZone = IsInCommandZone;
            var decayScale = inZone ? tuning.commandZone.pressureDecayMultiplier : 1f;

            float rate;
            if (moving)
            {
                rate = -settings.decayPerSecond * decayScale;
            }
            else
            {
                var gainScale = inZone ? tuning.commandZone.pressureAccumulationMultiplier : 1f;
                rate = settings.stationaryGainPerSecond * gainScale - settings.idleDecayPerSecond * decayScale;
            }

            _pressure = Mathf.Clamp(_pressure + rate * deltaTime, 0f, settings.maximum);
        }

        private void ResetMovementTracking()
        {
            _previousPosition = transform.position;
            _skipMovementSample = true;
        }

        private void PlaySound(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
        }

        // This gives each accepted shot a fixed power/interval pair even as Pressure changes afterward.
        public override bool TryUsePrimary()
        {
            if (!IsPrimaryReady) return false;

            var pressure = PressureNormalized;
            var direction = GetDirection(primaryProjectilePrefab);
            var resonates = _primaryResonates;
            var projectile = Instantiate(primaryProjectilePrefab, ProjectilePosition, Quaternion.LookRotation(direction));
            var damage = Mathf.Lerp(tuning.primary.minimumDamage, tuning.primary.maximumDamage, pressure);

            projectile.InitializePrimary(Player, direction, damage, resonates, primaryProjectilePrefab, pressure);
            PlaySound(primarySound);

            _primaryResonates = false;

            _primaryCooldownDuration = Mathf.Lerp(tuning.primary.cooldown, tuning.primary.maximumPressureCooldown, pressure);

            BeginAction(ref _primaryReadyAt, _primaryCooldownDuration, blockMovement: true, blockDodge: false);
            AccumulatePressure();

            if (_weaponVisual != null) _weaponVisual.PlayShot(direction, ProjectilePosition, special: false,
                resonates: resonates, interval: _primaryCooldownDuration, pressure: pressure);

            return true;
        }

        public override bool TryUseSecondary()
        {
            if (!CanActivate || !CooldownReady(_secondaryReadyAt)
                || (_commandZone != null && _commandZone.IsActive)) return false;

            _commandZone = Instantiate(commandZonePrefab, transform.position, Quaternion.identity);

            _commandZone.Initialize(Player);

            PlaySound(secondarySound);
            BeginAction(ref _secondaryReadyAt, tuning.commandZone.cooldown, blockMovement: false, blockDodge: false);

            if (_weaponVisual != null) _weaponVisual.PlayZone(Player.AimDirection);

            return true;
        }

        public override bool TryUseDefensive()
        {
            if (!CanActivate || IsGuarding || !CooldownReady(_defensiveReadyAt)) return false;

            _guardUntil = ActionTime + tuning.defensive.guardLifespan;
            PlaySound(defensiveSound);

            Player.Health.GrantAbilityInvulnerability(tuning.defensive.guardLifespan);
            BeginAction(ref _defensiveReadyAt, tuning.defensive.cooldown, blockMovement: false, blockDodge: true);

            if (_weaponVisual != null) _weaponVisual.PlayGuard(Player.AimDirection);

            return true;
        }

        // Lock in Special's aim and damage, start its cooldown, then wind up before firing.
        // An interruption during windup cancels the shot and keeps the spent cooldown.
        public override bool TryUseSpecial()
        {
            if (!CanActivate || !CooldownReady(_specialReadyAt)) return false;

            EndGuard();

            _specialDirection = GetDirection(specialProjectilePrefab);
            _specialPressure = PressureNormalized;
            _specialDamage = Mathf.Lerp(tuning.special.minimumDamage, tuning.special.maximumDamage, _specialPressure);
            _specialWindupDuration = tuning.special.windup;
            _specialRecoveryDuration = tuning.special.recovery;
            _specialReleaseAt = ActionTime + _specialWindupDuration;
            _specialPending = true;


            BeginAction(ref _specialReadyAt, tuning.special.cooldown, blockMovement: true, blockDodge: true);
            _activeUntil = _specialReleaseAt + _specialRecoveryDuration;

            if (_weaponVisual != null) _weaponVisual.PlaySpecialWindup(_specialDirection);

            return true;
        }

        private void ReleaseSpecial()
        {
            _specialPending = false;

            var projectile = Instantiate(specialProjectilePrefab, ProjectilePosition, Quaternion.LookRotation(_specialDirection));
            projectile.InitializeSpecial(Player, _specialDirection, _specialDamage, specialReverberationPrefab,
                _specialDamage * tuning.specialReverberationStrengthMultiplier, specialProjectilePrefab, _specialPressure);

            PlaySound(specialSound);

            var consumedZone = IsInCommandZone;
            if (consumedZone)
            {
                _commandZone.ConsumeZone();
                _commandZone = null;
            }

            if (_weaponVisual != null) _weaponVisual.PlayShot(_specialDirection, ProjectilePosition, special: true,
                resonates: false, interval: _specialRecoveryDuration,
                pressure: _specialPressure,
                consumedZone: consumedZone);
        }

        private Vector3 GetDirection(TromboneProjectile prefab) => Player.GetAttackDirection(
            ProjectilePosition, prefab.MaximumTravelDistance, prefab.CollisionMask, prefab.HitVolume, prefab.ResonatingAttack);

        private bool CooldownReady(float readyAt) => IgnoreCooldowns || Time.time >= readyAt;

        private void BeginAction(ref float readyAt, float cooldown, bool blockMovement, bool blockDodge)
        {
            readyAt = IgnoreCooldowns ? 0f : Time.time + cooldown;
            _activeUntil = ActionTime + tuning.actionDuration;
            BlocksMovement = blockMovement;
            BlocksDodge = blockDodge;
            BlocksAbilities = true;
        }

        private void AccumulatePressure()
        {
            var multiplier = IsInCommandZone ? tuning.commandZone.pressureAccumulationMultiplier : 1f;
            _pressure = Mathf.Clamp(_pressure + tuning.pressure.gainPerAttack * multiplier, 0f, tuning.pressure.maximum);
        }

        private void HandleGuardedHit()
        {
            if (!IsGuarding) return;

            if (IsInCommandZone) _primaryResonates = true;

            if (_weaponVisual != null) _weaponVisual.PlayGuardImpact(IsInCommandZone);

            EndGuard();
        }

        private void EndGuard()
        {
            _guardUntil = 0f;
            Player.Health.ClearAbilityInvulnerability();
        }

        private void EndAction()
        {
            _activeUntil = 0f;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }

        private void ClearCommandZone()
        {
            if (_commandZone != null) _commandZone.CancelZone();
            _commandZone = null;
        }

        public override void Cancel()
        {
            // Interruption releases the active action while preserving combat momentum and spent cooldowns.
            // Downing also clears encounter resources, to make sure revival starts with new Pressure and zone state.
            if (_weaponVisual != null) _weaponVisual.Cancel();
            _specialPending = false;
            EndAction();
            EndGuard();
            _primaryResonates = false;

            if (Player.Health.IsDowned)
            {
                _pressure = 0f;
                ClearCommandZone();
            }
        }

        public override PlayerAbilityStats GetAbilityStats(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Primary => new(("Damage by Pressure", Range(tuning.primary.minimumDamage, tuning.primary.maximumDamage)),
                    ("Cooldown by Pressure", Range(tuning.primary.maximumPressureCooldown, tuning.primary.cooldown) + " s")),
                PlayerAbility.Secondary => new(("Zone radius", Number(tuning.commandZone.radius)),
                    ("Cooldown", Seconds(tuning.commandZone.cooldown))),
                PlayerAbility.Special => new(("Damage by Pressure", Range(tuning.special.minimumDamage, tuning.special.maximumDamage)),
                    ("Cooldown", Seconds(tuning.special.cooldown))),
                PlayerAbility.Defensive => new(("Guard duration", Seconds(tuning.defensive.guardLifespan)),
                    ("Cooldown", Seconds(tuning.defensive.cooldown))),
                _ => default
            };
        }

        public override PlayerAbilityState GetAbilityState(PlayerAbility ability) => ability switch
        {
            PlayerAbility.Primary
                => new PlayerAbilityState(_primaryReadyAt - Time.time, _primaryCooldownDuration),
            PlayerAbility.Secondary
                => new PlayerAbilityState(_secondaryReadyAt - Time.time, tuning.commandZone.cooldown),
            PlayerAbility.Defensive
                => new PlayerAbilityState(_defensiveReadyAt - Time.time, tuning.defensive.cooldown),
            PlayerAbility.Special
                => new PlayerAbilityState(_specialReadyAt - Time.time, tuning.special.cooldown),
            _ => default
        };

        public override void ClearCooldowns()
        {
            _primaryReadyAt = _secondaryReadyAt = _defensiveReadyAt = _specialReadyAt = 0f;
        }

        public override void ResetForEncounter()
        {
            base.ResetForEncounter();
            ClearCommandZone();
            _pressure = 0f;
            ResetMovementTracking();
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            Cancel();
            ClearCommandZone();
            ClearCooldowns();
            _pressure = 0f;
            ResetMovementTracking();
        }
    }
}
