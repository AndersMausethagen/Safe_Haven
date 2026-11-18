using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>The firefly lantern: worn in the Light slot, a day of light kept as its condition, refilled with a firefly, made
/// by hand from sticks and a firefly; and the fireflies that can be caught to fill it.</summary>
public sealed class LanternTests
{
    private static ItemDefinition Lantern => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/PrimitiveFireflyLantern.asset");
    private static ItemDefinition Firefly => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Firefly.asset");

    [Test]
    public void TheLantern_IsWornInTheLightSlot_AndKeepsItsLightLeft()
    {
        ItemDefinition lantern = Lantern;
        Assert.IsTrue(lantern.IsLantern && lantern.IsEquippable && lantern.KeepsCondition && !lantern.Stackable);
        Assert.IsTrue(GearSlots.Accepts(GearSlot.Light, lantern));
        Assert.IsFalse(GearSlots.Accepts(GearSlot.OffHand, lantern), "both hands stay free");
        Assert.IsFalse(GearSlots.Accepts(GearSlot.Light, AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HandTorch.asset")));
        Assert.IsTrue(GearSlots.TryFindSlot(lantern, null, out GearSlot slot) && slot == GearSlot.Light);

        Assert.AreEqual(24f, lantern.Lantern.Hours, "a day of light");
        var full = new ItemStack(lantern, 1);
        Assert.AreEqual(24f, full.LightHours, 1e-3f);
        Assert.AreEqual(6f, full.WithLightHours(6f).LightHours, 1e-3f);
        Assert.AreEqual(0f, full.WithLightHours(-3f).LightHours, 1e-3f);
        Assert.AreSame(Firefly, lantern.Lantern.Refill);
        Assert.NotNull(lantern.Lantern.Model);
        Assert.NotNull(lantern.Icon);
    }

    [Test]
    public void TheLanternsLight_BreathesButNeverGoesOut()
    {
        LanternDefinition light = Lantern.Lantern;
        for (float t = 0f; t < 30f; t += 0.37f)
        {
            float pulse = light.Pulse(t, 3f);
            Assert.That(pulse, Is.InRange(1f - light.Flicker - 1e-4f, 1.6f));
        }
    }

    [Test]
    public void TheLantern_IsMadeByHand_FromSticksAndAFirefly()
    {
        // What it is made of; how many is the recipe's to tune.
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/PrimitiveFireflyLantern.asset");
        Assert.AreSame(Lantern, recipe.Output);
        Assert.IsTrue(recipe.IsHandcrafted && recipe.KnownFromStart);
        Assert.IsTrue(recipe.ItemCost.Any(c => c.Item != null && c.Item.name == "Sticks" && c.Amount > 0), "sticks");
        Assert.IsTrue(recipe.ItemCost.Any(c => c.Item == Firefly && c.Amount > 0), "a firefly");
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Assert.Contains(recipe, book.Recipes.ToList());
    }

    [Test]
    public void Fireflies_CanBeCaught_MoreAtNight_FewerInRain()
    {
        foreach (string name in new[] { "Fireflies - Haven", "Fireflies - Misty Woods" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<FireflyProfile>($"Assets/_Project/Data/World/{name}.asset");
            Assert.AreSame(Firefly, profile.Catchable, name);
            Assert.NotNull(profile.CatchableGlow, name);
            Assert.Greater(profile.CatchableFor(1f, 0f), profile.CatchableFor(0f, 0f), name + ": more at night");
            Assert.Greater(profile.CatchableFor(1f, 0f), 0, name + ": not too rare");
            Assert.Less(profile.CatchableFor(1f, 1f), profile.CatchableFor(1f, 0f), name + ": they shelter from rain");
            Assert.That(profile.LightShare, Is.InRange(0.05f, 0.5f), name + ": some gather at lights, not all");
            Assert.LessOrEqual(profile.CountFor(1f, 0f), 170, name + ": the swarm a little less thick");
        }
    }

    [Test]
    public void ThePlayerWearsLanterns_AndThePrologueHandsOneOutInsteadOfATorch()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        Assert.NotNull(player.GetComponent<PlayerLantern>());
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity");
        try
        {
            StoryScript road = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).Single(s => s.name == "Story - The Road East");
            GiveItemsNode gift = road.Nodes.OfType<GiveItemsNode>().Single(n => n.equip);
            Assert.IsTrue(gift.items.Any(i => i.Item == Lantern), "the lantern, worn");
            Assert.IsFalse(gift.items.Any(i => i.Item != null && i.Item.name == "HandTorch"), "no torch to hold");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
