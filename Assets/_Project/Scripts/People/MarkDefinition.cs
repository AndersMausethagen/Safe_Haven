using System.Collections.Generic;
using UnityEngine;

/// <summary>What leaves a mark on someone who went along on a raid (MarkDefinition › Cause). Saved as numbers in assets: add at the end.</summary>
public enum MarkCause
{
    /// <summary>Given by nothing on its own (only as what another mark turns into).</summary>
    None = 0,
    /// <summary>Home from a raid that went well: out alive and never downed.</summary>
    GoodRaid = 1,
    /// <summary>Downed in the raid and helped up.</summary>
    Downed = 2,
    /// <summary>The raid failed with them alive beside the player.</summary>
    Failed = 3,
    /// <summary>The forest had them (lost, then found).</summary>
    Lost = 4,
    /// <summary>Felled enough of one kind of creature along (Creature, Count), over raids.</summary>
    Kills = 5,
    /// <summary>Beat a boss beside the player.</summary>
    Boss = 6,
}

/// <summary>
/// A mark a raid leaves on a person (Docs/PeopleResearch_2026-11-23.md §3.6; Darkest Dungeon's quirks, RimWorld's scars, without death):
/// good ones earned by what went well, hard ones by being downed, lost or beaten. Each hard mark has a way back: a thing given in
/// conversation (Healed By), a friend's scene, or deeds that turn it into its opposite (Turns Into). Shown on their card. Its
/// effects are a trait's (TraitEffect). Data/People/Marks.
/// </summary>
[CreateAssetMenu(fileName = "NewMark", menuName = "Safe Haven/People/Mark")]
public sealed class MarkDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string icon = "Star";
    [SerializeField, TextArea(2, 4)] private string description;
    [Tooltip("A good mark (kept in the good three) or a hard one (the hard three).")]
    [SerializeField] private bool good;
    [SerializeField] private List<TraitEffect> effects = new List<TraitEffect>();

    [Header("How it comes")]
    [SerializeField] private MarkCause cause;
    [Tooltip("Chance it comes when its cause happens (for Kills: once the count is reached).")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.3f;
    [Tooltip("Kills: which creatures (a word in their id) and how many along.")]
    [SerializeField] private string creature;
    [SerializeField, Min(0)] private int count;

    [Header("The way back (hard marks)")]
    [Tooltip("Given to them in conversation, it heals the mark (and is used up).")]
    [SerializeField] private ItemDefinition healedBy;
    [Tooltip("A small scene with a Friend heals it.")]
    [SerializeField] private bool friendsHeal;
    [Tooltip("Deeds turn it into this (its Creature and Count, counted from when it came).")]
    [SerializeField] private MarkDefinition turnsInto;
    [Tooltip("Can't be healed while they still have memories lost to the forest.")]
    [SerializeField] private bool heldByTheForest;
    [Tooltip("Said of it when spoken to (their tell).")]
    [SerializeField, TextArea] private string line;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Icon => string.IsNullOrWhiteSpace(icon) ? "Star" : icon;
    public string Description => description;
    public bool Good => good;
    public IReadOnlyList<TraitEffect> Effects => effects;
    public MarkCause Cause => cause;
    public float Chance => chance;
    public string Creature => creature;
    public int Count => count;
    public ItemDefinition HealedBy => healedBy;
    public bool FriendsHeal => friendsHeal;
    public MarkDefinition TurnsInto => turnsInto;
    public bool HeldByTheForest => heldByTheForest;
    public string Line => line;

    /// <summary>Sets it up (content tools and tests).</summary>
    public void Configure(string markId, string shownName, string iconName, string about, bool isGood, IEnumerable<TraitEffect> changes,
        MarkCause comesOf, float odds, string kind = null, int needed = 0)
    {
        id = markId;
        displayName = shownName;
        icon = iconName;
        description = about;
        good = isGood;
        effects = new List<TraitEffect>(changes ?? new TraitEffect[0]);
        cause = comesOf;
        chance = odds;
        creature = kind;
        count = needed;
    }

    public void SetWayBack(ItemDefinition item, bool friends, MarkDefinition becomes, bool forest, string said = null)
    {
        healedBy = item;
        friendsHeal = friends;
        turnsInto = becomes;
        heldByTheForest = forest;
        line = said;
    }
}
