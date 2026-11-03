using UnityEngine;

/// <summary>
/// A party's steady walk along a route: how far it has come, and where that is. Plain C#, moved on by the story's Walk The
/// Party node, which also decides when the party waits. It keeps no scene references, so it can be tested on its own.
/// </summary>
public sealed class PartyWalk
{
    private readonly Vector3[] points;
    private readonly float[] upTo;
    private float travelled;

    public PartyWalk(Vector3[] points)
    {
        this.points = points;
        upTo = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            upTo[i] = upTo[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        Length = points.Length > 0 ? upTo[points.Length - 1] : 0f;
    }

    /// <summary>The walk under way, or the last one: what Wait Until The Walk Reaches and Change Time of Day follow.</summary>
    public static PartyWalk Current { get; internal set; }

    public float Length { get; }

    /// <summary>How much of the way is behind them, 0 to 1.</summary>
    public float Progress => Length > 0f ? Mathf.Clamp01(travelled / Length) : 1f;

    public bool IsDone => travelled >= Length;

    public Vector3 Position => PointAt(travelled);

    /// <summary>The way they are heading (flat), at the point <paramref name="ahead"/> metres on.</summary>
    public Vector3 Heading(float ahead = 0f)
    {
        Vector3 from = PointAt(travelled + ahead - 1f);
        Vector3 to = PointAt(travelled + ahead + 1f);
        Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
        return flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
    }

    /// <summary>Where the walk will be in <paramref name="ahead"/> metres; never past the end.</summary>
    public Vector3 Ahead(float ahead) => PointAt(travelled + ahead);

    public void Advance(float metres)
    {
        travelled = Mathf.Min(Length, travelled + Mathf.Max(0f, metres));
    }

    private Vector3 PointAt(float distance)
    {
        if (points.Length == 0)
        {
            return Vector3.zero;
        }

        distance = Mathf.Clamp(distance, 0f, Length);
        for (int i = 1; i < points.Length; i++)
        {
            if (distance <= upTo[i])
            {
                float leg = upTo[i] - upTo[i - 1];
                return leg > 0f ? Vector3.Lerp(points[i - 1], points[i], (distance - upTo[i - 1]) / leg) : points[i];
            }
        }

        return points[points.Length - 1];
    }
}
