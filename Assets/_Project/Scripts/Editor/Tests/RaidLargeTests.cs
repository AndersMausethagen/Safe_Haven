using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RaidLargeTests
{
    private const float Size = 200f;
    private const float Width = 2f;

    private static RaidDefinition Misty => AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
    private static RaidLayoutRules Rules => Misty.LayoutRules;

    [Test]
    public void MistyWoodsCanBeLarge()
    {
        Assert.IsTrue(Misty.CanBeLarge);
        Assert.AreEqual(Width, Misty.LargeRaid.widthScale);
        Assert.Greater(Misty.LargeRaid.extraSeconds, 0f);
    }

    [Test]
    public void ALargeMapHasAsMuchOnEverySquareMetre()
    {
        RaidLayoutRules large = Rules.ScaledBy(Width);
        Assert.AreEqual(Rules.Rooms.x * 4, large.Rooms.x);
        Assert.AreEqual(Rules.Rooms.y * 4, large.Rooms.y);
        Assert.AreEqual(Rules.FieldResolution * 2, large.FieldResolution, "The same detail per metre");
        foreach (RaidSiteQuota quota in Rules.Sites)
        {
            int wanted = quota.count * (quota.kind == RaidSiteKind.Rumour ? 2 : 4);
            Assert.AreEqual(wanted, large.Sites.First(one => one.kind == quota.kind).count, quota.kind.ToString());
        }

        Object.DestroyImmediate(large);
    }

    [Test]
    public void GrowingARaidLeavesTheAssetsAlone()
    {
        Vector2Int rooms = Rules.Rooms;
        int count = Rules.Sites[0].count;
        RaidLayoutRules large = Rules.ScaledBy(Width);
        Assert.AreEqual(rooms, Rules.Rooms);
        Assert.AreEqual(count, Rules.Sites[0].count);
        Object.DestroyImmediate(large);
    }

    [Test]
    public void ALargePlanFillsTheWiderMap()
    {
        RaidLayoutRules large = Rules.ScaledBy(Width);
        RaidLayoutPlan normal = RaidLayoutPlanner.Plan(Rules, Size, 11);
        RaidLayoutPlan big = RaidLayoutPlanner.Plan(large, Size * Width, 11);
        Assert.Greater(big.Rooms.Count, normal.Rooms.Count * 2, "Far more clearings");
        Assert.Greater(big.Sites.Count, normal.Sites.Count * 2);
        Assert.IsTrue(big.Rooms.Exists(room => room.Position.x > Size || room.Position.y > Size), "Out past where the normal map ends");
        Object.DestroyImmediate(large);
    }

    [Test]
    public void SpawnsGrowWithTheGroundButHuntersDont()
    {
        var rules = new SpawnRules(1f, null, 60f, 110f, 4f);
        RaidSpawnList creatures = Misty.SpawnLists.First(list => list.name == "Creatures");
        RaidSpawnList hunters = Misty.SpawnLists.First(list => list.name == "Hunters");
        RaidSpawnList resources = Misty.SpawnLists.First(list => list.name == "Resources");
        Assert.AreEqual(creatures.MaxCount * 4, creatures.CountFor(rules).y);
        Assert.AreEqual(resources.MaxCount * 4, resources.CountFor(rules).y);
        Assert.AreEqual(hunters.MaxCount, hunters.CountFor(rules).y, "They come where the player is");
    }

    [Test]
    public void ANormalRaidIsUntouched()
    {
        var rules = new SpawnRules(1f, null, 60f, 110f);
        RaidSpawnList creatures = Misty.SpawnLists.First(list => list.name == "Creatures");
        Assert.AreEqual(creatures.MaxCount, creatures.CountFor(rules).y);
    }

    [Test]
    public void RoomsAreReachedHopByHop()
    {
        RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, 3);
        bool[] all = RaidMapGenerator.RoomsReached(plan, (a, b) => true);
        Assert.IsTrue(all.All(reached => reached), "The trails join everything");

        bool[] none = RaidMapGenerator.RoomsReached(plan, (a, b) => false);
        Assert.AreEqual(1, none.Count(reached => reached), "Only the start, if no way is open");
    }

    [Test]
    public void ABlockedTrailCutsOffWhatIsOnlyBehindIt()
    {
        var plan = new RaidLayoutPlan { Size = 100f };
        for (int i = 0; i < 3; i++)
        {
            plan.Rooms.Add(new RaidLayoutRoom { Position = new Vector2(i * 10f, 0f) });
        }

        plan.Links.Add((0, 1));
        plan.Links.Add((1, 2));
        bool[] reached = RaidMapGenerator.RoomsReached(plan, (a, b) => a.x + b.x < 25f);
        CollectionAssert.AreEqual(new[] { true, true, false }, reached);
    }

    [Test]
    public void ALargeRaidHasMoreRumours()
    {
        var settlement = new SettlementState();
        List<RaidRumourDefinition> normal = RaidRumours.For(Misty, 21, settlement, null);
        List<RaidRumourDefinition> large = RaidRumours.For(Misty, 21, settlement, null, Width);
        Assert.Greater(large.Count, normal.Count);
    }
}
