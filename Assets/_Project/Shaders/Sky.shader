// Safe Haven: the sky (a skybox material, put on every scene by SkyDome). Seldom seen straight from the top-down camera,
// but every reflection shows it: water, puddles, wet stone and metal reflect it through the follow-the-player probe.
// - A day gradient that melts into the fog at the horizon, warm light round a low sun, a night sky of stars and a band
//   of the galaxy, and a moon with its phase.
// - Clouds: the same pattern, cover and drift as the cloud shadows on the ground (CloudNoise.hlsl, CloudLayer), lit by
//   the sun or moon through their own thickness; heavy weather closes them into a grey sheet, and lightning lights them.
// Globals from SkyDome and CloudLayer; colours on the material (Resources/Sky).
Shader "Safe Haven/Sky"
{
    Properties
    {
        [Header(Day)]
        _DayZenith ("Day Zenith", Color) = (0.22, 0.42, 0.78, 1)
        _DayHorizon ("Day Horizon", Color) = (0.68, 0.79, 0.9, 1)
        _DayBrightness ("Day Brightness", Range(0, 4)) = 1.35
        _SunsetColour ("Sunset Glow", Color) = (1.0, 0.45, 0.18, 1)
        _SunsetStrength ("Sunset Strength", Range(0, 4)) = 1.6
        _SunSize ("Sun Size (degrees)", Range(0.2, 5)) = 1.4
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.35
        [Header(Night)]
        _NightZenith ("Night Zenith", Color) = (0.012, 0.02, 0.05, 1)
        _NightHorizon ("Night Horizon", Color) = (0.035, 0.05, 0.085, 1)
        _StarDensity ("Star Density", Range(40, 400)) = 170
        _StarBrightness ("Star Brightness", Range(0, 12)) = 6
        _GalaxyBrightness ("Galaxy Band", Range(0, 1)) = 0.18
        _MoonColour ("Moon Colour", Color) = (0.9, 0.93, 1.0, 1)
        _MoonBrightness ("Moon Brightness", Range(0, 8)) = 2.2
        _MoonSize ("Moon Size (degrees)", Range(0.5, 10)) = 3.2
        [Header(Clouds)]
        _CloudScale ("Cloud Scale", Range(0.05, 2)) = 0.32
        _CloudSoftness ("Cloud Softness", Range(0.02, 0.5)) = 0.2
        _CloudLit ("Lit Side", Color) = (1.0, 0.98, 0.95, 1)
        _CloudShade ("Shaded Side", Color) = (0.46, 0.52, 0.6, 1)
        _OvercastColour ("Overcast", Color) = (0.5, 0.53, 0.57, 1)
        _StormColour ("Storm", Color) = (0.2, 0.22, 0.25, 1)
        _LightningColour ("Lightning", Color) = (0.75, 0.82, 1.0, 1)
        [Header(Horizon)]
        _HorizonFog ("Horizon Takes The Fog", Range(0, 1)) = 0.75
        _GroundColour ("Below The Horizon", Color) = (0.18, 0.2, 0.17, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CloudNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DayZenith, _DayHorizon, _SunsetColour, _NightZenith, _NightHorizon, _MoonColour;
                half4 _CloudLit, _CloudShade, _OvercastColour, _StormColour, _LightningColour, _GroundColour;
                float _DayBrightness, _SunsetStrength, _SunSize, _SunGlow, _StarDensity, _StarBrightness, _GalaxyBrightness;
                float _MoonBrightness, _MoonSize, _CloudScale, _CloudSoftness, _HorizonFog;
            CBUFFER_END

            // SkyDome: toward the sun and moon (below the horizon when down), the moon's phase (0 new, 0.5 full).
            float4 _SkySunDirection;
            float4 _SkyMoonDirection;
            float4 _SkySunColour;
            // x daylight, y night, z golden hour, w lightning flash.
            float4 _SkyState;
            // CloudLayer: x the weather's cloud (0-1), y cover share, z overcast (the sheet closing), w storm darkness.
            float4 _SkyClouds;
            // CloudLayer: the drift, in tiles (the cloud shadows' cookie offset over its size).
            float4 _SafeHavenCloudOffset;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // Points of light: one chance in a few cells holds a star, of its own brightness and twinkle.
            half Stars(float3 direction)
            {
                float3 p = direction * _StarDensity;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;
                float3 jitter = Hash33(cell) - 0.5;
                float chance = Hash31(cell + 17.0);
                float present = step(0.86, chance);
                float size = lerp(0.05, 0.16, pow(saturate((chance - 0.86) / 0.14), 3.0));
                float light = smoothstep(size, 0.0, length(local - jitter * 0.6));
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + chance * 4.0) + chance * 60.0);
                return light * present * twinkle * lerp(0.3, 1.0, chance);
            }

            half Galaxy(float3 direction)
            {
                // Mapped on the band's own plane, so there is no seam or pinch anywhere on the sphere.
                float3 axis = normalize(float3(0.35, 0.25, 0.9));
                float3 along = normalize(cross(axis, float3(0, 1, 0)));
                float3 other = cross(axis, along);
                float across = dot(direction, axis);
                float band = exp(-across * across * 28.0);
                float2 uv = float2(dot(direction, along), dot(direction, other)) * 1.6 + across * 0.7;
                float dust = CloudDensity(uv + 0.37, 4);
                return band * smoothstep(0.35, 0.75, dust);
            }

            // The moon's disc lit by its phase; 0 outside it.
            half4 Moon(float3 direction, float3 moon, float phase)
            {
                float radius = sin(radians(_MoonSize) * 0.5);
                float3 right = normalize(cross(abs(moon.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0), moon));
                float3 up = cross(moon, right);
                float2 p = float2(dot(direction, right), dot(direction, up)) / radius;
                float r2 = dot(p, p);
                if (dot(direction, moon) <= 0.0 || r2 >= 1.2)
                {
                    return 0;
                }

                float3 normal = float3(p, sqrt(saturate(1.0 - r2)));
                float angle = phase * 6.2831853;
                float3 lightDir = float3(sin(angle), 0.0, -cos(angle));
                float lit = smoothstep(-0.06, 0.12, dot(normal, lightDir));
                float maria = CloudDensity(p * 0.35 + 0.5, 3);
                half3 colour = _MoonColour.rgb * _MoonBrightness * (lit * lerp(0.55, 1.0, smoothstep(0.35, 0.65, maria)) + 0.015);
                float edge = 1.0 - smoothstep(0.92, 1.0, sqrt(r2));
                return half4(colour, edge);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 sun = normalize(_SkySunDirection.xyz);
                float3 moon = normalize(_SkyMoonDirection.xyz);
                float daylight = _SkyState.x;
                float night = _SkyState.y;
                float flash = _SkyState.w;
                float up = direction.y;
                float height = saturate(up);

                // The gradient, day and night, and the glow round a low sun.
                half3 day = lerp(_DayHorizon.rgb, _DayZenith.rgb, pow(height, 0.45)) * _DayBrightness;
                half3 dark = lerp(_NightHorizon.rgb, _NightZenith.rgb, pow(height, 0.6));
                half3 sky = lerp(dark, day, daylight);
                float towardSun = saturate(dot(direction, sun) * 0.5 + 0.5);
                float lowSun = (1.0 - saturate(abs(sun.y) / 0.4)) * smoothstep(-0.3, 0.0, sun.y);
                float glow = max(_SkyState.z, lowSun);
                sky += _SunsetColour.rgb * _SunsetStrength * glow * pow(towardSun, 4.0) * pow(1.0 - height, 3.0);
                sky = lerp(sky, sky * lerp(1.0, 0.55, glow), saturate(1.0 - towardSun) * glow * 0.6); // the far side dims

                // Night: stars and the galaxy, faint at dusk.
                half3 heavens = (Stars(direction) * _StarBrightness + Galaxy(direction) * _GalaxyBrightness * half3(0.75, 0.8, 1.0))
                              * night * smoothstep(0.0, 0.2, up);
                // The sun and the moon.
                float cosSun = dot(direction, sun);
                float sunUp = smoothstep(-0.03, 0.02, sun.y);
                float discCos = cos(radians(_SunSize) * 0.5);
                half3 sunLight = _SkySunColour.rgb * sunUp * (smoothstep(discCos, lerp(discCos, 1.0, 0.25), cosSun) * 40.0
                               + pow(saturate(cosSun), 12.0) * _SunGlow + pow(saturate(cosSun), 300.0) * 2.0);
                half4 moonDisc = Moon(direction, moon, _SkyMoonDirection.w);
                float moonUp = smoothstep(-0.02, 0.04, moon.y);
                half moonHalo = pow(saturate(dot(direction, moon)), 400.0) * 0.25 * night * moonUp;
                half3 bodies = heavens + sunLight + moonDisc.rgb * moonDisc.a * moonUp * lerp(1.0, 0.35, daylight)
                             + _MoonColour.rgb * moonHalo;
                half3 behind = lerp(heavens, 0, moonDisc.a * moonUp); // the moon hides the stars behind it
                bodies = bodies - heavens + behind;

                // Clouds, on a ceiling: the cloud shadows' pattern, cover and drift.
                float cloud = 0.0;
                half3 cloudColour = 0;
                float overcast = _SkyClouds.z;
                if (up > 0.0)
                {
                    float2 uv = direction.xz / (up + 0.08) * _CloudScale + _SafeHavenCloudOffset.xy;
                    float density = CloudDensity(uv, up > 0.25 ? 6 : 4);
                    float edge = lerp(0.68, 0.38, _SkyClouds.y);
                    cloud = CloudMask(density, _SkyClouds.y, _CloudSoftness);
                    // Lit from the sun or moon: thick cores and the side away from the light are in shade.
                    float3 light = sun.y > -0.05 ? sun : moon;
                    float toward = CloudDensity(uv + normalize(light.xz + 1e-4) * 0.03, 4);
                    float thickness = saturate((density - edge) * 3.0);
                    float lit = saturate(0.9 - thickness * 0.5 - max(0.0, toward - density) * 2.5);
                    float facing = pow(towardSun, 2.0);
                    float silver = pow(saturate(dot(direction, light)), 6.0) * (1.0 - thickness) * 1.2;
                    half3 litColour = lerp(_CloudLit.rgb * _SkySunColour.rgb * 1.25, _MoonColour.rgb * 0.07, night);
                    litColour = lerp(litColour, litColour * _SunsetColour.rgb * 1.6, glow * (0.25 + 0.75 * facing));
                    half3 shade = _CloudShade.rgb * lerp(day, dark * 3.0, night) * lerp(1.0, 0.7, glow * (1.0 - facing));
                    cloudColour = lerp(shade, litColour, lit) + litColour * silver;
                    // Heavy weather closes the gaps into one grey sheet, darker in a storm.
                    half3 sheet = lerp(_OvercastColour.rgb, _StormColour.rgb, _SkyClouds.w) * lerp(0.04, 1.0, daylight) * _DayBrightness;
                    // Its undersides keep shape: heavier lumps darker, thinner places lighter, more so in a storm.
                    float lumps = smoothstep(0.3, 0.72, density);
                    half3 underside = sheet * lerp(lerp(1.3, 1.5, _SkyClouds.w), lerp(0.62, 0.42, _SkyClouds.w), lumps);
                    cloudColour = lerp(cloudColour, underside, overcast);
                    cloudColour += _LightningColour.rgb * flash * 3.0 * (0.5 + density);
                    cloud *= smoothstep(0.0, 0.12, up);
                    cloud = lerp(cloud, 1.0, overcast); // a closed sheet reaches the horizon
                }

                half3 colour = lerp(sky, cloudColour, cloud) + bodies * (1.0 - cloud);
                // A covered sky's light: the haze greys toward the sheet even between the clouds.
                colour = lerp(colour, colour * 0.6 + _OvercastColour.rgb * daylight * 0.4, overcast * 0.5);
                // The horizon melts into the fog, so distant ground and sky meet without a seam; below it, dim ground.
                float horizon = pow(1.0 - saturate(abs(up) * 3.0), 3.0);
                colour = lerp(colour, unity_FogColor.rgb, horizon * _HorizonFog);
                if (up < 0.0)
                {
                    half3 ground = _GroundColour.rgb * lerp(0.05, 1.0, daylight) + unity_FogColor.rgb * 0.3;
                    colour = lerp(colour, ground, smoothstep(0.0, 0.25, -up));
                }

                return half4(max(colour, 0), 1.0);
            }
            ENDHLSL
        }
    }
}
