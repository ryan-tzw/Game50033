namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class TromboneCommandZone : MonoBehaviour
    {
        [SerializeField] private TromboneTuning tuning;
        private float Radius => tuning != null ? tuning.commandZone.radius : 0f;
        [SerializeField] private Reverberation sustainReverberationPrefab;
        [Tooltip("Optional child GameObject displaying the Command Zone area.")]
        [SerializeField] private GameObject visualRoot;

        private Player _owner;
        private Scene _arenaScene;
        private bool _ended;

        internal Player PresentationOwner => _owner;
        internal event Action<TromboneCommandZone> Ended;

        // Keep destroyed owners marked as initialized so they still reach cleanup.
        public bool IsActive => !ReferenceEquals(_owner, null) && !_ended && isActiveAndEnabled;
        public bool IsConfigured => tuning != null && enabled && gameObject.activeSelf
            && sustainReverberationPrefab != null && sustainReverberationPrefab.enabled
            && sustainReverberationPrefab.gameObject.activeSelf
            && sustainReverberationPrefab.Effect is SustainEffect sustain && sustain.IsConfigured
            && GetComponentInChildren<Reverberation>(true) == null;

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
                return offset.sqrMagnitude <= Radius * Radius;
            }
        }

        private bool OwnerCanPlay()
        {
            if (_owner == null || !_owner.isActiveAndEnabled || !_owner.HasActiveInput)
            {
                return false;
            }

            if (_owner.Health.IsDowned)
            {
                return false;
            }

            if (!_owner.Abilities.isActiveAndEnabled)
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
                _owner.Session?.Debug.RecordAttack(new DebugAttackSnapshot(transform.position, Vector3.forward, 0f,
                    Radius, 360f, Vector3.zero, 1f, live: true));
                return;
            }

            ConsumeZone();
        }

        public void ConsumeZone()
        {
            if (!IsActive) return;
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
            EndZone();

            // Queue cleanup before spawning REV; Unity defers destruction until later this frame.
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            GetComponent<AudioSource>()?.Stop();
            EndZone();
        }

        private void EndZone()
        {
            if (_ended || ReferenceEquals(_owner, null)) return;
            _ended = true;
            SetVisualActive(false);
            Ended?.Invoke(this);
        }

        private void SetVisualActive(bool active)
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(active);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
