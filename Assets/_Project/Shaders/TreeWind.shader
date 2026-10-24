// Lit PBR for Local Generator trees (Vegetation tab GLBs) with their baked wind: uv1 = (sway, flex),
// uv2 = (phase, flutter), animated by TreeWind.hlsl, the same formula as the generator's 3D preview.
// Trunk, leaves and vines must all use it so leaves stay on the bending branches.
// Direction and strength come from the scene's MossWind (and its WindZone): _SafeHavenWind / _SafeHavenTreeWind.
// Materials are created on import by TreeWindMaterials (Safe Haven > Environment > Apply Tree Wind Materials).
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
        #include "Assets/_Project/Models/Generated/Vegetation/TreeWind.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_ORMMap); SAMPLER(sampler_ORMMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale, _OcclusionStrength, _Roughness, _Metallic, _Cutoff;
            float _WindStrength, _TreeHeight;
        CBUFFER_END
        float4 _SafeHavenWind; // horizontal direction XZ (world), moss displacement, moss speed
        float _SafeHavenTreeWind; // tree strength (WindZone Main), 0 = still; time is real seconds like the preview
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
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float3 WindPositionWS(Attributes input)
        {
            float3 dirOS = TransformWorldToObjectDir(float3(_SafeHavenWind.x, 0.0, _SafeHavenWind.y));
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
            return output;
        }
        half4 SampleBase(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(color.a - _Cutoff);
            #endif
            return color;
        }
        half3 SurfaceNormal(Varyings input, half faceSign)
        {
            half3 normal = normalize(input.normalWS) * faceSign;
            half3 tangent = input.tangentWS.xyz;
            if (dot(tangent, tangent) < 1e-6)
            {
                return normal; // leaf cards without tangents: no normal map
            }
            tangent = normalize(tangent - normal * dot(normal, tangent));
            half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
            half3 mapped = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
            return normalize(tangent * mapped.x + bitangent * mapped.y + normal * mapped.z);
        }
        half4 TreeFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 baseColor = SampleBase(input.uv);
            half3 orm = SAMPLE_TEXTURE2D(_ORMMap, sampler_ORMMap, input.uv).rgb;
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.positionCS = input.positionCS;
            lighting.normalWS = SurfaceNormal(input, IS_FRONT_VFACE(face, 1.0, -1.0));
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
            surface.metallic = orm.b * _Metallic;
            surface.smoothness = 1.0 - orm.g * _Roughness;
            surface.occlusion = lerp(1.0, orm.r, _OcclusionStrength);
            surface.normalTS = half3(0, 0, 1);
            half4 color = UniversalFragmentPBR(lighting, surface);
            color.rgb = MixFog(color.rgb, input.fog);
            return half4(color.rgb, 1);
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
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TreeVertex
            #pragma fragment TreeFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
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
            half4 DepthFragment(Varyings input) : SV_Target
            {
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
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
