namespace Game50033
{
    using System.Collections.Generic;
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

        internal struct Shockwave
        {
            internal Vector3 origin;
            internal float startedAt, duration;
        }

        private static readonly List<PlayerDodgeVFX> _active = new();
        internal static IReadOnlyList<PlayerDodgeVFX> Active => _active;
        internal IReadOnlyList<Shockwave> Shockwaves => _shockwaves;
        internal PlayerDodgeTuning Tuning => tuning;
        private readonly Shockwave[] _shockwaves = new Shockwave[2];
        private int _nextShockwave;

        private Player _player;
        private PlayerMovement _movement;
        private PlayerHitstop _hitstop;
        private GameObject _poolRoot;
        private Snapshot[] _pool;
        private GameObject _flashObject;
        private Renderer _flashRenderer;
        private Camera _camera;
        private Vector3 _flashOrigin, _flashDirection;
        private float _flashStartedAt = -1f, _flashDuration;
        private MaterialPropertyBlock _flashProperties;
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashPoseId = Shader.PropertyToID("_FlashPose");
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
            _active.Add(this);
            System.Array.Clear(_shockwaves, 0, _shockwaves.Length);
            _nextShockwave = 0;
            _properties ??= new MaterialPropertyBlock();
            _flashProperties ??= new MaterialPropertyBlock();
            _movement.DodgeStarted += HandleDodgeStarted;
            _movement.DodgeEnded += HandleDodgeEnded;
            SceneManager.activeSceneChanged += HandleSceneChanged;
            BuildPool();
        }

        private void BuildPool()
        {
            _emitting = false;
            _flashStartedAt = -1f;
            _flashObject = null;
            _flashRenderer = null;
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
            if (tuning.flashPrefab != null)
            {
                _flashObject = Instantiate(tuning.flashPrefab, _poolRoot.transform);
                _flashRenderer = _flashObject.GetComponent<Renderer>();
                _flashObject.SetActive(false);
            }
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            System.Array.Clear(_shockwaves, 0, _shockwaves.Length);
            _nextShockwave = 0;
            BuildPool();
        }

        private void HandleDodgeStarted(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            if (tuning.hitstopEnabled) _hitstop.Request(tuning.hitstopDuration);
            if (_poolRoot == null || _pool.Length != Mathf.Max(1, tuning.maxAfterimages) * 2
                || _flashObject == null && tuning.flashPrefab != null) BuildPool();
            _emitting = tuning.afterimagesEnabled && tuning.maxAfterimages > 0;
            _emitted = 0;
            _distanceSinceSnapshot = 0f;
            _previousPosition = transform.position;
            _previousAvatarPosition = avatarRenderer.transform.position;
            if (tuning.shockwaveEnabled)
            {
                // world time keeps the expanding disturbance independent of the player's startup freeze.
                _shockwaves[_nextShockwave] = new Shockwave
                {
                    origin = transform.position + Vector3.up * tuning.shockwaveHeight,
                    startedAt = Time.time,
                    duration = Mathf.Max(0.01f, tuning.shockwaveDuration)
                };
                _nextShockwave = (_nextShockwave + 1) % _shockwaves.Length;
            }
            if (tuning.flashEnabled && _flashObject != null && _flashRenderer != null)
            {
                _flashOrigin = transform.position + Vector3.up * tuning.flashHeight;
                _flashDirection = direction;
                _flashStartedAt = _hitstop.ActionTime;
                _flashDuration = Mathf.Max(0.01f, tuning.flashDuration);
                _flashObject.SetActive(true);
            }
        }

        private void HandleDodgeEnded(bool completed)
        {
            // completion is notified after the final collision-limited move; sample that segment in LateUpdate.
            // cancellation must not mistake subsequent walking or a teleport for dodge travel.
            if (!completed)
            {
                _emitting = false;
                _flashStartedAt = -1f;
                if (_flashObject != null) _flashObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (_poolRoot == null) { _emitting = false; return; }
            if (_flashObject != null && _flashStartedAt >= 0f)
            {
                var progress = Mathf.Clamp01((_hitstop.ActionTime - _flashStartedAt) / _flashDuration);
                if (!tuning.flashEnabled || progress >= 1f || !_player.isActiveAndEnabled || _player.Health.IsDowned)
                {
                    _flashStartedAt = -1f;
                    _flashObject.SetActive(false);
                }
                else
                {
                    if (_camera == null) _camera = Camera.main;
                    if (_camera != null)
                    {
                        // the flash stays at activation's origin; its optional drift is independent of player travel.
                        var direction = Vector3.ProjectOnPlane(_flashDirection, _camera.transform.forward);
                        if (direction.sqrMagnitude < 0.000001f) direction = _camera.transform.right;
                        direction.Normalize();
                        _flashObject.transform.SetPositionAndRotation(_flashOrigin + _flashDirection * (tuning.flashForwardDrift * progress),
                            Quaternion.LookRotation(_camera.transform.forward, Vector3.Cross(_camera.transform.forward, direction)));
                        _flashObject.transform.localScale = new Vector3(tuning.flashLength, tuning.flashWidth, 1f);
                        // use action time so pose zero is visible through startup hitstop, without affecting other players.
                        var pose = Mathf.Min(3, Mathf.FloorToInt(progress * 4f));
                        var color = tuning.flashColor;
                        var fade = tuning.flashFadeCurve != null ? tuning.flashFadeCurve.Evaluate(pose * 0.25f) : 1f - pose * 0.25f;
                        color.a *= tuning.flashOpacity * Mathf.Clamp01(fade);
                        _flashProperties.SetColor(FlashColorId, color);
                        _flashProperties.SetFloat(FlashPoseId, pose);
                        _flashRenderer.SetPropertyBlock(_flashProperties);
                    }
                }
            }

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
            _active.Remove(this);
            System.Array.Clear(_shockwaves, 0, _shockwaves.Length);
            _nextShockwave = 0;
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
            _flashObject = null;
            _flashRenderer = null;
            _flashStartedAt = -1f;
        }
    }
}
