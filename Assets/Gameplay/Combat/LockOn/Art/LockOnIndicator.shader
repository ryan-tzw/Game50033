Shader "Game50033/Lock-On Indicator"
{
    Properties
    {
        _Radius("Radius", Float) = 0.7
        _Thickness("Thickness", Float) = 0.04
        _PlayerCount("Players", Float) = 1
        _ArcsPerPlayer("Arcs Per Player", Float) = 4
        _Coverage("Arc Coverage", Range(0, 1)) = 0.6
        _Rotation("Rotation", Float) = 0
        _Opacity("Opacity", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PlayerColors[4];
                float4 _PlayerScales, _PlayerFades;
                float _Radius, _Thickness, _PlayerCount, _ArcsPerPlayer, _Coverage, _Rotation, _Opacity;
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
            half4 Frag(Varyings input) : SV_Target
            {
                float2 radialPosition = input.uv * 2 - 1;
                float count = clamp(round(_PlayerCount), 1, 4);
                float angle = frac((atan2(radialPosition.y, radialPosition.x) - _Rotation) / TWO_PI + 1);
                float sector = angle * count * max(1, _ArcsPerPlayer);
                int player = (int)fmod(floor(sector), count);
                float scale = max(0.001, _PlayerScales[player]);
                // each slot contributes one hard-edged arc; no spatial gradients or bloom.
                clip(_Coverage * 0.5 - abs(frac(sector) - 0.5));
                clip(_Thickness * scale * 0.5 - abs(length(radialPosition) - _Radius * scale));
                half4 color = _PlayerColors[player];
                color.a *= saturate(_Opacity) * _PlayerFades[player];
                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }
}
