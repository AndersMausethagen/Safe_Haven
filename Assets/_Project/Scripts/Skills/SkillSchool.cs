using UnityEngine;

/// <summary>
/// The kind of power a skill draws on, like V Rising's spell schools. It colors the skill's slot and tooltip. Values are
/// serialized as numbers, so add new schools at the end and never renumber.
/// </summary>
public enum SkillSchool
{
    /// <summary>Weapon arts: steel and muscle.</summary>
    Steel = 0,
    /// <summary>Beasts of the wild: wolves, bears, ravens.</summary>
    Beast = 1,
    /// <summary>Rune magic of ice and winter.</summary>
    Frost = 2,
    /// <summary>The Ash Lad's embers and hearth-fire.</summary>
    Ember = 3,
    /// <summary>The hidden folk's gifts: healing and luck.</summary>
    Wyrd = 4,
}

/// <summary>Which art a weapon-art bar entry strikes (SkillDefinition.WeaponArt).</summary>
public enum WeaponArtSlot
{
    /// <summary>A skill of its own.</summary>
    None = 0,
    /// <summary>The weapon in hand's own art.</summary>
    Own = 1,
    /// <summary>The art of the rune attuned to the weapon in hand.</summary>
    Rune = 2,
}

/// <summary>How schools are shown everywhere, so the skill window, tooltip and skill bar agree.</summary>
public static class SkillSchools
{
    public static Color ColorOf(SkillSchool school)
    {
        switch (school)
        {
            case SkillSchool.Beast: return new Color(0.62f, 0.66f, 0.70f, 1f);
            case SkillSchool.Frost: return new Color(0.45f, 0.72f, 0.95f, 1f);
            case SkillSchool.Ember: return new Color(0.95f, 0.52f, 0.20f, 1f);
            case SkillSchool.Wyrd: return new Color(0.50f, 0.80f, 0.38f, 1f);
            default: return new Color(0.80f, 0.64f, 0.38f, 1f);
        }
    }

    /// <summary>One short line on what the school draws on (beside its heading in the skill window).</summary>
    public static string Explain(SkillSchool school) => school switch
    {
        SkillSchool.Beast => "Wolves, bears and ravens.",
        SkillSchool.Frost => "Rune magic of ice and winter.",
        SkillSchool.Ember => "The Ash Lad's embers and hearth-fire.",
        SkillSchool.Wyrd => "The hidden folk's gifts: healing and luck.",
        _ => "Steel and muscle.",
    };

    public static string Rich(SkillSchool school)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(ColorOf(school))}>{school}</color>";
    }
}
