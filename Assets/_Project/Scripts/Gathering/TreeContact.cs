using UnityEngine;

/// <summary>
/// The geometry and rules of a falling tree meeting what is in its way, in plain C# so they can be tested without a scene.
/// Trees are lines here: painted terrain trees have no colliders of their own to ask.
/// </summary>
public static class TreeContact
{
    private const float MinimumSpinKept = 0.55f;
    private const float MaximumSpinKept = 0.92f;

    /// <summary>The shortest distance between two segments, with how far along the first (0 to 1) it falls and the point there.</summary>
    public static float Distance(Vector3 start, Vector3 end, Vector3 otherStart, Vector3 otherEnd, out float along, out Vector3 point)
    {
        Vector3 first = end - start;
        Vector3 second = otherEnd - otherStart;
        Vector3 gap = start - otherStart;
        float a = Vector3.Dot(first, first);
        float e = Vector3.Dot(second, second);
        float f = Vector3.Dot(second, gap);
        float c = Vector3.Dot(first, gap);
        float b = Vector3.Dot(first, second);
        float denominator = a * e - b * b;
        // Parallel lines have no one closest pair: any will do. A point is no line at all, so the first is measured from it.
        float s = denominator > 0.000001f ? Mathf.Clamp01((b * f - c * e) / denominator) : a > 0.000001f ? Mathf.Clamp01(-c / a) : 0f;
        float t = e > 0.000001f ? (b * s + f) / e : 0f;
        if (t < 0f)
        {
            t = 0f;
            s = a > 0.000001f ? Mathf.Clamp01(-c / a) : 0f;
        }
        else if (t > 1f)
        {
            t = 1f;
            s = a > 0.000001f ? Mathf.Clamp01((b - c) / a) : 0f;
        }

        along = s;
        point = start + first * s;
        return Vector3.Distance(point, otherStart + second * t);
    }

    /// <summary>A tree's weight for a collision, from its size.</summary>
    public static float Mass(float radius, float height) => Mathf.PI * radius * radius * height;

    /// <summary>How much of its spin a falling tree keeps after hitting a standing one: a heavy neighbour checks it hard, a
    /// slim one barely, and neither ever stops it dead and leaves it hanging.</summary>
    public static float SpinKept(float falling, float standing)
    {
        float total = falling + standing;
        return total > 0f ? Mathf.Lerp(MinimumSpinKept, MaximumSpinKept, falling / total) : MaximumSpinKept;
    }

    /// <summary>How far a standing tree is knocked by a falling one, in degrees: harder for a faster blow, a heavier tree
    /// falling and a lighter tree standing.</summary>
    public static float KnockDegrees(float speed, float falling, float standing, float mostDegrees, float fullSpeed)
    {
        float total = falling + standing;
        float weight = total > 0f ? falling / total : 0.5f;
        return mostDegrees * Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeed)) * Mathf.Sqrt(weight);
    }

    /// <summary>The damage of being hit at this speed: nothing below the slowest speed that hurts, all of it at full speed.</summary>
    public static float CrushDamage(float damage, float size, float speed, float slowest, float fullSpeed) =>
        damage * size * Mathf.InverseLerp(slowest, Mathf.Max(slowest + 0.01f, fullSpeed), speed);
}

/// <summary>A tree's lean as a damped spring, in degrees towards world X and Z. Plain C# so it can be tested.</summary>
public struct TreeSpring
{
    private const float Stiffness = 16f;
    private const float Damping = 2.4f;
    private const float StillDegrees = 0.04f;

    public Vector2 Lean;
    public Vector2 Rest;
    private Vector2 speed;

    /// <summary>True once it has settled where it will stay.</summary>
    public bool IsStill => (Lean - Rest).magnitude < StillDegrees && speed.magnitude < StillDegrees * 4f;

    /// <summary>Knocks it towards a direction, so it swings out about this many degrees.</summary>
    public void Knock(Vector2 direction, float degrees) => speed += direction * (degrees * Mathf.Sqrt(Stiffness));

    public void Step(float seconds)
    {
        Vector2 pull = (Rest - Lean) * Stiffness - speed * Damping;
        speed += pull * seconds;
        Lean += speed * seconds;
    }
}
