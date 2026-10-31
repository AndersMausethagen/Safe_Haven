using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Monster senses: the noticing arithmetic (PerceptionRules), awareness stages and line of sight.</summary>
public sealed class PerceptionTests
{
    private readonly List<Object> created = new List<Object>();
    private PerceptionSettings rules;
    private float fog;
    private float night;

    [SetUp]
    public void SetUp()
    {
        rules = ScriptableObject.CreateInstance<PerceptionSettings>();
        created.Add(rules);
        // Sight shrinks in fog and at night; the open scene's sky must not decide these tests.
        fog = AtmosphereConditions.FogDensity;
        night = AtmosphereConditions.Night;
        AtmosphereConditions.FogDensity = 0f;
        AtmosphereConditions.Night = 0f;
    }

    [TearDown]
    public void TearDown()
    {
        AtmosphereConditions.FogDensity = fog;
        AtmosphereConditions.Night = night;
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
    public void Hearing_ThroughCover_ReachesLess()
    {
        Assert.Greater(PerceptionRules.HeardLevel(4f, 5f, 1f, rules), 0f, "in the open");
        Assert.AreEqual(0f, PerceptionRules.HeardLevel(4f, 5f, 1f, rules, true), "4 m is too far through cover at 60% of 5 m");
        Assert.Greater(PerceptionRules.HeardLevel(2f, 5f, 1f, rules, true), 0f, "cover muffles a sound, it doesn't stop it");
    }

    [Test]
    public void Hearing_PlacesFarSoundsRoughly_AndNearOnesExactly()
    {
        Assert.AreEqual(0f, PerceptionRules.HeardError(0f, 10f, 1f, rules), "at the source");
        Assert.AreEqual(rules.HeardSpreadAtEdge * 0.5f, PerceptionRules.HeardError(5f, 10f, 1f, rules), 1e-4f);
        Assert.AreEqual(rules.HeardSpreadAtEdge, PerceptionRules.HeardError(10f, 10f, 1f, rules), 1e-4f, "at the edge");
    }

    [Test]
    public void Noise_GearAndRain_ScaleWhatCarries()
    {
        Assert.AreEqual(1f, PerceptionRules.GearNoiseScale(6f, 6f, 13f, rules), 1e-5f, "a light load is as set");
        Assert.AreEqual(rules.HeavyGearNoise, PerceptionRules.GearNoiseScale(20f, 6f, 13f, rules), 1e-5f, "capped from the heavy load");
        Assert.AreEqual(1f, PerceptionRules.RainNoiseScale(0f, rules), 1e-5f, "dry");
        Assert.AreEqual(rules.NoiseInRain, PerceptionRules.RainNoiseScale(1f, rules), 1e-5f, "the heaviest rain");
    }

    [Test]
    public void NoiseMeter_ShowsTheLastSound_HoldsIt_ThenFalls()
    {
        var meter = new NoiseMeter();
        Assert.AreEqual(5f, meter.Add(5f, rules), 1e-5f, "a first sound carries as it is");
        Assert.AreEqual(5f, meter.Level, 1e-5f);
        meter.Tick(rules.NoiseHoldSeconds * 0.5f, rules);
        Assert.AreEqual(5f, meter.Level, 1e-5f, "held");
        meter.Tick(rules.NoiseHoldSeconds, rules);
        Assert.Less(meter.Level, 5f, "then it falls");
        for (int i = 0; i < 100; i++)
        {
            meter.Tick(0.1f, rules);
        }

        Assert.AreEqual(0f, meter.Level, "and silence comes back");
    }

    [Test]
    public void NoiseMeter_WalkingStaysLevel_ButAFightBuildsUp()
    {
        var walking = new NoiseMeter();
        float step = 0f;
        for (int i = 0; i < 40; i++)
        {
            step = walking.Add(rules.FootstepNoise, rules);
            walking.Tick(rules.FootstepInterval, rules);
        }

        Assert.AreEqual(rules.FootstepNoise, step, 1e-4f, "steps don't add up");
        Assert.AreEqual(rules.FootstepNoise, walking.Level, 1e-4f, "and the meter reads steady between them");

        var fighting = new NoiseMeter();
        float first = fighting.Add(rules.CombatNoise, rules);
        float blow = first;
        for (int i = 0; i < 60; i++)
        {
            fighting.Tick(rules.CombatNoiseInterval, rules);
            blow = fighting.Add(rules.CombatNoise, rules);
        }

        Assert.Greater(blow, first + 1f, "a long fight carries further than one blow");
        Assert.LessOrEqual(blow, rules.CombatNoise + rules.NoiseBuildMax + 1e-4f, "up to the cap");
    }

    [Test]
    public void NoiseBand_NamesTheLevel()
    {
        Assert.AreEqual(NoiseBand.Silent, NoiseMeter.BandOf(0f, rules));
        Assert.AreEqual(NoiseBand.Quiet, NoiseMeter.BandOf(rules.SneakingFootstepNoise, rules), "sneaking");
        Assert.AreEqual(NoiseBand.Audible, NoiseMeter.BandOf(rules.FootstepNoise, rules), "walking");
        Assert.AreEqual(NoiseBand.Loud, NoiseMeter.BandOf(rules.CombatNoise, rules), "a fight");
        Assert.AreEqual(NoiseBand.Deafening, NoiseMeter.BandOf(rules.CombatNoise + rules.NoiseBuildMax, rules), "a long fight");
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
