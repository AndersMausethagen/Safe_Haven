using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The Haven world's plan from the project's recipe: the same seed always gives the same world (so a new game's seed can
/// make its own), and what it strews over the mountain keeps to its rules.</summary>
public sealed class HavenWorldPlanTests
{
    private HavenWorldRecipe recipe;

    [SetUp]
    public void SetUp()
    {
        recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        Assert.That(recipe, Is.Not.Null, "The Haven World Recipe asset is missing.");
    }

    [Test]
    public void SameSeedPlansTheSameWorld()
    {
        HavenWorldPlan a = HavenWorldPlan.Make(recipe, 7);
        HavenWorldPlan b = HavenWorldPlan.Make(recipe, 7);
        Assert.That(b.Pois.Select(poi => poi.At), Is.EqualTo(a.Pois.Select(poi => poi.At)));
        Assert.That(b.Nodes.Select(node => node.At), Is.EqualTo(a.Nodes.Select(node => node.At)));
        Assert.That(b.Rocks.Select(rock => rock.At), Is.EqualTo(a.Rocks.Select(rock => rock.At)));
        Assert.That(b.Landings, Is.EqualTo(a.Landings));
        Assert.That(b.Lake.Position, Is.EqualTo(a.Lake.Position));
    }

    [Test]
    public void AnotherSeedPlansAnotherWorld()
    {
        HavenWorldPlan a = HavenWorldPlan.Make(recipe, 7);
        HavenWorldPlan b = HavenWorldPlan.Make(recipe, 8);
        Assert.That(b.Pois.Select(poi => poi.At), Is.Not.EqualTo(a.Pois.Select(poi => poi.At)));
    }

    [Test]
    public void MountainRocksStayOnTheMountainAndOffPathsAndPlaces([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        if (recipe.mountainRocks.Count == 0)
        {
            Assert.That(plan.Rocks, Is.Empty);
            return;
        }

        Assert.That(plan.Rocks.Count, Is.GreaterThanOrEqualTo(recipe.mountainRockCount.x / 2), "Too few rocks found room on the mountain.");
        foreach (HavenWorldPlan.Rock rock in plan.Rocks)
        {
            Assert.That(plan.MountainWeight(rock.At), Is.GreaterThanOrEqualTo(0.3f), $"Rock at {rock.At} is off the mountain.");
            Assert.That(HavenWorldPlan.Near(plan.Paths, rock.At, 5f), Is.False, $"Rock at {rock.At} blocks a path.");
            Assert.That(plan.Pois.Any(poi => Vector2.Distance(poi.At, rock.At) < poi.Rule.clearing), Is.False, $"Rock at {rock.At} is in a place's clearing.");
            Assert.That(rock.Kind, Is.InRange(0, recipe.mountainRocks.Count - 1));
            Assert.That(rock.Scale, Is.InRange(recipe.mountainRockScale.x, recipe.mountainRockScale.y));
            Assert.That(plan.Rocks.Count(other => other != rock && (other.At - rock.At).sqrMagnitude < 81f), Is.Zero, $"Rocks crowd at {rock.At}.");
        }
    }

    [Test]
    public void EveryGladeIsReachedByAPath([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.Glades.Count, Is.GreaterThanOrEqualTo(recipe.glades.x / 2), "Too few glades found room in the woods.");
        Assert.That(plan.PathKinds.Count, Is.EqualTo(plan.Paths.Count), "Every path needs its kind.");
        foreach (HavenWorldPlan.Glade glade in plan.Glades)
        {
            Assert.That(HavenWorldPlan.Near(plan.Paths, glade.Centre, glade.Radius), Is.True, $"The glade at {glade.Centre} has no way in.");
        }
    }

    [Test]
    public void TrailsKeepOutOfRiversAndRavines([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        for (int p = 0; p < plan.Paths.Count; p++)
        {
            if (plan.PathKinds[p] != HavenWorldPlan.PathKind.Trail && plan.PathKinds[p] != HavenWorldPlan.PathKind.Loop)
            {
                continue;
            }

            List<Vector2> line = plan.Paths[p];
            // The ends join other paths or lie in glades; between them, every 2 m must be dry and off the ravines.
            for (int i = 1; i + 2 < line.Count; i++)
            {
                for (float t = 0f; t < 1f; t += 2f / Mathf.Max(2f, Vector2.Distance(line[i], line[i + 1])))
                {
                    Vector2 point = Vector2.Lerp(line[i], line[i + 1], t);
                    Assert.That(HavenWorldPlan.Near(plan.Rivers, point, 3f), Is.False, $"{plan.PathKinds[p]} {p} steps into a river at {point}.");
                    Assert.That(plan.Ravines.Any(ravine => HavenWorldPlan.NearLine(ravine.Line, point, ravine.FloorWidth)), Is.False,
                                $"{plan.PathKinds[p]} {p} runs into a ravine at {point}.");
                }
            }
        }
    }

    [Test]
    public void RiversRunOnlyDownhillAndEveryCrossingIsAFord([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        for (int r = 0; r < plan.Rivers.Count; r++)
        {
            for (int k = 1; k < plan.Rivers[r].Count; k++)
            {
                Assert.That(plan.RiverLevel(r, k), Is.LessThanOrEqualTo(plan.RiverLevel(r, k - 1) + 0.0001f), $"River {r} runs uphill at knot {k}.");
            }

            // Wherever a path crosses, the river has a ford knot within a few metres.
            foreach (List<Vector2> path in plan.Paths)
            {
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    for (int k = 0; k + 1 < plan.Rivers[r].Count; k++)
                    {
                        if (Crosses(path[i], path[i + 1], plan.Rivers[r][k], plan.Rivers[r][k + 1], out Vector2 at))
                        {
                            int river = r;
                            Assert.That(plan.Fords.Any(ford => ford.river == river && Vector2.Distance(plan.Rivers[river][ford.knot], at) < 3f), Is.True,
                                        $"A path crosses river {r} at {at} without a ford.");
                        }
                    }
                }
            }
        }
    }

    private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
    {
        Vector2 r = b - a, s = d - c;
        float cross = r.x * s.y - r.y * s.x;
        at = default;
        if (Mathf.Abs(cross) < 1e-6f)
        {
            return false;
        }

        Vector2 ca = c - a;
        float t = (ca.x * s.y - ca.y * s.x) / cross, u = (ca.x * r.y - ca.y * r.x) / cross;
        at = a + r * t;
        return t > 0.001f && t < 0.999f && u > 0.001f && u < 0.999f;
    }

    [Test]
    public void FirstLandingIsOnTheLakeShoreFacingTheWater()
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        Assert.That(plan.Landings, Is.Not.Empty);
        (Vector2 at, float bearing) = plan.Landings[0];
        Assert.That(Mathf.Abs(plan.Lake.Edge(at)), Is.LessThan(4f), "The landing is not at the lake's edge.");
        Vector2 out_ = at + HavenWorldPlan.Dir(bearing) * 8f;
        Assert.That(plan.Lake.Edge(out_), Is.LessThan(plan.Lake.Edge(at)), "The landing does not run out over the water.");
    }
}
