// The arc drawn along a weapon swing (SlashEffect). The mesh is a ring sector: uv.x runs along the swing, uv.y from the
// inner to the outer edge. _Progress moves the bright head along the arc with a fading tail behind it; _Fade fades it
// all out. Additive, unlit, double sided.
Shader "Safe Haven/Slash"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.95, 0.85, 0.8)
        _Progress ("Progress", Range(0, 2)) = 1
        _Tail ("Tail", Range(0.05, 1)) = 0.6
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Slash"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Progress;
                half _Tail;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half behind = _Progress - input.uv.x;
                half along = saturate(1.0h - behind / _Tail) * step(0.0h, behind);
                // Soft inner and outer edges, brightest toward the outer edge where the blade tip travels.
                half radial = smoothstep(0.0h, 0.35h, input.uv.y) * smoothstep(1.0h, 0.78h, input.uv.y);
                half edge = 0.35h + 0.65h * smoothstep(0.5h, 0.92h, input.uv.y);
                half alpha = along * along * radial * edge * _Fade * _Color.a;
                return half4(_Color.rgb * (1.0h + along), alpha);
            }
            ENDHLSL
        }
    }
}
