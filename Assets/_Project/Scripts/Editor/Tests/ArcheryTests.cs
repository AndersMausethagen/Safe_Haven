using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class ArcheryTests
{
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Weapons = "Assets/_Project/Data/Combat/Weapons/";
    private const string Arrows = "Assets/_Project/Data/Combat/Arrows/";

    // Each tier's bow with its best arrow against the tier's sword (damage x 1.6, the melee ratio per second): full draws let go late near
    // half the sword, every release perfect near nine tenths (Docs/RangedStealthResearch.md §3.5). The bow trades pace for range.
    [TestCase("CrudeWoodenBow", "CrudeStoneArrow", "WornSword")]
    [TestCase("HuntersBow", "BodkinArrow", "CopperSword")]
    [TestCase("ElderwoodLongbow", "BodkinArrow", "IronSword")]
    [TestCase("MoonstrungBow", "BodkinArrow", "MoonsilverBlade")]
    [TestCase("TrollhornBow", "BodkinArrow", "TrollstoneBlade")]
    public void Bows_GrowWithTheTiersBelowTheSword(string bowName, string arrowName, string swordName)
    {
        var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Weapons + bowName + ".asset");
        var arrow = AssetDatabase.LoadAssetAtPath<ArrowDefinition>(Arrows + arrowName + ".asset");
        var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Weapons + swordName + ".asset");
        Assert.IsNotNull(bow, bowName);
        Assert.IsNotNull(arrow, arrowName);
        Assert.IsNotNull(sword, swordName);
        float swordPerSecond = sword.Damage * 1.6f;
        float late = ArcheryRules.DamagePerSecond(arrow.Damage, bow.DrawWeight, bow.DrawSeconds, bow.ReloadSeconds, bow.PerfectReleaseDamage, 1.5f, false) / swordPerSecond;
        float perfect = ArcheryRules.DamagePerSecond(arrow.Damage, bow.DrawWeight, bow.DrawSeconds, bow.ReloadSeconds, bow.PerfectReleaseDamage, 1.5f, true) / swordPerSecond;
        Assert.That(late, Is.InRange(0.45f, 0.62f), $"{bowName}: full draws let go late at {late:P0} of the {swordName}");
        Assert.That(perfect, Is.InRange(0.8f, 0.98f), $"{bowName}: every release perfect at {perfect:P0} of the {swordName}");
    }

    [Test]
    public void Bows_DrawNoSlowerAsTheyGetBetter()
    {
        string[] order = { "CrudeWoodenBow", "HuntersBow", "ElderwoodLongbow", "MoonstrungBow", "TrollhornBow" };
        for (int i = 1; i < order.Length; i++)
        {
            var before = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Weapons + order[i - 1] + ".asset");
            var after = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Weapons + order[i] + ".asset");
            Assert.That(after.DrawSeconds + after.ReloadSeconds, Is.LessThanOrEqualTo(before.DrawSeconds + before.ReloadSeconds + 0.06f), $"{order[i]} after {order[i - 1]}");
        }
    }

    [Test]
    public void ALongerDrawFliesFasterHitsHarderAndStraysLess()
    {
        Assert.AreEqual(0f, ArcheryRules.DrawShare(0f, 1f));
        Assert.AreEqual(0.5f, ArcheryRules.DrawShare(0.5f, 1f), 0.001f);
        Assert.AreEqual(1f, ArcheryRules.DrawShare(3f, 1f));
        Assert.Less(ArcheryRules.SpeedShare(0.3f), ArcheryRules.SpeedShare(1f));
        Assert.Less(ArcheryRules.DamageShare(0.3f), ArcheryRules.DamageShare(1f));
        Assert.AreEqual(1f, ArcheryRules.DamageShare(1f));
        Assert.Greater(ArcheryRules.SpreadDegrees(2f, 0.3f, 0.3f, 1f), ArcheryRules.SpreadDegrees(2f, 1f, 1f, 1f));
        Assert.AreEqual(2f, ArcheryRules.SpreadDegrees(2f, 1f, 1f + ArcheryRules.SteadySeconds, 1f), 0.001f, "steady for a while at full draw");
        Assert.Greater(ArcheryRules.SpreadDegrees(2f, 1f, 3f + ArcheryRules.SteadySeconds, 1f), 2f, "then the arms shake");
    }

    [TestCase(20f, 0f)]
    [TestCase(35f, -3f)]
    [TestCase(15f, 4f)]
    public void TheLaunchArcLandsOnTheAimPoint(float distance, float height)
    {
        const float speed = 30f;
        const float gravity = 6f;
        Vector3 from = new Vector3(2f, 1f, -1f);
        Vector3 target = from + new Vector3(distance * 0.6f, height, distance * 0.8f);
        Vector3 velocity = ArcheryRules.LaunchDirection(from, target, speed, gravity) * speed;

        // Fly it: where is the arrow when it has covered the horizontal distance?
        float flat = new Vector2(velocity.x, velocity.z).magnitude;
        float time = distance / flat;
        Vector3 at = from + velocity * time + Vector3.down * (0.5f * gravity * time * time);
        Assert.Less(Vector3.Distance(at, target), 0.05f);
        Assert.Greater(velocity.y, 0f, "a falling arrow is aimed a little high");
    }

    [Test]
    public void ATargetOutOfReachGetsTheFarthestShot()
    {
        Vector3 direction = ArcheryRules.LaunchDirection(Vector3.zero, new Vector3(0f, 0f, 500f), 20f, 9.81f);
        Assert.AreEqual(45f, Vector3.Angle(direction, Vector3.forward), 0.5f);
    }

    [Test]
    public void AStrayShotStaysInsideTheSpread()
    {
        Vector3 aim = new Vector3(1f, 0.2f, 0.5f).normalized;
        for (int i = 0; i < 20; i++)
        {
            Vector3 shot = ArcheryRules.Stray(aim, 3f, i / 20f, (i * 7 % 20) / 20f);
            Assert.LessOrEqual(Vector3.Angle(aim, shot), 3.001f);
        }

        Assert.AreEqual(aim, ArcheryRules.Stray(aim, 0f, 0.5f, 1f));
    }

    [Test]
    public void AnArrowSticksInWhatItHits()
    {
        var arrowModel = AssetDatabase.LoadAssetAtPath<ArrowProjectile>("Assets/_Project/Prefabs/Effects/Combat/CrudeStoneArrowProjectile.prefab");
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var arrow = (ArrowProjectile)PrefabUtility.InstantiatePrefab(arrowModel);
        try
        {
            wall.transform.position = new Vector3(0f, 1f, 5f);
            arrow.transform.position = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();
            arrow.Launch(null, null, null, 10f, Vector3.forward * 30f);
            for (int i = 0; i < 30 && arrow.IsFlying; i++)
            {
                arrow.Fly(0.02f);
            }

            Assert.IsFalse(arrow.IsFlying, "stuck in the wall");
            Assert.Less(arrow.transform.position.z, 4.5f, "the nock (its pivot) stays outside the wall's face");
            Assert.Greater(arrow.transform.position.z, 3.3f, "the tip is buried, not the whole shaft");
            Assert.Greater(Vector3.Dot(arrow.transform.forward, Vector3.forward), 0.99f);
        }
        finally
        {
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(arrow.gameObject);
        }
    }

    [Test]
    public void TheCrudeBowAndArrowsAreReady()
    {
        var bow = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "CrudeWoodenBow.asset");
        var arrow = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "CrudeStoneArrow.asset");
        Assert.IsTrue(bow.IsWeapon && bow.Weapon.IsBow && bow.IsEquippable);
        Assert.AreEqual(WeaponStance.Bow, bow.Weapon.Stance);
        Assert.IsTrue(bow.Weapon.RequiresBothHands);
        Assert.IsNotNull(bow.Weapon.ShotAttack);
        Assert.Greater(bow.Weapon.DrawSeconds, 0f);
        Assert.Greater(bow.Weapon.ArrowSpeed, 10f);
        Assert.IsNotNull(bow.HeldModel);
        Assert.IsNotNull(bow.Icon);

        Assert.IsTrue(arrow.IsArrow && arrow.Stackable && !arrow.IsEquippable);
        Assert.Greater(arrow.Arrow.Damage, 0f);
        Assert.IsNotNull(arrow.Arrow.Projectile);
        Assert.IsNotNull(arrow.Icon);
    }

    [Test]
    public void APerfectReleaseIsJustAfterTheDrawFills()
    {
        Assert.IsFalse(ArcheryRules.IsPerfectRelease(0.9f, 1f, 0.2f), "not yet full");
        Assert.IsTrue(ArcheryRules.IsPerfectRelease(1f, 1f, 0.2f));
        Assert.IsTrue(ArcheryRules.IsPerfectRelease(1.15f, 1f, 0.2f));
        Assert.IsFalse(ArcheryRules.IsPerfectRelease(1.3f, 1f, 0.2f), "held too long");
        Assert.IsFalse(ArcheryRules.IsPerfectRelease(1f, 1f, 0f), "a bow without a window has none");
    }

    [Test]
    public void SpecialArrowsAndTheHuntersBowAreReady()
    {
        var fire = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "FireArrow.asset");
        var whistle = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "WhistlingArrow.asset");
        var hunter = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "HuntersBow.asset");
        var crude = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "CrudeWoodenBow.asset");
        Assert.IsTrue(fire.IsArrow && fire.Arrow.Burns);
        Assert.AreEqual(0f, fire.Arrow.RecoverChance, "fire arrows burn up");
        Assert.IsNotNull(fire.Arrow.Projectile.GetComponentInChildren<VisibilitySource>(), "a fire arrow lights the fog");
        Assert.IsTrue(whistle.IsArrow && !whistle.Arrow.Burns);
        Assert.Greater(whistle.Arrow.ImpactNoise, 2f * AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "CrudeStoneArrow.asset").Arrow.ImpactNoise,
            "a whistling arrow is heard far away");
        Assert.IsTrue(hunter.IsWeapon && hunter.Weapon.IsBow);
        Assert.AreEqual(2, hunter.Tier);
        Assert.Less(hunter.Weapon.DrawSeconds, crude.Weapon.DrawSeconds);
        Assert.Greater(hunter.Weapon.ArrowSpeed, crude.Weapon.ArrowSpeed);
        Assert.Less(hunter.Weapon.Spread, crude.Weapon.Spread);
        Assert.IsNotNull(fire.Icon);
        Assert.IsNotNull(whistle.Icon);
        Assert.IsNotNull(hunter.Icon);
    }

    [Test]
    public void TheHuntersBowIsMadeAtTheHuntersLodge()
    {
        RecipeDefinition recipe = RecipeFor("HuntersBow");
        Assert.AreEqual(CraftingStationType.Tannery, recipe.Station);
        Assert.IsTrue(recipe.KnownFromStart);
    }

    private static RecipeDefinition RecipeFor(string output)
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        RecipeDefinition recipe = book.Recipes.FirstOrDefault(r => r != null && r.Output != null && r.Output.name == output);
        Assert.IsNotNull(recipe, $"{output} is in the recipe book");
        return recipe;
    }

    [TestCase("CrudeWoodenBow", "Sticks")]
    [TestCase("CrudeStoneArrow", "Sticks", "SmallRocks")]
    [TestCase("FireArrow", "CrudeStoneArrow", "Flax")]
    [TestCase("WhistlingArrow", "CrudeStoneArrow", "Bone")]
    public void TheyAreCraftedByHandFromTheBag(string output, params string[] materials)
    {
        RecipeDefinition recipe = RecipeFor(output);
        Assert.IsTrue(recipe.IsHandcrafted && recipe.KnownFromStart);
        foreach (string material in materials)
        {
            Assert.IsTrue(recipe.ItemCost.Any(c => c.Item != null && c.Item.name == material), $"{output} uses {material}");
        }
    }

    [Test]
    public void ThePlayerCanShootAndTheAnimatorHasTheBowLayer()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        Assert.IsNotNull(player.GetComponent<PlayerArchery>());
        Assert.IsNotNull(player.GetComponent<PlayerArcheryAnimation>());
        Assert.IsNotNull(player.GetComponent<HeldBowView>());
        Assert.IsNotNull(player.GetComponent<ArcheryCameraAim>());
        Assert.IsNotNull(new SerializedObject(player.GetComponent<ArcheryTrajectoryView>()).FindProperty("lineMaterial").objectReferenceValue);
        var outline = new SerializedObject(player.GetComponent<ArcheryTargetOutline>());
        Assert.IsNotNull(outline.FindProperty("outlineMaterial").objectReferenceValue);
        Assert.IsNotNull(outline.FindProperty("maskMaterial").objectReferenceValue);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        AnimatorControllerLayer layer = controller.layers.FirstOrDefault(l => l.name == PlayerArcheryAnimation.LayerName);
        Assert.IsNotNull(layer);
        Assert.IsNotNull(layer.avatarMask);
        string[] states = layer.stateMachine.states.Select(s => s.state.name).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "Bow Idle", "Bow Load", "Bow Hold", "Bow Release" }, states);
        Assert.IsTrue(layer.stateMachine.states.All(s => s.state.motion != null));
        int bow = System.Array.IndexOf(controller.layers.Select(l => l.name).ToArray(), PlayerArcheryAnimation.LayerName);
        int upper = System.Array.IndexOf(controller.layers.Select(l => l.name).ToArray(), "Upper Body");
        Assert.Less(bow, upper, "guarding, eating and knockbacks show over the bow");
        foreach (string parameter in new[] { "BowDrawing", "BowShot", "DrawSpeed", "ReloadSpeed" })
        {
            Assert.IsTrue(controller.parameters.Any(p => p.name == parameter), parameter);
        }
    }
}
