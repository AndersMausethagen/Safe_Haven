using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Relics (GDD §13.6): gear no one at home can make, found only in raids. A relic is an ordinary piece of gear carrying a
/// power from this list: an EnchantmentDefinition of the Relic school, with RelicEffects that change how it plays. So a
/// relic keeps its name, tooltip, save and empowering from the enchantment system, and any piece of gear can turn up with
/// any power that fits it, which is what keeps finding them worthwhile. The altar can neither make nor replace one.
/// </summary>
[CreateAssetMenu(fileName = "RelicSettings", menuName = "Safe Haven/Settings/Relic Settings")]
public sealed class RelicSettings : GameSettings<RelicSettings>
{
    [Tooltip("Every power a relic can have.")]
    [SerializeField] private List<EnchantmentDefinition> powers = new List<EnchantmentDefinition>();
    [Tooltip("The gear relics are found as.")]
    [SerializeField] private List<ItemDefinition> gear = new List<ItemDefinition>();
    [Tooltip("Gear this many tiers above the raid's can still turn up.")]
    [SerializeField, Min(0)] private int tiersAboveRaid = 1;
    [Tooltip("Chance a raid's supply cache holds a relic besides its loot, before the raid's loot multiplier.")]
    [SerializeField, Range(0f, 1f)] private float cacheChance = 0.06f;
    [SerializeField] private Color glow = new Color(1f, 0.74f, 0.32f);

    public IReadOnlyList<EnchantmentDefinition> Powers => powers;
    public IReadOnlyList<ItemDefinition> Gear => gear;
    public float CacheChance => cacheChance;
    public Color Glow => glow;

    public EnchantmentDefinition FindPower(string id)
    {
        foreach (EnchantmentDefinition power in powers)
        {
            if (power != null && power.Id == id)
            {
                return power;
            }
        }

        return null;
    }

    /// <summary>A new relic for a raid of this tier, or an empty stack when the lists are empty.</summary>
    public ItemStack Roll(System.Random random, int raidTier)
    {
        var fitting = new List<(ItemDefinition item, EnchantmentDefinition power)>();
        foreach (ItemDefinition item in gear)
        {
            if (item == null || item.Tier > raidTier + tiersAboveRaid)
            {
                continue;
            }

            foreach (EnchantmentDefinition power in powers)
            {
                if (power != null && power.Fits(item))
                {
                    fitting.Add((item, power));
                }
            }
        }

        if (fitting.Count == 0)
        {
            return ItemStack.Empty;
        }

        (ItemDefinition found, EnchantmentDefinition given) = fitting[random.Next(fitting.Count)];
        return new ItemStack(found, 1, 1f, 0, given);
    }

    /// <summary>Puts a new relic in the player's bag, or at their feet when it is full. Returns what was found.</summary>
    public static ItemStack Grant(GameObject player, System.Random random, int raidTier)
    {
        ItemStack relic = Current.Roll(random, raidTier);
        ItemInventory bag = player != null ? player.GetComponentInParent<ItemInventory>() : null;
        if (relic.IsEmpty || bag == null)
        {
            return ItemStack.Empty;
        }

        if (bag.Container.AddStack(relic) == 0)
        {
            DroppedItem.Drop(bag.transform, relic);
            GameMessages.Post("Bag full: it falls at your feet.", MessageKind.Warning);
        }

        GameMessages.Post($"A relic: {relic.DisplayName}!", MessageKind.Gain);
        AudioManager.Play(Rarity.StyleOf(ItemRarity.Legendary).PickupSound, bag.transform.position);
        return relic;
    }

#if UNITY_EDITOR
    /// <summary>Fills in the lists (builders).</summary>
    public void Configure(IEnumerable<EnchantmentDefinition> relicPowers, IEnumerable<ItemDefinition> relicGear)
    {
        powers = new List<EnchantmentDefinition>(relicPowers);
        gear = new List<ItemDefinition>(relicGear);
    }
#endif
}
