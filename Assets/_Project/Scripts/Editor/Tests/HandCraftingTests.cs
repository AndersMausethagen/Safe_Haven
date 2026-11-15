using NUnit.Framework;
using UnityEditor;

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
        HandCraftingSettings rules = HandCraftingSettings.Current;
        Assert.AreEqual(20f, rules.SecondsFor(Load<RecipeDefinition>("StoneAxe")), "a two-handed weapon");
        Assert.AreEqual(20f, rules.SecondsFor(Load<RecipeDefinition>("CrudeWoodenBow")), "a bow");
        Assert.AreEqual(10f, rules.SecondsFor(Load<RecipeDefinition>("WoodsmansAxe")), "a one-handed weapon");
        Assert.AreEqual(10f, rules.SecondsFor(Load<RecipeDefinition>("StoneHatchet")), "a tool");
        Assert.AreEqual(5f, rules.SecondsFor(Load<RecipeDefinition>("HealingSalve")), "a salve");
        Assert.Less(rules.SecondsFor(Load<RecipeDefinition>("CrudeStoneArrow")), 5f, "a bundle of arrows is quick");
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
}
