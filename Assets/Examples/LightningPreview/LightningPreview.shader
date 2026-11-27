Shader "Game50033/Examples/LightningPreview"
{
    Properties
    {
        [HDR] _CoreColor("Hot Strand Colour", Color) = (0.68, 0.90, 1.28, 1)
        [HDR] _StrandColor("Blue Strand Colour", Color) = (0.12, 0.45, 1.12, 1)
        _Emission("Emission", Range(0.1, 6)) = 2.8
        _StrandWidth("Strand Width", Range(0.01, 0.13)) = 0.040
        _Displacement("Kink Size", Range(0, 1.5)) = 0.56
        _ChannelSpread("Channel Spread", Range(0, 1.5)) = 0.72
        _CycleRange("Cycle Range (XY Seconds)", Vector) = (0.035, 0.085, 0, 0)
        _LengthRange("Length Range (XY Fraction)", Vector) = (0.07, 0.40, 0, 0)
        _Throttle("Flash Chance", Range(0, 1)) = 0.83
        [Toggle(_INSTANT_FLASH)] _InstantFlash("Instant Flash", Float) = 0
        _FlashDurationRange("Flash Duration Range (XY Seconds)", Vector) = (0.025, 0.055, 0, 0)
        _Seed("Seed", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _INSTANT_FLASH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _StrandColor;
                float4 _CycleRange, _LengthRange, _FlashDurationRange;
                float _Emission, _StrandWidth, _Displacement, _ChannelSpread, _Throttle, _Seed;
            CBUFFER_END

            static const float SpanLength = 7.6;
            static const float JointCount = 24.0;

            struct Attributes
            {
                float2 ribbon : TEXCOORD0;
                float2 strand : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 emission : COLOR;
                half2 ribbon : TEXCOORD0;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(117.173, 39.417))) * 43758.54);
            }

            float3 StrandPoint(float t, float start, float length, float seed)
            {
                // each cycle has a different chain of straight segments.
                float sampleIndex = t * (3.0 + floor(length * 20.0));
                float joint = floor(sampleIndex);
                float2 previous = float2(Hash(float2(joint, seed)), Hash(float2(joint + 31.0, seed)));
                float2 next = float2(Hash(float2(joint + 1.0, seed)), Hash(float2(joint + 32.0, seed)));
                float2 bend = lerp(previous, next, frac(sampleIndex)) * 2.0 - 1.0;
                float2 channel = float2(Hash(float2(seed, 45.0)), Hash(float2(seed, 69.0))) * 2.0 - 1.0;
                float fraction = start + t * length;
                float envelope = sin(PI * fraction);
                float2 radial = (bend * _Displacement + channel * _ChannelSpread) * envelope;
                return float3((fraction - 0.5) * SpanLength, radial.x * 0.48, radial.y);
            }

            Varyings Vert(Attributes input)
            {
                // use the partial-span and independent-cycle approach of SpektrLightning.
                float strand = input.strand.x + _Seed * 41.23;
                float seconds = max(0.005, lerp(_CycleRange.x, _CycleRange.y, Hash(float2(strand, 20.0))));
                float cycleTime = _Time.y / seconds + Hash(float2(strand, 39.0));
                float age = frac(cycleTime);
                float seed = strand + fmod(floor(cycleTime), 1129.0) * 7.81;

                float span = clamp(lerp(_LengthRange.x, _LengthRange.y, Hash(float2(seed, 11.0))), 0.01, 0.98);
                float active = step(Hash(float2(seed, 7.0)), _Throttle);
                #if defined(_INSTANT_FLASH)
                    // sample placement from the cycle seed, so the entire arc stays put while visible.
                    float start = Hash(float2(seed, 57.0)) * (1.0 - span);
                    float duration = lerp(_FlashDurationRange.x, _FlashDurationRange.y, Hash(float2(seed, 81.0)));
                    active *= 1.0 - step(clamp(duration, 0.0, seconds), age * seconds);
                #else
                    float start = age * (1.0 - span);
                    active *= 1.0 - smoothstep(0.58, 0.84, age);
                #endif
                float t = input.ribbon.x;
                float joint = round(t * JointCount);
                float neighbor = joint > 0.0 ? joint - 1.0 : joint + 1.0;
                float3 p = StrandPoint(t, start, span, seed);
                float3 next = StrandPoint(neighbor / JointCount, start, span, seed);
                float3 view = TransformWorldToView(TransformObjectToWorld(p));
                float3 otherView = TransformWorldToView(TransformObjectToWorld(next));

                // a screen-facing ribbon keeps narrow strikes legible through the pixel pass.
                float2 tangent = normalize((view.xy - otherView.xy) * (joint > 0.0 ? 1.0 : -1.0));
                float2 crossLine = float2(-tangent.y, tangent.x);
                float width = _StrandWidth * lerp(0.48, 1.12, Hash(float2(seed, 27.0)));
                view.xy += crossLine * input.ribbon.y * width * 0.5;

                float brightness = lerp(0.28, 1.0, Hash(float2(seed, 91.0)));
                half3 colour = lerp(_StrandColor.rgb, _CoreColor.rgb, smoothstep(0.62, 0.92, brightness));
                Varyings output;
                output.positionCS = mul(UNITY_MATRIX_P, float4(view, 1.0));
                output.emission = colour * (_Emission * active * brightness);
                output.ribbon = input.ribbon;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = 1.0h - smoothstep(0.60h, 1.0h, abs(input.ribbon.y));
                half tip = smoothstep(0.0h, 0.06h, input.ribbon.x) * (1.0h - smoothstep(0.94h, 1.0h, input.ribbon.x));
                return half4(input.emission * edge * tip, 0);
            }
            ENDHLSL
        }
    }
}
