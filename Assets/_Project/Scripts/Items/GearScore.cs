using UnityEngine;

/// <summary>
/// One number for how well equipped someone is: the sum of what they wear, as in V Rising, so an empty slot simply adds nothing and
/// can never beat a filled one. It reads what gear is, and changes nothing by itself: gear already makes the player stronger through
/// its stats, and raid tiers are tuned to the score (see Docs/GearScoreAndRaidTiers.md). Pure rules on definitions and stacks.
/// </summary>
public static class GearScore
{
    private static readonly GearSlot[] Counted =
    {
        GearSlot.MainHand, GearSlot.OffHand, GearSlot.Head, GearSlot.Neck, GearSlot.Chest,
        GearSlot.Hands, GearSlot.Waist, GearSlot.Feet, GearSlot.Finger1, GearSlot.Finger2,
    };

    /// <summary>What a piece is worth as it comes, or its own Gear Score Override.</summary>
    public static int Of(ItemDefinition item) => item != null ? Mathf.RoundToInt(BaseOf(item, GearScoreSettings.Current)) : 0;

    /// <summary>True for what a gear score counts: weapons and worn armor and jewellery.</summary>
    public static bool Counts(ItemDefinition item) => item != null && BaseOf(item, GearScoreSettings.Current) > 0f;

    /// <summary>What a piece in hand is worth with its empowering and enchantment. Broken gear gives no stats, so it counts for nothing.</summary>
    public static int Of(ItemStack stack) => Of(stack, enchantmentCounts: true);

    // The enchantment scores where the stats take it (GearPower.BonusesOf): only where it may be carved (Fits), and, worn,
    // only on the first whole piece carrying it (GearPower.CountsEnchantment), so seven copies of one cheap enchantment
    // can't lift the score towards a tier the stats don't reach.
    private static int Of(ItemStack stack, bool enchantmentCounts)
    {
        if (stack.IsEmpty || stack.IsBroken)
        {
            return 0;
        }

        GearScoreSettings settings = GearScoreSettings.Current;
        float score = BaseOf(stack.Item, settings);
        // A better make counts as the part of an empowering level it is worth (LevelingSettings › Quality Strength).
        float empowered = stack.Empower + LevelingSettings.Current.QualityStrength(stack.Quality);
        bool enchanted = enchantmentCounts && stack.Enchantment != null && stack.Enchantment.Fits(stack.Item);
        float extra = empowered * settings.PerEmpowerLevel + (enchanted ? settings.PerEnchantment : 0f);
        return Mathf.RoundToInt(score * (1f + extra));
    }

    /// <summary>The score of everything worn.</summary>
    public static int Of(GearSet gear)
    {
        int total = 0;
        if (gear != null)
        {
            foreach (GearSlot slot in Counted)
            {
                total += Of(gear.Get(slot), GearPower.CountsEnchantment(gear.Slots, (int)slot));
            }
        }

        return total;
    }

    /// <summary>The score of a player's worn gear; 0 for someone with none.</summary>
    public static int Of(GameObject player) =>
        player != null && player.TryGetComponent(out PlayerEquipment equipment) ? Of(equipment.Gear) : 0;

    private static float BaseOf(ItemDefinition item, GearScoreSettings settings)
    {
        if (item.GearScoreOverride > 0)
        {
            return item.GearScoreOverride;
        }

        float share = settings.ShareOf(item);
        return share <= 0f ? 0f : Mathf.Max(1f, settings.FullKit(item.Tier) * share * settings.RarityFactor(item.Rarity));
    }
}
