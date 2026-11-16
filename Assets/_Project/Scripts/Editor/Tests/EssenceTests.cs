using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>What Essence buys (GDD §13.5, §16.4): empowered and enchanted gear, skill ranks, and the rules that keep them fair.</summary>
public sealed class EssenceTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    [Test]
    public void WorkOnGear_StaysWithItThroughWearMovesAndSwaps()
    {
        EnchantmentDefinition enchantment = Enchantment();
        ItemDefinition helm = Helm();
        var bag = new ItemContainer(2, 1);
        var other = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(helm, 1, 1f, 2, enchantment));

        bag.SetDurability(0, 0.5f);
        Assert.AreEqual(2, bag.GetSlot(0).Empower, "wear keeps it");
        Assert.AreEqual(enchantment, bag.GetSlot(0).Enchantment);

        Assert.AreEqual(1, bag.TransferTo(0, other), "moved as gear, not made anew");
        ItemStack moved = other.GetSlot(0);
        Assert.AreEqual(2, moved.Empower);
        Assert.AreEqual(enchantment, moved.Enchantment);
        Assert.AreEqual(0.5f, moved.Durability, 0.001f);
        Assert.AreEqual("Test Enchantment helm +2", moved.DisplayName);

        ItemStack replaced = moved.WithEmpower(3).WithEnchantment(null);
        Assert.AreEqual(3, replaced.Empower);
        Assert.IsNull(replaced.Enchantment);
        Assert.AreEqual(0.5f, replaced.Durability, 0.001f);
    }

    [Test]
    public void ThingsThatArentGear_TakeNoWork()
    {
        var berry = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(berry);
        var stack = new ItemStack(berry, 3, 1f, 2, Enchantment());
        Assert.AreEqual(0, stack.Empower);
        Assert.IsNull(stack.Enchantment);
        Assert.IsFalse(GearPower.CanEmpower(berry));
    }

    [Test]
    public void GearSet_AddsWorkWhileWorn_AndNotWhenBroken()
    {
        ItemDefinition helm = Helm();
        var gear = new GearSet();
        var stats = new StatSheet();
        gear.Bind(stats);
        gear.Slots.SetSlot((int)GearSlot.Head, new ItemStack(helm, 1));
        Assert.AreEqual(10f, stats.Get(StatType.Armor), 0.001f);

        EssenceSettings settings = EssenceSettings.Current;
        gear.Slots.SetSlot((int)GearSlot.Head, gear.Get(GearSlot.Head).WithEmpower(2).WithEnchantment(Enchantment()));
        Assert.AreEqual(10f + 10f * settings.StatShare * 2f + 8f, stats.Get(StatType.Armor), 0.001f);
        Assert.AreEqual(-15f, stats.Get(StatType.MaxHealth), 0.001f, "the drawback counts too");

        gear.Slots.SetDurability((int)GearSlot.Head, 0f);
        Assert.AreEqual(0f, stats.Get(StatType.Armor), 0.001f, "broken gear gives nothing, like its own stats");
    }

    [Test]
    public void Empowering_TakesEssenceAndTheItemsOwnMaterials()
    {
        EssenceSettings settings = EssenceSettings.Current;
        ResourceDefinition purified = Create<ResourceDefinition>("purified");
        ItemDefinition iron = Create<ItemDefinition>("iron");
        ItemDefinition helm = Helm(tier: 3);
        var recipe = Create<RecipeDefinition>("helm recipe");
        var data = new SerializedObject(recipe);
        SerializedProperty cost = data.FindProperty("itemCost");
        cost.arraySize = 1;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = iron;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 8;
        data.FindProperty("outputAmount").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();

        RepairCost first = GearPower.EmpowerCost(helm, 1, recipe, purified, settings);
        Assert.AreEqual(settings.EmpowerLevels[0].purified + 2 * settings.PurifiedPerTier, first.Resources[0].Amount, "dearer by tier");
        Assert.AreEqual(iron, first.Items[0].Item, "a tier-II iron weapon still requires iron");
        Assert.AreEqual(Mathf.CeilToInt(8 * settings.EmpowerLevels[0].materialShare), first.Items[0].Amount);
        Assert.Greater(GearPower.EmpowerCost(helm, 2, recipe, purified, settings).Resources[0].Amount, first.Resources[0].Amount);
        Assert.IsTrue(GearPower.EmpowerCost(helm, settings.EmpowerLevels.Count + 1, recipe, purified, settings).IsFree, "there is no level past the last");
    }

    [Test]
    public void Empowering_StrengthensWhatTheGearIsFor()
    {
        EssenceSettings settings = EssenceSettings.Current;
        ItemDefinition axe = Create<ItemDefinition>("axe");
        var data = new SerializedObject(axe);
        data.FindProperty("toolType").intValue = (int)ToolType.Axe;
        data.FindProperty("toolTier").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        List<StatBonus> tool = GearPower.EmpowerBonuses(axe, 2, settings);
        Assert.AreEqual(settings.ToolYield * 2f, tool.First(b => b.stat == StatType.GatherYield).value, 0.001f);
        Assert.AreEqual(settings.ToolCare * 2f, tool.First(b => b.stat == StatType.ToolCare).value, 0.001f);
        Assert.IsEmpty(GearPower.EmpowerBonuses(axe, 0, settings));
    }

    [Test]
    public void EssenceFind_AddsItsShare_EvenOnSmallDrops()
    {
        Assert.AreEqual(4, GearPower.Reaped(4, 0f, 0.99f), "without it, nothing changes");
        Assert.AreEqual(5, GearPower.Reaped(4, 0.25f, 0f));
        Assert.AreEqual(1, GearPower.Reaped(1, 0.25f, 0.5f), "a quarter of one, by chance: not this time");
        Assert.AreEqual(2, GearPower.Reaped(1, 0.25f, 0.8f), "this time");
        Assert.AreEqual(0, GearPower.Reaped(0, 0.25f, 0.99f), "nothing makes nothing");
    }

    [Test]
    public void SkillRanks_GrowAndCount()
    {
        SkillDefinition skill = Create<SkillDefinition>("Ember");
        var progress = new CharacterProgress();
        Assert.AreEqual(0, progress.RankOf(skill));
        progress.DeepenSkill(skill);
        progress.DeepenSkill(skill);
        Assert.AreEqual(2, progress.RankOf(skill));

        EssenceSettings settings = EssenceSettings.Current;
        Assert.AreEqual(0f, settings.PowerAt(0));
        Assert.AreEqual(settings.SkillRanks[1].power, settings.PowerAt(2));
        Assert.AreEqual(settings.SkillRanks[settings.SkillRanks.Count - 1].cooldownCut, settings.CooldownCutAt(99), "a rank past the last counts as the last");
        for (int i = 1; i < settings.SkillRanks.Count; i++)
        {
            Assert.Greater(settings.SkillRanks[i].power, settings.SkillRanks[i - 1].power, "each rank states its total");
            Assert.Greater(settings.SkillRanks[i].purified, settings.SkillRanks[i - 1].purified);
        }

        progress.ResetPerks();
        Assert.AreEqual(2, progress.RankOf(skill), "choosing perks again doesn't undo what essence bought");
    }

    [Test]
    public void AltarUpgrades_SpeedItUp_AndTheBonusIsSaved()
    {
        var settlement = new SettlementState();
        settlement.QueuePurification(4, 10);
        settlement.SetPurificationBonus(0.5f);
        Assert.AreEqual(1, settlement.TickPurification(20f * (1f + settlement.PurificationBonus) * 0.7f, 20f));
        settlement.Restore(false, 0, 0, default, 2, 0f, 0.25f);
        Assert.AreEqual(0.25f, settlement.PurificationBonus);

        var altar = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/PurificationAltar.asset");
        Assert.Greater(altar.UpgradeTo(2).craftSpeed, 0f, "the GDD promises throughput, not only renown");
        Assert.Greater(altar.UpgradeTo(altar.MaxLevel).essence.Count, 0, "the greatest tier takes Purified Essence");
    }

    [Test]
    public void Saves_KeepTheWork_AndOldSavesLoadWithout()
    {
        ItemDefinition helm = FirstGear();
        EnchantmentDefinition enchantment = EssenceSettings.Current.Enchantments[0];
        var bag = new ItemContainer(4, 1);
        bag.SetSlot(1, new ItemStack(helm, 1, 0.75f, 3, enchantment));
        var progress = new CharacterProgress();
        SkillDefinition skill = Create<SkillDefinition>("Ember");
        progress.DeepenSkill(skill);
        var settlement = new SettlementState();
        settlement.SetPurificationBonus(0.5f);
        var content = Resources.Load<ContentDatabase>("ContentDatabase");

        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(bag, null, null), null, "SafeHaven", null, DateTime.UtcNow, settlement: settlement, progress: progress);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content);

        ItemStack stack = loaded.Player.Slots.First().Stack;
        Assert.AreEqual(3, stack.Empower);
        Assert.AreEqual(enchantment, stack.Enchantment);
        Assert.AreEqual(0.75f, stack.Durability, 0.001f);
        Assert.AreEqual(1, loaded.Progress.RankOf(skill));
        Assert.AreEqual(0.5f, loaded.Settlement.PurificationBonus);

        string json = JsonUtility.ToJson(data).Replace("\"empower\":3", "\"old\":0").Replace($"\"enchantment\":\"{enchantment.Id}\"", "\"older\":\"\"");
        LoadedGame old = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(json), content);
        Assert.AreEqual(0, old.Player.Slots.First().Stack.Empower);
        Assert.IsNull(old.Player.Slots.First().Stack.Enchantment);
        Assert.IsEmpty(old.MissingContent);
    }

    [Test]
    public void FailedRaid_KeepsTheWorkOnDeployedGear()
    {
        ItemDefinition helm = Helm();
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(helm, 1, 1f, 2, Enchantment()));
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, new ResourceStorage(10), null);
        PlayerSnapshot kept = loadout.AfterFailedRaid(loadout, 0.25f, new List<EquipmentWear>());
        Assert.AreEqual(2, kept.Slots[0].Stack.Empower);
        Assert.IsNotNull(kept.Slots[0].Stack.Enchantment);
        Assert.AreEqual(0.75f, kept.Slots[0].Stack.Durability, 0.001f);
    }

    [Test]
    public void AuthoredEnchantments_KeepTheBargainOfTheirSchool()
    {
        SettlementSettings settlement = SettlementSettings.Current;
        IReadOnlyList<EnchantmentDefinition> enchantments = EssenceSettings.Current.Enchantments;
        Assert.GreaterOrEqual(enchantments.Count, 10);
        Assert.AreEqual(enchantments.Count, enchantments.Select(e => e.Id).Distinct().Count(), "ids are what saves hold");
        foreach (EnchantmentDefinition enchantment in enchantments)
        {
            bool cursed = enchantment.School == EssenceSchool.Cursed;
            Assert.AreNotEqual(EnchantTarget.None, enchantment.Targets, enchantment.name);
            Assert.AreEqual(cursed ? settlement.CursedEssence : settlement.PurifiedEssence, enchantment.Cost.Single().Resource, enchantment.name);
            // A penalty is what makes its owner worse off: a negative health, or a positive noise (StatNames.IsPenalty).
            Assert.AreEqual(cursed, enchantment.Bonuses.Any(b => StatNames.IsPenalty(b.stat, b.value)), $"{enchantment.name}: every drawback must be stated, and only Cursed work has one");
            // A great enchantment gives a verb (its effect) where others give a number (Docs/EnchantingResearch.md #7).
            Assert.IsTrue(enchantment.Bonuses.Any(b => !StatNames.IsPenalty(b.stat, b.value)) || (enchantment.IsGreat && enchantment.Effects.Any(e => e != null)), enchantment.name);
            Assert.AreEqual(enchantment.IsGreat, enchantment.Effects.Any(e => e != null), $"{enchantment.name}: great means it has an effect");
            Assert.IsNotEmpty(enchantment.Description, enchantment.name);
            StringAssert.DoesNotContain(" ", enchantment.Id, enchantment.name);
        }

        // Each kind of gear with its slots (an enchantment made for body armour alone is still an armour choice).
        var kinds = new System.Collections.Generic.Dictionary<EnchantTarget, EnchantTarget>
        {
            [EnchantTarget.Weapon] = EnchantTarget.Weapon | EnchantTarget.Bow | EnchantTarget.Staff,
            [EnchantTarget.Armor] = EnchantTarget.Armor | EnchantTarget.Head | EnchantTarget.Chest | EnchantTarget.Hands | EnchantTarget.Waist | EnchantTarget.Feet,
            [EnchantTarget.Jewelry] = EnchantTarget.Jewelry | EnchantTarget.Neck | EnchantTarget.Ring,
            [EnchantTarget.Tool] = EnchantTarget.Tool,
        };
        foreach (var kind in kinds)
        {
            Assert.IsTrue(enchantments.Any(e => e.School == EssenceSchool.Purified && (e.Targets & kind.Value) != 0), $"a safe choice for {kind.Key}");
            Assert.IsTrue(enchantments.Any(e => e.School == EssenceSchool.Cursed && (e.Targets & kind.Value) != 0), $"a risky choice for {kind.Key}");
        }
    }

    [Test]
    public void EverySpentEssence_HasARecipeOrUpgradeToGoTo()
    {
        var content = Resources.Load<ContentDatabase>("ContentDatabase");
        ItemDefinition purifiedDraught = content.FindItem("purifieddraught");
        ItemDefinition cursedDraught = content.FindItem("curseddraught");
        Assert.IsNotNull(purifiedDraught);
        Assert.IsNotNull(cursedDraught);
        Assert.IsTrue(purifiedDraught.IsTonic && purifiedDraught.HasBoon);
        Assert.IsTrue(cursedDraught.BoonBonuses.Any(b => b.value < 0f), "a Cursed draught states its price too");
        Assert.AreEqual(SettlementSettings.Current.PurifiedEssence, content.FindRecipe("purifieddraught").ResourceCost[0].Resource);
        Assert.AreEqual(SettlementSettings.Current.CursedEssence, content.FindRecipe("curseddraught").ResourceCost[0].Resource);
    }

    [Test]
    public void WorkedGear_ShowsWhatWasDone()
    {
        EssenceSettings settings = EssenceSettings.Current;
        ItemDefinition helm = Helm(tier: 3);
        EnchantmentDefinition enchantment = Enchantment();
        Assert.AreEqual(Color.black, GearGlow.ColorFor(new ItemStack(helm, 1), settings), "plain gear doesn't glow");
        Assert.IsNull(GearGlow.SlotTint(new ItemStack(helm, 1), UIStyle.Current));
        Assert.AreEqual("III", GearGlow.CornerText(new ItemStack(helm, 1)));
        Assert.AreEqual("III +2", GearGlow.CornerText(new ItemStack(helm, 1, 1f, 2)));
        Assert.AreEqual("", GearGlow.CornerText(ItemStack.Empty));

        Color plain = GearGlow.ColorFor(new ItemStack(helm, 1, 1f, 0, enchantment), settings);
        Color strong = GearGlow.ColorFor(new ItemStack(helm, 1, 1f, 3, enchantment), settings);
        Assert.AreEqual(settings.PurifiedGlow, plain);
        Assert.Greater(strong.g, plain.g, "empowering strengthens it");
        Assert.AreEqual(UIStyle.Current.Good, GearGlow.SlotTint(new ItemStack(helm, 1, 1f, 0, enchantment), UIStyle.Current));

        var cursed = new SerializedObject(enchantment);
        cursed.FindProperty("school").intValue = (int)EssenceSchool.Cursed;
        cursed.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(settings.CursedGlow, GearGlow.ColorFor(new ItemStack(helm, 1, 1f, 0, enchantment), settings));
        Assert.AreEqual(UIStyle.Current.Danger, GearGlow.SlotTint(new ItemStack(helm, 1, 1f, 0, enchantment), UIStyle.Current));
    }

    [Test]
    public void TheWindowsSymbols_AreInTheFont()
    {
        TMPro.TMP_FontAsset font = TMPro.TMP_Settings.defaultFontAsset;
        foreach (char symbol in new[] { '♦', '×', '•', '→' })
        {
            Assert.IsTrue(font.HasCharacter(symbol, true, true), $"{symbol} would show as a missing glyph");
        }
    }

    [Test]
    public void TargetOf_SortsGearTheWayEnchantmentsMeanIt()
    {
        Assert.AreEqual(EnchantTarget.Armor | EnchantTarget.Head, EnchantmentDefinition.TargetOf(Helm()));
        ItemDefinition ring = Create<ItemDefinition>("ring");
        var data = new SerializedObject(ring);
        data.FindProperty("armorSlot").intValue = (int)ArmorSlot.Finger;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(EnchantTarget.Jewelry | EnchantTarget.Ring, EnchantmentDefinition.TargetOf(ring));
        Assert.AreEqual(EnchantTarget.None, EnchantmentDefinition.TargetOf(null));
    }

    private static ItemDefinition FirstGear()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.IsArmor)
            {
                return item;
            }
        }

        throw new InvalidOperationException("No armor to test with.");
    }

    private ItemDefinition Helm(int tier = 1)
    {
        ItemDefinition item = Create<ItemDefinition>("helm");
        var data = new SerializedObject(item);
        data.FindProperty("armorSlot").intValue = (int)ArmorSlot.Head;
        data.FindProperty("tier").intValue = tier;
        SerializedProperty bonuses = data.FindProperty("statBonuses");
        bonuses.arraySize = 1;
        bonuses.GetArrayElementAtIndex(0).FindPropertyRelative("stat").intValue = (int)StatType.Armor;
        bonuses.GetArrayElementAtIndex(0).FindPropertyRelative("op").intValue = (int)StatModifierOp.Flat;
        bonuses.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = 10f;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private EnchantmentDefinition Enchantment()
    {
        EnchantmentDefinition enchantment = Create<EnchantmentDefinition>("Test Enchantment");
        var data = new SerializedObject(enchantment);
        SerializedProperty bonuses = data.FindProperty("bonuses");
        bonuses.arraySize = 2;
        Bonus(bonuses.GetArrayElementAtIndex(0), StatType.Armor, 8f);
        Bonus(bonuses.GetArrayElementAtIndex(1), StatType.MaxHealth, -15f);
        // For armour: an enchantment only counts where it may be carved (GearPower.BonusesOf checks Fits).
        data.FindProperty("targets").intValue = (int)EnchantTarget.Armor;
        data.ApplyModifiedPropertiesWithoutUndo();
        return enchantment;
    }

    private static void Bonus(SerializedProperty entry, StatType stat, float value)
    {
        entry.FindPropertyRelative("stat").intValue = (int)stat;
        entry.FindPropertyRelative("op").intValue = (int)StatModifierOp.Flat;
        entry.FindPropertyRelative("value").floatValue = value;
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
