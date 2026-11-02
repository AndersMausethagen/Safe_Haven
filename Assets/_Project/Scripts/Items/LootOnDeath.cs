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
    [Tooltip("Chance it leaves a relic (RelicSettings), in a raid only: for what stands at the end of the map.")]
    [SerializeField, Range(0f, 1f)] private float relicChance;

    private Damageable damageable;

    public ItemDropTable Drops => drops;

    /// <summary>
    /// This creature leaves no Cursed Essence: what is killed outside a raid (home's night prowlers) doesn't pay in what only
    /// raids should give (GDD §13.5).
    /// </summary>
    public bool WithholdsEssence { get; set; }
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
            List<ItemAmount> items = drops.Roll();
            if (GameSession.ActiveRaid != null)
            {
                items.RemoveAll(drop => !GameSession.ActiveRaid.Allows(drop.Item));
            }

            ItemGrantResult result = inventory.Give(items);
            if (result.HasOverflow)
            {
                GameMessages.Post("Bag full, left behind: " + AmountText.Describe(result.Overflow), MessageKind.Warning);
            }
        }

        if (killingBlow.Source.GetComponentInParent<ResourceInventory>() != null)
        {
            List<ResourceAmount> rolled = drops.RollResources();
            if (WithholdsEssence)
            {
                rolled.RemoveAll(entry => entry.Resource == SettlementSettings.Current.CursedEssence);
            }

            List<ResourceAmount> overflow = LootGrant.GiveResources(killingBlow.Source.gameObject, Reaped(rolled, killingBlow.Source));
            if (overflow.Count > 0)
            {
                GameMessages.Post("Can't carry more: " + AmountText.Describe(overflow) + " left behind.", MessageKind.Warning);
            }
        }

        RaidDirector raid = RaidDirector.Current;
        if (raid != null && relicChance > 0f && inventory != null && Random.value < relicChance)
        {
            RelicSettings.Grant(inventory.gameObject, new System.Random(Random.Range(0, int.MaxValue)), raid.Session.Tier);
        }

        var crafting = killingBlow.Source.GetComponentInParent<PlayerCrafting>();
        if (crafting != null && unlocksRecipes.Count > 0)
        {
            crafting.Unlock(unlocksRecipes);
        }
    }

    private static IReadOnlyList<ResourceAmount> Reaped(IReadOnlyList<ResourceAmount> rolled, Combatant killer)
    {
        // The night's share and Essence Find on gear multiply: both are "more of what fell".
        float find = (1f + killer.Stats.Get(StatType.EssenceFind)) * NightSettings.Current.EssenceAt(NightSettings.Darkness) - 1f;
        ResourceDefinition cursed = SettlementSettings.Current.CursedEssence;
        if (find <= 0f || cursed == null)
        {
            return rolled;
        }

        var reaped = new List<ResourceAmount>(rolled.Count);
        foreach (ResourceAmount entry in rolled)
        {
            reaped.Add(entry.Resource == cursed ? new ResourceAmount(cursed, GearPower.Reaped(entry.Amount, find, Random.value)) : entry);
        }

        return reaped;
    }
}
