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

    /// <summary>Raised with whoever gathered a node (its yield granted) and the node, for experience.</summary>
    public static event System.Action<GameObject, ResourceSource> Harvested;

    /// <summary>The node's definition when this is a gathering node: its tool and tier say which skill it trains.</summary>
    public GatheringNodeDefinition Node => TryGetComponent(out ResourceNode node) ? node.Definition : null;

    private int tierOverride;
    private ItemDefinition mainOverride;

    /// <summary>The tier what is gathered here trains at: its node's, or the tree's it came from (TakeOnTree).</summary>
    public int Tier => tierOverride > 0 ? tierOverride : Node != null ? Node.Tier : 1;

    /// <summary>
    /// A felled tree's source becomes the tree it was (FelledTree): its tier for the skill it trains, its own wood for the bonus
    /// logs. The felled tree's own node is tier I with Oak Wood as its main yield, so an Elderwood's landing, limbing and
    /// sections trained Woodcutting as a tier I tree and its lucky swings gave Oak Wood.
    /// </summary>
    public void TakeOnTree(int tier, ItemDefinition wood)
    {
        tierOverride = tier;
        mainOverride = wood;
    }

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
        AfterHarvest(recipient);
        return true;
    }

    // The chance of one extra of the main yield (StatType.GatherYield, plus the tool's own from the woodcutting or
    // mining skill), then the Harvested news.
    private void AfterHarvest(GameObject recipient)
    {
        Combatant gatherer = recipient != null ? recipient.GetComponentInParent<Combatant>() : null;
        if (gatherer != null && Random.value < gatherer.Stats.GetChance(StatType.GatherYield) + ToolYield(gatherer.Stats, Node))
        {
            GrantBonus(recipient, 1);
        }

        Harvested?.Invoke(recipient, this);
    }

    private static float ToolYield(StatSheet stats, GatheringNodeDefinition node)
    {
        switch (node != null ? node.GatherTool : ToolType.None)
        {
            case ToolType.Axe: return stats.Get(StatType.ChopYield);
            case ToolType.Pickaxe: return stats.Get(StatType.MineYield);
            case ToolType.Sickle: return stats.Get(StatType.ForageYield);
            case ToolType.None: return node != null ? stats.Get(StatType.ForageYield) : 0f;
            default: return 0f;
        }
    }

    /// <summary>Gives these items instead of a roll of the drop table (a felled tree's logs, which depend on the axe), with
    /// the same overflow handling. Returns false when there is nobody to give them to.</summary>
    public bool GrantItems(GameObject recipient, List<ItemAmount> items)
    {
        ItemInventory bag = recipient != null ? recipient.GetComponentInParent<ItemInventory>() : null;
        if (recipient == null || items == null || items.Count == 0)
        {
            return false;
        }

        var leftBehind = new List<string>();
        Give(bag, recipient, items, new List<ResourceAmount>(), leftBehind);
        ReportLeftBehind(leftBehind);
        AfterHarvest(recipient);
        return true;
    }

    /// <summary>Rolls another table on top of the yield (a mined node's burst), with the same overflow handling. It isn't another
    /// harvest: no experience, no talent's extra. Returns false when there is nothing to give or nobody to give it to.</summary>
    public bool GrantExtra(GameObject recipient, ItemDropTable extra)
    {
        if (recipient == null || extra == null || extra.Entries.Count == 0)
        {
            return false;
        }

        var leftBehind = new List<string>();
        Give(recipient.GetComponentInParent<ItemInventory>(), recipient, extra.Roll(), extra.RollResources(), leftBehind);
        ReportLeftBehind(leftBehind);
        return true;
    }

    /// <summary>
    /// Gives extra units of the main yield (the first drop entry): the bonus for well-timed gathering swings. Returns
    /// false when there is no main yield or nobody to give it to.
    /// </summary>
    public bool GrantBonus(GameObject recipient, int amount)
    {
        Object main = mainOverride != null ? mainOverride : drops.Entries.Count > 0 && drops.Entries[0] != null ? drops.Entries[0].Drop : null;
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
                    LootGrant.LeftBehind(recipient, result.Overflow);
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
            LootGrant.LeftBehind(recipient, overflow);
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
