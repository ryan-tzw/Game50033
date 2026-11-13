namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class GuitarStrumVisual : MonoBehaviour
    {
        [SerializeField] private MeshFilter waveFilter;

        private Mesh _mesh;
        private Renderer _renderer;
        private MaterialPropertyBlock _properties;
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _radials = new();
        private readonly List<float> _widths = new();
        private readonly List<float> _tails = new();
        private Vector3 _direction;
        private static readonly int BodyColorId = Shader.PropertyToID("_BodyColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int TailColorId = Shader.PropertyToID("_TailColor");
        private static readonly int TailOpacityId = Shader.PropertyToID("_TailOpacity");
        private static readonly int BreakupId = Shader.PropertyToID("_BreakupScale");
        private static readonly int FlowId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int FadeStepsId = Shader.PropertyToID("_FadeSteps");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int AgeId = Shader.PropertyToID("_Age");

        private void OnEnable()
        {
            _properties ??= new MaterialPropertyBlock();
        }

        public void Initialize(Vector3 direction, float width, float angle,
            Vector3 stretchAxis, float stretch, float tipTaper, GuitarTuning.VFXSettings settings)
        {
            if (waveFilter == null) return;

            _direction = direction;
            _vertices.Clear();
            _radials.Clear();
            _widths.Clear();
            _tails.Clear();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var segments = Mathf.Max(2, Mathf.CeilToInt(angle / 4f));
            // three vertices per spoke: ragged tail, inner hit boundary, outer hit boundary.
            // the strong front follows gameplay; the darker tail is only residual energy.
            for (var segment = 0; segment <= segments; segment++)
            {
                var t = (float)segment / segments;
                var radial = Quaternion.AngleAxis(Mathf.Lerp(-angle * 0.5f, angle * 0.5f, t), Vector3.up) * direction;
                radial += stretchAxis * (Vector3.Dot(radial, stretchAxis) * (stretch - 1f));
                _radials.Add(radial);
                var tip = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
                _widths.Add(width * Mathf.Lerp(1f - tipTaper, 1f, tip));
                _tails.Add(settings.strumTailLength * Mathf.Sqrt(tip));
                for (var band = 0; band < 3; band++)
                {
                    _vertices.Add(Vector3.zero);
                    uvs.Add(new Vector2(t, band));
                }
                if (segment == 0) continue;
                for (var band = 0; band < 2; band++)
                {
                    var a = (segment - 1) * 3 + band;
                    triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 4);
                }
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = new Mesh { name = "Guitar strum energy" };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0);
            waveFilter.sharedMesh = _mesh;
            _renderer = waveFilter.GetComponent<Renderer>();
            if (_renderer == null) return;
            _properties ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor(BodyColorId, settings.strumColor);
            _properties.SetColor(EdgeColorId, settings.strumEdgeColor);
            _properties.SetColor(TailColorId, settings.strumTailColor);
            _properties.SetFloat(TailOpacityId, settings.strumTailOpacity);
            _properties.SetFloat(BreakupId, settings.strumBreakupScale);
            _properties.SetFloat(FlowId, settings.strumFlowSpeed);
            _properties.SetFloat(FadeStepsId, settings.strumFadeSteps);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetWaveState(float radius, float opacity, float age)
        {
            if (_mesh == null) return;
            // reuse the mesh and lists; changing radius never allocates a new mesh per frame.
            for (var i = 0; i < _radials.Count; i++)
            {
                var inner = _radials[i] * Mathf.Max(0f, radius - _widths[i] * 0.5f);
                var outer = _radials[i] * (radius + _widths[i] * 0.5f);
                var tail = inner - _direction * _tails[i];
                _vertices[i * 3] = waveFilter.transform.InverseTransformPoint(transform.position + tail);
                _vertices[i * 3 + 1] = waveFilter.transform.InverseTransformPoint(transform.position + inner);
                _vertices[i * 3 + 2] = waveFilter.transform.InverseTransformPoint(transform.position + outer);
            }
            _mesh.SetVertices(_vertices);
            if (_renderer == null) return;
            _properties.SetFloat(OpacityId, opacity);
            _properties.SetFloat(AgeId, age);
            _renderer.SetPropertyBlock(_properties);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
