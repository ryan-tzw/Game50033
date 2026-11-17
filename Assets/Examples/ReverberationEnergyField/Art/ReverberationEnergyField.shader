Shader "Game50033/Experiments/Reverberation Energy Field"
{
    Properties
    {
        _EnergyColor("Energy Color", Color) = (0.62, 0.22, 0.95, 1)
        _Density("Density", Float) = 1
        _LayerOpacity("Layer Opacity", Range(0, 1)) = 0.23
        _Emission("Emission", Float) = 1.15
        _BrightnessBands("Brightness Bands", Float) = 3
        _NoiseScale("Noise Scale", Float) = 1.3
        _FlowSpeed("Flow Speed", Float) = 0.12
        _Turbulence("Turbulence", Float) = 0.8
        _EdgeFalloff("Edge Falloff", Range(0, 1)) = 0.55
        _Layer("Layer", Float) = 0
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EnergyColor;
                float _Density, _LayerOpacity, _Emission, _BrightnessBands;
                float _NoiseScale, _FlowSpeed, _Turbulence, _EdgeFalloff, _Layer;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                float3 h = frac(float3(p.xyx) * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
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
                float time = _Time.y * _FlowSpeed;
                float angle = _Layer * 1.73 + 0.4;
                float2 direction = float2(cos(angle), sin(angle));
                float2 p = input.positionWS.xz * _NoiseScale + float2(17.17, 31.13) * _Layer;
                p += direction * time * (0.85 + 0.07 * _Layer);

                // two broad warp channels and one contour sample, with no octave loop or texture dependencies.
                float2 warp = float2(Noise(p * 0.63 + time * float2(0.11, -0.08)),
                    Noise(p * 0.63 + float2(9.2, 14.7) - time * float2(0.06, 0.09)));
                float field = Noise(p + (warp - 0.5) * _Turbulence * 2);
                float ridge = 1 - abs(field * 2 - 1);
                float width = clamp(_Density * 0.1, 0.015, 0.35);
                float currents = smoothstep(1 - width * 2, 1 - width * 0.25, ridge);
                float concentration = smoothstep(0.12, 0.8, warp.x * 0.6 + warp.y * 0.4);
                float energy = (currents * 0.9 + 0.08) * concentration;

                // fade through the interior before reaching a quad edge, without drawing a hollow rim.
                float radial = length(input.uv * 2 - 1) + (warp.y - 0.5) * 0.16;
                float envelope = 1 - smoothstep(1 - _EdgeFalloff, 1, radial);
                float alpha = saturate(energy * _LayerOpacity * _EnergyColor.a) * envelope;
                clip(alpha - 0.001);
                float bands = max(2, _BrightnessBands) - 1;
                float brightness = floor(saturate(currents) * bands + 0.5) / bands;
                half3 color = _EnergyColor.rgb * (0.28 + brightness * 0.85) * _Emission;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
