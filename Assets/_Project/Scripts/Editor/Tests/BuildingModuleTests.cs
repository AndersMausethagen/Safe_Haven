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
    public void EveryModule_FitsSomething_AndIsTaughtSomewhere()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Assert.Greater(Content.Modules.Count, 0);
        foreach (BuildingModuleDefinition module in Content.Modules)
        {
            Assert.IsTrue(book.Buildings.Any(module.Fits), module.name + " fits a building");
            Assert.IsTrue(module.KnownFromStart || Content.Quests.Any(q => q != null && q.UnlocksModules.Contains(module)), module.name + " is taught");
            Assert.IsNotEmpty(module.Effect(), module.name + " does something");
            Assert.IsTrue(module.Cost.All(c => c.Item != null && c.Amount > 0), module.name + "'s cost");
        }
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
