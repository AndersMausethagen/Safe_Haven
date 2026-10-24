// Waterfall mist, spray and splash particles: soft alpha-blended billboards lit by the sun and the ambient light,
// so they darken at night, and faded where they meet the ground (soft particles, from the camera depth texture).
Shader "Safe Haven/Water Spray"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture (alpha = shape)", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (0.9, 0.94, 0.95, 1)
        _Softness ("Soft Edge Where It Meets Things (metres)", Range(0, 2)) = 0.4
        _Backlight ("Glow Towards the Sun", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ SAFE_HAVEN_WATER_SIMPLE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Softness, _Backlight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                #if !defined(SAFE_HAVEN_WATER_SIMPLE)
                    if (_Softness > 0)
                    {
                        float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                        float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                        float self = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());
                        colour.a *= saturate((scene - self) / _Softness);
                    }
                #endif
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half back = pow(saturate(dot(-view, sun.direction)), 4.0) * _Backlight;
                half3 light = sun.color * lerp(0.5, 1.0, sun.shadowAttenuation) * (0.55 + back) + SampleSH(half3(0, 1, 0));
                colour.rgb *= light;
                colour.rgb = MixFog(colour.rgb, input.fog);
                return colour;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
