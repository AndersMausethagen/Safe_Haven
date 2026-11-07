using System.Linq;
using UnityEngine;

/// <summary>
/// What can be gathered: maple groves, spawn zones and resource clusters in their bands, some gathered into glades that
/// suit them. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private void PlaceGroves()
    {
        int count = random.Next(Recipe.groves.x, Recipe.groves.y + 1);
        float turn = Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                float bearing = turn + i * 360f / count + Range(-30f, 30f);
                float radius = Range(30f, 50f);
                Vector2 at = Dir(bearing) * Range(Recipe.buildable + radius * 0.6f, Recipe.haven + 25f);
                if (!InWater(at, radius * 0.5f))
                {
                    Groves.Add(Loop(at, radius));
                    break;
                }
            }
        }
    }

    private void PlaceZones()
    {
        foreach (HavenCreatureBand band in Recipe.creatures)
        {
            if (band == null)
            {
                continue;
            }

            for (int i = 0; i < band.zones; i++)
            {
                // Lairs first, then any dry glade still free: open ground to fight in, reached by a trail.
                Glade den = Glades.Find(glade => !glade.HasZone && glade.Kind == GladeKind.Lair && InBand(glade.Centre, band.distance))
                            ?? Glades.Find(glade => !glade.HasZone && glade.Kind != GladeKind.Wet && InBand(glade.Centre, band.distance));
                if (den != null)
                {
                    den.HasZone = true;
                    Zones.Add(new Zone { Band = band, At = den.Centre, Radius = Mathf.Max(9f, den.Radius * 0.55f) });
                    continue;
                }

                for (int tries = 0; tries < Tries; tries++)
                {
                    Vector2 at = Around(band.distance.x, Mathf.Min(band.distance.y, MaxReach));
                    float radius = Range(9f, 15f);
                    if (at.magnitude < Recipe.haven || Near(Paths, at, radius + 4f) || Near(Rivers, at, radius + 6f) || InWater(at, radius + 6f)
                        || Ravines.Exists(ravine => NearLine(ravine.Line, at, radius + 10f))
                        || Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, at) < outcrop.Radius + radius + 4f)
                        || Zones.Exists(zone => Vector2.Distance(zone.At, at) < 40f))
                    {
                        continue;
                    }

                    Zones.Add(new Zone { Band = band, At = at, Radius = radius });
                    break;
                }
            }
        }
    }

    private void PlaceNodes()
    {
        foreach (HavenResourceBand band in Recipe.resources)
        {
            if (band == null)
            {
                continue;
            }

            for (int i = 0; i < band.clusters; i++)
            {
                if (!band.mountainOnly && Range(0f, 1f) < 0.5f && TryGladeCluster(band))
                {
                    continue;
                }

                for (int tries = 0; tries < Tries; tries++)
                {
                    Vector2 at = Around(band.distance.x, Mathf.Min(band.distance.y, MaxReach));
                    if (!NodeFits(at, band) || (band.mountainOnly && MountainWeight(at) < 0.35f))
                    {
                        continue;
                    }

                    int count = random.Next(band.perCluster.x, band.perCluster.y + 1);
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 spot = k == 0 ? at : at + Dir(Range(0f, 360f)) * Range(1.5f, 6f);
                        if (k == 0 || NodeFits(spot, band))
                        {
                            Nodes.Add(new Node { Band = band, At = spot });
                        }
                    }

                    break;
                }
            }
        }
    }

    private bool InBand(Vector2 point, Vector2 band) => point.magnitude >= band.x && point.magnitude <= band.y;

    // A cluster round the edge of a glade that suits it: stone in the rocky ones, forage and plants in meadows and by pools.
    private bool TryGladeCluster(HavenResourceBand band)
    {
        var suited = Glades.FindAll(glade => InBand(glade.Centre, band.distance) && Suits(glade.Kind, band.kind));
        if (suited.Count == 0)
        {
            return false;
        }

        Glade glade = suited[random.Next(suited.Count)];
        int count = random.Next(band.perCluster.x, band.perCluster.y + 1);
        int placed = 0;
        for (int k = 0; k < count * 3 && placed < count; k++)
        {
            Vector2 spot = glade.Centre + Dir(Range(0f, 360f)) * glade.Radius * Range(0.35f, 0.85f);
            if (NodeFits(spot, band) && !Nodes.Exists(node => (node.At - spot).sqrMagnitude < 4f))
            {
                Nodes.Add(new Node { Band = band, At = spot });
                placed++;
            }
        }

        return placed > 0;
    }

    private static bool Suits(GladeKind glade, ResourceNodeKind kind) =>
        kind == ResourceNodeKind.Rock ? glade == GladeKind.Rocky
        : kind == ResourceNodeKind.Forage || kind == ResourceNodeKind.Plant ? glade == GladeKind.Meadow || glade == GladeKind.Wet
        : glade != GladeKind.Lair;

    private bool NodeFits(Vector2 at, HavenResourceBand band) =>
        at.magnitude > Recipe.buildable && !Near(Paths, at, 3f) && !Near(Rivers, at, 6f) && !InWater(at, 2f)
        && !Ravines.Exists(ravine => NearLine(ravine.Line, at, 10f))
        && !Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, at) < outcrop.Radius + 2f)
        && !Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing);
}
