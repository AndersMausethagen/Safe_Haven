using System.Linq;
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
            Assert.That(stats.feedsNeeded, Is.InRange(2, 4), "2-4 feeds to tame");
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
    public void TheBreakingRide_AsksEverySixToFourteenSeconds_UntilFedEnough()
    {
        HorseDefinition kind = Wild;
        var random = new System.Random(9);
        float total = 0f;
        for (int i = 0; i < 100; i++)
        {
            float ask = HorseTamingRules.NextAsk(kind, random);
            Assert.That(ask, Is.InRange(6f, 14f));
            total += ask;
        }

        // About half a minute for a horse of three feeds (the user found 10-30 s, 3-5 times, too long).
        Assert.Less(total / 100f * 3f, 40f);
        HorseSettings settings = HorseSettings.Current;
        Assert.Greater(settings.MountReach, 2.6f, "getting on works from where the horse's menu opens");
        Vector4 start = settings.MountPoint(0f, false), end = settings.MountPoint(1f, false);
        Assert.Less(start.x, -0.3f, "climbing on from the left starts beside its shoulder");
        Assert.Less(start.y, -0.4f, "on the ground, below the saddle");
        Assert.AreEqual(0f, end.magnitude, 0.01f, "and ends in the saddle");
        Assert.AreEqual(-start.x, settings.MountPoint(0f, true).x, 0.001f, "from the right it is mirrored");
        Assert.That((float)new SerializedObject(Wild.Prefab.GetComponent<HorseMotion>()).FindProperty("turnInPlaceRate").floatValue, Is.InRange(50f, 140f),
            "it turns on the spot at its clip's pace");

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
        // The pony (the wild horse's breed since 2026-11-13): its eyes are in its body, its mane and tail are one hair mesh.
        Assert.AreEqual(2, shown.Length, "a living pony: body and hair; no armour or reins");
        var sway = prefab.GetComponentInChildren<HairSway>();
        Assert.IsNotNull(sway, "its mane and tail swing");
        Assert.AreEqual(7, sway.ChainCount, "six mane bones and the tail");
        var saddleName = new SerializedObject(prefab).FindProperty("saddleModel").objectReferenceValue as GameObject;
        Assert.AreEqual("Pony Saddle", saddleName != null ? saddleName.name : null, "the pony wears its own saddle, fitted to its back");
        foreach (string hoof in new[] { "L Finger0", "R Finger0", "L Toe0", "R Toe0" })
        {
            Assert.IsTrue(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == hoof), $"{hoof}: HoofPrints presses a print where it lands");
        }
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
        foreach ((string file, int slots) in new[] { ("LeatherSaddle", 12), ("ReinforcedSaddle", 16) })
        {
            var bigger = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/{file}.asset");
            Assert.AreEqual(slots, bigger.SaddleSlots, file);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<RecipeDefinition>($"Assets/_Project/Data/Crafting/Recipes/{file}.asset"), file + " can be made");
        }

        foreach (string file in new[] { "PaintedBarding", "BlackenedBarding", "SilveredBarding" })
        {
            var barding = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/{file}.asset");
            Assert.IsTrue(barding.IsBarding, file);
            Assert.That(barding.HorseArmour, Is.InRange(0.15f, 0.4f), file);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<RecipeDefinition>($"Assets/_Project/Data/Crafting/Recipes/{file}.asset"), file + " can be made");
        }

        Assert.IsTrue(kind.Barley.IsHorseTreat, "barley is what it eats from the hand");
    }

    [Test]
    public void Temperaments_AreRolled_AndChangeTheTaming()
    {
        HorseSettings settings = HorseSettings.Current;
        var seen = new System.Collections.Generic.HashSet<HorseTemperament>();
        var sexes = new System.Collections.Generic.HashSet<HorseSex>();
        var random = new System.Random(5);
        for (int i = 0; i < 200; i++)
        {
            HorseStats stats = HorseStats.Roll(Wild, random);
            seen.Add(stats.temperament);
            sexes.Add(stats.sex);
            Assert.IsTrue(stats.IsGrown, "a horse met wild is grown");
            Assert.That(stats.mane, Is.InRange(0, Wild.Manes.Length - 1), "it wears one of its kind's manes");
        }

        Assert.AreEqual(4, seen.Count, "calm, skittish, stubborn and bold all turn up");
        Assert.AreEqual(2, sexes.Count, "mares and stallions");
        Assert.Less(settings.Temperament(HorseTemperament.Calm).shyness, settings.Temperament(HorseTemperament.Skittish).shyness, "a calm horse lets you nearer");
        Assert.Greater(settings.Temperament(HorseTemperament.Stubborn).fight, settings.Temperament(HorseTemperament.Calm).fight, "a stubborn one fights longer");
        Assert.Greater(HorseTamingRules.TapsToHold(0.5f, Wild, settings.Temperament(HorseTemperament.Stubborn).pull),
            HorseTamingRules.TapsToHold(0.5f, Wild, settings.Temperament(HorseTemperament.Calm).pull), "and pulls harder");
        Assert.Less(HorseTamingRules.TapsToHold(0.5f, Wild, 1f, 1.4f), HorseTamingRules.TapsToHold(0.5f, Wild), "Horsemanship's hold makes each tap count for more");
        var stubborn = new HorseStats { feedsNeeded = 4 };
        Assert.AreEqual(5, HorseTamingRules.FeedsNeeded(stubborn, settings.Temperament(HorseTemperament.Stubborn).feeds));
        Assert.AreEqual(3, HorseTamingRules.FeedsNeeded(stubborn, settings.Temperament(HorseTemperament.Calm).feeds));
    }

    [Test]
    public void LeaningAway_HoldsTheRope_AndSteering_KeepsTheSeat()
    {
        Vector3 fromHorse = Vector3.back;
        Assert.AreEqual(1f, HorseTamingRules.Lean(Vector3.back, fromHorse), 0.001f, "straight away from it counts in full");
        Assert.AreEqual(0f, HorseTamingRules.Lean(Vector3.forward, fromHorse), "toward it, not at all");
        Assert.AreEqual(0f, HorseTamingRules.Lean(Vector3.right, fromHorse), "nor across");
        float strain = 0.5f, stamina = 100f;
        HorseTamingRules.Struggle(ref strain, ref stamina, 0f, 1f, 0.5f, Wild);
        float alone = strain;
        strain = 0.5f;
        HorseTamingRules.Struggle(ref strain, ref stamina, 3f, 1f, 0.5f, Wild);
        Assert.Less(strain, alone, "a second of leaning (three taps' worth) takes strain off");

        Vector3 veer = Vector3.right;
        Assert.Greater(HorseTamingRules.Balance(0.5f, veer, Vector3.left, 1f, 0.3f, 0.35f, 1f), 0.5f, "steering against the veer wins balance back");
        Assert.Less(HorseTamingRules.Balance(0.5f, veer, Vector3.zero, 1f, 0.3f, 0.35f, 1f), 0.5f, "letting it be loses it");
        Assert.Less(HorseTamingRules.Balance(0.5f, veer, Vector3.zero, 1f, 0.3f, 0.35f, 1.3f),
            HorseTamingRules.Balance(0.5f, veer, Vector3.zero, 1f, 0.3f, 0.35f, 0.7f), "a hard bucker throws you sooner");
        Assert.AreEqual(0, HorseMotion.HitSide(Vector3.forward, new Vector3(-1f, 0f, 2f)), "struck at the front left");
        Assert.AreEqual(3, HorseMotion.HitSide(Vector3.forward, Vector3.right), "on its right");
        Assert.AreEqual(5, HorseMotion.HitSide(Vector3.forward, new Vector3(1f, 0f, -2f)), "at the back right");
    }

    [Test]
    public void TheRope_RewardsALeanCaughtAfterTheTell_AndPunishesLeaningWithIt()
    {
        Assert.IsTrue(HorseTamingRules.CaughtPull(0.2f, 0.8f, 0.7f), "hard against it, just after the pull starts");
        Assert.IsFalse(HorseTamingRules.CaughtPull(-0.1f, 1f, 0.7f), "not during the tell, before it pulls");
        Assert.IsFalse(HorseTamingRules.CaughtPull(1.2f, 1f, 0.7f), "nor long after");
        Assert.IsFalse(HorseTamingRules.CaughtPull(0.2f, 0.3f, 0.7f), "nor a half-hearted lean");
        Assert.IsTrue(HorseTamingRules.LeansWith(Vector3.forward, Vector3.forward), "the keys with the pull give it rope");
        Assert.IsFalse(HorseTamingRules.LeansWith(Vector3.back, Vector3.forward));
        Assert.AreEqual(1f, HorseTamingRules.Lean(Vector3.back, -Vector3.forward), 0.001f, "against the pull counts in full");
    }

    [Test]
    public void Grounds_PathsAreQuick_MudIsSlow_AndTraitsComeDownToFoals()
    {
        HorseSettings settings = HorseSettings.Current;
        Assert.Greater(settings.GroundSpeedOf("Haven Path", out bool pathSoft), 1f, "a path is quicker");
        Assert.IsFalse(pathSoft);
        Assert.Less(settings.GroundSpeedOf("Muddy Bank", out bool mudSoft), 0.85f, "mud is slower");
        Assert.IsTrue(mudSoft, "and soft, for a bog-strider");
        Assert.AreEqual(1f, settings.GroundSpeedOf("Haven Meadow", out _), "a meadow is as it is");
        Assert.Less(Wild.TrotSpeed, Wild.CanterSpeed, "the canter is between the trot");
        Assert.Less(Wild.CanterSpeed, Wild.GallopSpeed.x, "and the gallop");
        var random = new System.Random(3);
        int kept = 0;
        for (int i = 0; i < 400; i++)
        {
            HorseTrait foal = HorseBreeding.InheritTraits(HorseTrait.Brave, HorseTrait.Swimmer, random);
            Assert.AreEqual(HorseTrait.None, foal & ~(HorseTrait.Brave | HorseTrait.Swimmer), "only what its parents have");
            kept += (foal & HorseTrait.Brave) != 0 ? 1 : 0;
        }

        Assert.That(kept, Is.InRange(150, 250), "each trait half the time");
        HorseDefinition toxic = System.Linq.Enumerable.First(settings.SpecialHorses, k => k != null && k.Home == HorseHome.Bog);
        Assert.IsTrue(toxic.Has(HorseAbility.BogStrider), "the bog's horse strides through mud");
    }

    [Test]
    public void Bond_GrowsWithCare_WearsDownWithout_AndGivesWhatItSays()
    {
        HorseSettings settings = HorseSettings.Current;
        Assert.AreEqual(100f, HorseBondRules.Add(95f, 20f), "never more than full");
        Assert.AreEqual(10f - 2f * settings.BondLostPerIdleDay, HorseBondRules.AfterIdle(10f, 2.6f, settings), 0.001f, "whole idle days wear it down");
        Assert.Greater(HorseBondRules.StaminaScale(100f, settings), HorseBondRules.StaminaScale(0f, settings), "more stamina at full bond");
        Assert.Less(HorseBondRules.PanicScale(100f, settings), HorseBondRules.PanicScale(0f, settings), "and less panic");
        Assert.IsFalse(HorseBondRules.HearsWhistle(settings.WhistleBond - 1f, 5f, settings), "a horse that hardly knows you won't come");
        Assert.IsTrue(HorseBondRules.HearsWhistle(100f, settings.WhistleReach.y - 1f, settings), "a fond one hears from far");
        Assert.IsFalse(HorseBondRules.HearsWhistle(settings.WhistleBond, settings.WhistleReach.y - 1f, settings), "a new one only from near");
        Assert.IsFalse(HorseBondRules.Throws(settings.ThrowsBelowBond, HorseTemperament.Skittish, settings, 0f), "a fond horse never throws you");
        Assert.IsTrue(HorseBondRules.Throws(0f, HorseTemperament.Skittish, settings, 0.1f), "a new skittish one may");
        Assert.Greater(settings.ExperienceFor(HorseWork.Tame), settings.ExperienceFor(HorseWork.Feed), "taming trains Horsemanship most");
    }

    [Test]
    public void Foals_TakeAfterTheirParents_AndGrowUp()
    {
        HorseSettings settings = HorseSettings.Current;
        var random = new System.Random(11);
        HorseStats mare = HorseStats.Roll(Wild, random);
        mare.sex = HorseSex.Mare;
        HorseStats stallion = HorseStats.Roll(Wild, random);
        stallion.sex = HorseSex.Stallion;
        Assert.IsTrue(HorseBreeding.CanBreed(mare, stallion));
        Assert.IsFalse(HorseBreeding.CanBreed(mare, mare), "two mares don't");
        HorseStats young = mare;
        young.growth = 0.6f;
        Assert.IsFalse(HorseBreeding.CanBreed(young, stallion), "nor a foal");
        for (int i = 0; i < 50; i++)
        {
            HorseStats foal = HorseBreeding.Foal(mare, stallion, Wild, settings, random);
            float average = (mare.gallopSpeed + stallion.gallopSpeed) * 0.5f;
            Assert.That(foal.gallopSpeed, Is.InRange(average * (1f - settings.FoalSpread) - 0.1f, average * (1f + settings.FoalSpread) + 0.1f), "its parents' speed, give or take");
            Assert.That(foal.coat, Is.EqualTo(mare.coat).Or.EqualTo(stallion.coat), "one parent's coat");
            Assert.AreEqual(1, foal.generation);
            Assert.IsFalse(foal.IsGrown, "born a foal");
        }

        Assert.AreEqual(settings.FoalSize, HorseBreeding.GrowthAfter(settings.FoalSize, 0f, settings), 0.001f);
        Assert.AreEqual(1f, HorseBreeding.GrowthAfter(settings.FoalSize, settings.DaysToGrow, settings), 0.001f, "grown after Days To Grow");
    }

    [Test]
    public void TheToxicHorse_LivesInTheBog_AndTheStable_IsBuilt()
    {
        HorseSettings settings = HorseSettings.Current;
        HorseDefinition toxic = System.Linq.Enumerable.FirstOrDefault(settings.SpecialHorses, k => k != null && k.Home == HorseHome.Bog);
        Assert.IsNotNull(toxic, "a special horse of the bog");
        Assert.IsTrue(toxic.IsSpecial && toxic.AlwaysArmoured);
        Assert.IsNotNull(toxic.Prefab);
        Assert.Greater(toxic.GallopSpeed.x, Wild.GallopSpeed.x, "faster than the common horse");
        Assert.IsTrue(toxic.IsAbroadAt(22f) && !toxic.IsAbroadAt(12f), "out at night, not at noon");
        Assert.Greater(toxic.HomeOffset.magnitude, 100f, "the bog lies well out from the Haven");
        Assert.IsNotNull(toxic.Prefab.GetComponentInChildren<ParticleSystem>(true), "its breath is a fog");
        var armour = new SerializedObject(toxic.Prefab).FindProperty("armourModel").objectReferenceValue as Renderer;
        Assert.IsTrue(armour != null && armour.gameObject.activeSelf, "it wears its barding");
        Assert.AreEqual(5, Wild.Manes.Length, "five manes for the common horse");
        foreach (SoundDefinition sound in new[] { Wild.Whinny, Wild.Neigh, Wild.Snort, Wild.Breathing, Wild.Hooves, settings.Whistle })
        {
            Assert.IsNotNull(sound, "the pack's horse sounds");
        }

        var stable = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Stable.asset");
        Assert.IsNotNull(stable);
        HorseStable keep = stable.Prefab.GetComponent<HorseStable>();
        Assert.IsNotNull(keep, "the stable keeps horses");
        Assert.AreEqual(4, keep.Stalls);
        Assert.IsNotNull(stable.Prefab.GetComponent<Interactable>(), "and has a menu");
    }

    [Test]
    public void TheHorseWindow_HasItsArt_AndEveryIconItsButtonsAskFor()
    {
        HorsePanelArt art = HorsePanelArt.Current;
        Assert.IsNotNull(art, "Resources/HorsePanelArt: BuildHorses --entry BuildHorses.PanelArt");
        Assert.IsNotNull(art.Corner);
        Assert.IsNotNull(art.Card);
        Assert.IsNotNull(art.CardRim);
        Assert.AreNotEqual(Vector4.zero, art.Card.border, "the card is 9-sliced");
        foreach (string icon in new[] { "Heart", "Bolt", "Weight", "Horseshoe", "Bond", "Info", "Stats", "Star", "Wheat", "Brush", "Gear",
                     "HorseHead", "Lead", "Stable", "Saddle", "Hourglass", "Pencil", "Barding", "Bags", "Wheel", "Stay", "Graze" })
        {
            Assert.IsNotNull(art.Icon(icon), icon);
        }
    }
}
