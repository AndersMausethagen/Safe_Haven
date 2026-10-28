using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A flat area drawn as a polygon on the ground (its corners' X and Z; height is ignored): whether a point is inside,
/// and random points in it. Plain functions, so zones are easy to test. Used by MobSpawnZone.
/// </summary>
public static class ZonePolygon
{
    private const int Attempts = 64;

    /// <summary>True when <paramref name="point"/> lies inside the polygon (seen from above).</summary>
    public static bool Contains(IReadOnlyList<Vector3> corners, Vector3 point)
    {
        bool inside = false;
        for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[j];
            if ((a.z > point.z) != (b.z > point.z) && point.x < (b.x - a.x) * (point.z - a.z) / (b.z - a.z) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>The area seen from above, in square units.</summary>
    public static float Area(IReadOnlyList<Vector3> corners)
    {
        float twice = 0f;
        for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
        {
            twice += corners[j].x * corners[i].z - corners[i].x * corners[j].z;
        }

        return Mathf.Abs(twice) * 0.5f;
    }

    /// <summary>A random point inside the polygon (height 0), or false for a polygon with no inside.</summary>
    public static bool TryRandomPoint(IReadOnlyList<Vector3> corners, System.Random random, out Vector3 point)
    {
        point = Vector3.zero;
        if (corners.Count < 3)
        {
            return false;
        }

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 corner in corners)
        {
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minZ = Mathf.Min(minZ, corner.z);
            maxZ = Mathf.Max(maxZ, corner.z);
        }

        for (int i = 0; i < Attempts; i++)
        {
            var candidate = new Vector3(Mathf.Lerp(minX, maxX, (float)random.NextDouble()), 0f, Mathf.Lerp(minZ, maxZ, (float)random.NextDouble()));
            if (Contains(corners, candidate))
            {
                point = candidate;
                return true;
            }
        }

        return false;
    }
}
