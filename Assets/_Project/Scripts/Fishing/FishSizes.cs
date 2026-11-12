using UnityEngine;

/// <summary>
/// How big a fish is (plain rules): its length in centimetres from a wide bell curve (log-normal, so it leans long: most
/// 20-60 cm, a sharp fall past that, now and then a trophy, never under 10 or over 200), scaled by its kind (a perch runs
/// small, a pike big); what that weighs for its kind; and how much harder it fights the bigger it is (a 40 cm fish is the
/// fight FishingSettings is tuned for), which a good bait eases. Size is for show and for the fight: a fish feeds the same
/// whatever its length.
/// </summary>
public static class FishSizes
{
    public const float Shortest = 10f;
    public const float Longest = 200f;

    // The middle of the curve, in cm, for a kind of scale 1, and its spread (of the logarithm).
    public const float Median = 33f;
    public const float Spread = 0.45f;

    // A fish this long is the fight the settings are tuned for.
    private const float Tuned = 40f;

    /// <summary>A length for a kind (its Length Scale), from two uniform numbers in 0-1 (deterministic, for tests).</summary>
    public static float Roll(float u1, float u2, float scale = 1f)
    {
        u1 = Mathf.Clamp(u1, 1e-6f, 1f);
        float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return Mathf.Clamp(Median * Mathf.Max(0.1f, scale) * Mathf.Exp(Spread * z), Shortest, Longest);
    }

    public static float Roll(System.Random random, float scale = 1f) => Roll((float)random.NextDouble(), (float)random.NextDouble(), scale);

    /// <summary>What a fish of this kind and length weighs: its kind's middle weight at its middle length, by the cube of the length.</summary>
    public static float Kilograms(FishingSettings.Fish kind, float centimetres)
    {
        float middle = kind != null ? (kind.size.x + kind.size.y) * 0.5f : 1f;
        float typical = Median * (kind != null ? Mathf.Max(0.1f, kind.lengthScale) : 1f);
        float share = Mathf.Max(0.05f, centimetres / typical);
        return middle * share * share * share;
    }

    /// <summary>How much harder it fights than a 40 cm fish: it tires that much slower and pulls a little harder on its runs.</summary>
    public static float Difficulty(float centimetres) => Mathf.Clamp(Mathf.Pow(Mathf.Max(Shortest, centimetres) / Tuned, 0.8f), 0.6f, 3f);

    /// <summary>A big fish's extra fight, eased by the bait (0: not at all; 0.5: halved). A small fish is small whatever the bait.</summary>
    public static float Eased(float difficulty, float ease) => difficulty <= 1f ? difficulty : 1f + (difficulty - 1f) * (1f - Mathf.Clamp01(ease));

    /// <summary>How readily a fish of this length takes the bait, against a 40 cm one: big fish are wary, less so on good bait.</summary>
    public static float BiteWillingness(float centimetres, float ease) => 1f / Mathf.Pow(Mathf.Max(1f, Difficulty(centimetres)), 1.5f * (1f - Mathf.Clamp01(ease)));

    /// <summary>"46 cm", or "1.2 m" for a giant.</summary>
    public static string Describe(float centimetres) => centimetres >= 100f ? $"{centimetres / 100f:0.0#} m" : $"{centimetres:0} cm";
}
