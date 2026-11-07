using UnityEngine;

/// <summary>
/// A place or thing that quests and stories point at by name: the Old Tower, the Lake Stone, the Portal. Mark the scene
/// object with it (a Quest Zone's or Inspect Point's Target, a Talkable's Quest Target), and any quest can ask the player
/// to reach it, use it or talk to it, with no Quest Event made for each. People need none: their Resident Definition is
/// who they are.
/// </summary>
[CreateAssetMenu(fileName = "NewQuestTarget", menuName = "Safe Haven/Quests/Quest Target")]
public sealed class QuestTargetDefinition : ScriptableObject, IQuestTarget
{
    [Tooltip("Stable unique identifier, used in saves (what the player has done to it). Empty: the asset name.")]
    [SerializeField] private string id;
    [Tooltip("How quests name it, e.g. \"the Old Tower\". Empty: the asset name.")]
    [SerializeField] private string displayName;
    [Tooltip("How \"use it\" reads in an objective: Use, Inspect, Light, Touch, Open.")]
    [SerializeField] private string useVerb = "Use";
    [SerializeField, TextArea] private string notes;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id.Trim();
    public string QuestId => Id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string UseVerb => string.IsNullOrWhiteSpace(useVerb) ? "Use" : useVerb.Trim();

    /// <summary>Sets it up from code (tools and tests).</summary>
    public void Configure(string targetId, string shownName, string verb = "Use")
    {
        id = targetId;
        displayName = shownName;
        useVerb = verb;
    }
}
