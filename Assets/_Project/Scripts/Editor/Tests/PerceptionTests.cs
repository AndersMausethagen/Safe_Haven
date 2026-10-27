using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Monster senses: the noticing arithmetic (PerceptionRules), awareness stages and line of sight.</summary>
public sealed class PerceptionTests
{
    private readonly List<Object> created = new List<Object>();
    private PerceptionSettings rules;

    [SetUp]
    public void SetUp()
    {
        rules = ScriptableObject.CreateInstance<PerceptionSettings>();
        created.Add(rules);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object value in created)
        {
            Object.DestroyImmediate(value);
        }
        created.Clear();
    }

    private static SenseProfile Eyes(float range = 10f, float angle = 120f, float close = 2.5f) =>
        new SenseProfile(range, angle, close, 1f, 1f);

    [Test]
    public void Notice_IsFasterNearThanFar()
    {
        float near = PerceptionRules.NoticeRate(3f, 0f, Eyes(), 1f, rules);
        float far = PerceptionRules.NoticeRate(9.5f, 0f, Eyes(), 1f, rules);
        Assert.Greater(near, far);
        Assert.Greater(far, 0f);
    }

    [Test]
    public void Notice_OnlyInsideTheCone_ExceptUpClose()
    {
        Assert.Greater(PerceptionRules.NoticeRate(6f, 55f, Eyes(), 1f, rules), 0f, "inside a 120° cone");
        Assert.AreEqual(0f, PerceptionRules.NoticeRate(6f, 65f, Eyes(), 1f, rules), "outside the cone");
        Assert.AreEqual(0f, PerceptionRules.NoticeRate(6f, 180f, Eyes(), 1f, rules), "behind it");
        Assert.AreEqual(1f / rules.CloseNoticeSeconds, PerceptionRules.NoticeRate(2f, 180f, Eyes(), 1f, rules), 1e-4f,
            "close range notices from any direction");
    }

    [Test]
    public void Sneaking_ShortensSightRange_AndSlowsNoticing()
    {
        float visible = PerceptionRules.EffectiveSightRange(Eyes(), rules.MovingVisibility, rules);
        float sneaking = PerceptionRules.EffectiveSightRange(Eyes(), rules.SneakingVisibility, rules);
        float fighting = PerceptionRules.EffectiveSightRange(Eyes(), rules.FightingVisibility, rules);
        Assert.Less(sneaking, visible);
        Assert.Greater(fighting, visible);
        Assert.AreEqual(0f, PerceptionRules.NoticeRate(8f, 0f, Eyes(), rules.SneakingVisibility, rules),
            "a sneaking player at 8 m is past the shortened range");
        Assert.Less(PerceptionRules.NoticeRate(4f, 0f, Eyes(), rules.SneakingVisibility, rules),
            PerceptionRules.NoticeRate(4f, 0f, Eyes(), 1f, rules));
    }

    [Test]
    public void Hearing_FallsOffWithDistance_AndNeverSpots()
    {
        Assert.AreEqual(0f, PerceptionRules.HeardLevel(6f, 5f, 1f, rules), "beyond the radius");
        Assert.Greater(PerceptionRules.HeardLevel(6f, 5f, 1.5f, rules), 0f, "keen hearing reaches further");
        float edge = PerceptionRules.HeardLevel(4.9f, 5f, 1f, rules);
        float source = PerceptionRules.HeardLevel(0f, 5f, 1f, rules);
        Assert.GreaterOrEqual(edge, rules.SuspiciousAt, "a sound it hears at all makes it suspicious");
        Assert.Greater(source, edge);
        Assert.Less(source, 1f);
    }

    [Test]
    public void Fog_ShortensSight_DownToTheDenseFogShare()
    {
        Assert.AreEqual(1f, PerceptionRules.FogSightScale(0f, 0.06f, 0.55f), 1e-5f, "clear air");
        Assert.AreEqual(0.775f, PerceptionRules.FogSightScale(0.03f, 0.06f, 0.55f), 1e-4f, "half way to dense");
        Assert.AreEqual(0.55f, PerceptionRules.FogSightScale(0.5f, 0.06f, 0.55f), 1e-5f, "never below dense fog's share");
        float clear = PerceptionRules.EffectiveSightRange(Eyes(), 1f, rules);
        Assert.AreEqual(clear * 0.55f, PerceptionRules.EffectiveSightRange(Eyes(), 1f, rules, 0.55f), 1e-4f);
        Assert.AreEqual(0f, PerceptionRules.NoticeRate(8f, 0f, Eyes(), 1f, rules, 0.55f), "8 m is out of sight in dense fog");
    }

    [Test]
    public void Awareness_MovesThroughStages()
    {
        var awareness = new Awareness();
        Assert.AreEqual(AwarenessStage.Unaware, awareness.StageAt(rules.SuspiciousAt));
        Assert.IsFalse(awareness.Notice(0.5f, Vector3.one));
        Assert.AreEqual(AwarenessStage.Suspicious, awareness.StageAt(rules.SuspiciousAt));
        Assert.IsTrue(awareness.Notice(0.6f, Vector3.one), "filling it spots the target");
        Assert.AreEqual(AwarenessStage.Alerted, awareness.StageAt(rules.SuspiciousAt));
        awareness.Decay(10f);
        Assert.IsTrue(awareness.IsAlerted, "an alerted monster doesn't calm down by itself");
        awareness.LoseTrack(Vector3.zero, 0.8f);
        Assert.AreEqual(AwarenessStage.Suspicious, awareness.StageAt(rules.SuspiciousAt));
        awareness.Decay(1f);
        Assert.AreEqual(AwarenessStage.Unaware, awareness.StageAt(rules.SuspiciousAt));
        Assert.IsFalse(awareness.HasStimulus);
    }

    [Test]
    public void Awareness_SoundsRaiseButNeverSpot()
    {
        var awareness = new Awareness();
        awareness.Hear(0.6f, Vector3.forward);
        awareness.Hear(0.4f, Vector3.back);
        Assert.AreEqual(0.6f, awareness.Level, 1e-5f, "a quieter sound doesn't lower it");
        Assert.AreEqual(Vector3.back, awareness.Stimulus, "but it looks toward the newest one");
        awareness.Hear(5f, Vector3.zero);
        Assert.IsFalse(awareness.IsAlerted);
        Assert.Less(awareness.Level, 1f);
    }

    [Test]
    public void Senses_SeeAheadButNotThroughWallsOrBehind()
    {
        var definition = ScriptableObject.CreateInstance<MonsterDefinition>();
        created.Add(definition);
        Transform eyes = Spawn("Monster", Vector3.zero).transform;
        GameObject target = Spawn("Target", new Vector3(0f, 0f, 6f));
        target.AddComponent<CapsuleCollider>().center = Vector3.up;
        target.AddComponent<Damageable>();
        var senses = new MonsterSenses(eyes, definition, ~0, (d, c) => true);
        Physics.SyncTransforms();

        Assert.IsTrue(senses.CanSee(target.transform, 10f), "in the open");
        bool spotted = false;
        for (int i = 0; i < 20 && !spotted; i++)
        {
            spotted = senses.Look(0.25f);
        }
        Assert.IsTrue(spotted, "a plainly visible target ahead is spotted within five seconds");
        Assert.AreSame(target.GetComponent<Damageable>(), senses.Spotted);

        GameObject wall = Spawn("Wall", new Vector3(0f, 1f, 3f));
        wall.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 0.5f);
        Physics.SyncTransforms();
        Assert.IsFalse(senses.CanSee(target.transform, 10f), "behind a wall");

        created.Remove(wall);
        Object.DestroyImmediate(wall);
        target.transform.position = new Vector3(0f, 0f, -6f);
        Physics.SyncTransforms();
        var behind = new MonsterSenses(eyes, definition, ~0, (d, c) => true);
        for (int i = 0; i < 20; i++)
        {
            Assert.IsFalse(behind.Look(0.25f), "behind its back, beyond close range");
        }
        Assert.AreEqual(AwarenessStage.Unaware, behind.Stage);
    }

    private GameObject Spawn(string name, Vector3 position)
    {
        var go = new GameObject(name);
        created.Add(go);
        go.transform.position = position;
        return go;
    }
}
