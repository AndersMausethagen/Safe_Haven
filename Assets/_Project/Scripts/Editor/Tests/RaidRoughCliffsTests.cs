using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rough cliffs (Layout Rules › Cliff Roughness, the user 2026-11-18: "the edges of cliffs and along the river look unnaturally smooth"):
/// the terrain's heights (RaidLayoutField.Metres) carve steep ground and break up shores, but trails and clearings keep the field's own
/// heights exactly (nothing walkable moves), and the field itself (HeightAt, what placement and the golden tests read) is unchanged.
/// </summary>
public sealed class RaidRoughCliffsTests
{
    private const string ValleyRules = "Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset";
    private const int Samples = 257;

    [Test]
    public void SteepGroundTurnsRough_ButNothingWalkableMoves()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(ValleyRules);
        Assert.Greater(rules.CliffRoughness, 0f, "Troll Valley's cliffs are rough");
        foreach (int seed in new[] { 4, 11 })
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, 200f, seed);
            RaidLayoutField field = RaidLayoutField.Build(plan, rules);
            float[,] metres = field.Metres(Samples, out _);
            float moved = 0f;
            int walkable = 0;
            for (int row = 0; row < Samples; row += 2)
            {
                for (int column = 0; column < Samples; column += 2)
                {
                    float u = column / (Samples - 1f), v = row / (Samples - 1f);
                    float difference = Mathf.Abs(metres[row, column] - field.HeightAt(u, v));
                    if (field.WalkedDistance(u, v) < 1.5f)
                    {
                        walkable++;
                        Assert.Less(difference, 1e-4f, $"seed {seed}: walkable ground at ({u:0.000}, {v:0.000}) moved {difference:0.00} m");
                    }
                    else
                    {
                        moved = Mathf.Max(moved, difference);
                    }
                }
            }

            Assert.Greater(walkable, 100, $"seed {seed}: the check saw the trails");
            Assert.Greater(moved, 0.4f, $"seed {seed}: somewhere steep was carved");
            Assert.LessOrEqual(moved, rules.CliffRoughness * 1.3f + rules.CliffGullies + rules.Talus * 1.25f,
                $"seed {seed}: never more than the rules' metres (and a little for the shore and the rubble)");
        }
    }

    // Docs/LookAndFeelResearch_2026-11-18.md 4.1: gullies run down the fall line (they vary across a face, not along it).
    [Test]
    public void GulliesRunDownTheFallLine()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(ValleyRules);
        Assert.Greater(rules.CliffGullies, 0f, "the Valley's walls are gullied");
        Assert.Greater(rules.Talus, 0f, "and scree lies at their feet");
        RaidLayoutField field = RaidLayoutField.Build(RaidLayoutPlanner.Plan(rules, 200f, 4), rules);
        // A face rising to the east: the fall line runs east-west, so channels vary north-south.
        float along = 0f, across = 0f;
        for (int i = 0; i < 400; i++)
        {
            float x = 40f + (i % 20) * 0.37f, y = 40f + (i / 20) * 0.41f;
            along += Mathf.Abs(field.Gullies(x + 0.3f, y, 1f, 0f) - field.Gullies(x, y, 1f, 0f));
            across += Mathf.Abs(field.Gullies(x, y + 0.3f, 1f, 0f) - field.Gullies(x, y, 1f, 0f));
        }

        Assert.Greater(across, along * 2f, "channels change across the face far more than down it");
        Assert.AreEqual(0f, field.Gullies(10f, 10f, 0f, 0f), "level ground: none");
    }
}
