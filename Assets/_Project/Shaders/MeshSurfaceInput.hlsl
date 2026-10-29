#ifndef SAFE_HAVEN_MESH_SURFACE_INPUT
#define SAFE_HAVEN_MESH_SURFACE_INPUT
// Supply the material contract used by URP's stock lighting passes.
#define UNIVERSAL_LIT_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _BaseColor;
float4 _EmissionColor;
float4 _HeightChannel;
float4 _HeightMap_TexelSize;
float _Metallic;
float _Smoothness;
float _BumpScale;
float _OcclusionStrength;
float _Cutoff;
float _HeightStrength;
float _HeightMidpoint;
float _HeightMip;
float _HeightNormalStrength;
float _TessellationFactor;
float _TessellationEdgeLength;
float _TessellationNear;
float _TessellationFar;
float _AlphaClip;
float _Cull;
float _ReceiveShadows;
float _SpecularHighlights;
float _EnvironmentReflections;
CBUFFER_END

TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    half4 base = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    surface.alpha = Alpha(base.a, _BaseColor, _Cutoff);
    surface.albedo = base.rgb * _BaseColor.rgb;
    half4 metal = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    surface.metallic = metal.r * _Metallic;
    surface.smoothness = metal.a * _Smoothness;
    surface.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surface.occlusion = lerp(1, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g, _OcclusionStrength);
    surface.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
}
#endif
