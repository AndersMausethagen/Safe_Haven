using System.Collections.Generic;
using UnityEngine;

/// <summary>Which side of a building model is its entrance, in the model's own axes (Front is local +Z).</summary>
public enum YardSide
{
    Front,
    Back,
    Right,
    Left,
}

/// <summary>A rotated rectangle on the ground (x, z), used for building footprints and yards.</summary>
public readonly struct FootprintRect
{
    public readonly Vector2 Center;
    public readonly Vector2 HalfSize;
    public readonly float Yaw;

    public FootprintRect(Vector2 center, Vector2 halfSize, float yaw)
    {
        Center = center;
        HalfSize = halfSize;
        Yaw = yaw;
    }

    /// <summary>The rectangle's local x axis on the ground.</summary>
    public Vector2 AxisX
    {
        get
        {
            float radians = Yaw * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        }
    }

    /// <summary>The rectangle's local z axis on the ground.</summary>
    public Vector2 AxisZ
    {
        get
        {
            float radians = Yaw * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }
    }

    public Vector3 WorldCenter(float height) => new Vector3(Center.x, height, Center.y);
}

/// <summary>
/// The geometry behind placing buildings (GDD §14.1): footprints and yards as ground rectangles, overlap tests, snapping
/// a building flush beside another, and laying walls in lines. Plain math with no scene access, so it can be tested.
/// </summary>
public static class BuildPlacement
{
    private const float TouchTolerance = 0.02f;

    /// <summary>Bounds of every visible mesh under root, in root's local space.</summary>
    public static Bounds LocalBounds(Transform root)
    {
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        bool found = false;
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
            {
                continue;
            }

            Bounds part = TransformBounds(filter.sharedMesh.bounds, toRoot * filter.transform.localToWorldMatrix);
            if (found)
            {
                bounds.Encapsulate(part);
            }
            else
            {
                bounds = part;
                found = true;
            }
        }

        return bounds;
    }

    public static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
        Vector3 extents = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -extents.x : extents.x, (i & 2) == 0 ? -extents.y : extents.y, (i & 4) == 0 ? -extents.z : extents.z);
            result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + corner));
        }

        return result;
    }

    /// <summary>The ground rectangle covered by a building with these local bounds, standing at position turned by yaw.</summary>
    public static FootprintRect Footprint(Vector3 position, float yaw, Bounds local)
    {
        return new FootprintRect(Flat(position + Quaternion.Euler(0f, yaw, 0f) * local.center), new Vector2(local.extents.x, local.extents.z), yaw);
    }

    /// <summary>
    /// The open ground in front of a building's entrance that must stay clear: as wide as the building on that side and
    /// depth deep. False when the building has no yard.
    /// </summary>
    public static bool TryGetYard(Vector3 position, float yaw, Bounds local, YardSide side, float depth, out FootprintRect yard)
    {
        if (depth <= 0f)
        {
            yard = default;
            return false;
        }

        float half = depth * 0.5f;
        Vector3 center;
        Vector2 size;
        switch (side)
        {
            case YardSide.Back:
                center = new Vector3(local.center.x, 0f, local.min.z - half);
                size = new Vector2(local.extents.x, half);
                break;
            case YardSide.Right:
                center = new Vector3(local.max.x + half, 0f, local.center.z);
                size = new Vector2(half, local.extents.z);
                break;
            case YardSide.Left:
                center = new Vector3(local.min.x - half, 0f, local.center.z);
                size = new Vector2(half, local.extents.z);
                break;
            default:
                center = new Vector3(local.center.x, 0f, local.max.z + half);
                size = new Vector2(local.extents.x, half);
                break;
        }

        yard = new FootprintRect(Flat(position + Quaternion.Euler(0f, yaw, 0f) * center), size, yaw);
        return true;
    }

    /// <summary>True when the rectangles share area. Rectangles that only touch along an edge don't overlap.</summary>
    public static bool Overlaps(FootprintRect a, FootprintRect b)
    {
        Vector2 offset = b.Center - a.Center;
        return !Separated(a.AxisX, a, b, offset) && !Separated(a.AxisZ, a, b, offset)
            && !Separated(b.AxisX, a, b, offset) && !Separated(b.AxisZ, a, b, offset);
    }

    private static bool Separated(Vector2 axis, FootprintRect a, FootprintRect b, Vector2 offset)
    {
        float reachA = a.HalfSize.x * Mathf.Abs(Vector2.Dot(a.AxisX, axis)) + a.HalfSize.y * Mathf.Abs(Vector2.Dot(a.AxisZ, axis));
        float reachB = b.HalfSize.x * Mathf.Abs(Vector2.Dot(b.AxisX, axis)) + b.HalfSize.y * Mathf.Abs(Vector2.Dot(b.AxisZ, axis));
        return Mathf.Abs(Vector2.Dot(offset, axis)) >= reachA + reachB - TouchTolerance;
    }

    /// <summary>
    /// Snaps a building flush against a side of a neighbour when it is within distance of that side: it turns to the
    /// neighbour's facing (the nearest quarter turn to yaw), touches the side, and lines up with the neighbour's centre or
    /// edges when close to them. gap is how far it moved sideways, to choose between several neighbours.
    /// </summary>
    public static bool TrySnapBeside(Vector3 point, float yaw, Bounds local, FootprintRect neighbour, float distance,
        out Vector3 snappedPoint, out float snappedYaw, out float gap)
    {
        float relative = Mathf.Round(Mathf.DeltaAngle(neighbour.Yaw, yaw) / 90f) * 90f;
        snappedYaw = Mathf.Repeat(neighbour.Yaw + relative, 360f);
        bool quarterTurned = Mathf.Abs(Mathf.Abs(relative) - 90f) < 1f;
        Vector2 half = quarterTurned ? new Vector2(local.extents.z, local.extents.x) : new Vector2(local.extents.x, local.extents.z);
        Vector2 offset = Flat(Quaternion.Euler(0f, relative, 0f) * local.center);

        Quaternion neighbourRotation = Quaternion.Euler(0f, neighbour.Yaw, 0f);
        Vector3 fromNeighbour = Quaternion.Inverse(neighbourRotation) * (point - neighbour.WorldCenter(point.y));
        Vector2 center = Flat(fromNeighbour) + offset;
        Vector2 reach = neighbour.HalfSize + half;

        gap = float.MaxValue;
        Vector2 best = center;
        if (Mathf.Abs(center.y) < reach.y)
        {
            float sideGap = Mathf.Abs(Mathf.Abs(center.x) - reach.x);
            if (sideGap <= distance)
            {
                gap = sideGap;
                best = new Vector2(Mathf.Sign(center.x) * reach.x, Align(center.y, neighbour.HalfSize.y, half.y, distance));
            }
        }

        if (Mathf.Abs(center.x) < reach.x)
        {
            float sideGap = Mathf.Abs(Mathf.Abs(center.y) - reach.y);
            if (sideGap <= distance && sideGap < gap)
            {
                gap = sideGap;
                best = new Vector2(Align(center.x, neighbour.HalfSize.x, half.x, distance), Mathf.Sign(center.y) * reach.y);
            }
        }

        if (gap == float.MaxValue)
        {
            snappedPoint = point;
            return false;
        }

        Vector2 pivot = best - offset;
        snappedPoint = neighbour.WorldCenter(point.y) + neighbourRotation * new Vector3(pivot.x, 0f, pivot.y);
        return true;
    }

    // Lines a building up with the neighbour's centre or one of its edges when it is close to one of them.
    private static float Align(float value, float neighbourHalf, float half, float distance)
    {
        float edge = neighbourHalf - half;
        float best = value;
        float bestDistance = distance;
        foreach (float candidate in new[] { 0f, edge, -edge })
        {
            float candidateDistance = Mathf.Abs(value - candidate);
            if (candidateDistance <= bestDistance)
            {
                best = candidate;
                bestDistance = candidateDistance;
            }
        }

        return best;
    }

    /// <summary>True when a line piece (wall, fence) is longest along its local x axis.</summary>
    public static bool RunsAlongX(Bounds local) => local.size.x >= local.size.z;

    /// <summary>How long one line piece is, end to end.</summary>
    public static float PieceLength(Bounds local) => Mathf.Max(local.size.x, local.size.z);

    /// <summary>The yaw that turns a line piece so its long side follows direction.</summary>
    public static float LineYaw(Vector3 direction, bool alongX)
    {
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        return Mathf.Repeat(alongX ? yaw - 90f : yaw, 360f);
    }

    /// <summary>Turns a ground direction to the nearest multiple of step degrees.</summary>
    public static Vector3 SnapDirection(Vector3 direction, float step)
    {
        if (step <= 0f)
        {
            return direction.normalized;
        }

        float yaw = Mathf.Round(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg / step) * step * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }

    /// <summary>The two ends of a placed line piece, on the ground.</summary>
    public static void LineEnds(Vector3 position, float yaw, Bounds local, out Vector3 start, out Vector3 end)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 center = position + rotation * local.center;
        Vector3 axis = rotation * (RunsAlongX(local) ? Vector3.right : Vector3.forward) * (PieceLength(local) * 0.5f);
        center.y = position.y;
        axis.y = 0f;
        start = center - axis;
        end = center + axis;
    }

    /// <summary>
    /// Lays pieces of pieceLength end to end from start towards end: as many as fit best, at least one and at most
    /// maxPieces. Fills centers (on the ground) and returns the direction, or zero when start and end are the same.
    /// </summary>
    public static Vector3 LayLine(Vector3 start, Vector3 end, float pieceLength, int maxPieces, List<Vector3> centers)
    {
        centers.Clear();
        Vector3 delta = end - start;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f || pieceLength <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 direction = delta.normalized;
        int count = Mathf.Clamp(Mathf.RoundToInt(delta.magnitude / pieceLength), 1, Mathf.Max(1, maxPieces));
        for (int i = 0; i < count; i++)
        {
            centers.Add(start + direction * (pieceLength * (i + 0.5f)));
        }

        return direction;
    }

    /// <summary>
    /// Lays pieces round a circle about <paramref name="centre"/> through <paramref name="start"/> (a fence round the
    /// portal's ward): each piece is a chord of the circle, the next starting where the last ends, so a ring of them bends
    /// round with no gaps. They run from start the shorter way round to <paramref name="cursor"/>'s bearing and end exactly
    /// there: the pieces are spread evenly, overlapping a little rather than leaving a gap, so a ring closes on its own
    /// first piece. Nothing is laid when start is closer to the centre than a piece is long (too tight to bend round).
    /// </summary>
    public static void LayArc(Vector3 centre, Vector3 start, Vector3 cursor, float pieceLength, int maxPieces, List<Vector3> centers,
        List<Vector3> directions)
    {
        centers.Clear();
        directions.Clear();
        Vector2 middle = Flat(centre);
        Vector2 from = Flat(start) - middle;
        Vector2 to = Flat(cursor) - middle;
        float radius = from.magnitude;
        if (pieceLength <= 0f || radius < pieceLength || to.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float step = 2f * Mathf.Asin(Mathf.Clamp01(pieceLength / (2f * radius)));
        float first = Mathf.Atan2(from.y, from.x);
        float turn = Mathf.DeltaAngle(first * Mathf.Rad2Deg, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        // Only a hair under a whole piece is let off (float error): letting off 5% made each chord up to 5% longer than a piece.
        int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(turn) / step - 1e-4f), 1, Mathf.Max(1, maxPieces));
        if (count < maxPieces || Mathf.Abs(turn) <= step * count)
        {
            step = Mathf.Abs(turn) / count; // even, and never longer than a piece: they meet (or overlap) at every post pair
        }

        float sign = turn >= 0f ? 1f : -1f;
        for (int i = 0; i < count; i++)
        {
            float a = first + sign * step * i, b = first + sign * step * (i + 1);
            Vector2 p = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Vector2 q = middle + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
            Vector2 mid = (p + q) * 0.5f;
            centers.Add(new Vector3(mid.x, start.y, mid.y)); // put on the ground by the placer (BuildPlacer.Lines)
            directions.Add(new Vector3(q.x - p.x, 0f, q.y - p.y).normalized);
        }
    }

    /// <summary>Where a building's pivot goes so its footprint is centred on center.</summary>
    public static Vector3 PivotFor(Vector3 center, float yaw, Bounds local)
    {
        Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * local.center;
        return new Vector3(center.x - offset.x, center.y, center.z - offset.z);
    }

    private static Vector2 Flat(Vector3 value) => new Vector2(value.x, value.z);
}
