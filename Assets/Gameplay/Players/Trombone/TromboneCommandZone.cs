namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class TromboneCommandZone : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float radius = 1f;
        [SerializeField] private Reverberation sustainReverberationPrefab;
        [Tooltip("Optional child GameObject displaying the Command Zone area.")]
        [SerializeField] private GameObject visualRoot;

        private Player _owner;
        private Scene _arenaScene;
        private bool _ended;

        // Keep destroyed owners marked as initialized so they still reach cleanup.
        public bool IsActive => !ReferenceEquals(_owner, null) && !_ended && isActiveAndEnabled;
        public Reverberation SustainReverberationPrefab => sustainReverberationPrefab;

        public bool IsOwnerInside
        {
            get
            {
                if (!IsActive || !OwnerCanPlay())
                {
                    return false;
                }

                var offset = _owner.transform.position - transform.position;
                offset.y = 0f;
                return offset.sqrMagnitude <= radius * radius;
            }
        }

        private bool OwnerCanPlay()
        {
            if (_owner == null || !_owner.isActiveAndEnabled || !_owner.HasActiveInput)
            {
                return false;
            }

            if (_owner.Health == null || _owner.Health.IsDowned)
            {
                return false;
            }

            if (_owner.Abilities == null || !_owner.Abilities.isActiveAndEnabled)
            {
                return false;
            }

            return _arenaScene.isLoaded && SceneManager.GetActiveScene().handle == _arenaScene.handle;
        }

        private void Awake()
        {
            SetVisualActive(false);
        }

        public void Initialize(Player owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!ReferenceEquals(_owner, null))
            {
                throw new InvalidOperationException("A Command Zone can only be initialized once.");
            }

            transform.SetParent(null, true);
            _owner = owner;
            _arenaScene = gameObject.scene;
            SetVisualActive(true);
        }

        private void LateUpdate()
        {
            if (!IsActive)
            {
                return;
            }

            // Check for exit directly because zones always spawn around their owner.
            if (IsOwnerInside)
            {
                return;
            }

            ConsumeZone();
        }

        public void ConsumeZone()
        {
            // Prevent owner removal or scene cleanup from creating Sustain REV.
            var createReverberation = OwnerCanPlay();
            CancelZone();

            if (!createReverberation)
            {
                return;
            }

            var reverberation = Instantiate(sustainReverberationPrefab, transform.position, Quaternion.identity);
            reverberation.Initialize(_owner);
        }

        public void CancelZone()
        {
            _ended = true;
            SetVisualActive(false);

            // Queue cleanup before spawning REV; Unity defers destruction until later this frame.
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            _ended = true;
            SetVisualActive(false);
        }

        private void SetVisualActive(bool active)
        {
            // Toggle only a child visual so the zone itself stays active.
            if (visualRoot != null && visualRoot != gameObject && visualRoot.transform.IsChildOf(transform))
            {
                visualRoot.SetActive(active);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
