namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // snapshot the impact after player movement; presentation never determines hit coverage.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class GuitarAbilities : PlayerAbilities
    {
        private enum GuitarAction
        {
            None,
            Primary,
            Secondary,
            Defensive,
            Special
        }

        private GuitarAction _action = GuitarAction.None;

        [SerializeField] private GuitarWeaponVisual weaponVisual;
        [SerializeField] private GuitarVFX vfx;
        [SerializeField] private GuitarTuning tuning;

        private readonly HashSet<IHittable> _hitThisSwing = new();
        private readonly List<CombatHurtbox> _hurtboxBuffer = new();
        private Collider[] _hitBuffer = new Collider[32];
        private GuitarTuning.Swing _swing;
        private Vector3 _direction;
        private Vector3 _impactOrigin;
        private float _startedAt;
        private float _readyAt;
        private float _comboExpiresAt;
        private int _nextComboIndex;
        private float _swingSign;
        private float _swingReach;
        private float _swingSweetSpotRadius;
        private Vector3 _swingStretchAxis;
        private float _swingDepthStretch;
        private bool _activeFinished;
        private bool _impactResolved;
        private float _specialReadyAt;
        private float _specialWindup, _specialActive, _specialRecovery, _specialImpactFraction;
        private float _specialDistance, _specialRadius, _specialSweetSpotRadius, _specialHeight;
        private float _defensiveReadyAt;
        private bool _strumReleased;
        private float _strumWindup;
        private float _strumActive;
        private float _strumRecovery;
        private float _secondaryReadyAt;
        private float _empowerMultiplier = 1f;
        private float _damageMultiplier = 1f;

        public bool IsAttacking => _action != GuitarAction.None;
        public bool IsStrumming => _action == GuitarAction.Secondary;
        public bool IsDefending => _action == GuitarAction.Defensive;
        public float DefensiveCooldownRemaining => Mathf.Max(0f, _defensiveReadyAt - Time.time);
        public override bool BlocksDefensive => IsAttacking && (IsDefending || AnimationPhase == PlayerAttackPhase.Active);
        public override PlayerAttackPhase AnimationPhase
        {
            get
            {
                if (!IsAttacking) return PlayerAttackPhase.None;

                var elapsed = Player.Hitstop.ActionTime - _startedAt;
                var windup = _action == GuitarAction.Special ? _specialWindup : IsStrumming ? _strumWindup : _swing.windup;
                var active = _action == GuitarAction.Special ? _specialActive : IsStrumming ? _strumActive : _swing.active;
                if (elapsed < windup) return PlayerAttackPhase.Windup;
                return elapsed < windup + active ? PlayerAttackPhase.Active : PlayerAttackPhase.Recovery;
            }
        }

        public override float MovementSpeedMultiplier => !IsAttacking ? 1f
            : _action == GuitarAction.Special ? tuning.special.movementSpeedMultiplier
            : IsStrumming ? tuning.secondary.movementSpeedMultiplier
            : IsDefending ? tuning.defensive.movementSpeedMultiplier : tuning.attack.movementSpeedMultiplier;
        public bool HasEmpoweredAttack { get; private set; }
        public float SpecialCooldownRemaining => Mathf.Max(0f, _specialReadyAt - Time.time);
        public float SecondaryCooldownRemaining => Mathf.Max(0f, _secondaryReadyAt - Time.time);
        public int ComboStep { get; private set; }
        public float PrimaryCooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);

        public override PlayerAbilityState GetAbilityState(PlayerAbility ability)
        {
            if (tuning == null) return default;
            return ability switch
            {
                PlayerAbility.Primary => new PlayerAbilityState(PrimaryCooldownRemaining,
                    _action == GuitarAction.Primary ? _swing.windup + _swing.active + _swing.recovery : tuning.attack.comboCooldown),
                PlayerAbility.Secondary => new PlayerAbilityState(SecondaryCooldownRemaining, tuning.secondary.cooldown),
                PlayerAbility.Defensive => new PlayerAbilityState(DefensiveCooldownRemaining, tuning.defensive.cooldown),
                PlayerAbility.Special => new PlayerAbilityState(SpecialCooldownRemaining, tuning.special.cooldown),
                _ => default
            };
        }

        public override void ClearCooldowns()
        {
            _readyAt = _secondaryReadyAt = _defensiveReadyAt = _specialReadyAt = 0f;
        }

        private void Awake()
        {
            if (weaponVisual == null) weaponVisual = GetComponent<GuitarWeaponVisual>();
            if (vfx == null) vfx = GetComponent<GuitarVFX>();
        }

        private void OnEnable()
        {
            if (tuning == null)
            {
                Debug.LogError("GuitarAbilities requires a GuitarTuning asset.", this);
                enabled = false;
                return;
            }
        }

        public override bool TryUsePrimary()
        {
            if (!isActiveAndEnabled) return false;
            var attack = tuning.attack;
            if (!Player.CanUseAbilities || IsAttacking || (!IgnoreCooldowns && Time.time < _readyAt)
                || attack.combo == null || attack.combo.Length == 0) return false;

            if (Time.time > _comboExpiresAt) _nextComboIndex = 0;
            _nextComboIndex = Mathf.Clamp(_nextComboIndex, 0, attack.combo.Length - 1);
            _swing = attack.combo[_nextComboIndex];
            _swing.windup = Mathf.Max(0.01f, _swing.windup);
            _swing.active = Mathf.Max(0.01f, _swing.active);
            _swing.recovery = Mathf.Max(0.01f, _swing.recovery);
            // older serialized combos have no multiplier; keep their original reach.
            var reachScale = _swing.reachMultiplier > 0f ? _swing.reachMultiplier : 1f;
            _swingReach = attack.reach * reachScale;
            _swingSweetSpotRadius = attack.sweetSpotRadius * reachScale;
            // the attack shape stays in arena space; opted-in hurtboxes are compared through the gameplay camera.
            _swingStretchAxis = new Vector3(attack.stretchAxis.x, 0f, attack.stretchAxis.y);
            _swingStretchAxis = _swingStretchAxis.sqrMagnitude > 0.0001f ? _swingStretchAxis.normalized : Vector3.forward;
            _swingDepthStretch = Mathf.Max(1f, attack.depthStretch);
            if (_swing.lungeDistance > 0f && (!Player.Movement.isActiveAndEnabled
                || !GetComponent<CharacterController>().enabled || Player.Movement.IsDisplacing)) return false;
            _direction = Player.GetAttackDirection();
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;

            // request the whole lunge here so movement and attack phases share the same start time.
            if (_swing.lungeDistance > 0f && !Player.Movement.TryStartDisplacement(
                _direction, _swing.lungeDistance, _swing.active, attack.lungeEasing, _swing.windup)) return false;

            // spend empowerment only after activation succeeds; it applies to the whole next strike.
            _damageMultiplier = HasEmpoweredAttack ? _empowerMultiplier : 1f;
            HasEmpoweredAttack = false;
            ComboStep = _nextComboIndex + 1;
            _swingSign = _nextComboIndex % 2 == 0 ? 1f : -1f;
            _startedAt = Player.Hitstop.ActionTime;
            _readyAt = IgnoreCooldowns ? 0f : Time.time + _swing.windup + _swing.active + _swing.recovery;
            _activeFinished = false;
            _impactResolved = false;
            _hitThisSwing.Clear();
            _action = GuitarAction.Primary;
            BlocksMovement = false;
            BlocksDodge = true;
            BlocksAbilities = true;
            return true;
        }

        private void Update()
        {
            if (IsAttacking && (Player.Health.IsDowned || !Player.HasActiveInput))
            {
                Cancel();
                return;
            }
            if (IsAttacking && Player.Hitstop.IsStopped) return;

            switch (_action)
            {
                case GuitarAction.None:
                    if (_nextComboIndex > 0 && Time.time > _comboExpiresAt)
                    {
                        _nextComboIndex = 0;
                        if (weaponVisual != null) weaponVisual.Cancel();
                    }
                    break;
                case GuitarAction.Primary:
                case GuitarAction.Defensive:
                    UpdateSweep(Player.Hitstop.ActionTime - _startedAt);
                    break;
                case GuitarAction.Secondary:
                    UpdateSecondary(Player.Hitstop.ActionTime - _startedAt);
                    break;
                case GuitarAction.Special:
                    UpdateSpecial(Player.Hitstop.ActionTime - _startedAt);
                    break;
            }
        }

        private void UpdateSweep(float elapsed)
        {
            var attack = tuning.attack;
            // primary and defensive share sweep resolution; defensive supplies its own tuning and has no sweet band.
            var defensive = tuning.defensive;
            var height = IsDefending ? defensive.attackHeight : attack.attackHeight;
            var innerRadius = IsDefending ? defensive.innerRadius : attack.innerRadius;
            var impactFraction = IsDefending ? defensive.impactFraction : attack.impactFraction;
            var damage = IsDefending ? defensive.damage : attack.damage;
            var sweetMultiplier = IsDefending ? 1f : attack.sweetSpotMultiplier;
            var hitMask = IsDefending ? defensive.hitMask : attack.hitMask;
            var hitThickness = Mathf.Max(0.001f, IsDefending ? defensive.hitThickness : attack.hitThickness);
            var windupEasing = IsDefending ? defensive.windupEasing : attack.windupEasing;
            var swingEasing = IsDefending ? defensive.swingEasing : attack.swingEasing;
            var recoveryEasing = IsDefending ? defensive.recoveryEasing : attack.recoveryEasing;
            var anticipationAngle = IsDefending ? defensive.anticipationAngle : attack.anticipationAngle;
            var windupScale = IsDefending ? defensive.windupScale : attack.windupScale;
            var swingScale = IsDefending ? defensive.swingScale : attack.swingScale;
            var origin = transform.position + Vector3.up * height;
            var startAngle = -_swing.angle * 0.5f * _swingSign;

            // -----------------------------
            // windup and active sweep
            // -----------------------------
            if (elapsed < _swing.windup)
            {
                var progress = windupEasing.Evaluate(Mathf.Clamp01(elapsed / _swing.windup));
                var angle = startAngle - anticipationAngle * _swingSign * Mathf.Sin(progress * Mathf.PI);
                if (weaponVisual != null)
                    weaponVisual.SetSwingPose(_direction, angle, height, false, windupScale);
                return;
            }

            var activeProgress = Mathf.Clamp01((elapsed - _swing.windup) / _swing.active);
            var currentAngle = Mathf.LerpUnclamped(startAngle, -startAngle, swingEasing.Evaluate(activeProgress));
            if (!_activeFinished && weaponVisual != null)
                weaponVisual.SetSwingPose(_direction, currentAngle, height, true, swingScale);

            // the entire sector resolves once, even when a slow frame crosses the impact time.
            // later lunging, weapon motion and smear poses cannot move or repeat this hit area.
            if (!_impactResolved && elapsed >= _swing.windup + _swing.active * Mathf.Clamp01(impactFraction))
            {
                _impactResolved = true;
                var hitstopDuration = 0f;
                _impactOrigin = origin;
                Player.Session?.Debug.RecordAttack(new DebugAttackSnapshot(origin, _direction, innerRadius, _swingReach,
                    _swing.angle, _swingStretchAxis, _swingDepthStretch, IsDefending ? -1f : _swingSweetSpotRadius));
                if (vfx != null)
                    vfx.BeginSwing(origin, _direction, startAngle, -startAngle, innerRadius, _swingReach,
                        _swingSweetSpotRadius, _swing.active, _swingStretchAxis, _swingDepthStretch);

                // snapshot the registry because a hit callback can disable or destroy another hurtbox.
                _hurtboxBuffer.Clear();
                foreach (var hurtbox in CombatHurtbox.Active) _hurtboxBuffer.Add(hurtbox);
                var camera = Camera.main;
                foreach (var hurtbox in _hurtboxBuffer)
                {
                    if (hurtbox == null || !hurtbox.isActiveAndEnabled
                        || (hitMask.value & (1 << hurtbox.gameObject.layer)) == 0
                        || hurtbox.GetComponentInParent<Player>() != null) continue;
                    var target = hurtbox.Target;
                    if (target == null || _hitThisSwing.Contains(target)
                        || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                    // a hurtbox spanning both bands takes normal damage, matching near-surface classification.
                    var boundary = Mathf.Clamp(_swingSweetSpotRadius, innerRadius, _swingReach);
                    var normalHit = hurtbox.TryOverlapSector(camera, origin, _direction, innerRadius, boundary,
                        _swing.angle, _swingStretchAxis, _swingDepthStretch, out var contact);
                    if (!normalHit && !hurtbox.TryOverlapSector(camera, origin, _direction, boundary, _swingReach,
                        _swing.angle, _swingStretchAxis, _swingDepthStretch, out contact)) continue;

                    var hitDirection = contact - origin;
                    hitDirection.y = 0f;
                    hitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : _direction;
                    _hitThisSwing.Add(target);
                    var dealt = damage * _damageMultiplier * (normalHit ? 1f : sweetMultiplier);
                    target.TakeHit(new HitInfo(gameObject, dealt, hitDirection));
                    PlayHitSplat(contact, hitDirection, dealt, !normalHit);
                    if (!IsAttacking || !isActiveAndEnabled) return;
                    hitstopDuration = Mathf.Max(hitstopDuration, normalHit ? _swing.hitstopDuration : _swing.sweetSpotHitstopDuration);
                    if (vfx != null) vfx.PlayImpact(contact, !normalHit);
                }

                // overlapping radial capsules cover the sector without a physics-step-dependent trigger.
                // clamp contacts to the sector itself so the capsule thickness does not add hidden reach.
                var samples = Mathf.Max(1, Mathf.CeilToInt(_swing.angle / 4f),
                    Mathf.CeilToInt(_swing.angle * Mathf.Deg2Rad * _swingReach * _swingDepthStretch / hitThickness));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var sampleAngle = Mathf.Lerp(startAngle, -startAngle, (float)sample / samples);
                    var radial = Quaternion.AngleAxis(sampleAngle, Vector3.up) * _direction;
                    radial += _swingStretchAxis * (Vector3.Dot(radial, _swingStretchAxis) * (_swingDepthStretch - 1f));
                    var inside = origin + radial * innerRadius;
                    var outside = origin + radial * _swingReach;
                    int count;
                    while (true)
                    {
                        count = Physics.OverlapCapsuleNonAlloc(inside, outside, hitThickness,
                            _hitBuffer, hitMask, QueryTriggerInteraction.Collide);
                        if (count < _hitBuffer.Length) break;
                        Array.Resize(ref _hitBuffer, _hitBuffer.Length * 2);
                    }

                    for (var i = 0; i < count; i++)
                    {
                        var candidate = _hitBuffer[i];
                        if (candidate == null || candidate.GetComponentInParent<Player>() != null) continue;
                        // an opted-in target must never gain a second, world-space damage shape.
                        if (candidate.GetComponentInParent<CombatHurtbox>() != null) continue;
                        var target = candidate.GetComponentInParent<IHittable>();
                        if (target == null || _hitThisSwing.Contains(target)
                            || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                        // classify the near surface, including targets whose centres lie beyond reach.
                        var contact = candidate.ClosestPoint(inside);
                        var offset = contact - origin;
                        offset.y = 0f;
                        // undo the same stretch before testing the sector and its sweet-spot radius.
                        offset -= _swingStretchAxis * (Vector3.Dot(offset, _swingStretchAxis) * (1f - 1f / _swingDepthStretch));
                        var distance = offset.magnitude;
                        // allow only floating-point roundoff at the sector boundary.
                        if (distance < innerRadius - 0.0001f || distance > _swingReach + 0.0001f
                            || Mathf.Abs(Vector3.SignedAngle(_direction, offset, Vector3.up)) > _swing.angle * 0.5f + 0.001f)
                            continue;

                        var sweetSpot = !IsDefending && distance >= _swingSweetSpotRadius;
                        _hitThisSwing.Add(target);
                        var dealt = damage * _damageMultiplier * (sweetSpot ? sweetMultiplier : 1f);
                        target.TakeHit(new HitInfo(gameObject, dealt, radial.normalized));
                        PlayHitSplat(contact, radial.normalized, dealt, sweetSpot);

                        // a hit callback may down or disable the attacker; stop resolving immediately.
                        if (!IsAttacking || !isActiveAndEnabled) return;
                        hitstopDuration = Mathf.Max(hitstopDuration, sweetSpot ? _swing.sweetSpotHitstopDuration : _swing.hitstopDuration);
                        if (vfx != null) vfx.PlayImpact(contact, sweetSpot);
                    }
                }
                // resolve every target first, then pause once using the strongest contact in this sweep.
                if (hitstopDuration > 0f)
                {
                    Player.Hitstop.Request(hitstopDuration);
                    if (Player.Hitstop.IsStopped) return;
                }
            }
            _activeFinished = activeProgress >= 1f;

            // -----------------------------
            // recovery and combo continuation
            // -----------------------------
            if (!_activeFinished) return;

            var recoveryProgress = Mathf.Clamp01((elapsed - _swing.windup - _swing.active) / _swing.recovery);
            var easedRecovery = recoveryEasing.Evaluate(recoveryProgress);
            if (weaponVisual != null)
                weaponVisual.SetSwingPose(_direction, -startAngle + 12f * _swingSign * easedRecovery,
                    height, false,
                    Mathf.Lerp(swingScale, 1f, easedRecovery));

            if (recoveryProgress < 1f) return;

            var finishedDefensive = IsDefending;
            _action = GuitarAction.None;
            ComboStep = 0;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (finishedDefensive)
            {
                Player.Health.ClearAbilityInvulnerability();
                if (vfx != null) vfx.SetDefensiveFlash(false);
                _comboExpiresAt = Time.time + attack.comboResetDelay;
                if (weaponVisual != null) weaponVisual.Cancel();
                return;
            }
            _nextComboIndex++;
            _comboExpiresAt = Time.time + attack.comboResetDelay;
            _readyAt = Time.time;
            if (_nextComboIndex >= attack.combo.Length)
            {
                _nextComboIndex = 0;
                if (!IgnoreCooldowns) _readyAt += attack.comboCooldown;
                if (weaponVisual != null) weaponVisual.Cancel();
            }
        }

        private void UpdateSecondary(float elapsed)
        {
            var attack = tuning.attack;
            var secondary = tuning.secondary;
            var windingUp = elapsed < _strumWindup;
            var recovering = elapsed >= _strumWindup + _strumActive;
            var phase = windingUp ? PlayerAttackPhase.Windup : recovering ? PlayerAttackPhase.Recovery : PlayerAttackPhase.Active;
            var progress = windingUp ? elapsed / _strumWindup : recovering
                ? (elapsed - _strumWindup - _strumActive) / _strumRecovery
                : (elapsed - _strumWindup) / _strumActive;
            if (weaponVisual != null)
                weaponVisual.SetStrumPose(_direction, phase, Mathf.Clamp01(progress),
                    Mathf.Max(0f, elapsed - _strumWindup), _strumActive + _strumRecovery);

            if (!_strumReleased && elapsed >= _strumWindup)
            {
                if (secondary.wavePrefab == null)
                {
                    Cancel();
                    return;
                }
                _strumReleased = true;
                var position = transform.position + Vector3.up * attack.attackHeight + _direction * secondary.spawnOffset;
                var wave = Instantiate(secondary.wavePrefab, position, Quaternion.identity);
                wave.Initialize(Player, _direction, tuning, vfx);
                // launching grants one pending boost, regardless of whether the wave hits anything.
                HasEmpoweredAttack = true;
                _empowerMultiplier = Mathf.Max(1f, secondary.empowermentMultiplier);
            }
            if (elapsed < _strumWindup + _strumActive + _strumRecovery) return;

            _action = GuitarAction.None;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            _comboExpiresAt = Time.time + attack.comboResetDelay;
            if (weaponVisual != null) weaponVisual.Cancel();
            return;
        }

        private void UpdateSpecial(float elapsed)
        {
            var special = tuning.special;
            var windingUp = elapsed < _specialWindup;
            var recovering = elapsed >= _specialWindup + _specialActive;
            var phase = windingUp ? PlayerAttackPhase.Windup : recovering ? PlayerAttackPhase.Recovery : PlayerAttackPhase.Active;
            var progress = windingUp ? elapsed / _specialWindup : recovering
                ? (elapsed - _specialWindup - _specialActive) / _specialRecovery
                : (elapsed - _specialWindup) / _specialActive;
            var centre = _impactResolved ? _impactOrigin
                : transform.position + _direction * _specialDistance + Vector3.up * _specialHeight;
            if (weaponVisual != null)
                weaponVisual.SetSmashPose(_direction, phase, Mathf.Clamp01(progress), _specialImpactFraction);
            if (!_impactResolved && vfx != null)
                vfx.ShowSmashIndicator(centre, _specialRadius, _specialSweetSpotRadius);

            if (!_impactResolved && elapsed >= _specialWindup + _specialActive * _specialImpactFraction)
            {
                _impactResolved = true;
                _impactOrigin = centre;
                Player.Session?.Debug.RecordAttack(new DebugAttackSnapshot(centre, _direction, 0f, _specialRadius,
                    360f, Vector3.forward, 1f, _specialSweetSpotRadius, sweetSpotInside: true));
                if (vfx != null) vfx.PlaySmashImpact();
                var hitstop = 0f;
                var camera = Camera.main;
                _hurtboxBuffer.Clear();
                foreach (var hurtbox in CombatHurtbox.Active) _hurtboxBuffer.Add(hurtbox);
                foreach (var hurtbox in _hurtboxBuffer)
                {
                    if (hurtbox == null || !hurtbox.isActiveAndEnabled
                        || (special.hitMask.value & (1 << hurtbox.gameObject.layer)) == 0
                        || hurtbox.GetComponentInParent<Player>() != null) continue;
                    var target = hurtbox.Target;
                    if (target == null || _hitThisSwing.Contains(target)
                        || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                    if (!hurtbox.TryOverlapSector(camera, centre, _direction, 0f, _specialRadius,
                            360f, Vector3.forward, 1f, out var contact)) continue;

                    // sweet spots use the ground footprint; height must not shift the centre along the camera ray.
                    var targetCentre = hurtbox.WorldCenter;
                    targetCentre.y = centre.y;
                    var sweet = (targetCentre - centre).sqrMagnitude <= _specialSweetSpotRadius * _specialSweetSpotRadius + 0.000001f;
                    var direction = targetCentre - centre;
                    direction.y = 0f;
                    direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : _direction;
                    _hitThisSwing.Add(target);
                    var dealt = special.damage * _damageMultiplier * (sweet ? special.sweetSpotMultiplier : 1f);
                    target.TakeHit(new HitInfo(gameObject, dealt, direction));
                    PlayHitSplat(contact, direction, dealt, true);
                    if (!IsAttacking || _action != GuitarAction.Special || !isActiveAndEnabled) return;
                    hitstop = Mathf.Max(hitstop, sweet ? special.sweetSpotHitstopDuration : special.hitstopDuration);
                    if (vfx != null) vfx.PlayImpact(contact, sweet);
                }

                // retain the existing physics fallback for targets without an authored projected hurtbox.
                int count;
                while (true)
                {
                    var halfHeight = Vector3.up * Mathf.Max(0f, special.hitHeight) * 0.5f;
                    count = Physics.OverlapCapsuleNonAlloc(centre - halfHeight, centre + halfHeight, _specialRadius,
                        _hitBuffer, special.hitMask, QueryTriggerInteraction.Collide);
                    if (count < _hitBuffer.Length) break;
                    Array.Resize(ref _hitBuffer, _hitBuffer.Length * 2);
                }
                for (var i = 0; i < count; i++)
                {
                    var candidate = _hitBuffer[i];
                    if (candidate == null || candidate.GetComponentInParent<Player>() != null
                        || candidate.GetComponentInParent<CombatHurtbox>() != null) continue;
                    var target = candidate.GetComponentInParent<IHittable>();
                    if (target == null || _hitThisSwing.Contains(target)
                        || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                    var contact = candidate.ClosestPoint(centre);
                    var near = contact - centre;
                    near.y = 0f;
                    if (near.sqrMagnitude > _specialRadius * _specialRadius + 0.000001f) continue;
                    var targetCentre = candidate.bounds.center;
                    var offset = targetCentre - centre;
                    offset.y = 0f;
                    var sweet = offset.sqrMagnitude <= _specialSweetSpotRadius * _specialSweetSpotRadius + 0.000001f;
                    var direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : _direction;
                    _hitThisSwing.Add(target);
                    var dealt = special.damage * _damageMultiplier * (sweet ? special.sweetSpotMultiplier : 1f);
                    target.TakeHit(new HitInfo(gameObject, dealt, direction));
                    PlayHitSplat(contact, direction, dealt, true);
                    if (!IsAttacking || _action != GuitarAction.Special || !isActiveAndEnabled) return;
                    hitstop = Mathf.Max(hitstop, sweet ? special.sweetSpotHitstopDuration : special.hitstopDuration);
                    if (vfx != null) vfx.PlayImpact(contact, sweet);
                }
                Player.Hitstop.Request(hitstop);
                if (Player.Hitstop.IsStopped) return;
            }
            if (elapsed < _specialWindup + _specialActive + _specialRecovery) return;
            _action = GuitarAction.None;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (weaponVisual != null) weaponVisual.Cancel();
            return;
        }

        public override bool TryUseSecondary()
        {
            if (!isActiveAndEnabled || !Player.CanUseAbilities || IsAttacking || (!IgnoreCooldowns && Time.time < _secondaryReadyAt)) return false;
            var secondary = tuning.secondary;
            if (secondary.wavePrefab == null) return false;

            if (Time.time > _comboExpiresAt) _nextComboIndex = 0;
            if (secondary.backstepDistance > 0f && (!Player.Movement.isActiveAndEnabled
                || !GetComponent<CharacterController>().enabled || Player.Movement.IsDisplacing)) return false;
            _direction = Player.GetAttackDirection();
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;
            _strumWindup = Mathf.Max(0.001f, secondary.windup);
            _strumActive = Mathf.Max(0.001f, secondary.active);
            _strumRecovery = Mathf.Max(0.001f, secondary.recovery);
            // use the same collision-aware motion path as Primary, delayed until the wave launches.
            if (secondary.backstepDistance > 0f && !Player.Movement.TryStartDisplacement(
                -_direction, secondary.backstepDistance, _strumActive, secondary.backstepEasing, _strumWindup)) return false;

            _startedAt = Player.Hitstop.ActionTime;
            _secondaryReadyAt = IgnoreCooldowns ? 0f : Time.time + Mathf.Max(0f, secondary.cooldown);
            _strumReleased = false;
            ComboStep = 0;
            _action = GuitarAction.Secondary;
            BlocksMovement = false;
            BlocksDodge = true;
            BlocksAbilities = true;
            return true;
        }
        public override bool TryUseDefensive()
        {
            if (!isActiveAndEnabled || !Player.CanUseDefensive || (!IgnoreCooldowns && Time.time < _defensiveReadyAt)) return false;
            var defensive = tuning.defensive;
            if (defensive.backstepDistance > 0f && (!Player.Movement.isActiveAndEnabled
                || !GetComponent<CharacterController>().enabled)) return false;

            var comboIndex = !IsAttacking && Time.time > _comboExpiresAt ? 0 : _nextComboIndex;
            // a cancelled anticipation retries its strike; a strike already in recovery counts as completed.
            if (IsAttacking && ComboStep > 0 && AnimationPhase == PlayerAttackPhase.Recovery)
            {
                comboIndex++;
                if (comboIndex >= tuning.attack.combo.Length)
                {
                    comboIndex = 0;
                    if (!IgnoreCooldowns) _readyAt += tuning.attack.comboCooldown;
                }
            }
            var empowered = HasEmpoweredAttack;
            var empowermentMultiplier = _empowerMultiplier;
            Cancel();
            _nextComboIndex = comboIndex;
            HasEmpoweredAttack = empowered;
            _empowerMultiplier = empowermentMultiplier;
            _direction = Player.GetAttackDirection();
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;
            _swing = new GuitarTuning.Swing
            {
                windup = Mathf.Max(0.001f, defensive.windup), active = Mathf.Max(0.001f, defensive.active),
                recovery = Mathf.Max(0.001f, defensive.recovery), angle = defensive.angle,
                hitstopDuration = defensive.hitstopDuration, sweetSpotHitstopDuration = defensive.hitstopDuration
            };
            if (defensive.backstepDistance > 0f && !Player.Movement.TryStartDisplacement(
                -_direction, defensive.backstepDistance, _swing.active, defensive.backstepEasing, _swing.windup)) return false;

            _swingReach = Mathf.Max(0.01f, defensive.reach);
            _swingSweetSpotRadius = _swingReach + 1f;
            _swingStretchAxis = new Vector3(defensive.stretchAxis.x, 0f, defensive.stretchAxis.y);
            _swingStretchAxis = _swingStretchAxis.sqrMagnitude > 0.0001f ? _swingStretchAxis.normalized : Vector3.forward;
            _swingDepthStretch = Mathf.Max(1f, defensive.depthStretch);
            _swingSign = 1f;
            _damageMultiplier = 1f;
            _startedAt = Player.Hitstop.ActionTime;
            _defensiveReadyAt = IgnoreCooldowns ? 0f : Time.time + Mathf.Max(0f, defensive.cooldown);
            _comboExpiresAt = Time.time + tuning.attack.comboResetDelay;
            _impactResolved = false;
            _activeFinished = false;
            _action = GuitarAction.Defensive;
            BlocksMovement = false;
            BlocksDodge = true;
            BlocksAbilities = true;
            Player.Health.GrantAbilityInvulnerability(_swing.windup + _swing.active);
            if (vfx != null) vfx.SetDefensiveFlash(true);
            return true;
        }
        public override bool TryUseSpecial()
        {
            if (!isActiveAndEnabled || !Player.CanUseAbilities || IsAttacking || (!IgnoreCooldowns && Time.time < _specialReadyAt)) return false;
            var special = tuning.special;
            _direction = Player.GetAttackDirection();
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;
            _specialWindup = Mathf.Max(0.01f, special.windup);
            _specialActive = Mathf.Max(0.01f, special.active);
            _specialRecovery = Mathf.Max(0.01f, special.recovery);
            _specialImpactFraction = Mathf.Clamp(special.impactFraction, 0.01f, 1f);
            _specialDistance = Mathf.Max(0f, special.distance);
            _specialRadius = Mathf.Max(0.01f, special.radius);
            _specialSweetSpotRadius = Mathf.Clamp(special.sweetSpotRadius, 0f, _specialRadius);
            _specialHeight = special.impactHeight;
            _damageMultiplier = HasEmpoweredAttack ? _empowerMultiplier : 1f;
            HasEmpoweredAttack = false;
            _startedAt = Player.Hitstop.ActionTime;
            _specialReadyAt = IgnoreCooldowns ? 0f : Time.time + Mathf.Max(0f, special.cooldown);
            _impactResolved = false;
            _hitThisSwing.Clear();
            _nextComboIndex = 0;
            ComboStep = 0;
            _action = GuitarAction.Special;
            BlocksMovement = false;
            BlocksDodge = true;
            BlocksAbilities = true;
            return true;
        }

        // presentation only: the hit splat (sprite, damage number, sound) shared with the Strum wave.
        private void PlayHitSplat(Vector3 point, Vector3 direction, float damage, bool big)
        {
            var effect = tuning != null ? tuning.vfx.hitEffect : null;
            if (effect != null) effect.Play(new HitEffectRequest(point, direction, damage, big));
        }

        public override void Cancel()
        {
            _action = GuitarAction.None;
            if (Player.Health != null) Player.Health.ClearAbilityInvulnerability();
            HasEmpoweredAttack = false;
            _damageMultiplier = 1f;
            _empowerMultiplier = 1f;
            ComboStep = 0;
            _nextComboIndex = 0;
            _hitThisSwing.Clear();
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (Player.Movement != null) Player.Movement.CancelDisplacement();
            if (Player.Hitstop != null) Player.Hitstop.Clear();
            if (weaponVisual != null) weaponVisual.Cancel();
            if (vfx != null) vfx.Cancel();
        }

        private void OnDrawGizmosSelected()
        {
            if (tuning == null) return;
            var attack = tuning.attack;
            if (_action == GuitarAction.Special)
            {
                var centre = _impactResolved ? _impactOrigin
                    : transform.position + _direction * _specialDistance + Vector3.up * _specialHeight;
                for (var ring = 0; ring < 2; ring++)
                {
                    Gizmos.color = ring == 0 ? Color.cyan : new Color(1f, 0.8f, 0.25f);
                    var radius = ring == 0 ? _specialRadius : _specialSweetSpotRadius;
                    for (var i = 0; i < 90; i++)
                        Gizmos.DrawLine(centre + Quaternion.Euler(0, i * 4f, 0) * Vector3.forward * radius,
                            centre + Quaternion.Euler(0, (i + 1) * 4f, 0) * Vector3.forward * radius);
                }
                return;
            }
            var sweepActive = _action == GuitarAction.Primary || _action == GuitarAction.Defensive;
            var direction = IsAttacking ? _direction : Vector3.forward;
            var defensive = tuning.defensive;
            var height = IsDefending ? defensive.attackHeight : attack.attackHeight;
            var innerRadius = IsDefending ? defensive.innerRadius : attack.innerRadius;
            var origin = sweepActive && _impactResolved ? _impactOrigin : transform.position + Vector3.up * height;
            var angle = sweepActive ? _swing.angle : attack.combo != null && attack.combo.Length > 0 ? attack.combo[0].angle : 140f;
            var shownReach = sweepActive ? _swingReach : attack.reach;
            var shownSweetSpot = sweepActive ? _swingSweetSpotRadius : attack.sweetSpotRadius;
            var stretchAxis = sweepActive ? _swingStretchAxis : new Vector3(attack.stretchAxis.x, 0f, attack.stretchAxis.y);
            stretchAxis = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.forward;
            var depthStretch = sweepActive ? _swingDepthStretch : Mathf.Max(1f, attack.depthStretch);
            for (var ring = 0; ring < (IsDefending ? 1 : 2); ring++)
            {
                var radius = ring == 0 ? shownReach : shownSweetSpot;
                Gizmos.color = ring == 0 ? Color.cyan : new Color(1f, 0.8f, 0.25f);
                for (var i = 0; i < 40; i++)
                {
                    var a = Quaternion.AngleAxis(-angle * 0.5f + angle * i / 40f, Vector3.up) * direction;
                    var b = Quaternion.AngleAxis(-angle * 0.5f + angle * (i + 1) / 40f, Vector3.up) * direction;
                    a += stretchAxis * (Vector3.Dot(a, stretchAxis) * (depthStretch - 1f));
                    b += stretchAxis * (Vector3.Dot(b, stretchAxis) * (depthStretch - 1f));
                    Gizmos.DrawLine(origin + a * radius, origin + b * radius);
                }
            }

            var left = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * direction;
            var right = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * direction;
            left += stretchAxis * (Vector3.Dot(left, stretchAxis) * (depthStretch - 1f));
            right += stretchAxis * (Vector3.Dot(right, stretchAxis) * (depthStretch - 1f));
            Gizmos.DrawLine(origin + left * innerRadius, origin + left * shownReach);
            Gizmos.DrawLine(origin + right * innerRadius, origin + right * shownReach);
        }
    }
}
