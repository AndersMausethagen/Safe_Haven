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
    public void AHeavyShattersTheFrozen_AndTheChilledAreSlowed()
    {
        var frozen = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Frozen.asset");
        var frostbite = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Frostbite.asset");
        Assert.IsTrue(frozen.StopsStrikes, "a frozen creature strikes no blow");
        Assert.AreEqual(0f, frozen.MoveSpeed, "nor moves");

        var target = new GameObject("A test's creature to freeze");
        made.Add(target);
        var combatant = target.AddComponent<Combatant>();
        combatant.SetFaction(Faction.Enemy);
        combatant.Stats.SetBase(StatType.MaxHealth, 500f);
        Damageable body = target.AddComponent<Damageable>();
        var data = new SerializedObject(body);
        data.FindProperty("destroyOnDeath").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(Damageable).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(body, null);

        StatusEffects effects = StatusEffects.Apply(body, frostbite, null);
        Assert.AreEqual(frostbite.MoveSpeed, effects.MoveScale, 1e-4f, "the chilled creature is slowed too, not only the player");
        StatusEffects.Apply(body, frozen, null);
        Assert.AreEqual(0f, effects.MoveScale, 1e-4f, "and the frozen held fast");

        var landed = new DamageResult(40f, 40f, 40f, false, false, DamageType.Physical);
        StatusEffects.BeforeHitStatuses(body, new DamageInfo(null, null, 40f, DamageType.Physical, false, Vector3.zero, Vector3.forward, 0f,
            HitTechnique.Melee), landed);
        Assert.IsTrue(effects.Has(frozen), "an ordinary swing doesn't shatter it");

        float before = body.CurrentHealth;
        StatusEffects.BeforeHitStatuses(body, new DamageInfo(null, null, 40f, DamageType.Physical, false, Vector3.zero, Vector3.forward, 0f,
            HitTechnique.Heavy | HitTechnique.Melee), landed);
        Assert.IsFalse(effects.Has(frozen), "a heavy shatters it");
        Assert.Greater(before - body.CurrentHealth, 0f, "and the shards hurt");
        Assert.IsTrue(effects.Has(frostbite), "the chill under it stays");
    }

    [Test]
    public void FireAndFrost_LeaveTheirGroundBehind()
    {
        foreach (string name in new[] { "AshladsEmber", "FrostRune" })
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{name}.asset");
            Assert.IsNotNull(skill.Leaves, $"{name} leaves something where it lands");
            Assert.IsNotNull(skill.Leaves.GetComponent<GroundHazard>(), $"{name}: a patch of ground that hurts the caster's foes");
            Assert.Greater(skill.LeavesCount, 0);
        }
    }

    [Test]
    public void AWeaponArt_LightsTheBlade_InItsOwnColour()
    {
        GameObject glow = CombatFeelSettings.Current.ArtBladeGlow;
        Assert.IsNotNull(glow, "an art lights the blade (ArtBladeView)");
        var visual = glow.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
        Assert.IsNotNull(visual);
        Assert.IsTrue(visual.HasVector3("Color"), "coloured as the art");
        Assert.IsTrue(visual.HasFloat("Radius"), "and spread along the blade");
    }

    [Test]
    public void TheWarCry_CowsFoes_AndThePortalTeachesItAfterAFall()
    {
        var cry = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/WarCry.asset");
        Assert.IsNotNull(cry.FoesStatus, "it lays something on the foes that hear it");
        Assert.Less(cry.FoesStatus.DamageDealt, 1f, "they strike weaker");

        // A Cowed creature's blows: its own statuses scale what it deals.
        var foe = new GameObject("A test's cowed creature");
        made.Add(foe);
        var combatant = foe.AddComponent<Combatant>();
        Damageable body = foe.AddComponent<Damageable>();
        float before = DamageCalculator.DealtScale(combatant, TargetCategory.Creature);
        StatusEffects.Apply(body, cry.FoesStatus, null);
        Assert.AreEqual(before * cry.FoesStatus.DamageDealt, DamageCalculator.DealtScale(combatant, TargetCategory.Creature), 1e-4f);

        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/BackThroughTheDoor.asset");
        CollectionAssert.Contains(quest.RewardSkills, cry, "the portal's quest teaches it");
        Assert.AreEqual(ObjectiveType.FallInRaid, quest.StartWhen.Type, "it begins with a fall");
        var fall = quest.StartWhen;
        Assert.AreEqual(1, fall.Progress(new GameEvent(GameEventKind.RaidFinished, null, 1, null, RaidOutcome.Died, 300f)), "a fall counts");
        Assert.AreEqual(0, fall.Progress(new GameEvent(GameEventKind.RaidFinished, null, 1, null, RaidOutcome.Extracted, 300f)), "getting out doesn't");
    }

    [Test]
    public void InCoop_AFriendSeesTheHostsStatuses_AndTheGroundLeft()
    {
        var rend = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Rend.asset");
        var copy = new GameObject("A test's copy of the host's creature");
        made.Add(copy);
        Damageable body = copy.AddComponent<Damageable>();
        StatusEffectDefinition endedSeen = null;
        System.Action<Damageable, StatusEffectDefinition> ended = (who, what) => endedSeen = what;
        StatusEffects.EndedOn += ended;
        try
        {
            StatusEffects.Mirror(body, rend, 3f);
            var effects = copy.GetComponent<StatusEffects>();
            Assert.IsTrue(effects.Has(rend), "the host says it bleeds: the copy shows it");
            Assert.AreEqual(3f, effects.Current[0].SecondsLeft, 0.1f, "for as long as the host's has left");
            StatusEffects.Mirror(body, rend, 0f);
            Assert.IsFalse(effects.Has(rend), "the host says it ended");
            Assert.AreSame(rend, endedSeen, "and its end is heard (the look goes, and a host would tell its friends)");
        }
        finally
        {
            StatusEffects.EndedOn -= ended;
        }

        var points = new List<Vector3>();
        for (int i = 0; i < 10; i++)
        {
            points.Add(new Vector3(i, 0.5f * i, -i));
        }

        GroundLeftMessageData left = GroundLeftMessageData.Of(default, "AshladsEmber", points);
        Assert.AreEqual(8, left.Count, "at most eight patches a cast");
        for (int i = 0; i < left.Count; i++)
        {
            Assert.AreEqual(points[i], left.Point(i), "each where it was left");
        }

        Assert.AreEqual("AshladsEmber", left.Skill.ToString());
    }

    [Test]
    public void SteelAndBeastSkills_AreBlows_FrostEmberAndWyrdAreSpells()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data/Skills" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            bool blow = skill.School == SkillSchool.Steel || skill.School == SkillSchool.Beast;
            Assert.AreEqual(blow, skill.IsPhysical, $"{skill.name} ({skill.School})");
        }

        Assert.IsTrue(AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/TrollsbaneWrath.asset").IsPhysical,
            "the troll-slaying hammer is a blow: it shatters the frozen and trains Melee");
        Assert.IsFalse(AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/FrostRune.asset").IsPhysical);
        string card = SkillText.Describe(AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/TrollsbaneWrath.asset"), -1);
        StringAssert.Contains("takes 15% more", card, "its card says what Sundered does");
    }

    [Test]
    public void FireOnIce_BurstsInSteam_AndTakesBoth()
    {
        StatusEffectDefinition Load(string n) => AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>($"Assets/_Project/Data/Combat/Status/{n}.asset");
        var frozen = Load("Frozen");
        var burning = Load("Burning");
        Assert.IsTrue(System.Linq.Enumerable.Any(frozen.Reactions, r => r.with == burning), "ice knows what fire does to it");

        var target = new GameObject("A test's frozen creature");
        made.Add(target);
        var combatant = target.AddComponent<Combatant>();
        combatant.SetFaction(Faction.Enemy);
        combatant.Stats.SetBase(StatType.MaxHealth, 500f);
        Damageable body = target.AddComponent<Damageable>();
        var data = new SerializedObject(body);
        data.FindProperty("destroyOnDeath").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(Damageable).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(body, null);
        var effects = typeof(StatusEffects).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        StatusEffects shown = StatusEffects.Apply(body, frozen, null);
        effects?.Invoke(shown, null); // its bearer, as in play
        float before = body.CurrentHealth;
        StatusEffects.Apply(body, burning, null);
        Assert.IsFalse(shown.Has(frozen), "the ice is gone");
        Assert.IsFalse(shown.Has(burning), "and the fire with it");
        Assert.Less(body.CurrentHealth, before, "in a burst of steam");
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
