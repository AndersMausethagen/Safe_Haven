// Terrain grass and bushes painted with the terrain's Paint Details brush (GPU-instanced detail meshes, see
// Editor/Environment/GrassSetup). Vertices bend with the scene's MossWind (_SafeHavenWind, _SafeHavenWindGusts,
// _SafeHavenTreeWind) and away from GrassInteractor objects (the player...): _GrassInteractors[i] = (position, radius).
// Vertex colour R is the bend weight (0 at the root, 1 at the tip) and G a per-blade random value; the generated
// meshes set both. Colour runs from _BottomColor to _TopColor along the blade, takes on the ground's colour (the
// terrain colour map GrassTerrainColour bakes) and varies by clumps and dry patches. Far away (camera distance)
// blades thin out, stop animating and shrink to nothing at the draw distance, so the field stays cheap.
// Glowing plants (mushrooms, flowers) set vertex colour B where they glow; _GlowColor shines there at night
// (_SafeHavenNight from DayNightCycle). Around WitherSpots (_SafeHavenWither) the blades turn dead brown.
Shader "Safe Haven/Grass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture (alpha = shape)", 2D) = "white" {}
        [Toggle] _FacingBlades ("Blades Face the Camera (grass made by Grass Setup)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        [Header(Colour)]
        _BottomColor ("Root Colour", Color) = (0.11, 0.2, 0.05, 1)
        _TopColor ("Tip Colour", Color) = (0.34, 0.48, 0.16, 1)
        _DryColor ("Dry Patch Colour", Color) = (0.55, 0.52, 0.22, 1)
        _DryAmount ("Dry Patches", Range(0, 1)) = 0.2
        _PatchScale ("Patch Size (metres)", Float) = 6
        _BladeVariation ("Per-Blade Brightness Variation", Range(0, 1)) = 0.3
        _GroundColor ("Take Ground Colour", Range(0, 1)) = 0.6
        _GroundTip ("Ground Colour at the Tips", Range(0, 1)) = 0.3
        _GroundBrightness ("Ground Colour Brightness", Range(0.5, 2)) = 1.1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _Translucency ("Translucency (backlight)", Range(0, 1)) = 0.5
        _NormalUp ("Soft Lighting (normals point up)", Range(0, 1)) = 0.6
        [Header(Size)]
        _Size ("Size", Range(0.2, 3)) = 1
        _ClumpScale ("Clump Size (metres)", Float) = 1.5
        _ClumpHeight ("Clump Height Variation", Range(0, 1)) = 0.35
        [Header(Wind)]
        _WindStrength ("Wind Bend (metres at the tip)", Range(0, 1)) = 0.12
        _WindSpeed ("Sway Speed", Range(0, 4)) = 1
        _Flutter ("Flutter", Range(0, 1)) = 0.3
        _WindWaves ("Wind Waves (travelling gusts)", Range(0, 1)) = 0.6
        [Header(Interaction)]
        _PushStrength ("Push Away (metres)", Range(0, 1)) = 0.3
        _Flatten ("Flatten", Range(0, 1)) = 0.6
        [Header(Distance)]
        _FarStart ("Thinning Starts (metres)", Float) = 20
        _FarEnd ("Draw Distance (metres; the terrain's Detail Distance)", Float) = 50
        _FarDensity ("Density at the Draw Distance", Range(0.05, 1)) = 0.35
        _AnimationDistance ("Animated Up To (metres)", Float) = 45
        [Toggle(_BAKED_ROOTS)] _BakedRoots ("Merged Clumps (roots in UV1; roof grass)", Float) = 0
        [Header(Night Glow)]
        [HDR] _GlowColor ("Glow Colour (at night, where vertex colour B is set)", Color) = (0, 0, 0, 1)
        _GlowPulse ("Glow Pulse", Range(0, 1)) = 0.4
        _GlowSpeed ("Pulse Speed", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Shaders/SurfaceWetness.hlsl"
        #define GRASS_MAX_INTERACTORS 8
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BottomColor, _TopColor, _DryColor;
            half _Cutoff, _DryAmount, _BladeVariation, _Smoothness, _Translucency, _NormalUp;
            half _GroundColor, _GroundTip, _GroundBrightness;
            float _PatchScale, _Size, _WindStrength, _WindSpeed, _Flutter, _PushStrength, _Flatten, _WindWaves;
            float _ClumpScale, _ClumpHeight, _FarStart, _FarEnd, _FarDensity, _AnimationDistance;
            half4 _GlowColor;
            float _GlowPulse, _GlowSpeed, _FacingBlades;
        CBUFFER_END
        float _SafeHavenNight; // 0 by day, 1 at night (DayNightCycle)
        // Ground colour of the terrain under the grass (GrassTerrainColour): world XZ -> UV = (xz - rect.xy) * rect.zw.
        // Alpha 0 marks water (WaterSurface), where grass is hidden.
        TEXTURE2D(_GrassTerrainColor); SAMPLER(sampler_GrassTerrainColor);
        float4 _GrassTerrainRect;
        float _GrassTerrainColorOn;
        float4 _SafeHavenWind; // horizontal direction XZ (world), moss displacement, speed
        float _SafeHavenWindGusts, _SafeHavenTreeWind;
        float4 _GrassInteractors[GRASS_MAX_INTERACTORS]; // world position, radius
        // Withered ground (WitherSpot, from story graphs): world XZ, radius, how withered (0-1).
        float4 _SafeHavenWither[4];
        float _SafeHavenWitherCount;
        float4 _SafeHavenSeason; // Seasons: x autumn (the grass yellows), z frost
        float _SafeHavenSnow; // Snow lying on the ground (WeatherSystem): sinks the blades into it and whitens their feet
        float _GrassInteractorCount;
        TEXTURE2D(_GrassFootprint0); SAMPLER(sampler_GrassFootprint0);
        TEXTURE2D(_GrassFootprint1);
        TEXTURE2D(_GrassFootprint2);
        TEXTURE2D(_GrassFootprint3);
        float4 _GrassFootprintAreas[4];
        float4 _GrassFootprintDarkening;
        float4 _GrassFootprintDepths;
        float _GrassFootprintCount;
        // Heavy blows (GroundImpacts): bursts of wind rolling out through the grass, and the marks they leave (the
        // impact field: R = ground pressed down in metres, B = grass pressed down or cut, 0-1).
        #define GROUND_MAX_GUSTS 6
        float4 _GroundGusts[GROUND_MAX_GUSTS];      // x, z, start time, strength (metres at the tip)
        float4 _GroundGustShapes[GROUND_MAX_GUSTS]; // radius, speed, cos of half the arc (-2: all round), heading
        float _GroundGustCount;
        TEXTURE2D(_GroundImpactField); SAMPLER(sampler_GroundImpactField);
        float4 _GroundImpactArea; // origin x, z, 1 / metres, on

        float2 GroundImpact(float2 worldXZ)
        {
            if (_GroundImpactArea.w <= 0) return 0;
            float2 uv = (worldXZ - _GroundImpactArea.xy) * _GroundImpactArea.z;
            if (any(uv < 0) || any(uv > 1)) return 0;
            float4 mark = SAMPLE_TEXTURE2D_LOD(_GroundImpactField, sampler_GroundImpactField, uv, 0);
            return float2(mark.r, saturate(mark.b));
        }

        float3 GrassFootprint(float2 worldXZ)
        {
            float3 pressed = 0;
            for (int i = 0; i < (int)min(_GrassFootprintCount, 4); i++)
            {
                float4 area = _GrassFootprintAreas[i];
                float2 uv = (worldXZ - area.xy) * area.z;
                if (area.w <= 0 || any(uv < 0) || any(uv > 1)) continue;
                float mask = 0;
                if (i == 0) mask = SAMPLE_TEXTURE2D_LOD(_GrassFootprint0, sampler_GrassFootprint0, uv, 0).r;
                else if (i == 1) mask = SAMPLE_TEXTURE2D_LOD(_GrassFootprint1, sampler_GrassFootprint0, uv, 0).r;
                else if (i == 2) mask = SAMPLE_TEXTURE2D_LOD(_GrassFootprint2, sampler_GrassFootprint0, uv, 0).r;
                else mask = SAMPLE_TEXTURE2D_LOD(_GrassFootprint3, sampler_GrassFootprint0, uv, 0).r;
                // Plants bend under the whole sole, including the shallow tread grooves.
                pressed.x = max(pressed.x, saturate(mask * 4) * area.w);
                pressed.y = max(pressed.y, mask * area.w * _GrassFootprintDarkening[i]);
                pressed.z = max(pressed.z, saturate(mask * 4) * area.w * _GrassFootprintDepths[i]);
            }
            return float3(saturate(pressed.xy), pressed.z);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            #if defined(_BAKED_ROOTS)
            float3 root : TEXCOORD1; // the clump's root in object space (see _BAKED_ROOTS below)
            #endif
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half3 tint : TEXCOORD3;
            half fog : TEXCOORD4;
            half glow : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }
        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                        lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
        }

        // How the camera distance changes a blade: kept (0 = removed when the field thins out), scale, animated.
        void DistanceLod(float3 rootWS, half random, out float keep, out float scale, out float animate)
        {
            float distance = length(rootWS - _WorldSpaceCameraPos);
            float far = saturate((distance - _FarStart) / max(_FarEnd - _FarStart, 1e-3));
            keep = random <= lerp(1.0, _FarDensity, far) ? 1.0 : 0.0;
            scale = 1.0 - smoothstep(0.85, 1.0, distance / max(_FarEnd, 1e-3)); // shrink instead of popping out
            animate = 1.0 - saturate((distance - _AnimationDistance) / 6.0);
        }

        // Bent world position of a vertex: clump height, wind sway (stronger at the tip, in travelling gust waves)
        // and interactors pushing it away. Far blades skip all of it.
        // _BAKED_ROOTS: many clumps merged into one mesh (the grass on a sod roof), each vertex carrying its clump's
        // root in TEXCOORD1. A terrain detail is one clump per instance, rooted at the instance's origin.
        float3 GrassPosition(Attributes input, out float3 rootWS)
        {
            #if defined(_BAKED_ROOTS)
            float3 rootOS = input.root;
            #else
            float3 rootOS = float3(0, 0, 0);
            #endif
            float3 bladeOS = input.positionOS.xyz - rootOS;
            rootWS = TransformObjectToWorld(rootOS);
            float keep, scale, animate;
            DistanceLod(rootWS, input.color.g, keep, scale, animate);
            if (_GrassTerrainColorOn > 0.5) // the colour map's alpha is 0 under water: nothing grows there
            {
                float2 mapUV = (rootWS.xz - _GrassTerrainRect.xy) * _GrassTerrainRect.zw;
                keep *= step(0.5, SAMPLE_TEXTURE2D_LOD(_GrassTerrainColor, sampler_GrassTerrainColor, mapUV, 0).a);
            }
            float clump = Noise(rootWS.xz / max(_ClumpScale, 0.05));
            float3 positionOS = bladeOS * (_Size * scale * keep);
            positionOS.y *= 1.0 + (clump * 2.0 - 1.0) * _ClumpHeight;
            positionOS.y *= 1.0 - 0.55 * _SafeHavenSnow;
            float3 world = TransformObjectToWorld(rootOS + positionOS);
            if (_FacingBlades > 0.5)
            {
                // Each blade turns around its own axis to show its full width to the camera (an axial billboard).
                // Done in world space: instanced terrain details have no reliable world-to-object matrix.
                // Mesh (BuildClump): normal = along the blade, colour A = half-width / 0.05 m, UV0.x = side (0 or 1).
                float3 axisWS = normalize(TransformObjectToWorldDir(input.normalOS));
                float3 sideWS = cross(axisWS, world - _WorldSpaceCameraPos);
                sideWS = dot(sideWS, sideWS) > 1e-8 ? normalize(sideWS) : float3(1, 0, 0);
                float instanceScale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float halfWidth = input.color.a * 0.05 * (input.uv.x * 2.0 - 1.0);
                world += sideWS * (halfWidth * instanceScale * _Size * scale * keep);
            }
            #if defined(_BAKED_ROOTS)
            float2 footprint = float2(0, 0); // footprints are a map of the ground: a roof over a path is not trodden
            #else
            float2 footprint = GrassFootprint(world.xz).xz;
            #endif
            #if defined(_BAKED_ROOTS)
            float2 impact = float2(0, 0);
            #else
            float2 impact = GroundImpact(rootWS.xz);
            #endif
            if (animate <= 0.0 || keep <= 0.0)
            {
                world.y = lerp(world.y, min(world.y, rootWS.y + 0.01), footprint.x) - footprint.y;
                world.y = lerp(world.y, rootWS.y + (world.y - rootWS.y) * 0.2, impact.y) - impact.x;
                return world;
            }
            float bend = input.color.r * input.color.r * animate;
            float2 direction = _SafeHavenWind.xy;
            float windy = dot(direction, direction) > 1e-4 ? saturate(max(_SafeHavenTreeWind, 0.0)) : 0.0;
            direction = windy > 0 ? normalize(direction) : float2(0.7071, 0.7071);
            float time = _Time.y * max(_SafeHavenWind.w, 0.5) * _WindSpeed;
            float phase = dot(rootWS.xz, direction) * 0.35 + input.color.g * 6.2831;
            float gust = 1.0 - _SafeHavenWindGusts * (0.5 + 0.5 * sin(time * 0.63 + rootWS.x * 0.08));
            float wave = Noise(rootWS.xz * 0.08 - direction * time * 0.4); // gusts rolling over the field
            gust *= lerp(1.0, 0.3 + 1.4 * wave, _WindWaves);
            float sway = (0.55 + 0.45 * sin(time * 1.3 - phase)) * gust;
            float flutter = sin(time * 7.0 + phase * 3.1) * _Flutter * 0.25;
            // A little life without a MossWind; stronger than its usual 1 in storms (WeatherSystem).
            float amount = _WindStrength * _Size * lerp(0.15, 1.0, windy) * clamp(_SafeHavenTreeWind, 1.0, 4.0);
            world.xz += (direction * sway + float2(-direction.y, direction.x) * flutter) * amount * bend;
            float height = max(bladeOS.y * _Size, 1e-3);
            int count = (int)min(_GrassInteractorCount, GRASS_MAX_INTERACTORS);
            for (int i = 0; i < count; i++)
            {
                float4 interactor = _GrassInteractors[i];
                float2 away = rootWS.xz - interactor.xz;
                float distance = length(away);
                float reach = saturate(1.0 - distance / max(interactor.w, 1e-3));
                reach *= step(abs(rootWS.y - interactor.y), interactor.w * 2.0 + height);
                if (reach > 0)
                {
                    float2 push = (distance > 1e-4 ? away / distance : direction) * reach * _PushStrength * bend;
                    world.xz += push;
                    world.y -= (_Flatten * reach * height * 0.6 + dot(push, push) / (2.0 * height)) * input.color.r;
                }
            }
            // A heavy blow's wind: a wave rolling out from where it landed (or ahead along the swing), pressing the grass
            // over as it passes, then letting it spring back and sway a moment.
            int gusts = (int)min(_GroundGustCount, GROUND_MAX_GUSTS);
            for (int g = 0; g < gusts; g++)
            {
                float4 gustAt = _GroundGusts[g];
                float4 gustShape = _GroundGustShapes[g];
                float2 out_ = rootWS.xz - gustAt.xy;
                float far = length(out_);
                float since = _Time.y - gustAt.z - far / max(gustShape.y, 0.1);
                if (since <= 0 || far > gustShape.x || far < 1e-3) continue;
                out_ /= far;
                float arc = gustShape.z < -1.5 ? 1.0 : saturate((dot(out_, float2(sin(gustShape.w), cos(gustShape.w))) - gustShape.z) * 6.0);
                float response = saturate(since * 25.0) * exp(-since * 3.0) * (0.75 + 0.25 * cos(since * 13.0));
                float push = gustAt.w * arc * response * saturate(1.0 - far / gustShape.x) * bend;
                world.xz += out_ * push;
                world.y -= push * push / (2.0 * height) * input.color.r;
            }
            world.y = lerp(world.y, min(world.y, rootWS.y + 0.01), footprint.x) - footprint.y;
            // Pressed down or cut short where a blow landed, and sunk with the ground in a crater.
            world.y = lerp(world.y, rootWS.y + (world.y - rootWS.y) * 0.2, impact.y) - impact.x;
            return world;
        }

        half3 GrassColour(Attributes input, float3 rootWS)
        {
            half tip = saturate(input.color.r);
            half3 colour = lerp(_BottomColor.rgb, _TopColor.rgb, tip);
            if (_GrassTerrainColorOn > 0.5)
            {
                float2 uv = (rootWS.xz - _GrassTerrainRect.xy) * _GrassTerrainRect.zw;
                half3 ground = SAMPLE_TEXTURE2D_LOD(_GrassTerrainColor, sampler_GrassTerrainColor, uv, 0).rgb;
                ground *= _GroundBrightness * lerp(0.85, 1.15, tip); // lighter towards the tips, like the grass
                colour = lerp(colour, ground, _GroundColor * lerp(1.0, _GroundTip, tip));
            }
            half dry = smoothstep(1.0 - _DryAmount, 1.0 - _DryAmount + 0.25, Noise(rootWS.xz / max(_PatchScale, 0.01)));
            colour = lerp(colour, _DryColor.rgb * lerp(0.6, 1.0, tip), dry * saturate(tip + 0.2));
            for (int w = 0; w < (int)_SafeHavenWitherCount; w++)
            {
                float4 spot = _SafeHavenWither[w];
                float near = 1.0 - smoothstep(spot.z * 0.55, spot.z, distance(rootWS.xz, spot.xy));
                colour = lerp(colour, half3(0.33, 0.24, 0.12) * lerp(0.55, 1.0, tip), saturate(near * spot.w));
            }
            // The year: autumn yellows it, in patches; frost whitens the tips.
            half autumn = _SafeHavenSeason.x * smoothstep(0.2, 0.7, Noise(rootWS.xz / 6.0 + 31.0) + 0.25);
            colour = lerp(colour, half3(0.62, 0.52, 0.26) * lerp(0.65, 1.0, tip), saturate(autumn * 0.7));
            colour = lerp(colour, half3(0.8, 0.84, 0.88), saturate(_SafeHavenSeason.z * 0.6 * (0.4 + 0.6 * tip)));
            colour = lerp(colour, half3(0.82, 0.86, 0.92), saturate(_SafeHavenSnow * (1.0 - 0.45 * tip)));
            half clump = Noise(rootWS.xz / max(_ClumpScale, 0.05) + 17.0);
            #if !defined(_BAKED_ROOTS)
            colour *= 1 - GrassFootprint(rootWS.xz).y;
            #endif
            return colour * (1.0 + (input.color.g - 0.5) * 2.0 * _BladeVariation) * lerp(0.85, 1.12, clump);
        }

        Varyings GrassVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 rootWS;
            output.positionWS = GrassPosition(input, rootWS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            half3 normalWS = _FacingBlades > 0.5 ? half3(0, 1, 0) : TransformObjectToWorldNormal(input.normalOS);
            output.normalWS = normalize(lerp(normalWS, half3(0, 1, 0), _NormalUp));
            output.tint = GrassColour(input, rootWS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(output.positionCS.z);
            float pulse = 0.55 + 0.45 * sin(_Time.y * _GlowSpeed + input.color.g * 6.2831);
            output.glow = input.color.b * _SafeHavenNight * lerp(1.0, pulse, _GlowPulse);
            return output;
        }

        half4 GrassAlbedo(Varyings input)
        {
            half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            clip(texel.a - _Cutoff);
            return half4(texel.rgb * input.tint, 1);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_vertex _BAKED_ROOTS
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Forward+ reflections; without these, renderers drawn outside the SRP batcher go dark (see ARCHITECTURE.md).
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            half4 GrassFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 albedo = GrassAlbedo(input);
                half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                normal.y = abs(normal.y); // both sides of a blade face the sky
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normalize(normal);
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
                data.albedo = albedo.rgb;
                data.alpha = 1;
                data.smoothness = _Smoothness;
                data.occlusion = SkyAmbient(input.positionWS);
                data.normalTS = half3(0, 0, 1);
                SurfaceWet(data.albedo, data.smoothness, lighting.normalWS, input.positionWS, 0.5);
                half4 colour = UniversalFragmentPBR(lighting, data);
                // Sun shining through the blades when looking towards it.
                Light sun = GetMainLight(lighting.shadowCoord, input.positionWS, lighting.shadowMask);
                half back = pow(saturate(dot(lighting.viewDirectionWS, -sun.direction)), 4.0);
                colour.rgb += albedo.rgb * sun.color * back * _Translucency * sun.shadowAttenuation;
                colour.rgb += _GlowColor.rgb * input.glow; // glowing plants at night (bloom does the halo)
                colour.rgb = MixFog(colour.rgb, input.fog);
                return half4(colour.rgb, 1);
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
            #pragma shader_feature_local_vertex _BAKED_ROOTS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = GrassVertex(input);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - output.positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }
            half4 ShadowFragment(Varyings input) : SV_Target
            {
                GrassAlbedo(input);
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
            #pragma vertex GrassVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_vertex _BAKED_ROOTS
            half4 DepthFragment(Varyings input) : SV_Target
            {
                GrassAlbedo(input);
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
            #pragma vertex GrassVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_vertex _BAKED_ROOTS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(Varyings input) : SV_Target
            {
                GrassAlbedo(input);
                float3 normal = normalize(input.normalWS);
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
