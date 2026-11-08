using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FishingTests
{
    private static FishingSettings.FightTuning Tuning => FishingSettings.Current.Fight;

    // Plays a fight with a simple angler: reels while the fish isn't running and the line isn't near breaking, gives line
    // on runs, and (when leaning) holds the rod against the run. Returns the outcome and the seconds it took.
    private static FishFight.Outcome Play(float fight, float size, bool leaning, out float seconds, float control = 1f, int seed = 3,
        System.Func<FishFight, bool> reel = null)
    {
        var fish = new FishFight(Tuning, fight, size, control, 9f, seed);
        const float step = 0.02f;
        seconds = 0f;
        for (int i = 0; i < 9000 && !fish.Over; i++, seconds += step)
        {
            bool reeling = reel != null ? reel(fish) : fish.Current != FishFight.Mood.Running && fish.Tension < 0.9f;
            fish.Tick(step, reeling, leaning ? fish.Counter : 0f);
        }

        return fish.Result;
    }

    [Test]
    public void Fight_ReelingOnRestsAndGivingLineOnRuns_LandsEveryFish()
    {
        foreach (float fight in new[] { 0.2f, 0.35f, 0.55f, 0.75f })
        {
            foreach (float size in new[] { 0f, 1f })
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    Assert.AreEqual(FishFight.Outcome.Landed, Play(fight, size, true, out float seconds, seed: seed), $"fight {fight}, size {size}, seed {seed}");
                    Assert.Less(seconds, 75f, $"a fight is a minute or so at most (fight {fight}, size {size})");
                    Assert.Greater(seconds, 6f, "and it is a fight, not a tug");
                }
            }
        }
    }

    [Test]
    public void Fight_IsForgiving_WithoutLeaningTheRodItStillLands()
    {
        foreach (float fight in new[] { 0.2f, 0.55f, 0.75f })
        {
            Assert.AreEqual(FishFight.Outcome.Landed, Play(fight, 0.5f, false, out _), $"fight {fight}");
        }
    }

    [Test]
    public void Fight_LeaningAgainstTheRuns_TiresTheFishSooner()
    {
        Play(0.75f, 1f, true, out float leaning);
        Play(0.75f, 1f, false, out float straight);
        Assert.Less(leaning, straight * 0.9f);
    }

    [Test]
    public void Fight_ReelingThroughEveryRun_SnapsTheLine()
    {
        Assert.AreEqual(FishFight.Outcome.Snapped, Play(0.75f, 1f, false, out _, reel: fish => true));
    }

    [Test]
    public void Fight_NeverReeling_LosesTheFish()
    {
        FishFight.Outcome outcome = Play(0.35f, 0.5f, false, out _, reel: fish => false);
        Assert.That(outcome, Is.EqualTo(FishFight.Outcome.ThrewHook).Or.EqualTo(FishFight.Outcome.Spooled));
    }

    [Test]
    public void Fight_TheSkillsLineControl_LetsTheLineTakeMore()
    {
        // Reeling through the runs of a strong fish: a practised angler's line holds for longer before it goes.
        Play(0.75f, 1f, false, out float beginner, 0.8f, reel: fish => true);
        Play(0.75f, 1f, false, out float master, 1.3f, reel: fish => true);
        Assert.Greater(master, beginner);
    }

    [Test]
    public void Fight_AFishWarnsBeforeEveryRun_AndTheHudSaysWhatToDo()
    {
        var fish = new FishFight(Tuning, 0.5f, 0.5f, 1f, 9f, 5);
        FishFight.Mood last = fish.Current;
        int runs = 0;
        bool sawSpent = false;
        for (int i = 0; i < 6000 && !fish.Over; i++)
        {
            if (fish.Spent)
            {
                sawSpent = true;
                StringAssert.Contains("tired", FishingHud.HintFor(fish, true));
            }

            fish.Tick(0.02f, fish.Current != FishFight.Mood.Running && fish.Tension < 0.9f, fish.Counter);
            if (fish.Current == FishFight.Mood.Running && last != FishFight.Mood.Running)
            {
                Assert.AreEqual(FishFight.Mood.Warning, last, "a run comes after a warning");
                runs++;
            }

            Assert.IsFalse(string.IsNullOrEmpty(FishingHud.HintFor(fish, false)));
            last = fish.Current;
        }

        Assert.Greater(runs, 1);
        Assert.IsTrue(sawSpent, "it tires before it is landed");
        Assert.AreEqual(FishFight.Outcome.Landed, fish.Result);
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
