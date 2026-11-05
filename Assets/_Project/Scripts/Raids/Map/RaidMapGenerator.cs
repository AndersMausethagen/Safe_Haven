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
    public const string PondsName = "Ponds";
    public const string PiecesName = "Set Pieces";

    private const float RockClearing = 3f;
    private const float ReachSample = 8f;
    private const float PondMeshBeyond = 1.5f;
    private const int PondMeshPoints = 48;
    private const float PondDrinkSeconds = 1.5f;

    /// <summary>Gives the terrain the plan's heights, paint, trees and rocks. <paramref name="data"/> must be a runtime copy.</summary>
    public static void ShapeGround(RaidMapDefinition definition, TerrainData data, RaidLayoutPlan plan, RaidLayoutRules rules, int tier = 0)
    {
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        data.SetHeights(0, 0, field.Heights(data.heightmapResolution, data.size.y));

        // The map's own painting and scattering, on a copy that paints from the generated zones.
        field.Zones(out byte[] path, out byte[] forest, out byte[] clearing);
        RaidMapDefinition working = Object.Instantiate(definition);
        working.UseGenerated(field.Resolution, path, forest, clearing, plan.Mood, plan.Seed, tier);
        System.Func<Vector2, float> wetness = spot => plan.Wetness(spot, rules.WetReach, rules.DampGround);
        RaidMapSurface.Paint(data, working, wetness);
        var clearings = new List<RaidMapSurface.Clearing>(plan.Sites.Count);
        foreach (RaidSite site in plan.Sites)
        {
            clearings.Add(new RaidMapSurface.Clearing(site.Position * plan.Size, working.ClearAroundSites, RockClearing));
        }

        RaidMapSurface.Scatter(data, working, clearings, out _, wetness, plan.InWater);
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
        PlacePieces(map, plan, terrain, position => World(position));

        var rumours = new List<Vector3>();
        foreach (RaidSite site in plan.SitesOf(RaidSiteKind.Rumour))
        {
            rumours.Add(World(site.Position));
        }

        return rumours;
    }

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

    /// <summary>
    /// Lays the plan's ponds on the map, in place of an earlier attempt's. Kept out of the map's Content, which is snapped to the
    /// ground. <paramref name="meshes"/> holds the meshes made, for whoever owns the map to destroy.
    /// </summary>
    public static void PlacePonds(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, List<Mesh> meshes)
    {
        Transform old = map.transform.Find(PondsName);
        if (old != null)
        {
            // Detached first, as it lives on to the end of the frame and the name must be free for the new ones.
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        foreach (Mesh mesh in meshes)
        {
            Object.Destroy(mesh);
        }

        meshes.Clear();
        if (plan.Ponds.Count == 0)
        {
            return;
        }

        if (rules.PondMaterial == null)
        {
            Debug.LogWarning($"{rules.name} wants ponds but has no Pond Material, so there is only an empty hollow.", rules);
            return;
        }

        var holder = new GameObject(PondsName).transform;
        holder.SetParent(map.transform, false);
        Vector3 origin = map.Terrain.transform.position;
        for (int i = 0; i < plan.Ponds.Count; i++)
        {
            RaidLayoutPond pond = plan.Ponds[i];
            var water = new GameObject($"Pond {i + 1}");
            water.transform.SetParent(holder, false);
            water.transform.position = origin + new Vector3(pond.Position.x, pond.Level, pond.Position.y);
            Mesh mesh = PondMesh(pond);
            meshes.Add(mesh);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rules.PondMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<WaterSurface>().IsStill = true;
            if (rules.PondDrink > 0f || rules.PondFillItem != null)
            {
                AddDrinkingPlace(water.transform, pond, rules, origin);
            }
        }
    }

    // Its own object, as a hovered Interactable outlines every renderer under it and the water should not light up.
    private static void AddDrinkingPlace(Transform water, RaidLayoutPond pond, RaidLayoutRules rules, Vector3 origin)
    {
        var place = new GameObject("Drinking Place");
        place.transform.SetParent(water, false);
        place.AddComponent<RaidPondShore>().Configure(pond, origin);
        var source = place.AddComponent<WaterSource>();
        source.Configure("Pond", rules.PondDrink, rules.PondFillItem, rules.PondFillUpTo);
        place.GetComponent<Interactable>().Configure(InteractionAnimation.GatherLow, PondDrinkSeconds, rules.PondReach, false);
    }

    // A disc running a little past the shore, under the banks: the terrain draws the waterline.
    private static Mesh PondMesh(RaidLayoutPond pond)
    {
        var vertices = new Vector3[PondMeshPoints + 1];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[PondMeshPoints * 3];
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < PondMeshPoints; i++)
        {
            Vector2 rim = pond.Rim(i * Mathf.PI * 2f / PondMeshPoints, PondMeshBeyond) - pond.Position;
            vertices[i + 1] = new Vector3(rim.x, 0f, rim.y);
            normals[i + 1] = Vector3.up;
            uvs[i + 1] = new Vector2(0.5f + rim.x * 0.5f / pond.Reach, 0.5f + rim.y * 0.5f / pond.Reach);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % PondMeshPoints + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        var mesh = new Mesh { name = "Pond" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>True when the navmesh leads from the start to every exit (GDD §7.6: a reachable extraction must always exist).</summary>
    public static bool ExitsReachable(RaidMap map, RaidLayoutPlan plan)
    {
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => RaidMapSurface.Ground(terrain, origin + new Vector3(position.x * plan.Size, 0f, position.y * plan.Size));
        var path = new NavMeshPath();
        bool Walkable(Vector2 from, Vector2 to) =>
            NavMesh.SamplePosition(World(from / plan.Size), out NavMeshHit a, ReachSample, NavMesh.AllAreas)
            && NavMesh.SamplePosition(World(to / plan.Size), out NavMeshHit b, ReachSample, NavMesh.AllAreas)
            && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete;

        // Clearing to clearing along the trails, as the player goes. One long search runs out of nodes on a large map and reports a partial path even where the way is open.
        bool[] reached = RoomsReached(plan, Walkable);
        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            if (plan.Rooms[i].Role == RaidRoomRole.Exit && !reached[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Which clearings can be walked to from the start, given whether the way between two is open.</summary>
    public static bool[] RoomsReached(RaidLayoutPlan plan, System.Func<Vector2, Vector2, bool> walkable)
    {
        var reached = new bool[plan.Rooms.Count];
        var open = new Queue<int>();
        int start = plan.Rooms.IndexOf(plan.Start);
        reached[start] = true;
        open.Enqueue(start);
        while (open.Count > 0)
        {
            int room = open.Dequeue();
            foreach ((int a, int b) in plan.Links)
            {
                int other = a == room ? b : b == room ? a : -1;
                if (other >= 0 && !reached[other] && walkable(plan.Rooms[room].Position, plan.Rooms[other].Position))
                {
                    reached[other] = true;
                    open.Enqueue(other);
                }
            }
        }

        return reached;
    }
}
