namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using Game50033.Examples;
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class Reverberation : MonoBehaviour
    {
        [SerializeField] private ReverberationEffect effect;
        [SerializeField] private ReverberationEnergyField energyFieldPrefab;

        private static readonly List<Reverberation> _active = new();
        internal static IReadOnlyList<Reverberation> Active => _active;

        private SphereCollider _volume;
        private ReverberationEnergyField _energyField;
        private ReverberationFieldSpawn _spawn;
        private Vector3 _groundAimOffset;
        private float _expiresAt;
        private bool _consumed;
        private bool _retired;

        public Player Creator { get; private set; }
        public ReverberationEffect Effect => effect;
        public float Strength { get; private set; } = 1f;
        public float RemainingLifetime => _retired ? 0f : Mathf.Max(0f, _expiresAt - Time.time);
        public bool IsConsumed => _consumed;

        public bool IsAvailable
        {
            get
            {
                if (_retired || !isActiveAndEnabled)
                {
                    return false;
                }

                if (Creator == null || effect == null)
                {
                    return false;
                }

                return _volume != null && _volume.enabled && Time.time < _expiresAt;
            }
        }

        internal SphereCollider Volume => _volume;
        internal IAttackReplay OriginalAttack { get; private set; }
        internal Vector3 GroundOrigin { get; private set; }
        internal Vector3 GroundAimPoint => GroundOrigin + _groundAimOffset * FootprintScale;
        internal Vector3[] FootprintEdges { get; private set; }
        internal float FootprintRadius { get; private set; }
        internal float FootprintScale => _spawn.GetScale(_spawn.Progress);

        private void Awake()
        {
            ConfigurePhysics();
            if (ReferenceEquals(Creator, null))
            {
                _expiresAt = Time.time + GameplayLifetime;
            }
        }

        private void OnEnable()
        {
            if (!IsAvailable) return;
            _active.Add(this);
            SetVisualActive(true);
        }

        private void OnDisable()
        {
            _active.Remove(this);
            SetVisualActive(false);
        }

        public void Initialize(Player creator, float strength = 1f)
        {
            Initialize(creator, strength, null);
        }

        /// <summary>Supplies a footprint in world-aligned XZ units relative to the grounded spawn centre.</summary>
        public void Initialize(Player creator, float strength, ReverberationFieldShape? footprint)
        {
            Initialize(creator, strength, footprint, null);
        }

        internal void Initialize(Player creator, float strength, ReverberationFieldShape? footprint, IAttackReplay originalAttack)
        {
            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            if (float.IsNaN(strength) || float.IsInfinity(strength) || strength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(strength));
            }

            // Prevent reuse even after Unity destroys the creator.
            if (!ReferenceEquals(Creator, null))
            {
                throw new InvalidOperationException("A Reverberation can only be initialized once.");
            }

            if (effect == null)
            {
                throw new InvalidOperationException("Assign a Reverberation effect before spawning it.");
            }

            ConfigurePhysics();
            var scale = _volume.transform.lossyScale;
            var radius = _volume.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var shape = footprint ?? new ReverberationFieldShape
            {
                kind = ReverberationFieldShapeKind.Circle,
                outerRadius = radius,
                stretchFactor = 1f
            };
            if (shape.kind != ReverberationFieldShapeKind.Circle && shape.kind != ReverberationFieldShapeKind.AnnularSector)
                throw new ArgumentException("A gameplay REV needs a circle or annular-sector footprint.", nameof(footprint));

            var ground = _volume.transform.TransformPoint(_volume.center);
            ground.y = 0f;
            GroundOrigin = ground + new Vector3(shape.origin.x, 0f, shape.origin.y);
            FootprintEdges = ReverberationOverlap.BuildEdges(shape, out var reach, out var aimOffset);
            FootprintRadius = reach;
            _groundAimOffset = aimOffset;

            var startedAt = Time.time;
            _expiresAt = startedAt + GameplayLifetime;
            if (energyFieldPrefab != null)
            {
                _spawn = energyFieldPrefab.GetSpawnAnimation(startedAt);
                // the footprint is authored in world units even if the legacy trigger has a scaled prefab root.
                _energyField = Instantiate(energyFieldPrefab, ground, Quaternion.identity);
                _energyField.transform.SetParent(transform, true);
                _energyField.SetShape(shape);
                var color = creator.PlayerColor;
                _energyField.ConfigureGameplayAppearance(color, Color.Lerp(Color.black, color, 0.35f),
                    Camera.main, _spawn, _expiresAt);
            }
            Creator = creator;
            OriginalAttack = originalAttack;
            Strength = strength;
            if (IsAvailable) _active.Add(this);
            SetVisualActive(IsAvailable);
        }

        private void ConfigurePhysics()
        {
            _volume = GetComponent<SphereCollider>();
            _volume.isTrigger = true;

            var body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
        }

        private void Update()
        {
            if (!_retired && Time.time >= _expiresAt) Retire();
        }

        private float GameplayLifetime => energyFieldPrefab != null
            ? energyFieldPrefab.GameplayLifetime : ReverberationEnergyFieldTuning.DefaultGameplayLifetime;

        internal bool TryConsume()
        {
            if (!IsAvailable)
            {
                return false;
            }

            _consumed = true;
            Retire();
            return true;
        }

        private void Retire()
        {
            // end gameplay contact now while allowing the visual to finish on the same object.
            _retired = true;
            _volume.enabled = false;
            _active.Remove(this);
            var fadeDuration = _consumed && _energyField != null ? _energyField.BeginConsumptionFadeOut() : 0f;
            if (fadeDuration <= 0f) SetVisualActive(false);
            Destroy(gameObject, fadeDuration);
        }

        private void SetVisualActive(bool active)
        {
            if (_energyField != null) _energyField.gameObject.SetActive(active);
        }
    }
}
