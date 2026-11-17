using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lily pads on still water (RaidLilyPadRule, RaidLilyPads, Docs/WorldLookPlan_2026-11-17.md B9): a rule without pads places nothing
/// (so every map without one is as it was); on a fen every pad floats where the bed lies in the rule's depth band, within its reach of a
/// shore and clear of the trails, without two overlapping; the same seed gives the same pads; flowers and buds follow their chances; and
/// the pads are built into a mesh per stretch of map with no collider and no shadow. Run on fens from a copy of the Woods' rules with the
/// fen turned on, as RaidFenTests does.
/// </summary>
public sealed class RaidLilyPadTests
{
    private const float Size = 200f;
    private const int BedResolution = 257;
    // Radii at scale 1 of the four pad looks (Lily Pad A-D).
    private static readonly float[] Radii = { 0.15f, 0.22f, 0.27f, 0.25f };
    private RaidLayoutRules rules;
    private readonly List<Object> made = new List<Object>();

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
    public void TearDown()
    {
        Object.DestroyImmediate(rules);
        foreach (Object thing in made)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        made.Clear();
    }

    private static RaidLilyPadRule Rule()
    {
        var rule = new RaidLilyPadRule();
        foreach (float _ in Radii)
        {
            rule.pads.Add(new RaidLilyPadRule.Pad());
        }

        return rule;
    }

    private GameObject Stand(string name)
    {
        var stand = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        made.Add(stand);
        return stand;
    }

    private (RaidLayoutField field, float[,] bed) Fen(int seed)
    {
        RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        return (field, field.Metres(BedResolution, out _));
    }

    private static List<RaidLilyPads.Piece> Place(RaidLilyPadRule rule, int seed, RaidLayoutField field, float[,] bed)
    {
        float level = field.FenWater;
        return RaidLilyPads.Place(rule, seed, Size, bed, _ => level,
            point => field.WalkedDistance(point.x / Size, point.y / Size) < rule.trailClearance, Radii);
    }

    [Test]
    public void ARuleWithoutPads_PlacesNothing_AndTheOtherMapsHaveNone()
    {
        Assert.IsFalse(new RaidLilyPadRule().Enabled, "a new rule has no pads");
        var noPrefab = new RaidLilyPadRule();
        noPrefab.pads.Add(new RaidLilyPadRule.Pad());
        Assert.IsFalse(noPrefab.Enabled, "a pad without a prefab is no pad");

        (RaidLayoutField field, float[,] bed) = Fen(1);
        Assert.IsEmpty(Place(new RaidLilyPadRule(), 1, field, bed), "an empty list places nothing");
        Assert.IsEmpty(RaidLilyPads.Place(Rule(), 1, Size, bed, _ => field.FenWater, null, new float[0]), "no pad with a size");

        foreach (string map in new[] { "MistyWoods Map", "TrollValley Map" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>($"Assets/_Project/Data/Raids/Maps/{map}.asset");
            Assert.IsNotNull(definition, map);
            Assert.IsFalse(definition.LilyPads.Enabled, $"{map} has no water lilies, and keeps its look");
        }
    }

    [Test]
    public void Pads_FloatInTheirDepthBand_NearAShore_ClearOfTrails_AndApart()
    {
        RaidLilyPadRule rule = Rule();
        int all = 0;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            (RaidLayoutField field, float[,] bed) = Fen(seed);
            float level = field.FenWater;
            List<RaidLilyPads.Piece> pieces = Place(rule, seed, field, bed);
            var pads = pieces.FindAll(piece => piece.Kind >= 0);
            all += pads.Count;
            foreach (RaidLilyPads.Piece pad in pads)
            {
                float depth = level - RaidLilyPads.Bed(bed, Size, pad.Position);
                Assert.That(depth, Is.InRange(rule.depth.x - 0.001f, rule.depth.y + 0.001f), $"seed {seed}: a pad at {pad.Position} over {depth:0.00} m");
                Assert.That(pad.Height, Is.InRange(level + rule.aboveWater, level + rule.aboveWater + 0.0065f), "floats just above the water");
                Assert.GreaterOrEqual(field.WalkedDistance(pad.Position.x / Size, pad.Position.y / Size), rule.trailClearance - 0.01f,
                    $"seed {seed}: a pad on a trail at {pad.Position}");
                Assert.IsTrue(ShoreWithin(bed, level, pad.Position, rule.shoreReach + 0.5f), $"seed {seed}: no shore near {pad.Position}");
            }

            for (int i = 0; i < pads.Count; i++)
            {
                for (int j = i + 1; j < pads.Count; j++)
                {
                    float apart = (Radii[pads[i].Kind] * pads[i].Scale + Radii[pads[j].Kind] * pads[j].Scale) * 0.92f - 0.001f;
                    Assert.GreaterOrEqual(Vector2.Distance(pads[i].Position, pads[j].Position), apart, $"seed {seed}: two pads overlap");
                }
            }
        }

        Assert.Greater(all, 50, "the fens' pools carry pads");
    }

    // Brute force: any spot within reach whose bed stands at or above the water.
    private static bool ShoreWithin(float[,] bed, float level, Vector2 at, float reach)
    {
        for (float y = -reach; y <= reach; y += 0.25f)
        {
            for (float x = -reach; x <= reach; x += 0.25f)
            {
                var spot = at + new Vector2(x, y);
                if (x * x + y * y <= reach * reach && spot.x >= 0f && spot.y >= 0f && spot.x <= Size && spot.y <= Size
                    && RaidLilyPads.Bed(bed, Size, spot) >= level)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Test]
    public void TheSameSeed_GivesTheSamePads_AnotherSeedOthers()
    {
        RaidLilyPadRule rule = Rule();
        (RaidLayoutField field, float[,] bed) = Fen(4);
        List<RaidLilyPads.Piece> first = Place(rule, 77, field, bed);
        List<RaidLilyPads.Piece> again = Place(rule, 77, field, bed);
        List<RaidLilyPads.Piece> other = Place(rule, 78, field, bed);
        Assert.Greater(first.Count, 0);
        Assert.AreEqual(first.Count, again.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i].Position, again[i].Position);
            Assert.AreEqual(first[i].Yaw, again[i].Yaw);
            Assert.AreEqual(first[i].Scale, again[i].Scale);
            Assert.AreEqual(first[i].Kind, again[i].Kind);
        }

        bool differs = first.Count != other.Count;
        for (int i = 0; !differs && i < first.Count; i++)
        {
            differs = first[i].Position != other[i].Position;
        }

        Assert.IsTrue(differs, "another seed lays other pads");
    }

    [Test]
    public void FlowersAndBuds_FollowTheirChances_AndAFlowerSitsOnItsPad()
    {
        RaidLilyPadRule rule = Rule();
        rule.flower = Stand("Flower");
        rule.bud = Stand("Bud");
        (RaidLayoutField field, float[,] bed) = Fen(5);

        rule.flowerChance = rule.budChance = 0f;
        Assert.IsTrue(Place(rule, 5, field, bed).TrueForAll(piece => piece.Kind >= 0), "no chance, no flowers or buds");

        rule.flowerChance = 1f;
        List<RaidLilyPads.Piece> pieces = Place(rule, 5, field, bed);
        int pads = 0, flowers = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Kind >= 0)
            {
                pads++;
                Assert.IsTrue(i + 1 < pieces.Count && pieces[i + 1].Kind == RaidLilyPads.Flower, "every pad carries a flower");
                Assert.AreEqual(pieces[i].Position, pieces[i + 1].Position, "on the pad");
                Assert.Greater(pieces[i + 1].Height, pieces[i].Height, "on top of it");
            }

            flowers += pieces[i].Kind == RaidLilyPads.Flower ? 1 : 0;
        }

        Assert.AreEqual(pads, flowers);

        rule.flowerChance = 1f / 12f;
        rule.budChance = 1f / 12f;
        pieces = Place(rule, 5, field, bed);
        pads = pieces.FindAll(piece => piece.Kind >= 0).Count;
        flowers = pieces.FindAll(piece => piece.Kind == RaidLilyPads.Flower).Count;
        int buds = pieces.FindAll(piece => piece.Kind == RaidLilyPads.Bud).Count;
        Assert.That(flowers, Is.InRange(pads / 40, pads / 5 + 1), $"about one flower in twelve pads ({flowers} of {pads})");
        Assert.LessOrEqual(buds, pads / 5 + 1, "buds are rare");
    }

    [Test]
    public void Build_MakesAMeshPerStretchOfMap_WithNoColliderAndNoShadow()
    {
        var quad = new Mesh { name = "Test Pad" };
        quad.SetVertices(new[] { new Vector3(-0.2f, 0f, -0.2f), new Vector3(-0.2f, 0f, 0.2f), new Vector3(0.2f, 0f, 0.2f), new Vector3(0.2f, 0f, -0.2f) });
        quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        quad.RecalculateNormals();
        made.Add(quad);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        made.Add(material);
        var pieces = new List<RaidLilyPads.Piece>
        {
            new RaidLilyPads.Piece { Position = new Vector2(10f, 10f), Height = 3f, Scale = 1f, Kind = 0 },
            new RaidLilyPads.Piece { Position = new Vector2(12f, 11f), Height = 3f, Scale = 1.2f, Yaw = 40f, Kind = 0 },
            new RaidLilyPads.Piece { Position = new Vector2(130f, 70f), Height = 3f, Scale = 1f, Kind = 0 },
            new RaidLilyPads.Piece { Position = new Vector2(131f, 70f), Height = 3f, Scale = 1f, Kind = RaidLilyPads.Flower },
        };
        GameObject holder = Stand("Lily Pads");
        var meshes = new List<Mesh>();
        int vertices = RaidLilyPads.Build(holder.transform, pieces, new[] { quad }, null, null, material, meshes);
        made.AddRange(meshes);

        Assert.AreEqual(2, holder.transform.childCount, "two stretches of map hold pads");
        Assert.AreEqual(2, meshes.Count);
        Assert.AreEqual(12, vertices, "three pads of four vertices; the flower without a mesh is left out");
        foreach (Transform stretch in holder.transform)
        {
            var renderer = stretch.GetComponent<MeshRenderer>();
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, "casts no shadow");
            Assert.AreSame(material, renderer.sharedMaterial);
            Assert.IsNull(stretch.GetComponent<Collider>(), "nothing to bump into");
            Bounds bounds = stretch.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(3f, bounds.center.y, 0.001f, "at the water's height");
        }
    }
}
