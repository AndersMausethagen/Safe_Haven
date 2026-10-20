using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// What a monster leaves its killer: items rolled from its loot table, given straight to the killer's bag, resources
/// (hides, Cursed Essence) given to their resource bars, and recipes it teaches (a boss unlocking iron smithing). Only a
/// killer with an inventory (the player) collects anything. Whatever doesn't fit is reported rather than silently lost.
/// </summary>
[RequireComponent(typeof(Damageable))]
public sealed class LootOnDeath : MonoBehaviour
{
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Recipes the killer learns, for bosses that unlock new crafting.")]
    [SerializeField] private List<RecipeDefinition> unlocksRecipes = new List<RecipeDefinition>();

    private Damageable damageable;

    public ItemDropTable Drops => drops;
    public IReadOnlyList<RecipeDefinition> UnlocksRecipes => unlocksRecipes;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        damageable.Died += OnDied;
    }

    private void OnDisable()
    {
        damageable.Died -= OnDied;
    }

    private void OnValidate()
    {
        drops.Validate();
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (killingBlow.Source == null)
        {
            return;
        }

        var inventory = killingBlow.Source.GetComponentInParent<ItemInventory>();
        if (inventory != null)
        {
            ItemGrantResult result = inventory.Give(drops.Roll());
            if (result.HasOverflow)
            {
                GameMessages.Post("Bag full, left behind: " + AmountText.Describe(result.Overflow), MessageKind.Warning);
            }
        }

        if (killingBlow.Source.GetComponentInParent<ResourceInventory>() != null)
        {
            List<ResourceAmount> overflow = LootGrant.GiveResources(killingBlow.Source.gameObject, drops.RollResources());
            if (overflow.Count > 0)
            {
                GameMessages.Post("Can't carry more: " + AmountText.Describe(overflow) + " left behind.", MessageKind.Warning);
            }
        }

        var crafting = killingBlow.Source.GetComponentInParent<PlayerCrafting>();
        if (crafting != null && unlocksRecipes.Count > 0)
        {
            crafting.Unlock(unlocksRecipes);
        }
    }
}
