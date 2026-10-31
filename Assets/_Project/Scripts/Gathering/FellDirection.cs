using System;
using UnityEngine;

/// <summary>
/// Which way a tree comes down: away from whoever felled it, turned as far as it takes to find open ground, since a good
/// feller lays a tree where there is room for it. Plain C# so the choice can be tested without a scene.
/// </summary>
public static class FellDirection
{
    /// <summary>
    /// The best of the directions around <paramref name="away"/> (flat), up to <paramref name="spreadDegrees"/> to either
    /// side. <paramref name="clearance"/> says how open a direction is, from 0 (blocked at once) to 1 (open all the way).
    /// Each degree turned costs a little, so open ground away from the feller always wins over open ground beside them.
    /// </summary>
    public static Vector3 Choose(Vector3 away, float spreadDegrees, int stepsPerSide, Func<Vector3, float> clearance)
    {
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
        Vector3 best = away;
        float bestScore = float.NegativeInfinity;
        stepsPerSide = Mathf.Max(0, stepsPerSide);
        for (int step = -stepsPerSide; step <= stepsPerSide; step++)
        {
            float turn = stepsPerSide > 0 ? spreadDegrees * step / stepsPerSide : 0f;
            Vector3 direction = Quaternion.AngleAxis(turn, Vector3.up) * away;
            float score = clearance(direction) - 0.25f * Mathf.Abs(turn) / Mathf.Max(1f, spreadDegrees);
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }

        return best;
    }
}
