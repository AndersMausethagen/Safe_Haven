using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wanted at home (Docs/SystemsGapReview_2026-11-19.md §1): the portal's next tier and up to three tracked building levels say what they
/// still need, less what is home; through the real settlement, the shipped costs and a material pool as the game makes one.
/// </summary>
public sealed class WantedAtHomeTests
{
    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    [Test]
    public void ThePortalsNextTier_IsAlwaysWanted_LessWhatIsHome()
    {
        var settlement = new SettlementState();
        settlement.RepairPortal();
        SettlementSettings.WardLevel next = PortalTiers.Next(settlement);
        Assert.IsNotNull(next, "a repaired portal has a next tier");
        Assert.Greater(next.cost.Count, 0);
        ResourceAmount first = next.cost[0];

        var stash = new ResourceStorage(9999);
        stash.Add(first.Resource, first.Amount - 2);
        var home = new MaterialPool(new ResourceStorage(), new ItemContainer(4, 4), stash, new ItemContainer(4, 4));
        List<WantedAtHome.Want> wants = WantedAtHome.Compute(WantedAtHome.Targets(settlement, new CharacterProgress(), Content), home);
        WantedAtHome.Want want = wants.Find(w => w.Resource == first.Resource);
        Assert.AreEqual(2, want.Short, $"{first.Resource.DisplayName}: two more than the stash holds");
        StringAssert.Contains("portal", want.For);

        stash.Add(first.Resource, 5);
        wants = WantedAtHome.Compute(WantedAtHome.Targets(settlement, new CharacterProgress(), Content), home);
        Assert.IsFalse(wants.Exists(w => w.Resource == first.Resource), "all of it home: no longer wanted");
    }

    [Test]
    public void ATrackedLevel_WantsItsCost_AndAFourthLetsTheOldestGo()
    {
        var smithy = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Forge.asset");
        Assert.IsNotNull(smithy);
        BuildingLevel second = smithy.UpgradeTo(2);
        Assert.IsNotNull(second, "the Smithy has a second tier");
        Assert.Greater(second.cost.Count, 0);
        var progress = new CharacterProgress();
        Assert.IsTrue(progress.ToggleTracked(WantedAtHome.BuildingKey(smithy, 2), WantedAtHome.MostTracked));
        List<WantedAtHome.Target> targets = WantedAtHome.Targets(new SettlementState(), progress, Content);
        WantedAtHome.Target tracked = targets.Find(t => t.Name.Contains(smithy.DisplayName));
        Assert.IsNotNull(tracked, "the tracked level is a target");
        Assert.AreEqual(second.cost.Count, tracked.Items.Count);
        List<WantedAtHome.Want> wants = WantedAtHome.Compute(targets, new MaterialPool(new ResourceStorage(), new ItemContainer(4, 4)));
        Assert.IsTrue(wants.Exists(w => w.Item == second.cost[0].Item && w.Short == second.cost[0].Amount), "nothing home: the whole cost is wanted");

        progress.ToggleTracked("building:a:2", WantedAtHome.MostTracked);
        progress.ToggleTracked("building:b:2", WantedAtHome.MostTracked);
        progress.ToggleTracked("building:c:2", WantedAtHome.MostTracked);
        Assert.AreEqual(WantedAtHome.MostTracked, progress.Tracked.Count);
        Assert.IsFalse(progress.IsTracked(WantedAtHome.BuildingKey(smithy, 2)), "the oldest went");
        Assert.IsFalse(progress.ToggleTracked("building:c:2", WantedAtHome.MostTracked), "tracked again: untracked");
    }

    // A fuller homecoming (§2): what a station holds ready, what the people made, and an answerable ask, each a line.
    [Test]
    public void TheHomecoming_SaysWhatIsReady_AndWhatThePeopleMade()
    {
        var home = new SettlementState();
        var player = new GameObject("Player", typeof(Player), typeof(ItemInventory));
        try
        {
            Assert.AreEqual(0, Homecoming.Compose(player.GetComponent<Player>(), home).FindAll(l => !l.StartsWith("Within reach")).Count, "nothing waiting: nothing to say");
            var plank = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/StoneBlock.asset");
            home.MadeByWorkers[plank] = 4;
            List<string> lines = Homecoming.Compose(player.GetComponent<Player>(), home);
            Assert.IsTrue(lines.Exists(l => l.Contains("Your people made 4 " + plank.DisplayName)), string.Join(" | ", lines));
            Assert.LessOrEqual(lines.Count, Homecoming.MostLines);
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }
}
