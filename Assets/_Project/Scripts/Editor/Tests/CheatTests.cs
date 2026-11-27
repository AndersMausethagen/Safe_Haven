using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The cheat menu's lists stay whole, and its switches leave the game as it was.</summary>
public sealed class CheatTests
{
    [TearDown]
    public void TearDown()
    {
        Cheats.SetFreeCosts(false);
        Cheats.SetNoRequirements(false);
    }

    private static System.Collections.Generic.IEnumerable<T> All<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    [Test]
    public void EveryCreatureRaidWeatherAndEssence_IsInTheMenu()
    {
        CheatSettings settings = CheatSettings.Current;
        foreach (MonsterDefinition monster in All<MonsterDefinition>().Where(one => one.Prefab != null))
        {
            CollectionAssert.Contains(settings.Creatures, monster, "add it to Resources/CheatSettings (BuildCheatMenu.cs is retired: a targeted edit)");
        }

        foreach (RaidDefinition raid in All<RaidDefinition>())
        {
            CollectionAssert.Contains(settings.Raids, raid, "add it to Resources/CheatSettings (BuildCheatMenu.cs is retired: a targeted edit)");
        }

        foreach (WeatherDefinition weather in All<WeatherDefinition>())
        {
            CollectionAssert.Contains(settings.Weathers, weather, "add it to Resources/CheatSettings (BuildCheatMenu.cs is retired: a targeted edit)");
        }

        foreach (ResourceDefinition essence in All<ResourceDefinition>())
        {
            CollectionAssert.Contains(settings.Essences, essence, "add it to Resources/CheatSettings (BuildCheatMenu.cs is retired: a targeted edit)");
        }
    }

    [Test]
    public void F1_OpensTheMenu_AndIsTheOnlyThingOnIt()
    {
        Assert.AreEqual("<Keyboard>/f1", GameInput.CheatMenu.bindings[0].path);
        foreach (UnityEngine.InputSystem.InputActionMap map in UnityEngine.InputSystem.InputSystem.actions.actionMaps)
        {
            foreach (UnityEngine.InputSystem.InputAction action in map.actions)
            {
                if (action != GameInput.CheatMenu)
                {
                    Assert.IsFalse(action.bindings.Any(binding => binding.path == "<Keyboard>/f1"), action.name);
                }
            }
        }
    }

    [Test]
    public void FreeCosts_PayNothing_AndStopWhenSwitchedOff()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var bag = new ItemContainer(4, 1);
            var pool = new MaterialPool(null, bag);
            var cost = new[] { new ItemAmount(item, 5) };
            Assert.IsFalse(pool.CanAfford(null, cost));

            Cheats.SetFreeCosts(true);
            Assert.IsTrue(pool.CanAfford(null, cost));
            Assert.IsTrue(pool.TrySpend(null, cost));

            Cheats.SetFreeCosts(false);
            Assert.IsFalse(pool.CanAfford(null, cost));
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void FreeCosts_LeaveWhatIsHeld()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var bag = new ItemContainer(4, 1);
            bag.Add(item, 3);
            Cheats.SetFreeCosts(true);
            Assert.IsTrue(new MaterialPool(null, bag).TrySpend(null, new[] { new ItemAmount(item, 2) }));
            Assert.AreEqual(3, bag.Count(item));
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void NoRequirements_OpensLockedBuildings_AndTiers()
    {
        var smithy = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Forge.asset");
        Assert.IsFalse(BuildingUnlocks.IsOpen(smithy));
        Cheats.SetNoRequirements(true);
        Assert.IsTrue(BuildingUnlocks.IsOpen(smithy));
    }

    [Test]
    public void LearnEverySkill_ReachesEverySkillInTheGame()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (SkillDefinition skill in All<SkillDefinition>())
        {
            Assert.IsTrue(content.Skills.Contains(skill), skill.name + " is missing from the content list, so the cheat can't teach it");
        }
    }

    [Test]
    public void GivenWithoutAPlayer_ThingsGoToTheStash()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            int before = GameSession.Stash.Items.Count(item);
            Cheats.Give(item, 4, false);
            Assert.AreEqual(before + 4, GameSession.Stash.Items.Count(item));
            GameSession.Stash.Items.Remove(item, 4);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }
}
