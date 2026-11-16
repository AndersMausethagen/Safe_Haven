using UnityEngine;

/// <summary>
/// What is left of a boss, standing near the portal once it has been beaten (GDD §15.6). Raised by ResidentSpawner from
/// SettlementState.DefeatedBosses, so it needs no saving of its own. Press E to read what it says of it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "looking at a trophy changes nothing")]
public sealed class BossTrophy : MonoBehaviour, IInteractionName, IInteractionVerb
{
    [Tooltip("Written on the screen when the player looks at it.")]
    [SerializeField, TextArea] private string text;

    private Interactable interactable;
    private BossDefinition boss;

    public string InteractionName => boss != null ? $"What is left of {boss.DisplayName}" : "A trophy";
    public string Verb => "Look";

    public void Bind(BossDefinition beaten)
    {
        boss = beaten;
        name = InteractionName;
    }

    private void Awake() => interactable = GetComponent<Interactable>();

    private void OnEnable() => interactable.Completed.AddListener(OnLooked);

    private void OnDisable() => interactable.Completed.RemoveListener(OnLooked);

    private void OnLooked()
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            GameMessages.Post(text);
        }
    }
}
