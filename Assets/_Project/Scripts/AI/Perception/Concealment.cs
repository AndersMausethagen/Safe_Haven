using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place to hide (Docs/RangedStealthResearch.md §3.10: Shadow Tactics' bushes, Ghost of Tsushima's tall grass): a sneaking player
/// inside it is seen as if Visibility Scale times as visible, so creatures see them from closer and notice slower. Nothing hides from a
/// creature's close sense, which notices from any side. On shrub and bracken prefabs; its reach is the plant's own footprint unless set.
/// PlayerStealth asks <see cref="ScaleAt"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class Concealment : MonoBehaviour
{
    private static readonly List<Concealment> all = new List<Concealment>();

    [Tooltip("How visible a sneaking player inside it is, as a share (0.5: half).")]
    [SerializeField, Range(0.05f, 1f)] private float visibilityScale = 0.5f;
    [Tooltip("Metres from its middle that count as inside. 0: worked out from the plant's own size.")]
    [SerializeField, Min(0f)] private float radius;

    private Vector3 centre;
    private float reach;

    // Entering Play doesn't reload scripts in this project, so the list is cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    private void OnEnable()
    {
        centre = transform.position;
        reach = radius;
        if (reach <= 0f)
        {
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer part in GetComponentsInChildren<Renderer>())
            {
                if (part is MeshRenderer || part is SkinnedMeshRenderer)
                {
                    bounds = any ? Encapsulate(bounds, part.bounds) : part.bounds;
                    any = true;
                }
            }

            centre = any ? bounds.center : transform.position;
            reach = any ? Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.85f : 0.8f;
        }

        all.Add(this);
    }

    private void OnDisable() => all.Remove(this);

    private static Bounds Encapsulate(Bounds bounds, Bounds more)
    {
        bounds.Encapsulate(more);
        return bounds;
    }

    /// <summary>The visibility share where the player stands: the deepest cover they are in, 1 when in none.</summary>
    public static float ScaleAt(Vector3 point)
    {
        float scale = 1f;
        foreach (Concealment cover in all)
        {
            float dx = point.x - cover.centre.x, dz = point.z - cover.centre.z;
            if (dx * dx + dz * dz <= cover.reach * cover.reach && cover.visibilityScale < scale)
            {
                scale = cover.visibilityScale;
            }
        }

        return scale;
    }
}
