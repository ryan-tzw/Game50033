namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    public sealed class DodgeShockwaveRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material material;
        [SerializeField] private PixelArtRendererFeature pixelArtRenderer;
        private ShockwavePass _pass;

        public override void Create()
        {
            _pass = new ShockwavePass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pixelArtRenderer == null || PlayerDodgeVFX.Active.Count == 0
                || renderingData.cameraData.cameraType != CameraType.Game
                || renderingData.cameraData.renderType != CameraRenderType.Base) return;
            _pass.Setup(material, pixelArtRenderer.Resolution);
            renderer.EnqueuePass(_pass);
        }

        private sealed class ShockwavePass : ScriptableRenderPass
        {
            private const int MaxWaves = 8;
            private static readonly int SourceId = Shader.PropertyToID("_BlitTexture");
            private static readonly int WavesId = Shader.PropertyToID("_Shockwaves");
            private static readonly int StrengthsId = Shader.PropertyToID("_ShockwaveStrengths");
            private static readonly int CountId = Shader.PropertyToID("_ShockwaveCount");
            private static readonly int GridId = Shader.PropertyToID("_ShockwaveGrid");
            private static readonly int LimitId = Shader.PropertyToID("_ShockwaveLimit");
            private readonly Vector4[] _waves = new Vector4[MaxWaves];
            private readonly Vector4[] _strengths = new Vector4[MaxWaves];
            private Material _material;
            private Vector2Int _resolution;

            private sealed class PassData
            {
                internal TextureHandle source;
                internal Material material;
                internal readonly Vector4[] waves = new Vector4[MaxWaves];
                internal readonly Vector4[] strengths = new Vector4[MaxWaves];
                internal readonly MaterialPropertyBlock properties = new();
                internal int count;
                internal float limit;
                internal Vector4 grid;
            }

            internal void Setup(Material material, Vector2Int resolution)
            {
                _material = material;
                _resolution = resolution;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer || !cameraData.camera.orthographic) return;

                // snapshot per-camera wave data in the graph pass; do not mutate a shared material between cameras.
                // create the render target only after finding a visible, live wave.
                var source = resources.activeColorTexture;
                var count = 0;
                var combinedLimit = 0f;
                // the compatibility-only cameraData GPU projection accessor returns zero in render graph mode.
                var projection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                var viewProjection = projection * cameraData.GetViewMatrix();
                var radiusToUV = Mathf.Abs(projection.m11) * 0.5f;
                var desc = graph.GetTextureDesc(source);
                var aspect = (float)desc.width / Mathf.Max(1, desc.height);
                var grid = new Vector4(Mathf.Max(1, _resolution.x), Mathf.Max(1, _resolution.y), aspect, 0f);

                for (var ownerIndex = 0; ownerIndex < PlayerDodgeVFX.Active.Count; ownerIndex++)
                {
                    var owner = PlayerDodgeVFX.Active[ownerIndex];
                    if (owner == null || !owner.isActiveAndEnabled || !owner.Tuning.shockwaveEnabled) continue;
                    var tuning = owner.Tuning;
                    for (var waveIndex = 0; waveIndex < owner.Shockwaves.Count; waveIndex++)
                    {
                        var wave = owner.Shockwaves[waveIndex];
                        if (wave.duration <= 0f) continue;
                        var age = (Time.time - wave.startedAt) / wave.duration;
                        if (age < 0f || age >= 1f) continue;
                        var growth = tuning.shockwaveExpansion != null ? tuning.shockwaveExpansion.Evaluate(age) : age;
                        var radius = Mathf.Max(0f, Mathf.LerpUnclamped(tuning.shockwaveInitialRadius, tuning.shockwaveFinalRadius, growth)) * radiusToUV;
                        var thickness = Mathf.Max(0.001f, tuning.shockwaveThickness) * radiusToUV;
                        var fade = tuning.shockwaveFalloff != null ? tuning.shockwaveFalloff.Evaluate(age) : 1f - age;
                        var strength = Mathf.Max(0f, fade * tuning.shockwaveStrengthPixels);
                        var limit = Mathf.Max(0f, tuning.shockwaveCombinedLimitPixels);
                        if (strength <= 0f || limit <= 0f) continue;
                        var clip = viewProjection * new Vector4(wave.origin.x, wave.origin.y, wave.origin.z, 1f);
                        var uv = new Vector2(clip.x / clip.w, clip.y / clip.w) * 0.5f + Vector2.one * 0.5f;
                        if (SystemInfo.graphicsUVStartsAtTop) uv.y = 1f - uv.y;
                        var reach = radius + thickness * 0.5f;
                        if (uv.x + reach / aspect < 0f || uv.x - reach / aspect > 1f
                            || uv.y + reach < 0f || uv.y - reach > 1f) continue;
                        if (count == MaxWaves) break;
                        _waves[count] = new Vector4(uv.x, uv.y, radius, thickness);
                        _strengths[count] = new Vector4(strength, 0f, 0f, 0f);
                        combinedLimit = Mathf.Max(combinedLimit, limit);
                        count++;
                    }
                    if (count == MaxWaves) break;
                }
                if (count == 0) return;

                using var builder = graph.AddRasterRenderPass<PassData>("Dodge Distortion Shockwaves", out var data);
                data.source = source;
                data.material = _material;
                data.count = count;
                data.limit = combinedLimit;
                data.grid = grid;
                System.Array.Copy(_waves, data.waves, count);
                System.Array.Copy(_strengths, data.strengths, count);

                desc.name = "DodgeShockwave_ComposedColor";
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.bindTextureMS = false;
                desc.clearBuffer = false;
                var destination = graph.CreateTexture(desc);
                builder.UseTexture(data.source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    pass.properties.Clear();
                    pass.properties.SetTexture(SourceId, pass.source);
                    pass.properties.SetVectorArray(WavesId, pass.waves);
                    pass.properties.SetVectorArray(StrengthsId, pass.strengths);
                    pass.properties.SetInteger(CountId, pass.count);
                    pass.properties.SetVector(GridId, pass.grid);
                    pass.properties.SetFloat(LimitId, pass.limit);
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1, pass.properties);
                });
                resources.cameraColor = destination;
            }
        }
    }
}
