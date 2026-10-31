using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Generated raid maps: fair, different every run, and the same from the same seed.</summary>
public sealed class RaidLayoutTests
{
    private const float Size = 200f;
    private RaidLayoutRules rules;

    [SetUp]
    public void SetUp()
    {
        rules = ScriptableObject.CreateInstance<RaidLayoutRules>();
        rules.Configure(
            new[]
            {
                new RaidShapeRule { shape = RaidLayoutShape.Hub, loopChance = 0.1f },
                new RaidShapeRule { shape = RaidLayoutShape.Ring, loopChance = 0.1f },
                new RaidShapeRule { shape = RaidLayoutShape.Spine, loopChance = 0.1f },
                new RaidShapeRule { shape = RaidLayoutShape.Thicket, loopChance = 0.4f },
            },
            new[] { new RaidMapMood { name = "Close" }, new RaidMapMood { name = "Open", forest = 0.5f, rooms = 1.3f } },
            new[]
            {
                new RaidSiteQuota { kind = RaidSiteKind.Rumour, placement = RaidSitePlacement.DeadEnd, count = 3, separation = 40f, fromStart = 70f },
                new RaidSiteQuota { kind = RaidSiteKind.EnemyCamp, placement = RaidSitePlacement.Room, count = 8, separation = 22f, fromStart = 40f },
                new RaidSiteQuota { kind = RaidSiteKind.ResourceSite, placement = RaidSitePlacement.BesidePath, count = 12, separation = 16f, fromStart = 12f, clearing = 11f },
                new RaidSiteQuota { kind = RaidSiteKind.Patrol, placement = RaidSitePlacement.OnPath, count = 8, separation = 18f, fromStart = 35f, clearing = 0f },
            },
            new RaidGroupRule[0]);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(rules);

    private static IEnumerable<int> Seeds => Enumerable.Range(1, 60);

    [Test]
    public void TheSameSeed_GivesTheSameMap_AndAnotherSeedAnotherOne()
    {
        RaidLayoutPlan first = RaidLayoutPlanner.Plan(rules, Size, 1234);
        RaidLayoutPlan again = RaidLayoutPlanner.Plan(rules, Size, 1234);
        RaidLayoutPlan other = RaidLayoutPlanner.Plan(rules, Size, 1235);

        CollectionAssert.AreEqual(first.Rooms.Select(room => room.Position), again.Rooms.Select(room => room.Position));
        CollectionAssert.AreEqual(first.Links, again.Links);
        CollectionAssert.AreEqual(first.Sites.Select(site => site.Position), again.Sites.Select(site => site.Position));
        CollectionAssert.AreNotEqual(first.Rooms.Select(room => room.Position), other.Rooms.Select(room => room.Position));
    }

    [Test]
    public void EveryClearing_CanBeReachedFromTheStart_OnEveryMap()
    {
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            var reached = new HashSet<int> { 0 };
            var open = new Stack<int>();
            open.Push(0);
            while (open.Count > 0)
            {
                int room = open.Pop();
                foreach ((int a, int b) in plan.Links)
                {
                    int next = a == room ? b : b == room ? a : -1;
                    if (next >= 0 && reached.Add(next))
                    {
                        open.Push(next);
                    }
                }
            }

            Assert.AreEqual(plan.Rooms.Count, reached.Count, $"seed {seed}");
        }
    }

    [Test]
    public void TheExits_LieFarFromTheStart_AndApartFromEachOther()
    {
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            List<RaidSite> exits = plan.SitesOf(RaidSiteKind.Extraction);
            Assert.AreEqual(3, exits.Count, $"seed {seed}");
            Vector2 start = plan.Start.Position / Size;
            foreach (RaidSite exit in exits)
            {
                Assert.Greater(Vector2.Distance(exit.Position, start), 0.45f, $"seed {seed}");
            }

            Assert.Greater(Vector2.Distance(exits[0].Position, exits[1].Position), 0.2f, $"seed {seed}");
            Assert.Greater(Vector2.Distance(exits[1].Position, exits[2].Position), 0.2f, $"seed {seed}");
        }
    }

    [Test]
    public void AMap_HasItsApex_AndTheSitesItWasPromised()
    {
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            Assert.AreEqual(1, plan.SitesOf(RaidSiteKind.Apex).Count, $"seed {seed}");
            Assert.GreaterOrEqual(plan.SitesOf(RaidSiteKind.Rumour).Count, 2, $"seed {seed}: rumours need somewhere to stand");
            Assert.GreaterOrEqual(plan.SitesOf(RaidSiteKind.EnemyCamp).Count, 5, $"seed {seed}");
            Assert.GreaterOrEqual(plan.SitesOf(RaidSiteKind.ResourceSite).Count, 8, $"seed {seed}");
        }
    }

    [Test]
    public void NoCampStands_RightAtTheStart()
    {
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            foreach (RaidSite camp in plan.SitesOf(RaidSiteKind.EnemyCamp))
            {
                Assert.GreaterOrEqual(Vector2.Distance(camp.Position * Size, plan.Start.Position), 39.9f, $"seed {seed}");
            }
        }
    }

    [Test]
    public void EveryShape_ComesUp_AndGivesDifferentMaps()
    {
        var shapes = new HashSet<RaidLayoutShape>();
        var linkCounts = new HashSet<int>();
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            shapes.Add(plan.Shape.shape);
            linkCounts.Add(plan.Links.Count);
        }

        Assert.AreEqual(4, shapes.Count);
        Assert.Greater(linkCounts.Count, 2);
    }

    [Test]
    public void TrailsAndClearings_LieLowerThanTheWildGroundBetweenThem()
    {
        foreach (int seed in Seeds.Take(8))
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            RaidLayoutField field = RaidLayoutField.Build(plan, rules);
            Vector2 room = plan.Rooms[1].Position / Size;
            Assert.LessOrEqual(field.OpenDistance(room.x, room.y), 0f, $"seed {seed}");

            float wild = 0f;
            for (int i = 1; i < 30; i++)
            {
                for (int j = 1; j < 30; j++)
                {
                    float u = i / 30f;
                    float v = j / 30f;
                    if (field.OpenDistance(u, v) > rules.RidgeWidth)
                    {
                        wild = Mathf.Max(wild, field.HeightAt(u, v));
                    }
                }
            }

            Assert.Greater(wild, field.HeightAt(room.x, room.y), $"seed {seed}");
        }
    }

    [Test]
    public void ThePlannedShapeAndMood_DontDependOnTheAttempt()
    {
        (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(rules, 77);
        RaidLayoutPlan retried = RaidLayoutPlanner.Plan(rules, Size, 77, 2);
        Assert.AreSame(shape, retried.Shape);
        Assert.AreSame(mood, retried.Mood);
        CollectionAssert.AreNotEqual(RaidLayoutPlanner.Plan(rules, Size, 77).Rooms.Select(room => room.Position), retried.Rooms.Select(room => room.Position));
    }
}
