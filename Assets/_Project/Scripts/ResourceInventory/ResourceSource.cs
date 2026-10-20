using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something that yields materials and essences: a tree, an ore deposit, a destructible. It holds a drop table, rolls it
/// and hands the drops over: items to the recipient's bag (within its slots and carry weight), essences to the essence
/// bars. What doesn't fit is left behind and reported. Items raise ItemObtained (through ItemInventory.Give) and essences
/// ResourceGathered, which is what gather quests count.
/// On an object that also has an Interactable, completing the interaction grants the drops to whoever did the work.
/// Anything else (a destructible's death handler) calls GrantTo itself.
/// </summary>
public sealed class ResourceSource : MonoBehaviour
{
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Grant the drops when this object's Interactable completes. Turn off to call GrantTo yourself.")]
    [SerializeField] private bool grantOnInteractionComplete = true;

    private Interactable interactable;

    public ItemDropTable Drops => drops;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        if (interactable != null && grantOnInteractionComplete)
        {
            interactable.Completed.AddListener(OnInteractionCompleted);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnInteractionCompleted);
        }
    }

    private void OnValidate()
    {
        drops.Validate();
    }

    /// <summary>
    /// Rolls the drops and gives them to the ItemInventory and ResourceInventory on the recipient or its parents.
    /// Returns false when the recipient has neither.
    /// </summary>
    public bool GrantTo(GameObject recipient)
    {
        ItemInventory bag = recipient != null ? recipient.GetComponentInParent<ItemInventory>() : null;
        ResourceInventory essences = recipient != null ? recipient.GetComponentInParent<ResourceInventory>() : null;
        if (bag == null && essences == null)
        {
            Debug.LogWarning($"{name} has nobody with an inventory to grant its yield to.", this);
            return false;
        }

        var leftBehind = new List<string>();
        Give(bag, recipient, drops.Roll(), drops.RollResources(), leftBehind);
        ReportLeftBehind(leftBehind);
        return true;
    }

    /// <summary>
    /// Gives extra units of the main yield (the first drop entry): the bonus for well-timed gathering swings. Returns
    /// false when there is no main yield or nobody to give it to.
    /// </summary>
    public bool GrantBonus(GameObject recipient, int amount)
    {
        Object main = drops.Entries.Count > 0 && drops.Entries[0] != null ? drops.Entries[0].Drop : null;
        ItemInventory bag = recipient != null ? recipient.GetComponentInParent<ItemInventory>() : null;
        if (amount <= 0 || main == null || recipient == null)
        {
            return false;
        }

        var items = new List<ItemAmount>();
        var resources = new List<ResourceAmount>();
        if (main is ItemDefinition item)
        {
            items.Add(new ItemAmount(item, amount));
        }
        else if (main is ResourceDefinition resource)
        {
            resources.Add(new ResourceAmount(resource, amount));
        }

        var leftBehind = new List<string>();
        Give(bag, recipient, items, resources, leftBehind);
        ReportLeftBehind(leftBehind);
        return true;
    }

    private static void Give(ItemInventory bag, GameObject recipient, List<ItemAmount> items, List<ResourceAmount> resources, List<string> leftBehind)
    {
        if (items.Count > 0)
        {
            if (bag != null)
            {
                ItemGrantResult result = bag.Give(items);
                if (result.HasOverflow)
                {
                    leftBehind.Add(AmountText.Describe(result.Overflow));
                }
            }
            else
            {
                leftBehind.Add(AmountText.Describe(items));
            }
        }

        List<ResourceAmount> overflow = LootGrant.GiveResources(recipient, resources);
        if (overflow.Count > 0)
        {
            leftBehind.Add(AmountText.Describe(overflow));
        }
    }

    private static void ReportLeftBehind(List<string> leftBehind)
    {
        if (leftBehind.Count > 0)
        {
            GameMessages.Post($"Can't carry more: {string.Join(", ", leftBehind)} left behind.", MessageKind.Warning);
        }
    }

    private void OnInteractionCompleted()
    {
        GrantTo(interactable.Interactor);
    }
}
