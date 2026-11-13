namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // resolve hits after player movement; the weapon then renders the same pose in late update.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class GuitarAbilities : PlayerAbilities
    {
        [Serializable]
        private struct Swing
        {
            [Min(0.01f)] public float windup;
            [Min(0.01f)] public float active;
            [Min(0.01f)] public float recovery;
            [Range(1f, 300f)] public float angle;
            [Min(0f)] public float lungeDistance;
            [Min(0.01f)] public float reachMultiplier;
        }

        [SerializeField] private GuitarWeaponVisual weaponVisual;
        [SerializeField] private GuitarVFX vfx;
        [SerializeField] private Swing[] combo =
        {
            new Swing { windup = 0.14f, active = 0.18f, recovery = 0.16f, angle = 140f, lungeDistance = 0.22f, reachMultiplier = 1f },
            new Swing { windup = 0.12f, active = 0.18f, recovery = 0.18f, angle = 140f, lungeDistance = 0.22f, reachMultiplier = 1f },
            new Swing { windup = 0.22f, active = 0.22f, recovery = 0.28f, angle = 180f, lungeDistance = 0.3f, reachMultiplier = 1.4f },
        };
        [SerializeField, Min(0f)] private float comboCooldown = 0.4f;
        [SerializeField, Min(0f)] private float comboResetDelay = 0.8f;
        [SerializeField, Min(0.01f)] private float reach = 1.05f;
        [SerializeField, Min(0f)] private float innerRadius = 0.12f;
        [SerializeField, Min(0f)] private float sweetSpotRadius = 0.7f;
        [SerializeField, Min(0.01f)] private float hitThickness = 0.08f;
        [SerializeField] private float attackHeight = 0.25f;
        [SerializeField, Min(0f)] private float damage = 2f;
        [SerializeField, Min(1f)] private float sweetSpotMultiplier = 1.5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private AnimationCurve swingEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve lungeEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve windupEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve recoveryEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float anticipationAngle = 22f;
        [SerializeField, Min(0.01f)] private float windupScale = 0.9f;
        [SerializeField, Min(0.01f)] private float swingScale = 1.05f;

        private readonly HashSet<IHittable> _hitThisSwing = new();
        private Collider[] _hitBuffer = new Collider[32];
        private Swing _swing;
        private Vector3 _direction;
        private Vector3 _previousOrigin;
        private float _previousAngle;
        private float _startedAt;
        private float _readyAt;
        private float _comboExpiresAt;
        private int _nextComboIndex;
        private float _swingSign;
        private float _swingReach;
        private float _swingSweetSpotRadius;
        private bool _activeFinished;
        private bool _smearStarted;

        public bool IsAttacking { get; private set; }
        public int ComboStep { get; private set; }
        public float PrimaryCooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);

        private void Awake()
        {
            if (weaponVisual == null) weaponVisual = GetComponent<GuitarWeaponVisual>();
            if (vfx == null) vfx = GetComponent<GuitarVFX>();
        }

        public override bool TryUsePrimary()
        {
            if (!isActiveAndEnabled || !Player.CanUseAbilities || IsAttacking || Time.time < _readyAt
                || combo == null || combo.Length == 0) return false;

            if (Time.time > _comboExpiresAt) _nextComboIndex = 0;
            _nextComboIndex = Mathf.Clamp(_nextComboIndex, 0, combo.Length - 1);
            _swing = combo[_nextComboIndex];
            _swing.windup = Mathf.Max(0.01f, _swing.windup);
            _swing.active = Mathf.Max(0.01f, _swing.active);
            _swing.recovery = Mathf.Max(0.01f, _swing.recovery);
            // older serialized combos have no multiplier; keep their original reach.
            var reachScale = _swing.reachMultiplier > 0f ? _swing.reachMultiplier : 1f;
            _swingReach = reach * reachScale;
            _swingSweetSpotRadius = sweetSpotRadius * reachScale;
            _direction = Player.AimDirection;
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;

            // request the whole lunge here so movement and attack phases share the same start time.
            if (_swing.lungeDistance > 0f && !Player.Movement.TryStartDisplacement(
                _direction, _swing.lungeDistance, _swing.active, lungeEasing, _swing.windup)) return false;

            ComboStep = _nextComboIndex + 1;
            _swingSign = _nextComboIndex % 2 == 0 ? 1f : -1f;
            _previousAngle = -_swing.angle * 0.5f * _swingSign;
            _previousOrigin = transform.position + Vector3.up * attackHeight;
            _startedAt = Time.time;
            _readyAt = _startedAt + _swing.windup + _swing.active + _swing.recovery;
            _activeFinished = false;
            _smearStarted = false;
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

            var elapsed = Time.time - _startedAt;
            var origin = transform.position + Vector3.up * attackHeight;
            var startAngle = -_swing.angle * 0.5f * _swingSign;

            // -----------------------------
            // windup and active sweep
            // -----------------------------
            if (elapsed < _swing.windup)
            {
                var progress = windupEasing.Evaluate(Mathf.Clamp01(elapsed / _swing.windup));
                var angle = startAngle - anticipationAngle * _swingSign * Mathf.Sin(progress * Mathf.PI);
                if (weaponVisual != null)
                    weaponVisual.SetSwingPose(_direction, angle, attackHeight, false, windupScale);
                _previousOrigin = origin;
                return;
            }

            var activeProgress = Mathf.Clamp01((elapsed - _swing.windup) / _swing.active);
            var currentAngle = Mathf.LerpUnclamped(startAngle, -startAngle, swingEasing.Evaluate(activeProgress));
            if (!_activeFinished)
            {
                if (vfx != null)
                {
                    if (!_smearStarted)
                        vfx.BeginSwing(_previousOrigin, _direction, startAngle, -startAngle, innerRadius, _swingReach, _swingSweetSpotRadius);
                }
                _smearStarted = true;
                if (weaponVisual != null)
                    weaponVisual.SetSwingPose(_direction, currentAngle, attackHeight, true, swingScale);

                // sample the swept blade, including player travel, so low frame rates cannot skip targets.
                var samples = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(currentAngle - _previousAngle) / 5f),
                    Mathf.CeilToInt(Vector3.Distance(origin, _previousOrigin) / 0.04f));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var t = (float)sample / samples;
                    var sampleOrigin = Vector3.Lerp(_previousOrigin, origin, t);
                    var sampleAngle = Mathf.LerpUnclamped(_previousAngle, currentAngle, t);
                    var radial = Quaternion.AngleAxis(sampleAngle, Vector3.up) * _direction;
                    var inside = sampleOrigin + radial * innerRadius;
                    var outside = sampleOrigin + radial * _swingReach;
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
                        if (candidate.GetComponentInParent<Player>() != null) continue;
                        var target = candidate.GetComponentInParent<IHittable>();
                        if (target == null || _hitThisSwing.Contains(target)
                            || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                        var projection = Mathf.Clamp(Vector3.Dot(candidate.bounds.center - sampleOrigin, radial), innerRadius, _swingReach);
                        var contact = candidate.ClosestPoint(sampleOrigin + radial * projection);
                        var offset = contact - sampleOrigin;
                        offset.y = 0f;
                        var distance = offset.magnitude;
                        var angle = Mathf.Abs(Vector3.SignedAngle(radial, offset, Vector3.up));
                        var angularWidth = Mathf.Atan2(hitThickness, Mathf.Max(innerRadius, distance)) * Mathf.Rad2Deg;
                        if (distance < innerRadius - hitThickness || distance > _swingReach + hitThickness
                            || angle > angularWidth + 0.1f) continue;

                        var sweetSpot = distance >= _swingSweetSpotRadius;
                        _hitThisSwing.Add(target);
                        target.TakeHit(new HitInfo(gameObject, damage * (sweetSpot ? sweetSpotMultiplier : 1f), radial));
                        if (vfx != null) vfx.PlayImpact(contact, sweetSpot);

                        // a hit callback may down or disable the attacker; stop resolving immediately.
                        if (!IsAttacking || !isActiveAndEnabled) return;
                    }
                }

                _previousAngle = currentAngle;
                _previousOrigin = origin;
                if (activeProgress >= 1f)
                {
                    _activeFinished = true;
                }
            }

            // -----------------------------
            // recovery and combo continuation
            // -----------------------------
            if (!_activeFinished) return;

            var recoveryProgress = Mathf.Clamp01((elapsed - _swing.windup - _swing.active) / _swing.recovery);
            var easedRecovery = recoveryEasing.Evaluate(recoveryProgress);
            if (weaponVisual != null)
                weaponVisual.SetSwingPose(_direction, -startAngle + 12f * _swingSign * easedRecovery,
                    attackHeight, false,
                    Mathf.Lerp(swingScale, 1f, easedRecovery));

            if (recoveryProgress < 1f) return;

            IsAttacking = false;
            ComboStep = 0;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            _nextComboIndex++;
            _comboExpiresAt = Time.time + comboResetDelay;
            _readyAt = Time.time;
            if (_nextComboIndex >= combo.Length)
            {
                _nextComboIndex = 0;
                _readyAt += comboCooldown;
                if (weaponVisual != null) weaponVisual.Cancel();
            }
        }

        public override bool TryUseSecondary() => false;
        public override bool TryUseDefensive() => false;
        public override bool TryUseSpecial() => false;

        public override void Cancel()
        {
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
            var direction = IsAttacking ? _direction : Vector3.forward;
            var origin = transform.position + Vector3.up * attackHeight;
            var angle = IsAttacking ? _swing.angle : combo != null && combo.Length > 0 ? combo[0].angle : 140f;
            var shownReach = IsAttacking ? _swingReach : reach;
            var shownSweetSpot = IsAttacking ? _swingSweetSpotRadius : sweetSpotRadius;
            for (var ring = 0; ring < 2; ring++)
            {
                var radius = ring == 0 ? shownReach : shownSweetSpot;
                Gizmos.color = ring == 0 ? Color.cyan : new Color(1f, 0.8f, 0.25f);
                for (var i = 0; i < 40; i++)
                {
                    var a = Quaternion.AngleAxis(-angle * 0.5f + angle * i / 40f, Vector3.up) * direction;
                    var b = Quaternion.AngleAxis(-angle * 0.5f + angle * (i + 1) / 40f, Vector3.up) * direction;
                    Gizmos.DrawLine(origin + a * radius, origin + b * radius);
                }
            }

            var left = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * direction;
            var right = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * direction;
            Gizmos.DrawLine(origin + left * innerRadius, origin + left * shownReach);
            Gizmos.DrawLine(origin + right * innerRadius, origin + right * shownReach);
            if (!IsAttacking) return;
            var radial = Quaternion.AngleAxis(_previousAngle, Vector3.up) * direction;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(origin + radial * innerRadius, origin + radial * shownReach);
            Gizmos.DrawWireSphere(origin + radial * shownReach, hitThickness);
        }
    }
}
