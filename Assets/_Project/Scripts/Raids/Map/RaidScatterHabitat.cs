using System;
using UnityEngine;

/// <summary>
/// Where one kind of tree, shrub, rock or log belongs on a raid map. Scattering asks every kind how well it suits a spot
/// (<see cref="Suitability"/>) and picks among them by weight times suitability, so cedars crowd the deep forest, saplings
/// fill gaps and edges, logs lie by trunks and boulders gather under slopes. Plain data, so it is tuned in the World tab.
/// </summary>
[Serializable]
public sealed class RaidScatterHabitat
{
    private const float SlopeSoftness = 4f;
    private const float ForestSoftness = 0.1f;
    private const float Least = 0.12f;

    [Tooltip("Slopes it grows or lies on (degrees). The middle of the range is no better than its ends; past them it fades out quickly.")]
    public Vector2 slope = new Vector2(0f, 90f);
    [Tooltip("How dense the forest around it may be (0 open ground, 1 densest).")]
    public Vector2 forest = new Vector2(0f, 1f);
    [Tooltip("Positive: favours wet ground (by water, in damp hollows). Negative: favours dry ground.")]
    [Range(-1f, 1f)] public float wet;
    [Tooltip("Positive: favours the forest's edge by trails and clearings. Negative: favours the deep forest.")]
    [Range(-1f, 1f)] public float edge;
    [Tooltip("Positive: favours spots close to a trunk (stumps, fallen branches). Negative: favours gaps between trees.")]
    [Range(-1f, 1f)] public float nearTrees;
    [Tooltip("Grows in stands of its own kind instead of evenly mixed in.")]
    [Range(0f, 1f)] public float groves;
    [Tooltip("Metres it keeps from every other trunk (at full size): big crowns need room, saplings squeeze in.")]
    [Min(0f)] public float room;

    /// <summary>What scattering knows about a spot: everything a habitat can prefer.</summary>
    public readonly struct Spot
    {
        public readonly Vector2 Position;
        public readonly float Slope;
        public readonly float Forest;
        public readonly float Wet;
        public readonly float Edge;
        public readonly float NearTrees;

        public Spot(Vector2 position, float slope, float forest, float wet, float edge, float nearTrees)
        {
            Position = position;
            Slope = slope;
            Forest = forest;
            Wet = wet;
            Edge = edge;
            NearTrees = nearTrees;
        }
    }

    /// <summary>0 (never here) to about 1.5 (thrives here). <paramref name="kind"/> spreads the groves of different kinds apart.</summary>
    public float Suitability(in Spot spot, int kind)
    {
        float fit = Within(spot.Slope, slope, SlopeSoftness) * Within(spot.Forest, forest, ForestSoftness);
        if (fit <= 0f)
        {
            return 0f;
        }

        fit *= Leaning(wet, spot.Wet) * Leaning(edge, spot.Edge) * Leaning(nearTrees, spot.NearTrees);
        if (groves > 0f)
        {
            float stand = Mathf.PerlinNoise(spot.Position.x / 22f + kind * 37.1f, spot.Position.y / 22f - kind * 11.3f);
            fit *= Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1.5f, Mathf.InverseLerp(0.3f, 0.75f, stand)), groves);
        }

        return fit;
    }

    // 1 inside the range, fading to 0 over `soft` outside it.
    private static float Within(float value, Vector2 range, float soft)
    {
        if (value < range.x)
        {
            return Mathf.Clamp01(1f - (range.x - value) / soft);
        }

        return value > range.y ? Mathf.Clamp01(1f - (value - range.y) / soft) : 1f;
    }

    // A preference from -1 to 1 for a quality from 0 to 1: never quite rules a spot out, so mixes stay natural.
    private static float Leaning(float preference, float amount)
    {
        if (Mathf.Approximately(preference, 0f))
        {
            return 1f;
        }

        float wanted = preference > 0f ? amount : 1f - amount;
        return Mathf.Lerp(1f, Mathf.Lerp(Least, 1.4f, wanted), Mathf.Abs(preference));
    }
}
