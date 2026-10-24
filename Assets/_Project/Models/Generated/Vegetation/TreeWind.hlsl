// Tree wind for Vegetation-tab GLBs (Local Generator). Same formula as the in-app preview (viewer3d.py).
//
// Shader Graph: Custom Function node, Type = File, Source = this file, Name = TreeWind (float precision).
//   Inputs : PositionOS (Position, Object), NormalOS (Normal, Object), Wind (UV1), WindPhase (UV2),
//            Time (Time.Time), Strength (float, 0..1.5), TreeHeight (float, metres; recipe stats or mesh bounds),
//            WindDirOS (Vector3, object-space direction, e.g. (0.8, 0, 0.6))
//   Output : Out (Vector3) -> Vertex Position (Object) of the Master Stack.
//
// Channels (glTFast keeps them as uv1 / uv2):
//   uv1.x sway    0 at the ground, 1 at the crown top   -> whole-tree bend
//   uv1.y flex    0 on the trunk, 1 at twig tips        -> branch motion
//   uv2.x phase   0..1 per branch                        -> desynchronises branches
//   uv2.y flutter 0 at the leaf stem, 1 at the leaf tip  -> fast leaf flutter
#ifndef TREE_WIND_INCLUDED
#define TREE_WIND_INCLUDED

void TreeWind_float(float3 PositionOS, float3 NormalOS, float2 Wind, float2 WindPhase, float Time,
                    float Strength, float TreeHeight, float3 WindDirOS, out float3 Out)
{
    float3 dir = normalize(float3(WindDirOS.x, 0.0, WindDirOS.z) + float3(1e-5, 0.0, 0.0));
    float3 side = float3(-dir.z, 0.0, dir.x);
    float ph = WindPhase.x * 6.2831853;
    float gust = 0.55 + 0.35 * sin(Time * 0.7) + 0.15 * sin(Time * 1.9 + 1.3);

    float3 d = dir * (Wind.x * gust * 0.035 * TreeHeight);
    float b = 0.6 * sin(Time * 2.1 + ph) + 0.4 * sin(Time * 3.7 + ph * 1.7);
    d += (dir * 0.5 + side * 0.6 + float3(0.0, 0.5, 0.0)) * (b * Wind.y * gust * 0.02 * TreeHeight);
    float f = sin(Time * 11.0 + ph * 13.0 + dot(PositionOS, float3(2.3, 1.7, 3.1)));
    d += NormalOS * (f * WindPhase.y * 0.05 * (0.5 + gust));

    Out = PositionOS + d * Strength;
}

void TreeWind_half(half3 PositionOS, half3 NormalOS, half2 Wind, half2 WindPhase, half Time,
                   half Strength, half TreeHeight, half3 WindDirOS, out half3 Out)
{
    float3 o;
    TreeWind_float(PositionOS, NormalOS, Wind, WindPhase, Time, Strength, TreeHeight, WindDirOS, o);
    Out = (half3)o;
}

#endif
