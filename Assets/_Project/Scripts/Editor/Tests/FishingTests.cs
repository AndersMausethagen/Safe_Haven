using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FishingTests
{
    [Test]
    public void Reel_FillsWhileTheZoneIsOnTheFish_AndDrainsWhenItIsNot()
    {
        var held = new ReelSession(0f, 0.3f, 2.6f, 2.2f, 0.32f, 0.15f, 1);
        Assert.IsTrue(held.InZone);
        float start = held.Catch;
        for (int i = 0; i < 10; i++)
        {
            held.Tick(0.05f, held.Zone + 0.15f < held.Fish);
        }

        Assert.Greater(held.Catch, start);

        var dropped = new ReelSession(0f, 0.2f, 2.6f, 2.2f, 0.32f, 0.15f, 1);
        for (int i = 0; i < 400 && !dropped.Over; i++)
        {
            dropped.Tick(0.05f, false);
        }

        Assert.IsTrue(dropped.Escaped, "letting go entirely loses the fish");
    }

    [Test]
    public void Reel_CanBeWonByFollowingTheFish()
    {
        var reel = new ReelSession(0.5f, 0.28f, 2.6f, 2.2f, 0.32f, 0.15f, 7);
        for (int i = 0; i < 2000 && !reel.Over; i++)
        {
            reel.Tick(0.02f, reel.Zone + reel.ZoneSize * 0.5f < reel.Fish);
        }

        Assert.IsTrue(reel.Caught);
    }

    [Test]
    public void FishBiteByWater_AndTheAuthoredTableHasFishForBoth()
    {
        FishingSettings settings = FishingSettings.Current;
        Assert.GreaterOrEqual(settings.FishList.Count, 4);
        Assert.IsTrue(settings.FishList.All(f => f.item != null && f.item.IsConsumable), "fish can be eaten raw");
        for (float roll = 0f; roll <= 1f; roll += 0.05f)
        {
            Assert.AreNotEqual(FishWater.Still, settings.PickFish(false, roll).water, "no still-water fish in rivers");
            Assert.AreNotEqual(FishWater.Flowing, settings.PickFish(true, roll).water, "no river fish in ponds");
        }

        Assert.IsTrue(settings.BaitList.Any(b => b.item != null && b.item.name == "Grubs"));
        Assert.Greater(settings.NokkenChance, 0f);
        Assert.NotNull(settings.MetNokken);
        Assert.NotNull(settings.NokkenModel);
    }

    [Test]
    public void TheRodIsAWeaponThatFishes_AndIsCraftedByHand()
    {
        var rod = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/FishingRod.asset");
        Assert.IsTrue(rod.IsWeapon);
        Assert.IsTrue(rod.Weapon.IsFishingRod);
        Assert.IsTrue(rod.Weapon.TakesAttackInput, "attack casts instead of swinging");
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/FishingRod.asset");
        Assert.AreSame(rod, recipe.Output);
        Assert.IsTrue(recipe.IsHandcrafted);
        Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab").GetComponent<PlayerFishing>());
    }

    [Test]
    public void SafeHaven_HasAStillTarnAndTheRiver()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/SafeHaven.unity");
        try
        {
            WaterSurface[] water = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaterSurface>(true)).ToArray();
            Assert.IsTrue(water.Any(w => w.IsStill), "a pond for the still-water fish and the Nøkken");
            Assert.IsTrue(water.Any(w => !w.IsStill), "the river");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
