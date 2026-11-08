// Lit PBR for Local Generator trees (Vegetation tab GLBs) with their baked wind: uv1 = (sway, flex),
// uv2 = (phase, flutter), animated by TreeWind.hlsl, the same formula as the generator's 3D preview.
// Trunk, leaves and vines must all use it so leaves stay on the bending branches.
// Direction and strength come from the scene's MossWind (and its WindZone): _SafeHavenWind / _SafeHavenTreeWind.
// Materials are created on import by TreeWindMaterials (Safe Haven > Environment > Apply Tree Wind Materials).
// Fast Leaf Lighting (leaves and vines): sun and ambient are lit per vertex, darker inside the canopy (the baked
// flex: 0 at the trunk, 1 at the twig tips); the pixel samples the leaf texture, one hardware shadow compare
// (no soft-shadow filtering) and a cheap Lambert for nearby point lights. Alpha-tested leaf cards overlap many times, so the
// full PBR path (normal map, specular, reflections, soft shadows) per leaf pixel was the trees' main cost.
Shader "Safe Haven/Tree Wind"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        [NoScaleOffset] _ORMMap ("Occlusion (R) Roughness (G) Metallic (B)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _Roughness ("Roughness", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (leaf cards)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(_FAST_LEAF_LIGHTING)] _FastLeafLighting ("Fast Leaf Lighting (per vertex, no specular)", Float) = 0
        _Translucency ("Light Through Leaves", Range(0, 1)) = 0.5
        [Toggle] _Deciduous ("Deciduous (turns and falls in autumn)", Float) = 0
        [Toggle(_MASK_HDRP)] _MaskHDRP ("Mask Is HDRP Layout (metallic R, occlusion G, smoothness A)", Float) = 0
        _Smoothness ("Smoothness (HDRP mask)", Range(0, 1)) = 1
        [Header(Moss Covering)]
        [Toggle(_MOSS_COVER)] _MossCover ("Moss on Upward Surfaces", Float) = 0
        [NoScaleOffset] _MossMap ("Moss Colour", 2D) = "white" {}
        [Normal][NoScaleOffset] _MossNormal ("Moss Normal", 2D) = "bump" {}
        [NoScaleOffset] _MossMask ("Moss Mask (HDRP layout)", 2D) = "white" {}
        _MossColor ("Moss Tint", Color) = (0.75, 0.75, 0.75, 1)
        _MossTiling ("Moss Tiling (UV0)", Float) = 4
        _MossAmount ("Moss Amount", Range(0, 1)) = 1
        _MossRemap ("Moss Starts / Full (surface facing up, -1..1)", Vector) = (0, 0.5, 0, 0)
        [Header(Wind)]
        _WindStrength ("Wind Strength", Range(0, 2)) = 1
        _TreeHeight ("Tree Height (object units)", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Shaders/SeeThrough/SeeThrough.hlsl"
        #include "Assets/_Project/Models/Generated/Vegetation/TreeWind.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_ORMMap); SAMPLER(sampler_ORMMap);
        TEXTURE2D(_MossMap); SAMPLER(sampler_MossMap);
        TEXTURE2D(_MossNormal);
        TEXTURE2D(_MossMask);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale, _OcclusionStrength, _Roughness, _Metallic, _Cutoff, _Translucency, _Deciduous;
            float _WindStrength, _TreeHeight;
            half4 _MossColor;
            float4 _MossRemap;
            float _MossTiling, _MossAmount, _Smoothness;
        CBUFFER_END
        float4 _SafeHavenWind; // horizontal direction XZ (world), moss displacement, moss speed
        float _SafeHavenTreeWind; // tree strength (WindZone Main), 0 = still; time is real seconds like the preview
        float4 _SafeHavenSeason; // Seasons: x autumn colour, y leaf fall, z frost (all 0 outside a season cycle)

        // A tree's own number from where it stands, so each turns its own colour and drops its leaves in its own time.
        float SeasonTreeHash()
        {
            float3 at = TransformObjectToWorld(float3(0, 0, 0));
            return frac(sin(dot(floor(at.xz * 0.5), float2(12.9898, 78.233))) * 43758.5453);
        }

        // Autumn and frost on a leaf's colour; leaf fall thins the cover (only broadleaf leaves turn and fall).
        half4 SeasonLeaf(half4 color, float2 uv)
        {
            float tree = SeasonTreeHash();
            half autumn = saturate(_SafeHavenSeason.x * (0.75 + 0.5 * tree)) * _Deciduous;
            half grey = dot(color.rgb, half3(0.3, 0.59, 0.11));
            // Gold, orange or rust by tree, keeping the leaf's own shading.
            half3 turned = lerp(half3(1.0, 0.7, 0.18), half3(0.9, 0.3, 0.08), tree) * (0.3 + grey * 1.9);
            color.rgb = lerp(color.rgb, turned, autumn);
            half fall = saturate(_SafeHavenSeason.y * (0.7 + 0.6 * tree)) * _Deciduous;
            half spot = frac(sin(dot(floor(uv * 23.0), float2(41.3, 289.1))) * 15731.743);
            color.a *= step(fall * 1.05, spot);
            color.rgb = lerp(color.rgb, half3(0.8, 0.85, 0.9) * (0.45 + grey), _SafeHavenSeason.z * 0.28);
            return color;
        }
        #include "Assets/_Project/Shaders/SurfaceWetness.hlsl"
        #include "Assets/_Project/Shaders/SeasonSnow.hlsl"
        // Snow on the tops of crowns and branches: what faces up whitens, in clumps, so the woods read wintry from above.
        half3 SnowOn(half3 albedo, half3 normalWS, float2 uv)
        {
            if (_SafeHavenSnow <= 0.001)
            {
                return albedo;
            }

            half clumps = frac(sin(dot(floor(uv * 9.0), float2(12.9, 78.2))) * 43758.5);
            half cover = _SafeHavenSnow * smoothstep(0.15, 0.65, normalWS.y) * (0.55 + 0.45 * clumps);
            return lerp(albedo, half3(0.82, 0.86, 0.92), saturate(cover * 0.8));
        }
        float3 _LightDirection;
        float3 _LightPosition;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float2 wind : TEXCOORD1;
            float2 windPhase : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 tangentWS : TEXCOORD3;
            half fog : TEXCOORD4;
            half3 ambient : TEXCOORD5; // fast leaf lighting: ambient (per vertex)
            half3 sun : TEXCOORD6;     // fast leaf lighting: unshadowed sunlight (per vertex)
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float3 WindPositionWS(Attributes input)
        {
            // No Moss Wind in the scene leaves the direction at zero, and normalising it would give NaN (an invisible tree).
            float2 windXZ = dot(_SafeHavenWind.xy, _SafeHavenWind.xy) > 1e-8 ? _SafeHavenWind.xy : float2(1.0, 0.0);
            float3 dirOS = TransformWorldToObjectDir(float3(windXZ.x, 0.0, windXZ.y));
            float3 moved;
            TreeWind_float(input.positionOS.xyz, input.normalOS, input.wind, input.windPhase,
                _Time.y, _WindStrength * _SafeHavenTreeWind, _TreeHeight, dirOS, moved);
            return TransformObjectToWorld(moved);
        }
        Varyings TreeVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = WindPositionWS(input);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.normalWS = normals.normalWS;
            output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(output.positionCS.z);
            #if defined(_FAST_LEAF_LIGHTING)
                // Leaves are lit from both sides: the facing side directly, the far side by light shining through.
                Light sun = GetMainLight();
                half facing = dot(normals.normalWS, sun.direction);
                half diffuse = saturate(abs(facing)) * (facing >= 0 ? 1.0 : _Translucency);
                half canopy = lerp(0.45, 1.0, saturate(input.wind.y)); // inner leaves get less light
                output.sun = sun.color * diffuse * canopy;
                output.ambient = SampleSH(normals.normalWS) * canopy;
            #endif
            return output;
        }
        half4 SampleBase(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                color = SeasonLeaf(color, uv);
                clip(color.a - _Cutoff);
            #endif
            return color;
        }
        // The colour pass's version (its Pass has AlphaToMask On): the cutout edge becomes a 0-1 ramp one pixel wide,
        // which MSAA turns into partial sample coverage, so leaf edges are smoothed instead of stepping (they read as
        // pixelated with a hard clip). Without MSAA it is the same cutoff as before. Alpha is 1 when not cut out.
        half4 SampleBaseCoverage(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                color = SeasonLeaf(color, uv);
                color.a = saturate((color.a - _Cutoff) / max(fwidth(color.a), 0.0001) + 0.5);
                clip(color.a - 0.004);
            #else
                color.a = 1;
            #endif
            return color;
        }
        // A tangent-space normal (already unpacked) on this pixel's surface.
        half3 ToWorldNormal(Varyings input, half faceSign, half3 mapped)
        {
            half3 normal = normalize(input.normalWS) * faceSign;
            half3 tangent = input.tangentWS.xyz;
            if (dot(tangent, tangent) < 1e-6)
            {
                return normal; // leaf cards without tangents: no normal map
            }
            tangent = normalize(tangent - normal * dot(normal, tangent));
            half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
            return normalize(tangent * mapped.x + bitangent * mapped.y + normal * mapped.z);
        }
        half3 SurfaceNormal(Varyings input, half faceSign)
        {
            return ToWorldNormal(input, faceSign, UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale));
        }
        half3 FastLeafLighting(Varyings input, half faceSign)
        {
            half shadow = 1.0;
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                shadow = GetMainLight(ComputeScreenPos(input.positionCS)).shadowAttenuation;
            #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                // One hardware compare, no soft-shadow taps: dappled light through the canopy at little cost.
                float4 coord = TransformWorldToShadowCoord(input.positionWS);
                half raw = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, coord.xyz);
                shadow = BEYOND_SHADOW_FAR(coord) ? 1.0 : LerpWhiteTo(raw, GetMainLightShadowParams().x);
                shadow = lerp(shadow, 1.0, GetMainLightShadowFade(input.positionWS));
            #endif
            #if defined(_LIGHT_COOKIES)
                shadow *= SampleMainLightCookie(input.positionWS).r; // passing cloud shadows
            #endif
            half3 light = input.ambient * SkyAmbient(input.positionWS) + input.sun * shadow;
            #if defined(_ADDITIONAL_LIGHTS)
                half3 normal = normalize(input.normalWS) * faceSign;
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light extra = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    light += extra.color * extra.distanceAttenuation * saturate(abs(dot(normal, extra.direction)));
                }
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light extra = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    light += extra.color * extra.distanceAttenuation * saturate(abs(dot(normal, extra.direction)));
                LIGHT_LOOP_END
            #endif
            return light;
        }
        half4 TreeFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
                #if defined(_ALPHATEST_ON)
                    SeeThroughCanopyClip(input.positionCS); // leaves: the canopy cutaway as well
                #else
                    SeeThroughClip(input.positionCS);
                #endif
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 baseColor = SampleBaseCoverage(input.uv);
            #if defined(_FAST_LEAF_LIGHTING)
                half3 leaf = SnowOn(baseColor.rgb, normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0), input.uv)
                    * FastLeafLighting(input, IS_FRONT_VFACE(face, 1.0, -1.0));
                leaf *= lerp(1.0, 0.78, (half)_SafeHavenWetness); // rain-darkened leaves
                return half4(MixFog(leaf, input.fog), baseColor.a);
            #endif
            half4 mask = SAMPLE_TEXTURE2D(_ORMMap, sampler_ORMMap, input.uv);
            half faceSign = IS_FRONT_VFACE(face, 1.0, -1.0);
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.positionCS = input.positionCS;
            lighting.normalWS = SurfaceNormal(input, faceSign);
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord = ComputeScreenPos(input.positionCS);
            #else
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
            lighting.fogCoord = input.fog;
            lighting.bakedGI = SampleSH(lighting.normalWS);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            lighting.shadowMask = half4(1, 1, 1, 1);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = baseColor.rgb;
            surface.alpha = 1;
            #if defined(_MASK_HDRP)
                surface.metallic = mask.r * _Metallic;
                surface.smoothness = mask.a * _Smoothness;
                surface.occlusion = lerp(1.0, mask.g, _OcclusionStrength);
            #else
                surface.metallic = mask.b * _Metallic;
                surface.smoothness = 1.0 - mask.g * _Roughness;
                surface.occlusion = lerp(1.0, mask.r, _OcclusionStrength);
            #endif
            #if defined(_MOSS_COVER)
                // Moss on whatever faces up (the tops of branches and the upper side of a leaning trunk), with a ragged
                // edge from the moss's own occlusion: the covering the SeedMesh shaders give these trees.
                float2 mossUV = input.uv * _MossTiling;
                half4 moss = SAMPLE_TEXTURE2D(_MossMap, sampler_MossMap, mossUV) * _MossColor;
                half4 mossMask = SAMPLE_TEXTURE2D(_MossMask, sampler_MossMap, mossUV);
                half up = normalize(input.normalWS).y * faceSign;
                half reach = smoothstep(_MossRemap.x, max(_MossRemap.y, _MossRemap.x + 0.01), up) * _MossAmount;
                half cover = smoothstep(0.35, 0.65, reach * (0.55 + 0.9 * mossMask.g));
                half3 mossNormal = ToWorldNormal(input, faceSign, UnpackNormal(SAMPLE_TEXTURE2D(_MossNormal, sampler_MossMap, mossUV)));
                lighting.normalWS = normalize(lerp(lighting.normalWS, mossNormal, cover));
                surface.albedo = lerp(surface.albedo, moss.rgb, cover);
                surface.metallic *= 1.0 - cover;
                surface.smoothness = lerp(surface.smoothness, mossMask.a * 0.5, cover);
                surface.occlusion = lerp(surface.occlusion, mossMask.g, cover);
            #endif
            surface.albedo = SnowOn(surface.albedo, normalize(input.normalWS) * faceSign, input.uv);
            surface.occlusion *= SkyAmbient(lighting.positionWS);
            #if defined(_ALPHATEST_ON)
                SurfaceWet(surface.albedo, surface.smoothness, lighting.normalWS, lighting.positionWS, 0.4);
            #else
                SurfaceWet(surface.albedo, surface.smoothness, lighting.normalWS, lighting.positionWS, 1.0);
            #endif
            surface.normalTS = half3(0, 0, 1);
            half4 color = UniversalFragmentPBR(lighting, surface);
            #if defined(_ALPHATEST_ON)
                // Sunlight through the leaf from behind, as the SeedMesh foliage's transmission does.
                // Through the leaf from behind, and brightest looking towards a low sun: canopies glow at golden hour.
                Light sun = GetMainLight(lighting.shadowCoord, lighting.positionWS, lighting.shadowMask);
                half through = saturate(dot(-lighting.normalWS, sun.direction));
                half toward = pow(saturate(dot(lighting.viewDirectionWS, -sun.direction)), 3.0);
                half back = through * 0.6 + toward * 0.8;
                color.rgb += surface.albedo * sun.color * back * sun.shadowAttenuation * _Translucency;
            #endif
            color.rgb = MixFog(color.rgb, input.fog);
            return half4(color.rgb, baseColor.a);
        }
        float4 ShadowVertex(Attributes input, out float2 uv : TEXCOORD0) : SV_POSITION
        {
            UNITY_SETUP_INSTANCE_ID(input);
            uv = TRANSFORM_TEX(input.uv, _BaseMap);
            float3 positionWS = WindPositionWS(input);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
            return positionCS;
        }
        half4 ClipFragment(float4 positionCS : SV_POSITION, float2 uv : TEXCOORD0) : SV_Target
        {
            SampleBase(uv);
            return 0;
        }
        half4 NormalsFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
                #if defined(_ALPHATEST_ON)
                    SeeThroughCanopyClip(input.positionCS); // leaves: the canopy cutaway as well
                #else
                    SeeThroughClip(input.positionCS);
                #endif
            SampleBase(input.uv);
            float3 normal = SurfaceNormal(input, IS_FRONT_VFACE(face, 1.0, -1.0));
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment TreeFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _FAST_LEAF_LIGHTING
            #pragma shader_feature_local_fragment _MASK_HDRP
            #pragma shader_feature_local_fragment _MOSS_COVER
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Forward+ supplies reflections through the probe atlas; without these, renderers drawn outside the SRP
            // batcher (any MaterialPropertyBlock, e.g. hit flashes) lose their reflections and go dark.
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ClipFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment DepthFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            half4 DepthFragment(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    SeeThroughCanopyClip(input.positionCS); // leaves: the canopy cutaway as well
                #else
                    SeeThroughClip(input.positionCS);
                #endif
                SampleBase(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment NormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
