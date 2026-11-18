Shader "Game50033/Guitar Smash"
{
    Properties
    {
        _BodyColor("Area", Color) = (0.9, 0.78, 0.57, 1)
        _SweetSpotColor("Sweet Spot", Color) = (1, 0.94, 0.72, 1)
        _SweetRatio("Sweet Spot Radius Ratio", Float) = 0.333333
        _IndicatorOpacity("Indicator Opacity", Float) = 0.3
        _Impact("Impact", Float) = 0
        _Progress("Impact Progress", Float) = 0
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
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor, _SweetSpotColor;
                float _SweetRatio, _IndicatorOpacity, _Impact, _Progress;
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
                float radial = length(input.uv * 2 - 1);
                clip(1 - radial);
                float sweet = saturate(_SweetRatio);
                half3 color = radial <= sweet ? _SweetSpotColor.rgb : _BodyColor.rgb;
                half opacity;
                if (_Impact < 0.5)
                {
                    // two crisp boundaries communicate exactly the gameplay radii, without a smooth gradient.
                    bool rim = radial > 0.96 || abs(radial - sweet) < 0.025;
                    opacity = rim ? saturate(_IndicatorOpacity + 0.4) : _IndicatorOpacity * (radial <= sweet ? 1.0 : 0.45);
                }
                else
                {
                    // hold four toon poses: filled impact, opening centre, broad ring, thin remaining ring.
                    float frame = min(3, floor(saturate(_Progress) * 4));
                    clip(radial - frame * 0.25 + 0.001);
                    opacity = (4 - frame) / 4;
                }
                return half4(color, opacity);
            }
            ENDHLSL
        }
    }
}
