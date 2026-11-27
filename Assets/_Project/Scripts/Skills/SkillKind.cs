/// <summary>
/// What a skill is for, and so what pays for it and which bar slot takes it (Docs/SkillChoiceResearch_2026-11-27.md 3.1-3.2; the user,
/// 2026-11-27: "the meta is to just put all skills on the bar and spam the one that does the most damage"). Values are serialized as
/// numbers: add new ones at the end.
/// </summary>
public enum SkillKind
{
    /// <summary>A school's spell or craft: paid in Focus (one circle, two for the strongest), on a cooldown too.</summary>
    Power = 0,
    /// <summary>A move of the body and the weapon (a lunge, a leap, a spin, a throw): free of Focus, used whenever its cooldown allows.
    /// Modest damage; it moves, opens or sets up.</summary>
    Technique = 1,
    /// <summary>The big turn of a fight: a full Focus bar (three circles) and a long cooldown.</summary>
    Ultimate = 2,
    /// <summary>The weapon in hand's art (or its rune's), as a bar entry: the art's own cost and cooldown.</summary>
    Art = 3,
}

/// <summary>The kinds of weapon, named by their signature as the player knows them (a skill's Weapons, SkillDefinition).</summary>
public static class WeaponFamilies
{
    public static string Name(WeaponSignature signature) => signature switch
    {
        WeaponSignature.CounterChain => "a sword",
        WeaponSignature.Assassin => "a dagger",
        WeaponSignature.GuardBreaker => "an axe or club",
        WeaponSignature.Momentum => "a greataxe",
        WeaponSignature.HyperArmour => "a greatsword",
        WeaponSignature.Crush => "a hammer",
        WeaponSignature.AntiCharge => "a spear",
        WeaponSignature.Flurry => "bare fists",
        WeaponSignature.Channel => "a staff",
        WeaponSignature.HuntersMark => "a bow",
        _ => "a weapon",
    };

    /// <summary>"a sword, a dagger or a spear".</summary>
    public static string Names(System.Collections.Generic.IReadOnlyList<WeaponSignature> signatures)
    {
        if (signatures == null || signatures.Count == 0)
        {
            return "a weapon";
        }

        var names = new System.Collections.Generic.List<string>();
        foreach (WeaponSignature signature in signatures)
        {
            names.Add(Name(signature));
        }

        return names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " or " + names[names.Count - 1];
    }
}

/// <summary>How each kind is named and explained to the player.</summary>
public static class SkillKinds
{
    public static string Name(SkillKind kind) => kind switch
    {
        SkillKind.Technique => "Technique",
        SkillKind.Ultimate => "Ultimate",
        SkillKind.Art => "Weapon Art",
        _ => "Power",
    };

    /// <summary>One line on what the kind is for and what pays for it.</summary>
    public static string Explain(SkillKind kind) => kind switch
    {
        SkillKind.Technique => "No Focus: use it whenever it is ready.",
        SkillKind.Ultimate => "Takes a full Focus bar.",
        SkillKind.Art => "Your weapon's own art.",
        _ => "Paid in Focus.",
    };
}
