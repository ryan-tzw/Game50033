Shader "Game50033/Bosses/HomingSparkOrb"
{
    Properties
    {
        [HDR] _CoreColor("Core Color", Color) = (1.5, 1.7, 2.0, 1)
        [HDR] _RimColor("Electrical Blue", Color) = (0.018, 0.28, 1.65, 1)
        _CoreTurbulence("Core Turbulence", Range(0, 1)) = 0.55
        _FlowSpeed("Flow Speed", Range(0, 8)) = 2.7
        _ArcWidth("Arc Width", Range(0.025, 0.22)) = 0.10
        _ArcOpacity("Arc Opacity", Range(0, 1)) = 0.85
        _BranchIntensity("Outward Branch Intensity", Range(0, 1)) = 0.65
        _TrailWidth("Trail Bolt Width", Range(0.02, 0.3)) = 0.13
        _TrailJitter("Trail Jaggedness", Range(0, 1)) = 0.75
        _TrailBranchIntensity("Trail Branch Intensity", Range(0, 1)) = 0.62
        [Toggle(_SPARK_TRAIL)] _Trail("Trail Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SPARK_TRAIL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _RimColor;
                float _CoreTurbulence, _FlowSpeed, _ArcWidth, _ArcOpacity, _BranchIntensity;
                float _TrailWidth, _TrailJitter, _TrailBranchIntensity;
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
                float3 positionOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half alpha : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionOS = input.positionOS;
                output.uv = input.uv;
                output.alpha = input.color.a;
                return output;
            }

            float Hash(float id)
            {
                return frac(sin(id * 43.73) * 43758.45);
            }

            float ArcBand(float distance, float width)
            {
                return 1.0 - smoothstep(width * 0.52, width, abs(distance));
            }

            #if !defined(_SPARK_TRAIL)
            float ShellArcs(float3 direction, float time, float seed, float width)
            {
                // three tilted bands wrap around the shell and follow its 3D curvature.
                float turn = time + seed;
                float sine, cosine;
                sincos(turn, sine, cosine);
                float3 p = float3(cosine * direction.x - sine * direction.z, direction.y,
                    sine * direction.x + cosine * direction.z);

                float3 planes = float3(dot(p, float3(0.25, 0.91, 0.33)), dot(p, float3(-0.66, 0.51, 0.55)),
                    dot(p, float3(0.85, -0.32, 0.41)));
                float3 wave = abs(frac(p.zxy * 4.5 + float3(0.31, 0.53, 0.67) * turn) - 0.5) * 2.0 - 0.5;
                planes += wave * 0.23 + 0.10 * sin(p.yzx * 20.0 - turn * 3.0);
                float3 windows = smoothstep(-0.43, 0.06, sin(p.xyz * 7.0 + turn * 1.8));
                float3 arcs = (1.0 - smoothstep(width * 0.52, width, abs(planes))) * windows;
                return max(arcs.x, max(arcs.y, arcs.z));
            }

            float Branch(float3 rayStart, float3 rayDir, float3 start, float3 end, float width)
            {
                // project each 3D branch segment perpendicular to the camera ray.
                float3 a = start - rayStart;
                float3 b = end - rayStart;
                a -= rayDir * dot(a, rayDir);
                b -= rayDir * dot(b, rayDir);
                float3 edge = b - a;
                float closest = saturate(-dot(a, edge) / max(dot(edge, edge), 0.0001));
                return ArcBand(length(a + edge * closest), width);
            }
            #endif

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                #if defined(_SPARK_TRAIL)
                    float x = input.uv.x * 7.0;
                    float y = input.uv.y * 2.0 - 1.0;
                    float cell = floor(x);
                    float phase = frac(x);
                    float frame = floor(time * 5.0);
                    float seed = Hash(cell + frame * 7.3);
                    float bend = lerp(Hash(cell + frame * 3.1), Hash(cell + 1.0 + frame * 3.1), phase) - 0.5;
                    float fromBolt = y - bend * _TrailJitter;
                    float streak = ArcBand(fromBolt, _TrailWidth);
                    float glow = ArcBand(fromBolt, _TrailWidth * 2.35) * 0.55;
                    float side = seed < 0.5 ? -1.0 : 1.0;
                    float fork = ArcBand(fromBolt - side * saturate((phase - 0.22) * 1.65) * 0.76, _TrailWidth * 0.68)
                        * step(0.22, phase) * (1.0 - smoothstep(0.66, 0.94, phase)) * _TrailBranchIntensity;
                    float alpha = max(glow, max(streak, fork)) * input.alpha * _ArcOpacity;
                    return half4(lerp(_RimColor.rgb, _CoreColor.rgb, max(streak, fork * 0.6)) * alpha, alpha);
                #else
                    // the mesh is the outer bound. Sample the core and electrical shells along the camera ray.
                    float3 rayStart = input.positionOS * 3.5;
                    float3 world = TransformObjectToWorld(input.positionOS);
                    float3 rayDir = -TransformWorldToObjectDir(GetWorldSpaceViewDir(world));
                    float3 rayCenter = rayStart - rayDir * dot(rayStart, rayDir);
                    float radiusSq = dot(rayCenter, rayCenter);
                    float energy = 0.0;
                    half3 color = 0;
                    float alpha = 0.0;

                    if (radiusSq < 1.0)
                    {
                        float3 surface = rayCenter - rayDir * sqrt(1.0 - radiusSq);
                        float noise = sin(dot(surface, float3(9, 14, 7)) + time * 2.8)
                            * sin(dot(surface, float3(-12, 7, 10)) - time * 2.2);
                        float core = 1.0 - smoothstep(0.18, 0.68, radiusSq + noise * _CoreTurbulence * 0.52);
                        color = lerp(_RimColor.rgb * 0.58, _CoreColor.rgb, core);
                        alpha = 1.0;
                    }

                    const float shellRadius = 1.19;
                    if (radiusSq < shellRadius * shellRadius)
                    {
                        float depth = sqrt(shellRadius * shellRadius - radiusSq);
                        float3 front = (rayCenter - rayDir * depth) / shellRadius;
                        energy = ShellArcs(front, time, 0.0, _ArcWidth);
                        if (radiusSq >= 1.0)
                        {
                            float3 back = (rayCenter + rayDir * depth) / shellRadius;
                            energy = max(energy, ShellArcs(back, time, 0.0, _ArcWidth) * 0.76);
                        }
                    }
                    energy *= _ArcOpacity;

                    // occasionally split one of the surface strikes out past the main shell.
                    float burstFrame = floor(time * 5.0);
                    float burst = Hash(burstFrame + 2.7);
                    if (burst > 0.40 && _BranchIntensity > 0.0)
                    {
                        float azimuth = Hash(burstFrame + 6.1) * TWO_PI;
                        float y = Hash(burstFrame + 1.3) * 1.38 - 0.69;
                        float radial = sqrt(1.0 - y * y);
                        float3 direction = float3(cos(azimuth) * radial, y, sin(azimuth) * radial);
                        float3 side = normalize(cross(direction, float3(0.0, 1.0, 0.0)));
                        float3 a = direction * 0.98;
                        float3 b = direction * 1.27 + side * 0.12;
                        float3 c = direction * 1.58 - side * 0.18;
                        float3 fork = direction * 1.41 + side * 0.36;
                        if (dot(direction, rayDir) < 0.0 || radiusSq >= 1.0)
                        {
                            float branch = max(Branch(rayStart, rayDir, a, b, _ArcWidth * 0.65),
                                max(Branch(rayStart, rayDir, b, c, _ArcWidth * 0.52),
                                    Branch(rayStart, rayDir, b, fork, _ArcWidth * 0.44)));
                            energy = max(energy, branch * _BranchIntensity);
                        }
                    }

                    half3 lightning = lerp(_RimColor.rgb, _CoreColor.rgb, energy * 0.62);
                    return half4(color * (1.0 - energy) + lightning * energy, max(alpha, energy));
                #endif
            }
            ENDHLSL
        }
    }
}
