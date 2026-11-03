using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RaidSetPieceTests
{
    private const float Size = 500f;

    private static RaidLayoutRules Rules => AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset");

    [Test]
    public void EveryRumourSiteStandsInASetPiece()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, seed);
            foreach (RaidSite site in plan.SitesOf(RaidSiteKind.Rumour))
            {
                Vector2 spot = site.Position * Size;
                RaidLayoutPiece piece = plan.Pieces.Find(one => Vector2.Distance(one.Position, spot) < 0.01f);
                Assert.IsNotNull(piece, $"Seed {seed}: a rumour stands in the open");
                Assert.IsTrue(piece.Definition.HoldsRumours, $"Seed {seed}");
            }
        }
    }

    [Test]
    public void TheSameSeedGivesTheSamePlaces()
    {
        RaidLayoutPlan a = RaidLayoutPlanner.Plan(Rules, Size, 77);
        RaidLayoutPlan b = RaidLayoutPlanner.Plan(Rules, Size, 77);
        Assert.AreEqual(a.Pieces.Count, b.Pieces.Count);
        for (int i = 0; i < a.Pieces.Count; i++)
        {
            Assert.AreEqual(a.Pieces[i].Definition, b.Pieces[i].Definition);
            Assert.AreEqual(a.Pieces[i].Position, b.Pieces[i].Position);
        }
    }

    [Test]
    public void AMapHasVarietyAndOpenGroundForEach()
    {
        RaidLayoutPlan plan = RaidLayoutPlanner.Plan(Rules, Size, 5);
        var kinds = new HashSet<RaidSetPieceDefinition>();
        foreach (RaidLayoutPiece piece in plan.Pieces)
        {
            kinds.Add(piece.Definition);
            Assert.IsTrue(plan.Pockets.Exists(pocket => pocket.centre == piece.Position && pocket.radius >= piece.Definition.Radius), piece.Definition.name);
        }

        Assert.GreaterOrEqual(plan.Pieces.Count, 3);
        Assert.AreEqual(plan.Pieces.Count, kinds.Count, "No place twice while there are others");
    }

    [Test]
    public void SetPiecesLeaveTheSitesWhereTheyWere()
    {
        RaidLayoutRules with = Rules;
        RaidLayoutRules without = Object.Instantiate(with);
        new SerializedObjectEdit(without).ClearPieces();
        RaidLayoutPlan a = RaidLayoutPlanner.Plan(with, Size, 9);
        RaidLayoutPlan b = RaidLayoutPlanner.Plan(without, Size, 9);
        Assert.AreEqual(b.Sites.Count, a.Sites.Count);
        for (int i = 0; i < a.Sites.Count; i++)
        {
            Assert.AreEqual(b.Sites[i].Position, a.Sites[i].Position);
        }

        Assert.AreEqual(0, b.Pieces.Count);
        Object.DestroyImmediate(without);
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(4, true)]
    public void GuardsComeFromTheirTier(int tier, bool comes)
    {
        var guard = new RaidSetPieceGuard { fromTier = 2 };
        Assert.AreEqual(comes, guard.ComesAt(tier));
    }

    [Test]
    public void AGuardWithNoTierAlwaysComes()
    {
        Assert.IsTrue(new RaidSetPieceGuard().ComesAt(0));
    }

    private sealed class SerializedObjectEdit
    {
        private readonly RaidLayoutRules rules;

        public SerializedObjectEdit(RaidLayoutRules rules) => this.rules = rules;

        public void ClearPieces()
        {
            var so = new SerializedObject(rules);
            so.FindProperty("setPieces").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
