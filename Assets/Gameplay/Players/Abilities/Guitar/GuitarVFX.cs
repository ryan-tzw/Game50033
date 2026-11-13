namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class GuitarVFX : MonoBehaviour
    {
        private struct Smear
        {
            public Vector3 origin;
            public Vector3 direction;
            public float startAngle, endAngle, inner, outer, sweetSpot, startsAt;
        }

        [SerializeField] private MeshFilter smearFilter;
        [SerializeField] private GuitarTuning tuning;

        private readonly List<Smear> _smears = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Vector4> _poseData = new();
        private readonly List<int> _triangles = new();
        private MaterialPropertyBlock _properties;
        private Mesh _mesh;
        private Renderer _renderer;
        private static readonly int BodyColorId = Shader.PropertyToID("_BodyColor");
        private static readonly int SweetSpotColorId = Shader.PropertyToID("_SweetSpotColor");

        private void Awake()
        {
            _mesh = new Mesh { name = "Guitar smear" };
            _mesh.MarkDynamic();
            if (smearFilter == null) return;
            smearFilter.sharedMesh = _mesh;
            _renderer = smearFilter.GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            if (tuning == null)
            {
                Debug.LogError("GuitarVFX requires a GuitarTuning asset.", this);
                enabled = false;
                return;
            }
            // native-backed blocks do not survive a play-mode script reload.
            _properties ??= new MaterialPropertyBlock();
        }

        public void BeginSwing(Vector3 origin, Vector3 direction, float startAngle, float endAngle,
            float innerRadius, float reach, float sweetSpotRadius)
        {
            if (!isActiveAndEnabled || smearFilter == null) return;

            // gameplay supplies the complete impact snapshot; later poses are only afterimages.
            _smears.Add(new Smear
            {
                origin = origin, direction = direction, startAngle = startAngle, endAngle = endAngle,
                inner = innerRadius, outer = reach,
                sweetSpot = sweetSpotRadius, startsAt = Time.time
            });
        }

        private void LateUpdate()
        {
            if (_mesh == null || smearFilter == null) return;
            if (_smears.Count == 0 && _mesh.vertexCount == 0) return;

            var vfx = tuning.vfx;
            var durations = new Vector4(Mathf.Max(0.001f, vfx.frameDurations.x), Mathf.Max(0.001f, vfx.frameDurations.y),
                Mathf.Max(0.001f, vfx.frameDurations.z), Mathf.Max(0.001f, vfx.frameDurations.w));
            _vertices.Clear();
            _uvs.Clear();
            _poseData.Clear();
            _triangles.Clear();

            for (var i = 0; i < _smears.Count; i++)
            {
                var smear = _smears[i];
                var age = Time.time - smear.startsAt;
                if (age >= durations.x + durations.y + durations.z + durations.w)
                {
                    _smears.RemoveAt(i--);
                    continue;
                }
                if (age < 0f) continue;

                var frame = age < durations.x ? 0 : age < durations.x + durations.y ? 1
                    : age < durations.x + durations.y + durations.z ? 2 : 3;
                var sign = Mathf.Sign(smear.endAngle - smear.startAngle);
                var start = smear.startAngle + sign * (frame == 0 ? 0f : frame == 1 ? 8f : 16f);
                var end = smear.endAngle + sign * (frame == 0 ? 0f : 10f);
                var inner = Mathf.Lerp(smear.inner, smear.outer, frame == 0 ? 0f : frame == 1 ? 0.12f : frame == 2 ? 0.35f : 0.65f);
                var outer = smear.outer;
                var first = _vertices.Count;
                var segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(end - start) / 4f));

                // uv1 carries each smear's world-space center, actual sweet-spot radius and pose.
                // the shader measures radial distance, so changing the mesh size cannot shift the color boundary.
                for (var segment = 0; segment <= segments; segment++)
                {
                    var t = (float)segment / segments;
                    var radial = Quaternion.AngleAxis(Mathf.Lerp(start, end, t), Vector3.up) * smear.direction;
                    _vertices.Add(smearFilter.transform.InverseTransformPoint(smear.origin + radial * inner));
                    _vertices.Add(smearFilter.transform.InverseTransformPoint(smear.origin + radial * outer));
                    _uvs.Add(new Vector2(t, 0f));
                    _uvs.Add(new Vector2(t, 1f));
                    _poseData.Add(new Vector4(smear.origin.x, smear.origin.z, smear.sweetSpot, frame));
                    _poseData.Add(new Vector4(smear.origin.x, smear.origin.z, smear.sweetSpot, frame));
                    if (segment == 0) continue;
                    var a = first + (segment - 1) * 2;
                    _triangles.Add(a); _triangles.Add(a + 2); _triangles.Add(a + 1);
                    _triangles.Add(a + 1); _triangles.Add(a + 2); _triangles.Add(a + 3);
                }
            }

            _mesh.Clear();
            if (_triangles.Count == 0) return;
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetUVs(1, _poseData);
            _mesh.SetTriangles(_triangles, 0);
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor(BodyColorId, vfx.smearColor);
            _properties.SetColor(SweetSpotColorId, vfx.smearSweetSpotColor);
            _renderer.SetPropertyBlock(_properties);
        }

        public void PlayImpact(Vector3 position, bool sweetSpot)
        {
            if (!isActiveAndEnabled) return;
            var vfx = tuning.vfx;
            if (vfx.impactPrefab == null) return;

            var impact = Instantiate(vfx.impactPrefab, position, Quaternion.identity);
            var main = impact.main;
            main.startColor = sweetSpot ? vfx.sweetSpotColor : vfx.normalHitColor;
            main.startSizeMultiplier = sweetSpot ? vfx.sweetSpotImpactSize : vfx.normalImpactSize;
            impact.Play();
        }

        public void Cancel()
        {
            _smears.Clear();
            if (_mesh != null) _mesh.Clear();
        }

        private void OnDisable()
        {
            Cancel();
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
