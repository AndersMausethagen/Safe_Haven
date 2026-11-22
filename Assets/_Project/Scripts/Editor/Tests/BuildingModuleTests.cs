using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Building modules (BuildingModules, the user 2026-11-21): a Storage Loft's second output slot lets another kind wait, the shipped modules
/// each fit something and are taught by a quest, and a quest that asks for one teaches one that fits.
/// </summary>
public sealed class BuildingModuleTests
{
    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    private static RecipeDefinition Recipe(string name) =>
        AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/" + name + ".asset");

    // Two crafts of different things, ticked until the station can do no more.
    private static StationQueue Run(int outputSlots, out RecipeDefinition first, out RecipeDefinition second)
    {
        first = Recipe("Bandage");
        second = Recipe("BoneCharm");
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first.Output, second.Output, "two different things");
        Assert.AreEqual(first.Station, second.Station);
        var queue = new StationQueue("test", first.Station, 20, 5) { OutputSlots = outputSlots };
        queue.Enqueue(first);
        queue.Enqueue(second);
        for (int i = 0; i < 20; i++)
        {
            queue.Tick(30f, null);
        }

        return queue;
    }

    // The systems review, 2026-11-21: a module's thrifty recipe trades time for materials, so the plain one keeps a reason to be chosen.
    [Test]
    public void AVariantThatCostsLess_TakesLonger()
    {
        int checkedCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition"))
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            RecipeDefinition plain = recipe != null ? recipe.Replaces : null;
            if (plain == null || recipe.ItemCost.Sum(cost => cost.Amount) >= plain.ItemCost.Sum(cost => cost.Amount))
            {
                continue;
            }

            checkedCount++;
            Assert.Greater(recipe.CraftSeconds, plain.CraftSeconds, $"{recipe.name} costs less than {plain.name}, so it takes longer");
        }

        Assert.Greater(checkedCount, 0, "there are thrifty variants to check");
    }

    // Strength costs upkeep: a module that adds health makes mending dearer, and says so.
    [Test]
    public void AModuleThatAddsHealth_MakesMendingDearer()
    {
        int checkedCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingModuleDefinition"))
        {
            var module = AssetDatabase.LoadAssetAtPath<BuildingModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (module == null || module.Bonus.healthShare <= 0f)
            {
                continue;
            }

            checkedCount++;
            Assert.Greater(module.Bonus.mendCostShare, 0f, module.name);
            StringAssert.Contains("mending it costs", module.Effect(), module.name);
        }

        Assert.Greater(checkedCount, 0, "there are modules that add health");
        Assert.AreEqual(15, BuildingRepairs.Share(20, 1f, 0.5f * 1.5f), "all of it broken, at half the cost, half again: 20 × 0.5 × 1.5");
    }

    [Test]
    public void OneOutputSlot_StopsAtTheNextKind()
    {
        StationQueue queue = Run(1, out RecipeDefinition first, out RecipeDefinition second);
        Assert.AreSame(first.Output, queue.Output);
        Assert.IsTrue(queue.Blocked, "the second kind waits for the slot");
        Assert.AreEqual(1, queue.QueuedCount);
    }

    [Test]
    public void ASecondOutputSlot_LetsAnotherKindWait_AndItMovesUpWhenTheFirstIsTaken()
    {
        StationQueue queue = Run(2, out RecipeDefinition first, out RecipeDefinition second);
        Assert.IsFalse(queue.Blocked);
        Assert.AreEqual(0, queue.QueuedCount, "both made");
        Assert.AreSame(first.Output, queue.Output);
        Assert.AreEqual(1, queue.MoreOutput.Count);
        Assert.AreSame(second.Output, queue.MoreOutput[0].made);

        queue.TakeOutput(queue.OutputAmount);
        Assert.AreSame(second.Output, queue.Output, "the next kind moves up");
        Assert.AreEqual(0, queue.MoreOutput.Count);
    }

    [Test]
    public void EveryModule_FitsSomethingThatReachesItsTier_IsTaught_AndIsNotSpeedAlone()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Assert.GreaterOrEqual(Content.Modules.Count, 20, "the Storage Loft, the Apprentice's Corner and the catalogue's eighteen");
        foreach (BuildingModuleDefinition module in Content.Modules)
        {
            Assert.IsTrue(book.Buildings.Any(b => module.Fits(b) && b.MaxLevel >= module.Tier), module.name + " fits a building that reaches its tier");
            bool byQuest = Content.Quests.Any(q => q != null && q.UnlocksModules.Contains(module));
            bool byTier = book.Buildings.Any(b => b != null && Enumerable.Range(2, Mathf.Max(0, b.MaxLevel - 1))
                .Any(level => b.UpgradeTo(level) != null && b.UpgradeTo(level).unlocksModules.Contains(module)));
            Assert.IsTrue(module.KnownFromStart || byQuest || byTier, module.name + " is taught");
            Assert.IsNotEmpty(module.Effect(), module.name + " does something");
            Assert.IsTrue(module.Cost.All(c => c.Item != null && c.Amount > 0), module.name + "'s cost");
            BuildingBonus bonus = module.Bonus;
            bool opensRecipes = Content.Recipes.Any(r => r != null && r.NeedsModule == module);
            bool speedAlone = bonus.craftSpeed > 0f && !opensRecipes && bonus.extraQueue == 0 && bonus.extraOutput == 0 && bonus.extraOutputSlots == 0
                && bonus.extraChance <= 0f && bonus.fineChance <= 0f;
            Assert.IsFalse(speedAlone, module.name + " is more than speed");
        }
    }

    [Test]
    public void AThriftyVariant_MakesTheSameForLess_AtTheSameStation_WithItsModule()
    {
        int variants = 0;
        foreach (RecipeDefinition variant in Content.Recipes.Where(r => r != null && r.Replaces != null))
        {
            RecipeDefinition plain = variant.Replaces;
            Assert.IsNotNull(variant.NeedsModule, variant.name + " is opened by a module");
            Assert.AreSame(plain.OutputDefinition, variant.OutputDefinition, variant.name + " makes the same");
            Assert.AreEqual(plain.Station, variant.Station, variant.name + " at the same station");
            Assert.IsTrue(Content.Buildings.Any(b => b != null && b.Station == variant.Station && variant.NeedsModule.Fits(b)), variant.name + "'s module fits its station");
            float Each(RecipeDefinition r) => (r.ItemCost.Sum(c => c.Amount) + r.ResourceCost.Sum(c => c.Amount)) / (float)r.OutputAmount;
            Assert.Less(Each(variant), Each(plain), variant.name + " costs less for each made");
            // Ore never gets cheaper (the research's rule: metal sets the pace); home materials and herbs are what is saved.
            foreach (ItemAmount cost in plain.ItemCost.Where(c => c.Item != null && (c.Item.name.Contains("Ingot") || c.Item.name.Contains("Ore"))))
            {
                Assert.AreEqual(cost.Amount / (float)plain.OutputAmount, variant.ItemCost.Where(c => c.Item == cost.Item).Sum(c => c.Amount) / (float)variant.OutputAmount,
                    0.001f, $"{variant.name}: {cost.Item.name} costs the same");
            }

            Assert.IsTrue(Content.Recipes.Contains(variant), variant.name + " is in the recipe book");
            variants++;
        }

        Assert.GreaterOrEqual(variants, 5, "the Mason's Frame, Frame Saw, Spinning Wheel and Nykk's dock variants");
    }

    // The user, 2026-11-22: "change herb rack fitting to reduce the amount of herbs needed in tonics by -1, as long as the required herb
    // is more than 1. We do not need separate recipes for this effect."
    [Test]
    public void TheHerbRack_CutsOneHerb_FromBrewsNeedingTwoOrMore_WithNoRecipeAsset()
    {
        BuildingModuleDefinition rack = Content.Modules.Single(m => m != null && m.name == "HerbRack");
        ItemDefinition herb = rack.OneFewer.First(item => item != null && item.name == "HealingHerb");
        RecipeDefinition draught = Recipe("HealingDraught");
        Assert.AreEqual(2, draught.ItemCost.Where(c => c.Item == herb).Sum(c => c.Amount), "the plain draught takes two herbs");
        var queue = new StationQueue("test", draught.Station, 20, 5) { Tier = Tiers.Max };
        Assert.AreSame(draught, RecipeVariants.OpenAt(draught, queue), "no rack: the plain recipe");

        queue.Modules = new[] { rack };
        RecipeDefinition thrifty = RecipeVariants.OpenAt(draught, queue);
        Assert.AreNotSame(draught, thrifty, "the rack: a cheaper way");
        Assert.IsTrue(thrifty.IsThriftierCopy);
        Assert.IsFalse(Content.Recipes.Contains(thrifty), "made from the recipe, no asset");
        Assert.AreSame(thrifty, RecipeVariants.OpenAt(draught, queue), "the same copy each time, so a worker and the queue agree");
        Assert.AreEqual(1, thrifty.ItemCost.Where(c => c.Item == herb).Sum(c => c.Amount), "one herb");
        Assert.AreSame(draught.OutputDefinition, thrifty.OutputDefinition);
        Assert.AreEqual(draught.ResourceCost.Sum(c => c.Amount), thrifty.ResourceCost.Sum(c => c.Amount), "the tincture as before");
        Assert.AreSame(rack, thrifty.NeedsModule);
        Assert.AreSame(draught, thrifty.Replaces);
        Assert.AreSame(thrifty, Content.FindRecipe(thrifty.Id), "a queued craft saved with it is found again");

        // Never below one, and only what the rack lists.
        ItemDefinition other = draught.ItemCost.Select(c => c.Item).FirstOrDefault(item => item != null && item != herb) ?? Recipe("Bandage").ItemCost[0].Item;
        var cut = RecipeDefinition.Fewer(new[] { new ItemAmount(herb, 1), new ItemAmount(herb, 3), new ItemAmount(other, 3) }, rack.OneFewer);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, cut.Select(c => c.Amount).ToArray());

        // Every cauldron brew with two or more of a listed herb is cheaper there; one with a single herb is left alone.
        int cheaper = 0;
        foreach (RecipeDefinition recipe in Content.Recipes.Where(r => r != null && r.Station == draught.Station && r.NeedsModule == null && r.Replaces == null))
        {
            bool needsTwo = recipe.ItemCost.Any(c => rack.OneFewer.Contains(c.Item) && c.Amount > 1);
            RecipeDefinition open = RecipeVariants.OpenAt(recipe, queue);
            Assert.AreEqual(needsTwo, open != recipe, recipe.name);
            cheaper += needsTwo ? 1 : 0;
        }

        Assert.GreaterOrEqual(cheaper, 4, "the healing, purified, bear and warming brews at least");
    }

    // The user, 2026-11-22: the Herb Rack's own Healing Draught became another draught, "one that heals 30 hp instantly and then 40 over
    // 10 seconds".
    [Test]
    public void TheMendingDraught_HealsThirtyAtOnce_ThenFortyOverTenSeconds_AtTheHerbRack()
    {
        RecipeDefinition recipe = Recipe("MendingDraught");
        Assert.IsNotNull(recipe);
        Assert.IsNull(recipe.Replaces, "its own draught, no longer a cheaper Healing Draught");
        Assert.AreEqual("HerbRack", recipe.NeedsModule.name);
        ItemDefinition draught = recipe.Output;
        Assert.AreEqual("Mending Draught", draught.DisplayName);
        // As shares of maximum health since 2026-11-22 (PowerCurveTests): the user's 30 and 40 at a tier I kit's 150 health.
        Assert.IsTrue(draught.HealAsShare);
        Assert.AreEqual(30f, draught.HealAtOnceFor(150f), 1f);
        Assert.AreEqual(40f, draught.HealAmountFor(150f), 1.5f);
        Assert.AreEqual(10f, draught.HealSeconds);
        Assert.AreEqual(CraftedGoods.Tonics, CraftedWork.Of(draught), "a tonic, for Potent Remedies and the Alchemy skill");
        string told = ItemTooltipView.DescribeRestores(draught, 150f);
        StringAssert.Contains("30 health at once", told);
        StringAssert.Contains("40 over 10 s", told);
        Assert.IsTrue(Content.Items.Contains(draught));
        Assert.AreEqual(1, Content.Recipes.Count(r => r != null && r.OutputDefinition != null && r.OutputDefinition.name == "HealingDraught"),
            "one Healing Draught recipe in the book again");
    }

    [Test]
    public void AStation_MakesTheVariantOnlyWithItsModule_AndAWorkerUsesIt()
    {
        RecipeDefinition plain = Recipe("OakPlank");
        RecipeDefinition variant = Content.Recipes.Single(r => r != null && r.Replaces == plain);
        var queue = new StationQueue("test", plain.Station, 20, 5) { Tier = Tiers.Max };
        Assert.AreSame(plain, RecipeVariants.OpenAt(plain, queue), "no module: the plain recipe");
        Assert.IsFalse(RecipeVariants.IsOpenAt(variant, queue));

        // Enough wood for the variant and not for the plain recipe: a worker makes planks only with the module fitted.
        var stash = new StashStorage();
        ItemDefinition wood = variant.ItemCost[0].Item;
        stash.Items.Add(wood, variant.ItemCost[0].Amount);
        var order = new WorkOrder { recipe = plain, stockTarget = 99, keepInput = 0 };
        Assert.IsNotNull(WorkerProduction.WhyNot(order, queue, stash), "4 wood for planks, and only 3 in the stash");

        queue.Modules = new[] { variant.NeedsModule };
        Assert.AreSame(variant, RecipeVariants.OpenAt(plain, queue));
        Assert.IsNull(WorkerProduction.WhyNot(order, queue, stash), "the Frame Saw's 3 are enough");
        queue.Tier = variant.RequiredStationTier - 1;
        if (variant.RequiredStationTier > 1)
        {
            Assert.AreSame(plain, RecipeVariants.OpenAt(plain, queue), "below its tier the variant isn't open");
        }
    }

    [Test]
    public void AStationsFineChance_MakesBetterGear()
    {
        ItemDefinition gear = Content.Recipes.First(r => r != null && r.Output != null && r.Output.HasDurability).Output;
        Assert.AreEqual(ItemQuality.Plain, CraftedWork.Make(gear, null, 0f).Quality, "no skill, no module: plain");
        Assert.AreNotEqual(ItemQuality.Plain, CraftedWork.Make(gear, null, 1f).Quality, "a sure chance: better than plain");
    }

    [Test]
    public void AQuestThatAsksForAModule_TeachesOneThatFits()
    {
        int checkedSteps = 0;
        foreach (QuestDefinition quest in Content.Quests.Where(q => q != null))
        {
            foreach (QuestObjective step in quest.Objectives.Where(o => o != null && o.Type == ObjectiveType.FitModule))
            {
                Assert.IsTrue(quest.UnlocksModules.Any(m => m != null && (step.Building == null || m.Fits(step.Building))),
                    $"{quest.name} asks for a module at the {(step.Building != null ? step.Building.name : "any building")} and teaches one that fits");
                checkedSteps++;
            }
        }

        Assert.Greater(checkedSteps, 0, "Copper Fire asks for the Storage Loft");
    }
}
