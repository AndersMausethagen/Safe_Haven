using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a RaidLayoutPlan into ground: how far every spot lies from open ground, and from that the heights and the painted
/// zones a hand-made map would have. The trails run along the valley floors and the wild ground rises between them, so
/// what the plan joined is what the player can walk. Plain arithmetic over grids, free of the terrain, so it can be tested.
/// <para>
/// The field's grids and the sampling they share are here; how each is made is in parts: .Levels, .Ravines (with
/// spires), .Rivers and .Ground (the height, the height map and zones the terrain is made from).
/// </para>
/// </summary>
public sealed partial class RaidLayoutField
{
    private const float ForestEdge = 7f;
    private const float EdgeWobble = 2.5f;
    private const float RoughnessScale = 9f;
    private const float SpurWidth = 0.6f;
    private const float PondShore = 5f;
    // Cliffs and ravines (moods with terraces, ravines or spires).
    private const float Far = 1e6f;
    private const float RavineFloorLowest = 0.5f;
    private const float RavineFloorAbove = 1.5f;

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
    // On a Valley map: how far each spot lies beyond the floor's edge (below zero: on the floor), and the trails at fords only (a
    // bridged crossing keeps the river deep under it).
    private float[] valleyBeyond;
    private float[] fordDistance;
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
        if (rules.Fen.enabled)
        {
            field.BuildFen();
        }
        if (plan.Valley != null)
        {
            clock.Restart();
            field.BuildValley(halfWidth);
            field.timings.Append($"valley {clock.ElapsedMilliseconds} ms, ");
        }

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

    /// <summary>The metres from (u, v) to the edge of the nearest trail, spur, clearing or pocket, leaving ponds aside (which
    /// <see cref="OpenDistance"/> counts as open): 0 or less is where the player walks.</summary>
    public float WalkedDistance(float u, float v) => Mathf.Min(Sample(trailDistance, u, v), Sample(clearDistance, u, v));
}
