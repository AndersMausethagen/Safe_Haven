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

        Assert.GreaterOrEqual(variants, 5, "the Mason's Frame, Frame Saw, Spinning Wheel and Herb Rack variants");
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
