using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something that lets the player see around it: the player's own sight, a torch, the Heart Tree's ward. Inside its
/// radius the night veil lifts and the atmospheric fog thins; with Reveals, the ground it covers becomes explored
/// (ExplorationMap). Night creatures stay out of lit ground (<see cref="IsLit"/>). Drawn by AtmosphereFeature.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VisibilitySource : MonoBehaviour
{
    private static readonly List<VisibilitySource> active = new List<VisibilitySource>();

    [Tooltip("Metres around it that are seen clearly, even at night.")]
    [SerializeField, Min(0f)] private float radius = 10f;
    [Tooltip("Share of the radius over which it fades out at the edge.")]
    [SerializeField, Range(0.05f, 1f)] private float softness = 0.35f;
    [Tooltip("Marks the ground within Reveal Radius as explored.")]
    [SerializeField] private bool reveals = true;
    [SerializeField, Min(0f)] private float revealRadius = 16f;
    [Tooltip("On the Heart Tree: the radius follows the ward (plus Radius as a margin).")]
    [SerializeField] private bool followsWard;
    [Tooltip("Counts as light for night creatures, which won't step into it (torches, the ward). The player's own sight doesn't.")]
    [SerializeField] private bool isLight = true;

    /// <summary>Every enabled source.</summary>
    public static IReadOnlyList<VisibilitySource> Active => active;

    public float Radius
    {
        get
        {
            if (followsWard && Application.isPlaying && HeartTree.Current != null)
            {
                return HeartTree.Current.WardRadius + radius;
            }

            return radius;
        }
    }

    public float Softness => softness;
    public bool Reveals => reveals;
    public float RevealRadius => followsWard ? Mathf.Max(revealRadius, Radius) : revealRadius;
    public bool IsLight => isLight;

    /// <summary>Sets it up from code (a source added at runtime, such as a beacon's).</summary>
    public void Configure(float newRadius, float newSoftness, bool revealsGround, float newRevealRadius)
    {
        radius = Mathf.Max(0f, newRadius);
        softness = Mathf.Clamp(newSoftness, 0.05f, 1f);
        reveals = revealsGround;
        revealRadius = Mathf.Max(0f, newRevealRadius);
    }

    /// <summary>True when a position stands inside the radius of any light (not the player's own sight).</summary>
    public static bool IsLit(Vector3 position, float margin = 0f)
    {
        foreach (VisibilitySource source in active)
        {
            if (!source.isLight)
            {
                continue;
            }

            Vector3 offset = position - source.transform.position;
            offset.y = 0f;
            float reach = source.Radius + margin;
            if (offset.sqrMagnitude <= reach * reach)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.8f);
        DrawCircle(Radius);
        if (reveals)
        {
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.5f);
            DrawCircle(RevealRadius);
        }
    }

    private void DrawCircle(float r)
    {
        Vector3 previous = transform.position + new Vector3(r, 0.1f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float angle = i / 48f * Mathf.PI * 2f;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(angle) * r, 0.1f, Mathf.Sin(angle) * r);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
