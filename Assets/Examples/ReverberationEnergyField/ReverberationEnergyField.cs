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
        [SerializeField] private ReverberationFieldShape shape = new()
        {
            direction = Vector2.up,
            outerRadius = 1f,
            sweepAngle = 140f,
            stretchAxis = Vector2.up,
            stretchFactor = 1f
        };
        private Matrix4x4 _appliedRootMatrix;
        private MaterialPropertyBlock _properties;
        private ReverberationEnergyFieldTuning _appliedTuning;
        private int _appliedRevision = -1;
        private Color? _energyColor;
        private Color? _backgroundColor;
        private Camera _footprintCamera;
        private Vector3 _appliedViewDirection;
        private bool _keepBoundaryInsideFootprint;
        private ReverberationFieldSpawn _spawn;
        private float _appliedSpawnProgress;
        private float _appliedOpacity;
        private float _fadeOutDuration;
        private float _fadeOutStartedAt;
        private float _fadeOutSpawnProgress;
        private Vector3 _layerOpacities;
        private Vector4 _fullSizeGeometry;

        private static readonly int ColorId = Shader.PropertyToID("_EnergyColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int OpacityId = Shader.PropertyToID("_LayerOpacity");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int BandsId = Shader.PropertyToID("_BrightnessBands");
        private static readonly int ScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int SpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");
        private static readonly int FalloffId = Shader.PropertyToID("_EdgeFalloff");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeDistortionStrength");
        private static readonly int EdgeSpeedId = Shader.PropertyToID("_EdgeDistortionSpeed");
        private static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
        private static readonly int BackgroundOpacityId = Shader.PropertyToID("_BackgroundLayerOpacity");
        private static readonly int BackgroundVariationId = Shader.PropertyToID("_BackgroundVariation");
        private static readonly int MidgroundOpacityId = Shader.PropertyToID("_MidgroundLayerOpacity");
        private static readonly int MidgroundWidthId = Shader.PropertyToID("_MidgroundWidth");
        private static readonly int ShapeKindId = Shader.PropertyToID("_FieldShapeKind");
        private static readonly int ShapeFrameId = Shader.PropertyToID("_ShapeOriginDirection");
        private static readonly int ShapeGeometryId = Shader.PropertyToID("_ShapeGeometry");
        private static readonly int ShapeStretchId = Shader.PropertyToID("_ShapeStretch");
        private static readonly int RootInverseId = Shader.PropertyToID("_FieldWorldToLocal");
        private static readonly int LayerId = Shader.PropertyToID("_Layer");

        private void OnEnable()
        {
            _properties ??= new MaterialPropertyBlock();
            _appliedRevision = -1;
        }

        public void SetShape(ReverberationFieldShape geometry)
        {
            shape = geometry;
            _appliedRevision = -1;
        }

        internal ReverberationFieldSpawn GetSpawnAnimation(float startedAt)
        {
            return tuning != null ? new ReverberationFieldSpawn(startedAt, tuning.spawnDuration, tuning.spawnStartScale) : default;
        }

        internal float GameplayLifetime => tuning != null
            ? Mathf.Max(0.01f, tuning.gameplayLifetime) : ReverberationEnergyFieldTuning.DefaultGameplayLifetime;

        internal float BeginFadeOut()
        {
            _fadeOutStartedAt = Time.time;
            _fadeOutSpawnProgress = _spawn.Progress;
            _fadeOutDuration = tuning != null ? Mathf.Max(0f, tuning.fadeOutDuration) : 0f;
            return _fadeOutDuration;
        }

        internal void ConfigureGameplayAppearance(Color energyColor, Color backgroundColor, Camera footprintCamera,
            ReverberationFieldSpawn spawn)
        {
            _energyColor = energyColor;
            _backgroundColor = backgroundColor;
            _footprintCamera = footprintCamera;
            _keepBoundaryInsideFootprint = true;
            _spawn = spawn;
            _appliedRevision = -1;
        }

        private void OnValidate()
        {
            _appliedRevision = -1;
        }

        private void LateUpdate()
        {
            if (tuning == null || layers == null || layers.Length == 0) return;
            // legacy uses mesh UVs; procedural shapes need the shared root frame refreshed only when it changes.
            var rootMatrix = shape.kind == ReverberationFieldShapeKind.LegacyEllipse ? Matrix4x4.identity : transform.localToWorldMatrix;
            var viewDirection = _footprintCamera != null && _footprintCamera.orthographic
                ? _footprintCamera.transform.forward : Vector3.zero;
            // an early consumption fades from the size and opacity the field has reached.
            var fadingOut = _fadeOutDuration > 0f;
            var spawnProgress = fadingOut ? _fadeOutSpawnProgress : _spawn.Progress;
            var opacity = fadingOut
                ? spawnProgress * Mathf.SmoothStep(1f, 0f, (Time.time - _fadeOutStartedAt) / _fadeOutDuration)
                : spawnProgress;
            if (_appliedTuning == tuning && _appliedRevision == tuning.Revision
                && _appliedRootMatrix == rootMatrix && _appliedViewDirection == viewDirection)
            {
                if (_appliedSpawnProgress != spawnProgress || _appliedOpacity != opacity)
                    ApplyAnimatedAppearance(spawnProgress, opacity);
                return;
            }
            _appliedRootMatrix = rootMatrix;
            _appliedTuning = tuning;
            _appliedRevision = tuning.Revision;
            _appliedViewDirection = viewDirection;

            // ==================================================
            // shared field parameters
            // ==================================================

            var count = Mathf.Clamp(tuning.layerCount, 1, layers.Length);
            var dimensions = tuning.fieldDimensions;
            dimensions.x = Mathf.Max(0.05f, dimensions.x);
            dimensions.y = Mathf.Max(0.05f, dimensions.y);
            dimensions.z = Mathf.Max(0.01f, dimensions.z);
            // distribute the authored opacity across layers, keeping extra layers from simply becoming opaque.
            _layerOpacities = new Vector3(
                1f - Mathf.Pow(1f - Mathf.Clamp(tuning.opacity, 0f, 0.98f), 1f / count),
                1f - Mathf.Pow(1f - Mathf.Clamp(tuning.backgroundOpacity, 0f, 0.98f), 1f / count),
                1f - Mathf.Pow(1f - Mathf.Clamp(tuning.midgroundOpacity, 0f, 0.98f), 1f / count));
            _properties.SetColor(ColorId, _energyColor ?? tuning.energyColor);
            _properties.SetFloat(DensityId, Mathf.Clamp(tuning.density, 0.1f, 3f));
            _properties.SetFloat(EmissionId, Mathf.Max(0f, tuning.emission));
            _properties.SetFloat(BandsId, Mathf.Clamp(tuning.brightnessBands, 2, 5));
            _properties.SetFloat(ScaleId, Mathf.Max(0.05f, tuning.noiseScale));
            _properties.SetFloat(SpeedId, Mathf.Max(0f, tuning.flowSpeed));
            _properties.SetFloat(TurbulenceId, Mathf.Clamp(tuning.turbulence, 0f, 2f));
            _properties.SetColor(BackgroundColorId, _backgroundColor ?? tuning.backgroundColor);
            _properties.SetFloat(BackgroundVariationId, Mathf.Clamp01(tuning.backgroundVariation));
            _properties.SetFloat(MidgroundWidthId, Mathf.Clamp(tuning.midgroundWidth, 0.01f, 1f));
            var edgeStrength = _keepBoundaryInsideFootprint ? 0f : Mathf.Clamp01(tuning.edgeDistortionStrength);
            _properties.SetFloat(EdgeStrengthId, edgeStrength);
            _properties.SetFloat(EdgeSpeedId, Mathf.Max(0f, tuning.edgeDistortionSpeed));
            _properties.SetFloat(FalloffId, Mathf.Clamp(tuning.edgeFalloff, 0.05f, 0.95f));

            // ==================================================
            // per-instance footprint and shared root coordinates
            // ==================================================

            var direction = shape.direction.sqrMagnitude > 0.0001f ? shape.direction.normalized : Vector2.up;
            var stretchAxis = shape.stretchAxis.sqrMagnitude > 0.0001f ? shape.stretchAxis.normalized : Vector2.up;
            var stretch = Mathf.Max(1f, shape.stretchFactor);
            var radius = Mathf.Max(0.01f, shape.outerRadius);
            _properties.SetFloat(ShapeKindId, (float)shape.kind);
            var rootInverse = rootMatrix.inverse;
            var viewInRoot = rootInverse.MultiplyVector(viewDirection);
            var toGround = Matrix4x4.identity;
            // project each tilted sheet along the gameplay camera rays so all layers share the ground silhouette.
            if (Mathf.Abs(viewInRoot.y) > 0.0001f)
            {
                toGround.m01 = -viewInRoot.x / viewInRoot.y;
                toGround.m21 = -viewInRoot.z / viewInRoot.y;
            }
            _properties.SetMatrix(RootInverseId, toGround * rootInverse);
            _properties.SetVector(ShapeFrameId, new Vector4(shape.origin.x, shape.origin.y, direction.x, direction.y));
            _fullSizeGeometry = new Vector4(Mathf.Clamp(shape.innerRadius, 0f, radius), radius,
                Mathf.Clamp(shape.sweepAngle, 0f, 360f) * Mathf.Deg2Rad * 0.5f, 0f);
            _properties.SetVector(ShapeStretchId, new Vector4(stretchAxis.x, stretchAxis.y, stretch, 1f / stretch));

            // bound the complete stretched circle conservatively, including maximum outward boundary motion.
            // these bounds also contain reflex sectors and full rings without a separate mesh for each shape.
            var paddedRadius = radius * (1.05f + edgeStrength);
            var stretchSquared = stretch * stretch - 1f;
            var extentX = paddedRadius * Mathf.Sqrt(1f + stretchSquared * stretchAxis.x * stretchAxis.x);
            var extentZ = paddedRadius * Mathf.Sqrt(1f + stretchSquared * stretchAxis.y * stretchAxis.y);

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
                var rotation = Quaternion.Euler(90f + Mathf.Sin(i * 2.3f) * tilt, 0f, Mathf.Cos(i * 1.7f) * tilt);
                var center = new Vector3(0f, dimensions.z * fraction, 0f);
                var scale = new Vector3(dimensions.x, dimensions.y, 1f);
                if (shape.kind != ReverberationFieldShapeKind.LegacyEllipse)
                {
                    center.x = shape.origin.x - toGround.m01 * center.y;
                    center.z = shape.origin.y - toGround.m21 * center.y;
                    // invert the quad's projected basis so it covers the whole footprint even when tilted.
                    var right = toGround.MultiplyVector(rotation * Vector3.right);
                    var up = toGround.MultiplyVector(rotation * Vector3.up);
                    var determinant = right.x * up.z - up.x * right.z;
                    scale.x = 2f * (Mathf.Abs(up.z / determinant) * extentX + Mathf.Abs(up.x / determinant) * extentZ);
                    scale.y = 2f * (Mathf.Abs(right.z / determinant) * extentX + Mathf.Abs(right.x / determinant) * extentZ);
                }
                layer.transform.SetLocalPositionAndRotation(center, rotation);
                layer.transform.localScale = scale;
                layer.shadowCastingMode = ShadowCastingMode.Off;
                layer.receiveShadows = false;
            }
            ApplyAnimatedAppearance(spawnProgress, opacity);
        }

        private void ApplyAnimatedAppearance(float spawnProgress, float opacity)
        {
            _appliedSpawnProgress = spawnProgress;
            _appliedOpacity = opacity;
            var scale = _spawn.GetScale(spawnProgress);
            // animate shader radii and opacity while the sheets keep their full-size bounds.
            var geometry = _fullSizeGeometry;
            geometry.x *= scale;
            geometry.y *= scale;
            _properties.SetVector(ShapeGeometryId, geometry);
            _properties.SetFloat(OpacityId, _layerOpacities.x * opacity);
            _properties.SetFloat(BackgroundOpacityId, _layerOpacities.y * opacity);
            _properties.SetFloat(MidgroundOpacityId, _layerOpacities.z * opacity);
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null || !layer.enabled) continue;
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
