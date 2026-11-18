// Warnings drawn on the ground itself (Docs/LookAndFeelResearch_2026-11-18.md V1, U5): a monster's strike, a lob's landing, a lingering
// hazard, the opening ring at a spent foe's feet. Drawn on a box round the warning (GroundMarks.Box), the shader reads the scene's depth,
// finds the surface under each pixel and paints the shape onto it, so a fan lies over a slope, a step or a rock instead of clipping into
// the tessellated ground or floating over a hollow. Steep surfaces (a creature's legs, a cliff) fade out.
// The shape: a fan of Arc degrees (360 a disc) out to the box's edge, a ring when Inner is above 0; a bright rim of Rim metres; an area
// dim at the middle and brighter to its edge; a fill's front glowing; a pattern per kind so colour never carries it alone (0 a solid
// rim, 1 a dashed rim: a strike any blow breaks off, 2 hatched: one no guard stops, a hazard); a flash as the hit lands.
// _TelegraphContrast (global, Options › High-Contrast Warnings): wider rims, a dark line inside them, stronger fills.
Shader "Safe Haven/Ground Telegraph"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.3, 0.1, 0.45)
        _Arc ("Arc (degrees, 360 a disc)", Range(1, 360)) = 360
        _Inner ("Inner Radius (share, 0 none)", Range(0, 0.99)) = 0
        _Rim ("Rim (metres, 0 none)", Range(0, 0.5)) = 0.07
        _RimBoost ("Rim Opacity", Range(0, 4)) = 1.8
        _Pattern ("Pattern (0 solid, 1 dashed, 2 hatched)", Float) = 0
        _Gradient ("Gradient (0 even, 1 dim middle, bright edge)", Range(0, 1)) = 0.5
        _Front ("Front Glow", Range(0, 1)) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _Height ("Reach Up and Down (metres)", Range(0.2, 5)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Ground Telegraph"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // The box's far faces, tested against nothing: what the depth holds decides where it paints, so the camera may stand
            // inside the box (a close cinematic shot) and a warning behind a crown of leaves stays hidden (the leaves are outside it).
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Project/Shaders/GroundLight.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Arc;
                float _Inner;
                float _Rim;
                float _RimBoost;
                float _Pattern;
                float _Gradient;
                float _Front;
                float _Flash;
                float _Height;
            CBUFFER_END
            float _TelegraphContrast;
            // The terrain under the mark (GroundMarks.BindGround): its own 16-bit heightmap, so what stands on the ground (a character, a
            // tall rock's top) never takes the mark. xy its corner, zw uv per metre; x its base height, y metres per unit, z half a
            // texel, w 1 while bound.
            TEXTURE2D(_GroundMarkHeightmap); SAMPLER(sampler_GroundMark_linear_clamp);
            float4 _GroundMarkPlace;
            float4 _GroundMarkScale;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float raw = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
                float3 world = ComputeWorldSpacePosition(screenUV, raw, UNITY_MATRIX_I_VP);
                // The surface's slope, from the depth itself: ground and rock tops take the mark, legs and walls don't.
                float3 facing = normalize(cross(ddy(world), ddx(world)));
                half level = smoothstep(0.35, 0.65, abs(facing.y));
                // Under water (the depth holds the bed: water writes none) a raid's field knows the surface: the mark lies on the water
                // where the eye meets it, so a lob into a pool is drawn on the pool, not shifted and cut off on its bed.
                float waterLevel;
                bool onWater = false;
                if (GroundLightWaterLevel(world, waterLevel) && world.y < waterLevel - 0.03)
                {
                    float3 eye = GetCameraPositionWS();
                    float3 ray = world - eye;
                    if (ray.y < -1e-4)
                    {
                        world = eye + ray * saturate((waterLevel - eye.y) / ray.y);
                        onWater = true;
                        level = 1.0;
                    }
                }

                float3 local = TransformWorldToObject(world);
                float r = length(local.xz);
                UNITY_BRANCH if (_GroundMarkScale.w > 0.5 && !onWater)
                {
                    float2 heightUV = (world.xz - _GroundMarkPlace.xy) * _GroundMarkPlace.zw + _GroundMarkScale.z;
                    float ground = _GroundMarkScale.x + SAMPLE_TEXTURE2D_LOD(_GroundMarkHeightmap, sampler_GroundMark_linear_clamp, heightUV, 0).r * _GroundMarkScale.y;
                    level *= 1.0 - smoothstep(0.35, 0.7, world.y - ground);
                }
                clip(min(min(1.0 - r, r - _Inner), _Height - abs(local.y)));

                // Metres from the shape's outline (the box's own scale: a range of 2 m and one of 8 m get the same rim).
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float angle = atan2(local.x, local.z);
                float half_ = radians(min(_Arc, 360.0) * 0.5);
                float off = half_ - abs(angle);
                clip(_Arc >= 359.5 ? 1.0 : off);
                float outer = (1.0 - r) * scale;
                float inner = _Inner > 0.0 ? (r - _Inner) * scale : 1e4;
                float side = _Arc >= 359.5 ? 1e4 : r * scale * sin(min(off, 1.5707963));
                float edge = min(outer, inner);
                float d = min(edge, side);
                // Along the outline (metres), for the dashes: round the arc on the curved edges, out from the creature on the sides.
                float along = side < edge ? r * scale : angle * r * scale;

                half contrast = saturate(_TelegraphContrast);
                float rimWidth = _Rim * (1.0 + 0.6 * contrast);
                half rim = rimWidth > 0.0 ? 1.0 - smoothstep(rimWidth * 0.55, rimWidth, d) : 0.0;
                if (_Pattern > 0.5 && _Pattern < 1.5)
                {
                    rim *= step(frac(along / 0.45), 0.58);
                }

                half alpha = _BaseColor.a * lerp(1.0, lerp(0.5, 1.15, r * r), _Gradient) * (1.0 + 0.5 * contrast);
                if (_Pattern > 1.5)
                {
                    float stripe = frac((local.x + local.z) * scale / 0.38);
                    alpha *= lerp(0.45, 1.3, step(stripe, 0.45));
                }

                // A fill's front glows as it sweeps out (r near 1 is the front: the fill's box grows with it).
                alpha *= 1.0 + _Front * 1.4 * (1.0 - smoothstep(0.0, 0.16, 1.0 - r));
                half3 colour = _BaseColor.rgb;
                colour = lerp(colour, saturate(colour * 1.35 + 0.18), rim);
                alpha = lerp(alpha, saturate(_BaseColor.a * _RimBoost + 0.25), rim);
                // High contrast: a dark line just inside the rim, so the edge reads on bright grass and pale rock alike.
                if (contrast > 0.0 && rimWidth > 0.0)
                {
                    half line_ = (1.0 - smoothstep(rimWidth * 1.5, rimWidth * 1.9, d)) * (1.0 - rim) * contrast;
                    colour *= 1.0 - 0.75 * line_;
                    alpha = lerp(alpha, max(alpha, 0.8), line_);
                }

                colour = lerp(colour, half3(1.0, 0.97, 0.9), _Flash * 0.7);
                alpha = lerp(alpha, max(alpha, 0.75), _Flash);
                return half4(colour, saturate(alpha) * level);
            }
            ENDHLSL
        }
    }
}
