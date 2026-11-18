using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Compounds (Docs/WorldDesignReview_2026-11-17.md G3): set pieces that take a clearing of their own and ring its yard with wall pieces.
/// On plans only (no terrain): a compound stands in a plain clearing or a yard of its own beside a trail, away from the start and from any
/// site, every way in is left open so no wall piece ever stands on a trail or a side path (past its edge), a yard has a back opening too,
/// and the Woods (whose layouts are pinned) have none.
/// </summary>
public sealed class RaidCompoundTests
{
    // The Moss Marsh has compounds (its Bloomery Yard); Troll Valley's clearings and floor were too full for one (2026-11-18).
    private const string MarshRules = "Assets/_Project/Data/Raids/Maps/MossMarsh Layout.asset";
    private const string WoodsRules = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const float Size = 200f;

    private static IEnumerable<(int seed, RaidLayoutPlan plan, RaidLayoutPiece compound)> Compounds(string rulesPath, int seeds)
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(rulesPath);
        for (int seed = 1; seed <= seeds; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            foreach (RaidLayoutPiece piece in plan.Pieces)
            {
                if (piece.Definition.OwnsClearing)
                {
                    yield return (seed, plan, piece);
                }
            }
        }
    }

    [Test]
    public void TheMarsh_OftenHasACompound_InAClearingOrAYardOfItsOwn_AwayFromTheStart()
    {
        int found = 0;
        foreach ((int seed, RaidLayoutPlan plan, RaidLayoutPiece compound) in Compounds(MarshRules, 40))
        {
            found++;
            // In a clearing's middle (never the start's, an exit's or the apex's), or in a yard of its own beside a trail.
            RaidLayoutRoom room = plan.Rooms.Find(each => each.Position == compound.Position);
            if (room != null)
            {
                Assert.That(room.Role == RaidRoomRole.Plain || room.Role == RaidRoomRole.Hub, $"seed {seed}: never the start, an exit or the apex's");
            }
            else
            {
                Assert.IsTrue(plan.Spurs.Exists(spur => spur[spur.Count - 1] == compound.Position), $"seed {seed}: a yard beside a trail has its side path");
                foreach (RaidLayoutRoom other in plan.Rooms)
                {
                    Assert.Greater(Vector2.Distance(other.Position, compound.Position), other.Radius + compound.WallRadius, $"seed {seed}: the yard keeps off clearings");
                }
            }

            Assert.Greater(compound.WallRadius, compound.Definition.Radius, $"seed {seed}: the piece fits inside its ring");
            Assert.Greater(Vector2.Distance(compound.Position, plan.Start.Position), 40f, $"seed {seed}: not where the run begins");
            foreach (RaidSite site in plan.Sites)
            {
                Assert.GreaterOrEqual(Vector2.Distance(site.Position * plan.Size, compound.Position), compound.Definition.Radius,
                    $"seed {seed}: a {site.Kind} site stands inside the compound's ruin");
            }
        }

        Assert.GreaterOrEqual(found, 6, "a compound on a share of Marsh runs (not where its Bloomery already stands for a rumour)");
    }

    [Test]
    public void NoWallPiece_StandsOnATrailOrASidePath_AndEveryYardHasABackWay()
    {
        foreach ((int seed, RaidLayoutPlan plan, RaidLayoutPiece compound) in Compounds(MarshRules, 40))
        {
            RaidSetPieceDefinition definition = compound.Definition;
            float radius = compound.WallRadius;
            var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(MarshRules);
            float trailHalf = rules.TrailHalfWidth * plan.Mood.trails;
            int whole = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / definition.PieceLength));
            Assert.GreaterOrEqual(compound.Wall.Count, whole * 0.45f, $"seed {seed}: a yard with a wall round most of it, not stubs");
            foreach (float angle in compound.Wall)
            {
                // The piece's whole length, as the generator lays it: every trail and side path clears it.
                float rad = angle * Mathf.Deg2Rad;
                Vector2 outward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)), along = new Vector2(-outward.y, outward.x);
                Vector2 middle = compound.Position + outward * radius;
                for (float share = -0.5f; share <= 0.5f; share += 0.125f)
                {
                    Vector2 at = middle + along * definition.PieceLength * share;
                    foreach (List<Vector2> trail in plan.Trails)
                    {
                        Assert.Greater(Distance(trail, at), trailHalf + 0.5f, $"seed {seed}: a wall piece at {angle:0}° stands on a trail (past its edge)");
                    }

                    foreach (List<Vector2> spur in plan.Spurs)
                    {
                        Assert.Greater(Distance(spur, at), trailHalf * 0.6f + 0.5f, $"seed {seed}: a wall piece at {angle:0}° stands on a side path");
                    }
                }
            }

            Assert.Greater(compound.Openings.Count, 1, $"seed {seed}: a way in and a way out at least");
        }
    }

    [Test]
    public void TheWoods_HaveNoCompounds_TheirLayoutsArePinned()
    {
        foreach ((int seed, RaidLayoutPlan _, RaidLayoutPiece _) in Compounds(WoodsRules, 10))
        {
            Assert.Fail($"seed {seed}: the Woods got a compound (their golden layouts would move)");
        }
    }

    [Test]
    public void TheOpenings_AreWhereLinesCrossTheRing()
    {
        var plan = new RaidLayoutPlan { Size = Size };
        plan.Trails.Add(new List<Vector2> { new Vector2(100f, 100f), new Vector2(130f, 100f) });  // out to the east
        plan.Trails.Add(new List<Vector2> { new Vector2(80f, 120f), new Vector2(80f, 80f) });     // passing by, west: crosses twice
        var openings = new List<float>();
        RaidLayoutPlanner.CompoundOpenings(plan, new Vector2(100f, 100f), 22f, 0, new System.Random(1), openings);
        Assert.AreEqual(3, openings.Count);
        Assert.AreEqual(0f, openings[0], 0.5f, "east, where the trail leaves");
        float west = Mathf.Acos(-20f / 22f) * Mathf.Rad2Deg;
        Assert.AreEqual(west, Mathf.Abs(openings[1]), 0.5f);
        Assert.AreEqual(west, Mathf.Abs(openings[2]), 0.5f);
        Assert.IsTrue(RaidMapGenerator.CompoundOpenAt(5f, openings, 10f));
        Assert.IsFalse(RaidMapGenerator.CompoundOpenAt(90f, openings, 10f));
    }

    private static IEnumerable<List<Vector2>> Lines(RaidLayoutPlan plan)
    {
        foreach (List<Vector2> trail in plan.Trails)
        {
            yield return trail;
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            yield return spur;
        }
    }

    private static float Distance(List<Vector2> line, Vector2 point)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a = line[i], b = line[i + 1], ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
        }

        return best;
    }
}
