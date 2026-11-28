using UnityEngine;

/// <summary>
/// How tiers are shown everywhere: as Roman numerals in one neutral bone colour, so resources, items, tools and recipes all
/// agree, and colour is left to rarity alone (the user, 2026-11-28: the tiers' white-green-blue-purple-orange was the rarity
/// scale, so "Common · Tier II" read as Uncommon). Tier 0 contains hand-picked starting materials; Tier I introduces tools.
/// </summary>
public static class Tiers
{
    public const int Min = 0;
    public const int Max = 5;

    private static readonly string[] Numerals = { "0", "I", "II", "III", "IV", "V" };

    private static readonly Color Bone = new Color(0.88f, 0.82f, 0.68f);

    // The numeral carries the tier; tier 0, the starting materials, a little dimmer.
    private static readonly Color[] Colors =
    {
        new Color(0.62f, 0.59f, 0.53f),
        Bone,
        Bone,
        Bone,
        Bone,
        Bone,
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
