using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The valley of a map of the Valley shape, in metres from the map's south-west corner: a centreline from the mouth (its first point,
/// off one edge of the map) to the head (its last, off the opposite edge), the floor's half width along it, and the walls on either
/// side. The river runs down the centreline. Made by RaidLayoutPlanner (.Valley) and turned into ground by RaidLayoutField.
/// </summary>
public sealed class RaidLayoutValley
{
    /// <summary>The middle of the floor, a point every <see cref="Step"/> metres along <see cref="Axis"/>, from the mouth to the head.</summary>
    public readonly List<Vector2> Line = new List<Vector2>();
    /// <summary>Half the floor's width at each point of the line.</summary>
    public readonly List<float> FloorHalf = new List<float>();
    /// <summary>Unit direction from the mouth to the head, and the direction across it (the left bank's side, looking up the valley).</summary>
    public Vector2 Axis;
    public Vector2 Across;
    /// <summary>Where the line starts: the mouth's middle, <see cref="Extend"/> metres beyond the map's edge.</summary>
    public Vector2 Origin;
    public float Step;
    public float Extend;
    /// <summary>The map's width along the axis.</summary>
    public float Length;
    /// <summary>How high each wall stands above the floor at its foot: the left (+Across) and the right.</summary>
    public float LeftWall;
    public float RightWall;
    /// <summary>Metres a wall climbs over, and in how many ledges.</summary>
    public float WallRun;
    public int Benches;
    /// <summary>Metres the floor climbs from the mouth to the head.</summary>
    public float Tilt;

    /// <summary>Metres along the axis from the mouth's edge of the map (0) to the head's (Length).</summary>
    public float AlongOf(Vector2 point) => Vector2.Dot(point - Origin, Axis) - Extend;

    /// <summary>0 at the mouth's edge of the map, 1 at the head's.</summary>
    public float ShareOf(Vector2 point) => Mathf.Clamp01(AlongOf(point) / Mathf.Max(1f, Length));

    /// <summary>The centreline's point and the floor's half width at a distance along the axis from the mouth's edge.</summary>
    public Vector2 CentreAt(float along, out float floorHalf)
    {
        float index = Mathf.Clamp((along + Extend) / Step, 0f, Line.Count - 1.001f);
        int i = (int)index;
        float t = index - i;
        floorHalf = Mathf.Lerp(FloorHalf[i], FloorHalf[i + 1], t);
        return Vector2.Lerp(Line[i], Line[i + 1], t);
    }

    /// <summary>Metres across the valley from its centreline at the point's place along it: positive on the left bank.</summary>
    public float OffsetOf(Vector2 point)
    {
        Vector2 centre = CentreAt(AlongOf(point), out _);
        return Vector2.Dot(point - centre, Across);
    }

    /// <summary>Metres from the point to the edge of the floor (a wall's foot): below zero it is on the floor.</summary>
    public float FloorEdge(Vector2 point)
    {
        Vector2 centre = CentreAt(AlongOf(point), out float half);
        return Mathf.Abs(Vector2.Dot(point - centre, Across)) - half;
    }

    /// <summary>True when a circle of this radius about the point lies on the floor.</summary>
    public bool Holds(Vector2 point, float radius) => FloorEdge(point) + radius <= 0f;

    /// <summary>Which bank the point is on: 1 the left, -1 the right.</summary>
    public int BankOf(Vector2 point) => OffsetOf(point) >= 0f ? 1 : -1;

    /// <summary>The point at a distance along the axis and an offset across it from the centreline (positive: the left bank).</summary>
    public Vector2 At(float along, float offset) => CentreAt(along, out _) + Across * offset;

    /// <summary>
    /// How high the valley's shape lifts the ground at a point, given its metres beyond the floor's edge: the floor climbing up the
    /// valley and dipping gently towards its middle, then a wall of steep faces and level ledges, then the land above.
    /// </summary>
    public float RiseAt(Vector2 point, float beyondFloor, float roughness)
    {
        float rise = Tilt * ShareOf(point);
        float half = Mathf.Max(1f, CentreHalf(point));
        if (beyondFloor <= 0f)
        {
            // A U, not a V: nearly level, rising a little towards the walls.
            float across = 1f + beyondFloor / half;
            return rise + FloorDip * across * across * across;
        }

        float wall = OffsetOf(point) >= 0f ? LeftWall : RightWall;
        return rise + FloorDip + wall * Benched(beyondFloor / WallRun + roughness, Benches);
    }

    private float CentreHalf(Vector2 point)
    {
        CentreAt(AlongOf(point), out float half);
        return half;
    }

    /// <summary>The floor rises this much from the river's line to the foot of a wall.</summary>
    public const float FloorDip = 1.6f;

    /// <summary>A climb of 0 to 1 over 0 to 1 in ledges: each a steep face over most of its run and a level bench after it.</summary>
    public static float Benched(float x, int benches)
    {
        if (x <= 0f)
        {
            return 0f;
        }

        if (x >= 1f)
        {
            return 1f;
        }

        float scaled = x * benches;
        int ledge = Mathf.Min(benches - 1, (int)scaled);
        float within = scaled - ledge;
        return (ledge + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(within / FaceShare))) / benches;
    }

    /// <summary>Of each ledge's run, the share taken by its face (the rest is its level bench).</summary>
    public const float FaceShare = 0.68f;
}

/// <summary>Where a trail crosses a valley's river: squarely, at a ford or over a bridge.</summary>
public sealed class RaidLayoutCrossing
{
    /// <summary>The middle of the crossing, on the river's line.</summary>
    public Vector2 Position;
    /// <summary>Unit direction across the river, the way the trail goes.</summary>
    public Vector2 Direction;
    /// <summary>Metres from the middle to each end: the river's half width and its banks.</summary>
    public float Reach;
    /// <summary>Which trail (index into the plan's trails and links).</summary>
    public int Trail;
    /// <summary>True when a bridge carries the trail over (the river stays deep under it); false at a ford.</summary>
    public bool Bridge;
}
