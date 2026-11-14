namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.SceneManagement;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player), typeof(PlayerMovement), typeof(PlayerHitstop))]
    [DefaultExecutionOrder(300)]
    public sealed class PlayerDodgeVFX : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer avatarRenderer;
        [SerializeField] private PlayerDodgeTuning tuning;

        private Player _player;
        private PlayerMovement _movement;
        private PlayerHitstop _hitstop;
        private GameObject _poolRoot;
        private Snapshot[] _pool;
        private MaterialPropertyBlock _properties;
        private bool _emitting;
        private int _emitted, _nextSlot;
        private float _distanceSinceSnapshot;
        private Vector3 _previousPosition, _previousAvatarPosition;
        private static readonly int SilhouetteColorId = Shader.PropertyToID("_SilhouetteColor");

        private struct Snapshot
        {
            internal SpriteRenderer renderer;
            internal float bornAt, lifetime;
            internal Color color;
        }

        private void Awake()
        {
            _player = GetComponent<Player>();
            _movement = GetComponent<PlayerMovement>();
            _hitstop = GetComponent<PlayerHitstop>();
        }

        private void OnEnable()
        {
            if (tuning == null || avatarRenderer == null || tuning.afterimageMaterial == null)
            {
                Debug.LogError("PlayerDodgeVFX requires tuning, an avatar renderer and an afterimage material.", this);
                enabled = false;
                return;
            }
            _properties ??= new MaterialPropertyBlock();
            _movement.DodgeStarted += HandleDodgeStarted;
            _movement.DodgeEnded += HandleDodgeEnded;
            SceneManager.activeSceneChanged += HandleSceneChanged;
            BuildPool();
        }

        private void BuildPool()
        {
            _emitting = false;
            if (_poolRoot != null) { _poolRoot.SetActive(false); Destroy(_poolRoot); }
            // snapshots belong to the encounter scene, not the persistent moving player hierarchy.
            _poolRoot = new GameObject("Player Dodge Afterimages");
            _pool = new Snapshot[Mathf.Max(1, tuning.maxAfterimages) * 2];
            _nextSlot = 0;
            for (var i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("Afterimage " + i);
                go.transform.SetParent(_poolRoot.transform, false);
                go.layer = avatarRenderer.gameObject.layer;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = tuning.afterimageMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                _pool[i].renderer = renderer;
            }
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            BuildPool();
        }

        private void HandleDodgeStarted(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            if (tuning.hitstopEnabled) _hitstop.Request(tuning.hitstopDuration);
            if (_poolRoot == null || _pool.Length != Mathf.Max(1, tuning.maxAfterimages) * 2) BuildPool();
            _emitting = tuning.afterimagesEnabled && tuning.maxAfterimages > 0;
            _emitted = 0;
            _distanceSinceSnapshot = 0f;
            _previousPosition = transform.position;
            _previousAvatarPosition = avatarRenderer.transform.position;
        }

        private void HandleDodgeEnded(bool completed)
        {
            // completion is notified after the final collision-limited move; sample that segment in LateUpdate.
            // cancellation must not mistake subsequent walking or a teleport for dodge travel.
            if (!completed) _emitting = false;
        }

        private void LateUpdate()
        {
            if (_poolRoot == null) { _emitting = false; return; }
            var now = Time.time;
            for (var i = 0; i < _pool.Length; i++)
            {
                ref var snapshot = ref _pool[i];
                if (!snapshot.renderer.enabled) continue;
                var age = (now - snapshot.bornAt) / snapshot.lifetime;
                if (age >= 1f)
                {
                    snapshot.renderer.enabled = false;
                    continue;
                }
                var fade = tuning.fadeCurve != null ? tuning.fadeCurve.Evaluate(Mathf.Clamp01(age)) : 1f - age;
                fade = Mathf.Clamp01(fade);
                if (tuning.fadeSteps > 0) fade = Mathf.Ceil(fade * tuning.fadeSteps) / tuning.fadeSteps;
                var color = snapshot.color;
                color.a *= fade;
                _properties.SetColor(SilhouetteColorId, color);
                snapshot.renderer.SetPropertyBlock(_properties);
            }

            if (!_emitting || _hitstop.IsStopped) return;
            if (!tuning.afterimagesEnabled || !_movement.isActiveAndEnabled || !_player.isActiveAndEnabled
                || _player.Health.IsDowned || !avatarRenderer.enabled || avatarRenderer.sprite == null)
            {
                _emitting = false;
                return;
            }
            var position = transform.position;
            var avatarPosition = avatarRenderer.transform.position;
            var segment = position - _previousPosition;
            segment.y = 0f;
            var distance = segment.magnitude;
            var spacing = Mathf.Max(0.001f, tuning.spacing);
            if (distance > 0.000001f)
            {
                // distribute samples along actual travelled segments, including slow frames and wall-limited dodges.
                for (var travelled = spacing - _distanceSinceSnapshot;
                     travelled <= distance + 0.000001f && _emitted < tuning.maxAfterimages; travelled += spacing)
                {
                    ref var snapshot = ref _pool[_nextSlot];
                    _nextSlot = (_nextSlot + 1) % _pool.Length;
                    var renderer = snapshot.renderer;
                    renderer.sprite = avatarRenderer.sprite;
                    renderer.flipX = avatarRenderer.flipX;
                    renderer.flipY = avatarRenderer.flipY;
                    renderer.sortingLayerID = avatarRenderer.sortingLayerID;
                    renderer.sortingOrder = avatarRenderer.sortingOrder;
                    renderer.sharedMaterial = tuning.afterimageMaterial;
                    renderer.transform.SetPositionAndRotation(Vector3.Lerp(_previousAvatarPosition, avatarPosition,
                        Mathf.Clamp01(travelled / distance)), avatarRenderer.transform.rotation);
                    renderer.transform.localScale = avatarRenderer.transform.lossyScale;
                    snapshot.bornAt = now;
                    snapshot.lifetime = Mathf.Max(0.01f, tuning.lifetime);
                    snapshot.color = _player.PlayerColor * tuning.colorMultiplier;
                    snapshot.color.a *= tuning.opacity;
                    _properties.SetColor(SilhouetteColorId, snapshot.color);
                    renderer.SetPropertyBlock(_properties);
                    renderer.enabled = true;
                    _emitted++;
                }
                _distanceSinceSnapshot = Mathf.Repeat(_distanceSinceSnapshot + distance, spacing);
            }
            _previousPosition = position;
            _previousAvatarPosition = avatarPosition;
            if (!_movement.IsDodging || _emitted >= tuning.maxAfterimages) _emitting = false;
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.DodgeStarted -= HandleDodgeStarted;
                _movement.DodgeEnded -= HandleDodgeEnded;
            }
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            _emitting = false;
            if (_poolRoot != null) { _poolRoot.SetActive(false); Destroy(_poolRoot); }
            _poolRoot = null;
            _pool = null;
        }
    }
}
