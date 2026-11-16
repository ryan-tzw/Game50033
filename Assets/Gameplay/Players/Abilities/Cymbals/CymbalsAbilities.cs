namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // runs after Player so a dodge started this frame is seen before the hit advances.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CymbalsAbilities : PlayerAbilities
    {
        public enum Phase { Windup, Approach, FollowThrough, Recovery }

        [SerializeField] private CymbalsWeaponVisual weaponVisual;
        [SerializeField] private CymbalsTuning tuning;

        private readonly HashSet<IHittable> _hitThisStrike = new();
        private Collider[] _hitBuffer = new Collider[32];
        private CymbalsTuning.Hit _hit;
        private Vector3 _direction;
        private Vector3 _lastHitPoint;
        private float _startedAt;
        private float _readyAt;
        private float _comboExpiresAt;
        private int _nextHitIndex;
        private bool _lungeRequested;
        private bool _impactResolved;

        public bool IsAttacking { get; private set; }
        public int ComboStep { get; private set; }
        public Phase CurrentPhase { get; private set; }

        private void Awake()
        {
            if (weaponVisual == null) weaponVisual = GetComponent<CymbalsWeaponVisual>();
        }

        private void OnEnable()
        {
            if (tuning == null)
            {
                Debug.LogError("CymbalsAbilities requires a CymbalsTuning asset.", this);
                enabled = false;
            }
        }

        public override bool TryUsePrimary()
        {
            if (!isActiveAndEnabled) return false;
            var hits = tuning.combo.hits;
            if (!Player.CanUseAbilities || IsAttacking || Time.time < _readyAt
                || hits == null || hits.Length == 0) return false;

            if (Time.time > _comboExpiresAt) _nextHitIndex = 0;
            _nextHitIndex = Mathf.Clamp(_nextHitIndex, 0, hits.Length - 1);
            _hit = hits[_nextHitIndex];
            _hit.windup = Mathf.Max(0.01f, _hit.windup);
            _hit.approach = Mathf.Max(0.01f, _hit.approach);
            _hit.followThrough = Mathf.Max(0.01f, _hit.followThrough);
            _hit.recovery = Mathf.Max(0.01f, _hit.recovery);

            _direction = Player.AimDirection;
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Player.Movement.FacingDirection;

            ComboStep = _nextHitIndex + 1;
            CurrentPhase = Phase.Windup;
            _startedAt = Time.time;
            _lungeRequested = false;
            _impactResolved = false;
            _hitThisStrike.Clear();
            IsAttacking = true;
            BlocksMovement = true;
            // the windup stays open to a dash; only the strike itself is committed.
            BlocksDodge = false;
            BlocksAbilities = true;
            return true;
        }

        private void Update()
        {
            var combo = tuning.combo;
            if (!IsAttacking)
            {
                // a dash between hits keeps the chain alive until it ends.
                if (Player.Movement.IsDodging) _comboExpiresAt = Mathf.Max(_comboExpiresAt, Time.time + combo.comboResetDelay);
                else if (_nextHitIndex > 0 && Time.time > _comboExpiresAt) _nextHitIndex = 0;
                return;
            }
            if (Player.Health.IsDowned || !Player.HasActiveInput)
            {
                Cancel();
                return;
            }

            // a dash started during the windup or recovery (both leave BlocksDodge false).
            if (Player.Movement.IsDodging)
            {
                if (_impactResolved) FinishHit();
                else AbortHit();
                return;
            }

            var elapsed = Time.time - _startedAt;
            var impactAt = _hit.windup + _hit.approach;
            var followEndsAt = impactAt + _hit.followThrough;

            if (elapsed < _hit.windup)
            {
                SetPhase(Phase.Windup, elapsed / _hit.windup);
                return;
            }

            if (!_lungeRequested)
            {
                // starting the lunge with the approach lets the windup be dash-cancelled,
                // because movement refuses a dodge while a displacement is pending.
                _lungeRequested = true;
                if (_hit.lungeDistance > 0f)
                    Player.Movement.TryStartDisplacement(_direction, _hit.lungeDistance, _hit.approach, combo.lungeEasing);
            }

            if (elapsed < impactAt)
            {
                BlocksDodge = true;
                SetPhase(Phase.Approach, (elapsed - _hit.windup) / _hit.approach);
                return;
            }

            // resolves once even if a slow frame skips past the impact moment.
            if (!_impactResolved)
            {
                _impactResolved = true;
                ResolveImpact();
                if (!IsAttacking || !isActiveAndEnabled) return;

                // show the contact frame: the swinging cymbals sit exactly on the hitting point as damage lands.
                BlocksDodge = true;
                SetPhase(Phase.FollowThrough, 0f);
                return;
            }

            if (elapsed < followEndsAt)
            {
                BlocksDodge = true;
                SetPhase(Phase.FollowThrough, (elapsed - impactAt) / _hit.followThrough);
                return;
            }

            BlocksDodge = false;
            var recoveryProgress = (elapsed - followEndsAt) / _hit.recovery;
            SetPhase(Phase.Recovery, recoveryProgress);
            if (recoveryProgress >= 1f) FinishHit();
        }

        private void SetPhase(Phase phase, float progress)
        {
            CurrentPhase = phase;
            if (weaponVisual != null)
                weaponVisual.SetStrikePose(_nextHitIndex, _direction, phase, Mathf.Clamp01(progress));
        }

        private void ResolveImpact()
        {
            var combo = tuning.combo;
            // the hitting point follows the player through the lunge, so read the position now.
            var point = combo.GetHitPoint(transform.position, _direction);
            _lastHitPoint = point;

            // gameplay is on the XZ plane: the damage circle extends as a column so target height does not matter.
            // triggers are ignored because they are detection/range volumes (the boss has a 10-unit one), not bodies.
            var bottom = new Vector3(point.x, transform.position.y + combo.hitRadius, point.z);
            var top = bottom + Vector3.up * Mathf.Max(0f, combo.hitColumnHeight - 2f * combo.hitRadius);
            int count;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(bottom, top, combo.hitRadius, _hitBuffer, combo.hitMask, QueryTriggerInteraction.Ignore);
                if (count < _hitBuffer.Length) break;
                Array.Resize(ref _hitBuffer, _hitBuffer.Length * 2);
            }

            for (var i = 0; i < count; i++)
            {
                var candidate = _hitBuffer[i];
                if (candidate == null || candidate.GetComponentInParent<Player>() != null) continue;
                var target = candidate.GetComponentInParent<IHittable>();
                if (target == null || _hitThisStrike.Contains(target)
                    || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                _hitThisStrike.Add(target);
                target.TakeHit(new HitInfo(gameObject, _hit.damage, _direction));

                // a hit callback may down or disable the attacker; stop resolving immediately.
                if (!IsAttacking || !isActiveAndEnabled) return;
                if (combo.hitEffect != null)
                    combo.hitEffect.Play(new HitEffectRequest(ContactPoint(candidate, point), _direction, _hit.damage, _hit.bigHit));
            }
        }

        // ClosestPoint only supports primitive and convex colliders.
        private static Vector3 ContactPoint(Collider target, Vector3 point) =>
            target is MeshCollider { convex: false } ? target.bounds.ClosestPoint(point) : target.ClosestPoint(point);

        // the hit landed: release restrictions and let the next press continue the chain.
        private void FinishHit()
        {
            var combo = tuning.combo;
            EndAttackState();
            _nextHitIndex++;
            _comboExpiresAt = Time.time + combo.comboResetDelay;
            _readyAt = Time.time;
            if (_nextHitIndex >= combo.hits.Length)
            {
                _nextHitIndex = 0;
                _readyAt += combo.comboCooldown;
            }
            if (weaponVisual != null) weaponVisual.Cancel();
        }

        // the hit never landed: keep the same combo step for the next press.
        private void AbortHit()
        {
            EndAttackState();
            _comboExpiresAt = Time.time + tuning.combo.comboResetDelay;
            if (Player.Movement != null && !Player.Movement.IsDodging) Player.Movement.CancelDisplacement();
            if (weaponVisual != null) weaponVisual.Cancel();
        }

        private void EndAttackState()
        {
            IsAttacking = false;
            ComboStep = 0;
            _hitThisStrike.Clear();
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }

        public override bool TryUseSecondary() => false;
        public override bool TryUseDefensive() => false;
        public override bool TryUseSpecial() => false;

        public override void Cancel()
        {
            EndAttackState();
            _nextHitIndex = 0;
            if (Player.Movement != null) Player.Movement.CancelDisplacement();
            if (weaponVisual != null) weaponVisual.Cancel();
        }

        private void OnDrawGizmosSelected()
        {
            if (tuning == null) return;
            var combo = tuning.combo;
            var center = transform.position + Vector3.up * combo.hitHeight;
            var direction = IsAttacking ? _direction : Vector3.forward;

            // the ring every strike's hitting point lies on, and the damage circle at the current aim.
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            DrawCircle(center, combo.hitDistance);
            Gizmos.color = new Color(1f, 0.8f, 0.25f);
            DrawCircle(IsAttacking && _impactResolved ? _lastHitPoint : combo.GetHitPoint(transform.position, direction), combo.hitRadius);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            const int segments = 40;
            for (var i = 0; i < segments; i++)
            {
                var a = Quaternion.AngleAxis(360f * i / segments, Vector3.up) * Vector3.forward;
                var b = Quaternion.AngleAxis(360f * (i + 1) / segments, Vector3.up) * Vector3.forward;
                Gizmos.DrawLine(center + a * radius, center + b * radius);
            }
        }
    }
}
