using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Misty Woods maps for a few fixed seeds, as fingerprints of their plans and heights, recorded before Troll Valley's own shape was
/// added (2026-11-17, Docs/TrollValleyPlan.md step 0). Work on another raid's generator must leave these alone; a deliberate change to
/// the Misty Woods generator updates them (the failure message prints the new value).
/// </summary>
public sealed class RaidLayoutGoldenTests
{
    private const string RulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const float Size = 200f;
    private const int HeightSamples = 33;

    private static RaidLayoutRules Rules => AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(RulesPath);

    // seed → fingerprint of a map of the normal size.
    private static readonly Dictionary<int, string> Normal = new Dictionary<int, string>
    {
        { 1, "ad3ec333877f84fe" },
        { 2, "1fd5904b03377ef1" },
        { 3, "82a41d31334b1d37" },
        { 4, "a92ec778e0b5adb6" },
        { 5, "5ad386e00788a450" },
    };

    private const int LargeSeed = 3;
    private const string Large = "326f26f228a8d883";

    [Test]
    public void MistyWoods_NormalMaps_AreUnchanged()
    {
        var wrong = new List<string>();
        foreach (KeyValuePair<int, string> golden in Normal)
        {
            string now = Fingerprint(Rules, Size, golden.Key);
            if (now != golden.Value)
            {
                wrong.Add($"seed {golden.Key}: {now} (was {golden.Value})");
            }
        }

        Assert.IsEmpty(wrong, string.Join("\n", wrong));
    }

    [Test]
    public void MistyWoods_LargeMap_IsUnchanged()
    {
        RaidLayoutRules large = Rules.ScaledBy(2f);
        try
        {
            Assert.AreEqual(Large, Fingerprint(large, Size * 2f, LargeSeed));
        }
        finally
        {
            Object.DestroyImmediate(large);
        }
    }

    /// <summary>A short hash of everything a plan places and the heights its field gives, rounded to a centimetre.</summary>
    public static string Fingerprint(RaidLayoutRules rules, float size, int seed)
    {
        RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, size, seed);
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        ulong hash = 14695981039346656037UL;

        void Add(float value)
        {
            long rounded = (long)System.Math.Round(value * 100.0);
            for (int i = 0; i < 8; i++)
            {
                hash ^= (byte)(rounded >> (i * 8));
                hash *= 1099511628211UL;
            }
        }

        void AddPoint(Vector2 point)
        {
            Add(point.x);
            Add(point.y);
        }

        Add(plan.Rooms.Count);
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            AddPoint(room.Position);
            Add(room.Radius);
            Add((int)room.Role);
            Add(room.Level);
        }

        foreach ((int a, int b) in plan.Links)
        {
            Add(a);
            Add(b);
        }

        foreach (List<Vector2> line in plan.Trails)
        {
            line.ForEach(AddPoint);
        }

        foreach (List<Vector2> line in plan.Spurs)
        {
            line.ForEach(AddPoint);
        }

        foreach (RaidSite site in plan.Sites)
        {
            Add((int)site.Kind);
            AddPoint(site.Position * size);
        }

        foreach (RaidLayoutPiece piece in plan.Pieces)
        {
            AddPoint(piece.Position);
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            AddPoint(pond.Position);
            Add(pond.Level);
        }

        foreach (RaidLayoutRiver river in plan.Rivers)
        {
            river.Line.ForEach(AddPoint);
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Line.ForEach(AddPoint);
        }

        foreach (RaidLayoutBoulder boulder in plan.Boulders)
        {
            AddPoint(boulder.Position);
        }

        for (int y = 0; y < HeightSamples; y++)
        {
            for (int x = 0; x < HeightSamples; x++)
            {
                Add(field.HeightAt(x / (HeightSamples - 1f), y / (HeightSamples - 1f)));
            }
        }

        return hash.ToString("x16");
    }
}
