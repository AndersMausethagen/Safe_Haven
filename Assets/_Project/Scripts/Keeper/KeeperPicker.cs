using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the pointer is over in the Keeper's view: a resident or a building (KeeperView). A click picks it; the one under the
/// pointer and the one picked are outlined with the player's own interaction outline (OutlineCopies). On a controller there is no
/// pointer: the middle of the screen picks, and a person or building within KeeperViewSettings › Pad Snap Radius of that spot
/// on the ground is taken, so nothing has to be hit to the pixel.
/// </summary>
public sealed class KeeperPicker
{
    private readonly RaycastHit[] hits = new RaycastHit[48];
    private readonly Collider[] near = new Collider[48];
    private readonly Dictionary<Component, List<OutlineCopies.Entry>> outlines = new Dictionary<Component, List<OutlineCopies.Entry>>();
    private readonly List<Component> stale = new List<Component>();

    /// <summary>The person or building under the pointer now (a ResidentNpc or a BuildingObject), or null.</summary>
    public Component Hovered { get; private set; }

    /// <summary>The one picked last, or null.</summary>
    public Component Picked { get; set; }

    /// <summary>Finds what <paramref name="ray"/> meets first: a resident or a building, through the triggers of zones and yards.</summary>
    public Component Pick(Ray ray, float range, Vector3? snapAround, float snapRadius)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, range, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, 0, count, HitOrder.Instance);
        for (int i = 0; i < count; i++)
        {
            Component found = Owner(hits[i].collider);
            if (found != null)
            {
                return Hovered = found;
            }

            // The ground hides what is behind it.
            if (hits[i].collider is TerrainCollider)
            {
                break;
            }
        }

        Hovered = snapAround.HasValue ? Nearest(snapAround.Value, snapRadius) : null;
        return Hovered;
    }

    /// <summary>Nothing under the pointer (a window is up, a building is being placed).</summary>
    public void ClearHover() => Hovered = null;

    /// <summary>Shows the outline on what is hovered and what is picked, and takes it off everything else.</summary>
    public void ShowOutlines(Material outline, Material mask)
    {
        if (outline == null || mask == null)
        {
            return;
        }

        stale.Clear();
        foreach (var pair in outlines)
        {
            bool shown = pair.Key != null && (pair.Key == Hovered || pair.Key == Picked);
            if (!shown || OutlineCopies.AllGone(pair.Value))
            {
                stale.Add(pair.Key);
            }
        }

        foreach (Component gone in stale)
        {
            OutlineCopies.Destroy(outlines[gone]);
            outlines.Remove(gone);
        }

        Outline(Hovered, outline, mask);
        Outline(Picked, outline, mask);
    }

    /// <summary>Takes every outline away and forgets what was hovered and picked (the view closing).</summary>
    public void Clear()
    {
        foreach (var pair in outlines)
        {
            OutlineCopies.Destroy(pair.Value);
        }

        outlines.Clear();
        Hovered = null;
        Picked = null;
    }

    private void Outline(Component thing, Material outline, Material mask)
    {
        if (thing == null || outlines.ContainsKey(thing))
        {
            return;
        }

        List<OutlineCopies.Entry> made = OutlineCopies.Create(thing.transform, outline, mask, "Keeper Outline", "Keeper Mask",
            source => source.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
        OutlineCopies.SetShown(made, true);
        outlines[thing] = made;
    }

    /// <summary>The resident or building a collider belongs to, or null.</summary>
    public static Component Owner(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        ResidentNpc person = collider.GetComponentInParent<ResidentNpc>();
        if (person != null)
        {
            return person;
        }

        return collider.GetComponentInParent<BuildingObject>();
    }

    private Component Nearest(Vector3 spot, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(spot, radius, near, ~0, QueryTriggerInteraction.Collide);
        Component best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Component found = Owner(near[i]);
            if (found == null)
            {
                continue;
            }

            Vector3 apart = found.transform.position - spot;
            apart.y = 0f;
            // People before buildings: a person stands in a building's yard, and is the smaller target.
            float distance = apart.magnitude - (found is ResidentNpc ? radius : 0f);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = found;
            }
        }

        return best;
    }

    private sealed class HitOrder : IComparer<RaycastHit>
    {
        public static readonly HitOrder Instance = new HitOrder();

        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
