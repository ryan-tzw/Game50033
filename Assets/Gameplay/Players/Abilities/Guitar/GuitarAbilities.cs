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
        private bool _strumming;
        private bool _strumReleased;
        private float _strumWindup;
        private float _strumActive;
        private float _strumRecovery;
        private float _secondaryReadyAt;
        private float _empowerMultiplier = 1f;
        private float _primaryMultiplier = 1f;

        public bool IsAttacking { get; private set; }
        public bool IsStrumming => _strumming;
        public bool HasEmpoweredPrimary { get; private set; }
        public float SecondaryCooldownRemaining => Mathf.Max(0f, _secondaryReadyAt - Time.time);
        public int ComboStep { get; private set; }
        public float PrimaryCooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);

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
            if (!Player.CanUseAbilities || IsAttacking || Time.time < _readyAt
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
            _direction = Player.AimDirection;
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;

            // request the whole lunge here so movement and attack phases share the same start time.
            if (_swing.lungeDistance > 0f && !Player.Movement.TryStartDisplacement(
                _direction, _swing.lungeDistance, _swing.active, attack.lungeEasing, _swing.windup)) return false;

            // spend empowerment only after activation succeeds; it applies to the whole next strike.
            _primaryMultiplier = HasEmpoweredPrimary ? _empowerMultiplier : 1f;
            HasEmpoweredPrimary = false;
            ComboStep = _nextComboIndex + 1;
            _swingSign = _nextComboIndex % 2 == 0 ? 1f : -1f;
            _startedAt = Time.time;
            _readyAt = _startedAt + _swing.windup + _swing.active + _swing.recovery;
            _activeFinished = false;
            _impactResolved = false;
            _hitThisSwing.Clear();
            IsAttacking = true;
            BlocksMovement = true;
            BlocksDodge = true;
            BlocksAbilities = true;
            return true;
        }

        private void Update()
        {
            if (!IsAttacking)
            {
                if (_nextComboIndex > 0 && Time.time > _comboExpiresAt)
                {
                    _nextComboIndex = 0;
                    if (weaponVisual != null) weaponVisual.Cancel();
                }
                return;
            }
            if (Player.Health.IsDowned || !Player.HasActiveInput)
            {
                Cancel();
                return;
            }

            var attack = tuning.attack;
            var elapsed = Time.time - _startedAt;
            if (_strumming)
            {
                var secondary = tuning.secondary;
                var sign = _nextComboIndex % 2 == 0 ? 1f : -1f;
                var start = -secondary.swingAngle * 0.5f * sign;
                var windingUp = elapsed < _strumWindup;
                var recovering = elapsed >= _strumWindup + _strumActive;
                float angle;
                float scale;
                if (windingUp)
                {
                    var progress = Mathf.Clamp01(elapsed / _strumWindup);
                    angle = start - attack.anticipationAngle * sign * Mathf.Sin(attack.windupEasing.Evaluate(progress) * Mathf.PI);
                    scale = attack.windupScale;
                }
                else if (!recovering)
                {
                    var progress = Mathf.Clamp01((elapsed - _strumWindup) / _strumActive);
                    angle = Mathf.LerpUnclamped(start, -start, secondary.swingEasing.Evaluate(progress));
                    scale = attack.swingScale;
                }
                else
                {
                    var progress = Mathf.Clamp01((elapsed - _strumWindup - _strumActive) / _strumRecovery);
                    var eased = attack.recoveryEasing.Evaluate(progress);
                    angle = -start + 12f * sign * eased;
                    scale = Mathf.Lerp(attack.swingScale, 1f, eased);
                }
                if (weaponVisual != null)
                    weaponVisual.SetSwingPose(_direction, angle, attack.attackHeight, !windingUp && !recovering, scale);

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
                    HasEmpoweredPrimary = true;
                    _empowerMultiplier = Mathf.Max(1f, secondary.primaryMultiplier);
                }
                if (elapsed < _strumWindup + _strumActive + _strumRecovery) return;

                _strumming = false;
                IsAttacking = false;
                BlocksMovement = false;
                BlocksDodge = false;
                BlocksAbilities = false;
                _comboExpiresAt = Time.time + attack.comboResetDelay;
                if (weaponVisual != null)
                {
                    if (_nextComboIndex == 0) weaponVisual.Cancel();
                    else weaponVisual.SetSwingPose(_direction, angle, attack.attackHeight, false, scale);
                }
                return;
            }
            var origin = transform.position + Vector3.up * attack.attackHeight;
            var startAngle = -_swing.angle * 0.5f * _swingSign;

            // -----------------------------
            // windup and active sweep
            // -----------------------------
            if (elapsed < _swing.windup)
            {
                var progress = attack.windupEasing.Evaluate(Mathf.Clamp01(elapsed / _swing.windup));
                var angle = startAngle - attack.anticipationAngle * _swingSign * Mathf.Sin(progress * Mathf.PI);
                if (weaponVisual != null)
                    weaponVisual.SetSwingPose(_direction, angle, attack.attackHeight, false, attack.windupScale);
                return;
            }

            var activeProgress = Mathf.Clamp01((elapsed - _swing.windup) / _swing.active);
            var currentAngle = Mathf.LerpUnclamped(startAngle, -startAngle, attack.swingEasing.Evaluate(activeProgress));
            if (!_activeFinished && weaponVisual != null)
                weaponVisual.SetSwingPose(_direction, currentAngle, attack.attackHeight, true, attack.swingScale);

            // the entire sector resolves once, even when a slow frame crosses the impact time.
            // later lunging, weapon motion and smear poses cannot move or repeat this hit area.
            if (!_impactResolved && elapsed >= _swing.windup + Mathf.Clamp(attack.impactDelay, 0f, _swing.active))
            {
                _impactResolved = true;
                _impactOrigin = origin;
                if (vfx != null)
                    vfx.BeginSwing(origin, _direction, startAngle, -startAngle, attack.innerRadius, _swingReach,
                        _swingSweetSpotRadius, _swingStretchAxis, _swingDepthStretch);

                // snapshot the registry because a hit callback can disable or destroy another hurtbox.
                _hurtboxBuffer.Clear();
                foreach (var hurtbox in CombatHurtbox.Active) _hurtboxBuffer.Add(hurtbox);
                var camera = Camera.main;
                foreach (var hurtbox in _hurtboxBuffer)
                {
                    if (hurtbox == null || !hurtbox.isActiveAndEnabled
                        || (attack.hitMask.value & (1 << hurtbox.gameObject.layer)) == 0
                        || hurtbox.GetComponentInParent<Player>() != null) continue;
                    var target = hurtbox.Target;
                    if (target == null || _hitThisSwing.Contains(target)
                        || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                    // a hurtbox spanning both bands takes normal damage, matching near-surface classification.
                    var boundary = Mathf.Clamp(_swingSweetSpotRadius, attack.innerRadius, _swingReach);
                    var normalHit = hurtbox.TryOverlapSector(camera, origin, _direction, attack.innerRadius, boundary,
                        _swing.angle, _swingStretchAxis, _swingDepthStretch, out var contact);
                    if (!normalHit && !hurtbox.TryOverlapSector(camera, origin, _direction, boundary, _swingReach,
                        _swing.angle, _swingStretchAxis, _swingDepthStretch, out contact)) continue;

                    var hitDirection = contact - origin;
                    hitDirection.y = 0f;
                    hitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : _direction;
                    _hitThisSwing.Add(target);
                    target.TakeHit(new HitInfo(gameObject, attack.damage * _primaryMultiplier
                        * (normalHit ? 1f : attack.sweetSpotMultiplier), hitDirection));
                    if (!IsAttacking || !isActiveAndEnabled) return;
                    if (vfx != null) vfx.PlayImpact(contact, !normalHit);
                }

                // overlapping radial capsules cover the sector without a physics-step-dependent trigger.
                // clamp contacts to the sector itself so the capsule thickness does not add hidden reach.
                var samples = Mathf.Max(1, Mathf.CeilToInt(_swing.angle / 4f),
                    Mathf.CeilToInt(_swing.angle * Mathf.Deg2Rad * _swingReach * _swingDepthStretch / attack.hitThickness));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var sampleAngle = Mathf.Lerp(startAngle, -startAngle, (float)sample / samples);
                    var radial = Quaternion.AngleAxis(sampleAngle, Vector3.up) * _direction;
                    radial += _swingStretchAxis * (Vector3.Dot(radial, _swingStretchAxis) * (_swingDepthStretch - 1f));
                    var inside = origin + radial * attack.innerRadius;
                    var outside = origin + radial * _swingReach;
                    int count;
                    while (true)
                    {
                        count = Physics.OverlapCapsuleNonAlloc(inside, outside, attack.hitThickness,
                            _hitBuffer, attack.hitMask, QueryTriggerInteraction.Collide);
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
                        if (distance < attack.innerRadius - 0.0001f || distance > _swingReach + 0.0001f
                            || Mathf.Abs(Vector3.SignedAngle(_direction, offset, Vector3.up)) > _swing.angle * 0.5f + 0.001f)
                            continue;

                        var sweetSpot = distance >= _swingSweetSpotRadius;
                        _hitThisSwing.Add(target);
                        target.TakeHit(new HitInfo(gameObject, attack.damage * _primaryMultiplier * (sweetSpot ? attack.sweetSpotMultiplier : 1f), radial.normalized));

                        // a hit callback may down or disable the attacker; stop resolving immediately.
                        if (!IsAttacking || !isActiveAndEnabled) return;
                        if (vfx != null) vfx.PlayImpact(contact, sweetSpot);
                    }
                }
            }
            _activeFinished = activeProgress >= 1f;

            // -----------------------------
            // recovery and combo continuation
            // -----------------------------
            if (!_activeFinished) return;

            var recoveryProgress = Mathf.Clamp01((elapsed - _swing.windup - _swing.active) / _swing.recovery);
            var easedRecovery = attack.recoveryEasing.Evaluate(recoveryProgress);
            if (weaponVisual != null)
                weaponVisual.SetSwingPose(_direction, -startAngle + 12f * _swingSign * easedRecovery,
                    attack.attackHeight, false,
                    Mathf.Lerp(attack.swingScale, 1f, easedRecovery));

            if (recoveryProgress < 1f) return;

            IsAttacking = false;
            ComboStep = 0;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            _nextComboIndex++;
            _comboExpiresAt = Time.time + attack.comboResetDelay;
            _readyAt = Time.time;
            if (_nextComboIndex >= attack.combo.Length)
            {
                _nextComboIndex = 0;
                _readyAt += attack.comboCooldown;
                if (weaponVisual != null) weaponVisual.Cancel();
            }
        }

        public override bool TryUseSecondary()
        {
            if (!isActiveAndEnabled || !Player.CanUseAbilities || IsAttacking || Time.time < _secondaryReadyAt) return false;
            var secondary = tuning.secondary;
            if (secondary.wavePrefab == null) return false;

            if (Time.time > _comboExpiresAt) _nextComboIndex = 0;
            _direction = Player.AimDirection;
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;
            _strumWindup = Mathf.Max(0.001f, secondary.windup);
            _strumActive = Mathf.Max(0.001f, secondary.active);
            _strumRecovery = Mathf.Max(0.001f, secondary.recovery);
            // use the same collision-aware motion path as Primary, delayed until the wave launches.
            if (secondary.backstepDistance > 0f && !Player.Movement.TryStartDisplacement(
                -_direction, secondary.backstepDistance, _strumActive, secondary.backstepEasing, _strumWindup)) return false;

            _startedAt = Time.time;
            _secondaryReadyAt = Time.time + Mathf.Max(0f, secondary.cooldown);
            _strumReleased = false;
            _strumming = true;
            ComboStep = 0;
            IsAttacking = true;
            BlocksMovement = true;
            BlocksDodge = true;
            BlocksAbilities = true;
            return true;
        }
        public override bool TryUseDefensive() => false;
        public override bool TryUseSpecial() => false;

        public override void Cancel()
        {
            _strumming = false;
            HasEmpoweredPrimary = false;
            _primaryMultiplier = 1f;
            _empowerMultiplier = 1f;
            IsAttacking = false;
            ComboStep = 0;
            _nextComboIndex = 0;
            _hitThisSwing.Clear();
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (Player.Movement != null) Player.Movement.CancelDisplacement();
            if (weaponVisual != null) weaponVisual.Cancel();
            if (vfx != null) vfx.Cancel();
        }

        private void OnDrawGizmosSelected()
        {
            if (tuning == null) return;
            var attack = tuning.attack;
            var primaryActive = IsAttacking && !_strumming;
            var direction = IsAttacking ? _direction : Vector3.forward;
            var origin = primaryActive && _impactResolved ? _impactOrigin : transform.position + Vector3.up * attack.attackHeight;
            var angle = primaryActive ? _swing.angle : attack.combo != null && attack.combo.Length > 0 ? attack.combo[0].angle : 140f;
            var shownReach = primaryActive ? _swingReach : attack.reach;
            var shownSweetSpot = primaryActive ? _swingSweetSpotRadius : attack.sweetSpotRadius;
            var stretchAxis = primaryActive ? _swingStretchAxis : new Vector3(attack.stretchAxis.x, 0f, attack.stretchAxis.y);
            stretchAxis = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.forward;
            var depthStretch = primaryActive ? _swingDepthStretch : Mathf.Max(1f, attack.depthStretch);
            for (var ring = 0; ring < 2; ring++)
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
            Gizmos.DrawLine(origin + left * attack.innerRadius, origin + left * shownReach);
            Gizmos.DrawLine(origin + right * attack.innerRadius, origin + right * shownReach);
        }
    }
}
