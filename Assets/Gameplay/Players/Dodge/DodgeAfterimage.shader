Shader "Game50033/Dodge Afterimage"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _SilhouetteColor("Silhouette", Color) = (1,1,1,0.6)
    }
    SubShader
    {
        // billboard projection needs each sprite's object-space vertices; batching would pretransform them.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _SilhouetteColor;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // match the avatar's camera-facing projection while retaining its original mesh depth.
                float2 scale = float2(length(TransformObjectToWorldDir(float3(1,0,0), false)),
                                      length(TransformObjectToWorldDir(float3(0,1,0), false)));
                float3 pivotVS = TransformWorldToView(TransformObjectToWorld(float3(0,0,0)));
                output.positionCS = TransformWViewToHClip(pivotVS + float3(input.positionOS.xy * scale, 0));
                float4 originalCS = TransformObjectToHClip(input.positionOS);
                output.positionCS.z = originalCS.z / originalCS.w * output.positionCS.w;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // sprite RGB never contributes: even outlines and highlights become the same silhouette colour.
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.5h);
                return _SilhouetteColor;
            }
            ENDHLSL
        }
    }
}
