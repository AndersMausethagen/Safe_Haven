using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Docs/RaidEventsPlan_2026-11-26.md steps 4-6: the save remembers choices and follow-ups come of them, the player's state and the run's
/// kind of day weigh the picks, and legends come once in a save.
/// </summary>
public sealed class RaidEventsMemoryTests1126
{
    private static readonly string[] Generated = { "MistyWoods", "TrollValley", "MossMarsh" };

    private static RaidEventSettings Settings(string id) => RaidEventSettings.Of(AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{id}.asset"));

    private static List<RaidEventDefinition> AllPools() => Generated.SelectMany(id => Settings(id).pool).Where(e => e != null).Distinct().ToList();

    // A mark set anywhere in the event's asset: a choice's own outcomes, a chance's side, its start.
    private static bool SetsMark(RaidEventDefinition definition, string mark) =>
        System.Text.RegularExpressions.Regex.IsMatch(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(definition)),
            $@"^\s+mark: {System.Text.RegularExpressions.Regex.Escape(mark)}\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);

    [Test]
    public void AFollowUpWaitsForALaterRaid_ItsMarkAskedOfTheSaveAsTheRaidBegan()
    {
        var follow = ScriptableObject.CreateInstance<RaidEventDefinition>();
        try
        {
            var so = new SerializedObject(follow);
            so.FindProperty("needsMark").stringValue = "a";
            so.FindProperty("blockedByMark").stringValue = "b";
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(follow.AllowedBy(mark => mark == "a", mark => false), "the choice was made this raid: not yet");
            Assert.IsTrue(follow.AllowedBy(mark => mark == "a", mark => mark == "a"), "held as the raid began");
            Assert.IsFalse(follow.AllowedBy(mark => mark == "a" || mark == "b", mark => mark == "a"), "met already this raid: the blocker is asked now");
        }
        finally
        {
            Object.DestroyImmediate(follow);
        }
    }

    [Test]
    public void TheWatchPostNeverForetellsALegendMet_OrAFollowUpNotEarned()
    {
        foreach (string id in Generated)
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{id}.asset");
            RaidEventSettings settings = Settings(id);
            // Every legend met, no mark held otherwise: across many seeds and a forecast as long as the raid allows.
            System.Func<string, bool> legendsMet = mark => mark.StartsWith("event:once:");
            foreach (int seed in Enumerable.Range(1, 60))
            {
                foreach (RaidEventDefinition foretold in RaidForecast.For(raid, seed, settings.maxPerRaid, legendsMet))
                {
                    Assert.IsFalse(foretold.OncePerSave, $"{id} › seed {seed}: {foretold.name} was met already");
                    Assert.IsTrue(string.IsNullOrEmpty(foretold.NeedsMark), $"{id} › seed {seed}: {foretold.name} follows a choice never made");
                }
            }
        }
    }

    [Test]
    public void AFollowUpComesOnlyWithItsMark_AndNeverAfterItsOwn()
    {
        var follow = ScriptableObject.CreateInstance<RaidEventDefinition>();
        try
        {
            var so = new SerializedObject(follow);
            so.FindProperty("needsMark").stringValue = "a";
            so.FindProperty("blockedByMark").stringValue = "b";
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(follow.AllowedBy(mark => false), "not before the choice it follows");
            Assert.IsTrue(follow.AllowedBy(mark => mark == "a"));
            Assert.IsFalse(follow.AllowedBy(mark => mark == "a" || mark == "b"), "never again once met");
            so.FindProperty("needsMark").stringValue = "";
            so.FindProperty("oncePerSave").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(follow.AllowedBy(mark => false));
            Assert.IsFalse(follow.AllowedBy(mark => mark == follow.OnceMark), "a legend, once in a save");
        }
        finally
        {
            Object.DestroyImmediate(follow);
        }
    }

    [Test]
    public void StatesAndTheKindOfDayWeighThePick()
    {
        var followed = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>("Assets/_Project/Data/Raids/Events/Followed.asset");
        float calm = followed.WeightIn(new HashSet<RaidEventState>());
        float full = followed.WeightIn(new HashSet<RaidEventState> { RaidEventState.BagMostlyFull });
        Assert.Greater(full, calm * 5f, "a full bag calls up what follows it");

        var condition = new RaidEventCondition { events = new List<RaidEventDefinition> { followed }, times = 3f };
        Assert.AreEqual(3f, condition.Boosts(followed));
        Assert.AreEqual(1f, condition.Boosts(null));

        RaidEventSettings valley = Settings("TrollValley");
        int seedWithADay = Enumerable.Range(1, 400).First(seed => valley.ConditionFor(seed) != null);
        Assert.AreSame(valley.ConditionFor(seedWithADay), valley.ConditionFor(seedWithADay), "the briefing and the raid roll the same day from the seed");
        int days = Enumerable.Range(1, 400).Count(seed => valley.ConditionFor(seed) != null);
        float expected = valley.conditions.Sum(c => c.chance) * 400f;
        Assert.AreEqual(expected, days, 60f, "about as often as the chances say");
    }

    [Test]
    public void EveryFollowUpsMarkIsSetSomewhere_AndEveryChoiceEndsIt()
    {
        List<RaidEventDefinition> pools = AllPools();
        foreach (RaidEventDefinition definition in pools.Where(e => !string.IsNullOrEmpty(e.NeedsMark)))
        {
            Assert.IsTrue(pools.Any(other => other != definition && SetsMark(other, definition.NeedsMark)), $"{definition.name}: something sets {definition.NeedsMark}");
            var so = new SerializedObject(definition);
            string blocked = so.FindProperty("blockedByMark").stringValue;
            Assert.IsNotEmpty(blocked, $"{definition.name}: a follow-up comes once");
            // With no choice nothing would ever set its blocker, and it would come every raid.
            Assert.IsNotEmpty(definition.Choices, $"{definition.name}: a follow-up is ended by a choice");
            foreach (RaidEventChoice choice in definition.Choices)
            {
                Assert.IsTrue(choice.outcomes.OfType<SetMarkOutcome>().Any(m => m.Mark == blocked), $"{definition.name} › {choice.label}: ends it, whatever is chosen");
            }
        }
    }

    [Test]
    public void EveryRaidHasALegend_AndAKindOfDayOfItsOwn()
    {
        foreach (string id in Generated)
        {
            RaidEventSettings settings = Settings(id);
            RaidEventDefinition legend = settings.pool.FirstOrDefault(e => e != null && e.OncePerSave);
            Assert.IsNotNull(legend, $"{id}: a legend");
            // A relic somewhere in its outcomes, however deep (a chance's lucky side, a hold's reward).
            string asset = System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(legend));
            StringAssert.Contains("GiveRelicOutcome", asset, $"{id} › {legend.name}: a reward of its own");
            Assert.IsNotEmpty(settings.conditions, $"{id}: kinds of day");
            foreach (RaidEventCondition condition in settings.conditions)
            {
                Assert.IsTrue(condition.events.All(e => settings.pool.Contains(e)), $"{id} › {condition.name}: makes likelier only what can come here");
                Assert.IsFalse(string.IsNullOrWhiteSpace(condition.briefing), $"{id} › {condition.name}: told in the briefing");
            }

            Assert.LessOrEqual(settings.conditions.Sum(c => c.chance), 1f, $"{id}: at most one kind of day a run");
        }
    }
}
