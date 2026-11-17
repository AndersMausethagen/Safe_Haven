using UnityEngine;

/// <summary>
/// A Valley map's ground (RaidLayoutShape.Valley): the floor tilting up from the mouth to the head and dipping gently to the river, the
/// walls climbing in steep faces and level ledges with ragged feet, and fords only where a crossing has no bridge. Part of
/// RaidLayoutField, in its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    private const float WallFootWobble = 4f;
    private const float WallFootScale = 23f;
    private const float WallFaceRough = 0.06f;
    private const float WallFaceScale = 9f;

    private void BuildValley(float trailHalfWidth)
    {
        RaidLayoutValley valley = plan.Valley;
        valleyBeyond = new float[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                valleyBeyond[y * resolution + x] = valley.FloorEdge(new Vector2(x * cell, y * cell));
            }
        }

        fordDistance = Filled(reach);
        foreach (RaidLayoutCrossing crossing in plan.Crossings)
        {
            if (!crossing.Bridge)
            {
                Stamp(crossing.Position - crossing.Direction * crossing.Reach, crossing.Position + crossing.Direction * crossing.Reach,
                    LinkHalfWidth(crossing.Trail, trailHalfWidth), fordDistance);
            }
        }
    }

    // The valley's lift at a point: its foot wanders a few metres in and out, and its faces are rough, so a wall is never a ruled line.
    private float ValleyAt(float x, float y, float u, float v)
    {
        float beyond = Sample(valleyBeyond, u, v) + (Noise(x, y, WallFootScale, 151f) - 0.5f) * 2f * WallFootWobble;
        float rough = (Noise(x, y, WallFaceScale, 173f) - 0.5f) * 2f * WallFaceRough;
        return plan.Valley.RiseAt(new Vector2(x, y), beyond, rough);
    }

    /// <summary>On a Valley map, metres from the point (from the map's corner) to the floor's edge: below zero it is on the floor.</summary>
    public float ValleyFloorEdge(Vector2 point) => valleyBeyond != null ? Sample(valleyBeyond, point.x / plan.Size, point.y / plan.Size) : -Far;
}
