using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a trait or a mark changes (Docs/PeopleResearch_2026-11-23.md §3.3, §3.6). Each is asked for by the one system it concerns
/// (ResidentTraits.Sum), so a new trait is data, not code. Saved as numbers in nothing; add new ones at the end anyway (assets keep them).
/// </summary>
public enum TraitEffectKind
{
    /// <summary>A share of work speed (0.1 = 10% quicker), at all work or only with Skill.</summary>
    WorkSpeed = 0,
    /// <summary>How fast they learn: a multiplier's share added (0.5 = x1.5), in all skills or only Skill.</summary>
    Learning = 1,
    /// <summary>Chance a piece they make is one quality step better (their station's gear), with Skill or any.</summary>
    QualityChance = 2,
    /// <summary>Hours their working day ends later (negative: earlier).</summary>
    WorkLater = 3,
    /// <summary>Hours they get up and start work earlier.</summary>
    StartEarlier = 4,
    /// <summary>A share of the weight they carry for their work.</summary>
    CarryWeight = 5,
    /// <summary>A share of their own foray power while the party is at most Party Size strong (0: any size).</summary>
    ForayPowerSmall = 6,
    /// <summary>A share of their own foray power while the party is more than Party Size strong.</summary>
    ForayPowerLarge = 7,
    /// <summary>A share added to everyone else's power in a foray they go on.</summary>
    LeadsParty = 8,
    /// <summary>A share of damage beside the player (raids and breaches).</summary>
    FightDamage = 9,
    /// <summary>Chance of reading a blow and stepping aside, added.</summary>
    FightDodge = 10,
    /// <summary>A share of health beside the player.</summary>
    FightHealth = 11,
    /// <summary>A share of health while defending the Haven in a breach.</summary>
    DefendHealth = 12,
    /// <summary>A share of how far they notice foes at night beside the player.</summary>
    NightSight = 13,
    /// <summary>A chance of one more of each crop harvested by them at the farm, added to their own.</summary>
    ExtraHarvest = 14,
    /// <summary>A share added to how fast standings with others grow (Kind 0.5, Gruff -0.5).</summary>
    StandingGain = 15,
    /// <summary>A share of the cheer they take from small scenes (-1: none).</summary>
    SceneCheer = 16,
    /// <summary>Living beside a rival doesn't bother them (any amount).</summary>
    RivalsDontBother = 17,
    /// <summary>Others at home are in better spirits for them: steps added to each one's spirits while they are home.</summary>
    KindWords = 18,
    /// <summary>Rivalry scenes with them are bigger: the share added to how far they swing.</summary>
    BoldWords = 19,
    /// <summary>A share added to what their asks pay (Haven requests).</summary>
    AskReward = 20,
    /// <summary>A share added to everyone's Well Fed work while they are the Kitchen's cook.</summary>
    FeedsWell = 22,
    /// <summary>Spirits steps of their own (a mark's weight, a trait's temper): -1 lowers, +1 raises.</summary>
    Spirits = 23,
    /// <summary>A share of damage against one kind of creature (Creature).</summary>
    DamageAgainst = 24,
}

/// <summary>One thing a trait or mark changes, and by how much.</summary>
[Serializable]
public struct TraitEffect
{
    public TraitEffectKind kind;
    public float amount;
    [Tooltip("Only work, learning or quality with this skill (empty: any).")]
    public ProficiencyDefinition skill;
    [Tooltip("ForayPowerSmall/Large: the party size it turns at.")]
    [Min(0)] public int partySize;
    [Tooltip("DamageAgainst: which creatures (by a word in their id, e.g. troll, widow).")]
    public string creature;

    public TraitEffect(TraitEffectKind kind, float amount, ProficiencyDefinition skill = null, int partySize = 0, string creature = null)
    {
        this.kind = kind;
        this.amount = amount;
        this.skill = skill;
        this.partySize = partySize;
        this.creature = creature;
    }
}

/// <summary>
/// A trait (Docs/PeopleResearch_2026-11-23.md §3.3): one of two or three loud facts about a person, each with a tell the player sees
/// (its icon and name on their card, what it means on hover, and lines they say of it in passing) and a number. A flaw has an
/// upside. None takes control away. Written people have theirs in ResidentDefinition › Traits. Data/People/Traits.
/// </summary>
[CreateAssetMenu(fileName = "NewTrait", menuName = "Safe Haven/People/Trait")]
public sealed class ResidentTraitDefinition : ScriptableObject
{
    [Tooltip("Stable unique identifier.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("A kit icon's name (Star, Flame, Eye, Heart, Sword, Book, Hammer...).")]
    [SerializeField] private string icon = "Star";
    [Tooltip("What it means, in a line or two: shown on hover.")]
    [SerializeField, TextArea(2, 4)] private string description;
    [Tooltip("True for a double-edged trait: shown in the warm colour, not the good one.")]
    [SerializeField] private bool doubleEdged;
    [SerializeField] private List<TraitEffect> effects = new List<TraitEffect>();
    [Tooltip("Said of it now and then in passing (their tell). Empty: nothing.")]
    [SerializeField, TextArea] private string[] lines = new string[0];
    [Tooltip("Traits nobody has together with this one.")]
    [SerializeField] private List<ResidentTraitDefinition> excludes = new List<ResidentTraitDefinition>();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Icon => string.IsNullOrWhiteSpace(icon) ? "Star" : icon;
    public string Description => description;
    public bool DoubleEdged => doubleEdged;
    public IReadOnlyList<TraitEffect> Effects => effects;
    public IReadOnlyList<string> Lines => lines;
    public IReadOnlyList<ResidentTraitDefinition> Excludes => excludes;

    /// <summary>Sets it up (content tools and tests).</summary>
    public void Configure(string traitId, string shownName, string iconName, string about, bool edged, IEnumerable<TraitEffect> changes, IEnumerable<string> said)
    {
        id = traitId;
        displayName = shownName;
        icon = iconName;
        description = about;
        doubleEdged = edged;
        effects = new List<TraitEffect>(changes ?? Array.Empty<TraitEffect>());
        lines = said != null ? new List<string>(said).ToArray() : new string[0];
    }

    public void SetExcludes(IEnumerable<ResidentTraitDefinition> traits) => excludes = new List<ResidentTraitDefinition>(traits);
}
