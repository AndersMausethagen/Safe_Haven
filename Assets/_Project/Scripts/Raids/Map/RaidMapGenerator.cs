using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes a RaidLayoutPlan real while a raid loads: shapes and paints the terrain, grows the forest on it, and moves everything
/// that stands on the map to its new place: the start, the exits, the spawn groups' points and whatever carries a
/// RaidMapAnchor. Spawn lists then fill the points as on any map, so what spawns stays the raid's own. RaidMap calls this
/// and rebuilds the navmesh afterwards.
/// </summary>
public static class RaidMapGenerator
{
    private const float RockClearing = 3f;
    private const float ReachSample = 8f;

    /// <summary>Gives the terrain the plan's heights, paint, trees and rocks. <paramref name="data"/> must be a runtime copy.</summary>
    public static void ShapeGround(RaidMapDefinition definition, TerrainData data, RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        data.SetHeights(0, 0, field.Heights(data.heightmapResolution, data.size.y));

        // The map's own painting and scattering, on a copy that paints from the generated zones.
        field.Zones(out byte[] path, out byte[] forest, out byte[] clearing);
        RaidMapDefinition working = Object.Instantiate(definition);
        working.UseGenerated(field.Resolution, path, forest, clearing, plan.Mood, plan.Seed);
        RaidMapSurface.Paint(data, working);
        var clearings = new List<RaidMapSurface.Clearing>(plan.Sites.Count);
        foreach (RaidSite site in plan.Sites)
        {
            clearings.Add(new RaidMapSurface.Clearing(site.Position * plan.Size, working.ClearAroundSites, RockClearing));
        }

        RaidMapSurface.Scatter(data, working, clearings, out _);
        Object.Destroy(working);
    }

    /// <summary>Moves what stands on the map to the plan's places. Returns where the rumours' sites go.</summary>
    public static List<Vector3> PlaceContent(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => RaidMapSurface.Ground(terrain, origin + new Vector3(position.x * plan.Size, 0f, position.y * plan.Size));

        PlaceStart(map, plan, World);
        PlaceExits(map, plan, terrain, World);
        PlaceGroups(map, plan, rules, terrain, World);
        PlaceAnchors(plan, terrain, World);
        PlaceFog(map, plan, World);
        RaidMapSurface.SnapToGround(map.Content, terrain);

        var rumours = new List<Vector3>();
        foreach (RaidSite site in plan.SitesOf(RaidSiteKind.Rumour))
        {
            rumours.Add(World(site.Position));
        }

        return rumours;
    }

    private static void PlaceStart(RaidMap map, RaidLayoutPlan plan, System.Func<Vector2, Vector3> world)
    {
        if (map.PlayerStart == null)
        {
            return;
        }

        map.PlayerStart.position = world(plan.Start.Position / plan.Size);
        Vector3 toCentre = Vector3.ProjectOnPlane(map.Center - map.PlayerStart.position, Vector3.up);
        if (toCentre.sqrMagnitude > 0.01f)
        {
            map.PlayerStart.rotation = Quaternion.LookRotation(toCentre);
        }
    }

    // West to east on the plan as in the scene, so a beacon called West stands in the west.
    private static void PlaceExits(RaidMap map, RaidLayoutPlan plan, Terrain terrain, System.Func<Vector2, Vector3> world)
    {
        var exits = new List<ExtractionPoint>(map.Content.GetComponentsInChildren<ExtractionPoint>(true));
        exits.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        List<RaidSite> sites = plan.SitesOf(RaidSiteKind.Extraction);
        sites.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
        for (int i = 0; i < exits.Count && i < sites.Count; i++)
        {
            Transform exit = exits[i].transform;
            Vector3 moved = world(sites[i].Position) - exit.position;
            exit.position += moved;
            SpawnGroup alarm = exits[i].Alarm;
            if (alarm != null && !alarm.IsBakedFromMap)
            {
                alarm.transform.position += new Vector3(moved.x, 0f, moved.z);
                foreach (Transform point in alarm.transform)
                {
                    point.position = RaidMapSurface.Ground(terrain, point.position);
                }
            }
        }
    }

    private static void PlaceGroups(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, Terrain terrain, System.Func<Vector2, Vector3> world)
    {
        var byKind = new Dictionary<RaidSiteKind, List<SpawnGroup>>();
        foreach (SpawnGroup group in map.Content.GetComponentsInChildren<SpawnGroup>(true))
        {
            RaidSiteKind kind = group.Site;
            foreach (RaidGroupRule rule in rules.Groups)
            {
                if (rule != null && rule.group == group.name)
                {
                    kind = rule.site;
                }
            }

            if (kind == RaidSiteKind.None)
            {
                continue;
            }

            if (!byKind.TryGetValue(kind, out List<SpawnGroup> groups))
            {
                byKind[kind] = groups = new List<SpawnGroup>();
            }

            groups.Add(group);

            // Points from the painted map, or an earlier attempt, simply stop counting (SpawnGroup.CollectPoints).
            foreach (Transform point in group.transform)
            {
                point.gameObject.SetActive(false);
            }
        }

        foreach (KeyValuePair<RaidSiteKind, List<SpawnGroup>> pair in byKind)
        {
            List<SpawnGroup> groups = pair.Value;
            groups.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            List<RaidSite> sites = plan.SitesOf(pair.Key);
            int[] owners = RaidMapMarkers.ShareOut(sites.Count, groups.Count);
            RaidMapDefinition.SiteRule rule = map.Definition.RuleFor(pair.Key);
            for (int i = 0; i < sites.Count; i++)
            {
                Vector3 centre = world(sites[i].Position);
                for (int k = 0; k < rule.pointsPerSite; k++)
                {
                    var point = new GameObject($"{pair.Key} {i + 1}.{k + 1}");
                    point.transform.SetParent(groups[owners[i]].transform, false);
                    point.transform.position = RaidMapSurface.SitePoint(terrain, centre, rule, i, k);
                }
            }
        }
    }

    private static void PlaceAnchors(RaidLayoutPlan plan, Terrain terrain, System.Func<Vector2, Vector3> world)
    {
        var anchors = new List<RaidMapAnchor>(Object.FindObjectsByType<RaidMapAnchor>(FindObjectsInactive.Include));
        anchors.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var used = new Dictionary<RaidSiteKind, int>();
        foreach (RaidMapAnchor anchor in anchors)
        {
            if (anchor.Follows != null)
            {
                continue;
            }

            List<RaidSite> sites = plan.SitesOf(anchor.Site);
            if (sites.Count == 0)
            {
                Debug.LogWarning($"The generated map has no {anchor.Site} site for {anchor.name}.", anchor);
                continue;
            }

            used.TryGetValue(anchor.Site, out int index);
            used[anchor.Site] = index + 1;
            Vector3 spot = world(sites[index % sites.Count].Position);
            Vector3 moved = spot - anchor.transform.position;
            anchor.transform.position += moved;
            if (anchor.Ring != null)
            {
                SetInRing(anchor, spot, terrain);
            }
        }

        foreach (RaidMapAnchor anchor in anchors)
        {
            if (anchor.Follows != null)
            {
                anchor.transform.position = RaidMapSurface.Ground(terrain, anchor.Follows.position);
            }
        }
    }

    private static void SetInRing(RaidMapAnchor anchor, Vector3 centre, Terrain terrain)
    {
        int count = anchor.Ring.childCount;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 spot = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * anchor.RingRadius;
            anchor.Ring.GetChild(i).position = RaidMapSurface.Ground(terrain, spot);
        }
    }

    // The scene's banks of fog lie over clearings, plus as many more as the mood wants.
    private static void PlaceFog(RaidMap map, RaidLayoutPlan plan, System.Func<Vector2, Vector3> world)
    {
        FogArea[] areas = map.Content.GetComponentsInChildren<FogArea>(true);
        if (areas.Length == 0)
        {
            return;
        }

        var rooms = plan.Rooms.FindAll(room => room.Role == RaidRoomRole.Plain);
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Fog"));
        List<int> order = RaidLayout.PickIndices(random, rooms.Count, rooms.Count);
        int wanted = areas.Length + plan.Mood.fogBanks;
        for (int i = 0; i < wanted && i < order.Count; i++)
        {
            FogArea area = i < areas.Length ? areas[i] : Object.Instantiate(areas[0], areas[0].transform.parent);
            area.transform.position = world(rooms[order[i]].Position / plan.Size);
        }
    }

    /// <summary>True when the navmesh leads from the start to every exit (GDD §7.6: a reachable extraction must always exist).</summary>
    public static bool ExitsReachable(RaidMap map, RaidLayoutPlan plan)
    {
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => RaidMapSurface.Ground(terrain, origin + new Vector3(position.x * plan.Size, 0f, position.y * plan.Size));
        if (!NavMesh.SamplePosition(World(plan.Start.Position / plan.Size), out NavMeshHit from, ReachSample, NavMesh.AllAreas))
        {
            return false;
        }

        var path = new NavMeshPath();
        foreach (RaidSite exit in plan.SitesOf(RaidSiteKind.Extraction))
        {
            if (!NavMesh.SamplePosition(World(exit.Position), out NavMeshHit to, ReachSample, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
        }

        return true;
    }
}
