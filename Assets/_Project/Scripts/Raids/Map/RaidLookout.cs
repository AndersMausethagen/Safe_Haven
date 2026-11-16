using UnityEngine;

/// <summary>
/// A lookout on one of a generated map's highest clearings: looking out from it lifts the fog of war over the land around (the
/// minimap and world map show it), once. Made by RaidMapGenerator with the rules' Lookout Reveal; the clearing it stands in is found by
/// height, so climbing to high ground pays.
/// </summary>
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid's lookout")]
public sealed class RaidLookout : MonoBehaviour, IInteractionName, IInteractionVerb
{
    [Tooltip("Metres of the map revealed around the lookout.")]
    [SerializeField, Min(1f)] private float reveal = 70f;

    private Interactable interactable;

    public string InteractionName => "Lookout";
    public string Verb => "Look out over the land";

    public void Configure(float radius) => reveal = Mathf.Max(1f, radius);

    private void Awake() => interactable = GetComponent<Interactable>();

    private void OnEnable() => interactable.Completed.AddListener(OnLooked);

    private void OnDisable() => interactable.Completed.RemoveListener(OnLooked);

    private void OnLooked()
    {
        if (ExplorationMap.Current != null)
        {
            ExplorationMap.Current.RevealAround(transform.position, reveal);
        }

        GameMessages.Post("From up here the land lies open: the map shows what you can see.", MessageKind.Info);
    }
}
