Shader "Game50033/Lightning"
{
    Properties
    {
        [HDR] _CoreColor("Core Color", Color) = (0.9, 0.99, 1.25, 1)
        [HDR] _GlowColor("Glow Color", Color) = (0.025, 0.48, 1.6, 1)
        _TelegraphColor("Telegraph Color", Color) = (1, 0.16, 0.19, 0.8)
        _Thickness("Core Thickness", Range(0.05, 0.8)) = 0.28
        _GlowIntensity("Glow Intensity", Range(0, 4)) = 1.3
        _Jaggedness("Jaggedness", Range(0, 0.5)) = 0.17
        _NoiseScale("Noise Scale", Range(1, 32)) = 8
        _FlickerFrequency("Flicker Frequency", Range(0, 30)) = 18
        _Distortion("Distortion", Range(0, 0.5)) = 0.07
        _Opacity("Opacity", Range(0, 1)) = 1
        _AnimationSpeed("Animation Speed", Range(-8, 8)) = 1.6
        _Telegraph("Telegraph", Range(0, 1)) = 0
        [HideInInspector] _Fade("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _GlowColor, _TelegraphColor;
                float _Thickness, _GlowIntensity, _Jaggedness, _NoiseScale, _FlickerFrequency;
                float _Distortion, _Opacity, _AnimationSpeed, _Telegraph, _Fade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            half4 Frag(Varyings input) : SV_Target
            {
                // longitudinal UV can be distance-tiled on beams or mapped around procedural meshes.
                float x = input.uv.x;
                float y = input.uv.y * 2 - 1;
                float time = _Time.y;
                float frame = floor(time * _FlickerFrequency);
                float pattern = x * _NoiseScale - time * _AnimationSpeed;
                float cell = floor(pattern);
                float jagged = lerp(Hash(cell + frame * 7), Hash(cell + 1 + frame * 7), frac(pattern)) * 2 - 1;
                float offset = jagged * _Jaggedness + sin(x * 18 - time * _AnimationSpeed * 2) * _Distortion;
                float distance = abs(y - offset);
                float core = 1 - smoothstep(_Thickness * 0.55, _Thickness, distance);
                float band = 1 - smoothstep(0.42, 0.84, distance);
                float edge = 1 - smoothstep(0.68, 1, abs(y));
                float flicker = lerp(0.82, 1, Hash(frame + 0.31));
                half3 bright = _CoreColor.rgb * core + _GlowColor.rgb * band * _GlowIntensity * flicker;

                // the warning uses the full lane continuously, including its corners.
                float warningBorder = smoothstep(0.55, 0.74, abs(y));
                float warningAlpha = _TelegraphColor.a * lerp(0.52, 1, warningBorder);
                half3 warning = _TelegraphColor.rgb * warningAlpha;
                float alpha = edge * input.color.a * _Opacity * _Fade;
                return half4(lerp(bright, warning, _Telegraph) * input.color.rgb * alpha,
                    lerp(max(core, band * 0.5), warningAlpha, _Telegraph) * alpha);
            }
            ENDHLSL
        }
    }
}
