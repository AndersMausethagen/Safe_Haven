using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RaidCompanionTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object one in made)
        {
            Object.DestroyImmediate(one);
        }

        made.Clear();
    }

    private ResidentDefinition Person(string id, params JobDefinition[] jobs)
    {
        var person = ScriptableObject.CreateInstance<ResidentDefinition>();
        var so = new SerializedObject(person);
        so.FindProperty("id").stringValue = id;
        SerializedProperty list = so.FindProperty("jobs");
        list.arraySize = jobs.Length;
        for (int i = 0; i < jobs.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = jobs[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        made.Add(person);
        return person;
    }

    private JobDefinition Job(string id)
    {
        var job = ScriptableObject.CreateInstance<JobDefinition>();
        var so = new SerializedObject(job);
        so.FindProperty("id").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        made.Add(job);
        return job;
    }

    [Test]
    public void SomeoneLostIsGoneFromTheHavenAndCantBeRescued()
    {
        var settlement = new SettlementState();
        ResidentDefinition sigrid = Person("sigrid");
        settlement.AddResident(sigrid);
        settlement.LoseResident(sigrid, "The Misty Woods");
        Assert.IsNull(settlement.Find(sigrid), "They no longer live here");
        Assert.IsTrue(settlement.IsFallen(sigrid));
        Assert.IsTrue(settlement.HasResident(sigrid), "Raids treat them as accounted for, so they aren't found again");
        Assert.IsFalse(settlement.AddResident(sigrid), "Only by being found: CompanionAmnesiaTests");
        Assert.AreEqual("The Misty Woods", settlement.Fallen[0].Where);
    }

    [Test]
    public void AJobLeftBehindIsOpenToAnyoneUntilTaken()
    {
        var settlement = new SettlementState();
        JobDefinition smith = Job("smith");
        JobDefinition cook = Job("cook");
        ResidentDefinition bjarne = Person("bjarne", smith);
        ResidentDefinition liv = Person("liv", cook);
        settlement.AddResident(bjarne, smith);
        settlement.AddResident(liv);
        Assert.IsFalse(settlement.JobsOpenTo(liv).Contains(smith));

        settlement.LoseResident(bjarne, "The Misty Woods");
        Assert.IsTrue(settlement.JobsOpenTo(liv).Contains(smith), "Losing the only smith must not end smithing");

        settlement.AssignJob(liv, smith);
        Assert.AreEqual(0, settlement.VacantJobs.Count, "It is filled now");
    }

    [Test]
    public void AJobSomeoneElseHoldsIsNotLeftVacant()
    {
        var settlement = new SettlementState();
        JobDefinition farmer = Job("farmer");
        ResidentDefinition a = Person("a", farmer);
        ResidentDefinition b = Person("b", farmer);
        settlement.AddResident(a, farmer);
        settlement.AddResident(b, farmer);
        settlement.LoseResident(a, "There");
        Assert.AreEqual(0, settlement.VacantJobs.Count);
    }

    [Test]
    public void TalkOfALossFadesAfterAWhile()
    {
        var settlement = new SettlementState();
        ResidentDefinition a = Person("a");
        settlement.AddResident(a);
        Assert.IsNull(settlement.RecentLoss(2));
        settlement.LoseResident(a, "There");
        Assert.AreEqual(a, settlement.RecentLoss(2).Resident);
        settlement.RestoreFallen(Person("b"), "Long ago", -5);
        Assert.IsNull(settlement.RecentLoss(2), "The last one lost was long ago");
    }

    [Test]
    public void TheExpeditionCanGoAlong()
    {
        foreach (string name in new[] { "Sigrid", "Eirik", "Runa" })
        {
            var person = AssetDatabase.LoadAssetAtPath<ResidentDefinition>($"Assets/_Project/Data/Prologue/Residents/{name}.asset");
            Assert.IsNotNull(person.RaidCompanion, name);
            Assert.GreaterOrEqual(person.RaidCompanion.BleedOutSeconds, 30f, "Time to get to them");
        }

        Assert.IsNotNull(SettlementSettings.Current.RaidCompanionPrefab);
        Assert.IsNotNull(SettlementSettings.Current.FallenCairn);
        Assert.IsNull(SettlementSettings.Current.RaidCompanionPrefab.GetComponentInChildren<Animator>(true), "The model comes from the person");
        Assert.IsTrue(SettlementSettings.Current.RaidCompanionPrefab.gameObject.activeSelf, "It has to wake when it arrives");
    }

    // Review 2026-11-17 (S6): with nowhere for the companion to stand beside the player, they stayed at home, and the summary
    // still said "Gunnar made it home."
    [Test]
    public void ACompanionWhoNeverWalkedIn_IsntReportedAsHome()
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        made.Add(raid);
        RaidModifierDefinition[] none = new RaidModifierDefinition[0];
        var session = new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven") { Companion = Person("gunnar") };
        // Inactive, so nothing of the director runs: only its session is wanted.
        var holder = new GameObject("Raid");
        holder.SetActive(false);
        made.Add(holder);
        RaidDirector director = holder.AddComponent<RaidDirector>();
        typeof(RaidDirector).GetProperty(nameof(RaidDirector.Session)).SetValue(director, session);
        RaidReport report = RaidReport.Create(session, null, null);

        new RaidCompanions(director).Settle(report, true);

        Assert.IsNull(report.Companion, "they never left home, so the summary says nothing of them");
        Assert.IsFalse(report.CompanionLost, "nor are they lost");
    }
}
