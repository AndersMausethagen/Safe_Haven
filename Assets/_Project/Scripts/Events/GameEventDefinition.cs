using UnityEngine;

/// <summary>
/// A named moment designers define themselves: "Reached the Old Tower", "Talked to the Smith", "Lit the Beacon". Raise it
/// from a GameEventTrigger (walking into a zone, interacting with something) or from any Inspector event, then use it in a
/// quest objective, to start a quest, or in a GameEventListener to make something happen in the scene.
/// </summary>
[CreateAssetMenu(fileName = "NewQuestEvent", menuName = "Safe Haven/Quests/Quest Event")]
public sealed class GameEventDefinition : ScriptableObject
{
    [Tooltip("Stable unique identifier, used for lookups and saves.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the editor and in objectives that use it. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [Tooltip("How an objective using this event reads, e.g. \"Reach the Old Tower\". Empty uses the display name.")]
    [SerializeField] private string objectiveText;
    [SerializeField, TextArea] private string notes;
    [Tooltip("Co-op: counts for quests only in the player's own Haven and out on raids, not while visiting a friend's Haven, " +
             "because what it marks belongs to a Haven (a boundary stone lit, the portal repaired) and there it is the friend's.")]
    [SerializeField] private bool homeOnly;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public bool HomeOnly => homeOnly;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string ObjectiveText => string.IsNullOrWhiteSpace(objectiveText) ? DisplayName : objectiveText;

    /// <summary>Raises this event now. Can be chosen from any event in the Inspector.</summary>
    public void Raise()
    {
        GameEvents.Raise(this);
    }
}
