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
    Finished
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

    public QuestDefinition Quest => quest;
    public PrologueStep Behaviour => behaviour;
    public string ArrivalText => arrivalText;
    public string StatusText => statusText;
    public float Duration => duration;
    public int InitialEnemies => initialEnemies;
}

/// <summary>Authored prologue content. Progress belongs to the saved quest log, not this shared asset.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Story/Prologue", fileName = "ForestPrologue")]
public sealed class PrologueDefinition : ScriptableObject
{
    [Tooltip("Ordered story phases. Each phase waits for its quest to complete. Quests define objectives and prerequisites; Behaviour selects the scene reaction. Extra phases can reuse a behaviour.")]
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
    [SerializeField] private GameObject wolfPrefab;
    [SerializeField] private SoundDefinition rumble;
    [SerializeField] private string homeScene = "SafeHaven";
    [Header("Narrative")]
    [SerializeField, TextArea(2, 5)] private string survivedText;
    [SerializeField, TextArea(2, 5)] private string overwhelmedText;
    [SerializeField, TextArea(1, 3)] private string endingStatus;
    [SerializeField, TextArea(2, 5)] private string homecomingText;
    [SerializeField, TextArea(1, 3)] private string recoveryText;
    [SerializeField, Min(0f)] private float endingSeconds = 3f;
    [SerializeField, Min(0f)] private float recoverySeconds = 2f;

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
    public ItemDefinition[] StartingArmor => startingArmor;
    public BuildingDefinition Campfire => campfire;
    public float BuildRadius => buildRadius;
    public string WaitingForLeaderText => waitingForLeaderText;
    public ResidentDefinition Leader => leader;
    public float DialogueCameraDistance => dialogueCameraDistance;
    public float WaveInterval => waveInterval;
    public int MaximumEnemies => maximumEnemies;
    public int WaveSize(int wave) => Mathf.Min(firstWaveSize + Mathf.Max(0, wave - 1) * waveGrowth, maximumWaveSize);
    public GameObject WolfPrefab => wolfPrefab;
    public SoundDefinition Rumble => rumble;
    public string HomeScene => homeScene;
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
            if (log.StatusOf(chapter.Quest) != QuestStatus.Completed)
                return chapter;
        return null;
    }

    public string FindProblem()
    {
        if (chapters == null || chapters.Length == 0) return "The prologue needs story chapters.";
        var seen = new System.Collections.Generic.HashSet<QuestDefinition>();
        foreach (PrologueChapter chapter in chapters)
        {
            if (chapter == null || chapter.Quest == null) return "A prologue chapter or quest is missing.";
            if (!seen.Add(chapter.Quest)) return "Each prologue chapter needs a different quest.";
            if (chapter.Behaviour == PrologueStep.Finished) return "Finished is reserved for the end of the sequence.";
            if ((chapter.Behaviour == PrologueStep.CatchBreath || chapter.Behaviour == PrologueStep.HoldPortal) &&
                chapter.Duration <= 0f) return "Timed chapters need a positive duration.";
            if (chapter.Behaviour == PrologueStep.DefendCamp && chapter.InitialEnemies < 1)
                return "A camp defence needs at least one enemy.";
        }
        if (startedEvent == null || shelterEvent == null || fireEvent == null || restedEvent == null ||
            campDefendedEvent == null || portalReachedEvent == null || portalActivatedEvent == null || finishedEvent == null)
            return "A prologue story event is missing.";
        if (sticks == null || stones == null || wolfPrefab == null || wolfPrefab.GetComponent<Damageable>() == null)
            return "The prologue needs fuel items and a damageable wolf prefab.";
        if (campfire == null || campfire.Prefab == null || campfire.Prefab.GetComponent<SaveableEntity>() == null)
            return "The prologue needs a saveable campfire building.";
        if (waveInterval <= 0f || maximumEnemies < 1 || firstWaveSize < 1 || waveGrowth < 0 || maximumWaveSize < 1 ||
            string.IsNullOrWhiteSpace(homeScene))
            return "Prologue timing, costs, enemy limit and home scene must be valid.";
        return null;
    }
}
