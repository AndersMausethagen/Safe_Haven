using UnityEngine;

/// <summary>
/// How a dodge steers around the enemy in focus, kept pure so it can be tested: rolling toward or past it curves round it to the spot
/// behind, on the side the player leans; rolling away from it is just a roll. All directions are on the ground.
/// </summary>
public static class DodgeRules
{
    // Moving further from the enemy than this is rolling away, not around: the cosine of about 100 degrees.
    private const float AroundCosine = -0.17f;
    // How near the spot behind counts as there.
    private const float ArriveDistance = 0.45f;

    /// <summary>The way a dodge starts: where the player is steering, else straight back from the enemy in focus (nobody means to roll
    /// into its face by standing still), else toward the aim.</summary>
    public static Vector3 StartDirection(Vector3 input, Vector3 aim, Vector3 toFocus)
    {
        if (input.sqrMagnitude > 0.01f)
        {
            return input.normalized;
        }

        return toFocus.sqrMagnitude > 1e-4f ? -toFocus.normalized : aim;
    }

    /// <summary>True when steering this way means going around the enemy rather than away from it.</summary>
    public static bool GoesAround(Vector3 input, Vector3 toFocus)
    {
        input.y = 0f;
        toFocus.y = 0f;
        return input.sqrMagnitude > 0.01f && toFocus.sqrMagnitude > 1e-4f && Vector3.Dot(input.normalized, toFocus.normalized) >= AroundCosine;
    }

    /// <summary>The spot behind the enemy to make for.</summary>
    public static Vector3 SpotBehind(Vector3 enemy, Vector3 enemyForward, float distance)
    {
        enemyForward.y = 0f;
        return enemy - (enemyForward.sqrMagnitude > 1e-4f ? enemyForward.normalized : Vector3.forward) * distance;
    }

    public static bool HasArrived(Vector3 position, Vector3 spot)
    {
        Vector3 left = spot - position;
        left.y = 0f;
        return left.sqrMagnitude <= ArriveDistance * ArriveDistance;
    }

    /// <summary>
    /// Which way to head now: straight for the spot behind if the way is clear of the enemy's <paramref name="clearance"/>, else along the
    /// circle around it, on the side <paramref name="input"/> leans (the near side when it doesn't lean), easing out if already too close.
    /// </summary>
    public static Vector3 Heading(Vector3 position, Vector3 enemy, Vector3 spot, Vector3 input, float clearance)
    {
        Vector3 toEnemy = enemy - position;
        toEnemy.y = 0f;
        Vector3 toSpot = spot - position;
        toSpot.y = 0f;
        float distance = toEnemy.magnitude;
        if (distance < 1e-3f)
        {
            return toSpot.sqrMagnitude > 1e-6f ? toSpot.normalized : Vector3.forward;
        }

        Vector3 inward = toEnemy / distance;
        if (IsClear(toEnemy, toSpot, clearance))
        {
            return toSpot.normalized;
        }

        float lean = Vector3.Cross(inward, input).y;
        float side = Mathf.Abs(lean) > 0.05f ? Mathf.Sign(lean) : Mathf.Sign(Vector3.Cross(inward, toSpot).y);
        side = side == 0f ? 1f : side;
        Vector3 around = Vector3.Cross(Vector3.up, inward) * side;
        float tooClose = Mathf.Clamp01((clearance - distance) / Mathf.Max(0.01f, clearance));
        return (around - inward * tooClose).normalized;
    }

    // Whether the straight line to the spot stays outside the circle around the enemy.
    private static bool IsClear(Vector3 toEnemy, Vector3 toSpot, float clearance)
    {
        float length = toSpot.magnitude;
        if (length < 1e-3f)
        {
            return true;
        }

        Vector3 along = toSpot / length;
        float closest = Mathf.Clamp(Vector3.Dot(toEnemy, along), 0f, length);
        return (toEnemy - along * closest).sqrMagnitude >= clearance * clearance;
    }
}
