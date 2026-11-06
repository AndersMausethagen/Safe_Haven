using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something the Haven asks to be brought home from the raids: who asks, what they say, what and how much they want,
/// and what they give for it. A few are asked at a time (HavenRequestSettings, SettlementState.Requests); they show in
/// the raid planner's briefing and in the asker's words in passing, and are handed over as soon as what was asked for is
/// home (bag or stash; HavenRequestBoard). Ask for what only the raids give, so the asking sends the player out.
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

    public string AskerName => asker != null ? asker.DisplayName : "The Haven";

    /// <summary>Whether it may be asked now: its asker lives at the Haven (and hasn't fallen), and it is its season.</summary>
    public bool CanAsk(SettlementState settlement, string season)
    {
        if (wanted == null || (asker != null && (settlement.Find(asker) == null)))
        {
            return false;
        }

        if (string.IsNullOrEmpty(season) || inSeasons.Count == 0)
        {
            return true;
        }

        return inSeasons.Exists(one => string.Equals(one, season, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The briefing's line: who wants what, and how much of it is home already.</summary>
    public string BriefingLine(int haveAtHome) =>
        $"Asked at home: {AskerName} wants {count} {(wanted != null ? wanted.DisplayName : "?")} ({Mathf.Min(haveAtHome, count)}/{count} home). {ask}";

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
    }
}
