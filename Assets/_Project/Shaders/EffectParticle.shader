// Unlit particles for combat effects (CombatEffectsBuilder): texture x vertex colour x tint, additive by default. Set
// the blend to SrcAlpha / OneMinusSrcAlpha for dust and smoke. Soft: particles fade out where they meet what is behind them
// (the ground, a wall) over Soft Distance, so smoke and dust never show the hard line of their quad cutting into it.
Shader "Safe Haven/Effect Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
        _SoftDistance ("Soft Distance (m)", Range(0, 2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Effect"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _SrcBlend;
                half _DstBlend;
                float _SoftDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _TintColor;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 colour = texel * input.color;
                if (_SoftDistance > 0.001)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float behind = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - input.positionCS.w;
                    colour.a *= saturate(behind / _SoftDistance);
                }

                return colour;
            }
            ENDHLSL
        }
    }
}
