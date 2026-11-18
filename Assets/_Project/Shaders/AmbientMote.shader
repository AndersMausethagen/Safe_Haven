// Life in the air between fights (AmbientLife, Docs/LookAndFeelResearch_2026-11-18.md V2, V4): dust motes, pollen and seed fluff,
// midges, dragonflies, moths, leaves and needles carried by a gust. A particle drawn from its own UV, no texture: Shape 0 a soft dot,
// 1 a pointed leaf, 2 a dart (a body with a pale blur of wings). Lit by the sun through its shadows and the sky, so a mote shines only
// where light reaches it (the forest's shafts) and glints by Glint; the particle's colour and alpha tint it.
Shader "Safe Haven/Ambient Mote"
{
    Properties
    {
        [Enum(Dot, 0, Leaf, 1, Dart, 2)] _Shape ("Shape", Float) = 0
        _Glint ("Glint in Sunlight", Range(0, 4)) = 1
        _Shade ("Light in Shadow", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Ambient Mote"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Shape;
                half _Glint;
                half _Shade;
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
                float3 positionWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Particles are drawn in world space (the system's simulation space): their vertices are where they are.
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                half shape;
                half wing = 0;
                if (_Shape < 0.5)
                {
                    shape = saturate(1.0 - dot(p, p));
                    shape *= shape;
                }
                else if (_Shape < 1.5)
                {
                    // Pointed at both ends along v, a little paler down its middle vein.
                    float edge = abs(p.x) - 0.55 * saturate(1.0 - p.y * p.y);
                    shape = (1.0 - smoothstep(-0.12, 0.0, edge)) * step(abs(p.y), 1.0);
                }
                else
                {
                    // A dragonfly from above: a slim body along v, its head a bead, and two pairs of long clear wings across it.
                    half body = (1.0 - smoothstep(0.05, 0.09, abs(p.x))) * (1.0 - smoothstep(0.88, 1.0, abs(p.y)));
                    half head = 1.0 - smoothstep(0.1, 0.15, length(p - float2(0, 0.62)));
                    half fore = 1.0 - smoothstep(0.75, 1.0, length(float2(p.x, (p.y - 0.36) / 0.17)));
                    half hind = 1.0 - smoothstep(0.75, 1.0, length(float2(p.x / 0.92, (p.y - 0.12) / 0.17)));
                    wing = saturate(max(fore, hind) - max(body, head));
                    shape = max(max(body, head), wing * 0.4);
                }

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lit = sun.shadowAttenuation;
                half3 light = sun.color * lerp(_Shade, 1.0, lit) + SampleSH(half3(0, 1, 0));
                // A mote in a shaft of sun shines: the glint rides on the light that reaches it, never on shade.
                half3 colour = input.color.rgb * light * (1.0 + _Glint * lit * (1.0 - min(_Shape, 1.0)));
                // Wings are clear: pale, with a glint of sun on them.
                colour = lerp(colour, light * (0.75 + 0.6 * lit), wing);
                colour = MixFog(colour, input.fog);
                return half4(colour, saturate(shape * input.color.a));
            }
            ENDHLSL
        }
    }
}
