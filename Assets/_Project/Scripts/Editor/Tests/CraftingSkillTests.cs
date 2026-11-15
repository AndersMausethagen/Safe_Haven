using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The crafting skills: what kind of goods a craft is, the quality and condition a maker turns out, what a better make
/// is worth, and the experience a craft pays.</summary>
public sealed class CraftingSkillTests
{
    [Test]
    public void Goods_AreJudgedByWhatTheItemDoes()
    {
        Assert.AreEqual(CraftedGoods.Weapons, CraftedWork.Of(Item("CopperSword")));
        Assert.AreEqual(CraftedGoods.Armor, CraftedWork.Of(Item("CopperScaleHelm")));
        Assert.AreEqual(CraftedGoods.Armor, CraftedWork.Of(Item("CopperBoundShield")), "a shield is armour");
        Assert.AreEqual(CraftedGoods.Tools, CraftedWork.Of(Item("CopperPickaxe")));
        Assert.AreEqual(CraftedGoods.Arrows, CraftedWork.Of(Item("BodkinArrow")));
        Assert.AreEqual(CraftedGoods.Tonics, CraftedWork.Of(Item("HealingDraught")));
        Assert.AreEqual(CraftedGoods.Meals, CraftedWork.Of(Item("FishSoup")));
        Assert.AreEqual(CraftedGoods.Materials, CraftedWork.Of(Item("CopperIngot")));
    }

    [Test]
    public void GrowingAtTheFarmPlot_TrainsNoCraft()
    {
        RecipeDefinition[] farm = Recipes().Where(r => r.Station == CraftingStationType.Farm).ToArray();
        Assert.IsNotEmpty(farm);
        Assert.IsTrue(farm.All(r => CraftedWork.Of(r) == CraftedGoods.None));
    }

    [Test]
    public void EveryKindOfGoods_TrainsACraftingSkill()
    {
        LevelingSettings settings = LevelingSettings.Current;
        Assert.AreEqual("weaponcrafting", settings.SkillCrafting(CraftedGoods.Weapons).Id);
        Assert.AreEqual("weaponcrafting", settings.SkillCrafting(CraftedGoods.Tools).Id);
        Assert.AreEqual("weaponcrafting", settings.SkillCrafting(CraftedGoods.Arrows).Id);
        Assert.AreEqual("armorcrafting", settings.SkillCrafting(CraftedGoods.Armor).Id);
        Assert.AreEqual("cooking", settings.SkillCrafting(CraftedGoods.Meals).Id);
        Assert.AreEqual("alchemy", settings.SkillCrafting(CraftedGoods.Tonics).Id);
        Assert.AreEqual("construction", settings.SkillCrafting(CraftedGoods.Materials).Id, "refining trains the builder");
        Assert.AreSame(settings.FindSkill("construction"), settings.SkillTrainedBy(ProficiencyTraining.Construction), "and so does building");
    }

    [Test]
    public void Quality_FollowsTheRoll_WithAShareOfMasterwork()
    {
        Assert.AreEqual(ItemQuality.Masterwork, CraftedWork.QualityFor(0.4f, 0.05f, 0.25f));
        Assert.AreEqual(ItemQuality.Fine, CraftedWork.QualityFor(0.4f, 0.2f, 0.25f));
        Assert.AreEqual(ItemQuality.Plain, CraftedWork.QualityFor(0.4f, 0.5f, 0.25f));
        Assert.AreEqual(ItemQuality.Plain, CraftedWork.QualityFor(0f, 0f + 0.0001f, 0.25f), "a beginner makes plain pieces");
    }

    [Test]
    public void ABeginnersWork_StartsWorn_ByTheRightSkill()
    {
        var maker = new StatSheet();
        maker.AddModifier(new StatModifier(StatType.WeaponcraftFinish, StatModifierOp.Flat, -0.25f, this));
        maker.AddModifier(new StatModifier(StatType.ArmorcraftQuality, StatModifierOp.Flat, 0.3f, this));

        Assert.AreEqual(0.75f, CraftedWork.Finish(CraftedGoods.Weapons, maker), 0.0001f);
        Assert.AreEqual(1f, CraftedWork.Finish(CraftedGoods.Armor, maker), 0.0001f, "the smith's hand doesn't spoil armour");
        Assert.AreEqual(0.3f, CraftedWork.QualityChance(CraftedGoods.Armor, maker), 0.0001f);
        Assert.AreEqual(0f, CraftedWork.QualityChance(CraftedGoods.Weapons, maker), 0.0001f);

        ItemStack made = CraftedWork.Make(Item("CopperSword"), maker);
        Assert.AreEqual(0.75f, made.Durability, 0.001f);
        Assert.AreEqual(ItemQuality.Plain, made.Quality);
    }

    [Test]
    public void ABetterMake_GivesPartOfAnEmpowering_AndRaisesGearScore()
    {
        ItemDefinition sword = Item("CopperSword");
        var plain = new ItemStack(sword, 1);
        ItemStack fine = plain.WithQuality(ItemQuality.Fine);
        ItemStack masterwork = plain.WithQuality(ItemQuality.Masterwork);

        Assert.AreEqual("Fine " + sword.DisplayName, fine.DisplayName);
        Assert.LessOrEqual(GearScore.Of(plain), GearScore.Of(fine), "a copper sword's 5% may round away");
        Assert.Less(GearScore.Of(plain), GearScore.Of(masterwork));
        var greatsword = new ItemStack(Item("IronGreatsword"), 1);
        Assert.Less(GearScore.Of(greatsword), GearScore.Of(greatsword.WithQuality(ItemQuality.Fine)), "on a bigger piece it shows");

        EssenceSettings essence = EssenceSettings.Current;
        List<StatBonus> empowered = GearPower.EmpowerBonuses(sword, 1, essence);
        List<StatBonus> better = GearPower.QualityBonuses(sword, ItemQuality.Fine, essence);
        Assert.AreEqual(empowered.Count, better.Count);
        for (int i = 0; i < empowered.Count; i++)
        {
            Assert.AreEqual(empowered[i].value * LevelingSettings.Current.QualityStrength(ItemQuality.Fine), better[i].value, 0.0001f);
        }

        Assert.IsEmpty(GearPower.QualityBonuses(sword, ItemQuality.Plain, essence));
        Assert.AreEqual(ItemQuality.Plain, new ItemStack(Item("CopperIngot"), 3, 1f, 0, null, ItemQuality.Fine).Quality, "only gear has a make");
    }

    [Test]
    public void Quality_IsSaved()
    {
        var bag = new ItemContainer(4, 1);
        bag.AddStack(new ItemStack(Item("CopperSword"), 1, 0.8f, 0, null, ItemQuality.Masterwork, "Astrid"));
        string json = JsonUtility.ToJson(new SlotList { slots = SaveMapper.ToSlots(bag) });
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        ItemContainer read = SaveMapper.ReadSlots(JsonUtility.FromJson<SlotList>(json).slots, new ItemContainer(4, 1), content, new List<string>());

        ItemStack stack = read.GetSlot(0);
        Assert.AreEqual(ItemQuality.Masterwork, stack.Quality);
        Assert.AreEqual(0.8f, stack.Durability, 0.001f);
        Assert.AreEqual("Astrid", stack.Maker, "its maker's name goes with it");
        Assert.AreEqual("", new ItemStack(Item("CopperSword"), 1, 1f, 0, null, ItemQuality.Plain, "Astrid").Maker, "plain work goes unsigned");
    }

    [Test]
    public void TheCaravan_PaysForGoodWork_AndNothingForPlain()
    {
        TradeSettings trade = TradeSettings.Current;
        var plain = new ItemStack(Item("IronGreatsword"), 1);
        Assert.AreEqual(0, trade.CoinsFor(plain));
        Assert.Greater(trade.CoinsFor(plain.WithQuality(ItemQuality.Fine)), 0);
        Assert.Greater(trade.CoinsFor(plain.WithQuality(ItemQuality.Masterwork)), trade.CoinsFor(plain.WithQuality(ItemQuality.Fine)));
    }

    [Test]
    public void SpecialCrafts_NeedAPractisedHand()
    {
        RecipeDefinition helm = Recipes().First(r => r.Output != null && r.Output.name == "TrollstoneHelm");
        Assert.AreEqual("armorcrafting", helm.RequiredSkill.Id);
        var progress = new CharacterProgress();
        StringAssert.Contains("Armorcrafting", helm.SkillProblem(progress));
        progress.AddExperience(helm.RequiredSkill, LevelingSettings.Current.TotalExperienceFor(helm.RequiredSkillLevel), LevelingSettings.Current);
        Assert.IsNull(helm.SkillProblem(progress));
        // Docs/Skills.md: the kits from copper to moonsilver never wait on a skill, as raid tiers need them. Runecraft's own work does,
        // by design (GDD: the runic staff at Runecraft 3, the art runes at Runecraft 5); it is no tier's kit.
        bool RunecraftsOwn(RecipeDefinition r) => r.RequiredSkill != null && r.RequiredSkill.Id == "runecraft";
        Assert.IsTrue(Recipes().Where(r => r.Output != null && r.Output.Tier <= 4 && CraftedWork.Of(r.Output) != CraftedGoods.Meals
                && CraftedWork.Of(r.Output) != CraftedGoods.Tonics && !RunecraftsOwn(r)).All(r => r.RequiredSkill == null), "the tier kits never wait on a skill");
        Assert.IsTrue(RunecraftsOwn(Recipes().First(r => r.Output != null && r.Output.name == "RunicStaff")), "the runic staff waits on Runecraft");
    }

    [Test]
    public void ACraft_TrainsItsSkill_AtItsTier_WithoutTheRaidBonus()
    {
        LevelingSettings settings = LevelingSettings.Current;
        RecipeDefinition recipe = Recipes().First(r => r.Output != null && r.Output.name == "CopperSword");
        ProficiencyDefinition skill = settings.FindSkill("weaponcrafting");
        GameSession.StartNewGame();
        try
        {
            ExperienceTracker.PractiseCraft(recipe);
            int tier = Mathf.Max(1, recipe.Output.Tier);
            float expected = settings.TierExperience(tier) * settings.CraftWork(CraftedGoods.Weapons) / settings.PracticeTimeScale;
            Assert.AreEqual(Mathf.FloorToInt(expected), GameSession.Progress.ExperienceOf(skill));
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [System.Serializable]
    private sealed class SlotList
    {
        public List<SlotSaveData> slots;
    }

    private static ItemDefinition Item(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:ItemDefinition {name}"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.name == name)
            {
                return item;
            }
        }

        Assert.Fail($"no item {name}");
        return null;
    }

    private static IEnumerable<RecipeDefinition> Recipes() => AssetDatabase.FindAssets("t:RecipeDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(recipe => recipe != null);
}
