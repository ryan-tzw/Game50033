namespace Game50033.Examples
{
    using UnityEngine;
    using UnityEngine.Rendering;

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ReverberationEnergyField : MonoBehaviour
    {
        [SerializeField] private ReverberationEnergyFieldTuning tuning;
        [SerializeField] private MeshRenderer[] layers;
        private MaterialPropertyBlock _properties;
        private ReverberationEnergyFieldTuning _appliedTuning;
        private int _appliedRevision = -1;

        private static readonly int ColorId = Shader.PropertyToID("_EnergyColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int OpacityId = Shader.PropertyToID("_LayerOpacity");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int BandsId = Shader.PropertyToID("_BrightnessBands");
        private static readonly int ScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int SpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");
        private static readonly int FalloffId = Shader.PropertyToID("_EdgeFalloff");
        private static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
        private static readonly int BackgroundOpacityId = Shader.PropertyToID("_BackgroundLayerOpacity");
        private static readonly int BackgroundVariationId = Shader.PropertyToID("_BackgroundVariation");
        private static readonly int MidgroundOpacityId = Shader.PropertyToID("_MidgroundLayerOpacity");
        private static readonly int MidgroundWidthId = Shader.PropertyToID("_MidgroundWidth");
        private static readonly int LayerId = Shader.PropertyToID("_Layer");

        private void OnEnable()
        {
            _properties ??= new MaterialPropertyBlock();
            _appliedRevision = -1;
        }

        private void LateUpdate()
        {
            if (tuning == null || layers == null || layers.Length == 0) return;
            if (_appliedTuning == tuning && _appliedRevision == tuning.Revision) return;
            _appliedTuning = tuning;
            _appliedRevision = tuning.Revision;

            // ==================================================
            // shared field parameters
            // ==================================================

            var count = Mathf.Clamp(tuning.layerCount, 1, layers.Length);
            var dimensions = tuning.fieldDimensions;
            dimensions.x = Mathf.Max(0.05f, dimensions.x);
            dimensions.y = Mathf.Max(0.05f, dimensions.y);
            dimensions.z = Mathf.Max(0.01f, dimensions.z);
            // distribute the authored opacity across layers, keeping extra layers from simply becoming opaque.
            var opacity = 1f - Mathf.Pow(1f - Mathf.Clamp(tuning.opacity, 0f, 0.98f), 1f / count);
            _properties.SetColor(ColorId, tuning.energyColor);
            _properties.SetFloat(DensityId, Mathf.Clamp(tuning.density, 0.1f, 3f));
            _properties.SetFloat(OpacityId, opacity);
            _properties.SetFloat(EmissionId, Mathf.Max(0f, tuning.emission));
            _properties.SetFloat(BandsId, Mathf.Clamp(tuning.brightnessBands, 2, 5));
            _properties.SetFloat(ScaleId, Mathf.Max(0.05f, tuning.noiseScale));
            _properties.SetFloat(SpeedId, Mathf.Max(0f, tuning.flowSpeed));
            _properties.SetFloat(TurbulenceId, Mathf.Clamp(tuning.turbulence, 0f, 2f));
            // normalize each new component independently so its opacity does not multiply with sheet count.
            _properties.SetColor(BackgroundColorId, tuning.backgroundColor);
            _properties.SetFloat(BackgroundOpacityId,
                1f - Mathf.Pow(1f - Mathf.Clamp(tuning.backgroundOpacity, 0f, 0.98f), 1f / count));
            _properties.SetFloat(BackgroundVariationId, Mathf.Clamp01(tuning.backgroundVariation));
            _properties.SetFloat(MidgroundOpacityId,
                1f - Mathf.Pow(1f - Mathf.Clamp(tuning.midgroundOpacity, 0f, 0.98f), 1f / count));
            _properties.SetFloat(MidgroundWidthId, Mathf.Clamp(tuning.midgroundWidth, 0.01f, 1f));
            _properties.SetFloat(FalloffId, Mathf.Clamp(tuning.edgeFalloff, 0.05f, 0.95f));

            // ==================================================
            // static sheet placement; flow itself runs entirely on the gpu
            // ==================================================

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null) continue;
                layer.enabled = i < count;
                if (i >= count) continue;
                var fraction = (i + 0.5f) / count;
                var tilt = Mathf.Clamp(tuning.layerTilt, 0f, 10f);
                // different tilts and flow phases break up the appearance of evenly stacked ground decals.
                layer.transform.SetLocalPositionAndRotation(new Vector3(0f, dimensions.z * fraction, 0f),
                    Quaternion.Euler(90f + Mathf.Sin(i * 2.3f) * tilt, 0f, Mathf.Cos(i * 1.7f) * tilt));
                layer.transform.localScale = new Vector3(dimensions.x, dimensions.y, 1f);
                layer.shadowCastingMode = ShadowCastingMode.Off;
                layer.receiveShadows = false;
                _properties.SetFloat(LayerId, i);
                layer.SetPropertyBlock(_properties);
            }
        }

        private void OnDisable()
        {
            if (layers == null) return;
            foreach (var layer in layers)
                if (layer != null) layer.enabled = false;
        }
    }
}
