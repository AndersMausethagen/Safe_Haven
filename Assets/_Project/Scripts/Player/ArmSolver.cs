using UnityEngine;

/// <summary>
/// Two-bone reach for an arm: where the elbow goes so the wrist arrives at a target, bending the way the arm already bends. Too far a
/// target is reached as far as the arm goes. Pure, so it can be tested; <see cref="TwoHandGrip"/> applies it.
/// </summary>
public static class ArmSolver
{
    /// <summary>The elbow's new position; <paramref name="wrist"/> is where the wrist ends up.</summary>
    public static Vector3 Solve(Vector3 shoulder, Vector3 elbow, Vector3 target, float upperLength, float lowerLength, out Vector3 wrist)
    {
        Vector3 toTarget = target - shoulder;
        float wanted = toTarget.magnitude;
        Vector3 along = wanted > 1e-5f ? toTarget / wanted : Vector3.down;
        float most = upperLength + lowerLength - 1e-3f;
        float least = Mathf.Abs(upperLength - lowerLength) + 1e-3f;
        float reach = Mathf.Clamp(wanted, least, most);
        wrist = shoulder + along * reach;

        // The elbow's distance along the line from the shoulder, and how far off it (cosine rule).
        float down = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
        float off = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - down * down));
        Vector3 bend = Vector3.ProjectOnPlane(elbow - shoulder, along);
        bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.ProjectOnPlane(Vector3.down, along).normalized;
        return shoulder + along * down + bend * off;
    }
}
