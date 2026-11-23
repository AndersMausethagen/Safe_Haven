// The portal's living surface (the user, 2026-11-23: "change that purple color in the portal with something with more animation ...
// more green-blue with a whirly and magical feel, with a faint glow. not too strong, but subtle"). Two layers of soft noise wound into
// a slow whirl that turn against each other round the middle, deep teal at the rim lightening to pale sea-green where they meet, a
// gentle breath of brightness, and soft edges so it melts into the stone arch. Unlit and see-through (premultiplied), on both faces.
Shader "Safe Haven/Portal Swirl"
{
    Properties
    {
        [HDR] _DeepColor ("Deep", Color) = (0.02, 0.11, 0.13, 1)
        [HDR] _MidColor ("Middle", Color) = (0.05, 0.42, 0.45, 1)
        [HDR] _BrightColor ("Bright", Color) = (0.45, 1.0, 0.78, 1)
        _Glow ("Glow", Range(0, 4)) = 1.25
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _SwirlSpeed ("Swirl Speed (turns a second)", Range(0, 1)) = 0.06
        _Twist ("Twist", Range(0, 12)) = 4.5
        _NoiseScale ("Noise Scale", Range(0.5, 10)) = 3.2
        _Aspect ("Width over Height", Range(0.2, 2)) = 0.635
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.14
        _Breath ("Breath (brightness swell)", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PortalSwirl"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _MidColor;
                half4 _BrightColor;
                half _Glow;
                half _Opacity;
                half _SwirlSpeed;
                half _Twist;
                half _NoiseScale;
                half _Aspect;
                half _EdgeSoftness;
                half _Breath;
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
                float fog : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int o = 0; o < 4; o++)
                {
                    sum += Noise(p) * amplitude;
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }

                return sum;
            }

            // A point wound round the middle: the nearer the middle, the further it has turned.
            float2 Wound(float2 p, float r, float turn)
            {
                float angle = atan2(p.y, p.x) + _Twist * (1.0 - saturate(r)) + turn;
                return float2(cos(angle), sin(angle)) * r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 p = (input.uv - 0.5) * float2(_Aspect, 1.0) * 2.0;
                float r = length(p);
                float turn = t * _SwirlSpeed * 6.2831853;
                float a = Fbm(Wound(p, r, turn) * _NoiseScale + float2(0.0, t * 0.05));
                float b = Fbm(Wound(p, r, -turn * 0.7) * (_NoiseScale * 1.7) + float2(t * 0.04, 3.7));
                float pattern = saturate(a * 0.65 + b * 0.55);
                float heart = exp(-r * 2.2);

                half3 colour = lerp(_DeepColor.rgb, _MidColor.rgb, smoothstep(0.25, 0.65, pattern));
                colour = lerp(colour, _BrightColor.rgb, saturate(smoothstep(0.62, 0.9, pattern) * 0.75 + heart * 0.3));
                float breath = 1.0 + _Breath * sin(t * 1.1) + _Breath * 0.5 * sin(t * 2.7 + 1.3);
                colour *= _Glow * breath;

                // Soft at every edge, a little softer at the top, so it melts into the arch's stones.
                float2 uv = input.uv;
                float edge = smoothstep(0.0, _EdgeSoftness, uv.x) * smoothstep(0.0, _EdgeSoftness, 1.0 - uv.x)
                           * smoothstep(0.0, _EdgeSoftness * 0.6, uv.y) * smoothstep(0.0, _EdgeSoftness * 1.6, 1.0 - uv.y);
                float alpha = saturate(_Opacity * edge * (0.7 + 0.3 * pattern + 0.2 * heart));
                colour = MixFog(colour, input.fog);
                return half4(colour * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
