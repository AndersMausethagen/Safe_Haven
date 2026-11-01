Shader "Safe Haven/Interaction Outline"
{
    Properties
    {
        _OutlineColor ("Colour", Color) = (1, 0.72, 0.25, 1)
        _OutlinePixels ("Width (pixels)", Range(1, 6)) = 3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-1" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            Stencil
            {
                Ref 1
                ReadMask 1
                Comp NotEqual
                Pass Keep
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
                float2 direction = normalVS.xy * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11);
                float lengthSquared = dot(direction, direction);
                direction *= rsqrt(max(lengthSquared, 0.00001));
                output.positionCS.xy += direction * (2.0 * _OutlinePixels / _ScaledScreenParams.xy) * output.positionCS.w;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
