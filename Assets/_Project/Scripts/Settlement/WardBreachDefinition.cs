using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One ward breach encounter (GDD §6.6–6.9): when it becomes possible, what the player is told, which enemies come, how
/// strong they are and what winning gives. The first breach follows the decisions of the first-90-minutes pass: it is
/// offered after the first successful extraction, the player starts it at the Heart Tree, a few weak Forest-Bitten Wolves
/// come along one marked path and attack only the player, and winning gives 2 Life Orbs.
/// <para>
/// Played by a WardBreachDirector in Safe Haven. Later breaches can be more assets with other enemies and triggers.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NewWardBreach", menuName = "Safe Haven/Settlement/Ward Breach")]
public sealed class WardBreachDefinition : ScriptableObject
{
    [Header("Text")]
    [SerializeField] private string displayName = "The Weakening Ward";
    [Tooltip("Shown when the breach becomes possible (GDD: a visible warning before any attack).")]
    [SerializeField, TextArea] private string warningText = "The Heart Tree's light flickers. Something is testing the ward along the old path. Prepare, then speak to the tree.";
    [Tooltip("Shown at the Heart Tree while the breach waits for the player.")]
    [SerializeField, TextArea] private string pendingText = "The ward is weakening. When you are ready, I will open it and let them come to you.";
    [Tooltip("The detail line of the Heart Tree's \"Begin the defense\" choice.")]
    [SerializeField] private string startHint = "Wolves will come along the marked path";
    [SerializeField, TextArea] private string wonText = "The last wolf falls. The Heart Tree's light steadies, and the ward closes again.";
    [SerializeField, TextArea] private string failedText = "The wolves slink back into the dark. The ward still flickers; try again when you are ready.";

    [Header("When")]
    [Tooltip("Successful extractions needed before the breach is offered.")]
    [SerializeField, Min(0)] private int availableAfterExtractions = 1;

    [Header("Enemies")]
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
    public GameObject EnemyPrefab => enemyPrefab;
    public int EnemyCount => enemyCount;
    public float SecondsBetweenEnemies => secondsBetweenEnemies;
    public float EnemyHealthMultiplier => enemyHealthMultiplier;
    public float EnemyPowerMultiplier => enemyPowerMultiplier;
    public IReadOnlyList<ResourceAmount> Reward => reward;
    public GameEventDefinition WonEvent => wonEvent;
}
