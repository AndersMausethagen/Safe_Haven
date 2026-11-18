using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where things stand on the made map: the start, the exits spread over the far sites, the spawn groups' points (a
/// ravine's floor for those down a way in), set pieces and whatever carries a RaidMapAnchor. Part of RaidMapGenerator,
/// in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    public const string PiecesName = "Set Pieces";

    // Each part of a set-piece stands on the ground by itself, so a ruin sits into the hill and doesn't hang off it. Laid out anew, it starts over.
    private static void PlacePieces(RaidMap map, RaidLayoutPlan plan, Terrain terrain, System.Func<Vector2, Vector3> world)
    {
        Transform old = map.transform.Find(PiecesName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        var holder = new GameObject(PiecesName).transform;
        holder.SetParent(map.transform, false);
        foreach (RaidLayoutPiece piece in plan.Pieces)
        {
            Vector3 at = world(piece.Position / plan.Size);
            GameObject made = Object.Instantiate(piece.Definition.Prefab, at, Quaternion.Euler(0f, piece.Turn, 0f), holder);
            made.name = piece.Definition.DisplayName;
            foreach (Transform part in made.transform)
            {
                Vector3 position = part.position;
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y + part.localPosition.y;
                part.position = position;
            }

            made.AddComponent<RaidSetPieceSite>().Bind(piece.Definition);
            if (piece.Definition.OwnsClearing && piece.WallRadius > 0f)
            {
                PlaceCompoundWall(made.transform, piece, plan, terrain);
            }
        }
    }

    // A compound's wall leaves no piece on ground steeper than this (a wall up a bank floats or buries itself).
    private const float CompoundWallSteepest = 30f;

    /// <summary>
    /// A compound's ring (Docs/WorldDesignReview_2026-11-17.md G3): its wall pieces where the plan put them (RaidLayoutPlanner.CompoundWall:
    /// clear of its openings and of every trail), each along the ring, on the ground, and none in water or on a steep bank. Solid, so the navmesh built after the map's content leaves the ways in as chokepoints. From the
    /// map's seed: same seed, same wall.
    /// </summary>
    private static void PlaceCompoundWall(Transform compound, RaidLayoutPiece piece, RaidLayoutPlan plan, Terrain terrain)
    {
        RaidSetPieceDefinition definition = piece.Definition;
        var holder = new GameObject("Wall").transform;
        holder.SetParent(compound, false);
        holder.rotation = Quaternion.identity;
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Compound Wall " + piece.Position));
        Vector3 origin = terrain.transform.position;
        TerrainData data = terrain.terrainData;
        float radius = piece.WallRadius;
        foreach (float angle in piece.Wall)
        {
            float rad = angle * Mathf.Deg2Rad;
            var outward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 spot = piece.Position + outward * radius;
            if (spot.x < 1f || spot.y < 1f || spot.x > plan.Size - 1f || spot.y > plan.Size - 1f || plan.InRiver(spot, 0.5f) || plan.InWater(spot)
                || data.GetSteepness(spot.x / plan.Size, spot.y / plan.Size) > CompoundWallSteepest)
            {
                continue;
            }

            GameObject kind = definition.WallPieces[random.Next(definition.WallPieces.Count)];
            if (kind == null)
            {
                continue;
            }

            var at = origin + new Vector3(spot.x, 0f, spot.y);
            at.y = terrain.SampleHeight(at) + origin.y;
            // Local X along the ring, local Z outward.
            GameObject made = Object.Instantiate(kind, at, Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y), Vector3.up), holder);
            made.name = kind.name;
        }
    }

    /// <summary>True when <paramref name="angle"/> (degrees) lies within <paramref name="clear"/> of any opening. Pure, for tests.</summary>
    public static bool CompoundOpenAt(float angle, IReadOnlyList<float> openings, float clear)
    {
        foreach (float opening in openings)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angle, opening)) < clear)
            {
                return true;
            }
        }

        return false;
    }

    private const string ClutterName = "Clutter";

    // Crates, barrels and fence sections about the camps, caches and event sites: things to break, now and then with
    // something inside. On the ground, off steep slopes, never in the middle of the site.
    private static void PlaceClutter(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, Terrain terrain, System.Func<Vector2, Vector3> world)
    {
        Transform old = map.transform.Find(ClutterName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        IReadOnlyList<GameObject> kinds = rules.SiteClutter;
        if (kinds == null || kinds.Count == 0)
        {
            return;
        }

        var holder = new GameObject(ClutterName).transform;
        holder.SetParent(map.transform, false);
        var random = new System.Random(plan.Seed * 31 + 7);
        Vector2Int count = rules.ClutterPerSite;
        Vector2 reach = rules.ClutterReach;
        foreach (RaidSite site in plan.Sites)
        {
            if (site.Kind != RaidSiteKind.EnemyCamp && site.Kind != RaidSiteKind.SupplyCache && site.Kind != RaidSiteKind.EventSite)
            {
                continue;
            }

            Vector3 centre = world(site.Position);
            int many = random.Next(count.x, count.y + 1);
            for (int i = 0; i < many; i++)
            {
                GameObject kind = kinds[random.Next(kinds.Count)];
                if (kind == null)
                {
                    continue;
                }

                float turn = (float)(random.NextDouble() * Mathf.PI * 2.0);
                float far = Mathf.Lerp(reach.x, reach.y, (float)random.NextDouble());
                Vector3 at = centre + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * far;
                Vector3 local = at - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                Vector2 uv = new Vector2(local.x / size.x, local.z / size.z);
                if (uv.x < 0.02f || uv.y < 0.02f || uv.x > 0.98f || uv.y > 0.98f || terrain.terrainData.GetSteepness(uv.x, uv.y) > 22f)
                {
                    continue;
                }

                at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
                GameObject made = Object.Instantiate(kind, at, Quaternion.Euler(0f, (float)(random.NextDouble() * 360.0), 0f), holder);
                made.name = kind.name;
            }
        }
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

    /// <summary>
    /// Which of these sites, sorted west to east, the exits take, in that order. With sites to spare: both ends, then each time whichever is
    /// furthest from those taken, so no two ways out stand side by side.
    /// </summary>
    public static int[] SpreadOver(IReadOnlyList<Vector2> sites, int exits)
    {
        int wanted = Mathf.Min(exits, sites.Count);
        var taken = new List<int>(wanted);
        if (wanted > 0)
        {
            taken.Add(0);
        }

        if (wanted > 1)
        {
            taken.Add(sites.Count - 1);
        }

        while (taken.Count < wanted)
        {
            int furthest = -1;
            float best = -1f;
            for (int i = 0; i < sites.Count; i++)
            {
                if (taken.Contains(i))
                {
                    continue;
                }

                float nearest = float.MaxValue;
                foreach (int one in taken)
                {
                    nearest = Mathf.Min(nearest, (sites[i] - sites[one]).sqrMagnitude);
                }

                if (nearest > best)
                {
                    best = nearest;
                    furthest = i;
                }
            }

            taken.Add(furthest);
        }

        taken.Sort();
        return taken.ToArray();
    }

    /// <summary>
    /// A larger map's extra exits, copied from those the raid has. They go between the raid's own along the map, so what is called West
    /// still stands furthest west. A copy with a quest's waypoint on it would finish that quest in the wrong place, so those aren't copied.
    /// </summary>
    public static void AddExits(RaidMap map, IReadOnlyList<string> names)
    {
        var own = new List<ExtractionPoint>(map.Content.GetComponentsInChildren<ExtractionPoint>(true));
        own.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        List<ExtractionPoint> patterns = own.FindAll(exit => exit.GetComponentInChildren<QuestWaypoint>(true) == null && exit.name.IndexOf("West", System.StringComparison.OrdinalIgnoreCase) < 0);
        if (patterns.Count == 0 || names == null)
        {
            return;
        }

        float gap = own.Count > 1 ? (own[own.Count - 1].transform.position.x - own[0].transform.position.x) / (names.Count + 1) : 1f;
        for (int i = 0; i < names.Count; i++)
        {
            ExtractionPoint copy = patterns[i % patterns.Count].CopyAs(names[i]);
            Vector3 place = copy.transform.position;
            place.x = own[0].transform.position.x + gap * (i + 1) + 0.01f * i;
            copy.transform.position = place;
        }
    }

    // West to east on the plan as in the scene, so a beacon called West stands in the west.
    private static void PlaceExits(RaidMap map, RaidLayoutPlan plan, Terrain terrain, System.Func<Vector2, Vector3> world)
    {
        var exits = new List<ExtractionPoint>(map.Content.GetComponentsInChildren<ExtractionPoint>(true));
        exits.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        List<RaidSite> sites = plan.SitesOf(RaidSiteKind.Extraction);
        sites.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
        int[] taken = SpreadOver(sites.ConvertAll(site => site.Position), exits.Count);
        for (int i = 0; i < taken.Length; i++)
        {
            Transform exit = exits[i].transform;
            Vector3 moved = world(sites[taken[i]].Position) - exit.position;
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
                    point.transform.position = OntoDescentFloor(plan, terrain, RaidMapSurface.SitePoint(terrain, centre, rule, i, k));
                }
            }
        }
    }

    // A site on the floor of a ravine with a way down spreads its points along the floor, not up its walls.
    private static Vector3 OntoDescentFloor(RaidLayoutPlan plan, Terrain terrain, Vector3 point)
    {
        Vector3 origin = terrain.transform.position;
        var flat = new Vector2(point.x - origin.x, point.z - origin.z);
        if (!plan.OverRavine(flat, out RaidLayoutRavine ravine, out _, RaidLayoutRavine.RagReach) || !ravine.Descent)
        {
            return point;
        }

        ravine.Nearest(flat, out int segment, out float along, out float distance);
        Vector2 middle = Vector2.Lerp(ravine.Line[segment], ravine.Line[segment + 1], along);
        float half = RaidLayoutRavine.At(ravine.HalfWidths, segment, along);
        float floorHalf = Mathf.Max(0.3f, (half - RaidLayoutRavine.WallRun(half)) * 0.5f);
        Vector2 placed = distance > floorHalf ? middle + (flat - middle).normalized * floorHalf : flat;
        return RaidMapSurface.Ground(terrain, origin + new Vector3(placed.x, 0f, placed.y));
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
}
