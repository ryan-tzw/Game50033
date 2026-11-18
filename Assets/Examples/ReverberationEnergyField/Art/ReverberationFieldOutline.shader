Shader "Game50033/Experiments/Reverberation Field Outline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_OutlineMask);
            float4 _OutlineTexelSize;
            half4 _OutlineColor;
            float _OutlineThreshold;
            int _OutlineWidth;
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                output.uv = GetFullScreenTriangleTexCoord(vertexID);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float center = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_PointClamp, input.uv).r;
                clip(center - _OutlineThreshold);
                float minimum = center;
                // an inner border stays inside the depth-tested silhouette, never dilating over an opaque occluder.
                // sampling every distance keeps wider borders continuous around narrow contours.
                for (int distance = 1; distance <= _OutlineWidth; distance++)
                {
                    float2 offset = _OutlineTexelSize.xy * distance;
                    minimum = min(minimum, SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_PointClamp, input.uv + float2(offset.x, 0)).r);
                    minimum = min(minimum, SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_PointClamp, input.uv - float2(offset.x, 0)).r);
                    minimum = min(minimum, SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_PointClamp, input.uv + float2(0, offset.y)).r);
                    minimum = min(minimum, SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_PointClamp, input.uv - float2(0, offset.y)).r);
                }
                clip(_OutlineThreshold - minimum - 0.000001);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
