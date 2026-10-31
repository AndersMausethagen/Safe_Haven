// Keeps "Safe Haven/Water" out of a boat: an invisible cap across the top of the hull that only writes depth. It draws after
// the opaque hull and its load, so they stay as they are, and before the water, which then fails its depth test everywhere
// the cap covers. Without it the flat water plane shows through the floor of any hull sitting below the waterline.
Shader "Safe Haven/Water Mask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+50" }

        Pass
        {
            Name "Mask"
            ColorMask 0
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            float4 Vertex(Attributes input) : SV_POSITION
            {
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half4 Fragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
