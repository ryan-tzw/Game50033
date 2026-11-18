namespace Game50033
{
    using static PlayerAbilityStats;
    using System;
    using System.Collections.Generic;
    using Game50033.Examples;
    using UnityEngine;

    // runs after Player so a dodge started this frame is seen before the hit advances.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CymbalsAbilities : PlayerAbilities
    {
        public enum Phase { Windup, Approach, FollowThrough, Recovery }
        public enum ThrowState { None, Windup, Outbound, Returning, Dashing }
        public enum CrashState { None, Windup, Impact, Recovery }

        [SerializeField] private CymbalsWeaponVisual weaponVisual;
        [SerializeField] private CymbalsTuning tuning;
        [Tooltip("Child with a convex trigger MeshCollider; reshaped into the Crash triangle and enabled only for the impact query.")]
        [SerializeField] private ResonatingAttack crashVolume;

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

        // special: crash, a triangular shockwave from the hitting point; charges refill one at a time.
        private readonly HashSet<IHittable> _hitThisCrash = new();
        private readonly Collider[] _crashBuffer = new Collider[64];
        private MeshCollider _crashCollider;
        private Mesh _crashMesh;
        private Vector3 _crashMeshSize;
        private Vector3 _crashDirection;
        private Vector3 _crashTip;
        private float _crashPhaseStartedAt;
        private float _nextCrashChargeAt;
        private int _crashCharges = -1;
        private bool _crashVolumeReady;

        // defensive: locked frontal parry, with one reward per press.
        private Vector3 _parryDirection = Vector3.forward;
        private float _parryStartedAt;
        private float _parryReadyAt;
        private int _lastDefensiveFrame = -10;
        private bool _parryRewarded;
        private float _debugParryAt = float.NegativeInfinity;

        // debug only: the areas hits actually queried, kept briefly so the gizmos can show them.
        private readonly List<(Vector3 point, float radius, float time)> _debugColumns = new();
        private float _debugCrashAt = float.NegativeInfinity;

        public bool IsAttacking { get; private set; }
        public int ComboStep { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public ThrowState CurrentThrow { get; private set; }
        public bool IsThrowing => CurrentThrow != ThrowState.None;
        public bool RecastUsed => _recastUsed;
        public Vector3 ThrownCymbalPosition => _cymbalPosition;
        public CrashState CurrentCrash { get; private set; }
        public bool IsCrashing => CurrentCrash != CrashState.None;
        public int CrashCharges => Mathf.Max(0, _crashCharges);
        public int MaxCrashCharges => tuning.crash.maxCharges;
        public bool IsParrying { get; private set; }
        public float ParryCooldownRemaining => Mathf.Max(0f, _parryReadyAt - Time.time);

        // body attack poses for PlayerAnimation; presentation only, read from the existing ability state.
        // free-walking parts (throw flight, the parry hold) report None so the walk cycle keeps playing.
        public override PlayerAttackPhase AnimationPhase
        {
            get
            {
                if (IsAttacking)
                    return CurrentPhase switch
                    {
                        Phase.Windup => PlayerAttackPhase.Windup,
                        Phase.Recovery => PlayerAttackPhase.Recovery,
                        _ => PlayerAttackPhase.Active,
                    };
                if (IsCrashing)
                    return CurrentCrash switch
                    {
                        CrashState.Windup => PlayerAttackPhase.Windup,
                        CrashState.Impact => PlayerAttackPhase.Active,
                        _ => PlayerAttackPhase.Recovery,
                    };
                if (IsThrowing)
                    return CurrentThrow switch
                    {
                        ThrowState.Windup => PlayerAttackPhase.Windup,
                        ThrowState.Dashing => PlayerAttackPhase.Active,
                        _ => PlayerAttackPhase.None,
                    };
                if (IsParrying && Time.time - _parryStartedAt < tuning.parry.raiseTime) return PlayerAttackPhase.Windup;
                return PlayerAttackPhase.None;
            }
        }
        public override PlayerAbilityStats GetAbilityStats(PlayerAbility ability)
        {
            if (tuning == null || !GetAbilityState(ability).Supported) return default;
            if (ability == PlayerAbility.Primary)
            {
                var hits = System.Array.ConvertAll(tuning.combo.hits, hit => Number(hit.damage));
                return new PlayerAbilityStats(("Damage / hit", string.Join(" / ", hits)),
                    ("Combo cooldown", Seconds(tuning.combo.comboCooldown)));
            }

            return ability switch
            {
                PlayerAbility.Secondary => new(("Throw damage", Number(tuning.cymbalThrow.damage)),
                    ("Cooldown", Seconds(tuning.cymbalThrow.cooldown)), ("Recast damage", Number(tuning.cymbalThrow.dashDamage))),
                PlayerAbility.Special => new(("Damage", Number(tuning.crash.damage)),
                    ("Recharge / charge", Seconds(tuning.crash.rechargeTime)), ("Charges", tuning.crash.maxCharges.ToString())),
                PlayerAbility.Defensive => new(("Parry window", Seconds(tuning.parry.duration)),
                    ("Cooldown", Seconds(tuning.parry.cooldown))),
                _ => default
            };
        }

        public override PlayerAbilityState GetAbilityState(PlayerAbility ability)
        {
            if (tuning == null) return default;
            return ability switch
            {
                PlayerAbility.Primary => new PlayerAbilityState(_readyAt - Time.time, tuning.combo.comboCooldown),
                PlayerAbility.Secondary => new PlayerAbilityState(_throwReadyAt - Time.time, tuning.cymbalThrow.cooldown),
                PlayerAbility.Defensive => new PlayerAbilityState(ParryCooldownRemaining, tuning.parry.cooldown),
                PlayerAbility.Special => new PlayerAbilityState(CrashRechargeRemaining, tuning.crash.rechargeTime, CrashCharges, MaxCrashCharges),
                _ => default
            };
        }

        public float CrashRechargeRemaining => CrashCharges < MaxCrashCharges ? Mathf.Max(0f, _nextCrashChargeAt - Time.time) : 0f;

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
                return;
            }
            if (_crashCharges < 0) _crashCharges = tuning.crash.maxCharges;
        }

        private void Start()
        {
            // the volume is a child of the player prefab; it only exists as a shape for impact queries.
            if (crashVolume == null) return;
            _crashCollider = crashVolume.GetComponent<MeshCollider>();
            if (_crashCollider == null)
            {
                Debug.LogError("Crash volume needs a MeshCollider on the same GameObject.", crashVolume);
                return;
            }
            _crashCollider.convex = true;
            _crashCollider.isTrigger = true;
            _crashCollider.enabled = false;
            crashVolume.Initialize(Player);
            _crashVolumeReady = true;
        }

        public override bool TryUsePrimary()
        {
            if (!isActiveAndEnabled) return false;
            var hits = tuning.combo.hits;
            // the combo needs both cymbals, so it waits for a thrown cymbal to come back.
            if (!Player.CanUseAbilities || IsAttacking || IsThrowing || IsCrashing || Time.time < _readyAt
                || hits == null || hits.Length == 0) return false;

            if (Time.time > _comboExpiresAt) _nextHitIndex = 0;
            _nextHitIndex = Mathf.Clamp(_nextHitIndex, 0, hits.Length - 1);
            _hit = hits[_nextHitIndex];
            _hit.windup = Mathf.Max(0.01f, _hit.windup);
            _hit.approach = Mathf.Max(0.01f, _hit.approach);
            _hit.followThrough = Mathf.Max(0.01f, _hit.followThrough);
            _hit.recovery = Mathf.Max(0.01f, _hit.recovery);

            _direction = Player.GetAttackDirection();
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
            TickCrashRecharge();
            if (IsParrying)
            {
                UpdateParry();
                return;
            }

            if (IsCrashing)
            {
                UpdateCrash();
                return;
            }

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
            if (tuning.showHitboxes)
            {
                if (_debugColumns.Count >= 64) _debugColumns.RemoveAt(0);
                _debugColumns.Add((new Vector3(point.x, transform.position.y, point.z), radius, Time.time));
            }
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
            if (!Player.CanUseAbilities || IsAttacking || IsCrashing || Time.time < _throwReadyAt) return false;

            _throwDirection = Player.GetAttackDirection();
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
        // -----------------------------
        // defensive: parry
        // -----------------------------

        public override bool TryUseDefensive()
        {
            // Player retries every frame while the button is held; only the first frame of a press counts,
            // so holding Defensive through the cooldown never parries again by itself.
            var fresh = Time.frameCount - _lastDefensiveFrame > 1;
            _lastDefensiveFrame = Time.frameCount;
            if (!isActiveAndEnabled || !fresh) return false;
            // both cymbals are needed, so the thrown cymbal must be back.
            if (!Player.CanUseAbilities || IsAttacking || IsThrowing || IsCrashing || IsParrying
                || Time.time < _parryReadyAt) return false;

            IsParrying = true;
            _parryStartedAt = Time.time;
            _parryRewarded = false;
            _parryDirection = Player.GetAttackDirection();
            _parryDirection.y = 0f;
            _parryDirection = _parryDirection.sqrMagnitude > 0.0001f
                ? _parryDirection.normalized : Player.Movement.FacingDirection;
            // free movement and a dash-cancel; no other abilities while the cymbals hold the parry.
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = true;
            if (weaponVisual != null) weaponVisual.SetParryPose(_parryDirection, 0f);
            return true;
        }

        private void UpdateParry()
        {
            if (Player.Health.IsDowned || !Player.HasActiveInput || Player.Movement.IsDodging)
            {
                EndParry();
                return;
            }

            var settings = tuning.parry;
            var elapsed = Time.time - _parryStartedAt;
            if (elapsed >= settings.duration)
            {
                EndParry();
                return;
            }
            if (weaponVisual != null) weaponVisual.SetParryPose(_parryDirection, elapsed / settings.raiseTime);
        }

        internal override bool TryParryHit(Vector3 incomingFrom)
        {
            if (!IsParrying || !isActiveAndEnabled || !Player.HasActiveInput || Player.Movement.IsDodging) return false;
            var settings = tuning.parry;
            if (Time.time - _parryStartedAt >= settings.duration) return false;
            incomingFrom.y = 0f;
            if (incomingFrom.sqrMagnitude < 0.00000001f
                || Vector3.Angle(_parryDirection, incomingFrom) > settings.coneAngle * 0.5f) return false;

            _debugParryAt = Time.time;
            if (settings.parryEffect != null)
            {
                var point = tuning.combo.GetHitPoint(transform.position, incomingFrom.normalized);
                settings.parryEffect.Play(new HitEffectRequest(point, _parryDirection, 0f, true));
            }
            if (weaponVisual != null) weaponVisual.PlayParryPop();

            if (_parryRewarded) return true;
            _parryRewarded = true;
            for (var i = 0; i < settings.crashChargeReward; i++) AddCrashCharge();
            if (settings.ampReverberation != null)
            {
                var field = Instantiate(settings.ampReverberation, transform.position, Quaternion.identity);
                field.Initialize(Player, 1f, new ReverberationFieldShape
                {
                    kind = ReverberationFieldShapeKind.AnnularSector,
                    direction = new Vector2(_parryDirection.x, _parryDirection.z),
                    outerRadius = settings.range,
                    sweepAngle = settings.coneAngle,
                    stretchFactor = 1f
                });
            }
            return true;
        }

        private void EndParry()
        {
            IsParrying = false;
            _parryReadyAt = Time.time + tuning.parry.cooldown;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (weaponVisual != null) weaponVisual.EndParry();
        }

        // -----------------------------
        // special: crash
        // -----------------------------

        public override bool TryUseSpecial()
        {
            // a fresh press only, so holding the button never spends both charges.
            if (!isActiveAndEnabled || !Player.SpecialPressedThisFrame) return false;
            if (!Player.CanUseAbilities || IsAttacking || IsThrowing || IsCrashing || CrashCharges <= 0) return false;

            _crashDirection = Player.GetAttackDirection();
            _crashDirection.y = 0f;
            _crashDirection = _crashDirection.sqrMagnitude > 0.0001f ? _crashDirection.normalized : Player.Movement.FacingDirection;
            CurrentCrash = CrashState.Windup;
            _crashPhaseStartedAt = Time.time;
            _hitThisCrash.Clear();
            BlocksMovement = true;
            BlocksDodge = false;
            BlocksAbilities = true;
            return true;
        }

        /// <summary>Grants one Crash charge immediately (a successful parry, later). No effect when full.</summary>
        public void AddCrashCharge()
        {
            if (CrashCharges >= MaxCrashCharges) return;
            _crashCharges++;
        }

        private void TickCrashRecharge()
        {
            var settings = tuning.crash;
            if (_crashCharges < 0) _crashCharges = settings.maxCharges;
            // one at a time: spending more does not restart the charge already refilling.
            while (_crashCharges < settings.maxCharges && Time.time >= _nextCrashChargeAt)
            {
                _crashCharges++;
                _nextCrashChargeAt += settings.rechargeTime;
            }
        }

        private void UpdateCrash()
        {
            if (Player.Health.IsDowned || !Player.HasActiveInput)
            {
                Cancel();
                return;
            }

            var settings = tuning.crash;
            var elapsed = Time.time - _crashPhaseStartedAt;
            switch (CurrentCrash)
            {
                case CrashState.Windup:
                    if (Player.Movement.IsDodging)
                    {
                        EndCrash();
                        return;
                    }
                    SetCrashPose(Phase.Windup, elapsed / settings.windup);
                    if (elapsed < settings.windup) return;

                    CurrentCrash = CrashState.Impact;
                    _crashPhaseStartedAt = Time.time;
                    BlocksDodge = true;
                    ResolveCrash();
                    if (!IsCrashing || !isActiveAndEnabled) return;
                    SetCrashPose(Phase.FollowThrough, 0f);
                    return;

                case CrashState.Impact:
                    SetCrashPose(Phase.FollowThrough, elapsed / settings.impactHold);
                    if (elapsed < settings.impactHold) return;
                    CurrentCrash = CrashState.Recovery;
                    _crashPhaseStartedAt = Time.time;
                    BlocksDodge = false;
                    return;

                case CrashState.Recovery:
                    if (Player.Movement.IsDodging || elapsed >= settings.recovery)
                    {
                        EndCrash();
                        return;
                    }
                    SetCrashPose(Phase.Recovery, elapsed / settings.recovery);
                    return;
            }
        }

        private void SetCrashPose(Phase phase, float progress)
        {
            if (weaponVisual != null) weaponVisual.SetCrashPose(_crashDirection, phase, Mathf.Clamp01(progress));
        }

        private void ResolveCrash()
        {
            var settings = tuning.crash;
            var combo = tuning.combo;

            // the charge is spent on the slam, so a dash-cancelled windup costs nothing.
            if (_crashCharges >= settings.maxCharges) _nextCrashChargeAt = Time.time + settings.rechargeTime;
            _crashCharges--;

            _crashTip = combo.GetHitPoint(transform.position, _crashDirection);
            _crashTip.y = transform.position.y;
            _debugCrashAt = Time.time;
            if (weaponVisual != null) weaponVisual.PlayCrashWaves(_crashTip, _crashDirection);
            if (!_crashVolumeReady) return;
            crashVolume.ResetDamageMultiplier();

            // shape and place the prism, then query it once with the collider enabled.
            var rotation = Quaternion.LookRotation(_crashDirection, Vector3.up);
            BuildCrashMesh(settings, combo.hitColumnHeight);
            crashVolume.transform.SetPositionAndRotation(_crashTip, rotation);
            _crashCollider.enabled = true;
            Physics.SyncTransforms();
            try
            {
                var crash = new CymbalsCrash(Player, _crashCollider, _crashTip, _crashDirection, rotation, settings, combo);
                var count = crash.Collect(_crashBuffer);
                crashVolume.AreaReplay = crash;

                // resonance uses the same ground triangle as the crash waves.
                settings.GetTriangle(_crashTip, _crashDirection, out var left, out var right);
                var fields = Reverberation.Active;
                var camera = fields.Count > 0 ? Camera.main : null;
                for (var i = fields.Count - 1; i >= 0; i = Mathf.Min(i - 1, fields.Count - 1))
                {
                    ResonanceResolver.TryActivateTriangle(fields[i], crashVolume, _crashTip, left, right, camera);
                    if (!isActiveAndEnabled || Player.Health.IsDowned) return;
                }

                crash.Resolve(crashVolume.DamageMultiplier, _crashBuffer, count, _hitThisCrash, this);
            }
            finally
            {
                crashVolume.AreaReplay = null;
                if (_crashCollider != null) _crashCollider.enabled = false;
            }
        }

        // a triangular prism in the volume's local space: tip at the origin, opening along +Z, from the feet up.
        private void BuildCrashMesh(CymbalsTuning.CrashSettings settings, float height)
        {
            // reshape only when the tuned size changed; cooking a convex mesh every crash is wasted work.
            var size = new Vector3(settings.width, height, settings.length);
            if (_crashMesh != null && _crashCollider.sharedMesh == _crashMesh && size == _crashMeshSize) return;
            _crashMeshSize = size;

            // detach before editing: changing a mesh the collider still holds leaves the collider empty.
            _crashCollider.sharedMesh = null;
            if (_crashMesh == null) _crashMesh = new Mesh { name = "Crash volume" };
            var half = settings.width * 0.5f;
            var l = settings.length;
            _crashMesh.Clear();
            _crashMesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(-half, 0f, l), new Vector3(half, 0f, l),
                new Vector3(0f, height, 0f), new Vector3(-half, height, l), new Vector3(half, height, l),
            };
            _crashMesh.triangles = new[]
            {
                0, 2, 1, 3, 4, 5,
                0, 1, 4, 0, 4, 3,
                1, 2, 5, 1, 5, 4,
                2, 0, 3, 2, 3, 5,
            };
            _crashMesh.RecalculateBounds();
            _crashCollider.sharedMesh = _crashMesh;
        }

        private void OnDestroy()
        {
            if (_crashMesh != null) Destroy(_crashMesh);
        }

        private void EndCrash()
        {
            CurrentCrash = CrashState.None;
            _hitThisCrash.Clear();
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
            if (weaponVisual != null) weaponVisual.Cancel();
        }

        public override void Cancel()
        {
            if (IsThrowing) EndThrow(startCooldown: true);
            if (IsCrashing) EndCrash();
            if (IsParrying) EndParry();
            EndAttackState();
            _nextHitIndex = 0;
            if (Player.Movement != null) Player.Movement.CancelDisplacement();
            if (weaponVisual != null) weaponVisual.Cancel();
        }

        // debug only. In play mode the hit areas draw for every player (no selection needed) when
        // tuning.showHitboxes is on; turn on Gizmos in the Game view toolbar to see them there too.
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || tuning == null || !tuning.showHitboxes) return;
            DrawHitAreas(LiveAim());
        }

        // edit-mode preview when the player is selected.
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying || tuning == null) return;
            DrawHitAreas(transform.forward);
        }

        private Vector3 LiveAim()
        {
            if (IsAttacking) return _direction;
            if (IsCrashing) return _crashDirection;
            if (IsParrying) return _parryDirection;
            if (Player == null) return transform.forward;
            // idle previews use movement facing without running targeting from a gizmo callback.
            var aim = Player.AimDirection;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f) return aim.normalized;
            return Player.Movement != null ? Player.Movement.FacingDirection : transform.forward;
        }

        private void DrawHitAreas(Vector3 aim)
        {
            var combo = tuning.combo;
            var feet = transform.position.y;
            var height = combo.hitColumnHeight;
            var now = Time.time;
            var flash = tuning.hitboxFlashTime;
            var flashColor = new Color(1f, 0.15f, 0.15f);

            // the ring every strike's hitting point lies on, and the damage column at the current aim.
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            DrawCircle(transform.position + Vector3.up * combo.hitHeight, combo.hitDistance);
            var hitPoint = combo.GetHitPoint(transform.position, aim);
            hitPoint.y = feet;
            Gizmos.color = new Color(1f, 0.85f, 0.25f);
            DrawColumn(hitPoint, combo.hitRadius, height);

            // the crash prism at the current aim (locked to the press direction during the windup),
            // then where the slam actually landed. _crashTip is only set at the slam, so it is stale during the windup.
            var crashFlashing = now - _debugCrashAt <= flash;
            var landed = crashFlashing || CurrentCrash == CrashState.Impact || CurrentCrash == CrashState.Recovery;
            var tip = landed ? _crashTip : hitPoint;
            tuning.crash.GetTriangle(tip, landed ? _crashDirection : aim, out var left, out var right);
            Gizmos.color = crashFlashing ? flashColor : new Color(1f, 0.6f, 0.2f, 0.6f);
            DrawPrism(tip, left, right, height);

            // the thrown cymbal's damage column.
            if (IsThrowing && CurrentThrow != ThrowState.Windup)
            {
                Gizmos.color = InApexWindow ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.5f, 0.3f);
                DrawColumn(new Vector3(_cymbalPosition.x, feet, _cymbalPosition.z), tuning.cymbalThrow.hitRadius, height);
            }

            // the parry cone: faint at the current aim, bright while up, red just after a parry.
            var parry = tuning.parry;
            Gizmos.color = now - _debugParryAt <= flash ? flashColor
                : IsParrying ? new Color(0.5f, 0.95f, 1f) : new Color(0.5f, 0.95f, 1f, 0.25f);
            DrawCone(transform.position, aim, parry.coneAngle, parry.range, height);

            // every column a hit actually queried recently (combo strikes, throw flight, recast dash).
            Gizmos.color = flashColor;
            for (var i = _debugColumns.Count - 1; i >= 0; i--)
            {
                var (point, radius, time) = _debugColumns[i];
                if (now - time > flash)
                {
                    _debugColumns.RemoveAt(i);
                    continue;
                }
                DrawColumn(point, radius, height);
            }
        }

        private static void DrawColumn(Vector3 bottom, float radius, float height)
        {
            var top = bottom + Vector3.up * height;
            DrawCircle(bottom, radius);
            DrawCircle(top, radius);
            Gizmos.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
            Gizmos.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
            Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
            Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        }

        private static void DrawCone(Vector3 origin, Vector3 aim, float angle, float range, float height)
        {
            const int segments = 16;
            var up = Vector3.up * height;
            var previous = origin;
            for (var i = 0; i <= segments; i++)
            {
                var point = origin + Quaternion.AngleAxis(Mathf.Lerp(-angle * 0.5f, angle * 0.5f, i / (float)segments), Vector3.up) * aim * range;
                Gizmos.DrawLine(previous, point);
                Gizmos.DrawLine(previous + up, point + up);
                if (i == 0 || i == segments) Gizmos.DrawLine(point, point + up);
                previous = point;
            }
            Gizmos.DrawLine(previous, origin);
            Gizmos.DrawLine(previous + up, origin + up);
            Gizmos.DrawLine(origin, origin + up);
        }

        private static void DrawPrism(Vector3 tip, Vector3 left, Vector3 right, float height)
        {
            var up = Vector3.up * height;
            Gizmos.DrawLine(tip, left);
            Gizmos.DrawLine(left, right);
            Gizmos.DrawLine(right, tip);
            Gizmos.DrawLine(tip + up, left + up);
            Gizmos.DrawLine(left + up, right + up);
            Gizmos.DrawLine(right + up, tip + up);
            Gizmos.DrawLine(tip, tip + up);
            Gizmos.DrawLine(left, left + up);
            Gizmos.DrawLine(right, right + up);
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
