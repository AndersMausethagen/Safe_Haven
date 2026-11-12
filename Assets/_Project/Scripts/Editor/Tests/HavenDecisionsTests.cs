using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>The Haven's decisions (HavenDecisions): found by their attribute, made at home, and what a visitor paid comes back.</summary>
public sealed class HavenDecisionsTests
{
    [Test]
    public void EveryDecision_IsFoundByItsName()
    {
        foreach (string name in new[] { "ward.raise", "portal.improve", "altar.purify", "resident.pause", "resident.job" })
        {
            Assert.IsTrue(HavenDecisions.All.ContainsKey(name), $"'{name}' is a Haven decision");
        }
    }

    [Test]
    public void APayment_TravelsAsText_AndIsReadBack()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ResourceDefinition essence = SettlementSettings.Current.CursedEssence;
        ItemDefinition item = null;
        foreach (ItemDefinition candidate in content.Items)
        {
            if (candidate != null)
            {
                item = candidate;
                break;
            }
        }

        string paid = HavenDecisions.Paid(new[] { new ResourceAmount(essence, 12) }, new[] { new ItemAmount(item, 3), new ItemAmount(item, 0) });
        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        HavenDecisions.ReadPaid(paid, content, resources, items);
        Assert.AreEqual(1, resources.Count);
        Assert.AreSame(essence, resources[0].Resource);
        Assert.AreEqual(12, resources[0].Amount);
        Assert.AreEqual(1, items.Count, "Nothing paid is nothing to give back.");
        Assert.AreSame(item, items[0].Item);
        Assert.AreEqual(3, items[0].Amount);
        Assert.AreEqual("", HavenDecisions.Paid(null, null));
    }

    [Test]
    public void AMakeOfADecisionNobodyKnows_IsRefused_NotThrown()
    {
        HavenDecisions.Outcome outcome = HavenDecisions.Make("no.such.decision", "");
        Assert.IsFalse(outcome.Done);
        Assert.IsNotEmpty(outcome.Text);
    }
}
