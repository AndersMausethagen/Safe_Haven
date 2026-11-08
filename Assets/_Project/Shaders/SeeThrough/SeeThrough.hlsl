#ifndef SAFE_HAVEN_SEE_THROUGH_INCLUDED
#define SAFE_HAVEN_SEE_THROUGH_INCLUDED

// See-through occluders, in the way of Gord: whatever stands between the camera and the player dissolves in a soft,
// dithered circle around the player, so the player (and the ground they stand on) can always be seen. Set every frame
// by the SeeThrough component (on the player). Call SeeThroughClip(input.positionCS) first thing in a forward, depth
// and depth-normals fragment; never in a shadow caster, so shadows stay whole.

float4 _SeeThroughTarget;   // xyz: the point kept visible (the player's chest), w: strength 0..1 (0 = off)
float4 _SeeThroughParams;   // x: radius at the target (m), y: soft edge (share), z: margin before the target (m), w: radius at the camera (m)

// 4×4 Bayer threshold for the pixel: a fixed, even stipple instead of transparency, so no sorting is needed.
float SeeThroughBayer(float2 pixel)
{
    uint2 p = uint2(pixel) & 3;
    uint index = p.x + p.y * 4;
    const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (bayer[index] + 0.5) / 16.0;
}

// How see-through a fragment at this clip-space position is (0 solid, 1 gone).
float SeeThroughAmount(float4 positionCS)
{
    if (_SeeThroughTarget.w <= 0.0)
    {
        return 0.0;
    }

    float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    float3 positionWS = ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP);
    float3 origin = _WorldSpaceCameraPos;
    float3 axis = _SeeThroughTarget.xyz - origin;
    float span = max(length(axis), 1e-3);
    float t = dot(positionWS - origin, axis) / (span * span);
    // Only what is in front of the player (with a margin, so the player's own body and the ground at their feet stay).
    if (t <= 0.0 || t * span >= span - _SeeThroughParams.z)
    {
        return 0.0;
    }

    float radius = lerp(_SeeThroughParams.w, _SeeThroughParams.x, t);
    float fromAxis = distance(positionWS, origin + axis * t);
    float inner = radius * (1.0 - _SeeThroughParams.y);
    return (1.0 - smoothstep(inner, radius, fromAxis)) * _SeeThroughTarget.w;
}

void SeeThroughClip(float4 positionCS)
{
    #if defined(_CHARACTER_CLOTH) || defined(_CHARACTER_SKIN)
        return; // a creature between the camera and the player must stay in sight
    #endif
    float amount = SeeThroughAmount(positionCS);
    if (amount > 0.0)
    {
        clip(SeeThroughBayer(positionCS.xy) - amount);
    }
}

// The canopy cutaway, for leaves only (Tree Wind's leaf cards): from a top-down camera the crowns of a dense wood would
// roof over everything near the player, its creatures, its resources and a ravine's lip among them. A leaf higher than
// the crown start above the player's feet, whose line of sight comes down on the ground within the radius of the player,
// stipples away; bark, undergrowth and every shadow stay, so the wood still reads as a wood.
float4 _SeeThroughCanopy;   // x: radius on the ground round the player (m), y: crown start above the feet (m), z: soft edge (m), w: feet height

float SeeThroughCanopyAmount(float4 positionCS)
{
    if (_SeeThroughTarget.w <= 0.0 || _SeeThroughCanopy.x <= 0.0)
    {
        return 0.0;
    }

    float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    float3 positionWS = ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP);
    float3 origin = _WorldSpaceCameraPos;
    float feet = _SeeThroughCanopy.w;
    float above = positionWS.y - feet;
    if (above <= _SeeThroughCanopy.y || positionWS.y >= origin.y - 0.1)
    {
        return 0.0;
    }

    // Where the line from the camera through this leaf meets the ground at the player's feet.
    float along = (origin.y - feet) / max(origin.y - positionWS.y, 1e-3);
    float2 ground = origin.xz + (positionWS.xz - origin.xz) * along;
    float fromPlayer = distance(ground, _SeeThroughTarget.xz);
    float inside = 1.0 - smoothstep(_SeeThroughCanopy.x - _SeeThroughCanopy.z, _SeeThroughCanopy.x, fromPlayer);
    float crown = smoothstep(_SeeThroughCanopy.y, _SeeThroughCanopy.y + 1.5, above);
    return inside * crown * _SeeThroughTarget.w;
}

void SeeThroughCanopyClip(float4 positionCS)
{
    float amount = max(SeeThroughAmount(positionCS), SeeThroughCanopyAmount(positionCS));
    if (amount > 0.0)
    {
        clip(SeeThroughBayer(positionCS.xy) - amount);
    }
}

#endif
