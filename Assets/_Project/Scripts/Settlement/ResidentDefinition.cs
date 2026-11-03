using UnityEngine;

/// <summary>
/// A person who can live in Safe Haven (GDD §15): the stranded traveler found in a raid, later more. Holds who they are,
/// what they say, what they look like and which jobs they can take. Whether they have been rescued and which job they
/// hold is runtime state in SettlementState.
/// </summary>
[CreateAssetMenu(fileName = "NewResident", menuName = "Safe Haven/Settlement/Resident")]
public sealed class ResidentDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("Who they are, in a sentence or two. Shown when talking to them.")]
    [SerializeField, TextArea(2, 4)] private string description;

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

    [Header("Jobs")]
    [Tooltip("Jobs they can be given at home. The first is suggested.")]
    [SerializeField] private JobDefinition[] jobs = new JobDefinition[0];

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string RescueLine => rescueLine;
    public string ArrivalLine => arrivalLine;
    public string WelcomeLine => welcomeLine;
    public string FallenLine => fallenLine;
    public FightLines FightLines => fightLines;
    public GameObject Model => model;
    public JobDefinition[] Jobs => jobs;
}
