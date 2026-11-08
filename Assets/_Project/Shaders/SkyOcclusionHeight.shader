// Safe Haven: what the sky-occlusion camera draws everything with (SkyOcclusionMap, through the Sky Occlusion Renderer's
// Render Objects override): the world height of the topmost surface, so the map holds how high things stand above each
// spot. Leaves are drawn whole (no alpha test): a canopy counts as cover.
Shader "Hidden/Safe Haven/Sky Occlusion Height"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Height"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float height : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.height = positionWS.y;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(input.height, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
