// Ponds, streams and waterfalls (see Docs/Water.md). One surface shader for all of them:
// - Colour by depth: the scene depth under the surface gives the water's depth (shallow -> deep colour) and how much
//   of the bed shows through (Clarity); the bed is refracted and lit by caustics.
// - Flow: flat water scrolls its ripples and foam in world XZ along _FlowDirection (object space, so rotating the
//   object turns the river), or, with Flow Follows the Mesh, along each vertex's own direction (UV2: object-space
//   XZ, its length scales the speed; spline rivers), cross-fading two phases so curving flow never stretches the
//   ripples; steep faces (waterfalls) run down the mesh UV (metres, v downhill) at _FallSpeed.
//   Faster and steeper water turns whiter, murkier and foamier.
// - Foam where the bed or a stone comes near the surface, trailing downstream behind stones (upstream depth taps),
//   on fast or falling water, and around WaterInteractor foam spots (_WaterFoamSpots: x, z, radius, strength).
// - Ripples from the player and splashing stones (the WaterRipples height field: _WaterRippleTex / _WaterRippleRect),
//   rain rings (_SafeHavenRain) and wind chop (MossWind's _SafeHavenWind).
// Without the camera depth / opaque textures (e.g. the Mobile renderer) WaterSurface turns on SAFE_HAVEN_WATER_SIMPLE:
// plain alpha blending, no refraction or depth foam.
Shader "Safe Haven/Water"
{
    Properties
    {
        [Header(Colour)]
        _ShallowColor ("Shallow Colour", Color) = (0.2, 0.42, 0.38, 1)
        _DeepColor ("Deep Colour", Color) = (0.02, 0.08, 0.1, 1)
        _DepthDistance ("Deep At (metres)", Float) = 2.5
        _Clarity ("Clarity (metres you see into it)", Float) = 2
        _FastColor ("White Water Colour", Color) = (0.75, 0.84, 0.84, 1)
        _Murk ("Fast Water Is Murky", Range(0, 1)) = 0.6
        [Header(Surface)]
        [NoScaleOffset][Normal] _NormalMap ("Ripple Normals", 2D) = "bump" {}
        _NormalTiling ("Ripple Size (metres)", Float) = 3
        _NormalStrength ("Ripple Strength", Range(0, 2)) = 0.5
        _WindRipples ("Wind Makes It Choppy", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.93
        _Reflection ("Reflection Strength", Range(0, 1)) = 0.8
        _Refraction ("Refraction", Range(0, 0.15)) = 0.03
        [Header(Flow)]
        _FlowDirection ("Flow Direction (object X, Z)", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Flow Speed (metres per second)", Range(0, 6)) = 0
        [Toggle] _FlowFromMesh ("Flow Follows the Mesh (UV2 direction; spline rivers)", Float) = 0
        _FallSpeed ("Fall Speed on Steep Water (m/s)", Range(0, 15)) = 6
        _FallTiling ("Fall Streak Size (metres)", Float) = 2
        [Toggle] _SteepFaces ("Has Steep Faces (waterfalls; off hides the sides of boxes)", Float) = 0
        [Toggle] _VertexAlpha ("Edge Fade from Vertex Alpha", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Header(Foam)]
        [NoScaleOffset] _FoamMap ("Foam (R pattern, G noise)", 2D) = "black" {}
        _FoamColor ("Foam Colour", Color) = (0.9, 0.93, 0.93, 1)
        _FoamTiling ("Foam Size (metres)", Float) = 2
        _ShoreFoam ("Shore and Stone Foam (metres)", Range(0, 1.5)) = 0.25
        _FoamTrail ("Foam Trail Behind Stones (metres)", Range(0, 4)) = 1.2
        _SpeedFoam ("Foam from Speed", Range(0, 1)) = 0.5
        _FallFoam ("Foam on Steep Water", Range(0, 1)) = 0.85
        [Header(Underwater)]
        [NoScaleOffset] _CausticsMap ("Caustics", 2D) = "black" {}
        _Caustics ("Caustics Strength", Range(0, 2)) = 0.6
        _CausticsTiling ("Caustics Size (metres)", Float) = 2.5
        [Header(Interaction)]
        _RippleStrength ("Player and Stone Ripples", Range(0, 4)) = 1
        _RainRipples ("Rain Ripples", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-50" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Forward+ reflections; without these, renderers drawn outside the SRP batcher go dark (see ARCHITECTURE.md).
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ SAFE_HAVEN_WATER_SIMPLE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #define WATER_MAX_FOAM_SPOTS 8

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
            TEXTURE2D(_CausticsMap); SAMPLER(sampler_CausticsMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FastColor, _FoamColor;
                float4 _FlowDirection;
                float _DepthDistance, _Clarity, _Murk, _NormalTiling, _NormalStrength, _WindRipples, _Smoothness;
                float _Reflection, _Refraction, _FlowSpeed, _FallSpeed, _FallTiling, _VertexAlpha, _Cull, _SteepFaces;
                float _FoamTiling, _ShoreFoam, _FoamTrail, _SpeedFoam, _FallFoam, _Caustics, _CausticsTiling;
                float _RippleStrength, _RainRipples, _FlowFromMesh;
            CBUFFER_END
            // Ripple height field (WaterRipples): R = height. UV = (xz - rect.xy) * rect.z; rect.w = one texel in UV.
            TEXTURE2D(_WaterRippleTex); SAMPLER(sampler_WaterRippleTex);
            float4 _WaterRippleRect;
            float _WaterRippleOn;
            float4 _WaterFoamSpots[WATER_MAX_FOAM_SPOTS];
            float _WaterFoamSpotCount;
            float _SafeHavenRain;
            float4 _SafeHavenWind; // horizontal direction XZ (world), moss displacement, speed
            // Ice (WinterIce, per surface through a property block): still water freezes over in a snowing season.
            float _IceAmount;
            #include "Assets/_Project/Shaders/SeasonSnow.hlsl"

            // Cracks in the ice: the edges of a cell pattern, fine lines between plates.
            half IceCracks(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float nearest = 8.0, second = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float2 r = o + float2(SeasonSnowHash(cell + o), SeasonSnowHash(cell + o + 17.3)) - f;
                        float d = dot(r, r);
                        if (d < nearest) { second = nearest; nearest = d; }
                        else if (d < second) { second = d; }
                    }
                }

                return 1.0 - smoothstep(0.0, 0.035, sqrt(second) - sqrt(nearest));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 flowOS : TEXCOORD1; // Flow Follows the Mesh: object-space XZ flow direction, length = speed scale
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float2 flow : TEXCOORD4; // flat flow in world XZ, metres per second
                half2 alphaFog : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings WaterVertex(Attributes input)
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
                output.uv = input.uv;
                bool meshFlow = _FlowFromMesh > 0.5;
                float2 flowOS = meshFlow ? input.flowOS : _FlowDirection.xy;
                float2 direction = TransformObjectToWorldDir(float3(flowOS.x, 0, flowOS.y), false).xz;
                float speedScale = meshFlow ? length(input.flowOS) : 1.0;
                output.flow = dot(direction, direction) > 1e-6 ? normalize(direction) * (_FlowSpeed * speedScale) : 0;
                output.alphaFog = half2(_VertexAlpha > 0.5 ? input.color.a : 1.0, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half3 SampleRipple(float2 uv)
            {
                return UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv));
            }

            // Tangent-space ripples of flat water (x = world X, y = world Z): two layers moving with the flow, or
            // drifting with the wind when the water is still.
            half3 FlatRipples(float2 xz, float2 flow, float time, half strength)
            {
                float2 wind = _SafeHavenWind.xy;
                float2 drift = length(flow) > 0.02 ? flow : (dot(wind, wind) > 1e-4 ? normalize(wind) : float2(0.6, 0.8)) * 0.06;
                float2 p = xz / max(_NormalTiling, 0.05);
                float2 q = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8) * 1.7 + 0.31; // turned and smaller
                float2 drift2 = float2(drift.x * 0.8 - drift.y * 0.6, drift.x * 0.6 + drift.y * 0.8) * 1.15
                              + float2(-drift.y, drift.x) * 0.35;
                half3 ripples;
                if (_FlowFromMesh > 0.5)
                {
                    // The flow turns across the surface: scroll two copies over a short cycle, each restarting while
                    // invisible, and cross-fade them, so neighbouring pixels never drift apart (flow-map technique).
                    float cycle = 1.6;
                    float t0 = frac(time / cycle);
                    float t1 = frac(time / cycle + 0.5);
                    half w0 = 1.0 - abs(1.0 - 2.0 * t0);
                    float scale = cycle / max(_NormalTiling, 0.05);
                    half3 a0 = SampleRipple(p - drift * t0 * scale);
                    half3 b0 = SampleRipple(q - drift2 * t0 * scale * 1.7);
                    half3 a1 = SampleRipple(p - drift * t1 * scale + 0.5);
                    half3 b1 = SampleRipple(q - drift2 * t1 * scale * 1.7 + 0.5);
                    half3 phase0 = half3((a0.xy + b0.xy) * strength, a0.z * b0.z);
                    half3 phase1 = half3((a1.xy + b1.xy) * strength, a1.z * b1.z);
                    ripples = lerp(phase1, phase0, w0);
                }
                else
                {
                    half3 a = SampleRipple(p - frac(drift * time / max(_NormalTiling, 0.05)));
                    half3 b = SampleRipple(q - frac(drift2 * time * 1.7 / max(_NormalTiling, 0.05)));
                    ripples = half3((a.xy + b.xy) * strength, a.z * b.z);
                }
                return ripples;
            }

            // Tangent-space ripples of steep water along the mesh UV: long streaks running down at the fall speed.
            half3 FallRipples(float2 uv, float speed, float time, half strength)
            {
                float2 q = uv / max(_FallTiling, 0.05);
                float run = speed * time / max(_FallTiling, 0.05);
                half3 a = SampleRipple(float2(q.x, q.y * 0.3 - frac(run * 0.3)));
                half3 b = SampleRipple(float2(q.x * 1.6 + 0.37, q.y * 0.55 - frac(run * 0.55 * 1.3)));
                return half3((a.xy + b.xy) * strength, a.z * b.z);
            }

            // Rain: rings spreading from random drops, a few per square metre (gradient of the ring height, in XZ).
            float2 RainRings(float2 xz, float time, float rain)
            {
                float2 gradient = 0;
                [unroll] for (int layer = 0; layer < 2; layer++)
                {
                    float2 p = xz / 0.55 + layer * float2(0.37, 0.61);
                    float2 cell = floor(p);
                    float random = Hash21(cell + layer * 13.7);
                    if (Hash21(cell + 91.3) > rain)
                    {
                        continue;
                    }
                    float age = frac(time * 0.9 + random);
                    float2 centre = cell + 0.25 + 0.5 * float2(Hash21(cell + 3.1), Hash21(cell + 7.7));
                    float2 d = p - centre;
                    float r = length(d) + 1e-4;
                    float x = (r - age * 0.45) / 0.07;
                    float ring = saturate(1.0 - abs(x)) * (1.0 - age) * (1.0 - age);
                    gradient += d / r * cos(x * 3.1416) * ring;
                }
                return gradient;
            }

            float3 SceneWorldPosition(float2 uv, out float raw)
            {
                raw = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
                return ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
            }

            // GGX sun glint (URP's minimal Cook-Torrance), water F0 = 0.02.
            half3 Glint(half3 normal, half3 view, Light light, half roughness)
            {
                half3 h = SafeNormalize(light.direction + view);
                half nh = saturate(dot(normal, h));
                half lh = saturate(dot(light.direction, h));
                half r2 = roughness * roughness;
                half d = nh * nh * (r2 - 1.0) + 1.00001;
                half term = r2 / ((d * d) * max(0.1, lh * lh) * (roughness * 4.0 + 2.0));
                half nl = saturate(dot(normal, light.direction));
                return min(term * 0.02 * nl, 40.0) * light.color * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _Time.y;
                float3 positionWS = input.positionWS;
                half3 surfaceNormal = normalize(input.normalWS);
                if (_SteepFaces < 0.5 && surfaceNormal.y < 0.5)
                {
                    discard; // the sides and bottom of a box or slab used as a pond / river
                }
                half steep = _SteepFaces > 0.5 ? smoothstep(0.25, 0.75, 1.0 - abs(surfaceNormal.y)) : 0.0;
                float flatSpeed = length(input.flow);
                float speed = lerp(flatSpeed, _FallSpeed, steep);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Surface normal: flat ripples (wind chop, player / stone ripples, rain) and / or steep fall streaks.
                half wind = saturate(length(_SafeHavenWind.xy) > 0 ? _SafeHavenWind.z / 0.06 : 0);
                half strength = _NormalStrength * lerp(1.0, lerp(0.6, 1.8, wind), saturate(_WindRipples)) * (1.0 + saturate(speed / 4.0) * 0.6);
                half3 normal = surfaceNormal;
                half rippleFoam = 0;
                if (steep < 0.99)
                {
                    half3 ripples = FlatRipples(positionWS.xz, input.flow, time, strength);
                    float2 slope = 0;
                    if (_WaterRippleOn > 0.5)
                    {
                        float2 uv = (positionWS.xz - _WaterRippleRect.xy) * _WaterRippleRect.z;
                        float texel = _WaterRippleRect.w;
                        float edge = saturate(min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 12.0);
                        if (edge > 0)
                        {
                            float h = SAMPLE_TEXTURE2D(_WaterRippleTex, sampler_WaterRippleTex, uv).r;
                            float l = SAMPLE_TEXTURE2D(_WaterRippleTex, sampler_WaterRippleTex, uv - float2(texel, 0)).r;
                            float r = SAMPLE_TEXTURE2D(_WaterRippleTex, sampler_WaterRippleTex, uv + float2(texel, 0)).r;
                            float d = SAMPLE_TEXTURE2D(_WaterRippleTex, sampler_WaterRippleTex, uv - float2(0, texel)).r;
                            float u = SAMPLE_TEXTURE2D(_WaterRippleTex, sampler_WaterRippleTex, uv + float2(0, texel)).r;
                            slope += float2(r - l, u - d) * (_RippleStrength * 12.0 * edge);
                            rippleFoam = saturate(abs(h) * 1.5 - 0.35) * edge;
                        }
                    }
                    if (_SafeHavenRain > 0.01)
                    {
                        slope += RainRings(positionWS.xz, time, _SafeHavenRain) * (_RainRipples * 0.6);
                    }
                    ripples.xy -= slope;
                    half3 flatNormal = normalize(half3(ripples.x, ripples.z, ripples.y));
                    normal = flatNormal;
                }
                if (steep > 0.01)
                {
                    half3 streaks = FallRipples(input.uv, speed, time, strength * 1.3);
                    half3 tangent = normalize(input.tangentWS.xyz);
                    half3 bitangent = cross(surfaceNormal, tangent) * input.tangentWS.w;
                    half3 fallNormal = normalize(tangent * streaks.x + bitangent * streaks.y + surfaceNormal * streaks.z);
                    normal = normalize(lerp(normal, fallNormal, steep));
                }

                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half3 ambient = SampleSH(surfaceNormal);
                half3 light = sun.color * sun.shadowAttenuation * (0.35 + 0.65 * saturate(dot(surfaceNormal, sun.direction))) + ambient;
                half3 view = GetWorldSpaceNormalizeViewDir(positionWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                half roughness = max(PerceptualSmoothnessToRoughness(_Smoothness), 0.002);
                half3 glint = Glint(normal, view, sun, roughness);
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light extra = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                        light += extra.color * extra.distanceAttenuation * extra.shadowAttenuation * saturate(dot(surfaceNormal, extra.direction) * 0.65 + 0.35);
                        glint += Glint(normal, view, extra, roughness);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light extra = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                        light += extra.color * extra.distanceAttenuation * extra.shadowAttenuation * saturate(dot(surfaceNormal, extra.direction) * 0.65 + 0.35);
                        glint += Glint(normal, view, extra, roughness);
                    LIGHT_LOOP_END
                #endif

                // Water body: depth colour, the refracted bed seen through it (with caustics), murkier when fast.
                float clarity = max(_Clarity * lerp(1.0, 0.15, saturate(speed / 4.0) * _Murk) * (1.0 - steep * 0.6), 0.02);
                half alpha = input.alphaFog.x;
                half shore = 0, trail = 0;
                float shoreDepth = 1e3; // water depth for the shore foam (unknown in the simple look)
                half3 water;
                #if defined(SAFE_HAVEN_WATER_SIMPLE)
                    half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, 0.5) * light;
                    water = body;
                    alpha *= saturate(1.0 - exp(-_DepthDistance * 0.5 / clarity));
                #else
                    float raw;
                    float3 sceneWS = SceneWorldPosition(screenUV, raw);
                    float surfaceEye = LinearEyeDepth(positionWS, GetWorldToViewMatrix());
                    if (LinearEyeDepth(raw, _ZBufferParams) < surfaceEye - 0.02)
                    {
                        discard; // the ground or an object is in front of the water here
                    }
                    float vertical = max(positionWS.y - sceneWS.y, 0);
                    float2 refracted = screenUV + normal.xz * (_Refraction * saturate(vertical * 2.0));
                    float rawRefracted;
                    float3 bedWS = SceneWorldPosition(refracted, rawRefracted);
                    if (LinearEyeDepth(rawRefracted, _ZBufferParams) < surfaceEye) // something in front of the water
                    {
                        refracted = screenUV;
                        bedWS = sceneWS;
                    }
                    float bedDepth = max(positionWS.y - bedWS.y, 0);
                    float path = distance(positionWS, bedWS);
                    half3 bed = SampleSceneColor(refracted);
                    if (_Caustics > 0)
                    {
                        float2 c = bedWS.xz / max(_CausticsTiling, 0.05);
                        half c1 = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, c + time * float2(0.031, 0.022)).r;
                        half c2 = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, c * 0.83 + float2(0.5, 0.2) - time * float2(0.02, 0.034)).r;
                        half fade = saturate(bedDepth * 4.0) * (1.0 - saturate(bedDepth / max(_DepthDistance * 1.5, 0.01)));
                        half caustic = saturate((c1 + c2) * 0.7 - 0.15); // two drifting networks, brighter where they cross
                        bed *= 1.0 + caustic * _Caustics * 2.0 * fade * sun.color * sun.shadowAttenuation;
                    }
                    half transmittance = exp(-path / clarity);
                    half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(bedDepth / max(_DepthDistance, 0.01))) * light;
                    water = bed * transmittance + body * (1.0 - transmittance);
                    shoreDepth = vertical;
                    // Foam trails: a stone close to the surface a little upstream leaves foam downstream.
                    if (_FoamTrail > 0.01 && flatSpeed > 0.05 && steep < 0.5)
                    {
                        float3 upstreamDirection = float3(input.flow.x, 0, input.flow.y) / flatSpeed;
                        [unroll] for (int i = 1; i <= 3; i++)
                        {
                            float3 upstream = positionWS - upstreamDirection * (_FoamTrail * i / 3.0);
                            float4 clip = TransformWorldToHClip(upstream);
                            float4 screen = ComputeScreenPos(clip);
                            float rawUp;
                            float3 sceneUp = SceneWorldPosition(screen.xy / screen.w, rawUp);
                            if (LinearEyeDepth(rawUp, _ZBufferParams) > clip.w - 0.6) // the bed, not something in front
                            {
                                // Only an obstacle shallower than the water here (a stone), not a shore running alongside.
                                float upstreamDepth = upstream.y - sceneUp.y;
                                half near = 1.0 - saturate(upstreamDepth / max(_ShoreFoam, 0.01));
                                half obstacle = saturate((vertical - upstreamDepth) / max(_ShoreFoam, 0.01));
                                trail = max(trail, near * obstacle * (1.0 - (i - 1) * 0.3));
                            }
                        }
                    }
                #endif
                half white = saturate(steep * _FallFoam * 0.6 + saturate((speed - 1.0) / 4.0) * _SpeedFoam * 0.4);
                water = lerp(water, _FastColor.rgb * light, white);

                // Foam: pattern scrolling with the water, revealed by the foam mask.
                float2 foamUV = positionWS.xz / max(_FoamTiling, 0.05) - frac(input.flow * time / max(_FoamTiling, 0.05));
                half2 foamFlat = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, foamUV).rg;
                if (_FlowFromMesh > 0.5) // two cross-faded phases, as in FlatRipples
                {
                    float cycle = 2.4;
                    float t0 = frac(time / cycle);
                    float t1 = frac(time / cycle + 0.5);
                    float2 foamP = positionWS.xz / max(_FoamTiling, 0.05);
                    float2 foamDrift = input.flow * cycle / max(_FoamTiling, 0.05);
                    half2 f0 = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, foamP - foamDrift * t0).rg;
                    half2 f1 = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, foamP - foamDrift * t1 + 0.5).rg;
                    foamFlat = lerp(f1, f0, 1.0 - abs(1.0 - 2.0 * t0));
                }
                half pattern = foamFlat.r;
                shore = 1.0 - saturate(shoreDepth / max(_ShoreFoam * lerp(0.3, 1.6, foamFlat.g), 0.01)); // uneven foam line
                if (steep > 0.01)
                {
                    float2 fallUV = input.uv / max(_FallTiling, 0.05);
                    half fall = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, float2(fallUV.x, fallUV.y * 0.15 - frac(speed * time * 0.15 / max(_FallTiling, 0.05)))).r;
                    pattern = lerp(pattern, fall, steep);
                }
                half spots = 0;
                int count = (int)min(_WaterFoamSpotCount, WATER_MAX_FOAM_SPOTS);
                for (int s = 0; s < count; s++)
                {
                    float4 spot = _WaterFoamSpots[s];
                    half near = saturate(1.0 - distance(positionWS.xz, spot.xy) / max(spot.z, 1e-3));
                    spots += near * near * spot.w;
                }
                half mask = saturate(shore * shore * shore * 0.5 + trail * 0.7 + spots + rippleFoam
                                     + _SpeedFoam * saturate((flatSpeed - 1.0) / 3.0) * 0.45 * (1.0 - steep)
                                     + _FallFoam * steep * 0.9);
                mask *= lerp(0.75, 1.25, foamFlat.g); // break up the edges
                half foam = smoothstep(1.0 - mask, 1.0 - mask + 0.2, pattern) * saturate(mask * 3.0);

                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normal, view)), 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-view, normal), positionWS, 1.0 - _Smoothness, 1.0, screenUV);
                half3 colour = lerp(water, reflection, fresnel * _Reflection * (1.0 - white * 0.5)) + glint * (1.0 - white);
                half3 foamLight = sun.color * sun.shadowAttenuation * saturate(dot(surfaceNormal, sun.direction)) * 0.8 + ambient;
                colour = lerp(colour, _FoamColor.rgb * foamLight, foam);
                #if defined(SAFE_HAVEN_WATER_SIMPLE)
                    alpha = saturate(alpha + foam + fresnel * 0.5);
                #endif
                if (_IceAmount > 0.001)
                {
                    // Pale, cloudy ice with darker water seen through it in places, fine cracks, and snow drifted on it.
                    float2 icy = positionWS.xz;
                    half cloud = SeasonSnowNoise(icy * 0.35) * 0.6 + SeasonSnowNoise(icy * 1.7) * 0.4;
                    half3 iceLight = sun.color * sun.shadowAttenuation * saturate(sun.direction.y) * 0.7 + ambient * 1.3;
                    half3 ice = lerp(half3(0.18, 0.26, 0.32), half3(0.62, 0.72, 0.8), cloud) * iceLight;
                    ice *= 1.0 - 0.35 * (IceCracks(icy * 0.8) * 0.7 + IceCracks(icy * 2.6) * 0.3);
                    half drift = smoothstep(0.45, 0.75, cloud + (_SafeHavenSnow - 0.5) * 0.6) * _SafeHavenSnow;
                    ice = lerp(ice, half3(0.82, 0.86, 0.92) * iceLight, drift);
                    colour = lerp(colour, ice + reflection * 0.08, _IceAmount);
                    alpha = lerp(alpha, 0.96, _IceAmount);
                }

                colour = MixFog(colour, input.alphaFog.y);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
