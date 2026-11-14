namespace Game50033
{
    using Unity.Collections;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    public sealed class PlayerShadowRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private PlayerShadowTuning tuning;

        private static readonly int AvailableId = Shader.PropertyToID("_PlayerShadowMapAvailable");
        private static readonly int WeaponAvailableId = Shader.PropertyToID("_WeaponShadowMapAvailable");
        private PlayerShadowPass _pass;
        private PlayerShadowPass _weaponPass;
        private Camera _preparedCamera;
        private Camera _renderingCamera;
        private ScriptableRenderContext _context;
        private ScriptableCullingParameters _parameters;

        public override void Create()
        {
            _pass = new PlayerShadowPass("Player", "Player Shadows (Without Weapons)") { renderPassEvent = RenderPassEvent.AfterRenderingShadows };
            _weaponPass = new PlayerShadowPass("Weapon", "Weapon Shadows (Environment Only)") { renderPassEvent = RenderPassEvent.AfterRenderingShadows };
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            // reset on every camera, even while the feature is disabled, so receivers never sample a stale map.
            var cmd = CommandBufferPool.Get();
            cmd.SetGlobalFloat(AvailableId, 0f);
            cmd.SetGlobalFloat(WeaponAvailableId, 0f);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            _preparedCamera = null;

            _context = context;
            _renderingCamera = camera;
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            var camera = cameraData.camera;
            var pipeline = UniversalRenderPipeline.asset;
            if (_renderingCamera != camera || !isActive || tuning == null || pipeline == null || !pipeline.supportsMainLightShadows
                || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;

            if (!camera.TryGetCullingParameters(out var parameters)) return;
            var settings = cameraData;
            renderer.SetupCullingParameters(ref parameters, ref settings);

            // a separate native shadow cull preserves off-screen casters; filtering visible camera objects would not.
            parameters.shadowDistance = Mathf.Min(pipeline.shadowDistance, camera.farClipPlane);
            parameters.cullingOptions |= CullingOptions.ShadowCasters;
            _parameters = parameters;
            _preparedCamera = camera;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (tuning == null || _preparedCamera != renderingData.cameraData.camera) return;
            _pass.Setup(tuning.atlasResolution, tuning.excludedCasterLayers.value, _parameters, _context);
            _weaponPass.Setup(tuning.weaponAtlasResolution, tuning.weaponExcludedCasterLayers.value, _parameters, _context);
            renderer.EnqueuePass(_pass);
            renderer.EnqueuePass(_weaponPass);
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            Shader.SetGlobalFloat(AvailableId, 0f);
            Shader.SetGlobalFloat(WeaponAvailableId, 0f);
        }

        private sealed class PlayerShadowPass : ScriptableRenderPass
        {
            private readonly int _mapId, _matricesId, _spheresId, _radiiId, _sizeId, _cascadeCountId, _availableId;
            private readonly string _passName;
            private static readonly int BiasId = Shader.PropertyToID("_ShadowBias");
            private static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");
            private static readonly int CameraPositionId = Shader.PropertyToID("_WorldSpaceCameraPos");

            private static readonly GlobalKeyword PunctualShadowKeyword = GlobalKeyword.Create("_CASTING_PUNCTUAL_LIGHT_SHADOW");

            private int _atlasResolution, _excludedCasterLayers;
            private CullingResults _cullResults;
            private ScriptableCullingParameters _parameters;
            private ScriptableRenderContext _context;
            private readonly ShadowSliceData[] _slices = new ShadowSliceData[4];
            private readonly Vector4[] _spheres = new Vector4[4];

            private sealed class PassData
            {
                internal readonly ShadowSliceData[] slices = new ShadowSliceData[4];
                internal readonly RendererListHandle[] renderers = new RendererListHandle[4];
                internal readonly Matrix4x4[] matrices = new Matrix4x4[5];
                internal readonly Vector4[] spheres = new Vector4[4];
                internal readonly Vector4[] biases = new Vector4[4];
                internal Vector4 radii;
                internal Vector4 mapSize;
                internal Vector3 lightDirection;
                internal Vector3 cameraPosition;
                internal Matrix4x4 cameraView;
                internal Matrix4x4 cameraProjection;
                internal Rect cameraViewport;
                internal int cascadeCount;
                internal PlayerShadowPass owner;
            }

            internal PlayerShadowPass(string prefix, string passName)
            {
                _mapId = Shader.PropertyToID("_" + prefix + "ShadowMap");
                _matricesId = Shader.PropertyToID("_" + prefix + "WorldToShadow");
                _spheresId = Shader.PropertyToID("_" + prefix + "ShadowSplitSpheres");
                _radiiId = Shader.PropertyToID("_" + prefix + "ShadowSplitRadii");
                _sizeId = Shader.PropertyToID("_" + prefix + "ShadowMapSize");
                _cascadeCountId = Shader.PropertyToID("_" + prefix + "ShadowCascadeCount");
                _availableId = Shader.PropertyToID("_" + prefix + "ShadowMapAvailable");
                _passName = passName;
            }

            internal void Setup(int resolution, int excludedLayers, ScriptableCullingParameters parameters, ScriptableRenderContext context)
            {
                _atlasResolution = resolution;
                _excludedCasterLayers = excludedLayers;
                _parameters = parameters;
                _parameters.cullingMask &= (uint)~excludedLayers;
                _context = context;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var lightData = frameData.Get<UniversalLightData>();
                var shadowData = frameData.Get<UniversalShadowData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (!shadowData.supportsMainLightShadows || cameraData.maxShadowDistance <= 0f || lightData.mainLightIndex < 0) return;
#if UNITY_EDITOR
                if (CoreUtils.IsSceneLightingDisabled(cameraData.camera)) return;
#endif
                var light = lightData.visibleLights[lightData.mainLightIndex];
                if (light.lightType != LightType.Directional || light.light.shadows == LightShadows.None) return;

                // camera culling masks alone do not filter all native shadow casters. snapshot a filtered light mask
                // into this separate cull, then immediately restore it for ordinary world lighting and shadows.
                var originalMask = light.light.cullingMask;
                try
                {
                    light.light.cullingMask = originalMask & ~_excludedCasterLayers;
                    _cullResults = _context.Cull(ref _parameters);
                }
                finally
                {
                    light.light.cullingMask = originalMask;
                }

                var shadowLightIndex = -1;
                var lights = _cullResults.visibleLights;
                for (var i = 0; i < lights.Length; i++)
                    if (lights[i].light == light.light) { shadowLightIndex = i; break; }
                if (shadowLightIndex < 0) return;
                if (!_cullResults.GetShadowCasterBounds(shadowLightIndex, out _))
                {
                    // an empty filtered map means fully lit, not a fallback to the weapon-containing world map.
                    using var empty = graph.AddRasterRenderPass<PassData>(_passName + " (No Casters)", out var emptyData);
                    emptyData.owner = this;
                    empty.SetGlobalTextureAfterPass(graph.defaultResources.defaultShadowTexture, _mapId);
                    empty.AllowGlobalStateModification(true);
                    empty.SetRenderFunc(static (PassData pass, RasterGraphContext context) => context.cmd.SetGlobalFloat(pass.owner._availableId, 2f));
                    return;
                }

                var cascadeCount = shadowData.mainLightShadowCascadesCount;
                var width = Mathf.Clamp(Mathf.ClosestPowerOfTwo(_atlasResolution), 256, 8192);
                var height = cascadeCount == 2 ? width / 2 : width;
                var tileResolution = ShadowUtils.GetMaxTileResolutionInAtlas(width, height, cascadeCount);

                // unity 6 requires explicit shadow-caster culling for the independently filtered culling results.
                // temp arrays stay alive until frame end, matching URP's asynchronous culling lifetime.
                var splits = new NativeArray<ShadowSplitData>(cascadeCount, Allocator.Temp);
                var perLight = new NativeArray<LightShadowCasterCullingInfo>(lights.Length, Allocator.Temp);
                for (var cascade = 0; cascade < cascadeCount; cascade++)
                {
                    if (!ShadowUtils.ExtractDirectionalLightMatrix(ref _cullResults, shadowData, shadowLightIndex,
                            cascade, width, height, tileResolution, light.light.shadowNearPlane,
                            out _spheres[cascade], out _slices[cascade])) return;
                    splits[cascade] = _slices[cascade].splitData;
                }
                perLight[shadowLightIndex] = new LightShadowCasterCullingInfo
                {
                    splitRange = new RangeInt(0, cascadeCount),
                    projectionType = BatchCullingProjectionType.Orthographic
                };
                try
                {
                    light.light.cullingMask = originalMask & ~_excludedCasterLayers;
                    _context.CullShadowCasters(_cullResults, new ShadowCastersCullingInfos
                    {
                        splitBuffer = splits,
                        perLightInfos = perLight
                    });
                }
                finally
                {
                    light.light.cullingMask = originalMask;
                }

                using var builder = graph.AddRasterRenderPass<PassData>(_passName, out var data);
                data.owner = this;
                data.cascadeCount = cascadeCount;
                data.lightDirection = -light.localToWorldMatrix.GetColumn(2);
                data.cameraPosition = cameraData.worldSpaceCameraPos;
                data.cameraView = cameraData.GetViewMatrix();
                data.cameraProjection = cameraData.GetProjectionMatrix();
                data.cameraViewport = cameraData.camera.pixelRect;
                data.mapSize = new Vector4(1f / width, 1f / height, width, height);
                data.radii = Vector4.zero;

                // retain each material's own ShadowCaster pass, including sprite alpha clipping and dissolve.
                for (var cascade = 0; cascade < cascadeCount; cascade++)
                {
                    data.spheres[cascade] = _spheres[cascade];
                    data.slices[cascade] = _slices[cascade];
                    var settings = new ShadowDrawingSettings(_cullResults, shadowLightIndex)
                    {
                        // URP has already created lists for this light; do not continue its automatic split counter.
                        splitIndex = cascade,
                        // native rendering-layer tests override the light's gameobject mask; use the filtered mask here.
                        useRenderingLayerMaskTest = false
                    };
                    data.renderers[cascade] = graph.CreateShadowRendererList(ref settings);
                    builder.UseRendererList(data.renderers[cascade]);
                    data.matrices[cascade] = data.slices[cascade].shadowTransform;
                    data.radii[cascade] = data.spheres[cascade].w * data.spheres[cascade].w;
                    data.biases[cascade] = ShadowUtils.GetShadowBias(ref light, lightData.mainLightIndex,
                        shadowData, data.slices[cascade].projectionMatrix, tileResolution);
                }
                // outside all cascades, sample the cleared near plane rather than an unrelated atlas tile.
                var noShadow = Matrix4x4.zero;
                noShadow.m22 = SystemInfo.usesReversedZBuffer ? 1f : 0f;
                for (var i = cascadeCount; i < 5; i++) data.matrices[i] = noShadow;

                var texture = graph.CreateTexture(new TextureDesc(width, height)
                {
                    name = _passName + " Atlas",
                    depthBufferBits = DepthBits.Depth16,
                    isShadowMap = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = true
                });
                builder.SetRenderAttachmentDepth(texture, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(texture, _mapId);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetGlobalVector(CameraPositionId, pass.cameraPosition);
                    cmd.SetGlobalVector(LightDirectionId, pass.lightDirection);
                    cmd.SetKeyword(PunctualShadowKeyword, false);
                    cmd.SetGlobalDepthBias(1f, 2.5f);
                    for (var i = 0; i < pass.cascadeCount; i++)
                    {
                        var slice = pass.slices[i];
                        cmd.SetGlobalVector(BiasId, pass.biases[i]);
                        cmd.SetViewport(new Rect(slice.offsetX, slice.offsetY, slice.resolution, slice.resolution));
                        cmd.SetViewProjectionMatrices(slice.viewMatrix, slice.projectionMatrix);
                        cmd.DrawRendererList(pass.renderers[i]);
                    }
                    cmd.DisableScissorRect();
                    cmd.SetGlobalDepthBias(0f, 0f);
                    cmd.SetViewProjectionMatrices(pass.cameraView, pass.cameraProjection);
                    cmd.SetViewport(pass.cameraViewport);
                    cmd.SetGlobalMatrixArray(pass.owner._matricesId, pass.matrices);
                    cmd.SetGlobalVectorArray(pass.owner._spheresId, pass.spheres);
                    cmd.SetGlobalVector(pass.owner._radiiId, pass.radii);
                    cmd.SetGlobalVector(pass.owner._sizeId, pass.mapSize);
                    cmd.SetGlobalInt(pass.owner._cascadeCountId, pass.cascadeCount);
                    cmd.SetGlobalFloat(pass.owner._availableId, 1f);
                });
            }
        }
    }
}
