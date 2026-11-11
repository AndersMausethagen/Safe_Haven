using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Horses: rolled stats, the struggle and the breaking ride's numbers, and what BuildHorses made.</summary>
public sealed class HorseTests
{
    private static HorseDefinition Wild => HorseSettings.Current.WildHorse;

    [Test]
    public void EveryHorse_RollsItsOwnStats_InsideItsKindsRanges()
    {
        HorseDefinition kind = Wild;
        Assert.IsNotNull(kind, "HorseSettings names the wild horse");
        var random = new System.Random(5);
        bool differ = false;
        HorseStats first = HorseStats.Roll(kind, random);
        for (int i = 0; i < 200; i++)
        {
            HorseStats stats = HorseStats.Roll(kind, random);
            Assert.That(stats.maxHealth, Is.InRange(kind.Health.x, kind.Health.y));
            Assert.That(stats.maxStamina, Is.InRange(kind.Stamina.x, kind.Stamina.y));
            Assert.That(stats.gallopSpeed, Is.InRange(kind.GallopSpeed.x - 0.05f, kind.GallopSpeed.y + 0.05f));
            Assert.That(stats.carryWeight, Is.InRange(kind.CarryWeight.x, kind.CarryWeight.y));
            Assert.That(stats.feedsNeeded, Is.InRange(3, 5), "3-5 feeds to tame");
            Assert.That(stats.coat, Is.InRange(-1, kind.Coats.Length - 1));
            Assert.That(stats.Quality(kind), Is.InRange(0f, 1f));
            differ |= !Mathf.Approximately(stats.gallopSpeed, first.gallopSpeed);
        }

        Assert.IsTrue(differ, "horses differ");
    }

    [Test]
    public void TheStruggle_IsHeldByTapping_AndWonByTiringIt()
    {
        HorseDefinition kind = Wild;
        float strain = 0.25f, stamina = kind.StruggleStamina;
        HorseTamingRules.Struggle(ref strain, ref stamina, 0, 1f, 0f, kind);
        Assert.Greater(strain, 0.25f, "untapped, the rope strains");
        Assert.AreEqual(kind.StruggleStamina, stamina, "and the horse doesn't tire");

        float hold = HorseTamingRules.TapsToHold(1f, kind);
        Assert.That(hold, Is.InRange(2.5f, 6.5f), "a person can tap fast enough to hold even the best horse");
        Assert.Greater(hold, HorseTamingRules.TapsToHold(0f, kind), "a better horse pulls harder");

        // Six taps a second against the strongest horse: it gives in before the rope slips.
        strain = 0.25f;
        stamina = kind.StruggleStamina;
        float seconds = 0f;
        while (!HorseTamingRules.GivenIn(stamina) && !HorseTamingRules.Slipped(strain) && seconds < 60f)
        {
            HorseTamingRules.Struggle(ref strain, ref stamina, 1, 1f / 6f, 1f, kind);
            seconds += 1f / 6f;
        }

        Assert.IsTrue(HorseTamingRules.GivenIn(stamina), $"won after {seconds:0.0} s");
        Assert.That(seconds, Is.InRange(3f, 15f), "a struggle of seconds, not a chore");
        Assert.IsTrue(HorseTamingRules.Slipped(1f));
    }

    [Test]
    public void TheBreakingRide_AsksEveryTenToThirtySeconds_UntilFedEnough()
    {
        HorseDefinition kind = Wild;
        var random = new System.Random(9);
        for (int i = 0; i < 100; i++)
        {
            Assert.That(HorseTamingRules.NextAsk(kind, random), Is.InRange(10f, 30f));
        }

        var stats = new HorseStats { feedsNeeded = 4 };
        Assert.IsFalse(HorseTamingRules.IsBroken(3, stats));
        Assert.IsTrue(HorseTamingRules.IsBroken(4, stats));
    }

    [Test]
    public void TheWildHorse_IsBuilt_WithEverythingItNeeds()
    {
        HorseDefinition kind = Wild;
        Horse prefab = kind.Prefab;
        Assert.IsNotNull(prefab, "the definition has its prefab");
        Assert.IsNotNull(prefab.GetComponent<HorseMotion>());
        Assert.IsNotNull(prefab.GetComponent<HorseTaming>());
        Assert.IsNotNull(prefab.GetComponent<NavMeshAgent>());
        Assert.IsNotNull(prefab.GetComponent<Damageable>());
        Assert.IsNotNull(prefab.GetComponent<Interactable>());
        Assert.AreEqual(Faction.Ally, prefab.GetComponent<Combatant>().Faction, "monsters fight it as one of the player's side");
        var entity = prefab.GetComponent<SaveableEntity>();
        Assert.IsFalse(string.IsNullOrEmpty(entity.PrefabId), "a tamed horse can be recreated after loading");
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        Assert.AreEqual(entity, content.FindPrefab(entity.PrefabId));
        Assert.AreNotEqual(prefab.transform, prefab.Seat, "the rider sits on its back");
        Assert.IsNotNull(prefab.GetComponentInChildren<Animator>().runtimeAnimatorController);
        // A prefab asset is in no scene, so nothing in it is active in a hierarchy: what is switched on is what shows.
        Renderer[] shown = System.Array.FindAll(prefab.GetComponentsInChildren<Renderer>(true), r => r.gameObject.activeSelf);
        Assert.AreEqual(4, shown.Length, "a living horse: body, mane, tail and eyes; no armour or reins");
        var saddleModel = new SerializedObject(prefab).FindProperty("saddleModel").objectReferenceValue as GameObject;
        Assert.IsNotNull(saddleModel, "the saddle it wears once saddled");
        Assert.IsFalse(saddleModel.activeSelf, "and not before");
        float[] gaits = (float[])typeof(HorseMotion).GetField("gaitSpeeds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(prefab.GetComponent<HorseMotion>());
        Assert.AreEqual(5, gaits.Length, "walk, trot, canter, gallop, sprint");
        for (int i = 1; i < gaits.Length; i++) Assert.Greater(gaits[i], gaits[i - 1], "each gait faster than the last");
        Assert.That(HorseMotion.GaitFor(kind.TrotSpeed, gaits), Is.InRange(3, 4), "ridden, it canters or gallops");
        Assert.AreEqual(5, HorseMotion.GaitFor(kind.GallopSpeed.x, gaits), "spurred on, it sprints");
        Assert.Less(kind.ShyDistance + 1.5f * kind.PerHeardMetre, kind.LassoRange, "a sneaking player gets within the lasso's reach");
        Assert.Greater(kind.ShyDistance + 5f * kind.PerHeardMetre, kind.LassoRange, "a walking one doesn't");
    }

    [Test]
    public void TheLassoAndTheSaddle_AreMade_AndDoWhatTheySay()
    {
        HorseDefinition kind = Wild;
        Assert.IsNotNull(kind.Lasso);
        Assert.IsTrue(kind.Lasso.IsLasso);
        Assert.IsTrue(kind.Lasso.IsUsable, "used from the bag or the skill bar");
        Assert.IsNotNull(kind.Barley);
        var saddle = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HideSaddle.asset");
        Assert.IsTrue(saddle.IsSaddle);
        Assert.AreEqual(8, saddle.SaddleSlots);
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/HideSaddle.asset");
        Assert.AreEqual(CraftingStationType.Workbench, recipe.Station, "made at the Workshop");
        Assert.AreEqual(saddle, recipe.Output);
        Assert.AreEqual(10, recipe.ItemCost[0].Amount);
        Assert.AreEqual("Rough Hide", recipe.ItemCost[0].Item.DisplayName);
        var lasso = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/Lasso.asset");
        Assert.AreEqual(kind.Lasso, lasso.Output);
    }
}
