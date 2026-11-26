using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes a RaidLayoutPlan real while a raid loads: shapes and paints the terrain, grows the forest on it, and moves everything
/// that stands on the map to its new place: the start, the exits, the spawn groups' points and whatever carries a
/// RaidMapAnchor. Spawn lists then fill the points as on any map, so what spawns stays the raid's own. RaidMap calls this
/// and rebuilds the navmesh afterwards.
/// <para>
/// Shaping the ground and the order of the rest are here; the rest is in parts: .Points (start, exits, groups,
/// anchors, set pieces), .Scenery (boulders, landmarks, lookouts, fog), .Water (ponds, ravine streams and falls, rivers)
/// and .Reach (trails cleared, exits reachable).
/// </para>
/// </summary>
public static partial class RaidMapGenerator
{
    public const string RavinesName = "Ravines";
    public const string BouldersName = "Boulders";

    private const float RockClearing = 3f;
    private const float TerrainHeadroom = 4f;
    /// <summary>The most metres across a boulder lying about may be, so it keeps off trails and clearings (RaidLayoutPlanner's clearances).</summary>
    public const float BoulderWidest = 9f;

    /// <summary>
    /// Gives the terrain the plan's heights, paint, trees and rocks. <paramref name="data"/> must be a runtime copy. The field and the heights
    /// can come worked out already (RaidMap does them on a worker thread); returns how long each part took. <paramref name="vergeRoom"/>
    /// is where the paths' grassy verge may grow, from the paint (RaidMapSurface.Paint), for the grass grown afterwards. The set dressing's
    /// heroes that stand as objects are added to <paramref name="heroes"/> for <see cref="PlaceDressing"/>, and the band's windthrow logs
    /// to <paramref name="barrierLogs"/> for <see cref="PlaceBarriers"/>.
    /// </summary>
    public static string ShapeGround(RaidMapDefinition definition, TerrainData data, RaidLayoutPlan plan, RaidLayoutRules rules,
        out byte[,] vergeRoom, int tier = 0, RaidLayoutField field = null, float[,] metres = null, float highest = 0f,
        List<RaidMapDefinition.HeroObject> heroes = null, List<RaidMapSurface.LogObject> barrierLogs = null)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        field ??= RaidLayoutField.Build(plan, rules);
        if (metres == null)
        {
            metres = field.Metres(data.heightmapResolution, out highest);
        }

        // Hills, cliffs, ravines and spires can reach higher than the map was made for: the copy grows taller rather than cut them flat.
        if (highest > data.size.y - TerrainHeadroom)
        {
            data.size = new Vector3(data.size.x, Mathf.Ceil(highest + TerrainHeadroom), data.size.z);
        }

        data.SetHeights(0, 0, RaidLayoutField.Scaled(metres, data.size.y));
        long heights = clock.ElapsedMilliseconds;

        // The map's own painting and scattering, on a copy that paints from the generated zones.
        field.Zones(out byte[] path, out byte[] forest, out byte[] clearing);
        RaidMapDefinition working = Object.Instantiate(definition);
        // Roads are worked out only for a map that paints them (Road Layer, G5).
        byte[] road = definition.RoadLayer != null ? field.RoadZone() : null;
        working.UseGenerated(field.Resolution, path, forest, clearing, plan.Mood, plan.Seed, tier, road);
        System.Func<Vector2, float> wetness = plan.Rivers.Count > 0
            ? spot => Mathf.Max(plan.Wetness(spot, rules.WetReach, rules.DampGround), field.RiverWetness(spot, rules.WetReach))
            : spot => plan.Wetness(spot, rules.WetReach, rules.DampGround);
        vergeRoom = RaidMapSurface.Paint(data, working, wetness, field.WaterNear, plan.Barriers, plan.BarrierGaps);
        long paint = clock.ElapsedMilliseconds;
        var clearings = new List<RaidMapSurface.Clearing>(plan.Sites.Count);
        foreach (RaidSite site in plan.Sites)
        {
            clearings.Add(new RaidMapSurface.Clearing(site.Position * plan.Size, working.ClearAroundSites, RockClearing));
        }

        System.Func<Vector2, bool> keepClear = plan.Ravines.Count > 0 || plan.Spires.Count > 0 || plan.Rivers.Count > 0
            ? spot => plan.InWater(spot) || field.KeepsClear(spot)
            : plan.InWater;

        // On a map of several regions each favours its own trees (its mood's weights), where the map's own list allows them at this tier.
        System.Func<Vector2, int, float> treeWeight = null;
        if (plan.Regions.Count > 1)
        {
            treeWeight = (spot, index) =>
            {
                RaidMapDefinition.Scatter scatter = index < definition.Trees.Count ? definition.Trees[index] : null;
                if (scatter == null || !scatter.AppearsAt(tier))
                {
                    return 0f;
                }

                RaidMapMood mood = plan.MoodAt(spot);
                return index < mood.treeWeights.Count ? mood.treeWeights[index] : scatter.weight;
            };
        }

        // Stones by the water (the map's Shore list) go by the field's ponds, rivers and fen.
        // The set dressing (G9) measures from walkable ground; only a map with heroes or mids asks.
        System.Func<Vector2, float> walked = working.Heroes.Count > 0 || working.Mids.Count > 0
            ? spot => field.WalkedDistance(spot.x / plan.Size, spot.y / plan.Size)
            : (System.Func<Vector2, float>)null;
        RaidMapSurface.Scatter(data, working, clearings, out _, wetness, keepClear, treeWeight, field.WaterNear, walked, heroes, plan.Barriers, plan.BarrierGaps, barrierLogs);
        Object.Destroy(working);
        return $"heights {heights} ms, paint {paint - heights} ms, scatter {clock.ElapsedMilliseconds - paint} ms";
    }

    /// <summary>Moves what stands on the map to the plan's places. Returns where the rumours' sites go; <paramref name="places"/>, when
    /// given, gets every site with where it stands and what took it.</summary>
    public static List<Vector3> PlaceContent(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, List<RaidPlace> places = null)
    {
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => RaidMapSurface.Ground(terrain, origin + new Vector3(position.x * plan.Size, 0f, position.y * plan.Size));

        PlaceStart(map, plan, World);
        PlaceExits(map, plan, terrain, World);
        PlaceGroups(map, plan, rules, terrain, World, places);
        PlaceAnchors(plan, terrain, World);
        PlaceFog(map, plan, World);
        RaidMapSurface.SnapToGround(map.Content, terrain);
        PlacePieces(map, plan, terrain, position => World(position));
        PlaceClutter(map, plan, rules, terrain, World);
        PlaceBoulders(map, plan, rules);

        var rumours = new List<Vector3>();
        foreach (RaidSite site in plan.SitesOf(RaidSiteKind.Rumour))
        {
            rumours.Add(World(site.Position));
        }

        return rumours;
    }
}
