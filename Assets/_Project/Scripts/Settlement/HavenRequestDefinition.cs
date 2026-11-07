using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something the Haven asks to be brought home from the raids: who asks, what they say, what and how much they want,
/// and what they give for it. A few are asked at a time (HavenRequestSettings, SettlementState.Requests); they show in
/// the raid planner's briefing and in the asker's words in passing, and are handed over as soon as what was asked for is
/// home (bag or stash; HavenRequestBoard). Ask for what only the raids give, so the asking sends the player out.
/// <para>A crafting order (Quality Fine or Masterwork) asks instead for a piece the player makes, of at least that make: it is
/// asked once the player's skill that makes it has reached From Level, and it pays a lesson in that skill and lasting renown.</para>
/// </summary>
[CreateAssetMenu(fileName = "Haven Request", menuName = "Safe Haven/Settlement/Haven Request")]
public sealed class HavenRequestDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [Tooltip("Who asks: only asked while they live at the Haven. Empty: the Haven as a whole asks.")]
    [SerializeField] private ResidentDefinition asker;
    [Tooltip("What they say when asking (the briefing and their words in passing).")]
    [SerializeField, TextArea(2, 4)] private string ask;
    [Tooltip("What they say when it is brought.")]
    [SerializeField, TextArea(2, 4)] private string thanks;
    [SerializeField] private ItemDefinition wanted;
    [SerializeField, Min(1)] private int count = 5;
    [Header("Given for it")]
    [SerializeField] private ItemDefinition reward;
    [SerializeField, Min(0)] private int rewardCount = 1;
    [SerializeField, Min(0)] private int coins;
    [SerializeField, Min(0)] private int experience = 60;
    [Tooltip("How much it eases the blight round home (SettlementState.Blight, 0-1).")]
    [SerializeField, Range(0f, 1f)] private float easesBlight;
    [Tooltip("Seasons it is asked in, by the Season Cycle's names. Empty: any.")]
    [SerializeField] private List<string> inSeasons = new List<string>();

    [Header("A crafting order")]
    [Tooltip("Plain: any of the item will do (an ask from the raids). Fine or Masterwork: a crafting order, met only by a piece " +
             "of at least this make.")]
    [SerializeField] private ItemQuality quality;
    [Tooltip("A crafting order is asked only once the player's skill that makes the item has reached this level.")]
    [SerializeField, Range(0, 50)] private int fromLevel;
    [Tooltip("The asker's lesson: experience in the skill that makes the item, given in full (no fall-off) when it is brought.")]
    [SerializeField, Min(0)] private int lesson;
    [Tooltip("Renown the Haven keeps for good work done for its people (HavenRenown › Good work for its people).")]
    [SerializeField, Min(0)] private int renown;

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public ResidentDefinition Asker => asker;
    public string Ask => ask;
    public string Thanks => thanks;
    public ItemDefinition Wanted => wanted;
    public int Count => count;
    public ItemDefinition Reward => reward;
    public int RewardCount => rewardCount;
    public int Coins => coins;
    public int Experience => experience;
    public float EasesBlight => easesBlight;
    public ItemQuality Quality => quality;
    public int FromLevel => fromLevel;
    public int Lesson => lesson;
    public int Renown => renown;

    /// <summary>A crafting order: wants a piece of at least Fine make.</summary>
    public bool IsOrder => quality != ItemQuality.Plain;

    /// <summary>What is wanted, by name, with the make an order asks for ("Fine Iron Sword").</summary>
    public string WantedName => wanted == null ? "?" : IsOrder ? $"{quality} {wanted.DisplayName}" : wanted.DisplayName;

    /// <summary>The skill that makes what an order wants, which its lesson teaches; null when no skill makes it.</summary>
    public ProficiencyDefinition Craft => wanted != null ? LevelingSettings.Current.SkillCrafting(CraftedWork.Of(wanted)) : null;

    public string AskerName => asker != null ? asker.DisplayName : "The Haven";

    /// <summary>Whether it may be asked now: its asker lives at the Haven (and hasn't fallen), and it is its season.</summary>
    public bool CanAsk(SettlementState settlement, string season)
    {
        if (wanted == null || (asker != null && (settlement.Find(asker) == null)))
        {
            return false;
        }

        // An order waits until the player could make it.
        if (IsOrder)
        {
            ProficiencyDefinition craft = Craft;
            if (craft == null || GameSession.Progress.LevelOf(craft, LevelingSettings.Current) < fromLevel)
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(season) || inSeasons.Count == 0)
        {
            return true;
        }

        return inSeasons.Exists(one => string.Equals(one, season, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The briefing's line: who wants what, and how much of it is home already.</summary>
    public string BriefingLine(int haveAtHome) =>
        $"{(IsOrder ? "Ordered" : "Asked")} at home: {AskerName} wants {count} {WantedName} ({Mathf.Min(haveAtHome, count)}/{count} home). {ask}";

    public void Configure(string requestId, ResidentDefinition who, string asking, string thanking, ItemDefinition item, int amount,
        ItemDefinition given, int givenCount, int givenCoins, int givenExperience, float blight, List<string> seasons)
    {
        id = requestId;
        asker = who;
        ask = asking;
        thanks = thanking;
        wanted = item;
        count = amount;
        reward = given;
        rewardCount = givenCount;
        coins = givenCoins;
        experience = givenExperience;
        easesBlight = blight;
        inSeasons = seasons ?? new List<string>();
        quality = ItemQuality.Plain;
        fromLevel = 0;
        lesson = 0;
        renown = 0;
    }

    /// <summary>Makes it a crafting order (after Configure): a piece of at least <paramref name="make"/>, asked from
    /// <paramref name="level"/> in the skill that makes it, paying a lesson and renown.</summary>
    public void ConfigureOrder(ItemQuality make, int level, int lessonExperience, int renownGiven)
    {
        quality = make;
        fromLevel = level;
        lesson = lessonExperience;
        renown = renownGiven;
    }
}
