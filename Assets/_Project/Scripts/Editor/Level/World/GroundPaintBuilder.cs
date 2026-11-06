using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a <see cref="GroundPaintArea"/>: inside its loop, each of its paint rules lays its layer by the ground's
/// steepness, height and hollows, broken into patches (WorldPaint.RuleWeight, as the terrain's auto-paint), faded in
/// from the edge over Edge Fade with noise so the border frays instead of drawing a line.
/// </summary>
public sealed class GroundPaintBuilder : FeatureBuilder
{
    private readonly GroundPaintArea area;

    public GroundPaintBuilder(GroundPaintArea area) : base(area)
    {
        this.area = area;
    }

    public override IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (area.Rules == null) yield break;
            foreach (TerrainPaintRules.Rule rule in area.Rules.Rules)
            {
                if (rule.layer != null) yield return rule.layer;
            }
        }
    }

    public override void Paint(PaintLayer layer)
    {
        if (area.Rules == null || Line == null || Line.Count < 3)
        {
            return;
        }

        TerrainData data = layer.Terrain.terrainData;
        Vector3 origin = layer.Terrain.transform.position;
        Vector3 size = data.size;
        var rules = area.Rules.Rules;
        var index = new int[rules.Count];
        for (int r = 0; r < rules.Count; r++)
        {
            index[r] = layer.LayerIndex(rules[r].layer);
        }

        var mask = new AreaMask(Line, area.EdgeFade, area.Seed);
        float du = WorldPaint.HollowRadius / size.x, dv = WorldPaint.HollowRadius / size.z;
        mask.ForEachInside(layer.Grid, layer.Rect, (x, y, p, fade) =>
        {
            float u = Mathf.Clamp01((p.x - origin.x) / size.x), v = Mathf.Clamp01((p.y - origin.z) / size.z);
            float slope = data.GetSteepness(u, v);
            float h = data.GetInterpolatedHeight(u, v);
            float around = (data.GetInterpolatedHeight(Mathf.Clamp01(u - du), v) + data.GetInterpolatedHeight(Mathf.Clamp01(u + du), v) +
                            data.GetInterpolatedHeight(u, Mathf.Clamp01(v - dv)) + data.GetInterpolatedHeight(u, Mathf.Clamp01(v + dv))) * 0.25f;
            float hollow = WorldPaint.Hollowness(h, around, WorldPaint.HollowRadius);
            for (int r = 0; r < rules.Count; r++)
            {
                if (index[r] < 0) continue;
                float noise = WorldNoise2D.Fbm(p.x / rules[r].patchSize, p.y / rules[r].patchSize, area.Rules.Seed + r * 17 + area.Seed);
                float w = WorldPaint.RuleWeight(rules[r], slope, origin.y + h, hollow, noise);
                layer.Paint(x, y, index[r], w * fade * area.Strength);
            }
        });
    }
}
