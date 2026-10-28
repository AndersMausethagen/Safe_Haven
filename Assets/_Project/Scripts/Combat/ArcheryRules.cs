using UnityEngine;

/// <summary>
/// The bow's numbers as plain functions, so they are easy to test: how far a held draw has come, what a partial draw
/// does to speed, damage and accuracy, and the launch angle that drops an arrow onto the aim point.
/// <para>
/// A shot loosed at a quarter draw flies at about 60% speed with 45% damage and strays three times as far as a full one.
/// Holding a full draw too long makes the arms shake: the spread grows again after <see cref="SteadySeconds"/>.
/// </para>
/// </summary>
public static class ArcheryRules
{
    /// <summary>Seconds a full draw can be held before the aim starts to shake.</summary>
    public const float SteadySeconds = 2.5f;
    private const float MinSpeedShare = 0.5f;
    private const float MinDamageShare = 0.25f;
    private const float PartialSpreadMultiplier = 3f;
    private const float ShakeSpreadPerSecond = 0.6f;

    /// <summary>Share of a full draw (0-1) after holding for <paramref name="heldSeconds"/>.</summary>
    public static float DrawShare(float heldSeconds, float drawSeconds) => Mathf.Clamp01(heldSeconds / Mathf.Max(0.01f, drawSeconds));

    /// <summary>True when letting go after <paramref name="heldSeconds"/> is a perfect release: within
    /// <paramref name="windowSeconds"/> of the draw becoming full.</summary>
    public static bool IsPerfectRelease(float heldSeconds, float drawSeconds, float windowSeconds) =>
        windowSeconds > 0f && heldSeconds >= drawSeconds && heldSeconds <= drawSeconds + windowSeconds;

    /// <summary>Share of the bow's arrow speed a shot at <paramref name="draw"/> gets.</summary>
    public static float SpeedShare(float draw) => Mathf.Lerp(MinSpeedShare, 1f, Mathf.Clamp01(draw));

    /// <summary>Share of the arrow's damage a shot at <paramref name="draw"/> deals.</summary>
    public static float DamageShare(float draw) => Mathf.Lerp(MinDamageShare, 1f, Mathf.Clamp01(draw));

    /// <summary>How far a shot can stray, in degrees: the bow's spread, more for a partial draw or a draw held too long.</summary>
    public static float SpreadDegrees(float bowSpread, float draw, float heldSeconds, float drawSeconds)
    {
        if (draw < 1f)
        {
            return bowSpread * Mathf.Lerp(PartialSpreadMultiplier, 1f, Mathf.Clamp01(draw));
        }

        float shaking = Mathf.Max(0f, heldSeconds - drawSeconds - SteadySeconds);
        return bowSpread * Mathf.Min(PartialSpreadMultiplier, 1f + shaking * ShakeSpreadPerSecond);
    }

    /// <summary>
    /// The direction to loose an arrow at <paramref name="speed"/> so that, falling with <paramref name="gravity"/>
    /// (m/s², positive), it passes through <paramref name="target"/>: the flatter of the two arcs. Out of reach, the
    /// farthest shot (45° up) toward it.
    /// </summary>
    public static Vector3 LaunchDirection(Vector3 from, Vector3 target, float speed, float gravity)
    {
        Vector3 delta = target - from;
        var flat = new Vector3(delta.x, 0f, delta.z);
        float x = flat.magnitude;
        if (x < 0.01f)
        {
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;
        }

        flat /= x;
        if (gravity <= 0f)
        {
            return delta.normalized;
        }

        float v2 = speed * speed;
        float discriminant = v2 * v2 - gravity * (gravity * x * x + 2f * delta.y * v2);
        float angle = discriminant < 0f ? 45f * Mathf.Deg2Rad : Mathf.Atan((v2 - Mathf.Sqrt(discriminant)) / (gravity * x));
        return flat * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
    }

    /// <summary>Turns <paramref name="direction"/> by up to <paramref name="degrees"/>, using two random values from 0 to 1
    /// (the square root spreads shots evenly over the cone rather than bunching them in the middle).</summary>
    public static Vector3 Stray(Vector3 direction, float degrees, float randomAngle, float randomRadius)
    {
        if (degrees <= 0f || direction.sqrMagnitude < 0.0001f)
        {
            return direction.normalized;
        }

        direction.Normalize();
        Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
        side = Quaternion.AngleAxis(randomAngle * 360f, direction) * side;
        return Quaternion.AngleAxis(Mathf.Sqrt(Mathf.Clamp01(randomRadius)) * degrees, side) * direction;
    }
}
