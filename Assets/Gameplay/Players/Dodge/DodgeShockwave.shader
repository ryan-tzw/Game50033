Shader "Game50033/Dodge Shockwave"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShockwaveVertex
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Shockwaves[8];
            float4 _ShockwaveStrengths[8];
            float4 _ShockwaveGrid;
            int _ShockwaveCount;
            float _ShockwaveLimit;
            Varyings ShockwaveVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 cellUV = (floor(uv * _ShockwaveGrid.xy) + 0.5) / _ShockwaveGrid.xy;
                float2 offset = 0;
                [loop] for (int i = 0; i < _ShockwaveCount; i++)
                {
                    float2 delta = cellUV - _Shockwaves[i].xy;
                    float2 circular = delta * float2(_ShockwaveGrid.z, 1);
                    float distance = length(circular);
                    float ring = 1 - smoothstep(0, max(0.000001, _Shockwaves[i].w * 0.5), abs(distance - _Shockwaves[i].z));
                    float2 direction = delta * _ShockwaveGrid.xy;
                    float directionLength = length(direction);
                    if (ring > 0 && directionLength > 0.0001)
                        offset += direction / directionLength * (ring * _ShockwaveStrengths[i].x);
                }
                float offsetLength = length(offset);
                if (offsetLength > _ShockwaveLimit)
                    offset *= _ShockwaveLimit / offsetLength;
                // shift by whole reference pixels while retaining full-resolution details inside each shifted cell.
                // point sampling avoids introducing intermediate colours or blur into the composed image.
                float2 displacedUV = saturate(uv + round(offset) / _ShockwaveGrid.xy);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, displacedUV, 0);
            }
            ENDHLSL
        }
    }
}
