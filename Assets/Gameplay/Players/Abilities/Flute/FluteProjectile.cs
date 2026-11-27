namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody), typeof(ResonatingAttack))]
    public sealed class FluteProjectile : MonoBehaviour
    {
        public enum Kind { Secondary, Charged, Primary }

        [SerializeField] private FluteTuning tuning;
        [SerializeField] private Kind kind;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private Transform visualRoot;

        private SphereCollider _hitVolume;
        private Rigidbody _body;
        private ResonatingAttack _resonatingAttack;
        private Player _source;
        private Collider _homingTarget;
        private Vector3 _direction;
        private Vector3 _previousPosition;
        private Vector3 _visualScale;
        private float _damage;
        private float _travelled;
        private float _expiresAt;
        private float _nextTargetSearchAt;
        private Reverberation _reverberationPrefab;
        private float _revStrength;
        private bool _finished;
        private DebugProjectileTrace _debugTrace;
        private bool _pulsing;
        private bool _bigHit;
        private IHittable _pulseTarget;
        private Component _pulseReceiver;
        private Vector3 _contactOffset;
        private int _pulsesRemaining;
        private float _pulseDamage;
        private float _damageRemaining;
        private float _nextPulseAt;
        private float _presentationScale = 1f;
        private TrailRenderer _trail;
        private MaterialPropertyBlock _headProperties;
        private MeshRenderer[] _meshHeads;
        private SpriteRenderer _noteHead;
        private Color _flightColor;
        private float _visualStartedAt;
        private float _visualSide = 1f;
        private float _visualFlow;
        private float _resonanceFlashUntil = float.NegativeInfinity;
        private bool _secondaryFlowRewarded;
        private FluteProjectile _replayPrefab;
        private ProjectileReplay _replay;
        private float _visualCharge;

        internal bool TryClaimSecondaryFlowReward()
        {
            if (kind != Kind.Secondary || _source == null || _secondaryFlowRewarded) return false;
            _secondaryFlowRewarded = true;
            return true;
        }

        public LayerMask CollisionMask => collisionMask;
        public SphereCollider HitVolume => GetComponent<SphereCollider>();
        public ResonatingAttack ResonatingAttack => GetComponent<ResonatingAttack>();
        public float MaximumTravelDistance => Mathf.Min(Range, Speed * Lifetime);

        private float Speed => _replay?.Speed ?? (kind switch
        {
            Kind.Primary => tuning.primary.speed,
            Kind.Charged => tuning.special.speed,
            _ => tuning.secondary.speed
        });
        private float Range => _replay?.Distance ?? (kind switch
        {
            Kind.Primary => tuning.primary.range,
            Kind.Charged => tuning.special.range,
            _ => tuning.secondary.range
        });
        private float Lifetime => _replay?.Lifetime ?? (kind switch
        {
            Kind.Primary => tuning.primary.lifetime,
            Kind.Charged => tuning.special.lifetime,
            _ => tuning.secondary.lifetime
        });

        public bool IsConfigured
        {
            get
            {
                var volume = HitVolume;
                var body = GetComponent<Rigidbody>();
                var attack = ResonatingAttack;
                return tuning != null && enabled && gameObject.activeSelf
                    && volume != null && volume.enabled && volume.isTrigger && volume.center == Vector3.zero
                    && body != null && body.isKinematic && !body.useGravity && body.detectCollisions
                    && attack != null && (attack.HitVolume == null || attack.HitVolume == volume);
            }
        }

        private void Awake()
        {
            _hitVolume = GetComponent<SphereCollider>();
            _body = GetComponent<Rigidbody>();
            _resonatingAttack = GetComponent<ResonatingAttack>();
            _previousPosition = transform.position;
            if (visualRoot == null)
            {
                return;
            }
            _visualScale = visualRoot.localScale;
            _meshHeads = visualRoot.GetComponentsInChildren<MeshRenderer>();
            if (tuning == null || tuning.visuals.material == null)
            {
                return;
            }

            var visual = tuning.visuals;
            if (kind != Kind.Secondary && tuning.melody.noteSprite != null && visual.noteMaterial != null)
            {
                // Give each renderer its own object; Unity rejects a sprite beside an existing mesh renderer.
                var head = new GameObject("Musical note");
                head.transform.SetParent(visualRoot, false);
                head.layer = LayerMask.NameToLayer("WeaponVisual");
                _noteHead = head.AddComponent<SpriteRenderer>();
                _noteHead.sprite = tuning.melody.noteSprite;
                _noteHead.sharedMaterial = visual.noteMaterial;
                _noteHead.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _noteHead.enabled = kind == Kind.Primary
                    ? tuning.melody.showPrimaryNoteHead : tuning.melody.showSpecialNoteHead;
                var size = kind == Kind.Primary ? tuning.melody.primaryNoteSize : tuning.melody.chargedNoteSize;
                _visualScale = Vector3.one * (size / Mathf.Max(0.001f, _noteHead.sprite.bounds.size.y));
                visualRoot.localScale = _visualScale;
                var camera = Camera.main;
                if (camera != null)
                {
                    visualRoot.rotation = camera.transform.rotation;
                }
                foreach (var renderer in _meshHeads)
                {
                    renderer.enabled = false;
                }
            }
            var ribbon = new GameObject("Projectile ribbon");
            ribbon.transform.SetParent(visualRoot, false);
            ribbon.layer = LayerMask.NameToLayer("WeaponVisual");
            _trail = ribbon.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = visual.material;
            _trail.time = visual.trailDuration;
            _trail.minVertexDistance = 0.02f;
            _trail.startWidth = kind switch
            {
                Kind.Primary => tuning.melody.primaryRibbonWidth,
                Kind.Secondary => tuning.melody.secondaryRibbonWidth,
                _ => visual.trailWidth
            };
            _trail.endWidth = 0f;
            _trail.startColor = visual.color;
            _trail.endColor = new Color(visual.color.r, visual.color.g, visual.color.b, 0f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _flightColor = visual.color;
            SetHeadColor(visual.color);
        }

        private void OnEnable()
        {
            ResonanceResolver.Activated += HandleResonance;
            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        private void OnDisable()
        {
            ResonanceResolver.Activated -= HandleResonance;
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            // Disabling or unloading a projectile is cancellation, never a REV-producing impact.
            _finished = true;
            _debugTrace?.Finish();
            _reverberationPrefab = null;
            if (_hitVolume != null)
            {
                _hitVolume.enabled = false;
            }
            if (_resonatingAttack != null)
            {
                _resonatingAttack.enabled = false;
            }
            HideHead();
        }

        public void InitializePrimary(Player source, Vector3 direction, Collider target, float damage)
        {
            Initialize(source, direction, damage, false);
            _homingTarget = target;
        }

        public void InitializeSecondary(Player source, Vector3 direction, float damage, FluteProjectile replayPrefab = null)
        {
            _replayPrefab = replayPrefab;
            Initialize(source, direction, damage, true);
        }

        public void InitializeCharged(Player source, Vector3 direction, float damage, bool canResonate,
            Reverberation reverberationPrefab, float reverberationStrength, FluteProjectile replayPrefab = null)
        {
            _replayPrefab = replayPrefab;
            Initialize(source, direction, damage, canResonate);
            _reverberationPrefab = reverberationPrefab;
            _revStrength = reverberationStrength;
        }

        private void Initialize(Player source, Vector3 direction, float damage, bool canResonate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (!ReferenceEquals(_source, null))
            {
                throw new InvalidOperationException("A Flute projectile can only be initialized once.");
            }
            _source = source;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _damage = Mathf.Max(0f, damage);
            _expiresAt = Time.time + Lifetime;
            if (_replay != null) collisionMask = _replay.Mask;
            _resonatingAttack.Initialize(source, collisionMask);
            _resonatingAttack.enabled = canResonate;
            if (canResonate && _replayPrefab != null) _resonatingAttack.CaptureProjectileReplay = CaptureReplay;
            _visualStartedAt = Time.time;
            var flow = source.GetComponent<FluteFlow>();
            _visualFlow = flow != null ? flow.Normalized : 0f;
            if (kind == Kind.Primary && source.TryGetComponent<FluteWeaponVisual>(out var weaponVisual))
            {
                _visualSide = weaponVisual.TakeProjectileVisualSide();
            }
        }

        public void SetChargePresentation(float charge)
        {
            _visualCharge = charge;
            _bigHit = charge >= 1f;
            _presentationScale = Mathf.Lerp(1f, tuning.visuals.chargedScale, Mathf.SmoothStep(0f, 1f, charge));
            _flightColor = Color.Lerp(tuning.visuals.color, tuning.visuals.fullChargeColor, charge);
            if (_trail != null)
            {
                _trail.startWidth = tuning.visuals.trailWidth * _presentationScale;
                _trail.startColor = _flightColor;
                var color = _trail.startColor;
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
            SetHeadColor(_flightColor);
        }

        private void SetHeadColor(Color color)
        {
            if (visualRoot == null)
            {
                return;
            }
            if (_noteHead != null)
            {
                _noteHead.color = color;
                return;
            }
            if (_meshHeads == null)
            {
                return;
            }
            _headProperties ??= new MaterialPropertyBlock();
            foreach (var renderer in _meshHeads)
            {
                renderer.GetPropertyBlock(_headProperties);
                _headProperties.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_headProperties);
            }
        }

        private void FixedUpdate()
        {
            if (_finished || ReferenceEquals(_source, null))
            {
                return;
            }
            if (_source == null || !_source.isActiveAndEnabled || !_source.HasActiveInput)
            {
                Finish(false);
                return;
            }
            if (_pulsing)
            {
                UpdatePulses();
                return;
            }
            if (Time.time >= _expiresAt || _travelled >= Range)
            {
                Finish(true);
                return;
            }
            if (kind == Kind.Primary)
            {
                UpdateHoming();
            }

            var start = _body.position;
            _previousPosition = start;
            var scale = transform.lossyScale;
            var radius = _hitVolume.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var distance = Mathf.Min(Speed * Time.fixedDeltaTime, Range - _travelled);
            _source.Session?.Debug.BeginSphereSweep(this, ref _debugTrace, start, radius, _direction * distance);
            foreach (var overlap in Physics.OverlapSphere(start, radius, collisionMask, QueryTriggerInteraction.Collide))
            {
                HandleContact(overlap);
                if (_finished || _pulsing)
                {
                    return;
                }
            }

            // resolve projected fields only along the path the projectile can actually travel.
            var hits = Physics.SphereCastAll(start, radius, _direction, distance, collisionMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
            var sweptDistance = 0f;
            foreach (var hit in hits)
            {
                if (ShouldIgnore(hit.collider) || hit.collider.isTrigger)
                {
                    continue;
                }
                _resonatingAttack.ActivateAlongSweep(start + _direction * sweptDistance, start + _direction * hit.distance, radius);
                sweptDistance = hit.distance;
                _debugTrace?.CompleteFrame(distance > 0f ? hit.distance / distance : 0f);
                SetPosition(start + _direction * Mathf.Min(distance, hit.distance + 0.005f));
                HandleContact(hit.collider);
                if (_finished || _pulsing)
                {
                    return;
                }
            }
            _resonatingAttack.ActivateAlongSweep(start + _direction * sweptDistance, start + _direction * distance, radius);
            _debugTrace?.CompleteFrame(1f);
            SetPosition(start + _direction * distance);
            _travelled += distance;
            if (_travelled >= Range)
            {
                Finish(true);
            }
        }

        private void UpdateHoming()
        {
            var target = _homingTarget != null ? _homingTarget.GetComponentInParent<IHittable>() : null;
            var available = _homingTarget != null && _homingTarget.enabled && _homingTarget.gameObject.activeInHierarchy
                && target != null && (!(target is Behaviour receiver) || receiver.isActiveAndEnabled);
            if (!available && Time.time >= _nextTargetSearchAt)
            {
                _homingTarget = FluteTargeting.FindTarget(_source, transform.position,
                    Mathf.Min(tuning.primary.range, Range - _travelled), collisionMask, _hitVolume);
                _nextTargetSearchAt = Time.time + 0.1f;
            }
            else if (!available)
            {
                _homingTarget = null;
            }
            if (_homingTarget == null || !_homingTarget.enabled || !_homingTarget.gameObject.activeInHierarchy)
            {
                return;
            }
            var offset = FluteTargeting.GetAimPoint(_homingTarget, transform.position) - transform.position;
            if (offset.sqrMagnitude > 0.0001f)
            {
                _direction = Vector3.RotateTowards(_direction, offset.normalized,
                    tuning.primary.turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f).normalized;
            }
        }

        private bool ShouldIgnore(Collider other)
        {
            return other == null || other.transform.IsChildOf(transform)
                || other.GetComponentInParent<Player>() != null || !_resonatingAttack.CanHit(other);
        }

        private void HandleContact(Collider other)
        {
            if (_finished || _pulsing || ShouldIgnore(other))
            {
                return;
            }
            if (_source == null || !_source.isActiveAndEnabled || !_source.HasActiveInput)
            {
                Finish(false);
                return;
            }
            if (other.isTrigger)
            {
                return;
            }

            var target = other.GetComponentInParent<IHittable>();
            if (target is Behaviour receiver && !receiver.isActiveAndEnabled)
            {
                return;
            }
            var damage = _damage * _resonatingAttack.DamageMultiplier;
            if (kind == Kind.Secondary && target is Component component)
            {
                _pulsing = true;
                _pulseTarget = target;
                _pulseReceiver = component;
                _contactOffset = component.transform.InverseTransformPoint(transform.position);
                _pulsesRemaining = Mathf.Clamp(_replay?.PulseCount ?? tuning.secondary.pulseCount, 1, 8);
                _pulseDamage = damage / _pulsesRemaining;
                _damageRemaining = damage;
                StopCollisions();
                HideHead();
                ApplyPulse();
                return;
            }

            var point = other.ClosestPoint(transform.position);
            // Finish before callbacks so additional colliders cannot apply the same hit twice.
            Finish(true);
            if (target != null)
            {
                target.TakeHit(new HitInfo(_source.gameObject, damage, _direction));
                PlayHit(point, damage);
            }
        }

        private void UpdatePulses()
        {
            if (_pulseReceiver == null || !_pulseReceiver.gameObject.activeInHierarchy
                || (_pulseReceiver is Behaviour receiver && !receiver.isActiveAndEnabled))
            {
                Finish(false);
                return;
            }
            SetPosition(_pulseReceiver.transform.TransformPoint(_contactOffset));
            if (Time.time >= _nextPulseAt)
            {
                ApplyPulse();
            }
        }

        private void ApplyPulse()
        {
            var damage = _pulsesRemaining == 1 ? _damageRemaining : _pulseDamage;
            _damageRemaining -= damage;
            _pulsesRemaining--;
            _nextPulseAt = Time.time + (_replay?.PulseSpacing ?? tuning.secondary.pulseSpacing);
            _pulseTarget.TakeHit(new HitInfo(_source.gameObject, damage, _direction));
            PlayHit(transform.position, damage);
            if (_pulsesRemaining == 0)
            {
                Finish(false);
            }
        }

        private void PlayHit(Vector3 point, float damage)
        {
            if (tuning.visuals.hitEffect != null)
            {
                tuning.visuals.hitEffect.Play(new HitEffectRequest(point, _direction, damage, _bigHit));
            }
        }

        private void HandleResonance(ResonanceActivation activation)
        {
            if (activation.Attack != _resonatingAttack)
            {
                return;
            }
            _resonanceFlashUntil = Time.time + tuning.melody.resonanceFlashDuration;
            var effect = tuning.melody.resonanceEffect != null
                ? tuning.melody.resonanceEffect : tuning.visuals.accentEffect;
            if (effect != null)
            {
                effect.Play(new HitEffectRequest(activation.Position, _direction, 0f, true));
            }
        }

        private void LateUpdate()
        {
            if (_finished || _pulsing || visualRoot == null)
            {
                return;
            }
            var interpolation = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            visualRoot.position = Vector3.Lerp(_previousPosition, transform.position, interpolation);
            visualRoot.localScale = _visualScale * (_presentationScale * (1f + 0.06f * Mathf.Sin(Time.time * 18f)));
            var camera = Camera.main;
            if (_noteHead != null && camera != null)
            {
                // Offset only the drawing; keep the homing trajectory and hit volume authoritative.
                var age = Time.time - _visualStartedAt;
                var launch = 1f - Mathf.Clamp01(age / 0.18f);
                var offset = kind == Kind.Primary
                    ? tuning.melody.primaryVisualOffset * _visualSide * launch * launch * launch : 0f;
                visualRoot.position += camera.transform.right * offset
                    + camera.transform.up * (Mathf.Sin(age * 12f) * 0.012f);
                visualRoot.rotation = camera.transform.rotation
                    * Quaternion.AngleAxis(Mathf.Sin(age * 10f) * 16f * _visualSide, Vector3.forward);
            }
            var flash = Mathf.Clamp01((_resonanceFlashUntil - Time.time) / tuning.melody.resonanceFlashDuration);
            var color = Color.Lerp(_flightColor, tuning.visuals.fullChargeColor, flash);
            SetHeadColor(Color.Lerp(Color.white, color, tuning.visuals.projectileTint));
            if (_trail != null)
            {
                color.a = Mathf.Lerp(tuning.visuals.minimumTrailOpacity, 1f, _visualFlow);
                _trail.startColor = color;
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!ReferenceEquals(_source, null) && (collisionMask.value & (1 << other.gameObject.layer)) != 0)
            {
                HandleContact(other);
            }
        }

        private void SetPosition(Vector3 position)
        {
            _body.position = position;
            transform.position = position;
        }

        private void StopCollisions()
        {
            _debugTrace?.Finish();
            _hitVolume.enabled = false;
            _resonatingAttack.enabled = false;
        }

        private void HideHead()
        {
            if (_trail != null)
            {
                _trail.emitting = false;
            }
            if (_noteHead != null)
            {
                _noteHead.enabled = false;
            }
            if (_meshHeads != null)
            {
                foreach (var renderer in _meshHeads)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void Finish(bool createReverberation)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            StopCollisions();
            HideHead();
            Destroy(gameObject, tuning.visuals.trailDuration);
            if (createReverberation && _source != null && _source.isActiveAndEnabled && _source.HasActiveInput
                && _reverberationPrefab != null)
            {
                var reverberation = Instantiate(_reverberationPrefab, transform.position, Quaternion.identity);
                reverberation.Initialize(_source, _revStrength);
            }
        }

        private IAttackReplay CaptureReplay(Vector3 contact)
        {
            var distance = Range - _travelled - Mathf.Max(0f, Vector3.Dot(contact - _previousPosition, _direction));
            var remainingTime = _expiresAt - Time.time;
            return distance > 0f && remainingTime > 0f ? new ProjectileReplay(this, contact, distance, remainingTime) : null;
        }

        private sealed class ProjectileReplay : IAttackReplay
        {
            private readonly FluteProjectile _prefab;
            private readonly Player _owner;
            private readonly Vector3 _origin, _direction;
            private readonly float _damage, _radius, _charge;
            public readonly float Speed, Distance, Lifetime, PulseSpacing;
            public readonly int PulseCount;
            public readonly LayerMask Mask;

            public ProjectileReplay(FluteProjectile shot, Vector3 origin, float distance, float lifetime)
            {
                _prefab = shot._replayPrefab;
                _owner = shot._source;
                _origin = origin;
                _direction = shot._direction;
                _damage = shot._damage;
                _radius = shot._hitVolume.radius;
                _charge = shot._visualCharge;
                Speed = shot.Speed;
                Distance = distance;
                Lifetime = lifetime;
                Mask = shot.collisionMask;
                PulseCount = shot.tuning.secondary.pulseCount;
                PulseSpacing = shot.tuning.secondary.pulseSpacing;
            }

            public void Replay(float damageMultiplier)
            {
                if (_owner == null || _prefab == null) return;
                var shot = Instantiate(_prefab, _origin, Quaternion.LookRotation(_direction, Vector3.up));
                shot._replay = this;
                shot._hitVolume.radius = _radius;
                shot.Initialize(_owner, _direction, _damage * damageMultiplier, false);
                if (shot.kind == Kind.Charged) shot.SetChargePresentation(_charge);
            }
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            Finish(false);
        }
    }
}
