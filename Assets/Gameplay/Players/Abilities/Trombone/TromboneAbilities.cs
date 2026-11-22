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
        private Vector3 _previousPosition;
        private bool _skipMovementSample;

        private float ActionTime => Player.Hitstop.ActionTime;
        private Vector3 ProjectilePosition => projectileOrigin != null
            ? projectileOrigin.position : transform.position + Vector3.up * 0.25f;
        private bool HasControl => Player.isActiveAndEnabled && Player.HasActiveInput && !Player.Health.IsDowned;
        private bool CanActivate => isActiveAndEnabled && HasControl && !Player.Movement.IsDodging
            && !Player.Hitstop.IsStopped && !BlocksAbilities;

        public float Pressure => _pressure;
        public float PressureNormalized => tuning != null
            ? Mathf.Clamp01(_pressure / Mathf.Max(0.01f, tuning.pressure.maximum)) : 0f;
        public bool IsInCommandZone => _commandZone != null && _commandZone.IsOwnerInside;
        public bool IsGuarding => _guardUntil > ActionTime;
        public bool PrimaryResonates => _primaryResonates;
        public override float MovementSpeedMultiplier => IsGuarding ? tuning.defensive.movementSpeedMultiplier : 1f;

        private void OnEnable()
        {
            Player.Initialize();
            _weaponVisual = GetComponent<TromboneWeaponVisual>();
            if (tuning == null)
            {
                Debug.LogError("TromboneAbilities requires Trombone tuning.", this);
                enabled = false;
                return;
            }

            Player.Health.AbilityHitAvoided += HandleGuardedHit;
            SceneManager.activeSceneChanged += HandleSceneChanged;
            _primaryCooldownDuration = Mathf.Max(0.01f, tuning.primary.cooldown);
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

            // Completing the short cast must not cancel an independently active guard.
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
            var rate = moving
                ? -settings.decayPerSecond * (inZone ? tuning.commandZone.pressureDecayMultiplier : 1f)
                : settings.stationaryGainPerSecond * (inZone ? tuning.commandZone.pressureAccumulationMultiplier : 1f);
            _pressure = Mathf.Clamp(_pressure + rate * deltaTime, 0f, settings.maximum);
        }

        private void ResetMovementTracking()
        {
            _previousPosition = transform.position;
            _skipMovementSample = true;
        }

        public override bool TryUsePrimary()
        {
            if (!CanActivate || primaryProjectilePrefab == null || !primaryProjectilePrefab.IsConfigured
                || !CooldownReady(_primaryReadyAt)) return false;

            var pressure = PressureNormalized;
            var direction = GetDirection(primaryProjectilePrefab);
            var resonates = _primaryResonates;
            var projectile = Instantiate(primaryProjectilePrefab, ProjectilePosition, Quaternion.LookRotation(direction));
            var damage = Mathf.Lerp(tuning.primary.minimumDamage, tuning.primary.maximumDamage, pressure);
            projectile.InitializePrimary(Player, direction, damage, _primaryResonates, primaryProjectilePrefab);
            _primaryResonates = false;
            var cooldown = Mathf.Lerp(tuning.primary.cooldown,
                Mathf.Min(tuning.primary.cooldown, tuning.primary.maximumPressureCooldown), pressure);
            _primaryCooldownDuration = Mathf.Max(0.01f, cooldown);
            BeginAction(ref _primaryReadyAt, _primaryCooldownDuration, blockMovement: true, blockDodge: false);
            AccumulatePressure();
            if (_weaponVisual != null) _weaponVisual.PlayShot(direction, ProjectilePosition, special: false, resonates: resonates, interval: cooldown);
            return true;
        }

        public override bool TryUseSecondary()
        {
            if (!CanActivate || !CooldownReady(_secondaryReadyAt)
                || _commandZone != null
                || commandZonePrefab == null || !commandZonePrefab.IsConfigured) return false;

            _commandZone = Instantiate(commandZonePrefab, transform.position, Quaternion.identity);
            _commandZone.Ended += HandleCommandZoneEnded;
            _commandZone.Initialize(Player);
            BeginAction(blockMovement: false, blockDodge: false);
            if (_weaponVisual != null) _weaponVisual.PlayZone(Player.AimDirection);
            return true;
        }

        public override bool TryUseDefensive()
        {
            if (!CanActivate || IsGuarding || !CooldownReady(_defensiveReadyAt)) return false;

            _guardUntil = ActionTime + Mathf.Max(0.01f, tuning.defensive.guardLifespan);
            Player.Health.GrantAbilityInvulnerability(tuning.defensive.guardLifespan);
            BeginAction(ref _defensiveReadyAt, tuning.defensive.cooldown, blockMovement: false, blockDodge: true);
            if (_weaponVisual != null) _weaponVisual.PlayGuard(Player.AimDirection);
            return true;
        }

        public override bool TryUseSpecial()
        {
            if (!CanActivate || !CooldownReady(_specialReadyAt)
                || specialProjectilePrefab == null || !specialProjectilePrefab.IsConfigured
                || specialReverberationPrefab == null || !specialReverberationPrefab.enabled
                || !specialReverberationPrefab.gameObject.activeSelf || specialReverberationPrefab.Effect == null) return false;

            var direction = GetDirection(specialProjectilePrefab);
            var projectile = Instantiate(specialProjectilePrefab, ProjectilePosition, Quaternion.LookRotation(direction));
            var damage = Mathf.Lerp(tuning.special.minimumDamage, tuning.special.maximumDamage, PressureNormalized);
            projectile.InitializeSpecial(Player, direction, damage, specialReverberationPrefab,
                Mathf.Max(0.01f, damage * tuning.specialReverberationStrengthMultiplier), specialProjectilePrefab);
            if (_commandZone != null) _commandZone.ConsumeZone();
            BeginAction(ref _specialReadyAt, tuning.special.cooldown, blockMovement: true, blockDodge: true);
            AccumulatePressure();
            if (_weaponVisual != null) _weaponVisual.PlayShot(direction, ProjectilePosition, special: true, resonates: false, interval: tuning.special.cooldown);
            return true;
        }

        private Vector3 GetDirection(TromboneProjectile prefab) => Player.GetAttackDirection(
            ProjectilePosition, prefab.MaximumTravelDistance, prefab.CollisionMask, prefab.HitVolume, prefab.ResonatingAttack);

        private bool CooldownReady(float readyAt) => IgnoreCooldowns || Time.time >= readyAt;

        private void BeginAction(ref float readyAt, float cooldown, bool blockMovement, bool blockDodge)
        {
            readyAt = IgnoreCooldowns ? 0f : Time.time + cooldown;
            BeginAction(blockMovement, blockDodge);
        }

        private void BeginAction(bool blockMovement, bool blockDodge)
        {
            _activeUntil = ActionTime + Mathf.Max(0.01f, tuning.actionDuration);
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
        }

        private void HandleCommandZoneEnded(TromboneCommandZone zone)
        {
            zone.Ended -= HandleCommandZoneEnded;
            _commandZone = null;
            _secondaryReadyAt = IgnoreCooldowns ? 0f : Time.time + tuning.commandZone.cooldown;
        }

        public override void Cancel()
        {
            if (_weaponVisual != null) _weaponVisual.Cancel();
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
            if (tuning == null || !GetAbilityState(ability).Supported) return default;
            if (ability == PlayerAbility.Primary)
            {
                var slowest = Mathf.Max(0.01f, tuning.primary.cooldown);
                var fastest = Mathf.Clamp(tuning.primary.maximumPressureCooldown, 0.01f, slowest);
                return new PlayerAbilityStats(
                    ("DMG", Range(tuning.primary.minimumDamage, tuning.primary.maximumDamage)),
                    ("Fire rate", Range(1f / slowest, 1f / fastest) + "/s"));
            }

            return ability switch
            {
                PlayerAbility.Secondary => new(Cooldown(tuning.commandZone.cooldown)),
                PlayerAbility.Special => new(("DMG", Range(tuning.special.minimumDamage, tuning.special.maximumDamage)),
                    Cooldown(tuning.special.cooldown)),
                PlayerAbility.Defensive => new(("Guard duration", Seconds(tuning.defensive.guardLifespan)),
                    Cooldown(tuning.defensive.cooldown)),
                _ => default
            };
        }

        public override PlayerAbilityState GetAbilityState(PlayerAbility ability) => ability switch
        {
            PlayerAbility.Primary when tuning != null && primaryProjectilePrefab != null && primaryProjectilePrefab.IsConfigured
                => new PlayerAbilityState(_primaryReadyAt - Time.time, _primaryCooldownDuration),
            PlayerAbility.Secondary when tuning != null && commandZonePrefab != null && commandZonePrefab.IsConfigured
                => new PlayerAbilityState(_secondaryReadyAt - Time.time, tuning.commandZone.cooldown),
            PlayerAbility.Defensive when tuning != null
                => new PlayerAbilityState(_defensiveReadyAt - Time.time, tuning.defensive.cooldown),
            PlayerAbility.Special when tuning != null && specialProjectilePrefab != null && specialProjectilePrefab.IsConfigured
                && specialReverberationPrefab != null && specialReverberationPrefab.enabled
                && specialReverberationPrefab.gameObject.activeSelf && specialReverberationPrefab.Effect != null
                => new PlayerAbilityState(_specialReadyAt - Time.time, tuning.special.cooldown),
            _ => default
        };

        public override void ClearCooldowns()
        {
            _primaryReadyAt = _secondaryReadyAt = _defensiveReadyAt = _specialReadyAt = 0f;
        }

        public override void ResetForEncounter()
        {
            ClearCommandZone();
            base.ResetForEncounter();
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
