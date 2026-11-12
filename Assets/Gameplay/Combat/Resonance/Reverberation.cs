namespace Game50033
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class Reverberation : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 2f;
        [SerializeField] private ReverberationEffect effect;
        [Tooltip("Optional child GameObject shown only while this Reverberation is available.")]
        [SerializeField] private GameObject visualRoot;

        private SphereCollider _volume;
        private float _expiresAt;
        private bool _consumed;

        public Player Creator { get; private set; }
        public ReverberationEffect Effect => effect;
        public float Strength { get; private set; } = 1f;
        public float RemainingLifetime => Mathf.Max(0f, _expiresAt - Time.time);
        public bool IsConsumed => _consumed;

        public bool IsAvailable
        {
            get
            {
                if (_consumed || !isActiveAndEnabled)
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

        private void Awake()
        {
            ConfigurePhysics();
            if (ReferenceEquals(Creator, null))
            {
                _expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
            }

            SetVisualActive(false);
        }

        private void OnEnable()
        {
            SetVisualActive(IsAvailable);
        }

        private void OnDisable()
        {
            SetVisualActive(false);
        }

        public void Initialize(Player creator, float strength = 1f)
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
            Creator = creator;
            Strength = strength;
            _expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
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
            if (Time.time >= _expiresAt)
            {
                SetVisualActive(false);
                Destroy(gameObject);
            }
        }

        internal bool TryConsume()
        {
            if (!IsAvailable)
            {
                return false;
            }

            // Prevent other colliders from consuming the same REV this frame.
            _consumed = true;
            _volume.enabled = false;
            SetVisualActive(false);
            return true;
        }

        private void SetVisualActive(bool active)
        {
            if (visualRoot != null && visualRoot != gameObject && visualRoot.transform.IsChildOf(transform))
            {
                visualRoot.SetActive(active);
            }
        }
    }
}
