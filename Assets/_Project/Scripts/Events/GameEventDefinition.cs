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

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string ObjectiveText => string.IsNullOrWhiteSpace(objectiveText) ? DisplayName : objectiveText;

    /// <summary>Raises this event now. Can be chosen from any event in the Inspector.</summary>
    public void Raise()
    {
        GameEvents.Raise(this);
    }
}
