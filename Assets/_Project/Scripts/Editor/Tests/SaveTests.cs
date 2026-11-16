using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Edit Mode tests for turning the game's in-memory state into save files and back.</summary>
public sealed class SaveTests
{
    private sealed class Lookup : IContentLookup
    {
        public readonly Dictionary<string, BuildingDefinition> Buildings = new Dictionary<string, BuildingDefinition>();
        public readonly Dictionary<string, ResidentDefinition> Residents = new Dictionary<string, ResidentDefinition>();
        public readonly Dictionary<string, JobDefinition> Jobs = new Dictionary<string, JobDefinition>();
        public BuildingDefinition FindBuilding(string id) => id != null && Buildings.TryGetValue(id, out BuildingDefinition b) ? b : null;
        public ResidentDefinition FindResident(string id) => id != null && Residents.TryGetValue(id, out ResidentDefinition r) ? r : null;
        public JobDefinition FindJob(string id) => id != null && Jobs.TryGetValue(id, out JobDefinition j) ? j : null;
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
        public readonly Dictionary<string, ResourceDefinition> Resources = new Dictionary<string, ResourceDefinition>();
        public readonly Dictionary<string, RecipeDefinition> Recipes = new Dictionary<string, RecipeDefinition>();

        public ItemDefinition FindItem(string id) => id != null && Items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => id != null && Resources.TryGetValue(id, out ResourceDefinition r) ? r : null;
        public RecipeDefinition FindRecipe(string id) => id != null && Recipes.TryGetValue(id, out RecipeDefinition r) ? r : null;
        public QuestDefinition FindQuest(string id) => id != null && Quests.TryGetValue(id, out QuestDefinition q) ? q : null;
        public readonly Dictionary<string, QuestDefinition> Quests = new Dictionary<string, QuestDefinition>();
    }

    private readonly List<Object> created = new List<Object>();
    private string folder;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "SafeHavenSaveTests_" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
        SaveGameService.Storage = null;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private RaidSession StartRaid()
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        created.Add(raid);
        var none = new RaidModifierDefinition[0];
        var session = new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SampleScene");
        GameSession.BeginRaid(session);
        return session;
    }

    [Test]
    public void ADuringRaid_NeitherSavingNorLoading()
    {
        SaveGameService.Storage = new SaveStorage(folder);
        RaidSession raid = StartRaid();
        try
        {
            Assert.IsNotNull(SaveGameService.CheckSave());
            Assert.IsNotNull(SaveGameService.CheckLoad());
            Assert.IsFalse(SaveGameService.TryLoad("anything", new Lookup(), "SafeHaven", out string problem));
            StringAssert.Contains("raid", problem);
            Assert.IsNotNull(GameSession.ActiveRaid, "a refused load leaves the raid running");
        }
        finally
        {
            GameSession.AbandonRaid(raid);
        }
    }

    [Test]
    public void ARaidLeftWithoutAnEnding_LetsTheMenuLoadAgain()
    {
        RaidSession raid = StartRaid();
        GameSession.AbandonRaid(new RaidSession(raid.Definition, new RaidModifierDefinition[0], raid.Parameters, null, "SampleScene"));
        Assert.AreSame(raid, GameSession.ActiveRaid, "only the raid in hand can be left");
        GameSession.AbandonRaid(raid);
        Assert.IsNull(GameSession.ActiveRaid);
        Assert.IsNull(SaveGameService.CheckLoad());
    }

    [Test]
    public void Mapper_RoundTripsPlayerStashAndPose()
    {
        var lookup = new Lookup();
        ItemDefinition potion = Item(lookup, "potion");
        ItemDefinition axe = Item(lookup, "axe");
        ResourceDefinition wood = Resource(lookup, "wood");
        ResourceDefinition ore = Resource(lookup, "ore");
        RecipeDefinition recipe = Create<RecipeDefinition>("plank_recipe");
        lookup.Recipes[recipe.Id] = recipe;

        var bag = new ItemContainer(8, 5);
        bag.SetSlot(7, new ItemStack(potion, 3));
        var resources = new ResourceStorage(10);
        resources.SetAmount(wood, 14);
        resources.MarkDiscovered(ore);
        PlayerSnapshot player = PlayerSnapshot.Capture(bag, resources, new[] { recipe });
        var stash = new StashStorage();
        stash.Items.SetSlot(12, new ItemStack(axe, 1));
        stash.Resources.Add(ore, 40);
        var pose = new PlayerPose(new Vector3(1f, 2f, 3f), 90f);

        SaveData data = SaveMapper.ToSaveData(player, stash, "SampleScene", pose, new DateTime(2026, 10, 14, 13, 0, 0, DateTimeKind.Utc));
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);

        Assert.IsEmpty(loaded.MissingContent);
        Assert.AreEqual("SampleScene", loaded.Scene);
        Assert.AreEqual(pose.Position, loaded.Pose.Value.Position);
        Assert.AreEqual(90f, loaded.Pose.Value.Yaw);

        var restoredBag = new ItemContainer(8, 5);
        var restoredResources = new ResourceStorage(10);
        loaded.Player.ApplyTo(restoredBag, restoredResources, null);
        Assert.AreEqual(potion, restoredBag.GetSlot(7).Item);
        Assert.AreEqual(3, restoredBag.GetSlot(7).Count);
        Assert.AreEqual(14, restoredResources.GetAmount(wood));
        Assert.IsTrue(restoredResources.IsDiscovered(ore));
        CollectionAssert.AreEqual(new[] { recipe }, loaded.Player.KnownRecipes);
        Assert.AreEqual(axe, loaded.Stash.Items.GetSlot(12).Item);
        Assert.AreEqual(40, loaded.Stash.Resources.GetAmount(ore));
    }

    [Test]
    public void Mapper_StoresIdsNotAssetReferences()
    {
        var lookup = new Lookup();
        ItemDefinition potion = Item(lookup, "potion");
        var bag = new ItemContainer(2, 1);
        bag.Add(potion, 1);

        string json = JsonUtility.ToJson(SaveMapper.ToSaveData(PlayerSnapshot.Capture(bag, null, null), null, "SampleScene", null, DateTime.UtcNow));

        StringAssert.Contains("\"item\":\"potion\"", json);
        StringAssert.DoesNotContain("instanceID", json);
    }

    [Test]
    public void Mapper_SkipsAndReportsContentThatNoLongerExists()
    {
        var lookup = new Lookup();
        ItemDefinition potion = Item(lookup, "potion");
        var data = new SaveData { version = SaveMapper.CurrentVersion, scene = "SampleScene" };
        data.player.bag.Add(new SlotSaveData { slot = 0, item = "potion", count = 2 });
        data.player.bag.Add(new SlotSaveData { slot = 1, item = "deleted_sword", count = 1 });
        data.stash.resources.Add(new AmountSaveData { id = "deleted_ore", amount = 5 });

        LoadedGame loaded = SaveMapper.FromSaveData(data, lookup);

        CollectionAssert.AreEquivalent(new[] { "item 'deleted_sword'", "resource 'deleted_ore'" }, loaded.MissingContent);
        var bag = new ItemContainer(4, 1);
        loaded.Player.ApplyTo(bag, null, null);
        Assert.AreEqual(2, bag.Count(potion));
        Assert.IsFalse(loaded.Pose.HasValue);
    }

    [Test]
    public void Mapper_RoundTripsSettlementWearConditionAndKnownBuildings()
    {
        var lookup = new Lookup();
        ItemDefinition axe = Tool(Item(lookup, "stone_axe"));
        BuildingDefinition campfire = Create<BuildingDefinition>("campfire");
        lookup.Buildings[campfire.Id] = campfire;
        ResidentDefinition ingrid = Create<ResidentDefinition>("ingrid");
        lookup.Residents[ingrid.Id] = ingrid;
        JobDefinition alchemist = Create<JobDefinition>("alchemist");
        lookup.Jobs[alchemist.Id] = alchemist;

        var bag = new ItemContainer(4, 2);
        bag.SetSlot(3, new ItemStack(axe, 1, 0.75f));
        PlayerSnapshot player = PlayerSnapshot.Capture(bag, null, null, null, new[] { campfire }, new PlayerSnapshot.Condition(42f, 17f, 3));
        var settlement = new SettlementState();
        settlement.RepairPortal();
        settlement.RaiseWardLevel();
        settlement.RecordExtraction();
        settlement.RecordExtraction();
        settlement.SetBreach(BreachStatus.Pending);
        settlement.AddResident(ingrid, alchemist);
        settlement.QueuePurification(2, 5);
        settlement.TickPurification(5.5f, 20f);
        settlement.ChangeBlight(0.35f);
        settlement.GiveSpiritGift(SpiritBonds.NokkGift);
        settlement.SetHuldra(1, 9);
        settlement.OpenHeldQuestShare();
        settlement.NoteHeldQuestStarted();

        SaveData data = SaveMapper.ToSaveData(player, null, "SafeHaven", null, DateTime.UtcNow, settlement: settlement);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);

        Assert.IsEmpty(loaded.MissingContent);
        var restoredBag = new ItemContainer(4, 2);
        loaded.Player.ApplyTo(restoredBag, null, null);
        Assert.AreEqual(0.75f, restoredBag.GetSlot(3).Durability, 0.001f);
        CollectionAssert.AreEqual(new[] { campfire }, loaded.Player.KnownBuildings);
        Assert.IsTrue(loaded.Player.State.HasNeeds);
        Assert.AreEqual(42f, loaded.Player.State.Hunger);
        Assert.AreEqual(17f, loaded.Player.State.Thirst);
        Assert.AreEqual(3, loaded.Player.State.EquippedSlot);

        SettlementState restored = loaded.Settlement;
        Assert.AreEqual(1, restored.HeldQuestsHomecoming);
        Assert.AreEqual(1, restored.HeldQuestsStarted, "a Continue doesn't open a fresh share of held quests at the same homecoming");
        restored.OpenHeldQuestShare();
        Assert.AreEqual(0, restored.HeldQuestsStarted, "the next return home does, extracted or not");
        Assert.AreEqual(0.35f, restored.Blight, 0.001f);
        Assert.IsTrue(restored.HasSpiritGift(SpiritBonds.NokkGift));
        Assert.AreEqual(1, restored.HuldraRespect);
        Assert.AreEqual(9, restored.HuldraAwayUntil);
        Assert.IsTrue(restored.PortalRepaired);
        Assert.AreEqual(1, restored.WardLevel);
        Assert.AreEqual(2, restored.SuccessfulExtractions);
        Assert.AreEqual(BreachStatus.Pending, restored.Breach);
        Assert.AreEqual(2, restored.PurificationQueued);
        Assert.AreEqual(5.5f, restored.PurificationProgress, 0.001f);
        Assert.AreEqual(1, restored.Residents.Count);
        Assert.AreEqual(ingrid, restored.Residents[0].Resident);
        Assert.AreEqual(alchemist, restored.Residents[0].Job);
    }

    [Test]
    public void Mapper_LoadsGearFromSavesWithoutWearUndamaged()
    {
        var lookup = new Lookup();
        Tool(Item(lookup, "stone_axe"));
        var data = new SaveData { version = SaveMapper.CurrentVersion, scene = "SafeHaven" };
        data.player.bag.Add(new SlotSaveData { slot = 0, item = "stone_axe", count = 1 });

        LoadedGame loaded = SaveMapper.FromSaveData(data, lookup);

        var bag = new ItemContainer(2, 1);
        loaded.Player.ApplyTo(bag, null, null);
        Assert.AreEqual(1f, bag.GetSlot(0).Durability);
    }

    [Test]
    public void Mapper_RoundTripsWorldState_AndSkipsScenesNotWrittenToDisk()
    {
        var world = new WorldState();
        SceneState haven = world.GetOrAdd("SampleScene", true);
        EntityState brute = haven.GetOrAdd("brute-1");
        brute.Destroyed = true;
        EntityState wall = haven.GetOrAdd("spawned-wall");
        wall.Spawned = true;
        wall.PrefabId = "wall-prefab";
        wall.HasTransform = true;
        wall.Position = new Vector3(4f, 0f, 2f);
        wall.Components["health"] = "{\"health\":120.0}";
        world.GetOrAdd("SessionOnly", false).GetOrAdd("x");

        SaveData data = SaveMapper.ToSaveData(null, null, "SampleScene", null, DateTime.UtcNow, world);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), new Lookup());

        Assert.IsFalse(loaded.World.Scenes.ContainsKey("SessionOnly"));
        SceneState restored = loaded.World.Scenes["SampleScene"];
        Assert.IsTrue(restored.Entities["brute-1"].Destroyed);
        EntityState restoredWall = restored.Entities["spawned-wall"];
        Assert.IsTrue(restoredWall.Spawned);
        Assert.AreEqual("wall-prefab", restoredWall.PrefabId);
        Assert.AreEqual(new Vector3(4f, 0f, 2f), restoredWall.Position);
        Assert.AreEqual("{\"health\":120.0}", restoredWall.Components["health"]);
    }

    [Test]
    public void Storage_WritesAndReadsBack()
    {
        var storage = new SaveStorage(folder);
        Assert.IsFalse(storage.Exists("slot"));

        storage.Write("slot", new SaveData { version = 1, scene = "SampleScene" });

        Assert.IsTrue(storage.Exists("slot"));
        Assert.IsTrue(storage.TryRead("slot", out SaveData read, out string error), error);
        Assert.AreEqual("SampleScene", read.scene);
    }

    [Test]
    public void Storage_FallsBackToTheBackupWhenTheSaveIsDamaged()
    {
        var storage = new SaveStorage(folder);
        storage.Write("slot", new SaveData { version = 1, scene = "First" });
        storage.Write("slot", new SaveData { version = 1, scene = "Second" });
        File.WriteAllText(storage.PathFor("slot"), "{ not json");

        Assert.IsTrue(storage.TryRead("slot", out SaveData read, out string error), error);
        Assert.AreEqual("First", read.scene);
    }

    private SaveData Written(SaveStorage storage, string slot, string name, string savedAt, int day = 1)
    {
        var data = new SaveData { version = SaveMapper.CurrentVersion, savedAtUtc = savedAt, scene = "SafeHaven", displayName = name };
        data.settlement.daysPassed = day - 1;
        storage.Write(slot, data);
        return data;
    }

    [Test]
    public void Slots_AreListedOnceEachAndABackupAloneStillCounts()
    {
        var storage = new SaveStorage(folder);
        Assert.IsEmpty(storage.ListSlots(), "a folder that doesn't exist yet holds no saves");
        Written(storage, "first", "One", "2026-10-01T10:00:00Z");
        Written(storage, "first", "One", "2026-10-02T10:00:00Z");
        Written(storage, "second", "Two", "2026-10-03T10:00:00Z");
        File.Delete(storage.PathFor("second"));
        Written(storage, "third", "Three", "2026-10-03T10:00:00Z");
        Written(storage, "third", "Three", "2026-10-03T11:00:00Z");
        File.Delete(storage.PathFor("third"));
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a save");
        CollectionAssert.AreEquivalent(new[] { "first", "third" }, storage.ListSlots());
    }

    [Test]
    public void Slots_ANewOneNeverTakesAnExistingFile()
    {
        var storage = new SaveStorage(folder);
        var now = new DateTime(2026, 10, 30, 12, 0, 0);
        string first = storage.NewSlot(now);
        Written(storage, first, "One", "2026-10-30T12:00:00Z");
        string second = storage.NewSlot(now);
        Assert.AreNotEqual(first, second);
        Written(storage, second, "Two", "2026-10-30T12:00:00Z");
        Assert.AreNotEqual(second, storage.NewSlot(now));
    }

    [Test]
    public void Saves_AreListedNewestFirstAndADamagedOneIsShownButNotChosen()
    {
        var storage = new SaveStorage(folder);
        SaveGameService.Storage = storage;
        Written(storage, "old", "Old Game", "2026-01-01T10:00:00Z", 2);
        Written(storage, "new", "New Game", "2026-06-01T10:00:00Z", 7);
        Directory.CreateDirectory(folder);
        File.WriteAllText(storage.PathFor("broken"), "{ this is not a save");
        LogAssert.ignoreFailingMessages = true;
        List<SaveSlotInfo> saves = SaveGameService.ListSaves();
        LogAssert.ignoreFailingMessages = false;
        Assert.AreEqual(3, saves.Count);
        var readable = saves.FindAll(save => save.IsReadable);
        Assert.AreEqual("New Game", readable[0].Name);
        Assert.AreEqual(7, readable[0].Day);
        Assert.AreEqual("Old Game", readable[1].Name);
        Assert.IsFalse(saves.Find(save => save.Slot == "broken").IsReadable);
        Assert.AreEqual("new", SaveGameService.LatestSave().Slot);
    }

    [Test]
    public void Saves_FromBeforeNamesGoByTheirFile()
    {
        Assert.AreEqual("Journey 1", SaveSlotInfo.NameFor(SaveStorage.DefaultSlot, new SaveData()));
        Assert.AreEqual("save 2026-10-27 before new game", SaveSlotInfo.NameFor("save_2026-10-27_before_new_game", new SaveData()));
        Assert.AreEqual("Mine", SaveSlotInfo.NameFor("save", new SaveData { displayName = "Mine" }));
    }

    [Test]
    public void Saves_CanBeRenamedAndNotToNothing()
    {
        var storage = new SaveStorage(folder);
        SaveGameService.Storage = storage;
        Written(storage, "game", "Before", "2026-01-01T10:00:00Z");
        Assert.IsFalse(SaveGameService.TryRename("game", "   ", out string problem));
        Assert.IsNotEmpty(problem);
        Assert.IsTrue(SaveGameService.TryRename("game", "  The Long Winter  ", out _));
        Assert.AreEqual("The Long Winter", SaveGameService.ListSaves()[0].Name);
        Assert.IsTrue(SaveGameService.TryRename("game", new string('x', 80), out _));
        Assert.AreEqual(32, SaveGameService.ListSaves()[0].Name.Length);
    }

    [Test]
    public void Saves_DeletingOneLeavesTheOthersAndItsBackupGoesToo()
    {
        var storage = new SaveStorage(folder);
        SaveGameService.Storage = storage;
        Written(storage, "keep", "Keep", "2026-01-01T10:00:00Z");
        Written(storage, "drop", "Drop", "2026-01-01T10:00:00Z");
        Written(storage, "drop", "Drop", "2026-01-02T10:00:00Z");
        Assert.IsTrue(SaveGameService.TryDelete("drop", out _));
        CollectionAssert.AreEqual(new[] { "keep" }, storage.ListSlots());
        Assert.IsFalse(storage.Exists("drop"));
        Assert.IsTrue(SaveGameService.HasSave);
    }

    [Test]
    public void Saves_DescribeThemselves()
    {
        Assert.AreEqual("Safe Haven", SaveSlotInfo.PlaceName("SafeHaven"));
        Assert.AreEqual("12 min played", SaveSlotInfo.DescribePlayed(725f));
        Assert.AreEqual("3 h 20 min played", SaveSlotInfo.DescribePlayed(12000f));
    }

    [Test]
    public void Storage_ReportsAMissingSave()
    {
        var storage = new SaveStorage(folder);

        Assert.IsFalse(storage.TryRead("nothing", out SaveData read, out string error));
        Assert.IsNull(read);
        Assert.IsNotEmpty(error);
    }

    private ItemDefinition Item(Lookup lookup, string id)
    {
        ItemDefinition item = Create<ItemDefinition>(id);
        lookup.Items[item.Id] = item;
        return item;
    }

    private ResourceDefinition Resource(Lookup lookup, string id)
    {
        ResourceDefinition resource = Create<ResourceDefinition>(id);
        lookup.Resources[resource.Id] = resource;
        return resource;
    }

    // Tools have durability, so their wear is saved.
    private static ItemDefinition Tool(ItemDefinition item)
    {
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)ToolType.Axe;
        data.FindProperty("toolTier").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    // Definitions without an explicit id use their asset name as id, so naming the instance sets the id.
    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
