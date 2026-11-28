namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class ElectricKnifeProjectile : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int SpriteRotationId = Shader.PropertyToID("_SpriteRotation");
        private static readonly int SpriteFlatDepthId = Shader.PropertyToID("_SpriteFlatDepth");
        private const float ReleaseHeight = 1.35f;
        private const float AuthoredBladeAngle = -68f;

        [SerializeField] private SpriteRenderer blade;
        [SerializeField] private Material lightningMaterial;

        private readonly HashSet<Player> _pathContacts = new();
        private readonly HashSet<Player> _damaged = new();
        private readonly LineRenderer[] _sparks = new LineRenderer[6];
        private MaterialPropertyBlock _properties;
        private LineRenderer _landingMarker;
        private Boss3Controller _owner;
        private Vector3 _origin;
        private Vector3 _landing;
        private Vector3 _direction;
        private float _length;
        private float _distance;
        private float _speed;
        private float _landedAt;
        private float _embeddedRotation;
        private bool _embedded;
        private uint _launchSequence;

        internal bool IsEmbedded => _embedded;

        internal void Launch(Boss3Controller owner, Vector3 origin, Vector3 landing, float speed)
        {
            _owner = owner;
            _origin = origin;
            _landing = landing;
            var offset = landing - origin;
            _length = offset.magnitude;
            _direction = _length > 0.0001f ? offset / _length : Vector3.forward;
            _distance = 0f;
            _speed = speed;
            _embedded = false;
            _launchSequence++;
            _pathContacts.Clear();
            _damaged.Clear();

            if (_properties == null)
            {
                _properties = new MaterialPropertyBlock();
                var marker = new GameObject("Landing Circle") { layer = gameObject.layer };
                marker.transform.SetParent(transform, false);
                _landingMarker = marker.AddComponent<LineRenderer>();
                _landingMarker.sharedMaterial = lightningMaterial;
                _landingMarker.shadowCastingMode = ShadowCastingMode.Off;
                _landingMarker.receiveShadows = false;
                _landingMarker.alignment = LineAlignment.TransformZ;
                _landingMarker.loop = true;
                _landingMarker.positionCount = 32;
                _properties.Clear();
                _properties.SetFloat(TelegraphId, 1f);
                _landingMarker.SetPropertyBlock(_properties);

                for (var i = 0; i < _sparks.Length; i++)
                {
                    var spark = new GameObject("Impact Arc") { layer = gameObject.layer };
                    spark.transform.SetParent(transform, false);
                    var arc = spark.AddComponent<LineRenderer>();
                    arc.sharedMaterial = lightningMaterial;
                    arc.shadowCastingMode = ShadowCastingMode.Off;
                    arc.receiveShadows = false;
                    arc.positionCount = 3;
                    _sparks[i] = arc;
                }
            }

            foreach (var arc in _sparks) arc.enabled = false;
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(_direction));
            blade.transform.localPosition = Vector3.up * ReleaseHeight;
            var camera = Camera.main;
            var view = camera ? camera.transform : null;
            var flight = offset - Vector3.up * ReleaseHeight;
            var screenX = view ? Vector3.Dot(flight, view.right) : flight.x;
            var screenY = view ? Vector3.Dot(flight, view.up) : flight.y;
            var flightAngle = Mathf.Atan2(screenY, screenX) * Mathf.Rad2Deg;
            _embeddedRotation = screenX >= 0f ? 0f : -180f - 2f * AuthoredBladeAngle;
            _properties.Clear();
            _properties.SetFloat(SpriteFlatDepthId, 2f);
            _properties.SetFloat(SpriteRotationId, flightAngle - AuthoredBladeAngle);
            blade.SetPropertyBlock(_properties);
            // keep the blast marked while the knife is in flight.
            _landingMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _landingMarker.widthMultiplier = 0.10f;
            var markerRadius = Mathf.Max(0f, owner.Tuning.knifeBlastRadius - _landingMarker.widthMultiplier * 0.5f);
            for (var i = 0; i < _landingMarker.positionCount; i++)
            {
                var angle = i * (2f * Mathf.PI / _landingMarker.positionCount);
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                _landingMarker.SetPosition(i, landing + Vector3.up * 0.01f + radial * markerRadius);
            }
            _landingMarker.enabled = true;
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (!_owner || !_owner.isActiveAndEnabled || _owner.IsDefeated || _embedded) return;

            var tuning = _owner.Tuning;
            var nextDistance = Mathf.Min(_length, _distance + _speed * Time.fixedDeltaTime);
            var landed = nextDistance >= _length;
            var targets = _owner.Targets;
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                if (i >= targets.Count) continue;
                var player = targets[i];
                if (!player || !player.isActiveAndEnabled || player.Health.IsDowned
                    || _damaged.Contains(player) || !player.Hurtbox || !player.Hurtbox.IsAvailable) continue;
                player.Hurtbox.GetWorldCapsule(out var bottom, out _, out var playerRadius);
                bottom.y = _origin.y;

                // overlap the ground-plane combat capsule against the rectangular portion crossed this step.
                var offset = bottom - _origin;
                var along = Vector3.Dot(offset, _direction);
                var beside = (offset - _direction * along).magnitude;
                var endGap = along - Mathf.Clamp(along, _distance, nextDistance);
                var sideGap = Mathf.Max(0f, beside - tuning.knifeLaneWidth * 0.5f);
                if (_length > 0.0001f && endGap * endGap + sideGap * sideGap <= playerRadius * playerRadius
                    && _pathContacts.Add(player) && player.Health.TryTakeDamage(tuning.knifeThrowDamage, -_direction, _owner))
                {
                    _damaged.Add(player);
                    continue;
                }

                var fromImpact = bottom - _landing;
                var blastReach = tuning.knifeBlastRadius + playerRadius;
                if (landed && fromImpact.sqrMagnitude <= blastReach * blastReach
                    && player.Health.TryTakeDamage(tuning.knifeBlastDamage, -fromImpact.normalized, _owner))
                    _damaged.Add(player);
            }

            _distance = nextDistance;
            transform.position = landed ? _landing : _origin + _direction * _distance;
            if (!landed)
            {
                blade.transform.localPosition = Vector3.up * (ReleaseHeight * (1f - _distance / _length));
                return;
            }

            _embedded = true;
            _landedAt = Time.time;
            _landingMarker.enabled = false;
            // the source sprite's pivot is at the tip, so planting it only changes the visual height and angle.
            blade.transform.localPosition = Vector3.zero;
            _properties.Clear();
            _properties.SetFloat(SpriteFlatDepthId, 2f);
            _properties.SetFloat(SpriteRotationId, _embeddedRotation);
            blade.SetPropertyBlock(_properties);

            // use a per-throw seed so cosmetic variation never changes the attack deck's randomness.
            var random = new System.Random(unchecked(GetInstanceID() * 397 ^ (int)_launchSequence));
            var turn = (float)random.NextDouble() * 360f;
            var sparkOrigin = _landing + Vector3.up * 0.10f;
            for (var i = 0; i < _sparks.Length; i++)
            {
                var angle = turn + (i + (float)random.NextDouble() * 0.5f) * (360f / _sparks.Length);
                var bearing = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var lateral = Vector3.Cross(Vector3.up, bearing);
                var reach = tuning.knifeBlastRadius * Mathf.Lerp(0.58f, 1f, (float)random.NextDouble());
                var bend = Mathf.Lerp(-0.28f, 0.28f, (float)random.NextDouble());
                var spark = _sparks[i];
                spark.widthMultiplier = tuning.knifeBlastRadius * (i % 2 == 0 ? 0.27f : 0.16f);
                spark.SetPosition(0, sparkOrigin + bearing * (reach * 0.13f));
                spark.SetPosition(1, sparkOrigin + (bearing * 0.6f + lateral * bend) * reach + Vector3.up * 0.12f);
                spark.SetPosition(2, sparkOrigin + bearing * reach);
                _properties.Clear();
                _properties.SetFloat(FadeId, 1f);
                spark.SetPropertyBlock(_properties);
                spark.enabled = true;
            }
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
            if (!_embedded) return;

            var age = Time.time - _landedAt;
            var tuning = _owner.Tuning;
            if (age >= Mathf.Max(tuning.knifeAnchorLifetime, tuning.knifeImpactDuration))
            {
                _owner.ReturnKnife(this);
                return;
            }
            if (!_sparks[0].enabled) return;

            var fade = 1f - Mathf.Clamp01(age / tuning.knifeImpactDuration);
            _properties.Clear();
            _properties.SetFloat(FadeId, fade * fade);
            foreach (var spark in _sparks)
            {
                spark.SetPropertyBlock(_properties);
                spark.enabled = fade > 0f;
            }
        }

        private void OnDisable()
        {
            _embedded = false;
            _pathContacts.Clear();
            _damaged.Clear();
        }
    }
}
