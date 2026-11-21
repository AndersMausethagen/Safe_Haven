#ifndef SAFE_HAVEN_PROBE_GI
#define SAFE_HAVEN_PROBE_GI
// The ambient light a pixel gets: from Adaptive Probe Volumes where a scene has them baked (the boss arenas, 2026-11-21: soft light
// from the land itself, sky occlusion, one scenario per phase), else from the ambient probe as before. URP's own lit shaders do this
// through SAMPLE_GI; our hand-written ones called SampleSH and stayed flat where probes were baked. Without a bake
// (_EnableProbeVolumes off) URP's SampleProbeVolumePixel gives the ambient probe, so every unbaked scene looks exactly as it did.
// Include after Lighting.hlsl, in a pass that has ProbeVolumeVariants.hlsl's keywords (include_with_pragmas).
half3 SafeHavenAmbient(float3 positionWS, half3 normalWS, float4 positionCS)
{
#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    return SampleProbeVolumePixel(half3(0, 0, 0), GetAbsolutePositionWS(positionWS), normalWS, viewDirWS, positionCS.xy);
#else
    return SampleSH(normalWS);
#endif
}
#endif
