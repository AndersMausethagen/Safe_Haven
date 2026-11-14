using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a quest waypoint at the open water nearest the player: a river's bank, a pond's shore, a well. A quest that asks
/// for "a drink at any water" (Strength for the Road) then points somewhere useful without a fixed drinking spot, which a
/// world rebuild can take away (the Haven's Drinking Spot went that way, and the quest waited a week for it).
/// <para>
/// Where it belongs: beside a <see cref="QuestWaypoint"/> on its own object in the scene. It looks again every few seconds
/// while there is a player; frozen water and water that is only for show (no ripples) are passed over.
/// </para>
/// </summary>
[RequireComponent(typeof(QuestWaypoint))]
public sealed class NearestWaterMark : MonoBehaviour
{
    [Tooltip("Seconds between looks for the nearest water.")]
    [SerializeField, Min(0.5f)] private float lookEvery = 3f;
    [Tooltip("Water further than this from the player isn't considered.")]
    [SerializeField, Min(10f)] private float reach = 800f;

    private const float WellsEvery = 30f;
    private readonly List<WaterSource> wells = new List<WaterSource>();
    private float nextLook;
    private float nextWells;

    private void Update()
    {
        if (Time.time < nextLook || Player.Current == null)
        {
            return;
        }

        nextLook = Time.time + lookEvery;
        if (Time.time >= nextWells)
        {
            nextWells = Time.time + WellsEvery;
            wells.Clear();
            wells.AddRange(FindObjectsByType<WaterSource>());
        }

        if (TryNearest(Player.Current.transform.position, reach, wells, out Vector3 at))
        {
            transform.position = at;
        }
    }

    /// <summary>The nearest point of open water to <paramref name="from"/> within <paramref name="within"/> metres: on a
    /// river's near bank, a pond's shore, or at a water source.</summary>
    public static bool TryNearest(Vector3 from, float within, IReadOnlyList<WaterSource> sources, out Vector3 nearest)
    {
        nearest = default;
        float best = within;
        var flat = new Vector2(from.x, from.z);
        foreach (WaterSurface surface in WaterSurface.Surfaces)
        {
            if (surface == null || !surface.isActiveAndEnabled || surface.IsFrozen || !surface.TakesRipples)
            {
                continue;
            }

            if (surface.HasCentreline)
            {
                // A river: the point on its centreline nearest the player, then out to the near bank.
                WaterSurface.NearestOnCentreline(surface.WorldCentreline(), flat, out Vector4 on, out float away);
                float toBank = Mathf.Max(0f, away - on.w);
                if (toBank < best)
                {
                    Vector2 outward = away > 0.01f ? (flat - new Vector2(on.x, on.z)) / away : Vector2.zero;
                    Vector2 bank = new Vector2(on.x, on.z) + outward * Mathf.Min(away, on.w * 0.8f);
                    best = toBank;
                    nearest = new Vector3(bank.x, on.y, bank.y);
                }

                continue;
            }

            if (!surface.TryGetComponent(out Renderer shape))
            {
                continue;
            }

            // Still water: the nearest point of its bounds, walked in towards the middle until it is on the water.
            Bounds bounds = shape.bounds;
            Vector3 point = bounds.ClosestPoint(new Vector3(from.x, bounds.center.y, from.z));
            if (surface.HasShoreline)
            {
                Vector3 toMiddle = bounds.center - point;
                toMiddle.y = 0f;
                for (int step = 0; step < 40 && surface.ShorelineDistance(point.x, point.z) > 0f; step++)
                {
                    point += toMiddle / 40f;
                }
            }

            float distance = Vector2.Distance(flat, new Vector2(point.x, point.z));
            if (distance < best)
            {
                best = distance;
                nearest = new Vector3(point.x, surface.transform.position.y, point.z);
            }
        }

        if (sources != null)
        {
            foreach (WaterSource source in sources)
            {
                if (source == null || !source.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 at = source.transform.position;
                float distance = Vector2.Distance(flat, new Vector2(at.x, at.z));
                if (distance < best)
                {
                    best = distance;
                    nearest = at;
                }
            }
        }

        return best < within;
    }
}
