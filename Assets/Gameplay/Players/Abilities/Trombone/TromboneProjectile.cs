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
        private float speed => Settings.speed;
        private float maximumDistance => _replayDistance ?? Settings.maximumDistance;
        private float lifetime => _replayLifetime ?? Settings.lifetime;
        private LayerMask collisionMask => Settings.collisionMask;

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
        private float? _replayDistance;
        private float? _replayLifetime;
        private bool _isReplay;
        private TromboneProjectile _replayPrefab;
        private Vector3 _stepOrigin;
        private bool _finished;
        private DebugProjectileTrace _debugTrace;

        internal float ShotPressureNormalized { get; private set; }

        public float MaximumTravelDistance => Mathf.Min(maximumDistance, speed * lifetime);
        public LayerMask CollisionMask => collisionMask;
        public SphereCollider HitVolume => GetComponent<SphereCollider>();
        public ResonatingAttack ResonatingAttack => GetComponent<ResonatingAttack>();

        private void Awake()
        {
            _hitVolume = GetComponent<SphereCollider>();
            _rigidbody = GetComponent<Rigidbody>();
            _resonatingAttack = GetComponent<ResonatingAttack>();
        }

        private void Initialize(Player source, Vector3 direction, float damage, bool canResonate, float pressure = 0f)
        {
            _sourcePlayer = source;
            _stepOrigin = transform.position;
            _flightDirection = direction.normalized;
            _damage = damage;
            ShotPressureNormalized = pressure;
            _hitVolume.radius = Settings.radius;
            _expiresAt = Time.time + lifetime;
            _resonatingAttack.Initialize(source, collisionMask);
            _resonatingAttack.enabled = canResonate;
        }

        public void InitializePrimary(Player source, Vector3 direction, float damage, bool canResonate,
            TromboneProjectile replayPrefab = null, float pressure = 0f)
        {
            Initialize(source, direction, damage, canResonate, pressure);
            _replayPrefab = replayPrefab;
            if (canResonate && replayPrefab != null) _resonatingAttack.CaptureProjectileReplay = CaptureReplay;
        }

        public void InitializeSpecial(Player source, Vector3 direction, float damage, Reverberation reverberationPrefab,
            float reverberationStrength, TromboneProjectile replayPrefab = null, float pressure = 0f)
        {
            Initialize(source, direction, damage, false, pressure);
            _reverberationPrefab = reverberationPrefab;
            _reverberationStrength = reverberationStrength;
            if (replayPrefab != null)
                _specialReplay = new ProjectileReplay(replayPrefab, source, transform.position, _flightDirection, _damage,
                    ShotPressureNormalized);
        }

        private void FixedUpdate()
        {
            if (_finished)
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

            var distance = Mathf.Min(speed * Time.fixedDeltaTime, maximumDistance - _distanceTravelled);
            _sourcePlayer.Session?.Debug.BeginSphereSweep(this, ref _debugTrace, start, radius, _flightDirection * distance);

            // Resolve REV contacts before damage so Amp affects the hit.
            _resonatingAttack.ActivateAlongSweep(start, start, radius);

            foreach (var overlap in Physics.OverlapSphere(start, radius, collisionMask, QueryTriggerInteraction.Collide))
            {
                HandleContact(overlap);

                if (_finished)
                {
                    return;
                }
            }

            // test the projected sweep separately, stopping at the first blocking 3d hit.
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
                _debugTrace?.CompleteFrame(distance > 0f ? hit.distance / distance : 0f);
                SetPosition(start + _flightDirection * Mathf.Min(distance, hit.distance + HitOverlapDistance));
                HandleContact(hit.collider);

                if (_finished)
                {
                    return;
                }
            }

            _resonatingAttack.ActivateAlongSweep(start, start + _flightDirection * distance, radius);
            _debugTrace?.CompleteFrame(1f);
            SetPosition(start + _flightDirection * distance);
            _distanceTravelled += distance;

            if (_distanceTravelled >= maximumDistance)
            {
                Finish(createReverberation: true);
            }
        }

        private bool ShouldIgnore(Collider other)
        {
            if (other.transform.IsChildOf(transform))
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
                target.TakeHit(HitInfo.FromResonatingAttack(_sourcePlayer.gameObject, damage, _flightDirection,
                    _damage, _isReplay));

                if (tuning.hitEffect != null)
                    tuning.hitEffect.Play(new HitEffectRequest(other.ClosestPoint(transform.position), _flightDirection,
                        damage, kind == ProjectileKind.Special));
            }

            Finish(createReverberation: true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((collisionMask.value & (1 << other.gameObject.layer)) == 0)
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
            _debugTrace?.Finish();
            _hitVolume.enabled = false;
            _resonatingAttack.enabled = false;

            Destroy(gameObject);

            if (createReverberation && _sourcePlayer != null && _reverberationPrefab != null)
            {
                var reverberation = Instantiate(_reverberationPrefab, transform.position, Quaternion.identity);
                reverberation.Initialize(_sourcePlayer, _reverberationStrength, null, _specialReplay);
            }
        }

        private IAttackReplay CaptureReplay(Vector3 contact)
        {
            // replay: preserve remaining range and lifetime at REV contact
            var distance = maximumDistance - _distanceTravelled - Mathf.Max(0f, Vector3.Dot(contact - _stepOrigin, _flightDirection));
            var remainingTime = _expiresAt - Time.time;
            return distance > 0f && remainingTime > 0f
                ? new ProjectileReplay(_replayPrefab, _sourcePlayer, contact, _flightDirection, _damage,
                    ShotPressureNormalized, distance, remainingTime)
                : null;
        }

        private sealed class ProjectileReplay : IAttackReplay
        {
            private readonly TromboneProjectile _prefab;
            private readonly Player _owner;
            private readonly Vector3 _origin;
            private readonly Vector3 _direction;
            private readonly float _damage;
            private readonly float _pressure;
            private readonly float? _remainingDistance;
            private readonly float? _remainingLifetime;

            public ProjectileReplay(TromboneProjectile prefab, Player owner, Vector3 origin, Vector3 direction, float damage,
                float pressure, float? remainingDistance = null, float? remainingLifetime = null)
            {
                _prefab = prefab;
                _owner = owner;
                _origin = origin;
                _direction = direction;
                _damage = damage;
                _pressure = pressure;
                _remainingDistance = remainingDistance;
                _remainingLifetime = remainingLifetime;
            }

            public void Replay(float damageMultiplier)
            {
                if (_owner == null) return;

                var shot = Instantiate(_prefab, _origin, Quaternion.LookRotation(_direction));
                shot._replayDistance = _remainingDistance;
                shot._replayLifetime = _remainingLifetime;
                shot._isReplay = true;
                shot.Initialize(_owner, _direction, _damage * damageMultiplier, canResonate: false, pressure: _pressure);
            }
        }
    }
}
