// Safe Haven: heat shimmering over a fire (LightFlicker adds it over campfires, bonfires and torches). A camera-facing
// quad that redraws the scene behind it (the opaque texture) through a slow upward-rolling ripple, strongest just
// above the flames and fading out at the quad's edges and top. Drawn before the flames, so they stay sharp. Kept faint:
// the user found it far too strong (2026-11-08), and the opaque texture is half resolution, so a full blend blurs.
Shader "Safe Haven/Heat Haze"
{
    Properties
    {
        _Strength ("Strength", Range(0, 0.05)) = 0.004
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _Speed ("Rise Speed", Range(0, 4)) = 1.4
        _Scale ("Ripple Size", Range(1, 40)) = 14
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Haze"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Opacity;
                float _Speed;
                float _Scale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float seed : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                // Turned to the camera round the object's own origin, kept upright.
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), 1);
                float3 right = normalize(UNITY_MATRIX_V[0].xyz);
                float3 positionWS = centre + right * input.positionOS.x * scale.x + float3(0, 1, 0) * input.positionOS.y * scale.y;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.seed = frac(dot(centre, float3(0.13, 0.71, 0.37)));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed + input.seed * 10.0;
                // Two ripples rolling up at different rates, crossing, so the shimmer never repeats plainly.
                float2 ripple = float2(sin(uv.y * _Scale - t * 3.0 + sin(uv.x * 9.0 + t)) ,
                                       cos(uv.y * _Scale * 0.7 - t * 2.3 + uv.x * 6.0)) * 0.5;
                // Strongest low and in the middle, fading up and out to the sides.
                float across = 1.0 - saturate(abs(uv.x - 0.5) * 2.0);
                float mask = smoothstep(0.0, 0.35, across) * smoothstep(0.0, 0.15, uv.y) * (1.0 - smoothstep(0.4, 1.0, uv.y));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 scene = SampleSceneColor(screenUV + ripple * _Strength * mask);
                // The scene copy is half the screen's resolution: blended in fully it reads as a blur over the fire, so
                // only part of it shows, enough to carry the ripple.
                return half4(scene, mask * _Opacity);
            }
            ENDHLSL
        }
    }
}
