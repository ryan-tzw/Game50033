namespace Game50033
{
    using UnityEngine;

    public sealed class ElectricKnifeProjectile : MonoBehaviour
    {
        private static readonly int TelegraphColorId = Shader.PropertyToID("_TelegraphColor");
        private static readonly int SpriteRotationId = Shader.PropertyToID("_SpriteRotation");
        private static readonly int SpriteFlatDepthId = Shader.PropertyToID("_SpriteFlatDepth");
        internal const float ThrowHandHeight = 1.35f;
        private const float AuthoredBladeAngle = -68f;

        [SerializeField] private SpriteRenderer blade;
        [SerializeField] private Material lightningMaterial;

        private MaterialPropertyBlock _properties;
        private MeshFilter _landingMarker;
        private LineRenderer _flashingOutline;
        private Boss3Controller _owner;
        private Vector3 _origin;
        private Vector3 _landing;
        private Vector3 _direction;
        private float _length;
        private float _distance;
        private float _speed;
        private float _releaseHeight;
        private float _impactRadius;
        private float _lockedAt;
        private float _flashDuration;
        private float _flashIntensity;
        private float _landedAt;
        private bool _embeddedFlipX;
        private bool _embedded;

        internal bool IsEmbedded => _embedded;

        internal void Launch(Boss3Controller owner, Vector3 origin, Vector3 landing, float speed, float releaseHeight,
            float impactRadius, MeshFilter marker, float lockedAt, float flashDuration, float flashIntensity)
        {
            _owner = owner;
            _origin = origin;
            _landing = landing;
            var offset = landing - origin;
            var horizontalDistance = offset.magnitude;
            _direction = horizontalDistance > 0.0001f ? offset / horizontalDistance : Vector3.forward;
            _length = Mathf.Sqrt(horizontalDistance * horizontalDistance + releaseHeight * releaseHeight);
            _distance = 0f;
            _speed = speed;
            _releaseHeight = releaseHeight;
            _embedded = false;
            _impactRadius = impactRadius;
            _landingMarker = marker;
            _landingMarker.transform.SetParent(null, true);
            _lockedAt = lockedAt;
            _flashDuration = flashDuration;
            _flashIntensity = flashIntensity;
            _flashingOutline = Time.time - lockedAt < flashDuration ? marker.GetComponentInChildren<LineRenderer>() : null;

            _properties ??= new MaterialPropertyBlock();
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(_direction));
            blade.transform.localPosition = Vector3.up * _releaseHeight;
            blade.transform.localRotation = Quaternion.identity;
            blade.flipX = false;
            var camera = Camera.main;
            var view = camera ? camera.transform : null;
            var flight = offset - Vector3.up * _releaseHeight;
            var screenX = view ? Vector3.Dot(flight, view.right) : flight.x;
            var screenY = view ? Vector3.Dot(flight, view.up) : flight.y;
            var flightAngle = Mathf.Atan2(screenY, screenX) * Mathf.Rad2Deg;
            _embeddedFlipX = screenX < 0f;
            _properties.Clear();
            _properties.SetFloat(SpriteFlatDepthId, 2f);
            _properties.SetFloat(SpriteRotationId, flightAngle - AuthoredBladeAngle);
            blade.SetPropertyBlock(_properties);
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (!_owner || !_owner.isActiveAndEnabled || _owner.IsDefeated || _embedded) return;

            var tuning = _owner.Tuning;
            _distance = Mathf.Min(_length, _distance + _speed * Time.fixedDeltaTime);
            var progress = _length > 0f ? _distance / _length : 1f;
            transform.position = Vector3.Lerp(_origin, _landing, progress);
            if (progress < 1f)
            {
                blade.transform.localPosition = Vector3.up * (_releaseHeight * (1f - progress));
                return;
            }

            _embedded = true;
            _landedAt = Time.time;
            ClearLandingMarker();

            var targets = _owner.Targets;
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                if (i >= targets.Count) continue;
                var player = targets[i];
                if (!player || !player.isActiveAndEnabled || player.Health.IsDowned
                    || !player.Hurtbox || !player.Hurtbox.IsAvailable) continue;
                player.Hurtbox.GetWorldCapsule(out var bottom, out _, out var playerRadius);
                bottom.y = _landing.y;
                var fromImpact = bottom - _landing;
                var blastReach = _impactRadius + playerRadius;
                if (fromImpact.sqrMagnitude <= blastReach * blastReach)
                    player.Health.TryTakeDamage(tuning.knifeBlastDamage, -fromImpact.normalized, _owner);
                if (!_embedded) return;
            }

            // the blade's pivot is at the tip. plant it on the floor below the raised landing marker.
            blade.transform.SetPositionAndRotation(
                new Vector3(_landing.x, _owner.ArenaFloor.bounds.max.y, _landing.z), Quaternion.identity);
            // the upright mesh and the billboard use the same flip, so the shadow follows the blade.
            blade.flipX = _embeddedFlipX;
            _properties.Clear();
            _properties.SetFloat(SpriteFlatDepthId, 2f);
            blade.SetPropertyBlock(_properties);

            _owner.PlayElectricalImpact(_landing, lightningMaterial, tuning.knifeImpact, _impactRadius);
            _owner.KnifeEmbedded();
        }

        private void Update()
        {
            if (!_owner) return;
            if (!_owner.isActiveAndEnabled || _owner.IsDefeated)
            {
                _owner.ReturnKnife(this);
                return;
            }
            if (!_embedded)
            {
                if (_flashingOutline)
                {
                    var elapsed = Time.time - _lockedAt;
                    _flashingOutline.GetPropertyBlock(_properties);
                    _properties.SetVector(TelegraphColorId, EvaluateLockOutlineColor(elapsed, _flashDuration, _flashIntensity));
                    _flashingOutline.SetPropertyBlock(_properties);
                    if (elapsed >= _flashDuration) _flashingOutline = null;
                }
                return;
            }

            if (Time.time - _landedAt >= _owner.Tuning.knifeAnchorLifetime)
                _owner.ReturnKnife(this);
        }

        private void ClearLandingMarker()
        {
            _flashingOutline = null;
            if (!_landingMarker) return;
            _landingMarker.gameObject.SetActive(false);
            Destroy(_landingMarker.sharedMesh);
            Destroy(_landingMarker.gameObject);
            _landingMarker = null;
        }

        internal static Vector4 EvaluateLockOutlineColor(float elapsed, float duration, float intensity)
        {
            var flash = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            var red = Mathf.Lerp(2.55f, intensity, flash);
            return new Vector4(red, red * Mathf.Lerp(0.05f, 0.46f, flash), red * Mathf.Lerp(0.07f, 0.34f, flash), 0.98f);
        }

        private void OnDisable()
        {
            _embedded = false;
            ClearLandingMarker();
        }
    }
}
