Shader "Game50033/Guitar Strum"
{
    Properties
    {
        _BodyColor("Body", Color) = (0.95, 0.82, 0.55, 1)
        _EdgeColor("Rim", Color) = (1, 0.96, 0.78, 1)
        _TailColor("Tail", Color) = (0.68, 0.45, 0.21, 1)
        _TailOpacity("Tail Opacity", Range(0, 1)) = 0.4
        _BreakupScale("Breakup Scale", Float) = 24
        _FlowSpeed("Flow Speed", Float) = 3
        _FadeSteps("Fade Steps", Float) = 4
        [PerRendererData] _Opacity("Opacity", Range(0, 1)) = 1
        [PerRendererData] _Age("Age", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor, _EdgeColor, _TailColor;
                float _TailOpacity, _BreakupScale, _FlowSpeed, _FadeSteps, _Opacity, _Age;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float along = saturate(input.uv.x);
                float radial = input.uv.y;
                float steps = max(1, _FadeSteps);
                half opacity = ceil(saturate(_Opacity) * steps) / steps;
                clip(opacity - 0.001);
                half3 color;
                if (radial < 1)
                {
                    // hard-edged lanes and moving chunks imply string harmonics without drawing full rings.
                    float lane = floor(along * max(1, _BreakupScale));
                    float stagger = Hash(float2(lane, 5));
                    clip(radial - (0.12 + stagger * 0.64));
                    float noise = Hash(float2(lane, floor(radial * 8 + _Age * _FlowSpeed)));
                    clip(noise + radial - 0.8);
                    color = noise > 0.82 ? _BodyColor.rgb : _TailColor.rgb;
                    opacity *= _TailOpacity;
                }
                else
                {
                    // keep the readable front intact, with two flat bands and a crisp bright rim.
                    color = radial > 1.75 ? _EdgeColor.rgb : _BodyColor.rgb;
                }
                return half4(color, opacity);
            }
            ENDHLSL
        }
    }
}
