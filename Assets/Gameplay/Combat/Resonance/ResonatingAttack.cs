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
        private bool _areaActive;

        public Player SourcePlayer { get; private set; }
        public Collider HitVolume => hitVolume;
        internal float DamageMultiplier { get; private set; } = 1f;
        internal IAttackReplay AreaReplay { private get; set; }
        internal Func<Vector3, IAttackReplay> CaptureProjectileReplay { private get; set; }
        internal IAttackReplay CaptureReplay(Vector3 contact) => AreaReplay ?? CaptureProjectileReplay?.Invoke(contact);
        public bool IsReady =>
            SourcePlayer != null && isActiveAndEnabled
            && (_areaActive || hitVolume != null && hitVolume.gameObject == gameObject && hitVolume.enabled);

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
            return other != null
                && (_collisionMask & (1 << other.gameObject.layer)) != 0
                && (_areaActive || hitVolume != null
                    && !Physics.GetIgnoreLayerCollision(hitVolume.gameObject.layer, other.gameObject.layer)
                    && !Physics.GetIgnoreCollision(hitVolume, other));
        }

        internal void BeginArea(Player sourcePlayer, int collisionMask)
        {
            SourcePlayer = sourcePlayer;
            _collisionMask = collisionMask;
            _areaActive = true;
            ResetDamageMultiplier();
        }

        internal void EndArea()
        {
            _areaActive = false;
            AreaReplay = null;
            SourcePlayer = null;
        }

        internal void ApplyDamageMultiplier(float multiplier)
        {
            DamageMultiplier = Mathf.Max(DamageMultiplier, multiplier);
        }

        internal void ResetDamageMultiplier()
        {
            DamageMultiplier = 1f;
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

        internal void ActivateAlongSweep(Vector3 start, Vector3 end, float radius)
        {
            if (!IsReady || hitVolume is not SphereCollider sphere) return;
            var centerOffset = sphere.transform.TransformVector(sphere.center);
            ResonanceResolver.ActivateAlongSweep(this, start + centerOffset, end + centerOffset, radius);
        }
    }
}
