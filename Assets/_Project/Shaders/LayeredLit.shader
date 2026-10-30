// Lit PBR for Local Generator models with up to two live surface layers (moss, rust, dirt, snow...) that grow
// over the baked maps at runtime. The base uses the URP Lit property names, so GeneratedTexturePostprocessor fills
// it like Lit. Each layer is a tileable material blended where the model's _BlendMask (R curvature, G edge wear,
// B cavity, A coverage) allows; its Amount (0-1) grows it from the strongest mask spots outward, shaped by the
// layer's height. Drive Amount per object with SurfaceLayerController (MaterialPropertyBlock).
// GLB models (LocalSpawnMaterials) use the glTF channel packing instead of Lit's: _ORMMap (R occlusion,
// G roughness, B metallic) with the _ORMMAP keyword.
Shader "Safe Haven/Layered Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        [NoScaleOffset] _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [NoScaleOffset] _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [NoScaleOffset] _ORMMap ("ORM (R occlusion, G roughness, B metallic; glTF models)", 2D) = "white" {}
        _Roughness ("Roughness (ORM)", Range(0, 1)) = 1
        [NoScaleOffset] _EmissionMap ("Emission", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _BlendMask ("Blend Mask (R curvature, G edge wear, B cavity, A coverage)", 2D) = "black" {}
        [Toggle] _LayerTriplanar ("Project layers in object space (no UV seams)", Float) = 1

        [Header(Layer 1)]
        _L1Amount ("Amount", Range(0, 1)) = 0
        [Enum(Curvature, 0, Edge wear, 1, Cavity, 2, Coverage, 3, Everywhere, 4)] _L1Source ("Grows from", Float) = 3
        _L1Upward ("Only on upward faces", Range(0, 1)) = 0
        _L1Patchiness ("Grows in patches", Range(0, 1)) = 0.7
        _L1PatchScale ("Patch size (fraction of tiling)", Range(0.05, 2)) = 0.5
        _L1HeightBlend ("Edges follow the texture", Range(0, 1)) = 0.6
        _L1Sharpness ("Edge sharpness", Range(1, 32)) = 6
        _L1Tiling ("Tiling (repeats per unit)", Float) = 2
        [NoScaleOffset] _L1Color ("Color", 2D) = "white" {}
        _L1Tint ("Tint", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _L1Normal ("Normal", 2D) = "bump" {}
        _L1NormalScale ("Normal Strength", Float) = 1
        [NoScaleOffset] _L1MetallicGloss ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _L1Metallic ("Metallic", Range(0, 1)) = 0
        _L1Smoothness ("Smoothness", Range(0, 1)) = 0.2
        [NoScaleOffset] _L1Height ("Height", 2D) = "gray" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _L1HeightChannel ("Height Channel", Float) = 0

        [Header(Layer 2)]
        _L2Amount ("Amount", Range(0, 1)) = 0
        [Enum(Curvature, 0, Edge wear, 1, Cavity, 2, Coverage, 3, Everywhere, 4)] _L2Source ("Grows from", Float) = 1
        _L2Upward ("Only on upward faces", Range(0, 1)) = 0
        _L2Patchiness ("Grows in patches", Range(0, 1)) = 0.7
        _L2PatchScale ("Patch size (fraction of tiling)", Range(0.05, 2)) = 0.5
        _L2HeightBlend ("Edges follow the texture", Range(0, 1)) = 0.6
        _L2Sharpness ("Edge sharpness", Range(1, 32)) = 6
        _L2Tiling ("Tiling (repeats per unit)", Float) = 2
        [NoScaleOffset] _L2Color ("Color", 2D) = "white" {}
        _L2Tint ("Tint", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _L2Normal ("Normal", 2D) = "bump" {}
        _L2NormalScale ("Normal Strength", Float) = 1
        [NoScaleOffset] _L2MetallicGloss ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _L2Metallic ("Metallic", Range(0, 1)) = 0
        _L2Smoothness ("Smoothness", Range(0, 1)) = 0.2
        [NoScaleOffset] _L2Height ("Height", 2D) = "gray" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _L2HeightChannel ("Height Channel", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Shaders/SeeThrough/SeeThrough.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap);
        TEXTURE2D(_ORMMap);
        TEXTURE2D(_EmissionMap);
        TEXTURE2D(_BlendMask);
        TEXTURE2D(_L1Color); SAMPLER(sampler_L1Color);
        TEXTURE2D(_L1Normal); TEXTURE2D(_L1MetallicGloss); TEXTURE2D(_L1Height);
        TEXTURE2D(_L2Color); SAMPLER(sampler_L2Color);
        TEXTURE2D(_L2Normal); TEXTURE2D(_L2MetallicGloss); TEXTURE2D(_L2Height);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _L1Tint, _L2Tint;
            half _BumpScale, _Metallic, _Smoothness, _OcclusionStrength, _LayerTriplanar, _Roughness;
            half _L1Amount, _L1Source, _L1Upward, _L1HeightBlend, _L1Sharpness, _L1NormalScale, _L1Patchiness;
            half _L1Metallic, _L1Smoothness, _L1HeightChannel;
            half _L2Amount, _L2Source, _L2Upward, _L2HeightBlend, _L2Sharpness, _L2NormalScale, _L2Patchiness;
            half _L2Metallic, _L2Smoothness, _L2HeightChannel;
            float _L1Tiling, _L2Tiling, _L1PatchScale, _L2PatchScale;
        CBUFFER_END
        float3 _LightDirection;
        float3 _LightPosition;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 tangentWS : TEXCOORD3;
            float3 positionOS : TEXCOORD4;
            half3 normalOS : TEXCOORD5;
            half fog : TEXCOORD6;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings LayeredVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.normalWS = normals.normalWS;
            output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.positionOS = input.positionOS.xyz;
            output.normalOS = input.normalOS;
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        struct Frame
        {
            half3 normal, tangent, bitangent;
            bool hasTangent;
        };
        Frame SurfaceFrame(Varyings input, half faceSign)
        {
            Frame frame;
            frame.normal = normalize(input.normalWS) * faceSign;
            half3 tangent = input.tangentWS.xyz;
            frame.hasTangent = dot(tangent, tangent) > 1e-6;
            tangent = frame.hasTangent ? normalize(tangent - frame.normal * dot(frame.normal, tangent)) : half3(1, 0, 0);
            frame.tangent = tangent;
            frame.bitangent = cross(frame.normal, tangent) * input.tangentWS.w;
            return frame;
        }
        half3 ToWorld(Frame frame, half3 normalTS)
        {
            return frame.hasTangent
                ? normalize(frame.tangent * normalTS.x + frame.bitangent * normalTS.y + frame.normal * normalTS.z)
                : frame.normal;
        }
        half3 BaseNormal(Varyings input, Frame frame)
        {
            return ToWorld(frame, UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale));
        }

        // Layer projection: object-space triplanar (stable across the generator's UV islands) or the model's UVs.
        struct Projection
        {
            float2 uv, uvX, uvY, uvZ;
            half3 weights, normalOS, axisSign;
            bool triplanar;
        };
        Projection Project(Varyings input, float tiling)
        {
            Projection p;
            p.triplanar = _LayerTriplanar > 0.5;
            p.uv = input.uv * tiling;
            float3 position = input.positionOS * tiling;
            p.normalOS = normalize(input.normalOS);
            p.axisSign = half3(p.normalOS.x < 0 ? -1 : 1, p.normalOS.y < 0 ? -1 : 1, p.normalOS.z < 0 ? -1 : 1);
            p.uvX = float2(position.z * p.axisSign.x, position.y);
            p.uvY = float2(position.x * p.axisSign.y, position.z);
            p.uvZ = float2(-position.x * p.axisSign.z, position.y);
            half3 w = pow(abs(p.normalOS), 4);
            p.weights = w / max(w.x + w.y + w.z, 1e-4);
            return p;
        }
        half4 SampleProjected(TEXTURE2D_PARAM(map, mapSampler), Projection p)
        {
            half4 value;
            if (p.triplanar)
            {
                value = SAMPLE_TEXTURE2D(map, mapSampler, p.uvX) * p.weights.x
                    + SAMPLE_TEXTURE2D(map, mapSampler, p.uvY) * p.weights.y
                    + SAMPLE_TEXTURE2D(map, mapSampler, p.uvZ) * p.weights.z;
            }
            else
            {
                value = SAMPLE_TEXTURE2D(map, mapSampler, p.uv);
            }
            return value;
        }
        half3 SampleProjectedNormal(TEXTURE2D_PARAM(map, mapSampler), Projection p, half scale, Frame frame)
        {
            half3 normalWS;
            if (p.triplanar)
            {
                // Whiteout blend (Golus, "Normal Mapping for a Triplanar Shader").
                half3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(map, mapSampler, p.uvX), scale);
                half3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(map, mapSampler, p.uvY), scale);
                half3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(map, mapSampler, p.uvZ), scale);
                nx.x *= p.axisSign.x;
                ny.x *= p.axisSign.y;
                nz.x *= -p.axisSign.z;
                half3 n = p.normalOS;
                nx = half3(nx.xy + n.zy, abs(nx.z) * n.x);
                ny = half3(ny.xy + n.xz, abs(ny.z) * n.y);
                nz = half3(nz.xy + n.xy, abs(nz.z) * n.z);
                half3 normalOS = normalize(nx.zyx * p.weights.x + ny.xzy * p.weights.y + nz.xyz * p.weights.z);
                normalWS = TransformObjectToWorldNormal(normalOS);
            }
            else
            {
                normalWS = ToWorld(frame, UnpackNormalScale(SAMPLE_TEXTURE2D(map, mapSampler, p.uv), scale));
            }
            return normalWS;
        }
        half Channel(half4 value, half channel)
        {
            return dot(value, half4(channel < 0.5, abs(channel - 1) < 0.5, abs(channel - 2) < 0.5, channel > 2.5));
        }
        half SourceMask(half4 blendMask, half source)
        {
            return source > 3.5 ? 1.0 : Channel(blendMask, source);
        }
        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }
        float ValueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float x00 = lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x);
            float x10 = lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x);
            float x01 = lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x);
            float x11 = lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
        }
        // Smooth object-space clumps that decide where a layer starts growing (no texture, no repeats).
        half PatchNoise(float3 p)
        {
            return ValueNoise(p) * 0.65 + ValueNoise(p * 2.37 + 17.3) * 0.35;
        }
        // 0 where the layer is absent, 1 where it fully covers. Amount 0 shows nothing; as Amount rises the layer
        // appears first where mask x patch noise is strongest and spreads from there, with its height texture
        // breaking up the edge, so it grows in clumps instead of fading in uniformly.
        half LayerWeight(half mask, half patch, half height, half amount, half patchiness, half heightBlend, half sharpness)
        {
            half grow = mask * lerp(1.0, smoothstep(0.2, 0.8, patch), patchiness);
            grow = saturate(grow + (height - 0.5) * 0.35 * heightBlend) * step(1e-3, mask);
            return saturate((grow - (1.0 - amount)) * sharpness) * step(1e-4, amount);
        }
        struct Surface
        {
            half3 albedo, normal, emission;
            half metallic, smoothness;
        };
        #define APPLY_LAYER(L, surface, input, frame, blendMask, upward) \
            if (L##Amount > 0) \
            { \
                Projection p = Project(input, L##Tiling); \
                half mask = SourceMask(blendMask, L##Source) * lerp(1.0, upward, L##Upward); \
                half height = Channel(SampleProjected(TEXTURE2D_ARGS(L##Height, sampler##L##Color), p), L##HeightChannel); \
                half patch = 1.0; \
                if (L##Amount < 0.999 && L##Patchiness > 0) \
                { \
                    patch = PatchNoise(input.positionOS * L##Tiling * L##PatchScale); \
                } \
                half w = LayerWeight(mask, patch, height, L##Amount, L##Patchiness, L##HeightBlend, L##Sharpness); \
                if (w > 0) \
                { \
                    half3 color = SampleProjected(TEXTURE2D_ARGS(L##Color, sampler##L##Color), p).rgb * L##Tint.rgb; \
                    half4 gloss = SampleProjected(TEXTURE2D_ARGS(L##MetallicGloss, sampler##L##Color), p); \
                    half3 normal = SampleProjectedNormal(TEXTURE2D_ARGS(L##Normal, sampler##L##Color), p, L##NormalScale, frame); \
                    surface.albedo = lerp(surface.albedo, color, w); \
                    surface.normal = normalize(lerp(surface.normal, normal, w)); \
                    surface.metallic = lerp(surface.metallic, gloss.r * L##Metallic, w); \
                    surface.smoothness = lerp(surface.smoothness, gloss.a * L##Smoothness, w); \
                    surface.emission *= 1.0 - w; \
                } \
            }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LayeredVertex
            #pragma fragment LayeredFragment
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ORMMAP
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
            // batcher (any MaterialPropertyBlock, e.g. SurfaceLayerController) lose their reflections and go dark.
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            half4 LayeredFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                SeeThroughClip(input.positionCS);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                Frame frame = SurfaceFrame(input, IS_FRONT_VFACE(face, 1.0, -1.0));
                Surface surface;
                surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                surface.normal = BaseNormal(input, frame);
                half occlusion;
                #if defined(_ORMMAP)
                    half4 orm = SAMPLE_TEXTURE2D(_ORMMap, sampler_BaseMap, input.uv);
                    surface.metallic = orm.b * _Metallic;
                    surface.smoothness = 1.0 - orm.g * _Roughness;
                    occlusion = lerp(1.0, orm.r, _OcclusionStrength);
                #else
                    #if defined(_METALLICSPECGLOSSMAP)
                        half4 gloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_BaseMap, input.uv);
                        surface.metallic = gloss.r;
                        surface.smoothness = gloss.a * _Smoothness;
                    #else
                        surface.metallic = _Metallic;
                        surface.smoothness = _Smoothness;
                    #endif
                    occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, input.uv).g, _OcclusionStrength);
                #endif
                surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb;
                half4 blendMask = SAMPLE_TEXTURE2D(_BlendMask, sampler_BaseMap, input.uv);
                half upward = smoothstep(0.2, 0.75, frame.normal.y);
                APPLY_LAYER(_L1, surface, input, frame, blendMask, upward)
                APPLY_LAYER(_L2, surface, input, frame, blendMask, upward)

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = surface.normal;
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
                SurfaceData data = (SurfaceData)0;
                data.albedo = surface.albedo;
                data.alpha = 1;
                data.metallic = surface.metallic;
                data.smoothness = surface.smoothness;
                data.occlusion = occlusion;
                data.emission = surface.emission;
                data.normalTS = half3(0, 0, 1);
                half4 color = UniversalFragmentPBR(lighting, data);
                color.rgb = MixFog(color.rgb, input.fog);
                return half4(color.rgb, 1);
            }
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
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
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
            half4 ShadowFragment() : SV_Target
            {
                return 0;
            }
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
            #pragma vertex LayeredVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            half4 DepthFragment(Varyings input) : SV_Target
            {
                SeeThroughClip(input.positionCS);
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
            #pragma vertex LayeredVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                SeeThroughClip(input.positionCS);
                float3 normal = BaseNormal(input, SurfaceFrame(input, IS_FRONT_VFACE(face, 1.0, -1.0)));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
