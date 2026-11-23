using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>How a quest begins once its prerequisites are done.</summary>
public enum QuestStart
{
    /// <summary>As soon as every prerequisite quest is complete (or right away when there are none).</summary>
    Automatically = 0,
    /// <summary>When a Quest Event happens: walking into a zone, using an object.</summary>
    OnQuestEvent = 1,
    /// <summary>When something starts it: a QuestGiver the player interacts with, or any Inspector event calling QuestGiver.StartQuest.</summary>
    ByQuestGiver = 2,
    /// <summary>When the player does something (Start When): talks to someone, reaches a place, uses a thing, finds a
    /// stranded person, kills or crafts something. Any objective type can be the condition.</summary>
    WhenPlayerDoes = 3,
}

/// <summary>
/// What kind of quest it is: the colour of its marks everywhere, its name for the player and its place in the tracker
/// (QuestMarkerSettings). Saved by number, so new kinds go at the end; the tracker's order is QuestMarkerSettings.RankOrder.
/// Docs/QuestRedesign.md says what belongs where.
/// </summary>
public enum QuestRank
{
    /// <summary>The main story: gold. Something happens in every one: a fight, a find, a choice, a reveal. Never only "gather" or "build".</summary>
    MainStory = 0,
    /// <summary>A tale found out in the woods, a story of its own beside the main one: silver.</summary>
    SideQuest = 1,
    /// <summary>A small errand or bounty that can be left undone: bronze.</summary>
    Optional = 2,
    /// <summary>Bringing one of the lost expedition home: teal. Each opens a building, a job and recipes.</summary>
    Rescue = 3,
    /// <summary>Building and learning at home: green. Optional, several open at once, and it never gates the portal.</summary>
    Haven = 4,
}

/// <summary>
/// A quest: what the player is asked to do (objectives), what has to come first (prerequisites, which make chains), how
/// it begins, whether it is handed in at a Quest Giver and what it rewards. Everything is authored data; the QuestLog keeps
/// each player's progress. A chain is simply quests that list the previous quest as a prerequisite and start automatically.
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "Safe Haven/Quests/Quest")]
public sealed class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 5)] private string description;
    [Tooltip("Groups quests in the editor, e.g. \"Main Story\" or \"Smithing Chain\".")]
    [SerializeField] private string chain;
    [Tooltip("More than the description has room for, for the journal: why it matters, what people say about it. Keys written as [E] or (N) follow a rebind.")]
    [SerializeField, TextArea(3, 12)] private string journalNotes;
    [Tooltip("Main Story (gold), Side Quest (silver) or Optional (bronze): the colour of its marks on givers, the tracker, the maps and dialogue answers.")]
    [SerializeField] private QuestRank rank;
    [Tooltip("Shown in the tracker once prerequisites are met, before accepting or starting this quest. Empty hides it until it starts.")]
    [SerializeField] private string availableObjective;
    [Tooltip("Announce a new quest and play its sound. Turn off for intermediate stages of a larger quest chain.")]
    [SerializeField] private bool announceStart = true;
    [Tooltip("Announce completion and play its sound. Turn off until the whole quest chain is finished.")]
    [SerializeField] private bool announceCompletion = true;
    [Tooltip("Show this quest's registered timer in the main countdown HUD as well as the tracker.")]
    [SerializeField] private bool showTimerInHud;

    [Header("Starting")]
    [Tooltip("Quests that must be completed first. This is how chains are made.")]
    [SerializeField] private List<QuestDefinition> prerequisites = new List<QuestDefinition>();
    [Tooltip("Quests that only need to have been started (or finished): e.g. side quests offered once the leader has given " +
             "the opening quest, without waiting for it to be done.")]
    [SerializeField] private List<QuestDefinition> startedFirst = new List<QuestDefinition>();
    [SerializeField] private QuestStart start = QuestStart.Automatically;
    [Tooltip("For On Quest Event: the event that starts the quest.")]
    [SerializeField] private GameEventDefinition startEvent;
    [Tooltip("For When Player Does: what the player does that starts it, e.g. Reach Place: the Wilds Echo. Its amount is ignored.")]
    [SerializeField] private QuestObjective startWhen = new QuestObjective();
    [Tooltip("For an optional quest: once this quest starts, it is no longer offered. If it is under way it lapses and leaves the tracker; if it only waits to be handed in, it is handed in by itself.")]
    [SerializeField] private QuestDefinition closedBy;
    [Tooltip("Automatically and On Quest Event: seconds to wait once it is ready, for a beat of its own (a breather before a big moment). " +
             "0 uses Between Quests (Systems Editor › World › Story Pacing). Either way it also waits out a fight and anyone talking.")]
    [SerializeField, Min(0f)] private float startDelay;
    [Tooltip("Starts as soon as its delay is up, even in a fight or a conversation (Story Pacing otherwise holds it back, up to its " +
             "Longest Hold): for a quest that is the fight, such as holding the stones while the old troll comes.")]
    [SerializeField] private bool startsInAFight;
    [Tooltip("Haven, side and optional quests that start by themselves wait for the player at home, and only Story Pacing › New Per " +
             "Homecoming of them begin per return from a raid (one more whenever none of its kind is under way). Tick to let this one " +
             "start as soon as it can. Main and Rescue quests never wait.")]
    [SerializeField] private bool startsWithoutWaiting;
    [Tooltip("Custom events raised when this quest starts. Use them to drive scene listeners, weather, music and other quests.")]
    [SerializeField, RaisedByGame] private List<GameEventDefinition> onStarted = new List<GameEventDefinition>();

    [Header("Objectives")]
    [Tooltip("Off: objectives can be done in any order. On: each one only counts once the one above it is done.")]
    [SerializeField] private bool objectivesInOrder;
    [SerializeField] private List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("Teaches when it starts")]
    [Tooltip("Buildings learned the moment this quest starts, announced across the screen (Docs/QuestFlowReview_2026-11-20.md rule 3: " +
             "one building per quest, learned when the quest that introduces it starts). A building listed here is learned no other way " +
             "until one of the quests listing it has started (BuildingGate): a rescue or a job no longer teaches it first.")]
    [SerializeField] private List<BuildingDefinition> unlocksBuildings = new List<BuildingDefinition>();
    [Tooltip("Building modules known once this quest starts (BuildingModules.IsKnown): Copper Fire teaches the Storage Loft.")]
    [SerializeField] private List<BuildingModuleDefinition> unlocksModules = new List<BuildingModuleDefinition>();

    [Header("Finishing")]
    [Tooltip("Off: the quest completes the moment its objectives are done. On: the player must return to a Quest Giver for this " +
             "quest to hand it in; rewards and the completion event wait until then. A quest that brings someone home (Rescue Person " +
             "with Bring Home) is always handed in to that person at home, ticked or not.")]
    [SerializeField] private bool turnInToGiver;
    [Tooltip("Who to return to, shown in the quest tracker as \"Return to ...\", e.g. \"the Smith\". Empty: \"the quest giver\".")]
    [SerializeField] private string turnInTo;

    [Header("Rewards")]
    [SerializeField] private List<ItemAmount> rewardItems = new List<ItemAmount>();
    [SerializeField] private List<ResourceAmount> rewardResources = new List<ResourceAmount>();
    [SerializeField] private List<RecipeDefinition> rewardRecipes = new List<RecipeDefinition>();
    [Tooltip("Recipes it teaches as it starts, so one of its objectives can ask for the thing made (What the Old Wood Fears teaches " +
             "Ember Pitch, then asks for one). Rewards come at the end; these come at once.")]
    [SerializeField] private List<RecipeDefinition> teachesAtStart = new List<RecipeDefinition>();
    [Tooltip("Enchantments taught for good (a teacher shows how): Liv's quests (Docs/EnchantingResearch.md #6).")]
    [SerializeField] private List<EnchantmentDefinition> rewardEnchantments = new List<EnchantmentDefinition>();
    [Tooltip("Someone at home the quest rewards: their job skill grows by Reward Work (Liv's rank from her own quests), and they may be given a " +
             "passion to grow (Reward Passion) or a mark (Reward Mark).")]
    [SerializeField] private ResidentDefinition rewardWorkFor;
    [SerializeField, Min(0)] private int rewardWork;
    [Tooltip("Reward Work For may grow a passion: a pick owed, chosen in talk or their window's Talents.")]
    [SerializeField] private bool rewardPassion;
    [Tooltip("A mark Reward Work For carries from it (Giant-felled after a story boss).")]
    [SerializeField] private MarkDefinition rewardMark;
    [Tooltip("Experience for completing it. 0: the Quest Experience from LevelingSettings.")]
    [SerializeField, Min(0)] private int experience;
    [Tooltip("Raised when the quest is completed, for scene reactions (open a gate) through a GameEventListener.")]
    [SerializeField] private GameEventDefinition completionEvent;
    [Tooltip("Additional custom events raised when this quest completes, after Completion Event.")]
    [SerializeField, RaisedByGame] private List<GameEventDefinition> onCompleted = new List<GameEventDefinition>();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string Chain => chain;
    public string JournalNotes => journalNotes;
    public QuestRank Rank => rank;
    public string AvailableObjective => availableObjective;
    public bool AnnounceStart => announceStart;
    public bool AnnounceCompletion => announceCompletion;
    public bool ShowTimerInHud => showTimerInHud;
    public IReadOnlyList<QuestDefinition> Prerequisites => prerequisites;
    public IReadOnlyList<QuestDefinition> StartedFirst => startedFirst;
    public QuestStart Start => start;
    public GameEventDefinition StartEvent => startEvent;
    public QuestObjective StartWhen => startWhen;
    public QuestDefinition ClosedBy => closedBy;
    public float StartDelay => startDelay;
    public bool StartsInAFight => startsInAFight;

    /// <summary>
    /// Held for a homecoming (QuestManager): a Haven, side or optional quest that starts by itself, unless Starts Without Waiting.
    /// The flood after the first raid was nine of these starting in eleven minutes (Docs/QuestAndStoryResearch.md 1.3).
    /// </summary>
    public bool HeldForHomecoming => start == QuestStart.Automatically && !startsWithoutWaiting
        && (rank == QuestRank.Haven || rank == QuestRank.SideQuest || rank == QuestRank.Optional);
    public IReadOnlyList<GameEventDefinition> OnStarted => onStarted;
    public IReadOnlyList<GameEventDefinition> OnCompleted => onCompleted;
    public bool ObjectivesInOrder => objectivesInOrder;
    public IReadOnlyList<QuestObjective> Objectives => objectives;
    /// <summary>
    /// Handed in rather than completed on the spot: ticked here, or a rescue (<see cref="BroughtHome"/>), which is always handed
    /// in to the person saved once they are home (the user, 2026-11-21: "I should deliver and complete that quest with her and not
    /// automatically on extraction ... true on all quests where I save an NPC").
    /// </summary>
    public bool TurnInToGiver => turnInToGiver || BroughtHome != null;
    /// <summary>Who the quest is handed in to, for "Return to ..." text: a rescue's person when nothing else is named.</summary>
    public string TurnInTo => !string.IsNullOrWhiteSpace(turnInTo) ? turnInTo.Trim()
        : BroughtHome != null ? BroughtHome.DisplayName + " at home" : "the quest giver";

    /// <summary>
    /// The person a Rescue Person objective with Bring Home brings to the Haven, or null. Such a quest waits at the end for its
    /// hand-in to them in their conversation (ResidentNpc, ResidentQuests.HandInRescue), and their own quest line starts then.
    /// </summary>
    public ResidentDefinition BroughtHome
    {
        get
        {
            foreach (QuestObjective objective in objectives)
            {
                if (objective != null && objective.Type == ObjectiveType.RescuePerson && objective.BringHome && objective.Person != null)
                {
                    return objective.Person;
                }
            }

            return null;
        }
    }
    public IReadOnlyList<ItemAmount> RewardItems => rewardItems;
    public int Experience => experience;
    public IReadOnlyList<ResourceAmount> RewardResources => rewardResources;
    public IReadOnlyList<RecipeDefinition> RewardRecipes => rewardRecipes;
    public IReadOnlyList<RecipeDefinition> TeachesAtStart => teachesAtStart;
    public IReadOnlyList<BuildingDefinition> UnlocksBuildings => unlocksBuildings;
    public IReadOnlyList<BuildingModuleDefinition> UnlocksModules => unlocksModules;
    public IReadOnlyList<EnchantmentDefinition> RewardEnchantments => rewardEnchantments;
    public ResidentDefinition RewardWorkFor => rewardWorkFor;
    public int RewardWork => rewardWork;
    public bool RewardPassion => rewardPassion;
    public MarkDefinition RewardMark => rewardMark;

    /// <summary>Sets what the quest gives its person (content tools and tests).</summary>
    public void SetPersonReward(ResidentDefinition who, int work, bool passion, MarkDefinition givenMark)
    {
        rewardWorkFor = who;
        rewardWork = work;
        rewardPassion = passion;
        rewardMark = givenMark;
    }
    public GameEventDefinition CompletionEvent => completionEvent;

    /// <summary>The rewards in words: "3 Iron Ingot, 10 Hardwood, recipe: Iron Axe".</summary>
    public string DescribeRewards()
    {
        var parts = new List<string> { AmountText.Describe(rewardItems), AmountText.Describe(rewardResources) };
        foreach (RecipeDefinition recipe in rewardRecipes)
        {
            if (recipe != null)
            {
                parts.Add("recipe: " + recipe.DisplayName);
            }
        }

        foreach (EnchantmentDefinition enchantment in rewardEnchantments)
        {
            if (enchantment != null)
            {
                parts.Add("enchantment: " + enchantment.DisplayName);
            }
        }

        if (rewardWorkFor != null && rewardPassion)
        {
            parts.Add($"a passion for {rewardWorkFor.DisplayName}");
        }

        if (rewardWorkFor != null && rewardMark != null)
        {
            parts.Add($"{rewardWorkFor.DisplayName}: {rewardMark.DisplayName}");
        }

        parts.RemoveAll(string.IsNullOrEmpty);
        return string.Join(", ", parts);
    }

    /// <summary>Things wrong with the quest, in plain language.</summary>
    public List<string> FindProblems()
    {
        var problems = new List<string>();
        if (objectives.Count == 0)
        {
            problems.Add("No objectives, so it completes the moment it starts.");
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            string problem = objectives[i]?.FindProblem();
            if (problem != null)
            {
                problems.Add($"Objective {i + 1} {problem}.");
            }
        }

        if (start == QuestStart.OnQuestEvent && startEvent == null)
        {
            problems.Add("Starts on a quest event, but none is chosen, so it never starts.");
        }

        string startProblem = start == QuestStart.WhenPlayerDoes ? startWhen?.FindProblem() ?? (startWhen == null ? "is empty" : null) : null;
        if (startProblem != null)
        {
            problems.Add($"Starts when the player does something, but that {startProblem}, so it never starts.");
        }

        if (start == QuestStart.ByQuestGiver && startDelay > 0f)
        {
            problems.Add("Has a Start Delay, but it starts when someone asks, so the delay is never used.");
        }

        if (closedBy == this)
        {
            problems.Add("Is closed by itself, so it lapses the moment it starts.");
        }
        else if (closedBy != null && prerequisites.Contains(closedBy))
        {
            problems.Add("Comes after the quest that closes it, so it can never start.");
        }

        if (prerequisites.Contains(this))
        {
            problems.Add("Lists itself as a prerequisite, so it can never start.");
        }
        else if (DependsOn(this, new HashSet<QuestDefinition>()))
        {
            problems.Add("Its prerequisites lead back to itself, so it can never start.");
        }

        if (onStarted.Contains(null) || onCompleted.Contains(null))
            problems.Add("Lifecycle events contain an empty entry. Choose an event or remove the entry.");
        if (onStarted.Count != new HashSet<GameEventDefinition>(onStarted).Count ||
            onCompleted.Count != new HashSet<GameEventDefinition>(onCompleted).Count ||
            completionEvent != null && onCompleted.Contains(completionEvent))
            problems.Add("A lifecycle event is listed more than once, which would trigger reactions twice.");
        return problems;
    }

    private bool DependsOn(QuestDefinition target, HashSet<QuestDefinition> visited)
    {
        foreach (QuestDefinition prerequisite in prerequisites)
        {
            if (prerequisite == null || !visited.Add(prerequisite))
            {
                continue;
            }

            if (prerequisite == target || prerequisite.DependsOn(target, visited))
            {
                return true;
            }
        }

        return false;
    }
}
