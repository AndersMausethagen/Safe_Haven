using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The Lost (LostOneRules, LostOneSettings, the roster's save): who rises and what it is called from the roster's seed, nothing before the
/// story gate, which one walks a raid from the run's seed, rising a rank with what the death took, fleeing and what the player learns,
/// being laid to rest and the place it leaves, the body its rank wears, the name sent to friends, and the roster through a save.
/// </summary>
public sealed class LostOneTests
{
    private sealed class Lookup : IContentLookup
    {
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();

        public ItemDefinition FindItem(string id) => id != null && Items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
    }

    private readonly List<Object> created = new List<Object>();

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
    }

    // ----- Rising -----

    [Test]
    public void TheSameRoster_RaisesTheSameLost_EachNamedApart()
    {
        LostOneSettings settings = Settings();
        var a = new LostOneRoster();
        var b = new LostOneRoster();
        List<LostOneRecord> first = LostOneRules.Refill(a, settings, true, 1234);
        List<LostOneRecord> second = LostOneRules.Refill(b, settings, true, 1234);

        Assert.AreEqual(settings.RosterSize, first.Count, "the roster fills to its size");
        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i].FullName, second[i].FullName, "the same seed, the same name");
            Assert.AreEqual(first[i].PastLife, second[i].PastLife);
            CollectionAssert.AreEqual(first[i].Traits, second[i].Traits);
            Assert.AreEqual("lost-" + (i + 1), first[i].Id);
            Assert.AreEqual(1, first[i].Rank);
            Assert.AreEqual(settings.RankAt(1).traits, first[i].Traits.Count, "its first rank's traits");
            CollectionAssert.DoesNotContain(first[i].Traits, "Late", "only traits usable at its rank as a tier");
            Assert.IsFalse(string.IsNullOrWhiteSpace(first[i].Epithet));
            StringAssert.StartsWith("once ", first[i].PastLife);
        }

        Assert.AreEqual(first.Count, first.Select(record => record.GivenName).Distinct().Count(), "no two share a name");
        Assert.AreEqual(0, LostOneRules.Refill(a, settings, true, 99).Count, "a full roster raises none");

        var other = new LostOneRoster();
        LostOneRules.Refill(other, settings, true, 98765);
        Assert.IsFalse(Enumerable.Range(0, first.Count).All(i => other.All[i].FullName == first[i].FullName), "another character, other Lost");
    }

    [Test]
    public void BeforeTheStoryGate_NoneRises_AndNoneWalks()
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        created.Add(quest);
        LostOneSettings settings = Settings(gate: quest);
        var log = new QuestLog();
        var roster = new LostOneRoster();

        Assert.IsFalse(LostOneRules.GateOpen(settings, log, 50), "the story isn't there yet");
        Assert.AreEqual(0, LostOneRules.Refill(roster, settings, LostOneRules.GateOpen(settings, log, 50), 5).Count);
        Assert.AreEqual(0, roster.All.Count);
        Assert.AreEqual(0, roster.Seed, "nothing rolled before the gate");
        Assert.IsNull(LostOneRules.WhoWalks(roster, settings, 77, 1, false));

        log.Restore(quest, QuestStatus.Completed, new int[0]);
        Assert.IsFalse(LostOneRules.GateOpen(settings, log, settings.MinimumExtractions - 1), "too few raids come home from");
        Assert.IsTrue(LostOneRules.GateOpen(settings, log, settings.MinimumExtractions));
        Assert.Greater(LostOneRules.Refill(roster, settings, true, 5).Count, 0, "then they rise");
    }

    [Test]
    public void AFreshSettingsAsset_KeepsTheLostBehindAFewRaids()
    {
        var settings = ScriptableObject.CreateInstance<LostOneSettings>();
        created.Add(settings);
        Assert.IsTrue(settings.Enabled);
        Assert.Greater(settings.MinimumExtractions, 0);
        Assert.IsFalse(LostOneRules.GateOpen(settings, new QuestLog(), 0), "not on the first raid");
        Assert.AreEqual(5, settings.MaxRank);
        Assert.Greater(settings.GivenNames.Count, settings.RosterSize * 3);
        Assert.Greater(settings.ScentLines.Count, 0);
        Assert.IsFalse(settings.HasABody, "no body until the asset names one");
    }

    // ----- Walking a raid -----

    [Test]
    public void WhoWalks_IsDecidedByTheRunsSeed_OneAtMost_NeverInABossFight()
    {
        LostOneSettings settings = Settings(chance: 1f);
        var roster = new LostOneRoster();
        LostOneRules.Refill(roster, settings, true, 42);

        var chosen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            LostOneRecord walks = LostOneRules.WhoWalks(roster, settings, seed, 1, false);
            Assert.IsNotNull(walks, "a sure chance");
            Assert.AreSame(walks, LostOneRules.WhoWalks(roster, settings, seed, 1, false), "the briefing and the raid agree");
            chosen.Add(walks.Id);
        }

        Assert.Greater(chosen.Count, 1, "each of them walks some raids");
        Assert.IsNull(LostOneRules.WhoWalks(roster, settings, 3, 1, true), "never in a boss encounter");
        Assert.IsNull(LostOneRules.WhoWalks(roster, Settings(chance: 0f), 3, 1, false), "no chance, none walks");
        Assert.IsNull(LostOneRules.WhoWalks(roster, Settings(chance: 1f, withBody: false), 3, 1, false), "no body, none walks");

        foreach (LostOneRecord record in roster.All)
        {
            record.Status = LostOneStatus.LaidToRest;
        }

        Assert.IsNull(LostOneRules.WhoWalks(roster, settings, 3, 1, false), "the laid to rest walk no more");
    }

    [Test]
    public void AHigherRank_WalksMoreOften()
    {
        LostOneSettings settings = Settings(chance: 0.3f);
        Assert.Greater(settings.ChanceFor(1, 4), settings.ChanceFor(1, 1));
        Assert.LessOrEqual(settings.ChanceFor(4, settings.MaxRank), settings.MostChance);
    }

    [Test]
    public void TheBriefing_NamesOnlyOneAlreadyMet()
    {
        LostOneSettings settings = Settings();
        var record = new LostOneRecord { Id = "lost-1", GivenName = "Halvard", Epithet = "Hollow-Eyed", PastLife = "once the Haven's smith" };
        Assert.IsNull(LostOneRules.BriefingLine(record, "Misty Woods", settings), "one not met comes unannounced");

        record.Met = true;
        string line = LostOneRules.BriefingLine(record, "Misty Woods", settings);
        StringAssert.Contains("Halvard Hollow-Eyed", line);
        StringAssert.Contains("the Misty Woods", line);

        record.Carried.Add(new ItemStack(Item("Axe", 1, 20), 1));
        StringAssert.Contains(settings.BriefingCarriesLine, LostOneRules.BriefingLine(record, "Misty Woods", settings));
    }

    // ----- Growing -----

    [Test]
    public void KillingThePlayer_RaisesItARank_WithANewTrait_AndKeepsTheBestOfWhatItTook()
    {
        LostOneSettings settings = Settings(carriedCap: 2);
        var roster = new LostOneRoster();
        LostOneRules.Refill(roster, settings, true, 7);
        LostOneRecord record = roster.All[0];
        ItemDefinition cheap = Item("Cheap", 10, 1);
        ItemDefinition dear = Item("Dear", 1, 50);
        ItemDefinition middling = Item("Middling", 5, 8);
        var taken = new List<ItemStack> { new ItemStack(cheap, 10), new ItemStack(dear, 1), new ItemStack(middling, 5) };

        Assert.IsTrue(LostOneRules.RiseOnKill(record, taken, settings, new System.Random(1)));
        Assert.AreEqual(2, record.Rank);
        Assert.AreEqual(1, record.Kills);
        Assert.AreEqual(settings.RankAt(2).traits, record.Traits.Count, "a new trait with the rank");
        Assert.AreEqual(record.Traits.Count, record.Traits.Distinct().Count(), "never the same trait twice");
        Assert.AreEqual(2, record.Carried.Count, "it keeps the most valuable, up to its cap");
        Assert.AreSame(dear, record.Carried[0].Item, "50 before 40 before 10");
        Assert.AreSame(middling, record.Carried[1].Item);

        record.Rank = settings.MaxRank;
        ItemDefinition rare = Item("Rare", 1, 100);
        Assert.IsFalse(LostOneRules.RiseOnKill(record, new[] { new ItemStack(rare, 1) }, settings, new System.Random(2)), "no rank above the last");
        Assert.AreEqual(settings.MaxRank, record.Rank);
        Assert.AreEqual(2, record.Kills);
        Assert.AreSame(rare, record.Carried[0].Item, "but it still takes the best of what the Keeper had");
        Assert.AreEqual(2, record.Carried.Count);
    }

    [Test]
    public void WhatTheDeathTook_IsWhatWasHeld_LessWhatComesHome()
    {
        ItemDefinition loot = Item("Loot", 10, 5);
        ItemDefinition treasure = Item("Treasure", 1, 30);
        var bag = new ItemContainer(4, 1);
        bag.SetSlot(0, new ItemStack(loot, 6));
        var pocket = new ItemContainer(1, 1);
        pocket.SetSlot(0, new ItemStack(treasure, 1));
        PlayerSnapshot final = PlayerSnapshot.Capture(bag, null, null, pocket);
        PlayerSnapshot keeps = final.AfterFailedRaid(null, 0.1f);

        List<ItemStack> taken = LostOneRules.TakenStacks(final, keeps);
        Assert.AreEqual(1, taken.Count, "the safe pocket is never taken");
        Assert.AreSame(loot, taken[0].Item);
        Assert.AreEqual(6, taken[0].Count);
        Assert.AreEqual(0, LostOneRules.TakenStacks(final, final).Count, "an extraction takes nothing");
    }

    [Test]
    public void Fleeing_CountsAnEscape_TeachesATrait_AndScars_ButNeverAtTheTop()
    {
        LostOneSettings settings = Settings();
        var record = new LostOneRecord { Id = "lost-1", GivenName = "Ketil", Rank = 2 };
        record.Traits.AddRange(new[] { "Trait0", "Trait1" });

        Assert.IsTrue(LostOneRules.CanFlee(record, settings, false));
        Assert.IsFalse(LostOneRules.CanFlee(record, settings, true), "once a raid");
        var random = new System.Random(3);
        string first = LostOneRules.Escape(record, random);
        string second = LostOneRules.Escape(record, random);
        CollectionAssert.AreEquivalent(new[] { "Trait0", "Trait1" }, new[] { first, second }, "each escape shows one not seen through yet");
        Assert.IsNull(LostOneRules.Escape(record, random), "nothing left to learn");
        Assert.AreEqual(3, record.Escapes);
        Assert.AreEqual(Mathf.Min(settings.MostScar, 3 * settings.ScarPerEscape), LostOneRules.ScarShare(record, settings), 1e-5f, "it comes back scarred");

        record.Escapes = 100;
        Assert.AreEqual(settings.MostScar, LostOneRules.ScarShare(record, settings), 1e-5f, "to a limit");
        record.Rank = settings.MaxRank;
        Assert.IsFalse(LostOneRules.CanFlee(record, settings, false), "the highest rank fights to the end");
    }

    // ----- Laid to rest -----

    [Test]
    public void LaidToRest_DropsWhatItCarried_AndItsPlaceRisesAgainAfterAHomecoming()
    {
        LostOneSettings settings = Settings();
        var roster = new LostOneRoster();
        LostOneRules.Refill(roster, settings, true, 11);
        LostOneRecord record = roster.All[0];
        ItemDefinition axe = Item("Axe", 1, 20);
        record.Carried.Add(new ItemStack(axe, 1));
        roster.Homecomings = 5;

        List<ItemStack> dropped = LostOneRules.LayToRest(record, roster.Homecomings, "Misty Woods", settings);
        Assert.AreEqual(1, dropped.Count);
        Assert.AreSame(axe, dropped[0].Item);
        Assert.IsEmpty(record.Carried, "what it carried lies where it fell");
        Assert.AreEqual(LostOneStatus.LaidToRest, record.Status);
        CollectionAssert.IsSubsetOf(record.Traits, record.Learned, "all of it is known now");
        StringAssert.Contains("the Misty Woods", record.Epitaph);
        Assert.AreEqual(settings.RosterSize - 1, roster.WalkingCount);

        roster.Homecomings++;
        Assert.AreEqual(0, LostOneRules.Refill(roster, settings, true, 11).Count, "not at the homecoming it was laid to rest on");
        roster.Homecomings++;
        List<LostOneRecord> risen = LostOneRules.Refill(roster, settings, true, 11);
        Assert.AreEqual(1, risen.Count, "a new one rises in its place after a homecoming");
        Assert.AreEqual("lost-" + (settings.RosterSize + 1), risen[0].Id);
        Assert.AreEqual(1, roster.All.Count(other => other.GivenName == risen[0].GivenName), "a name none of the Lost has had");
        Assert.AreEqual(LostOneStatus.LaidToRest, roster.Find(record.Id).Status, "the laid to rest stay in the journal");
    }

    // ----- Bodies -----

    [Test]
    public void ItsBody_FollowsItsRank_AndKeepsItsFace()
    {
        MonsterDefinition clothA = Monster("Lost Cloth A"), clothB = Monster("Lost Cloth B"), mailA = Monster("Lost Mail A"), mailB = Monster("Lost Mail B");
        LostOneSettings settings = Settings();
        var data = new SerializedObject(settings);
        SerializedProperty grades = data.FindProperty("bodyGrades");
        Assert.AreEqual(3, grades.arraySize, "cloth, leather and mail by default");
        SetBodies(grades.GetArrayElementAtIndex(0), clothA, clothB);
        SetBodies(grades.GetArrayElementAtIndex(2), mailA, mailB);
        data.ApplyModifiedPropertiesWithoutUndo();

        var faces = new HashSet<MonsterDefinition>();
        for (int i = 1; i <= 12; i++)
        {
            var record = new LostOneRecord { Id = "lost-" + i, Rank = 1 };
            MonsterDefinition low = settings.BodyFor(record);
            CollectionAssert.Contains(new[] { clothA, clothB }, low, "cloth at the first rank");
            Assert.AreSame(low, settings.BodyFor(record), "the same one each time");
            record.Rank = 3;
            Assert.AreSame(low, settings.BodyFor(record), "leather has none yet: still cloth");
            record.Rank = 5;
            MonsterDefinition high = settings.BodyFor(record);
            CollectionAssert.Contains(new[] { mailA, mailB }, high, "mail at the top");
            Assert.AreEqual(low == clothA, high == mailA, "the same place in each grade's list: the same face");
            faces.Add(low);
        }

        Assert.AreEqual(2, faces.Count, "different Lost, different faces");
        LostOneSettings plain = Settings();
        Assert.IsNotNull(plain.FallbackBody);
        Assert.AreSame(plain.FallbackBody, plain.BodyFor(new LostOneRecord { Id = "lost-1", Rank = 4 }), "the fallback while every grade is empty");
    }

    // ----- Friends and saves -----

    [Test]
    public void ANameSentToFriends_ComesBackWithItsRank()
    {
        var record = new LostOneRecord { GivenName = "Halvard", Epithet = "Hollow-Eyed", Rank = 4 };
        string packed = LostOne.Pack(record);
        StringAssert.DoesNotContain("|", packed, "it travels among affix names split by '|'");
        Assert.IsTrue(LostOne.TryUnpack(packed, out string name, out int rank));
        Assert.AreEqual("Halvard Hollow-Eyed", name);
        Assert.AreEqual(4, rank);
        Assert.IsFalse(LostOne.TryUnpack("Frenzied", out _, out _), "an affix's name is no Lost One");
    }

    [Test]
    public void TheDeathScreen_NamesALostOne_AndAKindOfCreatureAsBefore()
    {
        Assert.AreEqual("a Draug", CreatureRecords.ByWhom("Draug"), "a kind of creature, and every record kept before");
        Assert.AreEqual("Halvard Hollow-Eyed, one of the Lost",
            CreatureRecords.ByWhom($"{CreatureRecords.OwnNameMark}Halvard Hollow-Eyed, one of the Lost"), "a person, by name, without \"a\"");
        Assert.IsNull(CreatureRecords.ByWhom(null));
    }

    [Test]
    public void TheRoster_SurvivesASave()
    {
        var lookup = new Lookup();
        ItemDefinition fish = Item("Black Pike", 5, 12);
        lookup.Items[fish.Id] = fish;
        var progress = new CharacterProgress();
        LostOneSettings settings = Settings();
        LostOneRules.Refill(progress.LostOnes, settings, true, 2024);
        LostOneRecord record = progress.LostOnes.All[1];
        record.Rank = 3;
        record.Kills = 2;
        record.Escapes = 1;
        record.RaidsMet = 4;
        record.Met = true;
        record.LastSeenIn = "Moss Marsh";
        record.Learned.Add(record.Traits[0]);
        record.Carried.Add(new ItemStack(fish, 3, 1f, 0, null, ItemQuality.Plain, null, 42.5f));
        record.Carried.Add(new ItemStack(Item("Gone", 1, 1), 1));
        LostOneRecord rested = progress.LostOnes.All[2];
        LostOneRules.LayToRest(rested, 6, "Troll Valley", settings);
        progress.LostOnes.Homecomings = 9;

        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, DateTime.UtcNow, null, null, null, null, progress);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);
        LostOneRoster back = loaded.Progress.LostOnes;

        Assert.AreEqual(progress.LostOnes.Seed, back.Seed);
        Assert.AreEqual(9, back.Homecomings);
        Assert.AreEqual(progress.LostOnes.Risen, back.Risen);
        Assert.AreEqual(progress.LostOnes.All.Count, back.All.Count);
        for (int i = 0; i < back.All.Count; i++)
        {
            LostOneRecord want = progress.LostOnes.All[i], got = back.All[i];
            Assert.AreEqual(want.Id, got.Id);
            Assert.AreEqual(want.FullName, got.FullName);
            Assert.AreEqual(want.PastLife, got.PastLife);
            Assert.AreEqual(want.Rank, got.Rank);
            CollectionAssert.AreEqual(want.Traits, got.Traits);
            CollectionAssert.AreEqual(want.Learned, got.Learned);
            Assert.AreEqual(want.Kills, got.Kills);
            Assert.AreEqual(want.Escapes, got.Escapes);
            Assert.AreEqual(want.RaidsMet, got.RaidsMet);
            Assert.AreEqual(want.Status, got.Status);
            Assert.AreEqual(want.Met, got.Met);
            Assert.AreEqual(want.RestedAtHomecoming, got.RestedAtHomecoming);
            Assert.AreEqual(want.LastSeenIn, got.LastSeenIn);
            Assert.AreEqual(want.Epitaph, got.Epitaph);
        }

        LostOneRecord carrier = back.All[1];
        Assert.AreEqual(1, carrier.Carried.Count, "a thing that no longer exists is left out");
        Assert.AreSame(fish, carrier.Carried[0].Item);
        Assert.AreEqual(3, carrier.Carried[0].Count);
        Assert.AreEqual(42.5f, carrier.Carried[0].Size, 1e-3f, "the stack as it was taken");
        CollectionAssert.Contains(loaded.MissingContent, "item 'Gone'", "and named as missing, as a bag's would be");
    }

    [Test]
    public void ASaveFromBeforeTheLost_LoadsWithNone()
    {
        var data = new SaveData();
        data.progress.lostOnes = null;
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), new Lookup());
        Assert.AreEqual(0, loaded.Progress.LostOnes.All.Count);
        Assert.AreEqual(0, loaded.Progress.LostOnes.Seed);

        string old = JsonUtility.ToJson(new SaveData()).Replace("\"lostOnes\"", "\"somethingElse\"");
        loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(old), new Lookup());
        Assert.AreEqual(0, loaded.Progress.LostOnes.All.Count, "a file without the field");
    }

    // ----- Helpers -----

    private LostOneSettings Settings(QuestDefinition gate = null, float chance = 0.3f, int carriedCap = 4, bool withBody = true)
    {
        var settings = ScriptableObject.CreateInstance<LostOneSettings>();
        created.Add(settings);
        var data = new SerializedObject(settings);
        SerializedProperty traits = data.FindProperty("traits");
        traits.arraySize = 5;
        for (int i = 0; i < 4; i++)
        {
            traits.GetArrayElementAtIndex(i).objectReferenceValue = Affix("Trait" + i, 1);
        }

        traits.GetArrayElementAtIndex(4).objectReferenceValue = Affix("Late", 3);
        data.FindProperty("storyGate.quest").objectReferenceValue = gate;
        data.FindProperty("storyGate.requirement").enumValueIndex = (int)QuestGate.Requirement.Completed;
        SerializedProperty chances = data.FindProperty("chanceByTier");
        for (int i = 0; i < chances.arraySize; i++)
        {
            chances.GetArrayElementAtIndex(i).floatValue = chance;
        }

        data.FindProperty("mostChance").floatValue = Mathf.Max(chance, 0.6f);
        data.FindProperty("chancePerRank").floatValue = chance >= 1f || chance <= 0f ? 0f : 0.05f;
        data.FindProperty("carriedCap").intValue = carriedCap;
        if (withBody)
        {
            data.FindProperty("fallbackBody").objectReferenceValue = Monster("Draug");
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return settings;
    }

    private static void SetBodies(SerializedProperty grade, params MonsterDefinition[] bodies)
    {
        SerializedProperty list = grade.FindPropertyRelative("bodies");
        list.arraySize = bodies.Length;
        for (int i = 0; i < bodies.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
        }
    }

    private EliteAffixDefinition Affix(string name, int fromTier)
    {
        var affix = ScriptableObject.CreateInstance<EliteAffixDefinition>();
        affix.name = name;
        created.Add(affix);
        var data = new SerializedObject(affix);
        data.FindProperty("fromTier").intValue = fromTier;
        data.ApplyModifiedPropertiesWithoutUndo();
        return affix;
    }

    private MonsterDefinition Monster(string name)
    {
        var body = new GameObject(name);
        created.Add(body);
        var monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        monster.name = name;
        created.Add(monster);
        var data = new SerializedObject(monster);
        data.FindProperty("prefab").objectReferenceValue = body;
        data.ApplyModifiedPropertiesWithoutUndo();
        return monster;
    }

    private ItemDefinition Item(string id, int stack, int value)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.name = id;
        created.Add(item);
        var data = new SerializedObject(item);
        data.FindProperty("stackable").boolValue = stack > 1;
        data.FindProperty("maximumStack").intValue = stack;
        data.FindProperty("value").intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }
}
