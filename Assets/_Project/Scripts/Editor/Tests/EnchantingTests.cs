using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Enchanting as a build (Docs/EnchantingResearch.md): learned from inscriptions, found by source, carved with Runecraft or Liv's hand,
/// counted once, fitted to slots, and kept apart from art runes. The balance budget is in RaidTierTests.
/// </summary>
public sealed class EnchantingTests
{
    private const string Folder = "Assets/_Project/Data/Essence/Enchantments/";
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp() => GameSession.StartNewGame();

    [TearDown]
    public void TearDown()
    {
        foreach (Object made in created)
        {
            if (made != null)
            {
                Object.DestroyImmediate(made);
            }
        }

        created.Clear();
        GameSession.StartNewGame();
    }

    private static EnchantmentDefinition E(string asset) => AssetDatabase.LoadAssetAtPath<EnchantmentDefinition>(Folder + asset + ".asset");

    private static ItemDefinition Item(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/" + path + ".asset");

    // ----- The catalogue -----

    [Test]
    public void ThereAreThirtyToFiftyEnchantments_EachFindableAndTheGreatOnesOnWeaponsShieldsAndJewellery()
    {
        IReadOnlyList<EnchantmentDefinition> all = EssenceSettings.Current.Enchantments;
        Assert.That(all.Count, Is.InRange(30, 50));
        Assert.AreEqual(all.Count, all.Select(e => e.Id).Distinct().Count());
        EnchantTarget verbs = EnchantTarget.Weapon | EnchantTarget.Bow | EnchantTarget.Staff | EnchantTarget.Shield | EnchantTarget.Jewelry
                              | EnchantTarget.Neck | EnchantTarget.Ring;
        foreach (EnchantmentDefinition enchantment in all)
        {
            Assert.IsNotEmpty(enchantment.SourceHint, enchantment.name);
            Assert.AreEqual(enchantment.Source == EnchantmentSource.Start, enchantment.KnownFromStart, $"{enchantment.name}: only the start's are known from it");
            if (enchantment.IsGreat)
            {
                Assert.AreEqual(EnchantTarget.None, enchantment.Targets & ~verbs, $"{enchantment.name}: great ones go on weapons, shields and jewellery");
            }
        }

        Assert.GreaterOrEqual(all.Count(e => e.IsGreat), 10);
        Assert.GreaterOrEqual(all.Count(e => (e.Targets & (EnchantTarget.Weapon | EnchantTarget.Armor | EnchantTarget.Jewelry | EnchantTarget.Tool)) == 0), 10,
            "slot-specific enchantments: boots, gloves, helmets, shields, bows...");
        Assert.AreEqual(14, all.Count(e => e.KnownInOldSaves), "old saves keep the first fourteen");
    }

    [Test]
    public void EveryEliteMarkAndGreatCreature_CarriesItsOwn()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EliteAffixDefinition"))
        {
            var affix = AssetDatabase.LoadAssetAtPath<EliteAffixDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsNotNull(affix.Inscription, affix.name);
            Assert.AreEqual(EnchantmentSource.Elite, affix.Inscription.Source, affix.name);
        }

        foreach (string prefab in new[] { "OldMossTroll", "StoneTroll", "Bergjutul", "TheHeartroot" })
        {
            var loot = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemies/{prefab}.prefab").GetComponent<LootOnDeath>();
            Assert.IsTrue(loot.Inscriptions.Count > 0 && loot.Inscriptions.All(e => e.Source == EnchantmentSource.Boss && e.IsGreat), prefab);
        }

        foreach (string site in new[] { "Raids/SetPieces/Standing Stones", "Raids/Events/Sites/Livs Circle", "Raids/Events/Sites/Living Stones" })
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{site}.prefab").GetComponentInChildren<CarvedStone>(true), site);
        }
    }

    [Test]
    public void SlotSpecific_FitsOnlyItsSlot_AndAShieldIsNoLongerAWeapon()
    {
        EnchantmentDefinition fleetfoot = E("Fleetfoot");
        Assert.IsTrue(fleetfoot.Fits(Item("Armor/CuredLeatherBoots")));
        Assert.IsFalse(fleetfoot.Fits(Item("Armor/CuredLeatherCap")));
        ItemDefinition shield = Item("Weapons/RoundShield");
        Assert.AreEqual(EnchantTarget.Shield, EnchantmentDefinition.TargetOf(shield));
        Assert.IsFalse(E("Bloodletting").Fits(shield), "one weapon enchantment per fighter, not two through the shield");
        Assert.IsTrue(E("Wardwood").Fits(shield));
        Assert.IsTrue(E("Pinning").Fits(Item("HuntersBow")));
        Assert.IsFalse(E("Pinning").Fits(Item("Weapons/CopperSword")));
    }

    [Test]
    public void ArtRunes_StayApart_FromEnchantments()
    {
        // Art runes attune a kind of weapon (ArtRunes); enchantments are carved into one piece and learned from inscriptions.
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.IsArtRune)
            {
                Assert.IsFalse(item.IsInscription, item.name);
                Assert.AreEqual(EnchantTarget.None, EnchantmentDefinition.TargetOf(item), $"{item.name}: an art rune takes no enchantment");
            }
        }

        foreach (EnchantmentDefinition enchantment in EssenceSettings.Current.Enchantments)
        {
            StringAssert.DoesNotContain("Rune", enchantment.DisplayName, "enchantments aren't called runes: the word belongs to art and raid runes");
        }
    }

    // ----- Learning -----

    [Test]
    public void ANewGame_KnowsOnlyTheStart_AndAnInscriptionReadAtHomeTeaches()
    {
        EnchantmentDefinition vengeful = E("Vengeful");
        Assert.IsTrue(GameSession.Progress.Knows(E("Mossgrown")));
        Assert.IsFalse(GameSession.Progress.Knows(vengeful));

        var bag = new ItemContainer(4, 1);
        bag.SetSlot(0, Inscriptions.Make(vengeful));
        Assert.IsTrue(bag.GetSlot(0).Item.IsInscription);
        Assert.IsTrue(Inscriptions.Read(null, bag, 0));
        Assert.IsTrue(GameSession.Progress.Knows(vengeful));
        Assert.IsTrue(bag.GetSlot(0).IsEmpty, "reading uses it up");

        // Read again, a copy is Runecraft practice instead.
        ProficiencyDefinition runecraft = EssenceSettings.Current.EnchantingSkill;
        int before = GameSession.Progress.ExperienceOf(runecraft);
        bag.SetSlot(0, Inscriptions.Make(vengeful));
        Assert.IsTrue(Inscriptions.Read(null, bag, 0));
        Assert.Greater(GameSession.Progress.ExperienceOf(runecraft), before);
    }

    [Test]
    public void AnOldSave_KnowsTheFirstFourteen_AndANewOneOnlyWhatItLearned()
    {
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow);
        data.progress.enchantments.Clear();
        data.progress.enchantmentsLearned = false;
        CharacterProgress old = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content).Progress;
        Assert.IsTrue(old.Knows(E("Bloodletting")) && old.Knows(E("Greedy")), "everything an old save could carve");
        Assert.IsFalse(old.Knows(E("Vengeful")));

        data.progress.enchantmentsLearned = true;
        data.progress.enchantments.Add("frenzied");
        CharacterProgress fresh = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content).Progress;
        Assert.IsFalse(fresh.Knows(E("Bloodletting")));
        Assert.IsTrue(fresh.Knows(E("Frenzied")));
        Assert.IsTrue(fresh.Knows(E("Mossgrown")));
    }

    [Test]
    public void ASource_GivesOneNotKnownFirst()
    {
        var pool = new List<EnchantmentDefinition> { E("Frenzied"), E("Ironhide") };
        GameSession.Progress.Learn(E("Frenzied"));
        for (int i = 0; i < 20; i++)
        {
            Assert.AreSame(E("Ironhide"), Inscriptions.Pick(pool, null, new System.Random(i)));
        }

        GameSession.Progress.Learn(E("Ironhide"));
        Assert.IsNull(Inscriptions.Pick(pool, null, new System.Random(1), copies: false));
        Assert.IsNotNull(Inscriptions.Pick(pool, null, new System.Random(1)), "a copy, for Liv or practice");
        Assert.IsTrue(Inscriptions.Pool(EnchantmentSource.CarvedStone, 1).All(e => e.Tier == 1));
        Assert.Greater(Inscriptions.Pool(EnchantmentSource.CarvedStone, 4).Count, Inscriptions.Pool(EnchantmentSource.CarvedStone, 1).Count);
    }

    [Test]
    public void Carving_NeedsRunecraft_OrLivsHand()
    {
        EnchantmentDefinition rallying = E("Rallying");
        Assert.Greater(rallying.RequiredRunecraft, 0);
        Assert.IsFalse(Inscriptions.CanCarve(rallying, out _, out string problem));
        StringAssert.Contains("Runecraft", problem);

        var liv = AssetDatabase.FindAssets("t:ResidentDefinition Liv").Select(id => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(id)))
            .First(r => r.name == "Liv");
        var job = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/RuneScholar.asset");
        Assert.IsTrue(job.Carves);
        GameSession.Settlement.AddResident(liv, job);
        ResidentRecord record = GameSession.Settlement.Find(liv);
        record.WorkDone = 1000;
        Assert.AreEqual(job.CarvesPastAt(4), Inscriptions.CarverReach(record), "a master carves furthest");
        EnchantmentDefinition frenzied = E("Frenzied");
        Assert.IsTrue(Inscriptions.CanCarve(frenzied, out bool byCarver, out _));
        Assert.AreEqual(frenzied.RequiredRunecraft > 0, byCarver);
    }

    // ----- Counted once -----

    [Test]
    public void TheSameEnchantment_OnTwoPieces_CountsOnce()
    {
        var holder = new GameObject("Wearer");
        created.Add(holder);
        StatSheet stats = holder.AddComponent<Combatant>().Stats;
        var gear = new GearSet();
        gear.Bind(stats);
        EnchantmentDefinition springfed = E("Springfed");
        float plain = stats.Get(StatType.MaxStamina);
        ItemDefinition ring = Item("Jewelry/CopperBand");
        gear.Slots.SetSlot((int)GearSlot.Finger1, new ItemStack(ring, 1, 1f, 0, springfed));
        float one = stats.Get(StatType.MaxStamina);
        Assert.Greater(one, plain);
        gear.Slots.SetSlot((int)GearSlot.Finger2, new ItemStack(ring, 1, 1f, 0, springfed));
        float two = stats.Get(StatType.MaxStamina);
        gear.Slots.SetSlot((int)GearSlot.Finger2, new ItemStack(ring, 1));
        float plainSecond = stats.Get(StatType.MaxStamina);
        Assert.AreEqual(plainSecond, two, 0.01f, "a second piece's copy adds nothing");
        gear.Slots.SetSlot((int)GearSlot.Finger2, new ItemStack(ring, 1, 1f, 0, springfed));
        gear.Slots.SetSlot((int)GearSlot.Finger1, new ItemStack(ring, 1));
        Assert.AreEqual(two, stats.Get(StatType.MaxStamina), 0.01f, "the other piece takes it over");
        Assert.IsTrue(GearPower.CountsEnchantment(gear.Slots, (int)GearSlot.Finger2));
    }

    [Test]
    public void TheGearScore_CountsAnEnchantmentOnce_AsTheStatsDo()
    {
        // Review 2026-11-16: each enchanted piece added Per Enchantment to the score, while the stats take it once.
        var gear = new GearSet();
        EnchantmentDefinition springfed = E("Springfed");
        ItemDefinition ring = Item("Jewelry/CopperBand");
        gear.Slots.SetSlot((int)GearSlot.Finger1, new ItemStack(ring, 1, 1f, 0, springfed));
        gear.Slots.SetSlot((int)GearSlot.Finger2, new ItemStack(ring, 1));
        int once = GearScore.Of(gear);
        gear.Slots.SetSlot((int)GearSlot.Finger2, new ItemStack(ring, 1, 1f, 0, springfed));
        Assert.AreEqual(once, GearScore.Of(gear), "a second copy of the enchantment scores nothing");
        gear.Slots.SetSlot((int)GearSlot.Finger1, new ItemStack(ring, 1));
        Assert.AreEqual(once, GearScore.Of(gear), "the other piece takes the score over");
    }

    [Test]
    public void AnInscription_KeepsItsEnchantment_IntoABagAndAChest()
    {
        // Review 2026-11-16: AddStack (every Grant, a pickup) and TransferTo (Store) re-made a parchment by kind, blank.
        EnchantmentDefinition vengeful = E("Vengeful");
        var bag = new ItemContainer(4, 1);
        var chest = new ItemContainer(4, 1);
        Assert.AreEqual(1, bag.AddStack(Inscriptions.Make(vengeful)));
        Assert.AreEqual(vengeful, bag.GetSlot(0).Enchantment, "into the bag whole");
        Assert.AreEqual(1, bag.TransferTo(0, chest));
        Assert.AreEqual(vengeful, chest.GetSlot(0).Enchantment, "into a chest whole");
        Assert.IsTrue(bag.GetSlot(0).IsEmpty);
    }

    // ----- Liv -----

    [Test]
    public void LivsQuests_TeachEnchanting_InOrder()
    {
        string[] chain = { "TheRuneScholar", "TheOldMarks", "Rubbings", "MarksOfTheMarked", "AHandForTheShelf", "TheGreatWork" };
        for (int i = 1; i < chain.Length; i++)
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/_Project/Data/Quests/{chain[i]}.asset");
            Assert.IsNotNull(quest, chain[i]);
            Assert.AreEqual(chain[i - 1], quest.Prerequisites.Single().name, chain[i]);
            Assert.IsEmpty(quest.FindProblems(), chain[i]);
            Assert.AreEqual(QuestRank.Haven, quest.Rank);
        }

        var first = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/TheOldMarks.asset");
        StringAssert.Contains("carved stones", first.JournalNotes, "her lesson says where enchantments are found");
        Assert.IsTrue(first.Objectives.Any(o => o.Type == ObjectiveType.EnchantItem));
    }
}
