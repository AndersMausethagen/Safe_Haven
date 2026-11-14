using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Some of one kind of enemy in a wave.</summary>
[Serializable]
public sealed class BreachGroup
{
    [Tooltip("The monster prefab that attacks (it needs MonsterAI).")]
    public GameObject enemy;
    [Min(1)] public int count = 1;
    [Tooltip("They come only once this many breaches have been held off (the last breach repeats, harder each time). 0: always.")]
    [Min(0)] public int fromBreachesWon;
}

/// <summary>Who comes at once.</summary>
[Serializable]
public sealed class BreachWave
{
    public List<BreachGroup> groups = new List<BreachGroup>();
}

/// <summary>
/// One ward breach encounter (GDD §6.6–6.9): when it becomes possible, what the player is told, which enemies come, how
/// strong they are and what winning gives. The first breach follows the decisions of the first-90-minutes pass: it is
/// offered after the first successful extraction, the player starts it at the portal, a few weak Forest-Bitten Wolves
/// come along one marked path and attack only the player, and winning gives 2 Life Orbs. Later ones threaten the home
/// itself, in waves down several lanes.
/// <para>
/// Played by a WardBreachDirector in Safe Haven, in the order WardBreachSettings lists them.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NewWardBreach", menuName = "Safe Haven/Settlement/Ward Breach")]
public sealed class WardBreachDefinition : ScriptableObject
{
    [Header("Text")]
    [SerializeField] private string displayName = "The Weakening Ward";
    [Tooltip("Shown when the breach becomes possible (GDD: a visible warning before any attack).")]
    [SerializeField, TextArea] private string warningText = "The portal's light flickers. Something is testing the ward along the old path. Prepare, then speak to the portal.";
    [Tooltip("Shown at the portal while the breach waits for the player.")]
    [SerializeField, TextArea] private string pendingText = "The ward is weakening. When you are ready, I will open it and let them come to you.";
    [Tooltip("The detail line of the portal's \"Begin the defense\" choice.")]
    [SerializeField] private string startHint = "Wolves will come along the marked path";
    [SerializeField, TextArea] private string wonText = "The last wolf falls. The portal's light steadies, and the ward closes again.";
    [SerializeField, TextArea] private string failedText = "The wolves slink back into the dark. The ward still flickers; try again when you are ready.";

    [Header("When")]
    [Tooltip("Successful extractions needed before the breach is offered.")]
    [SerializeField, Min(0)] private int availableAfterExtractions = 1;

    [Header("Enemies")]
    [Tooltip("Off: the first breach's gentle rules. Enemies hunt only the player, buildings can't be harmed and falling ends it. On: they go for the home and the ward, and the ward can fall.")]
    [SerializeField] private bool threatensHome;
    [Tooltip("How many ways in. 0 keeps to the scene's marked path.")]
    [SerializeField, Min(0)] private int laneCount;
    [Tooltip("Who comes, wave by wave. Empty: Enemy Count of the Enemy Prefab, all at once.")]
    [SerializeField] private List<BreachWave> waves = new List<BreachWave>();
    [Tooltip("The monster prefab that attacks (it needs MonsterAI).")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField, Min(1)] private int enemyCount = 3;
    [Tooltip("Seconds between enemies arriving.")]
    [SerializeField, Min(0f)] private float secondsBetweenEnemies = 2.5f;
    [Tooltip("Multiplier on the enemies' health; below 1 makes them weaker than in raids.")]
    [SerializeField, Min(0.1f)] private float enemyHealthMultiplier = 0.6f;
    [Tooltip("Multiplier on the enemies' damage.")]
    [SerializeField, Min(0f)] private float enemyPowerMultiplier = 0.7f;

    [Header("Reward")]
    [SerializeField] private List<ResourceAmount> reward = new List<ResourceAmount>();
    [Tooltip("Raised when the breach is won, for quests.")]
    [SerializeField, RaisedByGame] private GameEventDefinition wonEvent;

    public string DisplayName => displayName;
    public string WarningText => warningText;
    public string PendingText => pendingText;
    public string StartHint => startHint;
    public string WonText => wonText;
    public string FailedText => failedText;
    public int AvailableAfterExtractions => availableAfterExtractions;
    public bool ThreatensHome => threatensHome;
    public int LaneCount => laneCount;
    public int WaveCount => Mathf.Max(1, waves.Count);
    public bool HasEnemies => waves.Count > 0 || enemyPrefab != null;
    public GameObject EnemyPrefab => enemyPrefab;
    public int EnemyCount => enemyCount;
    public float SecondsBetweenEnemies => secondsBetweenEnemies;
    public float EnemyHealthMultiplier => enemyHealthMultiplier;
    public float EnemyPowerMultiplier => enemyPowerMultiplier;
    public IReadOnlyList<ResourceAmount> Reward => reward;
    public GameEventDefinition WonEvent => wonEvent;

    /// <summary>Everyone who comes in a wave (from 0), in the order they arrive, with every group counted.</summary>
    public List<GameObject> Roster(int wave) => Roster(wave, int.MaxValue);

    /// <summary>Everyone who comes in a wave (from 0) once <paramref name="breachesWon"/> breaches have been held off.</summary>
    public List<GameObject> Roster(int wave, int breachesWon)
    {
        var roster = new List<GameObject>();
        if (waves.Count == 0)
        {
            for (int i = 0; i < enemyCount && enemyPrefab != null; i++)
            {
                roster.Add(enemyPrefab);
            }

            return roster;
        }

        foreach (BreachGroup group in waves[Mathf.Clamp(wave, 0, waves.Count - 1)].groups)
        {
            for (int i = 0; i < group.count && group.enemy != null && breachesWon >= group.fromBreachesWon; i++)
            {
                roster.Add(group.enemy);
            }
        }

        return roster;
    }
}
