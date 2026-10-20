using UnityEngine;

/// <summary>
/// How tiers are shown everywhere: as Roman numerals with one color per tier, so resources, items, tools and recipes
/// all agree. Tier 1 is the starting tier.
/// </summary>
public static class Tiers
{
    public const int Max = 5;

    private static readonly string[] Numerals = { "-", "I", "II", "III", "IV", "V" };

    private static readonly Color[] Colors =
    {
        new Color(0.6f, 0.6f, 0.6f),
        new Color(0.86f, 0.86f, 0.86f),
        new Color(0.45f, 0.85f, 0.45f),
        new Color(0.45f, 0.65f, 1f),
        new Color(0.78f, 0.48f, 1f),
        new Color(1f, 0.72f, 0.25f),
    };

    public static string ToRoman(int tier)
    {
        return tier >= 0 && tier <= Max ? Numerals[tier] : tier.ToString();
    }

    public static Color ColorOf(int tier)
    {
        return Colors[Mathf.Clamp(tier, 0, Max)];
    }

    /// <summary>"Tier II" written in the tier's color, for UI text with rich text enabled.</summary>
    public static string RichLabel(int tier)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(ColorOf(tier))}>Tier {ToRoman(tier)}</color>";
    }
}
