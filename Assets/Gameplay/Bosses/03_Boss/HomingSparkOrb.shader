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
        _FlashInterval("Orb Flash Interval", Range(0.06, 0.25)) = 0.11
        _FlashDurationRange("Orb Flash Duration Range (XY Seconds)", Vector) = (0.025, 0.055, 0, 0)
        _BranchIntensity("Outward Branch Intensity", Range(0, 1)) = 0.65
        _TrailWidth("Trail Bolt Width", Range(0.02, 0.3)) = 0.13
        _TrailJitter("Trail Jaggedness", Range(0, 1)) = 0.75
        _TrailBranchIntensity("Trail Branch Intensity", Range(0, 1)) = 0.62
        _TrailVisibility("Trail Visibility", Range(0, 3)) = 1.55
        [Toggle(_SPARK_TRAIL)] _Trail("Trail Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [HideInInspector] _SparkEnding("Spark Ending", Vector) = (0, 0, 0, 0)
        [HideInInspector] _StrikeHead("Strike Head", Float) = 0
        [HideInInspector] _TrailIntensity("Trail Intensity", Float) = 1
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
                float _FlashInterval;
                float4 _FlashDurationRange;
                float _TrailWidth, _TrailJitter, _TrailBranchIntensity, _TrailVisibility;
                float4 _SparkEnding;
                float _StrikeHead, _TrailIntensity;
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
            float ShellArcs(float3 direction, float flashTime, float width)
            {
                // sample fixed, partial arcs at different angles while each individual flash is on.
                float result = 0.0;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float clock = flashTime / max(0.02, _FlashInterval) + i * 0.41;
                    float seed = fmod(floor(clock), 1301.0) * 3.7 + i * 71.6;
                    float duration = lerp(_FlashDurationRange.x, _FlashDurationRange.y, Hash(seed + 14.8));
                    if (frac(clock) * _FlashInterval >= duration || Hash(seed + 0.8) < 0.20) continue;
                    float turn = Hash(seed + 5.1) * TWO_PI;
                    float sine, cosine;
                    sincos(turn, sine, cosine);
                    float3 p = float3(cosine * direction.x - sine * direction.z, direction.y,
                        sine * direction.x + cosine * direction.z);
                    float plane = dot(p, normalize(float3(Hash(seed + 2.0) - 0.5, 0.62 - i * 0.49, 0.46)));
                    float angularBend = (abs(frac(p.z * 3.0 + Hash(seed + 3.1)) - 0.5) * 2.0 - 0.5) * 0.36;
                    float window = smoothstep(0.02, 0.28, p.x);
                    result = max(result, ArcBand(plane + angularBend, width) * window);
                }
                return result;
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
                float time = _Time.y * _FlowSpeed + _SparkEnding.z * 0.67;
                #if defined(_SPARK_TRAIL)
                    float x = input.uv.x * 2.55;
                    float y = input.uv.y * 2.0 - 1.0;
                    if (_StrikeHead > 0.5)
                    {
                        // a hot tip stays readable between flickers in the rest of the bolt.
                        float u = saturate(input.uv.x);
                        float seed = floor(time * 8.0);
                        float segment = u * 3.0;
                        float cell = floor(segment);
                        float zigzag = lerp(Hash(seed + cell * 3.7), Hash(seed + (cell + 1.0) * 3.7), frac(segment)) - 0.5;
                        float bend = zigzag * _TrailJitter * (1.0 - smoothstep(0.72, 1.0, u));
                        float fromCore = y - bend;
                        float tip = smoothstep(0.0, 0.08, u) * (1.0 - smoothstep(0.95, 1.0, u));
                        float core = ArcBand(fromCore, _TrailWidth * lerp(1.0, 1.45, u));
                        float halo = ArcBand(fromCore, _TrailWidth * 1.90) * 0.46;
                        float energy = max(core, halo) * tip;
                        float alpha = energy * input.alpha * _ArcOpacity;
                        half3 light = _RimColor.rgb * 1.12 + _CoreColor.rgb * smoothstep(0.44, 0.90, core);
                        return half4(light * alpha, alpha);
                    }

                    float energy = 0.0;
                    float filamentCore = 0.0;
                    // the trail gradient already encodes age. thin older arcs without shrinking fresh ones.
                    float trailFade = saturate(input.alpha);
                    float taper = lerp(0.72, 1.0, smoothstep(0.0, 0.55, trailFade));
                    float width = _TrailWidth * taper;
                    [unroll]
                    for (int i = 0; i < 3; i++)
                    {
                        float lane = x + i * 0.37;
                        float cycle = time * (2.0 + i * 0.58) + i * 0.61;
                        float age = frac(cycle);
                        float seed = floor(lane) * 37.13 + floor(cycle) * 11.17 + i * 71.0;
                        float span = lerp(0.42, 0.80, Hash(seed + 6.0));
                        float u = (frac(lane) - age * (1.0 - span)) / span;
                        float segment = saturate(u) * 2.5;
                        float node = floor(segment);
                        float bend = lerp(Hash(seed + node * 3.7), Hash(seed + (node + 1.0) * 3.7), frac(segment)) - 0.5;
                        float crossLine = y - (i - 1.0) * _TrailBranchIntensity * 0.66 - bend * _TrailJitter;
                        float tip = smoothstep(0.0, 0.075, u) * (1.0 - smoothstep(0.91, 1.0, u));
                        float strength = tip * (1.0 - smoothstep(0.65, 1.0, age))
                            * lerp(0.60, 1.0, Hash(seed + 2.6)) * (i == 1 ? 1.0 : 0.64);
                        float core = ArcBand(crossLine, width * 1.15) * strength;
                        float halo = ArcBand(crossLine, width * 1.64) * strength * 0.42;
                        if (i == 1) filamentCore = core;
                        energy = max(energy, max(core, halo));
                    }
                    float residual = trailFade * lerp(0.55, 1.0, trailFade);
                    float alpha = saturate(energy * residual * _ArcOpacity * _TrailVisibility) * _TrailIntensity;
                    half3 light = _RimColor.rgb * 1.10
                        + _CoreColor.rgb * smoothstep(0.45, 0.92, filamentCore) * 0.70;
                    return half4(light * alpha, alpha);
                #else
                    // the mesh is the outer bound. Sample the core and electrical shells along the camera ray.
                    float3 rayStart = input.positionOS * 4.5;
                    float3 world = TransformObjectToWorld(input.positionOS);
                    float3 rayDir = -TransformWorldToObjectDir(GetWorldSpaceViewDir(world));
                    float3 rayCenter = rayStart - rayDir * dot(rayStart, rayDir);
                    float radiusSq = dot(rayCenter, rayCenter);
                    float flashTime = _Time.y + _SparkEnding.z * 0.071;

                    if (_SparkEnding.x > 0.5)
                    {
                        float progress = _SparkEnding.y;
                        float fade = 1.0 - progress;
                        float seed = _SparkEnding.z;
                        float distance = sqrt(radiusSq);
                        float coreRadius = fade * fade;
                        float flash = (1.0 - smoothstep(coreRadius * 0.62, coreRadius + 0.025, distance))
                            * fade;
                        float glow = (1.0 - smoothstep(coreRadius * 0.85, coreRadius * 1.36 + 0.07, distance))
                            * fade * 0.58;

                        // residual arcs dissipate as the core collapses.
                        float sparks = 0.0;
                        int sparkCount = 2 + (int)(Hash(seed + 2.9) * 3.0);
                        [unroll]
                        for (int i = 0; i < 5; i++)
                        {
                            if (i >= sparkCount) continue;
                            float delay = i == 0 ? 0.0 : Hash(i * 1.6 + seed) * 0.23;
                            if (progress < delay) continue;
                            float cycleTime = (progress - delay) / 0.47;
                            float cycle = floor(cycleTime);
                            float armSeed = seed + cycle * 19.3 + i * 3.4;
                            if (frac(cycleTime) >= lerp(0.27, 0.55, Hash(armSeed + 81.0))) continue;
                            float azimuth = (i + Hash(i * 3.3 + armSeed) * 0.65) * (TWO_PI / sparkCount) + seed;
                            float y = Hash(i + seed) * 1.2 - 0.6;
                            float radial = sqrt(1.0 - y * y);
                            float3 direction = float3(cos(azimuth) * radial, y, sin(azimuth) * radial);
                            float3 side = normalize(cross(direction, float3(0.0, 1.0, 0.0)));
                            float reach = 1.12 - cycle * 0.33;
                            reach *= lerp(0.55, 1.12, Hash(armSeed + 12.1));
                            float width = lerp(0.045, 0.085, Hash(armSeed + 30.7));
                            width *= lerp(0.55, 1.0, fade);
                            float kink = lerp(-0.28, 0.28, Hash(armSeed + 3.4));
                            float3 middle = direction * reach * 0.72 + side * reach * kink;
                            float3 tip = direction * reach - side * reach * 0.10;
                            float3 basePoint = direction * reach * (0.18 + cycle * 0.16);
                            float arc = max(Branch(rayStart, rayDir, basePoint, middle, width),
                                Branch(rayStart, rayDir, middle, tip, width * 0.80));
                            float3 fork = direction * reach * 0.91 + side * reach * 0.31;
                            float branch = Branch(rayStart, rayDir, middle, fork, width * 0.65) * step(0.3, Hash(i * 1.9 + seed));
                            sparks = max(sparks, max(arc, branch * 0.72) * lerp(0.5, 1.0, Hash(i * 4.3 + seed)));
                        }
                        sparks *= 0.75 * fade * _ArcOpacity;
                        float blue = max(glow, sparks);
                        float white = max(flash, sparks * 0.38);
                        return half4(_CoreColor.rgb * white + _RimColor.rgb * blue * (1.0 - white),
                            max(white, blue));
                    }

                    float shellEnergy = 0.0;
                    float branchEnergy = 0.0;
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

                    const float shellRadius = 1.34;
                    if (radiusSq < shellRadius * shellRadius)
                    {
                        float depth = sqrt(shellRadius * shellRadius - radiusSq);
                        float3 front = (rayCenter - rayDir * depth) / shellRadius;
                        // brighten the near arcs as they wrap past the core's silhouette.
                        float edge = smoothstep(0.48, 1.22, radiusSq);
                        shellEnergy = ShellArcs(front, flashTime, _ArcWidth * 1.22) * lerp(0.91, 1.23, edge);
                        if (radiusSq >= 1.0)
                        {
                            float3 back = (rayCenter + rayDir * depth) / shellRadius;
                            shellEnergy = max(shellEnergy, ShellArcs(back, flashTime, _ArcWidth * 1.22) * 0.76);
                        }
                    }
                    shellEnergy *= _ArcOpacity;

                    // occasionally split one of the surface strikes out past the main shell.
                    float clock = flashTime / max(0.02, _FlashInterval) + 0.31;
                    float burstFrame = fmod(floor(clock), 1301.0);
                    float burst = Hash(burstFrame + 2.7);
                    float duration = lerp(_FlashDurationRange.x, _FlashDurationRange.y, Hash(burstFrame + 6.7));
                    if (frac(clock) * _FlashInterval < duration && burst > 0.40 && _BranchIntensity > 0.0)
                    {
                        float azimuth = Hash(burstFrame + 6.1) * TWO_PI;
                        float y = Hash(burstFrame + 1.3) * 1.38 - 0.69;
                        float radial = sqrt(1.0 - y * y);
                        float3 direction = float3(cos(azimuth) * radial, y, sin(azimuth) * radial);
                        float3 side = normalize(cross(direction, float3(0.0, 1.0, 0.0)));
                        float3 a = direction * 0.98;
                        float3 b = direction * 1.48 + side * 0.22;
                        float3 c = direction * 2.06 - side * 0.32;
                        float3 fork = direction * 1.88 + side * 0.66;
                        if (dot(direction, rayDir) < 0.0 || radiusSq >= 1.0)
                        {
                            float branch = max(Branch(rayStart, rayDir, a, b, _ArcWidth * 0.95),
                                max(Branch(rayStart, rayDir, b, c, _ArcWidth * 0.87),
                                    Branch(rayStart, rayDir, b, fork, _ArcWidth * 0.76)));
                            branchEnergy = branch * _BranchIntensity * 0.85;
                        }
                    }

                    float energy = saturate(max(shellEnergy, branchEnergy));
                    half3 lightning = _RimColor.rgb * 1.10
                        + _CoreColor.rgb * smoothstep(0.62, 1.0, shellEnergy) * 0.60;
                    return half4(color * (1.0 - energy) + lightning * energy, max(alpha, energy));
                #endif
            }
            ENDHLSL
        }
    }
}
