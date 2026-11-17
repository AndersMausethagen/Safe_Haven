using UnityEngine;

/// <summary>
/// A fen map's ground (RaidLayoutRules › Fen, the Moss Marsh): the trails and clearings stand just above one water level and the wild
/// ground between them sinks under it, with hummocks (patches of noise) and long strings of firmer peat (bands across the map at a
/// seed's angle, the Norwegian string bog) rising back above it. The water itself is one sheet (RaidMapGenerator.PlaceFenWater) at
/// <see cref="FenWater"/>. Part of RaidLayoutField, in its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    /// <summary>A fen's ground roughness, as a share of the rules' own: still water would show every bump.</summary>
    private const float FenRoughShare = 0.25f;

    /// <summary>Metres above the water that count as dry ground for trees and rocks.</summary>
    private const float FenDryAbove = 0.08f;

    private const float StringWobbleScale = 23f;

    private Vector2 fenStrings = Vector2.right;

    /// <summary>The fen's water level in metres above the map's lowest point (meaningless when the rules have no fen).</summary>
    public float FenWater => rules.GroundHeight + lift - rules.Fen.aboveWater;

    // Set once in Build, before the heights are worked out on every core.
    private void BuildFen()
    {
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Fen Strings"));
        float angle = (float)random.NextDouble() * Mathf.PI;
        fenStrings = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // The fen's lift (below zero: down) at a point that lies open metres from the nearest trail or clearing.
    private float FenAt(float x, float y, float open)
    {
        RaidFenRule fen = rules.Fen;
        float sunk = Mathf.SmoothStep(0f, 1f, open / fen.sinkWidth);
        float hummock = Mathf.Clamp01((Noise(x, y, fen.hummockScale, 211f) - (1f - fen.hummocks)) / Mathf.Max(0.01f, fen.hummocks) * 2f);
        Vector2 across = fenStrings;
        float along = x * across.x + y * across.y + (Noise(x, y, StringWobbleScale, 97f) - 0.5f) * fen.stringSpacing * 0.5f;
        float band = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(along / fen.stringSpacing * Mathf.PI * 2f), 8f) * fen.strings;
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Max(hummock, band));
        return sunk * (rise * (fen.sink + fen.hummockHeight) - (fen.sink + fen.aboveWater));
    }
}
