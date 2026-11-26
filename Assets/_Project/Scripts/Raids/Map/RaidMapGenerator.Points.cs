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
        var placed = new List<(float angle, Transform made)>();
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
            placed.Add((angle, made.transform));
        }

        PlaceGateLights(piece, placed);
    }

    /// <summary>
    /// The compound's gate lights (Docs/LookAndFeelResearch_2026-11-18.md L5): on the end post of every run of wall, where a way in
    /// begins (the ring leaves pieces out at its openings and wherever a trail or a steep bank comes, so the gaps are where the way in
    /// really is), turned in toward the yard. A piece's local X runs along the ring with the angle going down (its local Z is outward),
    /// so a run's end at its larger angle is that piece's -X end.
    /// </summary>
    private static void PlaceGateLights(RaidLayoutPiece piece, List<(float angle, Transform made)> placed)
    {
        RaidSetPieceDefinition definition = piece.Definition;
        if (definition.GateLight == null || placed.Count == 0 || piece.WallRadius <= 0f)
        {
            return;
        }

        float half = definition.PieceLength * 0.5f;
        float step = definition.PieceLength / piece.WallRadius * Mathf.Rad2Deg;
        Vector3 at = definition.GateLightAt;
        var angles = new List<float>(placed.Count);
        foreach ((float angle, Transform _) in placed)
        {
            angles.Add(angle);
        }

        foreach ((int index, int side) in WallEnds(angles, step))
        {
            Transform wall = placed[index].made;
            // side 1: the run ends at the piece's larger angle, its -X end.
            float end = side > 0 ? -(half - at.x) : half - at.x;
            Vector3 position = wall.TransformPoint(new Vector3(end, at.y, -at.z));
            GameObject light = Object.Instantiate(definition.GateLight, position, Quaternion.LookRotation(-wall.forward, Vector3.up), wall);
            light.name = definition.GateLight.name;
        }
    }

    /// <summary>The ends of a ring's runs of wall: each piece (by index) with no neighbour a step further round on that side (1: at a
    /// larger angle, -1: at a smaller), angles in degrees. A whole ring has none. Pure, for tests.</summary>
    public static List<(int index, int side)> WallEnds(IReadOnlyList<float> angles, float step)
    {
        var ends = new List<(int, int)>();
        for (int i = 0; i < angles.Count; i++)
        {
            foreach (int side in new[] { -1, 1 })
            {
                bool neighbour = false;
                for (int k = 0; k < angles.Count && !neighbour; k++)
                {
                    float delta = Mathf.DeltaAngle(angles[i], angles[k]) * side;
                    neighbour = k != i && delta > step * 0.5f && delta < step * 1.5f;
                }

                if (!neighbour)
                {
                    ends.Add((i, side));
                }
            }
        }

        return ends;
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

        var holder = new GameObject(ClutterName).transform;
        holder.SetParent(map.transform, false);
        IReadOnlyList<GameObject> kinds = rules.SiteClutter;
        if (kinds == null || kinds.Count == 0)
        {
            return;
        }

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

    // The steepest ground a dressing piece stands on (a lean-to on a bank leans out of it), the half-width of its footprint checked
    // for water, and how far it keeps from a trail's middle beyond the trail's own half-width.
    private const float DressingSteepest = 18f;
    private const float DressingHalfFootprint = 1.5f;
    private const float DressingFromTrail = 2f;
    private const int DressingTries = 8;
    public const string CampDressingName = "Camp Dressing";

    /// <summary>
    /// What makes a camp read as one (Docs/RaidLayoutAnalysis_2026-11-26.md, follow-up 2): a few of the rules' Camp Dressing round each
    /// camp, each kind once, spread round the ring beyond its creatures' points and stash, facing the middle. Called once the water stands
    /// (ponds, fen, rivers), so a piece keeps its whole footprint out of any water, off trails and off steep ground; solid, so the navmesh
    /// built after goes round it. From its own stream, so the clutter's rolls are as before.
    /// </summary>
    public static void PlaceCampDressing(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        Transform old = map.transform.Find(CampDressingName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        IReadOnlyList<GameObject> kinds = rules.CampDressing;
        if (kinds == null || kinds.Count == 0)
        {
            return;
        }

        var holder = new GameObject(CampDressingName).transform;
        holder.SetParent(map.transform, false);
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Camp Dressing"));
        Vector2Int count = rules.CampDressingPerCamp;
        Vector2 reach = rules.CampDressingReach;
        TerrainData data = terrain.terrainData;
        float trailKeep = rules.TrailHalfWidth + DressingHalfFootprint + DressingFromTrail;
        foreach (RaidSite site in plan.SitesOf(RaidSiteKind.EnemyCamp))
        {
            Vector3 centre = RaidMapSurface.Ground(terrain, origin + new Vector3(site.Position.x * plan.Size, 0f, site.Position.y * plan.Size));
            List<int> picks = RaidLayout.PickIndices(random, kinds.Count, Mathf.Min(kinds.Count, random.Next(count.x, count.y + 1)));
            float start = (float)(random.NextDouble() * Mathf.PI * 2.0);
            for (int i = 0; i < picks.Count; i++)
            {
                GameObject kind = kinds[picks[i]];
                // Spread round the ring, a little off even, so two pieces never stand together; where that spot won't do (a trail, a
                // bank, water), a little further round each try, within this piece's share of the ring.
                float turn = start + i * Mathf.PI * 2f / picks.Count + (float)(random.NextDouble() - 0.5) * 0.6f;
                float far = Mathf.Lerp(reach.x, reach.y, (float)random.NextDouble());
                float share = Mathf.PI * 2f / picks.Count;
                Vector3 at = centre;
                bool found = false;
                for (int attempt = 0; attempt < DressingTries && kind != null && !found; attempt++)
                {
                    float tried = turn + (attempt % 2 == 0 ? 1f : -1f) * ((attempt + 1) / 2) * share / DressingTries;
                    at = centre + new Vector3(Mathf.Cos(tried), 0f, Mathf.Sin(tried)) * far;
                    Vector3 local = at - terrain.transform.position;
                    var spot = new Vector2(local.x, local.z);
                    found = spot.x >= 3f && spot.y >= 3f && spot.x <= plan.Size - 3f && spot.y <= plan.Size - 3f
                        && !NearAny(plan.Trails, spot, trailKeep) && !NearAny(plan.Spurs, spot, trailKeep)
                        && data.GetSteepness(spot.x / data.size.x, spot.y / data.size.z) <= DressingSteepest && FootprintDry(plan, terrain, spot);
                }

                if (!found)
                {
                    continue;
                }

                at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
                Vector3 inward = centre - at;
                inward.y = 0f;
                GameObject made = Object.Instantiate(kind, at, Quaternion.LookRotation(inward.sqrMagnitude > 0.01f ? inward : Vector3.forward), holder);
                made.name = kind.name;
            }
        }
    }

    // The middle and four points round a piece's footprint: none in a pond, a river or any water surface standing on the map (a fen's).
    private static bool FootprintDry(RaidLayoutPlan plan, Terrain terrain, Vector2 spot)
    {
        Vector3 origin = terrain.transform.position;
        for (int i = -1; i < 4; i++)
        {
            Vector2 at = i < 0 ? spot : spot + new Vector2(Mathf.Cos(i * Mathf.PI * 0.5f), Mathf.Sin(i * Mathf.PI * 0.5f)) * DressingHalfFootprint;
            if (plan.InWater(at) || plan.InRiver(at, 0.5f) || !RaidSpots.IsDryEnough(RaidMapSurface.Ground(terrain, origin + new Vector3(at.x, 0f, at.y))))
            {
                return false;
            }
        }

        return true;
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

    // An every-site group's point stands this share of the site's spread to one side, between the ring's points, so a camp's stash
    // stands beside its guards rather than under them.
    private const float EverySiteAside = 0.6f;

    private static void PlaceGroups(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, Terrain terrain, System.Func<Vector2, Vector3> world,
        List<RaidPlace> places)
    {
        var byKind = new Dictionary<RaidSiteKind, List<SpawnGroup>>();
        var everySite = new Dictionary<RaidSiteKind, List<SpawnGroup>>();
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

            Dictionary<RaidSiteKind, List<SpawnGroup>> into = group.EverySite ? everySite : byKind;
            if (!into.TryGetValue(kind, out List<SpawnGroup> groups))
            {
                into[kind] = groups = new List<SpawnGroup>();
            }

            groups.Add(group);

            // Points from the painted map, or an earlier attempt, simply stop counting (SpawnGroup.CollectPoints).
            foreach (Transform point in group.transform)
            {
                point.gameObject.SetActive(false);
            }
        }

        var labelled = new HashSet<RaidSiteKind>();
        foreach (KeyValuePair<RaidSiteKind, List<SpawnGroup>> pair in byKind)
        {
            List<SpawnGroup> groups = pair.Value;
            groups.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            List<RaidSite> sites = plan.SitesOf(pair.Key);
            int[] owners = RaidMapMarkers.ShareOut(sites.ConvertAll(site => Vector2.Distance(site.Position * plan.Size, plan.Start.Position)),
                groups.ConvertAll(group => group.SiteShare), groups.ConvertAll(group => group.FarthestSites));
            RaidMapDefinition.SiteRule rule = map.Definition.RuleFor(pair.Key);
            labelled.Add(pair.Key);
            for (int i = 0; i < sites.Count; i++)
            {
                Vector3 centre = world(sites[i].Position);
                places?.Add(new RaidPlace(pair.Key, owners[i] >= 0 ? groups[owners[i]].name : null, centre, owners[i] >= 0 ? groups[owners[i]].MapIcon : null));
                if (owners[i] < 0)
                {
                    continue;
                }

                for (int k = 0; k < rule.pointsPerSite; k++)
                {
                    var point = new GameObject($"{pair.Key} {i + 1}.{k + 1}");
                    point.transform.SetParent(groups[owners[i]].transform, false);
                    point.transform.position = OntoDescentFloor(plan, terrain, RaidMapSurface.SitePoint(terrain, centre, rule, i, k));
                }
            }
        }

        foreach (KeyValuePair<RaidSiteKind, List<SpawnGroup>> pair in everySite)
        {
            List<RaidSite> sites = plan.SitesOf(pair.Key);
            RaidMapDefinition.SiteRule rule = map.Definition.RuleFor(pair.Key);
            foreach (SpawnGroup group in pair.Value)
            {
                for (int i = 0; i < sites.Count; i++)
                {
                    Vector3 centre = world(sites[i].Position);
                    // Square to the ring's first point (SitePoint turns the ring by the site's index).
                    float angle = i + Mathf.PI * 0.5f;
                    Vector3 aside = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (rule.spread * EverySiteAside);
                    var point = new GameObject($"{pair.Key} {i + 1} {group.name}");
                    point.transform.SetParent(group.transform, false);
                    point.transform.position = OntoDescentFloor(plan, terrain, RaidMapSurface.Ground(terrain, centre + aside));
                }
            }
        }

        // The kinds no group takes are places too (rumours, the apex, sides, exits).
        foreach (RaidSite site in plan.Sites)
        {
            if (site.Kind != RaidSiteKind.None && site.Kind != RaidSiteKind.PlayerStart && !labelled.Contains(site.Kind))
            {
                places?.Add(new RaidPlace(site.Kind, null, world(site.Position)));
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
