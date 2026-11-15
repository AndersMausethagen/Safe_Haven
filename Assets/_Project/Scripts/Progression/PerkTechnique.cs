using System.Collections.Generic;

/// <summary>
/// A new way of doing something that a perk can teach, beyond plain stat bonuses (PerkDefinition › Technique). Each is
/// carried out by the system it changes, which asks <see cref="Techniques.Has"/>; the numbers are in TechniqueSettings. Which
/// perk teaches which technique is only data, so perks can be reshuffled without touching code.
/// </summary>
public enum PerkTechnique
{
    None = 0,
    /// <summary>Woodcutting: a normal swing keeps the combo; only a glancing one breaks it.</summary>
    SteadyRhythm = 1,
    /// <summary>Woodcutting: an axe's critical swing counts twice.</summary>
    HeartwoodStrike = 2,
    /// <summary>Mining: each critical swing brings one more ore.</summary>
    RichSeam = 3,
    /// <summary>Mining: a pickaxe's critical swing counts twice.</summary>
    Shatter = 4,
    /// <summary>Foraging: picking a plant by hand picks the same plants close by.</summary>
    Sweep = 5,
    /// <summary>Foraging: picking or cutting a plant gives a burst of speed.</summary>
    ForagersHaste = 6,
    /// <summary>Fishing: fish bite sooner.</summary>
    QuickBite = 7,
    /// <summary>Fishing: sometimes two fish come up on the line.</summary>
    DoubleCatch = 8,
    /// <summary>Weaponcrafting: after a run of plain pieces, the next comes out Fine for sure.</summary>
    WeaponSureHand = 9,
    /// <summary>Weaponcrafting: a Masterwork weapon or tool comes out already empowered once.</summary>
    WeaponRunedMake = 10,
    /// <summary>Armorcrafting: after a run of plain pieces, the next comes out Fine for sure.</summary>
    ArmorSureHand = 11,
    /// <summary>Armorcrafting: a Masterwork piece of armour comes out already empowered once.</summary>
    ArmorRunedMake = 12,
    /// <summary>Cooking: one more meal's bonus at a time.</summary>
    ThirdPlate = 13,
    /// <summary>Cooking: a meal heals too.</summary>
    HeartyMeals = 14,
    /// <summary>Alchemy: healing draughts heal more.</summary>
    PotentRemedies = 15,
    /// <summary>Alchemy: sometimes a draught or tonic isn't used up.</summary>
    SpareDose = 16,
    /// <summary>Construction: mending what a breach broke costs less.</summary>
    PatchUp = 17,
    /// <summary>Construction: hammering goes far faster.</summary>
    RaiseInADay = 18,
    /// <summary>Melee: a parry readies a counter, as a perfect dodge does.</summary>
    Riposte = 19,
    /// <summary>Melee: a kill with a weapon swing gives back stamina.</summary>
    SecondWind = 20,
    /// <summary>Archery: a perfect shot hits harder.</summary>
    DeadlyDraw = 21,
    /// <summary>Archery: a critical arrow roots what it hits for a moment.</summary>
    PinningShot = 22,
    /// <summary>Runecraft: sometimes a spell leaves no cooldown behind.</summary>
    SpellEcho = 23,
    /// <summary>Runecraft: a kill with a spell heals.</summary>
    SoulSiphon = 24,
    /// <summary>Sneaking: a blow from the shadows finishes an ordinary creature that is badly enough hurt by it.</summary>
    SilentTakedown = 25,
    /// <summary>Sneaking: sneaking is faster.</summary>
    Shadowstep = 26,
    /// <summary>Horsemanship: a blow never throws you from the saddle, and a horse being broken in bucks you half as hard.</summary>
    SureSeat = 27,
    /// <summary>Horsemanship: at a gallop, your horse runs down a creature in its path and knocks it off its feet.</summary>
    Trample = 28,
    /// <summary>Alchemy: a second tonic can work at once (Docs/PlayerStatsResearch.md #4).</summary>
    SecondVial = 29,
}

/// <summary>
/// Which techniques the player knows: those of the perks chosen in GameSession.Progress. Worked out again only when the
/// choice of perks changes (CharacterProgress.PerkVersion) or another game is loaded.
/// </summary>
public static class Techniques
{
    private static readonly HashSet<PerkTechnique> known = new HashSet<PerkTechnique>();
    private static CharacterProgress forProgress;
    private static int forVersion = -1;

    /// <summary>Raised with the perk the first time in a game its technique comes into play (the HUD names it).</summary>
    public static event System.Action<PerkDefinition> FirstUsed;

    /// <summary>
    /// Called where a technique just came into play. The first time in a game (remembered in the save) it is announced,
    /// so the player sees what they bought.
    /// </summary>
    public static void Used(PerkTechnique technique)
    {
        if (technique == PerkTechnique.None || !GameSession.Progress.MarkTechniqueSeen(technique))
        {
            return;
        }

        foreach (string id in GameSession.Progress.Perks)
        {
            PerkDefinition perk = LevelingSettings.Current.FindPerk(id, out _);
            if (perk != null && perk.Technique == technique)
            {
                FirstUsed?.Invoke(perk);
                return;
            }
        }
    }

    /// <summary>
    /// Whether Weapon Arts have their flourish: from TechniqueSettings › Flourish Level of the Melee skill. One-handed arts
    /// strike harder and a kill gives Focus back (Flurry), two-handed ones sunder poise, polearm ones pin.
    /// </summary>
    public static bool HasFlourish
    {
        get
        {
            LevelingSettings settings = LevelingSettings.Current;
            ProficiencyDefinition melee = settings.SkillTrainedBy(ProficiencyTraining.Melee);
            return melee != null && GameSession.Progress.LevelOf(melee, settings) >= TechniqueSettings.Current.FlourishLevel;
        }
    }

    /// <summary>The flourish of a stance's Weapon Art, by name, or null for one that has none.</summary>
    public static string FlourishOf(WeaponStance stance)
    {
        switch (stance)
        {
            case WeaponStance.OneHanded: return "Flurry";
            case WeaponStance.TwoHanded: return "Sunder";
            case WeaponStance.Polearm: return "Pin";
            default: return null;
        }
    }

    /// <summary>True when a chosen perk teaches this technique.</summary>
    public static bool Has(PerkTechnique technique)
    {
        CharacterProgress progress = GameSession.Progress;
        if (progress != forProgress || progress.PerkVersion != forVersion)
        {
            Refresh(progress);
        }

        return known.Contains(technique);
    }

    private static void Refresh(CharacterProgress progress)
    {
        known.Clear();
        forProgress = progress;
        forVersion = progress.PerkVersion;
        LevelingSettings settings = LevelingSettings.Current;
        foreach (string id in progress.Perks)
        {
            PerkDefinition perk = settings.FindPerk(id, out _);
            if (perk != null && perk.Technique != PerkTechnique.None)
            {
                known.Add(perk.Technique);
            }
        }
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        known.Clear();
        forProgress = null;
    }
}
