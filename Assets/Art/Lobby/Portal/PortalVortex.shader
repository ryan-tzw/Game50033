Shader "Game50033/Lobby/Portal Vortex"
{
    Properties
    {
        [HDR] _PrimaryColor("Primary Color", Color) = (0.26, 0.67, 1.6, 1)
        [HDR] _SecondaryColor("Secondary Color", Color) = (0.65, 0.2, 1.35, 1)
        _BackgroundColor("Background Tint (Alpha = Opacity)", Color) = (0.35, 0.28, 0.65, 0.87)
        _SwirlSpeed("Swirl Speed", Range(-4, 4)) = 0.85
        _SwirlStrength("Swirl Strength", Range(0, 18)) = 8.5
        _NoiseScale("Noise Scale", Range(1, 24)) = 5
        _NoiseSpeed("Noise Speed", Range(0, 4)) = 0.45
        _Emission("Emission Intensity", Range(0, 8)) = 2.4
        _EdgeGlowWidth("Edge Glow Width", Range(0.01, 0.4)) = 0.07
        _EdgeDistortion("Edge Distortion", Range(0, 0.12)) = 0.027
        _PulseSpeed("Pulse Speed", Range(0, 5)) = 1.3
        _PulseIntensity("Pulse Intensity", Range(0, 0.6)) = 0.12
        _Opacity("Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PrimaryColor, _SecondaryColor, _BackgroundColor;
                float _SwirlSpeed, _SwirlStrength, _NoiseScale, _NoiseSpeed;
                float _Emission, _EdgeGlowWidth, _EdgeDistortion, _PulseSpeed, _PulseIntensity, _Opacity;
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

            float Hash(float2 p)
            {
                float3 q = frac(p.xyx * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // reserve UV padding so even the strongest edge warp fits inside the quad.
                float2 p = (input.uv * 2 - 1) * 1.16;
                float radius = length(p);
                float angle = atan2(p.y, p.x + 0.00001);
                float time = _Time.y;
                float noiseTime = time * _NoiseSpeed;
                float broad = Noise(p * _NoiseScale + float2(0.31, -0.47) * noiseTime);
                float fine = Noise(p * (_NoiseScale * 2.25) + float2(-0.52, 0.27) * noiseTime);
                float energy = broad * 0.68 + fine * 0.32;
                float edge = radius + (broad * 2 - 1) * _EdgeDistortion;
                float boundary = 1 - smoothstep(0.965, 1.015, edge);

                // positive speed moves each logarithmic arm toward smaller radii as it rotates.
                float phase = angle * 3 - _SwirlStrength * log(max(radius, 0.045)) - time * _SwirlSpeed * 3;
                float wave = 0.5 + 0.5 * cos(phase + (energy - 0.5) * 1.6);
                // widen the inner turns and taper back to the original width toward the rim.
                float innerWidth = 0.26 * (1 - smoothstep(0.28, 0.70, radius));
                float arms = smoothstep(0.34 - innerWidth, 0.56 - innerWidth, wave)
                    * smoothstep(0.04, 0.16, radius);
                float inward = 1 - smoothstep(0.20, 1.0, edge);
                float bands = 0.25 + 0.35 * step(0.57 - innerWidth, wave) + 0.4 * step(0.83, wave);
                float core = 1 - smoothstep(0.06, 0.25, radius + (fine - 0.5) * 0.05);
                float rim = smoothstep(0.985 - _EdgeGlowWidth, 0.985, edge) * 0.13;
                float pulse = 1 + sin(time * _PulseSpeed) * _PulseIntensity;
                half3 tint = lerp(_SecondaryColor.rgb, _PrimaryColor.rgb, saturate(bands * 0.58 + inward * 0.48));
                float armOpacity = arms * lerp(0.34, 0.86, inward) * lerp(0.65, 1.0, energy);
                float foregroundOpacity = saturate(armOpacity + core * 0.9 + rim);
                half3 glow = tint * armOpacity * bands * lerp(0.47, 1.0, inward)
                    + _PrimaryColor.rgb * (core * 1.15 + rim * 0.28);
                float coverage = boundary * _Opacity;
                // composite a non-emissive fill behind the spiral so the gaps still show scenery.
                float backgroundOpacity = (1 - smoothstep(0.52, 1.0, edge)) * _BackgroundColor.a;
                float visibleBackground = backgroundOpacity * (1 - foregroundOpacity);
                half3 color = glow * (_Emission * pulse) + _BackgroundColor.rgb * visibleBackground;
                return half4(color, foregroundOpacity + visibleBackground) * coverage;
            }
            ENDHLSL
        }
    }
}
