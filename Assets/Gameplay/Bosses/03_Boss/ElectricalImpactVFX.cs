namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;

    internal sealed class ElectricalImpactVFX : MonoBehaviour
    {
        private const int FlashPoints = 20;
        private const int RingPoints = 40;
        private const int MainArcCount = 6;

        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int JaggednessId = Shader.PropertyToID("_Jaggedness");
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int TelegraphColorId = Shader.PropertyToID("_TelegraphColor");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private MaterialPropertyBlock _properties;
        private readonly Vector3[] _flashVertices = new Vector3[FlashPoints + 1];
        private readonly Vector3[] _ringDirections = new Vector3[RingPoints];
        private readonly Vector3[] _ringVertices = new Vector3[RingPoints];
        private readonly LineRenderer[] _arcs = new LineRenderer[11];
        private readonly Vector3[] _directions = new Vector3[11];
        private readonly Vector3[] _bends = new Vector3[11];
        private readonly float[] _sparkTimes = new float[11];

        private Boss3Controller _owner;
        private MeshRenderer _flash;
        private Mesh _flashMesh;
        private LineRenderer _ring;
        private Boss3Tuning.ElectricalImpact _tuning;
        private Vector3 _origin;
        private Vector3 _ground;
        private Vector4 _core;
        private Vector4 _blue;
        private float _shockwaveRadius;
        private float _startedAt;
        private System.Random _random;

        internal void Play(Boss3Controller owner, Vector3 contact, Material material, Boss3Tuning.ElectricalImpact tuning,
            float? shockwaveRadius, Vector3 inward)
        {
            _owner = owner;
            _tuning = tuning;
            _startedAt = Time.time;
            _shockwaveRadius = shockwaveRadius ?? tuning.radius;
            _ground = new Vector3(contact.x, owner.ArenaFloor.bounds.max.y + 0.045f, contact.z);
            // keep the flash on the arena side of opaque walls while the ground wave stays at contact.
            _origin = contact + inward * (tuning.radius * 0.28f);
            _origin.y = Mathf.Max(_origin.y, _ground.y + tuning.radius * 0.15f);
            _core = material.GetVector(CoreColorId);
            _blue = material.GetVector(GlowColorId);

            if (!_flashMesh)
            {
                _properties = new MaterialPropertyBlock();
                var flashObject = new GameObject("Contact Flash") { layer = gameObject.layer };
                flashObject.transform.SetParent(transform, false);
                _flash = flashObject.AddComponent<MeshRenderer>();
                _flash.shadowCastingMode = ShadowCastingMode.Off;
                _flash.receiveShadows = false;
                _flash.sortingOrder = 1;
                _flashMesh = new Mesh { name = "Electrical Impact Starburst" };
                _flashMesh.MarkDynamic();
                var uv = new Vector2[_flashVertices.Length];
                var colors = new Color32[_flashVertices.Length];
                var triangles = new int[FlashPoints * 3];
                for (var i = 0; i < _flashVertices.Length; i++)
                {
                    uv[i] = new Vector2(0.5f, 0.5f);
                    colors[i] = new Color32(255, 255, 255, 255);
                }
                for (var i = 0; i < FlashPoints; i++)
                {
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = (i + 1) % FlashPoints + 1;
                }
                _flashMesh.vertices = _flashVertices;
                _flashMesh.uv = uv;
                _flashMesh.colors32 = colors;
                _flashMesh.triangles = triangles;
                flashObject.AddComponent<MeshFilter>().sharedMesh = _flashMesh;
                _ring = CreateLine("Ground Shockwave", RingPoints);
                _ring.loop = true;
                _ring.alignment = LineAlignment.TransformZ;
                _ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                for (var i = 0; i < _ringDirections.Length; i++)
                {
                    var angle = i * (2f * Mathf.PI / RingPoints);
                    _ringDirections[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
                for (var i = 0; i < _arcs.Length; i++)
                    _arcs[i] = CreateLine(i < MainArcCount ? "Lightning Arc" : "Residual Spark", 4);
                _random = new System.Random(GetInstanceID());
            }

            // each launch gets its own shape without advancing the boss attack deck's random sequence.
            var turn = (float)_random.NextDouble() * 360f;
            for (var i = 0; i < FlashPoints; i++)
            {
                var angle = (i + (float)_random.NextDouble() * 0.55f) * (360f / FlashPoints) + turn;
                var reach = i % 2 == 0 ? Mathf.Lerp(0.35f, 0.69f, (float)_random.NextDouble()) : 0.17f;
                _flashVertices[i + 1] = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.right * reach;
            }
            _flashMesh.vertices = _flashVertices;
            _flashMesh.RecalculateBounds();
            _flash.sharedMaterial = material;
            var camera = Camera.main;
            _flash.transform.SetPositionAndRotation(_origin, camera ? camera.transform.rotation : Quaternion.identity);
            _ring.sharedMaterial = material;

            for (var i = 0; i < _arcs.Length; i++)
            {
                var bearing = inward == Vector3.zero ? turn + 360f * (i / (float)MainArcCount)
                    : Mathf.Lerp(-57f, 57f, (i % MainArcCount) / (MainArcCount - 1f));
                bearing += Mathf.Lerp(-tuning.directionVariation, tuning.directionVariation, (float)_random.NextDouble());
                var flat = Quaternion.AngleAxis(bearing, Vector3.up) * (inward == Vector3.zero ? Vector3.forward : inward);
                _directions[i] = (flat + Vector3.up * Mathf.Lerp(0.12f, 0.65f, (float)_random.NextDouble()))
                    * Mathf.Lerp(0.72f, 1.18f, (float)_random.NextDouble());
                _bends[i] = Vector3.Cross(Vector3.up, flat) * (i % 2 == 0 ? 0.23f : -0.23f);
                _sparkTimes[i] = i < MainArcCount ? 0f
                    : Mathf.Lerp(0.20f, 0.76f, (i - MainArcCount + (float)_random.NextDouble()) / (_arcs.Length - MainArcCount));
                _arcs[i].sharedMaterial = material;
            }
            gameObject.SetActive(true);
            Update();
        }

        private LineRenderer CreateLine(string objectName, int pointCount)
        {
            var child = new GameObject(objectName) { layer = gameObject.layer };
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = pointCount;
            return line;
        }

        private void Update()
        {
            var elapsed = Time.time - _startedAt;
            var progress = elapsed / Mathf.Max(0.01f, _tuning.duration);
            if (progress >= 1f || !_owner.isActiveAndEnabled)
            {
                _owner.ReturnElectricalImpact(this);
                return;
            }

            var remaining = 1f - progress;
            var expansion = 1f - remaining * remaining * remaining * remaining;
            var flash = 1f - Mathf.Clamp01(elapsed / Mathf.Min(0.085f, _tuning.duration * 0.25f));
            _flash.enabled = flash > 0f;
            if (_flash.enabled)
            {
                _properties.Clear();
                _properties.SetFloat(TelegraphId, 1f);
                var white = (_core + _blue * 0.08f) * (_tuning.intensity * 2.2f);
                white.w = 1f;
                _properties.SetVector(TelegraphColorId, white);
                _properties.SetFloat(FadeId, flash * flash);
                _flash.SetPropertyBlock(_properties);
                _flash.transform.localScale = Vector3.one * (_tuning.radius * Mathf.Lerp(1f, 1.28f, expansion));
            }

            // the warning radius stays fixed in gameplay. the shockwave reaches it as the energy dissipates.
            var ringRadius = Mathf.Lerp(0.22f, 1f, expansion) * _shockwaveRadius;
            for (var i = 0; i < _ringVertices.Length; i++)
                _ringVertices[i] = _ground + _ringDirections[i] * ringRadius;
            _ring.SetPositions(_ringVertices);
            _ring.widthMultiplier = Mathf.Clamp(_shockwaveRadius * 0.14f, 0.07f, 0.17f) * Mathf.Lerp(0.72f, 1f, remaining);
            _properties.Clear();
            _properties.SetVector(CoreColorId, Vector4.Lerp(_blue, _core, 0.40f) * _tuning.intensity);
            _properties.SetVector(GlowColorId, _blue * _tuning.intensity);
            _properties.SetFloat(JaggednessId, 0.08f);
            _properties.SetFloat(ThicknessId, 0.52f);
            _properties.SetFloat(GlowIntensityId, 0.68f);
            _properties.SetFloat(FadeId, remaining * remaining * 0.75f);
            _ring.SetPropertyBlock(_properties);

            for (var i = 0; i < _arcs.Length; i++)
            {
                var arc = _arcs[i];
                var main = i < MainArcCount;
                var age = progress - _sparkTimes[i];
                var duration = main ? 0.56f - (i % 3) * 0.06f : Mathf.Lerp(0.18f, 0.07f, _sparkTimes[i]);
                arc.enabled = age >= 0f && age < duration;
                if (!arc.enabled) continue;

                var fade = 1f - age / duration;
                var reach = _tuning.radius * Mathf.Lerp(0.34f, 1.20f, expansion);
                var direction = _directions[i];
                var bend = _bends[i] * reach;
                var root = _origin + direction * (reach * (main ? expansion * 0.32f : 0.72f));
                var length = reach * (main ? 0.72f : 0.24f) * Mathf.Lerp(0.35f, 1f, fade);
                arc.widthMultiplier = Mathf.Max(0.085f, _tuning.radius * (main ? 0.23f : 0.10f)) * Mathf.Lerp(0.62f, 1f, fade);
                arc.SetPosition(0, root);
                arc.SetPosition(1, root + direction * (length * 0.38f) + bend * (main ? 0.46f : 0.15f));
                arc.SetPosition(2, root + direction * (length * 0.63f) - bend * (main ? 0.30f : 0.10f));
                arc.SetPosition(3, root + direction * length);
                _properties.SetVector(CoreColorId, (main ? _core : Vector4.Lerp(_blue, _core, 0.35f)) * _tuning.intensity);
                _properties.SetFloat(GlowIntensityId, main ? 1.12f : 0.64f);
                _properties.SetFloat(FadeId, main ? fade : fade * remaining * 0.82f);
                arc.SetPropertyBlock(_properties);
            }
        }

        private void OnDestroy()
        {
            if (_flashMesh) Destroy(_flashMesh);
        }
    }
}
