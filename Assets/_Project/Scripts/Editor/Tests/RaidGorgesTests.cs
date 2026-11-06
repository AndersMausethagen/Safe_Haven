using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Gorges (cliffs, ravines, spires) and the relief every Misty Woods map gets, on plans and height fields only (no terrain, so the
/// open scene is untouched): trails stay walkable over ramps, necks and hills, nothing stands in a ravine, and the other moods have none.
/// </summary>
public sealed class RaidGorgesTests
{
    private const string RulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string Gorges = "The Gorges";
    private const float Size = 200f;
    private const float SteepestTrail = 36f;
    private const float BiggestStep = 0.4f;

    private static RaidLayoutRules Rules => AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(RulesPath);

    private static IEnumerable<int> SeedsOf(RaidLayoutRules rules, bool gorges, int count)
    {
        int from = 500;
        for (int i = 0; i < count; i++)
        {
            int seed = RaidLayoutPlanner.FindSeed(rules, (shape, mood) => (mood.name == Gorges) == gorges, from).Value;
            from = seed + 1;
            yield return seed;
        }
    }

    [Test]
    public void TheGorges_IsAMood_ThatComesLarge()
    {
        RaidMapMood mood = null;
        foreach (RaidMapMood one in Rules.Moods)
        {
            if (one.name == Gorges)
            {
                mood = one;
            }
        }

        Assert.IsNotNull(mood, "Run AgentScripts/BuildRaidGorges.cs");
        Assert.IsTrue(mood.largeByDefault);
        Assert.Greater(mood.terraces, 1);
        Assert.Greater(mood.ravines.y, 0);
        Assert.IsNotNull(Rules.RiverMaterial);
        Assert.IsNotNull(Rules.RavineCrags);
    }

    [Test]
    public void FindSeed_FindsARunOfTheWantedMood()
    {
        int? seed = RaidLayoutPlanner.FindSeed(Rules, (shape, mood) => mood.name == Gorges && shape.shape == RaidLayoutShape.Ring, 7);
        Assert.IsTrue(seed.HasValue);
        (RaidShapeRule chosenShape, RaidMapMood chosenMood) = RaidLayoutPlanner.Choose(Rules, seed.Value);
        Assert.AreEqual(Gorges, chosenMood.name);
        Assert.AreEqual(RaidLayoutShape.Ring, chosenShape.shape);
        Assert.IsNull(RaidLayoutPlanner.FindSeed(Rules, (shape, mood) => mood.name == "No Such Mood", 7, 50));
    }

    [Test]
    public void GorgesMaps_HaveRavines_AndWalkableTrails_AndNothingInTheRavines()
    {
        int ravines = 0;
        foreach (int seed in SeedsOf(Rules, true, 4))
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, seed);
            RaidLayoutField field = RaidLayoutField.Build(plan, Rules);
            ravines += plan.Ravines.Count;
            AssertTrailsWalkable(plan, field, seed);
            foreach (RaidSite site in plan.Sites)
            {
                // The finds at the bottom of a ravine with a way down are meant to be there.
                bool onDescentFloor = plan.OverRavine(site.Position * Size, out RaidLayoutRavine ravine, out _) && ravine.Descent;
                Assert.IsTrue(onDescentFloor || !field.KeepsClear(site.Position * Size), $"seed {seed}: {site} stands in a ravine or on a spire");
            }

            foreach (RaidLayoutBoulder boulder in plan.Boulders)
            {
                Assert.IsFalse(field.KeepsClear(boulder.Position), $"seed {seed}: a boulder in a ravine");
            }

            foreach (RaidLayoutRavine ravine in plan.Ravines)
            {
                for (int i = 1; i < ravine.Floor.Length; i++)
                {
                    Assert.LessOrEqual(ravine.Floor[i], ravine.Floor[i - 1] + 0.001f, $"seed {seed}: a ravine's floor rises, so its river would run uphill");
                }
            }
        }

        Assert.Greater(ravines, 0, "Four Gorges maps without one ravine");
    }

    [Test]
    public void OtherMoods_HaveNoCliffs_ButStillHillsAndBoulders()
    {
        foreach (int seed in SeedsOf(Rules, false, 4))
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, seed);
            Assert.AreEqual(0, plan.Ravines.Count, $"seed {seed}");
            Assert.AreEqual(0, plan.Spires.Count, $"seed {seed}");
            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                Assert.AreEqual(0, room.Level, $"seed {seed}: a level without terraces");
            }

            AssertTrailsWalkable(plan, RaidLayoutField.Build(plan, Rules), seed);
        }
    }

    [Test]
    public void EveryMap_HasTwoWaysToTheExitAhead()
    {
        foreach (bool gorges in new[] { false, true })
        {
            foreach (int seed in SeedsOf(Rules, gorges, 4))
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, seed);
                Assert.GreaterOrEqual(plan.MainExit, 0, $"seed {seed}");
                Assert.Greater(plan.ShortRoute.Count, 0, $"seed {seed}");
                Assert.Greater(plan.LongRoute.Count, 0, $"seed {seed}: only one way to the exit ahead");
            }
        }
    }

    [Test]
    public void LargeMaps_HaveRegionsOfTheirOwnMood_AndRunsAreRepeatable()
    {
        RaidLayoutRules large = Rules.ScaledBy(2f);
        try
        {
            int seed = RaidLayoutPlanner.FindSeed(large, (shape, mood) => mood.name != Gorges, 77).Value;
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(large, Size * 2f, seed);
            Assert.GreaterOrEqual(plan.Regions.Count, 2);
            Assert.AreEqual(plan.Mood.name, plan.MoodAt(plan.Start.Position).name, "The start lies in the run's own mood");
            RaidLayoutPlan again = RaidLayoutPlanner.Plan(large, Size * 2f, seed);
            Assert.AreEqual(plan.Rooms.Count, again.Rooms.Count);
            Assert.AreEqual(plan.Rivers.Count, again.Rivers.Count);
            Assert.AreEqual(plan.Landmarks.Count, again.Landmarks.Count);
            Assert.AreEqual(plan.Regions[1].Mood.name, again.Regions[1].Mood.name);
        }
        finally
        {
            Object.DestroyImmediate(large);
        }
    }

    [Test]
    public void Rivers_KeepOffSites_AndRunDownhill()
    {
        int rivers = 0;
        foreach (int seed in SeedsOf(Rules, false, 10))
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, seed);
            RaidLayoutField.Build(plan, Rules);
            foreach (RaidLayoutRiver river in plan.Rivers)
            {
                rivers++;
                for (int i = 1; i < river.Level.Length; i++)
                {
                    Assert.LessOrEqual(river.Level[i], river.Level[i - 1] + 0.001f, $"seed {seed}: a river runs uphill");
                }
            }

            foreach (RaidSite site in plan.Sites)
            {
                Assert.IsFalse(plan.InRiver(site.Position * Size), $"seed {seed}: {site} stands in a river");
            }
        }

        Assert.Greater(rivers, 0, "Ten maps without a river");
    }

    [Test]
    public void TheRaids_RunTerrainEvents()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var data = new SerializedObject(raid);
        SerializedProperty systems = data.FindProperty("systems");
        bool found = false;
        for (int i = 0; i < systems.arraySize; i++)
        {
            found |= systems.GetArrayElementAtIndex(i).managedReferenceValue is TerrainEventsSystem;
        }

        Assert.IsTrue(found, "Run AgentScripts/BuildRaidGorges.cs");
    }

    [Test]
    public void HillinessIsFixedByTheSeed_AndWithinItsRange()
    {
        float first = RaidLayoutPlanner.Hilliness(Rules, 42);
        Assert.AreEqual(first, RaidLayoutPlanner.Hilliness(Rules, 42));
        Assert.GreaterOrEqual(first, Rules.HillVariance.x);
        Assert.LessOrEqual(first, Rules.HillVariance.y);
    }

    [Test]
    public void HighGround_GivesMoreDamageFromAbove_UpToItsCap()
    {
        var feel = ScriptableObject.CreateInstance<CombatFeelSettings>();
        Assert.AreEqual(1f, feel.HighGround(0f));
        Assert.AreEqual(1f, feel.HighGround(-10f), "From below: no bonus, no penalty");
        Assert.Greater(feel.HighGround(5f), 1f);
        Assert.AreEqual(feel.HighGround(100f), feel.HighGround(1000f), "Capped");
        Object.DestroyImmediate(feel);
    }

    [Test]
    public void AFall_CountsOnlyOverTheCut_NotOnANeck()
    {
        foreach (int seed in SeedsOf(Rules, true, 6))
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, seed);
            RaidLayoutField field = RaidLayoutField.Build(plan, Rules);
            if (plan.Ravines.Count == 0)
            {
                continue;
            }

            RaidLayoutRavine ravine = plan.Ravines[0];
            Vector2 middle = ravine.Line[ravine.Line.Count / 2];
            float floor = field.HeightAt(middle.x / Size, middle.y / Size);
            Assert.IsTrue(RaidChasm.HasFallen(plan, middle, floor + 1f, floor), $"seed {seed}: down in the ravine is a fall");
            float rim = ravine.Rim[ravine.Line.Count / 2];
            Assert.IsFalse(RaidChasm.HasFallen(plan, middle, rim, rim), $"seed {seed}: at rim height (a neck) is no fall");
            return;
        }

        Assert.Inconclusive("No Gorges map with a ravine among the seeds tried");
    }

    private static void AssertTrailsWalkable(RaidLayoutPlan plan, RaidLayoutField field, int seed)
    {
        var lines = new List<List<Vector2>>(plan.Trails);
        lines.AddRange(plan.Spurs);
        foreach (List<Vector2> trail in lines)
        {
            for (int i = 0; i + 1 < trail.Count; i++)
            {
                float length = Vector2.Distance(trail[i], trail[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
                float last = field.HeightAt(trail[i].x / Size, trail[i].y / Size);
                for (int k = 1; k <= steps; k++)
                {
                    Vector2 at = Vector2.Lerp(trail[i], trail[i + 1], k / (float)steps);
                    float height = field.HeightAt(at.x / Size, at.y / Size);
                    float rise = Mathf.Abs(height - last);
                    Assert.LessOrEqual(rise, BiggestStep, $"seed {seed}: a {rise:F2} m step on a trail at {at}");
                    Assert.LessOrEqual(Mathf.Atan(rise / (length / steps)) * Mathf.Rad2Deg, SteepestTrail, $"seed {seed}: too steep at {at}");
                    last = height;
                }
            }
        }
    }
}
