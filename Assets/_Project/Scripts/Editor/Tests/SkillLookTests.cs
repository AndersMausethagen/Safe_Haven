using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skills dressed with effects (Docs/CombatSkillsResearch_2026-11-24.md slice 1): where a look's effects go over the attack's shape,
/// the frost spell freezing what it already chilled, and the three first skills dressed with prefabs that exist.
/// </summary>
public sealed class SkillLookTests
{
    private readonly List<GameObject> made = new List<GameObject>();

    [TearDown]
    public void Clean()
    {
        foreach (GameObject thing in made)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        made.Clear();
    }

    [Test]
    public void EffectsAreSpreadOverTheAttacksOwnShape()
    {
        Vector3 origin = new Vector3(1000f, 500f, 1000f); // over nothing: the points keep their height
        var along = SkillFx.Points(new SkillFxCue { place = SkillFxPlace.AlongReach, count = 4 }, origin, Vector3.forward, 8f, 30f, null);
        Assert.AreEqual(4, along.Count);
        for (int i = 1; i < along.Count; i++)
        {
            Assert.Greater(along[i].z, along[i - 1].z, "out along the aim, nearest first");
        }

        Assert.AreEqual(origin.z + 8f, along[3].z, 0.01f, "the last at the reach");

        var ring = SkillFx.Points(new SkillFxCue { place = SkillFxPlace.Ring, count = 8 }, origin, Vector3.forward, 4f, 360f, null);
        Assert.AreEqual(8, ring.Count);
        foreach (Vector3 point in ring)
        {
            Assert.AreEqual(4f, Vector3.Distance(new Vector3(point.x, origin.y, point.z), origin), 0.01f, "round the reach");
        }

        var arc = SkillFx.Points(new SkillFxCue { place = SkillFxPlace.AcrossArc, count = 5 }, origin, Vector3.forward, 5f, 90f, null);
        foreach (Vector3 point in arc)
        {
            Assert.LessOrEqual(Vector3.Angle(Vector3.forward, point - origin), 45.01f, "inside the arc");
        }

        var struck = SkillFx.Points(new SkillFxCue { place = SkillFxPlace.Struck }, origin, Vector3.forward, 5f, 90f,
            new List<Vector3> { origin + Vector3.right, origin + Vector3.left });
        Assert.AreEqual(2, struck.Count, "one on each creature struck");
    }

    [Test]
    public void TheFrostSpell_FreezesWhatItHasAlreadyChilled()
    {
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/Skills/FrostRune.asset");
        Assert.IsNotNull(attack.AppliesStatus, "it chills");
        Assert.IsNotNull(attack.OnRepeatStatus, "and freezes the chilled");
        var target = new GameObject("A test's creature");
        made.Add(target);
        Damageable body = target.AddComponent<Damageable>();
        var hit = new DamageResult(10f, 10f, 10f, false, false, DamageType.Frost);

        StatusEffects.AfterHit(attack, body, null, hit);
        var effects = target.GetComponent<StatusEffects>();
        Assert.IsTrue(effects.Has(attack.AppliesStatus));
        Assert.IsFalse(effects.Has(attack.OnRepeatStatus), "one blow only chills");

        StatusEffects.AfterHit(attack, body, null, hit);
        Assert.IsTrue(effects.Has(attack.OnRepeatStatus), "the second, on a chilled creature, freezes it");
    }

    [Test]
    public void TheFirstSkills_AreDressedWithEffectsThatExist()
    {
        int cues = 0;
        foreach (string name in new[] { "FrostRune", "AshladsEmber", "TrollsbaneWrath" })
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{name}.asset");
            Assert.IsNotNull(skill.Look, name);
            Assert.Greater(skill.Look.release.Count, 0, $"{name} shows its release");
            foreach (var cue in skill.Look.cast) { Assert.IsNotNull(cue.prefab, name); cues++; }
            foreach (var cue in skill.Look.release) { Assert.IsNotNull(cue.prefab, name); cues++; }
            foreach (var cue in skill.Look.struck) { Assert.IsNotNull(cue.prefab, name); cues++; }
        }

        Assert.Greater(cues, 10);
    }
}
