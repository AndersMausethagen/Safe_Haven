using UnityEngine;

/// <summary>
/// What a selected monster draws in the Scene view: its sight, its close sense, its chase range and leash.
/// </summary>
public sealed partial class MonsterAI
{
    private void OnDrawGizmosSelected()
    {
        if (definition == null)
        {
            return;
        }

        Vector3 origin = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        DrawSightCone(definition.DetectionRange, definition.SightAngle);
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, definition.CloseSenseRange);
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, definition.ChaseRange);

        if (definition.LeashRange > 0f)
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(origin, definition.LeashRange);
        }
    }

    private void DrawSightCone(float range, float angle)
    {
        const int segments = 24;
        Vector3 center = transform.position + Vector3.up * 0.1f;
        float half = Mathf.Clamp(angle, 0f, 360f) * 0.5f;
        Vector3 previous = center + Quaternion.Euler(0f, -half, 0f) * transform.forward * range;
        if (half < 180f)
        {
            Gizmos.DrawLine(center, previous);
        }

        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = center + Quaternion.Euler(0f, Mathf.Lerp(-half, half, i / (float)segments), 0f) * transform.forward * range;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        if (half < 180f)
        {
            Gizmos.DrawLine(center, previous);
        }
    }
}
