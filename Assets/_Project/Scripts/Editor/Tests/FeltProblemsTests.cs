using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The problems a raid makes the player feel and who answers each (HavenAnswers, the raid summary), and the objectives that
/// teach an answer by doing it: put something in a chest, repair a piece of gear, raise a building a tier.
/// </summary>
public sealed class FeltProblemsTests
{
    private readonly List<Object> created = new List<Object>();
    private GameObject player;

    [TearDown]
    public void TearDown()
    {
        if (player != null)
        {
            Object.DestroyImmediate(player);
        }

        foreach (Object thing in created)
        {
            Object.DestroyImmediate(thing);
        }

        created.Clear();
        GameSession.StartNewGame();
    }

    private static T Asset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/" + path + ".asset");
    private static ItemDefinition Material(string name) => Asset<ItemDefinition>("Data/Items/Materials/" + name);
    private static BuildingDefinition Building(string name) => Asset<BuildingDefinition>("Data/Buildings/" + name);
    private static ContentDatabase Content => Asset<ContentDatabase>("Resources/ContentDatabase");
    private static ResidentDefinition Person(string name) => Content.Residents.First(person => person != null && person.name == name);

    // A player with the game's recipe book who knows what a new game knows. Made inactive, so nothing runs before the book is set.
    private PlayerCrafting NewPlayer()
    {
        player = new GameObject("Player");
        player.SetActive(false);
        PlayerCrafting crafting = player.AddComponent<PlayerCrafting>();
        var data = new SerializedObject(crafting);
        data.FindProperty("recipeBook").objectReferenceValue = Asset<RecipeBook>("Data/Crafting/RecipeBook");
        data.ApplyModifiedPropertiesWithoutUndo();
        return crafting;
    }

    private RaidSession Session()
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        created.Add(raid);
        RaidModifierDefinition[] none = new RaidModifierDefinition[0];
        return new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven");
    }

    // ----- Finds nobody at home can use yet -----

    [Test]
    public void CopperOre_IsNoUse_UntilBjarneComesHome()
    {
        PlayerCrafting crafting = NewPlayer();
        var settlement = new SettlementState();

        List<HavenAnswer> who = HavenAnswers.WhoCouldUse(Material("CopperOre"), crafting, Content, settlement, out bool unusable);
        Assert.IsTrue(unusable, "the old Furnace left the build menu: nothing at home smelts copper");
        Assert.AreEqual(Person("Bjarne"), who[0].Person);
        Assert.AreEqual("Bring Bjarne home: the Smith knows how.", who[0].HowTo());

        ResidentNpc.TeachWhatTheyKnow(Person("Bjarne"), player, false);
        HavenAnswers.WhoCouldUse(Material("CopperOre"), crafting, Content, settlement, out unusable);
        Assert.IsFalse(unusable, "his Smithy smelts it");
    }

    [Test]
    public void ThickHide_WaitsForSomeoneWhoWorksIt()
    {
        PlayerCrafting crafting = NewPlayer();
        var settlement = new SettlementState();
        HavenAnswers.WhoCouldUse(Material("RoughHide"), crafting, Content, settlement, out bool roughUnusable);
        Assert.IsFalse(roughUnusable, "rough hide goes into the Workshop's woven gear");

        List<HavenAnswer> who = HavenAnswers.WhoCouldUse(Material("ThickHide"), crafting, Content, settlement, out bool unusable);
        Assert.IsTrue(unusable);
        Assert.IsNotEmpty(who);
        Assert.LessOrEqual(who.Count, 2, "the summary names two at most");
        Assert.IsTrue(who.All(answer => answer.Person != null && !answer.PersonIsHome), "someone still out there knows");

        ResidentNpc.TeachWhatTheyKnow(who[0].Person, player, false);
        HavenAnswers.WhoCouldUse(Material("ThickHide"), crafting, Content, settlement, out unusable);
        Assert.IsFalse(unusable, "the first named is enough");
    }

    [Test]
    public void WhatTheWorkshopTakes_IsNeverAProblem()
    {
        PlayerCrafting crafting = NewPlayer();
        HavenAnswers.WhoCouldUse(Material("OakPlank"), crafting, Content, new SettlementState(), out bool unusable);
        Assert.IsFalse(unusable);
    }

    // ----- A load left behind -----

    [Test]
    public void ALoadLeftBehind_NamesTheBarrow_AndWhoBuildsIt()
    {
        PlayerCrafting crafting = NewPlayer();
        var settlement = new SettlementState();

        HavenAnswer carrier = HavenAnswers.CarrierFor(crafting, Content, settlement);
        Assert.AreEqual("Wheelbarrow", carrier.Carrier.name, "the cheapest carrier");
        Assert.AreEqual(Person("Tor"), carrier.Person);
        Assert.AreEqual(Building("WainwrightsYard"), carrier.Building);

        ResidentNpc.TeachWhatTheyKnow(Person("Tor"), player, false);
        carrier = HavenAnswers.CarrierFor(crafting, Content, settlement);
        Assert.IsNull(carrier.Person, "the player knows the Yard now: nobody to fetch");
        Assert.AreEqual("", carrier.HowTo());
    }

    [Test]
    public void TheRaid_CountsWhatWasLeftBehind_AndTheSummarySaysWhatWouldHaveCarriedIt()
    {
        PlayerCrafting crafting = NewPlayer();
        RaidSession session = Session();
        session.RecordLeftBehind(Material("CopperOre"), 3);
        session.RecordLeftBehind(Material("CopperOre"), 2);
        session.End(RaidOutcome.Extracted);
        session.RecordLeftBehind(Material("CopperOre"), 9);

        RaidReport report = RaidReport.Create(session, null, null);
        HavenAnswers.Fill(report, crafting, Content, new SettlementState());

        Assert.AreEqual(1, report.ItemsLeftBehind.Count);
        Assert.AreEqual(5, report.ItemsLeftBehind[0].Amount, "nothing counts once the raid is over");
        Assert.AreEqual(Material("CopperOre").Weight * 5, report.WeightLeftBehind, 0.001f);
        string text = RaidReportText.Problems(report);
        StringAssert.Contains("Left behind", text);
        StringAssert.Contains("Wheelbarrow would have carried it. Bring Tor home", text);
    }

    [Test]
    public void TheSummary_SaysWhoCouldUseWhatCameHome()
    {
        PlayerCrafting crafting = NewPlayer();
        var bag = new ItemContainer(4, 1);
        PlayerSnapshot before = PlayerSnapshot.Capture(bag, null, null);
        bag.Add(Material("CopperOre"), 6);
        bag.Add(Material("OakPlank"), 2);
        PlayerSnapshot after = PlayerSnapshot.Capture(bag, null, null);
        RaidSession session = Session();
        session.End(RaidOutcome.Extracted);

        RaidReport report = RaidReport.Create(session, before, after);
        HavenAnswers.Fill(report, crafting, Content, new SettlementState());

        Assert.AreEqual(1, report.NoUseYet.Count, "the planks are the workshop's");
        Assert.AreEqual(Material("CopperOre"), report.NoUseYet[0].What);
        Assert.AreEqual(6, report.NoUseYet[0].Amount);
        string text = RaidReportText.Problems(report);
        StringAssert.Contains("Nobody at home can use these yet", text);
        StringAssert.Contains("Bring Bjarne home: the Smith knows how.", text);
    }

    // ----- Worn gear -----

    [Test]
    public void WornGear_NamesWhoWouldMendItForLess_UntilTheyWork()
    {
        var settlement = new SettlementState();
        HavenAnswer mender = HavenAnswers.Mender(Content, settlement);
        Assert.AreEqual(Person("Bjarne"), mender.Person);
        Assert.IsFalse(mender.PersonIsHome);

        settlement.AddResident(Person("Bjarne"));
        Assert.AreEqual("Give Bjarne the Smith job.", HavenAnswers.Mender(Content, settlement).HowTo());

        settlement.Find(Person("Bjarne")).Job = mender.Job;
        Assert.IsNull(HavenAnswers.Mender(Content, settlement), "a smith at work: nothing to say");
    }

    [Test]
    public void AnAxeChippedOnTheWay_IsStillTheOneCarriedIn()
    {
        ItemDefinition axe = Axe();
        var gear = new ItemContainer(4, 1);
        gear.SetSlot(0, new ItemStack(axe, 1));
        PlayerSnapshot loadout = PlayerSnapshot.Capture(new ItemContainer(2, 1), null, null, gear: gear);
        gear.SetDurability(0, 0.9f);
        PlayerSnapshot atTheEnd = PlayerSnapshot.Capture(new ItemContainer(2, 1), null, null, gear: gear);

        PlayerSnapshot kept = atTheEnd.AfterFailedRaid(loadout, 0.25f);
        Assert.AreEqual(1, kept.Gear.Count, "a glancing blow wore it, but it is still the axe carried in");
        Assert.AreEqual(0.65f, kept.Gear[0].Stack.Durability, 0.001f);

        List<EquipmentWear> worn = atTheEnd.WornSince(loadout);
        Assert.AreEqual(1, worn.Count);
        Assert.AreEqual(1f, worn[0].Before, 0.001f);
        Assert.AreEqual(0.9f, worn[0].After, 0.001f);
    }

    [Test]
    public void AnAxeFoundOutThere_InBetterShape_DoesNotTakeTheCarriedOnesPlace()
    {
        ItemDefinition axe = Axe();
        var gear = new ItemContainer(4, 1);
        gear.SetSlot(0, new ItemStack(axe, 1, 0.5f));
        PlayerSnapshot loadout = PlayerSnapshot.Capture(new ItemContainer(2, 1), null, null, gear: gear);
        gear.SetSlot(0, new ItemStack(axe, 1, 1f));
        PlayerSnapshot atTheEnd = PlayerSnapshot.Capture(new ItemContainer(2, 1), null, null, gear: gear);

        Assert.AreEqual(0, atTheEnd.AfterFailedRaid(loadout, 0.25f).Gear.Count);
    }

    private ItemDefinition Axe()
    {
        var axe = ScriptableObject.CreateInstance<ItemDefinition>();
        axe.name = "axe";
        created.Add(axe);
        var data = new SerializedObject(axe);
        data.FindProperty("toolType").intValue = (int)ToolType.Axe;
        data.FindProperty("toolTier").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        return axe;
    }

    // ----- Whoever is home teaches their workplace when the player appears -----

    [Test]
    public void EveryoneHome_TeachesTheirWorkplace_SoARaidRescueReachesHome()
    {
        GameSession.Home.AddResident(Person("Bjarne"));
        PlayerCrafting crafting = NewPlayer();
        Assert.IsFalse(crafting.IsKnown(Building("Forge")));

        ResidentNpc.TeachEveryoneHome(player);
        Assert.IsTrue(crafting.IsKnown(Building("Forge")));
    }

    // ----- The objectives -----

    [Test]
    public void StoreItem_CountsWhatIsPutAway()
    {
        QuestObjective anything = QuestObjective.Create(ObjectiveType.StoreItem, 2);
        Assert.AreEqual(3, anything.Progress(new GameEvent(GameEventKind.Stored, Material("OakPlank"), 3)));
        Assert.AreEqual(0, anything.Progress(new GameEvent(GameEventKind.ItemObtained, Material("OakPlank"), 3)));
        QuestObjective ore = QuestObjective.Create(ObjectiveType.StoreItem, 1, Material("CopperOre"));
        Assert.AreEqual(0, ore.Progress(new GameEvent(GameEventKind.Stored, Material("OakPlank"), 1)));
        Assert.AreEqual("Put 2 things in a chest", anything.Describe());
        Assert.AreEqual("Put something in a chest", QuestObjective.Create(ObjectiveType.StoreItem, 1).Describe());
    }

    [Test]
    public void TheStash_SaysWhatWasPutIn()
    {
        var stash = new StashStorage();
        var bag = new ItemContainer(2, 1);
        bag.Add(Material("OakPlank"), 4);
        int heard = 0;
        Action<GameEvent> listen = raised =>
        {
            if (raised.Kind == GameEventKind.Stored && raised.Subject == Material("OakPlank")) heard += raised.Amount;
        };
        GameEvents.Raised += listen;
        try
        {
            stash.Store(bag, 0, null);
        }
        finally
        {
            GameEvents.Raised -= listen;
        }

        Assert.AreEqual(4, heard);
        Assert.AreEqual(4, stash.Items.Count(Material("OakPlank")));
        Assert.AreEqual(0, bag.Count(Material("OakPlank")));
    }

    [Test]
    public void RepairItem_CountsARepair()
    {
        QuestObjective repair = QuestObjective.Create(ObjectiveType.RepairItem, 1);
        Assert.AreEqual(1, repair.Progress(new GameEvent(GameEventKind.Repaired, Material("OakPlank"), 1)));
        Assert.AreEqual(0, repair.Progress(new GameEvent(GameEventKind.Crafted, Material("OakPlank"), 1)));
        Assert.IsFalse(repair.CanProgressOnARaid);
    }

    [Test]
    public void UpgradeBuilding_CountsTheTierReached()
    {
        QuestObjective lodgeII = QuestObjective.Upgrade(Building("Sawbench"), 2);
        Assert.AreEqual(1, lodgeII.Progress(new GameEvent(GameEventKind.Upgraded, Building("Sawbench"), 2)));
        Assert.AreEqual(1, lodgeII.Progress(new GameEvent(GameEventKind.Upgraded, Building("Sawbench"), 3)), "past it counts");
        Assert.AreEqual(0, lodgeII.Progress(new GameEvent(GameEventKind.Upgraded, Building("Forge"), 2)));
        Assert.AreEqual(0, lodgeII.Progress(new GameEvent(GameEventKind.Built, Building("Sawbench"), 1)), "building it is tier I");
        Assert.AreEqual(0, QuestObjective.Upgrade(Building("Sawbench"), 3).Progress(new GameEvent(GameEventKind.Upgraded, Building("Sawbench"), 2)));
        // The key is named by its action; the views that show the line turn it into the bound key (GameInput.WithRebinds).
        Assert.AreEqual("Raise the Woodcutter's Lodge to Tier II ({key:Haven})", lodgeII.Describe());
        Assert.IsNull(lodgeII.FindProblem());
        Assert.IsNotNull(QuestObjective.Upgrade(Building("Sawbench"), 99).FindProblem(), "no such tier");
    }
}
