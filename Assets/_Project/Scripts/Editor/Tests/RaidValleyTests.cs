using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Troll Valley's Valley shape (Docs/TrollValleyPlan.md step 3), on plans and height fields only (no terrain): a valley from edge to edge
/// with a river down it, the start at its mouth and the main way out at its head, everything that stands on the map on the floor and out
/// of the water, every trail crossing the river at most once and squarely, walls high on both sides, walkable trails, and the same seed
/// giving the same valley. The Misty Woods' maps are pinned by RaidLayoutGoldenTests.
/// </summary>
public sealed class RaidValleyTests
{
    private const string ValleyRules = "Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset";
    private const string WoodsRules = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const float Size = 200f;
    private const float SteepestTrail = 30f;
    private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    private static RaidLayoutRules Rules => AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(ValleyRules);

    private static IEnumerable<(int seed, RaidLayoutPlan plan)> Plans()
    {
        foreach (int seed in Seeds)
        {
            yield return (seed, RaidLayoutPlanner.Plan(Rules, Size, seed));
        }
    }

    [Test]
    public void TrollValley_IsAlwaysAValley_AndTheWoodsNever()
    {
        foreach (RaidShapeRule shape in Rules.Shapes)
        {
            Assert.AreEqual(RaidLayoutShape.Valley, shape.shape);
        }

        foreach (RaidShapeRule shape in AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(WoodsRules).Shapes)
        {
            Assert.AreNotEqual(RaidLayoutShape.Valley, shape.shape);
        }

        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            Assert.IsNotNull(plan.Valley, $"seed {seed}");
            Assert.AreEqual(1, plan.Rivers.Count, $"seed {seed}: one river, the valley's");
        }
    }

    [Test]
    public void TheRiver_RunsFromEdgeToEdge_AndOnlyDownhill()
    {
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            RaidLayoutField field = RaidLayoutField.Build(plan, Rules);
            RaidLayoutRiver river = plan.Rivers[0];
            Assert.IsFalse(Inside(river.Line[0]), $"seed {seed}: the river comes in from beyond the map");
            Assert.IsFalse(Inside(river.Line[river.Line.Count - 1]), $"seed {seed}: and leaves it");
            for (int i = 1; i < river.Level.Length; i++)
            {
                Assert.LessOrEqual(river.Level[i], river.Level[i - 1] + 1e-4f, $"seed {seed}: the river never flows uphill");
            }

            Assert.Greater(river.Level[0] - river.Level[river.Level.Length - 1], 2f, $"seed {seed}: it falls down the valley's tilt");
            Assert.IsNotNull(field);
        }
    }

    [Test]
    public void TheStart_IsAtTheMouth_AndTheMainExit_AtTheHead()
    {
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            Assert.Less(plan.Valley.ShareOf(plan.Start.Position), 0.2f, $"seed {seed}");
            Assert.GreaterOrEqual(plan.MainExit, 0, $"seed {seed}");
            Assert.Greater(plan.Valley.ShareOf(plan.Rooms[plan.MainExit].Position), 0.8f, $"seed {seed}");
            RaidLayoutRoom apex = plan.Rooms.Find(room => room.Role == RaidRoomRole.Apex);
            Assert.IsNotNull(apex, $"seed {seed}");
            Assert.Greater(plan.Valley.ShareOf(apex.Position), 0.5f, $"seed {seed}: the danger waits up the valley");
        }
    }

    [Test]
    public void EveryClearingAndSite_StandsOnTheFloor_OutOfTheWater()
    {
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                Assert.IsTrue(plan.Valley.Holds(room.Position, room.Radius), $"seed {seed}: a {room.Role} clearing reaches up a wall");
                Assert.IsFalse(plan.InRiver(room.Position, room.Radius), $"seed {seed}: a {room.Role} clearing is in the river");
            }

            foreach (RaidSite site in plan.Sites)
            {
                Vector2 spot = site.Position * Size;
                Assert.LessOrEqual(plan.Valley.FloorEdge(spot), 0f, $"seed {seed}: a {site.Kind} up a wall");
                Assert.IsFalse(plan.InRiver(spot, 1f), $"seed {seed}: a {site.Kind} in the river");
            }

            foreach (RaidLayoutPiece piece in plan.Pieces)
            {
                Assert.IsFalse(plan.InRiver(piece.Position, piece.Definition.Radius), $"seed {seed}: a set piece in the river");
            }
        }
    }

    [Test]
    public void EveryTrail_CrossesTheRiverAtMostOnce_Squarely()
    {
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            RaidLayoutValley valley = plan.Valley;
            for (int t = 0; t < plan.Trails.Count; t++)
            {
                List<Vector2> trail = plan.Trails[t];
                int changes = 0;
                for (int i = 0; i + 1 < trail.Count; i++)
                {
                    changes += valley.BankOf(trail[i]) != valley.BankOf(trail[i + 1]) ? 1 : 0;
                }

                bool crosses = valley.BankOf(trail[0]) != valley.BankOf(trail[trail.Count - 1]);
                Assert.AreEqual(crosses ? 1 : 0, changes, $"seed {seed}: trail {t} crosses the river {changes} times");
                Assert.AreEqual(crosses ? 1 : 0, plan.Crossings.FindAll(c => c.Trail == t).Count, $"seed {seed}: trail {t}'s crossing is recorded");
            }

            foreach (RaidLayoutCrossing crossing in plan.Crossings)
            {
                float along = valley.AlongOf(crossing.Position);
                Vector2 tangent = (valley.CentreAt(along + 2f, out _) - valley.CentreAt(along - 2f, out _)).normalized;
                Assert.Less(Mathf.Abs(Vector2.Dot(tangent, crossing.Direction)), 0.05f, $"seed {seed}: a crossing goes square over the river");
                Assert.Greater(crossing.Reach, plan.Rivers[0].HalfWidths[0], $"seed {seed}: a crossing spans the water and its banks");
            }
        }
    }

    [Test]
    public void TheWalls_RiseHighOnBothSides()
    {
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            RaidLayoutField field = RaidLayoutField.Build(plan, Rules);
            RaidLayoutValley valley = plan.Valley;
            foreach (float share in new[] { 0.3f, 0.5f, 0.7f })
            {
                float along = Size * share;
                Vector2 centre = valley.CentreAt(along, out float half);
                float floor = Height(field, centre + valley.Across * (half * 0.5f));
                foreach (int side in new[] { 1, -1 })
                {
                    Vector2 top = centre + valley.Across * side * (half + valley.WallRun + 2f);
                    if (!Inside(top))
                    {
                        continue;
                    }

                    Assert.Greater(Height(field, top) - floor, 12f, $"seed {seed}: the {(side > 0 ? "left" : "right")} wall at {share:0.0} of the way up");
                }
            }
        }
    }

    [Test]
    public void Trails_StayWalkable()
    {
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            RaidLayoutField field = RaidLayoutField.Build(plan, Rules);
            float steepest = 0f;
            foreach (List<Vector2> trail in plan.Trails)
            {
                for (int i = 0; i + 1 < trail.Count; i++)
                {
                    float length = Vector2.Distance(trail[i], trail[i + 1]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 a = Vector2.Lerp(trail[i], trail[i + 1], k / (float)steps);
                        Vector2 b = Vector2.Lerp(trail[i], trail[i + 1], (k + 1) / (float)steps);
                        if (plan.InRiver(a, 6.5f) || plan.InRiver(b, 6.5f))
                        {
                            continue; // a ford's banks slope down into the water by design (6 m: RiverBank and RiverFordBank)
                        }

                        float rise = Mathf.Abs(Height(field, b) - Height(field, a));
                        steepest = Mathf.Max(steepest, Mathf.Atan2(rise, Vector2.Distance(a, b)) * Mathf.Rad2Deg);
                    }
                }
            }

            Assert.Less(steepest, SteepestTrail, $"seed {seed}: the steepest trail");
        }
    }

    [Test]
    public void EveryValley_HasTollBridges_AndTheBrukamHasLeftForHisOwnBridge()
    {
        // The Brukam fights in his own encounter now (Brukam's Bridge, 2026-11-17): every valley bridge asks its toll.
        Assert.IsNull(Rules.Valley.bossBridge, "the Brukam is back on a valley bridge");
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            int bridged = plan.Crossings.FindAll(c => c.Bridge).Count;
            Assert.GreaterOrEqual(bridged, 1, $"seed {seed}: no bridge, so no toll");
            if (plan.Crossings.Count >= 2)
            {
                Assert.GreaterOrEqual(bridged, 2, $"seed {seed}: two toll bridges");
            }
        }
    }

    [Test]
    public void EveryToll_SitsOnTheBankTheWayUpComesFrom()
    {
        // The valley's bend chose the bank (review 2026-11-17, R4): on some stretches the player crossed the whole bridge before the toll.
        // Walking the short way from the start to the main way out, each toll bridge on it is met on the bank the walk comes from.
        int met = 0;
        foreach ((int seed, RaidLayoutPlan plan) in Plans())
        {
            List<int> way = plan.MainExit >= 0 ? RaidLayoutPlanner.ShortestPath(plan, 0, plan.MainExit, null) : null;
            if (way == null)
            {
                continue;
            }

            int room = 0;
            foreach (int link in way)
            {
                (int a, int b) = plan.Links[link];
                int next = a == room ? b : a;
                RaidLayoutCrossing bridge = plan.Crossings.Find(c => c.Bridge && c.Trail == link);
                if (bridge != null)
                {
                    met++;
                    Assert.AreEqual(room, RaidLayoutPlanner.ArrivalRoom(plan, bridge), $"seed {seed}: the walk comes to the bridge from clearing {room}");
                    Vector2 toll = RaidMapGenerator.TollSpot(plan, bridge, 6f);
                    Assert.AreEqual(plan.Valley.BankOf(plan.Rooms[room].Position), plan.Valley.BankOf(toll), $"seed {seed}: the toll waits on the near bank");
                    Assert.AreNotEqual(plan.Valley.BankOf(plan.Rooms[next].Position), plan.Valley.BankOf(toll), $"seed {seed}: not across the river");
                    Assert.AreEqual(6f + 2.5f, Vector2.Distance(toll, bridge.Position), 0.01f, $"seed {seed}: just past the bridge's end");
                }

                room = next;
            }
        }

        Assert.Greater(met, 0, "some short way crosses a toll bridge");
    }

    [Test]
    public void TheSameSeed_MakesTheSameValley()
    {
        foreach (int seed in new[] { 3, 9 })
        {
            Assert.AreEqual(RaidLayoutGoldenTests.Fingerprint(Rules, Size, seed), RaidLayoutGoldenTests.Fingerprint(Rules, Size, seed));
        }

        Assert.AreNotEqual(RaidLayoutGoldenTests.Fingerprint(Rules, Size, 3), RaidLayoutGoldenTests.Fingerprint(Rules, Size, 9));
    }

    private static bool Inside(Vector2 point) => point.x >= 0f && point.y >= 0f && point.x <= Size && point.y <= Size;

    private static float Height(RaidLayoutField field, Vector2 point) => field.HeightAt(point.x / Size, point.y / Size);
}
