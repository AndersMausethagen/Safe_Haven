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
            Assert.AreEqual(4f, new Vector2(point.x - origin.x, point.z - origin.z).magnitude, 0.01f);
        }

        var arc = SkillFx.Points(new SkillFxCue { place = SkillFxPlace.AcrossArc, count = 5 }, origin, Vector3.forward, 5f, 90f, null);
        foreach (Vector3 point in arc)
        {
            // Flat: over a scene's terrain the points sit on the ground, below the origin.
            Vector3 flat = point - origin;
            flat.y = 0f;
            Assert.LessOrEqual(Vector3.Angle(Vector3.forward, flat), 45.01f, "inside the arc");
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
        var frostRune = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/FrostRune.asset");
        Assert.Greater(attack.AppliesStatus.Seconds, frostRune.Cooldown, "its chill outlasts its cooldown, so its next cast can freeze (not only a double hit)");
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
    public void EveryAbility_HasATeacher_AndANewGameStartsWithTheWeaponsArtOnTheBar()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PlayerSkillSettings>("Assets/_Project/Data/Player/PlayerSkillSettings.asset");
        var taught = new HashSet<SkillDefinition>(settings.StartingSkills);
        foreach (string guid in AssetDatabase.FindAssets("t:BossDefinition", new[] { "Assets/_Project" }))
        {
            taught.Add(AssetDatabase.LoadAssetAtPath<BossDefinition>(AssetDatabase.GUIDToAssetPath(guid)).RewardSkill);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project" }))
        {
            foreach (SkillDefinition skill in AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid)).RewardSkills)
            {
                taught.Add(skill);
            }
        }

        int checkedSkills = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data/Skills" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsTrue(taught.Contains(skill), $"{skill.name}: no start, boss or quest teaches it");
            checkedSkills++;
        }

        Assert.Greater(checkedSkills, 8);
        Assert.AreEqual(WeaponArtSlot.Own, settings.StartingLoadout[0].WeaponArt, "slot 1 is the weapon's art");
        for (int i = 1; i < settings.StartingLoadout.Count; i++)
        {
            Assert.IsNull(settings.StartingLoadout[i], "the rest of the bar waits for what the story teaches");
        }
    }

    [Test]
    public void SteelSundersAndTheWolfRends_AndTheSunderedTakeMore()
    {
        var whirl = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/WhirlingSteel.asset");
        var wrath = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/TrollsbaneWrath.asset");
        var wolf = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/WolfsLunge.asset");
        StatusEffectDefinition sundered = whirl.Attack.AppliesStatus;
        Assert.IsNotNull(sundered, "Whirling Steel cracks armour");
        Assert.AreSame(sundered, wrath.Attack.AppliesStatus, "so does Trollsbane Wrath: one Steel status");
        Assert.Greater(sundered.DamageTaken, 1f);
        Assert.IsNotNull(wolf.Attack.AppliesStatus, "Wolf's Lunge leaves a wound");
        Assert.Greater(wolf.Attack.AppliesStatus.DamagePerSecond, 0f, "that bleeds");

        var target = new GameObject("A test's armoured creature");
        made.Add(target);
        var combatant = target.AddComponent<Combatant>();
        combatant.SetFaction(Faction.Enemy);
        // No stats definition (it warns): the values come from here.
        combatant.Stats.SetBase(StatType.MaxHealth, 500f);
        combatant.Stats.SetBase(StatType.Armor, 40f);
        Damageable body = target.AddComponent<Damageable>();
        var data = new SerializedObject(body);
        data.FindProperty("destroyOnDeath").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(Damageable).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(body, null);
        var blow = new DamageInfo(null, null, 20f, DamageType.Physical, false, Vector3.zero, Vector3.forward);
        float before = body.CurrentHealth;
        body.ApplyDamage(blow);
        float plain = before - body.CurrentHealth;
        Assert.Greater(plain, 0f);

        StatusEffects.Apply(body, sundered, null);
        before = body.CurrentHealth;
        body.ApplyDamage(blow);
        Assert.AreEqual(plain * sundered.DamageTaken, before - body.CurrentHealth, 0.01f, "the same blow bites deeper on cracked armour");
    }

    [Test]
    public void TheSchoolsStatuses_ShowOnTheirBearer_WithAnEffectThatKeepsGoing()
    {
        foreach (string name in new[] { "Burning", "Frostbite", "Frozen", "Rend", "Sundered" })
        {
            var status = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>($"Assets/_Project/Data/Combat/Status/{name}.asset");
            Assert.IsNotNull(status.Look, $"{name} shows on what bears it");
            var visual = status.Look.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            Assert.IsNotNull(visual, name);
            Assert.IsNotNull(visual.visualEffectAsset, name);
            Assert.IsNotNull(status.Look.GetComponent<VfxValues>(), $"{name} carries its own look on the shared graph");
        }
    }

    [Test]
    public void EveryAbility_PlaysAMoveOfItsOwn_ThatTheBodyHas()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Project/Animation/Controllers/PlayerAnimator.controller");
        var fullBody = System.Array.Find(controller.layers, layer => layer.name == "Combat Full Body");
        var states = new HashSet<string>();
        foreach (var child in fullBody.stateMachine.states)
        {
            states.Add(child.state.name);
        }

        var used = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data/Skills" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill.WeaponArt != WeaponArtSlot.None)
            {
                continue; // the weapon's own art plays the art's swing
            }

            Assert.IsTrue(skill.PlayAttackAnimation, $"{skill.name} is seen to be cast");
            Assert.IsTrue(states.Contains(skill.AnimationState), $"{skill.name}: no full-body state '{skill.AnimationState}'");
            // Walking hands the move to the arms and the upper body: without it there, the whole-body clip slides along the ground.
            foreach (string walking in new[] { "Attack Arms", "Upper Body" })
            {
                var layer = System.Array.Find(controller.layers, l => l.name == walking);
                Assert.IsTrue(System.Array.Exists(layer.stateMachine.states, s => s.state.name == skill.AnimationState),
                    $"{skill.name}: '{skill.AnimationState}' missing on {walking}");
            }

            Assert.IsFalse(used.TryGetValue(skill.AnimationState, out string other), $"{skill.name} and {other} share '{skill.AnimationState}'");
            used[skill.AnimationState] = skill.name;
        }

        Assert.GreaterOrEqual(used.Count, 8);
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
