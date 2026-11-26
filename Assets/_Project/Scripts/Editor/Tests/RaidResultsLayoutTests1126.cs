using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The results screen's UX pass of 2026-11-26 (the user: "veldig mye tekst i høyre del av skjermen hvor man må scrolle ... del opp tekst
/// med ikoner"): the right-hand column is short facts with pictures and icons (RaidReportFacts), grouped, each a title and at most a line;
/// the events met show their paintings; a fall says what killed the player first and that they woke at the portal.
/// </summary>
public sealed class RaidResultsLayoutTests1126
{
    private static RaidReport Report(string raidId, RaidOutcome outcome, params string[] events)
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{raidId}.asset");
        var none = new RaidModifierDefinition[0];
        var session = new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven");
        foreach (string happened in events)
        {
            session.RecordEvent(happened);
        }

        session.End(outcome);
        return RaidReport.Create(session, null, null);
    }

    [Test]
    public void AFall_ToldFirst_ByWhatBroughtYouDown_AndThatYouWokeAtThePortal()
    {
        RaidReport report = Report("MistyWoods", RaidOutcome.Died);
        report.BroughtDownBy = "ForestBittenWolf";
        report.KillerAdvice = "Wolves circle: keep your back to a tree.";
        List<ReportFact> facts = RaidReportFacts.For(report);
        Assert.AreEqual("Skull", facts[0].Icon);
        StringAssert.StartsWith("Brought down by", facts[0].Title);
        Assert.AreEqual(FactTone.Bad, facts[0].Tone);
        Assert.AreEqual(report.KillerAdvice, facts[0].Line, "the advice under it, not a paragraph");
        Assert.IsTrue(facts.Any(f => f.Icon == "Lock" && f.Title.Contains("woke at the portal")));
    }

    [Test]
    public void EveryFactIsATitleAndAtMostALine_InItsGroupsOrder()
    {
        RaidReport report = Report("TrollValley", RaidOutcome.Extracted, "The Pedlar's Brother: Pay him back", "The Troll Wedding: Watch unseen");
        List<ReportFact> facts = RaidReportFacts.For(report, line => RaidReportFacts.EventPicture(report.Definition, line));
        Assert.IsTrue(facts.All(f => !string.IsNullOrEmpty(f.Title) && !f.Title.Contains("\n") && (f.Line == null || !f.Line.Contains("\n"))), "no paragraphs");
        Assert.IsTrue(facts.All(f => !string.IsNullOrEmpty(f.Icon) || f.Picture != null), "every fact has a picture or an icon");
        string[] order = { RaidReportFacts.People, RaidReportFacts.AtHome, RaidReportFacts.OnTheWay };
        List<int> groups = facts.Select(f => System.Array.IndexOf(order, f.Group)).ToList();
        CollectionAssert.AreEqual(groups.OrderBy(g => g).ToList(), groups, "grouped, in reading order");
        List<ReportFact> met = facts.Where(f => f.Group == RaidReportFacts.OnTheWay && f.Icon == "Path").ToList();
        Assert.AreEqual(2, met.Count);
        Assert.IsTrue(met.All(f => f.Picture != null), "the events met show their paintings");
    }

    [Test]
    public void AnEventsPainting_IsFoundByTheLongestNameStartingItsLine()
    {
        RaidDefinition valley = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/TrollValley.asset");
        RaidEventDefinition brother = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>("Assets/_Project/Data/Raids/Events/PedlarsBrother.asset");
        Assert.AreSame(brother.Artwork, RaidReportFacts.EventPicture(valley, brother.DisplayName + ": Run for it"));
        Assert.IsNull(RaidReportFacts.EventPicture(valley, "Crossed to the Old Stones"), "a line that names no event has no painting");
        Assert.IsNull(RaidReportFacts.EventPicture(null, "The Pedlar's Brother"));
    }
}
