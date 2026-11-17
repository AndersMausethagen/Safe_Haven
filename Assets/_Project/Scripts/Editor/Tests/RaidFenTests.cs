using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The fen's ground (RaidLayoutRules › Fen, the Moss Marsh), on plans and height fields only: every clearing and trail stands above the
/// one water level, a good share of the wild ground lies under it, hummocks and strings rise back out of it, and the same seed gives the
/// same fen. Run on a copy of the Woods' rules with the fen turned on; the Woods' own maps are pinned by RaidLayoutGoldenTests. Also
/// that what appears during a raid keeps out of the water (RaidSpots.IsDryEnough, on a stand-in water sheet).
/// </summary>
public sealed class RaidFenTests
{
    private const float Size = 200f;
    private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };
    private RaidLayoutRules rules;

    [SetUp]
    public void SetUp()
    {
        var woods = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset");
        rules = Object.Instantiate(woods);
        var so = new SerializedObject(rules);
        so.FindProperty("fen.enabled").boolValue = true;
        so.FindProperty("hills").floatValue = 0.2f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(rules);

    private IEnumerable<(int seed, RaidLayoutPlan plan, RaidLayoutField field)> Fens()
    {
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            yield return (seed, plan, RaidLayoutField.Build(plan, rules));
        }
    }

    private static float Height(RaidLayoutField field, Vector2 point) => field.HeightAt(point.x / Size, point.y / Size);

    [Test]
    public void ClearingsAndTrails_StandAboveTheWater()
    {
        foreach ((int seed, RaidLayoutPlan plan, RaidLayoutField field) in Fens())
        {
            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                if (plan.InRiver(room.Position, 0f))
                {
                    continue;
                }

                Assert.Greater(Height(field, room.Position), field.FenWater + 0.15f, $"seed {seed}: a {room.Role} clearing is in the water");
            }

            foreach (List<Vector2> trail in plan.Trails)
            {
                for (int i = 0; i + 1 < trail.Count; i++)
                {
                    Vector2 middle = (trail[i] + trail[i + 1]) * 0.5f;
                    if (plan.InRiver(middle, 4f) || middle.x < 2f || middle.y < 2f || middle.x > Size - 2f || middle.y > Size - 2f)
                    {
                        continue;
                    }

                    Assert.Greater(Height(field, middle), field.FenWater + 0.05f, $"seed {seed}: a trail goes under the water at {middle}");
                }
            }
        }
    }

    [Test]
    public void TheWildGround_IsMostlyWater_WithHummocksAndStringsAboveIt()
    {
        foreach ((int seed, RaidLayoutPlan plan, RaidLayoutField field) in Fens())
        {
            int under = 0, above = 0, all = 0;
            for (float y = 10f; y < Size - 10f; y += 2f)
            {
                for (float x = 10f; x < Size - 10f; x += 2f)
                {
                    var point = new Vector2(x, y);
                    float height = Height(field, point);
                    all++;
                    if (height < field.FenWater)
                    {
                        under++;
                    }
                    else if (field.Plan.Rooms.TrueForAll(room => (room.Position - point).magnitude > room.Radius + 10f))
                    {
                        above++;
                    }
                }
            }

            float share = under / (float)all;
            Assert.Greater(share, 0.12f, $"seed {seed}: only {share:P0} of the fen is water (the Woods' rules: many trails, little wild ground)");
            Assert.Less(share, 0.85f, $"seed {seed}: {share:P0} of the fen is water: nowhere to stand");
            Assert.Greater(above, all / 20, $"seed {seed}: no hummocks or strings above the water away from the clearings");
        }
    }

    [Test]
    public void WhatAppearsDuringARaid_KeepsOutOfTheWater()
    {
        // The fen's sheet has no collider and the NavMesh runs on the sunken ground under it (review 2026-11-17, R5): event sites, roamers
        // and event creatures ask RaidSpots.IsDryEnough. A stand-in sheet far from anything in the open scene, its top at 1.1 m.
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            water.transform.position = new Vector3(10000f, 1f, 10000f);
            water.transform.localScale = new Vector3(20f, 0.2f, 20f);
            WaterSurface surface = water.AddComponent<WaterSurface>();
            Assert.IsTrue(RaidSpots.IsDryEnough(new Vector3(10000f, 1f, 10000f)), "a hand's depth: a wet edge, still ground");
            Assert.IsFalse(RaidSpots.IsDryEnough(new Vector3(10000f, 0.5f, 10000f)), "0.6 m under");
            Assert.IsFalse(RaidSpots.IsDryEnough(new Vector3(10003f, -2.5f, 9996f)), "deeper than the surface's own Depth");
            Assert.IsTrue(RaidSpots.IsDryEnough(new Vector3(10000f, 1.6f, 10000f)), "above it, on a bridge's deck");
            Assert.IsTrue(RaidSpots.IsDryEnough(new Vector3(10030f, 0f, 10000f)), "beside it");
            surface.SetFrozen(true);
            Assert.IsTrue(RaidSpots.IsDryEnough(new Vector3(10000f, 0.5f, 10000f)), "ice is ground");
        }
        finally
        {
            Object.DestroyImmediate(water);
        }
    }

    [Test]
    public void TheSameSeed_MakesTheSameFen()
    {
        RaidLayoutField a = RaidLayoutField.Build(RaidLayoutPlanner.Plan(rules, Size, 5), rules);
        RaidLayoutField b = RaidLayoutField.Build(RaidLayoutPlanner.Plan(rules, Size, 5), rules);
        for (float t = 0.05f; t < 1f; t += 0.1f)
        {
            Assert.AreEqual(a.HeightAt(t, 1f - t), b.HeightAt(t, 1f - t), 1e-4f);
        }

        Assert.AreEqual(a.FenWater, b.FenWater, 1e-5f);
    }

    [Test]
    public void TheMossMarsh_IsAFen_WithItsClearingsAboveTheWater_AndNoValley()
    {
        var marsh = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MossMarsh Layout.asset");
        Assert.IsNotNull(marsh, "the Moss Marsh has no layout");
        Assert.IsTrue(marsh.Fen.enabled, "the Moss Marsh's layout is not a fen");
        foreach (RaidShapeRule shape in marsh.Shapes)
        {
            Assert.IsFalse(shape.shape == RaidLayoutShape.Valley && shape.weight > 0f, "the Moss Marsh can be laid out as a valley");
        }

        Assert.Less(marsh.Hills, marsh.Fen.aboveWater, "Hills higher than the fen's Above Water dip trails under it");
        foreach (int seed in Seeds)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(marsh, marsh.MapSize, seed);
            RaidLayoutField field = RaidLayoutField.Build(plan, marsh);
            int under = 0, all = 0;
            for (float y = 10f; y < marsh.MapSize - 10f; y += 4f)
            {
                for (float x = 10f; x < marsh.MapSize - 10f; x += 4f)
                {
                    all++;
                    if (field.HeightAt(x / marsh.MapSize, y / marsh.MapSize) < field.FenWater)
                    {
                        under++;
                    }
                }
            }

            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                Assert.Greater(field.HeightAt(room.Position.x / marsh.MapSize, room.Position.y / marsh.MapSize), field.FenWater + 0.15f,
                    $"seed {seed} ({plan.Shape.shape}): a {room.Role} clearing is in the water");
            }

            Assert.Greater(under / (float)all, 0.15f, $"seed {seed} ({plan.Shape.shape}): only {under / (float)all:P0} of the marsh is water");
        }
    }

    [Test]
    public void TreesAndRocks_KeepOutOfTheWater()
    {
        (int seed, RaidLayoutPlan plan, RaidLayoutField field) = (1, RaidLayoutPlanner.Plan(rules, Size, 1), null);
        field = RaidLayoutField.Build(plan, rules);
        for (float y = 10f; y < Size - 10f; y += 7f)
        {
            for (float x = 10f; x < Size - 10f; x += 7f)
            {
                var point = new Vector2(x, y);
                if (Height(field, point) < field.FenWater - 0.05f)
                {
                    Assert.IsTrue(field.KeepsClear(point), $"seed {seed}: something could stand in the water at {point}");
                }
            }
        }
    }
}
