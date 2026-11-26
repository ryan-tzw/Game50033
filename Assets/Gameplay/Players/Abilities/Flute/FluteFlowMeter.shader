Shader "Game50033/Flute/Flow Meter"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
        [PerRendererData] _MeterCenter("Meter Center", Vector) = (0, 0, 0, 0)
        [PerRendererData] _Opacity("Opacity", Range(0, 1)) = 1
        [PerRendererData] _Dashed("Dashed", Float) = 0
        [PerRendererData] _DashCount("Dash Count", Float) = 24
        [PerRendererData] _DashCoverage("Dash Coverage", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            Name "FlowMeter"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MeterCenter;
                float _Opacity;
                float _Dashed;
                float _DashCount;
                float _DashCoverage;
                float _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_Dashed > 0.5)
                {
                    // Angular phase keeps dashes fixed as the fill grows or the player moves.
                    float2 offset = input.positionWS.xz - _MeterCenter.xz;
                    float turns = atan2(offset.x, offset.y) * 0.159154943 + 1.0;
                    clip(_DashCoverage - frac(turns * _DashCount));
                }
                return half4(input.color.rgb, input.color.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
