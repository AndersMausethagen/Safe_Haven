using System;
using UnityEngine;

/// <summary>What kind of thing a craft makes, for the crafting skill it trains (ProficiencyDefinition › Crafts).</summary>
[Flags]
public enum CraftedGoods
{
    None = 0,
    Weapons = 1,
    Tools = 2,
    Arrows = 4,
    /// <summary>Armour, shields and packs.</summary>
    Armor = 8,
    /// <summary>Food and drink: anything that feeds, quenches or gives a meal's bonus.</summary>
    Meals = 16,
    /// <summary>Tonics, cures and remedies.</summary>
    Tonics = 32,
    /// <summary>Refined materials: planks, ingots, blocks, leather.</summary>
    Materials = 64,
}

/// <summary>How well a piece of gear was made. Fine and Masterwork pieces give more, as if empowered part of the way.</summary>
public enum ItemQuality
{
    Plain = 0,
    Fine = 1,
    Masterwork = 2,
}

/// <summary>
/// The rules of what the player makes, as plain functions (tested in CraftingSkillTests): what kind of goods an item is,
/// which of the maker's stats shape it, the quality a roll gives, and the gear a maker turns out (its quality and the
/// condition it starts in).
/// </summary>
public static class CraftedWork
{
    /// <summary>What a recipe makes, as goods a skill is trained by. Growing (the Farm Plot, the Beehive) and the portal's
    /// work make nothing a craftsman does.</summary>
    public static CraftedGoods Of(RecipeDefinition recipe)
    {
        if (recipe == null || recipe.Station == CraftingStationType.Farm || recipe.Station == CraftingStationType.Beehive
            || recipe.Station == CraftingStationType.Portal)
        {
            return CraftedGoods.None;
        }

        return Of(recipe.Output);
    }

    /// <summary>The kind of goods an item is, by what it does.</summary>
    public static CraftedGoods Of(ItemDefinition item)
    {
        if (item == null)
        {
            return CraftedGoods.None;
        }

        if (item.IsArrow)
        {
            return CraftedGoods.Arrows;
        }

        if (item.IsWeapon)
        {
            return item.Weapon.IsShield ? CraftedGoods.Armor : CraftedGoods.Weapons;
        }

        if (item.IsArmor || item.IsBackpack)
        {
            return CraftedGoods.Armor;
        }

        if (item.IsTool)
        {
            return CraftedGoods.Tools;
        }

        if (item.IsTonic || item.Cures.Count > 0)
        {
            return CraftedGoods.Tonics;
        }

        if (item.Category == ItemCategory.Consumable && (item.HasBoon || item.HungerRestore > 0f || item.ThirstRestore > 0f))
        {
            return CraftedGoods.Meals;
        }

        if (item.Category == ItemCategory.Consumable && item.HealAmount > 0f)
        {
            return CraftedGoods.Tonics;
        }

        return item.Category == ItemCategory.Material ? CraftedGoods.Materials : CraftedGoods.None;
    }

    /// <summary>The maker's chance of a better piece for these goods (Weaponcraft or Armorcraft Quality); 0 for the rest.</summary>
    public static float QualityChance(CraftedGoods goods, StatSheet maker) =>
        maker == null ? 0f
        : IsArmour(goods) ? maker.Get(StatType.ArmorcraftQuality)
        : IsWeaponry(goods) ? maker.Get(StatType.WeaponcraftQuality)
        : 0f;

    /// <summary>The condition, 0-1, a new piece of these goods starts in: a beginner's work starts worn.</summary>
    public static float Finish(CraftedGoods goods, StatSheet maker)
    {
        float less = maker == null ? 0f
            : IsArmour(goods) ? maker.Get(StatType.ArmorcraftFinish)
            : IsWeaponry(goods) ? maker.Get(StatType.WeaponcraftFinish)
            : 0f;
        return Mathf.Clamp(1f + less, 0.1f, 1f);
    }

    /// <summary>The maker's chance of one more of a batch of these goods (an extra portion, draught or plank).</summary>
    public static float ExtraChance(CraftedGoods goods, StatSheet maker)
    {
        if (maker == null)
        {
            return 0f;
        }

        switch (goods)
        {
            case CraftedGoods.Meals: return maker.Get(StatType.CookingYield);
            case CraftedGoods.Tonics: return maker.Get(StatType.AlchemyYield);
            case CraftedGoods.Materials: return maker.Get(StatType.RefiningYield);
            default: return 0f;
        }
    }

    /// <summary>The quality a roll from 0 to 1 gives: Masterwork below chance x masterwork share, Fine below chance.</summary>
    public static ItemQuality QualityFor(float chance, float roll, float masterworkShare)
    {
        chance = Mathf.Clamp01(chance);
        if (roll < chance * Mathf.Clamp01(masterworkShare))
        {
            return ItemQuality.Masterwork;
        }

        return roll < chance ? ItemQuality.Fine : ItemQuality.Plain;
    }

    /// <summary>One piece of gear as this maker turns it out: its quality rolled, the condition it starts in. Anything that
    /// isn't gear is a plain stack of one.</summary>
    public static ItemStack Make(ItemDefinition item, StatSheet maker)
    {
        if (item == null || !item.HasDurability)
        {
            return new ItemStack(item, 1);
        }

        CraftedGoods goods = Of(item);
        ItemQuality quality = QualityFor(QualityChance(goods, maker), UnityEngine.Random.value, LevelingSettings.Current.MasterworkShare);
        bool armour = IsArmour(goods);
        string run = armour ? "armour" : "weapons";
        // Sure Hand: after a run of plain pieces, the next is good work for sure.
        if (quality == ItemQuality.Plain && Techniques.Has(armour ? PerkTechnique.ArmorSureHand : PerkTechnique.WeaponSureHand)
            && GameSession.Progress.PlainRunOf(run) >= TechniqueSettings.Current.SureHandRun)
        {
            quality = ItemQuality.Fine;
        }

        GameSession.Progress.NoteMade(run, quality == ItemQuality.Plain);
        // Runed Make: a Masterwork comes from the anvil already empowered once.
        int empower = quality == ItemQuality.Masterwork && GearPower.CanEmpower(item)
                      && Techniques.Has(armour ? PerkTechnique.ArmorRunedMake : PerkTechnique.WeaponRunedMake) ? 1 : 0;
        string name = GameSession.Appearance != null ? GameSession.Appearance.DisplayName : null;
        return new ItemStack(item, 1, Finish(goods, maker), empower, null, quality, name);
    }

    /// <summary>Gives a crafted batch to a maker's bag: gear piece by piece as they make it, the rest as it comes, with the
    /// chance of one more. Returns how many arrived.</summary>
    public static int GiveTo(ItemInventory bag, ItemDefinition item, int amount, StatSheet maker)
    {
        if (bag == null || item == null || amount <= 0)
        {
            return 0;
        }

        if (!item.HasDurability)
        {
            int extra = UnityEngine.Random.value < ExtraChance(Of(item), maker) ? 1 : 0;
            if (extra > 0)
            {
                GameMessages.Post($"A little extra: +1 {item.DisplayName}", MessageKind.Gain);
            }

            return bag.Give(item, amount + extra);
        }

        int given = 0;
        for (int i = 0; i < amount; i++)
        {
            ItemStack piece = Make(item, maker);
            if (bag.GiveMade(piece) <= 0)
            {
                break;
            }

            given++;
            if (piece.Quality != ItemQuality.Plain)
            {
                GameMessages.Post($"You made a {piece.DisplayName}!", MessageKind.Gain);
            }
        }

        return given;
    }

    private static bool IsWeaponry(CraftedGoods goods) => (goods & (CraftedGoods.Weapons | CraftedGoods.Tools | CraftedGoods.Arrows)) != 0;

    private static bool IsArmour(CraftedGoods goods) => (goods & CraftedGoods.Armor) != 0;
}
