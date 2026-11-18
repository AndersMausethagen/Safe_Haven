using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a map's zones become what the player walks on: terrain paint, trees, rocks and logs, the invisible trunks the navmesh
/// cuts around, and the points of a site. Shared by the editor's Bake (RaidMapBaker) and by maps generated while a raid
/// loads (RaidMapGenerator), so a generated forest is made exactly like a painted one.
/// </summary>
public static class RaidMapSurface
{
    public const string TrunksName = "Tree Trunks";

    /// <summary>A spot (metres from the map's south-west corner) that trees and rocks keep away from, each by its own distance.</summary>
    public readonly struct Clearing
    {
        public readonly Vector2 Spot;
        public readonly float TreeRadius;
        public readonly float RockRadius;

        public Clearing(Vector2 spot, float treeRadius, float rockRadius)
        {
            Spot = spot;
            TreeRadius = treeRadius;
            RockRadius = rockRadius;
        }
    }

    // Layer 0 forest floor (moss), 1 path soil, 2 cliff rock, 3 litter (when the map has it). Rock follows the slope; soil
    // follows the paths and clearings and gives way to moss on wet ground; litter lies in patches under the densest canopy,
    // where it is dry; moss covers the rest.
    // Returns where the paths' grassy verge may grow, by alphamap cell (row, column; 255 all of it), for TerrainGrowth.Fill through
    // VergeRoomAt: the paint can't say it, as the path layer is half the paint at a trail's edge but also down the middle of a
    // narrow side path once its border is feathered, and wherever leaves drift over a trail (Docs/SystemReview2_2026-11-17.md S4).
    public static byte[,] Paint(TerrainData data, RaidMapDefinition definition, Func<Vector2, float> wetness = null, WaterNear water = null)
    {
        int resolution = data.alphamapResolution;
        int layers = data.alphamapLayers;
        bool hasLitter = definition.LitterLayer != null && layers >= 4;
        var weights = new float[resolution, resolution, layers];
        Vector2 cliff = definition.CliffSlope;
        float size = definition.Size;

        // Soil first, on its own, so its border can be feathered: the zones step from forest to path within one cell.
        var soils = new float[resolution, resolution];
        // The trails alone, feathered as the soil is: Edge Blend's step follows a path's border, not a clearing's or open ground's.
        var trails = definition.EdgeBlend > 0f ? new float[resolution, resolution] : null;
        var wets = new float[resolution, resolution];
        var verges = new byte[resolution, resolution];
        // Roads (G5): a generated map's two ways to the exit ahead, painted with the Road Layer, feathered as the soil is. Only when the map
        // has both the layer and the zone, so every other map paints as it did.
        int roadIndex = definition.RoadLayerIndex;
        var roads = roadIndex >= 0 && roadIndex < layers && definition.Zones.Exists(zone => zone != null && zone.role == RaidZoneRole.Road)
            ? new float[resolution, resolution] : null;
        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                float u = (column + 0.5f) / resolution;
                float v = (row + 0.5f) / resolution;
                if (roads != null)
                {
                    roads[row, column] = definition.Strength(RaidZoneRole.Road, u, v);
                }

                float path = definition.Strength(RaidZoneRole.Path, u, v);
                float forest = definition.Strength(RaidZoneRole.Forest, u, v);
                float clearing = definition.Strength(RaidZoneRole.Clearing, u, v);
                float wet = wetness != null ? wetness(new Vector2(u * size, v * size)) : 0f;
                float soil = Mathf.Clamp01(Mathf.Max(path, clearing * 0.55f, (1f - forest) * definition.OpenSoil));
                soils[row, column] = soil * (1f - 0.6f * wet * (1f - path));
                if (trails != null)
                {
                    trails[row, column] = path;
                }

                wets[row, column] = wet;
                verges[row, column] = (byte)Mathf.RoundToInt(VergeRoom(path) * 255f);
            }
        }

        int featherCells = Mathf.Max(1, Mathf.RoundToInt(definition.PathFeather * resolution / size));
        Feather(soils, featherCells);
        if (trails != null)
        {
            Feather(trails, featherCells);
        }

        if (roads != null)
        {
            Feather(roads, featherCells);
        }

        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                float u = (column + 0.5f) / resolution;
                float v = (row + 0.5f) / resolution;
                float forest = definition.Strength(RaidZoneRole.Forest, u, v);
                float wet = wets[row, column];
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cliff.x, cliff.y, data.GetSteepness(u, v)));
                float soil = soils[row, column];
                // Moss creeps over the path's feathered margin in tufts: noise frays the edge instead of a clean line.
                float ragged = Mathf.PerlinNoise(u * size / EdgeNoise + 11.3f, v * size / EdgeNoise - 5.1f);
                float fine = Mathf.PerlinNoise(u * size / FineNoise - 2.7f, v * size / FineNoise + 8.9f);
                float margin = soil * (1f - soil) * 4f;
                soil = Mathf.Clamp01(soil + ((ragged - 0.5f) * 0.6f + (fine - 0.5f) * 0.4f) * margin * definition.PathFray);

                // The shore (Shore Mud): mud or gravel at the waterline, a damp step of the litter layer a little higher, then the floor.
                float shoreMud = 0f, shoreDamp = 0f;
                // By the ground's height over the nearest water (generated maps pass it; damp ground away from water is not a shore):
                // mud from under the surface to a hand above it, damp to a metre, both fading within a few metres of the edge (a
                // fen's sheet has no edge: its height alone).
                if (definition.ShoreMud > 0f && water != null && water(new Vector2(u * size, v * size), ShoreReach, out float level, out float edge))
                {
                    float above = data.GetInterpolatedHeight(u, v) - level;
                    // A fen is a flat sheet with no shore line: much of it lies a hand above its water, so its band is kept to the
                    // last few centimetres, or the whole moss turns to mud.
                    bool sheet = float.IsNaN(edge);
                    float band = sheet ? FenShoreBand : 1f;
                    float near = sheet ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreReach * 0.5f, ShoreReach, edge));
                    shoreMud = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreMudTop * 0.3f * band, ShoreMudTop * band, above))) * near;
                    shoreDamp = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreMudTop * band, ShoreDampTop * band, above))) * near;
                    shoreMud *= definition.ShoreMud * (0.75f + 0.25f * fine);
                    shoreDamp *= definition.ShoreMud * (0.6f + 0.4f * ragged);
                    soil = Mathf.Max(soil, shoreMud);
                }

                float litter = 0f;
                if (hasLitter)
                {
                    float canopy = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.95f, forest));
                    float patch = Mathf.PerlinNoise(u * size / LitterPatch + 3.1f, v * size / LitterPatch - 7.7f) * 0.8f + ragged * 0.2f;
                    litter = definition.Litter * canopy * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, patch)) * (1f - wet);
                }

                // The step between path and floor (Edge Blend): where both show, part of each is the litter layer, in drifts.
                float step = 0f;
                if (hasLitter)
                {
                    if (trails != null)
                    {
                        float trail = trails[row, column];
                        float both = trail * (1f - trail) * 4f;
                        float drifts = Mathf.PerlinNoise(u * size / EdgeStepNoise + 21.7f, v * size / EdgeStepNoise - 9.3f);
                        step = definition.EdgeBlend * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.6f, both)) * (0.55f + 0.45f * drifts);
                    }

                    step = Mathf.Max(step, shoreDamp * (1f - shoreMud));
                }

                float rest = 1f - rock;
                float onPath = rest * soil * (1f - step);
                float floor = rest * (1f - soil) * (1f - step);
                // Leaves drift over the trails in patches, so a path is dirt, needles and leaves, never one bare strip.
                float drift = 0f;
                if (hasLitter)
                {
                    float leaves = Mathf.PerlinNoise(u * size / PathLitterPatch - 13.7f, v * size / PathLitterPatch + 4.2f) * 0.75f + fine * 0.25f;
                    drift = PathLitter * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.75f, leaves)) * (1f - wet);
                }

                weights[row, column, 2] = rock;
                weights[row, column, 1] = onPath * (1f - drift);
                weights[row, column, 0] = floor * (1f - litter);
                if (hasLitter)
                {
                    weights[row, column, 3] = floor * litter + onPath * drift + rest * step;
                }

                // A road's soil is the Road Layer (part of it, for a worn mix); the trail's verge, drifts and edge step stay as on any trail.
                if (roads != null)
                {
                    float moved = weights[row, column, 1] * Mathf.Clamp01(roads[row, column]) * definition.RoadPaint;
                    weights[row, column, 1] -= moved;
                    weights[row, column, roadIndex] += moved;
                }
            }
        }

        data.SetAlphamaps(0, 0, weights);
        return verges;
    }

    // From this much of the raw path (before its border is feathered) inward, the verge gives way, to none on a trail's centre line.
    // At a trail's edge line the raw path is a half, so the verge there, and outside it, grows as the paint says.
    private const float VergeInsideFrom = 0.7f;

    // Edge Blend's drifts of litter (metres across).
    private const float EdgeStepNoise = 5f;
    // Shore Mud: metres searched for water, and the heights over its surface the mud and the damp step reach.
    private const float ShoreReach = 5f;
    private const float ShoreMudTop = 0.35f;
    private const float ShoreDampTop = 1.1f;
    private const float FenShoreBand = 0.3f;

    /// <summary>
    /// How much of a path's verge may grow (0 to 1) where the trail's own strength, before feathering, is <paramref name="rawPath"/>:
    /// all of it at and beyond the trail's edge, none inside the trail, so a narrow side path's middle and leaves on a trail stay
    /// bare of it.
    /// </summary>
    public static float VergeRoom(float rawPath) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VergeInsideFrom, 1f, rawPath));

    /// <summary>
    /// <see cref="Paint"/>'s verge room as TerrainGrowth.Fill asks for it, by world point over the terrain it was painted on (not
    /// turned since). Null when there is none, and Fill then lets every verge grow as the paint says.
    /// </summary>
    public static Func<Vector3, float> VergeRoomAt(Terrain terrain, byte[,] room)
    {
        if (terrain == null || room == null)
        {
            return null;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 extent = terrain.terrainData.size;
        int rows = room.GetLength(0), columns = room.GetLength(1);
        return point =>
        {
            int column = Mathf.Clamp((int)((point.x - origin.x) / extent.x * columns), 0, columns - 1);
            int row = Mathf.Clamp((int)((point.z - origin.z) / extent.z * rows), 0, rows - 1);
            return room[row, column] / 255f;
        };
    }

    // Two box blurs of the given radius (cells) along each axis: close to a gaussian, at four cheap passes.
    private static void Feather(float[,] values, int radius)
    {
        int rows = values.GetLength(0), columns = values.GetLength(1);
        var line = new float[Mathf.Max(rows, columns)];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++) line[column] = values[row, column];
                for (int column = 0; column < columns; column++) values[row, column] = Average(line, columns, column, radius);
            }

            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++) line[row] = values[row, column];
                for (int row = 0; row < rows; row++) values[row, column] = Average(line, rows, row, radius);
            }
        }
    }

    private static float Average(float[] line, int count, int at, int radius)
    {
        float sum = 0f;
        int from = Mathf.Max(0, at - radius), to = Mathf.Min(count - 1, at + radius);
        for (int i = from; i <= to; i++) sum += line[i];
        return sum / (to - from + 1);
    }

    /// <summary>
    /// The water by a spot (metres from the map's south-west corner) within <paramref name="reach"/> metres of its edge: its surface's
    /// height over the terrain's origin and the metres from the spot to its edge (below zero: in it; NaN where the edge isn't known, as on
    /// a fen's one sheet). False when no water is that near. RaidLayoutField.WaterNear on a generated map.
    /// </summary>
    public delegate bool WaterNear(Vector2 spot, float reach, out float level, out float edge);

    /// <summary>
    /// What a map's terrain-tree prototype stands for, in the order Scatter registers them (each list without its empty entries):
    /// trees, undergrowth, rocks and logs, pile stones, then stones by the water.
    /// </summary>
    public enum Group : byte
    {
        Tree = 0,
        Undergrowth = 1,
        Rock = 2,
        PileStone = 3,
        ShoreStone = 4,
        /// <summary>A hero of the set dressing standing as a terrain tree (RaidMapDefinition › Heroes, G9).</summary>
        Hero = 5,
        /// <summary>A mid of the set dressing (RaidMapDefinition › Mids, G9).</summary>
        Mid = 6,
    }

    /// <summary>The group of each of the map's terrain-tree prototypes, by prototype index, as Scatter lays them out.</summary>
    public static Group[] PrototypeGroups(RaidMapDefinition definition)
    {
        var groups = new List<Group>();
        AddGroup(groups, definition.Trees, Group.Tree);
        AddGroup(groups, definition.Undergrowth, Group.Undergrowth);
        AddGroup(groups, definition.Rocks, Group.Rock);
        AddGroup(groups, definition.PileStones, Group.PileStone);
        AddGroup(groups, definition.Shore.stones, Group.ShoreStone);
        foreach (RaidMapDefinition.Hero hero in definition.Heroes)
        {
            if (TreeHero(hero))
            {
                groups.Add(Group.Hero);
            }
        }

        AddGroup(groups, definition.Mids, Group.Mid);
        return groups.ToArray();
    }

    // A hero that may stand as a terrain tree, and so has a prototype of its own.
    private static bool TreeHero(RaidMapDefinition.Hero hero) => hero?.prefab != null && !hero.alwaysObject;

    private static void AddGroup(List<Group> groups, IReadOnlyList<RaidMapDefinition.Scatter> list, Group group)
    {
        foreach (RaidMapDefinition.Scatter scatter in list)
        {
            if (scatter?.prefab != null)
            {
                groups.Add(group);
            }
        }
    }

    /// <summary>
    /// Which of the map's prototypes get a trunk prism (<see cref="TrunkMesh"/>): all but the pile stones without a collider. A raid's
    /// terrain has its tree colliders on, so a pile stone whose prefab has one (Mossy Rock Small's capsule) is solid to the player and
    /// to creatures alike: without its prism the navmesh would lead creatures into it. One without a collider is walked through by
    /// everyone and leaves the navmesh as it was.
    /// </summary>
    public static bool[] Prisms(RaidMapDefinition definition)
    {
        Group[] groups = PrototypeGroups(definition);
        var prisms = new bool[groups.Length];
        int pile = 0;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != Group.PileStone)
            {
                prisms[i] = true;
                continue;
            }

            // The pile stones' prototypes are their list's entries with a prefab, in order (AddGroup).
            while (pile < definition.PileStones.Count && definition.PileStones[pile]?.prefab == null)
            {
                pile++;
            }

            prisms[i] = pile < definition.PileStones.Count && definition.PileStones[pile].prefab.GetComponent<Collider>() != null;
            pile++;
        }

        return prisms;
    }

    /// <summary>
    /// Places trees, then undergrowth (saplings, shrubs, clutter), then rocks and logs over the terrain, then stones by the water when
    /// <paramref name="water"/> says where it is. Each spot is offered to every kind of its group, weighted by how well its habitat suits
    /// the spot; trunks keep the room their kind needs. <paramref name="wetness"/> and <paramref name="inWater"/> take metres from the
    /// map's south-west corner. Copses, piles and shore stones draw from streams of their own, so at their defaults the forest is the
    /// same tree for tree. Returns how many trees.
    /// <para>
    /// Last of all, on a generated map (<paramref name="walked"/> given: metres from a spot to walkable ground), the set dressing (G9): each
    /// <see cref="RaidMapDefinition.DressingCell"/> cell near a trail gets at most one hero, 6-15 m off the trail, and a few mids, from its
    /// own stream. A hero near walkable ground goes into <paramref name="heroObjects"/> for the caller to stand as an object (solid, as the
    /// boulders are); farther off it is a terrain tree, as cheap as the forest.
    /// </para>
    /// </summary>
    public static int Scatter(TerrainData data, RaidMapDefinition definition, List<Clearing> clearings, out int rockCount,
        Func<Vector2, float> wetness = null, Func<Vector2, bool> inWater = null, Func<Vector2, int, float> treeWeightAt = null,
        WaterNear water = null, Func<Vector2, float> walked = null, List<RaidMapDefinition.HeroObject> heroObjects = null)
    {
        var prototypes = new List<TreePrototype>();
        Kind[] trees = Kinds(definition.Trees, prototypes);
        Kind[] undergrowth = Kinds(definition.Undergrowth, prototypes);
        Kind[] rocks = Kinds(definition.Rocks, prototypes);
        Kind[] pileStones = Kinds(definition.PileStones, prototypes);
        Kind[] shoreStones = Kinds(definition.Shore.stones, prototypes);
        // In PrototypeGroups' order: the heroes that may be terrain trees, then the mids.
        var heroPrototypes = new int[definition.Heroes.Count];
        for (int i = 0; i < heroPrototypes.Length; i++)
        {
            heroPrototypes[i] = -1;
            if (TreeHero(definition.Heroes[i]))
            {
                heroPrototypes[i] = prototypes.Count;
                prototypes.Add(new TreePrototype { prefab = definition.Heroes[i].prefab });
            }
        }

        Kind[] mids = Kinds(definition.Mids, prototypes);
        data.treePrototypes = prototypes.ToArray();
        data.RefreshPrototypes();

        float size = definition.Size;
        var random = new System.Random(definition.ScatterSeed);
        var placed = new List<TreeInstance>();
        var trunks = new TrunkGrid(size);
        var weights = new float[Mathf.Max(trees.Length, Mathf.Max(undergrowth.Length, rocks.Length))];
        float clumping = definition.TreeClumping;
        Vector2 copses = clumping > 0f ? NoiseOffset(definition.ScatterSeed, "Copses") : Vector2.zero;
        System.Random piles = definition.PileChance > 0f ? new System.Random(RaidLayout.SeedFor(definition.ScatterSeed, "Rock Piles")) : null;

        RaidScatterHabitat.Spot Read(Vector2 spot, float u, float v) => new RaidScatterHabitat.Spot(spot, data.GetSteepness(u, v),
            definition.Strength(RaidZoneRole.Forest, u, v), wetness != null ? wetness(spot) : 0f, Edge(definition, u, v, size),
            1f - Mathf.Clamp01(trunks.Nearest(spot, NearTreesReach) / NearTreesReach));

        // Where a pile's stones and its plant may go: on the map, off trails, sites and water.
        bool Free(Vector2 at) => at.x >= 0f && at.y >= 0f && at.x < size && at.y < size
            && definition.Strength(RaidZoneRole.Path, at.x / size, at.y / size) <= 0.1f
            && !Blocked(at, clearings, forTrees: false) && (inWater == null || !inWater(at));

        // A few small stones round a rock and a plant at its foot, so it reads as a pile rather than a prop set down on the grass.
        // A stone keeps out of a trunk (and the pile's own rock) as everything else does; asking draws nothing from the stream.
        void AddPile(Vector2 centre, float standing)
        {
            int count = piles.Next(PileLeast, PileMost + 1);
            for (int k = 0; k < count && pileStones.Length > 0; k++)
            {
                Vector2 at = centre + Around(piles, standing * 0.5f + Range(piles, 0.2f, 1.2f));
                int pick = PickByWeight(pileStones, piles);
                if (pick >= 0 && Free(at) && trunks.Fits(at, TrunkClearance))
                {
                    placed.Add(MakeInstance(pileStones[pick].Prototype, at.x / size, at.y / size, pileStones[pick].Scatter.scale, piles));
                }
            }

            Vector2 foot = centre + Around(piles, standing * 0.5f + Range(piles, 0.3f, 0.8f));
            if (undergrowth.Length > 0 && Free(foot))
            {
                Place(undergrowth, Read(foot, foot.x / size, foot.y / size), piles, weights, trunks, placed, foot.x / size, foot.y / size, false);
            }
        }

        // Trees: one candidate per cell of a jittered grid; the forest's density decides whether one grows, the habitats which.
        foreach (Vector2 spot in Grid(definition.TreeSpacing, size, random))
        {
            float u = spot.x / size, v = spot.y / size;
            if (definition.Strength(RaidZoneRole.Path, u, v) > 0.15f || definition.Strength(RaidZoneRole.Clearing, u, v) > 0.3f
                || data.GetSteepness(u, v) > definition.MaxTreeSlope) continue;
            if (Blocked(spot, clearings, forTrees: true) || (inWater != null && inWater(spot))) continue;
            float accept = Mathf.Pow(definition.Strength(RaidZoneRole.Forest, u, v), 1.3f);
            if (clumping > 0f)
            {
                accept *= CopseFactor(Copse(spot, definition.ClumpSize, copses), clumping);
            }

            if (random.NextDouble() > accept) continue;

            Place(trees, Read(spot, u, v), random, weights, trunks, placed, u, v, true, treeWeightAt);
        }

        int treeCount = placed.Count;

        // Undergrowth: a finer grid, thickest where the forest is and along its edges; saplings take gaps, clutter lies by trunks.
        if (undergrowth.Length > 0)
        {
            foreach (Vector2 spot in Grid(definition.UndergrowthSpacing, size, random))
            {
                float u = spot.x / size, v = spot.y / size;
                if (definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || definition.Strength(RaidZoneRole.Clearing, u, v) > 0.6f) continue;
                if (Blocked(spot, clearings, forTrees: true) || (inWater != null && inWater(spot))) continue;

                RaidScatterHabitat.Spot here = Read(spot, u, v);
                float chance = definition.UndergrowthChance * Mathf.Clamp01(0.1f + here.Forest * 0.8f + here.Edge * 0.6f);
                if (clumping > 0f)
                {
                    chance *= CopseFactor(Copse(spot, definition.ClumpSize, copses), clumping * UndergrowthClumping);
                }

                if (random.NextDouble() > chance) continue;

                Place(undergrowth, here, random, weights, trunks, placed, u, v, false);
            }
        }

        // Rocks and logs: a coarser grid, most likely at the foot of cliffs, now and then in open forest and, where the map wants it, out
        // in the open; which one by habitat. A big one may become a pile. Rocks in the open lie on gentle ground only: past the cliffs'
        // band (38° and up) a rock set on the face hangs off it.
        foreach (Vector2 spot in Grid(definition.RockSpacing, size, random))
        {
            float u = spot.x / size, v = spot.y / size;
            if (definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || Blocked(spot, clearings, forTrees: false)) continue;
            if (inWater != null && inWater(spot)) continue;

            float slope = data.GetSteepness(u, v);
            float forest = definition.Strength(RaidZoneRole.Forest, u, v);
            float chance = slope > CliffRocksFrom && slope < CliffRocksTo ? definition.RocksAtCliffs
                : forest > 0.3f ? definition.RocksInForest
                : definition.RocksInOpen > 0f && slope <= CliffRocksFrom && definition.Strength(RaidZoneRole.Clearing, u, v) < OpenRocksBelowClearing
                    ? definition.RocksInOpen
                : 0f;
            if (random.NextDouble() > chance) continue;

            int chosen = Place(rocks, Read(spot, u, v), random, weights, trunks, placed, u, v, false);
            if (piles != null && chosen >= 0 && rocks[chosen].Room >= PileFromRoom && piles.NextDouble() < definition.PileChance)
            {
                AddPile(spot, rocks[chosen].Room * placed[placed.Count - 1].widthScale);
            }
        }

        rockCount = placed.Count - treeCount;
        if (water != null && shoreStones.Length > 0)
        {
            PlaceShore(data, definition, shoreStones, water, clearings, trunks, placed);
        }

        if (walked != null && (mids.Length > 0 || HasHeroes(definition)))
        {
            Dress(data, definition, mids, heroPrototypes, clearings, trunks, placed, walked, inWater, heroObjects, Read);
        }

        data.SetTreeInstances(placed.ToArray(), true);
        return treeCount;
    }

    private static bool HasHeroes(RaidMapDefinition definition)
    {
        foreach (RaidMapDefinition.Hero hero in definition.Heroes)
        {
            if (hero?.prefab != null && hero.weight > 0f)
            {
                return true;
            }
        }

        return false;
    }

    // Set dressing (G9): heroes give each screen near a trail one thing to be composed round, mids fill its middle distance.
    private const int HeroTries = 8;
    private const int MidTries = 3;
    private const float HeroSteepest = 40f;
    private const float HeroKeepsClear = 1f;

    private static void Dress(TerrainData data, RaidMapDefinition definition, Kind[] mids, int[] heroPrototypes, List<Clearing> clearings,
        TrunkGrid trunks, List<TreeInstance> placed, Func<Vector2, float> walked, Func<Vector2, bool> inWater,
        List<RaidMapDefinition.HeroObject> heroObjects, Func<Vector2, float, float, RaidScatterHabitat.Spot> read)
    {
        float size = definition.Size;
        float cell = Mathf.Max(10f, definition.DressingCell);
        var random = new System.Random(RaidLayout.SeedFor(definition.ScatterSeed, "Dressing"));
        var midWeights = new float[Mathf.Max(1, mids.Length)];
        float heroTotal = 0f;
        // How far each hero reaches from its pivot at scale 1, any way it is turned: a fallen giant set 6 m off a trail must not lie
        // across it, so the whole of it keeps a metre clear of walkable ground.
        var heroReach = new float[definition.Heroes.Count];
        float rimTotal = 0f;
        for (int i = 0; i < heroReach.Length; i++)
        {
            RaidMapDefinition.Hero hero = definition.Heroes[i];
            float weight = hero?.prefab != null ? Mathf.Max(0f, hero.weight) : 0f;
            if (hero != null && hero.atRims)
            {
                rimTotal += weight;
            }
            else
            {
                heroTotal += weight;
            }

            if (hero?.prefab != null)
            {
                Bounds bounds = PrefabBounds(hero.prefab);
                Vector3 far = new Vector3(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), 0f,
                    Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)));
                heroReach[i] = far.magnitude * hero.prefab.transform.localScale.x;
            }
        }

        Vector2 heroFrom = definition.HeroFromTrail;
        Vector2 midFrom = definition.MidFromTrail;
        Vector2Int midCount = definition.MidsPerCell;
        int cells = Mathf.CeilToInt(size / cell);
        for (int gz = 0; gz < cells; gz++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                var centre = new Vector2(Mathf.Min((gx + 0.5f) * cell, size - 1f), Mathf.Min((gz + 0.5f) * cell, size - 1f));
                if (walked(centre) > definition.DressingReach)
                {
                    continue;
                }

                for (int tries = 0; heroTotal > 0f && tries < HeroTries; tries++)
                {
                    var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                    if (spot.x >= size || spot.y >= size)
                    {
                        continue;
                    }

                    float off = walked(spot);
                    float u = spot.x / size, v = spot.y / size;
                    if (off < heroFrom.x || off > heroFrom.y || data.GetSteepness(u, v) > HeroSteepest
                        || definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || Blocked(spot, clearings, forTrees: true)
                        || (inWater != null && inWater(spot)))
                    {
                        continue;
                    }

                    int pick = PickHero(definition.Heroes, heroTotal, random, false);
                    RaidMapDefinition.Hero hero = definition.Heroes[pick];
                    TreeInstance instance = MakeInstance(Mathf.Max(0, heroPrototypes[pick]), u, v, hero.scale, random);
                    if (off - heroReach[pick] * instance.widthScale < HeroKeepsClear)
                    {
                        continue;
                    }

                    float room = Mathf.Max(hero.room * instance.widthScale, TrunkClearance);
                    ClearAround(placed, spot, room, size);
                    trunks.Add(spot, room);
                    if (heroPrototypes[pick] < 0 || off <= definition.HeroObjectWithin)
                    {
                        heroObjects?.Add(new RaidMapDefinition.HeroObject(hero, spot, instance.rotation * Mathf.Rad2Deg, instance.widthScale));
                    }
                    else
                    {
                        placed.Add(instance);
                    }

                    break;
                }

                int count = mids.Length > 0 ? random.Next(Mathf.Max(0, midCount.x), Mathf.Max(midCount.x, midCount.y) + 1) : 0;
                for (int m = 0; m < count; m++)
                {
                    for (int tries = 0; tries < MidTries; tries++)
                    {
                        var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                        if (spot.x >= size || spot.y >= size)
                        {
                            continue;
                        }

                        float off = walked(spot);
                        float u = spot.x / size, v = spot.y / size;
                        if (off < midFrom.x || off > midFrom.y || definition.Strength(RaidZoneRole.Path, u, v) > 0.1f
                            || Blocked(spot, clearings, forTrees: false) || (inWater != null && inWater(spot)))
                        {
                            continue;
                        }

                        if (Place(mids, read(spot, u, v), random, midWeights, trunks, placed, u, v, false) >= 0)
                        {
                            break;
                        }
                    }
                }
            }
        }

        if (rimTotal > 0f)
        {
            DressRims(data, definition, heroPrototypes, heroReach, rimTotal, clearings, trunks, placed, walked, inWater, heroObjects);
        }
    }

    // A rim: ground near level (no steeper than RimFlattest) with a drop beyond, a probe RimProbe metres off finding a face steeper than RimFace
    // at least RimDrop lower. Tries a spot this many times a cell.
    private const float RimFlattest = 20f;
    private const float RimFace = 48f;
    private const float RimProbe = 3f;
    private const float RimDrop = 1.2f;
    private const int RimTries = 14;

    /// <summary>
    /// Heroes At Rims (the user's cliff-edge picture, 2026-11-18: a lone pine on the very edge): over the whole map, one cell in Rim Hero
    /// Chance gets one, at a lip found in it (<see cref="IsRim"/>), kept off trails, clearings and water, clear of walkable ground and other
    /// heroes, and standing as an object near walkable ground (Hero Object Within) as the other heroes do. From its own stream, after the rest: turning it on moves nothing else.
    /// </summary>
    private static void DressRims(TerrainData data, RaidMapDefinition definition, int[] heroPrototypes, float[] heroReach, float rimTotal,
        List<Clearing> clearings, TrunkGrid trunks, List<TreeInstance> placed, Func<Vector2, float> walked, Func<Vector2, bool> inWater,
        List<RaidMapDefinition.HeroObject> heroObjects)
    {
        float size = definition.Size;
        float cell = Mathf.Max(10f, definition.RimHeroCell);
        var random = new System.Random(RaidLayout.SeedFor(definition.ScatterSeed, "Rim Heroes"));
        int cells = Mathf.CeilToInt(size / cell);
        for (int gz = 0; gz < cells; gz++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                if (random.NextDouble() > definition.RimHeroChance)
                {
                    continue;
                }

                for (int tries = 0; tries < RimTries; tries++)
                {
                    var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                    if (spot.x < 2f || spot.y < 2f || spot.x >= size - 2f || spot.y >= size - 2f)
                    {
                        continue;
                    }

                    float u = spot.x / size, v = spot.y / size;
                    if (!IsRim(data, size, spot) || definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || walked(spot) < 2f
                        || Blocked(spot, clearings, forTrees: true) || (inWater != null && inWater(spot)))
                    {
                        continue;
                    }

                    int pick = PickHero(definition.Heroes, rimTotal, random, true);
                    RaidMapDefinition.Hero hero = definition.Heroes[pick];
                    TreeInstance instance = MakeInstance(Mathf.Max(0, heroPrototypes[pick]), u, v, hero.scale, random);
                    float off = walked(spot);
                    float room = Mathf.Max(hero.room * instance.widthScale, TrunkClearance);
                    // As the other heroes: the whole of it a metre clear of walkable ground, and not inside another hero's room.
                    if (off - heroReach[pick] * instance.widthScale < HeroKeepsClear || !trunks.Fits(spot, room))
                    {
                        continue;
                    }

                    ClearAround(placed, spot, room, size);
                    trunks.Add(spot, room);
                    if (heroPrototypes[pick] < 0 || off <= definition.HeroObjectWithin)
                    {
                        heroObjects?.Add(new RaidMapDefinition.HeroObject(hero, spot, instance.rotation * Mathf.Rad2Deg, instance.widthScale));
                    }
                    else
                    {
                        placed.Add(instance);
                    }

                    break;
                }
            }
        }
    }

    /// <summary>
    /// True when <paramref name="spot"/> (metres on a terrain <paramref name="size"/> across) is a cliff's lip: its own ground no steeper than
    /// <see cref="RimFlattest"/>, and in one of eight directions, <see cref="RimProbe"/> metres off, a face steeper than <see cref="RimFace"/>
    /// whose ground lies at least <see cref="RimDrop"/> lower. Reads only the terrain's heights.
    /// </summary>
    public static bool IsRim(TerrainData data, float size, Vector2 spot)
    {
        float u = spot.x / size, v = spot.y / size;
        if (data.GetSteepness(u, v) > RimFlattest)
        {
            return false;
        }

        float here = data.GetInterpolatedHeight(u, v);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            Vector2 probe = spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RimProbe;
            float pu = probe.x / size, pv = probe.y / size;
            if (pu < 0f || pv < 0f || pu > 1f || pv > 1f)
            {
                continue;
            }

            if (data.GetSteepness(pu, pv) >= RimFace && here - data.GetInterpolatedHeight(pu, pv) >= RimDrop)
            {
                return true;
            }
        }

        return false;
    }

    private static int PickHero(IReadOnlyList<RaidMapDefinition.Hero> heroes, float total, System.Random random, bool atRims)
    {
        double roll = random.NextDouble() * total;
        int last = 0;
        for (int i = 0; i < heroes.Count; i++)
        {
            RaidMapDefinition.Hero hero = heroes[i];
            float weight = hero?.prefab != null && hero.atRims == atRims ? Mathf.Max(0f, hero.weight) : 0f;
            if (weight <= 0f) continue;
            last = i;
            roll -= weight;
            if (roll < 0d) return i;
        }

        return last;
    }

    // Takes what was scattered within the radius (metres) of a spot off the map, so a hero stands on its own ground. Compacts in place.
    private static void ClearAround(List<TreeInstance> placed, Vector2 spot, float radius, float size)
    {
        float squared = radius * radius;
        int kept = 0;
        for (int i = 0; i < placed.Count; i++)
        {
            TreeInstance tree = placed[i];
            float dx = tree.position.x * size - spot.x, dz = tree.position.z * size - spot.y;
            if (dx * dx + dz * dz >= squared)
            {
                placed[kept++] = tree;
            }
        }

        placed.RemoveRange(kept, placed.Count - kept);
    }

    /// <summary>
    /// The copse mask at a spot (metres from the map's corner): 0 in the meadow between copses, 1 in their hearts. Two octaves of noise
    /// <paramref name="clumpSize"/> metres across (Docs/WorldLookPlan_2026-11-17.md B3); <paramref name="offset"/> makes each map's its own.
    /// </summary>
    public static float Copse(Vector2 spot, float clumpSize, Vector2 offset)
    {
        float size = Mathf.Max(1f, clumpSize);
        float broad = Mathf.PerlinNoise(spot.x / size + offset.x, spot.y / size + offset.y);
        float fine = Mathf.PerlinNoise(spot.x / (FineCopse * size) - offset.y, spot.y / (FineCopse * size) + offset.x);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CopseFrom, CopseFull, broad * 0.75f + fine * 0.25f));
    }

    /// <summary>
    /// What the copse mask multiplies a tree's chance by: exactly 1 without clumping; at full clumping from 0.12 in the meadow (the odd
    /// lone tree) to 1.72 in a copse's heart, so trees move into copses rather than vanish.
    /// </summary>
    public static float CopseFactor(float copse, float clumping) => Mathf.Lerp(1f, LoneTrees + CopseGain * Mathf.Clamp01(copse), Mathf.Clamp01(clumping));

    // Stones on the shallow beds and in clumps along the banks of rivers and ponds (ShoreRule), last of all and from their own stream.
    // A bed stone is sized to stand a little out of the water, so the water shader's foam rings it (Docs/Water.md).
    private static void PlaceShore(TerrainData data, RaidMapDefinition definition, Kind[] stones, WaterNear water, List<Clearing> clearings,
        TrunkGrid trunks, List<TreeInstance> placed)
    {
        RaidMapDefinition.ShoreRule rule = definition.Shore;
        float size = definition.Size;
        var random = new System.Random(RaidLayout.SeedFor(definition.ScatterSeed, "Shore Stones"));
        Vector2 clumps = NoiseOffset(definition.ScatterSeed, "Shore Clumps");
        var tops = new float[stones.Length];
        // Metres across at scale 1 of the stones with no capsule (walked through as terrain trees), for the banks' Bank Widest; 0 for the
        // solid ones, which a navmesh prism and their collider keep everyone out of.
        var hollow = new float[stones.Length];
        for (int i = 0; i < stones.Length; i++)
        {
            Bounds bounds = PrefabBounds(stones[i].Scatter.prefab);
            tops[i] = Mathf.Max(0f, bounds.max.y);
            hollow[i] = stones[i].Scatter.prefab.GetComponent<CapsuleCollider>() == null ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f;
        }

        bool Free(Vector2 at) => at.x >= 0f && at.y >= 0f && at.x < size && at.y < size
            && definition.Strength(RaidZoneRole.Path, at.x / size, at.y / size) <= ShorePathClear && !Blocked(at, clearings, forTrees: false);

        void Add(Vector2 at, int pick, TreeInstance stone)
        {
            float room = Mathf.Max(stones[pick].Room * stone.widthScale, TrunkClearance);
            if (trunks.Fits(at, room))
            {
                trunks.Add(at, room);
                placed.Add(stone);
            }
        }

        // The bed: only where it is shallow, thinning towards the deep middle.
        foreach (Vector2 spot in Grid(rule.bedSpacing, size, random))
        {
            if (!water(spot, 0f, out float level, out float edge) || float.IsNaN(edge) || edge >= 0f) continue;
            float u = spot.x / size, v = spot.y / size;
            float depth = level - data.GetInterpolatedHeight(u, v);
            if (depth <= 0f || depth > rule.deepest || !Free(spot)) continue;
            if (random.NextDouble() > rule.bedChance * (1f - BedDeepThins * depth / rule.deepest)) continue;

            int pick = PickByWeight(stones, random);
            if (pick < 0) return;
            TreeInstance stone = MakeInstance(stones[pick].Prototype, u, v, stones[pick].Scatter.scale, random);
            if (tops[pick] > MeasurableTop)
            {
                float needed = (depth + Mathf.Lerp(rule.proud.x, rule.proud.y, (float)random.NextDouble())) / tops[pick];
                if (needed > stone.widthScale * BedTallest) continue;
                stone.heightScale = Mathf.Clamp(needed, stone.widthScale * BedShortest, stone.widthScale * BedTallest);
            }

            Add(spot, pick, stone);
        }

        // The banks: in clumps of noise, from the water's edge up to Bank Reach.
        foreach (Vector2 spot in Grid(rule.bankSpacing, size, random))
        {
            if (!water(spot, rule.bankReach, out _, out float edge) || float.IsNaN(edge) || edge < 0f || edge > rule.bankReach) continue;
            float clump = Mathf.PerlinNoise(spot.x / rule.clumpSize + clumps.x, spot.y / rule.clumpSize + clumps.y);
            float chance = rule.bankChance * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BankClumpFrom, BankClumpFull, clump));
            if (chance <= 0f || random.NextDouble() > chance || !Free(spot)) continue;

            int pick = PickByWeight(stones, random);
            if (pick < 0) return;
            TreeInstance stone = MakeInstance(stones[pick].Prototype, spot.x / size, spot.y / size, stones[pick].Scatter.scale, random);
            // A bank is walked along, and a stone without a collider is walked through: a big one (a lake stone shared with the Haven,
            // which has no collider to give) is kept to Bank Widest across, so nobody wades through a boulder. Bed stones stand in the water.
            if (rule.bankWidest > 0f && hollow[pick] * stone.widthScale > rule.bankWidest)
            {
                float shrink = rule.bankWidest / (hollow[pick] * stone.widthScale);
                stone.widthScale *= shrink;
                stone.heightScale *= shrink;
            }

            Add(spot, pick, stone);
        }
    }

    /// <summary>
    /// A prefab's extent about its pivot at scale 1, from its meshes' bounds (the first LOD only), as a terrain tree stands it; empty at the
    /// pivot when it has no mesh. How high a bed stone reaches and how wide a bank stone is (ShoreRule.bankWidest).
    /// </summary>
    public static Bounds PrefabBounds(GameObject prefab)
    {
        var extent = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || (filter.name.Contains("_LOD") && !filter.name.EndsWith("_LOD0")))
            {
                continue;
            }

            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 toPrefab = toRoot * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = toPrefab.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, sign));
                if (any)
                {
                    extent.Encapsulate(point);
                }
                else
                {
                    extent = new Bounds(point, Vector3.zero);
                    any = true;
                }
            }
        }

        return extent;
    }

    // A kind by weight alone, for stones placed where a rock or the water puts them rather than by habitat; -1 when none has weight.
    private static int PickByWeight(Kind[] kinds, System.Random random)
    {
        float total = 0f;
        foreach (Kind kind in kinds)
        {
            total += Mathf.Max(0f, kind.Scatter.weight);
        }

        if (total <= 0f)
        {
            return -1;
        }

        double roll = random.NextDouble() * total;
        int last = -1;
        for (int i = 0; i < kinds.Length; i++)
        {
            float weight = Mathf.Max(0f, kinds[i].Scatter.weight);
            if (weight <= 0f) continue;
            last = i;
            roll -= weight;
            if (roll < 0d) return i;
        }

        return last;
    }

    private static Vector2 Around(System.Random random, float distance)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    // A noise offset of its own for each map and purpose, so copses and clumps never line up with the paint's noise.
    private static Vector2 NoiseOffset(int seed, string key)
    {
        var random = new System.Random(RaidLayout.SeedFor(seed, key));
        return new Vector2((float)random.NextDouble() * NoiseRange, (float)random.NextDouble() * NoiseRange);
    }

    // Copses (B3): where the noise reads as meadow and as the heart of a copse, the second octave's share of a copse's size, the meadow's
    // floor that keeps lone trees in it, and how much a copse's heart gains. Undergrowth follows at half strength.
    private const float CopseFrom = 0.38f;
    private const float CopseFull = 0.62f;
    private const float FineCopse = 0.4f;
    private const float LoneTrees = 0.12f;
    private const float CopseGain = 1.6f;
    private const float UndergrowthClumping = 0.5f;
    private const float NoiseRange = 1000f;
    // Rocks in the open (B4) keep off clearings stronger than this; a rock with this much room or more may become a pile of 2-5 stones.
    private const float OpenRocksBelowClearing = 0.3f;
    // The slopes (degrees) where rocks lie at the foot of cliffs; rocks in the open keep to ground no steeper than the band's start.
    private const float CliffRocksFrom = 16f;
    private const float CliffRocksTo = 38f;
    private const float PileFromRoom = 1f;
    private const int PileLeast = 2;
    private const int PileMost = 5;
    // Shore stones (B6): fewer towards the deepest bed; a bed stone's height may be stretched from 0.7 to 1.5 times its width to stand
    // proud; bank clumps by noise; trails stay clear (fords included).
    private const float BedDeepThins = 0.6f;
    private const float BedShortest = 0.7f;
    private const float BedTallest = 1.5f;
    private const float MeasurableTop = 0.05f;
    private const float BankClumpFrom = 0.45f;
    private const float BankClumpFull = 0.65f;
    private const float ShorePathClear = 0.05f;

    private const float NearTreesReach = 4f;
    private const float EdgeReach = 7f;
    private const float LitterPatch = 9f;
    private const float EdgeNoise = 2.5f;
    private const float FineNoise = 0.7f;
    private const float PathLitterPatch = 3.5f;
    // The most of a trail that leaf litter covers.
    private const float PathLitter = 0.5f;
    // Nothing is placed inside a trunk, whatever its room.
    private const float TrunkClearance = 0.6f;

    private readonly struct Kind
    {
        public readonly int Prototype;
        public readonly RaidMapDefinition.Scatter Scatter;
        /// <summary>Its place in the map's list (which skips entries without a prefab here).</summary>
        public readonly int ListIndex;

        public Kind(int prototype, RaidMapDefinition.Scatter scatter, int listIndex)
        {
            Prototype = prototype;
            Scatter = scatter;
            ListIndex = listIndex;
        }

        public float Room => Scatter.habitat != null ? Scatter.habitat.room : 0f;
    }

    private static Kind[] Kinds(IReadOnlyList<RaidMapDefinition.Scatter> list, List<TreePrototype> prototypes)
    {
        var kinds = new List<Kind>();
        for (int i = 0; i < list.Count; i++)
        {
            RaidMapDefinition.Scatter scatter = list[i];
            if (scatter?.prefab == null) continue;
            kinds.Add(new Kind(prototypes.Count, scatter, i));
            prototypes.Add(new TreePrototype { prefab = scatter.prefab });
        }

        return kinds.ToArray();
    }

    // One jittered point per cell, row by row, from the map's own random stream.
    private static IEnumerable<Vector2> Grid(float cell, float size, System.Random random)
    {
        int cells = Mathf.CeilToInt(size / cell);
        for (int gz = 0; gz < cells; gz++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                if (spot.x < size && spot.y < size)
                {
                    yield return spot;
                }
            }
        }
    }

    // Picks a kind by weight × suitability among those with room to stand here, and places it at its own random size; returns which kind
    // (its index in kinds), or -1 when none had room. A tree's weight can come from where it stands (a large map's regions each favour
    // their own trees), by its place in the map's list.
    private static int Place(Kind[] kinds, in RaidScatterHabitat.Spot spot, System.Random random, float[] weights, TrunkGrid trunks,
        List<TreeInstance> placed, float u, float v, bool trunk, Func<Vector2, int, float> weightAt = null)
    {
        float total = 0f;
        for (int i = 0; i < kinds.Length; i++)
        {
            RaidMapDefinition.Scatter scatter = kinds[i].Scatter;
            float room = kinds[i].Room * (scatter.scale.x + scatter.scale.y) * 0.5f;
            bool fits = trunks.Fits(spot.Position, Mathf.Max(room, TrunkClearance));
            float weight = weightAt != null && scatter.weight > 0f ? weightAt(spot.Position, kinds[i].ListIndex) : scatter.weight;
            weights[i] = fits && weight > 0f ? weight * (scatter.habitat != null ? scatter.habitat.Suitability(spot, kinds[i].Prototype) : 1f) : 0f;
            total += weights[i];
        }

        if (total <= 0f)
        {
            return -1;
        }

        double roll = random.NextDouble() * total;
        int chosen = kinds.Length - 1;
        for (int i = 0; i < kinds.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0d)
            {
                chosen = i;
                break;
            }
        }

        TreeInstance instance = MakeInstance(kinds[chosen].Prototype, u, v, kinds[chosen].Scatter.scale, random);
        placed.Add(instance);
        float standing = kinds[chosen].Room * instance.widthScale;
        if (trunk || standing > 0f)
        {
            trunks.Add(spot.Position, Mathf.Max(standing, TrunkClearance));
        }

        return chosen;
    }

    // How close the forest's edge is: open ground (trail or clearing) within EdgeReach of a spot that is not open itself.
    private static float Edge(RaidMapDefinition definition, float u, float v, float size)
    {
        float Open(float x, float y) => Mathf.Max(definition.Strength(RaidZoneRole.Path, x, y), definition.Strength(RaidZoneRole.Clearing, x, y));
        float here = Open(u, v);
        float around = 0f;
        float reach = EdgeReach / size;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            around = Mathf.Max(around, Open(u + Mathf.Cos(angle) * reach, v + Mathf.Sin(angle) * reach));
        }

        return Mathf.Clamp01(around - here * 0.5f);
    }

    /// <summary>Trunks placed so far, in buckets, so a new one can keep the room both of them need.</summary>
    private sealed class TrunkGrid
    {
        private const float Bucket = 4f;
        private readonly int cells;
        private readonly List<Vector3>[] buckets;

        public TrunkGrid(float size)
        {
            cells = Mathf.Max(1, Mathf.CeilToInt(size / Bucket));
            buckets = new List<Vector3>[cells * cells];
        }

        public void Add(Vector2 spot, float room)
        {
            int index = Index(spot);
            (buckets[index] ??= new List<Vector3>()).Add(new Vector3(spot.x, spot.y, room));
        }

        /// <summary>True when every trunk is at least the mean of both rooms away.</summary>
        public bool Fits(Vector2 spot, float room)
        {
            bool fits = true;
            Visit(spot, Mathf.Max(room, 6f), (trunk, distance) => fits &= distance >= (room + trunk.z) * 0.5f);
            return fits;
        }

        /// <summary>Metres to the nearest trunk, or <paramref name="reach"/> when none is closer.</summary>
        public float Nearest(Vector2 spot, float reach)
        {
            float nearest = reach;
            Visit(spot, reach, (trunk, distance) => nearest = Mathf.Min(nearest, distance));
            return nearest;
        }

        private void Visit(Vector2 spot, float reach, Action<Vector3, float> each)
        {
            int span = Mathf.CeilToInt(reach / Bucket);
            int cx = Mathf.Clamp((int)(spot.x / Bucket), 0, cells - 1), cz = Mathf.Clamp((int)(spot.y / Bucket), 0, cells - 1);
            for (int z = Mathf.Max(0, cz - span); z <= Mathf.Min(cells - 1, cz + span); z++)
            {
                for (int x = Mathf.Max(0, cx - span); x <= Mathf.Min(cells - 1, cx + span); x++)
                {
                    List<Vector3> bucket = buckets[z * cells + x];
                    if (bucket == null) continue;
                    foreach (Vector3 trunk in bucket)
                    {
                        each(trunk, Vector2.Distance(spot, new Vector2(trunk.x, trunk.y)));
                    }
                }
            }
        }

        private int Index(Vector2 spot) =>
            Mathf.Clamp((int)(spot.y / Bucket), 0, cells - 1) * cells + Mathf.Clamp((int)(spot.x / Bucket), 0, cells - 1);
    }

    private static TreeInstance MakeInstance(int prototype, float u, float v, Vector2 scale, System.Random random)
    {
        float s = Mathf.Lerp(scale.x, scale.y, (float)random.NextDouble());
        float turn = (float)random.NextDouble();
        float shade = 0.82f + 0.18f * (float)random.NextDouble();
        return new TreeInstance
        {
            prototypeIndex = prototype,
            position = new Vector3(u, 0f, v),
            widthScale = s,
            heightScale = s * Mathf.Lerp(0.92f, 1.08f, (float)random.NextDouble()),
            rotation = turn * Mathf.PI * 2f,
            color = new Color(shade, shade, shade, 1f),
            lightmapColor = Color.white,
        };
    }

    private static bool Blocked(Vector2 spot, List<Clearing> clearings, bool forTrees)
    {
        foreach (Clearing clearing in clearings)
        {
            float radius = forTrees ? clearing.TreeRadius : clearing.RockRadius;
            if ((clearing.Spot - spot).sqrMagnitude < radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A navmesh bake ignores terrain trees, so creatures would walk through trunks. Every tree and rock with a capsule
    /// collider gets a matching prism in one mesh, for an invisible, not-walkable collider the navmesh cuts around.
    /// <paramref name="prisms"/> (by prototype, from <see cref="Prisms"/>) leaves out the prototypes marked false, such as pile stones;
    /// null gives every capsule its prism.
    /// </summary>
    public static Mesh TrunkMesh(Terrain terrain, Vector3 holderPosition, bool[] prisms = null)
    {
        TerrainData data = terrain.terrainData;
        TreePrototype[] prototypes = data.treePrototypes;
        var shapes = new CapsuleCollider[prototypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            bool prism = prisms == null || i >= prisms.Length || prisms[i];
            shapes[i] = prism && prototypes[i].prefab != null ? prototypes[i].prefab.GetComponent<CapsuleCollider>() : null;
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        Vector3 origin = terrain.transform.position;
        foreach (TreeInstance tree in data.treeInstances)
        {
            CapsuleCollider shape = shapes[tree.prototypeIndex];
            if (shape == null) continue;

            Vector3 foot = origin + Vector3.Scale(tree.position, data.size) - holderPosition;
            var turn = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
            float radius = shape.radius * tree.widthScale;
            if (shape.direction == 1)
            {
                AddPrism(vertices, triangles, foot, turn, radius, radius, Mathf.Min(3f, shape.height * tree.heightScale));
            }
            else
            {
                AddPrism(vertices, triangles, foot, turn, shape.height * tree.widthScale * 0.5f, radius, radius * 2f);
            }
        }

        var mesh = new Mesh { name = TrunksName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // A box around a tree's foot (half width along its turned x and z), standing on the ground.
    private static void AddPrism(List<Vector3> vertices, List<int> triangles, Vector3 foot, Quaternion turn, float halfX, float halfZ, float height)
    {
        int first = vertices.Count;
        for (int level = 0; level < 2; level++)
        {
            float y = level == 0 ? -0.5f : height;
            vertices.Add(foot + turn * new Vector3(-halfX, y, -halfZ));
            vertices.Add(foot + turn * new Vector3(halfX, y, -halfZ));
            vertices.Add(foot + turn * new Vector3(halfX, y, halfZ));
            vertices.Add(foot + turn * new Vector3(-halfX, y, halfZ));
        }

        int[] faces = { 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0, 4, 7, 6, 4, 6, 5 };
        foreach (int index in faces)
        {
            triangles.Add(first + index);
        }
    }

    /// <summary>Where the given point of a site goes: the first on its centre, the rest in a ring around it.</summary>
    public static Vector3 SitePoint(Terrain terrain, Vector3 centre, RaidMapDefinition.SiteRule rule, int site, int point)
    {
        if (point == 0)
        {
            return centre;
        }

        float angle = (point - 1) * Mathf.PI * 2f / Mathf.Max(1, rule.pointsPerSite - 1) + site;
        return Ground(terrain, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rule.spread);
    }

    /// <summary>Everything on the map sits on the ground: spawn points, and every object directly inside a plain folder object.</summary>
    public static void SnapToGround(Transform node, Terrain terrain, Action<Transform> beforeMoving = null)
    {
        foreach (Transform child in node)
        {
            if (child.name == TrunksName)
            {
                continue;
            }

            if (child.TryGetComponent(out SpawnGroup _))
            {
                foreach (Transform point in child)
                {
                    Snap(point, terrain, beforeMoving);
                }
            }
            else if (IsFolder(child))
            {
                SnapToGround(child, terrain, beforeMoving);
            }
            else
            {
                Snap(child, terrain, beforeMoving);
            }
        }
    }

    // The generator only holds the spawn groups: moving it would lift every point inside, after they were put on the ground.
    private static bool IsFolder(Transform node) => node.childCount > 0
        && (node.GetComponents<Component>().Length == 1 || node.TryGetComponent(out RaidGenerator _));

    private static void Snap(Transform target, Terrain terrain, Action<Transform> beforeMoving)
    {
        Vector3 grounded = Ground(terrain, target.position);
        if (Mathf.Abs(grounded.y - target.position.y) > 0.001f)
        {
            beforeMoving?.Invoke(target);
            target.position = grounded;
        }
    }

    public static Vector3 Ground(Terrain terrain, Vector3 position)
    {
        position.y = terrain.transform.position.y + terrain.SampleHeight(position);
        return position;
    }
}
