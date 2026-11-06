using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a RaidLayoutPlan into ground: how far every spot lies from open ground, and from that the heights and the painted
/// zones a hand-made map would have. The trails run along the valley floors and the wild ground rises between them, so
/// what the plan joined is what the player can walk. Plain arithmetic over grids, free of the terrain, so it can be tested.
/// </summary>
public sealed class RaidLayoutField
{
    private const float SoilEdge = 1.5f;
    private const float ForestEdge = 7f;
    private const float EdgeWobble = 2.5f;
    private const float WobbleScale = 11f;
    private const float RoughnessScale = 9f;
    private const float RoughFrom = 9f;
    private const float RidgeVariety = 0.55f;
    private const float RidgeVarietyScale = 46f;
    private const float SpurWidth = 0.6f;
    private const float PondShore = 5f;
    private const float PondBank = 3.5f;
    private const float PondBelowRim = 0.12f;
    private const float PondRimBeyond = 2f;
    private const int PondRimSamples = 24;
    private const float PondSoil = 2.4f;
    // Cliffs and ravines (moods with terraces, ravines or spires).
    private const float Far = 1e6f;
    // Stamped this far; beyond, the nearest two places spread outward cell by cell (much cheaper than stamping the whole map).
    private const float LevelReach = 26f;
    private const float RiverBank = 3f;
    private const float RiverFordFade = 4f;
    private const float RiverFordBank = 3f;
    private const float RiverFreeboard = 0.35f;
    private const float RiverWaterline = 0.2f;
    private const float RiverReachBeyond = 10f;
    private const float RiverIntoBank = 0.5f;
    private const float DescentLip = 0.3f;
    private const float DescentSlope = 2.6f;
    private const float DescentShortest = 25f;
    private const float LevelBlend = 9f;
    private const float CliffFrom = 2.5f;
    private const float CliffFull = 4.5f;
    private const float NeckShoulder = 1.5f;
    private const float NeckRamp = 1.2f;
    private const float RavineBed = 0.8f;
    private const float RavineFloorLowest = 0.5f;
    private const float RavineFloorAbove = 1.5f;
    private const float RavineFall = 0.12f;
    private const float CascadeFrom = 2.5f;
    private const float RavineReachBeyond = 3f;
    private const float RavineSoil = 1f;
    private const float SpireWall = 1.6f;
    private const float SpireScree = 3f;
    private const float SpireScreeHeight = 1.4f;
    private const float SpireRoughness = 1.2f;
    private const float KeepClearBeyond = 1.5f;
    private const float RagScale = 7f;
    private const float RagFineScale = 2.6f;
    private const float RagFine = 0.45f;
    private const float WallVarietyScale = 11f;
    private const float StrataMaskScale = 17f;
    private const int StrataBands = 3;
    private const float StrataRiser = 0.18f;
    private const float RubbleScale = 3.2f;
    private const float Rubble = 0.9f;
    private const float Talus = 0.7f;

    private readonly RaidLayoutPlan plan;
    private readonly RaidLayoutRules rules;
    private readonly int resolution;
    private readonly float cell;
    private readonly float reach;
    private readonly float[] openDistance;
    private readonly float[] trailDistance;
    private readonly float[] clearDistance;

    // The height of the level each spot stands on (terraces), and how far it is into a ravine with that ravine's half width and floor.
    private float[] levelHeight;
    private float[] ravineEdge;
    private float[] ravineHalf;
    private float[] ravineFloor;
    private float[] riverEdge;
    private float[] riverHalf;
    private float[] riverLevel;
    private float[] riverDepth;
    private float lift;
    private readonly System.Text.StringBuilder timings = new System.Text.StringBuilder();

    /// <summary>How long each part of building took, for the raid's generation log.</summary>
    public string Timings => timings.ToString();

    // While the levels are worked out: the nearest two places with a height of their own, each with its distance, height and which it is.
    private float[] nearDistance, nearHeight, otherDistance, otherHeight;
    private int[] nearId, otherId;

    private RaidLayoutField(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        this.plan = plan;
        this.rules = rules;
        resolution = rules.FieldResolution;
        cell = plan.Size / (resolution - 1);
        reach = rules.RidgeWidth + EdgeWobble * 2f + ForestEdge;
        openDistance = Filled(reach);
        trailDistance = Filled(reach);
        clearDistance = Filled(reach);
    }

    public int Resolution => resolution;

    /// <summary>The plan this field was built for.</summary>
    public RaidLayoutPlan Plan => plan;

    /// <summary>Metres the whole map stands raised by, so the deepest ravine's floor stays above the map's lowest point.</summary>
    public float Lift => lift;

    public static RaidLayoutField Build(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        var field = new RaidLayoutField(plan, rules);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        foreach (List<Vector2> trail in plan.Trails)
        {
            field.StampLine(trail, halfWidth, field.trailDistance);
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            field.StampLine(spur, halfWidth * SpurWidth, field.trailDistance);
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            field.Stamp(room.Position, room.Position, room.Radius, field.clearDistance);
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            field.Stamp(centre, centre, radius, field.clearDistance);
        }

        for (int i = 0; i < field.openDistance.Length; i++)
        {
            field.openDistance[i] = Mathf.Min(field.trailDistance[i], field.clearDistance[i]);
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (plan.HasTerraces)
        {
            field.BuildLevels(halfWidth);
            field.timings.Append($"levels {clock.ElapsedMilliseconds} ms, ");
        }

        field.lift = LiftFor(plan, rules);

        // Ponds lie on open ground: the ridges keep back and the forest thins, though only trails and clearings are soil.
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            field.Stamp(pond.Position, pond.Position, pond.Reach + PondShore, field.openDistance);
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            pond.Level = field.PondLevel(pond);
        }

        clock.Restart();
        field.BuildRavines();
        field.BuildRivers();
        field.timings.Append($"ravines and rivers {clock.ElapsedMilliseconds} ms");
        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            spire.Top = field.GroundAt(spire.Position.x / plan.Size, spire.Position.y / plan.Size) + spire.Height;
        }

        return field;
    }

    // Enough that the deepest ravine's floor stays above the map's lowest point under the lowest trail.
    private static float LiftFor(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        float deepest = 0f;
        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            deepest = Mathf.Max(deepest, ravine.Depth);
        }

        return deepest > 0f ? Mathf.Max(0f, deepest + RavineFloorAbove - (rules.GroundHeight - rules.Hills)) : 0f;
    }

    // Every trail, spur, clearing and pocket has a height: a clearing its level's, a trail climbing from one end's to the other's (a ramp
    // that eases in and out between the clearings' edges), a spur or pocket the height where it starts. The wild ground takes the height
    // of the nearest. Between two nearby places of about the same height it blends, so ramps leave no ledges; where they differ by a level
    // it doesn't, and that edge is a cliff.
    private void BuildLevels(float halfWidth)
    {
        nearDistance = Filled(Far);
        otherDistance = Filled(Far);
        nearHeight = new float[resolution * resolution];
        otherHeight = new float[resolution * resolution];
        nearId = new int[resolution * resolution];
        otherId = new int[resolution * resolution];
        for (int i = 0; i < nearId.Length; i++)
        {
            nearId[i] = -1;
            otherId[i] = -1;
        }

        float step = plan.TerraceHeight;
        int id = 0;
        for (int i = 0; i < plan.Trails.Count && i < plan.Links.Count; i++, id++)
        {
            List<Vector2> trail = plan.Trails[i];
            RaidLayoutRoom from = plan.Rooms[plan.Links[i].a];
            RaidLayoutRoom to = plan.Rooms[plan.Links[i].b];
            float length = 0f;
            for (int k = 0; k + 1 < trail.Count; k++)
            {
                length += Vector2.Distance(trail[k], trail[k + 1]);
            }

            float low = from.Level * step, high = to.Level * step;
            float walk = Mathf.Max(0.01f, length - from.Radius - to.Radius);
            float start = from.Radius;
            float Ramp(float at) => Mathf.Lerp(low, high, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((at - start) / walk)));
            float run = 0f;
            for (int k = 0; k + 1 < trail.Count; k++)
            {
                float next = run + Vector2.Distance(trail[k], trail[k + 1]);
                StampLevel(trail[k], trail[k + 1], halfWidth, run, next, Ramp, id);
                run = next;
            }
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            float height = NearestHeight(spur[0]);
            for (int k = 0; k + 1 < spur.Count; k++)
            {
                StampLevel(spur[k], spur[k + 1], halfWidth * SpurWidth, 0f, 0f, _ => height, id);
            }

            id++;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            float height = room.Level * step;
            StampLevel(room.Position, room.Position, room.Radius, 0f, 0f, _ => height, id++);
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            float height = NearestHeight(centre);
            StampLevel(centre, centre, radius, 0f, 0f, _ => height, id++);
        }

        FillLevels();
        levelHeight = new float[resolution * resolution];
        for (int i = 0; i < levelHeight.Length; i++)
        {
            levelHeight[i] = BlendedLevel(i);
        }

        nearDistance = nearHeight = otherDistance = otherHeight = null;
        nearId = otherId = null;
    }

    private float NearestHeight(Vector2 point)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(point.x / cell), 0, resolution - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(point.y / cell), 0, resolution - 1);
        int index = y * resolution + x;
        return nearId[index] >= 0 ? BlendedLevel(index) : 0f;
    }

    // Like Stamp, but keeps the nearest two different places at every cell, with the height the place has at its nearest point.
    private void StampLevel(Vector2 from, Vector2 to, float radius, float runFrom, float runTo, System.Func<float, float> heightAt, int id)
    {
        float extent = radius + LevelReach;
        int xMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - extent) / cell));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + extent) / cell));
        int yMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - extent) / cell));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + extent) / cell));
        Vector2 along = to - from;
        float lengthSquared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x * cell, y * cell);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);
                float distance = Vector2.Distance(point, from + along * t) - radius;
                int index = y * resolution + x;
                if (id == nearId[index])
                {
                    if (distance < nearDistance[index])
                    {
                        nearDistance[index] = distance;
                        nearHeight[index] = heightAt(Mathf.Lerp(runFrom, runTo, t));
                    }
                }
                else if (distance < nearDistance[index])
                {
                    otherDistance[index] = nearDistance[index];
                    otherHeight[index] = nearHeight[index];
                    otherId[index] = nearId[index];
                    nearDistance[index] = distance;
                    nearHeight[index] = heightAt(Mathf.Lerp(runFrom, runTo, t));
                    nearId[index] = id;
                }
                else if (distance < otherDistance[index])
                {
                    otherDistance[index] = distance;
                    otherHeight[index] = heightAt(Mathf.Lerp(runFrom, runTo, t));
                    otherId[index] = id;
                }
            }
        }
    }

    private float BlendedLevel(int index)
    {
        if (otherId[index] < 0 || otherDistance[index] >= Far)
        {
            return nearHeight[index];
        }

        float rise = Mathf.Abs(nearHeight[index] - otherHeight[index]);
        float width = Mathf.Lerp(LevelBlend, 0.05f, Mathf.InverseLerp(CliffFrom, CliffFull, rise));
        float near = Mathf.SmoothStep(0f, 1f, 0.5f + (otherDistance[index] - nearDistance[index]) / width);
        return Mathf.Lerp(otherHeight[index], nearHeight[index], near);
    }

    // Beyond the stamps' reach, the nearest two places spread outward cell by cell, a step's length further each time (diagonals 1.41),
    // so deep wild ground still knows both and blends between them. Only shorter distances replace longer, so it settles.
    private void FillLevels()
    {
        var open = new Queue<int>();
        for (int i = 0; i < nearId.Length; i++)
        {
            if (nearId[i] >= 0)
            {
                open.Enqueue(i);
            }
        }

        float diagonal = cell * 1.41421f;
        while (open.Count > 0)
        {
            int index = open.Dequeue();
            int x = index % resolution, y = index / resolution;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= resolution || ny >= resolution)
                    {
                        continue;
                    }

                    int next = ny * resolution + nx;
                    float step = dx != 0 && dy != 0 ? diagonal : cell;
                    bool changed = Offer(next, nearDistance[index] + step, nearHeight[index], nearId[index]);
                    if (otherId[index] >= 0)
                    {
                        changed |= Offer(next, otherDistance[index] + step, otherHeight[index], otherId[index]);
                    }

                    if (changed)
                    {
                        open.Enqueue(next);
                    }
                }
            }
        }
    }

    // Keeps the nearest two different places at a cell; true when either changed.
    private bool Offer(int index, float distance, float height, int id)
    {
        if (id == nearId[index])
        {
            if (distance < nearDistance[index] - 0.001f)
            {
                nearDistance[index] = distance;
                nearHeight[index] = height;
                return true;
            }

            return false;
        }

        if (distance < nearDistance[index])
        {
            otherDistance[index] = nearDistance[index];
            otherHeight[index] = nearHeight[index];
            otherId[index] = nearId[index];
            nearDistance[index] = distance;
            nearHeight[index] = height;
            nearId[index] = id;
            return true;
        }

        if (id == otherId[index])
        {
            if (distance < otherDistance[index] - 0.001f)
            {
                otherDistance[index] = distance;
                otherHeight[index] = height;
                return true;
            }

            return false;
        }

        if (distance < otherDistance[index])
        {
            otherDistance[index] = distance;
            otherHeight[index] = height;
            otherId[index] = id;
            return true;
        }

        return false;
    }

    // A ravine's rim is the ground along its line before the cut. Its floor lies at least its depth below that and only ever falls, gently,
    // from the higher end, so a river runs the whole way down it; where the land rises over it, the ravine is deeper.
    private void BuildRavines()
    {
        if (plan.Ravines.Count == 0)
        {
            return;
        }

        ravineEdge = Filled(Far);
        ravineHalf = new float[resolution * resolution];
        ravineFloor = new float[resolution * resolution];
        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            int count = ravine.Line.Count;
            var rim = new float[count];
            for (int i = 0; i < count; i++)
            {
                rim[i] = GroundAt(ravine.Line[i].x / plan.Size, ravine.Line[i].y / plan.Size);
            }

            // A river runs from the higher end; a ravine with a way down keeps its open end first, whatever the land does.
            if (!ravine.Descent && rim[0] < rim[count - 1])
            {
                ravine.Line.Reverse();
                ravine.HalfWidths.Reverse();
                ravine.Tapers.Reverse();
                System.Array.Reverse(rim);
            }

            var floor = new float[count];
            for (int i = 0; i < count; i++)
            {
                floor[i] = rim[i] - ravine.Depth;
                if (i > 0)
                {
                    floor[i] = Mathf.Min(floor[i], floor[i - 1]);
                }
            }

            // No steeper than a fast stream, except where the land drops by a cliff: there the river falls (a waterfall), and the gentler
            // drops ahead lower the floor before them.
            ravine.Cascades.Clear();
            for (int i = count - 2; i >= 0; i--)
            {
                if (floor[i] - floor[i + 1] >= CascadeFrom)
                {
                    ravine.Cascades.Add(i);
                    continue;
                }

                floor[i] = Mathf.Min(floor[i], floor[i + 1] + RavineFall * Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]));
            }

            for (int i = 0; i < count; i++)
            {
                floor[i] = Mathf.Max(RavineFloorLowest, floor[i]);
            }

            if (ravine.Descent)
            {
                SlopeIn(ravine, rim, floor);
            }

            ravine.Rim = rim;
            ravine.Floor = floor;
            for (int i = 0; i + 1 < count; i++)
            {
                StampRavine(ravine.Line[i], ravine.Line[i + 1], ravine.HalfWidths[i], ravine.HalfWidths[i + 1], floor[i], floor[i + 1]);
            }
        }
    }

    // The open end's floor rises to just under the rim, steady enough to walk (about 21 degrees), and the cascades there are gone.
    private static void SlopeIn(RaidLayoutRavine ravine, float[] rim, float[] floor)
    {
        int count = floor.Length;
        float total = 0f;
        for (int i = 0; i + 1 < count; i++)
        {
            total += Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]);
        }

        float top = rim[0] - DescentLip;
        float run = 0f;
        float length = Mathf.Clamp((top - floor[count / 2]) * DescentSlope, DescentShortest, total * 0.6f);
        int foot = 0;
        for (int i = 0; i < count && run < length; i++)
        {
            foot = i;
            if (i > 0)
            {
                run += Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]);
            }
        }

        float bottom = floor[foot];
        run = 0f;
        for (int i = 0; i <= foot; i++)
        {
            if (i > 0)
            {
                run += Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]);
            }

            floor[i] = Mathf.Max(floor[i], Mathf.Lerp(top, bottom, Mathf.SmoothStep(0f, 1f, run / Mathf.Max(0.01f, length))));
        }

        ravine.Cascades.RemoveAll(at => at < foot);
    }

    private void StampRavine(Vector2 from, Vector2 to, float halfFrom, float halfTo, float floorFrom, float floorTo)
    {
        float extent = Mathf.Max(halfFrom, halfTo) + RavineReachBeyond;
        int xMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - extent) / cell));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + extent) / cell));
        int yMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - extent) / cell));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + extent) / cell));
        Vector2 along = to - from;
        float lengthSquared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x * cell, y * cell);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);
                float half = Mathf.Lerp(halfFrom, halfTo, t);
                float edge = Vector2.Distance(point, from + along * t) - half;
                int index = y * resolution + x;
                if (edge < ravineEdge[index])
                {
                    ravineEdge[index] = edge;
                    ravineHalf[index] = half;
                    ravineFloor[index] = Mathf.Lerp(floorFrom, floorTo, t);
                }
            }
        }
    }

    // A river's surface lies just under the ground along it and both its banks, and only ever falls, from the higher end; where the land
    // rises across its way it cuts a deeper bed through. Its channel is cut down from that surface (RiverCut).
    private void BuildRivers()
    {
        if (plan.Rivers.Count == 0)
        {
            return;
        }

        riverEdge = Filled(Far);
        riverHalf = new float[resolution * resolution];
        riverLevel = new float[resolution * resolution];
        riverDepth = new float[resolution * resolution];
        foreach (RaidLayoutRiver river in plan.Rivers)
        {
            int count = river.Line.Count;
            var surface = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 point = river.Line[i];
                Vector2 along = (river.Line[Mathf.Min(i + 1, count - 1)] - river.Line[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = new Vector2(-along.y, along.x) * (river.HalfWidths[i] + RiverBank * 0.5f);
                float low = Mathf.Min(GroundAt(point.x / plan.Size, point.y / plan.Size),
                    Mathf.Min(GroundAt((point.x + side.x) / plan.Size, (point.y + side.y) / plan.Size), GroundAt((point.x - side.x) / plan.Size, (point.y - side.y) / plan.Size)));
                surface[i] = low - RiverFreeboard;
            }

            if (surface[0] < surface[count - 1])
            {
                river.Line.Reverse();
                river.HalfWidths.Reverse();
                System.Array.Reverse(surface);
            }

            for (int i = 1; i < count; i++)
            {
                surface[i] = Mathf.Max(RavineFloorLowest + river.Depth, Mathf.Min(surface[i], surface[i - 1]));
            }

            river.Level = surface;
            for (int i = 0; i + 1 < count; i++)
            {
                StampRiver(river.Line[i], river.Line[i + 1], river.HalfWidths[i], river.HalfWidths[i + 1], surface[i], surface[i + 1], river.Depth);
            }
        }
    }

    private void StampRiver(Vector2 from, Vector2 to, float halfFrom, float halfTo, float levelFrom, float levelTo, float depth)
    {
        float extent = Mathf.Max(halfFrom, halfTo) + RiverReachBeyond;
        int xMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - extent) / cell));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + extent) / cell));
        int yMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - extent) / cell));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + extent) / cell));
        Vector2 along = to - from;
        float lengthSquared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x * cell, y * cell);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);
                float half = Mathf.Lerp(halfFrom, halfTo, t);
                float edge = Vector2.Distance(point, from + along * t) - half;
                int index = y * resolution + x;
                if (edge < riverEdge[index])
                {
                    riverEdge[index] = edge;
                    riverHalf[index] = half;
                    riverLevel[index] = Mathf.Lerp(levelFrom, levelTo, t);
                    riverDepth[index] = depth;
                }
            }
        }
    }

    // A bed deepest in the middle, shallow at a ford where a trail crosses, and banks from the waterline up to the ground: gentle at a
    // ford, so the trail walks down into the water.
    private float RiverCut(float u, float v, float ground)
    {
        float edge = Sample(riverEdge, u, v);
        if (edge >= RiverBank + RiverFordBank)
        {
            return ground;
        }

        float half = Mathf.Max(0.5f, Sample(riverHalf, u, v));
        float level = Sample(riverLevel, u, v);
        float ford = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Sample(trailDistance, u, v) / RiverFordFade));
        float bank = RiverBank + RiverFordBank * ford;
        if (edge >= bank)
        {
            return ground;
        }

        float height;
        if (edge < 0f)
        {
            float across = Mathf.Clamp01((edge + half) / half);
            float deep = Mathf.Lerp(Sample(riverDepth, u, v), rules.FordDepth, ford);
            height = Mathf.Lerp(level - deep, level - RiverWaterline, across * across);
        }
        else
        {
            height = Mathf.Lerp(level - RiverWaterline, ground, Mathf.SmoothStep(0f, 1f, edge / bank));
        }

        return Mathf.Min(ground, height);
    }

    /// <summary>How wet the ground is by the rivers at a point (metres from the map's corner): 1 at the water, fading over <paramref name="reach"/>.</summary>
    public float RiverWetness(Vector2 point, float reach)
    {
        if (riverEdge == null || reach <= 0f)
        {
            return 0f;
        }

        return 1f - Mathf.Clamp01(Sample(riverEdge, point.x / plan.Size, point.y / plan.Size) / reach);
    }

    /// <summary>
    /// True where nothing may stand, in metres from the map's south-west corner: in a ravine or at its very edge, and on a spire. What grows
    /// or lies there could never be reached, so trees, rocks and resources keep off.
    /// </summary>
    public bool KeepsClear(Vector2 point)
    {
        if (ravineEdge != null && Sample(ravineEdge, point.x / plan.Size, point.y / plan.Size) < KeepClearBeyond + RaidLayoutRavine.RagReach)
        {
            return true;
        }

        if (riverEdge != null && Sample(riverEdge, point.x / plan.Size, point.y / plan.Size) < RiverIntoBank)
        {
            return true;
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if ((point - spire.Position).sqrMagnitude < (spire.Reach + KeepClearBeyond) * (spire.Reach + KeepClearBeyond)
                && spire.Edge(point) < KeepClearBeyond)
            {
                return true;
            }
        }

        return false;
    }

    // Rock walls from the floor's edge to the rim, ragged in plan, wider or narrower and here and there stepped in shelves (strata); rubble
    // along their foot and boulders in the bed, some breaking the water. A trail crossing keeps a neck of solid ground.
    private float Cut(float u, float v, float ground)
    {
        float edge = Sample(ravineEdge, u, v);
        if (edge >= RaidLayoutRavine.RagReach)
        {
            return ground;
        }

        float x = u * plan.Size, y = v * plan.Size;
        edge -= (Noise(x, y, RagScale, 401f) - 0.5f) * 2f * (RaidLayoutRavine.RagReach - RagFine) + (Noise(x, y, RagFineScale, 433f) - 0.5f) * 2f * RagFine;
        if (edge >= 0f)
        {
            return ground;
        }

        float half = Sample(ravineHalf, u, v);
        float wall = RaidLayoutRavine.WallRun(half) * Mathf.Lerp(0.75f, 2.3f, Noise(x, y, WallVarietyScale, 419f));
        float up = Mathf.Clamp01(1f + edge / wall);
        float rise = Mathf.SmoothStep(0f, 1f, up);
        float strata = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.7f, Noise(x, y, StrataMaskScale, 409f)));
        if (strata > 0f)
        {
            float band = up * StrataBands + (Noise(x, y, RagScale, 421f) - 0.5f) * 0.6f;
            float step = Mathf.Floor(band);
            float stepped = Mathf.Clamp01((step + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f - StrataRiser, 0.5f + StrataRiser, band - step))) / StrataBands);
            rise = Mathf.Lerp(rise, stepped, strata);
        }

        float floorHalf = Mathf.Max(0.5f, half - wall);
        float across = Mathf.Clamp01((edge + half) / floorHalf);
        float rubble = Mathf.Max(0f, Noise(x, y, RubbleScale, 457f) - 0.5f) * 2f * Rubble + across * across * across * Talus;
        float floor = Sample(ravineFloor, u, v) - RavineBed * (1f - across * across) + rubble;
        float cut = Mathf.Min(ground, Mathf.Lerp(floor, ground, rise));
        float neck = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Sample(trailDistance, u, v) - NeckShoulder) / NeckRamp));
        return Mathf.Lerp(ground, cut, neck);
    }

    // A flat, rough top on sheer sides, with a little scree around the foot.
    private float Spires(Vector2 point, float ground)
    {
        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            float far = spire.Reach + SpireScree;
            if ((point - spire.Position).sqrMagnitude > far * far)
            {
                continue;
            }

            float edge = spire.Edge(point);
            float scree = Mathf.Clamp01(1f - edge / SpireScree);
            float body = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge + SpireWall) / SpireWall));
            float top = spire.Top + (Noise(point.x, point.y, RoughnessScale * 0.5f, 191f) - 0.5f) * 2f * SpireRoughness;
            ground = Mathf.Max(ground + scree * scree * SpireScreeHeight, Mathf.Lerp(ground, top, body));
        }

        return ground;
    }

    // Just below the lowest ground around it, so the water never stands above its own banks.
    private float PondLevel(RaidLayoutPond pond)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < PondRimSamples; i++)
        {
            Vector2 rim = pond.Rim(i * Mathf.PI * 2f / PondRimSamples, PondRimBeyond);
            lowest = Mathf.Min(lowest, GroundAt(rim.x / plan.Size, rim.y / plan.Size));
        }

        return lowest - PondBelowRim;
    }

    private float[] Filled(float value)
    {
        var grid = new float[resolution * resolution];
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = value;
        }

        return grid;
    }

    private void StampLine(List<Vector2> line, float radius, float[] grid)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Stamp(line[i], line[i + 1], radius, grid);
        }
    }

    // Writes the distance to a capsule into the cells near it, keeping whatever is nearer.
    private void Stamp(Vector2 from, Vector2 to, float radius, float[] grid)
    {
        float extent = radius + reach;
        int xMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - extent) / cell));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + extent) / cell));
        int yMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - extent) / cell));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + extent) / cell));
        Vector2 along = to - from;
        float lengthSquared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x * cell, y * cell);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);
                float distance = Vector2.Distance(point, from + along * t) - radius;
                int index = y * resolution + x;
                if (distance < grid[index])
                {
                    grid[index] = distance;
                }
            }
        }
    }

    private float Sample(float[] grid, float u, float v)
    {
        float x = Mathf.Clamp01(u) * (resolution - 1);
        float y = Mathf.Clamp01(v) * (resolution - 1);
        int x0 = Mathf.Min((int)x, resolution - 2);
        int y0 = Mathf.Min((int)y, resolution - 2);
        float tx = x - x0;
        float ty = y - y0;
        float bottom = Mathf.Lerp(grid[y0 * resolution + x0], grid[y0 * resolution + x0 + 1], tx);
        float top = Mathf.Lerp(grid[(y0 + 1) * resolution + x0], grid[(y0 + 1) * resolution + x0 + 1], tx);
        return Mathf.Lerp(bottom, top, ty);
    }

    private float Noise(float x, float y, float scale, float salt) =>
        Mathf.PerlinNoise((x + plan.NoiseOffset.x + salt) / scale, (y + plan.NoiseOffset.y - salt) / scale);

    /// <summary>The metres from (u, v) to the nearest trail or clearing: 0 or less is open ground.</summary>
    public float OpenDistance(float u, float v) => Sample(openDistance, u, v);

    /// <summary>The ground's height at (u, v), in metres above the map's lowest point.</summary>
    public float HeightAt(float u, float v)
    {
        float height = GroundAt(u, v);
        if (plan.Ponds.Count == 0 && plan.Spires.Count == 0 && ravineEdge == null && riverEdge == null)
        {
            return height;
        }

        var point = new Vector2(u * plan.Size, v * plan.Size);
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            height = Basin(pond, point, height);
        }

        if (riverEdge != null)
        {
            height = RiverCut(u, v, height);
        }

        if (plan.Spires.Count > 0)
        {
            height = Spires(point, height);
        }

        return ravineEdge != null ? Cut(u, v, height) : height;
    }

    // The bed sinks from the shore to the pond's depth; the bank leads back up to the ground around it.
    private static float Basin(RaidLayoutPond pond, Vector2 point, float ground)
    {
        float far = pond.Reach + PondBank;
        if ((point - pond.Position).sqrMagnitude >= far * far)
        {
            return ground;
        }

        float edge = pond.Edge(point);
        if (edge >= PondBank)
        {
            return ground;
        }

        if (edge >= 0f)
        {
            return Mathf.Lerp(pond.Level, ground, Mathf.SmoothStep(0f, 1f, edge / PondBank));
        }

        float slope = Mathf.Min(Mathf.Min(pond.Radii.x, pond.Radii.y) * 0.8f, 3.5f);
        return pond.Level - pond.Depth * Mathf.SmoothStep(0f, 1f, -edge / slope);
    }

    private float GroundAt(float u, float v)
    {
        float x = u * plan.Size;
        float y = v * plan.Size;
        float open = Mathf.Max(0f, OpenDistance(u, v));
        float ground = rules.GroundHeight + (Noise(x, y, rules.HillScale, 0f) - 0.5f) * 2f * rules.Hills * plan.Hilliness;
        if (plan.Hills.Count > 0)
        {
            var point = new Vector2(x, y);
            foreach (RaidLayoutHill hill in plan.Hills)
            {
                ground += hill.RiseAt(point);
            }
        }

        float variety = 1f + (Noise(x, y, RidgeVarietyScale, 31f) - 0.5f) * 2f * RidgeVariety;
        float ridges = plan.Regions.Count > 1 ? plan.Blend(new Vector2(x, y), mood => mood.ridges) : plan.Mood.ridges;
        float ridge = Mathf.SmoothStep(0f, 1f, open / rules.RidgeWidth) * rules.RidgeHeight * ridges * variety;
        float rough = (Noise(x, y, RoughnessScale, 67f) - 0.5f) * 2f * rules.Roughness * Mathf.Clamp01(open / RoughFrom);
        float edge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(plan.Size - x, plan.Size - y)));
        float rim = Mathf.SmoothStep(0f, 1f, 1f - edge / rules.RimWidth) * rules.RimHeight;
        float level = levelHeight != null ? Sample(levelHeight, u, v) : 0f;
        return ground + ridge + rough + rim + level + lift;
    }

    /// <summary>The heights as a terrain takes them (rows from the south, 0–1 of the map's height).</summary>
    public float[,] Heights(int size, float mapHeight) => Scaled(Metres(size, out _), mapHeight);

    /// <summary>The heights in metres above the map's lowest point (rows from the south), and the highest of them.</summary>
    public float[,] Metres(int size, out float highest)
    {
        var heights = new float[size, size];
        var rowHighest = new float[size];
        float last = size - 1f;
        // Row by row on every core: HeightAt only reads what Build worked out.
        System.Threading.Tasks.Parallel.For(0, size, row =>
        {
            float top = 0f;
            for (int column = 0; column < size; column++)
            {
                float height = HeightAt(column / last, row / last);
                heights[row, column] = height;
                top = Mathf.Max(top, height);
            }

            rowHighest[row] = top;
        });
        highest = 0f;
        foreach (float top in rowHighest)
        {
            highest = Mathf.Max(highest, top);
        }

        return heights;
    }

    /// <summary>Heights in metres as 0-1 of a terrain this many metres high, in place.</summary>
    public static float[,] Scaled(float[,] metres, float mapHeight)
    {
        int rows = metres.GetLength(0), columns = metres.GetLength(1);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                metres[row, column] = Mathf.Clamp01(metres[row, column] / mapHeight);
            }
        }

        return metres;
    }

    /// <summary>The painted zones a map needs, each resolution × resolution with row 0 at the south.</summary>
    public void Zones(out byte[] path, out byte[] forest, out byte[] clearing)
    {
        path = new byte[resolution * resolution];
        forest = new byte[resolution * resolution];
        clearing = new byte[resolution * resolution];
        float density = rules.ForestDensity * plan.Mood.forest;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                float worldX = x * cell;
                float worldY = y * cell;
                // A little noise on the edges, so a clearing is never a perfect circle.
                float wobble = (Noise(worldX, worldY, WobbleScale, 11f) - 0.5f) * 2f * EdgeWobble;
                float patches = 0.55f + 0.9f * Noise(worldX, worldY, rules.ForestPatches, 113f);
                path[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (trailDistance[index] + SoilEdge) / (SoilEdge * 2f)));
                clearing[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (clearDistance[index] + wobble + SoilEdge) / (SoilEdge * 2f)));
                float here = plan.Regions.Count > 1 ? rules.ForestDensity * plan.Blend(new Vector2(worldX, worldY), mood => mood.forest) : density;
                forest[index] = Byte(here * patches * Mathf.SmoothStep(0f, 1f, (openDistance[index] + wobble) / ForestEdge));
                if (ravineEdge != null && ravineEdge[index] < 0f)
                {
                    // Gravel on a ravine's floor, which the walls' rock gives way to.
                    float floorEdge = -RaidLayoutRavine.WallRun(ravineHalf[index]);
                    float gravel = 1f - Mathf.SmoothStep(0f, 1f, (ravineEdge[index] - floorEdge + RavineSoil) / RavineSoil);
                    path[index] = (byte)Mathf.Max(path[index], Byte(gravel));
                }
            }
        }

        // A bed of bare soil under the water and along the shore: trees, rocks and plants keep off it as they do off a trail.
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            float far = pond.Reach + PondSoil;
            int xMin = Mathf.Max(0, Mathf.FloorToInt((pond.Position.x - far) / cell));
            int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((pond.Position.x + far) / cell));
            int yMin = Mathf.Max(0, Mathf.FloorToInt((pond.Position.y - far) / cell));
            int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((pond.Position.y + far) / cell));
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float soil = 1f - Mathf.SmoothStep(0f, 1f, pond.Edge(new Vector2(x * cell, y * cell)) / PondSoil);
                    path[y * resolution + x] = (byte)Mathf.Max(path[y * resolution + x], Byte(soil));
                }
            }
        }
    }

    private static byte Byte(float strength) => (byte)Mathf.RoundToInt(Mathf.Clamp01(strength) * 255f);
}
