using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Crafting by hand takes time by what is made (the user, 2026-11-15: weapons 20 s, small weapons 10 s), and a
/// put-away weapon is worn on the back or at the hip by its kind.</summary>
public sealed class HandCraftingTests
{
    private static T Load<T>(string name) where T : UnityEngine.Object
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == name) return asset;
        }

        Assert.Fail("No " + typeof(T).Name + " named " + name);
        return null;
    }

    [Test]
    public void HandCrafts_TakeTheirTime()
    {
        // The order the user set (a weapon takes longest, a small weapon or tool half that, a salve less, arrows least), not the
        // seconds themselves, which are HandCraftingSettings' to tune.
        HandCraftingSettings rules = HandCraftingSettings.Current;
        float twoHanded = rules.SecondsFor(Load<RecipeDefinition>("StoneAxe"));
        Assert.AreEqual(twoHanded, rules.SecondsFor(Load<RecipeDefinition>("CrudeWoodenBow")), "a bow takes as long as a two-handed weapon");
        float oneHanded = rules.SecondsFor(Load<RecipeDefinition>("WoodsmansAxe"));
        Assert.Less(oneHanded, twoHanded, "a one-handed weapon is quicker");
        Assert.AreEqual(oneHanded, rules.SecondsFor(Load<RecipeDefinition>("StoneHatchet")), "a tool takes as long as a one-handed weapon");
        float salve = rules.SecondsFor(Load<RecipeDefinition>("HealingSalve"));
        Assert.Less(salve, oneHanded, "a salve is quicker still");
        Assert.Less(rules.SecondsFor(Load<RecipeDefinition>("CrudeStoneArrow")), salve, "a bundle of arrows is quickest");
        Assert.Less(rules.WalkShare, 1f, "the player walks slower while crafting");
    }

    [Test]
    public void PutAwayWeapons_AreWornByKind()
    {
        Assert.AreEqual(true, StowedWeaponSettings.WornOnBack(Load<WeaponDefinition>("IronGreatsword")), "greatsword on the back");
        Assert.AreEqual(true, StowedWeaponSettings.WornOnBack(Load<WeaponDefinition>("RunicStaff")), "staff on the back");
        Assert.AreEqual(true, StowedWeaponSettings.WornOnBack(Load<WeaponDefinition>("HuntersBow")), "bow on the back");
        Assert.AreEqual(false, StowedWeaponSettings.WornOnBack(Load<WeaponDefinition>("WornSword")), "one-handed sword at the hip");
        Assert.IsNull(StowedWeaponSettings.WornOnBack(null));
    }

    [Test]
    public void ARefund_GoesBackWhereItWasPaidFrom()
    {
        // Review 2026-11-16: a cancelled craft paid from the stash came back into the bag, filling it (and the rest fell).
        var pelt = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var bag = new ItemContainer(4, 1);
            var stash = new ItemContainer(4, 1);
            bag.Add(pelt, 2);
            stash.Add(pelt, 5);
            var pool = new MaterialPool(null, bag, null, stash);

            Assert.IsTrue(pool.TrySpend(null, new[] { new ItemAmount(pelt, 4) }, out MaterialPool.Payment paid));
            Assert.AreEqual(0, bag.Count(pelt), "carried things are spent first");
            Assert.AreEqual(3, stash.Count(pelt));

            pool.GiveBack(paid, null);
            Assert.AreEqual(2, bag.Count(pelt), "the bag's share goes back to the bag");
            Assert.AreEqual(5, stash.Count(pelt), "the stash's share goes back to the stash");
        }
        finally
        {
            Object.DestroyImmediate(pelt);
        }
    }

    [Test]
    public void ADelivery_GoesToTheStashWhenTheBagIsFull()
    {
        var pelt = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var bag = new ItemContainer(1, 1);
            var stash = new ItemContainer(4, 1);
            bag.Add(pelt, pelt.MaximumStack - 1);

            ItemDelivery.Delivery got = ItemDelivery.Give(bag, new ItemStack(pelt, 3), stash, null);
            Assert.AreEqual(1, got.Bag);
            Assert.AreEqual(2, got.Stash);
            Assert.IsFalse(got.Fell);
        }
        finally
        {
            Object.DestroyImmediate(pelt);
        }
    }

    [Test]
    public void ASavedQueue_ComesBackPaid_WithItsCraftUnderWay()
    {
        // Review 2026-11-16: every save (F5, autosaves) stopped the crafts and gave the materials back; now a save keeps them.
        RecipeDefinition axe = Load<RecipeDefinition>("StoneAxe");
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        var saved = new HandCraftSaveData { elapsed = 7f };
        saved.crafts.Add(new HandCraftEntrySaveData { recipe = axe.Id });
        saved.crafts.Add(new HandCraftEntrySaveData { recipe = axe.Id });
        var holder = new GameObject("Crafter");
        try
        {
            HandCraftQueue queue = holder.AddComponent<HandCraftQueue>();
            queue.Restore(JsonUtility.FromJson<HandCraftSaveData>(JsonUtility.ToJson(saved)), content);
            Assert.AreEqual(axe, queue.Current);
            Assert.AreEqual(1, queue.Queued);
            Assert.AreEqual(7f / HandCraftingSettings.Current.SecondsFor(axe), queue.Progress, 0.01f, "it goes on from where it was");

            HandCraftSaveData again = queue.CaptureState();
            Assert.AreEqual(2, again.crafts.Count);
            Assert.AreEqual(7f, again.elapsed, 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }
}
