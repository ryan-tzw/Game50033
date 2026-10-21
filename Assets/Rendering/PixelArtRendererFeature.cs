namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.RenderGraphModule.Util;
    using UnityEngine.Rendering.Universal;

    public sealed class PixelArtRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Vector2Int resolution = new(640, 360);
        [SerializeField] private LayerMask worldLayers = ~0;
        [SerializeField] private LayerMask toonLayers;
        [SerializeField] private LayerMask spriteLayers;
        [SerializeField] private Shader toonOverrideShader;
        [SerializeField] private Material edgeDetectMat;

        private PixelArtPass _pass;

        public override void Create()
        {
            _pass = new PixelArtPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _pass.Setup(resolution, worldLayers, toonLayers, spriteLayers, toonOverrideShader, edgeDetectMat);
            renderer.EnqueuePass(_pass);
        }
        
        // ================================================== 
        // Basically everything happens in this class
        // ================================================== 

        private sealed class PixelArtPass : ScriptableRenderPass
        {
            private Vector2Int _resolution;
            private LayerMask _worldLayers;
            private LayerMask _toonLayers;
            private LayerMask _spriteLayers;
            private Shader _toonOverrideShader;
            private Material _edgeDetectMat;
            
            // ==================================================  
            // shader pass tags
            // ==================================================  
            
            private static readonly List<ShaderTagId> ForwardShaderTags = new()
            {
                new("SRPDefaultUnlit"),
                new("UniversalForward"),
                new("UniversalForwardOnly")
            };

            private static readonly List<ShaderTagId> DepthNormalShaderTags = new()
            {
                new("DepthNormals"),
                new("DepthNormalsOnly")
            };

            // ==================================================  
            // shader property IDs
            // ==================================================  
            
            private static readonly int CameraNormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
            private static readonly int PixelArtResolutionId = Shader.PropertyToID("_PixelArtResolution");
            private static readonly int PixelArtColorTextureId = Shader.PropertyToID("_PixelArtColorTexture");
            private static readonly int PixelArtDepthTextureId = Shader.PropertyToID("_PixelArtDepthTexture");
            private static readonly MaterialPropertyBlock EdgeProperties = new();

            // ==================================================  
            // Classes to store data for each render pass
            // ==================================================  
            
            private sealed class WorldPassData
            {
                internal RendererListHandle RegularRenderers;
                internal RendererListHandle ToonRenderers;
            }

            private sealed class DepthNormalPassData
            {
                internal RendererListHandle Renderers;
            }

            private sealed class EdgePassData
            {
                internal TextureHandle Color;
                internal TextureHandle Depth;
                internal TextureHandle Normals;
                internal Material Material;
                internal Vector2Int Resolution;
            }

            private sealed class SpritePassData
            {
                internal RendererListHandle Renderers;
            }
            
            // ================================================== 
            // Setup
            // ================================================== 

            public void Setup(Vector2Int resolution, LayerMask worldLayers, LayerMask toonLayers, LayerMask spriteLayers, Shader toonOverrideShader, Material edgeDetectMat)
            {
                _resolution.x = Mathf.Max(1, resolution.x);
                _resolution.y = Mathf.Max(1, resolution.y);
                _worldLayers = worldLayers;
                _toonLayers = toonLayers;
                _spriteLayers = spriteLayers;
                _toonOverrideShader = toonOverrideShader;
                _edgeDetectMat = edgeDetectMat;
                requiresIntermediateTexture = true;
            }
            
            // ==================================================  
            // Create renderer lists
            // ==================================================  

            // determine which objects to draw
            private static RendererListHandle CreateRendererList(
                RenderGraph renderGraph,
                UniversalRenderingData renderingData,
                UniversalCameraData cameraData,
                UniversalLightData lightData,
                int layerMask,
                Shader overrideShader = null)
            {
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    ForwardShaderTags,
                    renderingData, 
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags
                );

                if (overrideShader != null)
                {
                    drawingSettings.overrideShader = overrideShader;
                    drawingSettings.overrideShaderPassIndex = 0;
                }

                // restrict to just opaque materials and objects in the masked layers
                FilteringSettings filteringSettings = new(RenderQueueRange.opaque, layerMask);
                RendererListParams rendererListParams = new(renderingData.cullResults, drawingSettings, filteringSettings);

                return renderGraph.CreateRendererList(rendererListParams);
            }

            private static RendererListHandle CreateDepthNormalRendererList(
                RenderGraph renderGraph,
                UniversalRenderingData renderingData,
                UniversalCameraData cameraData,
                UniversalLightData lightData,
                int layerMask)
            {
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    DepthNormalShaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags
                );

                drawingSettings.perObjectData = PerObjectData.None;

                FilteringSettings filteringSettings = new(RenderQueueRange.opaque, layerMask);
                RendererListParams rendererListParams = new(renderingData.cullResults, drawingSettings, filteringSettings);

                return renderGraph.CreateRendererList(rendererListParams);
            }
            
            // ==================================================
            // compose render passes
            // ==================================================
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                if (resourceData.isActiveTargetBackBuffer)
                {
                    Debug.LogError("PixelArtRendererFeature requires an intermediate colour texture");
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;

                // ==================================================  
                // low-res render targets
                // ==================================================  
                
                TextureDesc lowResColorDesc = renderGraph.GetTextureDesc(source);
                lowResColorDesc.name = "PixelWorld_LowResolutionColor";
                lowResColorDesc.sizeMode = TextureSizeMode.Explicit;
                lowResColorDesc.width = _resolution.x;
                lowResColorDesc.height = _resolution.y;
                lowResColorDesc.depthBufferBits = DepthBits.None;
                lowResColorDesc.msaaSamples = MSAASamples.None;
                lowResColorDesc.bindTextureMS = false;
                lowResColorDesc.useDynamicScale = false;
                lowResColorDesc.useDynamicScaleExplicit = false;
                lowResColorDesc.filterMode = FilterMode.Point;
                lowResColorDesc.clearBuffer = true;
                lowResColorDesc.clearColor = cameraData.camera.backgroundColor;
                TextureHandle lowResColor = renderGraph.CreateTexture(lowResColorDesc);

                TextureDesc lowResDepthDesc = renderGraph.GetTextureDesc(resourceData.activeDepthTexture);
                lowResDepthDesc.name = "PixelWorld_LowResolutionDepth";
                lowResDepthDesc.sizeMode = TextureSizeMode.Explicit;
                lowResDepthDesc.width = _resolution.x;
                lowResDepthDesc.height = _resolution.y;
                lowResDepthDesc.msaaSamples = MSAASamples.None;
                lowResDepthDesc.bindTextureMS = false;
                lowResDepthDesc.useDynamicScale = false;
                lowResDepthDesc.useDynamicScaleExplicit = false;
                lowResDepthDesc.filterMode = FilterMode.Point;
                lowResDepthDesc.clearBuffer = true;
                TextureHandle lowResDepth = renderGraph.CreateTexture(lowResDepthDesc);

                TextureDesc lowResNormalsDesc = lowResColorDesc;
                lowResNormalsDesc.name = "PixelWorld_LowResolutionNormals";
                lowResNormalsDesc.format = UnityEngine.Rendering.Universal.Internal.DepthNormalOnlyPass.GetGraphicsFormat();
                lowResNormalsDesc.clearBuffer = true;
                lowResNormalsDesc.clearColor = Color.black;
                TextureHandle lowResNormals = renderGraph.CreateTexture(lowResNormalsDesc);
                
                // ==================================================
                // low-res color+depth pass
                // ==================================================

                using (var builder = renderGraph.AddRasterRenderPass<WorldPassData>("Pixel Art Low-Resolution World", out var passData))
                {
                    int toonMask = _worldLayers.value & _toonLayers.value;     // world and toon
                    int regularMask = _worldLayers.value & ~_toonLayers.value; // world and NOT toon

                    passData.RegularRenderers = CreateRendererList(renderGraph, renderingData, cameraData, lightData, regularMask);
                    passData.ToonRenderers = CreateRendererList(renderGraph, renderingData, cameraData, lightData, toonMask, _toonOverrideShader);

                    builder.UseAllGlobalTextures(true);
                    builder.UseRendererList(passData.RegularRenderers);
                    builder.UseRendererList(passData.ToonRenderers);
                    builder.SetRenderAttachment(lowResColor, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(lowResDepth, AccessFlags.Write);

                    // perform the draws
                    builder.SetRenderFunc(static (WorldPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.RegularRenderers);
                        context.cmd.DrawRendererList(data.ToonRenderers);
                    });
                }

                // ==================================================
                // low-res depth+normal pass
                // ==================================================
                
                using (var builder = renderGraph.AddRasterRenderPass<DepthNormalPassData>("Pixel Art Low-Resolution Depth Normals", out var passData))
                {
                    passData.Renderers = CreateDepthNormalRendererList(renderGraph, renderingData, cameraData, lightData, _worldLayers.value);

                    builder.UseRendererList(passData.Renderers);
                    builder.SetRenderAttachment(lowResNormals, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(lowResDepth, AccessFlags.ReadWrite);

                    builder.SetRenderFunc(static (DepthNormalPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.Renderers);
                    });
                }

                // ================================================== 
                // edge detection pass
                // ================================================== 
                
                TextureHandle outlinedColor = lowResColor;

                if (_edgeDetectMat != null)
                {
                    TextureDesc edgeDesc = lowResColorDesc;
                    edgeDesc.name = "PixelWorld_EdgeDetection";
                    edgeDesc.clearBuffer = false;
                    
                    outlinedColor = renderGraph.CreateTexture(edgeDesc);

                    using var builder = renderGraph.AddRasterRenderPass<EdgePassData>("Pixel Art Edge Detection", out var passData);

                    passData.Color = lowResColor;
                    passData.Depth = lowResDepth;
                    passData.Normals = lowResNormals;
                    passData.Material = _edgeDetectMat;
                    passData.Resolution = _resolution;

                    builder.UseTexture(lowResColor, AccessFlags.Read);
                    builder.UseTexture(lowResDepth, AccessFlags.Read);
                    builder.UseTexture(lowResNormals, AccessFlags.Read);
                    builder.SetRenderAttachment(outlinedColor, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (EdgePassData data, RasterGraphContext context) =>
                    {
                        EdgeProperties.Clear();
                        EdgeProperties.SetTexture(PixelArtColorTextureId, data.Color);
                        EdgeProperties.SetTexture(PixelArtDepthTextureId, data.Depth);
                        EdgeProperties.SetTexture(CameraNormalsTextureId, data.Normals);
                        EdgeProperties.SetVector(PixelArtResolutionId, new Vector4(data.Resolution.x, data.Resolution.y, 0f, 0f));

                        // draw fullscreen quad
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Quads, 4, 1, EdgeProperties);
                    });
                }
                
                // ================================================== 
                // upscale low-res depth to use for sprite occlusion
                // ================================================== 
                
                TextureDesc fullResDepthDesc = renderGraph.GetTextureDesc(resourceData.activeDepthTexture);
                fullResDepthDesc.name = "PixelWorld_FullResolutionDepth";
                fullResDepthDesc.msaaSamples = MSAASamples.None;
                fullResDepthDesc.bindTextureMS = false;
                fullResDepthDesc.filterMode = FilterMode.Point;
                fullResDepthDesc.clearBuffer = false;
                TextureHandle fullResDepth = renderGraph.CreateTexture(fullResDepthDesc);

                renderGraph.AddBlitPass(
                    lowResDepth,
                    fullResDepth,
                    Vector2.one,
                    Vector2.zero,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                    passName: "Pixel Art Depth Upscale"
                );

                // ================================================== 
                // upscale and full-res sprite pass
                // ================================================== 

                TextureDesc fullResDesc = renderGraph.GetTextureDesc(source);
                fullResDesc.name = "PixelWorld_FullResolution";
                fullResDesc.depthBufferBits = DepthBits.None;
                fullResDesc.msaaSamples = MSAASamples.None;
                fullResDesc.bindTextureMS = false;
                fullResDesc.filterMode = FilterMode.Point;
                fullResDesc.clearBuffer = false;
                
                TextureHandle fullRes = renderGraph.CreateTexture(fullResDesc);

                renderGraph.AddBlitPass(
                    outlinedColor,
                    fullRes,
                    Vector2.one,
                    Vector2.zero,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                    passName: "Pixel Art Upscale"
                );

                using (var builder = renderGraph.AddRasterRenderPass<SpritePassData>("Pixel Art Full-Resolution Sprites", out var passData))
                {
                    passData.Renderers = CreateRendererList(renderGraph, renderingData, cameraData, lightData, _spriteLayers.value);

                    builder.UseAllGlobalTextures(true);
                    builder.UseRendererList(passData.Renderers);
                    builder.SetRenderAttachment(fullRes, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(fullResDepth, AccessFlags.ReadWrite);

                    builder.SetRenderFunc(static (SpritePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.Renderers);
                    });
                }

                // all subsequent URP passes will use these
                resourceData.cameraColor = fullRes;
                resourceData.cameraDepth = fullResDepth;
            }
        }
    }
}