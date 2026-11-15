using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The Haven world's plan from the project's recipe: the same seed always gives the same world (so a new game's seed can
/// make its own), what it strews over the mountain keeps to its rules, and the rules a world read from above lives by hold:
/// every path leads somewhere and no walk goes long with nothing on it, the ground can be walked to, cliffs break for paths
/// and water, water lies below its shores, places stand on level ground and game keeps to its own zones.</summary>
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
                    // The causeway into the bog crosses the bog's own stream: that is what it is for.
                    if (plan.BogWeight(point) > 0f)
                    {
                        continue;
                    }

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

    [Test]
    public void EveryPathLeadsSomewhereAndNoWalkIsDull([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.DeadEnds, Is.Empty, "A path ends at nothing: not at a place, a glade, the water or another path.");
        Assert.That(plan.LongestDullWalk, Is.LessThanOrEqualTo(recipe.longestDullWalk),
                    $"A walk of {plan.LongestDullWalk:0} m passes nothing (the recipe allows {recipe.longestDullWalk:0} m).");
        Assert.That(plan.EmptyShare, Is.LessThan(0.03f), "Too much ground has nothing within reach of it.");
        // No road ends at nothing: each has a place within a few steps of its end.
        foreach (Vector2 end in plan.RoadEnds)
        {
            Assert.That(plan.Pois.Any(poi => Vector2.Distance(poi.At, end) < poi.Rule.clearing + 10f), Is.True, $"The road ending at {end} leads to no place.");
        }
    }

    [Test]
    public void TheGroundCanBeWalkedAndWhatStandsOnItCanBeReached([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.ReachableShare, Is.GreaterThan(0.985f), "Cliffs cut off too much ground: more scrambles or ramps are needed.");
        foreach (HavenWorldPlan.Node node in plan.Nodes)
        {
            Assert.That(plan.Reachable(node.At), Is.True, $"The resource point at {node.At} cannot be walked to.");
        }

        foreach (HavenWorldPlan.Zone zone in plan.Zones)
        {
            Assert.That(plan.Reachable(zone.At), Is.True, $"The spawn zone at {zone.At} cannot be walked to.");
        }
    }

    [Test]
    public void CliffsBreakForPathsAndWaterAndPlacesStandLevel([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.Cliffs, Is.Not.Empty, "The land has no cliffs.");
        Assert.That(plan.Scrambles, Is.Not.Empty, "No cliff has a way up but the paths.");
        foreach (HavenWorldPlan.Cliff cliff in plan.Cliffs)
        {
            Assert.That(cliff.Line.Count, Is.GreaterThanOrEqualTo(3));
            foreach (Vector2 knot in cliff.Line)
            {
                Assert.That(HavenWorldPlan.Near(plan.Paths, knot, 1.5f), Is.False, $"A cliff stands across a path at {knot}.");
                Assert.That(plan.InWater(knot, 0f), Is.False, $"A cliff stands in the water at {knot}.");
            }
        }

        foreach (HavenWorldPlan.Poi poi in plan.Pois)
        {
            if (poi.Rule.onIsland || poi.Rule.inBog)
            {
                continue;
            }

            int tier = plan.Tier(poi.At);
            for (int i = 0; i < 8; i++)
            {
                Vector2 edge = poi.At + HavenWorldPlan.Dir(i * 45f) * poi.Rule.clearing * 0.9f;
                Assert.That(plan.Tier(edge), Is.EqualTo(tier), $"A cliff runs through {poi.Rule.name} at {poi.At}.");
            }
        }
    }

    [Test]
    public void WaterLiesBelowItsShores([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.Meres.Count, Is.EqualTo(recipe.meres.Count), "A mere found no room.");
        for (int m = 0; m < plan.Meres.Count; m++)
        {
            RaidLayoutPond mere = plan.Meres[m];
            Assert.That(plan.Islands.Any(island => island.Water == mere), Is.True, $"{plan.MereNames[m]} has no island.");
            for (int i = 0; i < 16; i++)
            {
                Vector2 shore = mere.Rim(i * Mathf.PI / 8f, 6f);
                // A stream's valley may leave it lower: only its own bank is asked.
                if (!HavenWorldPlan.Near(plan.Rivers, shore, 12f))
                {
                    Assert.That(plan.Height(shore), Is.GreaterThan(mere.Level), $"{plan.MereNames[m]} stands above its shore at {shore}.");
                }
            }
        }

        if (plan.HasBog)
        {
            // A basin: nowhere round it is the ground lower than its water (it hung over the Haven's side once).
            for (int i = 0; i < 48; i++)
            {
                Vector2 outward = HavenWorldPlan.Dir(i * 7.5f);
                for (float d = recipe.bogRadius * 0.5f; d < recipe.bogRadius * 1.5f; d += 3f)
                {
                    Vector2 point = plan.BogCentre + outward * d;
                    if (plan.BogWeight(point) > 0f)
                    {
                        continue;
                    }

                    // Where its road and its stream come in, the land is level with it: that is the way in and the way out.
                    if (!HavenWorldPlan.Near(plan.Rivers, point, 14f) && !HavenWorldPlan.Near(plan.Paths, point, 45f))
                    {
                        Assert.That(plan.Height(point), Is.GreaterThan(plan.BogLevel), $"The bog's water stands above the ground at {point}.");
                    }

                    break;
                }
            }
        }

        // Every river that runs into still water has its fan there.
        for (int r = 0; r < plan.Rivers.Count; r++)
        {
            Vector2 end = plan.Rivers[r][plan.Rivers[r].Count - 1];
            if (plan.Lake.Edge(end) < 0f)
            {
                Assert.That(plan.Mouths.Any(mouth => mouth.At == end && mouth.Water == plan.Lake), Is.True, $"River {r} runs into the lake without a mouth.");
                Assert.That(plan.MouthFan(end), Is.GreaterThan(0.5f));
            }
        }
    }

    [Test]
    public void GameIsPeacefulAndKeepsToItsOwnGround([Values(1, 7)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.Zones.Count(zone => zone.Band.peaceful), Is.GreaterThanOrEqualTo(10), "Too little game found room.");
        foreach (HavenWorldPlan.Zone zone in plan.Zones)
        {
            if (!zone.Band.peaceful)
            {
                Assert.That(zone.At.magnitude, Is.GreaterThanOrEqualTo(recipe.haven - 0.01f), $"What hunts you spawns inside the woods' edge at {zone.At}.");
                continue;
            }

            Assert.That(zone.At.magnitude, Is.GreaterThan(recipe.buildable), $"Game spawns on the ground the Haven is built on at {zone.At}.");
            Assert.That(zone.Glade == null || zone.Glade.Kind != HavenWorldPlan.GladeKind.Lair, Is.True, "Game was put in a lair.");
            Assert.That(plan.Zones.Any(other => !other.Band.peaceful && other.Glade != null && other.Glade == zone.Glade), Is.False, "Game shares its glade with what hunts.");
        }

        // What the recipe calls game is game: it takes no side, runs instead of fighting and has no attack.
        foreach (HavenCreatureBand band in recipe.creatures.Where(band => band.peaceful))
        {
            Assert.That(band.creatures, Is.Not.Empty, $"{band.name} has no creature: run AgentScripts/BuildGame.cs, then BuildHavenWorldRecipe.cs.");
            foreach (SpawnTable.Entry entry in band.creatures)
            {
                MonsterDefinition definition = new SerializedObject(entry.prefab.GetComponent<MonsterAI>()).FindProperty("definition").objectReferenceValue as MonsterDefinition;
                Assert.That(definition, Is.Not.Null, $"{entry.prefab.name} has no definition.");
                Assert.That(definition.Faction, Is.EqualTo(Faction.Neutral), $"{definition.name} takes a side.");
                Assert.That(definition.Timid, Is.True, $"{definition.name} is not timid.");
                Assert.That(definition.Attack == null && definition.Attacks.Count == 0, Is.True, $"{definition.name} has an attack.");
            }
        }
    }

    // The user, 2026-11-15: the moss round the portal goes back to its natural brown; the meadow grass stays.
    [Test]
    public void TheMossRoundThePortalIsBrownEarth()
    {
        const int Meadow = 0, Moss = 1, Soil = 2, Leaves = 3;
        float[] Painted(Vector2 at)
        {
            var weights = new[] { 0.4f, 0.6f, 0f, 0f };
            HavenWorldGenerator.TurnMossToEarth(weights, at, recipe.portalEarth, recipe.portalEarthPatch, new[] { Moss }, Soil, Leaves);
            return weights;
        }

        float[] near = Painted(new Vector2(6f, -3f));
        Assert.That(near[Moss], Is.Zero, "no moss by the portal");
        Assert.That(near[Soil] + near[Leaves], Is.EqualTo(0.6f).Within(1e-5f), "it is earth there");
        Assert.That(near[Meadow], Is.EqualTo(0.4f), "the meadow grass stays");
        Assert.That(Painted(new Vector2(0f, recipe.portalEarth.y + 1f)), Is.EqualTo(new[] { 0.4f, 0.6f, 0f, 0f }), "moss as ever further out");

        // And the Haven's saved terrain has it: no moss within the full radius.
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain.asset");
        Assert.That(data, Is.Not.Null, "The Haven's terrain data is missing.");
        int lush = System.Array.FindIndex(data.terrainLayers, layer => layer != null && layer.name == "Haven Lush Moss");
        int resolution = data.alphamapResolution;
        float pixel = data.size.x / resolution, reach = recipe.portalEarth.x - 1f;
        int first = Mathf.FloorToInt((data.size.x / 2f - reach) / pixel), across = Mathf.CeilToInt(2f * reach / pixel);
        float[,,] maps = data.GetAlphamaps(first, first, across, across);
        float most = 0f;
        for (int y = 0; y < across; y++)
        {
            for (int x = 0; x < across; x++)
            {
                var at = new Vector2((first + x + 0.5f) * pixel - data.size.x / 2f, (first + y + 0.5f) * pixel - data.size.z / 2f);
                if (at.magnitude <= reach) most = Mathf.Max(most, maps[y, x, lush]);
            }
        }

        Assert.That(most, Is.LessThan(0.02f), "The Haven's terrain still has moss by the portal.");
    }

    [Test]
    public void TheHavenSceneHoldsItsWater()
    {
        // The saved scene, read as text (the test runner works in a scene of its own): the lake, the meres, the bog and every pond
        // have their water. It was once found gone from the saved scene, and nothing said so.
        const string path = "Assets/_Project/Scenes/SafeHaven.unity";
        Assert.That(System.IO.File.Exists(path), Is.True, "The Haven's scene is missing.");
        string scene = System.IO.File.ReadAllText(path);
        // A name at the end of its line: "Pond 1" is not "Pond 10".
        int Named(string name) => scene.Contains("m_Name: " + name + "\n") || scene.Contains("m_Name: " + name + "\r") ? 1 : 0;
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        Assert.That(Named("Lake"), Is.GreaterThanOrEqualTo(1), "The Haven's scene has no lake water: run the generator's Finish step and save.");
        foreach (string mere in plan.MereNames)
        {
            Assert.That(Named(mere), Is.GreaterThanOrEqualTo(1), $"The Haven's scene has no water for {mere}: run the generator's Finish step and save.");
        }

        if (plan.HasBog)
        {
            Assert.That(Named("Bog"), Is.GreaterThanOrEqualTo(1), "The Haven's scene has no bog water: run the generator's Finish step and save.");
        }

        for (int i = 1; i <= plan.Ponds.Count; i++)
        {
            Assert.That(Named($"Pond {i}"), Is.GreaterThanOrEqualTo(1), $"Pond {i} has no water in the Haven's scene.");
        }
    }

    [Test]
    public void StillWaterKnowsItsTrueShoreline()
    {
        // A square pond turned 45 degrees: its mesh's box reaches past its corners, its shoreline does not.
        var go = new GameObject("Shoreline Test Water", typeof(WaterSurface)) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var water = go.GetComponent<WaterSurface>();
            go.transform.position = new Vector3(100f, 3f, 50f);
            Assert.That(water.HasShoreline, Is.False);
            water.SetShoreline(new[] { new Vector2(10f, 0f), new Vector2(0f, 10f), new Vector2(-10f, 0f), new Vector2(0f, -10f) });
            Assert.That(water.HasShoreline, Is.True);
            Assert.That(water.ShorelineDistance(100f, 50f), Is.LessThan(0f), "The middle is in the water.");
            Assert.That(water.ShorelineDistance(100f, 50f), Is.EqualTo(-7.07f).Within(0.05f));
            // Inside the box a mesh of it would have, outside the water.
            Assert.That(water.ShorelineDistance(108f, 58f), Is.GreaterThan(0f), "A corner of the box is dry land.");
            Assert.That(water.ShorelineDistance(120f, 50f), Is.EqualTo(10f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(go);
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

    [Test]
    public void TheHaven_HasAFishingPond_FedByAStreamThroughItsMeadows([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        Assert.That(plan.HavenPond, Is.Not.Null, "No fishing pond in the Haven.");
        Assert.That(plan.HavenPond.Position.magnitude - plan.HavenPond.Reach, Is.GreaterThan(recipe.yard), "The pond lies in the yard.");
        Assert.That(plan.HavenPond.Position.magnitude, Is.LessThan(recipe.haven), "The pond is outside the Haven's meadows.");
        // Always a stream in; a stream out where the lake or a lower river can be reached without crossing another river (the
        // shipped world's seed has both; some seeds' ponds lie still).
        Assert.That(plan.HavenStreams.Count, Is.InRange(1, 2), "The pond wants a stream in (and one out where it can).");
        foreach (int stream in plan.HavenStreams)
        {
            foreach (Vector2 point in plan.Rivers[stream])
            {
                Assert.That(point.magnitude, Is.GreaterThan(recipe.yard), "A Haven stream runs through the yard.");
            }

            float[] levels = Enumerable.Range(0, plan.Rivers[stream].Count).Select(knot => plan.RiverLevel(stream, knot)).ToArray();
            for (int k = 1; k < levels.Length; k++)
            {
                Assert.That(levels[k], Is.LessThanOrEqualTo(levels[k - 1] + 0.001f), "A Haven stream runs uphill.");
            }
        }

        // The stream in comes down to the pond's level; the one out leaves at it.
        int into = plan.HavenStreams[0];
        Assert.That(plan.HavenPond.Edge(plan.Rivers[into][plan.Rivers[into].Count - 1]), Is.LessThan(0f), "The first Haven stream doesn't end in the pond.");
        Assert.That(plan.RiverLevel(into, plan.Rivers[into].Count - 1), Is.EqualTo(plan.HavenPond.Level + 0.05f).Within(0.01f));
        if (plan.HavenStreams.Count == 2)
        {
            int outOf = plan.HavenStreams[1];
            Assert.That(plan.HavenPond.Edge(plan.Rivers[outOf][0]), Is.LessThan(0f), "The second Haven stream doesn't leave the pond.");
            Assert.That(plan.RiverLevel(outOf, 0), Is.LessThanOrEqualTo(plan.HavenPond.Level + 0.01f));
        }

        if (seed == recipe.seed)
        {
            Assert.That(plan.HavenStreams.Count, Is.EqualTo(2), "The shipped world's pond has a stream in and one out.");
        }
    }

    [Test]
    public void TheYard_StaysAsLevelAsEver_WhereSavedBuildingsStand([Values(1, 7)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        for (float r = 0f; r <= recipe.yard; r += 5f)
        {
            for (float bearing = 0f; bearing < 360f; bearing += 15f)
            {
                Vector2 point = HavenWorldPlan.Dir(bearing) * r;
                Assert.That(plan.Shelf(point), Is.EqualTo(0f), $"A shelf in the yard at {point}.");
                Assert.That(plan.KnollAt(point), Is.EqualTo(0f), $"A knoll in the yard at {point}.");
            }
        }
    }

    [Test]
    public void AKnoll_HasAFlatCrown_ToBuildOn([Values(1, 7, 23)] int seed)
    {
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe, seed);
        // As many as the recipe asks where they fit (some seeds' meadows are too crowded with paths and water for the second).
        Assert.That(plan.Knolls.Count, Is.GreaterThanOrEqualTo(seed == recipe.seed ? recipe.havenKnolls.x : 1));
        foreach (HavenWorldPlan.Knoll knoll in plan.Knolls)
        {
            float crown = plan.Ground(knoll.At);
            for (int i = 0; i < 8; i++)
            {
                Vector2 point = knoll.At + HavenWorldPlan.Dir(i * 45f) * 3.5f;
                Assert.That(Mathf.Abs(plan.Ground(point) - crown), Is.LessThan(0.5f), $"The knoll at {knoll.At} has an uneven crown.");
            }
        }
    }
}
