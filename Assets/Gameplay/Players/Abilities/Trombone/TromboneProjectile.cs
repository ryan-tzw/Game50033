namespace Game50033
{
    using System;
    using UnityEngine;
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody), typeof(ResonatingAttack))]
    public sealed class TromboneProjectile : MonoBehaviour
    {
        private const float HitOverlapDistance = 0.005f;
        public enum ProjectileKind { Primary, Special }

        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private ProjectileKind kind;
        private TromboneTuning.ProjectileSettings Settings => kind == ProjectileKind.Special
            ? tuning.special.projectile : tuning.primary.projectile;
        private float speed => _replayFlight?.Speed ?? Settings.speed;
        private float maximumDistance => _replayFlight?.Distance ?? Settings.maximumDistance;
        private float lifetime => _replayFlight?.Lifetime ?? Settings.lifetime;
        private LayerMask collisionMask => _replayFlight?.Mask ?? Settings.collisionMask;
        private SphereCollider _hitVolume;
        private Rigidbody _rigidbody;
        private ResonatingAttack _resonatingAttack;
        private Player _sourcePlayer;
        private Vector3 _flightDirection;
        private float _damage;
        private float _distanceTravelled;

        private float _expiresAt;
        private Reverberation _reverberationPrefab;
        private float _reverberationStrength;
        private ProjectileReplay _specialReplay;
        private FlightSettings? _replayFlight;
        private TromboneProjectile _replayPrefab;
        private Vector3 _stepOrigin;
        private bool _finished;
        public float MaximumTravelDistance => Mathf.Min(maximumDistance, speed * lifetime);
        public LayerMask CollisionMask => collisionMask;
        public SphereCollider HitVolume => GetComponent<SphereCollider>();
        public ResonatingAttack ResonatingAttack => GetComponent<ResonatingAttack>();
        public bool IsConfigured
        {
            get
            {
                var volume = GetComponent<SphereCollider>();
                var body = GetComponent<Rigidbody>();
                var resonatingAttack = GetComponent<ResonatingAttack>();

                if (tuning == null || !enabled || !gameObject.activeSelf)
                {
                    return false;
                }

                if (volume == null || !volume.enabled || !volume.isTrigger || volume.center != Vector3.zero)
                {
                    return false;
                }

                if (body == null || !body.isKinematic || body.useGravity || !body.detectCollisions)
                {
                    return false;
                }

                if (resonatingAttack == null)
                {
                    return false;
                }

                return resonatingAttack.HitVolume == null || resonatingAttack.HitVolume == volume;
            }
        }
        private void OnEnable()
        {
            if (tuning != null) return;
            Debug.LogError("TromboneProjectile requires Trombone tuning.", this);
            enabled = false;
        }

        private void Awake()
        {
            _hitVolume = GetComponent<SphereCollider>();
            _rigidbody = GetComponent<Rigidbody>();
            _resonatingAttack = GetComponent<ResonatingAttack>();
        }
        private void Initialize(Player source, Vector3 direction, float damage, bool canResonate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!ReferenceEquals(_sourcePlayer, null))
            {
                throw new InvalidOperationException("A Trombone projectile can only be initialized once.");
            }

            _sourcePlayer = source;
            _stepOrigin = transform.position;
            _flightDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _damage = Mathf.Max(0f, damage);
            _hitVolume.radius = Mathf.Max(0.01f, _replayFlight?.Radius ?? Settings.radius);
            _expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
            _resonatingAttack.Initialize(source, collisionMask);
            _resonatingAttack.enabled = canResonate;
        }

        public void InitializePrimary(Player source, Vector3 direction, float damage, bool canResonate,
            TromboneProjectile replayPrefab = null)
        {
            Initialize(source, direction, damage, canResonate);
            _replayPrefab = replayPrefab;
            if (canResonate && replayPrefab != null) _resonatingAttack.CaptureProjectileReplay = CaptureReplay;
        }
        public void InitializeSpecial(Player source, Vector3 direction, float damage, Reverberation reverberationPrefab,
            float reverberationStrength, TromboneProjectile replayPrefab = null)
        {
            if (float.IsNaN(reverberationStrength) || float.IsInfinity(reverberationStrength) || reverberationStrength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(reverberationStrength));
            }

            Initialize(source, direction, damage, false);
            _reverberationPrefab = reverberationPrefab;
            _reverberationStrength = reverberationStrength;
            if (replayPrefab != null)
                _specialReplay = new ProjectileReplay(replayPrefab, source, transform.position, _flightDirection, _damage,
                    new FlightSettings(Settings));
        }
        private void FixedUpdate()
        {
            // Distinguish an uninitialized projectile from one whose player was destroyed.
            if (ReferenceEquals(_sourcePlayer, null) || _finished)
            {
                return;
            }

            if (_sourcePlayer == null)
            {
                Finish(createReverberation: false);
                return;
            }

            if (Time.time >= _expiresAt || _distanceTravelled >= maximumDistance)
            {
                Finish(createReverberation: true);
                return;
            }

            var start = _rigidbody.position;
            _stepOrigin = start;
            var scale = transform.lossyScale;
            var radius = _hitVolume.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            foreach (var overlap in Physics.OverlapSphere(start, radius, collisionMask, QueryTriggerInteraction.Collide))
            {
                HandleContact(overlap);

                if (_finished)
                {
                    return;
                }
            }
            // test the projected sweep separately, stopping at the first blocking 3d hit.
            var distance = Mathf.Min(speed * Time.fixedDeltaTime, maximumDistance - _distanceTravelled);
            var hits = Physics.SphereCastAll(
                start,
                radius,
                _flightDirection,
                distance,
                collisionMask,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

            foreach (var hit in hits)
            {
                if (ShouldIgnore(hit.collider) || hit.collider.isTrigger)
                {
                    continue;
                }

                _resonatingAttack.ActivateAlongSweep(start, start + _flightDirection * hit.distance, radius);
                SetPosition(start + _flightDirection * Mathf.Min(distance, hit.distance + HitOverlapDistance));
                HandleContact(hit.collider);

                if (_finished)
                {
                    return;
                }
            }

            _resonatingAttack.ActivateAlongSweep(start, start + _flightDirection * distance, radius);
            SetPosition(start + _flightDirection * distance);
            _distanceTravelled += distance;

            if (_distanceTravelled >= maximumDistance)
            {
                Finish(createReverberation: true);
            }
        }

        private bool ShouldIgnore(Collider other)
        {
            if (other == null || other.transform.IsChildOf(transform))
            {
                return true;
            }

            if (other.GetComponentInParent<Player>() != null)
            {
                return true;
            }

            return !_resonatingAttack.CanHit(other);
        }

        private void HandleContact(Collider other)
        {
            if (_finished || ShouldIgnore(other))
            {
                return;
            }

            if (_sourcePlayer == null)
            {
                Finish(createReverberation: false);
                return;
            }

            if (other.isTrigger)
            {
                return;
            }

            var target = other.GetComponentInParent<IHittable>();
            if (target != null)
            {
                var damage = _damage * _resonatingAttack.DamageMultiplier;
                target.TakeHit(new HitInfo(_sourcePlayer.gameObject, damage, _flightDirection));
                // presentation only: the hit splat (sprite, damage number, sound).
                if (tuning != null && tuning.hitEffect != null)
                    tuning.hitEffect.Play(new HitEffectRequest(other.ClosestPoint(transform.position), _flightDirection,
                        damage, kind == ProjectileKind.Special));
            }
            Finish(createReverberation: true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ReferenceEquals(_sourcePlayer, null)
                || (collisionMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            HandleContact(other);
        }

        private void SetPosition(Vector3 position)
        {
            _rigidbody.position = position;
            transform.position = position;
        }

        private void Finish(bool createReverberation)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _hitVolume.enabled = false;
            _resonatingAttack.enabled = false;

            // Queue cleanup before creating REV in case its initialization fails.
            Destroy(gameObject);

            if (createReverberation && _sourcePlayer != null && _reverberationPrefab != null)
            {
                var reverberation = Instantiate(_reverberationPrefab, transform.position, Quaternion.identity);
                reverberation.Initialize(_sourcePlayer, _reverberationStrength, null, _specialReplay);
            }
        }

        private IAttackReplay CaptureReplay(Vector3 contact)
        {
            var distance = maximumDistance - _distanceTravelled - Mathf.Max(0f, Vector3.Dot(contact - _stepOrigin, _flightDirection));
            var remainingTime = _expiresAt - Time.time;
            return distance > 0f && remainingTime > 0f
                ? new ProjectileReplay(_replayPrefab, _sourcePlayer, contact, _flightDirection, _damage,
                    new FlightSettings(Settings, distance, remainingTime))
                : null;
        }

        private readonly struct FlightSettings
        {
            public readonly float Speed, Distance, Lifetime, Radius;
            public readonly LayerMask Mask;

            public FlightSettings(TromboneTuning.ProjectileSettings settings, float? distance = null, float? lifetime = null)
            {
                Speed = settings.speed;
                Distance = distance ?? settings.maximumDistance;
                Lifetime = lifetime ?? settings.lifetime;
                Radius = settings.radius;
                Mask = settings.collisionMask;
            }
        }

        private sealed class ProjectileReplay : IAttackReplay
        {
            private readonly TromboneProjectile _prefab;
            private readonly Player _owner;
            private readonly Vector3 _origin;
            private readonly Vector3 _direction;
            private readonly float _damage;
            private readonly FlightSettings _flight;

            public ProjectileReplay(TromboneProjectile prefab, Player owner, Vector3 origin, Vector3 direction, float damage, FlightSettings flight)
            {
                _prefab = prefab;
                _owner = owner;
                _origin = origin;
                _direction = direction;
                _damage = damage;
                _flight = flight;
            }

            public void Replay(float damageMultiplier)
            {
                if (_owner == null || _prefab == null) return;
                var shot = Instantiate(_prefab, _origin, Quaternion.LookRotation(_direction));
                shot._replayFlight = _flight;
                shot.Initialize(_owner, _direction, _damage * damageMultiplier, canResonate: false);
            }
        }
    }
}
