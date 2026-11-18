Shader "Game50033/Dodge Flash"
{
    Properties
    {
        _FlashColor("Flash", Color) = (1,1,1,1)
        _FlashPose("Pose", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FlashColor;
                float _FlashPose;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                // test depth on the flash's horizontal plane so upward/downward artwork cannot intersect the floor.
                float3 pivotWS = TransformObjectToWorld(float3(0,0,0));
                float3 rayWS = unity_OrthoParams.w > 0.5 ? TransformViewToWorldDir(float3(0,0,1)) : positionWS - GetCameraPositionWS();
                if (abs(rayWS.y) > 0.001)
                {
                    float4 depthCS = TransformWorldToHClip(positionWS + rayWS * ((pivotWS.y - positionWS.y) / rayWS.y));
                    output.positionCS.z = depthCS.z / depthCS.w * output.positionCS.w;
                }
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float pose = clamp(floor(_FlashPose), 0, 3);
                float span = pose < 0.5 ? 0.65 : 1.0;
                float width = pose < 0.5 ? 1.0 : pose < 1.5 ? 0.65 : pose < 2.5 ? 0.4 : 0.18;
                float taper = saturate(1 - abs(p.x / span));
                float edge = abs(p.y) / max(0.001, taper * width);
                float mask = step(edge, 1);
                // the initial broad flash becomes straight broken speed streaks, never a sweeping attack arc.
                if (pose >= 2)
                {
                    float gap = step(0.42, frac((p.x + 1) * 3));
                    mask *= p.x < 0.15 ? gap : 1;
                    float rails = step(abs(abs(p.y) - width * 0.7), 0.045) * step(abs(p.x), 0.85);
                    mask = max(mask, rails * gap);
                }
                clip(mask - 0.5);
                half shade = edge < 0.35 ? 1.0h : edge < 0.75 ? 0.85h : 0.65h;
                return half4(_FlashColor.rgb * shade, _FlashColor.a);
            }
            ENDHLSL
        }
    }
}
