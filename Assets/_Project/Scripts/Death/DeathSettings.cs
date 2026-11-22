using System;
using UnityEngine;

/// <summary>How hard a fall is (Docs/DeathPenaltyResearch_2026-11-22.md, the user's answers): chosen at a new game, changeable in the options.</summary>
public enum DeathRules
{
    /// <summary>As before: a fall at home costs nothing.</summary>
    Gentle = 0,
    /// <summary>The default: a cairn, fading lessons, meals ended; a breach's falls cost the ward; a lost breach is plundered.</summary>
    Normal = 1,
    /// <summary>Harder: the cairn takes worn gear too, lessons fade twice as much, and a raid death loses everything.</summary>
    Harsh = 2,
}

/// <summary>What a fall costs under one set of <see cref="DeathRules"/>. Every number the death rules use is here.</summary>
[Serializable]
public sealed class DeathRuleSet
{
    [Header("A fall at home")]
    [Tooltip("The bag's contents drop into a cairn where the player fell, to be fetched back (Cairn). The safe pocket stays.")]
    public bool cairn = true;
    [Tooltip("And what they wear and wield goes into it too.")]
    public bool cairnTakesGear;
    [Tooltip("Each skill loses this share of what its next level takes, never below the start of its level (no level is lost).")]
    [Range(0f, 1f)] public float lessonShare = 0.25f;
    [Tooltip("The share of those lessons the cairn gives back when it is emptied.")]
    [Range(0f, 1f)] public float lessonsReturned = 0.5f;
    [Tooltip("For this long after waking, another fall costs no lessons (Valheim's 10 minutes). Kept through saving.")]
    [Min(0f)] public float graceSeconds = 600f;
    [Tooltip("Meals and tonics end with the fall: the cooking was the price.")]
    public bool boonsEnd = true;

    [Header("A fall in a ward breach")]
    [Tooltip("The portal wakes the fallen, and it costs the ward this share of its full health a fall.")]
    [Range(0f, 1f)] public float wardPerFall = 0.1f;
    [Tooltip("A fall never takes the ward below this share: only the attackers can bring it down.")]
    [Range(0f, 1f)] public float wardFloor = 0.2f;
    [Tooltip("Seconds before waking in a breach, and more for each further fall in it, up to the most.")]
    [Min(0f)] public float wakeSeconds = 10f;
    [Min(0f)] public float wakePerFall = 5f;
    [Min(0f)] public float wakeMost = 25f;

    [Header("A lost ward breach")]
    [Tooltip("The band carries off this share of the stash's most valuable material stacks (Plunder), carried by its leader into the next raid.")]
    [Range(0f, 1f)] public float plunderShare = 0.2f;
    [Min(0)] public int plunderStacks = 3;
    [Tooltip("At most this many of a stack.")]
    [Min(0)] public int plunderMost = 60;
    [Tooltip("Those who fought it come home Bruised (until the next dawn).")]
    public bool fightersBruised = true;

    [Header("A fall in a raid")]
    [Tooltip("Everything is lost: the bag, the safe pocket and what is worn and wielded (the user, 2026-11-22, for Harsh). Off: deployed gear " +
             "comes home worn, as ever.")]
    public bool raidTakesEverything;

    /// <summary>Gentle: the rules from before (nothing at home, nothing in a breach but the wait).</summary>
    public static DeathRuleSet MakeGentle() => new DeathRuleSet
    {
        cairn = false, lessonShare = 0f, boonsEnd = false, wardPerFall = 0f, wakeSeconds = 3f, wakePerFall = 0f, wakeMost = 3f,
        plunderShare = 0f, fightersBruised = false,
    };

    /// <summary>Harsh: the cairn takes gear, lessons halve, the ward and the stores cost more, and a raid death takes all.</summary>
    public static DeathRuleSet MakeHarsh() => new DeathRuleSet
    {
        cairnTakesGear = true, lessonShare = 0.5f, wardPerFall = 0.15f, plunderShare = 0.35f, raidTakesEverything = true,
    };
}

/// <summary>
/// The death rules (Docs/DeathPenaltyResearch_2026-11-22.md §4, the user's answers of 2026-11-22: "as recommended, also on harsh, when a
/// player dies in a raid, everything gets lost. even equipped items"). Which set applies is the character's (CharacterProgress ›
/// Death Rules), chosen at a new game (the character screen) and changeable in Options › Gameplay. The prologue and the first
/// breach are spared: they teach (Spared).
/// </summary>
[CreateAssetMenu(fileName = "DeathSettings", menuName = "Safe Haven/Settings/Death")]
public sealed class DeathSettings : GameSettings<DeathSettings>
{
    [SerializeField] private DeathRules newGameRules = DeathRules.Normal;
    [Tooltip("The pile of stones left where the player fell (Cairn), with a SaveableEntity so it lasts through saves.")]
    [SerializeField] private SaveableEntity cairnPrefab;
    [SerializeField] private DeathRuleSet gentle = DeathRuleSet.MakeGentle();
    [SerializeField] private DeathRuleSet normal = new DeathRuleSet();
    [SerializeField] private DeathRuleSet harsh = DeathRuleSet.MakeHarsh();

    /// <summary>The rules a new game starts with, unless the character screen chose others (ChosenForNewGame).</summary>
    public DeathRules NewGameRules => newGameRules;
    public SaveableEntity CairnPrefab => cairnPrefab;

    /// <summary>What the character screen chose for the game about to start; null for the default.</summary>
    public static DeathRules? ChosenForNewGame { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ChosenForNewGame = null;

    public DeathRuleSet For(DeathRules rules) => rules == DeathRules.Gentle ? gentle : rules == DeathRules.Harsh ? harsh : normal;

    /// <summary>The rules this character plays by.</summary>
    public static DeathRules Chosen => GameSession.Progress != null ? GameSession.Progress.DeathRules : Current.NewGameRules;

    /// <summary>What a fall costs now.</summary>
    public static DeathRuleSet Now => Current.For(Chosen);

    /// <summary>The prologue teaches: a fall there costs what it always did.</summary>
    public static bool Spared => PrologueDirector.Current != null;

    public static string Describe(DeathRules rules)
    {
        switch (rules)
        {
            case DeathRules.Gentle: return "A fall at home costs nothing; a raid death keeps your gear, worn.";
            case DeathRules.Harsh: return "A fall at home leaves your bag and gear in a cairn and costs half a level's lessons; a raid death loses everything, worn gear too.";
            default: return "A fall at home leaves your bag in a cairn and costs a quarter of a level's lessons; a raid death keeps your gear, worn.";
        }
    }
}
