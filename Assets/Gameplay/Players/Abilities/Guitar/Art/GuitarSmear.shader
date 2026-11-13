Shader "Game50033/Guitar Smear"
{
    Properties
    {
        _BodyColor("Body Color", Color) = (0.9, 0.78, 0.57, 1)
        _SweetSpotColor("Sweet Spot Color", Color) = (1, 0.94, 0.72, 1)
    }
    SubShader
    {
        // keep the effect in the dedicated low-resolution world-vfx pass.
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "GuitarSmear"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _SweetSpotColor;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 pose : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 pose : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.uv = input.uv;
                output.pose = input.pose;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float frame = input.pose.w;
                float radial = input.uv.y;
                float along = input.uv.x;
                // taper the ends with a stepped, streaky silhouette rather than soft alpha.
                float lane = floor(radial * 18.0);
                float stagger = frac(sin(lane * 127.1 + 31.7) * 43758.5453);
                // uv.x increases in swing order, including reverse swings.
                // the broken crescent sheds only from its trailing end; its leading cut stays flat.
                float tip = frame >= 1.5 ? along : min(along, 1.0 - along);
                float taper = (0.012 + stagger * 0.045) * (1.0 - radial);
                if (frame >= 1.5) taper += stagger * 0.13;
                clip(tip - taper);
                if (frame >= 2.5)
                {
                    // the final pose leaves three streaks near the swing endpoint.
                    clip(frac(radial * 3.0) - 0.57);
                    clip(along - (0.5 + stagger * 0.15));
                    clip(0.94 - along - stagger * 0.08);
                }
                float distanceFromOrigin = length(input.positionWS.xz - input.pose.xy);
                half3 color = distanceFromOrigin >= input.pose.z ? _SweetSpotColor.rgb : _BodyColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
