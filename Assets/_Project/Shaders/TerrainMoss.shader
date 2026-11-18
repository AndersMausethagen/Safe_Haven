Shader "Safe Haven/Terrain Moss"
{
    Properties
    {
        _ShadowColor ("Deep Moss", Color) = (0.065, 0.12, 0.045, 1)
        _MossColor ("Moss", Color) = (0.22, 0.34, 0.09, 1)
        _TipColor ("Fresh Tips", Color) = (0.43, 0.49, 0.19, 1)
        _PatternScale ("Pattern Scale", Float) = 3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _BaseMap ("Moss Base Color", 2D) = "white" {}
        [Normal] _BumpMap ("Moss Normal", 2D) = "bump" {}
        _PackedMap ("AO Roughness Height", 2D) = "white" {}
        _TextureSize ("Texture Size (metres)", Float) = 1.5
        [HideInInspector] _UseTextures ("Texture Influence", Float) = 0
        [HideInInspector] _UseNormal ("Normal Strength", Float) = 0
        [HideInInspector] _UsePacked ("Use Packed Map", Float) = 0
        [HideInInspector] _Control ("Terrain Alphamap", 2D) = "white" {}
        [HideInInspector] _Holes ("Terrain Holes", 2D) = "white" {}
        [HideInInspector] _MaskChannel ("Mask Channel", Vector) = (1, 0, 0, 0)
        [HideInInspector] _Threshold ("Coverage Threshold", Float) = 0.15
        [HideInInspector] _Softness ("Edge Softness", Float) = 0.2
        [HideInInspector] _Tufts ("Tuft Mode", Float) = 0
        [HideInInspector] _DrawDistance ("Tuft Distance", Float) = 65
        [HideInInspector] _WindResponse ("Wind Response", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        // A raid's ground light, as the terrain under the moss gets it (nothing elsewhere).
        #include "Assets/_Project/Shaders/GroundLight.hlsl"
        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_Holes); SAMPLER(sampler_Holes);
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_PackedMap); SAMPLER(sampler_PackedMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _ShadowColor, _MossColor, _TipColor;
            float4 _MaskChannel;
            float _PatternScale, _Smoothness, _Threshold, _Softness;
            float _Tufts, _DrawDistance, _WindResponse;
            float _TextureSize, _UseTextures, _UseNormal, _UsePacked;
        CBUFFER_END
        float4 _SafeHavenWind; // horizontal direction XZ, displacement, speed
        float _SafeHavenWindGusts;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 maskUV : TEXCOORD1;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 maskUV : TEXCOORD2;
            half height : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }
        float Noise(float2 p)
        {
            float2 cell = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
        }
        Varyings MossVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            float time = _Time.y * _SafeHavenWind.w;
            float wave = sin(dot(world.xz, float2(0.7, 0.43)) + time * 2.1);
            float gust = 1.0 - _SafeHavenWindGusts * (0.5 + 0.5 * sin(time * 0.63 + world.x * 0.08));
            world.xz += _SafeHavenWind.xy * _SafeHavenWind.z * _WindResponse
                * (0.6 + 0.4 * wave) * gust * input.color.a * input.color.a * _Tufts;
            output.positionWS = world;
            output.positionCS = TransformWorldToHClip(world);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.maskUV = input.maskUV;
            output.height = input.color.a;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        void ClipMoss(Varyings input)
        {
            clip(SAMPLE_TEXTURE2D(_Holes, sampler_Holes, input.maskUV).r - 0.5);
            float weight = dot(SAMPLE_TEXTURE2D(_Control, sampler_Control, input.maskUV), _MaskChannel);
            float breakup = Noise(input.positionWS.xz * _PatternScale * 2.0);
            clip(weight - _Threshold - (breakup - 0.5) * _Softness);
            if (_Tufts > 0.5)
            {
                float fade = saturate((_DrawDistance - distance(GetCameraPositionWS(), input.positionWS)) / max(1.0, _DrawDistance * 0.2));
                clip(fade - Hash(floor(input.positionWS.xz * 100.0)) - 0.001);
            }
        }
        half3 MossNormal(Varyings input)
        {
            half3 normal = NormalizeNormalPerPixel(input.normalWS);
            half3 axis = abs(normal.z) < 0.99 ? half3(0, 0, 1) : half3(0, 1, 0);
            half3 tangent = normalize(cross(normal, axis));
            half3 bitangent = cross(tangent, normal);
            float2 uv = input.positionWS.xz / max(0.1, _TextureSize);
            half3 mapped = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _UseNormal);
            return normalize(tangent * mapped.x + bitangent * mapped.y + normal * mapped.z);
        }
        half4 MossFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            ClipMoss(input);
            float2 p = input.positionWS.xz * _PatternScale;
            float variation = saturate(Noise(p) * 0.65 + Noise(p * 5.7) * 0.35);
            half3 albedo = lerp(_ShadowColor.rgb, _MossColor.rgb, variation);
            albedo = lerp(albedo, _TipColor.rgb, input.height * 0.65);
            float2 textureUV = input.positionWS.xz / max(0.1, _TextureSize);
            half3 textured = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, textureUV).rgb;
            // Preserve palette controls as a tint while letting authored texture detail carry the surface.
            textured *= _MossColor.rgb / half3(0.22, 0.34, 0.09);
            textured = lerp(textured, textured * 1.25, input.height * 0.6);
            albedo = lerp(albedo, textured, _UseTextures);
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.normalWS = MossNormal(input);
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #else
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
            lighting.bakedGI = SampleSH(lighting.normalWS);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            lighting.shadowMask = half4(1, 1, 1, 1);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = albedo;
            surface.alpha = 1;
            half3 packed = SAMPLE_TEXTURE2D(_PackedMap, sampler_PackedMap, textureUV).rgb;
            surface.smoothness = lerp(_Smoothness, 1 - packed.g, _UsePacked);
            surface.occlusion = lerp(lerp(0.8, 1.0, input.height), packed.r, _UsePacked);
            // The canopy's and the land's shade and the light thrown back from sunlit ground, as on the terrain it grows on, so the
            // moss doesn't light up against a darker floor (GroundLight.hlsl; nothing without a field).
            GroundLightApply(GroundLightOnGround(input.positionWS), surface.occlusion, lighting.bakedGI);
            lighting.bakedGI += GroundLightBounce(input.positionWS, lighting.normalWS);
            // Sun through gaps in the canopy on the moss as on the ground it covers (GroundLight.hlsl).
            lighting.bakedGI += GroundLightSunflecks(input.positionWS);
            surface.normalTS = half3(0, 0, 1);
            half4 color = UniversalFragmentPBR(lighting, surface);
            color.rgb = MixFog(color.rgb, input.fog);
            return color;
        }
        half4 MossDepth(Varyings input) : SV_Target
        {
            ClipMoss(input);
            return 0;
        }
        half4 MossNormals(Varyings input) : SV_Target
        {
            ClipMoss(input);
            float3 normal = MossNormal(input);
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
            #pragma vertex MossVertex
            #pragma fragment MossFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
            #pragma vertex MossVertex
            #pragma fragment MossDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MossVertex
            #pragma fragment MossNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
