using UnityEngine;

/// <summary>
/// A person who can live in Safe Haven (GDD §15): the stranded traveler found in a raid, later more. Holds who they are,
/// what they say, what they look like and which jobs they can take. Whether they have been rescued and which job they
/// hold is runtime state in SettlementState.
/// </summary>
[CreateAssetMenu(fileName = "NewResident", menuName = "Safe Haven/Settlement/Resident")]
public sealed class ResidentDefinition : ScriptableObject, IQuestTarget
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("Who they are, in a sentence or two. Shown when talking to them.")]
    [SerializeField, TextArea(2, 4)] private string description;
    [Tooltip("A painted head-and-shoulders portrait for the Haven window's People page (LocalSpawn: Tools/LocalSpawn/resident_portraits.json).")]
    [SerializeField] private Sprite portrait;

    [Header("Lines")]
    [Tooltip("Said when the player frees them in a raid.")]
    [SerializeField, TextArea] private string rescueLine = "Thank you! Get me out of here and I'll follow you anywhere.";
    [Tooltip("Said when they arrive in Safe Haven.")]
    [SerializeField, TextArea] private string arrivalLine = "So this is your haven. I could be useful here.";
    [Tooltip("Said when the player talks to them at home, until they have a job. Empty: their description.")]
    [SerializeField, TextArea] private string welcomeLine;
    [Tooltip("Said when they fall in a raid and are left behind.")]
    [SerializeField, TextArea] private string fallenLine = "I can't go on... come back for me.";

    [Tooltip("What they say in a fight, when they go along as a companion. A moment left empty uses what their kind of companion says.")]
    [SerializeField] private FightLines fightLines = new FightLines();

    [Header("World")]
    [Tooltip("The character model used in raids and at home.")]
    [SerializeField] private GameObject model;

    [Header("Raids")]
    [Tooltip("How they fight when they go along on a raid. Empty: they stay at home.")]
    [SerializeField] private CompanionDefinition raidCompanion;
    [Tooltip("What they say of a raid just gone, in their own words. A kind left out uses Settlement Settings' general lines.")]
    [SerializeField] private System.Collections.Generic.List<CompanionMemoryLines> memoryLines = new System.Collections.Generic.List<CompanionMemoryLines>();
    [Tooltip("What they say when a memory the forest took comes back, in their own words. A kind left out uses Settlement Settings' general lines.")]
    [SerializeField] private System.Collections.Generic.List<CompanionMemoryLines> returningLines = new System.Collections.Generic.List<CompanionMemoryLines>();

    [Header("Who they are")]
    [Tooltip("Where they start in the skills they had before the forest (the player's scale: 10 is ordinary), and what they love. " +
             "Every skill left out starts at People Settings › Unlisted Level (Docs/PeopleResearch_2026-11-23.md §3.1).")]
    [SerializeField] private System.Collections.Generic.List<TalentStart> talents = new System.Collections.Generic.List<TalentStart>();
    [Tooltip("Their two or three traits (§3.3): what they do differently, each with its tell.")]
    [SerializeField] private System.Collections.Generic.List<ResidentTraitDefinition> traits = new System.Collections.Generic.List<ResidentTraitDefinition>();
    [Tooltip("The one they get on with from the start (§3.5): they begin Friendly, once both live here.")]
    [SerializeField] private ResidentDefinition friend;
    [Tooltip("The one they rub up against from the start: they begin Cool, and argue now and then.")]
    [SerializeField] private ResidentDefinition friction;
    [Tooltip("Two things they love to be given (PeopleGifts): given in talk, a gift lifts their spirits for a few days and grows their bond " +
             "with the player.")]
    [SerializeField] private System.Collections.Generic.List<ItemDefinition> loves = new System.Collections.Generic.List<ItemDefinition>();
    [Tooltip("Their favourite dish (ResidentMeals): eaten first at breakfast when the chests hold it, and a lift to their spirits that day. " +
             "Food the residents eat (basic food, Home Life Settings › Most Meal Tier at most).")]
    [SerializeField] private ItemDefinition favouriteDish;
    [Tooltip("What they want of a home (HomeLife, HomeWishes): the feature they care for, what they say of it, and their thanks when it comes. " +
             "Here with the rest of who they are (Docs/PeopleSystemReview_2026-11-23.md §2.6); Home Life Settings › Likings is the old place.")]
    [SerializeField] private HomeLifeSettings.Liking homeLiking = new HomeLifeSettings.Liking();

    [Header("Jobs")]
    [Tooltip("Jobs they can be given at home. The first is suggested.")]
    [SerializeField] private JobDefinition[] jobs = new JobDefinition[0];

    [Header("Their quests at home")]
    [Tooltip("Quests they give once home, one after another (a QuestGiver's ordered offers: Start By Quest Giver, the next needing the one " +
             "before): Ingrid's The Alchemist and what she teaches at the altar after it. Their lines lead the conversation and the ! " +
             "shows over them. Empty: none.")]
    [SerializeField] private QuestOffer[] questLine = new QuestOffer[0];
    [Tooltip("Said when the last of their quests is handed in.")]
    [SerializeField, TextArea] private string questLineDone;
    [Tooltip("Quests they also have, each on its own beside their line, asked about in talk in any order (a QuestGiver's side offers): " +
             "the region of the Haven's land they know (Docs/HavenWorldStory_2026-11-26.md step 5). The offer says the hook in their voice, " +
             "the waiting line gives the way while it is under way, the thanks is said when its last quest is handed in to them.")]
    [SerializeField] private QuestOffer[] sideQuests = new QuestOffer[0];

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string QuestId => Id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Portrait => portrait;
    public string RescueLine => rescueLine;
    public string ArrivalLine => arrivalLine;
    public string WelcomeLine => welcomeLine;
    public string FallenLine => fallenLine;
    public FightLines FightLines => fightLines;
    public GameObject Model => model;
    public CompanionDefinition RaidCompanion => raidCompanion;
    public System.Collections.Generic.IReadOnlyList<QuestOffer> QuestLine => questLine;
    public string QuestLineDone => questLineDone;
    public System.Collections.Generic.IReadOnlyList<QuestOffer> SideQuests => sideQuests;
    public System.Collections.Generic.IReadOnlyList<CompanionMemoryLines> MemoryLines => memoryLines;
    public System.Collections.Generic.IReadOnlyList<CompanionMemoryLines> ReturningLines => returningLines;
    public JobDefinition[] Jobs => jobs;
    public System.Collections.Generic.IReadOnlyList<TalentStart> Talents => talents;
    public System.Collections.Generic.IReadOnlyList<ResidentTraitDefinition> Traits => traits;
    public ResidentDefinition Friend => friend;
    public ResidentDefinition Friction => friction;
    public System.Collections.Generic.IReadOnlyList<ItemDefinition> Loves => loves;
    public ItemDefinition FavouriteDish => favouriteDish;
    /// <summary>Their home liking, or null when it isn't set here.</summary>
    public HomeLifeSettings.Liking HomeLiking => homeLiking != null && !string.IsNullOrEmpty(homeLiking.likes) ? homeLiking : null;

    // A liking held on the person names them (a reference to the asset itself isn't kept in its own file): what HomeWishes and the
    // Likings list read.
    private void OnEnable()
    {
        if (homeLiking != null)
        {
            homeLiking.resident = this;
        }
    }

    /// <summary>Sets their favourite dish and home liking (content tools and tests).</summary>
    public void SetTastes(ItemDefinition dish, HomeLifeSettings.Liking liking)
    {
        favouriteDish = dish;
        homeLiking = liking ?? new HomeLifeSettings.Liking();
        homeLiking.resident = this;
    }

    /// <summary>Sets what they love to be given (content tools and tests).</summary>
    public void SetLoves(System.Collections.Generic.IEnumerable<ItemDefinition> items) => loves = new System.Collections.Generic.List<ItemDefinition>(items);

    /// <summary>Their written start in <paramref name="skill"/>, or one with no skill when it isn't listed.</summary>
    public TalentStart TalentFor(ProficiencyDefinition skill)
    {
        foreach (TalentStart start in talents)
        {
            if (start.skill != null && start.skill == skill)
            {
                return start;
            }
        }

        return default;
    }

    /// <summary>Sets who they are (content tools and tests).</summary>
    public void SetIdentity(System.Collections.Generic.IEnumerable<TalentStart> starts, System.Collections.Generic.IEnumerable<ResidentTraitDefinition> theirTraits,
        ResidentDefinition getsOnWith = null, ResidentDefinition rubsAgainst = null)
    {
        talents = new System.Collections.Generic.List<TalentStart>(starts ?? new TalentStart[0]);
        traits = new System.Collections.Generic.List<ResidentTraitDefinition>(theirTraits ?? new ResidentTraitDefinition[0]);
        friend = getsOnWith;
        friction = rubsAgainst;
    }
}
