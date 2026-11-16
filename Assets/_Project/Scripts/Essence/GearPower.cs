using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a piece of gear gives on top of its authored stats once it has been empowered or enchanted, what the next level
/// costs, and the extra Cursed Essence reaped. Pure rules on definitions and an ItemStack; GearSet applies them, the altar
/// and the tooltip show them.
/// </summary>
public static class GearPower
{
    /// <summary>True for gear that can be empowered. A backpack, say, has nothing to strengthen.</summary>
    public static bool CanEmpower(ItemDefinition item) => EnchantmentDefinition.TargetOf(item) != EnchantTarget.None;

    /// <summary>Everything the stack adds while worn: the item's own stats, its empowering and its enchantment.</summary>
    public static List<StatBonus> BonusesOf(ItemStack stack, EssenceSettings settings) => BonusesOf(stack, settings, true);

    /// <summary>
    /// What the stack adds while worn, with its enchantment's bonuses only when <paramref name="withEnchantment"/>: an enchantment
    /// counts once however many pieces carry it (GearSet.CountsEnchantment, Docs/EnchantingResearch.md #1).
    /// </summary>
    public static List<StatBonus> BonusesOf(ItemStack stack, EssenceSettings settings, bool withEnchantment)
    {
        var bonuses = new List<StatBonus>();
        if (stack.IsEmpty)
        {
            return bonuses;
        }

        bonuses.AddRange(stack.Item.StatBonuses);
        bonuses.AddRange(QualityBonuses(stack.Item, stack.Quality, settings));
        bonuses.AddRange(EmpowerBonuses(stack.Item, stack.Empower, settings));
        // Only where it may be carved (Fits): one carved before the slot rules (an old save's shield with a weapon's enchantment)
        // adds nothing, as the altar would no longer allow it (review 2026-11-16).
        if (withEnchantment && stack.Enchantment != null && stack.Enchantment.Fits(stack.Item))
        {
            bonuses.AddRange(stack.Enchantment.Bonuses);
        }

        return bonuses;
    }

    /// <summary>
    /// True when the enchantment on <paramref name="index"/> of <paramref name="slots"/> counts: it is the first whole piece that
    /// carries it. A second Mossgrown piece adds nothing (Docs/EnchantingResearch.md #1); a broken first piece hands it on.
    /// </summary>
    public static bool CountsEnchantment(ItemContainer slots, int index)
    {
        ItemStack stack = slots.GetSlot(index);
        if (stack.IsEmpty || stack.Enchantment == null)
        {
            return true;
        }

        for (int i = 0; i < index; i++)
        {
            ItemStack other = slots.GetSlot(i);
            if (!other.IsEmpty && !other.IsBroken && other.Enchantment == stack.Enchantment)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The worn piece that already gives this enchantment, if <paramref name="except"/> isn't it: for "counts once".</summary>
    public static ItemStack CarrierOf(ItemContainer slots, EnchantmentDefinition enchantment, int except = -1)
    {
        for (int i = 0; slots != null && enchantment != null && i < slots.SlotCount; i++)
        {
            ItemStack other = slots.GetSlot(i);
            if (i != except && !other.IsEmpty && !other.IsBroken && other.Enchantment == enchantment)
            {
                return other;
            }
        }

        return ItemStack.Empty;
    }

    /// <summary>
    /// What a better make adds to the item: what empowering it part of the way would (LevelingSettings › Fine and
    /// Masterwork Strength, in levels of empowering).
    /// </summary>
    public static List<StatBonus> QualityBonuses(ItemDefinition item, ItemQuality quality, EssenceSettings settings)
    {
        float strength = LevelingSettings.Current.QualityStrength(quality);
        List<StatBonus> bonuses = EmpowerBonuses(item, 1, settings);
        for (int i = bonuses.Count - 1; i >= 0; i--)
        {
            if (strength <= 0f)
            {
                bonuses.RemoveAt(i);
                continue;
            }

            StatBonus one = bonuses[i];
            bonuses[i] = new StatBonus(one.stat, one.value * strength, one.op);
        }

        return bonuses;
    }

    /// <summary>What this many levels of empowering add to the item.</summary>
    public static List<StatBonus> EmpowerBonuses(ItemDefinition item, int level, EssenceSettings settings)
    {
        var bonuses = new List<StatBonus>();
        if (item == null || level <= 0)
        {
            return bonuses;
        }

        foreach (StatBonus bonus in item.StatBonuses)
        {
            if (bonus.value > 0f)
            {
                bonuses.Add(new StatBonus(bonus.stat, bonus.value * settings.StatShare * level, bonus.op));
            }
        }

        if (item.IsWeapon)
        {
            bonuses.Add(new StatBonus(StatType.DamageBonus, settings.WeaponDamage * level));
        }

        if (item.IsTool)
        {
            bonuses.Add(new StatBonus(StatType.GatherYield, settings.ToolYield * level));
            bonuses.Add(new StatBonus(StatType.ToolCare, settings.ToolCare * level));
        }

        return bonuses;
    }

    /// <summary>What reaching a level costs: Purified Essence by the item's tier, and a share of its recipe's materials.</summary>
    public static RepairCost EmpowerCost(ItemDefinition item, int level, RecipeDefinition recipe, ResourceDefinition purified, EssenceSettings settings)
    {
        var cost = new RepairCost();
        if (item == null || level < 1 || level > settings.EmpowerLevels.Count)
        {
            return cost;
        }

        EssenceSettings.EmpowerLevel step = settings.EmpowerLevels[level - 1];
        int essence = step.purified + (item.Tier - 1) * settings.PurifiedPerTier;
        if (purified != null && essence > 0)
        {
            cost.Resources.Add(new ResourceAmount(purified, essence));
        }

        if (recipe == null)
        {
            return cost;
        }

        foreach (ItemAmount part in recipe.ItemCost)
        {
            if (part.Item != null && part.Amount > 0)
            {
                int amount = Mathf.CeilToInt(part.Amount * step.materialShare / Mathf.Max(1, recipe.OutputAmount) - 0.0001f);
                cost.Items.Add(new ItemAmount(part.Item, Mathf.Max(1, amount)));
            }
        }

        return cost;
    }

    /// <summary>
    /// A kill's Cursed Essence with the killer's Essence Find: 0.25 is a quarter more on average, the fraction left over
    /// paid by chance so that small drops gain too. <paramref name="roll"/> is 0..1.
    /// </summary>
    public static int Reaped(int amount, float find, float roll)
    {
        return find <= 0f || amount <= 0 ? amount : amount + Mathf.FloorToInt(amount * find + roll);
    }
}
