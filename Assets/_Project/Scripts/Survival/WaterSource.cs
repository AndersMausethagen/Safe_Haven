using UnityEngine;

/// <summary>
/// A stream, well or spring (GDD §21.2 "fill water"). Using it (its Interactable) lets the player drink, restoring
/// thirst, and fills water containers: it tops the bag up to a number of the fill item (Waterskins) so the player can
/// carry water into a raid or cook with it. Water sources never run dry.
/// <para>
/// Where it belongs: on a stream object in Safe Haven, next to an Interactable that doesn't deactivate on completion.
/// Tune the amounts here on the object; the fill item is an ordinary consumable ItemDefinition.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class WaterSource : MonoBehaviour, IInteractionName
{
    public string InteractionName => string.IsNullOrWhiteSpace(sourceName) ? "Stream" : sourceName;

    [Tooltip("What the prompt calls it. Empty: Stream.")]
    [SerializeField] private string sourceName;
    [Tooltip("Thirst restored by drinking here.")]
    [SerializeField, Min(0f)] private float drinkAmount = 100f;
    [Tooltip("The water container handed out, such as a Waterskin. Empty means the source is only for drinking.")]
    [SerializeField] private ItemDefinition fillItem;
    [Tooltip("The bag is topped up to this many of the fill item.")]
    [SerializeField, Min(0)] private int fillUpTo = 3;

    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    /// <summary>Sets it up from code (water made while playing, such as a generated map's ponds).</summary>
    public void Configure(string name, float drink, ItemDefinition item, int upTo)
    {
        sourceName = name;
        drinkAmount = Mathf.Max(0f, drink);
        fillItem = item;
        fillUpTo = Mathf.Max(0, upTo);
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user == null)
        {
            return;
        }

        PlayerNeeds needs = user.GetComponentInParent<PlayerNeeds>();
        bool drank = false;
        if (needs != null && drinkAmount > 0f && !needs.Thirst.IsFull)
        {
            drank = needs.TryConsume("Fresh water", 0f, drinkAmount);
        }

        int filled = 0;
        ItemInventory bag = user.GetComponentInParent<ItemInventory>();
        if (bag != null && fillItem != null)
        {
            int missing = fillUpTo - bag.Container.Count(fillItem);
            if (missing > 0)
            {
                filled = bag.Give(fillItem, missing);
            }
        }

        if (!drank && filled == 0)
        {
            GameMessages.Post("You've had your fill, and your water containers are full.");
        }
    }
}
