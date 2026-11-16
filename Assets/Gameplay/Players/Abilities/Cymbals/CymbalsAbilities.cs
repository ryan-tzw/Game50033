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
        public enum ThrowState { None, Windup, Outbound, Returning, Dashing }

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

        // secondary: the right cymbal is thrown out to an apex and returns; one recast per throw.
        private readonly HashSet<IHittable> _hitThisPass = new();
        private Vector3 _throwDirection;
        private Vector3 _throwOrigin;
        private Vector3 _returnFrom;
        private Vector3 _cymbalPosition;
        private Vector3 _dashDirection;
        private float _throwPhaseStartedAt;
        private float _dashDuration;
        private float _throwReadyAt;
        private float _flightSpeed;
        private int _lastSecondaryFrame = -10;
        private bool _recastUsed;

        public bool IsAttacking { get; private set; }
        public int ComboStep { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public ThrowState CurrentThrow { get; private set; }
        public bool IsThrowing => CurrentThrow != ThrowState.None;
        public bool RecastUsed => _recastUsed;
        public Vector3 ThrownCymbalPosition => _cymbalPosition;

        // the apex window spans the end of the outbound and the start of the return.
        public bool InApexWindow
        {
            get
            {
                var half = tuning.cymbalThrow.apexWindow * 0.5f;
                var elapsed = Time.time - _throwPhaseStartedAt;
                return CurrentThrow == ThrowState.Outbound && elapsed >= tuning.cymbalThrow.outboundTime - half
                    || CurrentThrow == ThrowState.Returning && elapsed <= half;
            }
        }

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
            // the combo needs both cymbals, so it waits for a thrown cymbal to come back.
            if (!Player.CanUseAbilities || IsAttacking || IsThrowing || Time.time < _readyAt
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
            if (IsThrowing)
            {
                UpdateThrow();
                return;
            }

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
            DamageColumn(point, combo.hitRadius, _hitThisStrike, _hit.damage, _direction, _hit.bigHit);
        }

        // gameplay is on the XZ plane: a damage circle extends as a column so target height does not matter.
        // triggers are ignored because they are detection/range volumes (the boss has a 10-unit one), not bodies.
        // returns false when a hit callback downed or disabled this player, so the caller stops.
        private bool DamageColumn(Vector3 point, float radius, HashSet<IHittable> alreadyHit, float damage, Vector3 direction, bool big)
        {
            var combo = tuning.combo;
            var bottom = new Vector3(point.x, transform.position.y + radius, point.z);
            var top = bottom + Vector3.up * Mathf.Max(0f, combo.hitColumnHeight - 2f * radius);
            int count;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _hitBuffer, combo.hitMask, QueryTriggerInteraction.Ignore);
                if (count < _hitBuffer.Length) break;
                Array.Resize(ref _hitBuffer, _hitBuffer.Length * 2);
            }

            for (var i = 0; i < count; i++)
            {
                var candidate = _hitBuffer[i];
                if (candidate == null || candidate.GetComponentInParent<Player>() != null) continue;
                var target = candidate.GetComponentInParent<IHittable>();
                if (target == null || alreadyHit.Contains(target)
                    || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                alreadyHit.Add(target);
                target.TakeHit(new HitInfo(gameObject, damage, direction));

                if (!isActiveAndEnabled || Player.Health.IsDowned) return false;
                if (combo.hitEffect != null)
                    combo.hitEffect.Play(new HitEffectRequest(ContactPoint(candidate, point), direction, damage, big));
            }
            return true;
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

        // -----------------------------
        // secondary: cymbal throw
        // -----------------------------

        public override bool TryUseSecondary()
        {
            if (!isActiveAndEnabled) return false;

            // Player retries this every frame while held; a recast needs a fresh press, not the throw's own hold.
            var freshPress = _lastSecondaryFrame < Time.frameCount - 1;
            _lastSecondaryFrame = Time.frameCount;

            if (CurrentThrow == ThrowState.Outbound || CurrentThrow == ThrowState.Returning)
                return freshPress && TryRecast();
            if (IsThrowing) return false;
            if (!Player.CanUseAbilities || IsAttacking || Time.time < _throwReadyAt) return false;

            _throwDirection = Player.AimDirection;
            _throwDirection.y = 0f;
            _throwDirection = _throwDirection.sqrMagnitude > 0.0001f ? _throwDirection.normalized : Player.Movement.FacingDirection;
            CurrentThrow = ThrowState.Windup;
            _throwPhaseStartedAt = Time.time;
            _recastUsed = false;
            // the throw motion roots the player; a dash cancels it. Abilities stay unblocked so the held
            // button keeps registering (BlocksAbilities would stop the calls and fake a fresh recast press).
            BlocksMovement = true;
            BlocksDodge = false;
            BlocksAbilities = false;
            return true;
        }

        private void UpdateThrow()
        {
            if (Player.Health.IsDowned || !Player.HasActiveInput)
            {
                Cancel();
                return;
            }

            var settings = tuning.cymbalThrow;
            var height = Vector3.up * tuning.combo.hitHeight;
            var elapsed = Time.time - _throwPhaseStartedAt;

            if (CurrentThrow == ThrowState.Windup)
            {
                if (Player.Movement.IsDodging)
                {
                    EndThrow(startCooldown: false);
                    return;
                }
                if (weaponVisual != null) weaponVisual.SetThrowWindup(_throwDirection, Mathf.Clamp01(elapsed / settings.windup));
                if (elapsed < settings.windup) return;

                // release: the cymbal flies on its own and the player is free again.
                CurrentThrow = ThrowState.Outbound;
                _throwPhaseStartedAt = Time.time;
                _throwOrigin = transform.position;
                _hitThisPass.Clear();
                BlocksMovement = false;
                elapsed = 0f;
            }

            if (CurrentThrow == ThrowState.Outbound)
            {
                var t = Mathf.Clamp01(elapsed / settings.outboundTime);
                var apex = _throwOrigin + _throwDirection * settings.range;
                _cymbalPosition = Vector3.LerpUnclamped(_throwOrigin, apex, settings.outboundEasing.Evaluate(t)) + height;
                _flightSpeed = Slope(settings.outboundEasing, t);
                if (!FlightDamage(settings, _throwDirection)) return;
                if (t >= 1f)
                {
                    CurrentThrow = ThrowState.Returning;
                    _throwPhaseStartedAt = Time.time;
                    _returnFrom = apex;
                    _hitThisPass.Clear();
                }
            }
            else if (CurrentThrow == ThrowState.Returning)
            {
                // homes on the player's current position, speeding up on the way back.
                var t = Mathf.Clamp01(elapsed / settings.returnTime);
                var target = transform.position;
                _cymbalPosition = Vector3.LerpUnclamped(_returnFrom, target, settings.returnEasing.Evaluate(t)) + height;
                _flightSpeed = Slope(settings.returnEasing, t);
                var back = target - _returnFrom;
                back.y = 0f;
                if (!FlightDamage(settings, back.sqrMagnitude > 0.0001f ? back.normalized : -_throwDirection)) return;
                if (t >= 1f)
                {
                    EndThrow(startCooldown: true);
                    return;
                }
            }
            else if (CurrentThrow == ThrowState.Dashing)
            {
                // damage rides the combo's hitting point ahead of the dashing player.
                var combo = tuning.combo;
                var point = combo.GetHitPoint(transform.position, _dashDirection);
                if (!DamageColumn(point, combo.hitRadius, _hitThisPass, settings.dashDamage, _dashDirection, true)) return;
                _flightSpeed = 0f;
                if (weaponVisual != null) weaponVisual.SetCharge(_dashDirection);
                if (!Player.Movement.IsDisplacing || elapsed >= _dashDuration + 0.1f)
                {
                    EndThrow(startCooldown: true);
                    return;
                }
            }

            if (weaponVisual != null) weaponVisual.SetThrownCymbal(_cymbalPosition, _flightSpeed, InApexWindow && !_recastUsed);
        }

        private bool FlightDamage(CymbalsTuning.ThrowSettings settings, Vector3 direction) =>
            DamageColumn(_cymbalPosition, settings.hitRadius, _hitThisPass, settings.damage, direction, false);

        // normalised flight speed for presentation: the curve's slope, where 2 is the default peak.
        private static float Slope(AnimationCurve curve, float t)
        {
            const float step = 0.02f;
            var a = Mathf.Clamp01(t - step);
            var b = Mathf.Clamp01(t + step);
            return b > a ? Mathf.Clamp01(Mathf.Abs(curve.Evaluate(b) - curve.Evaluate(a)) / (b - a) * 0.5f) : 0f;
        }

        private bool TryRecast()
        {
            if (_recastUsed) return false;
            var settings = tuning.cymbalThrow;
            var nearApex = InApexWindow;
            _recastUsed = true;

            // every recast leaves a Burst where the cymbal is.
            if (settings.burstReverberation != null)
            {
                var burst = Instantiate(settings.burstReverberation, _cymbalPosition, Quaternion.identity);
                burst.Initialize(Player, Mathf.Max(0.01f, settings.burstStrength));
            }

            if (!nearApex) return true;

            // near the apex the player also dashes to the cymbal and catches it.
            var offset = _cymbalPosition - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude - settings.catchOffset;
            if (distance <= 0.05f || offset.sqrMagnitude < 0.0001f)
            {
                EndThrow(startCooldown: true);
                return true;
            }

            _dashDirection = offset.normalized;
            _dashDuration = Mathf.Max(0.05f, distance / settings.dashSpeed);
            if (!Player.Movement.TryStartDisplacement(_dashDirection, distance, _dashDuration, settings.dashEasing))
                return true; // could not dash; the Burst still happened and the cymbal flies on

            CurrentThrow = ThrowState.Dashing;
            _throwPhaseStartedAt = Time.time;
            _hitThisPass.Clear();
            BlocksMovement = true;
            BlocksDodge = true;
            BlocksAbilities = true;
            return true;
        }

        private void EndThrow(bool startCooldown)
        {
            CurrentThrow = ThrowState.None;
            _hitThisPass.Clear();
            _flightSpeed = 0f;
            if (startCooldown) _throwReadyAt = Time.time + tuning.cymbalThrow.cooldown;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (weaponVisual != null) weaponVisual.EndThrow();
        }
        public override bool TryUseDefensive() => false;
        public override bool TryUseSpecial() => false;

        public override void Cancel()
        {
            if (IsThrowing) EndThrow(startCooldown: true);
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

            if (!IsThrowing || CurrentThrow == ThrowState.Windup) return;
            Gizmos.color = InApexWindow ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.5f, 0.3f);
            DrawCircle(_cymbalPosition, tuning.cymbalThrow.hitRadius);
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
