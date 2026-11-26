using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Docs/RaidEventsPlan_2026-11-26.md steps 1-3: what is at stake is told before the walk, every site has a choice that is free on the clock,
/// choices that need something show greyed with their reason, the existing events have such a choice, and the new tasks (Defend, Carry).
/// </summary>
public sealed class RaidEventsPlanTests1126
{
    private static readonly string[] Generated = { "MistyWoods", "TrollValley", "MossMarsh" };

    private static List<RaidEventDefinition> Pools()
    {
        var all = new List<RaidEventDefinition>();
        foreach (string id in Generated)
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{id}.asset");
            var so = new SerializedObject(raid);
            SerializedProperty systems = so.FindProperty("systems");
            for (int i = 0; i < systems.arraySize; i++)
            {
                SerializedProperty system = systems.GetArrayElementAtIndex(i);
                if (system.managedReferenceValue is RaidEventsSystem)
                {
                    SerializedProperty pool = system.FindPropertyRelative("settings").FindPropertyRelative("pool");
                    for (int k = 0; k < pool.arraySize; k++)
                    {
                        if (pool.GetArrayElementAtIndex(k).objectReferenceValue is RaidEventDefinition e && !all.Contains(e))
                        {
                            all.Add(e);
                        }
                    }
                }
            }
        }

        return all;
    }

    private static RaidEventDefinition Event(string name) => AssetDatabase.LoadAssetAtPath<RaidEventDefinition>($"Assets/_Project/Data/Raids/Events/{name}.asset");

    // ----- E1 -----

    [Test]
    public void TheStakesAreToldInWords_OnlyWhatIsAtStake()
    {
        string words = new EventStakes(2, 1, 0).Words("#a", "#b", "#c");
        StringAssert.Contains("rich", words);
        StringAssert.Contains("risky", words);
        StringAssert.DoesNotContain("time", words);
        Assert.IsTrue(new EventStakes(0, 0, 0).IsEmpty);
        StringAssert.Contains("deadly", new EventStakes(0, 2, 2).Words("#a", "#b", "#c"));
    }

    [Test]
    public void EveryEventWithASiteTellsItsStakes_AndHasAChoiceFreeOnTheClock()
    {
        foreach (RaidEventDefinition definition in Pools().Where(e => e.SitePrefab != null))
        {
            Assert.IsFalse(definition.Stakes.IsEmpty, $"{definition.name}: what is at stake, told before the walk");
            if (definition.Choices.Count == 0)
            {
                continue;
            }

            bool free = definition.Choices.Any(c => c.cost.Count == 0 && (c.requires == null || c.requires.Count == 0)
                && !c.outcomes.OfType<ChangeTimeOutcome>().Any());
            Assert.IsTrue(free, $"{definition.name}: one choice anyone can take that costs no time on the clock");
        }
    }

    // ----- E2 -----

    [Serializable]
    private sealed class Never : ChoiceRequirement
    {
        public override string DisplayName => "Never";
        public override string Reason => "Needs the moon";
        public override bool IsMet(GameObject player, RaidSession session) => false;
    }

    [Serializable]
    private sealed class Always : ChoiceRequirement
    {
        public override string DisplayName => "Always";
        public override string Reason => "";
        public override bool IsMet(GameObject player, RaidSession session) => true;
    }

    [Test]
    public void AChoiceNeedsEveryRequirement_AndSaysTheFirstUnmet()
    {
        var choice = new RaidEventChoice { requires = new List<ChoiceRequirement> { new Always(), new Never() } };
        Assert.IsInstanceOf<Never>(choice.Unmet(null, null));
        Assert.AreEqual("Needs the moon", choice.Unmet(null, null).Reason);
        choice.requires = new List<ChoiceRequirement> { new Always() };
        Assert.IsNull(choice.Unmet(null, null));
        choice.requires = null;
        Assert.IsNull(choice.Unmet(null, null), "no needs: anyone");
    }

    [Test]
    public void TheExistingEventsHaveAChoiceThatNeedsSomething()
    {
        foreach (string name in new[] { "TrappedPedlar", "ThreeWishes", "TradersWagon", "TollTroll", "LivingStones", "OldBarrow", "CursedTree" })
        {
            RaidEventDefinition definition = Event(name);
            RaidEventChoice needing = definition.Choices.FirstOrDefault(c => c.requires != null && c.requires.Count > 0);
            Assert.IsNotNull(needing, $"{name}: a choice for a skill, a tool or someone along");
            Assert.IsFalse(needing.requires.Any(r => r == null || string.IsNullOrEmpty(r.Reason)), $"{name}: says what it needs");
            Assert.IsNotEmpty(needing.outcomes, $"{name}: and pays for it");
        }

        Assert.IsTrue(Event("BeaconLight").Choices.Any(c => c.outcomes.OfType<DefendOutcome>().Any()), "the Old Beacon can be lit and held");
    }

    // ----- E3 -----

    [Serializable]
    private sealed class Counting : RaidEventOutcome
    {
        public int Count;
        public override string DisplayName => "Counting";
        public override void Apply(RaidEventContext context) => Count++;
    }

    [Test]
    public void AHoldCountsOnlyInside_WavesComeOnTime_AndPaysOnce()
    {
        var site = new GameObject("Site");
        try
        {
            var wave = new Counting();
            var reward = new Counting();
            DefendTask task = site.AddComponent<DefendTask>();
            task.Begin(new RaidEventContext(), "Hold", 5f, 40f, 13f, new List<RaidEventOutcome> { wave }, new List<RaidEventOutcome> { reward });
            task.Step(1f, true);
            Assert.AreEqual(1, wave.Count, "the first wave at once");
            task.Step(30f, false);
            Assert.AreEqual(1f, task.Held, 0.001f, "outside the circle the count waits");
            for (int i = 0; i < 39; i++)
            {
                task.Step(1f, true);
            }

            Assert.AreEqual(1, reward.Count, "held for 40 seconds: paid once");
            Assert.AreEqual(3, wave.Count, "waves at 0, 13 and 26 seconds held; none at 39, with the reward a second away");
            task.Step(10f, true);
            Assert.AreEqual(1, reward.Count, "never twice");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(site);
        }
    }

    [Test]
    public void EveryRaidHasAnEventWithATask()
    {
        foreach ((string raid, string name) in new[] { ("MistyWoods", "EldersBell"), ("TrollValley", "StoneHead"), ("MossMarsh", "SunkenBellTower") })
        {
            RaidEventDefinition definition = Event(name);
            Assert.IsNotNull(definition, name);
            Assert.IsTrue(definition.Choices.Any(c => c.outcomes.Any(o => o is DefendOutcome || o is CarryOutcome)), $"{name}: something to do at the site");
            Assert.IsNotNull(definition.Artwork, $"{name}: its painting");
        }

        var burden = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/Events/Burdens/Stone Head Burden.prefab");
        Assert.IsNotNull(burden.GetComponent<CarriedBurden>());
        Assert.IsNotNull(burden.GetComponent<Interactable>(), "picked up again after it is set down");
        Assert.IsNotNull(burden.GetComponent<Collider>(), "solid where it lies");
        Assert.Less(burden.GetComponent<CarriedBurden>().SpeedScale, 1f, "heavy: it slows the one who carries it");
    }
}
