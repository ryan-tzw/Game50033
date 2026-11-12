namespace Game50033
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class ResonatingAttack : MonoBehaviour
    {
        [Tooltip("The attack collider on this GameObject, it's active only during its hit window.")]
        [SerializeField] private Collider hitVolume;
        [Tooltip("Only Reverberations using one of these effect assets can be activated.")]
        [SerializeField] private ReverberationEffect[] compatibleEffects = Array.Empty<ReverberationEffect>();

        private int _collisionMask = Physics.AllLayers;

        public Player SourcePlayer { get; private set; }
        public Collider HitVolume => hitVolume;
        public bool IsReady =>
            SourcePlayer != null && isActiveAndEnabled
            && hitVolume != null && hitVolume.gameObject == gameObject && hitVolume.enabled;

        private void Awake()
        {
            if (hitVolume == null)
            {
                hitVolume = GetComponent<Collider>();
            }
        }

        public void Initialize(Player sourcePlayer)
        {
            Initialize(sourcePlayer, Physics.AllLayers);
        }

        public void Initialize(Player sourcePlayer, int collisionMask)
        {
            if (sourcePlayer == null)
            {
                throw new ArgumentNullException(nameof(sourcePlayer));
            }

            // Prevent reuse even after Unity destroys the source player.
            if (!ReferenceEquals(SourcePlayer, null))
            {
                throw new InvalidOperationException("A Resonating attack can only be initialized once.");
            }

            if (hitVolume == null)
            {
                hitVolume = GetComponent<Collider>();
            }

            if (hitVolume == null || hitVolume.gameObject != gameObject)
            {
                throw new InvalidOperationException("A Resonating attack needs a collider on the same GameObject.");
            }

            SourcePlayer = sourcePlayer;
            _collisionMask = collisionMask;
        }

        internal bool CanHit(Collider other)
        {
            return other != null && hitVolume != null
                && (_collisionMask & (1 << other.gameObject.layer)) != 0
                && !Physics.GetIgnoreLayerCollision(hitVolume.gameObject.layer, other.gameObject.layer)
                && !Physics.GetIgnoreCollision(hitVolume, other);
        }

        internal bool Accepts(ReverberationEffect effect)
        {
            if (effect == null || compatibleEffects == null)
            {
                return false;
            }

            foreach (var compatibleEffect in compatibleEffects)
            {
                if (compatibleEffect == effect)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryContact(other);
        }

        private void TryContact(Collider other)
        {
            if (!other.TryGetComponent(out Reverberation reverberation))
            {
                return;
            }

            ResonanceResolver.TryActivate(reverberation, this);
        }
    }
}
