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
        private float _primaryReadyAt;
        private float _secondaryReadyAt;
        private float _defensiveReadyAt;
        private float _specialReadyAt;
        private float _activeUntil;
        private float _guardUntil;
        private float _lastAttackAt = float.NegativeInfinity;
        private float _pressure;
        private bool _primaryResonates;
        private Vector3 _previousPosition;

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
            if (tuning == null)
            {
                Debug.LogError("TromboneAbilities requires Trombone tuning.", this);
                enabled = false;
                return;
            }

            Player.Health.AbilityHitAvoided += HandleGuardedHit;
            SceneManager.activeSceneChanged += HandleSceneChanged;
            _previousPosition = transform.position;
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
                _previousPosition = transform.position;
                return;
            }

            // Completing the short cast must not cancel an independently active guard.
            if (_activeUntil > 0f && ActionTime >= _activeUntil) EndAction();
            if (_guardUntil > 0f && !IsGuarding) EndGuard();

            var movement = transform.position - _previousPosition;
            movement.y = 0f;
            _previousPosition = transform.position;
            if (Time.time - _lastAttackAt >= tuning.pressure.consecutiveAttackInterval
                || movement.sqrMagnitude > 0.000001f)
            {
                var multiplier = IsInCommandZone ? tuning.commandZone.pressureDecayMultiplier : 1f;
                _pressure = Mathf.Max(0f, _pressure - tuning.pressure.decayPerSecond * multiplier * Time.deltaTime);
            }
        }

        public override bool TryUsePrimary()
        {
            if (!CanActivate || primaryProjectilePrefab == null || !primaryProjectilePrefab.IsConfigured
                || !CooldownReady(_primaryReadyAt)) return false;

            var pressure = PressureNormalized;
            var direction = GetDirection(primaryProjectilePrefab);
            var projectile = Instantiate(primaryProjectilePrefab, ProjectilePosition, Quaternion.LookRotation(direction));
            var damage = Mathf.Lerp(tuning.primary.minimumDamage, tuning.primary.maximumDamage, pressure);
            projectile.InitializePrimary(Player, direction, damage, _primaryResonates);
            _primaryResonates = false;
            var cooldown = Mathf.Lerp(tuning.primary.cooldown,
                Mathf.Min(tuning.primary.cooldown, tuning.primary.maximumPressureCooldown), pressure);
            BeginAction(ref _primaryReadyAt, Mathf.Max(0.01f, cooldown), blockMovement: true, blockDodge: false);
            AccumulatePressure();
            return true;
        }

        public override bool TryUseSecondary()
        {
            if (!CanActivate || !CooldownReady(_secondaryReadyAt)
                || (_commandZone != null && _commandZone.IsActive)
                || commandZonePrefab == null || !commandZonePrefab.IsConfigured) return false;

            _commandZone = Instantiate(commandZonePrefab, transform.position, Quaternion.identity);
            _commandZone.Initialize(Player);
            BeginAction(ref _secondaryReadyAt, tuning.commandZone.cooldown, blockMovement: false, blockDodge: false);
            return true;
        }

        public override bool TryUseDefensive()
        {
            if (!CanActivate || IsGuarding || !CooldownReady(_defensiveReadyAt)) return false;

            _guardUntil = ActionTime + Mathf.Max(0.01f, tuning.defensive.guardLifespan);
            Player.Health.GrantAbilityInvulnerability(tuning.defensive.guardLifespan);
            BeginAction(ref _defensiveReadyAt, tuning.defensive.cooldown, blockMovement: false, blockDodge: true);
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
                Mathf.Max(0.01f, damage * tuning.specialReverberationStrengthMultiplier));
            if (_commandZone != null) _commandZone.ConsumeZone();
            _commandZone = null;
            BeginAction(ref _specialReadyAt, tuning.special.cooldown, blockMovement: true, blockDodge: true);
            AccumulatePressure();
            return true;
        }

        private Vector3 GetDirection(TromboneProjectile prefab) => Player.GetAttackDirection(
            ProjectilePosition, prefab.MaximumTravelDistance, prefab.CollisionMask, prefab.HitVolume, prefab.ResonatingAttack);

        private bool CooldownReady(float readyAt) => IgnoreCooldowns || Time.time >= readyAt;

        private void BeginAction(ref float readyAt, float cooldown, bool blockMovement, bool blockDodge)
        {
            readyAt = IgnoreCooldowns ? 0f : Time.time + cooldown;
            _activeUntil = ActionTime + Mathf.Max(0.01f, tuning.actionDuration);
            BlocksMovement = blockMovement;
            BlocksDodge = blockDodge;
            BlocksAbilities = true;
        }

        private void AccumulatePressure()
        {
            var multiplier = IsInCommandZone ? tuning.commandZone.pressureAccumulationMultiplier : 1f;
            _pressure = Mathf.Clamp(_pressure + tuning.pressure.gainPerAttack * multiplier, 0f, tuning.pressure.maximum);
            _lastAttackAt = Time.time;
        }

        private void HandleGuardedHit()
        {
            if (!IsGuarding) return;
            if (IsInCommandZone) _primaryResonates = true;
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
            return ability switch
            {
                PlayerAbility.Primary => new(("Damage by Pressure", Range(tuning.primary.minimumDamage, tuning.primary.maximumDamage)),
                    ("Cooldown by Pressure", Range(Mathf.Min(tuning.primary.cooldown, tuning.primary.maximumPressureCooldown), tuning.primary.cooldown) + " s")),
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
            PlayerAbility.Primary when tuning != null && primaryProjectilePrefab != null && primaryProjectilePrefab.IsConfigured
                => new PlayerAbilityState(_primaryReadyAt - Time.time, tuning.primary.cooldown),
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

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            Cancel();
            ClearCommandZone();
            ClearCooldowns();
            _pressure = 0f;
            _lastAttackAt = float.NegativeInfinity;
            _previousPosition = transform.position;
        }
    }
}
