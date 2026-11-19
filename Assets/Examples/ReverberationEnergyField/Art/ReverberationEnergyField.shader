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
        _EdgeDistortionStrength("Edge Distortion Strength", Range(0, 0.35)) = 0
        _EdgeDistortionSpeed("Edge Distortion Speed", Float) = 0.3
        [HideInInspector] _InwardEdgeDistortion("Inward Edge Distortion", Float) = 0
        _BackgroundColor("Background Color", Color) = (0.12, 0.035, 0.22, 1)
        _BackgroundLayerOpacity("Background Layer Opacity", Range(0, 1)) = 0
        _BackgroundVariation("Background Variation", Range(0, 1)) = 0.5
        _MidgroundLayerOpacity("Midground Layer Opacity", Range(0, 1)) = 0
        _MidgroundWidth("Midground Width", Range(0.01, 1)) = 0.4
        _FieldShapeKind("Field Shape Kind", Float) = 0
        _ShapeOriginDirection("Shape Origin And Direction", Vector) = (0, 0, 0, 1)
        _ShapeGeometry("Inner Radius, Outer Radius, Half Angle", Vector) = (0, 1, 3.14159265, 0)
        _ShapeStretch("Stretch Axis, Factor, Inverse", Vector) = (0, 1, 1, 1)
        _Layer("Layer", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EnergyColor, _BackgroundColor;
                float _BackgroundLayerOpacity, _BackgroundVariation, _MidgroundLayerOpacity, _MidgroundWidth;
                float _Density, _LayerOpacity, _Emission, _BrightnessBands;
                float _EdgeDistortionStrength, _EdgeDistortionSpeed, _InwardEdgeDistortion;
                float _FieldShapeKind;
                float4 _ShapeOriginDirection, _ShapeGeometry, _ShapeStretch;
                float4x4 _FieldWorldToLocal;
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

            float2 FieldWarp(float3 positionWS, out float2 p)
            {
                float time = _Time.y * _FlowSpeed;
                float angle = _Layer * 1.73 + 0.4;
                float2 direction = float2(cos(angle), sin(angle));
                p = positionWS.xz * _NoiseScale + float2(17.17, 31.13) * _Layer;
                p += direction * time * (0.85 + 0.07 * _Layer);

                // two broad warp channels and one contour sample, with no octave loop or texture dependencies.
                float2 warp = float2(Noise(p * 0.63 + time * float2(0.11, -0.08)),
                    Noise(p * 0.63 + float2(9.2, 14.7) - time * float2(0.06, 0.09)));
                return warp;
            }

            float ProceduralDistance(float2 offset)
            {
                // undo Guitar's fixed-axis stretch before radial/angular tests, rather than stretching along aim.
                offset -= _ShapeStretch.xy * (dot(offset, _ShapeStretch.xy) * (1 - _ShapeStretch.w));
                float radius = length(offset);
                if (_FieldShapeKind < 1.5) return radius - _ShapeGeometry.y;

                float inner = _ShapeGeometry.x;
                float outer = _ShapeGeometry.y;
                float halfAngle = _ShapeGeometry.z;
                if (halfAngle >= 3.141592 - 0.000001)
                    return inner > 0 ? max(radius - outer, inner - radius) : radius - outer;

                float2 direction = _ShapeOriginDirection.zw;
                float2 q = float2(abs(dot(offset, float2(direction.y, -direction.x))), dot(offset, direction));
                float2 edge = float2(sin(halfAngle), cos(halfAngle));
                bool insideAngle = q.y >= radius * edge.y;
                // distance to the outer arc, optional inner arc and closest radial segment.
                // reflecting X lets one segment handle both sides, including sectors larger than 180 degrees.
                float distance = insideAngle ? abs(radius - outer) : length(q - edge * outer);
                if (inner > 0)
                    distance = min(distance, insideAngle ? abs(radius - inner) : length(q - edge * inner));
                distance = min(distance, length(q - edge * clamp(dot(q, edge), inner, outer)));
                bool inside = halfAngle > 0 && insideAngle && radius >= inner && radius <= outer;
                return inside ? -distance : distance;
            }

            float FieldEnvelope(float2 uv, float2 warp, float3 positionWS)
            {
                if (_FieldShapeKind > 0.5)
                {
                    float2 local = mul(_FieldWorldToLocal, float4(positionWS, 1)).xz - _ShapeOriginDirection.xy;
                    float distance = ProceduralDistance(local);
                    float radius = _ShapeGeometry.y;
                    if (_EdgeDistortionStrength > 0)
                    {
                        // shared root-local noise affects curved, angular and inner boundaries alike.
                        float edgeTime = _Time.y * _EdgeDistortionSpeed;
                        float edgeNoise = Noise(local / radius * 1.6 + float2(5.2, 9.7) + edgeTime * float2(0.37, -0.29));
                        // positive distance moves the visual inside the original footprint.
                        float distortion = _InwardEdgeDistortion > 0.5 ? edgeNoise : edgeNoise * 2 - 1;
                        distance += distortion * _EdgeDistortionStrength * radius;
                    }
                    // soften the warped contour from the inside; padded sheets cover outward test-scene motion.
                    return 1 - smoothstep(-_EdgeFalloff * radius, 0, distance);
                }
                // fade through the interior before reaching a quad edge, without drawing a hollow rim.
                float2 edgeUV = uv * 2 - 1;
                float radial = length(edgeUV) + (warp.y - 0.5) * 0.16;
                if (_EdgeDistortionStrength > 0)
                {
                    // shared uv phase keeps neighbouring sheets coherent; the new clock does not use Flow Speed.
                    float edgeTime = _Time.y * _EdgeDistortionSpeed;
                    float edgeNoise = Noise(edgeUV * 1.6 + float2(5.2, 9.7) + edgeTime * float2(0.37, -0.29));
                    // a slight inward bias leaves room for localized expansion without enlarging the mesh.
                    radial += (edgeNoise - 0.25) * _EdgeDistortionStrength;
                }
                float envelope = 1 - smoothstep(1 - _EdgeFalloff, 1, radial);
                if (_EdgeDistortionStrength > 0)
                {
                    // fade out before the square mesh edge even at peak outward displacement; disabled is exactly legacy.
                    envelope *= 1 - smoothstep(0.9, 1, max(abs(edgeUV.x), abs(edgeUV.y)));
                }
                return envelope;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p;
                float2 warp = FieldWarp(input.positionWS, p);
                float field = Noise(p + (warp - 0.5) * _Turbulence * 2);
                float ridge = 1 - abs(field * 2 - 1);
                float width = clamp(_Density * 0.1, 0.015, 0.35);
                float currents = smoothstep(1 - width * 2, 1 - width * 0.25, ridge);
                float concentration = smoothstep(0.12, 0.8, warp.x * 0.6 + warp.y * 0.4);
                float energy = (currents * 0.9 + 0.08) * concentration;

                float envelope = FieldEnvelope(input.uv, warp, input.positionWS);
                float alpha = saturate(energy * _LayerOpacity * _EnergyColor.a) * envelope;

                float bands = max(2, _BrightnessBands) - 1;
                float brightness = floor(saturate(currents) * bands + 0.5) / bands;
                half3 color = _EnergyColor.rgb * (0.28 + brightness * 0.85) * _Emission;

                // reuse the broad warp for a diffuse base and the contour for softer connecting concentrations.
                // background colour stays independent of foreground emission, keeping high-emission currents distinct.
                float ambient = lerp(1, 0.35 + 0.65 * (warp.x * 0.6 + warp.y * 0.4), _BackgroundVariation);
                float backgroundAlpha = saturate(_BackgroundLayerOpacity * _BackgroundColor.a * ambient) * envelope;
                half3 backgroundColor = _BackgroundColor.rgb * lerp(1, 0.7 + 0.3 * warp.y, _BackgroundVariation);
                float broadCurrents = smoothstep(max(0, 1 - width * 2 - _MidgroundWidth), 1 - width * 0.25, ridge);
                float midgroundAlpha = saturate(broadCurrents * concentration * _MidgroundLayerOpacity * _EnergyColor.a) * envelope;
                half3 midgroundColor = _EnergyColor.rgb * 0.48 * _Emission;

                // composite background, midground, then the unchanged foreground within this single draw.
                // convert back to straight alpha for the existing SrcAlpha blend; zero new opacities restores the old result.
                float underAlpha = midgroundAlpha + backgroundAlpha * (1 - midgroundAlpha);
                half3 underColor = midgroundColor * midgroundAlpha + backgroundColor * backgroundAlpha * (1 - midgroundAlpha);
                float combinedAlpha = alpha + underAlpha * (1 - alpha);
                half3 combinedColor = (color * alpha + underColor * (1 - alpha)) / max(combinedAlpha, 0.000001);
                clip(combinedAlpha - 0.001);
                return half4(combinedColor, combinedAlpha);
            }

            half4 MaskFrag(Varyings input) : SV_Target
            {
                // outline the continuous field envelope, not each luminous current or transparency gap.
                // max blending unions sheets and separate fields without adding opacity at overlaps.
                float visible = max(_LayerOpacity * _EnergyColor.a,
                    max(_MidgroundLayerOpacity * _EnergyColor.a, _BackgroundLayerOpacity * _BackgroundColor.a));
                clip(visible - 0.000001);
                float2 p;
                float2 warp = FieldWarp(input.positionWS, p);
                return FieldEnvelope(input.uv, warp, input.positionWS).xxxx;
            }
        ENDHLSL

        Pass
        {
            Name "Energy"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "FieldBoundaryMask"
            Tags { "LightMode" = "PixelWorldOutline" }
            Blend One One
            BlendOp Max
            ColorMask R
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment MaskFrag
            ENDHLSL
        }
    }
}
