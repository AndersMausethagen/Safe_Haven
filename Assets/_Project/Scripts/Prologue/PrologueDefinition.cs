using UnityEngine;
using System;

public enum PrologueStep
{
    FindShelter,
    GatherFuel,
    LightFire,
    CatchBreath,
    DefendCamp,
    ReachPortal,
    ActivatePortal,
    HoldPortal,
    Finished,
    /// <summary>No mechanic of its own: story graphs make the moments. The chapter says where it is and what the companions do.</summary>
    Story,
}

/// <summary>A place of the journey: the forest's edge, the camp by the shelter, the portal, the morning camp in the meadow.
/// Saved by position in chapters: add new places at the end.</summary>
public enum PrologueSite
{
    Forest,
    Camp,
    Portal,
    Meadow,
}

/// <summary>What the companions do in a Story chapter.</summary>
public enum PrologueCompanions
{
    /// <summary>They stay as they stand in the scene and take no orders (around the morning fire).</summary>
    StandStill,
    /// <summary>They wait at the chapter's place.</summary>
    WaitAtSite,
    /// <summary>They follow the player.</summary>
    Follow,
    /// <summary>A story moves them (Walk The Party), so the chapter gives them no order; they wait at the place until it does.</summary>
    StoryLeads,
    /// <summary>They wait at the chapter's place and leave any enemies to the player (a lesson).</summary>
    WatchOnly,
    /// <summary>They run to the chapter's place and fight nothing on the way (the flight from the old troll).</summary>
    Flee,
}

/// <summary>
/// What each step means for the places of the night, step by step in one place, so nothing depends on the order of the
/// steps: where a checkpoint puts the player back, where the companions wait and what they talk about. Earlier or later
/// in the story is still the order (PrologueInteraction's "not yet" and "already done").
/// </summary>
public static class PrologueSteps
{
    /// <summary>Where a checkpoint at this step puts the player back.</summary>
    public static PrologueSite Checkpoint(this PrologueStep step)
    {
        switch (step)
        {
            case PrologueStep.FindShelter:
                return PrologueSite.Forest;
            case PrologueStep.ActivatePortal:
            case PrologueStep.HoldPortal:
            case PrologueStep.Finished:
                return PrologueSite.Portal;
            case PrologueStep.Story:
                return PrologueSite.Meadow;
            default:
                return PrologueSite.Camp;
        }
    }

    /// <summary>Where the companions wait during this step, or null while they walk with the player.</summary>
    public static PrologueSite? Rally(this PrologueStep step)
    {
        switch (step)
        {
            case PrologueStep.FindShelter:
                return null;
            case PrologueStep.Story:
                return PrologueSite.Meadow;
            case PrologueStep.ReachPortal:
            case PrologueStep.ActivatePortal:
            case PrologueStep.HoldPortal:
            case PrologueStep.Finished:
                return PrologueSite.Portal;
            default:
                return PrologueSite.Camp;
        }
    }

    /// <summary>The place the companions talk about: where the expedition is, or is heading.</summary>
    public static PrologueSite Talk(this PrologueStep step)
    {
        switch (step)
        {
            case PrologueStep.FindShelter:
                return PrologueSite.Forest;
            case PrologueStep.Story:
                return PrologueSite.Meadow;
            case PrologueStep.ReachPortal:
            case PrologueStep.ActivatePortal:
            case PrologueStep.HoldPortal:
            case PrologueStep.Finished:
                return PrologueSite.Portal;
            default:
                return PrologueSite.Camp;
        }
    }

    /// <summary>True while there is time at the camp to sit and talk (the companions' camp topics).</summary>
    public static bool AtRest(this PrologueStep step) =>
        step == PrologueStep.GatherFuel || step == PrologueStep.LightFire || step == PrologueStep.CatchBreath;

    /// <summary>True once the portal stands open.</summary>
    public static bool PortalOpen(this PrologueStep step) => step == PrologueStep.HoldPortal || step == PrologueStep.Finished;
}

[Serializable]
public sealed class PrologueChapter
{
    [SerializeField] private QuestDefinition quest;
    [SerializeField] private PrologueStep behaviour;
    [SerializeField, TextArea(2, 6)] private string arrivalText;
    [SerializeField, TextArea(1, 3)] private string statusText;
    [SerializeField, Min(0f)] private float duration;
    [SerializeField, Min(0)] private int initialEnemies;
    [Tooltip("Who comes in this chapter's fight (camp defence, portal hold), picked at random by weight.")]
    [SerializeField] private PrologueWaveEnemy[] enemies = new PrologueWaveEnemy[0];
    [Header("Story chapters")]
    [Tooltip("Story behaviour: where it takes place: where a checkpoint puts the player back and what the companions talk about.")]
    [SerializeField] private PrologueSite site = PrologueSite.Meadow;
    [Tooltip("Story behaviour: what the companions do.")]
    [SerializeField] private PrologueCompanions companions = PrologueCompanions.WaitAtSite;
    [Tooltip("Story behaviour: there is time to talk, so the player can ask the companions their topics.")]
    [SerializeField] private bool timeToTalk;
    [Header("Part of a quest")]
    [Tooltip("0: the chapter lasts until its quest is complete. Otherwise it ends once the quest's objectives up to this one (1 is the first) are done, " +
             "and the next chapter takes the same quest on: one quest for the player, several steps for the scene (find the shelter, then build the fire).")]
    [SerializeField, Min(0)] private int endsAtObjective;
    [Header("Camp defence")]
    [Tooltip("How many times they come. Each wave after the first comes when the one before has fallen, and is as large as the first.")]
    [SerializeField, Min(1)] private int waves = 1;
    [Tooltip("Called out as each later wave comes (\"Runa: More of them!\"). Optional.")]
    [SerializeField, TextArea(1, 3)] private string nextWaveText;

    public QuestDefinition Quest => quest;
    public PrologueStep Behaviour => behaviour;
    /// <summary>The objective (1 is the first) this chapter ends at, or 0 when it lasts as long as its quest.</summary>
    public int EndsAtObjective => endsAtObjective;
    public int Waves => Mathf.Max(1, waves);
    public string NextWaveText => nextWaveText;

    /// <summary>True once the chapter is behind the player: its quest is complete, or the part of it this chapter stands for is done.</summary>
    public bool IsDone(QuestLog log)
    {
        if (log.StatusOf(quest) == QuestStatus.Completed) return true;
        if (endsAtObjective <= 0) return false;
        QuestLog.Entry entry = log.Get(quest);
        if (entry == null) return false;
        for (int i = 0; i < endsAtObjective && i < entry.Counts.Length; i++)
            if (!entry.IsObjectiveDone(i)) return false;
        return true;
    }
    public string ArrivalText => arrivalText;
    public string StatusText => statusText;
    public float Duration => duration;
    public int InitialEnemies => initialEnemies;
    public PrologueWaveEnemy[] Enemies => enemies;
    public PrologueCompanions Companions => behaviour == PrologueStep.Story ? companions
        // Running from the old troll: everyone makes for the standing stones, and nobody turns to fight it.
        : behaviour == PrologueStep.ReachPortal ? PrologueCompanions.Flee
        : behaviour.Rally().HasValue ? PrologueCompanions.WaitAtSite : PrologueCompanions.Follow;

    /// <summary>Where a checkpoint in this chapter puts the player back.</summary>
    public PrologueSite Checkpoint => behaviour == PrologueStep.Story ? site : behaviour.Checkpoint();
    /// <summary>Where the companions wait in this chapter.</summary>
    public PrologueSite RallySite => behaviour == PrologueStep.Story ? site : behaviour.Rally() ?? PrologueSite.Forest;
    /// <summary>The place the companions talk about.</summary>
    public PrologueSite Talk => behaviour == PrologueStep.Story ? site : behaviour.Talk();
    /// <summary>True while there is time to sit and talk (the companions' topics).</summary>
    public bool AtRest => behaviour == PrologueStep.Story ? timeToTalk : behaviour.AtRest();

    /// <summary>The enemy for one spawn: picked by weight among those allowed in this wave, with <paramref name="roll"/>
    /// in [0, 1); null when there are none.</summary>
    public GameObject Enemy(int wave, float roll)
    {
        float total = 0f;
        foreach (PrologueWaveEnemy enemy in enemies)
            if (enemy.Allowed(wave)) total += enemy.weight;
        if (total <= 0f) return null;

        float pick = Mathf.Clamp01(roll) * total;
        GameObject last = null;
        foreach (PrologueWaveEnemy enemy in enemies)
        {
            if (!enemy.Allowed(wave)) continue;
            last = enemy.prefab;
            pick -= enemy.weight;
            if (pick < 0f) return enemy.prefab;
        }
        return last;
    }
}

/// <summary>One kind of enemy that comes in a chapter's fight, and how often.</summary>
[Serializable]
public sealed class PrologueWaveEnemy
{
    [Tooltip("A monster prefab. Copies made while playing aren't saved, so encounters restart at checkpoints.")]
    public GameObject prefab;
    [Tooltip("How often it comes, against the others' weights.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("It comes from this wave on. At the camp, 1 = the enemies waiting when the defence starts. At the portal the enemies " +
             "waiting and the first timed wave are both 1, so 2 comes with the second timed wave (kept on purpose: the gentler hold).")]
    [Min(1)] public int fromWave = 1;

    public bool Allowed(int wave) => prefab != null && weight > 0f && Mathf.Max(1, wave) >= fromWave;
}

/// <summary>Authored prologue content. Progress belongs to the saved quest log, not this shared asset.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Story/Prologue", fileName = "ForestPrologue")]
public sealed class PrologueDefinition : ScriptableObject
{
    [Tooltip("Ordered story phases. Each phase waits for its quest to complete, or for the part of it the phase stands for (Ends At Objective), so one quest can run through several phases. Quests define objectives and prerequisites; Behaviour selects the scene reaction. Extra phases can reuse a behaviour.")]
    [SerializeField] private PrologueChapter[] chapters = new PrologueChapter[0];
    [SerializeField, RaisedByGame] private GameEventDefinition startedEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition shelterEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition fireEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition restedEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition campDefendedEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition portalReachedEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition portalActivatedEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition finishedEvent;
    [SerializeField] private ItemDefinition sticks;
    [SerializeField] private ItemDefinition stones;
    [Header("Starting Equipment")]
    [SerializeField] private ItemDefinition startingWeapon;
    [SerializeField] private ItemDefinition startingTorch;
    [Tooltip("On their back from the first moment, which gives the bag its room.")]
    [SerializeField] private ItemDefinition startingBackpack;
    [Tooltip("Granted and equipped once for a fresh expedition. Loading keeps the saved gear instead.")]
    [SerializeField] private ItemDefinition[] startingArmor = new ItemDefinition[0];
    [Header("Field Construction and Conversations")]
    [Tooltip("Only this building appears in the forest build menu. Keep the gathering quest aligned with its costs.")]
    [SerializeField] private BuildingDefinition campfire;
    [SerializeField, Min(1f)] private float buildRadius = 4f;
    [SerializeField, TextArea] private string waitingForLeaderText = "Speak to Sigrid about the next step.";
    [Tooltip("Who speaks a chapter's arrival text when it doesn't start with a name (\"Runa: ...\"): the expedition's leader.")]
    [SerializeField] private ResidentDefinition leader;
    [SerializeField, Range(0.3f, 1f)] private float dialogueCameraDistance = 0.65f;
    [Header("Encounters")]
    [SerializeField, Min(1f)] private float waveInterval = 25f;
    [SerializeField, Min(1)] private int firstWaveSize = 3;
    [SerializeField, Min(0)] private int waveGrowth = 1;
    [SerializeField, Min(1)] private int maximumWaveSize = 7;
    [SerializeField, Min(1)] private int maximumEnemies = 14;
    [SerializeField] private SoundDefinition rumble;
    [SerializeField] private string homeScene = "SafeHaven";
    [Tooltip("What the player wears at the end (armor, weapon, belt tools, backpack, lantern) and their arrows come home with them: the rule of a failed raid. Off: they arrive with nothing.")]
    [SerializeField] private bool wornGearComesHome = true;
    [Header("Narrative")]
    [SerializeField, TextArea(2, 5)] private string survivedText;
    [SerializeField, TextArea(2, 5)] private string overwhelmedText;
    [SerializeField, TextArea(1, 3)] private string endingStatus;
    [SerializeField, TextArea(2, 5)] private string homecomingText;
    [SerializeField, TextArea(1, 3)] private string recoveryText;
    [SerializeField, Min(0f)] private float endingSeconds = 3f;
    [SerializeField, Min(0f)] private float recoverySeconds = 2f;
    [Tooltip("Played as the connection breaks, before home loads: what happened at the stones, who caught the player, and what there is to do. Empty goes straight home.")]
    [SerializeField] private CinematicDefinition endingCinematic;
    [Tooltip("Loads home in the background while the cinematic plays, so the wait after it is short.")]
    [SerializeField] private bool loadHomeDuringCinematic = true;

    public CinematicDefinition EndingCinematic => endingCinematic;
    public bool LoadHomeDuringCinematic => loadHomeDuringCinematic;
    public PrologueChapter[] Chapters => chapters;
    public GameEventDefinition StartedEvent => startedEvent;
    public GameEventDefinition ShelterEvent => shelterEvent;
    public GameEventDefinition FireEvent => fireEvent;
    public GameEventDefinition RestedEvent => restedEvent;
    public GameEventDefinition CampDefendedEvent => campDefendedEvent;
    public GameEventDefinition PortalReachedEvent => portalReachedEvent;
    public GameEventDefinition PortalActivatedEvent => portalActivatedEvent;
    public GameEventDefinition FinishedEvent => finishedEvent;
    public ItemDefinition Sticks => sticks;
    public ItemDefinition Stones => stones;
    public ItemDefinition StartingWeapon => startingWeapon;
    public ItemDefinition StartingTorch => startingTorch;
    public ItemDefinition StartingBackpack => startingBackpack;
    public ItemDefinition[] StartingArmor => startingArmor;
    public BuildingDefinition Campfire => campfire;
    public float BuildRadius => buildRadius;
    public string WaitingForLeaderText => waitingForLeaderText;
    public ResidentDefinition Leader => leader;
    public float DialogueCameraDistance => dialogueCameraDistance;
    public float WaveInterval => waveInterval;
    public int MaximumEnemies => maximumEnemies;
    public int WaveSize(int wave) => Mathf.Min(firstWaveSize + Mathf.Max(0, wave - 1) * waveGrowth, maximumWaveSize);
    public SoundDefinition Rumble => rumble;
    public string HomeScene => homeScene;
    public bool WornGearComesHome => wornGearComesHome;
    public string SurvivedText => survivedText;
    public string OverwhelmedText => overwhelmedText;
    public string EndingStatus => endingStatus;
    public string HomecomingText => homecomingText;
    public string RecoveryText => recoveryText;
    public float EndingSeconds => endingSeconds;
    public float RecoverySeconds => recoverySeconds;

    public PrologueChapter Chapter(QuestLog log)
    {
        foreach (PrologueChapter chapter in chapters)
            if (!chapter.IsDone(log))
                return chapter;
        return null;
    }

    /// <summary>True when the chapter is the first of its quest: its quest's start is the chapter's start. A later part of the
    /// same quest begins without any hand-over, in the middle of what the player is doing.</summary>
    public bool OpensItsQuest(PrologueChapter chapter)
    {
        foreach (PrologueChapter other in chapters)
            if (other != null && other.Quest == chapter.Quest)
                return other == chapter;
        return true;
    }

    public string FindProblem()
    {
        if (chapters == null || chapters.Length == 0) return "The prologue needs story chapters.";
        var seen = new System.Collections.Generic.HashSet<QuestDefinition>();
        PrologueChapter previous = null;
        foreach (PrologueChapter chapter in chapters)
        {
            if (chapter == null || chapter.Quest == null) return "A prologue chapter or quest is missing.";
            bool samePart = previous != null && previous.Quest == chapter.Quest;
            if (!samePart && !seen.Add(chapter.Quest)) return "Chapters that share a quest must follow each other.";
            if (samePart && (previous.EndsAtObjective <= 0 || (chapter.EndsAtObjective > 0 && chapter.EndsAtObjective <= previous.EndsAtObjective)))
                return $"{chapter.Quest.DisplayName} runs through several chapters: each but the last needs Ends At Objective, later each time.";
            if (chapter.EndsAtObjective > chapter.Quest.Objectives.Count)
                return $"{chapter.Quest.DisplayName} has no objective {chapter.EndsAtObjective} for its chapter to end at.";
            previous = chapter;
            if (chapter.Behaviour == PrologueStep.Finished) return "Finished is reserved for the end of the sequence.";
            if ((chapter.Behaviour == PrologueStep.CatchBreath || chapter.Behaviour == PrologueStep.HoldPortal) &&
                chapter.Duration <= 0f) return "Timed chapters need a positive duration.";
            bool fight = chapter.Behaviour == PrologueStep.DefendCamp || chapter.Behaviour == PrologueStep.HoldPortal;
            if (fight && (chapter.InitialEnemies < 1 || chapter.Enemy(1, 0f) == null))
                return $"The {chapter.Behaviour} chapter needs enemies for its first wave.";
            foreach (PrologueWaveEnemy enemy in chapter.Enemies)
                if (enemy != null && enemy.prefab != null && enemy.prefab.GetComponent<Damageable>() == null)
                    return $"Enemy {enemy.prefab.name} must be damageable.";
        }
        if (startedEvent == null || shelterEvent == null || fireEvent == null || restedEvent == null ||
            campDefendedEvent == null || portalReachedEvent == null || portalActivatedEvent == null || finishedEvent == null)
            return "A prologue story event is missing.";
        if (sticks == null || stones == null)
            return "The prologue needs fuel items.";
        if (campfire == null || campfire.Prefab == null || campfire.Prefab.GetComponent<SaveableEntity>() == null)
            return "The prologue needs a saveable campfire building.";
        if (waveInterval <= 0f || maximumEnemies < 1 || firstWaveSize < 1 || waveGrowth < 0 || maximumWaveSize < 1 ||
            string.IsNullOrWhiteSpace(homeScene))
            return "Prologue timing, costs, enemy limit and home scene must be valid.";
        return null;
    }
}
